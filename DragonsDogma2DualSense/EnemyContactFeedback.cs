namespace DragonsDogma2DualSense;

// Damage-call provenance is required: the same flesh/condition media also play
// for player damage or weapon enchant swishes. Missing old-bridge tags bypass.
struct EnemyContactFeedback
{
    static readonly HashSet<uint> elementalHits=ElementalRoutes.Triggers.Values.Select(r=>r.Event).ToHashSet();
    internal static bool Applies(bool outgoingHit,uint eventId,string bank,int loops)
        => outgoingHit && loops==1 && ((eventId==1701720996 && bank is "meat_damage_m.sbnk.1.x64" or "wood_damage_m.sbnk.1.x64")
            || (bank=="condition_m.sbnk.1.x64" && elementalHits.Contains(eventId)));
    static readonly double attack=1-Math.Exp(-1/(48000*.0004));
    static readonly double release=1-Math.Exp(-1/(48000*.003));
    static readonly double body=1-Math.Exp(-1/(48000*.025));
    static readonly double gainFollow=1-Math.Exp(-1/(48000*.0007));
    static readonly double split=1-Math.Exp(-2*Math.PI*85/48000);
    double fast,slow,reduction,lowLeft,lowRight;
    public void Process(float left,float right,out float l,out float r)
    {
        double amplitude=Math.Max(Math.Abs(left),Math.Abs(right));
        fast+=(amplitude-fast)*(amplitude>fast?attack:release);
        slow+=(amplitude-slow)*body;
        double rise=Math.Clamp((fast-slow)/Math.Max(.002,slow),0,1);
        // Keep recorded attacks relative to the bed; no synthetic impact, fixed
        // duration or replay. Run after element shaping to bound its long body.
        reduction+=(.65-.50*rise-reduction)*gainFollow;
        lowLeft=left==0?0:lowLeft+split*(left-lowLeft);
        lowRight=right==0?0:lowRight+split*(right-lowRight);
        l=(float)((left-.30*lowLeft)*(1-reduction));
        r=(float)((right-.30*lowRight)*(1-reduction));
    }
}

