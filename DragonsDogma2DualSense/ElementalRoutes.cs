namespace DragonsDogma2DualSense;

static class ElementalRoutes
{
    public sealed record Rule(uint Event,string Element);
    public static Dictionary<string,bool> WeaponEvents(SoundCatalog catalog)
    {
        var result=new Dictionary<string,bool>();
        foreach(var (id,actions) in catalog.Events)
        {
            var seen=new HashSet<uint>();var pending=new Stack<uint>(actions);
            bool weapon=false,other=false;
            while(pending.TryPop(out uint current))
            {
                if(!seen.Add(current)||!catalog.Nodes.TryGetValue(current,out var node))continue;
                if(node.Kind=="Sound")
                {
                    if(node.Family=="attack"&&node.Bank.StartsWith("wp",StringComparison.Ordinal))weapon=true;
                    else other=true;
                }
                if(node.Kind=="ActionStop")continue;
                foreach(uint child in node.Children)pending.Push(child);
                foreach(var branch in node.Branches.Values)foreach(uint child in branch)pending.Push(child);
            }
            if(weapon&&!other)result[id.ToString()]=true;
        }
        return result;
    }
    // Exact trigger/event bindings from condition_triggerinfolistdata.user.3.
    // Includes weapon and shell enchant sounds, not DAM / DAM_GUARD effects.
    public static readonly IReadOnlyDictionary<uint,Rule> Triggers=new Dictionary<uint,Rule>
    {
        [2922778045]=new(1309599876,"ice"), [810816778]=new(1309599877,"ice"), [842847457]=new(1309599898,"ice"),
        [352781976]=new(1719705574,"light"), [1130267539]=new(1719705575,"light"), [1560396212]=new(1719705593,"light"),
        [1294265285]=new(2049616197,"thunder"), [3365525406]=new(2049616197,"thunder"),
        [1470590729]=new(2049616218,"thunder"), [4101627901]=new(2049616218,"thunder"),
        [2301102156]=new(2049616219,"thunder"), [3111810226]=new(2049616219,"thunder"),
        [3156186623]=new(2530456364,"ice"), [1627233820]=new(2530456370,"ice"), [4120732984]=new(2530456371,"ice"),
        [4259561765]=new(2666856015,"light"), [3484543192]=new(2666856016,"light"), [2085475951]=new(2666856017,"light"),
        [2081079854]=new(4164665954,"fire"), [2438713058]=new(4164665955,"fire"), [749804277]=new(4164665981,"fire"),
        [1903308345]=new(736341992,"fire"), [328318990]=new(736341993,"fire"), [955464211]=new(736342007,"fire")
    };
}
