using CaValAim.Modes;
using CaValAim.Core;

namespace CaValAim.Analysis;

/// <summary>Reproducible prescriptions. The saved mode key encodes its layout and demand.</summary>
public static class PersonalDrills
{
    /// <summary>With the player's measured numbers the coach builds the drill from them (<see cref="CoachForge"/>);
    /// without enough measurements it falls back to the authored prescription.</summary>
    public static string? For(Diagnosis issue, IReadOnlyDictionary<string, float>? metrics) =>
        CoachForge.For(issue, metrics) ?? For(issue);

    public static string? For(Diagnosis issue)
    {
        if (issue.Id.StartsWith("REACTION_GAME_", StringComparison.Ordinal)) return issue.Drills.FirstOrDefault();
        string? focus = issue.Id switch {
            "MICRO_CONFIRM" or "NO_MICRO" => "confirm",
            "OVERFLICK" or "RECOVERED_OVER" or "OVER_NEAR" => "over",
            "OVER_WIDE" => "overwide",
            "UNDERFLICK" => "under",
            "MIXED_FLICK" => "mixed",
            _ => null,
        };
        if (focus != null)
            return Resolve($"personal-coach-{focus}-{(issue.Severity == Severity.Major ? 0 : 1)}")?.Key;
        string section = issue.Skill switch
        {
            Skill.Tracking => issue.Id.Contains("JITTER", StringComparison.OrdinalIgnoreCase) ? "Control" : "Reactive",
            Skill.Reaction => "Reflex",
            Skill.Precision => "Micro",
            Skill.Flicking => "Precision",
            _ => "",
        };
        if (section.Length == 0) return null; // tactical issues use their authored match drills
        int demand = issue.Severity == Severity.Major ? 0 : issue.Severity == Severity.Minor ? 1 : 2;
        int layout = issue.Id.Contains("SMALL", StringComparison.OrdinalIgnoreCase) ? 3
            : issue.Id.Contains("LARGE", StringComparison.OrdinalIgnoreCase) ? 2
            : issue.Id.Contains("VERT", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        string id = $"personal-{section.ToLowerInvariant()}-{layout}-{demand}";
        return Resolve(id)?.Key;
    }

    public static AimVariant? Resolve(string id)
    {
        if (CoachForge.IsForged(id)) return CoachForge.Resolve(id);
        if (id.StartsWith("personal-coach-", StringComparison.Ordinal))
        {
            var parts = id.Split('-');
            if (parts.Length != 4 || !int.TryParse(parts[3], out int demand) || demand is < 0 or > 1) return null;
            string? basis = parts[2] switch {
                "over" => "coach-brake-near", "under" or "overwide" => "coach-brake-wide",
                "confirm" or "mixed" => "coach-micro-confirm", _ => null,
            };
            var source = AimVariants.All.FirstOrDefault(v => v.Id == basis);
            if (source == null) return null;
            string en = parts[2] switch { "overwide" => "Braking wide turns", "over" => "Overshoot control", "under" => "Reaching the target",
                "mixed" => "Consistent endpoints", _ => "Micro correction before firing" };
            string es = parts[2] switch { "overwide" => "Frenado en giros amplios", "over" => "Control de sobrepasos", "under" => "Llegar al objetivo",
                "mixed" => "Llegadas consistentes", _ => "Microcorrección antes del disparo" };
            return source with { Id = id, Name = "Personal practice · " + en, NameEs = "Práctica personal · " + es,
                Radius = source.Radius * (demand == 0 ? 1.25f : 1f),
                Yaw = source.Yaw * (demand == 0 ? .8f : 1f),
                OffMax = source.OffMax * (demand == 0 ? .8f : 1f) };
        }
        if (!id.StartsWith("personal-", StringComparison.Ordinal)) return null;
        string course = "course-" + id["personal-".Length..];
        var template = TrainingCatalog.All.FirstOrDefault(v => v.Id == course);
        if (template == null) return null;
        return template with
        {
            Id = id, Name = "Personal practice · " + template.Name,
            NameEs = "Práctica personal · " + template.NameEs,
        };
    }
}
