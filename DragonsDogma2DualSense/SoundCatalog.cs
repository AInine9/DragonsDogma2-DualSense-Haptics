using System.Text.Json;

namespace DragonsDogma2DualSense;
record SoundPlayback(double PitchCents = 0, double DelaySeconds = 0, double VolumeDb = 0);
record SoundVariant(uint Media, uint Path, SoundPlayback Fixed, SoundPlayback RandomMin, SoundPlayback RandomMax, bool DynamicRtpc, bool DynamicState);
sealed class SoundNode
{
    public string Kind { get; set; } = "";
    public uint Parent { get; set; }
    public uint[] Children { get; set; } = [];
    public double[] Fixed { get; set; } = [0,0,0];
    public double[] Low { get; set; } = [0,0,0];
    public double[] High { get; set; } = [0,0,0];
    public uint Group { get; set; }
    public uint Default { get; set; }
    public bool StateGroup { get; set; }
    public Dictionary<uint,uint[]> Branches { get; set; } = [];
    public bool DynamicRtpc { get; set; }
    public bool DynamicState { get; set; }
    public bool Sequence { get; set; }
    public bool Continuous { get; set; }
    public bool Loop { get; set; }
    public int LoopCount { get; set; } = 1;
    public uint Media { get; set; }
    public string Bank { get; set; } = "";
    public string MediaSha256 { get; set; } = "";
    public string Family { get; set; } = "impact";
}
sealed class SoundCatalog
{
    public int Format { get; set; }
    public Dictionary<string,string> Banks { get; set; } = [];
    public Dictionary<uint,SoundNode> Nodes { get; set; } = [];
    public Dictionary<uint,uint[]> Events { get; set; } = [];
    public static SoundCatalog Load() => JsonSerializer.Deserialize<SoundCatalog>(File.ReadAllText(Files.Bundled("catalog.json")), Configuration.Json) ?? throw new InvalidDataException("Missing catalog");
    public SoundVariant Variant(uint id)
    {
        double[] fix=[0,0,0],low=[0,0,0],high=[0,0,0];bool rtpc=false,state=false;
        var seen=new HashSet<uint>();uint current=id;
        while(current!=0 && Nodes.TryGetValue(current,out var n))
        {
            if(!seen.Add(current))throw new InvalidDataException("Cyclic sound ancestry");
            for(int i=0;i<3;i++){fix[i]+=n.Fixed[i];low[i]+=n.Low[i];high[i]+=n.High[i];}
            rtpc|=n.DynamicRtpc;state|=n.DynamicState;current=n.Parent;
        }
        return new(Nodes[id].Media,id,new(fix[0],fix[1],fix[2]),new(low[0],low[1],low[2]),new(high[0],high[1],high[2]),rtpc,state);
    }
}
