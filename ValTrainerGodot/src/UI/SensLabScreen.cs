using System.Globalization;
using Godot;
using ValTrainer.Analysis;
using ValTrainer.Core;
using ValTrainer.Game;

namespace ValTrainer.UI;

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
        f.Verdict == SensVerdict.Balanced
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

    public static string Advice(SensFeedback f) => Advice(f.Verdict, f.Delta, f.OvershootShare, f.UndershootShare);
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

    public static string Metrics(SensBlock b) =>
        Metrics(b.Flicks > 0 || b.OffTarget > 0, b.TrackSeconds > 0, b.OvershootShare, b.UndershootShare, b.Jitter);

    public static string Metrics(SensFeedback f) =>
        Metrics(f.HasFlickData, f.HasTrackData, f.OvershootShare, f.UndershootShare, f.Jitter);

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
        var a = p.Sens;
        if (a == null || a.RecommendedSens <= 0) return Es
            ? "Aún no hay una recomendación: sigue jugando flicks y tracking."
            : "No recommendation yet: keep playing flicks and tracking.";
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
        var title = Heading("SENS LAB", "OVER/UNDERSHOOT, TREMOR AND TRACKING · ACCUMULATED OVER TIME");
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        var coach = Btn("AIM COACH", VButton.Look.Secondary, Main.I.ShowProfile, 220, 48, 19);
        coach.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(coach);
        var back = Btn("BACK", VButton.Look.Primary, Main.I.ShowMenu, 150, 48, 20);
        back.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(back);
        col.AddChild(head);

        if (profile == null)
        {
            col.AddChild(Lbl("LOADING…", UiTheme.HudWide, 18, UiTheme.Dim));
            return;
        }

        var lab = profile.SensLab;
        if (lab == null || !lab.HasData)
        {
            col.AddChild(EmptyState(profile));
            return;
        }

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        col.AddChild(scroll);
        var body = VBox(16 * k);
        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(body);

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
