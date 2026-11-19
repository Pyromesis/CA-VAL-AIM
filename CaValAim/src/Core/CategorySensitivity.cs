using CaValAim.Modes;

namespace CaValAim.Core;

/// <summary>A sensitivity in VALORANT units, tied to the DPI at which it was chosen.</summary>
public sealed record CategorySensitivity(float Value, int Dpi, bool Verified)
{
    public float AtDpi(int dpi) => Dpi > 0 && dpi > 0 ? Value * Dpi / dpi : Value;
}

/// <summary>Last experimental value, anchored to the profile and DPI used when it was measured.</summary>
public sealed record SensitivitySearchValue(float Baseline, float Candidate, int Dpi)
{
    public bool Matches(float baseline, int dpi) => Dpi == dpi && Baseline == baseline
        && SensitivityProfiles.Valid(Candidate) && Candidate >= baseline * .88f && Candidate <= baseline * 1.12f;
}

public readonly record struct GeneralSensitivity(float Value, int Count, int Verified, float Low, float High, int Tests);

public static class SensitivityProfiles
{
    public static bool Valid(float value) => float.IsFinite(value) && value is >= SensInput.Min and <= SensInput.Max;
    public static bool Supports(string section) => section != "ReactionGames" && TrainingCatalog.Sections.Contains(section);
    public static string For(TrainingMode mode) => mode.Key is "sensfinder" or "xhairfinder"
        ? "" : TrainingCatalog.SectionOf(mode);

    public static float Resolve(IReadOnlyDictionary<string, CategorySensitivity>? profiles, string section, int dpi, float fallback)
    {
        float value = Supports(section) && profiles != null && profiles.TryGetValue(section, out var profile) && profile != null
            ? profile.AtDpi(dpi) : 0;
        return Valid(value) ? value : fallback;
    }

    public static GeneralSensitivity Combine(IReadOnlyDictionary<string, CategorySensitivity>? profiles, int dpi,
        IEnumerable<SensitivityTestResult>? history = null, int? tier = null, DateTime? nowUtc = null)
    {
        DateTime now = nowUtc ?? DateTime.UtcNow;
        var tests = (history ?? Enumerable.Empty<SensitivityTestResult>()).Where(t =>
            t.Dpi == dpi && (tier == null || t.Tier == tier) && Supports(t.Category) && t.Blocks is { Length: 4 }
            && Valid(t.Baseline) && Valid(t.Candidate))
            .GroupBy(t => (t.Category, t.Drill, Day: t.WhenUtc.Date,
                Low: Math.Min(t.Baseline, t.Candidate), High: Math.Max(t.Baseline, t.Candidate)))
            .Select(g => g.OrderByDescending(t => t.WhenUtc).First()).Where(t => t.Preferred is -1 or 1)
            .ToLookup(t => t.Category);
        var estimates = new List<(double Value, double Weight, bool Verified, int Tests)>();
        foreach (string section in TrainingCatalog.Sections.Where(Supports))
        {
            var profile = profiles?.GetValueOrDefault(section);
            float anchor = profile?.AtDpi(dpi) ?? 0;
            double mass = Valid(anchor) ? 1 : 0;
            double log = mass > 0 ? Math.Log(anchor) : 0;
            double evidence = 0;
            int observations = 0;
            foreach (var test in tests[section])
            {
                float consistent = test.Preferred * (test.Preferred > 0
                    ? Math.Min(test.PairGain(0), test.PairGain(2)) : Math.Max(test.PairGain(0), test.PairGain(2)));
                if (!float.IsFinite(consistent) || consistent <= 0) continue;
                double recency = Math.Pow(.5, Math.Max(0, (now - test.WhenUtc).TotalDays) / 60);
                double weight = Math.Clamp(consistent / .05, .25, 2) * recency;
                if (weight < .05) continue;
                log += weight * Math.Log(test.Preferred > 0 ? test.Candidate : test.Baseline);
                evidence += weight;
                observations++;
            }
            if (mass + evidence == 0) continue;
            estimates.Add((Math.Exp(log / (mass + evidence)), 1 + Math.Min(1, evidence / 3),
                observations > 0 || (profile is { Verified: true } && profile.Dpi == dpi), observations));
        }
        if (estimates.Count < 2) return default;
        double combined = Math.Exp(estimates.Sum(p => p.Weight * Math.Log(p.Value)) / estimates.Sum(p => p.Weight));
        return new(MathF.Round((float)combined, 4), estimates.Count, estimates.Count(p => p.Verified),
            (float)estimates.Min(p => p.Value), (float)estimates.Max(p => p.Value), estimates.Sum(p => p.Tests));
    }
}
