using CaValAim.Modes;
using CaValAim.Core;

namespace CaValAim.Analysis;

/// <summary>Reproducible prescriptions. The saved mode key encodes its layout and demand.</summary>
public static class PersonalDrills
{
    public static string? For(Diagnosis issue)
    {
        if (issue.Id.StartsWith("REACTION_GAME_", StringComparison.Ordinal)) return issue.Drills.FirstOrDefault();
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
