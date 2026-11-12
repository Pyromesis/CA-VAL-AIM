using System.Globalization;
using Godot;
using CaValAim.Analysis;
using CaValAim.Core;
using CaValAim.Game;

namespace CaValAim.UI;

/// <summary>
/// Aim coach / skill profile. Left: estimated overall rank (emblem, rank range, confidence, Iron→Radiant ladder)
/// and the 8-skill breakdown (radar + rows). Right: the problems the coach found as expandable cards (evidence,
/// why it happens, how to fix it, drill buttons), strengths, and the sensitivity recommendation. The profile is
/// built on a background thread (<see cref="CoachData"/>), so the screen opens instantly with a loading state.
/// </summary>
public partial class ProfileScreen : ScreenBase
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    SkillProfile? profile;
    readonly HashSet<string> expanded = new();
    bool expandInit;
    DrawBox? spinner;
    float spinT;
    // sensitivity state before "Try it" (restored by undo; static so it survives leaving and reopening the screen)
    static bool? prevUse;
    static float prevSens;

    public override void _Ready()
    {
        CoachData.Request();
        profile = CoachData.Ready;
        base._Ready();
    }

    protected override void Tick(float dt)
    {
        PollShowMe();
        PollLastRead();
        if (profile != null) return;
        spinT += dt;
        spinner?.QueueRedraw();
        if (CoachData.Ready is { } p)
        {
            profile = p;
            Rebuild();
        }
    }

    protected override void Build()
    {
        float k = K;
        spinner = null;
        var root = Margins(VBox(16 * k), 48 * k, 26 * k, 48 * k, 30 * k);
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(root);
        var col = (VBoxContainer)root.GetChild(0);
        col.AddChild(HeaderRow());

        if (profile == null)
        {
            col.AddChild(Loading());
            return;
        }
        var p = profile;
        if (!expandInit)
        {
            expandInit = true;
            if (p.Problems.Count > 0) expanded.Add(p.Problems[0].Id);
            if (Main.I.Dev && Core.CmdLine.After("--showme") is { } sc) expanded.Add(sc); // dev: open the card being shown
        }

        var body = HBox(24 * k);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        col.AddChild(body);
        float avail = GetViewportRect().Size.X - 96 * k - 24 * k;
        var left = VBox(16 * k);
        left.CustomMinimumSize = new Vector2(Mathf.Clamp(avail * 0.44f, 340 * k, 820 * k), 0);
        body.AddChild(left);
        // The right column is a whole page of cards (plan, conclusions, problems, last run, sens). It used to be a plain
        // VBox, so any card wider than the column (the old plan button row) pushed the page past the window and the
        // right-hand content was cut off. A vertical scroll gives every card the height it needs and keeps the width
        // locked to the column; horizontal scrolling stays off.
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddChild(scroll);
        var right = VBox(16 * k);
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(right);

        left.AddChild(new DrawBox
        {
            CustomMinimumSize = new Vector2(0, 250 * k), OnDraw = d => DrawRank(d, p), MouseFilter = MouseFilterEnum.Pass,
            TooltipText = "An aim-only estimate: CA-VAL-AIM rates your mechanics (aim, movement, flash dodging).\nYour in-game rank also depends on game sense, comms and utility.",
        });
        left.AddChild(SkillsPanel(p));
        var plan = CoachPlan.Build(p);
        right.AddChild(PlanPanel(p, plan));
        right.AddChild(ConclusionsPanel(p));
        right.AddChild(ProblemsPanel(p));
        right.AddChild(PlaysPanel());
        right.AddChild(SensPanel(p));

        // Dev screenshot path (`--createplan`): press CREATE PLAYLIST for the profile on screen, then open the playlist
        // editor, so a shot proves the generated series (order, runs, recoil map) reached the real playlist file.
        if (Main.I.Dev && Core.CmdLine.Has("--createplan") && !devPlanDone)
        {
            devPlanDone = true;
            Callable.From(() =>
            {
                CoachData.CreatePlaylist(plan);
                Main.I.ShowScreen(new PlaylistsScreen());
            }).CallDeferred();
        }
    }

    // ---------------- header / loading ----------------

    Control HeaderRow()
    {
        float k = K;
        var head = HBox(16 * k);
        var title = Heading("AIM COACH", "YOUR ESTIMATED RANK · WHAT HOLDS YOU BACK · HOW TO FIX IT");
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        if (CoachData.Fake && !Core.CmdLine.Showcase)
        {
            var tag = Lbl($"DEV · FAKE COACH DATA ({CoachData.FakeVariant!.ToUpperInvariant()})", UiTheme.HudWide, 13, UiTheme.Warn);
            tag.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            head.AddChild(tag);
        }
        var back = Btn("BACK", VButton.Look.Primary, Main.I.ShowMenu, 180, 52, 22);
        back.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(back);
        return head;
    }

    Control Loading()
    {
        var panel = new VPanel { K = K, SizeFlagsVertical = SizeFlags.ExpandFill };
        spinner = new DrawBox
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            OnDraw = d =>
            {
                var c = d.Size / 2;
                CoachDraw.Spinner(d, c - new Vector2(0, 24 * K), 26 * K, K, spinT);
                Gfx.TextC(d, UiTheme.HudWide, "ANALYSING YOUR RUNS…", c.X, c.Y + 40 * K, UiTheme.Fs(15, K), UiTheme.Dim);
            },
        };
        panel.AddChild(spinner);
        return panel;
    }

    // ---------------- overall rank ----------------

    void DrawRank(DrawBox d, SkillProfile p)
    {
        float k = K;
        var r = new Rect2(Vector2.Zero, d.Size);
        Mn.Card(d, r, k);
        // a single accent run on the top edge; the rank colour lives in the emblem and the range
        d.DrawRect(new Rect2(0, 0, 46 * k, Mathf.Max(1.5f, 2 * k)), UiTheme.Accent);
        bool ranked = CoachData.HasOverall(p);
        float t = p.OverallTier;
        var rc = ranked ? CoachRank.Col(t) : UiTheme.NoTier;

        // emblem with a confidence ring
        var ec = new Vector2(132 * k, r.Size.Y / 2);
        float er = 62 * k, ring = er * 1.45f;
        d.DrawCircle(ec, ring, new Color(rc, ranked ? 0.08f : 0.035f));
        d.DrawArc(ec, ring, 0, Mathf.Tau, 72, new Color(rc, ranked ? 0.25f : 0.16f), Mathf.Max(1, 2 * k), true);
        if (ranked)
        {
            float a0 = -Mathf.Pi / 2;
            d.DrawArc(ec, ring, a0, a0 + Mathf.Tau * Mathf.Clamp(p.OverallConfidence, 0.02f, 1f), 72, rc, Mathf.Max(2, 3.5f * k), true);
        }
        RankEmblem.Draw(d, ec, er, CoachRank.EmblemTier(ranked ? t : -1));

        float x0 = 268 * k, right = r.Size.X - 32 * k, tw = right - x0;
        int cs = UiTheme.Fs(14, k), ms = UiTheme.Fs(12, k);
        const string caption = "ESTIMATED RANK · AIM ONLY";
        Gfx.Text(d, UiTheme.HudWide, caption, x0, Gfx.Mid(46 * k, cs), cs, UiTheme.Dim);
        string runs = Loc.Spanish
            ? $"{p.RunsAnalyzed} PARTIDA{(p.RunsAnalyzed == 1 ? "" : "S")} · {When(p.Updated)}"
            : $"{p.RunsAnalyzed} RUN{(p.RunsAnalyzed == 1 ? "" : "S")} · {When(p.Updated)}";
        string meta = p.RunsAnalyzed > 0
            ? (Loc.Spanish ? "BASADO EN " + runs : "BASED ON " + runs)
            : (Loc.Spanish ? "AÚN NO HAY PARTIDAS ANALIZADAS" : "NO COACHED RUNS YET");
        // Narrow (4:3 / 5:4) windows: shorten, or drop, the caption rather than overlap "ESTIMATED RANK".
        float room = tw - Gfx.TextW(UiTheme.HudWide, caption, cs) - 16 * k;
        if (Gfx.TextW(UiTheme.HudWide, meta, ms) > room && p.RunsAnalyzed > 0) meta = runs;
        if (Gfx.TextW(UiTheme.HudWide, meta, ms) <= room)
            Gfx.TextR(d, UiTheme.HudWide, meta, right, Gfx.Mid(46 * k, ms), ms, UiTheme.Faint);

        int fs = UiTheme.Fs(15, k);
        if (ranked)
        {
            // The band comes from the evidence itself (sample size, coverage, how much the skills disagree), not from
            // a fixed spread around the point estimate.
            var est = p.Estimate;
            var (lo, hi) = est is { } e ? CoachRank.Band(e.Low, e.High) : CoachRank.Range(t, p.OverallConfidence);
            RangeTitle(d, lo, hi, x0, 114 * k, tw, k);
            float y = 148 * k;
            float bw = CoachDraw.ConfBars(d, new Vector2(x0, y), k, p.OverallConfidence);
            string conf = ConfCaption(p.OverallConfidence);
            float cw = Gfx.TextW(UiTheme.HudWide, conf, fs);
            Gfx.Text(d, UiTheme.HudWide, conf, x0 + bw + 10 * k, Gfx.Mid(y, fs), fs, CoachRank.ConfidenceColor(p.OverallConfidence));
            float nx = x0 + bw + 10 * k + cw + 14 * k;
            Gfx.TextFit(d, UiTheme.Body, $"·   most likely {Pretty(CoachRank.Name(t))}", nx, Gfx.Mid(y, fs), fs, UiTheme.Dim, right - nx);
        }
        else
        {
            Gfx.Text(d, UiTheme.Light, "UNRANKED", x0, 114 * k, UiTheme.Fs(64, k), UiTheme.Dim);
            int rated = p.Skills.Count(CoachRank.Rated);
            int need = CoachData.RunsNeeded();
            string msg = rated == 0
                ? (Loc.Spanish ? $"Aún no hay suficientes datos: el coach necesita unas {(need > 0 ? need : 3)} partidas más."
                    : $"Not enough data yet — the coach needs about {(need > 0 ? need : 3)} more runs.")
                : (Loc.Spanish ? $"{rated} de 8 habilidades valoradas: 4 con confianza media desbloquean tu rango general."
                    : $"{rated} of 8 skills rated - 4 with medium confidence unlock your overall rank.");
            Gfx.TextFit(d, UiTheme.Body, msg, x0, Gfx.Mid(148 * k, fs), fs, UiTheme.Dim, tw);
        }
        // Where the estimate sits in the ranked population: more honest than the rank name alone, and it explains
        // why the band is narrow around Gold and wide near Radiant.
        if (ranked && !float.IsNaN(p.OverallPercentile))
        {
            string pct = Loc.Spanish
                ? $"MEJOR QUE EL {p.OverallPercentile:0}% DE LA POBLACIÓN RANKED"
                : $"BETTER THAN {p.OverallPercentile:0}% OF THE RANKED POPULATION";
            Gfx.TextFit(d, UiTheme.HudWide, pct, x0, 170 * k, UiTheme.Fs(11.5f, k), UiTheme.Faint, tw);
        }
        // Fonts hit their size floor in small windows, so all five tier names would collide: keep the coloured
        // ladder and the confidence marker, but omit the labels when the UI is scaled down.
        CoachDraw.Ladder(d, new Rect2(x0, 186 * k, tw, 12 * k), k, ranked ? t : -1, p.OverallConfidence, labels: k >= 0.8f,
            band: p.Estimate is { } b ? (b.Low, b.High) : null);
    }

    static void RangeTitle(CanvasItem d, string lo, string? hi, float x, float baseline, float maxW, float k)
    {
        const string sep = "  –  ";
        var f = UiTheme.Light;
        int fs = UiTheme.Fs(66, k);
        float W(int s) => Gfx.TextW(f, lo, s) + (hi == null ? 0 : Gfx.TextW(f, sep, s) + Gfx.TextW(f, hi, s));
        while (fs > 20 && W(fs) > maxW) fs -= 2;
        Gfx.Text(d, f, lo, x, baseline, fs, CoachRank.Col(lo));
        if (hi == null) return;
        x += Gfx.TextW(f, lo, fs);
        Gfx.Text(d, f, sep, x, baseline, fs, UiTheme.Faint);
        x += Gfx.TextW(f, sep, fs);
        Gfx.Text(d, f, hi, x, baseline, fs, CoachRank.Col(hi));
    }

    static string Pretty(string rank) => rank.Length == 0 ? rank : rank[0] + rank[1..].ToLowerInvariant();

    static string ConfWord(float c) => c < 0.5f ? "BAJA" : c < 0.75f ? "MEDIA" : "ALTA";
    static string ConfCaption(float c) => Loc.Spanish ? $"CONFIANZA {ConfWord(c)}" : $"{CoachRank.Confidence(c)} CONFIDENCE";

    static string When(DateTime u)
    {
        var age = DateTime.Now - u;
        if (age.TotalMinutes < 1) return Loc.Spanish ? "AHORA MISMO" : "JUST NOW";
        if (u.Date == DateTime.Today) return (Loc.Spanish ? "HOY " : "TODAY ") + u.ToString("HH:mm", Inv);
        return (Loc.Spanish ? $"{u.Day} {Loc.MonthShort(u)}" : u.ToString("MMM d", Inv)).ToUpperInvariant();
    }

    // ---------------- skills ----------------

    Control SkillsPanel(SkillProfile p)
    {
        float k = K;
        int rated = p.Skills.Count(CoachRank.Rated);
        // At 1024x768 the two-column body leaves ~460 px for this panel. A radar plus a readable skill list does not
        // fit there, so drop the chart (the rows still carry every rating) and use the full width for the list.
        bool narrow = GetViewportRect().Size.X < 1360;
        var panel = new VPanel
        {
            Title = "SKILL BREAKDOWN",
            Caption = narrow ? null : Loc.Spanish ? $"{rated} de 8 valoradas · pasa el ratón por una habilidad para ver detalles"
                : $"{rated} of 8 rated · hover a skill for details",
            K = k, SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var h = HBox(14 * k);
        panel.AddChild(h);
        if (!narrow) h.AddChild(new DrawBox { CustomMinimumSize = new Vector2(388 * k, 0), OnDraw = d => DrawRadar(d, p) });
        var rows = VBox(4 * k);
        rows.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        h.AddChild(rows);
        int i = 0;
        foreach (var s in Enum.GetValues<Skill>())
            rows.AddChild(new SkillRow
            {
                Skill = s, Rating = p.Skills.FirstOrDefault(r => r.Skill == s), K = k, Stripe = i++ % 2 == 0,
                Trend = p.Trend.TryGetValue(s, out var tr) ? tr : null, TrendSpan = p.TrendSpan,
            });
        return panel;
    }

    void DrawRadar(DrawBox d, SkillProfile p)
    {
        float k = K;
        // Leave room for the widest side label (the 10 px font floor makes labels relatively wider in small windows,
        // which used to push "TRACKING / DIAMOND 1" under the skill list at 1024×768).
        int nameFs = UiTheme.Fs(15, k), rankFs = UiTheme.Fs(12.5f, k);
        float labelW = 0;
        foreach (var sk in Enum.GetValues<Skill>())
        {
            var rating = p.Skills.FirstOrDefault(x => x.Skill == sk);
            labelW = Mathf.Max(labelW, Gfx.TextW(UiTheme.Display, CoachSkills.Short(sk), nameFs));
            if (CoachRank.Rated(rating)) labelW = Mathf.Max(labelW, Gfx.TextW(UiTheme.Display, CoachRank.Name(rating!.Tier), rankFs));
        }
        float margin = Mathf.Max(74 * k, labelW + 20 * k);
        float R = Mathf.Max(30 * k, Mathf.Min(d.Size.X / 2 - margin, d.Size.Y / 2 - 70 * k));
        var c = new Vector2(d.Size.X / 2, d.Size.Y / 2 - 14 * k);
        CoachDraw.Radar(d, c, R, k, p);
        int ls = UiTheme.Fs(12, k);
        float ly = c.Y + R + 62 * k;
        if (ly > d.Size.Y - 4 * k) return;
        string legend = CoachData.HasOverall(p) ? "dashed ring = your overall rank" : "centre = Iron · rim = Radiant";
        Gfx.TextC(d, UiTheme.Body, legend, d.Size.X / 2, ly, ls, UiTheme.Faint);
    }

    // ---------------- today's plan ----------------

    /// <summary>
    /// Today's plan as a vertical list (a row of buttons used to force the column wider than the window). Each step shows
    /// what it fixes, how many series it wants and whether the camera climb should be on; the two bottom buttons turn the
    /// whole plan into a playlist that plays every series with that recoil setting already applied.
    /// </summary>
    Control PlanPanel(SkillProfile p, CoachPlan plan)
    {
        float k = K;
        bool es = Loc.Spanish;
        var panel = new VPanel
        {
            Title = "TODAY'S PLAN",
            Caption = plan.IsEmpty ? null : es
                ? $"{plan.Steps.Count} pasos · {plan.TotalRuns} series · ≈{plan.Minutes} min · el tutorial de tu próxima sesión"
                : $"{plan.Steps.Count} steps · {plan.TotalRuns} runs · ≈{plan.Minutes} min · the guide to your next session",
            K = k,
        };
        if (plan.IsEmpty) return panel;
        // VPanel is a MarginContainer: it fits every direct child to the same rect, so multiple children would overlap.
        // One root VBox owns the whole card body.
        var root = VBox(0);
        root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.AddChild(root);
        var v = VBox(0);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(v);
        int n = 1;
        foreach (var step in plan.Steps)
        {
            if (n > 1) v.AddChild(new DrawBox { CustomMinimumSize = new Vector2(0, 1), OnDraw = d => d.DrawRect(new Rect2(0, 0, d.Size.X, 1), UiTheme.Line) });
            v.AddChild(PlanStepRow(step, n++, p));
        }
        v.AddChild(Spacer(0, 12 * k));

        var status = Lbl("", UiTheme.Body, 13, UiTheme.Dim);
        status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        status.CustomMinimumSize = new Vector2(60 * k, 0);
        status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        status.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        var foot = HBox(10 * k);
        var create = Btn(es ? "CREAR SERIE" : "CREATE PLAYLIST", VButton.Look.Secondary, () =>
        {
            var pl = CoachData.CreatePlaylist(plan);
            status.Text = es
                ? $"Serie «{pl.Name}» lista: {pl.Modes.Count} series. Está en SERIES."
                : $"Playlist \"{pl.Name}\" ready: {pl.Modes.Count} runs. It is in PLAYLISTS.";
        }, 200, 46, 15);
        create.TooltipText = es
            ? "Crea una serie con estos ejercicios, en este orden, y el recoil ya puesto en cada uno"
            : "Creates a playlist with these drills, in this order, with the recoil already set for each";
        var play = Btn(es ? "PRÁCTICA GUIADA" : "GUIDED PRACTICE", VButton.Look.Primary, () => PracticeScreen.Start(plan, p), 200, 46, 16);
        play.TooltipText = es
            ? "Abre el plan con instrucciones y correcciones entre ejercicios"
            : "Opens the plan with instructions and feedback between drills";
        foot.AddChild(create);
        foot.AddChild(play);
        foot.AddChild(status);
        root.AddChild(foot);
        return panel;
    }

    /// <summary>One plan step: number, drill, recoil chip, what it fixes and a start button.</summary>
    Control PlanStepRow(PlanStep step, int index, SkillProfile p)
    {
        float k = K;
        var row = HBox(12 * k);
        row.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        row.AddChild(new DrawBox
        {
            CustomMinimumSize = new Vector2(30 * k, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            OnDraw = d =>
            {
                int fs = UiTheme.Fs(22, k);
                Gfx.Text(d, UiTheme.Display, index.ToString("00"), 0, Gfx.Mid(d.Size.Y / 2, fs), fs, new Color(UiTheme.Text, 0.22f));
            },
        });

        var text = VBox(3 * k);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        text.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var top = HBox(8 * k);
        var name = Lbl(CoachData.DrillName(step.Drill).ToUpperInvariant(), UiTheme.Display, 17, UiTheme.Text);
        name.ClipText = true;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        name.CustomMinimumSize = new Vector2(40 * k, 0);
        top.AddChild(name);
        top.AddChild(new CoachChip
        {
            Text = CoachPlan.RecoilLabel(step.Recoil),
            Color = step.Recoil == RecoilAdvice.On ? UiTheme.Good : UiTheme.Warn,
            K = k,
            TooltipText = step.RecoilWhy,
        });
        text.AddChild(top);
        text.AddChild(Para(Loc.Spanish
            ? $"{CoachSkills.Short(step.Skill).ToLowerInvariant()} · {step.Why}"
            : $"{CoachSkills.Short(step.Skill).ToLowerInvariant()} · {step.Why}", UiTheme.Body, 13.5f, new Color(UiTheme.Text, 0.62f)));
        row.AddChild(text);

        var side = VBox(2 * k);
        side.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var runs = Lbl($"×{step.Runs}", UiTheme.Display, 16, UiTheme.Text);
        runs.HorizontalAlignment = HorizontalAlignment.Center;
        side.AddChild(runs);
        var start = Btn(Loc.Spanish ? "EMPEZAR" : "START", VButton.Look.Secondary,
            () => CoachData.StartDrill(step.Drill, step.Source is { } d ? CoachData.FocusFrom(d) : null), 108, 32, 13);
        start.TooltipText = Loc.Spanish
            ? $"Empezar {CoachData.DrillName(step.Drill)} · {CoachPlan.RecoilLabel(step.Recoil)}"
            : $"Start {CoachData.DrillName(step.Drill)} · {CoachPlan.RecoilLabel(step.Recoil)}";
        side.AddChild(start);
        row.AddChild(side);
        return row;
    }

    // ---------------- conclusions ----------------

    /// <summary>The written synthesis: where the player is, the one thing that would move the rank most, what already
    /// works, the trend and the sens verdict. Built from the same profile the rest of the page draws.</summary>
    Control ConclusionsPanel(SkillProfile p)
    {
        float k = K;
        var list = CoachConclusions.Build(p);
        if (list.Count == 0) return new Control();
        bool es = Loc.Spanish;
        var panel = new VPanel
        {
            Title = es ? "CONCLUSIONES" : "THE COACH'S READ",
            Caption = es ? $"síntesis de {p.RunsAnalyzed} partidas" : $"a synthesis of {p.RunsAnalyzed} runs",
            K = k,
        };
        var v = VBox(9 * k);
        panel.AddChild(v);
        foreach (var c in list)
        {
            v.AddChild(new SectionLabel { Text = c.Title, K = k });
            v.AddChild(Para(c.Body, UiTheme.Body, 15, c.Tone switch
            {
                ConclusionTone.Good => UiTheme.Good,
                ConclusionTone.Warn => UiTheme.Warn,
                ConclusionTone.Bad => new Color(UiTheme.Accent, 0.95f),
                _ => new Color(UiTheme.Text, 0.86f),
            }));
        }
        return panel;
    }

    // ---------------- play by play ----------------

    /// <summary>The newest recorded run that has plays to show (skips finder runs and pruned files).</summary>
    static (RunTelemetry Run, PlayAnalysis Plays)? LatestRun()
    {
        foreach (var r in Main.I.Stats.Runs.OrderByDescending(r => r.When).Take(12))
        {
            if (string.IsNullOrEmpty(r.TelemetryFile) || !File.Exists(r.TelemetryFile)) continue;
            RunTelemetry? run = null;
            try { run = RunTelemetry.Load(r.TelemetryFile!); } catch { /* unreadable: try the next one */ }
            if (run == null) continue;
            // The saved coach archive is the source of truth (it keeps the timeline exactly as it was analysed);
            // older runs or a missing/corrupt file fall back to recomputing from the .vtt.
            var saved = CoachArchive.LoadFor(r.TelemetryFile, r.CoachFile);
            var plays = saved?.Review.Plays;
            if (plays == null || !plays.HasData) plays = PlayByPlay.Analyze(run);
            else plays.Timeline ??= RunTimeline.Build(run, plays);
            if (plays.HasData) return (run, plays);
        }
        return null;
    }

    /// <summary>What the newest recorded run was worth, read off the main thread.</summary>
    sealed record LastRunRead(string Mode, string Verdict, string[] Drills, string Timeline);

    /// <summary>The last run being read (static: rebuilding the screen must not start the read again).</summary>
    static Task<LastRunRead?>? lastRead;
    Label? lastReadLine;

    Control PlaysPanel()
    {
        float k = K;
        var panel = new VPanel
        {
            Title = "PLAY BY PLAY",
            Caption = Loc.Spanish ? "tu última partida, jugada a jugada" : "your last run, play by play",
            K = k,
        };
        var row = HBox(14 * k);
        row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        // VPanel fits all direct children to the same rect; one root VBox keeps the button row and the verdict line stacked.
        var root = VBox(10 * k);
        root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.AddChild(root);
        root.AddChild(row);
        var status = Lbl("", UiTheme.Body, 13.5f, UiTheme.Dim);
        status.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        status.CustomMinimumSize = new Vector2(60 * k, 0);
        var b = Btn(Loc.Spanish ? "VER ÚLTIMA PARTIDA" : "OPEN LAST RUN", VButton.Look.Secondary,
            () => OpenLastPlays(status), 230, 44, 16);
        b.TooltipText = Loc.Spanish
            ? "Cada duelo, spray, reacción o round de tu última partida, con su consejo"
            : "Every engagement, spray, reaction or round of your last run, with its advice";
        row.AddChild(b);
        row.AddChild(status);

        // The verdict of the last run, in words: which run it was, how many plays of each grade, the repeated mistake
        // and the drills that fix it. Reading the telemetry and analysing 40+ plays is not free, so it runs on a worker
        // (the same way "Show me" searches) and the line fills in when it lands.
        var read = Para(Loc.Spanish ? "Leyendo tu última partida…" : "Reading your last run…", UiTheme.Body, 14.5f, UiTheme.Dim);
        lastReadLine = read;
        root.AddChild(read);
        StartLastRead();
        return panel;
    }

    /// <summary>Snapshots the recent telemetry paths (the run list is a Godot-side object) and analyses them off-thread.</summary>
    static void StartLastRead()
    {
        if (lastRead != null) return;
        List<(string File, string? Coach)> files;
        try
        {
            files = Main.I.Stats.Runs
                .OrderByDescending(r => r.When)
                .Take(12)
                .Where(r => !string.IsNullOrEmpty(r.TelemetryFile) && File.Exists(r.TelemetryFile))
                .Select(r => (r.TelemetryFile!, r.CoachFile))
                .ToList();
        }
        catch { files = new List<(string, string?)>(); }
        lastRead = Task.Run(() => Summarize(files));
    }

    static LastRunRead? Summarize(List<(string File, string? Coach)> files)
    {
        foreach (var (file, coachFile) in files)
        {
            RunTelemetry? run = null;
            try { run = RunTelemetry.Load(file); } catch { /* unreadable: try the next one */ }
            if (run == null) continue;
            var saved = CoachArchive.LoadFor(file, coachFile);
            var plays = saved?.Review.Plays;
            if (plays == null || !plays.HasData) plays = PlayByPlay.Analyze(run);
            else plays.Timeline ??= RunTimeline.Build(run, plays);
            if (!plays.HasData) continue;

            string timeline = "";
            if (plays.Timeline is { HasData: true } tl)
            {
                var t = new System.Text.StringBuilder();
                t.Append(Loc.Spanish
                    ? $"Segundo a segundo: media {tl.Summary.Average:0}/100"
                    : $"Second by second: average {tl.Summary.Average:0}/100");
                if (tl.Summary.WorstWindow >= 0)
                    t.Append(Loc.Spanish
                        ? $", tramo más flojo {TimelineText.Clock(tl.Summary.WorstWindow)} ({tl.Summary.WorstWindowScore:0}/100)"
                        : $", weakest stretch {TimelineText.Clock(tl.Summary.WorstWindow)} ({tl.Summary.WorstWindowScore:0}/100)");
                if (tl.Summary.DeadPct >= 0.15f)
                    t.Append(Loc.Spanish ? $", {tl.Summary.DeadPct * 100:0}% de tiempo muerto" : $", {tl.Summary.DeadPct * 100:0}% dead time");
                if (tl.Actionable(1).FirstOrDefault() is { } finding)
                    t.Append(". ").Append(finding.Title()).Append(": ").Append(finding.Detail());
                else t.Append('.');
                timeline = t.ToString();
            }
            return new LastRunRead(run.Mode, plays.Verdict.Trim(), plays.Drills, timeline);
        }
        return null;
    }

    /// <summary>Fills the verdict line once the worker finishes (called from the screen's Tick).</summary>
    void PollLastRead()
    {
        if (lastRead is not { IsCompleted: true } task || !IsInstanceValid(lastReadLine)) return;
        var read = task.IsCompletedSuccessfully ? task.Result : null;
        lastRead = null;
        var line = lastReadLine!;
        if (read == null)
        {
            line.Text = Loc.Spanish ? "Aún no hay ninguna partida grabada que analizar." : "No recorded run to analyse yet.";
            return;
        }
        var sb = new System.Text.StringBuilder();
        sb.Append(Loc.T(CoachData.DrillName(read.Mode))).Append(": ").Append(read.Verdict);
        if (read.Drills.Length > 0)
        {
            string list = string.Join(", ", read.Drills.Select(CoachData.DrillName).Select(Loc.T));
            sb.Append(' ').Append(Loc.Spanish ? $"Para corregirlo: {list}." : $"To fix it: {list}.");
        }
        if (read.Timeline.Length > 0) sb.Append(' ').Append(read.Timeline);
        line.Text = sb.ToString();
        line.AddThemeColorOverride("font_color", new Color(UiTheme.Text, 0.86f));
    }

    void OpenLastPlays(Label status)
    {
        var found = LatestRun();
        if (found is not { } f)
        {
            status.Text = Loc.Spanish ? "Aún no hay ninguna partida grabada." : "No recorded run yet.";
            return;
        }
        PlaysScreen.Open(Main.I, f.Run, f.Plays);
    }

    // ---------------- problems & strengths ----------------

    Control ProblemsPanel(SkillProfile p)
    {
        float k = K;
        bool overall = CoachData.HasOverall(p), any = p.Problems.Count > 0;
        var panel = new VPanel
        {
            Title = any || overall ? "WHAT TO FIX" : "GET RATED",
            Caption = any ? (Loc.Spanish ? $"{p.Problems.Count} encontrados · primero los más importantes · clic para ampliar"
                : $"{p.Problems.Count} found · most important first · click to expand") : null,
            K = k, SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        panel.AddChild(scroll);
        var list = VBox(12 * k);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var pad = Margins(list, 0, 2 * k, 14 * k, 6 * k);
        pad.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(pad);

        int idx = 1;
        foreach (var d in p.Problems) list.AddChild(ProblemCard(d, idx++, p));
        if (!overall)
        {
            if (any) list.AddChild(Spacer(0, 4 * k));
            list.AddChild(GetRated(p, any || p.Skills.Any(CoachRank.Rated)));
        }
        else if (!any)
            list.AddChild(Para("No problems found in your recent runs — solid fundamentals. Keep playing: the coach re-checks after every run.",
                UiTheme.Body, 16, UiTheme.Text));

        if (p.Strengths.Count > 0)
        {
            list.AddChild(Spacer(0, 2 * k));
            list.AddChild(new SectionLabel { Text = "WHAT YOU DO WELL", K = k });
            foreach (var s in p.Strengths) list.AddChild(Bullet(s, UiTheme.Good, check: true));
        }
        return panel;
    }

    Control ProblemCard(Diagnosis d, int index, SkillProfile p)
    {
        float k = K;
        var skill = p.Skills.FirstOrDefault(s => s.Skill == d.Skill);
        var frame = new CardFrame { K = k, Strip = CoachSkills.SeverityColor(d.Severity), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var v = VBox(0);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        frame.AddChild(v);
        bool open = expanded.Contains(d.Id);
        var header = new ProblemHeader
        {
            D = d, Index = index, Possible = d.Severity != Severity.Tip && skill != null && skill.Confidence < 0.5f, Expanded = open, K = k,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        header.CustomMinimumSize = new Vector2(0, header.WantedHeight);
        v.AddChild(header);
        var details = Margins(Details(d), 24 * k, 0, 24 * k, 20 * k);
        details.Visible = open;
        details.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        v.AddChild(details);
        header.Pressed += () =>
        {
            bool now = !details.Visible;
            details.Visible = now;
            header.Expanded = now;
            header.CustomMinimumSize = new Vector2(0, header.WantedHeight);
            header.QueueRedraw();
            if (now) expanded.Add(d.Id); else expanded.Remove(d.Id);
        };
        return frame;
    }

    Control Details(Diagnosis d)
    {
        float k = K;
        var v = VBox(6 * k);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (d.Evidence.Length > 0) v.AddChild(Para(d.Evidence, UiTheme.Body, 16.5f, UiTheme.Text));
        if (d.Why.Length > 0)
        {
            v.AddChild(Spacer(0, 2 * k));
            v.AddChild(new SectionLabel { Text = "WHY IT HAPPENS", K = k });
            v.AddChild(Para(d.Why, UiTheme.Body, 15, new Color(UiTheme.Text, 0.78f)));
        }
        if (d.Fixes is { Length: > 0 })
        {
            v.AddChild(Spacer(0, 2 * k));
            v.AddChild(new SectionLabel { Text = "HOW TO FIX", K = k });
            foreach (var f in d.Fixes) v.AddChild(Bullet(f, UiTheme.Accent));
        }
        var drills = (d.Drills ?? Array.Empty<string>()).Where(CoachData.HasDrill).Distinct().Take(3).ToList();
        v.AddChild(Spacer(0, 6 * k));
        // "Show me": replay a moment from a recent run where this happened (searched on a worker thread)
        var status = Lbl("", UiTheme.Body, 14, UiTheme.Dim);
        status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        status.CustomMinimumSize = new Vector2(60 * k, 0);
        // A flow container, not a plain row: the drill buttons plus SHOW ME are wider than the column at 1024×768, and a
        // row simply overflowed (the last button was clipped at the right edge). This wraps onto a second line instead.
        var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        flow.AddThemeConstantOverride("h_separation", (int)(10 * k));
        flow.AddThemeConstantOverride("v_separation", (int)(8 * k));
        if (drills.Count > 0)
        {
            var cap = Lbl("PRACTISE", UiTheme.HudWide, 13, UiTheme.Dim);
            cap.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            flow.AddChild(cap);
            foreach (var key in drills)
            {
                string name = CoachData.DrillName(key).ToUpperInvariant();
                float w = Gfx.TextW(UiTheme.Display, name, UiTheme.Fs(18, k)) / k + 60;
                var b = Btn(name, VButton.Look.Secondary, () => CoachData.StartDrill(key, CoachData.FocusFrom(d)), Mathf.Max(140, w), 42, 18);
                // Every practice button says how the camera climb should be set: the coach's recoil rule is universal,
                // so the problem card shows it too instead of only the plan.
                var (advice, why) = CoachPlan.RecoilFor(key);
                b.TooltipText = Loc.Spanish
                    ? $"Empezar {CoachData.DrillName(key)} ahora · {CoachPlan.RecoilLabel(advice)}: {why}"
                    : $"Start {CoachData.DrillName(key)} now · {CoachPlan.RecoilLabel(advice)}: {why}";
                flow.AddChild(b);
            }
        }
        VButton? sm = null;
        sm = Btn("SHOW ME", VButton.Look.Ghost, () => ShowMe(d, sm!, status), 130, 42, 17);
        sm.TitleFont = UiTheme.HudWide;
        sm.TooltipText = "Replay a moment from your recent runs where this happened";
        flow.AddChild(sm);
        if (Academy.For(d) is { } lesson)
        {
            // The technique behind the fix: the lesson with its steps, drills and credited videos.
            string label = (Loc.Spanish ? "TÉCNICA: " : "TECHNIQUE: ") + lesson.Name.ToUpperInvariant();
            float lw = Gfx.RawW(UiTheme.HudWide, label, UiTheme.Fs(15, k)) / k + 56;
            var tb = Btn(label, VButton.Look.Ghost, () => Main.I.ShowAcademy(lesson.Id, Main.I.ShowProfile), lw, 42, 15);
            tb.TitleFont = UiTheme.HudWide;
            tb.TooltipText = "« " + lesson.Cue + " »";
            flow.AddChild(tb);
        }
        v.AddChild(flow);
        v.AddChild(status);
        if (Main.I.Dev && !devShowMeDone && Core.CmdLine.After("--showme") == d.Id)
        {
            devShowMeDone = true; // dev screenshots: --screen profile --showme CODE
            Callable.From(() => { if (IsInstanceValid(sm)) ShowMe(d, sm, status); }).CallDeferred();
        }
        return v;
    }

    // ---------------- "Show me" ----------------

    static bool devShowMeDone;
    static bool devPlanDone;
    Task<(RunTelemetry Run, RunInsights Ins)?>? search;
    /// <summary>The found run's 3D replay being read (after <see cref="search"/>; the button says "LOADING RUN…").</summary>
    Task? loading;
    (string Code, string Drill, VButton Btn, Label Status)? searchFor;

    void ShowMe(Diagnosis d, VButton b, Label status)
    {
        if (search != null) return;
        string drill = (d.Drills ?? Array.Empty<string>()).Where(CoachData.HasDrill).Select(CoachData.DrillName).FirstOrDefault() ?? "a few drills";
        b.Disabled = true;
        b.Label = "SEARCHING…";
        b.QueueRedraw();
        status.Text = "";
        searchFor = (d.Id, drill, b, status);
        search = RewindData.FindExample(d.Id);
    }

    void PollShowMe()
    {
        if (search is not { IsCompleted: true } task || searchFor is not { } sf) return;
        var found = task.IsCompletedSuccessfully ? task.Result : null;
        if (found is { } run && loading == null && run.Run.Replay == null && run.Run.SourceFile != null)
        {
            // Found: read its 3D replay (.vtr sidecar) on a worker too, so the Rewind opens ready to build the 3D view.
            loading = Task.Run(() => { try { run.Run.Replay ??= Game.Replay.ReplayFile.For(run.Run); } catch { /* 2D only */ } });
            if (IsInstanceValid(sf.Btn)) { sf.Btn.Label = "LOADING RUN…"; sf.Btn.QueueRedraw(); }
            return;
        }
        if (loading is { IsCompleted: false }) return;
        loading = null;
        search = null;
        searchFor = null;
        if (IsInstanceValid(sf.Btn)) { sf.Btn.Disabled = false; sf.Btn.Label = "SHOW ME"; sf.Btn.QueueRedraw(); }
        if (found is { } f) RewindScreen.Open(Main.I, f.Run, f.Ins, sf.Code); // not a child: a resize rebuilds this screen
        else if (IsInstanceValid(sf.Status)) sf.Status.Text = Loc.Spanish
            ? $"Aún no hay un ejemplo grabado. Juega {sf.Drill}."
            : $"No recorded example yet. Play {sf.Drill}.";
    }

    Control GetRated(SkillProfile p, bool partial)
    {
        float k = K;
        var v = VBox(10 * k);
        int need = CoachData.RunsNeeded();
        if (partial) v.AddChild(new SectionLabel { Text = "UNLOCK YOUR OVERALL RANK", K = k });
        string runs = $"{(need > 0 ? need : 3)}";
        v.AddChild(Para(
            Loc.Spanish
                ? $"Juega Flicks a la cabeza, Seguimiento en strafe, Práctica de asome, Contra-Strafe y Ráfaga Vandal unas cuantas veces: el coach necesita unas {runs} partidas más para estimar tu rango. Cada partida se analiza automáticamente."
                : $"Play Head Flicks, Strafe Tracking, Peek Practice, Counter-Strafe and Vandal Spray a few times — the coach needs about {runs} more run{(need == 1 ? "" : "s")} to estimate your rank. Every run is analysed automatically.",
            UiTheme.Body, 16, UiTheme.Text));
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", (int)(10 * k));
        grid.AddThemeConstantOverride("v_separation", (int)(10 * k));
        foreach (var (key, target, rates) in CoachData.Starter)
            if (CoachData.HasDrill(key))
                grid.AddChild(new DrillTile { Key = key, Target = target, Rates = rates, Runs = CoachData.CoachedRuns(key), K = k });
        v.AddChild(grid);
        if (Main.I.Dev && CoachData.LastError is { } err) v.AddChild(Para($"{(Loc.Spanish ? "Error del coach" : "Coach error")}: {err}", UiTheme.Body, 13, UiTheme.Warn));
        return v;
    }

    // ---------------- sensitivity ----------------

    Control SensPanel(SkillProfile p)
    {
        float k = K;
        var a = p.Sens;
        bool has = a != null && a.RecommendedSens > 0;
        var panel = new VPanel
        {
            Title = "SENSITIVITY",
            Caption = has ? (Loc.Spanish ? $"recomendación · confianza {ConfWord(a!.Confidence).ToLowerInvariant()}"
                : $"recommendation · {CoachRank.Confidence(a!.Confidence).ToLowerInvariant()} confidence") : (Loc.Spanish ? "aún no hay datos" : "not enough data yet"),
            K = k,
        };
        var v = VBox(10 * k);
        panel.AddChild(v);
        v.AddChild(new DrawBox { CustomMinimumSize = new Vector2(0, 112 * k), OnDraw = d => DrawSens(d, p) });
        string reason = has
            ? SensLabText.Recommendation(p)
            : (Loc.Spanish
                ? "Aún no hay suficientes datos de flicks y tracking para recomendar una sensibilidad. Juega Head Flicks y Strafe Tracking varias veces o abre el Sens Finder para una prueba A/B a ciegas."
                : "Not enough flick and tracking data for a recommendation yet. Play Head Flicks and Strafe Tracking a few times — or run the Sens Finder for a direct blind A/B test.");
        if (reason.Length > 0) v.AddChild(Para(reason, UiTheme.Body, 15, new Color(UiTheme.Text, 0.85f)));

        var row = HBox(12 * k);
        v.AddChild(row);
        bool tight = GetViewportRect().Size.X < 1360;
        if (has && a!.Direction != "keep")
        {
            var tb = new VButton { Kind = VButton.Look.Primary, K = k, FontPx = 19, SubPx = 11.5f, CustomMinimumSize = new Vector2(tight ? 0 : 250 * k, 48 * k) };
            tb.Pressed += () => ToggleTry(tb, a);
            ConfigureTry(tb, a);
            if (tight)
            {
                tb.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                v.AddChild(tb);
            }
            else row.AddChild(tb);
        }
        var finder = Btn("RUN SENS FINDER", VButton.Look.Secondary, () => CoachData.StartDrill("sensfinder"), tight ? 0 : 190, 48, 19);
        finder.TooltipText = "Blind A/B test of sensitivities with flicks + tracking (about 15 minutes)";
        finder.Disabled = !CoachData.HasDrill("sensfinder");
        if (tight) finder.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(finder);
        var lab = Btn("SENS LAB", VButton.Look.Secondary, Main.I.ShowSensLab, tight ? 0 : 160, 48, 19);
        lab.TooltipText = "Overshoot, undershoot, tremor and tracking evidence accumulated per sensitivity. CA-VAL-AIM can't change Valorant's settings — set it in Valorant: Settings → General → Mouse → Sensitivity.";
        if (tight) lab.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(lab);
        return panel;
    }

    static float Rounded(float sens) => MathF.Round(sens * 1000f) / 1000f;

    static bool TryActive(SensAdvice a)
    {
        var st = Main.I.Settings;
        return st.UseSensOverride && MathF.Abs(st.SensOverride - Rounded(a.RecommendedSens)) < 0.0006f;
    }

    void ConfigureTry(VButton b, SensAdvice a)
    {
        bool on = TryActive(a);
        b.Kind = on ? VButton.Look.Select : VButton.Look.Primary;
        b.Selected = on;
        b.Label = on ? (Loc.Spanish ? "ACTIVA" : "ACTIVE") : (Loc.Spanish ? "PROBARLA" : "TRY IT");
        b.Sub = on ? (Loc.Spanish ? "clic para volver" : "click to switch back") : (Loc.Spanish ? "en CA-VAL-AIM" : "in CA-VAL-AIM");
        b.TooltipText = on
            ? (Loc.Spanish ? "CA-VAL-AIM usa la sensibilidad recomendada. Haz clic para volver a la anterior."
                : "CA-VAL-AIM is using the recommended sens. Click to go back to your previous sens.")
            : (Loc.Spanish
                ? $"Usa {Rounded(a.RecommendedSens).ToString("0.000", Inv)} en los ejercicios de CA-VAL-AIM (tus ajustes de Valorant no cambian)"
                : $"Use {Rounded(a.RecommendedSens).ToString("0.000", Inv)} in CA-VAL-AIM's drills (your Valorant settings stay unchanged)");
        b.QueueRedraw();
    }

    void ToggleTry(VButton b, SensAdvice a)
    {
        var st = Main.I.Settings;
        if (TryActive(a))
        {
            st.UseSensOverride = prevUse ?? false;
            if (prevUse == true) st.SensOverride = prevSens;
        }
        else
        {
            prevUse = st.UseSensOverride;
            prevSens = st.SensOverride;
            st.UseSensOverride = true;
            st.SensOverride = Rounded(a.RecommendedSens);
        }
        CoachData.SaveSettings();
        ConfigureTry(b, a);
    }

    void DrawSens(DrawBox d, SkillProfile p)
    {
        float k = K;
        var app = Main.I;
        var a = p.Sens;
        bool has = a != null && a.RecommendedSens > 0;
        float cur = has && a!.CurrentSens > 0 ? a.CurrentSens : app.Sens;
        int dpi = has && a!.Dpi > 0 ? a.Dpi : app.Settings.Dpi;
        float curCm = has && a!.CurrentCm360 > 0 ? a.CurrentCm360 : PlayerView.Cm360(cur, dpi);
        int cap = UiTheme.Fs(12, k), big = UiTheme.Fs(58, k), sub = UiTheme.Fs(14, k);

        void Block(float x, string caption, float sens, float cm, Color c)
        {
            Gfx.Text(d, UiTheme.HudWide, caption, x, Gfx.Mid(10 * k, cap), cap, UiTheme.Dim);
            Gfx.Text(d, UiTheme.Display, sens.ToString("0.000", Inv), x - 2 * k, 76 * k, big, c);
            Gfx.Text(d, UiTheme.Body, Loc.Numeric($"{sens * dpi:0} eDPI · {cm:0.0} cm/360"), x, Gfx.Mid(100 * k, sub), sub, UiTheme.Dim);
        }

        Block(0, has ? "CURRENT" : "CURRENT · " + (app.Settings.UseSensOverride ? "CA-VAL-AIM" : "FROM VALORANT"), cur, curCm, UiTheme.Text);
        float colW = 190 * k, ax = colW + 46 * k, rx = colW + 104 * k;
        float? recCm = null;
        if (has)
        {
            float rec = a!.RecommendedSens;
            float rcm = a.RecommendedCm360 > 0 ? a.RecommendedCm360 : PlayerView.Cm360(rec, dpi);
            string dir = a.Direction;
            if (dir != "keep") recCm = rcm;
            var dc = dir == "keep" ? UiTheme.Good : UiTheme.Warn;
            float ay = 44 * k;
            if (dir == "lower")
            {
                Gfx.ChevronDown(d, new Vector2(ax, ay - 8 * k), 34 * k, 16 * k, 6 * k, dc);
                Gfx.ChevronDown(d, new Vector2(ax, ay + 6 * k), 34 * k, 16 * k, 6 * k, new Color(dc, 0.55f));
            }
            else if (dir == "higher")
            {
                Gfx.ChevronDown(d, new Vector2(ax, ay + 6 * k), 34 * k, -16 * k, -6 * k, dc);
                Gfx.ChevronDown(d, new Vector2(ax, ay - 8 * k), 34 * k, -16 * k, -6 * k, new Color(dc, 0.55f));
            }
            else
            {
                d.DrawRect(new Rect2(ax - 14 * k, ay - 8 * k, 28 * k, 5 * k), dc);
                d.DrawRect(new Rect2(ax - 14 * k, ay + 4 * k, 28 * k, 5 * k), dc);
            }
            int ps = UiTheme.Fs(20, k), ls = UiTheme.Fs(11, k);
            string pct = dir == "keep" || cur <= 0 ? "KEEP" : $"{(rec / cur - 1f) * 100f:+0;-0}%";
            Gfx.TextC(d, UiTheme.Display, pct, ax, Gfx.Mid(80 * k, ps), ps, dc);
            string dirLabel = Loc.Spanish
                ? dir switch { "lower" => "BAJAR", "higher" => "SUBIR", _ => "MANTENER" }
                : dir.ToUpperInvariant();
            if (dir != "keep") Gfx.TextC(d, UiTheme.HudWide, dirLabel, ax, Gfx.Mid(100 * k, ls), ls, UiTheme.Faint);
            string capText = dir != "keep" ? "RECOMMENDED" : a.Confidence < 0.5f ? "KEEP FOR NOW" : "KEEP YOUR SENS";
            Block(rx, capText, rec, rcm, dir == "keep" ? UiTheme.Text : UiTheme.Good);
        }
        else
        {
            int ns = UiTheme.Fs(22, k);
            Gfx.Text(d, UiTheme.HudWide, "RECOMMENDED", rx, Gfx.Mid(10 * k, cap), cap, UiTheme.Dim);
            Gfx.Text(d, UiTheme.Display, "NOT YET", rx, Gfx.Mid(50 * k, ns), ns, UiTheme.Faint);
        }

        // The pro-range mini chart is supplementary; at 1024x768 the third column would be clipped, so keep the
        // current/recommended blocks readable and drop the chart instead.
        if (d.Size.X >= 620f)
        {
            float px0 = rx + 232 * k, px1 = d.Size.X - 6 * k;
            if (px1 - px0 >= 150 * k) ProBar(d, new Rect2(px0, 0, px1 - px0, d.Size.Y), k, curCm, recCm);
        }
    }

    /// <summary>cm/360 on a log scale with the pro range (≈33–87 cm, median ≈54 cm) and your current / new sens.</summary>
    static void ProBar(CanvasItem d, Rect2 r, float k, float curCm, float? recCm)
    {
        const float lo = 15f, hi = 100f;
        float X(float cm) => r.Position.X + Mathf.Log(Mathf.Clamp(cm, lo, hi) / lo) / Mathf.Log(hi / lo) * r.Size.X;
        int cap = UiTheme.Fs(12, k), ls = UiTheme.Fs(11, k), ts = UiTheme.Fs(12, k);
        Gfx.TextFit(d, UiTheme.HudWide, Loc.Spanish ? "CM / 360 FRENTE A PROS" : "CM / 360 VS PROS", r.Position.X, Gfx.Mid(10 * k, cap), cap, UiTheme.Dim, r.Size.X);
        float by = r.Position.Y + 50 * k, bh = 6 * k;
        d.DrawRect(new Rect2(r.Position.X, by, r.Size.X, bh), new Color(UiTheme.Text, 0.08f));
        d.DrawRect(new Rect2(X(33), by, X(87) - X(33), bh), new Color(UiTheme.Teal, 0.45f));
        d.DrawRect(new Rect2(X(54) - 1 * k, by - 4 * k, 2 * k, bh + 8 * k), UiTheme.Teal);
        foreach (int cm in new[] { 20, 30, 50, 80 })
            Gfx.TextC(d, UiTheme.Hud, cm.ToString(Inv), X(cm), Gfx.Mid(by + 20 * k, ts), ts, UiTheme.Faint);
        Gfx.TextFit(d, UiTheme.Body, Loc.Spanish ? "pros 33–87 cm · mediana 54" : "pros 33–87 cm · median 54", r.Position.X, Gfx.Mid(by + 46 * k, ts), ts, UiTheme.Teal, r.Size.X);

        void Marker(float cm, string label, Color c)
        {
            float x = X(cm);
            d.DrawRect(new Rect2(x - 1 * k, by - 11 * k, Mathf.Max(1.5f, 2 * k), 20 * k), c);
            float lw = Gfx.TextW(UiTheme.HudWide, label, ls);
            float lx = Mathf.Clamp(x - 3 * k, r.Position.X, Mathf.Max(r.Position.X, r.End.X - lw));
            Gfx.Text(d, UiTheme.HudWide, label, lx, Gfx.Mid(by - 24 * k, ls), ls, c);
        }
        Marker(curCm, Loc.Spanish ? "AHORA" : "NOW", UiTheme.Text);
        if (recCm is { } rc) Marker(rc, Loc.Spanish ? "NUEVA" : "NEW", UiTheme.Good);
    }

    // ---------------- small builders ----------------

    Label Para(string text, Font f, float px, Color c)
    {
        var l = Lbl(text, f, px, c);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        l.CustomMinimumSize = new Vector2(60 * K, 0);
        return l;
    }

    Control Bullet(string text, Color c, bool check = false)
    {
        float k = K;
        var h = HBox(10 * k);
        var dot = new DrawBox
        {
            CustomMinimumSize = new Vector2(16 * k, 22 * k),
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            OnDraw = d =>
            {
                if (!check) { d.DrawRect(new Rect2(3 * k, 11 * k, 7 * k, 2 * k), c); return; }
                Span<Vector2> pts = stackalloc Vector2[3];
                pts[0] = new(2 * k, 12 * k); pts[1] = new(6.5f * k, 16.5f * k); pts[2] = new(14 * k, 6 * k);
                d.Polyline(pts, c, Mathf.Max(1.5f, 2.4f * k), true);
            },
        };
        h.AddChild(dot);
        h.AddChild(Para(text, UiTheme.Body, 15.5f, UiTheme.Text));
        return h;
    }
}
