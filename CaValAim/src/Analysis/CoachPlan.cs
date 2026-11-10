using CaValAim.Core;
using CaValAim.Modes;

namespace CaValAim.Analysis;

/// <summary>Whether the camera climb (pause menu → RECOIL) should be on for a drill.</summary>
public enum RecoilAdvice { On, Off }

/// <summary>One drill in the coach's plan: what it trains, how many runs and how the recoil should be set.</summary>
public sealed record PlanStep(
    string Drill,          // CA-VAL-AIM mode key, e.g. "flick"
    Skill Skill,
    int Runs,              // series of this drill in the plan
    RecoilAdvice Recoil,
    string RecoilWhy,      // one line shown as the chip's tooltip
    string Why,            // what this step fixes, in the player's words (a Diagnosis title when there is one)
    Diagnosis? Source);    // the problem that put it in the plan (null for a weakness / onboarding step)

/// <summary>
/// The coach's training plan: a short, ordered set of drills built from the player's worst problems (and, when those
/// run out, from the weakest rated skills). It is the bridge between the diagnosis screen and the playlist system:
/// <see cref="Queue"/> turns it into the exact drill queue a <see cref="Playlist"/> plays, recoil setting included.
/// </summary>
public sealed class CoachPlan
{
    public List<PlanStep> Steps { get; } = new();
    /// <summary>Name of the playlist the UI creates (one per day: creating it again updates it instead of duplicating).</summary>
    public string Name { get; init; } = "";

    public int TotalRuns => Steps.Sum(s => s.Runs);
    /// <summary>Rough wall-clock estimate: a run is the drill plus its results screen (≈2 min).</summary>
    public int Minutes => Math.Max(1, TotalRuns * 2);
    public bool IsEmpty => Steps.Count == 0;

    /// <summary>The ordered queue (one entry per run) and the recoil setting each run needs.</summary>
    public IEnumerable<(string Key, bool RecoilOn)> Queue()
    {
        foreach (var s in Steps)
            for (int i = 0; i < s.Runs; i++)
                yield return (s.Drill, s.Recoil == RecoilAdvice.On);
    }

    /// <summary>The drills the plan asks for, without repeats (for a summary line).</summary>
    public IEnumerable<string> Drills => Steps.Select(s => s.Drill);

    const int MaxSteps = 4;

    /// <summary>A follow-up from the last round, usable immediately without opening the Sens Lab.</summary>
    public static CoachPlan FromRun(RunReview review)
    {
        bool es = Loc.Spanish;
        var plan = new CoachPlan();
        if (review.Sens is { } f && SensLab.Enough(f))
        {
            string drill = f.Verdict switch
            {
                SensVerdict.Overshoot => "flickx:settle-confirm",
                SensVerdict.Undershoot => "flickx:sens-braking",
                SensVerdict.Mixed => "microx:sens-fine",
                SensVerdict.Shaky or SensVerdict.Tracking => "trackv:sens-smooth",
                _ => "",
            };
            if (drill.Length > 0)
            {
                string cue = f.Verdict switch
                {
                    SensVerdict.Overshoot => es
                        ? "Mantén tu sensibilidad y disparo pulsado. Frena sobre el blanco y mantenlo centrado hasta eliminarlo. Evita cruzarlo y tener que volver."
                        : "Keep your sensitivity and hold fire. Brake on the target and keep it centered until it clears. Avoid crossing it and having to return.",
                    SensVerdict.Undershoot => es
                        ? "Mantén tu sensibilidad. Completa el primer movimiento hasta el centro antes de pulsar. Intenta reducir la proporción de flicks cortos."
                        : "Keep your sensitivity. Finish the first motion at the center before clicking. Try to reduce the share of flicks that stop short.",
                    SensVerdict.Mixed => es
                        ? "Mantén tu sensibilidad y practica a un ritmo constante. Haz una pausa breve al llegar y compara la proporción de errores a cada lado."
                        : "Keep your sensitivity and practise at a steady pace. Pause briefly on arrival and compare the share of errors in each direction.",
                    _ => es
                        ? "Mantén pulsado disparo y sigue el centro del objetivo. Relaja el agarre al invertir el movimiento e intenta aumentar tu tiempo en blanco."
                        : "Hold fire and follow the center of the target. Relax your grip during reversals and try to increase time on target.",
                };
                Add(drill, SkillOf(drill), new Diagnosis("SENS_TASK", SkillOf(drill), Severity.Tip, "",
                    "", "", new[] { cue }, new[] { drill }, f.Flicks));
            }
        }
        foreach (var issue in review.Issues)
        {
            string? drill = issue.Drills.FirstOrDefault(k => ModeRegistry.Find(k) != null && plan.Steps.All(s => s.Drill != k));
            if (drill == null) continue;
            Add(drill, issue.Skill, issue);
            if (plan.Steps.Count >= 3) break;
        }
        if (plan.IsEmpty)
            Add("flick", Skill.Flicking, null);
        return plan;

        void Add(string drill, Skill skill, Diagnosis? source)
        {
            var (recoil, why) = RecoilFor(drill);
            plan.Steps.Add(new PlanStep(drill, skill, 1, recoil, why, source?.Title ?? "", source));
        }
    }

    /// <summary>
    /// Builds today's plan from a profile. Order: the worst problems first (one drill each, never repeated), then the
    /// rated skills with the most room for improvement; with no data at all it falls back to the onboarding drills so
    /// the button always produces something playable.
    /// </summary>
    public static CoachPlan Build(SkillProfile p)
    {
        bool es = Loc.Spanish;
        var plan = new CoachPlan { Name = "COACH · " + DateTime.Now.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture) };
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string drill, Skill skill, int runs, string why, Diagnosis? source)
        {
            string key = drill.Split(':')[0];
            if (plan.Steps.Count >= MaxSteps || !used.Add(key)) return;
            if (ModeRegistry.Find(drill) == null) return;
            var (advice, adviceWhy) = RecoilFor(drill);
            plan.Steps.Add(new PlanStep(drill, skill, Math.Clamp(runs, 1, 3), advice, adviceWhy, why, source));
        }

        foreach (var d in p.Problems)
        {
            string? drill = (d.Drills ?? Array.Empty<string>())
                .FirstOrDefault(k => ModeRegistry.Find(k) != null && !used.Contains(k.Split(':')[0]));
            if (drill == null) continue;
            int runs = d.Severity switch { Severity.Major => 3, Severity.Minor => 2, _ => 1 };
            Add(drill, d.Skill, runs, d.Title, d);
            if (plan.Steps.Count >= 3) break;
        }

        // The problems are the sharpest signal, but a profile can have a clearly weak skill without a diagnosis for it
        // (low confidence, or a skill no rule covers). Add the ones with the most room, weighted by how much the skill
        // actually predicts the rank: that is why "worst skill" and "biggest lever" are not always the same.
        if (plan.Steps.Count < MaxSteps)
        {
            float reference = Reference(p);
            var weak = p.Skills
                .Where(r => Rated(r) && r.Confidence >= 0.25f)
                .Select(r => (Rating: r, Gap: reference - r.Tier))
                .Where(x => x.Gap > 0.25f)
                .OrderByDescending(x => x.Gap * Weight(x.Rating.Skill) * Math.Max(0.4f, x.Rating.Confidence))
                .ToList();
            foreach (var (rating, gap) in weak)
            {
                if (plan.Steps.Count >= MaxSteps) break;
                string? drill = DrillsFor(rating.Skill).FirstOrDefault(k => ModeRegistry.Find(k) != null && !used.Contains(k.Split(':')[0]));
                if (drill == null) continue;
                string why = es
                    ? $"{SkillText(rating.Skill)}: {gap:0.0} niveles por debajo de tu media. {rating.Headline}"
                    : $"{SkillText(rating.Skill)}: {gap:0.0} tiers below your average. {rating.Headline}";
                Add(drill, rating.Skill, gap >= 0.8f ? 2 : 1, why, null);
            }
        }

        // No problems and nothing clearly weak (a new player, or a very even profile): a balanced first session.
        if (plan.Steps.Count == 0)
        {
            foreach (var (key, _, rates) in Starter)
            {
                if (plan.Steps.Count >= MaxSteps) break;
                Add(key, SkillOf(key), 1, es ? "Sesión base: toca todas las mecánicas para que el coach tenga datos." : "Baseline session: covers every mechanic so the coach gets data.", null);
            }
        }
        return plan;
    }

    /// <summary>True when a skill has a usable rating (the engine's −1 means "not enough data").</summary>
    static bool Rated(SkillRating r) => r.Tier >= -0.5f && r.Confidence > 0 && !float.IsNaN(r.Tier);

    /// <summary>The tier every skill is compared against: the overall rank, or the median skill when there is no overall.</summary>
    static float Reference(SkillProfile p)
    {
        if (p.OverallTier >= -0.5f) return p.OverallTier;
        var tiers = p.Skills.Where(Rated).Select(r => r.Tier).OrderBy(t => t).ToList();
        return tiers.Count == 0 ? 1f : tiers[tiers.Count / 2];
    }

    static float Weight(Skill s) => Assess.SkillWeight.TryGetValue(s, out var w) ? w : 0.1f;

    static string SkillText(Skill s) => Coach.SkillName(s);

    static Skill SkillOf(string key) => key.Split(':')[0] switch
    {
        "tracking" or "strafebots" or "trackv" or "airtrack" or "reacttrack" => Skill.Tracking,
        "peek" or "siteclear" or "anchor" => Skill.CrosshairPlacement,
        "counterstrafe" or "peekduel" or "jumppeek" or "jigglepeek" or "peek_mix" or "anti-mirror" => Skill.Movement,
        "spray_vandal" or "spray_phantom" or "spray_transfer" => Skill.SprayControl,
        "flashmap" or "flashpeek" => Skill.Utility,
        "reaction" or "reflex" => Skill.Reaction,
        "gridshot" or "microshot" or "microx" or "switch" or "popup" => Skill.Precision,
        _ => Skill.Flicking,
    };

    /// <summary>
    /// Recoil for a drill. Spray drills need the real climb (it is the thing being trained); flick / precision /
    /// tracking drills are measured with the camera quiet, so the score describes the hand and not the compensation;
    /// map, movement and utility drills play like a real round, recoil on.
    /// </summary>
    public static (RecoilAdvice Advice, string Why) RecoilFor(string drill)
    {
        bool es = Loc.Spanish;
        string key = drill.Split(':')[0].ToLowerInvariant();
        if (key.StartsWith("spray", StringComparison.Ordinal))
            return (RecoilAdvice.On, es
                ? "Actívalo: la subida real del arma es justo lo que entrenas."
                : "On: the real climb is the thing this drill trains.");
        if (key is "flick" or "gridshot" or "spider" or "microshot" or "popup" or "switch" or "reaction" or "flickx" or "microx" or "reflex")
            return (RecoilAdvice.Off, es
                ? "Desactívalo: son tiros sueltos y la cámara quieta aísla tu puntería."
                : "Off: single shots, and a quiet camera isolates your aim.");
        if (key is "tracking" or "strafebots" or "airtrack" or "reacttrack" or "trackv" or "anti-mirror")
            return (RecoilAdvice.Off, es
                ? "Desactívalo: aísla el seguimiento; el control de subida se entrena en Ráfaga."
                : "Off: it isolates tracking; spray control is trained in the spray drills.");
        return (RecoilAdvice.On, es
            ? "Actívalo: la cámara sube igual que en una ronda real."
            : "On: the camera climbs exactly like in a real round.");
    }

    /// <summary>Human label for the chip ("RECOIL ON" / "RECOIL OFF").</summary>
    public static string RecoilLabel(RecoilAdvice a) => a == RecoilAdvice.On ? "RECOIL ON" : "RECOIL OFF";

    /// <summary>The drills that produce data for a skill, best first (shared by the profile screen and the plan).</summary>
    public static string[] DrillsFor(Skill s) => s switch
    {
        Skill.Flicking => new[] { "flick", "spider", "gridshot", "flickx:horizontal", "flickx:mid" },
        Skill.Precision => new[] { "flick", "gridshot", "microx:tiny", "flickx:tiny" },
        Skill.Tracking => new[] { "tracking", "trackv:control-curves", "trackv:react-diagonal", "strafebots", "trackv:slow", "trackv:fast" },
        Skill.Reaction => new[] { "reaction", "reflex:signal-near", "reflex:signal-angle", "reflex:centre", "reflex:near" },
        Skill.CrosshairPlacement => new[] { "peek", "siteclear", "anchor" },
        Skill.SprayControl => new[] { "spray_vandal", "spray_phantom" },
        Skill.Movement => new[] { "anti-mirror", "counterstrafe", "peekduel", "peekduel:close", "peekduel:long" },
        Skill.Utility => new[] { "flashmap" },
        _ => Array.Empty<string>(),
    };

    /// <summary>The drills the empty state asks for, how many coached runs each needs and what they rate.</summary>
    public static readonly (string Key, int Target, string Rates)[] Starter =
    {
        ("flick", 2, "Flicking · Precision"),
        ("tracking", 2, "Tracking"),
        ("peek", 2, "Crosshair placement"),
        ("counterstrafe", 1, "Movement"),
        ("spray_vandal", 1, "Spray control"),
    };
}
