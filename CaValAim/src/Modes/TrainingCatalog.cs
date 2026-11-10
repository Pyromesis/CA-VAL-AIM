using CaValAim.Core;

namespace CaValAim.Modes;

/// <summary>Authored training progressions using independent placement, motion and timing constraints.</summary>
public static class TrainingCatalog
{
    public static readonly string[] Sections = { "Precision", "Dynamic", "Micro", "Sensitivity",
        "Tracking", "Reactive", "Control", "Switching", "Reflex", "ReactionGames", "Tactical" };

    public static string SectionName(string key) => Loc.Spanish ? key switch
    {
        "Precision" => "Precisión y frenado", "Dynamic" => "Clic sobre blancos móviles",
        "Micro" => "Ajustes finos", "Sensitivity" => "Sensibilidad",
        "Tracking" => "Seguimiento preciso", "Reactive" => "Seguimiento reactivo",
        "Control" => "Control en curvas", "Switching" => "Cambio y confirmación",
        "ReactionGames" => "Minijuegos de reacción", "Reflex" => "Reacción y adquisición", _ => "Combate, movimiento y utilidad",
    } : key switch
    {
        "Precision" => "Precision and braking", "Dynamic" => "Dynamic clicking",
        "Micro" => "Fine corrections", "Sensitivity" => "Sensitivity",
        "Tracking" => "Precise tracking", "Reactive" => "Reactive tracking",
        "Control" => "Curved control", "Switching" => "Switch and confirm",
        "ReactionGames" => "Reaction minigames", "Reflex" => "Reaction and acquisition", _ => "Combat, movement and utility",
    };

    public static string SectionOf(TrainingMode mode) => mode.Category switch
    {
        "Flick" => "Precision", "Aim" => "Precision", "Coach" => "Sensitivity",
        "Other" => mode.Is2D ? "Reflex" : "Sensitivity",
        "Micro" or "Sensitivity" or "Tracking" or "Reflex" or "Precision" or "Dynamic"
            or "Reactive" or "Control" or "Switching" or "ReactionGames" => mode.Category,
        _ => "Tactical",
    };

    public static readonly AimVariant[] All = Build().ToArray();

    static IEnumerable<AimVariant> Build()
    {
        foreach (string section in Sections.Where(s => s != "Tactical" && s != "ReactionGames"))
            for (int layout = 0; layout < 5; layout++)
                for (int demand = 0; demand < 4; demand++)
                    yield return Make(section, layout, demand);
    }

    static AimVariant Make(string section, int layout, int demand)
    {
        string[] en = { "Horizontal", "Vertical", "Wide", "Near", "Far" };
        string[] es = { "Horizontal", "Vertical", "Amplio", "Cercano", "Lejano" };
        float distance = layout == 3 ? 6 : layout == 4 ? 25 : 13;
        bool track = section is "Tracking" or "Reactive" or "Control";
        var family = track ? AimFamily.Track : section == "Reflex" ? AimFamily.Reflex
            : section == "Micro" ? AimFamily.Micro : AimFamily.Flick;
        string cueEn = section switch
        {
            "Sensitivity" => "Keep your sensitivity. Make one movement, brake on the target and compare short and long errors.",
            "Micro" => "Finish the small correction before clicking. Avoid a second large flick.",
            "Tracking" => "Hold fire. Stay centered through long strafes and relax your grip.",
            "Reactive" => "Hold fire. Read each reversal, react, and return smoothly to the center.",
            "Control" => "Hold fire. Match the curve and its vertical movement with continuous aim.",
            "Switching" => "Hold fire. Confirm the elimination before switching. Maintain contact on every target.",
            "Dynamic" => "Read the moving target and click on its center. Confirm each hit.",
            "Reflex" => "Wait for the target. Start moving only after it appears and confirm the click.",
            _ => "Move decisively, brake on the center and confirm the shot.",
        };
        string cueEs = section switch
        {
            "Sensitivity" => "Mantén tu sensibilidad. Haz un movimiento, frena en el blanco y compara errores cortos y largos.",
            "Micro" => "Termina el ajuste pequeño antes de pulsar. Evita un segundo movimiento grande.",
            "Tracking" => "Mantén disparo. Sigue el centro durante desplazamientos largos y relaja la mano.",
            "Reactive" => "Mantén disparo. Lee cada giro, reacciona y vuelve al centro con suavidad.",
            "Control" => "Mantén disparo. Acompaña la curva y su altura con un movimiento continuo.",
            "Switching" => "Mantén disparo. Confirma la eliminación antes de cambiar y conserva el contacto.",
            "Dynamic" => "Lee el blanco móvil y pulsa sobre su centro. Confirma cada impacto.",
            "Reflex" => "Espera al blanco. Muévete cuando aparezca y confirma el clic.",
            _ => "Haz un movimiento firme, frena en el centro y confirma el tiro.",
        };
        return new AimVariant($"course-{section.ToLowerInvariant()}-{layout}-{demand}", family,
            $"{section} · {en[layout]} · {demand + 1}", cueEn)
        {
            NameEs = $"{SectionNameEs(section)} · {es[layout]} · {demand + 1}", DescriptionEs = cueEs,
            CategoryOverride = section, TapOnly = !track && section != "Switching",
            Count = section is "Switching" or "Dynamic" ? 3 : 1,
            Radius = (track ? 0.34f : 0.20f) * (1 - demand * 0.15f),
            DistMin = distance, DistMax = distance + 3,
            Yaw = layout == 2 ? 55 : layout == 3 ? 16 : 28,
            MinAngle = layout == 2 ? 18 : 3,
            Pattern = layout == 0 ? AimPattern.Horizontal : layout == 1 ? AimPattern.Vertical : AimPattern.Random,
            PitchMin = layout == 1 ? -5 : -1, PitchMax = layout == 1 ? 25 : 8,
            OffMin = 0.5f + demand * 0.12f, OffMax = 1.8f + demand * 0.4f,
            Drift = section == "Dynamic" ? 1.5f + demand * 0.7f : 0,
            VerticalDrift = section == "Dynamic" && layout == 1 ? 0.6f : 0,
            ContactSeconds = section == "Switching" ? 0.10f + demand * 0.04f : 0,
            Motion = section == "Control" ? (layout % 2 == 0 ? TrackMotion.Figure8 : TrackMotion.Orbit)
                : section == "Reactive" ? TrackMotion.Zigzag : layout == 1 ? TrackMotion.Bob : TrackMotion.Strafe,
            Speed = (section == "Reactive" ? 4 : 2.5f) + demand * 0.6f,
            Width = layout == 2 ? 11 : 6, Height = layout == 1 ? 2 : 0.8f,
            TurnMin = section == "Reactive" ? 0.25f : 0.8f,
            TurnMax = section == "Reactive" ? 0.7f : 1.8f,
            DelayMin = 0.65f, DelayMax = 2.4f + layout * 0.2f,
            Window = 1.1f - demand * 0.12f,
        };
    }

    static string SectionNameEs(string section) => section switch
    {
        "Precision" => "Precisión", "Dynamic" => "Blancos móviles", "Micro" => "Microajustes",
        "Sensitivity" => "Sensibilidad", "Tracking" => "Seguimiento", "Reactive" => "Reactividad",
        "Control" => "Control", "Switching" => "Cambio de blanco", _ => "Reacción",
    };
}
