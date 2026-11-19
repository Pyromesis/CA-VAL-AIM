using CaValAim.Analysis;
using CaValAim.Audio;
using CaValAim.Core;
using CaValAim.Game;
using CaValAim.Modes;
using Godot;

namespace CaValAim.UI;

public partial class PracticeScreen : ScreenBase
{
    readonly PlanStep[]? drills;
    readonly SensComparison? comparison;
    readonly int round;
    readonly string feedback = "";
    SkillProfile? profile;
    Dictionary<string, RunRecord> previous = new();

    public PracticeScreen() { }

    PracticeScreen(PlanStep[] drills, SkillProfile profile, int round, string feedback)
    {
        this.drills = drills;
        this.profile = profile;
        this.round = round;
        this.feedback = feedback;
    }

    PracticeScreen(SensComparison comparison, int round)
    {
        this.comparison = comparison;
        this.round = round;
    }

    public static void Start(CoachPlan plan, SkillProfile profile) =>
        Main.I.ShowScreen(new PracticeScreen(plan.Steps.SelectMany(s =>
            Enumerable.Repeat(s, Math.Clamp(s.Runs, 1, 3))).ToArray(), profile, 0, ""));

    public static void Compare(float candidate, string section = "")
    {
        var app = Main.I;
        float baseline = app.SensFor(section);
        if (!SensitivityProfiles.Valid(candidate) || SensLab.SameSens(baseline, candidate)) return;
        var test = new SensComparison(baseline, candidate, app.Settings.Dpi, app.Tier, section);
        if (Representative(section) is { } drill) test.Drill = drill;
        app.ShowScreen(new PracticeScreen(test, 0));
    }

    internal static string? Representative(string section) => section switch
    {
        "Technique" => "flickx:tech-brake-ladder",
        "Precision" => "flickx:course-precision-0-0",
        "Dynamic" => "flickx:course-dynamic-0-0",
        "Micro" => "microx:course-micro-0-0",
        "Sensitivity" => "flickx:coach-brake-wide",
        "Tracking" => "tracking",
        "Reactive" => "trackv:course-reactive-0-0",
        "Control" => "trackv:course-control-0-0",
        "Switching" => "flickx:course-switching-0-0",
        "Reflex" => "reflex:course-reflex-0-0",
        "Tactical" => "counterstrafe",
        _ => null,
    };

    public override void _Ready()
    {
        if (drills == null && comparison == null)
        {
            CoachData.Request();
            profile = CoachData.Ready;
        }
        base._Ready();
    }

    protected override void Tick(float dt)
    {
        if (comparison == null && profile == null && CoachData.Ready is { } ready)
        {
            profile = ready;
            Rebuild();
        }
    }

    protected override void Back()
    {
        if (comparison != null) Main.I.ShowScreen(new SensLabScreen { SelectedSection = comparison.Section });
        else Main.I.ShowMenu();
    }

    static string L(string es, string en) => Loc.Spanish ? es : en;

    protected override void Build()
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var root = Margins(scroll, 70 * K, 42 * K, 70 * K, 36 * K);
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(root);
        var body = VBox(18 * K);
        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(body);

        void Line(string text, Color color)
        {
            var label = Lbl(text, UiTheme.Body, 18, color);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            body.AddChild(label);
        }
        void ActionButton(string title, Action action, VButton.Look look = VButton.Look.Primary)
        {
            var button = Btn(title, look, action, 240, 46, 16);
            button.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            body.AddChild(button);
        }

        if (comparison is { } test)
        {
            body.AddChild(Heading(L("PRUEBA DE SENSIBILIDAD", "SENSITIVITY TEST"), Loc.T(CoachData.DrillName(test.Drill))));
            if (test.Section.Length > 0) Line(TrainingCatalog.SectionName(test.Section), UiTheme.Teal);
            if (round == 0 && test.Section.Length == 0)
                foreach (var key in new[] { "tracking", "flickx:coach-brake-wide", "microx:coach-micro-confirm" })
                    if (key != test.Drill)
                        ActionButton(Loc.T(CoachData.DrillName(key)), () => {
                            test.Drill = key;
                            Rebuild();
                        }, VButton.Look.Secondary);
            if (round < 4)
            {
                Line(L($"Ronda {round + 1}/4 · {test.Label(round)} · 30 segundos", $"Round {round + 1}/4 · {test.Label(round)} · 30 seconds"), UiTheme.Teal);
                if (test.Tracking) Line(L("Mantén pulsado disparo mientras sigues el blanco. Acompaña sus cambios de dirección con un movimiento continuo y relaja la mano.",
                    "Hold fire while following the target. Follow its direction changes in one continuous motion and keep your hand relaxed."), UiTheme.Text);
                else if (test.Switching) Line(L("Mantén el disparo sobre cada blanco hasta que desaparezca y cambia al siguiente.",
                    "Hold fire on each target until it clears, then switch to the next."), UiTheme.Text);
                else Line(L("Apunta al centro y dispara una vez por intento. Mantén tu ritmo habitual en las cuatro rondas.",
                    "Aim at the center and tap once per attempt. Keep your usual pace across all four rounds."), UiTheme.Text);
                ActionButton(L("EMPEZAR RONDA", "START ROUND"), () => RunComparison(test));
                if (round == 0)
                    ActionButton(L("EMPEZAR Y APLICAR SI MEJORA", "START AND APPLY IF BETTER"), () =>
                    {
                        test.AutoApply = true;
                        RunComparison(test);
                    }, VButton.Look.Secondary);
            }
            else
            {
                Line(L($"Actual {test.Baseline:0.0000} · Candidata {test.Candidate:0.0000} · {test.Dpi} DPI",
                    $"Baseline {test.Baseline:0.0000} · Candidate {test.Candidate:0.0000} · {test.Dpi} DPI"), UiTheme.Text);
                Line($"{test.Measure} · {test.LabelFor(false)} {test.Mean(false):P0} · {test.LabelFor(true)} {test.Mean(true):P0}", UiTheme.Text);
                if (test.FirstShot && test.Valid)
                    Line(L("Tiempo hasta acertar", "Time to hit") + $" · {test.LabelFor(false)} {test.MeanTime(false):0} ms · {test.LabelFor(true)} {test.MeanTime(true):0} ms", UiTheme.Text);
                if (test.Switching)
                    Line(L($"Blancos completados · actual {test.Hits(false)} · candidata {test.Hits(true)}",
                        $"Targets cleared · baseline {test.Hits(false)} · candidate {test.Hits(true)}"), UiTheme.Text);
                else if (!test.Tracking)
                    Line(L($"Tiros de cada ajuste · actual {test.Hits(false)} aciertos / {test.Misses(false)} fallos · candidata {test.Hits(true)} aciertos / {test.Misses(true)} fallos",
                        $"Shots per setting · baseline {test.Hits(false)} hits / {test.Misses(false)} misses · candidate {test.Hits(true)} hits / {test.Misses(true)} misses"), UiTheme.Text);
                Line(!test.Valid ? test.SampleHint
                    : test.Applied ? L("La candidata mejoró en ambos pares y se aplicó en el entrenador.",
                        "The candidate improved in both pairs and was applied in the trainer.")
                    : test.Winner ? L("La candidata mejoró en ambos pares.", "The candidate improved in both pairs.")
                    : L("La candidata debe mejorar en ambos pares con margen suficiente. Conserva tu sensibilidad actual.",
                        "The candidate must improve both pairs by enough. Keep your current sensitivity."), UiTheme.Dim);
                if (test.Applied)
                    ActionButton(L("RESTAURAR MI SENSIBILIDAD", "RESTORE MY SENSITIVITY"), () => { test.Restore(); Rebuild(); });
                else if (test.Winner && test.CanApply)
                    ActionButton(L("APLICAR CANDIDATA", "APPLY CANDIDATE"), () => { test.Apply(); Rebuild(); });
            }
        }
        else
        {
            body.AddChild(Heading(L("PRÁCTICA GUIADA", "GUIDED PRACTICE"), L("EJERCICIOS CON INSTRUCCIONES Y RESULTADOS", "DRILLS WITH INSTRUCTIONS AND FEEDBACK")));
            if (feedback.Length > 0) Line(feedback, UiTheme.Dim);
            if (drills is { } plan && round < plan.Length)
            {
                var step = plan[round];
                Line($"{round + 1}/{plan.Length} · {Loc.T(CoachData.DrillName(step.Drill))}", UiTheme.Teal);
                Line(Cue(step), UiTheme.Text);
                if (step.Source is { } weakness) {
                    if (!string.IsNullOrWhiteSpace(weakness.Title)) Line(L("DEBILIDAD · ", "WEAKNESS · ") + weakness.Title, UiTheme.Accent);
                    if (!string.IsNullOrWhiteSpace(weakness.Evidence)) Line(L("QUÉ OBSERVÓ EL COACH · ", "WHAT THE COACH OBSERVED · ") + weakness.Evidence, UiTheme.Text);
                    if (!string.IsNullOrWhiteSpace(weakness.Why)) Line(L("QUÉ SIGNIFICA · ", "WHAT IT MEANS · ") + weakness.Why, UiTheme.Dim);
                    foreach (string fix in weakness.Fixes.Skip(1).Take(3)) Line("• " + fix, UiTheme.Text);
                    if (Academy.For(weakness) is { } lesson)
                    {
                        Line(L("TÉCNICA · ", "TECHNIQUE · ") + lesson.Name + "  —  « " + lesson.Cue + " »", UiTheme.Good);
                        var resume = (drills, profile, round, feedback, previous);
                        ActionButton(L("VER LA LECCIÓN Y SUS VÍDEOS", "OPEN THE LESSON AND ITS VIDEOS"), () => Main.I.ShowAcademy(lesson.Id,
                            () => Main.I.ShowScreen(new PracticeScreen(resume.drills!, resume.profile!, resume.round, resume.feedback) { previous = resume.previous })),
                            VButton.Look.Secondary);
                    }
                }
                Line(L("CÓMO COMPROBAR EL PROGRESO · ", "HOW TO CHECK PROGRESS · ") + PracticeTargets.For(step), UiTheme.Teal);
                if (ModeRegistry.Find(step.Drill) is { } factory) Line(factory().Description, UiTheme.Dim);
                if (ForecastFor(step) is { } forecast)
                    Line(L($"Red local · precisión prevista a dificultad fija {forecast.Accuracy:P0} · error medio {forecast.ValidationError:P0} frente a {forecast.BaselineError:P0} en {forecast.ValidationRuns} rondas reservadas.",
                        $"Local network · fixed-difficulty accuracy estimate {forecast.Accuracy:P0} · mean error {forecast.ValidationError:P0} vs {forecast.BaselineError:P0} over {forecast.ValidationRuns} held-out rounds."), UiTheme.Dim);
                if (ModeRegistry.Find(step.Drill)?.Invoke().SupportsAdaptive == true)
                {
                    Line(step.Drill == "tracking"
                        ? L("Mantén pulsado disparo. El tamaño se ajusta cada 2 segundos según el tiempo que mantengas la mira sobre el blanco.",
                            "Hold fire. Target size adjusts every 2 seconds based on time on target.")
                        : L("La exigencia se ajusta durante la ronda según aciertos, fallos y vencimientos.",
                            "Difficulty adjusts during the round using hits, misses and timeouts."), UiTheme.Dim);
                    ActionButton(L("PRACTICAR CON NIVEL ADAPTATIVO", "PRACTISE ADAPTIVELY"), () => RunGuided(step, adaptive: true));
                    ActionButton(L("MEDIR A DIFICULTAD FIJA", "MEASURE AT FIXED DIFFICULTY"), () => RunGuided(step), VButton.Look.Secondary);
                }
                else ActionButton(L("EMPEZAR EJERCICIO", "START DRILL"), () => RunGuided(step));
            }
            else if (drills != null) Line(L("Práctica completada.", "Practice complete."), UiTheme.Good);
            else if (profile == null) Line(L("Analizando tu historial…", "Analyzing your history…"), UiTheme.Dim);
            else
            {
                Line(L("El coach elige ejercicios según tus partidas. Antes de cada uno tendrás una consigna y al terminar verás qué corregir.",
                    "The coach picks drills from your run history. Get a cue before each one and a correction afterward."), UiTheme.Text);
                if (profile.Forecasts.Count == 0)
                    Line(L("La red local muestra previsiones tras reunir al menos 24 rondas comparables de un ejercicio y superar el promedio móvil y la última ronda como referencias.",
                        "Accuracy forecasts need at least 24 comparable runs and must beat both the moving average and the last-run baseline."), UiTheme.Dim);
                foreach (var forecast in profile.Forecasts)
                {
                    string drill = Loc.T(CoachData.DrillName(forecast.Drill));
                    string metric = forecast.Metric switch {
                        "recovered-over" => L("aciertos tras corregir un sobrepaso", "hits after correcting an overshoot"),
                        "overshoot" => L("sobrepasos previstos", "predicted overshoots"),
                        "undershoot" => L("movimientos cortos previstos", "predicted undershoots"),
                        "first-hit" => L("acierto al primer tiro", "first-shot hit rate"),
                        "correction-load" => L("carga de correcciones (escala 0–4)", "correction load (0–4 scale)"),
                        _ => L("precisión prevista", "predicted accuracy"),
                    };
                    string value = forecast.Metric == "correction-load" ? $"{forecast.Accuracy * 4:0.0}" : $"{forecast.Accuracy:P0}";
                    Line(L($"Red neuronal local · {drill} · {metric} {value} · {forecast.ValidationRuns} rondas de validación",
                        $"Local neural network · {drill} · {metric} {value} · {forecast.ValidationRuns} validation runs"), UiTheme.Dim);
                }
                ActionButton(L("VER PLAN DE PRÁCTICA", "VIEW PRACTICE PLAN"), () => Start(CoachPlan.Build(profile), profile));
                ActionButton("SENS LAB", Main.I.ShowSensLab, VButton.Look.Secondary);
            }
        }
        ActionButton(L("VOLVER", "BACK"), Back, VButton.Look.Secondary);
    }

    static string Cue(PlanStep step) => step.Source?.Fixes.FirstOrDefault() ?? (step.Skill switch
    {
        Skill.Tracking => L("Sigue el centro del blanco y acompaña cada cambio de dirección con suavidad.", "Follow the center and move smoothly through each reversal."),
        Skill.Movement => L("Muévete entre intentos y confirma que te has detenido antes de disparar.", "Move between attempts and come to a stop before firing."),
        Skill.CrosshairPlacement => L("Coloca la mira a la altura del blanco antes de que aparezca.", "Set the crosshair at target height before it appears."),
        Skill.SprayControl => L("Observa la subida de la mira y compénsala con un movimiento gradual.", "Watch the crosshair climb and counter it with a gradual motion."),
        Skill.Utility => L("Espera la señal del ejercicio y recupera la mira antes de disparar.", "Wait for the drill cue and regain your aim before firing."),
        Skill.Reaction => L("Mantén la mira quieta y espera la señal visual antes de pulsar.", "Hold the crosshair still and wait for the visual cue before pressing."),
        _ => L("Haz un movimiento firme, frena al llegar y confirma que la mira está sobre el blanco antes de pulsar.",
            "Move decisively, slow down near the target, and confirm your aim before pressing."),
    });

    AimForecast? ForecastFor(PlanStep step)
    {
        if (profile == null || ModeRegistry.Find(step.Drill)?.Invoke() is not { } mode
            || (mode.PreferredTier ?? Main.I.Tier) != profile.ForecastTier) return null;
        int recoil = mode.ForcedRecoil || (mode.SelectedWeapon != WeaponKind.None && step.Recoil == RecoilAdvice.On) ? 1 : 0;
        return profile.Forecasts.FirstOrDefault(f => f.Drill == step.Drill && f.Metric == "accuracy" &&
            SensLab.SameSens(f.Sensitivity, Main.I.SensFor(SensitivityProfiles.For(mode)))
            && f.Map == (mode.Map?.Key ?? "") && f.Dpi == Main.I.Settings.Dpi && f.Weapon == (int)mode.SelectedWeapon && f.Recoil == recoil);
    }

    void RunGuided(PlanStep step, bool adaptive = false)
    {
        if (ModeRegistry.Find(step.Drill) is not { } make) return;
        var app = Main.I;
        var mode = make();
        var forecast = ForecastFor(step);
        int tier = mode.PreferredTier ?? app.Tier;
        float start = tier;
        if (adaptive && mode.SupportsAdaptive)
        {
            if (previous.TryGetValue(step.Drill, out var prior) && prior.Tier == tier
                && SensLab.SameSens(prior.Sens, app.SensFor(SensitivityProfiles.For(mode)))
                && prior.Metrics is { } pm && pm.GetValueOrDefault("adapt.trials") >= 15
                && pm.GetValueOrDefault("context.dpi") == app.Settings.Dpi
                && pm.GetValueOrDefault("run.weapon") == (int)mode.SelectedWeapon
                && pm.GetValueOrDefault("weapon.changed") == 0)
                start = pm["adapt.level"];
            else if (forecast != null)
                start += Math.Clamp((forecast.Accuracy - .8f) * 2, -.5f, .5f);
        }
        app.ShowScreen(new GameSession(make)
        {
            Adaptive = adaptive && mode.SupportsAdaptive ? new Staircase(start) : null,
            SessionRecoil = step.Recoil == RecoilAdvice.On,
            GuidedPractice = true,
            OnFinished = game =>
            {
                Sfx.I.Play("practice_complete", 0.65f);
                var review = game.Review;
                string score = game.Record is { Accuracy: >= 0 and <= 1 } r
                    ? L($"Precisión de la ronda {r.Accuracy:P0}.", $"Round accuracy {r.Accuracy:P0}.") : "";
                if (game.Record?.Metrics is { } shots && shots.GetValueOrDefault("shot.n") > 0)
                    score += L($" {shots["shot.hits"]:0} aciertos y {shots["shot.misses"]:0} fallos.",
                        $" {shots["shot.hits"]:0} hits and {shots["shot.misses"]:0} misses.");
                if (game.Adaptive is { Trials: > 0 } ladder)
                {
                    score += L($" Nivel {ladder.StartLevel:0.00} → {ladder.Level:0.00}/4.", $" Level {ladder.StartLevel:0.00} → {ladder.Level:0.00}/4.");
                    score += step.Drill == "tracking"
                        ? L($" Lograste al menos 65% de contacto en el {ladder.HitRate:P0} de {ladder.Trials} bloques de 2 segundos.",
                            $" You held at least 65% contact in {ladder.HitRate:P0} of {ladder.Trials} two-second blocks.")
                        : L($" Acierto por intento {ladder.HitRate:P0} en {ladder.Trials} intentos.",
                            $" Success rate {ladder.HitRate:P0} over {ladder.Trials} attempts.");
                }
                if (game.Adaptive == null && forecast != null && game.Record is { } result && result.Tier == profile!.ForecastTier
                    && SensLab.SameSens(result.Sens, forecast.Sensitivity) &&
                    result.Metrics is { } rm && rm.GetValueOrDefault("weapon.changed") == 0
                    && rm.GetValueOrDefault("sens.changed") == 0 && rm.GetValueOrDefault("context.dpi") == forecast.Dpi &&
                    CoachNetwork.HasSample(result))
                {
                    float diff = (result.Accuracy - forecast.Accuracy) * 100;
                    score += L($" Diferencia frente a tu previsión: {diff:+0;-0;0} puntos porcentuales.",
                        $" Difference from your forecast: {diff:+0;-0;0} percentage points.");
                }
                var issue = review?.Issues.FirstOrDefault(d => d.Skill == step.Skill) ?? review?.Issues.FirstOrDefault();
                if (game.Record is { Accuracy: >= 0 and <= 1, Metrics: { } cm } current
                    && (current.Shots >= 15 || current.Kills >= 5 || current.Metrics?.GetValueOrDefault("track.secs") >= 15)
                    && cm.GetValueOrDefault("sens.changed") == 0 && cm.GetValueOrDefault("weapon.changed") == 0)
                {
                    if (game.Adaptive == null && previous.TryGetValue(step.Drill, out var before)
                        && before.Tier == current.Tier && before.Map == current.Map && SensLab.SameSens(before.Sens, current.Sens)
                        && before.Metrics is { } bm && !bm.ContainsKey("adapt.trials")
                        && bm.GetValueOrDefault("context.dpi", -1) == cm.GetValueOrDefault("context.dpi")
                        && bm.GetValueOrDefault("context.weapon", -1) == cm.GetValueOrDefault("context.weapon")
                        && bm.GetValueOrDefault("run.recoil", -1) == cm.GetValueOrDefault("run.recoil"))
                    {
                        if (before.Metrics is { } oldMetrics && current.Metrics is { } newMetrics &&
                            oldMetrics.GetValueOrDefault("flick.n_still") >= 15 && newMetrics.GetValueOrDefault("flick.n_still") >= 15 &&
                            oldMetrics.GetValueOrDefault("context.dpi", -1) == newMetrics.GetValueOrDefault("context.dpi") &&
                            oldMetrics.GetValueOrDefault("context.weapon", -1) == newMetrics.GetValueOrDefault("context.weapon")) {
                            string Read(Dictionary<string, float> m) {
                                float n = m["flick.n_still"];
                                return L($"{m.GetValueOrDefault("flick.n_over")/n:P0} largos, {m.GetValueOrDefault("flick.n_under")/n:P0} cortos",
                                    $"{m.GetValueOrDefault("flick.n_over")/n:P0} long, {m.GetValueOrDefault("flick.n_under")/n:P0} short");
                            }
                            score += L(" Frenado anterior y actual · ", " Previous and current braking · ") + Read(oldMetrics) + " → " + Read(newMetrics) + ".";
                        }
                        float change = (current.Accuracy - before.Accuracy) * 100f;
                        score += L($" Frente a la repetición anterior del mismo ejercicio: {change:+0.0;-0.0;0} puntos de precisión. Una ronda no confirma una tendencia.",
                            $" Against the previous repetition of this drill: {change:+0.0;-0.0;0} accuracy points. One round does not establish a trend.");
                    }
                    previous[step.Drill] = current;
                }
                if (review != null)
                {
                    int over = review.Observations.Count(o => o.Code == "over");
                    int under = review.Observations.Count(o => o.Code == "under");
                    var reaction = review.Observations.Where(o => o.Code == "reaction_ms").ToArray();
                    if (over + under > 0)
                        score += L($" {over} sobrepasos y {under} movimientos cortos registrados.",
                            $" Logged {over} overshoots and {under} short movements.");
                    if (reaction.Length > 0)
                        score += L($" Respuesta media registrada: {reaction.Average(o => o.Value):0} ms.",
                            $" Average recorded response: {reaction.Average(o => o.Value):0} ms.");
                }
                string sensitivity = review?.Sens is { HasData: true } sens
                    ? L("Sensibilidad en la ronda · ", "Sensitivity this round · ") + SensLabText.Metrics(sens)
                    : "";
                if (review?.Feedback is { } points)
                    score += L($" Puntos {points.Score:+0;-0;0} (+{points.Earned}/−{points.Lost}).", $" Points {points.Score:+0;-0;0} (+{points.Earned}/−{points.Lost}).");
                if (game.Record is { Badge: >= 0 } scored)
                    score += L($" Rango {Loc.T(Difficulty.Get(scored.Badge).Name)}.", $" Rank {Loc.T(Difficulty.Get(scored.Badge).Name)}.");
                string text = string.Join("\n", new[] { score, sensitivity, issue?.Title ?? review?.Summary ?? "", issue?.Fixes.FirstOrDefault() ?? review?.NextTip ?? "" }
                    .Where(s => s.Length > 0));
                Callable.From(() => Main.I.ShowScreen(new PracticeScreen(drills!, profile!, round + 1, text)
                    { previous = previous })).CallDeferred();
            }
        });
    }

    void RunComparison(SensComparison test)
    {
        if (ModeRegistry.Find(test.Drill) is not { } make) return;
        Main.I.ShowScreen(new GameSession(make)
        {
            SessionRecoil = false,
            SensComparisonTrial = new SensitivityTrial(test.Id, round + 1, test.IsCandidate(round), test.Seeds[round / 2], test.Section),
            ComparisonSeed = test.Seeds[round / 2],
            SessionSens = test.IsCandidate(round) ? test.Candidate : test.Baseline,
            TierOverride = test.Tier,
            DurationOverride = 30,
            KeepInStats = false,
            OnFinished = game =>
            {
                Sfx.I.Play("practice_complete", 0.65f);
                test.Record(round, game.Record?.Metrics, Main.I.Settings.Dpi, game.Record);
                SensitivityHistory.Record(test.Snapshot(round + 1, game.SensSection));
                if (round == 3 && test.AutoApply) test.Apply();
                Callable.From(() => Main.I.ShowScreen(new PracticeScreen(test, round + 1))).CallDeferred();
            }
        });
    }

    internal sealed class SensComparison(float baseline, float candidate, int dpi, int tier, string section = "")
    {
        public float Baseline { get; } = baseline;
        public float Candidate { get; } = candidate;
        public int Dpi { get; } = dpi;
        public int Tier { get; } = tier;
        public string Section { get; } = section;
        public Guid Id { get; } = Guid.NewGuid();
        public int[] Seeds { get; } = new[] { Random.Shared.Next(), Random.Shared.Next() };
        public float[] Scores { get; } = new float[4];
        public string Drill = "tracking";
        public bool Tracking => Drill == "tracking" || Drill.StartsWith("trackv:", StringComparison.Ordinal);
        public bool Switching => Section == "Switching";
        public bool FirstShot => !Tracking && !Switching && Section is not ("Dynamic" or "Reflex" or "Tactical");
        public string Measure => Tracking || Switching ? L("Tiempo sobre el blanco", "Time on target")
            : FirstShot ? L("Acierto al primer tiro", "First-shot hit rate") : L("Precisión de disparo", "Shot accuracy");
        public string SampleHint => Tracking
            ? L("Mantén el disparo pulsado al menos 24 segundos por ronda con el mismo DPI.", "Hold fire for 24 seconds per round at the same DPI.")
            : FirstShot
                ? L("Repite con 15 primeros tiros medidos y 10 aciertos analizados por ronda.", "Repeat with 15 measured first shots and 10 analyzed hits per round.")
                : L("Muestra insuficiente. Completa más blancos por ronda y mantén los ajustes.", "Insufficient data. Clear more targets per round and keep your settings fixed.");
        readonly float[] times = new float[4];
        readonly int[] hits = new int[4], misses = new int[4];
        readonly float[] weapons = new float[4];
        readonly bool[] recorded = new bool[4];
        readonly int[] sampleCounts = new int[4];
        readonly float[] trackingSeconds = new float[4];
        (bool Override, float Value) original;
        CategorySensitivity? originalCategory;
        public bool AutoApply { get; set; }
        public bool Applied { get; private set; }
        public int Hits(bool candidate) => Enumerable.Range(0, 4).Where(i => IsCandidate(i) == candidate).Sum(i => hits[i]);
        public int Misses(bool candidate) => Enumerable.Range(0, 4).Where(i => IsCandidate(i) == candidate).Sum(i => misses[i]);
        public float MeanTime(bool candidate) => Enumerable.Range(0, 4).Where(i => IsCandidate(i) == candidate).Average(i => times[i]);
        public void Record(int index, Dictionary<string, float>? m, int recordedDpi, RunRecord? run = null)
        {
            if (m != null)
            {
                sampleCounts[index] = (int)m.GetValueOrDefault(FirstShot ? "micro.n" : "shot.n");
                trackingSeconds[index] = m.GetValueOrDefault("track.secs");
            }
            if (m == null || m.GetValueOrDefault("sens.changed") != 0 || m.GetValueOrDefault("weapon.changed") != 0
                || recordedDpi != Dpi || m.GetValueOrDefault("context.dpi") != Dpi
                || !m.TryGetValue("context.weapon", out weapons[index]))
            {
                Valid = false;
                return;
            }
            recorded[index] = true;
            Valid &= weapons[0] == weapons[index];
            if (Tracking) {
                Scores[index] = m.GetValueOrDefault("track.ontarget") / 100f;
                Valid &= m.GetValueOrDefault("track.secs") >= 24 && m.ContainsKey("track.ontarget");
            } else if (Switching) {
                Scores[index] = run?.Accuracy ?? 0;
                hits[index] = run?.Kills ?? 0;
                Valid &= hits[index] >= 5 && Scores[index] is > 0 and <= 1;
            } else {
                int shots = (int)m.GetValueOrDefault("shot.n");
                hits[index] = (int)m.GetValueOrDefault("shot.hits");
                misses[index] = (int)m.GetValueOrDefault("shot.misses");
                if (!FirstShot)
                {
                    Scores[index] = shots > 0 ? (float)hits[index] / shots : 0;
                    Valid &= shots >= 15 && hits[index] >= 5;
                    return;
                }
                Scores[index] = m.GetValueOrDefault("micro.first_hit_share");
                times[index] = m.GetValueOrDefault("flick.ttk_ms");
                Valid &= m.GetValueOrDefault("micro.n") >= 15 && m.GetValueOrDefault("flick.n_hit") >= 10
                    && times[index] > 0 && m.ContainsKey("micro.first_hit_share") && hits[index] >= 10;
            }
        }
        public bool Valid = true;
        readonly bool startWithCandidate = Random.Shared.Next(2) == 0;
        readonly bool candidateIsA = Random.Shared.Next(2) == 0;
        public bool IsCandidate(int i) => (i is 0 or 3) == startWithCandidate;
        public string LabelFor(bool candidate) => candidate == candidateIsA ? "A" : "B";
        public string Label(int i) => LabelFor(IsCandidate(i));
        public float Mean(bool candidate) => Enumerable.Range(0, 4).Where(i => IsCandidate(i) == candidate).Average(i => Scores[i]);
        float PairGain(int i) => IsCandidate(i) ? Scores[i] - Scores[i + 1] : Scores[i + 1] - Scores[i];
        bool Wins(bool candidate)
        {
            float sign = candidate ? 1 : -1;
            return Valid && recorded.All(v => v) && (Tracking || Hits(candidate) >= Hits(!candidate) * .92f)
                && (!FirstShot || MeanTime(candidate) <= MeanTime(!candidate) * 1.08f)
                && sign * PairGain(0) > 0 && sign * PairGain(2) > 0
                && Mean(candidate) - Mean(!candidate) >= .02f;
        }
        public bool Winner => Wins(true);
        public SensitivityTestResult Snapshot(int completedBlocks, string category) =>
            new(Id, DateTime.UtcNow, category, Drill, Tier, Dpi, Baseline, Candidate, Winner ? 1 : Wins(false) ? -1 : 0,
                Enumerable.Range(0, completedBlocks).Select(i => new SensitivityBlockResult(IsCandidate(i),
                    Scores[i], times[i], hits[i], misses[i], sampleCounts[i], trackingSeconds[i], Seeds[i / 2])).ToArray());
        public bool CanApply => Main.I.SensFor(Section) == Baseline && Main.I.Settings.Dpi == Dpi;

        public void Apply()
        {
            if (Applied || !Winner || !CanApply) return;
            var settings = Main.I.Settings;
            if (Section.Length > 0)
            {
                originalCategory = settings.CategorySensitivities?.GetValueOrDefault(Section);
                Main.I.SetCategorySens(Section, new CategorySensitivity(Candidate, Dpi, Verified: true));
            }
            else
            {
                original = (settings.UseSensOverride, settings.SensOverride);
                settings.UseSensOverride = true;
                settings.SensOverride = Candidate;
                settings.Save();
            }
            Applied = true;
        }

        public void Restore()
        {
            var settings = Main.I.Settings;
            if (!Applied) return;
            if (Section.Length > 0)
            {
                if (settings.CategorySensitivities?.GetValueOrDefault(Section) == new CategorySensitivity(Candidate, Dpi, Verified: true))
                    Main.I.SetCategorySens(Section, originalCategory);
            }
            else if (settings.UseSensOverride && settings.SensOverride == Candidate)
            {
                settings.UseSensOverride = original.Override;
                settings.SensOverride = original.Value;
                settings.Save();
            }
            Applied = false;
        }
    }
}
