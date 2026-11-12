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

    public static void Compare(float candidate)
    {
        var app = Main.I;
        Main.I.ShowScreen(new PracticeScreen(new SensComparison(app.Sens, candidate, app.Settings.Dpi, app.Tier), 0));
    }

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
        if (comparison != null) Main.I.ShowSensLab();
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
            if (round == 0)
                foreach (var key in new[] { "tracking", "flickx:coach-brake-wide", "microx:coach-micro-confirm" })
                    if (key != test.Drill)
                        ActionButton(Loc.T(CoachData.DrillName(key)), () => {
                            test.Drill = key;
                            Rebuild();
                        }, VButton.Look.Secondary);
            Line($"A  {test.Baseline:0.0000}     B  {test.Candidate:0.0000}     ·     {test.Dpi} DPI", UiTheme.Text);
            if (round < 4)
            {
                Line(L($"Ronda {round + 1}/4 · {test.Label(round)} · 30 segundos", $"Round {round + 1}/4 · {test.Label(round)} · 30 seconds"), UiTheme.Teal);
                if (test.Tracking) Line(L("Mantén pulsado disparo mientras sigues el blanco. Acompaña sus cambios de dirección con un movimiento continuo y relaja la mano.",
                    "Hold fire while following the target. Follow its direction changes in one continuous motion and keep your hand relaxed."), UiTheme.Text);
                else Line(L("Apunta al centro y dispara una vez por intento. Mantén tu ritmo habitual en las cuatro rondas.",
                    "Aim at the center and tap once per attempt. Keep your usual pace across all four rounds."), UiTheme.Text);
                if (round > 0) Line($"{test.Measure} · {test.Scores[round - 1]:P0}", UiTheme.Dim);
                ActionButton(L("EMPEZAR RONDA", "START ROUND"), () => RunComparison(test));
            }
            else
            {
                Line($"{test.Measure} · A {test.Mean(false):P0} · B {test.Mean(true):P0}", UiTheme.Text);
                if (!test.Tracking && test.Valid)
                    Line(L("Tiempo hasta acertar", "Time to hit") + $" · A {test.MeanTime(false):0} ms · B {test.MeanTime(true):0} ms", UiTheme.Text);
                Line(!test.Valid && !test.Tracking ? L("Repite la prueba con al menos 15 primeros disparos medidos y 10 aciertos analizados por ronda, manteniendo los ajustes.", "Repeat with at least 15 measured first shots and 10 analyzed hits per round, keeping settings fixed.")
                    : !test.Valid ? L("Muestra insuficiente. Mantén pulsado disparo al menos 24 segundos por ronda y repite la prueba.",
                    "Insufficient data. Hold fire for at least 24 seconds per round and repeat the test.")
                    : test.Winner ? L("B mejoró en ambas comparaciones. Puedes usarla en el entrenador y repetir la prueba otro día.",
                        "B improved in both comparisons. You can use it in the trainer and repeat the test another day.")
                    : L("La mejora de B no se repitió con suficiente margen. Conserva tu sensibilidad actual.",
                        "B did not improve consistently by enough. Keep your current sensitivity."), UiTheme.Dim);
                if (test.Winner && test.CanApply)
                    ActionButton(L("APLICAR B EN EL ENTRENADOR", "APPLY B IN THE TRAINER"), () =>
                    {
                        if (!test.CanApply) return;
                        var settings = Main.I.Settings;
                        settings.UseSensOverride = true;
                        settings.SensOverride = test.Candidate;
                        settings.Save();
                        Main.I.ShowSensLab();
                    });
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
                    Line(L($"Red neuronal local · precisión prevista {forecast.Accuracy:P0} · validada en {forecast.ValidationRuns} rondas.",
                        $"Local neural network · predicted accuracy {forecast.Accuracy:P0} · validated on {forecast.ValidationRuns} runs."), UiTheme.Dim);
                ActionButton(L("EMPEZAR EJERCICIO", "START DRILL"), () => RunGuided(step));
            }
            else if (drills != null) Line(L("Práctica completada.", "Practice complete."), UiTheme.Good);
            else if (profile == null) Line(L("Analizando tu historial…", "Analyzing your history…"), UiTheme.Dim);
            else
            {
                Line(L("El coach elige ejercicios según tus partidas. Antes de cada uno tendrás una consigna y al terminar verás qué corregir.",
                    "The coach picks drills from your run history. Get a cue before each one and a correction afterward."), UiTheme.Text);
                if (profile.Forecasts.Count == 0)
                    Line(L("La red local muestra previsiones de precisión cuando reúne al menos 24 rondas comparables de un ejercicio y supera la media móvil en rondas reservadas.",
                        "The local network shows accuracy forecasts once it has at least 24 comparable runs for a drill and beats a moving average on held-out runs."), UiTheme.Dim);
                foreach (var forecast in profile.Forecasts)
                {
                    string drill = Loc.T(CoachData.DrillName(forecast.Drill));
                    string metric = forecast.Metric switch {
                        "recovered-over" => L("aciertos tras corregir un sobrepaso", "hits after correcting an overshoot"),
                        "overshoot" => L("sobrepasos previstos", "predicted overshoots"),
                        "undershoot" => L("movimientos cortos previstos", "predicted undershoots"),
                        "first-hit" => L("primeros disparos centrados", "centered first shots"),
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

    AimForecast? ForecastFor(PlanStep step) =>
        profile != null && Main.I.Tier == profile.ForecastTier && SensLab.SameSens(Main.I.Sens, profile.ForecastSens)
            ? profile.Forecasts.FirstOrDefault(f => f.Drill == step.Drill && f.Metric == "accuracy") : null;

    void RunGuided(PlanStep step)
    {
        if (ModeRegistry.Find(step.Drill) is not { } make) return;
        var forecast = ForecastFor(step);
        Main.I.ShowScreen(new GameSession(make)
        {
            SessionRecoil = step.Recoil == RecoilAdvice.On,
            GuidedPractice = true,
            OnFinished = game =>
            {
                Sfx.I.Play("practice_complete", 0.65f);
                var review = game.Review;
                string score = game.Record is { Accuracy: >= 0 and <= 1 } r
                    ? L($"Precisión de la ronda {r.Accuracy:P0}.", $"Round accuracy {r.Accuracy:P0}.") : "";
                if (forecast != null && game.Record is { } result && result.Tier == profile!.ForecastTier
                    && SensLab.SameSens(result.Sens, profile.ForecastSens) &&
                    CoachNetwork.HasSample(result))
                {
                    float diff = (result.Accuracy - forecast.Accuracy) * 100;
                    score += L($" Diferencia frente a tu previsión: {diff:+0;-0;0} puntos porcentuales.",
                        $" Difference from your forecast: {diff:+0;-0;0} percentage points.");
                }
                var issue = review?.Issues.FirstOrDefault(d => d.Skill == step.Skill) ?? review?.Issues.FirstOrDefault();
                if (game.Record is { Accuracy: >= 0 and <= 1 } current
                    && (current.Shots >= 15 || current.Kills >= 5 || current.Metrics?.GetValueOrDefault("track.secs") >= 15)
                    && current.Metrics?.GetValueOrDefault("sens.changed") != 1)
                {
                    if (previous.TryGetValue(step.Drill, out var before) && before.Tier == current.Tier
                        && before.Map == current.Map && SensLab.SameSens(before.Sens, current.Sens))
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
                    score += L($" Registro de jugadas: {over} movimientos que se pasan y {under} que quedan cortos.",
                        $" Play log: {over} overshoots and {under} undershoots.");
                    if (reaction.Length > 0)
                        score += L($" Respuesta media registrada: {reaction.Average(o => o.Value):0} ms.",
                            $" Average recorded response: {reaction.Average(o => o.Value):0} ms.");
                }
                string sensitivity = review?.Sens is { HasData: true } sens
                    ? L("Sensibilidad en la ronda · ", "Sensitivity this round · ") + SensLabText.Metrics(sens)
                    : "";
                string text = string.Join("\n", new[] { score, sensitivity, issue?.Title ?? review?.Summary ?? "", issue?.Fixes.FirstOrDefault() ?? "" }
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
            ComparisonSeed = test.Seeds[round / 2],
            SessionSens = test.IsCandidate(round) ? test.Candidate : test.Baseline,
            TierOverride = test.Tier,
            DurationOverride = 30,
            KeepInStats = false,
            OnFinished = game =>
            {
                Sfx.I.Play("practice_complete", 0.65f);
                test.Record(round, game.Record?.Metrics, Main.I.Settings.Dpi);
                Callable.From(() => Main.I.ShowScreen(new PracticeScreen(test, round + 1))).CallDeferred();
            }
        });
    }

    internal sealed class SensComparison(float baseline, float candidate, int dpi, int tier)
    {
        public float Baseline { get; } = baseline;
        public float Candidate { get; } = candidate;
        public int Dpi { get; } = dpi;
        public int Tier { get; } = tier;
        public int[] Seeds { get; } = new[] { Random.Shared.Next(), Random.Shared.Next() };
        public float[] Scores { get; } = new float[4];
        public string Drill = "tracking";
        public bool Tracking => Drill == "tracking";
        public string Measure => Tracking ? L("Tiempo sobre el blanco", "Time on target") : L("Primeros disparos centrados", "Centered first shots");
        readonly float[] times = new float[4];
        public float MeanTime(bool candidate) => Enumerable.Range(0, 4).Where(i => IsCandidate(i) == candidate).Average(i => times[i]);
        public void Record(int index, Dictionary<string, float>? m, int recordedDpi)
        {
            if (m == null || m.GetValueOrDefault("sens.changed") != 0 || recordedDpi != Dpi) { Valid = false; return; }
            if (Tracking) {
                Scores[index] = m.GetValueOrDefault("track.ontarget") / 100f;
                Valid &= m.GetValueOrDefault("track.secs") >= 24 && m.ContainsKey("track.ontarget");
            } else {
                Scores[index] = m.GetValueOrDefault("micro.centered_share");
                times[index] = m.GetValueOrDefault("flick.ttk_ms");
                Valid &= m.GetValueOrDefault("micro.n") >= 15 && m.GetValueOrDefault("flick.n_hit") >= 10
                    && times[index] > 0 && m.ContainsKey("micro.centered_share");
            }
        }
        public bool Valid = true;
        readonly bool startWithCandidate = Random.Shared.Next(2) == 0;
        public bool IsCandidate(int i) => (i is 0 or 3) == startWithCandidate;
        public string Label(int i) => IsCandidate(i) ? "B" : "A";
        public float Mean(bool candidate) => Enumerable.Range(0, 4).Where(i => IsCandidate(i) == candidate).Average(i => Scores[i]);
        float PairGain(int i) => IsCandidate(i) ? Scores[i] - Scores[i + 1] : Scores[i + 1] - Scores[i];
        public bool Winner => Valid && (Tracking || MeanTime(true) <= MeanTime(false) * 1.08f) && PairGain(0) > 0 && PairGain(2) > 0 && Mean(true) - Mean(false) >= 0.02f;
        public bool CanApply => Main.I.Sens == Baseline && Main.I.Settings.Dpi == Dpi;
    }
}
