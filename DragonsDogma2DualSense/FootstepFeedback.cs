namespace DragonsDogma2DualSense;

// The paired ground/footwear STEP routes are verified in the trigger metadata
// and live playback. Other material events (landing, sliding, impacts) stay intact.
struct FootstepFeedback
{
    internal static bool Applies(uint eventId, string bank) => eventId switch
    {
        2357958976 or 2357959006 => bank == "pm_pl_m.sbnk.1.x64",
        2448530926 or 2448530928 => bank == "pl_wear_foot_m.sbnk.1.x64",
        _ => false
    };
    // Artistic softening of these source-derived steps, not actuator calibration.
    // Two poles reduce sharp texture while retaining the original rhythm and bass.
    const int SampleRate = 48000, FadeInFrames = 288, FadeOutFrames = 480;
    const double CutoffHz = 120;
    static readonly float coefficient = (float)(1 - Math.Exp(-2 * Math.PI * CutoffHz / SampleRate));
    float leftLow, leftBody, rightLow, rightBody;
    public void Process(float left, float right, int frame, int end, out float l, out float r)
    {
        leftLow += coefficient * (left - leftLow);
        leftBody += coefficient * (leftLow - leftBody);
        rightLow += coefficient * (right - rightLow);
        rightBody += coefficient * (rightLow - rightBody);
        float fade = Math.Max(0, Math.Min(1, Math.Min(frame / (float)FadeInFrames, (end - 1 - frame) / (float)FadeOutFrames)));
        l = leftBody * fade;
        r = rightBody * fade;
    }
}
