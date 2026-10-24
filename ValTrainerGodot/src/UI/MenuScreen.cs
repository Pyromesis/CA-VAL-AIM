using System.Globalization;
using Godot;
using ValTrainer.Core;
using ValTrainer.Game;
using ValTrainer.Maps;
using ValTrainer.Modes;
using ValTrainer.World;

namespace ValTrainer.UI;

/// <summary>
/// Main menu: VALORANT-styled lobby. Left: tier selector (with the map selector on its caption row) and the drill
/// cards, grouped by category (PB at the current tier, best rank badge ever) and packed into balanced columns so the
/// whole list fits without scrolling on 16:9 screens. Agent drills live on the Agents screen; a link card stands in for
/// them. Right: everything imported from Valorant plus a live crosshair preview. Top-right: Lock in / Agents / Coach /
/// Settings / Stats / Quit.
/// </summary>
public partial class MenuScreen : ScreenBase
{
    /// <summary>Category of the per-agent drills: listed on the Agents screen, not in the menu grid.</summary>
    public const string AgentsCategory = "Agents";

    readonly List<(Func<TrainingMode> Make, TrainingMode Info)> modes =
        ModeRegistry.All.Select(f => (Make: f, Info: f())).Where(m => m.Info.Category != AgentsCategory).ToList();
    readonly List<VButton> tierButtons = new();
    readonly List<ModeCard> cards = new();
    int hoverTier = -1;
    DrawBox? tierInfo;
    MapSelector? mapSel;
    /// <summary>"ALL" or one of the drill categories: only that category's cards are shown.</summary>
    string catFilter = "ALL";
    /// <summary>Record summary for the hero and the rail; computed once per menu build from the run log.</summary>
    SessionSummary summary;

    /// <summary>Esc closes the map pop-up; otherwise it does nothing on the menu (Quit is a button).</summary>
    protected override void Back()
    {
        if (mapSel?.IsOpen == true) mapSel.Close();
    }

    VBoxContainer? bannerSlot;
    int bannerRev = -1;

    protected override void Build()
    {
        tierButtons.Clear(); cards.Clear();
        mapSel = null;
        coachHint = null; coachHintDone = false;
        summary = SessionSummary.From(Main.I.Stats.Runs);
        // Dev-only: "--menu-filter aim|valorant|..." opens the menu on that category (layout screenshots).
        if (Main.I.Dev && CmdLine.DevAfter("--menu-filter") is { } mf)
            catFilter = new[] { "ALL" }.Concat(modes.Select(m => m.Info.Category))
                .FirstOrDefault(c => c.Equals(mf, StringComparison.OrdinalIgnoreCase)) ?? "ALL";
        float k = K;

        // screen-wide animated frame: breathing corner brackets, a slow scanline and rotating corner arcs
        AddChild(new MenuFX { K = k });

        // App shell: a fixed navigation rail on the left, the training hub in the middle, the live setup inspector on the right.
        var root = HBox(0);
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(root);
        root.AddChild(Sidebar());

        var main = Margins(VBox(0), 26 * k, 18 * k, 24 * k, 12 * k);
        main.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(main);
        var col = (VBoxContainer)main.GetChild(0);

        col.AddChild(TopHeader());

        // "ValTrainer X is available / downloading / ready" strip (filled when the background update check answers)
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

        col.AddChild(Spacer(0, 10 * k));

        var body = HBox(20 * k);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        col.AddChild(body);

        var center = VBox(8 * k);
        center.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.AddChild(center);

        float rightW = 360 * k;
        var right = VBox(14 * k);
        right.CustomMinimumSize = new Vector2(rightW, 0);
        body.AddChild(right);
        BuildRight(right);

        var vp = GetViewportRect().Size;
        float centerW = Mathf.Max(360 * k, vp.X - (252 + 50) * k - rightW - 20 * k);
        BuildCenter(center, centerW);

        col.AddChild(Spacer(0, 8 * k));
        col.AddChild(new DrawBox
        {
            CustomMinimumSize = new Vector2(0, 1),
            OnDraw = d => d.DrawRect(new Rect2(0, 0, d.Size.X, 1), UiTheme.Line),
        });
        col.AddChild(Spacer(0, 4 * k));
        col.AddChild(Footer());
    }

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

    /// <summary>The menu's REPORT A BUG button.</summary>
    void ShowBugReport()
    {
        if (GetChildren().Any(c => c is BugReportPanel)) return;
        AddChild(new BugReportPanel(K, "menu", () => { }));
    }

    /// <summary>The menu's SUGGEST A FEATURE button: the same form, posting to #suggestions.</summary>
    void ShowSuggest()
    {
        if (GetChildren().Any(c => c is BugReportPanel)) return;
        AddChild(new BugReportPanel(K, "menu", () => { }, idea: true));
    }

    // ---------------- sidebar: brand, navigation, categories, quick settings ----------------

    /// <summary>
    /// The app shell rail: brand, the five destinations, the drill categories and the loadout switches, all in one
    /// quiet column so the training area can stay open and editorial.
    /// </summary>
    Control Sidebar()
    {
        float k = K;
        var panel = new DrawBox
        {
            CustomMinimumSize = new Vector2(252 * k, 0),
            OnDraw = d =>
            {
                var s = d.Size;
                // glass column: deep navy wash, teal-lit from the top-left, red beacon under the brand
                d.DrawRect(new Rect2(0, 0, s.X, s.Y), new Color(0.030f, 0.039f, 0.052f, 0.94f));
                d.DrawRect(new Rect2(0, 0, s.X, Mathf.Min(240 * k, s.Y)), new Color(0.10f, 0.16f, 0.15f, 0.055f));
                Gfx.VGradient(d, new Rect2(0, 0, s.X, s.Y), new Color(1, 1, 1, 0.030f), new Color(0, 0, 0, 0.16f));
                Gfx.HGradient(d, new Rect2(0, 0, s.X, 2 * k), new Color(UiTheme.Teal, 0.75f), new Color(UiTheme.Accent, 0.15f));
                d.DrawRect(new Rect2(0, 2 * k, 64 * k, 1.6f * k), new Color(UiTheme.Teal, 0.75f));
                Gfx.Glow(d, new Vector2(13 * k, 22 * k), 44 * k, new Color(UiTheme.Accent, 0.10f), 6);
                // circuit spine along the left edge: three ticks
                d.DrawRect(new Rect2(0, s.Y * 0.34f, 2 * k, 12 * k), new Color(UiTheme.Teal, 0.28f));
                d.DrawRect(new Rect2(0, s.Y * 0.62f, 2 * k, 8 * k), new Color(UiTheme.Teal, 0.18f));
                d.DrawRect(new Rect2(s.X - 1, 0, 1, s.Y), new Color(1, 1, 1, 0.08f));
            },
        };
        var pad = Margins(VBox(0), 18 * k, 16 * k, 16 * k, 12 * k);
        pad.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.AddChild(pad);
        var v = (VBoxContainer)pad.GetChild(0);

        v.AddChild(BrandBlock());
        v.AddChild(Spacer(0, 8 * k));
        v.AddChild(new SectionLabel { Text = "MENU", K = k, CustomMinimumSize = new Vector2(0, 22 * k) });
        Nav(v, "TRAINING", NavIcon.Crosshair, true, () =>
        {
            if (catFilter == "ALL") return;
            catFilter = "ALL";
            Rebuild();
        });
        Nav(v, "AGENTS", NavIcon.Agents, false, Main.I.ShowAgents);
        Nav(v, "COACH", NavIcon.Coach, false, Main.I.ShowProfile);
        Nav(v, "STATS", NavIcon.Stats, false, Main.I.ShowStats);
        Nav(v, "SETTINGS", NavIcon.Settings, false, Main.I.ShowSettings);

        v.AddChild(Spacer(0, 8 * k));
        v.AddChild(new DrawBox
        {
            CustomMinimumSize = new Vector2(0, 1),
            OnDraw = d => d.DrawRect(new Rect2(0, 0, d.Size.X, 1), UiTheme.Line),
        });
        v.AddChild(Spacer(0, 6 * k));
        v.AddChild(new SectionLabel { Text = "CATEGORIES", K = k, CustomMinimumSize = new Vector2(0, 22 * k) });
        SidebarCategories(v);
        v.AddChild(Spacer(0, 12 * k));
        v.AddChild(new StreakCard
        {
            K = k, Data = summary, CustomMinimumSize = new Vector2(0, 82 * k),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Days you have trained in a row. One drill a day keeps it alive.",
        });

        v.AddChild(Expander());
        v.AddChild(new SectionLabel { Text = "QUICK SETTINGS", K = k, CustomMinimumSize = new Vector2(0, 22 * k) });
        SideGroup(v, "RIFLE", new[] { "VANDAL", "PHANTOM" }, () => Main.I.Settings.PrimaryRifle == 1 ? 1 : 0,
            i => { Main.I.Settings.PrimaryRifle = i; Main.I.Settings.Save(); });
        SideGroup(v, "AMMO", new[] { "DEFAULT", "REAL", "INFINITE" }, () => Main.I.Settings.AmmoMode switch { 0 => 1, 1 => 2, _ => 0 },
            i => { Main.I.Settings.AmmoMode = i switch { 0 => -1, 1 => 0, _ => 1 }; Main.I.Settings.Save(); });
        SideGroup(v, "LANGUAGE", new[] { "EN", "ES" }, () => Loc.Spanish ? 1 : 0,
            i => Main.I.SetLanguage(i == 1 ? "es" : "en"));

        v.AddChild(Spacer(0, 3 * k));
        v.AddChild(new UpdateNowButton { K = k, CustomMinimumSize = new Vector2(0, 28 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill });
        v.AddChild(Spacer(0, 4 * k));
        Nav(v, "QUIT", NavIcon.Power, false, Main.I.Quit, 32);
        return panel;
    }

    /// <summary>Brand lockup: an animated orbiting servo on a red plate, the VALTRAINING wordmark and the discipline line.</summary>
    Control BrandBlock()
    {
        float k = K;
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 2);
        v.AddChild(new BrandTile { K = k, CustomMinimumSize = new Vector2(0, 34 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var word = new DrawBox
        {
            CustomMinimumSize = new Vector2(0, 18 * k),
            OnDraw = d =>
            {
                int fs = UiTheme.Fs(19, k);
                float baseY = Gfx.Mid(11 * k, fs);
                float valW = Gfx.RawW(UiTheme.DisplayWide, "VAL", fs);
                d.DrawString(UiTheme.DisplayWide, new Vector2(2 * k, baseY), "VAL", HorizontalAlignment.Left, -1, fs, UiTheme.Accent);
                d.DrawString(UiTheme.DisplayWide, new Vector2(2 * k + valW, baseY), "TRAINING", HorizontalAlignment.Left, -1, fs, UiTheme.Text);
            },
        };
        v.AddChild(word);
        v.AddChild(new DrawBox
        {
            CustomMinimumSize = new Vector2(0, 10 * k),
            OnDraw = d =>
            {
                int ss = UiTheme.Fs(7.8f, k);
                d.DrawString(UiTheme.HudWide, new Vector2(3 * k, Gfx.Mid(4.5f * k, ss)), "AIM · MOVEMENT · UTILITY",
                    HorizontalAlignment.Left, -1, ss, UiTheme.Faint);
                Gfx.HGradient(d, new Rect2(2 * k, 8.2f * k, 220 * k, 1), new Color(UiTheme.Teal, 0.30f), new Color(UiTheme.Teal, 0f));
            },
        });
        return v;
    }

    /// <summary>The brand emblem: a red cut-corner plate, a crosshair core, an orbiting sensor dot and a slowly rotating dashed ring.</summary>
    public partial class BrandTile : Control
    {
        public float K = 1f;
        float t = 1f; // start mid-animation so the orbit is already in place on first paint

        public BrandTile() => MouseFilter = MouseFilterEnum.Ignore;

        public override void _Process(double delta)
        {
            t += (float)delta;
            QueueRedraw();
        }

        public override void _Draw()
        {
            float k = K;
            var c = new Vector2(20 * k, Size.Y / 2);
            float rr = 15 * k;
            // plate with glow
            Gfx.Glow(this, c, rr + 10 * k, new Color(UiTheme.Accent, 0.16f), 6);
            Gfx.PlateEx(this, new Rect2(c.X - rr, c.Y - rr, rr * 2, rr * 2), 5 * k, true, false, false, true,
                UiTheme.Accent.Darkened(0.08f), new Color(1, 1, 1, 0.20f), 1);
            // rotating dashed ring
            for (int i = 0; i < 10; i++)
            {
                float a0 = t * 0.9f + i * Mathf.Tau / 10f;
                DrawArc(c, rr + 6.5f * k, a0, a0 + Mathf.Tau / 24f, 6, new Color(1, 1, 1, 0.22f), 1.1f * k, true);
            }
            // crosshair core
            var w = new Color(1, 1, 1, 0.97f);
            float g = 6.5f * k, l = 4 * k, tk = Mathf.Max(1.1f, 1.5f * k);
            DrawLine(c + new Vector2(-g - l, 0), c + new Vector2(-g, 0), w, tk, true);
            DrawLine(c + new Vector2(g, 0), c + new Vector2(g + l, 0), w, tk, true);
            DrawLine(c + new Vector2(0, -g - l), c + new Vector2(0, -g), w, tk, true);
            DrawLine(c + new Vector2(0, g), c + new Vector2(0, g + l), w, tk, true);
            DrawCircle(c, 1.6f * k, w, true, -1, true);
            // orbiting sensor
            float oa = t * 2.2f;
            DrawCircle(c + new Vector2(Mathf.Cos(oa), Mathf.Sin(oa)) * (rr + 6.5f * k), 2 * k, UiTheme.Teal, true, -1, true);
        }
    }

    void Nav(VBoxContainer v, string label, NavIcon icon, bool selected, Action onClick, float height = 36)
    {
        var b = new NavButton
        {
            K = K, Label = label, Icon = icon, Selected = selected,
            CustomMinimumSize = new Vector2(0, height * K), SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        b.Pressed += onClick;
        v.AddChild(b);
    }

    void SidebarCategories(VBoxContainer v)
    {
        float k = K;
        var cats = new List<string> { "ALL" };
        cats.AddRange(modes.Select(m => m.Info.Category).Distinct());
        if (!cats.Contains(catFilter)) catFilter = "ALL";
        foreach (string cat in cats)
        {
            string key = cat;
            int count = key == "ALL" ? modes.Count : modes.Count(m => m.Info.Category == key);
            var b = new CategoryButton
            {
                K = k, Label = key.ToUpperInvariant(), Count = count,
                Selected = catFilter == key, CustomMinimumSize = new Vector2(0, 30 * k),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = count == 1 ? "1 drill" : $"{count} drills",
            };
            b.Pressed += () => { if (catFilter == key) return; catFilter = key; Rebuild(); };
            v.AddChild(b);
        }
    }

    /// <summary>One labelled segmented switch in the rail (rifle, ammo, language).</summary>
    void SideGroup(VBoxContainer v, string label, string[] options, Func<int> get, Action<int> set)
    {
        float k = K;
        var l = Lbl(label, UiTheme.HudWide, 9.5f, UiTheme.Faint);
        l.CustomMinimumSize = new Vector2(0, 12 * k);
        v.AddChild(l);
        var row = HBox(4 * k);
        var buttons = new List<VButton>();
        for (int i = 0; i < options.Length; i++)
        {
            int idx = i;
            var b = new VButton
            {
                Kind = VButton.Look.Select, K = k, Label = options[i], FontPx = 10.5f,
                CustomMinimumSize = new Vector2(0, 26 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            b.Pressed += () =>
            {
                for (int j = 0; j < buttons.Count; j++) buttons[j].SetSelected(j == idx);
                set(idx);
            };
            buttons.Add(b);
            row.AddChild(b);
        }
        int current = Math.Clamp(get(), 0, buttons.Count - 1);
        buttons[current].Selected = true;
        v.AddChild(row);
        v.AddChild(Spacer(0, 5 * k));
    }

    Control Divider() => new DrawBox
    {
        CustomMinimumSize = new Vector2(1 * K, 22 * K), SizeFlagsVertical = SizeFlags.ShrinkCenter,
        OnDraw = d => d.DrawRect(new Rect2(0, 0, 1, d.Size.Y), UiTheme.Line),
    };

    // ---------------- update banner ----------------

    UpdateBanner? banner;

    /// <summary>The strip under the header: "X is available" (DOWNLOAD opens the release page) → "Downloading update
    /// 42%" → "X is ready — RESTART TO UPDATE · WHAT'S NEW · LATER" (see <see cref="Updater"/>). Progress only redraws
    /// the text; the buttons are rebuilt when the stage changes.</summary>
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
        banner = new UpdateBanner { K = k, Version = ver, Kind = stage, CustomMinimumSize = new Vector2(0, 46 * k) };
        var row = HBox(10 * k);
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 236 * k; // text is drawn by the banner itself
        row.OffsetRight = -8 * k;
        row.Alignment = BoxContainer.AlignmentMode.End;
        banner.AddChild(row);
        string? url = UpdateCheck.LatestUrl ?? (AppInfo.HasRepo ? AppInfo.LatestReleaseUrl : null);
        bool hasPage = url != null && AppInfo.HasRepo;
        void Add(VButton b)
        {
            b.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(b);
        }
        VButton WhatsNew()
        {
            var b = Btn("WHAT'S NEW", VButton.Look.Ghost, () => AppInfo.OpenGitHub(url), 140, 32, 15);
            b.Disabled = !hasPage;
            b.TooltipText = "Opens the release notes in your browser";
            return b;
        }
        switch (stage)
        {
            case UpdateBanner.Stage.Ready:
            {
                var go = Btn("RESTART TO UPDATE", VButton.Look.Primary, Updater.RestartToUpdate, 210, 32, 16);
                go.TooltipText = Updater.NeedsElevation
                    ? "Installs the update and restarts ValTraining. ValTraining is installed for all users, so Windows asks for permission"
                    : "Installs the update and restarts ValTraining (takes a few seconds; settings and stats are kept)";
                Add(go);
                Add(WhatsNew());
                var later = Btn("LATER", VButton.Look.Ghost, () => { UpdateCheck.BannerDismissed = true; bannerRev = -1; RefreshBanner(); }, 100, 32, 15);
                later.TooltipText = Updater.NeedsElevation ? "Hide until next launch" : "Installs the next time you start ValTraining";
                Add(later);
                break;
            }
            case UpdateBanner.Stage.Applying:
                break;
            default:
            {
                if (stage == UpdateBanner.Stage.Downloading) Add(WhatsNew());
                else
                {
                    var dl = Btn("DOWNLOAD", VButton.Look.Secondary, () =>
                    {
                        if (!Updater.UpdateNow()) AppInfo.OpenGitHub(url);
                    }, 150, 32, 16);
                    dl.Disabled = !hasPage;
                    dl.TooltipText = dl.Disabled
                        ? "No download page is set for this build"
                        : "Downloads the update, verifies it against SHA256SUMS.txt and offers to restart";
                    Add(dl);
                }
                var skip = Btn("SKIP THIS VERSION", VButton.Look.Ghost, () => UpdateCheck.SkipVersion(ver), 170, 32, 15);
                skip.TooltipText = $"Don't remind me about {ver} again (and don't download it)";
                Add(skip);
                var close = Btn("×", VButton.Look.Ghost, () => { UpdateCheck.BannerDismissed = true; bannerRev = -1; RefreshBanner(); }, 36, 32, 24);
                close.TitleFont = UiTheme.Body;
                close.TooltipText = "Hide until next launch";
                Add(close);
                break;
            }
        }
        bannerSlot.AddChild(banner);
        bannerSlot.AddChild(Spacer(0, 8 * k));
    }

    static UpdateBanner.Stage BannerStage(string ver)
    {
        if (Updater.Status == Updater.State.Applying && Updater.Target == ver) return UpdateBanner.Stage.Applying;
        if (Updater.ReadyVersion == ver) return UpdateBanner.Stage.Ready;
        if (Updater.Status == Updater.State.Downloading && Updater.Target == ver) return UpdateBanner.Stage.Downloading;
        return UpdateBanner.Stage.Available;
    }

    // ---------------- header + hero ----------------

    Control TopHeader()
    {
        float k = K;
        var h = HBox(18 * k);
        h.CustomMinimumSize = new Vector2(0, 62 * k);

        var left = VBox(0);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        left.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        string title = Loc.T("What are we training today?");
        string sub = $"{MapSelector.NameOf(Main.I.MapKey)}  ·  {Loc.T(Difficulty.Get(Main.I.Tier).Name).ToUpperInvariant()}  ·  {Loc.Count(modes.Count, "drill", "drills")}";
        left.AddChild(new TitleBlock { K = k, Title = title, Sub = sub, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 52 * k) });
        h.AddChild(left);

        coachHint = new CoachHint
        {
            K = k, CustomMinimumSize = new Vector2(500 * k, 32 * k),
            SizeFlagsVertical = SizeFlags.ShrinkCenter, SizeFlagsHorizontal = SizeFlags.ShrinkEnd, Visible = false,
        };
        coachHint.Pressed += Main.I.ShowProfile;
        h.AddChild(coachHint);
        UpdateCoachHint();
        return h;
    }

    /// <summary>Header title with a typewriter reveal, a live accent caret, an animated underline rail and the
    /// map · tier · drill-count line under a running "signal" diamond.</summary>
    public partial class TitleBlock : Control
    {
        public float K = 1f;
        public string Title = "";
        public string Sub = "";
        float t; // header clock: the typewriter fills over ~0.7s, then the rail dot keeps the line alive

        public TitleBlock() => MouseFilter = MouseFilterEnum.Ignore;

        public override void _Process(double delta)
        {
            t += (float)delta;
            // the typewriter runs once; afterwards only the rail dot animates
            if (t < 1.0f || (t % 3.5f) < 3.49f) QueueRedraw();
        }

        public override void _Draw()
        {
            float k = K;
            var s = Size;
            if (s.X < 30 || s.Y < 20) return;
            int fs = UiTheme.Fs(30, k), ss = UiTheme.Fs(11, k);

            // accent slash marker
            int ms = UiTheme.Fs(30, k);
            float mx = 0;
            Gfx.Text(this, UiTheme.Display, "//", mx, Gfx.Mid(19 * k, ms), ms, UiTheme.Accent);
            mx += Gfx.TextW(UiTheme.Display, "//", ms);

            // typewriter title with caret
            string full = Title;
            float reveal = Gfx.EaseOutQuart(Mathf.Clamp((t - 0.15f) / 0.75f, 0, 1));
            int n = Math.Min(full.Length, (int)MathF.Ceiling(full.Length * reveal));
            string shown = full[..n];
            float tx = mx + 10 * k;
            float w = Gfx.RawW(UiTheme.Display, shown, fs);
            Gfx.TextFit(this, UiTheme.Display, shown, tx, Gfx.Mid(19 * k, fs), fs, UiTheme.Text, s.X - tx - 8 * k);
            if (reveal < 1f)
            {
                // caret blocks the next slot while typing
                var caretX = tx + MathF.Min(w + 3 * k, s.X - tx - 10 * k);
                bool on = t % 0.6f < 0.36f;
                DrawRect(new Rect2(caretX, 19 * k - fs * 0.62f, 10 * k, fs * 0.82f), new Color(UiTheme.Accent, on ? 0.85f : 0.25f));
            }
            float railX0 = tx, railX1 = s.X;
            // underline rail: draws itself after the typing, a dot keeps travelling
            float rl = Gfx.EaseOutQuart(Mathf.Clamp((t - 0.6f) / 0.55f, 0, 1));
            float railW = (railX1 - railX0) * rl;
            Gfx.HGradient(this, new Rect2(railX0, 40 * k, railW, 1.6f * k), new Color(UiTheme.Accent, 0.0f), new Color(UiTheme.Accent, 0.55f));
            Gfx.HGradient(this, new Rect2(railX0, 40 * k, Mathf.Max(2f, railW * 0.3f), 1.6f * k), new Color(UiTheme.Accent, 0.85f), new Color(UiTheme.Accent, 0.15f));
            float dotP = (t * 0.35f) % 1f;
            float dotX = railX0 + (railX1 - railX0) * dotP;
            DrawCircle(new Vector2(dotX, 40.8f * k), 2.2f * k, new Color(UiTheme.Accent, 0.8f));
            DrawCircle(new Vector2(dotX, 40.8f * k), 5.2f * k, new Color(UiTheme.Accent, 0.18f));

            // sub-line with a "signal" diamond breathing
            Gfx.Diamond(this, new Vector2(3 * k, 51 * k), (2.4f + 0.7f * Gfx.Wave(t, 0.8f)) * k, (2.4f + 0.7f * Gfx.Wave(t, 0.8f)) * k, UiTheme.Teal);
            Gfx.TextFit(this, UiTheme.HudWide, Sub, 12 * k, Gfx.Mid(51 * k, ss), ss, UiTheme.Faint, s.X - 16 * k);
        }
    }

    /// <summary>
    /// The focal point of the screen: a warm, softly glowing card that sells the guided Lock-In session, with a
    /// slowly rotating target on the right. Everything else on the page stays quieter than this.
    /// </summary>
    Control Hero()
    {
        float k = K;
        var hero = new Control
        {
            CustomMinimumSize = new Vector2(0, 224 * k), ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var bg = new HeroFX { K = k };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        hero.AddChild(bg);

        var content = Margins(VBox(0), 26 * k, 16 * k, 300 * k, 12 * k);
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        hero.AddChild(content);
        var v = (VBoxContainer)content.GetChild(0);
        v.Alignment = BoxContainer.AlignmentMode.Center;

        v.AddChild(Lbl("GUIDED SESSION  ·  12 MIN", UiTheme.HudWide, 10.5f, UiTheme.Accent));
        v.AddChild(Spacer(0, 3 * k));
        v.AddChild(Lbl("Aim better.", UiTheme.Display, 31, UiTheme.Text));
        v.AddChild(Lbl("Climb higher.", UiTheme.Display, 31, UiTheme.Accent));
        v.AddChild(Spacer(0, 5 * k));
        var sub = Lbl("Check-in, adaptive aim drills, a deathmatch and your goal — ready for ranked.",
            UiTheme.Body, 13, UiTheme.Dim);
        sub.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        sub.CustomMinimumSize = new Vector2(0, 18 * k);
        v.AddChild(sub);
        v.AddChild(Spacer(0, 8 * k));

        var row = HBox(8 * k);
        var start = Btn("START LOCK-IN", VButton.Look.Primary, Main.I.ShowLockIn, 186, 40, 15);
        start.Pulse = true;
        start.TooltipText = "Lock-In: about 12 minutes before ranked. A check-in, aim drills that adapt to you, a deathmatch, then your goal and cue";
        row.AddChild(start);
        var browse = Btn("BROWSE DRILLS", VButton.Look.Secondary, () =>
        {
            if (catFilter == "ALL") return;
            catFilter = "ALL";
            Rebuild();
        }, 148, 40, 13);
        row.AddChild(browse);
        v.AddChild(row);

        var rec = new HeroRecordPanel { K = k, Data = summary, LastLabel = LastModeLabel(), MouseFilter = MouseFilterEnum.Ignore };
        rec.AnchorLeft = 1; rec.AnchorRight = 1; rec.AnchorTop = 0.5f; rec.AnchorBottom = 0.5f;
        rec.OffsetLeft = -284 * k; rec.OffsetRight = -18 * k;
        rec.OffsetTop = -88 * k; rec.OffsetBottom = 88 * k;
        hero.AddChild(rec);
        return hero;
    }

    /// <summary>The last played drill's display name (falls back to the raw mode key).</summary>
    string LastModeLabel()
    {
        if (string.IsNullOrEmpty(summary.LastMode)) return ""; // default summary (no runs yet): null
        foreach (var (_, info) in modes)
            if (info.Key == summary.LastMode) return info.Name;
        return summary.LastMode;
    }

    /// <summary>Animated hero stage: an angular operation plate with sweeping light beams, rising spark motes, an
    /// ember bloom behind the record panel, live corner brackets and an entry underline that draws across. Redrawn
    /// every frame while the menu is open (one control, few primitives).</summary>
    public partial class HeroFX : Control
    {
        public float K = 1f;
        float t = 0.05f; // seconds since build: the first beat drives the entry animations
        readonly Vector2[] sparks = InitSparks();

        public HeroFX() => MouseFilter = MouseFilterEnum.Ignore;

        static Vector2[] InitSparks()
        {
            var rnd = new Random(7);
            var v = new Vector2[14];
            for (int i = 0; i < v.Length; i++)
                v[i] = new Vector2((float)rnd.NextDouble(), (float)rnd.NextDouble()); // x, phase
            return v;
        }

        public override void _Process(double delta)
        {
            t += (float)delta;
            QueueRedraw();
        }

        public override void _Draw()
        {
            float k = K;
            var s = Size;
            if (s.X < 40 || s.Y < 40) return;

            // plate: cut corners, dark fill, hairline + drop shadow
            for (int i = 3; i >= 1; i--)
                Gfx.PlateEx(this, new Rect2(-i * 2.4f * k, -i * 2 * k + i * k, s.X + i * 4.8f * k, s.Y + i * 4.8f * k),
                    22 * k, false, true, true, false, default, new Color(0, 0, 0, 0.05f * i), i * 1.4f);
            Gfx.PlateEx(this, new Rect2(0, 0, s.X, s.Y), 22 * k, false, true, true, false,
                new Color(0.082f, 0.055f, 0.060f, 0.96f), new Color(1, 1, 1, 0.10f), 1);

            // sweeping diagonal beams: slanted glow quads, bright at the head, fading at both ends
            for (int b = 0; b < 3; b++)
            {
                float ph = (t * (0.05f + 0.017f * b) + b * 0.41f) % 1f;
                float cx = ph * (s.X + 360 * k) - 180 * k;
                float w2 = (90 + 36 * b) * k;
                if (cx + w2 < 0 || cx - w2 > s.X) continue;
                float a = 0.045f * (0.5f + 0.5f * MathF.Sin(t * 0.7f + b * 1.7f));
                Span<Vector2> p = stackalloc Vector2[4];
                Span<Color> co = stackalloc Color[4];
                p[0] = new(Mathf.Max(-2, cx - w2), s.Y + 6); p[1] = new(cx, s.Y * -0.55f);
                p[2] = new(cx + w2 * 0.55f, s.Y * -0.55f); p[3] = new(cx + w2, s.Y + 6);
                co[0] = new Color(UiTheme.Accent, a * 0.2f); co[1] = new Color(UiTheme.Accent, 0);
                co[2] = new Color(UiTheme.Accent, a * 0.25f); co[3] = new Color(UiTheme.Accent, 0);
                this.GradPoly(p, co);
            }

            // ember bloom behind the record half + ring
            Gfx.Glow(this, new Vector2(s.X - 150 * k, s.Y * 0.5f), (158 + 12 * Gfx.Wave(t, 0.4f)) * k, new Color(UiTheme.Accent, 0.10f), 8);
            Gfx.Glow(this, new Vector2(s.X * 0.30f, s.Y * 1.02f), 210 * k, new Color(UiTheme.Teal, 0.030f), 6);
            DrawArc(new Vector2(s.X - 150 * k, s.Y * 0.5f), 134 * k, 0, Mathf.Tau, 96, new Color(1, 1, 1, 0.04f), 1 * k, true);
            // rotating tick ring around the bloom
            for (int i = 0; i < 12; i++)
            {
                float a0 = t * 0.25f + i * Mathf.Tau / 12f;
                DrawArc(new Vector2(s.X - 150 * k, s.Y * 0.5f), 148 * k, a0, a0 + 0.14f, 6, new Color(UiTheme.Accent, 0.28f), 1.6f * k, true);
            }

            // rising spark motes
            float top = 6 * k, bot = s.Y - 6 * k;
            for (int i = 0; i < sparks.Length; i++)
            {
                var sp = sparks[i];
                float sp2 = (t * 0.10f + sp.Y) % 1f;
                float y = Mathf.Lerp(bot, top, sp2);
                float x = sp.X * (s.X * 0.56f);
                float fade = Mathf.Clamp(sp2 * 4f, 0, 1) * Mathf.Clamp((1f - sp2) * 3f, 0, 1);
                DrawCircle(new Vector2(x, y), 1.3f * k, new Color(1f, 0.86f, 0.80f, 0.35f * fade));
            }

            // live corner brackets around the whole card
            float bl = (18 + 4 * Gfx.Wave(t, 0.5f)) * k;
            var col = new Color(UiTheme.Accent, 0.55f);
            BracketFX(this, new Vector2(8 * k, 8 * k), bl, corner: 1, col, 1.6f * k);
            BracketFX(this, new Vector2(s.X - 8 * k, 8 * k), bl, corner: 2, col, 1.6f * k);
            BracketFX(this, new Vector2(s.X - 8 * k, s.Y - 8 * k), bl, corner: 3, col, 1.6f * k);
            BracketFX(this, new Vector2(8 * k, s.Y - 8 * k), bl, corner: 0, col, 1.6f * k);

            // bottom-right status: three live blocks ("signal" equalizer)
            for (int i = 0; i < 3; i++)
            {
                float hh = (4 + 5 * Gfx.Wave(t * (0.7f + 0.15f * i) + i * 2.1f, 0.65f)) * k;
                DrawRect(new Rect2(s.X - 30 * k + i * 9 * k, s.Y - 10 * k - hh, 5 * k, hh),
                    new Color(UiTheme.Teal, 0.45f));
            }

            // entry underline drawing across the bottom-left
            float draw = Gfx.EaseOutCubic(Mathf.Clamp((t - 0.1f) / 0.7f, 0, 1));
            Gfx.HGradient(this, new Rect2(22 * k, s.Y - 2.4f * k, (260 * k) * draw, 2.4f * k),
                new Color(UiTheme.Accent, 0.8f), new Color(UiTheme.Accent, 0f));
        }

        static void BracketFX(CanvasItem ci, Vector2 c, float len, int corner, Color col, float w)
        {
            // 1 top-left, 2 top-right, 3 bottom-right, 0 bottom-left
            float hx = corner is 1 or 0 ? -1 : 1;
            float vy = corner is 1 or 2 ? -1 : 1;
            ci.DrawRect(new Rect2(c.X - (hx < 0 ? len : 0), c.Y - (vy < 0 ? w : 0), len, w), col);
            ci.DrawRect(new Rect2(c.X - (hx < 0 ? w : 0), c.Y - (vy < 0 ? len : 0), w, len), col);
        }
    }

    /// <summary>Slim bottom bar: the fan-made note, the community link and the version.</summary>
    Control Footer()
    {
        float k = K;
        var wrap = new Control { MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, 28 * k) };
        var fx = new FooterFX { K = k };
        fx.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        wrap.AddChild(fx);
        var f = HBox(8 * k);
        f.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        f.OffsetLeft = 16 * k; // room for the signal diamond
        var fan = Lbl("Fan-made trainer · not endorsed by or affiliated with Riot Games", UiTheme.Body, 12, UiTheme.Faint);
        fan.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        fan.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        f.AddChild(fan);
        void Link(string label, string tooltip, Action onClick, float width)
        {
            var b = Btn(label, VButton.Look.Ghost, onClick, width, 26, 12);
            b.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            b.TooltipText = tooltip;
            f.AddChild(b);
        }
        Link("GITHUB", "Source code, releases and issue tracker", () => AppInfo.OpenGitHub(AppInfo.RepoUrl), 82);
        var ver = Lbl("v" + AppInfo.Version + (Main.I.Dev && !CmdLine.Showcase ? " · DEV" : ""), UiTheme.HudWide, 11, UiTheme.Faint);
        ver.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        f.AddChild(ver);
        return wrap;
    }

    // ---------------- coach hint (top problem one-liner, once the profile is ready) ----------------

    CoachHint? coachHint;
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
        if (refill) Callable.From(Rebuild).CallDeferred(); // the crosshair preview makes room
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

    void UpdateCoachHint()
    {
        if (coachHint == null || CoachData.Ready is not { } p) return;
        coachHintDone = true;
        if (!CoachData.HasData(p)) return;
        coachHint.Rank = CoachData.HasOverall(p) ? CoachRank.RangeText(p.OverallTier, p.OverallConfidence) : null;
        coachHint.RankColor = CoachRank.Col(p.OverallTier);
        coachHint.Problem = p.Problems.FirstOrDefault()?.Title;
        coachHint.TooltipText = "Open the aim coach";
        coachHint.Visible = coachHint.Rank != null || coachHint.Problem != null;
        coachHint.QueueRedraw();
    }

    // ---------------- centre: hero, difficulty, drills ----------------

    void BuildCenter(VBoxContainer center, float centerW)
    {
        float k = K;
        center.AddChild(Hero());
        center.AddChild(Spacer(0, 8 * k));

        var top = HBox(14 * k);
        top.AddChild(new SectionLabel
        {
            Text = "DIFFICULTY", Note = "Bots and targets scale", K = k,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        mapSel = new MapSelector(this, k) { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        top.AddChild(mapSel);
        center.AddChild(top);

        bool narrow = centerW / k < 980;
        var tiers = HBox(5 * k);
        foreach (var t in Difficulty.Tiers)
        {
            var b = new VButton
            {
                Kind = VButton.Look.Select, K = k, Label = t.Name.ToUpperInvariant(), Sub = t.Ranks, Emblem = t.Id,
                FontPx = narrow ? 14 : 15.5f, SubPx = narrow ? 8.5f : 9.5f,
                CustomMinimumSize = new Vector2(0, 56 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Selected = Main.I.Tier == t.Id, TooltipText = TierLine(t),
            };
            int id = t.Id;
            b.Pressed += () => SelectTier(id);
            b.MouseEntered += () => { hoverTier = id; tierInfo?.QueueRedraw(); };
            b.MouseExited += () => { if (hoverTier == id) hoverTier = -1; tierInfo?.QueueRedraw(); };
            tierButtons.Add(b);
            tiers.AddChild(b);
        }
        center.AddChild(tiers);

        tierInfo = new DrawBox { CustomMinimumSize = new Vector2(0, 24 * k), OnDraw = DrawTierInfo };
        center.AddChild(tierInfo);

        BuildGrid(center, centerW);
    }

    // ---- the drill grid ----

    /// <summary>Card height (design units) with two description lines; cards scroll when a category is long.</summary>
    const float CardTwo = 150, CardGap = 9, LabelH = 30, GroupGap = 28, ColGap = 12, MinCardW = 280;

    static float GroupH(int cards, float cardH) => LabelH + cards * (cardH + CardGap);

    /// <summary>Drills added in a version show a NEW tag until played once, through that version's minor releases.</summary>
    static readonly Dictionary<string, string> AddedIn = new()
    {
        ["microshot"] = "1.4", ["switch"] = "1.4", ["popup"] = "1.4", ["longtaps"] = "1.4", ["jumppeek"] = "1.4",
        ["jigglepeek"] = "1.4", ["postplant"] = "1.4", ["retake"] = "1.4", ["anchor"] = "1.4", ["sound"] = "1.4", ["peek_mix"] = "1.10",
    };

    static bool IsNew(string key)
    {
        if (!AddedIn.TryGetValue(key, out var v) || !SemVer.TryParse(v, out var added) || !SemVer.TryParse(AppInfo.Version, out var app)) return false;
        bool current = added.Major > app.Major || (added.Major == app.Major && added.Minor >= app.Minor);
        return current && !Main.I.Stats.For(key).Any();
    }

    /// <summary>
    /// Drill cards in balanced columns. "ALL" groups them by category (plus the Agents link) so the tallest column is
    /// as short as possible (exhaustive search with pruning; there are only ~9 groups). A category filter shows just
    /// that category, which keeps the hub quiet; the columns scroll when the list is longer than the window.
    /// </summary>
    void BuildGrid(VBoxContainer center, float leftW)
    {
        float k = K;
        int ncol = leftW / k >= 1560 ? 4 : leftW / k >= 1000 ? 3 : 2;
        int shown = catFilter == "ALL" ? modes.Count : modes.Count(m => m.Info.Category == catFilter);
        var lib = new SectionLabel
        {
            Text = catFilter == "ALL" ? "ALL DRILLS" : catFilter.ToUpperInvariant(),
            Note = shown == 1 ? "1 drill" : $"{shown} drills", K = k, CustomMinimumSize = new Vector2(0, 28 * k),
        };
        center.AddChild(lib);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        center.AddChild(scroll);
        var colsBox = HBox(ColGap * k);
        colsBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(colsBox);

        var groups = modes.GroupBy(m => m.Info.Category).Select(g => (Cat: g.Key, Items: g.ToList())).ToList();
        const float cardH = CardTwo;
        int cardIdx = 0;

        void AddCard(VBoxContainer box, Func<TrainingMode> make, TrainingMode info)
        {
            var card = new ModeCard(info)
            {
                K = k, TwoLines = true, IsNew = IsNew(info.Key), AnimateIn = true, RevealDelay = Math.Min(0.22f, cardIdx++ * 0.022f),
                CustomMinimumSize = new Vector2(0, cardH * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            card.Pressed += () => DrillPreview.Open(this, K, make, () => Main.I.StartMode(make), freePlay: () => Main.I.StartMode(make, true)); // preview first, START plays
            cards.Add(card);
            box.AddChild(card);
        }

        if (catFilter != "ALL")
        {
            var items = groups.FirstOrDefault(g => g.Cat == catFilter).Items ?? new List<(Func<TrainingMode>, TrainingMode)>();
            var boxes = Enumerable.Range(0, ncol).Select(_ => VBox(CardGap * k)).ToList();
            foreach (var box in boxes)
            {
                box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                colsBox.AddChild(box);
            }
            for (int i = 0; i < items.Count; i++) AddCard(boxes[i % ncol], items[i].Item1, items[i].Item2);
            return;
        }

        var counts = groups.Select(g => g.Items.Count).Append(1).ToArray(); // last group: the Agents link card
        var assign = Partition(counts.Select(c => GroupH(c, cardH)).ToArray(), ncol, GroupGap, out _);
        var columns = Enumerable.Range(0, ncol)
            .Select(c => Enumerable.Range(0, counts.Length).Where(g => assign[g] == c).ToList())
            .OrderBy(l => l.Count == 0 ? int.MaxValue : l[0]).ToList();

        foreach (var list in columns)
        {
            var box = VBox(CardGap * k);
            box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            colsBox.AddChild(box);
            foreach (int g in list)
            {
                if (box.GetChildCount() > 0) box.AddChild(Spacer(0, (GroupGap - 2 * CardGap) * k));
                if (g == groups.Count)
                {
                    box.AddChild(new SectionLabel { Text = "AGENTS", Note = $"{Agents.AgentRoster.All.Length} agents", K = k });
                    var link = new AgentsLinkCard { K = k, TwoLines = true, CustomMinimumSize = new Vector2(0, cardH * k), SizeFlagsHorizontal = SizeFlags.ExpandFill };
                    link.Pressed += Main.I.ShowAgents;
                    box.AddChild(link);
                    continue;
                }
                var (cat, items) = groups[g];
                box.AddChild(new SectionLabel { Text = cat.ToUpperInvariant(), Note = items.Count == 1 ? "1 drill" : $"{items.Count} drills", K = k });
                foreach (var (make, info) in items) AddCard(box, make, info);
            }
        }
    }

    /// <summary>Assigns groups of heights <paramref name="h"/> to <paramref name="ncol"/> columns minimising the tallest one.</summary>
    static int[] Partition(float[] h, int ncol, float gap, out float tallest)
    {
        int n = h.Length;
        var order = Enumerable.Range(0, n).OrderByDescending(i => h[i]).ToArray();
        var assign = new int[n];
        var best = new int[n];
        var load = new float[ncol];
        var count = new int[ncol];
        float bestMax = float.MaxValue;
        void Place(int idx, float curMax)
        {
            if (curMax >= bestMax) return;
            if (idx == n) { bestMax = curMax; Array.Copy(assign, best, n); return; }
            int g = order[idx];
            for (int c = 0; c < ncol; c++)
            {
                if (c > 0 && count[c] == 0 && count[c - 1] == 0) break; // empty columns are interchangeable
                float add = h[g] + (count[c] > 0 ? gap : 0);
                load[c] += add; count[c]++; assign[g] = c;
                Place(idx + 1, Math.Max(curMax, load[c]));
                load[c] -= add; count[c]--;
            }
        }
        Place(0, 0);
        tallest = bestMax;
        return best;
    }

    static string TierLine(Tier t)
    {
        var (fmin, fmax) = Difficulty.FlickDistance(t.Id);
        return $"Bots react in ~{t.Bot.ReactMs:0} ms · Gridshot targets {Difficulty.GridshotRadius(t.Id) * 200:0} cm wide · "
             + $"head flicks at {fmin:0}–{fmax:0} m · {(t.Bot.RunAndGun ? "bots run-and-gun" : "bots stop before shooting")}";
    }

    void DrawTierInfo(DrawBox d)
    {
        float k = K, cy = d.Size.Y / 2;
        var t = Difficulty.Get(hoverTier >= 0 ? hoverTier : Main.I.Tier);
        var col = UiTheme.TierColor(t.Id);
        // emblem notch on the left
        Gfx.PlateEx(d, new Rect2(0, 3 * k, 24 * k, d.Size.Y - 6 * k), 6 * k, true, false, false, true,
            new Color(col, 0.10f), new Color(col, 0.32f), 1);
        RankEmblem.Draw(d, new Vector2(12 * k, cy), 9 * k, t.Id);
        int fs = UiTheme.Fs(17, k), ds = UiTheme.Fs(15, k);
        string head = $"{t.Name.ToUpperInvariant()}  ·  {t.Ranks.ToUpperInvariant()}";
        float w = Gfx.TextW(UiTheme.HudWide, head, fs);
        Gfx.Text(d, UiTheme.HudWide, head, 32 * k, Gfx.Mid(cy, fs), fs, col);
        Gfx.Text(d, UiTheme.HudWide, "›", 32 * k + w + 6 * k, Gfx.Mid(cy, fs), fs, new Color(col, 0.5f));
        Gfx.TextFit(d, UiTheme.Body, TierLine(t), 32 * k + w + 18 * k, Gfx.Mid(cy, ds), ds, UiTheme.Dim, d.Size.X - (32 * k + w + 18 * k));
    }

    void SelectTier(int id)
    {
        Main.I.Settings.Tier = id;
        Main.I.Settings.Save();
        for (int i = 0; i < tierButtons.Count; i++) tierButtons[i].SetSelected(Main.I.Tier == i);
        foreach (var c in cards) c.Refresh();
        tierInfo?.QueueRedraw();
    }

    // ---------------- right: imported settings + crosshair ----------------

    void BuildRight(VBoxContainer right)
    {
        float k = K;
        var app = Main.I;
        var v = app.Valorant;

        var panel = new VPanel { Title = "YOUR SETUP", K = k, Caption = v.Found ? "auto-imported" : "defaults" };
        right.AddChild(panel);
        // the match-log card goes between the two panels (the crosshair preview gives up the room)
        matchSlot = VBox(0);
        right.AddChild(matchSlot);
        FillMatchSlot();
        var rows = VBox(0);
        panel.AddChild(rows);

        string Mode(int m) => m switch { 0 => "Fullscreen", 1 => "Windowed Fullscreen", _ => "Windowed" };
        int stripe = 0;
        void Row(string label, Func<string> value, Func<Color>? color = null, Func<Color?>? swatch = null, Func<string?>? tag = null) =>
            rows.AddChild(new InfoRow { K = k, Label = label, Value = value, ValueColor = color, Swatch = swatch, Tag = tag, Stripe = stripe++ % 2 == 0 });

        var inv = CultureInfo.InvariantCulture;
        if (!v.Found) Row("Valorant data", () => "Not found — check Settings", () => UiTheme.Warn);
        Row("Sensitivity", () => app.Sens.ToString("0.###", inv), tag: () => app.Settings.UseSensOverride ? "CUSTOM" : null);
        Row("eDPI / cm per 360", () => Loc.Numeric($"{app.Sens * app.Settings.Dpi:0} · {PlayerView.Cm360(app.Sens, app.Settings.Dpi):0.0} cm @ {app.Settings.Dpi} DPI"));
        Row("Display", () => $"{v.ResX}×{v.ResY} · {Mode(app.WindowMode)}");
        Row("Enemy highlight", () => UiTheme.EnemyColorNames[Math.Clamp(app.EnemyColorIndex, 0, 3)], () => app.EnemyColor, () => app.EnemyColor,
            () => app.Settings.EnemyColorOverride >= 0 ? "OVERRIDE" : null);
        Row("Crosshair profile", () => Valorant.CrosshairCode.Effective.Name);

        // crosshair preview (gets the room the old read-only rows used)
        var xp = new VPanel { Title = "CROSSHAIR", K = k, Caption = "real pixel size", SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddChild(xp);
        var col = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", (int)(8 * k));
        xp.AddChild(col);
        // PRIMARY / ADS / SNIPER tabs only for a split profile (own ADS crosshair or sniper dot): the menu is tight.
        col.AddChild(new CrosshairPreview
        {
            K = k, BoxHeight = matchSlot.Visible ? 120 : 215, Backdrop = DrawRange, TabsOnlyWhenSplit = true, Caption = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        });
        VButton? copy = null;
        copy = Btn("COPY CROSSHAIR CODE", VButton.Look.Secondary, () =>
        {
            DisplayServer.ClipboardSet(Valorant.CrosshairCode.EffectiveCode);
            copy!.Label = "COPIED ✓";
            copy.QueueRedraw();
            GetTree().CreateTimer(2.0).Timeout += () => { if (IsInstanceValid(copy)) { copy.Label = "COPY CROSSHAIR CODE"; copy.QueueRedraw(); } };
        }, 0, 38, 15);
        copy.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        copy.TooltipText = "The VALORANT code of this crosshair: paste it in VALORANT → Settings → Crosshair → Import Profile Code";
        col.AddChild(copy);
    }

    /// <summary>Range-like backdrop for the crosshair preview: a dark bay, a lit wall, the dummy under the crosshair.</summary>
    void DrawRange(DrawBox d)
    {
        float k = K;
        var s = d.Size;
        if (s.X < 8 || s.Y < 8) return;
        var c = s / 2;
        float wallBottom = Mathf.Min(c.Y + 74 * k, s.Y - 14 * k);
        float bay = Mathf.Min(s.X * 0.34f, 150 * k);

        // deep wall + a warm pool of light right behind the target so the crosshair always reads
        Gfx.VGradient(d, new Rect2(0, 0, s.X, wallBottom), Color.Color8(46, 54, 64), Color.Color8(25, 30, 38));
        Gfx.Glow(d, c + new Vector2(0, -8 * k), Mathf.Max(74 * k, s.X * 0.34f),
            new Color(1f, 0.91f, 0.84f, 0.13f), 7);

        // range bay: a large arch, a ceiling beam and two hanging light bars
        d.DrawArc(c + new Vector2(0, -6 * k), bay, 0, Mathf.Tau, 96, new Color(1, 1, 1, 0.055f), 1.4f * k, true);
        d.DrawArc(c + new Vector2(0, -6 * k), bay * 1.16f, 0, Mathf.Tau, 96, new Color(0, 0, 0, 0.16f), 1f * k, true);
        d.DrawRect(new Rect2(0, 16 * k, s.X, 2 * k), new Color(1, 1, 1, 0.05f));
        d.DrawRect(new Rect2(0, 18 * k, s.X, 5 * k), new Color(0, 0, 0, 0.10f));
        for (int side = -1; side <= 1; side += 2)
        {
            float lx = c.X + side * s.X * 0.23f;
            var lp = new Vector2(lx, 26 * k);
            d.DrawRect(new Rect2(lp.X - 17 * k, lp.Y, 34 * k, 2 * k), new Color(1f, 0.88f, 0.80f, 0.5f));
            Gfx.Glow(d, lp, 26 * k, new Color(1f, 0.72f, 0.58f, 0.05f), 5);
            d.DrawRect(new Rect2(c.X + side * s.X * 0.40f, 18 * k, 2 * k, Mathf.Max(0, wallBottom - 18 * k)),
                new Color(1, 1, 1, 0.03f));
        }

        // wall seams
        var seam = new Color(0, 0, 0, 0.14f);
        for (float x = c.X % (90 * k); x < s.X; x += 90 * k)
            d.DrawRect(new Rect2(x, 18 * k, 1, Mathf.Max(0, wallBottom - 18 * k)), seam);

        // floor with perspective lanes and faint distance lines
        Gfx.VGradient(d, new Rect2(0, wallBottom, s.X, s.Y - wallBottom),
            Color.Color8(35, 41, 49), Color.Color8(17, 21, 26));
        d.DrawRect(new Rect2(0, wallBottom - 2 * k, s.X, 2 * k), new Color(0, 0, 0, 0.28f));
        var lane = new Color(1, 1, 1, 0.055f);
        for (int i = -7; i <= 7; i++)
            d.DrawLine(new Vector2(c.X + i * 26 * k, wallBottom), new Vector2(c.X + i * 150 * k, s.Y), lane, 1f);
        for (int i = 1; i <= 3; i++)
        {
            float y = wallBottom + (s.Y - wallBottom) * i / 4f;
            d.DrawRect(new Rect2(0, y, s.X, 1), new Color(1, 1, 1, 0.022f));
        }

        // dummy (head centred on the crosshair), outlined in the enemy highlight colour
        var enemy = Main.I.EnemyColor;
        var body = Color.Color8(96, 104, 114);
        float hr = 9 * k, ow = 2f * Mathf.Max(1, k);
        var head = c;
        var torso = new Rect2(c.X - 23 * k, c.Y + hr + 5 * k, 46 * k, Mathf.Max(2 * k, wallBottom - (c.Y + hr + 5 * k)));
        d.DrawRect(torso.Grow(ow), enemy);
        d.DrawCircle(head, hr + ow, enemy);
        d.DrawRect(new Rect2(c.X - 4 * k, c.Y + hr - 1, 8 * k, 8 * k), body);
        d.DrawRect(torso, body);
        d.DrawCircle(head, hr, body.Lightened(0.10f));

        // tiny range mark + vignette + brackets
        int rs = UiTheme.Fs(8.5f, k);
        d.DrawString(UiTheme.HudWide, new Vector2(12 * k, 15 * k), "VALTRAINING RANGE",
            HorizontalAlignment.Left, -1, rs, new Color(1, 1, 1, 0.18f));
        Gfx.EdgeVignette(d, s, Mathf.Min(110 * k, Mathf.Min(s.X, s.Y) * 0.35f), new Color(0, 0, 0, 0.30f));
        Gfx.Brackets(d, new Rect2(Vector2.Zero, s).Grow(-1), 14 * k, new Color(UiTheme.Text, 0.35f), Mathf.Max(1, 2 * k));
    }
}

/// <summary>Header one-liner linking to the aim coach: "COACH · GOLD 2 – PLATINUM 1 · FIX FIRST: …  ›" (right-aligned).</summary>
public partial class CoachHint : BaseButton
{
    public float K = 1f;
    public string? Rank, Problem;
    public Color RankColor = UiTheme.Text;
    float hover;

    public CoachHint()
    {
        FocusMode = FocusModeEnum.None;
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }

    public override void _Ready() => MouseEntered += UiTheme.HoverSound;

    public override void _Process(double delta)
    {
        float t = IsHovered() ? 1f : 0f;
        if (hover != t) { hover = Mathf.MoveToward(hover, t, (float)delta * 8f); QueueRedraw(); }
    }

    public override void _Draw()
    {
        float k = K, cy = Size.Y / 2;
        int ls = UiTheme.Fs(11, k), ts = UiTheme.Fs(13, k);
        // two right-aligned lines: COACH · RANK over the current top problem, so neither is truncated.
        // the chevron nudges 3px right and brightens with the pointer
        Gfx.Chevron(this, new Vector2(Size.X - 4 * k + 3 * k * hover, cy), 10 * k, 1,
            new Color(UiTheme.Accent.Lerp(UiTheme.Text, hover * 0.6f), 0.5f + 0.5f * hover), 2.5f * k);
        float right = Size.X - 18 * k;
        float y1 = Problem != null ? cy - 7 * k : cy;
        float hx = right;
        if (Rank != null)
        {
            float rw = Gfx.TextW(UiTheme.HudWide, Rank, ls);
            Gfx.TextR(this, UiTheme.HudWide, Rank, hx, Gfx.Mid(y1, ls), ls, RankColor);
            hx -= rw + 9 * k;
            DrawCircle(new Vector2(hx, y1), 1.4f * k, new Color(UiTheme.Faint, 0.8f), true, -1, true);
            hx -= 9 * k;
        }
        string head = "COACH";
        float headW = Gfx.TextW(UiTheme.HudWide, head, ls);
        Gfx.Text(this, UiTheme.HudWide, head, hx - headW, Gfx.Mid(y1, ls), ls, UiTheme.Accent);
        float diamondX = hx - headW - 10 * k;
        Gfx.Diamond(this, new Vector2(diamondX, y1), 4 * k, 4 * k, UiTheme.Accent);

        if (Problem != null)
        {
            string p = "Fix first: " + Problem;
            float avail = Mathf.Min(right, Size.X - 4 * k);
            float w = Mathf.Min(Gfx.TextW(UiTheme.Body, p, ts), avail);
            Gfx.TextFit(this, UiTheme.Body, p, right - w, Gfx.Mid(cy + 9 * k, ts), ts,
                UiTheme.Text.Lerp(Colors.White, hover * 0.3f), w, HorizontalAlignment.Right);
        }
        if (hover > 0.01f)
            DrawRect(new Rect2(diamondX - 6 * k, Size.Y - 1, Size.X - diamondX + 6 * k, 1), new Color(UiTheme.Accent, hover));
    }
}

/// <summary>Line icons for the navigation rail (drawn, so they match the 1px geometry of the rest of the UI).</summary>
public enum NavIcon { Crosshair, Agents, Coach, Stats, Settings, Power }

/// <summary>
/// One destination in the left rail: icon and label on a sliding wash, a breathing accent bar when selected, and a
/// hover chevron. The icon glides 2px toward the pointer's pull (hover) to sell the button.
/// </summary>
public partial class NavButton : BaseButton
{
    public string Label = "";
    public NavIcon Icon;
    public float K = 1f;
    public bool Selected;
    float hover, t;

    public NavButton()
    {
        FocusMode = FocusModeEnum.None;
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }

    public override void _Ready()
    {
        MouseEntered += UiTheme.HoverSound;
        Pressed += UiTheme.ClickSound;
    }

    public override void _Process(double delta)
    {
        float target = IsHovered() ? 1f : 0f;
        t += (float)delta;
        if (hover != target || Selected) { hover = Mathf.MoveToward(hover, target, (float)delta * 10f); QueueRedraw(); }
    }

    public override void _Draw()
    {
        float k = K, h = hover;
        var r = new Rect2(Vector2.Zero, Size);
        if (Selected)
        {
            // deep wash with a cut top-right corner, a breathing accent bar and a teal tick
            Gfx.PlateEx(this, r, 9 * k, false, true, false, false,
                new Color(UiTheme.Accent, 0.10f), new Color(UiTheme.Accent, 0.28f), 1);
            Gfx.Glow(this, new Vector2(2 * k, Size.Y / 2), Size.Y * 0.7f, new Color(UiTheme.Accent, 0.10f + 0.05f * Gfx.Wave(t, 0.5f)), 6);
            DrawRect(new Rect2(0, 4 * k, 3.2f * k, Mathf.Max(0, Size.Y - 8 * k)), UiTheme.Accent);
            DrawRect(new Rect2(Size.X - 14 * k, Size.Y - 6 * k, 8 * k, 2 * k), new Color(UiTheme.Teal, 0.6f));
        }
        else if (h > 0.01f)
        {
            // grey wash that slides in from the left edge
            float wq = Size.X * Gfx.EaseOutQuart(h);
            Gfx.HGradient(this, new Rect2(0, 0, wq, Size.Y), new Color(1, 1, 1, 0.075f * h), new Color(1, 1, 1, 0.02f * h));
        }

        var col = Selected ? UiTheme.AccentHover : UiTheme.Dim.Lerp(UiTheme.Text, h);
        var iconC = new Vector2(21 * k + 2 * k * h, Size.Y / 2);
        if (Selected) DrawCircle(iconC, 9.5f * k, new Color(UiTheme.Accent, 0.08f + 0.05f * Gfx.Wave(t, 0.5f)));
        DrawIcon(this, Icon, iconC, 8.2f * k, col);
        int fs = UiTheme.Fs(13, k);
        Gfx.TextFit(this, UiTheme.BodySemi, Label, 38 * k, Gfx.Mid(Size.Y / 2, fs), fs,
            Selected ? UiTheme.Text : UiTheme.Dim.Lerp(UiTheme.Text, h), Size.X - 52 * k);
        if (Selected || h > 0.01f)
            Gfx.Chevron(this, new Vector2(Size.X - 9 * k, Size.Y / 2), 9 * k, 1,
                new Color(Selected ? UiTheme.Accent : UiTheme.TextSoft, Mathf.Max(Selected ? 0.8f : 0f, h * 0.8f)), 2 * k);
    }

    public static void DrawIcon(CanvasItem ci, NavIcon icon, Vector2 c, float r, Color col)
    {
        float w = Mathf.Max(1.2f, r * 0.18f);
        switch (icon)
        {
            case NavIcon.Crosshair:
                ci.DrawCircle(c, r * 0.62f, col, false, w, true);
                float gap = r * 0.95f, len = r * 0.42f;
                ci.DrawLine(c + new Vector2(-gap - len, 0), c + new Vector2(-gap, 0), col, w, true);
                ci.DrawLine(c + new Vector2(gap, 0), c + new Vector2(gap + len, 0), col, w, true);
                ci.DrawLine(c + new Vector2(0, -gap - len), c + new Vector2(0, -gap), col, w, true);
                ci.DrawLine(c + new Vector2(0, gap), c + new Vector2(0, gap + len), col, w, true);
                ci.DrawCircle(c, w * 0.9f, col, true, -1, true);
                break;
            case NavIcon.Agents:
                ci.DrawCircle(c + new Vector2(0, -r * 0.35f), r * 0.36f, col, false, w, true);
                ci.DrawArc(c + new Vector2(0, r * 0.72f), r * 0.72f, Mathf.Pi, Mathf.Tau, 20, col, w, true);
                break;
            case NavIcon.Coach:
                ci.DrawCircle(c, r * 0.18f, col, true, -1, true);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.Pi / 2f + Mathf.Pi / 4f;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    ci.DrawLine(c + d * (r * 0.45f), c + d * (r * 0.95f), col, w, true);
                }
                ci.DrawArc(c, r * 0.72f, 0.35f, 1.25f, 8, col, w, true);
                break;
            case NavIcon.Stats:
                ci.DrawLine(c + new Vector2(-r * 0.55f, r * 0.55f), c + new Vector2(-r * 0.55f, r * 0.10f), col, w, true);
                ci.DrawLine(c, c + new Vector2(0, r * 0.55f), col, w, true);
                ci.DrawLine(c + new Vector2(r * 0.55f, r * 0.55f), c + new Vector2(r * 0.55f, -r * 0.35f), col, w, true);
                ci.DrawLine(c + new Vector2(-r * 0.8f, r * 0.75f), c + new Vector2(r * 0.8f, r * 0.75f), col, w, true);
                break;
            case NavIcon.Settings:
                for (int i = -1; i <= 1; i++)
                {
                    float y = i * r * 0.52f;
                    ci.DrawLine(c + new Vector2(-r * 0.9f, y), c + new Vector2(r * 0.9f, y), col, w, true);
                    ci.DrawCircle(c + new Vector2(i * r * 0.42f, y), r * 0.20f, col, true, -1, true);
                }
                break;
            case NavIcon.Power:
                ci.DrawArc(c, r * 0.72f, -Mathf.Pi / 2f + 0.62f, -Mathf.Pi / 2f - 0.62f + Mathf.Tau, 28, col, w, true);
                ci.DrawLine(c + new Vector2(0, -r * 1.02f), c + new Vector2(0, r * 0.1f), col, w, true);
                break;
        }
    }
}

/// <summary>
/// Decorative target in the hero: concentric rings, a slowly rotating dashed ring, tick marks and a breathing core.
/// </summary>
public partial class HeroTarget : Control
{
    public float K = 1f;
    float ang, pulse;

    public HeroTarget() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Process(double delta)
    {
        ang += (float)delta * 0.18f;
        pulse += (float)delta * 1.1f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float k = K;
        var c = Size / 2;
        float R = Mathf.Min(Size.X, Size.Y) * 0.5f - 4 * k;
        if (R < 8) return;

        Gfx.Glow(this, c, R * 1.12f, new Color(UiTheme.Accent, 0.07f), 7);
        DrawArc(c, R, 0, Mathf.Tau, 96, new Color(1, 1, 1, 0.13f), 1.2f * k, true);
        DrawArc(c, R * 0.78f, 0, Mathf.Tau, 84, new Color(1, 1, 1, 0.06f), 1f * k, true);
        DrawArc(c, R * 0.46f, 0, Mathf.Tau, 64, new Color(1, 1, 1, 0.05f), 1f * k, true);

        for (int i = 0; i < 40; i++)
        {
            float a0 = ang + i * Mathf.Tau / 40f;
            DrawArc(c, R * 0.90f, a0, a0 + Mathf.Tau / 110f, 4, new Color(UiTheme.Accent, 0.55f), 1.8f * k, true);
        }
        DrawArc(c, R * 0.64f, -ang * 1.7f, -ang * 1.7f + 0.55f, 18, new Color(UiTheme.Accent, 0.8f), 2.4f * k, true);

        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Pi / 2f + Mathf.Pi / 4f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(c + dir * (R * 0.68f), c + dir * (R * 0.82f), new Color(1, 1, 1, 0.18f), 1.3f * k, true);
        }

        var cross = new Color(1, 1, 1, 0.22f);
        float g = 6 * k, l = 12 * k;
        DrawLine(c + new Vector2(-g - l, 0), c + new Vector2(-g, 0), cross, 1.2f * k, true);
        DrawLine(c + new Vector2(g, 0), c + new Vector2(g + l, 0), cross, 1.2f * k, true);
        DrawLine(c + new Vector2(0, -g - l), c + new Vector2(0, -g), cross, 1.2f * k, true);
        DrawLine(c + new Vector2(0, g), c + new Vector2(0, g + l), cross, 1.2f * k, true);

        float pr = (2.6f + 1.4f * (0.5f + 0.5f * Mathf.Sin(pulse))) * k;
        DrawCircle(c, pr, new Color(UiTheme.Accent, 0.92f), true, -1, true);
        DrawArc(c, R * (0.30f + 0.012f * Mathf.Sin(pulse)), 0, Mathf.Tau, 48,
            new Color(UiTheme.Accent, 0.16f), 1.2f * k, true);
    }
}

/// <summary>Rail entry: uppercase name, drill count in a small cut-corner chip, accent diamond and wash when selected.</summary>
public partial class CategoryButton : BaseButton
{
    public string Label = "";
    public int Count;
    public float K = 1f;
    public bool Selected;
    float hover, t = 1f;

    public CategoryButton()
    {
        FocusMode = FocusModeEnum.None;
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }

    public override void _Ready()
    {
        MouseEntered += UiTheme.HoverSound;
        Pressed += UiTheme.ClickSound;
    }

    public override void _Process(double delta)
    {
        t += (float)delta;
        float target = IsHovered() ? 1f : 0f;
        if (hover != target || Selected) { hover = Mathf.MoveToward(hover, target, (float)delta * 10f); QueueRedraw(); }
    }

    public override void _Draw()
    {
        float k = K, h = hover;
        var r = new Rect2(Vector2.Zero, Size);
        if (Selected)
        {
            Gfx.PlateEx(this, r, 7 * k, true, false, false, true,
                new Color(UiTheme.AccentSoft, 0.95f), new Color(UiTheme.Accent, 0.4f), 1);
            Gfx.PlateEx(this, r, 7 * k, true, false, false, true, default, new Color(1, 1, 1, 0.06f), 1);
            Gfx.Diamond(this, new Vector2(10 * k, Size.Y / 2), (3.2f + 0.5f * Gfx.Wave(t, 0.55f)) * k, (3.2f + 0.5f * Gfx.Wave(t, 0.55f)) * k, UiTheme.Accent);
        }
        else if (h > 0.01f)
        {
            float wq = Size.X * Gfx.EaseOutQuart(h);
            Gfx.HGradient(this, new Rect2(0, 0, wq, Size.Y), new Color(1, 1, 1, 0.065f * h), new Color(1, 1, 1, 0.015f * h));
        }

        float cy = Size.Y / 2;
        int fs = UiTheme.Fs(13, k), cs = UiTheme.Fs(11, k);
        var col = Selected ? UiTheme.Text : UiTheme.Dim.Lerp(UiTheme.Text, h);
        int tx = Selected ? 19 : 12;
        Gfx.TextFit(this, UiTheme.HudWide, Label, tx * k, Gfx.Mid(cy, fs), fs, col, Size.X - 48 * k);
        // count chip
        string count = Count.ToString();
        if (Selected)
        {
            float cw = Gfx.TextW(UiTheme.HudWide, count, cs) + 10 * k;
            var cr = new Rect2(Size.X - 12 * k - cw, cy - 8.5f * k, cw, 17 * k);
            Gfx.PlateEx(this, cr, 4 * k, false, true, true, false, new Color(UiTheme.Accent, 0.16f), new Color(UiTheme.Accent, 0.5f), 1);
            Gfx.TextC(this, UiTheme.HudWide, count, cr.GetCenter().X, Gfx.Mid(cy, cs), cs, UiTheme.AccentHover);
        }
        else Gfx.TextR(this, UiTheme.Body, count, Size.X - 12 * k, Gfx.Mid(cy, cs), cs,
            UiTheme.Faint.Lerp(UiTheme.Dim, h));
    }
}

/// <summary>
/// One drill card: name (+ a NEW / SIGNATURE tag, and COMMUNITY for drills made by a contributor), PB at the current tier and best badge ever on the first line, the
/// description (one or two lines; the tooltip has all of it) below. The Agents screen overrides the title, the second
/// line ("Uses Curveball (Q)") and the accent colour.
/// </summary>
public partial class ModeCard : BaseButton
{
    readonly TrainingMode info;
    public float K = 1f;
    /// <summary>Two description lines instead of one.</summary>
    public bool TwoLines;
    /// <summary>Added in this version and never played: shows a NEW tag.</summary>
    public bool IsNew;
    /// <summary>Card title instead of the drill's name.</summary>
    public string? Title;
    /// <summary>Second line instead of the description.</summary>
    public string? Sub;
    /// <summary>Colour of the left bar, the tag and <see cref="Sub"/>; null = the category colour.</summary>
    public Color? Tint;
    /// <summary>Tag after the name (overrides NEW).</summary>
    public string? Tag;
    /// <summary>Fade/slide the card in when the grid is (re)built.</summary>
    public bool AnimateIn;
    public float RevealDelay;
    float hover;
    float reveal = 1f;
    int pb = -1, badge = -1;
    string pbText = "—";

    public ModeCard(TrainingMode info)
    {
        this.info = info;
        FocusMode = FocusModeEnum.None;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = $"{Loc.T(info.Name)} — {Loc.T(info.Description)}" + (info.MadeBy is { } by ? $"\nMade by the community: @{by}" : "");
    }

    public string Key => info.Key;

    public override void _Ready()
    {
        MouseEntered += UiTheme.HoverSound;
        Pressed += UiTheme.ClickSound;
        Refresh();
        if (AnimateIn)
        {
            reveal = 0f;
            Modulate = new Color(1, 1, 1, 0f);
            var tw = CreateTween();
            tw.TweenInterval(RevealDelay);
            tw.TweenMethod(Callable.From<float>(v =>
            {
                reveal = v;
                Modulate = new Color(1, 1, 1, v);
                QueueRedraw();
            }), 0f, 1f, 0.38f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        }
    }

    public void Refresh()
    {
        var stats = Main.I.Stats;
        pb = stats.Best(info.Key, Main.I.Tier);
        badge = stats.BestBadge(info.Key);
        pbText = pb >= 0 ? pb.ToString("#,0", CultureInfo.InvariantCulture) : "—";
        QueueRedraw();
    }

    public static Color CategoryColor(string cat) => cat switch
    {
        "Aim" => UiTheme.Accent,
        "Valorant" => UiTheme.Teal,
        "Movement" => UiTheme.Warn,
        "Map" => Color.Color8(110, 170, 255),
        "Utility" => Color.Color8(186, 120, 255),
        "Agents" => Color.Color8(255, 150, 100),
        _ => UiTheme.Dim,
    };

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        t += dt;
        float target = IsHovered() ? 1f : 0f;
        bool down = GetDrawMode() is DrawMode.Pressed or DrawMode.HoverPressed;
        if (hover != target) { hover = Mathf.MoveToward(hover, target, dt * 8f); QueueRedraw(); }
        if (reveal < 1f || hover > 0.001f || (IsNew && Tag == null && t > 0.2f)) QueueRedraw();
        var want = Vector2.One * (1f + 0.012f * hover - (down ? 0.008f : 0f));
        if (Scale.DistanceTo(want) > 0.0005f)
        {
            PivotOffset = Size / 2;
            Scale = Scale.Lerp(want, Mathf.Min(1f, dt * 12f));
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        float k = K;
        DrawSetTransform(new Vector2(0, (1f - reveal) * 18 * k), 0f, Vector2.One);
        var r = new Rect2(Vector2.Zero, Size);
        bool down = GetDrawMode() is DrawMode.Pressed or DrawMode.HoverPressed;
        var cc = Tint ?? CategoryColor(info.Category);
        var fill = UiTheme.Card.Lerp(UiTheme.CardHover, hover);
        if (down) fill = UiTheme.CardHover.Lightened(0.06f);

        // hovered glow under the card + angular plate (top-right corner cut)
        if (hover > 0.02f)
            Gfx.Glow(this, r.GetCenter(), Mathf.Max(r.Size.X, r.Size.Y) * (0.55f + 0.2f * hover),
                new Color(cc, 0.05f * hover), 7);
        Gfx.PlateEx(this, r, 12 * k, false, true, false, false, new Color(fill, 0.97f),
            new Color(UiTheme.LineStrong.Lerp(cc, hover * 0.55f)), 1);
        // category-coloured top edge that lights up with the pointer
        Gfx.HGradient(this, new Rect2(16 * k, 0, Mathf.Max(0, r.Size.X - 40 * k), 1.6f * k),
            new Color(cc, 0.35f + 0.5f * hover), new Color(cc, 0.05f));
        // hover wash sweeping in from the left
        if (hover > 0.01f)
            Gfx.PlateEx(this, new Rect2(0, 0, r.Size.X * Gfx.EaseOutQuart(hover) * 0.96f, r.Size.Y), 0, false, false, false, false,
                new Color(cc, 0.055f * hover), default);
        // left spine
        DrawRect(new Rect2(1.5f * k, 16 * k, 3 * k, Mathf.Max(0, r.Size.Y - 32 * k)), new Color(cc, 0.55f + 0.45f * hover));

        float pad = 18 * k + 2.5f * k * hover, cy = TwoLines ? 26 * k : 21 * k, right = r.Size.X - 16 * k;
        // best badge ever (top right); none for drills without a meaningful score (Sens Finder)
        if (info.ShowScore)
        {
            float er = 8 * k;
            RankEmblem.Draw(this, new Vector2(right - er, cy), er, badge, badge >= 0 ? 1f : 0.42f);
            right -= er * 2 + 10 * k;
        }

        // name + tag (the name gets the whole first line, so it's never cut)
        string? tag = Tag ?? (IsNew ? "NEW" : null);
        string? community = Tag == null && info.MadeBy != null ? "COMMUNITY" : null;   // after NEW, outlined
        int ns = UiTheme.Fs(TwoLines ? 21 : 19, k), ts = UiTheme.Fs(9.5f, k);
        float tagW = tag != null ? Gfx.TextW(UiTheme.HudWide, tag, ts) + 12 * k : 0;
        float comW = community != null ? Gfx.TextW(UiTheme.HudWide, community, ts) + 12 * k : 0;
        string name = Loc.T(Title ?? info.Name);
        // Narrow card: the name stays whole, and COMMUNITY outranks NEW.
        if (community != null && tag != null && Gfx.TextW(UiTheme.Display, name, ns) > right - pad - tagW - comW - 14 * k) { tag = null; tagW = 0; }
        float nameMax = Mathf.Max(20 * k, right - pad - (tag != null ? tagW + 8 * k : 0) - (community != null ? comW + 6 * k : 0));
        float nw = Mathf.Min(Gfx.TextW(UiTheme.Display, name, ns), nameMax);
        Gfx.TextFit(this, UiTheme.Display, name, pad, Gfx.Mid(cy, ns), ns, UiTheme.Text, nameMax);
        if (tag != null)
        {
            var tc = Tag != null ? cc : UiTheme.Accent;
            var tr = new Rect2(pad + nw + 8 * k, cy - 9 * k, tagW, 18 * k);
            float pw = Tag == null ? Gfx.Wave(t, 0.9f) : 0f; // NEW: soft breathing halo
            if (pw > 0) Gfx.PlateEx(this, tr.Grow(2.5f * k * pw), 7 * k, true, true, true, true, new Color(tc, 0.10f * pw), default);
            Gfx.PlateEx(this, tr, 5 * k, true, false, false, true, new Color(tc, 0.14f), new Color(tc, 0.55f + 0.3f * pw), 1);
            Gfx.Text(this, UiTheme.HudWide, tag, tr.Position.X + 6 * k, Gfx.Mid(cy, ts), ts, tc);
        }
        if (community != null)
        {
            var cr = new Rect2(pad + nw + 8 * k + (tag != null ? tagW + 6 * k : 0), cy - 9 * k, comW, 18 * k);
            Gfx.PlateEx(this, cr, 5 * k, true, false, false, true, new Color(UiTheme.Good, 0.10f), new Color(UiTheme.Good, 0.5f), 1);
            Gfx.Text(this, UiTheme.HudWide, community, cr.Position.X + 6 * k, Gfx.Mid(cy, ts), ts, UiTheme.Good);
        }

        string desc = Loc.T(Sub ?? info.Description);
        var dc = Sub != null && Tint is { } tint ? tint : UiTheme.Dim;
        int ds = UiTheme.Fs(14, k);
        if (TwoLines)
        {
            DrawMultilineString(UiTheme.Body, new Vector2(pad, 48 * k + UiTheme.Body.GetAscent(ds)), desc,
                HorizontalAlignment.Left, r.Size.X - pad * 2, ds, 2, new Color(dc, dc.A * (1f - 0.2f * hover)));
            // PB chip (bottom right), separated from the description
            if (info.ShowScore && pb >= 0)
            {
                int ps = UiTheme.Fs(15, k), pl = UiTheme.Fs(9.5f, k);
                float numW = Gfx.TextW(UiTheme.Display, pbText, ps);
                float labW = Gfx.TextW(UiTheme.HudWide, "PB", pl);
                float chipW = numW + labW + 26 * k, chipH = 24 * k;
                var chip = new Rect2(r.Size.X - 16 * k - chipW, r.Size.Y - 14 * k - chipH, chipW, chipH);
                Gfx.PlateEx(this, chip, 6 * k, false, true, true, false,
                    new Color(1, 1, 1, 0.035f + 0.025f * hover), UiTheme.Line.Lerp(UiTheme.Good, hover * 0.8f), 1);                Gfx.TextR(this, UiTheme.Display, pbText, chip.End.X - 10 * k, Gfx.Mid(chip.GetCenter().Y, ps), ps,
                    UiTheme.Good.Lerp(Colors.White, hover * 0.5f));
                Gfx.Text(this, UiTheme.HudWide, "PB", chip.Position.X + 10 * k, Gfx.Mid(chip.GetCenter().Y, pl), pl, UiTheme.Faint);
            }
            // hover hint: a right chevron slides in at the bottom-left
            if (hover > 0.02f)
            {
                float hx = pad + (10 * k) * Gfx.EaseOutQuart(hover);
                Gfx.Chevron(this, new Vector2(hx, r.Size.Y - 10 * k), 7 * k, 1, new Color(cc, hover * 0.9f), 1.8f * k);
                Gfx.Chevron(this, new Vector2(hx + 6 * k, r.Size.Y - 10 * k), 7 * k, 1, new Color(cc, hover * 0.45f), 1.8f * k);
            }
        }
        else
        {
            float lineY = 48 * k, descRight = r.Size.X - pad;
            if (info.ShowScore && pb >= 0)
            {
                int vs = UiTheme.Fs(16, k), ls = UiTheme.Fs(9.5f, k);
                float x = r.Size.X - 16 * k;
                x -= Gfx.TextR(this, UiTheme.Display, pbText, x, Gfx.Mid(lineY, vs), vs, UiTheme.Good) + 6 * k;
                x -= Gfx.TextR(this, UiTheme.HudWide, "PB", x, Gfx.Mid(lineY, ls), ls, UiTheme.Faint);
                descRight = x - 12 * k;
            }
            Gfx.TextFit(this, UiTheme.Body, desc, pad, Gfx.Mid(lineY, ds), ds, dc, descRight - pad);
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    float t;
}

/// <summary>
/// Compact map control: "▪ MAP  ‹ [▦ ASCENT ▾] ›". The arrows step through the maps, the name opens a grid of every map
/// plus RANDOM (<see cref="MapPickerOverlay"/>). The choice is saved to <see cref="AppSettings.MapKey"/> (map, utility and
/// agent drills read <see cref="Main.MapKey"/>).
/// </summary>
public partial class MapSelector : HBoxContainer
{
    readonly Control host;
    readonly float k;
    readonly MapButton current;
    MapPickerOverlay? overlay;
    /// <summary>Called after the map changes.</summary>
    public Action? Changed;

    /// <summary>RANDOM, then every map once (MapSpots.All order).</summary>
    public static List<(string Key, string Name)> Options()
    {
        var list = new List<(string Key, string Name)> { ("random", "RANDOM") };
        list.AddRange(MapSpots.All.Select(m => (m.Key, m.Map.ToUpperInvariant())).DistinctBy(m => m.Key));
        return list;
    }

    public static string NameOf(string key) =>
        Options().FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase)).Name ?? key.ToUpperInvariant();

    /// <summary>True when the extracted art GLBs for this map are available next to the app (see <see cref="RealMap.HasArt"/>).</summary>
    public static bool HasArt(string key) =>
        key != "random" && MapSpots.All.FirstOrDefault(m => m.Key == key) is { } m && RealMap.HasArt(m.RealMapFolder);

    /// <summary>True when every level of the map's manifest was extracted (no empty or missing GLB).</summary>
    public static bool FullArt(string key) =>
        key != "random" && MapSpots.All.FirstOrDefault(m => m.Key == key) is { } m && RealMap.IsComplete(m.RealMapFolder);

    static int MapCount => MapSpots.All.Select(m => m.Key).Distinct().Count();
    public static int ArtCount => MapSpots.All.Select(m => m.Key).Distinct().Count(HasArt);

    /// <summary>Short art-status tag for the selector button and the map tiles.</summary>
    public static string ArtLabel(string key) => key == "random" ? $"{MapCount} MAPS" :
        !HasArt(key) ? "BLOCKOUT" : FullArt(key) ? "REAL ART" : "PARTIAL ART";

    /// <summary>Tag colour: teal for complete art, amber for a partial extraction, grey for a blockout.</summary>
    public static Color ArtColor(string key) => key == "random" ? UiTheme.Dim :
        !HasArt(key) ? UiTheme.Faint : FullArt(key) ? UiTheme.Teal : UiTheme.Warn;

    /// <param name="host">The screen the pop-up is added to (drawn on top of everything).</param>
    public MapSelector(Control host, float k, bool label = true)
    {
        this.host = host;
        this.k = k;
        AddThemeConstantOverride("separation", (int)(6 * k));
        if (label) AddChild(new DrawBox { CustomMinimumSize = new Vector2(56 * k, 40 * k), OnDraw = DrawLabel });
        var prev = new VButton { Kind = VButton.Look.Arrow, Dir = -1, K = k, CustomMinimumSize = new Vector2(40 * k, 40 * k), TooltipText = "Previous map" };
        prev.Pressed += () => Step(-1);
        AddChild(prev);
        current = new MapButton { K = k, CustomMinimumSize = new Vector2(196 * k, 40 * k), TooltipText = "Map for the map, utility and agent drills · click to see every map" };
        current.Pressed += () => { if (IsOpen) Close(); else Open(); };
        AddChild(current);
        var next = new VButton { Kind = VButton.Look.Arrow, Dir = 1, K = k, CustomMinimumSize = new Vector2(40 * k, 40 * k), TooltipText = "Next map" };
        next.Pressed += () => Step(1);
        AddChild(next);
    }

    void DrawLabel(DrawBox d)
    {
        float cy = d.Size.Y / 2;
        int fs = UiTheme.Fs(14, k);
        Gfx.Diamond(d, new Vector2(4 * k, cy), 4.5f * k, 4.5f * k, UiTheme.Accent);
        Gfx.Text(d, UiTheme.HudWide, "MAP", 14 * k, Gfx.Mid(cy, fs), fs, UiTheme.Dim);
    }

    public bool IsOpen => overlay != null && IsInstanceValid(overlay) && overlay.IsInsideTree();

    void Step(int dir)
    {
        var opts = Options();
        int i = opts.FindIndex(o => string.Equals(o.Key, Main.I.MapKey, StringComparison.OrdinalIgnoreCase));
        if (i < 0) i = 0;
        Select(opts[(i + dir + opts.Count) % opts.Count].Key);
    }

    public void Select(string key)
    {
        Main.I.Settings.MapKey = key;
        Main.I.Settings.Save();
        current.QueueRedraw();
        Changed?.Invoke();
    }

    public void Open()
    {
        if (IsOpen || !IsInsideTree()) return;
        overlay = new MapPickerOverlay(k, current.GetGlobalRect(), Main.I.MapKey, key => { Select(key); Close(); }, Close);
        host.AddChild(overlay);
        current.Open = true;
        current.QueueRedraw();
    }

    public void Close()
    {
        if (overlay != null && IsInstanceValid(overlay)) overlay.QueueFree();
        overlay = null;
        if (IsInstanceValid(current)) { current.Open = false; current.QueueRedraw(); }
    }

    public override void _ExitTree() => Close();
}

/// <summary>The selector's middle button: a small map postcard, the map name and a ▾.</summary>
public partial class MapButton : BaseButton
{
    public float K = 1f;
    public bool Open;
    float hover;

    public MapButton()
    {
        FocusMode = FocusModeEnum.None;
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }

    public override void _Ready()
    {
        MouseEntered += UiTheme.HoverSound;
        Pressed += UiTheme.ClickSound;
    }

    public override void _Process(double delta)
    {
        float t = IsHovered() || Open ? 1f : 0f;
        if (hover != t) { hover = Mathf.MoveToward(hover, t, (float)delta * 8f); QueueRedraw(); }
    }

    public override void _Draw()
    {
        float k = K, h = hover;
        var r = new Rect2(Vector2.Zero, Size);
        string key = Main.I.MapKey;
        // angular selector button with a cut top-right corner
        Gfx.PlateEx(this, r, 8 * k, true, false, false, true, new Color(1, 1, 1, (0.045f + 0.05f * h)),
            UiTheme.Fade(UiTheme.LineStrong.Lerp(UiTheme.Accent, h * 0.6f), 1f), 1);
        if (Open) Gfx.PlateEx(this, r, 8 * k, true, false, false, true, default, new Color(UiTheme.Accent, 0.9f), 1);
        var sw = new Rect2(6 * k, 6 * k, 36 * k, r.Size.Y - 12 * k);
        MapSwatch.Draw(this, sw, key, k);
        DrawRect(sw, new Color(0, 0, 0, 0.3f), false, 1f);
        Brackets(sw);
        int fs = UiTheme.Fs(16, k);
        Gfx.TextFit(this, UiTheme.Display, MapSelector.NameOf(key), 52 * k, 18 * k, fs, UiTheme.Text, r.Size.X - 52 * k - 30 * k);
        int ss = UiTheme.Fs(7.8f, k);
        var tagCol = MapSelector.ArtColor(key);
        Gfx.TextFit(this, UiTheme.HudWide, MapSelector.ArtLabel(key), 52 * k, r.Size.Y - 9 * k, ss, tagCol, r.Size.X - 52 * k - 26 * k);
        Gfx.ChevronDown(this, new Vector2(r.Size.X - 16 * k, r.Size.Y / 2), 11 * k, 6 * k, 2.2f * k,
            new Color(UiTheme.TextSoft.Lerp(UiTheme.AccentHover, Open ? 1f : h * 0.6f), 0.7f + 0.3f * h));
    }

    void Brackets(Rect2 r)
    {
        float k = K, len = 7 * k, w = Mathf.Max(1f, 1.4f * k);
        var c = new Color(1, 1, 1, 0.55f + 0.3f * hover);
        Gfx.Brackets(this, r, len, c, w);
    }
}

/// <summary>Map pop-up: dims the screen and shows every map (+ RANDOM) as a grid of postcards; click outside or Esc closes.</summary>
public partial class MapPickerOverlay : Control
{
    readonly float k;
    readonly Rect2 anchor;
    readonly string current;
    readonly Action<string> pick;
    readonly Action close;
    Rect2 panel;
    float headH;
    bool pickerLogged;

    public MapPickerOverlay(float k, Rect2 anchor, string current, Action<string> pick, Action close)
    {
        this.k = k;
        this.anchor = anchor;
        this.current = current;
        this.pick = pick;
        this.close = close;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var opts = MapSelector.Options();
        var vp = GetViewportRect().Size;
        float tw = 156 * k, th = 90 * k, gap = 9 * k, pad = 18 * k;
        headH = 50 * k; 
        int cols = Math.Min(7, opts.Count);
        float W(int c) => pad * 2 + c * tw + (c - 1) * gap;
        while (cols > 3 && W(cols) > vp.X - 16 * k) cols--;
        int rows = (opts.Count + cols - 1) / cols;
        float w = W(cols), h = pad + headH + rows * th + (rows - 1) * gap + pad;
        float x = Mathf.Clamp(anchor.End.X - w, 8 * k, Mathf.Max(8 * k, vp.X - w - 8 * k));
        float y = anchor.End.Y + 8 * k;
        if (y + h > vp.Y - 8 * k) y = Mathf.Max(8 * k, anchor.Position.Y - h - 8 * k);
        panel = new Rect2(x, y, w, h);
        if (Main.I.Dev) GD.Print($"[picker] vp={vp} k={k} cols={cols} tw={tw} th={th} panel={panel} anchor={anchor}");
        for (int i = 0; i < opts.Count; i++)
        {
            var (key, name) = opts[i];
            var t = new MapTile(key, name)
            {
                K = k, Selected = string.Equals(key, current, StringComparison.OrdinalIgnoreCase),
                Position = new Vector2(x + pad + (i % cols) * (tw + gap), y + pad + headH + (i / cols) * (th + gap)),
                Size = new Vector2(tw, th),
            };
            string kk = key;
            t.Pressed += () => pick(kk);
            if (Main.I.Dev && (i == 0 || i == 7)) GD.Print($"[picker] tile {i} {key} pos={t.Position} size={t.Size} min={t.CustomMinimumSize}");
            AddChild(t);
        }
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true } mb && !panel.HasPoint(mb.Position))
        {
            AcceptEvent();
            close();
        }
    }

    public override void _Draw()
    {
        if (Main.I.Dev && !pickerLogged) { pickerLogged = true; GD.Print($"[picker] draw size={Size} panel={panel}"); }
        // dim + hatch, then the angular library panel
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.02f, 0.04f, 0.06f, 0.62f));
        Gfx.PlateEx(this, panel, 20 * k, false, true, true, false, new Color(0.055f, 0.075f, 0.098f, 0.99f),
            new Color(0, 0, 0, 0.5f), 1.6f);
        Gfx.PlateEx(this, panel, 20 * k, false, true, true, false, default, new Color(1, 1, 1, 0.12f), 1);
        Gfx.Brackets(this, new Rect2(panel.Position + new Vector2(6 * k, 6 * k), panel.Size - new Vector2(12 * k, 12 * k)),
            16 * k, new Color(UiTheme.Accent, 0.35f), Mathf.Max(1, 1.6f * k));
        Gfx.HGradient(this, new Rect2(panel.Position.X, panel.Position.Y, panel.Size.X, 2), new Color(UiTheme.Teal, 0.6f), new Color(UiTheme.Accent, 0.25f));
        float pad = 18 * k, cy = panel.Position.Y + pad + 14 * k;
        int fs = UiTheme.Fs(18, k), ns = UiTheme.Fs(10, k);
        Gfx.Diamond(this, new Vector2(panel.Position.X + pad + 4 * k, cy), 5 * k, 5 * k, UiTheme.Accent);
        Gfx.Text(this, UiTheme.DisplayWide, "MAP LIBRARY", panel.Position.X + pad + 14 * k, Gfx.Mid(cy, fs), fs, UiTheme.Text);
        int art = MapSelector.ArtCount, total = MapSpots.All.Select(m => m.Key).Distinct().Count();
        string rest = $" · {total - art} BLOCKOUT · RANDOM PICKS A NEW ONE EVERY RUN";
        float restW = Gfx.TextW(UiTheme.HudWide, rest, ns);
        float right = panel.End.X - pad;
        Gfx.TextR(this, UiTheme.HudWide, rest, right, Gfx.Mid(cy, ns), ns, UiTheme.Faint);
        Gfx.TextR(this, UiTheme.HudWide, $"{art} REAL ART", right - restW, Gfx.Mid(cy, ns), ns, UiTheme.Teal);
        DrawRect(new Rect2(panel.Position.X + pad, panel.Position.Y + pad + headH - 14 * k, panel.Size.X - pad * 2, 1), UiTheme.Line);
    }

    /// <summary>Keeps the nullable bool off the draw path (dev-only print guard).</summary>
    void DebugDrawNullable() { }
}

/// <summary>A map in the pop-up: postcard in the map's palette, its name, selected = light frame + red underline.</summary>
public partial class MapTile : BaseButton
{
    readonly string key, name;
    public float K = 1f;
    public bool Selected;
    float hover;

    public MapTile(string key, string name)
    {
        this.key = key;
        this.name = name;
        FocusMode = FocusModeEnum.None;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = key == "random"
            ? Loc.T("Map drills pick a random map each run")
            : $"{Loc.T("Map drills use")} {name[..1]}{name[1..].ToLowerInvariant()}";
    }

    public override void _Ready()
    {
        MouseEntered += UiTheme.HoverSound;
        Pressed += UiTheme.ClickSound;
    }

    public override void _Process(double delta)
    {
        float t = IsHovered() ? 1f : 0f;
        if (hover != t) { hover = Mathf.MoveToward(hover, t, (float)delta * 8f); QueueRedraw(); }
    }

    public override void _Draw()
    {
        float k = K, h = hover;
        var r = new Rect2(Vector2.Zero, Size);
        MapSwatch.Draw(this, r, key, k);
        Gfx.VGradient(this, new Rect2(0, r.Size.Y * 0.40f, r.Size.X, r.Size.Y * 0.60f), new Color(0.03f, 0.05f, 0.07f, 0f), new Color(0.03f, 0.05f, 0.07f, 0.88f));
        if (h > 0.01f)
        {
            // light wash sweeping up from the name
            Gfx.PlateEx(this, new Rect2(0, r.Size.Y * (1f - 0.62f * Gfx.EaseOutQuart(h)), r.Size.X, r.Size.Y), 0,
                false, false, false, false, new Color(1, 1, 1, 0.07f * h), default);
        }
        // art status chip, top-left (angular)
        string tag = MapSelector.ArtLabel(key);
        var tagCol = MapSelector.ArtColor(key);
        int ps = UiTheme.Fs(8.2f, k);
        float pw = Gfx.TextW(UiTheme.HudWide, tag, ps) + 14 * k, ph = 15 * k;
        var pr = new Rect2(8 * k, 8 * k, pw, ph);
        Gfx.PlateEx(this, pr, 4 * k, true, false, false, true, new Color(0.02f, 0.04f, 0.055f, 0.74f),
            new Color(tagCol, 0.5f), 1);
        Gfx.TextC(this, UiTheme.HudWide, tag, pr.GetCenter().X, pr.Position.Y + 10.4f * k, ps, tagCol);
        int fs = UiTheme.Fs(18, k);
        Gfx.TextFit(this, UiTheme.Display, name, 10 * k, r.Size.Y - 10 * k, fs, Selected ? UiTheme.Text : UiTheme.TextSoft, r.Size.X - 20 * k);
        if (Selected)
        {
            // red frame with corner marks plus a glowing top edge
            DrawRect(r, UiTheme.Accent, false, Mathf.Max(2, 2 * k));
            Gfx.Brackets(this, r, 14 * k, new Color(1, 1, 1, 0.75f), Mathf.Max(1.4f, 2 * k));
            Gfx.HGradient(this, new Rect2(0, 0, r.Size.X, 2.4f * k), new Color(UiTheme.Accent, 0.4f), new Color(UiTheme.Accent, 1f));
        }
        else DrawRect(r, new Color(1, 1, 1, 0.10f + 0.35f * h), false, 1f);
    }
}

/// <summary>Little "postcard" of a map drawn from its palette (sky, walls, trim, floor, a crate); RANDOM is a strip of several.</summary>
public static class MapSwatch
{
    static int Seed(string s)
    {
        int h = 17;
        foreach (char c in s) h = h * 31 + c;
        return h & 0x7fffffff;
    }

    public static void Draw(CanvasItem ci, Rect2 r, string key, float k)
    {
        if (key == "random")
        {
            var maps = MapSpots.All;
            int n = Math.Min(5, maps.Length);
            for (int i = 0; i < n; i++)
            {
                var m = maps[(i * 3 + 1) % maps.Length];
                var part = new Rect2(r.Position.X + r.Size.X * i / n, r.Position.Y, r.Size.X / n + 0.5f, r.Size.Y);
                if (!MapArt.DrawRegion(ci, part, m.Key, new Color(1f, 1f, 1f, 0.98f), 2.8f))
                    Scene(ci, part, m.Palette, Seed(m.Key));
            }
            ci.DrawRect(r, new Color(0.05f, 0.08f, 0.11f, 0.38f));
            int fs = Math.Max(10, (int)(r.Size.Y * 0.62f));
            Gfx.TextC(ci, UiTheme.Display, "?", r.GetCenter().X, Gfx.Mid(r.Position.Y + r.Size.Y * 0.42f, fs), fs, new Color(UiTheme.Text, 0.9f));
            return;
        }
        if (MapArt.DrawRegion(ci, r, key, new Color(1f, 1f, 1f, 0.98f), 2.6f))
        {
            ci.DrawRect(r, new Color(0.03f, 0.06f, 0.1f, 0.14f));
            ci.DrawRect(r, new Color(UiTheme.Teal, 0.07f));
            return;
        }
        var map = MapSpots.All.FirstOrDefault(m => m.Key == key) ?? MapSpots.Ascent;
        Scene(ci, r, map.Palette, Seed(map.Key));
    }

    static void Scene(CanvasItem ci, Rect2 r, MapPalette p, int seed)
    {
        float x0 = r.Position.X, y0 = r.Position.Y, w = r.Size.X, h = r.Size.Y;
        float horizon = y0 + h * 0.62f;
        Gfx.VGradient(ci, new Rect2(x0, y0, w, horizon - y0), p.Sky.Lightened(0.12f), p.Sky.Darkened(0.08f));
        // two building blocks of the wall colour (heights vary per map), a darker one behind
        var rng = new Random(seed);
        float bx = x0 - w * 0.1f;
        while (bx < x0 + w)
        {
            float bw = w * (0.22f + (float)rng.NextDouble() * 0.3f);
            float top = y0 + h * (0.12f + (float)rng.NextDouble() * 0.3f);
            var col = rng.Next(3) == 0 ? p.Elevated : p.Wall;
            float l = Mathf.Max(x0, bx), rr = Mathf.Min(x0 + w, bx + bw);
            if (rr > l)
            {
                ci.DrawRect(new Rect2(l, top, rr - l, horizon - top), col.Darkened(0.12f));
                ci.DrawRect(new Rect2(l, top, rr - l, Mathf.Max(1, h * 0.05f)), p.Trim);
                ci.DrawRect(new Rect2(l, top, Mathf.Min(rr - l, Mathf.Max(1, w * 0.02f)), horizon - top), new Color(0, 0, 0, 0.18f));
            }
            bx += bw + w * (0.02f + (float)rng.NextDouble() * 0.08f);
        }
        ci.DrawRect(new Rect2(x0, horizon - Mathf.Max(1, h * 0.04f), w, Mathf.Max(1, h * 0.04f)), p.Trim.Darkened(0.2f));
        Gfx.VGradient(ci, new Rect2(x0, horizon, w, y0 + h - horizon), p.Floor, p.Floor.Darkened(0.4f));
        float cx = x0 + w * (0.55f + (float)rng.NextDouble() * 0.25f);
        float cw = w * 0.16f, ch = h * 0.16f;
        float l2 = Mathf.Max(x0, cx), r2 = Mathf.Min(x0 + w, cx + cw);
        if (r2 > l2) ci.DrawRect(new Rect2(l2, horizon - ch * 0.4f, r2 - l2, ch), p.Cover.Darkened(0.05f));
    }
}

/// <summary>Slim strip under the menu header: "VALTRAINER 1.2.0 IS AVAILABLE · you have 1.1.0" / "Downloading update 42%" /
/// "VALTRAINER 1.2.0 IS READY · restart to update" plus its buttons (children laid out by <see cref="MenuScreen"/>).</summary>
public partial class UpdateBanner : Control
{
    public enum Stage { Available, Downloading, Ready, Applying }

    public float K = 1f;
    public string Version = "";
    public Stage Kind;

    public UpdateBanner() => MouseFilter = MouseFilterEnum.Pass;

    float animT;

    public override void _Process(double delta)
    {
        animT += (float)delta; // the beacon breathes, whatever the stage
        QueueRedraw();
    }

    public override void _Draw()
    {
        float k = K, cy = Size.Y / 2;
        var r = new Rect2(Vector2.Zero, Size);
        bool ready = Kind is Stage.Ready or Stage.Applying;
        Color acc = ready ? UiTheme.Good : UiTheme.Accent;
        Gfx.PlateEx(this, r, 9 * k, true, false, false, true, new Color(acc, 0.085f), new Color(acc, 0.38f), 1);
        // left beacon: a breathing diamond in an angular housing
        var bz = new Rect2(10 * k, cy - 9 * k, 18 * k, 18 * k);
        Gfx.PlateEx(this, bz, 4 * k, true, false, false, true, new Color(acc, 0.10f), default);
        float pw = Gfx.Wave(animT, 0.9f);
        Gfx.Glow(this, bz.GetCenter(), (10 + 6 * pw) * k, new Color(acc, 0.20f + 0.16f * pw), 6);
        Gfx.Diamond(this, bz.GetCenter(), 5.5f * k, 5.5f * k, new Color(acc, 0.75f + 0.25f * pw));
        if (Kind == Stage.Downloading)
        {
            // thin progress bar along the bottom edge with a bright head
            float p = Updater.Progress;
            Gfx.HGradient(this, new Rect2(4 * k, r.Size.Y - 3 * k, (r.Size.X - 8 * k) * p, 2 * k), new Color(acc, 0.35f), new Color(acc, 0.95f));
            DrawRect(new Rect2(4 * k + (r.Size.X - 8 * k) * p - 1.4f * k, r.Size.Y - 3.6f * k, 2.8f * k, 3.2f * k), new Color(1, 1, 1, 0.85f));
        }
        int fs = UiTheme.Fs(14, k), ss = UiTheme.Fs(13, k);
        string v = Version.ToUpperInvariant();
        string head = Kind switch
        {
            Stage.Ready => $"ValTraining {v} IS READY",
            Stage.Applying => $"INSTALLING ValTraining {v}…",
            _ => $"ValTraining {v} IS AVAILABLE",
        };
        string sub = Kind switch
        {
            Stage.Downloading => $"Downloading update {Updater.Progress * 100:0}%",
            Stage.Ready => "restart to update · you have " + AppInfo.Version + (Updater.NeedsElevation ? " · Windows will ask for permission" : ""),
            Stage.Applying => "ValTraining restarts by itself",
            _ => $"you have {AppInfo.Version}",
        };
        Gfx.Text(this, UiTheme.HudWide, head, 42 * k, Gfx.Mid(cy, fs), fs, UiTheme.Text);
        Gfx.Text(this, UiTheme.HudWide, "›", 42 * k + Gfx.TextW(UiTheme.HudWide, head, fs) + 2 * k, Gfx.Mid(cy, fs), fs, new Color(acc, 0.55f));
        float x = 42 * k + Gfx.TextW(UiTheme.HudWide, head, fs) + 14 * k;
        Gfx.Text(this, UiTheme.Body, sub, x, Gfx.Mid(cy, ss), ss, Kind == Stage.Ready ? UiTheme.Good : UiTheme.Dim);
    }
}

/// <summary>The always-visible UPDATE button in the menu footer. It checks GitHub when pressed, then downloads the
/// newest release (even with automatic downloads off) and turns into RESTART TO UPDATE when the verified installer is
/// ready. If this copy can't update itself, it opens the release page instead.</summary>
public partial class UpdateNowButton : VButton
{
    int seen = -1;

    public override void _Ready()
    {
        base._Ready();
        Kind = Look.Secondary;
        FontPx = 12;
        Pressed += Act;
        Refresh(true);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (seen != UpdateCheck.Revision) Refresh(false);
    }

    void Act()
    {
        if (Updater.Status is Updater.State.Applying or Updater.State.Downloading) return;
        if (Updater.Status == Updater.State.Ready)
        {
            Updater.RestartToUpdate();
            return;
        }
        if (UpdateCheck.NewerAvailable)
        {
            if (!Updater.UpdateNow())
                AppInfo.OpenGitHub(UpdateCheck.LatestUrl ?? AppInfo.LatestReleaseUrl);
            return;
        }
        if (UpdateCheck.CanCheck) UpdateCheck.CheckNow();
        else if (AppInfo.HasRepo) AppInfo.OpenGitHub(UpdateCheck.LatestUrl ?? AppInfo.LatestReleaseUrl);
    }

    void Refresh(bool force)
    {
        if (!force && seen == UpdateCheck.Revision) return;
        seen = UpdateCheck.Revision;
        string label = "CHECK FOR UPDATES";
        var look = Look.Secondary;
        bool disabled = false;
        string tip;
        if (!AppInfo.HasRepo)
        {
            disabled = true;
            tip = "No GitHub repository is set for this build";
        }
        else if (Updater.Status == Updater.State.Applying)
        {
            label = "INSTALLING…";
            disabled = true;
            look = Look.Primary;
            tip = "ValTraining is installing the update and will restart";
        }
        else if (Updater.Status == Updater.State.Ready)
        {
            label = "RESTART TO UPDATE";
            look = Look.Primary;
            tip = Updater.NeedsElevation
                ? "Installs the update and restarts ValTraining. Windows will ask for permission"
                : "Installs the update and restarts ValTraining (settings and stats are kept)";
        }
        else if (Updater.Status == Updater.State.Downloading)
        {
            label = Loc.T("DOWNLOADING") + $" {Updater.Progress * 100:0}%";
            disabled = true;
            look = Look.Primary;
            tip = "Downloading and verifying the update…";
        }
        else if (UpdateCheck.Status == UpdateCheck.State.Checking)
        {
            label = "CHECKING…";
            disabled = true;
            tip = "Asking GitHub for the newest ValTraining release…";
        }
        else if (UpdateCheck.NewerAvailable)
        {
            bool canDownload = Updater.Status is not (Updater.State.Off or Updater.State.Failed)
                && UpdateCheck.LatestAssets is { } assets
                && (Updater.Kind == InstallKind.Portable ? assets.PortableUrl != null : assets.SetupUrl != null);
            label = canDownload ? Loc.T("UPDATE TO") + " " + UpdateCheck.Latest : Loc.T("OPEN DOWNLOAD");
            look = Look.Secondary;
            tip = canDownload
                ? $"Downloads ValTraining {UpdateCheck.Latest} and verifies it against SHA256SUMS.txt, then offers to restart"
                : "Opens the release page so you can download the installer";
        }
        else if (UpdateCheck.Status == UpdateCheck.State.UpToDate)
        {
            label = "UP TO DATE";
            tip = "You have the newest ValTraining" + (UpdateCheck.Message != null ? " · " + UpdateCheck.Message : "");
        }
        else
        {
            tip = UpdateCheck.CanCheck
                ? "Ask GitHub now whether a newer ValTraining release is out"
                : UpdateCheck.Message ?? "Updates aren't available in this build";
            if (!UpdateCheck.CanCheck) disabled = true;
            if (UpdateCheck.Status == UpdateCheck.State.Failed && UpdateCheck.Message != null)
                tip = "Last check failed: " + UpdateCheck.Message;
        }
        if (Label != label)
        {
            Log.Info($"Update button: {label} (available {UpdateCheck.Latest ?? "none"}, updater {Updater.Status})");
            Label = label;
            QueueRedraw();
        }
        if (Kind != look) { Kind = look; QueueRedraw(); }
        if (Disabled != disabled) { Disabled = disabled; QueueRedraw(); }
        TooltipText = tip;
    }
}

/// <summary>
/// Compact record of the player's run log for the menu: accuracy gauge, last session and day streak. Built once
/// per menu build (the run list is small) and shared by the hero panel and the rail card.
/// </summary>
public readonly record struct SessionSummary(
    int Runs, float Accuracy, DateTime? Last, int Streak, int Best, bool Today, string LastMode, int WeekMask)
{
    public bool Day(int index) => (WeekMask & (1 << index)) != 0;

    public static SessionSummary From(IReadOnlyList<RunRecord> runs)
    {
        if (runs.Count == 0) return default;
        var ordered = runs.Where(r => r.When.Year > 2000).OrderBy(r => r.When).ToList();
        if (ordered.Count == 0) return default;
        var last = ordered[^1];
        var days = ordered.Select(r => r.When.Date).Distinct().OrderBy(d => d).ToList();
        var set = days.ToHashSet();
        var today = DateTime.Now.Date;

        // A streak survives until the end of today; it only breaks once yesterday was missed too.
        var cursor = set.Contains(today) ? today : today.AddDays(-1);
        int streak = 0;
        while (set.Contains(cursor)) { streak++; cursor = cursor.AddDays(-1); }

        int best = 0, run = 0;
        DateTime? prev = null;
        foreach (var day in days)
        {
            run = prev is { } p && (day - p).Days == 1 ? run + 1 : 1;
            best = Math.Max(best, run);
            prev = day;
        }

        // Weighted by shots so a 200-shot drill does not count as much as a 12-shot pistol round.
        var valid = ordered.Where(r => r.Accuracy >= 0 && r.Shots > 0).TakeLast(20).ToList();
        int shots = valid.Sum(r => r.Shots);
        float acc = shots > 0 ? valid.Sum(r => r.Accuracy * r.Shots) / shots : -1;

        int mask = 0;
        for (int i = 0; i < 7; i++)
            if (set.Contains(today.AddDays(i - 6))) mask |= 1 << i;

        return new SessionSummary(ordered.Count, acc, last.When, streak, best, set.Contains(today), last.Mode, mask);
    }

    /// <summary>Short relative time for the last run ("2H", "3D", "YESTERDAY").</summary>
    public static string Ago(DateTime when)
    {        var now = DateTime.Now;
        var span = now - when;
        if (span.TotalMinutes < 2) return Loc.T("Just now").ToUpperInvariant();
        if (span.TotalMinutes < 60)
            return Loc.Spanish ? $"HACE {(int)span.TotalMinutes} MIN" : $"{(int)span.TotalMinutes} MIN AGO";
        if (span.TotalHours < 24)
            return Loc.Spanish ? $"HACE {(int)span.TotalHours} H" : $"{(int)span.TotalHours} H AGO";
        int days = (now.Date - when.Date).Days;
        if (days == 1) return Loc.Spanish ? "AYER" : "YESTERDAY";
        if (days < 7) return Loc.Spanish ? $"HACE {days} DÍAS" : $"{days} DAYS AGO";
        return when.ToString("d MMM", CultureInfo.InvariantCulture).ToUpperInvariant();
    }
}

/// <summary>The hero's right half: an animated accuracy ring, last session and day streak, separated by a hairline.
/// The ring sweeps to its value and the highlights breathe while the menu is open.</summary>
public partial class HeroRecordPanel : Control
{
    public float K = 1f;
    public SessionSummary Data;
    public string LastLabel = "";
    float t = 0.15f; // entry clock: the accuracy ring sweeps in during the first second

    public HeroRecordPanel() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Process(double delta)
    {
        t += (float)delta;
        if (t < 1.2f) QueueRedraw();
    }

    public override void _Draw()
    {
        float k = K, w = Size.X, h = Size.Y;
        if (w < 20 || h < 20) return;
        var hair = new Color(1, 1, 1, 0.09f);
        Gfx.VGradient(this, new Rect2(0, 6 * k, 1, h - 12 * k), new Color(1, 1, 1, 0.02f), hair);
        DrawRect(new Rect2(0, h * 0.5f - 10 * k, 1, 20 * k), new Color(UiTheme.Teal, 0.5f));

        int hs = UiTheme.Fs(9.4f, k);
        Gfx.Diamond(this, new Vector2(16 * k, 13 * k), 2.6f * k, 2.6f * k, UiTheme.Accent);
        Gfx.Text(this, UiTheme.HudWide, "YOUR RECORD", 25 * k, Gfx.Mid(13 * k, hs), hs, UiTheme.TextSoft);
        Gfx.TextR(this, UiTheme.HudWide, "20 RUNS", w - 2 * k, Gfx.Mid(13 * k, hs), hs, UiTheme.Faint);
        DrawRect(new Rect2(14 * k, 28 * k, w - 14 * k, 1), hair);

        // accuracy ring (sweeps in with ease-out)
        var c = new Vector2(64 * k, 95 * k);
        float r = 30 * k, ring = Mathf.Max(3f, 4.2f * k);
        float sweep = Gfx.EaseOutQuart(Mathf.Clamp((t - 0.15f) / 0.9f, 0, 1));
        DrawArc(c, r, 0, Mathf.Tau, 72, new Color(1, 1, 1, 0.075f), ring, true);
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.Tau / 12f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(c + dir * (r + 7 * k), c + dir * (r + 10 * k), new Color(1, 1, 1, 0.055f), Mathf.Max(1f, k));
        }
        float acc = Mathf.Clamp(Data.Accuracy, 0, 1);
        if (Data.Runs > 0 && Data.Accuracy >= 0)
        {
            float start = -Mathf.Pi / 2;
            float end = start + Mathf.Tau * acc * sweep;
            DrawArc(c, r, start, end, 72, UiTheme.Teal, ring, true);
            var tip = c + new Vector2(Mathf.Cos(end), Mathf.Sin(end)) * r;
            DrawCircle(tip, ring * (0.55f + 0.45f * sweep), UiTheme.Teal, true, -1, true);
            if (sweep >= 0.999f)
            {
                // landing pulse ring
                float pw = t % 2.4f / 2.4f;
                DrawArc(c, r * (1f + pw * 0.3f), 0, Mathf.Tau, 48, new Color(UiTheme.Teal, 0.25f * (1f - pw)), 1.4f * k, true);
            }
        }
        int vs = UiTheme.Fs(20, k);
        string accText = Data.Runs > 0 && Data.Accuracy >= 0 ? $"{Data.Accuracy * 100:0}%" : "—";
        Gfx.TextC(this, UiTheme.Display, accText, c.X, Gfx.Mid(c.Y, vs), vs, UiTheme.Text);
        Gfx.TextC(this, UiTheme.HudWide, "ACCURACY", c.X, c.Y + 27 * k, UiTheme.Fs(8.2f, k), UiTheme.Faint);

        // last session / day streak
        float x = 116 * k, right = w - 2 * k;
        int ls = UiTheme.Fs(8.6f, k), val = UiTheme.Fs(15, k), ss = UiTheme.Fs(8.6f, k);
        Gfx.Text(this, UiTheme.HudWide, "LAST SESSION", x, 46 * k, ls, UiTheme.Faint);
        string last = Data.Last is { } tt ? SessionSummary.Ago(tt) : "NO RUNS YET";
        Gfx.TextFit(this, UiTheme.Display, last, x, 66 * k, val, Data.Last != null ? UiTheme.Text : UiTheme.Dim, right - x);
        if (LastLabel.Length > 0)
            Gfx.TextFit(this, UiTheme.Body, LastLabel.ToUpperInvariant(), x, 78 * k, ss, UiTheme.Dim, right - x);

        DrawRect(new Rect2(x, 90 * k, right - x, 1), hair);
        Gfx.Text(this, UiTheme.HudWide, "DAY STREAK", x, 105 * k, ls, UiTheme.Faint);
        string streak = Data.Streak == 1
            ? (Loc.Spanish ? "1 DÍA" : "1 DAY")
            : Loc.Spanish ? $"{Data.Streak} DÍAS" : $"{Data.Streak} DAYS";
        Gfx.Text(this, UiTheme.Display, streak, x, 126 * k, val, Data.Streak > 0 ? UiTheme.Accent : UiTheme.Dim);
        if (Data.Best > 0)
            Gfx.TextR(this, UiTheme.HudWide, Loc.Spanish ? $"MEJOR {Data.Best}" : $"BEST {Data.Best}", right, 126 * k, ss, UiTheme.Faint);
        if (Data.Streak > 0)
        {
            // ember underline that grows under the streak, then breathes softly
            float gw = Gfx.EaseOutCubic(Mathf.Clamp((t - 0.5f) / 0.5f, 0, 1));
            Gfx.HGradient(this, new Rect2(x, 130 * k, 46 * k * gw, 2 * k), new Color(UiTheme.Accent, 0.5f), new Color(UiTheme.Accent, 0));
        }

        // today marker at the bottom
        var dot = Data.Today ? UiTheme.Teal : new Color(1, 1, 1, 0.18f);
        DrawCircle(new Vector2(16 * k, h - 10 * k), 2.2f * k, dot, true, -1, true);
        if (Data.Today) DrawArc(new Vector2(16 * k, h - 10 * k), (4 + 3 * ((t % 1.6f) / 1.6f)) * k, 0, Mathf.Tau, 32,
            new Color(UiTheme.Teal, 0.35f * (1f - (t % 1.6f) / 1.6f)), 1.1f * k, true);
        Gfx.Text(this, UiTheme.HudWide,
            Data.Today ? (Loc.Spanish ? "ENTRENADO HOY" : "TRAINED TODAY") : (Loc.Spanish ? "SIN ENTRENAR HOY" : "NOT TRAINED TODAY"),
            24 * k, h - 7 * k, UiTheme.Fs(8.2f, k), Data.Today ? UiTheme.Teal : UiTheme.Faint);
    }
}

/// <summary>Rail card: the current day streak; the seven day bars grow in with a stagger when the menu opens, and the
/// today marker pulses while the menu is up.</summary>
public partial class StreakCard : Control
{
    public float K = 1f;
    public SessionSummary Data;
    float t = 0.2f; // seconds since build: the first beat drives the grow-in

    public StreakCard() { } // default mouse filter, so the sidebar tooltip keeps working

    public override void _Process(double delta)
    {
        t += (float)delta;
        bool anim = t < 1.0f || (Data.Today && Data.Streak > 0);
        if (anim) QueueRedraw();
    }

    public override void _Draw()
    {
        float k = K, w = Size.X, h = Size.Y;
        if (w < 40 || h < 40) return;
        Gfx.PlateEx(this, new Rect2(0, 0, w, h), 10 * k, true, false, false, true,
            new Color(1, 1, 1, 0.038f), new Color(1, 1, 1, 0.085f), 1);
        DrawRect(new Rect2(w - 2 * k, h - 14 * k, 2 * k, 8 * k), new Color(UiTheme.Teal, 0.4f));
        Gfx.Diamond(this, new Vector2(13 * k, 17 * k), 4 * k, 4 * k, Data.Streak > 0 ? UiTheme.Accent : UiTheme.Faint);
        Gfx.Text(this, UiTheme.HudWide, "STREAK", 22 * k, 17 * k, UiTheme.Fs(8.6f, k), UiTheme.Faint);
        if (Data.Best > 0)
            Gfx.TextR(this, UiTheme.HudWide, Loc.Spanish ? $"MEJOR {Data.Best}D" : $"BEST {Data.Best}D", w - 10 * k, 17 * k, UiTheme.Fs(8.6f, k), UiTheme.Faint);

        int vs = UiTheme.Fs(17, k);
        string v = Data.Streak > 0
            ? (Data.Streak == 1 ? (Loc.Spanish ? "1 DÍA" : "1 DAY") : Loc.Spanish ? $"{Data.Streak} DÍAS" : $"{Data.Streak} DAYS")
            : (Loc.Spanish ? "EMPIEZA HOY" : "START TODAY");
        Gfx.Text(this, UiTheme.Display, v, 10 * k, 41 * k, vs, Data.Streak > 0 ? UiTheme.Text : UiTheme.Dim);
        if (Data.Streak > 0)
        {
            // hot ember under the number
            Gfx.HGradient(this, new Rect2(10 * k, 45 * k, 64 * k, 2 * k), new Color(UiTheme.Accent, 0.55f), new Color(UiTheme.Accent, 0f));
        }
        if (Data.Today && Data.Streak > 0)
        {
            int cs = UiTheme.Fs(7.4f, k);
            float cw = Gfx.TextW(UiTheme.HudWide, "TODAY", cs) + 10 * k;
            var cr = new Rect2(w - 10 * k - cw, 28 * k, cw, 14 * k);
            float pw = Gfx.Wave(t, 0.55f);
            Gfx.PlateEx(this, cr, 4 * k, true, false, false, true, new Color(UiTheme.Teal, 0.12f), new Color(UiTheme.Teal, 0.35f + 0.25f * pw), 1);
            Gfx.TextC(this, UiTheme.HudWide, "TODAY", cr.GetCenter().X, cr.Position.Y + 10 * k, cs, UiTheme.Teal.Lerp(Colors.White, 0.25f * pw));
        }

        // seven days: six days ago ... today, growing with a stagger
        float gap = 4 * k, bw = (w - 20 * k - gap * 6) / 7, by = h - 26 * k, bl = 16 * k;
        DrawRect(new Rect2(10 * k, by + bl, w - 20 * k, 1), new Color(1, 1, 1, 0.05f));
        var culture = CultureInfo.GetCultureInfo(Loc.Spanish ? "es-ES" : "en-US");
        int ds = UiTheme.Fs(7.2f, k);
        for (int i = 0; i < 7; i++)
        {
            bool on = Data.Day(i), today = i == 6;
            float grow = Gfx.EaseOutBack(Mathf.Clamp((t - 0.15f - i * 0.055f) / 0.4f, 0, 1));
            float bh = (on ? bl : 5 * k) * grow;
            float bx = 10 * k + i * (bw + gap);
            var col = on ? (today ? UiTheme.Accent.Lerp(Colors.White, 0.35f + 0.25f * Gfx.Wave(t, 1.1f)) : new Color(UiTheme.Accent, 0.62f)) : new Color(1, 1, 1, 0.09f);
            DrawRect(new Rect2(bx, by + bl - bh, bw, Mathf.Max(1.5f, bh)), col);
            string day = culture.DateTimeFormat.GetAbbreviatedDayName(DateTime.Now.Date.AddDays(i - 6).DayOfWeek);
            string initial = day.Length > 0 ? day[..1].ToUpperInvariant() : "";
            float iw = Gfx.RawW(UiTheme.HudWide, initial, ds);
            DrawString(UiTheme.HudWide, new Vector2(bx + (bw - iw) / 2, h - 9 * k), initial,
                HorizontalAlignment.Left, -1, ds, today ? UiTheme.Accent : UiTheme.Faint);
        }
    }
}

/// <summary>
/// The menu's HUD frame: breathing corner brackets on the right, a laser scanline crossing the screen every ~20s,
/// slowly spinning corner arcs and fine edge hairlines. Drawn behind the content, animated every frame (it is one
/// lightweight canvas item; the shader backdrop already repaints every pixel).
/// </summary>
public partial class MenuFX : Control
{
    public float K = 1f;
    float t = 0.4f;

    public MenuFX() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Process(double delta)
    {
        t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float k = K;
        var s = GetViewportRect().Size;
        if (s.X < 8 || s.Y < 8) return;

        // fine hairline frame
        DrawRect(new Rect2(0, 0, s.X, 1), new Color(1, 1, 1, 0.05f));
        DrawRect(new Rect2(0, s.Y - 1, s.X, 1), new Color(1, 1, 1, 0.05f));
        DrawRect(new Rect2(0, 0, 1, s.Y), new Color(1, 1, 1, 0.03f));
        DrawRect(new Rect2(s.X - 1, 0, 1, s.Y), new Color(1, 1, 1, 0.03f));

        // breathing corner brackets (right side; the left is under the nav rail)
        float len = (30 + 7 * Gfx.Wave(t, 0.42f)) * k, off = 10 * k;
        var br = new Color(UiTheme.Accent, 0.5f + 0.2f * Gfx.Wave(t, 0.42f));
        var teal = new Color(UiTheme.Teal, 0.35f);
        Bracket(new Vector2(s.X - off, off), len, 1, br);          // top-right
        Bracket(new Vector2(s.X - off, s.Y - off), len, 3, br);    // bottom-right
        DrawRect(new Rect2(s.X - off - 46 * k, s.Y - off - 3 * k, 26 * k, 2 * k), teal);
        Bracket(new Vector2(off, s.Y - off), len * 0.7f, 0, new Color(UiTheme.Text, 0.20f)); // bottom-left, quiet

        // mid-edge ticks
        float midX = s.X * 0.5f;
        DrawRect(new Rect2(midX - 4 * k, 2 * k, 8 * k, 1.6f * k), new Color(UiTheme.Accent, 0.45f));
        DrawRect(new Rect2(midX - 0.8f * k, 2 * k, 1.6f * k, 9 * k), new Color(UiTheme.Accent, 0.35f));
        DrawRect(new Rect2(s.X - 2 * k, s.Y * 0.5f, 2 * k, 10 * k), new Color(UiTheme.Teal, 0.30f));

        // rotating corner arcs top-right: two counter-rotating dashed rings
        var c = new Vector2(s.X - off, off);
        for (int i = 0; i < 9; i++)
        {
            float a0 = t * 0.35f + i * Mathf.Tau / 9f;
            DrawArc(c, 148 * k, a0, a0 + 0.16f, 8, new Color(UiTheme.Accent, 0.16f), 1.1f * k, true);
        }
        for (int i = 0; i < 6; i++)
        {
            float a0 = -t * 0.22f + i * Mathf.Tau / 6f;
            DrawArc(c, 96 * k, a0, a0 + 0.30f, 10, new Color(UiTheme.Teal, 0.13f), 1f * k, true);
        }

        // scanline crossing top→bottom
        float span = s.Y + 220 * k;
        float y = ((t * 0.055f) % 1.2f) * span - 160 * k;
        if (y > -40 * k && y < s.Y + 40 * k)
        {
            // soft trail above the bright line
            Gfx.VGradient(this, new Rect2(0, y - 150 * k, s.X, 150 * k), new Color(UiTheme.Accent, 0f), new Color(UiTheme.Accent, 0.05f));
            DrawRect(new Rect2(0, y, s.X, 1.6f * k), new Color(UiTheme.Accent, 0.28f));
            DrawRect(new Rect2(0, y, s.X, 0.8f), new Color(1, 1, 1, 0.16f));
        }

        // drifting "data" ticks along the bottom edge: a comb whose phases breathe
        for (int i = 0; i < 24; i++)
        {
            float x = s.X * (i + 0.5f) / 24f;
            float a = 0.05f + 0.05f * Gfx.Wave(t * 0.7f + i * 0.45f, 0.35f);
            float hh = (3 + 3 * Gfx.Wave(t * 0.5f + i * 0.9f, 0.4f)) * k;
            DrawRect(new Rect2(x, s.Y - 2 * k - hh, 1.4f * k, hh), new Color(UiTheme.Text, a));
        }
    }

    /// <summary>An L bracket opening toward the screen interior. dir: 0 bottom-left, 1 top-right, 3 bottom-right.</summary>
    void Bracket(Vector2 c, float len, int corner, Color col)
    {
        float w = 1.6f * K;
        // 1: arms go left and down; 3: arms go left and up; 0: arms right and up
        float sx = corner == 0 ? 1 : -1;
        float sy = corner == 1 ? 1 : -1;
        DrawRect(new Rect2(c.X - (sx > 0 ? 0 : len), c.Y - (sy < 0 ? w : 0), len, w), col);
        DrawRect(new Rect2(c.X - (sx < 0 ? w : 0), c.Y - (sy > 0 ? 0 : len), w, len), col);
    }
}

/// <summary>Quiet accent layer for the menu footer: a breathing signal diamond and terminal hairline ticks.</summary>
public partial class FooterFX : Control
{
    public float K = 1f;
    float t = 0.4f;

    public FooterFX() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Process(double delta)
    {
        t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float k = K;
        var s = Size;
        if (s.X < 8 || s.Y < 8) return;
        // breathing signal diamond, left of the fan notice
        float r = (2.2f + 0.8f * Gfx.Wave(t, 0.7f)) * k;
        Gfx.Diamond(this, new Vector2(6 * k, s.Y / 2), r, r, UiTheme.Teal);
        Gfx.Diamond(this, new Vector2(6 * k, s.Y / 2), r * 2.4f, r * 2.4f, new Color(UiTheme.Teal, 0.12f));
        // fading hairline under the row's left half, terminal ticks on the right
        Gfx.HGradient(this, new Rect2(22 * k, s.Y - 1, s.X * 0.5f, 1), new Color(UiTheme.Text, 0.07f), new Color(UiTheme.Text, 0f));
        DrawRect(new Rect2(s.X - 10 * k, s.Y - 3 * k, 6 * k, 2 * k), new Color(UiTheme.Accent, 0.4f));
        DrawRect(new Rect2(s.X - 20 * k, s.Y - 3 * k, 3 * k, 2 * k), new Color(UiTheme.Text, 0.25f));
    }
}
