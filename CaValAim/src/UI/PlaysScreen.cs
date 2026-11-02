using System.Globalization;
using Godot;
using CaValAim.Analysis;
using CaValAim.Core;
using CaValAim.Game;
using static Godot.Control;

namespace CaValAim.UI;

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

    const int PlaysPerPage = 40;
    const int SecondsPerPage = 14;

    readonly RunTelemetry run;
    readonly PlayAnalysis plays;
    readonly RunInsights? insights;
    readonly bool canRewind;

    Control? page;
    float k = 1f;
    int playPage, secPage, selectedSecond = -1;
    /// <summary>Which half of the screen is open: the play cards (default) or the second-by-second panel. Two tabs, because
    /// at 1024x768 and 1280x720 the timeline pushed every play card below the fold.</summary>
    bool showSeconds;
    TimelineBars? bars;
    TimelineTable? secTable;
    Label? secPageLbl, secInfo;
    VButton? secPrev, secNext, secWatch;

    /// <summary>Called when the list closes (the results screen uses it to bring its panel back).</summary>
    public Action? Closed;

    public PlaysScreen(RunTelemetry run, PlayAnalysis plays, RunInsights? insights = null, bool seconds = false)
    {
        this.run = run;
        this.plays = plays;
        this.insights = insights;
        showSeconds = seconds;
        canRewind = RewindData.CanRewind(run);
    }

    /// <summary>Opens the list above everything (a CanvasLayer, like the Rewind) under <paramref name="parent"/>.</summary>
    public static PlaysScreen Open(Node parent, RunTelemetry run, PlayAnalysis plays, RunInsights? insights = null, bool seconds = false)
    {
        var s = new PlaysScreen(run, plays, insights, seconds) { Name = "Plays" };
        parent.AddChild(s);
        return s;
    }

    /// <summary>Dev-only "&lt;godot&gt; --path . -- --plays-file run.vtt [--plays-seconds]": one saved run's plays (or its
    /// second-by-second panel) over the menu.</summary>
    public static void OpenDevFile(string path, bool seconds = false)
    {
        var run = RunTelemetry.Load(path);
        if (run == null) { Log.Error($"[plays] can't read {path}"); return; }
        run.SourceFile ??= Path.GetFullPath(path); // WATCH needs the .vtr sidecar (3D) next to it
        var plays = PlayByPlay.Analyze(run);
        Log.Info($"[plays] {path}: kind={plays.Kind} n={plays.Plays.Count} avg={plays.Average:0.0}");
        Open(Main.I, run, plays, RewindData.Ready(run), seconds);
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
        if (plays.Timeline is { HasData: true }) col.AddChild(SectionTabs());

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

        // Two tabs: the play cards or the full second-by-second panel. Before this, the timeline sat above the cards
        // and at 720p the screen opened with no play visible at all.
        if (plays.Timeline is { HasData: true } timeline && showSeconds)
        {
            list.AddChild(TimelinePanel(timeline));
            return;
        }

        // The verdict scrolls with the cards: an autowrapped paragraph cannot set the page's minimum height, or the
        // list (which has no height of its own) would be left with nothing on a short window.
        if (plays.HasData) list.AddChild(Verdict());

        if (!plays.HasData)
        {
            list.AddChild(Para(plays.Verdict, UiTheme.Body, 17, UiTheme.Dim));
            return;
        }
        int pages = Math.Max(1, (plays.Plays.Count + PlaysPerPage - 1) / PlaysPerPage);
        playPage = Math.Clamp(playPage, 0, pages - 1);
        if (pages > 1) list.AddChild(PlayPager(pages));
        foreach (var p in plays.Plays.Skip(playPage * PlaysPerPage).Take(PlaysPerPage)) list.AddChild(Card(p));
        if (pages > 1) list.AddChild(PlayPager(pages));
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

    Control SectionTabs()
    {
        var row = HBox(4 * k);
        row.CustomMinimumSize = new Vector2(0, 38 * k);
        void Tab(string label, bool seconds)
        {
            var b = new VButton
            {
                Kind = VButton.Look.Select, K = k, Label = label, FontPx = 12.5f,
                CustomMinimumSize = new Vector2(230 * k, 36 * k),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Selected = showSeconds == seconds,
            };
            b.Pressed += () =>
            {
                if (showSeconds == seconds) return;
                showSeconds = seconds;
                Callable.From(Rebuild).CallDeferred();
            };
            row.AddChild(b);
        }
        Tab(L("PLAYS", "JUGADAS"), false);
        Tab(L("SECOND BY SECOND", "SEGUNDO A SEGUNDO"), true);
        return row;
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

    // =====================================================================================
    // second-by-second timeline
    // =====================================================================================

    Control PlayPager(int pages)
    {
        var row = HBox(8 * k);
        int from = playPage * PlaysPerPage + 1;
        int to = Math.Min(plays.Plays.Count, (playPage + 1) * PlaysPerPage);
        var label = Lbl(L($"PLAYS {from}-{to} OF {plays.Plays.Count}   ·   PAGE {playPage + 1}/{pages}",
                          $"JUGADAS {from}-{to} DE {plays.Plays.Count}   ·   PÁGINA {playPage + 1}/{pages}"),
                        UiTheme.HudWide, 12, UiTheme.Faint);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(label);
        var prev = Btn(L("‹ PREVIOUS", "‹ ANTERIOR"), VButton.Look.Secondary, () => { playPage--; Callable.From(Rebuild).CallDeferred(); }, 150, 36, 14);
        prev.Disabled = playPage <= 0;
        row.AddChild(prev);
        var next = Btn(L("NEXT ›", "SIGUIENTE ›"), VButton.Look.Secondary, () => { playPage++; Callable.From(Rebuild).CallDeferred(); }, 150, 36, 14);
        next.Disabled = playPage >= pages - 1;
        row.AddChild(next);
        return row;
    }

    Control TimelinePanel(RunTimeline tl)
    {
        if (selectedSecond < 0) selectedSecond = tl.Summary.BestSecond;
        var panel = new VPanel
        {
            Title = L("SECOND BY SECOND", "SEGUNDO A SEGUNDO"),
            Caption = L($"{tl.Summary.Seconds} S   ·   {tl.Summary.Shots} SHOTS",
                        $"{tl.Summary.Seconds} S   ·   {tl.Summary.Shots} DISPAROS"),
            K = k,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var v = VBox(10 * k);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.AddChild(v);

        var chips = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        chips.AddThemeConstantOverride("h_separation", (int)(8 * k));
        chips.AddThemeConstantOverride("v_separation", (int)(6 * k));
        void Chip(string text, Color c, bool filled = false) =>
            chips.AddChild(new CoachChip { Text = text, Color = c, K = k, Filled = filled });
        Chip(L($"AVERAGE {TimelineUi.N(tl.Summary.Average)}/100", $"MEDIA {TimelineUi.N(tl.Summary.Average)}/100"), UiTheme.Text, true);
        if (tl.Summary.AimSeconds > 0)
            Chip(L($"ON TARGET {TimelineUi.Pct(tl.Summary.OnTargetPct)}", $"EN OBJETIVO {TimelineUi.Pct(tl.Summary.OnTargetPct)}"), UiTheme.Teal);
        if (tl.Summary.Shots > 0)
            Chip(L($"ACCURACY {TimelineUi.Pct(tl.Summary.Accuracy)}", $"ACIERTO {TimelineUi.Pct(tl.Summary.Accuracy)}"),
                tl.Summary.Accuracy >= 0.5f ? UiTheme.Good : UiTheme.Warn);
        if (tl.Summary.AimSeconds > 0)
            Chip(L($"AIM ERROR {TimelineUi.N(tl.Summary.AimErrDeg, "0.00")}°", $"ERROR DE MIRA {TimelineUi.N(tl.Summary.AimErrDeg, "0.00")}°"), UiTheme.Dim);
        if (tl.Summary.DeadSeconds > 0)
            Chip(L($"DEAD TIME {TimelineUi.Pct(tl.Summary.DeadPct)}", $"TIEMPO MUERTO {TimelineUi.Pct(tl.Summary.DeadPct)}"),
                tl.Summary.DeadPct >= 0.25f ? UiTheme.Warn : UiTheme.Dim);
        Chip(L($"TREND {TimelineUi.Signed(tl.Summary.Trend)}", $"TENDENCIA {TimelineUi.Signed(tl.Summary.Trend)}"),
            tl.Summary.Trend > 3 ? UiTheme.Good : tl.Summary.Trend < -3 ? UiTheme.Accent : UiTheme.Dim);
        if (tl.Summary.Kills + tl.Summary.Deaths > 0) Chip($"K/D {tl.Summary.Kills}/{tl.Summary.Deaths}", UiTheme.TextSoft);
        if (tl.Summary.ShotsPerMinute > 0)
            Chip(L($"{TimelineUi.N(tl.Summary.ShotsPerMinute)} SHOTS/MIN", $"{TimelineUi.N(tl.Summary.ShotsPerMinute)} DISPAROS/MIN"), UiTheme.Dim);
        v.AddChild(chips);

        bars = new TimelineBars
        {
            Tl = tl, K = k, Selected = selectedSecond,
            CustomMinimumSize = new Vector2(0, 128 * k),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        bars.Picked = SelectSecond;
        bars.TooltipText = L("Click a bar to inspect that second", "Haz clic en una barra para inspeccionar ese segundo");
        v.AddChild(bars);

        var jump = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        jump.AddThemeConstantOverride("h_separation", (int)(8 * k));
        jump.AddThemeConstantOverride("v_separation", (int)(6 * k));
        void Jump(string label, int second, Color col)
        {
            // Sized to its own text: a fixed width clipped "TRAMO MÁS FLOJO" on the narrow layouts.
            float w = MathF.Max(150, Gfx.TextW(UiTheme.HudWide, label, UiTheme.Fs(13, k)) / k + 40);
            var b = Btn(label, VButton.Look.Ghost, () => SelectSecond(second), w, 32, 13);
            b.TitleFont = UiTheme.HudWide;
            b.AddThemeColorOverride("font_color", col);
            jump.AddChild(b);
        }
        if (tl.Summary.BestSecond >= 0)
            Jump(L($"BEST {Clock(tl.Summary.BestSecond)}   ·   {tl.Summary.BestScore}/100",
                   $"MEJOR {Clock(tl.Summary.BestSecond)}   ·   {tl.Summary.BestScore}/100"), tl.Summary.BestSecond, UiTheme.Good);
        if (tl.Summary.WorstSecond >= 0)
            Jump(L($"WORST {Clock(tl.Summary.WorstSecond)}   ·   {tl.Summary.WorstScore}/100",
                   $"PEOR {Clock(tl.Summary.WorstSecond)}   ·   {tl.Summary.WorstScore}/100"), tl.Summary.WorstSecond, UiTheme.Accent);
        if (tl.Summary.WorstWindow >= 0)
            Jump(L($"WEAKEST STRETCH {Clock(tl.Summary.WorstWindow)}", $"TRAMO MÁS FLOJO {Clock(tl.Summary.WorstWindow)}"),
                tl.Summary.WorstWindow, UiTheme.Warn);
        v.AddChild(jump);

        foreach (var f in tl.Actionable(3)) v.AddChild(FindingRow(f));

        secTable = new TimelineTable
        {
            Tl = tl, K = k, Page = secPage, PageSize = SecondsPerPage, Selected = selectedSecond,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        secTable.Picked = SelectSecond;
        v.AddChild(secTable);

        var pager = HBox(8 * k);
        var range = Lbl(L("CLICK A ROW FOR THE FULL SECOND", "CLIC EN UNA FILA PARA VER EL SEGUNDO COMPLETO"),
                        UiTheme.HudWide, 10.5f, UiTheme.Faint);
        range.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        pager.AddChild(range);
        secPrev = Btn("‹", VButton.Look.Secondary, () => SetSecPage(secPage - 1), 42, 32, 16);
        secPrev.Dir = -1;
        pager.AddChild(secPrev);
        secPageLbl = Lbl("", UiTheme.HudWide, 11.5f, UiTheme.Dim);
        secPageLbl.CustomMinimumSize = new Vector2(64 * k, 0);
        secPageLbl.HorizontalAlignment = HorizontalAlignment.Center;
        pager.AddChild(secPageLbl);
        secNext = Btn("›", VButton.Look.Secondary, () => SetSecPage(secPage + 1), 42, 32, 16);
        secNext.Dir = 1;
        pager.AddChild(secNext);
        v.AddChild(pager);

        var info = HBox(10 * k);
        secInfo = Lbl("", UiTheme.Body, 12.5f, UiTheme.TextSoft);
        secInfo.ClipText = true;
        secInfo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        secInfo.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        info.AddChild(secInfo);
        secWatch = Btn(L("WATCH", "VER"), VButton.Look.Secondary, () =>
        {
            if (selectedSecond >= 0) RewindScreen.Open(Main.I, run, insights, null, MathF.Max(0, selectedSecond - 0.5f));
        }, 96, 32, 14);
        secWatch.Visible = false;
        info.AddChild(secWatch);
        v.AddChild(info);

        SetSecPage(Math.Clamp(selectedSecond, 0, Math.Max(0, tl.Seconds.Count - 1)) / SecondsPerPage);
        UpdateSecInfo();
        return panel;
    }

    Control FindingRow(TimelineFinding f)
    {
        var col = TimelineUi.Tone(f.Tone);
        var row = HBox(10 * k);
        row.AddChild(new ColorRect
        {
            Color = col,
            CustomMinimumSize = new Vector2(MathF.Max(2 * k, 2), 28 * k),
            MouseFilter = MouseFilterEnum.Ignore,
        });
        var text = VBox(3 * k);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var title = Lbl(f.Title().ToUpperInvariant(), UiTheme.HudWide, 12, col);
        text.AddChild(title);
        text.AddChild(Para(f.Detail(), UiTheme.Body, 13.5f, UiTheme.Dim));
        row.AddChild(text);
        if (f.Second >= 0)
        {
            var b = Btn(L("SEE", "VER"), VButton.Look.Ghost, () => SelectSecond(f.Second), 70, 32, 13);
            b.TitleFont = UiTheme.HudWide;
            b.SizeFlagsVertical = SizeFlags.ShrinkEnd;
            row.AddChild(b);
        }
        var wrap = VBox(0);
        wrap.AddChild(row);
        wrap.AddChild(Spacer(0, 2 * k));
        return wrap;
    }

    void SetSecPage(int p)
    {
        var tl = plays.Timeline;
        if (tl == null || secTable == null) return;
        int pages = tl.PageCount(SecondsPerPage);
        secPage = Math.Clamp(p, 0, pages - 1);
        secTable.Page = secPage;
        secTable.QueueRedraw();
        if (secPageLbl != null) secPageLbl.Text = $"{secPage + 1} / {pages}";
        if (secPrev != null) secPrev.Disabled = secPage <= 0;
        if (secNext != null) secNext.Disabled = secPage >= pages - 1;
    }

    void SelectSecond(int i)
    {
        var tl = plays.Timeline;
        if (tl == null || i < 0 || i >= tl.Seconds.Count) return;
        selectedSecond = i;
        if (bars != null) { bars.Selected = i; bars.QueueRedraw(); }
        if (secTable != null) secTable.Selected = i;
        int page = i / SecondsPerPage;
        if (page != secPage) { secPage = page; SetSecPage(page); }
        else if (secTable != null) secTable.QueueRedraw();
        UpdateSecInfo();
    }

    void UpdateSecInfo()
    {
        var tl = plays.Timeline;
        if (tl == null || secInfo == null) return;
        if (selectedSecond < 0 || selectedSecond >= tl.Seconds.Count)
        {
            secInfo.Text = L("Click a bar or a row to inspect that second.",
                             "Haz clic en una barra o en una fila para inspeccionar ese segundo.");
            secInfo.TooltipText = "";
            if (secWatch != null) secWatch.Visible = false;
            return;
        }
        var s = tl.Seconds[selectedSecond];
        var b = new System.Text.StringBuilder();
        b.Append(Clock(s.T)).Append("   ·   ").Append(TimelineText.TagName(s.Tag)).Append($"   ·   {s.Score}/100");
        if (s.FocusFrames > 0)
            b.Append($"   ·   {TimelineUi.Pct(s.OnTargetPct)} {L("on target", "en objetivo")}   ·   {TimelineUi.N(s.AimErrDeg, "0.00")}°");
        if (s.Shots > 0)
            b.Append($"   ·   {s.Hits}/{s.Shots} {L("shots", "disparos")} ({TimelineUi.Pct(s.Accuracy)})");
        b.Append($"   ·   {TimelineUi.N(s.SpeedAvg, "0.0")} m/s   ·   {TimelineUi.N(s.MouseCounts)} {L("counts", "cuentas")}");
        if (s.TurnDeg >= 1) b.Append($"   ·   {TimelineUi.N(s.TurnDeg)}° {L("turn", "giro")}");
        if (s.Kills + s.Deaths > 0) b.Append($"   ·   {s.Kills}K {s.Deaths}D");
        if (s.Events > 0) b.Append($"   ·   {s.Events} {L("events", "eventos")}");
        secInfo.Text = b.ToString();
        secInfo.TooltipText = b.ToString();
        if (secWatch != null) secWatch.Visible = canRewind;
    }

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

/// <summary>Numbers and colours shared by the timeline widgets, so the chart and the table never disagree.</summary>
internal static class TimelineUi
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string N(float v, string fmt = "0") => v.ToString(fmt, Inv);
    public static string Pct(float v) => N(Mathf.Clamp(v, 0, 1) * 100f);
    public static string Signed(float v) => (v >= 0 ? "+" : "-") + N(MathF.Abs(v), "0.0");

    public static Color Tone(TimelineTone t) => t switch
    {
        TimelineTone.Good => UiTheme.Good,
        TimelineTone.Warn => UiTheme.Warn,
        TimelineTone.Bad => UiTheme.Accent,
        _ => UiTheme.Dim,
    };

    public static Color Score(int score) => score >= 80 ? UiTheme.Good
        : score >= 60 ? UiTheme.Teal
        : score >= 45 ? UiTheme.Warn
        : score >= 30 ? new Color(1f, 0.55f, 0.25f)
        : UiTheme.Accent;
}

/// <summary>
/// The one-second bars of a run: height is the coach's score for that second, colour is its tone. Clicking a bar selects
/// that second (the table below jumps to it and the results line explains it).
/// </summary>
public partial class TimelineBars : Control
{
    public RunTimeline Tl = null!;
    public float K = 1f;
    public int Selected = -1;
    public Action<int>? Picked;

    Rect2 plot;

    public TimelineBars() => MouseFilter = MouseFilterEnum.Pass;

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) return;
        int n = Tl.Seconds.Count;
        if (n == 0 || plot.Size.X <= 0 || !plot.HasPoint(mb.Position)) return;
        float u = Mathf.Clamp((mb.Position.X - plot.Position.X) / plot.Size.X, 0f, 1f);
        Picked?.Invoke(Mathf.Clamp((int)(u * n), 0, n - 1));
    }

    public override void _Draw()
    {
        int n = Tl.Seconds.Count;
        plot = new Rect2(34 * K, 8 * K, MathF.Max(1, Size.X - 42 * K), MathF.Max(1, Size.Y - 30 * K));
        int fs = UiTheme.Fs(9.5f, K);
        for (int g = 0; g <= 4; g++)
        {
            float y = plot.Position.Y + plot.Size.Y * (1 - g / 4f);
            if (g > 0) DrawRect(new Rect2(plot.Position.X, y, plot.Size.X, MathF.Max(1, K * 0.6f)), new Color(1, 1, 1, 0.055f));
            Gfx.TextR(this, UiTheme.Hud, $"{g * 25}", plot.Position.X - 6 * K, y + fs * 0.36f, fs, UiTheme.Faint);
        }
        if (n == 0) return;

        int cols = Math.Max(1, Math.Min(n, (int)(plot.Size.X / MathF.Max(2 * K, 1f))));
        for (int c = 0; c < cols; c++)
        {
            int i0 = c * n / cols, i1 = Math.Max(i0 + 1, (c + 1) * n / cols);
            float sum = 0;
            int live = 0;
            for (int i = i0; i < i1; i++)
            {
                sum += Tl.Seconds[i].Score;
                if (!Tl.Seconds[i].Dead) live++;
            }
            float avg = sum / (i1 - i0);
            float x = plot.Position.X + c * plot.Size.X / cols;
            float w = MathF.Max(1, plot.Size.X / cols - 0.8f * K);
            if (live == 0)
            {
                DrawRect(new Rect2(x, plot.End.Y - 2 * K, w, 2 * K), new Color(UiTheme.Faint, 0.35f));
                continue;
            }
            float hgt = MathF.Max(2 * K, plot.Size.Y * Mathf.Clamp(avg / 100f, 0f, 1f));
            DrawRect(new Rect2(x, plot.End.Y - hgt, w, hgt), new Color(TimelineUi.Score((int)MathF.Round(avg)), 0.88f));
        }

        if (Selected >= 0 && Selected < n)
        {
            float x = plot.Position.X + (Selected + 0.5f) / n * plot.Size.X;
            DrawRect(new Rect2(x, plot.Position.Y, MathF.Max(1, 1.5f * K), plot.Size.Y), new Color(1, 1, 1, 0.85f));
            Gfx.Diamond(this, new Vector2(x, plot.Position.Y), 3.5f * K, 3.5f * K, Colors.White);
        }

        Gfx.Text(this, UiTheme.Hud, "0:00", plot.Position.X, Size.Y - 4 * K, fs, UiTheme.Faint);
        Gfx.TextR(this, UiTheme.Hud, TimelineText.Clock(Tl.Duration), plot.End.X, Size.Y - 4 * K, fs, UiTheme.Faint);
        Gfx.TextC(this, UiTheme.Hud, TimelineText.Clock(Tl.Duration / 2), plot.Position.X + plot.Size.X / 2,
            Size.Y - 4 * K, fs, UiTheme.Faint);
    }
}

/// <summary>Paged table of the timeline: one row per second, all the measured values, clickable.</summary>
public partial class TimelineTable : Control
{
    public RunTimeline Tl = null!;
    public float K = 1f;
    public int Page, PageSize = 14, Selected = -1;
    public Action<int>? Picked;

    float RowH => 22 * K;
    float HeadH => 22 * K;

    public TimelineTable() => MouseFilter = MouseFilterEnum.Pass;

    public override void _Ready() => CustomMinimumSize = new Vector2(0, HeadH + PageSize * RowH + 4 * K);

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) return;
        float y = mb.Position.Y - HeadH;
        if (y < 0) return;
        int row = (int)(y / RowH);
        if (row < 0 || row >= PageSize) return;
        int i = Page * PageSize + row;
        if (i < 0 || i >= Tl.Seconds.Count) return;
        Picked?.Invoke(i);
    }

    public override void _Draw()
    {
        int n = Tl.Seconds.Count;
        int start = Math.Max(0, Page * PageSize), end = Math.Min(n, start + PageSize);
        int fs = UiTheme.Fs(11, K);
        int hs = UiTheme.Fs(9.5f, K);
        bool es = Loc.Spanish;

        string[] head =
        {
            "T", es ? "SEGUNDO" : "SECOND", es ? "PUNTOS" : "SCORE", es ? "EN OBJETIVO" : "ON TARGET",
            es ? "ERROR" : "AIM ERR", es ? "DISPAROS" : "SHOTS", es ? "VELOCIDAD" : "SPEED", "K/D",
        };
        float[] frac = { 0.07f, 0.24f, 0.10f, 0.14f, 0.11f, 0.13f, 0.10f, 0.11f };

        float x = 0;
        for (int c = 0; c < frac.Length; c++)
        {
            float w = Size.X * frac[c];
            var align = c >= 2 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            Gfx.TextFit(this, UiTheme.HudWide, head[c], x + 2 * K, HeadH * 0.68f, hs, UiTheme.Faint, w - 5 * K, align);
            x += w;
        }
        DrawRect(new Rect2(0, HeadH - 1, Size.X, 1), new Color(1, 1, 1, 0.10f));

        if (n == 0)
        {
            Gfx.Text(this, UiTheme.Body, es ? "Sin datos por segundo en esta partida." : "No per-second data in this run.",
                2 * K, HeadH + fs * 1.4f, fs, UiTheme.Faint);
            return;
        }

        for (int r = 0; r < PageSize; r++)
        {
            int i = start + r;
            if (i >= end) break;
            float y = HeadH + r * RowH;
            var s = Tl.Seconds[i];
            if (i == Selected) DrawRect(new Rect2(0, y, Size.X, RowH), new Color(UiTheme.Accent, 0.16f));
            else if ((r & 1) == 1) DrawRect(new Rect2(0, y, Size.X, RowH), new Color(1, 1, 1, 0.025f));

            bool empty = s.Frames == 0 && s.Shots == 0 && s.Events == 0;
            var col = empty ? UiTheme.Faint : UiTheme.TextSoft;
            string[] cells =
            {
                TimelineText.Clock(s.T),
                empty ? "-" : TimelineText.TagName(s.Tag),
                empty ? "-" : $"{s.Score}",
                s.FocusFrames > 0 ? TimelineUi.Pct(s.OnTargetPct) + "%" : "-",
                s.FocusFrames > 0 ? TimelineUi.N(s.AimErrDeg, "0.00") + "°" : "-",
                s.Shots > 0 ? $"{s.Hits}/{s.Shots}" : "-",
                s.Frames > 0 ? TimelineUi.N(s.SpeedAvg, "0.0") : "-",
                s.Kills + s.Deaths > 0 ? $"{s.Kills}/{s.Deaths}" : "-",
            };
            Color[] colors =
            {
                UiTheme.Faint, col, empty ? UiTheme.Faint : TimelineUi.Score(s.Score), col, col, col, col,
                s.Deaths > 0 ? UiTheme.Accent : s.Kills > 0 ? UiTheme.Good : col,
            };

            float baseline = y + RowH * 0.5f + fs * 0.36f;
            x = 0;
            for (int c = 0; c < frac.Length; c++)
            {
                float w = Size.X * frac[c];
                var align = c >= 2 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                Gfx.TextFit(this, c == 0 ? UiTheme.Hud : UiTheme.Body, cells[c], x + 2 * K, baseline, fs, colors[c], w - 5 * K, align);
                x += w;
            }
        }
    }
}
