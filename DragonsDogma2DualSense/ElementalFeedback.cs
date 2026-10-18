namespace DragonsDogma2DualSense;
enum AttackElement { None, Fire, Ice, Thunder, Light, Dark }


// Color the existing sound-derived PCM; never generate a free-running tone or
// extend an event. Element identity comes from the attack, not the target.
struct ElementalFeedback
{
    double lowLeft, lowRight;
    // Light isolates the source middle band; other elements retain two bands.
    double lightUpperLeft,lightUpperRight;
    static readonly double lightUpperCoefficient=1-Math.Exp(-2*Math.PI*200/48000);
    double thunderFast, thunderSlow;
    double fireFast, fireSlow;
    double auraFast, auraBody;
    int auraSilentFrames;
    static readonly double lightCoefficient=1-Math.Exp(-2*Math.PI*55/48000);
    static readonly double darkCoefficient=1-Math.Exp(-2*Math.PI*55/48000);
    static readonly double auraFollow=1-Math.Exp(-1/(48000*.003));
    static readonly double auraSwell=1-Math.Exp(-1/(48000*.035));
    static readonly double lowCoefficient=1-Math.Exp(-2*Math.PI*95/48000);
    static readonly double iceCoefficient=1-Math.Exp(-2*Math.PI*200/48000);
    static readonly double thunderAttack=1-Math.Exp(-1/(48000*.0015));
    static readonly double thunderRelease=1-Math.Exp(-1/(48000*.004));
    static readonly double thunderBody=1-Math.Exp(-1/(48000*.025));
    static readonly double fireAttack=1-Math.Exp(-1/(48000*.001));
    static readonly double fireRelease=1-Math.Exp(-1/(48000*.006));
    static readonly double fireBody=1-Math.Exp(-1/(48000*.030));
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
        // Ice favors finer recorded detail above the middle body. The stronger
        // upper blend offsets the higher split without adding a pulse or tail;
        // Thunder keeps its separate source-rise emphasis unchanged.
        double coefficient=element switch { AttackElement.Ice=>iceCoefficient, AttackElement.Light=>lightCoefficient, AttackElement.Dark=>darkCoefficient, _=>lowCoefficient };
        lowLeft+=coefficient*(left-lowLeft);lowRight+=coefficient*(right-lowRight);
        if(element==AttackElement.Light)
        {
            lightUpperLeft+=lightUpperCoefficient*(left-lightUpperLeft);
            lightUpperRight+=lightUpperCoefficient*(right-lightUpperRight);
        }
        // Blend the original waveform with its low/high bands. Each profile
        // retains the source envelope and stereo relationship. These are artistic
        // starting values, subject to controller comparison, not calibrated feel.
        (double low,double high)=element switch
        {
            AttackElement.Fire => (1.18,.42),
            AttackElement.Ice => (.24,2.0),
            AttackElement.Thunder => (.45,1.70),
            AttackElement.Light => (.56,.36),
            AttackElement.Dark => (1.45,.08),
            _ => (1,1)
        };
        double emphasis=1;
        if(element==AttackElement.Fire)
        {
            // Keep Fire's warm band weighting. Expose rises already present in
            // its source instead of increasing the high band toward Thunder.
            // One common bounded gain preserves the instantaneous stereo ratio;
            // no independent rhythm, carrier or event extension is introduced.
            double amplitude=Math.Max(Math.Abs(left),Math.Abs(right));
            fireFast+=(amplitude-fireFast)*(amplitude>fireFast?fireAttack:fireRelease);
            fireSlow+=(amplitude-fireSlow)*fireBody;
            double rise=Math.Clamp((fireFast-fireSlow)/Math.Max(.01,fireSlow),0,1);
            emphasis=.80+.40*rise;
        }
        if(element is AttackElement.Light or AttackElement.Dark)
        {
            // Round the source's onset into a swell, without inserting a rhythm.
            // Dark favors sustained low weight. The authored light enchant
            // impacts have smoother envelopes than ice; retain their middle
            // body and favor source sustain over the initial fine edge.
            double amplitude=Math.Max(Math.Abs(left),Math.Abs(right));
            auraFast+=(amplitude-auraFast)*auraFollow;
            auraBody+=(amplitude-auraBody)*auraSwell;
            // Quantized PCM can contain isolated zeros inside a sustained sound.
            // Keep the swell across those crossings; restart it only after a
            // contiguous 10 ms silent gap. This is a debounce, not added decay.
            if(amplitude==0)
            {
                if(auraSilentFrames<480)auraSilentFrames++;
                if(auraSilentFrames==480)auraFast=auraBody=0;
            }
            else auraSilentFrames=0;
            double sustain=Math.Clamp(auraBody/Math.Max(.001,auraFast),0,1);
            emphasis=element==AttackElement.Dark?.35+.85*sustain:.40+.60*sustain;
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
        if(left==0&&right==0){lowLeft=lowRight=lightUpperLeft=lightUpperRight=0;l=r=0;return;}
        // The common high-band blend already contains the middle band. Add
        // only its gain difference for Light, retaining the source's smooth swell
        // and separating its middle body from Ice's finer upper emphasis.
        l=(float)((lowLeft*low+(left-lowLeft)*high+(element==AttackElement.Light?(lightUpperLeft-lowLeft)*(1.28-.36):0))*emphasis*edge);
        r=(float)((lowRight*low+(right-lowRight)*high+(element==AttackElement.Light?(lightUpperRight-lowRight)*(1.28-.36):0))*emphasis*edge);
    }
}
