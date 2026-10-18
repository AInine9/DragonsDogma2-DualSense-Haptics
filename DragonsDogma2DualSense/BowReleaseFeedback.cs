namespace DragonsDogma2DualSense;

// Typed Shell SoundParam stages and actor motion bindings; see bow-release-20261017.
// Applies only to generated audio haptics. Native HD and ActionStop are untouched.
static class BowReleaseFeedback
{
    internal static bool Mute(uint eventId) => eventId is
        52790231u or
        73534410u or
        82797343u or
        117638251u or
        144511191u or
        263738396u or
        388103406u or
        553361810u or
        661181461u or
        787429171u or
        858192925u or
        902190180u or
        910753969u or
        948039166u or
        1048284734u or
        1064328959u or
        1282927582u or
        1506853328u or
        1621435001u or
        1736098980u or
        1795409309u or
        1821795371u or
        1913405582u or
        2004922858u or
        2016322974u or
        2030592004u or
        2104715249u or
        2186654508u or
        2289930198u or
        2683313650u or
        2781047740u or
        2792948466u or
        2826522887u or
        2935597805u or
        3044308226u or
        3235762094u or
        3239471572u or
        3495785529u or
        3804249624u or
        4195418513u or
        4230755335u or
        4253768080u;

    internal static bool MagickRelease(uint eventId) => eventId is
        122069360u or 302948647u or 621824535u or 886856791u or 898670547u or
        1032838421u or 1171491724u or 1311697443u or 1319158439u or 1460210816u or
        1939099017u or 2642652131u or 3324360126u or 3839154065u or 3885412705u;

    internal static float DrawGain(uint eventId, string bank) => (bank, eventId) switch
    {
        ("job02_skill_m.sbnk.1.x64", 1804849216u or 2779698105u or 3465101965u) => .25f,
        ("job08_skill_m.sbnk.1.x64", 3450432170u or 2126607540u or 3899675434u or 2448382759u or 3087583468u or 3268151686u or 1680363921u) => .25f,
        _ => 1
    };
}
