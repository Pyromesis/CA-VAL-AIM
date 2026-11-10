using CaValAim.Core;

namespace CaValAim.Modes;

/// <summary>
/// The Kovaak's / Aim Lab scenarios that the built-in routines ask for, reproduced with the parameterised engines in
/// <see cref="AimVariants"/>. The names are the exact source names, kept for attribution: the implementation is our own
/// approximation on CA-VAL-AIM's targets, not the original map. Every entry is a real, playable drill.
/// </summary>
public static class ScenarioDrills
{
    static AimVariant F(string id, string name, string desc,
        float radius = 0.14f, float distMin = 10f, float distMax = 14f, AimPattern pattern = AimPattern.Random,
        int count = 1, float yaw = 25f, float minAngle = 6f, float life = 0f, float drift = 0f,
        float pitchMin = -3f, float pitchMax = 8f) =>
        new("sc-" + id, AimFamily.Flick, name, desc)
        {
            Radius = radius, DistMin = distMin, DistMax = distMax, Pattern = pattern, Count = count,
            Yaw = yaw, MinAngle = minAngle, Life = life, Drift = drift,
            PitchMin = pitchMin, PitchMax = pitchMax, TapOnly = true,
        };

    static AimVariant M(string id, string name, string desc,
        float radius = 0.12f, float distMin = 10f, float distMax = 15f, AimPattern pattern = AimPattern.Random,
        int count = 1, float offMin = 0.6f, float offMax = 2.6f, float life = 0f, float drift = 0f) =>
        new("sc-" + id, AimFamily.Micro, name, desc)
        {
            Radius = radius, DistMin = distMin, DistMax = distMax, Pattern = pattern, Count = count,
            OffMin = offMin, OffMax = offMax, Life = life, Drift = drift, TapOnly = true,
        };

    static AimVariant T(string id, string name, string desc,
        float radius = 0.24f, float distMin = 8f, float distMax = 14f, TrackMotion motion = TrackMotion.Strafe,
        float speed = 4.4f, float width = 8f, float height = 1.2f, float turnMin = 0.4f, float turnMax = 1.2f,
        float stopChance = 0f) =>
        new("sc-" + id, AimFamily.Track, name, desc)
        {
            Radius = radius, DistMin = distMin, DistMax = distMax, Motion = motion, Speed = speed,
            Width = width, Height = height, TurnMin = turnMin, TurnMax = turnMax, StopChance = stopChance,
        };

    static AimVariant X(string id, string name, string desc,
        float radius = 0.14f, float distMin = 8f, float distMax = 14f, float yaw = 25f, float minAngle = 5f,
        float window = 0.7f, int count = 1) =>
        new("sc-" + id, AimFamily.Reflex, name, desc)
        {
            Radius = radius, DistMin = distMin, DistMax = distMax, Yaw = yaw, MinAngle = minAngle,
            Window = window, Count = count, TapOnly = true,
        };

    /// <summary>cartoon · Flicks (KovaaKsReloadingMaroonLamp, the code kept in Routines.cs), eight scenarios, four reps each.</summary>
    public static readonly AimVariant[] CartoonFlicks =
    {
        F("cartoon-ww2t-voltaic-horizontalish", "ww2t Voltaic Horizontalish",
            "Two targets on the head line: short horizontal flicks between them, no vertical detour.",
            radius: 0.16f, pattern: AimPattern.Horizontal, count: 2, yaw: 38f, minAngle: 8f) with { Walls = 1 },
        F("cartoon-vox-ts-voltaic-25", "voxTS Voltaic 25% Larger",
            "Target switching on 25% larger targets: acquire, kill and move before the next one settles.",
            radius: 0.24f, count: 3, yaw: 36f, drift: 2.2f, minAngle: 6f) with { ContactSeconds = 0.12f },
        F("cartoon-pokeball-frenzy-wide-4-small", "Pokeball Frenzy Auto Wide Wall 4 Targets Small",
            "Four small targets across a wide wall: hold fire, plan the next target, keep the line clean.",
            radius: 0.13f, pattern: AimPattern.Wide, count: 4, yaw: 55f, minAngle: 12f, pitchMin: -2f, pitchMax: 6f) with { Walls = 1, ContactSeconds = 0.04f },
        F("cartoon-1w2ts-perfected", "1w2ts Perfected",
            "One wall, two targets, one shot each: the cleanest possible switch between two small heads.",
            radius: 0.14f, count: 2, yaw: 30f, minAngle: 5f) with { Walls = 1 },
        F("cartoon-float-ts-angelic-easy", "FloatTS Angelic Easy",
            "A forgiving floating-target switch: read the drift, flick once and reset for the next head.",
            radius: 0.22f, count: 3, yaw: 34f, drift: 2.0f, minAngle: 6f) with { VerticalDrift = 0.6f, ContactSeconds = 0.12f },
        F("cartoon-floating-heads-400-fixed", "Floating Heads Timing 400% FIXED",
            "Moving heads at 400% speed: time the click as the head crosses the crosshair; never chase the trail.",
            radius: 0.18f, yaw: 26f, drift: 3.0f, minAngle: 5f),
        F("cartoon-dev-ts-static-angelic", "devTS Static Click Angelic",
            "Static clicking with angelic-sized targets: one tap per head and a fast, clean switch.",
            radius: 0.2f, count: 3, yaw: 30f, minAngle: 6f),
        F("cartoon-1w2ts-pasu-easy", "1w2ts Pasu Perfected Easy",
            "Pasu-style two-target switching with a forgiving size: click, confirm and move on.",
            radius: 0.15f, count: 2, yaw: 28f, minAngle: 5f, drift: 1.8f) with { Walls = 1, VerticalDrift = 0.45f },
    };

    /// <summary>cartoon · Microadjustments (KovaaKsScreamingMehCt, the code kept in Routines.cs), twelve scenarios, four reps each.</summary>
    public static readonly AimVariant[] CartoonMicro =
    {
        F("cartoon-pokeball-wide-small", "Pokeball Frenzy Auto Wide Small",
            "Four small targets on a wide wall: micro-correct after each switch instead of over-flicking.",
            radius: 0.13f, pattern: AimPattern.Wide, count: 4, yaw: 50f, minAngle: 10f) with { Walls = 1, ContactSeconds = 0.04f },
        F("cartoon-vox-ts-raim", "voxTS rAim",
            "Small target switches with a short correction: the second movement should be the tiny one.",
            radius: 0.16f, count: 3, yaw: 30f, drift: 1.8f, minAngle: 5f),
        T("cartoon-waldots", "waldoTS",
            "A wandering tracking target: follow the direction change with the arm and keep the dot on it.",
            radius: 0.2f, distMin: 9f, distMax: 14f, motion: TrackMotion.Wander, speed: 3.6f, width: 5f),
        F("cartoon-1w2ts-pokeball-pasu-narrow-higherttk", "1w2ts Pokeball Pasu Narrow HigherTTK",
            "Two narrow targets with more time to kill: settle on the head and make the correction count.",
            radius: 0.13f, count: 2, yaw: 20f, minAngle: 4f, drift: 1.3f) with { Walls = 1, VerticalDrift = 0.42f, ContactSeconds = 0.16f },
        F("cartoon-floating-heads-400", "Floating Heads Timing 400%",
            "A moving head at high speed: click when it meets the crosshair, not after it has passed.",
            radius: 0.18f, yaw: 26f, drift: 3.0f, minAngle: 5f),
        F("cartoon-1w2ts-pasu-horizontal", "1w2ts Pasu Horizontal",
            "Two targets on one horizontal line: a clean side-to-side micro-flick, no vertical noise.",
            radius: 0.14f, pattern: AimPattern.Horizontal, count: 2, yaw: 30f, minAngle: 4f, drift: 1.6f) with { Walls = 1 },
        M("cartoon-1w3ts-pasu-micro-goated", "1w3ts Pasu Perfected Micro Goated",
            "Three-target Pasu pattern reduced to the last few degrees: chain tiny corrections without lifting the arm.",
            radius: 0.12f, count: 3, offMin: 0.5f, offMax: 2.2f, life: 1.1f, drift: 0.8f) with { VerticalDrift = 0.28f },
        F("cartoon-tam-ts-control", "tamTargetSwitch Control",
            "Controlled target switching: one clean flick, a short confirm, then the next target.",
            radius: 0.16f, count: 3, yaw: 28f, drift: 1.6f, minAngle: 5f)
            with { ContactSeconds = 0.5f, RegenDelay = 0.3f, RegenSeconds = 1f, DriftWidth = 0.65f, VerticalDrift = 0.2f },
        F("cartoon-tam-ts-control-hard", "tamTargetSwitch Control Hard",
            "The hard version: faster drift and smaller heads, so the correction has to be immediate.",
            radius: 0.12f, count: 3, yaw: 32f, drift: 2.8f, minAngle: 6f)
            with { ContactSeconds = 0.5f, RegenDelay = 0.3f, RegenSeconds = 1f, DriftWidth = 0.65f, VerticalDrift = 0.2f },
        F("cartoon-pokeball-1w2ts-horizontalish-small", "Pokeball 1w2ts Horizontalish small",
            "Two small heads nearly level with each other: keep the crosshair on the line and correct sideways.",
            radius: 0.13f, pattern: AimPattern.Horizontal, count: 2, yaw: 32f, minAngle: 4f) with { Walls = 1, ContactSeconds = 0.04f },
        F("cartoon-ww3t-voltaic-50-smaller", "ww3t Voltaic 50% smaller",
            "A wide three-target wall at half size: the flick still has to stop exactly on the head.",
            radius: 0.11f, pattern: AimPattern.Wide, count: 3, yaw: 42f, minAngle: 9f) with { Walls = 1 },
        F("cartoon-1w2ts-pasu-30-smaller", "1w2ts Pasu Perfected 30% Smaller",
            "Two heads 30% smaller than the original: the last correction is a few pixels, not a new flick.",
            radius: 0.1f, count: 2, yaw: 26f, minAngle: 4f, drift: 1.7f) with { Walls = 1, VerticalDrift = 0.5f },
    };

    /// <summary>
    /// The official Voltaic / Kovaak's / Aim Lab tasks of the built-in routines, one entry per source scenario. The
    /// source lists a few alternatives in the same step ("A / B"); the routine keeps the full text and plays the first
    /// one here. Sizes and speeds are our reading of the task name, not the original map values.
    /// </summary>
    public static readonly AimVariant[] Official =
    {
        // ---- Voltaic Easy / Advanced Classic ----
        T("voltaic-thin-gauntlet-easy", "Thin Gauntlet Easy",
            "A thin target strafing at a forgiving speed: keep the crosshair on it through the whole lane.",
            radius: 0.18f, speed: 3.6f, width: 10f, turnMin: 0.6f, turnMax: 1.5f),
        T("voltaic-thin-gauntlet", "Thin Gauntlet",
            "The small, faster Thin Gauntlet: smooth tracking with short reversals and no overcorrection.",
            radius: 0.13f, speed: 5f, width: 12f, turnMin: 0.35f, turnMax: 0.95f),
        T("voltaic-centering-i-easy", "Centering I Easy",
            "Horizontal centering at an easy pace: keep the target centred instead of letting it drift to an edge.",
            radius: 0.28f, speed: 4f, width: 8f, turnMin: 0.5f, turnMax: 1.3f),
        T("voltaic-centering-i", "Centering I",
            "The original Centering I: a smaller, quicker strafe and a clean horizontal read.",
            radius: 0.22f, speed: 4.6f, width: 10f, turnMin: 0.4f, turnMax: 1.05f),
        F("voltaic-pat-target-switch", "patTargetSwitch (no reload)",
            "Three strafing targets: hold fire, stay on each head and switch when it clears.",
            radius: 0.18f, count: 3, yaw: 30f, minAngle: 6f, drift: 2.2f) with { ContactSeconds = 0.12f },
        F("voltaic-pat-target-switch-small", "patTargetSwitch small no reload",
            "Small strafing heads: maintain contact before switching to the next head.",
            radius: 0.13f, count: 3, yaw: 30f, minAngle: 5f, drift: 2.2f) with { ContactSeconds = 0.12f },
        F("voltaic-pokeball-frenzy-te", "Pokeball Frenzy Auto TE 60s",
            "Static Pokeball targets: hold fire and briefly settle on each before switching.",
            radius: 0.18f, count: 3, yaw: 34f, minAngle: 6f) with { Walls = 1, ContactSeconds = 0.04f },
        F("voltaic-pokeball-frenzy-small", "Pokeball Frenzy Auto Small 60s",
            "Smaller static Pokeball targets: settle precisely and switch as soon as the target clears.",
            radius: 0.13f, count: 3, yaw: 30f, minAngle: 5f) with { Walls = 1, ContactSeconds = 0.04f },
        F("voltaic-vox-target-switch-click", "voxTargetSwitch click",
            "Four static targets and a click on every switch: plan the next target before the current one dies.",
            radius: 0.17f, count: 4, yaw: 30f, minAngle: 6f, life: 1f),
        F("voltaic-wide-wall-3", "Wide Wall 3 Targets",
            "Three targets across a wide wall: long flicks that stop dead on the head.",
            radius: 0.18f, pattern: AimPattern.Wide, count: 3, yaw: 50f, minAngle: 12f) with { Walls = 1 },
        F("voltaic-floating-heads-easy", "Floating Heads Timing Easy",
            "A slowly drifting head: read it, flick once and click as it reaches the crosshair.",
            radius: 0.2f, yaw: 26f, minAngle: 6f, drift: 1.4f, life: 1.2f),
        F("voltaic-vox-target-click-strafe", "voxTargetClick Easy Strafe",
            "Heads that strafe while you click them: lead the movement slightly, never chase the trail.",
            radius: 0.17f, yaw: 30f, minAngle: 6f, drift: 2.4f, life: 1.4f) with { SideStep = true },
        X("voltaic-reflex-micro-flick", "Reflex Micro++ Flick Reload",
            "A head pops after a short pause: react, make the small flick and let the micro-correction finish it.",
            radius: 0.15f, yaw: 18f, minAngle: 4f, window: 0.75f),
        M("voltaic-microshot-speed-medium", "Microshot Speed Medium",
            "Medium micro-steps at speed: one quick correction between heads, no second guess.",
            radius: 0.12f, offMin: 1f, offMax: 3f),
        X("voltaic-reactive-clicking", "Reactive Clicking",
            "Spot the head, flick to it and land both clicks before it disappears.",
            radius: 0.18f, yaw: 30f, minAngle: 8f, window: 0.7f) with { HitsToClear = 2 },
        F("voltaic-vox-target-click-small", "voxTargetClick 20% small",
            "The small voxTargetClick: 20% smaller heads, the same demand for one clean click per target.",
            radius: 0.12f, count: 4, yaw: 30f, minAngle: 5f, life: 0.9f),
        F("voltaic-1w3ts", "1w3ts",
            "One wall, three targets, one shot each: straight lines and an immediate read of the next head.",
            radius: 0.14f, count: 3, yaw: 26f, minAngle: 5f) with { Walls = 1 },
        X("voltaic-reactive-clicking-hard", "Reactive Clicking Hard",
            "The hard reactive task: land both clicks on a smaller head inside a shorter window.",
            radius: 0.13f, yaw: 30f, minAngle: 6f, window: 0.5f) with { HitsToClear = 2 },
        M("voltaic-microshot-speed-small", "Valorant Microshot Speed Small",
            "Valorant-sized small heads with a fast micro-step: stop on the head before adding speed.",
            radius: 0.09f, offMin: 0.8f, offMax: 2.2f),
        X("voltaic-reflex-micro-flick-small", "Reflex Micro++ Flick Reload Small",
            "The small Reflex Micro++: a tiny target and a short window, so the correction has to be exact.",
            radius: 0.12f, yaw: 18f, minAngle: 4f, window: 0.6f),

        // ---- bardOZ ----
        F("voltaic-vox-ts", "voxTS Voltaic",
            "Four drifting vox targets: acquire, kill and switch on a clean line, holding fire.",
            radius: 0.17f, count: 4, yaw: 34f, minAngle: 6f, drift: 1.5f) with { ContactSeconds = 0.12f },
        F("voltaic-pokeball-1w2ts", "Pokeball Frenzy Auto 1w2ts",
            "Two stationary Pokeball targets: switch, correct and maintain a short contact.",
            radius: 0.15f, count: 2, yaw: 26f, minAngle: 5f) with { Walls = 1, ContactSeconds = 0.04f },
        F("voltaic-pokeball-ww6t", "Pokeball auto ww6t",
            "Six stationary targets on a wide wall: move smoothly and speed up when the targets cluster.",
            radius: 0.15f, pattern: AimPattern.Wide, count: 6, yaw: 55f, minAngle: 12f) with { Walls = 1, ContactSeconds = 0.04f },
        F("voltaic-1w2ts-reload", "1w2ts reload",
            "Two targets on one wall: one shot, confirm the kill and switch on the reload rhythm.",
            radius: 0.15f, count: 2, yaw: 26f, minAngle: 5f) with { Walls = 1 },
        F("voltaic-5-sphere-extra-small", "5 sphere hipfire extra small",
            "Extra-small spheres and a minimal micro-step: correct quickly instead of firing again and hoping.",
            radius: 0.08f, count: 5, yaw: 8f, minAngle: 1.5f, pitchMin: -2f, pitchMax: 5f) with { Family = AimFamily.Micro, Walls = 1 },
        F("voltaic-6wall18", "6wall18targets small reload",
            "Small targets on awkward 3D angles: move the mouse only as far as the new target needs.",
            radius: 0.11f, count: 18, yaw: 22f, minAngle: 10f, pitchMin: -6f, pitchMax: 35f) with { Walls = 6 },
        T("voltaic-smoothbot", "SmoothBot Invincible Goated",
            "A smooth, wandering bot: track it with the arm, recentre when needed and do not tense up.",
            radius: 0.35f, motion: TrackMotion.Wander, speed: 3.8f, width: 7f),
        F("voltaic-pistol-strafe-gallery", "Pistol Strafe Gallery Small",
            "A close, fast strafing target: keep moving and fire like a pistol spam, never plant your feet to aim.",
            radius: 0.16f, distMin: 5f, distMax: 8f, count: 3, yaw: 30f, drift: 5.4f) with { Family = AimFamily.Track, SideStep = true },

        // ---- cartoon warm-up / Voltaic VALORANT Advanced ----
        F("vt-berryts", "VT berryTS Valorant",
            "Slow strafing spheres: acquire, track briefly and switch while holding fire.",
            radius: 0.17f, count: 3, yaw: 32f, minAngle: 6f, drift: 1.6f) with { ContactSeconds = 0.12f },
        M("vt-sky-dots-micro", "VT Sky Dots Micro 30% Smaller",
            "Small drifting dots: lead the movement slightly and click when the dot meets the crosshair.",
            radius: 0.09f, count: 2, offMin: 0.6f, offMax: 2.2f, drift: 0.8f),
        F("vt-floating-dots", "VT Floating Dots Goated Valorant",
            "Fast long-strafing dots: read the movement, flick once and click where the dot will be.",
            radius: 0.14f, yaw: 24f, minAngle: 5f, drift: 3.2f, life: 1f),
        M("vt-1w2ts-smallflicks", "VT 1w2ts Smallflicks Valorant",
            "Small flicks between two close heads: skip the correction when they are already on the crosshair.",
            radius: 0.11f, count: 2, offMin: 0.5f, offMax: 2.2f),
        X("vt-reactive-click-widewall", "VT Reactive Click Widewall Valorant",
            "Heads pop wide with no warning: let the eyes find them first, then flick and click before they vanish.",
            radius: 0.16f, yaw: 55f, minAngle: 12f, window: 0.65f, count: 2),
        M("vt-valorant-smallflicks", "VT Valorant Smallflicks",
            "Headshots only on a horizontal line: one clean motion from head to head, no vertical noise.",
            radius: 0.1f, count: 2, offMin: 0.5f, offMax: 2.4f, pattern: AimPattern.Horizontal),
        T("vt-close-range-tracking", "VT Close Range Valorant Tracking",
            "Close-range tracking: commit to the bot's direction in one smooth motion and react to its changes.",
            radius: 0.22f, distMin: 4f, distMax: 8f, speed: 5f, width: 5f),
        F("vt-ww3t-pokeball", "VT ww3t Pokeball Valorant",
            "Three Pokeball targets on a wide wall: hold fire and keep every switch on a straight line.",
            radius: 0.14f, pattern: AimPattern.Wide, count: 3, yaw: 48f, minAngle: 10f) with { Walls = 1, ContactSeconds = 0.04f },
        F("vt-1w2ts-pokeball", "VT 1w2ts Pokeball Valorant",
            "Two small heads on one wall: switch quickly and settle on the target.",
            radius: 0.13f, count: 2, yaw: 26f, minAngle: 5f) with { Walls = 1, ContactSeconds = 0.04f },
        T("vt-smoothsphere", "VT Smoothsphere Valorant",
            "The sphere circles around you: track the curve with the whole mousepad and reset as little as possible.",
            radius: 0.25f, motion: TrackMotion.Orbit, speed: 4.2f, width: 3f),
        F("vt-mpspeedts", "VT MPspeedTS Valorant",
            "Sporadic moving targets: acquire fast, track briefly and switch before the next one settles.",
            radius: 0.13f, count: 3, yaw: 30f, minAngle: 5f, drift: 2.5f, life: 1f) with { ContactSeconds = 0.12f },
        F("vt-mpxy", "VT mpXY Valorant",
            "Diagonal movers: keep the crosshair level and let the target cross it instead of chasing the trail.",
            radius: 0.15f, count: 2, yaw: 38f, minAngle: 6f, drift: 2.3f, pitchMin: -2f, pitchMax: 12f) with { VerticalDrift = 0.7f },
        F("vt-1w4ts-vss", "VT 1w4ts VSS Valorant",
            "Four static heads: a fast large flick, a quick correction and a confirmed kill before the switch.",
            radius: 0.15f, count: 4, yaw: 36f, minAngle: 6f) with { Walls = 1 },
    };

    public static readonly AimVariant[] All = CartoonFlicks.Concat(CartoonMicro).Concat(Official).ToArray();

    public static AimVariant? Find(string id) =>
        All.FirstOrDefault(v => v.Id.Equals("sc-" + id, StringComparison.OrdinalIgnoreCase));
}
