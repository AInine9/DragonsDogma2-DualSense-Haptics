namespace DragonsDogma2DualSense;

// Preparation-only selection: the runtime graph, event clocks, Wwise realization,
// source bass and channel balance remain authoritative. These are artistic source
// translations, not AC8 measurements or a calibrated actuator transfer function.
static class SourceTexture
{
    internal sealed record Profile(int MidDivisor, int HighDivisor, double Attack, double Release,
        double HighAttack, double HighRelease, double PhaseFollow, bool Edged = false, double TranslationCutoff = 280,
        double MidGain = .9, double HighGain = .4);
    internal static readonly Profile Default = new(8,32,.004,.045,.004,.045,.00156956327);
    // Independent followers expose upper detail without shortening the mid-band
    // body. All values are DD2 design candidates; no foreign-game calibration.
    static readonly Profile Contact = new(4,16,.0015,.012,.00075,.008,Follow(36));
    static readonly Profile Release = new(6,24,.002,.020,.001,.012,Follow(24));
    static readonly Profile Flight = new(6,24,.004,.045,.002,.020,Follow(18));
    static readonly Profile Body = new(10,40,.006,.060,.002,.025,Follow(8));
    // Long tonal charge layers retain their source-phase trajectory: changing
    // divisors can move beating peaks even when the total energy barely changes.
    static readonly Profile FineHeld = Default with {HighAttack=.002,HighRelease=.025};
    static readonly Profile MidHeld = Default with {Attack=.003,Release=.035};
    // Compact contacts preserve source phase where re-division inflated the
    // later body. Faster followers expose recorded edges without adding a pulse.
    static readonly Profile ContactDetail = Default with {Attack=.0015,Release=.012,HighAttack=.00075,HighRelease=.008};
    // Fine ice flight changes only upper-band translation. Its middle trajectory
    // keeps the slower source contour while the high layer retains finer texture.
    static readonly Profile IceHeld = Default with {HighDivisor=24,HighAttack=.002,HighRelease=.025};
    // Source-following flattened crests distinguish compact release/counter edges.
    // These DD2 artistic values alter translated bands only, not recorded bass,
    // event timing or native HD. They are not foreign-game carrier coefficients.
    static readonly Profile ReleaseEdge = ContactDetail with {Edged=true,TranslationCutoff=550};
    static readonly Profile ContactEdge = Contact with {Edged=true,TranslationCutoff=400};
    static readonly Profile DetailEdge = ContactDetail with {Edged=true,TranslationCutoff=400};
    // Preserve independent recorded contours while exposing translated detail in
    // these sustained layers. Source upper-band gain is not an actuator frequency.
    static readonly Profile BodyDetail = Body with {MidGain=1.35,HighGain=.9};
    static readonly Profile FineDetail = FineHeld with {MidGain=1.35,HighGain=.9};
    static readonly Profile FlareExpansion = Body with {MidGain=.6,HighGain=1.2,Edged=true,TranslationCutoff=400};
    // Measured compact contacts expose source-following crests while retaining
    // the slower body follower. Selection is per bank/source, including shared owners.
    static readonly Profile ReleaseGrain = Release with {Edged=true,TranslationCutoff=400,HighGain=.7};
    static readonly Profile ContactGrain = Contact with {Edged=true,TranslationCutoff=400,HighGain=.7};
    static readonly Profile BodyGrain = Body with {Edged=true,TranslationCutoff=400,HighGain=.7};
    static readonly Profile FlightEdge = Flight with {Edged=true,TranslationCutoff=400};
    // This upper-rich contact spent much of its raw crossing target at the
    // frequency ceiling. Wider division restores recorded frequency movement;
    // it does not add a fixed carrier or change the common 240 Hz ceiling.
    static readonly Profile ContactTrace = Contact with {HighDivisor=64};
    // Preserve different source-frequency trajectories in selected contacts and
    // sustained cues whose upper crossing estimates frequently hit the ceiling.
    // The original band envelopes, shared ownership and event clocks still apply.
    static readonly Profile ContactTraceFine = Contact with {HighDivisor=32};
    static readonly Profile FlightTrace = Flight with {HighDivisor=32};
    static readonly Profile ReleaseTraceFine = Release with {HighDivisor=32};
    static readonly Profile ReleaseTraceWide = Release with {HighDivisor=48};
    // Long ice flight retains its source phase and middle envelope. Balance
    // translated bands before exposing grain so its quiet tail is not replaced
    // by a stronger early body. Alternatives keep their own recorded contour.
    static readonly Profile IceFlightGrain = IceHeld with {HighAttack=.0005,HighRelease=.006,
        MidGain=.8,HighGain=.25,Edged=true,TranslationCutoff=550};
    static readonly Profile IceFlightGrainFine = IceFlightGrain with {MidGain=.7,HighGain=.30};
    static readonly Profile IceLoopGrain = IceHeld with {HighAttack=.0005,HighRelease=.006,
        Edged=true,TranslationCutoff=400};
    // Rock onset layers keep their long envelopes rather than becoming one
    // synthetic impact. Their charge layer retains its separate named loop.
    static readonly Profile RockBodyGrain = Body with {Edged=true,TranslationCutoff=550,MidGain=.8,HighGain=.3};
    static readonly Profile RockFineGrain = FineHeld with {Edged=true,TranslationCutoff=550,MidGain=.8,HighGain=.3};
    static readonly Profile RockContactGrain = ContactDetail with {Edged=true,TranslationCutoff=550,MidGain=.8,HighGain=.3};
    static readonly Profile RockLayerDetail = Body with {Edged=true,TranslationCutoff=400,MidGain=1.2,HighGain=.6};
    // Revisit low-heavy retained sources without re-dividing their phase. Balanced
    // translated bands expose grain while preserving recorded movement/charge beds.
    static readonly Profile HeldContourGrain = Default with {Edged=true,TranslationCutoff=550,MidGain=.8,HighGain=.3};
    // Actor openings retain their recorded onset; faster followers do not move the
    // event clock to a hand-motion peak or synthesize a separate contact pulse.
    static readonly Profile FastContourGrain = Default with {Attack=.002,Release=.035,
        HighAttack=.00075,HighRelease=.008,Edged=true,TranslationCutoff=400};
    static readonly Profile IceWithdrawalGrain = HeldContourGrain with {HighAttack=.0005,HighRelease=.006};
    static readonly Profile HeldContourEdge = Default with {Edged=true,TranslationCutoff=400};
    // Ice gift's charge keeps its long source phase and middle envelope; balance
    // its faster high follower without trimming the recorded charge tail.
    static readonly Profile IceGiftGrain = HeldContourEdge with {MidGain=.75,HighGain=.5,HighAttack=.0005,HighRelease=.006};
    // Selected support cues keep a smooth middle body. Wider upper division
    // follows a different source-derived frequency path without adding pulses.
    // Short physical layers: preserve phase/followers; balance translated grain against source body.
    static readonly Profile ReleaseCrest = Release with {Edged=true,TranslationCutoff=550,MidGain=.8,HighGain=.3};
    static readonly Profile ReleaseCrestLight = ReleaseCrest with {MidGain=.7,HighGain=.25};
    static readonly Profile ReleaseCrestSoft = ReleaseCrest with {TranslationCutoff=400};
    static readonly Profile ContactCrestSoft = ContactDetail with {Edged=true,TranslationCutoff=400,MidGain=.8,HighGain=.3};
    static readonly Profile ContactCrestFine = ContactCrestSoft with {MidGain=.75,HighGain=.35};
    static readonly Profile SoftTrace = Default with {HighDivisor=48,HighAttack=.0005,HighRelease=.006};
    static readonly Profile SoftTraceWide = SoftTrace with {HighDivisor=64};
    static double Follow(double hz) => 1-Math.Exp(-2*Math.PI*hz/48000);

    internal static Profile Select(string bank, uint sound) => (bank,sound) switch
    {
        // Holy field opening and completion keep separate smooth contours.
        ("mgc_skill_m.sbnk.1.x64",1072565511) => SoftTrace,
        ("mgc_skill_m.sbnk.1.x64",105799283) => SoftTraceWide,
        // Cure/haste charge detail contrasts with their softer completion.
        ("mgc_skill_m.sbnk.1.x64",252274223 or 650127091) => HeldContourGrain,
        ("mgc_skill_m.sbnk.1.x64",412105823) => SoftTrace,
        ("mgc_skill_m.sbnk.1.x64",715265082) => SoftTraceWide,
        // Full recover keeps buildup/late body smooth beside a detailed late layer.
        ("mgc_skill_m.sbnk.1.x64",330879688 or 428325339) => SoftTrace,
        ("mgc_skill_m.sbnk.1.x64",879309961) => HeldContourEdge,
        // Spell break charge and completion preserve their original named clocks.
        ("mgc_skill_m.sbnk.1.x64",947182571) => SoftTraceWide,
        ("mgc_skill_m.sbnk.1.x64",1068438413) => SoftTrace,
        // Shared snipe/rain/TU aerial launch bodies retain authored shell clocks.
        ("job02_skill_m.sbnk.1.x64",391507147 or 528918093) => FastContourGrain,
        // Normal/just powerful shot aliases keep the same recorded launch body.
        ("job02_skill_m.sbnk.1.x64",501980597 or 655841158) => HeldContourEdge,
        // Gungnir charge launch stays distinct from its heavier contact alternate.
        ("job07_skill_m.sbnk.1.x64",780842596) => HeldContourEdge,
        // Actor fire opening does not replace the separately authored flame layers.
        ("mgc_skill_m.sbnk.1.x64",70398896) => FastContourGrain,
        // TU Mindeye activation is a stance body, not an invented counter hit.
        ("job01_skill_m.sbnk.1.x64",340750111) => HeldContourGrain,
        // Fire completion and ice charge are different authored stages.
        ("mgc_skill_m.sbnk.1.x64",91099294) => HeldContourEdge,
        ("mgc_skill_m.sbnk.1.x64",733813911) => IceGiftGrain,
        // Shared warrior actor cues retain every owner's existing timing.
        ("job05_skill_m.sbnk.1.x64",383428356) => FastContourGrain,
        ("job05_skill_m.sbnk.1.x64",519638868) => HeldContourEdge,
        ("job05_skill_m.sbnk.1.x64",877677723) => HeldContourGrain,
        // Whirling arrow launch bodies, separate from their contact sources.
        ("job02_skill_m.sbnk.1.x64",212697429) => FastContourGrain,
        ("job02_skill_m.sbnk.1.x64",679725308) => HeldContourGrain,
        ("job02_skill_m.sbnk.1.x64",681670091 or 819617624) => HeldContourEdge,
        // Shared sword actor cue and guard opening keep their recorded low body.
        ("job01_skill_m.sbnk.1.x64",1025956528) => FastContourGrain,
        ("job01_skill_m.sbnk.1.x64",200523478) => HeldContourGrain,
        // Blood pact withdrawal stays gradual; start/end cues keep source onset.
        ("job05_skill_m.sbnk.1.x64",167127485) => HeldContourEdge,
        ("job05_skill_m.sbnk.1.x64",839826709 or 870005228 or 1063336563) => FastContourGrain,
        // Aerial counter preparation, distinct from the later hit sources.
        ("job04_skill_m.sbnk.1.x64",530599693) => FastContourGrain,
        // Frost actor opening; shell contact and breakup keep their separate cues.
        ("mgc_skill_m.sbnk.1.x64",475879887) => FastContourGrain,
        // Salamander's low-heavy shell opening retains its longer recorded body.
        ("mgc_skill_m.sbnk.1.x64",153174277) => HeldContourGrain,
        // TU revive preparation, not a fabricated revival or waiting timer.
        ("mgc_skill_m.sbnk.1.x64",479803219) => HeldContourGrain,
        // TU charge movement shared by start/loop/end, not a new impact per key.
        ("job05_skill_m.sbnk.1.x64",1058107920) => HeldContourGrain,
        // TU repeated explosion texture; original event/loop handling remains.
        ("job04_skill_m.sbnk.1.x64",267832626) => HeldContourGrain,
        // TU ice withdrawal starts at its authored finish cue, before hand recovery.
        ("job06_skill_m.sbnk.1.x64",903759326) => IceWithdrawalGrain,
        // Holy field sustained shimmer.
        ("mgc_skill_m.sbnk.1.x64",561712572) => FineHeld,
        // Cure field sustained body.
        ("mgc_skill_m.sbnk.1.x64",762798245) => MidHeld,
        // Cure field termination.
        ("mgc_skill_m.sbnk.1.x64",305111822) => Release,
        // Spell break effect opening.
        ("mgc_skill_m.sbnk.1.x64",671908871) => Contact,
        // Shared ordinary arrow flight detail.
        ("job02_skill_m.sbnk.1.x64",220390738 or 941561119) => FineHeld,
        // Shared arrow launch body.
        ("job02_skill_m.sbnk.1.x64",215575205 or 544391831) => MidHeld,
        // Compact arrow release detail.
        ("job02_skill_m.sbnk.1.x64",254012217 or 958779457 or 816962573) => ReleaseEdge,
        // Upper-middle launch scrape.
        ("job02_skill_m.sbnk.1.x64",391955782 or 740802384 or 501663895) => FineHeld,
        // Threefold launch body.
        ("job02_skill_m.sbnk.1.x64",515182307) => Contact,
        // Threefold launch alternate.
        ("job02_skill_m.sbnk.1.x64",845712023) => Release,
        // Triad launch.
        ("job02_skill_m.sbnk.1.x64",765631575) => Contact,
        // BodyBinder moving launch layer.
        ("job02_skill_m.sbnk.1.x64",160036994 or 578416049) => Flight,
        // BodyBinder release completion layer.
        ("job02_skill_m.sbnk.1.x64",498479454 or 728674263) => Release,
        // BodyBinder and FullBlast shared contact.
        ("job02_skill_m.sbnk.1.x64",316855708) => Contact,
        // BodyBinder contact alternate.
        ("job02_skill_m.sbnk.1.x64",804391980) => ContactDetail,
        // Bomb attached/sustained fine texture.
        ("job02_skill_m.sbnk.1.x64",156741543) => FineHeld,
        // Water arrow sustained fine texture.
        ("job02_skill_m.sbnk.1.x64",494991279 or 1062307344) => FineHeld,
        // Oil arrow sustained texture.
        ("job02_skill_m.sbnk.1.x64",1026612097) => FineHeld,
        // Shared water/oil contact detail.
        ("job02_skill_m.sbnk.1.x64",267800835 or 1048652325) => ContactDetail,
        // Shared water/oil contact alternate.
        ("job02_skill_m.sbnk.1.x64",994577548) => Contact,
        // Poison arrow release texture.
        ("job02_skill_m.sbnk.1.x64",715527239) => FineHeld,
        // Poison arrow contact detail.
        ("job02_skill_m.sbnk.1.x64",962304932) => ContactDetail,
        // Whirling arrow launch detail.
        ("job02_skill_m.sbnk.1.x64",272621757 or 944642266) => DetailEdge,
        // Whirling arrow contact.
        ("job02_skill_m.sbnk.1.x64",629147994) => Contact,
        // Whirling arrow contact alternates.
        ("job02_skill_m.sbnk.1.x64",671762669 or 1060889155) => ContactDetail,
        // Parry counter contact.
        ("job04_skill_m.sbnk.1.x64",314111051) => ContactEdge,
        // Parry counter broader contact.
        ("job04_skill_m.sbnk.1.x64",665335106) => DetailEdge,
        // Powder sustained texture.
        ("job04_skill_m.sbnk.1.x64",679643912) => FineHeld,
        // Wind wave launch.
        ("job04_skill_m.sbnk.1.x64",718080342) => Release,
        // Blade projectile flight.
        ("job07_skill_m.sbnk.1.x64",858997798) => Flight,
        // Blade projectile contact.
        ("job07_skill_m.sbnk.1.x64",330089970) => ContactDetail,
        // Gungnir secondary launch.
        ("job07_skill_m.sbnk.1.x64",976129556) => Release,
        // Gungnir contact body.
        ("job07_skill_m.sbnk.1.x64",861879619) => Release,
        // Shared magic bow release detail.
        ("job08_skill_m.sbnk.1.x64",388086599 or 541312599 or 869422652 or 882577065) => FineHeld,
        // Frost block fine contact.
        ("job08_skill_m.sbnk.1.x64",764765572) => Release,
        // Frost block fine alternate.
        ("job08_skill_m.sbnk.1.x64",1059487158) => FineHeld,
        // Heavy thrust late wall/large response.
        ("job05_skill_m.sbnk.1.x64",940854713) => ContactDetail,
        // TU carpet bombing: compact ignition, heavier second-stage detail,
        // warning texture and completion retain their separate recorded timing.
        ("job04_skill_m.sbnk.1.x64",86211701 or 735170374 or 498487898 or 997387640 or 1062618042) => ContactDetail,
        ("job04_skill_m.sbnk.1.x64",410123632) => Contact,
        ("job04_skill_m.sbnk.1.x64",530626899) => Release,
        ("job04_skill_m.sbnk.1.x64",239613109 or 426337333) => MidHeld,
        ("job04_skill_m.sbnk.1.x64",370666136) => FineHeld,
        // Charge entry differs from its retained multi-stage movement bed.
        ("job05_skill_m.sbnk.1.x64",379983348) => Release,
        // Frozen-storm key105 has separate fine and body completion layers.
        // Keep its earlier withdrawal source and all ThunderWhip sources intact.
        ("job06_skill_m.sbnk.1.x64",156531358) => IceHeld,
        ("job06_skill_m.sbnk.1.x64",1055968602) => Release,
        ("job07_skill_m.sbnk.1.x64",371691382) => Release,
        ("job08_skill_m.sbnk.1.x64",296573448) => FineHeld,
        // Shield-hate's authored frame-14 sound precedes the hand-motion peak.
        // Shape only this existing sound, without assuming which motion the
        // dedicated controller selects or adding a new skill callback.
        ("job01_skill_m.sbnk.1.x64",208502732) => DetailEdge,
        // TU flight-shot separates the kick and later cue from the air shot.
        // Preserve low body where re-division enlarged the subsequent energy.
        ("job02_skill_m.sbnk.1.x64",32672694 or 148181155) => ContactDetail,
        ("job02_skill_m.sbnk.1.x64",476429266) => Contact,
        // TU heavy-rain shell sources retain their own body and phase; faster
        // fine followers preserve the tail better than full re-division.
        ("job02_skill_m.sbnk.1.x64",385503608 or 995197699) => FineHeld,
        // Support-spell completion differs from the retained low charge bed.
        // Guard-bit completion keeps its body; full recovery's actor opening
        // and charge completion keep their independently authored contours.
        ("mgc_skill_m.sbnk.1.x64",348550298) => ContactDetail,
        ("mgc_skill_m.sbnk.1.x64",780238226) => Release,
        ("mgc_skill_m.sbnk.1.x64",560176414) => ContactTraceFine,
        // Cutting-wind and counter contacts are distinct from their sustained
        // loop/low stance cues. Preserve the latter instead of adding pulses.
        ("job04_skill_m.sbnk.1.x64",708609887) => ContactCrestSoft,
        ("job04_skill_m.sbnk.1.x64",147653757) => ContactDetail,
        ("job04_skill_m.sbnk.1.x64",898514402) => Contact,
        ("job05_skill_m.sbnk.1.x64",974869244) => Release,
        ("job07_skill_m.sbnk.1.x64",389241450) => Contact,
        // Magic-bow shared opening: retain the fine source-phase trajectory;
        // re-division moved its center by 20-40 ms. The later enchant cue keeps
        // a separate middle/body follower, with no new trigger or element ID.
        ("job08_skill_m.sbnk.1.x64",74243194) => FineHeld,
        ("job08_skill_m.sbnk.1.x64",693542636) => MidHeld,
        // Heavy-thrust late contacts (authored frames 76/80) also serve ground
        // puncture. Keep their low mass; faster detail followers avoid the large
        // late-energy growth observed with re-divided alternatives.
        ("job05_skill_m.sbnk.1.x64",10448500 or 379114694 or 1051100906
            or 210045111 or 239479268 or 497617962 or 523159704 or 862466789) => ContactDetail,
        ("job05_skill_m.sbnk.1.x64",28668471 or 1038944666) => Contact,
        // Heavy-rain finish frame 34 precedes its hand-motion peak at 35/36.
        // Translate each authored alternative without moving the event clock.
        ("job02_skill_m.sbnk.1.x64",168131427) => Release,
        ("job02_skill_m.sbnk.1.x64",463584839) => ContactDetail,
        ("job02_skill_m.sbnk.1.x64",781413557) => Contact,
        // Powder placement and camouflage's first cue are almost entirely fine
        // source energy. Preserve phase rather than inventing a bass contact.
        ("job04_skill_m.sbnk.1.x64",824933123 or 580706134) => FineHeld,
        ("job04_skill_m.sbnk.1.x64",1002729970) => MidHeld,
        // Ice spear: 86-99 ms short releases versus the 340 ms bass-rich branch.
        ("job08_skill_m.sbnk.1.x64",97427012 or 179257627 or 754931964) => Release,
        ("job08_skill_m.sbnk.1.x64",18009278) => Body,
        // Ricochet and frost contact: distinct short sources, not synthetic clicks.
        ("job08_skill_m.sbnk.1.x64",42890960 or 90918204 or 218108811 or 868624803
            or 257709556 or 589928526 or 691757877 or 1030707742) => Contact,
        // Frost flight has seconds of fine structure; preserve its loop and stop.
        ("job08_skill_m.sbnk.1.x64",319681727) => IceHeld,
        ("job08_skill_m.sbnk.1.x64",645908341 or 782668637) => IceFlightGrain,
        ("job08_skill_m.sbnk.1.x64",947377665) => IceFlightGrainFine,
        // Shared bow contacts use one translation for all six known owners.
        ("job02_skill_m.sbnk.1.x64",630234473 or 1056472735) => Contact,
        ("job02_skill_m.sbnk.1.x64",337415755 or 759026529) => Release,
        // Authored bow StartSeNo branches: powerful-shot 68-99 ms upper-rich
        // releases, explosive/poison arrow launches, shared water/oil launches,
        // and the longer 124-139 ms spiral release. Do not retime them to the
        // actor hand peak or relabel a launch as the subsequent arrow impact.
        ("job02_skill_m.sbnk.1.x64",300552152 or 494571988 or 633272546 or 790465242
            or 801809690 or 1018032330 or 152493519 or 475430202 or 505430214
            or 601005052 or 841493223 or 843154481 or 857853295 or 1030559050
            or 106234388 or 770979608 or 899315573 or 84616937 or 387086238
            or 553731296 or 570948535 or 1004136511 or 1038438066
            or 535725194 or 782692160 or 975287926 or 1019916250) => Release,
        // Very short (17-30 ms energy center) rain-arrow emission: retain its
        // individual granular discharge; the event graph provides repetition.
        ("job02_skill_m.sbnk.1.x64",104635567 or 256979503 or 629690358
            or 744684561 or 948810827 or 1003303714) => Contact,
        // Blizzard fragment contacts are separate from the heavy onset source.
        ("mgc_skill_m.sbnk.1.x64",464788626 or 767546015 or 986997235) => Release,
        // Thief: a 34 ms hook/contact differs from the 161 ms upper-rich slash.
        ("job04_skill_m.sbnk.1.x64",759272363) => Contact,
        ("job04_skill_m.sbnk.1.x64",888029300) => ReleaseCrestLight,
        // Flame blade's authored stages: short sweep at frame 23, contact at 57,
        // broader sweep at 76, then the bass-rich expansion at 105. Shared
        // ordinary/master sources retain the same rule and original event clock.
        ("job04_skill_m.sbnk.1.x64",775452824 or 188559376) => ReleaseCrestLight,
        ("job04_skill_m.sbnk.1.x64",354694302) => Flight,
        ("job04_skill_m.sbnk.1.x64",558290901 or 422411877 or 714162157 or 1005522303) => Body,
        // Additional authored motion branches: sword thrust details (117-119 ms)
        // and greatsword contact (173 ms), without replacing their recorded bass.
        ("job01_skill_m.sbnk.1.x64",287902115) => ReleaseCrestLight,
        ("job01_skill_m.sbnk.1.x64",468091368) => Release,
        ("job05_skill_m.sbnk.1.x64",386443708) => ReleaseCrestSoft,
        // Wider greatsword sweep has a 276 ms center and a strong mid band.
        ("job05_skill_m.sbnk.1.x64",314845806) => Flight,
        // Longer upper-rich magic motion sources preserve their entire contour.
        ("mgc_skill_m.sbnk.1.x64",1067217317) => FlightTrace,
        ("job07_skill_m.sbnk.1.x64",695779558) => Flight,
        ("job07_skill_m.sbnk.1.x64",302306968) => FlightEdge,
        ("job09_skill_m.sbnk.1.x64",334481252 or 888408035) => Flight,
        // Short mystic-spear release branches are distinct from long motion tails.
        ("job07_skill_m.sbnk.1.x64",11430722 or 636948581 or 807229816 or 1040024568
            or 7772690 or 609480220 or 756839386) => Release,
        // Typed HollowOut normal/Lv2 frame 37 versus the EX follow-through at
        // frames 145/149: upper-rich cut, then a longer 980 ms energy tail.
        ("job04_skill_m.sbnk.1.x64",322148639) => Release,
        ("job04_skill_m.sbnk.1.x64",1047867762) => Flight,
        // TU flying bow: frame 22 of jump-shoot is 84% upper-band energy.
        // Kick and takeoff bass sources remain unchanged, on their own clocks.
        ("job02_skill_m.sbnk.1.x64",1057385762) => Release,
        // TU upper-slash ground/air lead-ins precede the shared slash source.
        // The later impact branch has its own broader, bass-rich expansion.
        ("job04_skill_m.sbnk.1.x64",917539801 or 106530836) => Release,
        ("job04_skill_m.sbnk.1.x64",844710684) => BodyGrain,
        // DivineShot's typed shell onset: 108 ms center, 61% upper energy.
        // Individual projectiles keep their authored generation cadence.
        ("mgc_skill_m.sbnk.1.x64",887308550) => Release,
        // ShadowBinding separates the broader launch, normal contact and the
        // bass-dominant Lv2/EX contact/child shell. No artificial hold timer.
        ("job08_skill_m.sbnk.1.x64",669637523) => Flight,
        ("job08_skill_m.sbnk.1.x64",254618230) => Release,
        ("job08_skill_m.sbnk.1.x64",1024986610) => Body,
        // HealingSmoke onset is a mid-band movement, followed by a lower, softer
        // impact body. Retain source randomization and its quieter authored gain.
        ("job09_skill_m.sbnk.1.x64",207183739 or 255592447 or 976324502) => Flight,
        ("job09_skill_m.sbnk.1.x64",408385378 or 885493709 or 934119518) => Body,
        // Sword/shield motion sources: short evasive/catapult/hitback detail,
        // wider rising-slash branches, and the lower shield-contact body.
        // Shared sounds keep one rule across their known skill owners.
        ("job01_skill_m.sbnk.1.x64",1032584315 or 776297369) => Release,
        ("job01_skill_m.sbnk.1.x64",590866346) => ReleaseGrain,
        ("job01_skill_m.sbnk.1.x64",687749262 or 878198139 or 694997288) => Flight,
        ("job01_skill_m.sbnk.1.x64",104303331 or 579851951) => Body,
        // HeavyRain: two shell onsets, four broader impacts and two very short
        // 21 ms-center contacts. Its subsonic-dominant loop remains a control.
        ("job02_skill_m.sbnk.1.x64",461772898 or 1016090015 or 72991525
            or 135860392 or 332116984 or 403758880) => Release,
        ("job02_skill_m.sbnk.1.x64",548062008 or 1051446598) => Contact,
        // FrozenStorm has independent start / loop / stop / hit events. Preserve
        // the 7.4-second loop contour; shared fragment hits already use Release.
        ("job06_skill_m.sbnk.1.x64",461618224 or 394544554) => Flight,
        // DivineArrow small burst is upper-rich; its larger branch is mid-rich.
        ("job08_skill_m.sbnk.1.x64",36909210) => Release,
        ("job08_skill_m.sbnk.1.x64",732071634) => Body,
        // GravityField omen keeps recorded low pressure beneath sparse detail.
        ("job07_skill_m.sbnk.1.x64",273551205) => Body,
        // Typed named spell actions bind these element-specific charge sources.
        // Fire carries 82-89% low-band energy; ice has an 82% upper-rich layer.
        // Keep the source loops and named stop events, never the buff duration.
        ("mgc_skill_m.sbnk.1.x64",513290974 or 696830064) => Body,
        ("mgc_skill_m.sbnk.1.x64",219267501) => FineHeld,
        ("mgc_skill_m.sbnk.1.x64",886628332) => Release,
        // FrostSpike's named charge layers retain their long granular contour;
        // its charge-end cue remains separate from the actual ice-shell impact.
        ("mgc_skill_m.sbnk.1.x64",142334840 or 779465096) => IceHeld,
        ("mgc_skill_m.sbnk.1.x64",92107980) => Release,
        // Salamander charge is low-rich; the heavy fire-shell controls are not
        // changed by this preparation rule. GuardBit's finer charge layer is
        // separate from the low layer and from actual shield contact.
        ("mgc_skill_m.sbnk.1.x64",74708963 or 1013123102) => Body,
        ("mgc_skill_m.sbnk.1.x64",525146296) => Flight,
        // Wire motion: frame 10 movement (47% upper energy), then its separate
        // success/retraction cue (143 ms center). The shell hook remains Contact.
        ("job04_skill_m.sbnk.1.x64",838567611) => Flight,
        ("job04_skill_m.sbnk.1.x64",933281229) => Release,
        // Astral-body motion has authored cues at frames 14, 79 and 154. The
        // longer mid-rich opening differs from the later lower-bodied cues;
        // every source still starts only from its original event.
        ("job09_skill_m.sbnk.1.x64",349937591) => Flight,
        ("job09_skill_m.sbnk.1.x64",400252082 or 747523756 or 121365684 or 385671420) => Body,
        // Typed parry-counter motion: 120 ms upper/mid detail versus the longer
        // 676 ms-center branch. Stealth's separate movement cue is not its hold.
        ("job04_skill_m.sbnk.1.x64",5905912) => ReleaseCrest,
        ("job04_skill_m.sbnk.1.x64",699505597 or 306214562) => Flight,
        // Greatsword shared sources keep a single rule across typed owners:
        // finer short sweeps, a wider 455 ms-center sweep, and compact contacts.
        ("job05_skill_m.sbnk.1.x64",32510626) => ReleaseCrest,
        ("job05_skill_m.sbnk.1.x64",752970667 or 545446865) => Release,
        ("job05_skill_m.sbnk.1.x64",1039426853) => Flight,
        ("job05_skill_m.sbnk.1.x64",710736296) => ContactGrain,
        ("job05_skill_m.sbnk.1.x64",1022019493) => ContactTrace,
        // SkyDive's mixed-band contact needs its mid-band body: the sustained
        // follower retains its energy contour better than the short Release.
        // Separate detail and 437 ms low-rich branch keep their own event clocks.
        ("job07_skill_m.sbnk.1.x64",343294791) => Flight,
        ("job07_skill_m.sbnk.1.x64",335367558) => Release,
        ("job07_skill_m.sbnk.1.x64",812236040) => Body,
        // Named support-charge loops: haste/recovery carry sustained fine detail,
        // while spell-break carries mid-band motion. Holy/cure retain lower body.
        // These are preparation layers, not invented pulses on the buff target.
        ("mgc_skill_m.sbnk.1.x64",1047063402) => FlightTrace,
        ("mgc_skill_m.sbnk.1.x64",226472290) => FineHeld,
        ("mgc_skill_m.sbnk.1.x64",390902955) => MidHeld,
        ("mgc_skill_m.sbnk.1.x64",404593636 or 918596777) => Body,
        // Fire-storm/Salamander share these named charge sources. Rock charge
        // has a different recorded contour; retain both full low-rich envelopes.
        ("mgc_skill_m.sbnk.1.x64",339008809 or 955277186) => Body,
        ("mgc_skill_m.sbnk.1.x64",583987660) => RockLayerDetail,
        // WindWave's authored frame-2 detail is entirely upper-band material.
        ("job04_skill_m.sbnk.1.x64",398568522) => Flight,
        // Typed FlameLance shell: compact launch versus the low/mid-rich flight
        // loop. Preserve its phase contour and the authored LoopStop event.
        ("job08_skill_m.sbnk.1.x64",214201001 or 284746717) => Release,
        ("job08_skill_m.sbnk.1.x64",317846184 or 630236605) => MidHeld,
        // BurningLight has projectile onset, larger orb onset, fine sustained
        // light and a lower finish. Each is a separate shell field, not a timer.
        ("job08_skill_m.sbnk.1.x64",150289618 or 632794817) => Release,
        ("job08_skill_m.sbnk.1.x64",186917718 or 987880035 or 26396653 or 145833841) => Body,
        ("job08_skill_m.sbnk.1.x64",920848123 or 980691351) => FineHeld,
        // SleepArrow: short release, sustained upper detail, then a mid-rich
        // contact with the original quieter gain. No synthetic sleep heartbeat.
        ("job08_skill_m.sbnk.1.x64",274234093 or 862710952) => Release,
        ("job08_skill_m.sbnk.1.x64",122383032 or 837700483) => FineHeld,
        ("job08_skill_m.sbnk.1.x64",619578747 or 682729020) => ContactDetail,
        // AbsorbArrow distinguishes release, flight and actor/terrain contacts.
        // Flight keeps its authored +575-cent draw; no extra pitch is imposed.
        ("job08_skill_m.sbnk.1.x64",20360663 or 658966681 or 866221583
            or 338519680 or 637407474 or 760018392) => ContactDetail,
        ("job08_skill_m.sbnk.1.x64",3518705 or 124264099 or 963259484) => FineHeld,
        ("job08_skill_m.sbnk.1.x64",343906284 or 688130729 or 940345202) => ContactDetail,
        // Fighter shared slash/finish sources: one rule across dash stab,
        // circular slash and spinning guard. No directional hand inference.
        ("job01_skill_m.sbnk.1.x64",386049193 or 1011105678) => Release,
        ("job01_skill_m.sbnk.1.x64",586800044) => ContactDetail,
        // Flying thrust starts at motion frame 1; its separate hit/landing cue
        // is at frame 9. Preserve the lower start rather than adding hit mass.
        ("job07_skill_m.sbnk.1.x64",39419737 or 195578121) => ContactDetail,
        ("job07_skill_m.sbnk.1.x64",345682821) => ContactTraceFine,
        // Possession transfer cues at frames 36/55 retain source alternatives.
        // Their moving smoke detail is distinct from its long low-rich body.
        ("job09_skill_m.sbnk.1.x64",239190277) => Release,
        ("job09_skill_m.sbnk.1.x64",1044700297) => ReleaseTraceWide,
        ("job09_skill_m.sbnk.1.x64",305549351) => ContactDetail,
        // WarCry's later low-rich onset also belongs to ScarletVow. Preserve
        // that shared ownership, with a broader source-derived upper layer.
        ("job05_skill_m.sbnk.1.x64",101275906 or 197289337) => Body,
        // Fire-up: the broader alternative differs from the mostly-low branch;
        // neither replaces the independent frame-53 contact already selected.
        ("job05_skill_m.sbnk.1.x64",172791772) => Body,
        ("job05_skill_m.sbnk.1.x64",227062006) => MidHeld,
        // PsychoShootConst shell onset is separate from the throwing-hand clock.
        ("job07_skill_m.sbnk.1.x64",297099686) => ContactDetail,
        // TwoSeconds has onset at frame 9 and a later mid-rich cue at frame 60.
        // Do not turn the later cue into a large lingering impact.
        ("job07_skill_m.sbnk.1.x64",289176026) => Body,
        ("job07_skill_m.sbnk.1.x64",304753347) => MidHeld,
        // LifeReturn support contact is a longer mid-rich response, separate
        // from the short launch. Its high-rich flight sources are shared with
        // SpiritArrow and must retain one translation across both owners.
        ("job08_skill_m.sbnk.1.x64",549008567 or 1063217589) => ContactDetail,
        ("job08_skill_m.sbnk.1.x64",47083223 or 941327261) => MidHeld,
        ("job08_skill_m.sbnk.1.x64",32309412 or 100889346 or 255081053 or 612252421) => FineHeld,
        // CounterArrow's shell exposes StartSeNo only: a shield deployment,
        // not a fabricated per-block or reflected-hit pulse.
        ("job08_skill_m.sbnk.1.x64",351117362 or 441740009 or 959265494) => Body,
        // SeriesArrow start/loop/finish requests share actual source sets.
        // The event graph supplies repeated shots; no metronome is introduced.
        ("job08_skill_m.sbnk.1.x64",320471102 or 410276596 or 713233965 or 917907846) => Release,
        ("job08_skill_m.sbnk.1.x64",639770996) => ContactDetail,
        ("job08_skill_m.sbnk.1.x64",16979779 or 358924812 or 467501900 or 854643532 or 1051780576) => MidHeld,
        ("job08_skill_m.sbnk.1.x64",88036604 or 106233797 or 294076559 or 896982060 or 907876429) => ContactDetail,
        // SpiritArrow's own charge motion retains its low pressure; projectile
        // release and compact contacts differ from its shared fine flight.
        ("job08_skill_m.sbnk.1.x64",623623213 or 1009205120 or 972015845) => Body,
        ("job08_skill_m.sbnk.1.x64",849379511) => Release,
        ("job08_skill_m.sbnk.1.x64",69608656 or 495205846 or 591331663 or 958833483) => ContactDetail,
        // HolyGlare/Celestial Paean: low activation and charge-end cues frame
        // the separate mid/upper charge-loop layers. Preserve the named stop.
        ("mgc_skill_m.sbnk.1.x64",153648358 or 665713204) => BodyDetail,
        ("mgc_skill_m.sbnk.1.x64",411566016) => MidHeld,
        ("mgc_skill_m.sbnk.1.x64",721072129) => FineHeld,
        // QuickShield's recorded lower layer and mid-rich layer are longer
        // than CounterArrow deployment. Keep their original repeated keys.
        ("job07_skill_m.sbnk.1.x64",824263067) => Body,
        ("job07_skill_m.sbnk.1.x64",876347342) => MidHeld,
        // Snatch/powder-plant share this upper-rich hand-action source. Faster
        // envelopes preserve its phase; Release shifted its center by 48 ms.
        ("job04_skill_m.sbnk.1.x64",502600759) => ContactCrestFine,
        // CycloneSlash start and uppercut-hit share this low-rich source.
        // Expose existing fine detail without replacing protected HD siblings.
        ("job05_skill_m.sbnk.1.x64",193868319) => Contact,
        // Compact smoke openings, attached-explosion edges and stops preserve source phase; broader division inflated late energy.
        ("job04_skill_m.sbnk.1.x64",450762066) => ContactDetail,
        ("job09_skill_m.sbnk.1.x64",370570392 or 456538312 or 812033123 or 1005241933) => ContactDetail,
        ("mgc_skill_m.sbnk.1.x64",1066026579 or 171195892 or 789178465 or 619326307 or 446189484 or 455196819 or 921539679) => ContactDetail,
        // Separate attachment and charge-end cues follow their own short recorded contours.
        ("mgc_skill_m.sbnk.1.x64",370214632 or 1061424583 or 396207139 or 723330773) => Release,
        // Flare expansion and one vortex layer retain broader source-derived texture without a new pulse.
        ("mgc_skill_m.sbnk.1.x64",386558063) => FlareExpansion,
        ("mgc_skill_m.sbnk.1.x64",147436509 or 1005151400) => Body,
        // Moving vortex and smoke detail keep a sustained follower and their authored event clocks.
        ("job09_skill_m.sbnk.1.x64",99413873 or 677923955) => Flight,
        ("mgc_skill_m.sbnk.1.x64",56340893 or 176309447 or 531082884) => Flight,
        // Fine charge layers preserve the phase trajectory and change only their upper follower.
        ("mgc_skill_m.sbnk.1.x64",380912175 or 194125552) => FineDetail,
        // Long attached magic and smoke retain their low body and phase; a quicker middle follower exposes existing movement.
        ("job09_skill_m.sbnk.1.x64",143537381 or 105432824 or 319632143 or 284916788 or 584447361 or 628253529 or 884377445 or 1006306800) => MidHeld,
        ("mgc_skill_m.sbnk.1.x64",801606636 or 989369542 or 723778349 or 748283206 or 566542092 or 586134543 or 472378482) => MidHeld,
        // Smoke loops 255548853/274484173 remain default: almost entirely low-band,
        // with no supported need to disturb their paired source contour.
        // Element-core effects: actual shell start/loop/hit/end sources retain
        // authored clocks and bass. Long fire/absorption use source-phase middle
        // detail, ice loops change the upper translation, and rock debris keeps
        // its long contour. These rules never add a timed impact or heartbeat.
        ("mgc_skill_m.sbnk.1.x64",159003859) => Flight,
        ("mgc_skill_m.sbnk.1.x64",347568312 or 452834812 or 279916062 or 1015676210) => Release,
        ("mgc_skill_m.sbnk.1.x64",1073394918) => ReleaseTraceFine,
        ("mgc_skill_m.sbnk.1.x64",615248685 or 573257190 or 842782643 or 1035801933 or 936953378 or 624900050 or 358388408 or 839481045 or 414311981) => Body,
        ("mgc_skill_m.sbnk.1.x64",725605077) => RockLayerDetail,
        ("mgc_skill_m.sbnk.1.x64",138320457) => RockBodyGrain,
        ("mgc_skill_m.sbnk.1.x64",529251104 or 1008268542 or 743433655) => ContactDetail,
        ("mgc_skill_m.sbnk.1.x64",671695699) => RockContactGrain,
        ("mgc_skill_m.sbnk.1.x64",248400914 or 732159538) => MidHeld,
        ("mgc_skill_m.sbnk.1.x64",469623245) => IceLoopGrain,
        ("mgc_skill_m.sbnk.1.x64",663840840) => IceHeld,
        ("mgc_skill_m.sbnk.1.x64",421481169) => Contact,
        ("mgc_skill_m.sbnk.1.x64",501481644) => FineHeld,
        ("mgc_skill_m.sbnk.1.x64",925067202) => RockFineGrain,
        ("mgc_skill_m.sbnk.1.x64",823605932) => RockFineGrain,
        // Counter response/follow-through and spear launch/absorption/charge
        // have separate typed motion keys. Short recorded edges differ from
        // sustained mid-rich transfer; preserve shared sources and original clocks.
        ("job05_skill_m.sbnk.1.x64",675546022 or 183633909 or 1045459965) => ContactDetail,
        ("job07_skill_m.sbnk.1.x64",149243019 or 826136788) => ContactDetail,
        ("job05_skill_m.sbnk.1.x64",1059708204) => MidHeld,
        ("job07_skill_m.sbnk.1.x64",1060068872 or 47941028 or 977868607) => MidHeld,
        ("job05_skill_m.sbnk.1.x64",337175237 or 698751331) => Release,
        ("job07_skill_m.sbnk.1.x64",910807345 or 363938715 or 504161123) => Release,
        ("job05_skill_m.sbnk.1.x64",818502827) => ContactTrace,
        ("job07_skill_m.sbnk.1.x64",284828995 or 758357539) => Contact,
        ("job05_skill_m.sbnk.1.x64",223410493) => Body,
        ("job07_skill_m.sbnk.1.x64",694559054) => Body,
        ("job07_skill_m.sbnk.1.x64",14434967) => FineHeld,
        ("job07_skill_m.sbnk.1.x64",266100001) => Flight,
        // Rising/circular sword contacts retain phase where re-division thinned
        // their body. Guard and multi-hit bow sustain keep their source trajectory.
        // The explicit Job10 weapon-change audio cue retains its recorded timing;
        // this rule does not synthesize a controller switch event.
        ("job01_skill_m.sbnk.1.x64",130779092 or 939560248) => ContactDetail,
        ("job01_skill_m.sbnk.1.x64",708347800 or 968373638) => Release,
        ("job10_skill_m.sbnk.1.x64",691916506) => Release,
        ("job01_skill_m.sbnk.1.x64",480356483) => Flight,
        ("job02_skill_m.sbnk.1.x64",444790218) => Flight,
        ("job01_skill_m.sbnk.1.x64",375877526) => MidHeld,
        ("job02_skill_m.sbnk.1.x64",177035179) => MidHeld,
        // Unlisted sources include native-HD-shared sounds, shared preparation,
        // thunder and unchanged low-dominant actor/preparation reference controls.
        _ => Default
    };
}
