using System.Text.Json.Nodes;

namespace DragonsDogma2DualSense;

sealed class Dd2Inbox
{
    public string Session {get;private set;}="";
    public long Ack {get;private set;}
    public long Snapshot {get;private set;}=-1;
    public double Last {get;private set;}
    public bool Ready {get;private set;}
    public bool Player {get;private set;}
    public bool Suppressed {get;private set;}
    public IReadOnlyDictionary<long,bool> Lifetimes {get;private set;}=new Dictionary<long,bool>();
    public JsonNode[] Accept(JsonNode state,double now)
    {
        if(state["version"]?.GetValue<int>()!=2)throw new InvalidDataException("Unsupported DD2 bridge protocol");
        string session=state["session"]!.GetValue<string>();long snapshot=state["seq"]!.GetValue<long>(),frame=state["frame"]!.GetValue<long>();
        if(session.Length==0||snapshot<0||frame<0)throw new InvalidDataException("Invalid bridge envelope");
        long oldAck=session==Session?Ack:0;
        if(session==Session&&snapshot<=Snapshot)return [];
        var rows=state["events"]?.AsArray().Select(x=>x??throw new InvalidDataException("Null event")).ToArray()??[];
        if(rows.Length>128)throw new InvalidDataException("Event queue too large");
        var accepted=new List<JsonNode>();long next=oldAck;
        foreach(var row in rows)
        {
            long seq=row["seq"]!.GetValue<long>(),at=row["frame"]!.GetValue<long>();
            _=row["id"]!.GetValue<uint>();
            if(seq<=0||at<0)throw new InvalidDataException("Invalid event");
            if(seq>oldAck&&frame-at is >=0 and <=8)accepted.Add(row);
            next=Math.Max(next,seq);
        }
        var lifetimes=new Dictionary<long,bool>();
        var liveRows=state["lifetimes"]?.AsArray();
        if(liveRows?.Count>128)throw new InvalidDataException("Lifetime snapshot too large");
        foreach(var row in liveRows??new JsonArray())
        {
            long token=row?["token"]?.GetValue<long>()??0;
            if(token<=0||!lifetimes.TryAdd(token,row?["confirmed"]?.GetValue<bool>()==true))throw new InvalidDataException("Invalid lifetime token");
        }
        bool ready=state["enabled"]?.GetValue<bool>()==true&&state["hooks_ready"]?.GetValue<bool>()==true;
        bool player=state["player_ready"]?.GetValue<bool>()==true,suppressed=state["suppressed"]?.GetValue<bool>()==true;
        Session=session;Snapshot=snapshot;Ack=next;Last=now;Ready=ready;Player=player;Suppressed=suppressed;Lifetimes=lifetimes;
        return accepted.GroupBy(x=>x["seq"]!.GetValue<long>()).Select(g=>g.First()).ToArray();
    }
}
static class Bridge
{
    public const string MutexName=@"Local\DragonsDogma2DualSenseBridge";
    static IReadOnlyDictionary<uint,uint> Values(JsonNode? node)=>node is JsonObject o?o.ToDictionary(x=>uint.Parse(x.Key),x=>x.Value!.GetValue<uint>()):new Dictionary<uint,uint>();
    internal static void PostRow(Playback playback, JsonNode row) => playback.Post(
        row["id"]!.GetValue<uint>(), Values(row["switches"]), Values(row["states"]),
        row["frame"]!.GetValue<long>(), row["object"]?.ToString() ?? "",row["lifetime"]?.GetValue<long>()??0);
    public static void Run()
    {
        using var mutex=new Mutex(false,MutexName);
        bool owned;try{owned=mutex.WaitOne(0);}catch(AbandonedMutexException){owned=true;}
        if(!owned)throw new InvalidOperationException("The DD2 companion is already running");
        try{RunOwned();}finally{mutex.ReleaseMutex();}
    }
    static void RunOwned()
    {
        var config=Configuration.Read();
        if(!File.Exists(Path.Combine(config.Game,"DD2.exe")))throw new InvalidDataException("Run Setup.cmd first");
        string input=Path.Combine(config.Game,"reframework/data/dd2_dualsense_state.json"),control=Path.Combine(config.Game,"reframework/data/dd2_dualsense_control.json");
        var catalog=SoundCatalog.Load();var prepared=PreparedWaves.Load();var samples=PreparedWaves.Open(prepared);
        var mixer=new Mixer(samples,config.Gain);var playback=new Playback(catalog,prepared,mixer,config.DamageGain);var inbox=new Dd2Inbox();var reader=new ChangedJsonReader();var lifetime=new GameLifetime();
        using var hid=new HidRecovery(()=>new Hid(),Files.Log);
        Audio? audio=null;BluetoothHaptics? bluetooth=null;string binding="",error="Waiting for controller";double nextDevice=0,nextControl=0,nextHealth=0,nextGame=0;
        bool output=false,active=false;long received=0;string priorSession="";
        using var console=new ConsoleLifetime();
        using var nativeLease=new NativeAudioLease();
        File.Delete(Files.Data("stop.request"));Files.Log("DD2 companion started; prepared game-sound WAV playback.");
        try
        {
            while(!console.Stopping&&!File.Exists(Files.Data("stop.request")))
            {
                double now=Files.Now;
                if(now>=nextGame){nextGame=now+.5;if(lifetime.ShouldExit(Files.GameRunning(),now))break;}
                try
                {
                    if(now>=nextDevice)
                    {
                        nextDevice=now+1;var devices=Hid.Find();string current=devices.Count==1?devices[0].Path:"";
                        if(current!=binding)
                        {
                            mixer.Suspend();audio?.Dispose();audio=null;bluetooth?.Dispose();bluetooth=null;hid.Rebind();binding=current;output=false;
                            Files.Log(current.Length==0?"Waiting for one controller":"Controller transport changed; opening output");
                        }
                        if(current.Length>0&&audio==null&&bluetooth==null)
                        {
                            if(devices[0].Transport==HidTransport.Bluetooth)
                            {
                                if(devices[0].ReportLength<BluetoothHapticsProtocol.ReportLength)throw new InvalidOperationException("Bluetooth PCM collection unavailable; connect by USB");
                                bluetooth=new BluetoothHaptics(mixer,hid,Files.Log);
                            }
                            else audio=new Audio(mixer);
                        }
                    }
                    if(now>=nextHealth)
                    {
                        nextHealth=now+.05;
                        audio?.CheckHealth();
                        output=binding.Length>0&&(audio!=null||bluetooth!=null)&&hid.TrySend(Protocol.Report(audio:true),now);
                        if(bluetooth!=null)output &= bluetooth.Healthy;
                    }
                    if(File.Exists(input)&&reader.ReadChanged(input) is {} state)
                    {
                        var rows=inbox.Accept(state,now);
                        if(priorSession!=inbox.Session){mixer.Stop();priorSession=inbox.Session;}
                        active=output&&inbox.Ready&&inbox.Player&&inbox.Suppressed&&(!config.RequireFocus||Focus.IsGame());
                        if(active)
                        {
                            foreach(var row in rows){PostRow(playback,row);received++;}
                        }
                        mixer.SyncLifetimes(inbox.Lifetimes);
                    }
                    active=output&&inbox.Ready&&inbox.Player&&inbox.Suppressed&&now-inbox.Last<1&&(!config.RequireFocus||Focus.IsGame());
                    if(!inbox.Ready||!inbox.Player||now-inbox.Last>=1)mixer.Stop();
                    if(active)mixer.Resume();else mixer.Suspend();error=output?"":"Controller output unavailable";
                }
                catch(Exception e) when(e is IOException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception or System.Text.Json.JsonException)
                {
                    mixer.Suspend();output=false;active=false;
                    if(error!=e.Message){error=e.Message;Files.Log(error);}
                    audio?.Dispose();audio=null;bluetooth?.Dispose();bluetooth=null;nextDevice=Math.Max(nextDevice,now+1);
                }
                if(now>=nextControl)
                {
                    nextControl=now+.25;
                    bool ready=output&&inbox.Ready&&now-inbox.Last<1&&(!config.RequireFocus||Focus.IsGame());
                    if(!Files.Atomic(control,new{version=2,session=inbox.Session,timestamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds(),ready,ack=inbox.Ack,error,received,played=playback.Played}))
                    {mixer.Stop();output=false;ready=false;error="Cannot write bridge control";}
                    nativeLease.Renew(ready&&audio!=null);
                    Files.Atomic(Files.Data("status.json"),new{running=true,ready,active,session=inbox.Session,error,received,played=playback.Played,audio_underflows=audio?.Underflows??0,mixer=mixer.Diagnostics,recent_sounds=playback.Recent});
                }
                Thread.Sleep(4);
            }
        }
        finally
        {
            nativeLease.Renew(false);
            mixer.Stop();audio?.Dispose();bluetooth?.Dispose();hid.Release();
            Files.Atomic(control,new{version=2,session=inbox.Session,timestamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds(),ready=false,ack=inbox.Ack,error="Stopped"});
            Files.Atomic(Files.Data("status.json"),new{running=false,ready=false,active=false});Files.Log("DD2 companion stopped; native feedback released.");
        }
    }
}
