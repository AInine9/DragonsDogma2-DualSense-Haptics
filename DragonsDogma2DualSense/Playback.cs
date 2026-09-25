namespace DragonsDogma2DualSense;

// Selects the actual container route. Random choices are independent of Wwise's
// private RNG; RTPC/state curves are retained in metadata, not guessed at runtime.
sealed class Playback(SoundCatalog catalog, PreparedWaves.Index index, Mixer mixer)
{
    readonly Random random=new();
    readonly Dictionary<uint,int> positions=[];
    readonly Dictionary<uint,int> previous=[];
    readonly Queue<object> recent = new();
    uint currentEvent;
    long currentFrame;
    string currentObject = "";
    long currentLifetime;
    public object[] Recent => recent.ToArray();
    public int Played {get;private set;}
    public void Post(uint eventId,IReadOnlyDictionary<uint,uint> switches,IReadOnlyDictionary<uint,uint> states,long frame=0,string objectId="",long lifetime=0)
    {
        if(!catalog.Events.TryGetValue(eventId,out var actions))return;
        currentEvent=eventId;currentFrame=frame;currentObject=objectId;currentLifetime=lifetime;
        int budget=256;
        foreach(uint action in actions)Visit(action,0,switches,states,new HashSet<uint>(),ref budget);
    }
    void Visit(uint id,int delay,IReadOnlyDictionary<uint,uint> switches,IReadOnlyDictionary<uint,uint> states,HashSet<uint> path,ref int budget)
    {
        if(--budget<0||!path.Add(id)||!catalog.Nodes.TryGetValue(id,out var n))return;
        try
        {
            switch(n.Kind)
            {
                case "Silence":
                case "Unavailable": return;
                case "Sound":
                    if(index.Sounds.TryGetValue(id,out var entries)&&entries.Length>0)
                    {
                        var e=entries[random.Next(entries.Length)];if(mixer.Play(PreparedWaves.Key(e),delay,
                            group:currentLifetime,emitter:currentObject,loops:n.LoopCount,
                            loopStart:(int)Math.Round(e.Playback.DelaySeconds*48000)))
                        {
                            Played++;
                            if(recent.Count==64)recent.Dequeue();
                            recent.Enqueue(new{event_id=currentEvent,frame=currentFrame,object_id=currentObject,lifetime=currentLifetime,sound=id,draw=e.Draw,hash=e.Hash,delay_frames=delay,length_frames=e.Length/2,loop_count=n.LoopCount});
                        }
                    }return;
                case "ActionStop":
                    foreach(uint child in n.Children)Stop(child,new HashSet<uint>());return;
                case "ActionPlay":
                    delay+=checked((int)(Math.Clamp(n.Fixed[1]+n.Low[1]+random.NextDouble()*(n.High[1]-n.Low[1]),0,30)*48000));break;
                case "SwitchCntr":
                    var values=n.StateGroup?states:switches;
                    uint choice=values.TryGetValue(n.Group,out uint v)?v:n.Default;
                    if(!n.Branches.TryGetValue(choice,out var selected))n.Branches.TryGetValue(n.Default,out selected);
                    foreach(uint child in selected??[])Visit(child,delay,switches,states,path,ref budget);return;
                case "RanSeqCntr":
                    if(n.Children.Length==0)return;
                    int pos;
                    if(n.Sequence){positions.TryGetValue(id,out pos);positions[id]=(pos+1)%n.Children.Length;}
                    else{pos=random.Next(n.Children.Length);if(n.Children.Length>1&&previous.TryGetValue(id,out int old)&&old==pos)pos=(pos+1)%n.Children.Length;previous[id]=pos;}
                    Visit(n.Children[pos],delay,switches,states,path,ref budget);return;
            }
            foreach(uint child in n.Children)Visit(child,delay,switches,states,path,ref budget);
        }
        finally{path.Remove(id);}
    }
    void Stop(uint id,HashSet<uint> seen)
    {
        if(!seen.Add(id)||!catalog.Nodes.TryGetValue(id,out var n))return;
        if(index.Sounds.TryGetValue(id,out var entries))foreach(var e in entries)mixer.FadeOut(PreparedWaves.Key(e),currentObject);
        foreach(uint child in n.Children)Stop(child,seen);
    }
}
