namespace DragonsDogma2DualSense;

// Local haptic calibration for three quiet DLC sources. Route gains retain
// distance attenuation; authored media, timing, random choice and stereo stay
// intact. These factors were compared through the shared output limiter with
// Cyclops events of the same kind, not measured as equal controller feel.
static class NearbyEnemyFeedback
{
    internal static float SourceGain(uint eventId,string bank) => (eventId,bank) switch
    {
        (2181985848u,"ch250001_se_m.sbnk.1.x64") => 2,
        (1572558409u,"ch250001_se_m.sbnk.1.x64") => 2,
        (1083409134u,"ch253012_se_m.sbnk.1.x64") => 4,
        _ => 1
    };
}
