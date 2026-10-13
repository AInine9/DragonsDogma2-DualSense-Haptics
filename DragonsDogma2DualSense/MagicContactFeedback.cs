namespace DragonsDogma2DualSense;

// Exact magic HIT bindings; no inferred element or localized skill name.
// Their APPEAR/LP/END and other HIT routes are intentionally separate.
struct MagicContactFeedback
{
    internal static bool Applies(uint eventId,uint soundId,string bank,int loopCount)
        => bank=="mgc_skill_m.sbnk.1.x64" && loopCount==1 && (eventId,soundId) is
            (3470632968,571888909) or // COM_MAGIC_BARRIER_HIT
            (2234848889,818575703) or // SPELL_BREAK_HIT
            (763759700,376142847);   // GUARD_BIT_HIT

    static readonly double attack=1-Math.Exp(-1/(48000*.001));
    static readonly double release=1-Math.Exp(-1/(48000*.005));
    static readonly double body=1-Math.Exp(-1/(48000*.030));
    static readonly double gainFollow=1-Math.Exp(-1/(48000*.0005));
    double fast,slow,reduction;
    public void Process(float left,float right,out float l,out float r)
    {
        double amplitude=Math.Max(Math.Abs(left),Math.Abs(right));
        fast+=(amplitude-fast)*(amplitude>fast?attack:release);
        slow+=(amplitude-slow)*body;
        // Keep source rises, gently reduce the bed between them. Attenuation
        // only: no new impulse, clocked rhythm, sample shift or appended tail.
        double rise=Math.Clamp((fast-slow)/Math.Max(.002,slow),0,1);
        // Smooth gain changes for 0.5 ms to avoid sharpening a local sample
        // edge. This follows amplitude only; it adds no sample delay or tail.
        reduction+=(.30*(1-rise)-reduction)*gainFollow;
        float gain=(float)(1-reduction);
        l=left*gain;r=right*gain;
    }
}
