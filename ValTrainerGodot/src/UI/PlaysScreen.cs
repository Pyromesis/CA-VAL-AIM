using System.Globalization;
using Godot;
using ValTrainer.Analysis;
using ValTrainer.Core;
using ValTrainer.Game;
using static Godot.Control;

namespace ValTrainer.UI;

/// <summary>
/// PLAY BY PLAY: every play of one run, judged on its own. A card per engagement / spray / reaction / flash / round /
/// stop / tracking window or kill: what happened, the numbers behind it, the one cue that fixes it and the drills that
/// train it, plus WATCH (rewind at that moment) and PRACTISE. It opens over the results screen or from the coach
/// profile, so the run's 3D world (and its Rewind) stays available while it is up.
/// </summary>
public partial class PlaysScreen : CanvasLayer
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static bool Es => Loc.Spanish;
    static string L(string en, string es) => Es ? es : en;

    readonly RunTelemetry run;
    readonly PlayAnalysis plays;
    readonly RunInsights? insights;
    readonly bool canRewind;

    Control? page;
    float k = 1f;

    /// <summary>Called when the list closes (the results screen uses it to bring its panel back).</summary>
    public Action? Closed;

    public PlaysScreen(RunTelemetry run, PlayAnalysis plays, RunInsights? insights = null)
    {
        this.run = run;
        this.plays = plays;
        this.insights = insights;
        canRewind = RewindData.CanRewind(run);
    }

    /// <summary>Opens the list above everything (a CanvasLayer, like the Rewind) under <paramref name="parent"/>.</summary>
    public static PlaysScreen Open(Node parent, RunTelemetry run, PlayAnalysis plays, RunInsights? insights = null)
    {
        var s = new PlaysScreen(run, plays, insights) { Name = "Plays" };
        parent.AddChild(s);
        return s;
    }

    /// <summary>Dev-only "&lt;godot&gt; --path . -- --plays-file run.vtt": the list of one saved run, over the menu.</summary>
    public static void OpenDevFile(string path)
    {
        var run = RunTelemetry.Load(path);
        if (run == null) { Log.Error($"[plays] can't read {path}"); return; }
        run.SourceFile ??= Path.GetFullPath(path); // WATCH needs the .vtr sidecar (3D) next to it
        var plays = PlayByPlay.Analyze(run);
        Log.Info($"[plays] {path}: kind={plays.Kind} n={plays.Plays.Count} avg={plays.Average:0.0}");
        Open(Main.I, run, plays, RewindData.Ready(run));
    }

    public override void _Ready()
    {
        Layer = 7;
        ProcessMode = ProcessModeEnum.Always;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        UiTheme.ClickSound();
        Rebuild();
        GetViewport().SizeChanged += OnResized;
    }

    public override void _ExitTree()
    {
        var vp = GetViewport();
        if (vp != null && IsInstanceValid(vp)) vp.SizeChanged -= OnResized;
    }

    void OnResized()
    {
        if (IsInstanceValid(this) && IsInsideTree()) Rebuild();
    }

    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.F11) return; // window mode toggle (Main) still works
        if (RewindScreen.OpenCount > 0) return; // the rewind on top owns the keys
        GetViewport().SetInputAsHandled();
        if (key.Keycode == Key.Escape) Close();
    }

    void Close()
    {
        UiTheme.ClickSound();
        Closed?.Invoke();
        QueueFree();
    }

    // =====================================================================================
    // layout
    // =====================================================================================

    void Rebuild()
    {
        if (page != null && IsInstanceValid(page)) { RemoveChild(page); page.QueueFree(); }
        var vp = GetViewport().GetVisibleRect().Size;
        k = UiTheme.Fit(vp);

        var root = new Control { MouseFilter = Control.MouseFilterEnum.Stop, Theme = UiTheme.MakeTheme(k) };
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); // after AddChild: a Control under a CanvasLayer anchors to the viewport
        page = root;

        var dim = new ColorRect { Color = new Color(0.016f, 0.024f, 0.034f, 0.985f), MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        float side = Mathf.Clamp(vp.X * 0.06f, 26 * k, 150 * k);
        var margins = new MarginContainer();
        root.AddChild(margins);
        margins.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margins.AddThemeConstantOverride("margin_left", (int)side);
        margins.AddThemeConstantOverride("margin_right", (int)side);
        margins.AddThemeConstantOverride("margin_top", (int)(28 * k));
        margins.AddThemeConstantOverride("margin_bottom", (int)(22 * k));

        var col = VBox(14 * k);
        margins.AddChild(col);
        col.AddChild(Header());

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        col.AddChild(scroll);
        var pad = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        pad.AddThemeConstantOverride("margin_right", (int)(14 * k));
        pad.AddThemeConstantOverride("margin_top", (int)(4 * k));
        pad.AddThemeConstantOverride("margin_bottom", (int)(10 * k));
        scroll.AddChild(pad);
        var list = VBox(10 * k);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        pad.AddChild(list);

        // The verdict scrolls with the cards: an autowrapped paragraph cannot set the page's minimum height, or the
        // list (which has no height of its own) would be left with nothing on a short window.
        if (plays.HasData) list.AddChild(Verdict());

        if (!plays.HasData)
        {
            list.AddChild(Para(plays.Verdict, UiTheme.Body, 17, UiTheme.Dim));
            return;
        }
        foreach (var p in plays.Plays) list.AddChild(Card(p));
    }

    Control Header()
    {
        var h = HBox(16 * k);
        h.CustomMinimumSize = new Vector2(0, 74 * k);

        var title = VBox(5 * k);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.AddChild(new DrawBox
        {
            CustomMinimumSize = new Vector2(0, 3 * k),
            OnDraw = d =>
            {
                float solid = MathF.Min(34 * k, d.Size.X);
                d.DrawRect(new Rect2(0, 0, solid, MathF.Max(1.5f, 2 * k)), UiTheme.Accent);
                if (d.Size.X > solid)
                    Gfx.HGradient(d, new Rect2(solid, 0, d.Size.X - solid, MathF.Max(1.5f, 2 * k)),
                        new Color(1, 1, 1, 0.10f), new Color(1, 1, 1, 0f));
            },
        });
        title.AddChild(Lbl(L("PLAY BY PLAY", "JUGADA A JUGADA"), UiTheme.Light, 44, UiTheme.Text));
        string caption = Subtitle();
        var sub = Lbl(caption, UiTheme.HudWide, 11.5f, UiTheme.Faint);
        sub.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        sub.ClipText = true;
        title.AddChild(sub);
        h.AddChild(title);

        var close = Btn(L("CLOSE  (ESC)", "CERRAR  (ESC)"), VButton.Look.Primary, Close, 190, 50, 18);
        close.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        h.AddChild(close);
        return h;
    }

    string Subtitle()
    {
        string name = plays.Mode.Length > 0 ? CoachData.DrillName(RunAnalysis.Base(plays.Mode)) : L("THIS RUN", "ESTA PARTIDA");
        string when = Loc.Spanish
            ? $"{run.When.Day} {Loc.MonthShort(run.When)} · {run.When:HH:mm}"
            : run.When.ToString("d MMM · HH:mm", Inv);
        string kind = plays.Plays.Count == 1 ? KindName(plays.Kind) : KindPlural(plays.Kind);
        return $"{name.ToUpperInvariant()}  ·  {when}  ·  {plays.Plays.Count} {kind}";
    }

    static string KindName(string kind) => kind switch
    {
        "sprays" => L("SPRAY", "SPRAY"),
        "reactions" => L("REACTION", "REACCIÓN"),
        "flashes" => L("FLASH", "FLASH"),
        "rounds" => L("ROUND", "ROUND"),
        "stops" => L("STOP", "FRENADO"),
        "tracking" => L("TRACKING WINDOW", "VENTANA DE TRACKING"),
        "kills" => L("KILL", "KILL"),
        _ => L("ENGAGEMENT", "DUELO"),
    };

    static string KindPlural(string kind) => kind switch
    {
        "sprays" => L("SPRAYS", "SPRAYS"),
        "reactions" => L("REACTIONS", "REACCIONES"),
        "flashes" => L("FLASHES", "FLASHES"),
        "rounds" => L("ROUNDS", "ROUNDS"),
        "stops" => L("STOPS", "FRENADOS"),
        "tracking" => L("TRACKING WINDOWS", "VENTANAS DE TRACKING"),
        "kills" => L("KILLS", "KILLS"),
        _ => L("ENGAGEMENTS", "DUELOS"),
    };

    Control Verdict()
    {
        var panel = new VPanel { Title = L("RUN VERDICT", "VEREDICTO DE LA PARTIDA"), K = k, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var v = VBox(8 * k);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.AddChild(v);
        v.AddChild(Para(plays.Verdict, UiTheme.Body, 16.5f, UiTheme.Text));

        var row = HBox(18 * k);
        row.AddChild(Lbl(L($"AVERAGE  {plays.Average:0}/100", $"MEDIA  {plays.Average:0}/100"), UiTheme.Display, 20, UiTheme.Text));
        row.AddChild(Spacer(4 * k, 0));
        void Count(string label, int n, Color c)
        {
            if (n > 0) row.AddChild(Lbl($"{n} · {label}", UiTheme.HudWide, 13.5f, c));
        }
        Count("S", plays.Excellent, GradeColor(PlayGrade.Excellent));
        Count("A", plays.Good, GradeColor(PlayGrade.Good));
        Count("B", plays.Fair, GradeColor(PlayGrade.Fair));
        Count(L("TO FIX", "POR CORREGIR"), plays.Poor, GradeColor(PlayGrade.Bad));
        v.AddChild(row);
        return panel;
    }

    Control Card(Play p)
    {
        var col = GradeColor(p.Grade);
        var frame = new CardFrame { K = k, Strip = col, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var margins = new MarginContainer();
        margins.AddThemeConstantOverride("margin_left", (int)(20 * k));
        margins.AddThemeConstantOverride("margin_right", (int)(20 * k));
        margins.AddThemeConstantOverride("margin_top", (int)(14 * k));
        margins.AddThemeConstantOverride("margin_bottom", (int)(14 * k));
        frame.AddChild(margins);

        var h = HBox(16 * k);
        margins.AddChild(h);

        // left rail: grade chip, number and time
        var left = VBox(4 * k);
        left.CustomMinimumSize = new Vector2(58 * k, 0);
        h.AddChild(left);
        int fs = UiTheme.Fs(26, k);
        left.AddChild(new DrawBox
        {
            CustomMinimumSize = new Vector2(46 * k, 46 * k),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            OnDraw = d =>
            {
                d.DrawRect(new Rect2(Vector2.Zero, d.Size), new Color(col, 0.16f));
                d.DrawRect(new Rect2(0, 0, d.Size.X, MathF.Max(1.5f, 2 * k)), col);
                Gfx.TextC(d, UiTheme.Display, p.Letter, d.Size.X / 2, Gfx.Mid(d.Size.Y / 2, fs), fs, col);
            },
        });
        left.AddChild(Lbl($"#{p.Index}", UiTheme.HudWide, 12, UiTheme.Dim));
        left.AddChild(Lbl(Clock(p.T0), UiTheme.Hud, 11, UiTheme.Faint));

        // right: what happened and what to do about it
        var body = VBox(5 * k);
        body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        h.AddChild(body);

        var head = HBox(10 * k);
        var title = Lbl(p.Title, UiTheme.Display, 19, UiTheme.Text);
        title.ClipText = true;
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.CustomMinimumSize = new Vector2(80 * k, 0);
        head.AddChild(title);
        head.AddChild(Lbl(KindName(p.Kind).ToUpperInvariant(), UiTheme.HudWide, 10.5f, new Color(col, 0.9f)));
        body.AddChild(head);

        if (p.Detail.Length > 0) body.AddChild(Para(p.Detail, UiTheme.Body, 14, UiTheme.Dim));
        if (p.Advice.Length > 0) body.AddChild(Para(p.Advice, UiTheme.Body, 15, UiTheme.TextSoft));

        var drills = (p.Drills ?? Array.Empty<string>()).Where(CoachData.HasDrill).Distinct().Take(2).ToList();
        if (canRewind || drills.Count > 0)
        {
            var row = HBox(8 * k);
            row.AddChild(Spacer(0, 2 * k));
            if (canRewind)
            {
                var watch = Btn(L("WATCH", "VER"), VButton.Look.Secondary, () => Watch(p), 104, 36, 15);
                watch.TooltipText = L("Rewind this exact play", "Rebobina esta jugada exacta");
                row.AddChild(watch);
            }
            foreach (var key in drills)
            {
                string name = CoachData.DrillName(key).ToUpperInvariant();
                float w = Gfx.TextW(UiTheme.HudWide, name, UiTheme.Fs(15, k)) / k + 40;
                var b = Btn(name, VButton.Look.Ghost, () => Practice(key), Mathf.Max(110, w), 36, 15);
                b.TitleFont = UiTheme.HudWide;
                b.TooltipText = L("Practise this in ", "Entrena esto en ") + CoachData.DrillName(key);
                row.AddChild(b);
            }
            body.AddChild(row);
        }
        return frame;
    }

    void Watch(Play p)
    {
        // half a second of run-up, so the approach is visible before the play itself
        RewindScreen.Open(Main.I, run, insights, null, MathF.Max(0, p.T0 - 0.5f));
    }

    void Practice(string key)
    {
        Close();
        CoachData.StartDrill(key);
    }

    static string Clock(float s)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, s));
        return t.TotalHours >= 1
            ? t.ToString(@"h\:mm\:ss", Inv)
            : t.ToString(@"m\:ss", Inv);
    }

    static Color GradeColor(PlayGrade g) => g switch
    {
        PlayGrade.Excellent => UiTheme.Good,
        PlayGrade.Good => UiTheme.Teal,
        PlayGrade.Fair => UiTheme.Warn,
        PlayGrade.Poor => new Color(1f, 0.55f, 0.25f),
        _ => UiTheme.Accent,
    };

    // ---- small builders ----

    Label Lbl(string text, Font f, float px, Color c)
    {
        var l = new Label
        {
            Text = text,
            LabelSettings = UiTheme.Label(f, px, k, c),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        return l;
    }

    Label Para(string text, Font f, float px, Color c)
    {
        var l = Lbl(text, f, px, c);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        l.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        l.CustomMinimumSize = new Vector2(80 * k, 0);
        return l;
    }

    VButton Btn(string label, VButton.Look look, Action act, float w, float h, float px)
    {
        var b = new VButton
        {
            Label = label, Kind = look, K = k, FontPx = px,
            CustomMinimumSize = new Vector2(w * k, h * k),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        b.Pressed += act;
        return b;
    }

    static VBoxContainer VBox(float sep)
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", (int)sep);
        return v;
    }

    static HBoxContainer HBox(float sep)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", (int)sep);
        return h;
    }

    static Control Spacer(float w, float h) =>
        new() { CustomMinimumSize = new Vector2(w, h), MouseFilter = Control.MouseFilterEnum.Ignore };
}
