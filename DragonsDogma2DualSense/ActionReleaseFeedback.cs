namespace DragonsDogma2DualSense;

// Exact one-shot action routes. Follow recorded rises rather than adding an
// impulse or imposing animation timing on playback. Source PCM stays immutable.
struct ActionReleaseFeedback(int startFrame,float inverseFadeFrames,float tailReduction,
    double bodyReduction=.08,double riseContrast=.14,bool shortAttack=false,
    double lowBody=1)
{
    static readonly double attack=1-Math.Exp(-1/(48000*.001));
    static readonly double release=1-Math.Exp(-1/(48000*.005));
    static readonly double body=1-Math.Exp(-1/(48000*.030));
    static readonly double gainFollow=1-Math.Exp(-1/(48000*.003));
    static readonly double shortBody=1-Math.Exp(-1/(48000*.012));
    static readonly double shortGain=1-Math.Exp(-1/(48000*.002));
    static readonly double detailSplit=1-Math.Exp(-2*Math.PI*85/48000);
    static readonly double detailAttack=1-Math.Exp(-1/(48000*.0004));
    static readonly double detailRelease=1-Math.Exp(-1/(48000*.002));
    static readonly double detailGain=1-Math.Exp(-1/(48000*.0007));
    readonly double bodyFollow=shortAttack?shortBody:body;
    readonly double contrastFollow=shortAttack?shortGain:gainFollow;
    double fast,slow,reduction,lowLeft,lowRight;
    public readonly bool Enabled => riseContrast!=0;
    internal static ActionReleaseFeedback Select(uint eventId,uint soundId,string bank,int loops)
    {
        if(loops!=1)return default;
        // WindWave's ten ground/air starts post this cue at authored frame 2.
        // Its broad, high-frequency source follows the arm sweep; soften the
        // prepared rises without adding a launch/contact impulse or timer.
        if(bank=="job04_skill_m.sbnk.1.x64" && eventId==3057465803 && soundId==398568522)
            return new(0,0,0,0,-.08);
        // Gouge's hold-down/cling variants post two distinct recorded cues.
        // Reduce the bed between rises, never amplify or impose a stab rhythm;
        // the second post remains at the game's level-dependent timing.
        if(bank=="job04_skill_m.sbnk.1.x64" &&
           ((eventId==1973703678 && soundId==322148639) ||
            (eventId==1973703677 && soundId==1047867762)))
            return new(0,0,0,.25,.25,true,.72);
        // LeaveBody's early body gesture has a broad swelling source, separate
        // from the later two cues. Round rises only, preserving their timing
        // and the independently controlled phantom/return lifetime.
        if(bank=="job09_skill_m.sbnk.1.x64" && eventId==1089252474 && soundId==349937591)
            return new(0,0,0,0,-.08);
        // FullBlast is excluded because its game data contains native HD.
        // Hindsight's short passing swish follows the hand acceleration. Keep
        // the shared companion and the later recovery texture unchanged.
        if(bank=="job01_skill_m.sbnk.1.x64" && eventId==3429109828 && soundId==1032584315)
            return new(0,0,0,.16,.16,true,.65);
        // CounterSlash's low sweeping cue precedes separate later strikes.
        // Round its rises slightly; do not turn the sweep into another impact.
        if(bank=="job05_skill_m.sbnk.1.x64" && eventId==318455830 && soundId==337175237)
            return new(0,0,0,0,-.06);
        // Flight-shot kick has a separate earlier cue. This later passing
        // swish follows the recovery gesture; do not turn it into another hit.
        if(bank=="job02_skill_m.sbnk.1.x64" && eventId==230522855 && soundId==32672694)
            return new(0,0,0,0,-.05);
        // Gungnir's three aim directions share a short release swish whose
        // body follows the hand acceleration. Preserve its authored lead-in.
        if(bank=="job07_skill_m.sbnk.1.x64" && eventId==664315185 && soundId==302306968)
            return new(0,0,0,.18,.18,true,.72);
        // Shared by ten observed sword/shield motions, not a collision signal
        // or a Blink Strike-only route. Preserve the source's main impulse.
        if(bank=="job01_skill_m.sbnk.1.x64" && eventId==4085281348 && soundId==939560248)
            return new(0,0,0,.16,.16,true,.88);
        // Shared heavy-weapon action/guard sound. Neither shared route uses a
        // fixed phase boundary: preserve every source rise and natural decay.
        if(bank=="job05_skill_m.sbnk.1.x64" && eventId==3245888028 && soundId==32510626)
            return new(0,0,0,.12,.12,false,.92);
        // SkyDive landing's first sound only. Keep the separately triggered
        // later recovery sound unchanged rather than turning it into a hit.
        if(bank=="job07_skill_m.sbnk.1.x64" && eventId==163022634 && soundId==812236040)
            return new(31200,1f/12000,.18f,.025,.065);
        // CuttingWindFinish has several closely spaced source rises. Follow
        // them more quickly; leave its travel and shared companion sounds alone.
        if(bank=="job04_skill_m.sbnk.1.x64" && eventId==3308859417 && soundId==708609887)
            return new(0,0,0,.25,.25,true,.72);
        // Counter-hit branches share this short opening cue. Preserve each
        // branch's later strikes rather than synthesizing a combo rhythm.
        if(bank=="job04_skill_m.sbnk.1.x64" && eventId==3910865465 && soundId==5905912)
            return new(0,0,0,.25,.25,true,.72);
        // SpiritArrow preparation's two source alternatives have a sustained
        // swelling texture. Soften rises only; leave hold/release routes alone.
        if(bank=="job08_skill_m.sbnk.1.x64" && eventId==3268151686 && soundId is 623623213 or 1009205120)
            return new(0,0,0,0,-.04);
        // FullRecover has a separate early gesture and later broad swell.
        // Contrast the gesture's recorded rises, then soften the later texture;
        // neither route claims that healing actually reached a target.
        if(bank=="mgc_skill_m.sbnk.1.x64")
        {
            // AddFlare's release gesture has a heavy finite casting swell.
            // Round this cue's rises only; attachment, accumulation loops,
            // their stops and eventual explosion keep their own events.
            if(eventId==1015077566 && soundId==446189484)
                return new(0,0,0,0,-.05);
            // Firestorm/Frigor use near-identical early casting gestures.
            // Soften their recorded rises, not their later elemental effects.
            if((eventId==3481591478 && soundId==70398896) ||
               (eventId==1262230429 && soundId==475879887))
                return new(0,0,0,0,-.035);
            // VortexRage's effect layers contain three loops and this finite
            // onset. Round the onset only; preserve sustained source texture.
            if(eventId==1988849126 && soundId==176309447)
                return new(0,0,0,0,-.10);
            // RockBeat's final cast sound follows the large arm movement and
            // has multiple recorded rises. Keep its full natural ring-down.
            if(eventId==3074827773 && soundId==815594489)
                return new(0,0,0,.04,.10);
            // RockBeat eruption has independent short and sustained layers.
            // Follow the short layer's recorded rises; leave all three long
            // alternatives and the game's generation timing unchanged.
            if(eventId==3550924195 && soundId is 358388408 or 671695699 or 725605077)
                return new(0,0,0,.04,.08);
            // MeteorFall is excluded by its native HD appearance/impact data.
            if(eventId==3090200239 && soundId==560176414)
                return new(0,0,0,.03,.09);
            if(eventId==2514425910 && soundId is 428325339 or 879309961)
                return new(0,0,0,0,-.12);
        }
        // Smoke-dragon is excluded by its native HD appearance/roar data.
        return default;
    }
    public void Process(float left,float right,int frame,out float l,out float r)
    {
        double amplitude=Math.Max(Math.Abs(left),Math.Abs(right));
        bool detail=lowBody!=1;
        fast+=(amplitude-fast)*(amplitude>fast?(detail?detailAttack:attack):(detail?detailRelease:release));
        slow+=(amplitude-slow)*bodyFollow;
        double rise=Math.Clamp((fast-slow)/Math.Max(.002,slow),0,1);
        // Positive contrast lifts source rises; negative contrast softens them.
        // The intervening bed follows the route's independent reduction, and
        // the shared output limiter remains active.
        reduction+=(bodyReduction-riseContrast*rise-reduction)*(detail?detailGain:contrastFollow);
        float transientGain=(float)(1-reduction);
        float t=Math.Clamp((frame-startFrame)*inverseFadeFrames,0,1);
        float tailGain=1-tailReduction*t*t*(3-2*t);
        if(detail)
        {
            // A source-driven shelf, not an oscillator: keep the remaining
            // upper-band texture while reducing the common low-frequency bed.
            // Exact source gaps cannot acquire a filter tail or cross-channel
            // signal. Both channels use the same coefficients independently.
            lowLeft=left==0?0:lowLeft+detailSplit*(left-lowLeft);
            lowRight=right==0?0:lowRight+detailSplit*(right-lowRight);
            left-=(float)((1-lowBody)*lowLeft);
            right-=(float)((1-lowBody)*lowRight);
        }
        l=(left*transientGain)*tailGain;r=(right*transientGain)*tailGain;
    }
}
