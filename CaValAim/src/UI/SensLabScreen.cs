using System.Globalization;
using Godot;
using CaValAim.Analysis;
using CaValAim.Core;
using CaValAim.Game;
using CaValAim.Modes;

namespace CaValAim.UI;

/// <summary>Spanish/English text for the dynamic Sens Lab values (the static labels go through Loc/Gfx).</summary>
internal static class SensLabText
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static bool Es => Loc.Spanish;

    public static string Verdict(SensFeedback f) =>
        f.HasData && !SensLab.Enough(f) ? (Es ? "RECOLECTANDO" : "COLLECTING") : Verdict(f.Verdict);

    public static string Verdict(SensBlock b) =>
        b.HasData && !SensLab.Enough(b) ? (Es ? "RECOLECTANDO" : "COLLECTING") : Verdict(b.Verdict);

    public static string Verdict(SensVerdict v) => v switch
    {
        SensVerdict.Overshoot => Es ? "TE PASAS" : "OVERSHOOT",
        SensVerdict.Undershoot => Es ? "TE QUEDAS CORTO" : "UNDERSHOOT",
        SensVerdict.Mixed => Es ? "IRREGULAR" : "MIXED",
        SensVerdict.Shaky => Es ? "PULSO INESTABLE" : "SHAKY AIM",
        SensVerdict.Tracking => Es ? "TRACKING IMPRECISO" : "TRACKING DRIFT",
        SensVerdict.Balanced => Es ? "EQUILIBRADO" : "BALANCED",
        _ => Es ? "SIN DATOS" : "NO DATA",
    };

    public static Color VerdictColor(SensVerdict v) => v switch
    {
        SensVerdict.Overshoot or SensVerdict.Mixed => UiTheme.Accent,
        SensVerdict.Undershoot => UiTheme.Teal,
        SensVerdict.Shaky or SensVerdict.Tracking => UiTheme.Warn,
        SensVerdict.Balanced => UiTheme.Good,
        _ => UiTheme.Faint,
    };

    public static string Confidence(float c) => c >= 0.7f ? (Es ? "alta" : "high")
        : c >= 0.4f ? (Es ? "media" : "medium") : (Es ? "baja" : "low");

    public static string ConfidenceCaption(float c) => Es ? $"confianza {Confidence(c)}" : $"{Confidence(c)} confidence";

    /// <summary>
    /// The verdict and the recommendation use different evidence gates. Label the verdict as evidence so a
    /// "balanced / medium evidence" block next to a "low confidence" recommendation does not read as a contradiction.
    /// </summary>
    public static string EvidenceCaption(float c) => Es ? $"evidencia {Confidence(c)}" : $"{Confidence(c)} evidence";

    public static string VerdictSentence(SensVerdict v) => v switch
    {
        SensVerdict.Overshoot => Es ? "Tus flicks se pasan del objetivo." : "Your flicks travel past the target.",
        SensVerdict.Undershoot => Es ? "Tus flicks se quedan cortos." : "Your flicks stop short of the target.",
        SensVerdict.Mixed => Es ? "A veces te pasas y a veces te quedas corto." : "Sometimes you overshoot, sometimes you undershoot.",
        SensVerdict.Shaky => Es ? "La mira tiembla; el pulso mete ruido." : "Your crosshair trembles; hand jitter is adding noise.",
        SensVerdict.Tracking => Es ? "El tracking se queda atrás del objetivo." : "Your tracking falls behind the target.",
        SensVerdict.Balanced => BalancedSentence(hasFlick: true, hasTrack: true),
        _ => Es ? "Todavía no hay suficientes datos." : "There isn't enough data yet.",
    };

    /// <summary>Only claims the signals that actually exist in this run (e.g. no "flicks are balanced" without flicks).</summary>
    public static string VerdictSentence(SensFeedback f) =>
        !f.HasFlickData && f.MicroShots > 0
            ? (Es ? $"{f.MicroShots} primeros disparos medidos; {f.CenteredShare:P0} centrados. Falta un movimiento claro para evaluar el frenado."
                : $"{f.MicroShots} initial shots measured; {f.CenteredShare:P0} centered. A clear movement is needed to assess braking.")
            : f.Verdict == SensVerdict.Balanced
            ? BalancedSentence(f.HasFlickData, f.HasTrackData)
            : VerdictSentence(f.Verdict);

    public static string VerdictSentence(SensBlock b) =>
        b.Verdict == SensVerdict.Balanced
            ? BalancedSentence(b.Flicks > 0 || b.OffTarget > 0, b.TrackSeconds > 0)
            : VerdictSentence(b.Verdict);

    static string BalancedSentence(bool hasFlick, bool hasTrack) => (hasFlick, hasTrack) switch
    {
        (true, true) => Es ? "Tus flicks y tu tracking están equilibrados." : "Your flicks and tracking are balanced.",
        (true, false) => Es ? "Tus flicks están equilibrados; no hubo datos de tracking." : "Your flicks are balanced; there was no tracking data.",
        (false, true) => Es ? "Tu tracking está equilibrado; no hubo datos de flicks." : "Your tracking is balanced; there was no flick data.",
        _ => Es ? "Todavía no hay suficientes datos." : "There isn't enough data yet.",
    };

    public static string Advice(SensFeedback f) => f.Verdict switch
    {
        SensVerdict.Overshoot => Es ? "Frena el gesto antes de llegar y confirma la mira antes de pulsar." : "Slow the motion before arrival and confirm your aim before clicking.",
        SensVerdict.Undershoot => Es ? "Completa el primer gesto hasta el centro del blanco." : "Finish the first motion all the way to the center.",
        SensVerdict.Mixed => Es ? "Haz los gestos a un ritmo constante y deja que la mira se asiente." : "Use a consistent pace and let the crosshair settle.",
        SensVerdict.Shaky or SensVerdict.Tracking => Es ? "Relaja el agarre y sigue los cambios de dirección con un movimiento continuo." : "Relax your grip and follow direction changes in one continuous motion.",
        SensVerdict.Balanced => Es ? "Conserva tu ajuste mientras comparas más rondas." : "Keep your setting while you compare more rounds.",
        _ => Es ? "Prueba un ejercicio con blancos visibles para obtener una muestra comparable." : "Try a drill with visible targets to collect comparable aim samples.",
    };
    public static string Advice(SensBlock b) => Advice(b.Verdict, b.Delta, b.OvershootShare, b.UndershootShare);

    static string Advice(SensVerdict v, float delta, float over, float under) => v switch
    {
        SensVerdict.Overshoot => Es
            ? "Si el patrón se repite en varias partidas, prueba a bajar la sensibilidad un 5–10% y frena el flick antes de llegar. Al final del gesto, relaja la mano en vez de apretar."
            : "If the pattern repeats across several runs, try lowering sensitivity by 5–10% and brake before the target. Relax your hand at the end of the flick instead of gripping harder.",
        SensVerdict.Undershoot => Es
            ? "Si el patrón se repite en varias partidas, prueba a subir la sensibilidad un 5–10% y completa el gesto con el brazo. No cortes el flick antes de llegar a la cabeza."
            : "If the pattern repeats across several runs, try raising sensitivity by 5–10% and finish the motion with your arm. Don't cut the flick before the head.",
        SensVerdict.Mixed => Es
            ? "El error cambia de dirección: no toques la sensibilidad todavía. Juega más flicks y tracking para separar la causa."
            : "The error changes direction: don't change sensitivity yet. Play more flicks and tracking to separate the cause.",
        SensVerdict.Shaky => Es
            ? "La mira tiembla: si el pulso se repite en varias sesiones, prueba a bajar la sensibilidad o relajar el brazo. Practica tracking suave y respira; el pulso no se arregla subiendo sens."
            : "Your aim trembles: if the jitter repeats across sessions, try lower sensitivity or a more relaxed arm. Practise smooth tracking and breathe; jitter doesn't get fixed by raising sensitivity.",
        SensVerdict.Tracking => Es
            ? "Tu tracking se queda atrás: si se repite, prueba a bajar un poco la sensibilidad o sigue el objetivo de forma continua, sin corregir a golpes."
            : "Your tracking lags: if it repeats, try slightly lower sensitivity or follow the target continuously instead of correcting in jerks.",
        SensVerdict.Balanced => Es
            ? "Tu sens parece equilibrada. Sigue acumulando sesiones; no cambies por una sola partida."
            : "Your sens looks balanced. Keep collecting sessions; don't change it because of one run.",
        _ => Es
            ? $"Juega {Loc.T("Head Flicks")} y {Loc.T("Strafe Tracking")} para llenar el laboratorio con datos de flick y tracking."
            : "Play Head Flicks and Strafe Tracking to fill the lab with flick and tracking data.",
    };

    public static string Metrics(SensBlock b) => b.Flicks > 0 || b.OffTarget > 0 || b.TrackSeconds > 0
        ? Metrics(b.Flicks > 0 || b.OffTarget > 0, b.TrackSeconds > 0, b.OvershootShare, b.UndershootShare, b.Jitter)
        : b.Shots > 0 ? (Es ? $"{b.Hits}/{b.Shots} aciertos" : $"{b.Hits}/{b.Shots} hits")
        : b.Cleared > 0 ? (Es ? $"{b.Cleared} blancos completados" : $"{b.Cleared} targets cleared") : Verdict(b.Verdict);

    public static string Metrics(SensFeedback f) =>
        f.HasFlickData ? (Es ? $"{MathF.Round(f.OffTarget * f.OvershootShare):0}/{f.Flicks} sobrepasos · {MathF.Round(f.OffTarget * f.UndershootShare):0}/{f.Flicks} cortos"
            : $"{MathF.Round(f.OffTarget * f.OvershootShare):0}/{f.Flicks} overshoots · {MathF.Round(f.OffTarget * f.UndershootShare):0}/{f.Flicks} short")
            + (f.HasTrackData ? $" · {(Es ? "seguimiento" : "tracking")} {f.TrackSeconds:0} s" : "")
            : Metrics(false, f.HasTrackData, 0, 0, f.Jitter);

    /// <summary>Only prints the factors measured in this run. A missing tracking sample must not look like zero tremor.</summary>
    static string Metrics(bool hasFlick, bool hasTrack, float over, float under, float jitter)
    {
        var parts = new List<string>();
        if (hasFlick)
        {
            parts.Add($"{(Es ? "PASADO" : "OVER")} {Pct0(over)}");
            parts.Add($"{(Es ? "CORTO" : "SHORT")} {Pct0(under)}");
        }
        if (hasTrack) parts.Add($"{(Es ? "PULSO" : "JITTER")} {Num(jitter.ToString("0.00", Inv))}");
        return parts.Count > 0 ? string.Join(" · ", parts) : (Es ? "SIN DATOS" : "NO DATA");
    }

    public static string HistoryLabel(SensBlock b) => Es
        ? $"{b.Sens.ToString("0.000", Inv)} · {b.Runs} partida{(b.Runs == 1 ? "" : "s")} · {b.Sessions} sesión{(b.Sessions == 1 ? "" : "es")}"
        : $"{b.Sens.ToString("0.000", Inv)} · {b.Runs} run{(b.Runs == 1 ? "" : "s")} · {b.Sessions} session{(b.Sessions == 1 ? "" : "s")}";

    public static string Runs(int n) => Es ? $"{n} partida{(n == 1 ? "" : "s")}" : $"{n} run{(n == 1 ? "" : "s")}";

    public static string Recommendation(SkillProfile p)
    {
        if (p.SensLab?.Comparison is { } comparison) return comparison.Describe(Es);
        var a = p.Sens;
        if (a == null || a.RecommendedSens <= 0) return Es
            ? "Aún no hay una recomendación: sigue jugando flicks y tracking."
            : "No recommendation yet: keep playing flicks and tracking.";
        if (a.Direction == "keep" && p.SensLab?.Current is { HasData: true } observed)
            return VerdictSentence(observed) + (Es
                ? $" Mantén {a.CurrentSens:0.000} mientras confirmas el patrón: {observed.Flicks} movimientos y {observed.TrackSeconds:0} s de seguimiento medidos. Una observación no demuestra que la sensibilidad sea la causa; compara bloques A/B equivalentes."
                : $" Keep {a.CurrentSens:0.000} while confirming the pattern: {observed.Flicks} movements and {observed.TrackSeconds:0} s of tracking measured. An observation does not establish sensitivity as the cause; compare matched A/B blocks.");
        if (a.Direction == "keep")
            return Es
                ? $"Mantén {a.CurrentSens:0.000}. El algoritmo no ve una dirección clara con {Confidence(a.Confidence)} confianza."
                : $"Keep {a.CurrentSens:0.000}. The algorithm sees no clear direction at {Confidence(a.Confidence)} confidence.";
        float pct = (a.RecommendedSens / a.CurrentSens - 1f) * 100f;
        return Es
            ? $"Prueba {a.RecommendedSens:0.000} ({pct:+0;-0}%, {Num(a.RecommendedCm360.ToString("0.0", Inv))} cm/360) con {Confidence(a.Confidence)} confianza. Cámbiala solo si la señal se repite."
            : $"Try {a.RecommendedSens:0.000} ({pct:+0;-0}%, {a.RecommendedCm360:0.0} cm/360) at {Confidence(a.Confidence)} confidence. Only change if the signal repeats.";
    }

    public static string CurrentCaption(SensBlock? b) => b == null ? (Es ? "SIN DATOS EN ESTA SENS" : "NO DATA AT THIS SENS")
        : Es ? $"{b.Runs} partida{(b.Runs == 1 ? "" : "s")} · {b.Sessions} sesión{(b.Sessions == 1 ? "" : "es")}" : $"{b.Runs} run{(b.Runs == 1 ? "" : "s")} · {b.Sessions} session{(b.Sessions == 1 ? "" : "s")}";

    public static string Pct(float x) => x <= 0 ? "—" : $"{x * 100f:0}%";
    static string Pct0(float x) => $"{x * 100f:0}%";
    public static string Flicks(int n) => Es ? $"{n} flicks" : $"{n} flicks";
    public static string MissedFlicks(int n) => Es ? $"{n} fallado{(n == 1 ? "" : "s")}" : $"{n} missed";
    public static string MeanShort(float deg) => Es ? $"{Num(deg.ToString("0.0", Inv))}° medio" : $"{deg:0.0}° avg";
    public static string MeanOver(float pct) => Es ? $"{pct:0}% del flick" : $"{pct:0}% of flick";

    /// <summary>Spanish decimal comma for measured values (sensitivity itself stays in the game's dot notation).</summary>
    public static string Num(string s)
    {
        if (!Es) return s;
        var a = s.ToCharArray();
        for (int i = 1; i + 1 < a.Length; i++)
            if (a[i] == '.' && char.IsDigit(a[i - 1]) && char.IsDigit(a[i + 1])) a[i] = ',';
        return new string(a);
    }
}

/// <summary>
/// Aim Lab-style sensitivity laboratory. Shows the current sensitivity's overshoot / undershoot / tremor
/// evidence, the coach's recommendation, the measured factors and a history of every sensitivity played.
/// </summary>
public partial class SensLabScreen : ScreenBase
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    SkillProfile? profile;
    float spinT;
    public string SelectedSection { get; set; } = "";
    /// <summary>Only the current sensitivity, its measured fault and (when present) its corrective routine.</summary>
    public bool Dedicated { get; set; }

    public override void _Ready()
    {
        CoachData.Request();
        profile = CoachData.Ready;
        base._Ready();
    }

    protected override void Tick(float dt)
    {
        if (profile != null) return;
        spinT += dt;
        if (CoachData.Ready is { } p)
        {
            profile = p;
            Rebuild();
        }
    }

    protected override void Build()
    {
        float k = K;
        var root = Margins(VBox(14 * k), 48 * k, 26 * k, 48 * k, 28 * k);
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(root);
        var col = (VBoxContainer)root.GetChild(0);

        var head = HBox(16 * k);
        var title = Dedicated
            ? Heading("SENSITIVITY", Loc.Spanish
                ? "SENS ACTUAL · FALLOS MEDIDOS · CORRECCIÓN SOLO CUANDO HAY EVIDENCIA"
                : "CURRENT SENS · MEASURED FAULTS · CORRECTION ONLY WITH EVIDENCE")
            : Heading("SENS LAB", "OVER/UNDERSHOOT, TREMOR AND TRACKING · ACCUMULATED OVER TIME");
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        var converter = Btn(Loc.Spanish ? "CONVERTIDOR" : "CONVERTER", VButton.Look.Secondary,
            Main.I.ShowSensitivityConverter, 190, 48, 16);
        converter.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(converter);
        if (!Dedicated)
        {
            var coach = Btn("AIM COACH", VButton.Look.Secondary, Main.I.ShowProfile, 220, 48, 19);
            coach.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            head.AddChild(coach);
        }
        var back = Btn("BACK", VButton.Look.Primary, Main.I.ShowMenu, 150, 48, 20);
        back.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(back);
        col.AddChild(head);

        if (!Dedicated)
        {
            var drills = new HFlowContainer();
            drills.AddThemeConstantOverride("h_separation", (int)(10 * k));
            drills.AddThemeConstantOverride("v_separation", (int)(6 * k));
            foreach (var (key, en, es) in new[]
            {
                ("flickx:sens-braking", "BRAKING", "FRENADO"),
                ("microx:sens-fine", "FINE CORRECTIONS", "AJUSTES FINOS"),
                ("trackv:sens-smooth", "STEADY TRACKING", "SEGUIMIENTO ESTABLE"),
            })
                drills.AddChild(Btn(Loc.Spanish ? es : en, VButton.Look.Secondary, () => CoachData.StartDrill(key), 205, 40, 15));
            col.AddChild(drills);
        }

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        col.AddChild(scroll);
        var body = VBox(16 * k);
        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(body);
        if (!Dedicated) body.AddChild(CategorySettings());

        if (profile == null)
        {
            body.AddChild(Lbl("LOADING…", UiTheme.HudWide, 18, UiTheme.Dim));
            return;
        }
        if (Dedicated)
        {
            BuildDedicated(body, profile);
            return;
        }
        if (SelectedSection.Length > 0) return;

        var lab = profile.SensLab;
        if (profile.Sens is { } sensAdvice && sensAdvice.Direction != "keep" && sensAdvice.CurrentSens > 0
            && SensitivityProfiles.Valid(sensAdvice.RecommendedSens))
            body.AddChild(RecommendedCandidate(sensAdvice, lab?.Current ?? lab?.History.LastOrDefault()));

        var compare = new HFlowContainer();
        body.AddChild(compare);
        float sens = Main.I.Sens;
        foreach (float factor in new[] { 0.95f, 1.05f })
        {
            float candidate = factor > 1 ? SensShift.Applied(sens, Main.I.Settings.Dpi, factor) : sens * factor;
            if (candidate < 0.001f || !float.IsFinite(candidate) || SensLab.SameSens(candidate, sens)) continue;
            string compareLabel = Loc.Spanish ? $"COMPARAR CON {candidate:0.0000}" : $"COMPARE WITH {candidate:0.0000}";
            compare.AddChild(Btn(compareLabel, VButton.Look.Secondary, () => PracticeScreen.Compare(candidate), 200, 40, 14));
        }
        var custom = new SpinBox { MinValue = .0001, MaxValue = Math.Max(10, sens * 2), Step = .0001,
            Value = sens, CustomMinimumSize = new Vector2(115 * k, 40 * k) };
        compare.AddChild(custom);
        var tryCustom = Btn(Loc.Spanish ? "PROBAR ESTE VALOR" : "TEST THIS VALUE", VButton.Look.Secondary,
            () => PracticeScreen.Compare((float)custom.Value), 185, 40, 14);
        tryCustom.Disabled = SensLab.SameSens((float)custom.Value, sens);
        custom.ValueChanged += value => tryCustom.Disabled = SensLab.SameSens((float)value, sens);
        compare.AddChild(tryCustom);
        if (lab == null || !lab.HasData)
        {
            body.AddChild(EmptyState(profile));
            return;
        }

        var cur = lab.Current ?? lab.History.LastOrDefault();
        if (cur == null)
        {
            col.AddChild(EmptyState(profile));
            return;
        }
        body.AddChild(new DrawBox { CustomMinimumSize = new Vector2(0, 276 * k), OnDraw = d => DrawSummary(d, lab, cur) });

        var cols = HBox(16 * k);
        body.AddChild(cols);
        var left = new VPanel { Title = "WHAT WE MEASURE", K = k, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 300 * k) };
        var right = new VPanel { Title = "HISTORY BY SENSITIVITY", K = k, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 300 * k) };
        cols.AddChild(left);
        cols.AddChild(right);
        left.AddChild(Factors(cur));
        right.AddChild(History(lab));

        var advice = new VPanel { Title = "COACH ADVICE", K = k };
        var adviceText = Lbl(SensLabText.Advice(cur) + "\n\n" + SensLabText.Recommendation(profile), UiTheme.Body, 16, new Color(UiTheme.Text, 0.9f));
        adviceText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        adviceText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        advice.AddChild(adviceText);
        body.AddChild(advice);

        var actions = HBox(12 * k);
        body.AddChild(actions);
        actions.AddChild(Btn("RUN SENS FINDER", VButton.Look.Primary, () => CoachData.StartDrill("sensfinder"), 260, 48, 19));
        actions.AddChild(Btn("AIM COACH", VButton.Look.Secondary, Main.I.ShowProfile, 200, 48, 19));
        actions.AddChild(Btn("BACK", VButton.Look.Ghost, Main.I.ShowMenu, 150, 48, 18));
        body.AddChild(Spacer(0, 10 * k));
    }

    void BuildDedicated(VBoxContainer body, SkillProfile p)
    {
        float k = K;
        bool es = Loc.Spanish;
        var lab = p.SensLab ?? new SensLabData { CurrentSens = Main.I.Sens, Dpi = Main.I.Settings.Dpi };
        var current = lab.Current; // A focused diagnosis never borrows a fault from another sensitivity in History.
        body.AddChild(new DrawBox { CustomMinimumSize = new Vector2(0, 276 * k), OnDraw = d => DrawSummary(d, lab, current) });

        if (p.Sens is { } advice && advice.Direction != "keep" && advice.CurrentSens > 0
            && SensitivityProfiles.Valid(advice.RecommendedSens))
            body.AddChild(RecommendedCandidate(advice, current));

        if (CorrectivePlan(current) is { } plan && current != null)
            body.AddChild(CorrectiveCard(current, plan, p));
        else
        {
            bool balanced = current is { HasData: true } b && SensLab.Enough(b) && b.Verdict == SensVerdict.Balanced;
            string message = balanced
                ? (es ? "NO HAY FALLOS DETECTABLES · Las mediciones actuales están equilibradas."
                    : "NO DETECTABLE FAULTS · Your current measurements are balanced.")
                : (es ? "NO HAY FALLOS DETECTABLES · Todavía no hay datos suficientes para identificar un patrón."
                    : "NO DETECTABLE FAULTS · There is not enough evidence to identify a pattern yet.");
            var empty = Lbl(message, UiTheme.Body, 17, balanced ? UiTheme.Good : UiTheme.Dim);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            empty.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            body.AddChild(empty);
        }

        body.AddChild(Btn(es ? "ABRIR SENS LAB COMPLETO" : "OPEN FULL SENS LAB", VButton.Look.Secondary,
            Main.I.ShowSensLab, 300, 44, 16));
        body.AddChild(Spacer(0, 10 * k));
    }

    Control CorrectiveCard(SensBlock current, CoachPlan plan, SkillProfile p)
    {
        float k = K;
        bool es = Loc.Spanish;
        var tone = SensLabText.VerdictColor(current.Verdict);
        var frame = new CardFrame { K = k, Strip = tone, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var content = VBox(10 * k);
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        frame.AddChild(Margins(content, 22 * k, 18 * k, 22 * k, 20 * k));

        void Text(string value, int size, Color color)
        {
            var label = Lbl(value, UiTheme.Body, size, color);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            content.AddChild(label);
        }

        content.AddChild(Lbl($"{(es ? "FALLO MEDIDO" : "MEASURED FAULT")} · {SensLabText.Verdict(current.Verdict)}",
            UiTheme.HudWide, 16, tone));
        Text($"{SensLabText.Metrics(current)} · {SensLabText.CurrentCaption(current)}", 15, UiTheme.Text);
        Text(SensLabText.VerdictSentence(current), 15, UiTheme.Dim);
        content.AddChild(Lbl(es ? "RUTINA CORRECTIVA" : "CORRECTIVE ROUTINE", UiTheme.HudWide, 15, UiTheme.Text));
        for (int i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            string runs = es ? $"serie{(step.Runs == 1 ? "" : "s")}" : $"run{(step.Runs == 1 ? "" : "s")}";
            Text($"{i + 1}. {Loc.T(CoachData.DrillName(step.Drill))} · {step.Runs} {runs}", 16, UiTheme.Text);
            Text(step.Source?.Fixes.FirstOrDefault() ?? step.Why, 14, UiTheme.Dim);
        }
        content.AddChild(Btn(es ? "EMPEZAR RUTINA" : "START ROUTINE", VButton.Look.Primary,
            () => PracticeScreen.Start(plan, p), 260, 48, 18));
        return frame;
    }

    /// <summary>
    /// A corrective plan exists only for a measured, sufficiently sampled fault. Balanced, absent and thin samples
    /// deliberately return null so neither this screen nor a caller can mistake a candidate sensitivity for a routine.
    /// Every step retains its evidence and first practice cue in <see cref="PlanStep.Source"/>.
    /// </summary>
    internal static CoachPlan? CorrectivePlan(SensBlock? current)
    {
        if (current == null || !current.HasData || !SensLab.Enough(current)) return null;
        bool es = Loc.Spanish;
        (string Drill, int Runs, string Cue)[] specs = current.Verdict switch
        {
            SensVerdict.Undershoot => new[]
            {
                ("flickx:sens-braking", 2, es
                    ? "Completa el primer gesto hasta el centro y frena al llegar; no dispares mientras la mira siga corta."
                    : "Finish the first motion at the center and brake on arrival; do not click while the crosshair is still short."),
                ("flickx:coach-brake-wide", 2, es
                    ? "En los giros amplios, recorre toda la distancia con el brazo y termina sobre la cabeza antes de pulsar."
                    : "On wide turns, use your arm to cover the full distance and arrive on the head before clicking."),
                ("microx:coach-micro-confirm", 1, es
                    ? "Haz una sola corrección pequeña hacia el centro, confirma la mira y dispara sin añadir tirones."
                    : "Make one small correction toward the center, confirm your aim and click without adding extra jerks."),
            },
            SensVerdict.Overshoot => new[]
            {
                ("flickx:settle-confirm", 2, es
                    ? "Frena antes de cruzar el blanco, mantén la mira quieta y confirma el contacto antes de cambiar."
                    : "Brake before crossing the target, hold the crosshair steady and confirm contact before switching."),
                ("microx:sens-fine", 2, es
                    ? "Relaja el agarre y reduce la última corrección a un gesto pequeño, sin volver a atravesar el blanco."
                    : "Relax your grip and make the final correction small, without crossing back through the target."),
            },
            SensVerdict.Mixed => new[]
            {
                ("microx:sens-fine", 2, es
                    ? "Usa un ritmo constante y una única corrección final; no persigas el error anterior con otro gesto brusco."
                    : "Use a steady pace and one final correction; do not answer the previous error with another abrupt motion."),
                ("flickx:settle-confirm", 2, es
                    ? "Haz una pausa breve al llegar y comprueba si el error fue corto o pasado antes del siguiente flick."
                    : "Pause briefly on arrival and check whether the error was short or long before the next flick."),
            },
            SensVerdict.Shaky => new[]
            {
                ("trackv:sens-smooth", 2, es
                    ? "Relaja la mano y sigue el centro con un movimiento continuo; evita corregir el temblor con tirones."
                    : "Relax your hand and follow the center in one continuous motion; avoid correcting tremor with jerks."),
                ("trackv:control-curves", 2, es
                    ? "Anticipa cada cambio de dirección y conserva un movimiento suave por toda la curva."
                    : "Anticipate each direction change and keep your motion smooth through the whole curve."),
            },
            SensVerdict.Tracking => new[]
            {
                ("trackv:sens-smooth", 2, es
                    ? "Iguala primero la velocidad del objetivo y mantén el centro sin correcciones a golpes."
                    : "Match the target's speed first and hold the center without jerky corrections."),
                ("tracking", 2, es
                    ? "Sigue el centro durante cada cambio de dirección y procura aumentar el tiempo continuo sobre el blanco."
                    : "Follow the center through every direction change and try to increase continuous time on target."),
            },
            _ => Array.Empty<(string, int, string)>(),
        };
        if (specs.Length == 0) return null;

        string title = SensLabText.VerdictSentence(current.Verdict);
        string evidence = $"{SensLabText.Metrics(current)} · {SensLabText.CurrentCaption(current)}";
        string why = es
            ? "El patrón está medido, pero no demuestra que la sensibilidad sea su causa. Aísla el gesto antes de cambiar el ajuste."
            : "The pattern is measured, but it does not establish sensitivity as its cause. Isolate the motion before changing the setting.";
        var plan = new CoachPlan { Name = es ? "SENSIBILIDAD · CORRECCIÓN" : "SENSITIVITY · CORRECTION" };
        foreach (var spec in specs)
        {
            var skill = spec.Drill.StartsWith("microx:", StringComparison.Ordinal) ? Skill.Precision
                : spec.Drill.StartsWith("trackv:", StringComparison.Ordinal) || spec.Drill == "tracking" ? Skill.Tracking
                : Skill.Flicking;
            var source = new Diagnosis("SENS_" + current.Verdict.ToString().ToUpperInvariant(), skill, Severity.Minor,
                title, evidence, why, new[] { spec.Cue }, new[] { spec.Drill },
                Math.Max(1, (int)MathF.Round(Math.Max(current.Flicks, current.TrackSeconds))));
            var (recoil, recoilWhy) = CoachPlan.RecoilFor(spec.Drill);
            plan.Steps.Add(new PlanStep(spec.Drill, skill, spec.Runs, recoil, recoilWhy, source.Title, source));
        }
        return plan;
    }

    Control RecommendedCandidate(SensAdvice advice, SensBlock? observed)
    {
        float k = K, candidate = advice.RecommendedSens;
        bool es = Loc.Spanish, higher = advice.Direction == "higher";
        var tone = higher ? UiTheme.Teal : UiTheme.Accent;
        var frame = new CardFrame { K = k, Strip = tone, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var content = VBox(10 * k);
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        frame.AddChild(Margins(content, 22 * k, 18 * k, 22 * k, 20 * k));

        var heading = HBox(14 * k);
        heading.AddChild(Lbl(es ? "CANDIDATA RECOMENDADA" : "RECOMMENDED CANDIDATE", UiTheme.HudWide, 14, tone));
        heading.AddChild(Lbl(SensLabText.ConfidenceCaption(advice.Confidence), UiTheme.Body, 13, UiTheme.Dim));
        content.AddChild(heading);

        string value = candidate.ToString("0.000", Inv);
        float pct = (candidate / advice.CurrentSens - 1f) * 100f;
        string pctText = SensLabText.Num(pct.ToString("+0.0;-0.0;0.0", Inv));
        float cm = advice.RecommendedCm360 > 0 ? advice.RecommendedCm360 : SensAdvisor.Cm360(candidate, advice.Dpi);
        var values = new HFlowContainer();
        values.AddThemeConstantOverride("h_separation", (int)(16 * k));
        values.AddThemeConstantOverride("v_separation", (int)(4 * k));
        values.AddChild(Lbl(higher ? (es ? "SUBIR SENSIBILIDAD" : "RAISE SENSITIVITY") : (es ? "BAJAR SENSIBILIDAD" : "LOWER SENSITIVITY"),
            UiTheme.HudWide, 17, tone));
        values.AddChild(Lbl(value, UiTheme.Display, 34, UiTheme.Text));
        values.AddChild(Lbl($"{pctText}% · {SensLabText.Num(cm.ToString("0.0", Inv))} cm/360", UiTheme.Display, 20, UiTheme.Text));
        content.AddChild(values);

        string explanation = higher && observed?.Verdict == SensVerdict.Undershoot
            ? SensLabText.VerdictSentence(observed.Verdict)
            : !higher && observed?.Verdict == SensVerdict.Overshoot
                ? SensLabText.VerdictSentence(observed.Verdict)
                : higher
                    ? (es ? "Tus mediciones apuntan a una sensibilidad demasiado baja para completar el gesto."
                        : "Your measurements point to sensitivity being too low to finish the motion.")
                    : (es ? "Tus mediciones apuntan a una sensibilidad demasiado alta para frenar el gesto."
                        : "Your measurements point to sensitivity being too high to brake the motion.");
        var detail = Lbl(explanation + (es
                ? " Esta candidata se basa en mediciones; no es una garantía. Debe ganar la comparación A/B antes de aplicarse."
                : " This candidate is based on measurements; it is not a guarantee. It must win the A/B comparison before it is applied."),
            UiTheme.Body, 15, new Color(UiTheme.Text, 0.88f));
        detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        content.AddChild(detail);
        content.AddChild(Btn(es ? $"PROBAR {value} A/B" : $"TEST {value} A/B", VButton.Look.Primary,
            () => PracticeScreen.Compare(candidate), 250, 48, 18));
        return frame;
    }

    Control CategorySettings()
    {
        string L(string es, string en) => Loc.Spanish ? es : en;
        var app = Main.I;
        int dpi = app.Settings.Dpi;
        var profiles = app.Settings.CategorySensitivities;
        var panel = new VPanel { K = K, Title = L("SENSIBILIDAD POR CATEGORÍA", "SENSITIVITY BY CATEGORY") };
        var content = VBox(10 * K);
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.AddChild(content);
        var picker = new OptionButton { CustomMinimumSize = new Vector2(300 * K, 40 * K) };
        var sections = TrainingCatalog.Sections.Where(SensitivityProfiles.Supports).Prepend("").ToArray();
        foreach (string key in sections)
            picker.AddItem(key.Length == 0 ? L("General", "General") : TrainingCatalog.SectionName(key));
        picker.Select(Math.Max(0, Array.IndexOf(sections, SelectedSection)));
        picker.ItemSelected += index => { SelectedSection = sections[(int)index]; Rebuild(); };
        content.AddChild(picker);

        void Line(string text)
        {
            var label = Lbl(text, UiTheme.Body, 15, UiTheme.Text);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            content.AddChild(label);
        }
        void Refresh()
        {
            CoachData.Request();
            profile = CoachData.Ready;
            Rebuild();
        }
        var tests = SensitivityHistory.Tests();
        var combined = SensitivityProfiles.Combine(profiles, dpi, tests, app.Tier);
        if (combined.Count >= 2)
        {
            Line(L($"General calculada {combined.Value:0.0000} · {PlayerView.Cm360(combined.Value, dpi):0.0} cm/360 · {combined.Count} categorías, {combined.Verified} confirmadas A/B a {dpi} DPI. Rango {combined.Low:0.0000}–{combined.High:0.0000}.",
                $"Combined {combined.Value:0.0000} · {PlayerView.Cm360(combined.Value, dpi):0.0} cm/360 · {combined.Count} categories, {combined.Verified} A/B verified at {dpi} DPI. Range {combined.Low:0.0000}–{combined.High:0.0000}."));
            Line(combined.Tests > 0
                ? L($"El coach ponderó {combined.Tests} comparaciones A/B concluyentes con el DPI y nivel actuales.",
                    $"The coach weighted {combined.Tests} decisive A/B comparisons at the current DPI and tier.")
                : L("Estimación provisional basada en los perfiles guardados.", "Provisional estimate based on saved profiles."));
            var apply = Btn(L("USAR COMO GENERAL", "USE AS GENERAL"), VButton.Look.Secondary, () =>
            {
                app.Settings.UseSensOverride = true;
                app.Settings.SensOverride = combined.Value;
                app.Settings.Save();
                Refresh();
            }, 216, 38, 14);
            apply.Disabled = SensLab.SameSens(combined.Value, app.Sens);
            content.AddChild(apply);
        }
        else Line(L("Guarda al menos dos categorías para calcular una sensibilidad general.",
            "Save at least two categories to calculate a combined sensitivity."));

        if (SelectedSection.Length == 0)
        {
            Line(L($"Sensibilidad general activa {app.Sens:0.0000}. Se usa en categorías sin perfil propio.",
                $"Active general sensitivity {app.Sens:0.0000}. It is used when a category has no profile."));
            return panel;
        }
        string section = SelectedSection;
        var saved = profiles?.GetValueOrDefault(section);
        float current = app.SensFor(section);
        string source = saved == null ? L("usa la general", "using general")
            : saved.Verified && saved.Dpi == dpi ? L("confirmada en A/B", "verified in A/B")
            : saved.Dpi != dpi ? L("convertida al DPI actual", "converted to current DPI") : L("manual", "manual");
        Line(L($"Activa {current:0.0000} · {PlayerView.Cm360(current, dpi):0.0} cm/360 · {source}",
            $"Active {current:0.0000} · {PlayerView.Cm360(current, dpi):0.0} cm/360 · {source}"));
        int archived = tests.Count(t => t.Category == section);
        if (archived > 0) Line(L($"{archived} pruebas A/B archivadas.", $"{archived} archived A/B tests."));
        float candidate = MathF.Min(SensInput.Max, current * 1.05f);
        SensLabData? history = null;
        if (profile?.SectionSensitivity.TryGetValue(section, out var data) == true)
        {
            history = data.Lab;
            if (data.Lab.Current is { HasData: true } b)
            {
                Line($"{SensLabText.Runs(b.Runs)} · {SensLabText.Metrics(b)}");
                if ((b.Flicks > 0 || b.TrackSeconds > 0) && b.Shots > 0)
                    Line(L($"{b.Hits} aciertos · {b.Shots - b.Hits} fallos", $"{b.Hits} hits · {b.Shots - b.Hits} misses"));
                if (b.Shots > 0)
                {
                    int points = 10 * b.Hits - 5 * (b.Shots - b.Hits);
                    Line(L($"Puntos de esta sensibilidad {points:+0;-0;0} · +10 por acierto · −5 por fallo",
                        $"Sensitivity points {points:+0;-0;0} · +10 per hit · −5 per miss"));
                }
            }
            if (data.Lab.Comparison is { } comparison) Line(comparison.Describe(Loc.Spanish));
            if (data.Advice.Direction != "keep" && SensitivityProfiles.Valid(data.Advice.RecommendedSens))
            {
                candidate = data.Advice.RecommendedSens;
                Line(L($"Candidata del coach {candidate:0.0000} · {SensLabText.ConfidenceCaption(data.Advice.Confidence)}",
                    $"Coach candidate {candidate:0.0000} · {SensLabText.ConfidenceCaption(data.Advice.Confidence)}"));
            }
        }
        if (SensLab.SameSens(candidate, current)) candidate = current * .95f;
        var row = new HFlowContainer();
        var input = new SpinBox { MinValue = SensInput.Min, MaxValue = SensInput.Max, Step = .0001,
            Value = candidate, CustomMinimumSize = new Vector2(120 * K, 40 * K) };
        row.AddChild(input);
        row.AddChild(Btn(L("GUARDAR VALOR", "SAVE VALUE"), VButton.Look.Secondary,
            () => { app.SetCategorySens(section, new CategorySensitivity((float)input.Value, dpi, false)); Refresh(); }, 166, 40, 14));
        row.AddChild(Btn(L("COMPARAR A/B", "COMPARE A/B"), VButton.Look.Primary,
            () => PracticeScreen.Compare((float)input.Value, section), 166, 40, 14));
        if (saved != null)
            row.AddChild(Btn(L("USAR LA GENERAL", "USE GENERAL"), VButton.Look.Ghost,
                () => { app.SetCategorySens(section, null); Refresh(); }, 166, 40, 14));
        content.AddChild(row);
        if (history is { History.Count: > 0 }) content.AddChild(History(history));
        return panel;
    }

    Control EmptyState(SkillProfile p)
    {
        float k = K;
        var v = VBox(14 * k);
        var panel = new VPanel { Title = "NO DATA YET", K = k };
        var text = Lbl(SensLabText.Advice(new SensFeedback(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, SensVerdict.NoData))
                       + "\n\n" + SensLabText.Recommendation(p), UiTheme.Body, 17, UiTheme.Text);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.AddChild(text);
        v.AddChild(panel);
        var row = HBox(12 * k);
        row.AddChild(Btn("RUN SENS FINDER", VButton.Look.Primary, () => CoachData.StartDrill("sensfinder"), 260, 48, 19));
        row.AddChild(Btn("AIM COACH", VButton.Look.Secondary, Main.I.ShowProfile, 200, 48, 19));
        v.AddChild(row);
        return v;
    }

    Control Factors(SensBlock? b)
    {
        float k = K;
        var v = VBox(2 * k);
        if (b == null)
        {
            v.AddChild(Lbl("—", UiTheme.Body, 16, UiTheme.Faint));
            return v;
        }
        void Row(string label, string value, Color? c = null)
        {
            v.AddChild(new InfoRow { Label = label, Value = () => value, ValueColor = () => c ?? UiTheme.Text, K = k, H = 38, Stripe = v.GetChildCount() % 2 == 0 });
        }
        Row("OVERSHOOT", $"{SensLabText.Pct(b.OvershootShare)} · {SensLabText.MissedFlicks(b.OffTarget)}", UiTheme.Accent);
        Row("UNDERSHOOT", $"{SensLabText.Pct(b.UndershootShare)} · {SensLabText.MeanShort(b.MeanUndershootDeg)}", UiTheme.Teal);
        Row("MEAN OVERSHOOT", SensLabText.MeanOver(b.MeanOvershootPct));
        Row("TREMOR (JITTER)", SensLabText.Num(b.Jitter.ToString("0.00", Inv)));
        Row("CORRECTIONS PER FLICK", SensLabText.Num(b.Corrections.ToString("0.0", Inv)));
        Row("TRACKING ON TARGET", $"{b.TrackOnPct:0}%");
        return v;
    }

    Control History(SensLabData lab)
    {
        float k = K;
        var v = VBox(2 * k);
        if (lab.History.Count == 0)
        {
            v.AddChild(Lbl("—", UiTheme.Body, 16, UiTheme.Faint));
            return v;
        }
        int i = 0;
        foreach (var b in lab.History.AsEnumerable().Reverse())
        {
            var block = b;
            v.AddChild(new InfoRow
            {
                Label = SensLabText.HistoryLabel(block),
                Value = () => SensLabText.Metrics(block),
                ValueColor = () => SensLabText.VerdictColor(block.Verdict),
                Tag = () => SensLabText.Verdict(block),
                K = k, H = 38, Stripe = i++ % 2 == 0,
            });
        }
        return v;
    }

    void DrawSummary(DrawBox d, SensLabData lab, SensBlock? b)
    {
        float k = K;
        var app = Main.I;
        int dpi = lab.Dpi > 0 ? lab.Dpi : app.Settings.Dpi > 0 ? app.Settings.Dpi : 800;
        float sens = b?.Sens ?? lab.CurrentSens;
        float cm = SensAdvisor.Cm360(sens, dpi);
        var vc = SensLabText.VerdictColor(b?.Verdict ?? SensVerdict.NoData);
        int cap = UiTheme.Fs(12, k), big = UiTheme.Fs(52, k), sub = UiTheme.Fs(14, k), vs = UiTheme.Fs(30, k);

        Gfx.Text(d, UiTheme.HudWide, "CURRENT SENSITIVITY", 0, Gfx.Mid(12 * k, cap), cap, UiTheme.Dim);
        Gfx.Text(d, UiTheme.Display, sens.ToString("0.000", Inv), -2 * k, 68 * k, big, UiTheme.Text);
        Gfx.Text(d, UiTheme.Body, $"{sens * dpi:0} eDPI · {SensLabText.Num(cm.ToString("0.0", Inv))} cm/360 · {SensLabText.CurrentCaption(b)}", 0, Gfx.Mid(88 * k, sub), sub, UiTheme.Dim);

        Gfx.TextR(d, UiTheme.HudWide, "CURRENT VERDICT", d.Size.X, Gfx.Mid(12 * k, cap), cap, UiTheme.Dim);
        Gfx.TextR(d, UiTheme.Display, b == null ? SensLabText.Verdict(SensVerdict.NoData) : SensLabText.Verdict(b), d.Size.X, Gfx.Mid(44 * k, vs), vs, vc);
        string conf = b == null ? "" : SensLabText.EvidenceCaption(b.Confidence);
        if (conf.Length > 0) Gfx.TextR(d, UiTheme.Body, conf, d.Size.X, Gfx.Mid(66 * k, sub), sub, UiTheme.Dim);

        // Bias bar: left = stopping short, right = overshooting.
        float bx0 = 0, bx1 = d.Size.X, by = 120 * k, bh = 7 * k, mid = (bx0 + bx1) / 2;
        d.DrawRect(new Rect2(bx0, by, bx1 - bx0, bh), new Color(UiTheme.Teal, 0.20f));
        d.DrawRect(new Rect2(mid, by, bx1 - mid, bh), new Color(UiTheme.Accent, 0.20f));
        d.DrawRect(new Rect2(mid - 1 * k, by - 4 * k, 2 * k, bh + 8 * k), new Color(UiTheme.Text, 0.45f));
        float bias = b == null ? 0 : SensLab.Bias(b);
        float mx = mid + bias * (bx1 - bx0) * 0.44f;
        Gfx.Diamond(d, new Vector2(mx, by + bh / 2), 6 * k, 7 * k, vc);
        Gfx.Text(d, UiTheme.HudWide, "SHORT", bx0, Gfx.Mid(by + 30 * k, cap), cap, UiTheme.Teal);
        Gfx.TextC(d, UiTheme.HudWide, "CENTERED", mid, Gfx.Mid(by + 30 * k, cap), cap, UiTheme.Dim);
        Gfx.TextR(d, UiTheme.HudWide, "OVERSHOOT", bx1, Gfx.Mid(by + 30 * k, cap), cap, UiTheme.Accent);

        // Three headline numbers.
        float cw = (d.Size.X - 24 * k) / 3;
        (string Label, string Value, Color Col)[] items =
        {
            ("OVERSHOOT", SensLabText.Pct(b?.OvershootShare ?? 0), UiTheme.Accent),
            ("UNDERSHOOT", SensLabText.Pct(b?.UndershootShare ?? 0), UiTheme.Teal),
            (b is { Jitter: > 0 } ? "TREMOR" : "TRACKING", b is { Jitter: > 0 } ? SensLabText.Num(b.Jitter.ToString("0.00", Inv)) : SensLabText.Pct((b?.TrackOnPct ?? 0) / 100f), UiTheme.Warn),
        };
        for (int i = 0; i < items.Length; i++)
        {
            float cx = cw * (i + 0.5f) + (i > 0 ? 12 * k : 0);
            Gfx.TextC(d, UiTheme.Display, items[i].Value, cx, Gfx.Mid(208 * k, vs), vs, items[i].Col);
            Gfx.TextC(d, UiTheme.HudWide, items[i].Label, cx, Gfx.Mid(228 * k, cap), cap, UiTheme.Dim);
            if (i > 0) d.DrawRect(new Rect2(cx - cw / 2, 190 * k, 1, 46 * k), new Color(UiTheme.Text, 0.1f));
        }

        string line = b == null ? "" : SensLabText.VerdictSentence(b);
        if (line.Length > 0) Gfx.TextFit(d, UiTheme.Body, line, 0, Gfx.Mid(258 * k, sub), sub, UiTheme.Dim, d.Size.X);
    }
}
