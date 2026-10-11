namespace DragonsDogma2DualSense;

enum AttackElement { None, Fire, Ice, Thunder, Light, Dark }

// Color the existing sound-derived PCM; never generate a free-running tone or
// extend an event. Element identity comes from the attack, not the target.
struct ElementalFeedback
{
    double lowLeft, lowRight;
    double thunderFast, thunderSlow;
    double auraFast, auraBody;
    static readonly double lightCoefficient=1-Math.Exp(-2*Math.PI*160/48000);
    static readonly double darkCoefficient=1-Math.Exp(-2*Math.PI*55/48000);
    static readonly double auraFollow=1-Math.Exp(-1/(48000*.003));
    static readonly double auraSwell=1-Math.Exp(-1/(48000*.035));
    static readonly double lowCoefficient=1-Math.Exp(-2*Math.PI*95/48000);
    static readonly double iceCoefficient=1-Math.Exp(-2*Math.PI*120/48000);
    static readonly double thunderAttack=1-Math.Exp(-1/(48000*.0015));
    static readonly double thunderRelease=1-Math.Exp(-1/(48000*.004));
    static readonly double thunderBody=1-Math.Exp(-1/(48000*.025));
    public static AttackElement Parse(string? name) => name switch
    {
        null or "" or "none" => AttackElement.None,
        "fire" => AttackElement.Fire,
        "ice" => AttackElement.Ice,
        "thunder" => AttackElement.Thunder,
        "light" => AttackElement.Light,
        "dark" => AttackElement.Dark,
        _ => throw new InvalidDataException("Unknown attack element")
    };
    public void Process(AttackElement element,float left,float right,int frame,int frames,out float l,out float r)
    {
        if(element==AttackElement.None){l=left;r=right;return;}
        // Ice retains less low body and brings forward the recorded fine texture.
        double coefficient=element switch { AttackElement.Ice=>iceCoefficient, AttackElement.Light=>lightCoefficient, AttackElement.Dark=>darkCoefficient, _=>lowCoefficient };
        lowLeft+=coefficient*(left-lowLeft);lowRight+=coefficient*(right-lowRight);
        // Blend the original waveform with its low/high bands. Each profile
        // retains the source envelope and stereo relationship. These are artistic
        // starting values, subject to controller comparison, not calibrated feel.
        (double low,double high)=element switch
        {
            AttackElement.Fire => (1.18,.42),
            AttackElement.Ice => (.20,1.50),
            AttackElement.Thunder => (.45,1.70),
            AttackElement.Light => (.55,1.05),
            AttackElement.Dark => (1.45,.08),
            _ => (1,1)
        };
        double emphasis=1;
        if(element is AttackElement.Light or AttackElement.Dark)
        {
            // Round the source's onset into a swell, without inserting a rhythm.
            // Dark favors sustained low weight; light keeps a gentler bright edge.
            double amplitude=Math.Max(Math.Abs(left),Math.Abs(right));
            auraFast+=(amplitude-auraFast)*auraFollow;
            auraBody+=(amplitude-auraBody)*auraSwell;
            double sustain=Math.Clamp(auraBody/Math.Max(.001,auraFast),0,1);
            emphasis=element==AttackElement.Dark?.35+.85*sustain:.70+.30*sustain;
        }
        if(element==AttackElement.Thunder)
        {
            // Follow recorded amplitude rises rather than inserting a periodic
            // "electric" pulse. Reduce the sustained bed between those rises so
            // the source's crackles can stand apart from the weapon's body.
            double amplitude=Math.Max(Math.Abs(left),Math.Abs(right));
            thunderFast+=(amplitude-thunderFast)*(amplitude>thunderFast?thunderAttack:thunderRelease);
            thunderSlow+=(amplitude-thunderSlow)*thunderBody;
            double rise=Math.Clamp((thunderFast-thunderSlow)/Math.Max(.015,thunderSlow)*2,0,1);
            emphasis=.55+1.10*rise;
        }
        double edge=Math.Min(1,Math.Min(frame/72.0,(frames-1-frame)/480.0));
        // Exact source silence stays silent; a filter state must not turn an
        // authored gap into a new rumble or leave a tail beyond event lifetime.
        if(left==0&&right==0){lowLeft=lowRight=0;auraFast=auraBody=0;l=r=0;return;}
        l=(float)((lowLeft*low+(left-lowLeft)*high)*emphasis*edge);
        r=(float)((lowRight*low+(right-lowRight)*high)*emphasis*edge);
    }
}
