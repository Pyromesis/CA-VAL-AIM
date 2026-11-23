using CaValAim.Core;

namespace CaValAim.Analysis;

/// <summary>A complete routine and the measured finding that made it relevant to the current profile.</summary>
public sealed record AimRoutineRecommendation(Routine Routine, Diagnosis? Source, SensBlock? Sens);

/// <summary>
/// Selects one full practice routine without changing sensitivity or interpreting a measured pattern as its cause.
/// Confirmed coach findings take priority; the current sensitivity block is only a fallback. Tips, stale history,
/// balanced/thin samples and candidate sensitivity values never create a recommendation on their own.
/// </summary>
public static class AimRoutinePrescription
{
    static readonly IReadOnlyDictionary<string, string> ByDiagnosis =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OVERFLICK"] = "aim-overflick",
            ["OVER_NEAR"] = "aim-overflick",
            ["OVER_WIDE"] = "aim-overflick",
            ["RECOVERED_OVER"] = "aim-overflick",

            ["UNDERFLICK"] = "aim-underflick",

            ["MIXED_FLICK"] = "aim-mixed-flick",
            ["CURVED"] = "aim-mixed-flick",
            ["LEFT_WEAK"] = "aim-mixed-flick",
            ["RIGHT_WEAK"] = "aim-mixed-flick",
            ["UP_WEAK"] = "aim-mixed-flick",
            ["DOWN_WEAK"] = "aim-mixed-flick",

            ["AIM_TREMOR"] = "aim-tremor",
            ["JITTER"] = "aim-tremor",

            ["TRACK_BEHIND"] = "aim-tracking-control",
            ["TRACK_OVERRUN"] = "aim-tracking-control",
            ["SLOW_REACQ"] = "aim-tracking-control",

            ["MICRO_CONFIRM"] = "aim-click-control",
            ["NO_MICRO"] = "aim-click-control",
            ["SLOW_MICRO"] = "aim-click-control",
            ["PREMATURE"] = "aim-click-control",

            ["HESITATE"] = "aim-response-timing",
            ["SLOW_START"] = "aim-response-timing",
            ["SLOW_REACT"] = "aim-response-timing",

            ["TARGET_ORDER"] = "aim-target-order",

            ["XHAIR_LOW"] = "aim-crosshair-placement",
            ["XHAIR_WIDE"] = "aim-crosshair-placement",

            ["RUN_GUN"] = "aim-stop-shoot",
            ["CLUMSY_MOVE"] = "aim-stop-shoot",
            ["MIRRORING"] = "aim-stop-shoot",

            // Spray already has a complete, purpose-built routine; do not duplicate it.
            ["SPRAY_VERT"] = "recoil-lab",
            ["SPRAY_HORIZ"] = "recoil-lab",
        };

    public static AimRoutineRecommendation? For(SkillProfile? profile)
    {
        if (profile == null) return null;

        // Coach.BuildProfile has already ordered problems by importance. Return one routine, never a stack of sessions.
        foreach (var finding in profile.Problems)
        {
            if (finding.Severity == Severity.Tip) continue;
            if (!ByDiagnosis.TryGetValue(finding.Id, out string? id)) continue;
            if (RoutineLibrary.Find(id) is { } routine) return new AimRoutineRecommendation(routine, finding, null);
        }

        // Only the block for the sensitivity currently in use is relevant. Never borrow a verdict from History.
        var current = profile.SensLab?.Current;
        if (current is not { HasData: true } || !SensLab.Enough(current)) return null;
        string? routineId = current.Verdict switch
        {
            SensVerdict.Overshoot => "aim-overflick",
            SensVerdict.Undershoot => "aim-underflick",
            SensVerdict.Mixed => "aim-mixed-flick",
            SensVerdict.Shaky => "aim-tremor",
            SensVerdict.Tracking => "aim-tracking-control",
            _ => null,
        };
        return routineId != null && RoutineLibrary.Find(routineId) is { } selectedRoutine
            ? new AimRoutineRecommendation(selectedRoutine, null, current)
            : null;
    }
}
