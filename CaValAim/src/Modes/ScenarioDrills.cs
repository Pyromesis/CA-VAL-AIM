using CaValAim.Core;

namespace CaValAim.Modes;

/// <summary>
/// The Kovaak's / Aim Lab scenarios that cartoon's two playlists ask for, reproduced with the parameterised engines in
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
            PitchMin = pitchMin, PitchMax = pitchMax,
        };

    static AimVariant M(string id, string name, string desc,
        float radius = 0.12f, float distMin = 10f, float distMax = 15f, AimPattern pattern = AimPattern.Random,
        int count = 1, float offMin = 0.6f, float offMax = 2.6f, float life = 0f, float drift = 0f) =>
        new("sc-" + id, AimFamily.Micro, name, desc)
        {
            Radius = radius, DistMin = distMin, DistMax = distMax, Pattern = pattern, Count = count,
            OffMin = offMin, OffMax = offMax, Life = life, Drift = drift,
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
            Window = window, Count = count,
        };

    /// <summary>cartoon · Flicks (KovaaKsReloadingMaroonLamp, the code kept in Routines.cs), eight scenarios, four reps each.</summary>
    public static readonly AimVariant[] CartoonFlicks =
    {
        F("cartoon-ww2t-voltaic-horizontalish", "ww2t Voltaic Horizontalish",
            "Two targets on the head line: short horizontal flicks between them, no vertical detour.",
            radius: 0.16f, pattern: AimPattern.Horizontal, count: 2, yaw: 38f, minAngle: 8f),
        F("cartoon-vox-ts-voltaic-25", "voxTS Voltaic 25% Larger",
            "Target switching on 25% larger targets: acquire, kill and move before the next one settles.",
            radius: 0.24f, count: 3, yaw: 36f, drift: 2.2f, minAngle: 6f),
        F("cartoon-pokeball-frenzy-wide-4-small", "Pokeball Frenzy Auto Wide Wall 4 Targets Small",
            "Four small targets across a wide wall: hold fire, plan the next target, keep the line clean.",
            radius: 0.13f, pattern: AimPattern.Wide, count: 4, yaw: 55f, minAngle: 12f, pitchMin: -2f, pitchMax: 6f),
        F("cartoon-1w2ts-perfected", "1w2ts Perfected",
            "One wall, two targets, one shot each: the cleanest possible switch between two small heads.",
            radius: 0.14f, count: 2, yaw: 30f, minAngle: 5f),
        F("cartoon-float-ts-angelic-easy", "FloatTS Angelic Easy",
            "A forgiving floating-target switch: read the drift, flick once and reset for the next head.",
            radius: 0.22f, count: 3, yaw: 34f, drift: 2.0f, minAngle: 6f),
        F("cartoon-floating-heads-400-fixed", "Floating Heads Timing 400% FIXED",
            "Moving heads at 400% speed: time the click as the head crosses the crosshair; never chase the trail.",
            radius: 0.18f, yaw: 26f, drift: 3.0f, minAngle: 5f),
        F("cartoon-dev-ts-static-angelic", "devTS Static Click Angelic",
            "Static clicking with angelic-sized targets: one tap per head and a fast, clean switch.",
            radius: 0.2f, count: 3, yaw: 30f, minAngle: 6f),
        F("cartoon-1w2ts-pasu-easy", "1w2ts Pasu Perfected Easy",
            "Pasu-style two-target switching with a forgiving size: click, confirm and move on.",
            radius: 0.15f, count: 2, yaw: 28f, minAngle: 5f),
    };

    /// <summary>cartoon · Microadjustments (KovaaKsScreamingMehCt, the code kept in Routines.cs), twelve scenarios, four reps each.</summary>
    public static readonly AimVariant[] CartoonMicro =
    {
        F("cartoon-pokeball-wide-small", "Pokeball Frenzy Auto Wide Small",
            "Four small targets on a wide wall: micro-correct after each switch instead of over-flicking.",
            radius: 0.13f, pattern: AimPattern.Wide, count: 4, yaw: 50f, minAngle: 10f),
        F("cartoon-vox-ts-raim", "voxTS rAim",
            "Small target switches with a short correction: the second movement should be the tiny one.",
            radius: 0.16f, count: 3, yaw: 30f, drift: 1.8f, minAngle: 5f),
        T("cartoon-waldots", "waldoTS",
            "A wandering tracking target: follow the direction change with the arm and keep the dot on it.",
            radius: 0.2f, distMin: 9f, distMax: 14f, motion: TrackMotion.Wander, speed: 3.6f, width: 5f),
        F("cartoon-1w2ts-pokeball-pasu-narrow-higherttk", "1w2ts Pokeball Pasu Narrow HigherTTK",
            "Two narrow targets with more time to kill: settle on the head and make the correction count.",
            radius: 0.13f, count: 2, yaw: 20f, minAngle: 4f, life: 2.4f),
        F("cartoon-floating-heads-400", "Floating Heads Timing 400%",
            "A moving head at high speed: click when it meets the crosshair, not after it has passed.",
            radius: 0.18f, yaw: 26f, drift: 3.0f, minAngle: 5f),
        F("cartoon-1w2ts-pasu-horizontal", "1w2ts Pasu Horizontal",
            "Two targets on one horizontal line: a clean side-to-side micro-flick, no vertical noise.",
            radius: 0.14f, pattern: AimPattern.Horizontal, count: 2, yaw: 30f, minAngle: 4f),
        M("cartoon-1w3ts-pasu-micro-goated", "1w3ts Pasu Perfected Micro Goated",
            "Three-target Pasu pattern reduced to the last few degrees: chain tiny corrections without lifting the arm.",
            radius: 0.12f, count: 3, offMin: 0.5f, offMax: 2.2f, life: 1.1f),
        F("cartoon-tam-ts-control", "tamTargetSwitch Control",
            "Controlled target switching: one clean flick, a short confirm, then the next target.",
            radius: 0.16f, count: 3, yaw: 28f, drift: 1.6f, minAngle: 5f),
        F("cartoon-tam-ts-control-hard", "tamTargetSwitch Control Hard",
            "The hard version: faster drift and smaller heads, so the correction has to be immediate.",
            radius: 0.12f, count: 3, yaw: 32f, drift: 2.8f, minAngle: 6f),
        F("cartoon-pokeball-1w2ts-horizontalish-small", "Pokeball 1w2ts Horizontalish small",
            "Two small heads nearly level with each other: keep the crosshair on the line and correct sideways.",
            radius: 0.13f, pattern: AimPattern.Horizontal, count: 2, yaw: 32f, minAngle: 4f),
        F("cartoon-ww3t-voltaic-50-smaller", "ww3t Voltaic 50% smaller",
            "A wide three-target wall at half size: the flick still has to stop exactly on the head.",
            radius: 0.11f, pattern: AimPattern.Wide, count: 3, yaw: 42f, minAngle: 9f),
        F("cartoon-1w2ts-pasu-30-smaller", "1w2ts Pasu Perfected 30% Smaller",
            "Two heads 30% smaller than the original: the last correction is a few pixels, not a new flick.",
            radius: 0.1f, count: 2, yaw: 26f, minAngle: 4f),
    };

    public static readonly AimVariant[] All = CartoonFlicks.Concat(CartoonMicro).ToArray();

    public static AimVariant? Find(string id) =>
        All.FirstOrDefault(v => v.Id.Equals("sc-" + id, StringComparison.OrdinalIgnoreCase));
}
