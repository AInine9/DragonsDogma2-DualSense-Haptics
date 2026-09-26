using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DragonsDogma2DualSense;
static class PreparedWaves
{
    public record Entry(string Hash,int Length,uint Sound,int Draw,SoundPlayback Playback,bool Omitted=false);
    public record Index(string CatalogHash,string Renderer,Dictionary<uint,Entry[]> Sounds,string Filter="");
    public const string Filter="single-middle-peak001-rms0001-v1";
    public const string Renderer="dd2-source-continuous-v4";
    public static string WavePath(string hash)
    {
        if(hash.Length!=64 || !hash.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid waveform hash");
        return Files.Data("waves/"+hash+".wav");
    }
    public static Index Load()
    {
        var i=JsonSerializer.Deserialize<Index>(File.ReadAllText(Files.Data("waves/index.json")),Configuration.Json)!;
        if(i.CatalogHash!=Files.Sha(Files.Bundled("catalog.json")) || i.Renderer!=Renderer || i.Filter!=Filter)throw new InvalidDataException("Prepared assets are outdated; run Setup.cmd");
        return i;
    }
    public static void Prepare(SoundCatalog catalog,string extracted,string decoder)
    {
        Directory.CreateDirectory(Files.Data("waves"));Directory.CreateDirectory(Files.Data("sources"));
        var index=new Dictionary<uint,Entry[]>();var cache=new Dictionary<string,float[]>();
        string scratch=Files.Data("decode-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(scratch);
        try
        {
            foreach(var group in catalog.Nodes.Where(p=>p.Value.Kind=="Sound").GroupBy(p=>p.Value.Bank))
            {
                string resource="natives/stm/sound/wwise/"+group.Key,path=Path.Combine(extracted,resource);
                if(Files.Sha(path)!=catalog.Banks[resource])throw new InvalidDataException("Unsupported bank: "+group.Key);
                var chunks=Setup.Chunks(File.ReadAllBytes(path));var media=new Dictionary<uint,(int Offset,int Size)>();
                byte[] didx=chunks["DIDX"],data=chunks["DATA"];
                if(didx.Length%12!=0)throw new InvalidDataException("Invalid DIDX");
                for(int p=0;p<didx.Length;p+=12)media.Add(BitConverter.ToUInt32(didx,p),(checked((int)BitConverter.ToUInt32(didx,p+4)),checked((int)BitConverter.ToUInt32(didx,p+8))));
                foreach(var (id,n) in group)
                {
                    if(!cache.TryGetValue(n.MediaSha256,out var source))
                    {
                        var (offset,size)=media[n.Media];if(offset<0||size<0||offset>data.Length-size)throw new InvalidDataException("Invalid media range");
                        byte[] wem=data[offset..(offset+size)];
                        if(Convert.ToHexStringLower(SHA256.HashData(wem))!=n.MediaSha256)throw new InvalidDataException("Media hash mismatch");
                        string input=Path.Combine(scratch,"source.wem"),output=Path.Combine(scratch,"source.wav");
                        File.WriteAllBytes(input,wem);Setup.Exec(decoder,scratch,"-i","-o",output,input);
                        source=SoundHaptics.ReadSource(output,true);
                        if(source.Length>48000*2*60)throw new InvalidDataException("Unexpected sound duration");
                        File.Copy(output,Files.Data("sources/"+n.MediaSha256+".wav"),true);
                        cache[n.MediaSha256]=source;
                    }
                    var variant=catalog.Variant(id);int draws=SoundHaptics.DrawCount(variant);var choices=new List<Entry>();
                    foreach(int draw in new[]{SoundHaptics.RepresentativeDraw(variant)})
                    {
                        var playback=SoundHaptics.Realize(variant,draw,draws);
                        if(Math.Abs(playback.PitchCents)>4800||playback.DelaySeconds>30)throw new InvalidDataException("Unbounded playback metadata");
                        var wave=SoundHaptics.Render(source,n.Family,playback);
                        if(wave.All(v=>Math.Abs(v)<.00001f))continue;
                        string hash=Convert.ToHexStringLower(SHA256.HashData(MemoryMarshal.AsBytes(wave.AsSpan())));
                        // Keep the selected sound silent; never replace it with a stronger variant.
                        if(SoundHaptics.IsNegligible(wave)){choices.Add(new(hash,wave.Length,id,draw,playback,true));continue;}
                        WriteWave(WavePath(hash),wave);choices.Add(new(hash,wave.Length,id,draw,playback));
                    }
                    index[id]=choices.ToArray();
                }
                cache.Clear();Console.WriteLine("Prepared "+group.Key);
            }
            var result=new Index(Files.Sha(Files.Bundled("catalog.json")),Renderer,index,Filter);
            File.WriteAllText(Files.Data("waves/index.json.tmp"),JsonSerializer.Serialize(result,Configuration.Json));
            File.Move(Files.Data("waves/index.json.tmp"),Files.Data("waves/index.json"),true);
            Console.WriteLine($"Prepared {index.Count} sound nodes, {index.Values.Sum(a=>a.Length)} waveform variants.");
        }
        finally{Directory.Delete(scratch,true);}
    }
    public static SampleStore Open(Index index)
    {
        var store=new SampleStore();
        foreach(var e in index.Sounds.Values.SelectMany(x=>x).Where(e=>!e.Omitted))store.RegisterWave(Key(e),WavePath(e.Hash),e.Length,e.Hash);
        return store;
    }
    public static string Key(Entry e)=>e.Sound+":"+e.Draw;
    public static void Verify()
    {
        var index=Load();var waves=index.Sounds.Values.SelectMany(x=>x).Where(e=>!e.Omitted).DistinctBy(e=>e.Hash).ToArray();
        foreach(var e in waves)ReadWave(WavePath(e.Hash),e.Length,e.Hash);
        Console.WriteLine($"PASS: {waves.Length} distinct float32 WAV files verified; {index.Sounds.Count} sound nodes.");
    }
    internal static void WriteWave(string path,float[] data)
    {
        if(data.Length%2!=0||data.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Invalid samples");
        using var w=new BinaryWriter(File.Create(path));
        w.Write("RIFF"u8);w.Write(48+data.Length*4);w.Write("WAVEfmt "u8);w.Write(16);
        w.Write((ushort)3);w.Write((ushort)2);w.Write(48000);w.Write(384000);w.Write((ushort)8);w.Write((ushort)32);
        w.Write("fact"u8);w.Write(4);w.Write(data.Length/2);w.Write("data"u8);w.Write(data.Length*4);w.Write(MemoryMarshal.AsBytes(data.AsSpan()));
    }
    internal static float[] ReadWave(string path,int length,string hash)
    {
        if(length<0||length>48000*2*60||length%2!=0)throw new InvalidDataException("Invalid sample count");
        using var stream=File.OpenRead(path);using var r=new BinaryReader(stream);
        bool Tag(string s)=>Encoding.ASCII.GetString(r.ReadBytes(4))==s;
        if(stream.Length!=56L+length*4L||!Tag("RIFF")||r.ReadInt32()!=48L+length*4L||!Tag("WAVE")||!Tag("fmt ")||r.ReadInt32()!=16||r.ReadUInt16()!=3||r.ReadUInt16()!=2||r.ReadInt32()!=48000||r.ReadInt32()!=384000||r.ReadUInt16()!=8||r.ReadUInt16()!=32||!Tag("fact")||r.ReadInt32()!=4||r.ReadInt32()!=length/2||!Tag("data")||r.ReadInt32()!=length*4L)throw new InvalidDataException("Invalid waveform");
        var data=new float[length];stream.ReadExactly(MemoryMarshal.AsBytes(data.AsSpan()));
        if(data.Any(v=>!float.IsFinite(v))||Convert.ToHexStringLower(SHA256.HashData(MemoryMarshal.AsBytes(data.AsSpan())))!=hash)throw new InvalidDataException("Waveform checksum mismatch");return data;
    }
}
