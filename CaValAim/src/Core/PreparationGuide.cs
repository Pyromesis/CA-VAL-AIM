namespace CaValAim.Core;

/// <summary>One short, credited action in a guided preparation. The seconds organise the guide; they are not a medical dose.</summary>
public sealed record PreparationStep(
    string Id,
    string TitleEn,
    string TitleEs,
    string DetailEn,
    string DetailEs,
    int Seconds,
    string SourceUrl)
{
    public string Title => Loc.Spanish ? TitleEs : TitleEn;
    public string Detail => Loc.Spanish ? DetailEs : DetailEn;
}

/// <summary>A standalone hands or mind guide, or their combined five-minute sequence.</summary>
public sealed record PreparationPlan(
    string Id,
    string NameEn,
    string NameEs,
    string IntroEn,
    string IntroEs,
    PreparationStep[] Steps)
{
    public string Name => Loc.Spanish ? NameEs : NameEn;
    public string Intro => Loc.Spanish ? IntroEs : IntroEn;
    public int TotalSeconds => Steps.Sum(s => s.Seconds);
}

/// <summary>Evidence-aware, optional preparation before Lock-In or an Academy lesson. No pain, no forced range and no promises.</summary>
public static class PreparationGuide
{
    const string HandSource = "https://www.chelwest.nhs.uk/your-visit/patient-leaflets/hand-therapy";
    const string ErgonomicsSource = "https://www.osha.gov/etools/computer-workstations/work-process";
    const string RoutineSource = "https://www.tandfonline.com/doi/full/10.1080/1750984X.2021.1944271";
    const string BreathSource = "https://www.tandfonline.com/doi/full/10.1080/1750984X.2022.2145573";
    const string ImagerySource = "https://www.tandfonline.com/doi/full/10.1080/1750984X.2020.1780627";
    const string SelfTalkSource = "https://journals.sagepub.com/doi/10.1177/1745691611413136";
    const string ResetSource = "https://www.tandfonline.com/doi/full/10.1080/1750984X.2017.1408134";

    public static readonly PreparationPlan[] All = { Hands(), Mind(), Full() };

    public static PreparationPlan Get(string? id) =>
        All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException("Unknown preparation guide", nameof(id));

    static PreparationStep[] HandSteps() => new[]
    {
        new PreparationStep("posture", "Set a comfortable position", "Colócate con comodidad",
            "Plant your feet, lower your shoulders and bring the mouse close. Keep the wrist neutral; do not force a 'perfect' posture.",
            "Apoya los pies, baja los hombros y acerca el ratón. Mantén la muñeca neutra; no fuerces una postura «perfecta».",
            15, ErgonomicsSource),
        new PreparationStep("shoulders", "Release your shoulders", "Suelta los hombros",
            "Roll both shoulders gently backwards, then let them drop. Keep breathing normally and avoid bouncing.",
            "Gira los hombros suavemente hacia atrás y déjalos caer. Respira con normalidad y evita rebotes.",
            15, HandSource),
        new PreparationStep("open-close", "Open and close your hands", "Abre y cierra las manos",
            "Open your fingers without stretching them and close again without squeezing. Use a small, comfortable range.",
            "Abre los dedos sin estirar al máximo y ciérralos sin apretar. Usa un recorrido pequeño y cómodo.",
            20, HandSource),
        new PreparationStep("wrist-flex", "Gentle wrist movement", "Movimiento suave de muñeca",
            "Move the wrist slowly forwards and backwards, staying well inside a painless range. This is mobility, not a forceful stretch.",
            "Mueve la muñeca despacio hacia delante y atrás, dentro de un rango indoloro. Es movilidad, no un estiramiento forzado.",
            30, HandSource),
        new PreparationStep("wrist-side", "Side-to-side mobility", "Movilidad de lado a lado",
            "With your hand supported, guide it gently toward the thumb and then the little finger. No pushing at the end of the range.",
            "Con la mano apoyada, guíala suavemente hacia el pulgar y después hacia el meñique. No empujes al final del recorrido.",
            20, HandSource),
        new PreparationStep("forearm", "Turn your palms", "Gira las palmas",
            "Keep your elbow near your body and turn your palm up and down slowly. Stop if this causes pain or tingling.",
            "Mantén el codo cerca del cuerpo y gira la palma hacia arriba y abajo despacio. Para si hay dolor u hormigueo.",
            20, HandSource),
        new PreparationStep("fingers", "Relaxed finger contacts", "Contactos relajados de dedos",
            "Touch the thumb to each fingertip in both hands. Keep the contact light; speed is not the goal.",
            "Toca el pulgar con la punta de cada dedo en ambas manos. El contacto es ligero; la velocidad no es el objetivo.",
            20, HandSource),
        new PreparationStep("mouse", "Easy mouse movements", "Movimientos suaves del ratón",
            "Make slow, broad movements with the minimum grip that keeps control, then a few small adjustments. Do not chase speed.",
            "Haz movimientos amplios y lentos con el agarre mínimo que conserve el control, luego pequeños ajustes. No busques velocidad.",
            30, ErgonomicsSource),
        new PreparationStep("check", "Check before you play", "Comprueba antes de jugar",
            "Let go and check for pain, tingling, numbness or reduced strength. If any is present, stop rather than hiding it with more warm-up.",
            "Suelta y comprueba si hay dolor, hormigueo, entumecimiento o pérdida de fuerza. Si aparece algo, para en vez de ocultarlo con más calentamiento.",
            10, ErgonomicsSource),
    };

    static PreparationStep[] MindSteps() => new[]
    {
        new PreparationStep("breathe", "One comfortable breath", "Una respiración cómoda",
            "Breathe in gently and let the breath out slowly without holding it. This is optional regulation, not a promise of better aim; stop if uncomfortable.",
            "Inhala suavemente y suelta el aire despacio sin retenerlo. Es regulación opcional, no una promesa de mejorar el aim; para si resulta incómodo.",
            20, BreathSource),
        new PreparationStep("notice", "Notice, do not judge", "Observa sin juzgar",
            "Name your energy and confidence as they are. You do not need to force calm or confidence before you can choose a useful next action.",
            "Reconoce tu energía y confianza tal como están. No necesitas forzar calma o confianza para elegir una siguiente acción útil.",
            20, RoutineSource),
        new PreparationStep("rehearse", "Rehearse one process", "Ensaya un proceso",
            "Picture one concrete action: clear an angle, stop the mouse and confirm before clicking. Rehearse the process, not a guaranteed win.",
            "Visualiza una acción concreta: limpiar un ángulo, frenar el ratón y confirmar antes del clic. Ensaya el proceso, no una victoria garantizada.",
            20, ImagerySource),
        new PreparationStep("goal", "Choose goal and cue", "Elige objetivo y consigna",
            "Pick one controllable process goal and a short instructional cue such as 'head level' or 'stop, shoot'. Keep it external and useful.",
            "Elige un objetivo controlable y una consigna breve como «a la cabeza» o «paro, disparo». Que sea externa y útil.",
            30, SelfTalkSource),
        new PreparationStep("reset", "Plan your reset", "Planifica tu reset",
            "If you miss: exhale, name the fact without an insult and choose the next controllable action. If tilt keeps rising, take a real break.",
            "Si fallas: exhala, describe el hecho sin insultarte y elige la siguiente acción controlable. Si el tilt aumenta, haz una pausa real.",
            30, ResetSource),
    };

    static PreparationPlan Hands() => new("hands", "HANDS · 3-minute mobility", "MANOS · 3 minutos de movilidad",
        "Gentle active mobility and a posture check. No forced stretching and no claim to prevent injury. Stop for pain, tingling, numbness or reduced strength.",
        "Movilidad activa suave y revisión de postura. Sin estiramientos forzados ni promesa de prevenir lesiones. Para ante dolor, hormigueo, entumecimiento o pérdida de fuerza.",
        HandSteps());

    static PreparationPlan Mind() => new("mind", "MIND · 2-minute reset", "MENTE · 2 minutos de reset",
        "A short pre-performance routine built around process, attention and a planned reset. It cannot guarantee calm or a better result.",
        "Una rutina breve previa al rendimiento centrada en proceso, atención y un reset planificado. No garantiza calma ni un mejor resultado.",
        MindSteps());

    static PreparationPlan Full() => new("full", "PREPARE · 5 minutes", "PREPARACIÓN · 5 minutos",
        "Optional hands-and-mind preparation before Lock-In. Skip any step; never force a stretch or a breathing pattern.",
        "Preparación opcional de manos y mente antes de Lock-In. Omite cualquier paso; nunca fuerces un estiramiento ni la respiración.",
        HandSteps().Concat(MindSteps()).ToArray());
}

/// <summary>Independent timer for a <see cref="PreparationPlan"/>; starts paused and never emits negative remaining time.</summary>
public sealed class PreparationSession
{
    public PreparationPlan Plan { get; }
    public int Index { get; private set; }
    public float Remaining { get; private set; }
    public bool Paused { get; private set; } = true;
    public bool Done => Index >= Plan.Steps.Length;
    public PreparationStep? Current => Done ? null : Plan.Steps[Index];

    public PreparationSession(PreparationPlan plan)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        if (plan.Steps.Length == 0 || plan.Steps.Any(s => s.Seconds <= 0)) throw new ArgumentException("Preparation steps need positive durations", nameof(plan));
        Remaining = plan.Steps[0].Seconds;
    }

    public void TogglePause()
    {
        if (!Done) Paused = !Paused;
    }

    public void Next()
    {
        if (Done) return;
        Index++;
        Remaining = Done ? 0 : Plan.Steps[Index].Seconds;
        Paused = false;
    }

    public void Advance(float dt)
    {
        if (Paused || Done || dt <= 0 || !float.IsFinite(dt)) return;
        Remaining -= dt;
        while (Remaining <= 0 && !Done)
        {
            float overflow = -Remaining;
            Index++;
            Remaining = Done ? 0 : Plan.Steps[Index].Seconds - overflow;
        }
    }

    public void Reset()
    {
        Index = 0;
        Remaining = Plan.Steps[0].Seconds;
        Paused = true;
    }
}
