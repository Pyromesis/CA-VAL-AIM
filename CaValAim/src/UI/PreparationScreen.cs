using Godot;
using CaValAim.Core;
using CaValAim.Warmup;

namespace CaValAim.UI;

/// <summary>Optional timed hands/mind preparation with bilingual steps, pause, skip, restart and direct source links.</summary>
public partial class PreparationScreen : ScreenBase
{
    readonly PreparationSession session;
    readonly Action? backAction;
    internal Action? BackAction => backAction;

    Label? stepCount, stepTitle, stepDetail, timeText, sourceNote;
    VButton? pauseButton, nextButton, resetButton, continueButton;
    DrawBox? progress;
    int sourceCount;

    static string L(string es, string en) => Loc.Spanish ? es : en;

    public PreparationScreen(string planId = "full", Action? back = null)
    {
        session = new PreparationSession(PreparationGuide.Get(planId));
        backAction = back;
    }

    public override void _Ready()
    {
        base._Ready();
        if (Main.I.Dev && CmdLine.Has("--preparation-check")) Callable.From(RunDevCheck).CallDeferred();
        else Callable.From(() => { if (IsInstanceValid(pauseButton)) { pauseButton.FocusMode = FocusModeEnum.All; pauseButton.GrabFocus(); } }).CallDeferred();
    }

    protected override void Back()
    {
        if (backAction != null) backAction();
        else Main.I.ShowLockIn();
    }

    protected override void Build()
    {
        sourceCount = 0;
        float k = K;
        var root = Margins(VBox(18 * k), 54 * k, 36 * k, 54 * k, 30 * k);
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(root);
        var col = (VBoxContainer)root.GetChild(0);

        var head = HBox(16 * k);
        var title = Heading(session.Plan.Name, L(
            "PASO A PASO · PAUSA Y OMISIÓN DISPONIBLES · NUNCA FUERCES DOLOR, RESPIRACIÓN NI RANGO",
            "STEP BY STEP · PAUSE AND SKIP AVAILABLE · NEVER FORCE PAIN, BREATHING OR RANGE OF MOTION"));
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        var back = Btn("BACK", VButton.Look.Secondary, Back, 145, 48, 18);
        back.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(back);
        col.AddChild(head);

        var columns = HBox(20 * k);
        columns.SizeFlagsVertical = SizeFlags.ExpandFill;
        col.AddChild(columns);

        var main = new VPanel { K = k, Title = L("TU PREPARACIÓN", "YOUR PREPARATION"), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.35f };
        columns.AddChild(main);
        var content = VBox(15 * k);
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        main.AddChild(content);
        var intro = Lbl(session.Plan.Intro, UiTheme.Body, 16, UiTheme.TextSoft);
        intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        intro.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        content.AddChild(intro);

        stepCount = Lbl("", UiTheme.HudWide, 12, UiTheme.Teal);
        content.AddChild(stepCount);
        stepTitle = Lbl("", UiTheme.Light, 31, UiTheme.Text);
        stepTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        stepTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        content.AddChild(stepTitle);
        stepDetail = Lbl("", UiTheme.Body, 19, UiTheme.Text);
        stepDetail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        stepDetail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        stepDetail.CustomMinimumSize = new Vector2(0, 128 * k);
        content.AddChild(stepDetail);
        timeText = Lbl("", UiTheme.Display, 42, UiTheme.Accent);
        content.AddChild(timeText);
        progress = new DrawBox { CustomMinimumSize = new Vector2(0, 18 * k), OnDraw = DrawProgress };
        content.AddChild(progress);

        var actions = new HFlowContainer();
        actions.AddThemeConstantOverride("h_separation", (int)(10 * k));
        actions.AddThemeConstantOverride("v_separation", (int)(8 * k));
        content.AddChild(actions);
        pauseButton = Btn("START", VButton.Look.Primary, Toggle, 190, 50, 17);
        nextButton = Btn(L("SIGUIENTE", "NEXT"), VButton.Look.Secondary, Next, 155, 50, 16);
        resetButton = Btn(L("REINICIAR", "RESTART"), VButton.Look.Ghost, Reset, 165, 50, 15);
        continueButton = Btn(L("CONTINUAR", "CONTINUE"), VButton.Look.Primary, Back, 230, 50, 17);
        continueButton.Visible = false;
        actions.AddChild(pauseButton);
        actions.AddChild(nextButton);
        actions.AddChild(resetButton);
        actions.AddChild(continueButton);
        content.AddChild(Spacer(0, 10 * k));

        var sources = new VPanel { K = k, Title = L("FUENTES Y CRÉDITOS", "SOURCES AND CREDITS"), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        columns.AddChild(sources);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        sources.AddChild(scroll);
        var list = VBox(10 * k);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(list);
        sourceNote = Lbl(L(
            "Cada fuente se abre en el navegador. Los autores y títulos completos también están en CREDITS.md y Ajustes → Acerca de → Licencias.",
            "Every source opens in your browser. Full authors and titles are also in CREDITS.md and Settings → About → Licenses."),
            UiTheme.Body, 14, UiTheme.Dim);
        sourceNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        sourceNote.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        list.AddChild(sourceNote);
        foreach (var step in session.Plan.Steps.GroupBy(s => s.SourceUrl).Select(g => g.First()))
        {
            string url = step.SourceUrl;
            var row = HBox(10 * k);
            row.AddChild(Btn(L("LEER", "READ"), VButton.Look.Ghost, () => OpenSource(url), 84, 34, 12));
            var text = Lbl($"{step.Title}\n{url}", UiTheme.Body, 13, UiTheme.Dim);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(text);
            list.AddChild(row);
            sourceCount++;
        }
        UpdateLabels();
    }

    void DrawProgress(DrawBox d)
    {
        float fraction = session.Done ? 1
            : (session.Index + (session.Current is { } step ? 1 - session.Remaining / step.Seconds : 0)) / session.Plan.Steps.Length;
        fraction = Mathf.Clamp(fraction, 0, 1);
        d.DrawRect(new Rect2(0, 5 * K, d.Size.X, 7 * K), new Color(UiTheme.Text, .10f));
        if (fraction > 0) d.DrawRect(new Rect2(0, 5 * K, d.Size.X * fraction, 7 * K), session.Done ? UiTheme.Good : UiTheme.Accent);
        string status = session.Done ? L("COMPLETADO", "COMPLETE") : session.Paused ? L("EN PAUSA", "PAUSED") : L("EN CURSO", "RUNNING");
        Gfx.TextR(d, UiTheme.HudWide, status, d.Size.X, 14 * K, UiTheme.Fs(10, K), UiTheme.Dim);
    }

    protected override void Tick(float dt)
    {
        session.Advance(dt);
        UpdateLabels();
        progress?.QueueRedraw();
    }

    void UpdateLabels()
    {
        if (stepTitle == null) return;
        stepCount!.Text = session.Done
            ? L("PREPARACIÓN COMPLETA", "PREPARATION COMPLETE")
            : $"{L("PASO", "STEP")} {session.Index + 1}/{session.Plan.Steps.Length} · {session.Plan.TotalSeconds / 60} MIN";
        stepTitle.Text = session.Done
            ? L("Listo para continuar", "Ready to continue")
            : session.Current!.Title;
        stepDetail!.Text = session.Done
            ? L("Puedes repetir la guía o volver. La preparación es una ayuda opcional, no una prueba de rendimiento ni de salud.",
                "You can repeat the guide or go back. Preparation is optional support, not a test of performance or health.")
            : session.Current!.Detail;
        timeText!.Text = session.Done ? "✓" : WarmupPlan.Clock(Mathf.CeilToInt(session.Remaining));
        pauseButton!.Label = session.Paused ? L("EMPEZAR", "START") : L("PAUSA", "PAUSE");
        pauseButton.Disabled = session.Done;
        nextButton!.Disabled = session.Done;
        resetButton!.Disabled = session.Index == 0 && session.Paused && session.Remaining == session.Plan.Steps[0].Seconds;
        continueButton!.Visible = session.Done;
    }

    void Toggle() { session.TogglePause(); UpdateLabels(); }
    void Next() { session.Next(); UpdateLabels(); }
    void Reset() { session.Reset(); UpdateLabels(); }

    static void OpenSource(string url)
    {
        if (!url.StartsWith("https://", StringComparison.Ordinal)) return;
        try { OS.ShellOpen(url); }
        catch (Exception e) { Log.Error("Preparation: could not open source: " + e.Message); }
    }

    /// <summary>Dev-only end-to-end check of the real screen and buttons; it never opens external pages.</summary>
    void RunDevCheck()
    {
        int passed = 0;
        var failures = new List<string>();
        void Check(bool condition, string name)
        {
            if (condition) { passed++; GD.Print($"[preparation-check] PASS {name}"); }
            else { failures.Add(name); GD.PrintErr($"[preparation-check] FAIL {name}"); }
        }

        Check(session.Paused && session.Index == 0 && stepTitle!.Text.Length > 0 && pauseButton!.Disabled == false,
            "initial step is visible and paused");
        pauseButton!.EmitSignal(BaseButton.SignalName.Pressed);
        Check(!session.Paused && pauseButton.Label == (Loc.Spanish ? "PAUSA" : "PAUSE"), "start button starts the timer");
        session.Advance(15); UpdateLabels();
        Check(session.Index == 1 && stepTitle!.Text == session.Current!.Title, "time advances the real step labels");
        pauseButton.EmitSignal(BaseButton.SignalName.Pressed);
        float before = session.Remaining;
        session.Advance(30);
        Check(session.Paused && session.Remaining == before, "pause button freezes time");
        nextButton!.EmitSignal(BaseButton.SignalName.Pressed);
        Check(session.Index == 2 && !session.Paused, "next button skips one step");
        session.Advance(1000); UpdateLabels();
        Check(session.Done && session.Remaining == 0 && continueButton!.Visible && continueButton.IsVisibleInTree(), "completion shows the continue action");
        resetButton!.EmitSignal(BaseButton.SignalName.Pressed);
        Check(session.Index == 0 && session.Paused && !continueButton!.Visible, "restart restores the initial screen");
        Check(sourceCount == session.Plan.Steps.Select(s => s.SourceUrl).Distinct().Count() && sourceCount >= 7,
            "every distinct source has a real button");
        GD.Print($"[preparation-check] RESULT: {(failures.Count == 0 ? "PASS" : "FAIL")} ({passed} passed, {failures.Count} failed)");
        GetTree().Quit(failures.Count == 0 ? 0 : 1);
    }
}
