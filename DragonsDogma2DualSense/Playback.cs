namespace DragonsDogma2DualSense;

// Selects the actual container route. Random choices are independent of Wwise's
// private RNG; RTPC/state curves are retained in metadata, not guessed at runtime.
sealed class Playback(SoundCatalog catalog, PreparedWaves.Index index, Mixer mixer, float damageGain = 1)
{
    readonly Random random=new();
    readonly Dictionary<uint,int> positions=[];
    readonly Dictionary<uint,int> previous=[];
    readonly Queue<object> recent = new();
    uint currentEvent;
    long currentFrame;
    string currentObject = "";
    long currentLifetime;
    float currentLevel=1;
    internal static float ValidateLevel(float level)
    {
        if(!float.IsFinite(level)||level<0||level>1.5f)throw new InvalidDataException("Invalid playback level");
        return level;
    }
    public object[] Recent => recent.ToArray();
    public int Played {get;private set;}
    public void Post(uint eventId,IReadOnlyDictionary<uint,uint> switches,IReadOnlyDictionary<uint,uint> states,long frame=0,string objectId="",long lifetime=0,float level=1)
    {
        ValidateLevel(level);
        if(level==0)return;
        if(!catalog.Events.TryGetValue(eventId,out var actions))return;
        currentEvent=eventId;currentFrame=frame;currentObject=objectId;currentLifetime=lifetime;currentLevel=level;
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
                    PlaySound(id,n,delay);return;
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
    void PlaySound(uint id,SoundNode node,int delay)
    {
        if(!index.Sounds.TryGetValue(id,out var entries)||entries.Length==0)return;
        var entry=entries[random.Next(entries.Length)];
        if(entry.Omitted)return;
        // This bank is confirmed in live player damage playback. Scale only its
        // existing source-derived signal; ownership and omitted choices stay intact.
        float level=currentLevel*(node.Bank=="meat_damage_m.sbnk.1.x64"?damageGain:1);
        if(!mixer.Play(PreparedWaves.Key(entry),delay,level:level,group:currentLifetime,emitter:currentObject,
            loops:node.LoopCount,loopStart:(int)Math.Round(entry.Playback.DelaySeconds*48000)))return;
        Played++;
        if(recent.Count==64)recent.Dequeue();
        recent.Enqueue(new{event_id=currentEvent,frame=currentFrame,object_id=currentObject,lifetime=currentLifetime,
            sound=id,draw=entry.Draw,hash=entry.Hash,level,delay_frames=delay,length_frames=entry.Length/2,loop_count=node.LoopCount});
    }
    void Stop(uint id,HashSet<uint> seen)
    {
        if(!seen.Add(id)||!catalog.Nodes.TryGetValue(id,out var n))return;
        if(index.Sounds.TryGetValue(id,out var entries))foreach(var e in entries)mixer.FadeOut(PreparedWaves.Key(e),currentObject);
        foreach(uint child in n.Children)Stop(child,seen);
    }
}
