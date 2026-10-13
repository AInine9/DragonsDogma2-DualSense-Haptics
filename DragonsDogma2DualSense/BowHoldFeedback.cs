namespace DragonsDogma2DualSense;

// FullBlast's two body-bearing preparation sounds. Preserve the initial draw
// and leave its separate high-frequency texture Sound untouched.
struct BowHoldFeedback
{
    static readonly double attack=1-Math.Exp(-1/(48000*.001));
    static readonly double release=1-Math.Exp(-1/(48000*.005));
    static readonly double body=1-Math.Exp(-1/(48000*.030));
    static readonly double gainFollow=1-Math.Exp(-1/(48000*.003));
    double fast,slow,reduction;
    int openingFrames;
    // File-based exclusion covers FullBlast preparation as well as release.
    // Keep this tested processor dormant; no route currently selects it.
    internal static bool Applies(uint eventId,uint soundId,string bank,int loops) => false;

    public void Process(float left,float right,out float l,out float r)
    {
        double amplitude=Math.Max(Math.Abs(left),Math.Abs(right));
        fast+=(amplitude-fast)*(amplitude>fast?attack:release);
        slow+=(amplitude-slow)*body;
        double rise=Math.Clamp((fast-slow)/Math.Max(.002,slow),0,1);
        reduction+=(.12*rise-reduction)*gainFollow;
        // A source-local entrance, not a game-state timer. Saturation keeps the
        // counter finite and avoids restarting this entrance on every PCM loop.
        float t=Math.Clamp((openingFrames-48000)/12000f,0,1);
        float hold=t*t*(3-2*t);
        if(openingFrames<60000)openingFrames++;
        float gain=(float)(1-reduction*hold);
        l=left*gain;r=right*gain;
    }
}
