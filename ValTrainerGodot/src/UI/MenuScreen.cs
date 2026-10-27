using System.Globalization;
using Godot;
using ValTrainer.Core;
using ValTrainer.Game;
using ValTrainer.Maps;
using ValTrainer.Modes;

namespace ValTrainer.UI;

/// <summary>
/// Main menu (minimalist since 1.18): a flat canvas with generous margins, light display type, hairlines instead of
/// panels and one accent for the primary action and the current selection (the widgets in MinimalKit.cs).
/// Left: the wordmark, the greeting, START LOCK-IN and the drill library (category tabs over a list of drills with
/// the personal best at the current tier). Right: the difficulty tier, the map, the Deathmatch lobby (bot difficulty,
/// lobby size, length) and the imported setup. Every element arrives with a short staggered rise; hover and focus
/// ease in and out.
/// </summary>
public partial class MenuScreen : ScreenBase
{
    /// <summary>Category of the per-agent drills: listed on the Agents screen, not in the menu library.</summary>
    public const string AgentsCategory = "Agents";

    readonly List<(Func<TrainingMode> Make, TrainingMode Info)> modes =
        ModeRegistry.All.Select(f => (Make: f, Info: f())).Where(m => m.Info.Category != AgentsCategory).ToList();
    readonly List<(MDrillRow Row, TrainingMode Info)> rows = new();
    MMapSelector? mapSel;
    MWeaponSelector? weaponSel;
    /// <summary>"ALL" or one of the drill categories: only that category's drills are listed. Static, so returning from a
    /// drill (the menu screen is rebuilt) lands on the same library tab.</summary>
    static string catFilter = "ALL";
    SessionSummary summary;
    GridContainer? grid;
    MText? tierNote, dmNote, subLine;
    int gridColumns = 3;
    float libraryWidth;

    protected override bool AnimatedBackdrop => false;

    /// <summary>Esc closes the map pop-up; otherwise it does nothing on the menu (Quit is a link).</summary>
    protected override void Back()
    {
        if (weaponSel?.IsOpen == true) { weaponSel.Close(); return; }
        if (mapSel?.IsOpen == true) mapSel.Close();
    }

    VBoxContainer? bannerSlot;
    int bannerRev = -1;

    protected override void Build()
    {
        rows.Clear();
        mapSel = null;
        weaponSel = null;
        coachHint = null; coachHintDone = false;
        summary = SessionSummary.From(Main.I.Stats.Runs);
        // Dev-only: "--menu-filter aim|valorant|..." opens the menu on that category (layout screenshots).
        if (Main.I.Dev && CmdLine.DevAfter("--menu-filter") is { } mf)
            catFilter = Categories().FirstOrDefault(c => c.Equals(mf, StringComparison.OrdinalIgnoreCase)) ?? "ALL";
        float k = K;
        var vp = GetViewportRect().Size;
        float side = Mathf.Clamp(vp.X * 0.05f, 28 * k, 112 * k);

        AddChild(new MinimalBackdrop());

        var page = Margins(VBox(0), side, 44 * k, side, 26 * k);
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(page);
        var col = (VBoxContainer)page.GetChild(0);

        col.AddChild(Header());

        // "CA-VAL-AIM X is available / downloading / ready" strip (filled when the background update check answers)
        bannerSlot = VBox(0);
        col.AddChild(bannerSlot);
        bannerRev = -1;
        banner = null;
        RefreshBanner();

        // first-launch panels, one at a time: "What's new" first, then the updates opt-in
        if (WhatsNew.Pending is { } notes)
            AddChild(new WhatsNewPanel(notes, k, CloseWhatsNew));
        else if (UpdatesPrompt.Pending)
            AddChild(new UpdatesPromptPanel(k, AnswerUpdatesPrompt));

        col.AddChild(Spacer(0, 40 * k));

        var body = HBox(Mathf.Clamp(vp.X * 0.055f, 40 * k, 120 * k));
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        col.AddChild(body);

        var left = VBox(0);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.AddChild(left);
        float rightW = Mathf.Clamp(vp.X * 0.25f, 360 * k, 460 * k);
        var right = VBox(0);
        right.CustomMinimumSize = new Vector2(rightW, 0);
        body.AddChild(right);

        float leftW = vp.X - side * 2 - rightW - body.GetThemeConstant("separation");
        gridColumns = leftW >= 1050 * k ? 3 : leftW >= 640 * k ? 2 : 1;
        libraryWidth = leftW - 110 * k;
        BuildLeft(left);
        BuildRight(right);

        col.AddChild(Spacer(0, 18 * k));
        col.AddChild(new MRule { Delay = 0.5f });
        col.AddChild(Spacer(0, 12 * k));
        col.AddChild(Footer());
    }

    static string L(string en, string es) => Loc.Spanish ? es : en;

    IEnumerable<string> Categories() => new[] { "ALL" }.Concat(modes.Select(m => m.Info.Category).Distinct());

    // ---------------- header ----------------

    Control Header()
    {
        float k = K;
        var h = HBox(0);
        h.CustomMinimumSize = new Vector2(0, 34 * k);
        h.AddChild(new Wordmark { K = k, CustomMinimumSize = new Vector2(220 * k, 34 * k) });
        h.AddChild(Expander());
        var nav = HBox(34 * k);
        nav.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        h.AddChild(nav);
        float d = 0.06f;
        void Link(string label, Action go, string tip)
        {
            var l = new MLink { K = k, Label = label, Px = 12.5f, Delay = d, TooltipText = tip };
            l.Pressed += go;
            nav.AddChild(l);
            d += Mn.Stagger;
        }
        Link("AGENTS", Main.I.ShowAgents, "All 29 agents: signature drills, role drills and an agent Lock-In");
        Link("COACH", Main.I.ShowProfile, "Your estimated rank, the problems holding you back and how to fix them");
        Link("PLAYLISTS", Main.I.ShowPlaylists, "Your own sets of drills, played back to back");
        Link("STATS", Main.I.ShowStats, "Personal bests and history");
        Link("SETTINGS", Main.I.ShowSettings, "Sensitivity, crosshair, graphics, audio and updates");
        nav.AddChild(Spacer(10 * k, 0));
        Link("QUIT", Main.I.Quit, "Close CA-VAL-AIM");
        return h;
    }

    /// <summary>The wordmark: CA · VAL · AIM in tracked light caps, the middle word in the accent, a short rule under it
    /// that draws in once.</summary>
    public partial class Wordmark : Control
    {
        public float K = 1f;
        float t;

        public Wordmark() => MouseFilter = MouseFilterEnum.Ignore;

        public override void _Process(double delta)
        {
            if (t > 1.4f) return;
            t += (float)delta;
            QueueRedraw();
        }

        public override void _Draw()
        {
            float k = K, r = Mn.Reveal(t, 0f);
            int fs = UiTheme.Fs(17, k);
            float y = Gfx.Mid(Size.Y / 2 - 3 * k, fs) + (1 - r) * 6 * k;
            float x = 0;
            var f = UiTheme.LightWide;
            void Part(string s, Color c)
            {
                DrawString(f, new Vector2(x, y), s, HorizontalAlignment.Left, -1, fs, Mn.A(c, r));
                x += Gfx.RawW(f, s, fs);
            }
            Part("CA", UiTheme.Text);
            Part("-", UiTheme.Faint);
            Part("VAL", UiTheme.Accent);
            Part("-", UiTheme.Faint);
            Part("AIM", UiTheme.Text);
            float line = Gfx.EaseInOut(Mathf.Clamp((t - 0.25f) / 0.8f, 0, 1));
            DrawRect(new Rect2(0, Size.Y - 3 * k, 22 * k * line, Mathf.Max(1, 1.5f * k)), Mn.A(UiTheme.Accent, line));
        }
    }

    // ---------------- left: greeting, Lock-In, library ----------------

    void BuildLeft(VBoxContainer left)
    {
        float k = K;
        string kicker = summary.Streak > 1
            ? (Loc.Spanish ? $"RACHA DE {summary.Streak} DÍAS" : $"{summary.Streak}-DAY STREAK")
            : L("READY WHEN YOU ARE", "LISTO CUANDO QUIERAS");
        left.AddChild(new MText { K = k, Text = kicker, Font = UiTheme.HudWide, Px = 11.5f, Color = UiTheme.Faint, Delay = 0.08f });
        left.AddChild(Spacer(0, 6 * k));
        left.AddChild(new MText { K = k, Text = L("Train with intent.", "Entrena con intención."), Font = UiTheme.Light, Px = 62, Color = UiTheme.Text, Delay = 0.12f });
        left.AddChild(Spacer(0, 6 * k));
        subLine = new MText { K = k, Text = SubText(), Font = UiTheme.Body, Px = 15.5f, Color = UiTheme.Dim, Delay = 0.18f };
        left.AddChild(subLine);
        left.AddChild(Spacer(0, 4 * k));

        coachHint = new MLink { K = k, Font = UiTheme.Body, Px = 14, Arrow = true, Delay = 0.22f, Visible = false, TooltipText = "Open the aim coach", Label = " " };
        coachHint.Pressed += Main.I.ShowProfile;
        left.AddChild(coachHint);
        UpdateCoachHint();

        left.AddChild(Spacer(0, 30 * k));
        var actions = HBox(22 * k);
        var start = new MAction
        {
            K = k, Primary = true, Label = "START LOCK-IN", Px = 14.5f, Delay = 0.26f,
            CustomMinimumSize = new Vector2(270 * k, 56 * k),
            TooltipText = "Lock-In: about 12 minutes before ranked. A check-in, aim drills that adapt to you, a deathmatch, then your goal and cue",
        };
        start.Pressed += Main.I.ShowLockIn;
        actions.AddChild(start);
        var lockNote = new MText
        {
            K = k, Font = UiTheme.Body, Px = 13.5f, Color = UiTheme.Faint, Delay = 0.32f,
            Text = L("12 min · check-in, adaptive aim, a deathmatch, your goal", "12 min · chequeo, puntería adaptativa, un combate a muerte, tu objetivo"),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        actions.AddChild(lockNote);
        left.AddChild(actions);

        // One click back into the last drill played (same tier, map and settings), without the preview.
        if (!string.IsNullOrEmpty(summary.LastMode) && ModeRegistry.Find(summary.LastMode) is { } again)
        {
            left.AddChild(Spacer(0, 10 * k));
            var link = new MLink
            {
                K = k, Font = UiTheme.Body, Px = 14, Arrow = true, Delay = 0.3f, SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
                Label = (Loc.Spanish ? "Otra vez: " : "Again: ") + LastModeLabel(),
                TooltipText = L("Play your last drill again straight away", "Vuelve a jugar tu último ejercicio directamente"),
            };
            link.Pressed += () => Main.I.StartMode(again);
            left.AddChild(link);
        }

        left.AddChild(Spacer(0, 46 * k));
        var libHead = HBox(0);
        libHead.AddChild(new MText { K = k, Text = L("LIBRARY", "BIBLIOTECA"), Font = UiTheme.HudWide, Px = 11.5f, Color = UiTheme.Faint, Delay = 0.32f, CustomMinimumSize = new Vector2(110 * k, 34 * k) });
        var cats = Categories().ToList();
        if (!cats.Contains(catFilter)) catFilter = "ALL";
        var tabs = new MSegment
        {
            K = k, Delay = 0.34f, Px = 12f, Gap = 22, MaxWidth = libraryWidth,
            Options = cats.Select(c => c.ToUpperInvariant()).ToArray(),
            Counts = cats.Select(c => c == "ALL" ? modes.Count : modes.Count(m => m.Info.Category == c)).ToArray(),
            Selected = cats.IndexOf(catFilter),
        };
        tabs.Changed = i => { catFilter = cats[i]; FillGrid(); };
        libHead.AddChild(tabs);
        left.AddChild(libHead);
        left.AddChild(Spacer(0, 8 * k));
        left.AddChild(new MRule { Delay = 0.36f });

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        left.AddChild(scroll);
        grid = new GridContainer { Columns = gridColumns, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", (int)(26 * k));
        grid.AddThemeConstantOverride("v_separation", 0);
        scroll.AddChild(grid);
        FillGrid(0.38f);
    }

    string SubText()
    {
        string map = MapSelector.NameOf(Main.I.MapKey);
        string map2 = map.Length > 1 ? map[0] + map[1..].ToLowerInvariant() : map;
        string tier = Loc.T(Difficulty.Get(Main.I.Tier).Name);
        string s = $"{map2}  ·  {tier}  ·  {modes.Count} {L("drills", "ejercicios")}";
        if (summary.Last != null && !string.IsNullOrEmpty(summary.LastMode))
            s += Loc.Spanish ? $"  ·  último: {LastModeLabel()}" : $"  ·  last: {LastModeLabel()}";
        return s;
    }

    /// <summary>The last played drill's display name (falls back to the raw mode key).</summary>
    string LastModeLabel()
    {
        if (string.IsNullOrEmpty(summary.LastMode)) return "";
        foreach (var (_, info) in modes)
            if (info.Key == summary.LastMode) return Loc.T(info.Name);
        return summary.LastMode;
    }

    /// <summary>(Re)fills the library with the current category; rows re-enter with a short stagger.</summary>
    void FillGrid(float delay = 0f)
    {
        if (grid == null) return;
        float k = K;
        foreach (var c in grid.GetChildren()) { grid.RemoveChild(c); c.QueueFree(); }
        rows.Clear();
        int i = 0;
        foreach (var (make, info) in modes.Where(m => catFilter == "ALL" || m.Info.Category == catFilter))
        {
            var row = new MDrillRow
            {
                K = k, Title = info.Name, Meta = catFilter == "ALL" ? $"{Loc.T(info.Category)}  ·  {Loc.T(info.Description)}" : Loc.T(info.Description),
                IsNew = IsNew(info.Key), Delay = delay + Math.Min(0.42f, i++ * 0.018f),
                CustomMinimumSize = new Vector2(0, 62 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = $"{Loc.T(info.Name)} — {Loc.T(info.Description)}" + (info.MadeBy is { } by ? $"\nMade by the community: @{by}" : ""),
            };
            var m = make;
            row.Pressed += () => DrillPreview.Open(this, K, m, () => Main.I.StartMode(m), freePlay: () => Main.I.StartMode(m, true)); // preview first, START plays
            rows.Add((row, info));
            grid.AddChild(row);
        }
        RefreshBests();
    }

    void RefreshBests()
    {
        foreach (var (row, info) in rows)
        {
            int pb = Main.I.Stats.Best(info.Key, Main.I.Tier);
            row.Value = pb >= 0 ? pb.ToString("#,0", CultureInfo.InvariantCulture) : "";
            row.ValueCaption = pb >= 0 ? L("BEST", "RÉCORD") : "";
            row.QueueRedraw();
        }
    }

    /// <summary>Drills added in a version show a NEW mark until played once, through that version's minor releases.</summary>
    static readonly Dictionary<string, string> AddedIn = new()
    {
        ["microshot"] = "1.4", ["switch"] = "1.4", ["popup"] = "1.4", ["longtaps"] = "1.4", ["jumppeek"] = "1.4",
        ["jigglepeek"] = "1.4", ["postplant"] = "1.4", ["retake"] = "1.4", ["anchor"] = "1.4", ["sound"] = "1.4", ["peek_mix"] = "1.10",
        ["arsenal"] = "1.18", ["airtrack"] = "1.18", ["reacttrack"] = "1.18",
        ["reactlab:visual"] = "1.18", ["reactlab:audio"] = "1.18", ["reactlab:peripheral"] = "1.18",
        ["reactlab:choice"] = "1.18", ["reactlab:flick"] = "1.18",
    };

    static bool IsNew(string key)
    {
        if (!AddedIn.TryGetValue(key, out var v) || !SemVer.TryParse(v, out var added) || !SemVer.TryParse(AppInfo.Version, out var app)) return false;
        bool current = added.Major > app.Major || (added.Major == app.Major && added.Minor >= app.Minor);
        return current && !Main.I.Stats.For(key).Any();
    }

    // ---------------- right: difficulty, map, deathmatch, setup ----------------

    MText Caption(string text, float delay) =>
        new() { K = K, Text = text, Font = UiTheme.HudWide, Px = 11f, Color = UiTheme.Faint, Delay = delay, CustomMinimumSize = new Vector2(0, 20 * K) };

    void BuildRight(VBoxContainer right)
    {
        float k = K;
        float d = 0.2f;

        // difficulty tier (drills and bots)
        right.AddChild(Caption("DIFFICULTY", d));
        var tiers = new MSegment
        {
            K = k, Delay = d + 0.03f, Px = 12f, Gap = 18,
            Options = Difficulty.Tiers.Select(t => t.Name.ToUpperInvariant()).ToArray(),
            Tips = Difficulty.Tiers.Select(TierLine).ToArray(),
            Selected = Main.I.Tier,
        };
        tiers.Changed = SelectTier;
        right.AddChild(tiers);
        tierNote = new MText { K = k, Font = UiTheme.Body, Px = 13, Color = UiTheme.Dim, Delay = d + 0.06f, Text = TierNote() };
        right.AddChild(tierNote);

        right.AddChild(Spacer(0, 26 * k));
        d += 0.06f;
        right.AddChild(Caption("MAP", d));
        right.AddChild(Spacer(0, 4 * k));
        mapSel = new MMapSelector(this, k) { SizeFlagsHorizontal = SizeFlags.ShrinkBegin, Delay = d + 0.03f };
        mapSel.Changed = () => { if (subLine != null) { subLine.Text = SubText(); subLine.QueueRedraw(); } };
        right.AddChild(mapSel);

        right.AddChild(Spacer(0, 20 * k));
        d += 0.05f;
        var weaponHead = HBox(0);
        var weaponCap = Caption(L("WEAPON", "ARMA"), d);
        weaponCap.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        weaponHead.AddChild(weaponCap);
        weaponHead.AddChild(new MText
        {
            K = k, Text = L("every drill", "cada ejercicio"), Font = UiTheme.Body, Px = 12, Color = UiTheme.Faint,
            Delay = d, Align = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(160 * k, 20 * k),
        });
        right.AddChild(weaponHead);
        right.AddChild(Spacer(0, 2 * k));
        weaponSel = new MWeaponSelector(this, k) { SizeFlagsHorizontal = SizeFlags.ShrinkBegin, Delay = d + 0.02f };
        right.AddChild(weaponSel);

        right.AddChild(Spacer(0, 22 * k));
        d += 0.06f;
        BuildDeathmatch(right, d);

        // the Lock-In match-log card ("How did your first match go?") takes the setup's place when it is due
        matchSlot = VBox(0);
        right.AddChild(Spacer(0, 26 * k));
        right.AddChild(matchSlot);
        FillMatchSlot();
        if (!matchSlot.Visible) BuildSetup(right, d + 0.12f);
    }

    void BuildDeathmatch(VBoxContainer right, float d)
    {
        float k = K;
        var st = Main.I.Settings;
        right.AddChild(Caption(L("DEATHMATCH", "COMBATE A MUERTE"), d));
        var diff = new MSegment
        {
            K = k, Delay = d + 0.03f, Px = 12f, Gap = 18,
            Options = DeathmatchRules.All.Select(x => DmName(x).ToUpperInvariant()).ToArray(),
            Tips = DeathmatchRules.All.Select(DmBlurb).ToArray(),
            Selected = (int)DeathmatchRules.FromSetting(st.DmDifficulty),
        };
        diff.Changed = i =>
        {
            st.DmDifficulty = i;
            st.Save();
            if (dmNote != null) { dmNote.Text = DmBlurb(DeathmatchRules.FromSetting(i)); dmNote.QueueRedraw(); }
        };
        right.AddChild(diff);
        dmNote = new MText { K = k, Font = UiTheme.Body, Px = 13, Color = UiTheme.Dim, Delay = d + 0.06f, Text = DmBlurb(DeathmatchRules.FromSetting(st.DmDifficulty)) };
        right.AddChild(dmNote);
        right.AddChild(Spacer(0, 8 * k));

        int[] lobby = { 4, 6, 8, 10, 12 };
        int[] minutes = { 3, 5, 9, 15 };
        int Nearest(int[] opts, float v) => Array.IndexOf(opts, opts.OrderBy(o => MathF.Abs(o - v)).First());
        var row = HBox(36 * k);
        var lv = VBox(0);
        lv.AddChild(Caption(L("PLAYERS", "JUGADORES"), d + 0.08f));
        var players = new MSegment
        {
            K = k, Delay = d + 0.1f, Px = 12f, Gap = 14, Options = lobby.Select(x => x.ToString()).ToArray(),
            Tips = lobby.Select(x => Loc.Spanish ? $"Tú y {x - 1} bots" : $"You and {x - 1} bots").ToArray(),
            Selected = Nearest(lobby, Math.Clamp(st.DmPlayers, DeathmatchRules.MinPlayers, DeathmatchRules.LobbyPlayers)),
        };
        players.Changed = i => { st.DmPlayers = lobby[i]; st.Save(); };
        lv.AddChild(players);
        row.AddChild(lv);
        var mv = VBox(0);
        mv.AddChild(Caption("MINUTES", d + 0.1f));
        var mins = new MSegment
        {
            K = k, Delay = d + 0.12f, Px = 12f, Gap = 14, Options = minutes.Select(x => x.ToString()).ToArray(),
            Tips = minutes.Select(x => x == 9 ? L("VALORANT's length: 9 minutes or the kill target", "La duración de VALORANT: 9 minutos o el objetivo de bajas") : (Loc.Spanish ? $"{x} minutos" : $"{x} minutes")).ToArray(),
            Selected = Nearest(minutes, st.DmMinutes),
        };
        mins.Changed = i => { st.DmMinutes = minutes[i]; st.Save(); };
        mv.AddChild(mins);
        row.AddChild(mv);
        right.AddChild(row);
        right.AddChild(Spacer(0, 14 * k));

        var play = new MAction
        {
            K = k, Label = L("PLAY DEATHMATCH", "JUGAR COMBATE A MUERTE"), Px = 13f, Delay = d + 0.15f,
            CustomMinimumSize = new Vector2(0, 48 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Free-for-all against bots on the selected map, with the difficulty, lobby and length above",
        };
        play.Pressed += () =>
        {
            if (ModeRegistry.Find("deathmatch") is { } dm) Main.I.StartMode(dm);
        };
        right.AddChild(play);
    }

    static string DmName(DmDifficulty x) => !Loc.Spanish ? DeathmatchRules.Name(x) : x switch
    {
        DmDifficulty.Easy => "Fácil", DmDifficulty.Medium => "Medio", DmDifficulty.Hard => "Difícil", _ => "Pesadilla",
    };

    static string DmBlurb(DmDifficulty x) => !Loc.Spanish ? DeathmatchRules.Blurb(x) : x switch
    {
        DmDifficulty.Easy => "Bots Hierro–Bronce: reaccionan lento y disparan corriendo",
        DmDifficulty.Medium => "Bots Platino–Diamante: se paran para disparar y apuntan donde estabas",
        DmDifficulty.Hard => "Bots Ascendente–Inmortal: jiggle peeks, ángulos aguantados, ráfagas agachados",
        _ => "Más allá de Radiante: 150 ms de reacción y oyen cada paso que das corriendo",
    };

    void BuildSetup(VBoxContainer right, float d)
    {
        float k = K;
        var app = Main.I;
        var v = app.Valorant;
        var head = HBox(0);
        var cap = Caption("YOUR SETUP", d);
        cap.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(cap);
        head.AddChild(new MText { K = k, Text = v.Found ? L("imported from VALORANT", "importado de VALORANT") : L("VALORANT defaults", "valores de VALORANT"), Font = UiTheme.Body, Px = 12, Color = UiTheme.Faint, Delay = d, Align = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(220 * k, 20 * k) });
        right.AddChild(head);
        right.AddChild(Spacer(0, 4 * k));
        var inv = CultureInfo.InvariantCulture;
        int i = 0;
        void Row(string label, string value)
        {
            var r = new SetupRow { K = k, Label = label, Value = value, Delay = d + 0.04f + i++ * Mn.Stagger, CustomMinimumSize = new Vector2(0, 32 * k) };
            right.AddChild(r);
        }
        if (!v.Found) Row("VALORANT data", L("Not found — check Settings", "No encontrado — revisa Ajustes"));
        Row("Sensitivity", app.Sens.ToString("0.###", inv) + (app.Settings.UseSensOverride ? L("  (custom)", "  (personalizada)") : ""));
        Row("eDPI · cm/360", Loc.Numeric($"{app.Sens * app.Settings.Dpi:0} · {PlayerView.Cm360(app.Sens, app.Settings.Dpi):0.0} cm @ {app.Settings.Dpi} DPI"));
        Row("Crosshair", Valorant.CrosshairCode.Effective.Name);
        Row("Enemy highlight", Loc.T(UiTheme.EnemyColorNames[Math.Clamp(app.EnemyColorIndex, 0, 3)]));
        right.AddChild(Spacer(0, 6 * k));
        MLink? copy = null;
        copy = new MLink
        {
            K = k, Label = "COPY CROSSHAIR CODE", Px = 11.5f, Delay = d + 0.04f + i * Mn.Stagger,
            TooltipText = "The VALORANT code of this crosshair: paste it in VALORANT → Settings → Crosshair → Import Profile Code",
        };
        copy.Pressed += () =>
        {
            DisplayServer.ClipboardSet(Valorant.CrosshairCode.EffectiveCode);
            copy!.Label = L("COPIED ✓", "COPIADO ✓");
            copy.QueueRedraw();
            GetTree().CreateTimer(2.0).Timeout += () => { if (IsInstanceValid(copy)) { copy.Label = "COPY CROSSHAIR CODE"; copy.QueueRedraw(); } };
        };
        right.AddChild(copy);
    }

    /// <summary>"Label ........ value" over a hairline, fading in with the column.</summary>
    public partial class SetupRow : Control
    {
        public float K = 1f, Delay;
        public string Label = "", Value = "";
        float t;

        public SetupRow() => MouseFilter = MouseFilterEnum.Ignore;

        public override void _Process(double delta)
        {
            if (t > Delay + Mn.RevealTime) return;
            t += (float)delta;
            QueueRedraw();
        }

        public override void _Draw()
        {
            float k = K, r = Mn.Reveal(t, Delay);
            if (r <= 0) return;
            int fs = UiTheme.Fs(13.5f, k);
            float cy = Size.Y / 2 + (1 - r) * 6 * k;
            float lw = Gfx.TextW(UiTheme.Body, Label, fs);
            Gfx.Text(this, UiTheme.Body, Label, 0, Gfx.Mid(cy, fs), fs, Mn.A(UiTheme.Faint, r));
            Gfx.TextFit(this, UiTheme.BodySemi, Value, lw + 16 * k, Gfx.Mid(cy, fs), fs, Mn.A(UiTheme.TextSoft, r), Size.X - lw - 16 * k, HorizontalAlignment.Right);
            DrawRect(new Rect2(0, Size.Y - 1, Size.X * r, 1), UiTheme.Line);
        }
    }

    static string TierLine(Tier t)
    {
        var (fmin, fmax) = Difficulty.FlickDistance(t.Id);
        return $"Bots react in ~{t.Bot.ReactMs:0} ms · Gridshot targets {Difficulty.GridshotRadius(t.Id) * 200:0} cm wide · "
             + $"head flicks at {fmin:0}–{fmax:0} m · {(t.Bot.RunAndGun ? "bots run-and-gun" : "bots stop before shooting")}";
    }

    string TierNote()
    {
        var t = Difficulty.Get(Main.I.Tier);
        return Loc.Spanish ? $"{RanksEs(t.Ranks)} · los bots reaccionan en ~{t.Bot.ReactMs:0} ms" : $"{t.Ranks} · bots react in ~{t.Bot.ReactMs:0} ms";
    }

    /// <summary>Spanish rank names as the Spanish VALORANT client writes them.</summary>
    static string RanksEs(string ranks) => ranks.Replace("Iron", "Hierro").Replace("Bronze", "Bronce").Replace("Silver", "Plata")
        .Replace("Gold", "Oro").Replace("Platinum", "Platino").Replace("Diamond", "Diamante").Replace("Ascendant", "Ascendente")
        .Replace("Immortal", "Inmortal").Replace("Radiant", "Radiante");

    void SelectTier(int id)
    {
        Main.I.Settings.Tier = id;
        Main.I.Settings.Save();
        RefreshBests();
        if (tierNote != null) { tierNote.Text = TierNote(); tierNote.QueueRedraw(); }
        if (subLine != null) { subLine.Text = SubText(); subLine.QueueRedraw(); }
    }

    // ---------------- footer ----------------

    Control Footer()
    {
        float k = K;
        var f = HBox(28 * k);
        f.CustomMinimumSize = new Vector2(0, 30 * k);
        var fan = new MText
        {
            K = k, Font = UiTheme.Body, Px = 12, Color = UiTheme.Faint, Delay = 0.5f,
            Text = "Fan-made trainer · not endorsed by or affiliated with Riot Games",
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        f.AddChild(fan);
        var st = Main.I.Settings;
        void Quick(string label, string[] options, int selected, Action<int> set, string tip)
        {
            var g = HBox(10 * k);
            g.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            g.AddChild(new MText { K = k, Text = label, Font = UiTheme.HudWide, Px = 10.5f, Color = UiTheme.Faint, Delay = 0.52f, CustomMinimumSize = new Vector2(Gfx.TextW(UiTheme.HudWide, label, UiTheme.Fs(10.5f, k)) + 2, 30 * k), TooltipText = tip, MouseFilter = MouseFilterEnum.Pass });
            g.AddChild(new MSegment { K = k, Delay = 0.54f, Px = 10.5f, Gap = 12, Options = options, Selected = selected, Changed = set, CustomMinimumSize = new Vector2(0, 30 * k) });
            f.AddChild(g);
        }
        Quick("RIFLE", new[] { "VANDAL", "PHANTOM" }, st.PrimaryRifle == 1 ? 1 : 0,
            i => { st.PrimaryRifle = i; st.Save(); }, "Rifle for drills set to WEAPON · DRILL DEFAULT");
        Quick("AMMO", new[] { "DEFAULT", "REAL", "INFINITE" }, st.AmmoMode switch { 0 => 1, 1 => 2, _ => 0 },
            i => { st.AmmoMode = i switch { 0 => -1, 1 => 0, _ => 1 }; st.Save(); }, "Default: each drill's own; Real: magazines and reloads; Infinite: never reload");
        Quick("LANGUAGE", new[] { "EN", "ES" }, Loc.Spanish ? 1 : 0, i => Main.I.SetLanguage(i == 1 ? "es" : "en"), "Interface language");
        f.AddChild(new MUpdateLink { K = k, Px = 10.5f, Delay = 0.56f, CustomMinimumSize = new Vector2(190 * k, 30 * k), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        if (AppInfo.HasRepo)
        {
            var gh = new MLink { K = k, Label = "GITHUB", Px = 11, Delay = 0.58f, TooltipText = "Source code, releases and issue tracker", SizeFlagsVertical = SizeFlags.ShrinkCenter };
            gh.Pressed += () => AppInfo.OpenGitHub(AppInfo.RepoUrl);
            f.AddChild(gh);
        }
        f.AddChild(new MText
        {
            K = k, Font = UiTheme.Hud, Px = 11.5f, Color = UiTheme.Faint, Delay = 0.6f, Align = HorizontalAlignment.Right,
            Text = "v" + AppInfo.Version + (Main.I.Dev && !CmdLine.Showcase ? " · DEV" : ""),
            CustomMinimumSize = new Vector2(90 * k, 30 * k), SizeFlagsVertical = SizeFlags.ShrinkCenter,
        });
        return f;
    }

    // ---------------- first-launch panels, bug report hooks ----------------

    void CloseWhatsNew()
    {
        WhatsNew.Pending = null;
        foreach (var c in GetChildren()) if (c is WhatsNewPanel p) p.QueueFree();
        UiTheme.ClickSound();
        if (UpdatesPrompt.Pending && !GetChildren().Any(c => c is UpdatesPromptPanel))
            AddChild(new UpdatesPromptPanel(K, AnswerUpdatesPrompt));
    }

    void AnswerUpdatesPrompt(bool yes)
    {
        foreach (var c in GetChildren()) if (c is UpdatesPromptPanel p) p.QueueFree();
        UiTheme.ClickSound();
        UpdatesPrompt.Answer(yes);
    }

    /// <summary>Dev hook (--screen bugreport): the Report a bug panel.</summary>
    void ShowBugReport()
    {
        if (GetChildren().Any(c => c is BugReportPanel)) return;
        AddChild(new BugReportPanel(K, "menu", () => { }));
    }

    /// <summary>Dev hook (--screen suggest): the same form for a feature idea.</summary>
    void ShowSuggest()
    {
        if (GetChildren().Any(c => c is BugReportPanel)) return;
        AddChild(new BugReportPanel(K, "menu", () => { }, idea: true));
    }

    // ---------------- update banner ----------------

    MUpdateStrip? banner;

    /// <summary>The strip under the header (minimal): "CA-VAL-AIM X is available · you have Y" with DOWNLOAD / SKIP / HIDE
    /// links → "Downloading … 42%" with a thin progress line → "X is ready" with RESTART TO UPDATE (the accent) / WHAT'S
    /// NEW / LATER (see <see cref="Updater"/>). Progress only redraws the strip; the links are rebuilt when the stage changes.</summary>
    void RefreshBanner()
    {
        if (bannerSlot == null || bannerRev == UpdateCheck.Revision) return;
        bannerRev = UpdateCheck.Revision;
        string? ver = UpdateCheck.BannerVersion;
        var stage = ver == null ? UpdateBanner.Stage.Available : BannerStage(ver);
        if (banner != null && IsInstanceValid(banner) && ver == banner.Version && stage == banner.Kind) { banner.QueueRedraw(); return; }
        foreach (var c in bannerSlot.GetChildren()) c.QueueFree();
        banner = null;
        if (ver == null) return;
        float k = K;
        bannerSlot.AddChild(Spacer(0, 22 * k));
        banner = new MUpdateStrip { K = k, Version = ver, Kind = stage, CustomMinimumSize = new Vector2(0, 40 * k) };
        var row = HBox(26 * k);
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        row.OffsetBottom = -4 * k;
        row.Alignment = BoxContainer.AlignmentMode.End;
        banner.AddChild(row);
        string? url = UpdateCheck.LatestUrl ?? (AppInfo.HasRepo ? AppInfo.LatestReleaseUrl : null);
        bool hasPage = url != null && AppInfo.HasRepo;
        float d = 0.1f;
        MLink Link(string label, Action act, string tip, bool accent = false, bool disabled = false)
        {
            var l = new MLink { K = k, Label = label, Px = 11.5f, Delay = d, TooltipText = tip, Tint = accent ? UiTheme.Accent : null, SizeFlagsVertical = SizeFlags.ShrinkCenter };
            d += Mn.Stagger;
            l.Pressed += act;
            l.Disabled = disabled;
            row.AddChild(l);
            return l;
        }
        void Hide() { UpdateCheck.BannerDismissed = true; bannerRev = -1; RefreshBanner(); }
        switch (stage)
        {
            case UpdateBanner.Stage.Ready:
                Link("RESTART TO UPDATE", Updater.RestartToUpdate, Updater.NeedsElevation
                    ? "Installs the update and restarts CA-VAL-AIM. CA-VAL-AIM is installed for all users, so Windows asks for permission"
                    : "Installs the update and restarts CA-VAL-AIM (takes a few seconds; settings and stats are kept)", accent: true);
                Link("WHAT'S NEW", () => AppInfo.OpenGitHub(url), "Opens the release notes in your browser", disabled: !hasPage);
                Link("LATER", Hide, Updater.NeedsElevation ? "Hide until next launch" : "Installs the next time you start CA-VAL-AIM");
                break;
            case UpdateBanner.Stage.Applying:
                break;
            default:
                if (stage == UpdateBanner.Stage.Downloading)
                    Link("WHAT'S NEW", () => AppInfo.OpenGitHub(url), "Opens the release notes in your browser", disabled: !hasPage);
                else
                    Link("DOWNLOAD", () => { if (!Updater.UpdateNow()) AppInfo.OpenGitHub(url); },
                        hasPage ? "Downloads the update, verifies it against SHA256SUMS.txt and offers to restart" : "No download page is set for this build",
                        accent: true, disabled: !hasPage);
                Link("SKIP THIS VERSION", () => UpdateCheck.SkipVersion(ver), $"Don't remind me about {ver} again (and don't download it)");
                Link("HIDE", Hide, "Hide until next launch");
                break;
        }
        bannerSlot.AddChild(banner);
    }

    static UpdateBanner.Stage BannerStage(string ver)
    {
        if (Updater.Status == Updater.State.Applying && Updater.Target == ver) return UpdateBanner.Stage.Applying;
        if (Updater.ReadyVersion == ver) return UpdateBanner.Stage.Ready;
        if (Updater.Status == Updater.State.Downloading && Updater.Target == ver) return UpdateBanner.Stage.Downloading;
        return UpdateBanner.Stage.Available;
    }

    // ---------------- coach hint (top problem one-liner, once the profile is ready) ----------------

    MLink? coachHint;
    bool coachHintDone;
    static bool devScreenDone, menuShotDone;

    public override void _Ready()
    {
        CoachData.Request(); // background: ready by the time the player looks at the header
        CheckMatchPrompt(false);
        base._Ready();
        if (devScreenDone || !Main.I.Dev) return;
        devScreenDone = true;
        // Dev-only "--menushot path [--menushot-delay S]": save the menu's own viewport and quit. Unlike a desktop
        // capture this works while the machine is locked (the Vulkan frame still renders).
        if (CmdLine.DevAfter("--menushot") is { } ms && !menuShotDone)
        {
            menuShotDone = true;
            float delay = float.TryParse(CmdLine.DevAfter("--menushot-delay"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var sd) ? sd : 2.6f;
            GetTree().CreateTimer(delay).Timeout += () =>
            {
                if (!IsInstanceValid(this) || !IsInsideTree()) return;
                var img = GetViewport().GetTexture().GetImage();
                string path = ms.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? ms : ms + ".png";
                img.SavePng(path);
                GD.Print($"[menushot] saved {path}");
                GetTree().Quit();
            };
        }
        // Dev-only: "--matchlog-readout" shows the Lock-In match-log readout over the menu (with --matchlog-demo [N]).
        if (CmdLine.Has("--matchlog-readout")) Callable.From(() => AddChild(new Warmup.MatchLogDevPanel(K))).CallDeferred();
        // Dev-only: "--screen profile" opens the coach screen directly (layout checks / screenshots).
        var a = OS.GetCmdlineUserArgs().Concat(OS.GetCmdlineArgs()).ToArray();
        int i = Array.IndexOf(a, "--screen");
        if (i >= 0 && i + 1 < a.Length && a[i + 1].Equals("profile", StringComparison.OrdinalIgnoreCase))
            Callable.From(Main.I.ShowProfile).CallDeferred();
        // Dev-only: "--screen bugreport" opens the Report a bug panel over the menu (+ --bugreport-demo, --bugreport-answer).
        if (i >= 0 && i + 1 < a.Length && a[i + 1].Equals("bugreport", StringComparison.OrdinalIgnoreCase))
            GetTree().CreateTimer(0.5).Timeout += () => { if (IsInstanceValid(this) && IsInsideTree()) ShowBugReport(); };
        // Dev-only: "--preview <drill key>" opens that drill's preview popup (screenshots of the live demo).
        if (CmdLine.DevAfter("--preview") is { } pk && ModeRegistry.Find(pk) is { } pmake)
            GetTree().CreateTimer(0.5).Timeout += () => { if (IsInstanceValid(this) && IsInsideTree()) DrillPreview.Open(this, K, pmake, () => Main.I.StartMode(pmake), freePlay: () => Main.I.StartMode(pmake, true)); };
        // Dev-only: "--screen suggest" opens the Suggest a feature panel (same --bugreport-* flags).
        if (i >= 0 && i + 1 < a.Length && a[i + 1].Equals("suggest", StringComparison.OrdinalIgnoreCase))
            GetTree().CreateTimer(0.5).Timeout += () => { if (IsInstanceValid(this) && IsInsideTree()) ShowSuggest(); };
        // Dev-only: "--map-picker" opens the map pop-up once the layout has settled (screenshots).
        if (a.Contains("--map-picker"))
            GetTree().CreateTimer(0.5).Timeout += () => { if (IsInstanceValid(this) && IsInsideTree()) mapSel?.Open(); };
        // Dev-only: "--menu-start <drill key>" presses that drill's START after a delay (loading-screen screenshots).
        if (CmdLine.DevAfter("--menu-start") is { } sk && ModeRegistry.Find(sk) is { } smake)
            GetTree().CreateTimer(1.2).Timeout += () => { if (IsInstanceValid(this) && IsInsideTree()) Main.I.StartMode(smake); };
    }

    protected override void Tick(float dt)
    {
        if (!coachHintDone) UpdateCoachHint();
        if (bannerRev != UpdateCheck.Revision) RefreshBanner();
        // the menu may stay open while the player is in VALORANT: look again now and then
        if ((matchRecheck -= dt) <= 0) CheckMatchPrompt(true);
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationApplicationFocusIn && IsInsideTree()) CheckMatchPrompt(true); // back from VALORANT
    }

    void UpdateCoachHint()
    {
        if (coachHint == null || CoachData.Ready is not { } p) return;
        coachHintDone = true;
        if (!CoachData.HasData(p)) return;
        string? rank = CoachData.HasOverall(p) ? CoachRank.RangeText(p.OverallTier, p.OverallConfidence) : null;
        string? problem = p.Problems.FirstOrDefault()?.Title;
        if (rank == null && problem == null) return;
        var parts = new List<string> { L("Coach", "Coach") };
        if (rank != null) parts.Add(rank);
        if (problem != null) parts.Add((Loc.Spanish ? "corrige primero: " : "fix first: ") + Loc.T(problem));
        coachHint.Label = string.Join("  ·  ", parts);
        int fs = UiTheme.Fs(coachHint.Px, K);
        coachHint.CustomMinimumSize = new Vector2(Gfx.TextW(UiTheme.Body, coachHint.Label, fs) + fs * 1.6f, 30 * K);
        coachHint.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        coachHint.Visible = true;
        coachHint.QueueRedraw();
    }

    // ---------------- match log (Lock-In) ----------------

    VBoxContainer? matchSlot;
    Warmup.WarmupRecord? matchAsk;
    bool matchDone;
    float matchRecheck = 30;

    /// <summary>"How did your first match after Lock-In go?": at most one card per menu, once per Lock-In, never while
    /// the "What's new" or updates panels are up (see <see cref="Warmup.LockInMatchLog.Due"/>).</summary>
    void CheckMatchPrompt(bool refill)
    {
        matchRecheck = 30;
        if (matchAsk != null || matchDone || WhatsNew.Pending != null || UpdatesPrompt.Pending) return;
        var rec = Warmup.LockInMatchLog.Due(Warmup.WarmupStore.I, DateTime.Now);
        if (rec == null) return;
        matchAsk = rec;
        Warmup.LockInMatchLog.MarkAsked(rec);
        if (refill) Callable.From(Rebuild).CallDeferred(); // the setup rows make room
    }

    void FillMatchSlot()
    {
        if (matchSlot == null) return;
        foreach (var c in matchSlot.GetChildren()) c.QueueFree();
        bool show = matchAsk != null && !matchDone;
        matchSlot.Visible = show;
        if (show) matchSlot.AddChild(new Warmup.MatchLogPrompt(matchAsk!, K, CloseMatchPrompt));
    }

    void CloseMatchPrompt()
    {
        matchDone = true;
        Callable.From(Rebuild).CallDeferred();
    }
}
