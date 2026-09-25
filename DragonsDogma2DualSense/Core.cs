using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DragonsDogma2DualSense;

static class Files
{
    public static string Root = AppContext.BaseDirectory;
    internal static bool EchoLogsToConsole { get; set; } = true;
    public static string At(string name) => Path.Combine(Root, name);
    public static string Data(string name) => At(Path.Combine("data", name));
    public static string Bundled(string name) => At(Path.Combine("bin", name));
    public static JsonNode Read(string path) => JsonNode.Parse(File.ReadAllText(path)) ?? throw new InvalidDataException(path);
    public static string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    public static void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    public static bool Atomic(string path, object value)
    {
        string temp = path + "." + Environment.ProcessId + ".tmp";
        for (int i = 0; i < 4; i++)
        {
            try { Save(temp, value); File.Move(temp, path, true); return true; }
            catch (IOException e) when ((e.HResult & 65535) is 5 or 32 or 33) { }
            catch (UnauthorizedAccessException) { }
            if (i < 3) Thread.Sleep(2 << i);
        }
        return false;
    }
    public static void Log(string message)
    {
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
        if (EchoLogsToConsole) Console.WriteLine(line);
        try { File.AppendAllText(Data("bridge.log"), line + Environment.NewLine); }
        catch (IOException) { } // A concurrent diagnostic process must not stop controller output.
    }
    public static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    public static bool? GameRunning()
    {
        try { var processes = Process.GetProcessesByName("DD2"); bool found = processes.Length > 0; foreach (var p in processes) p.Dispose(); return found; }
        catch { return null; }
    }
}

sealed class ChangedJsonReader
{
    (DateTime Time, long Length)? previous;
    public JsonNode? ReadChanged(string path)
    {
        try
        {
        var info = new FileInfo(path);
        var stamp = (info.LastWriteTimeUtc, info.Length);
        if (previous == stamp) return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var value = JsonNode.Parse(reader.ReadToEnd());
        if (value == null) return null;
        info.Refresh();
        if (stamp != (info.LastWriteTimeUtc, info.Length)) return null;
        previous = stamp; // A failed or partial read must remain retryable.
        return value;
        }
        // REFramework writes in place. Retry an incomplete/locked snapshot on
        // the next poll without tearing down the controller audio connection.
        catch (IOException e) when ((e.HResult & 65535) is 2 or 3 or 32 or 33) { return null; }
        catch (JsonException) { return null; }
    }
}

sealed class GameLifetime
{
    public bool Seen { get; private set; }
    double? absent;
    public bool ShouldExit(bool? running, double now)
    {
        if (running == true) { Seen = true; absent = null; }
        else if (running == false && Seen) { absent ??= now; return now - absent >= 2; }
        else if (running == null) absent = null;
        return false;
    }
}

static class Protocol
{
    public static readonly byte[] Off = [5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    public static byte[] Report(bool audio = true)
    {
        byte[] result = new byte[48]; result[0] = 2; result[1] = audio ? (byte)0x0c : (byte)0x0e;
        Off.CopyTo(result, 11); Off.CopyTo(result, 22); return result;
    }
}

sealed class Mixer(IReadOnlyDictionary<string, float[]> samples, float gain, bool limitOutput = true)
{
    readonly object gate = new();
    sealed class Voice(string id,float[] data,int pos,float level,long group,string emitter,int loops,int loopStart)
    {
        public string Id=id, Emitter=emitter;
        public float[] Data=data;
        public int Pos=pos, Loops=loops, LoopStart=loopStart;
        public float Level=level;
        public long Group=group;
        public bool Confirmed;
    }
    readonly List<Voice> voices = [];
    // A bounded ring retains a stolen voice's next 20 ms instead of cutting it
    // at an arbitrary sample. No allocation or file access on the audio thread.
    readonly float[] retiring = new float[960 * 2];
    int retirementFrame, retirementRemaining;
    long steals, limitedFrames;
    bool suspended;
    public object Diagnostics { get { lock (gate) return new { voices = voices.Count, voice_steals = steals, limited_frames = limitedFrames }; } }
    public bool Playing { get { lock (gate) return voices.Count > 0 || retirementRemaining > 0; } }
    public bool Play(string id, int delayFrames = 0, float level = 1, long group = 0, string emitter = "", int loops = 1, int loopStart = 0)
    {
        if (!samples.TryGetValue(id, out var data) || data.Length<2) return false;
        lock (gate)
        {
            // Repeated game events are real strikes; IPC already deduplicates events.
            // Allow their recorded tails to overlap, with bounded voice counts.
            if (voices.Count(v => v.Id == id) >= 4)
            {
                int victim=voices.FindIndex(v=>v.Id==id && v.Loops==1);
                if(victim<0)return false;
                Retire(victim,960,true);
            }
            if (voices.Count == 32)
            {
                int victim=voices.FindIndex(v=>v.Loops==1);
                if(victim<0)return false;
                Retire(victim,960,true);
            }
            voices.Add(new(id, data, -Math.Max(0, delayFrames) * 2, level, group, emitter,
                group==0?1:Math.Max(0,loops),Math.Clamp(loopStart*2,0,data.Length-2))); return true;
        }
    }
    public void Stop() { lock (gate) { voices.Clear(); Array.Clear(retiring); retirementRemaining = 0; } }
    public void Suspend()
    {
        lock(gate)
        {
            suspended=true;
            voices.RemoveAll(v=>v.Group==0 || v.Loops==1);
            Array.Clear(retiring);retirementRemaining=0;
        }
    }
    public void Resume() { lock(gate) suspended=false; }
    void Retire(int index, int fadeFrames, bool stolen)
    {
        var v = voices[index];
        voices.RemoveAt(index);
        if (stolen) steals++;
        if(suspended)return;
        if (v.Pos <= 0) return; // Unstarted voices have produced no samples yet.
        int frames = Math.Min(fadeFrames, (v.Data.Length - v.Pos) / 2);
        for (int f = 0; f < frames; f++)
        {
            float level = v.Level * (1 - f / (float)Math.Max(1, frames - 1));
            int p = ((retirementFrame + f) % 960) * 2;
            retiring[p] += v.Data[v.Pos + f * 2] * level;
            retiring[p + 1] += v.Data[v.Pos + f * 2 + 1] * level;
        }
        retirementRemaining = Math.Max(retirementRemaining, frames);
    }
    public void FadeOut(string id, string? emitter = null)
    {
        lock (gate)
        {
            for (int index = voices.Count - 1; index >= 0; index--)
            {
            if (voices[index].Id != id || (emitter!=null && voices[index].Emitter!=emitter)) continue;
            Retire(index, 384, false);
            }
        }
    }
    // A complete snapshot, rather than best-effort stop messages, makes dropped
    // IPC frames unable to strand an infinite voice. Only confirmed live events loop.
    public void SyncLifetimes(IReadOnlyDictionary<long,bool> live)
    {
        lock(gate) for(int i=voices.Count-1;i>=0;i--)
        {
            var v=voices[i];if(v.Group==0)continue;
            if(!live.TryGetValue(v.Group,out bool confirmed))Retire(i,384,false);
            else v.Confirmed=confirmed;
        }
    }
    public void Fill(float[] output, int frames)
    {
        Array.Clear(output);
        lock (gate)
        {
            if(suspended)return;
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                var v = voices[i];
                int at=0;
                if(v.Pos<0){int skip=Math.Min(frames,-v.Pos/2);v.Pos+=skip*2;at+=skip;}
                while(at<frames && v.Pos>=0)
                {
                    if(v.Pos>=v.Data.Length)
                    {
                        if(!v.Confirmed || v.Loops==1)break;
                        if(v.Loops>1)v.Loops--;
                        v.Pos=v.LoopStart;
                    }
                    int n=Math.Min(frames-at,(v.Data.Length-v.Pos)/2);
                    for(int f=0;f<n;f++){output[(at+f)*4+2]+=v.Data[v.Pos+f*2]*gain*v.Level;output[(at+f)*4+3]+=v.Data[v.Pos+f*2+1]*gain*v.Level;}
                    v.Pos+=n*2;at+=n;
                }
                if(v.Pos>=v.Data.Length && (!v.Confirmed || v.Loops==1))voices.RemoveAt(i);
            }
            for (int f = 0; f < frames; f++)
            {
                int p = retirementFrame * 2;
                output[f * 4 + 2] += retiring[p] * gain;
                output[f * 4 + 3] += retiring[p + 1] * gain;
                retiring[p] = retiring[p + 1] = 0;
                retirementFrame = (retirementFrame + 1) % 960;
                if (retirementRemaining > 0) retirementRemaining--;
                bool limited = false;
                for (int c = 2; c < 4; c++)
                {
                    int i = f * 4 + c;
                    float magnitude = Math.Abs(output[i]);
                    if (limitOutput && magnitude > .65f) { limited = true; output[i] = MathF.CopySign(.65f + .2f * (1 - MathF.Exp(-(magnitude - .65f) / .2f)), output[i]); }
                }
                if (limited) limitedFrames++;
            }
        }
    }
}
