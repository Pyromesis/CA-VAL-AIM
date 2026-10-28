using Godot;
using ValTrainer.Audio;
using ValTrainer.Core;
using ValTrainer.Game;
using ValTrainer.Modes;
using ValTrainer.UI;
using ValTrainer.Valorant;

namespace ValTrainer;

/// <summary>
/// Application root (script of Main.tscn): loads settings + the Valorant profile, applies Valorant's
/// display settings, owns the audio engine and switches between the menu and game sessions.
/// </summary>
public partial class Main : Node
{
    public static Main I { get; private set; } = null!;
    /// <summary>This copy found another ValTrainer running and is quitting (it loads nothing and saves nothing).</summary>
    bool singleInstanceExit;

    public AppSettings Settings = AppSettings.Load();
    public StatsStore Stats = StatsStore.Load();
    public List<ValorantAccount> Accounts = new();
    public ValorantProfile Valorant = new();

    Node? screen;
    /// <summary>A drill owns the screen: full frame cap. Menus stay at 60 to keep the GPU/CPU idle.</summary>
    bool inGame;

    /// <summary>Automation/dev mode (--dev): windowed 1600×900, never captures the mouse or pauses on focus loss,
    /// so test runs don't take over the desktop. Also accepts --tier N and --map key overrides (not saved).</summary>
    public bool Dev { get; private set; }
    public int? TierOverride { get; private set; }
    public string? MapOverride { get; private set; }
    /// <summary>Key of the last drill started this session (the context of a bug report), null before the first.</summary>
    public string? LastModeKey;

    /// <summary>Whether this run may write settings, stats and telemetry: always in normal play; in dev runs only with
    /// --data-dir &lt;folder&gt; --write-data (so tests can exercise saving without touching the user's real files).</summary>
    public bool SavesData => !Dev || (Paths.DataDirOverridden && CmdLine.Has("--write-data"));

    // ---- Effective settings: trainer override if set, otherwise Valorant's ----
    public float Sens => Settings.UseSensOverride ? Settings.SensOverride : Valorant.Sensitivity;
    public int EnemyColorIndex => Settings.EnemyColorOverride >= 0 ? Settings.EnemyColorOverride : Valorant.EnemyHighlight;
    public Color EnemyColor => UiTheme.EnemyColors[Math.Clamp(EnemyColorIndex, 0, UiTheme.EnemyColors.Length - 1)];
    public int WindowMode => Settings.WindowModeOverride >= 0 ? Settings.WindowModeOverride : Valorant.WindowMode;
    public bool VSync => Settings.VSyncOverride >= 0 ? Settings.VSyncOverride == 1 : Valorant.VSync;
    public int FpsCap => Settings.FpsCapOverride >= 0 ? Settings.FpsCapOverride : (int)Valorant.FrameRateLimit;
    public int Monitor => Math.Clamp(Settings.MonitorOverride >= 0 ? Settings.MonitorOverride : Valorant.MonitorIndex, 0, Math.Max(0, DisplayServer.GetScreenCount() - 1));

    public override void _Ready()
    {
        I = this;
        Boot.InstallCrashHandlers();
        // One ValTrainer at a time (dev / automated runs excepted; dev: --single-instance tests it): a second launch brings
        // this one to the front and quits.
        var startArgs = OS.GetCmdlineUserArgs().Concat(OS.GetCmdlineArgs()).ToArray();
        Dev = startArgs.Contains("--dev");
        // Latch read-only before anything can save: Reimport() below persists an imported sensitivity, and a dev run with
        // --data-dir must never touch that folder unless it also asked for --write-data.
        AppSettings.ReadOnly = !SavesData;
        if ((!Dev || startArgs.Contains("--single-instance")) && !SingleInstance.Claim())
        {
            Log.Info("CA-VAL-AIM is already running: brought that window to the front; quitting this copy");
            singleInstanceExit = true;
            GetTree().Quit();
            return;
        }
        // Old builds stored Easy/Normal/Hard; map once onto the 5 rank tiers.
        if (Settings.Tier < 0 || Settings.Tier > 4) Settings.Tier = Settings.Difficulty switch { 0 => 0, 2 => 3, _ => 2 };
        Reimport();
        Sfx.Volume = Settings.Volume;
        AddChild(new Sfx { Name = "Sfx" });
        UiTheme.Init();
        Loc.SetLanguage(Settings.Language);
        if (!Dev) ApplyDisplay();
        GetTree().Root.Title = AppInfo.Name;

        var args = startArgs;
        if (Dev)
        {
            // Dev-only layout/QA overrides (never saved): --lang es, --rifle phantom, --ammo default|real|infinite.
            if (CmdLine.After("--lang") is { } devLang) Loc.SetLanguage(devLang);
            if (CmdLine.After("--rifle") is { } rifle) Settings.PrimaryRifle = rifle.Equals("phantom", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            // --dm-difficulty easy|medium|hard|nightmare, --dm-players N: the menu Deathmatch's lobby (not saved).
            if (CmdLine.After("--dm-difficulty") is { } dmd)
                Settings.DmDifficulty = dmd.ToLowerInvariant() switch { "easy" => 0, "hard" => 2, "nightmare" => 3, _ => 1 };
            if (int.TryParse(CmdLine.After("--dm-players"), out int dmp)) Settings.DmPlayers = dmp;
            if (CmdLine.After("--ammo") is { } ammo)
                Settings.AmmoMode = ammo.ToLowerInvariant() switch { "infinite" => 1, "real" => 0, _ => -1 };
        }
        if (args.Contains("--coach-analyze"))
        {
            // Dev tool: analyse saved telemetry and quit (works with --headless).
            Analysis.CoachCli.Run(args);
            GetTree().Quit();
            return;
        }
        if (Dev && CmdLine.After("--print-licenses") is { } licenses)
        {
            // Build tool (works with --headless): the open-source notices for THIRD-PARTY-NOTICES.txt.
            GetTree().Quit(AboutLicenses.WriteFile(licenses) ? 0 : 1);
            return;
        }
        PickFirstRunQuality();
        Log.Info($"CA-VAL-AIM {AppInfo.Version}; language {Loc.Language}; {SysInfo.Summary}; quality {Settings.Quality}{(Settings.AutoQualityReason != null ? $" (auto: {Settings.AutoQualityReason})" : "")}; " +
                 $"data {Paths.DataDir}{(Paths.DataDirProblem != null ? $" [{Paths.DataDirProblem}]" : "")}; " +
                 $"VALORANT {(Valorant.Found ? "imported" : Valorant.Status.ToString())}; logs {Paths.LogDir}");
        if (Dev && args.Contains("--keybind-dump"))
        {
            // Dev tool (works with --headless): the resolved VALORANT keybinds (defaults + BackupKeybinds.json overrides).
            GD.Print($"[keybinds] {(Valorant.Found ? "imported account" : "no account: VALORANT defaults")}");
            foreach (var line in Valorant.Binds.Dump().Split('\n')) GD.Print("[keybinds] " + line);
            GetTree().Quit();
            return;
        }
        if (Dev && args.Contains("--profile-dump"))
        {
            // Dev tool (works with --headless): what the VALORANT import actually produced.
            GD.Print($"[profile] account {Valorant.AccountId ?? "-"} ({Valorant.Status}); {Valorant.Source}");
            GD.Print($"[profile] dir {Paths.ValorantConfigDir}");
            GD.Print($"[profile] sensitivity {Valorant.Sensitivity} fromFile={Valorant.SensFromFile} custom={Settings.UseSensOverride} override={Settings.SensOverride} last={Settings.ValorantSens} effective={Sens}");
            var xh = Valorant.Crosshair;
            GD.Print($"[profile] crosshair '{xh.Name}' advanced={xh.UseAdvancedOptions} note={Valorant.CrosshairNote ?? "-"}");
            GD.Print($"[profile] primary color={xh.Primary.Color.ToHtml()} outline={xh.Primary.HasOutline} dot={xh.Primary.CenterDot} " +
                     $"inner={xh.Primary.Inner.Show} outer={xh.Primary.Outer.Show}");
            GetTree().Quit();
            return;
        }
        if (Dev && CmdLine.After("--spawn-dump") is { } spawnDump)
        {
            // Dev tool (works with --headless): every map's spawn points through the game's own spawn code, for
            // tools/mapgen/audit_spawns.py (--spawn-maps a,b limits the maps).
            GetTree().Quit(Maps.SpawnAudit.Run(spawnDump, CmdLine.After("--spawn-maps")));
            return;
        }
        if (Dev && args.Contains("--culture-test"))
        {
            // Dev self-test (works with --headless): parsers and number formatting under the current culture.
            int failures = CultureCheck.Run();
            GetTree().Quit(failures == 0 ? 0 : 1);
            return;
        }
        if (Dev && args.Contains("--bugreport-test"))
        {
            // Dev self-test (works with --headless): bug report redaction, JSON shape and size limits.
            int failures = BugReport.SelfTest();
            GetTree().Quit(failures == 0 ? 0 : 1);
            return;
        }
        if (Dev && args.Contains("--preview-check"))
        {
            // Dev self-test (works with --headless --fixed-fps 60): every drill's preview demo plays and saves nothing.
            Callable.From(() => AddChild(new ValTrainer.UI.PreviewCheck())).CallDeferred();
        }
        if (Dev && args.Contains("--throw-test")) throwTest = 61; // dev: prove callback/task exceptions reach the log
        WhatsNew.Prepare(Settings);  // "What's new" panel after an update (dev: --whats-new [fromVersion])
        UpdatesPrompt.Prepare(Settings); // opt-in "Check GitHub for new versions?" (dev: --updates-prompt, --updates-consent yes|no)
        // Support panel disabled in this build: no external donation or chat services.
        Updater.Init();              // how this copy updates itself; finishes / gives up on an update started last run
        UpdateCheck.StartupCheck();  // only once the player opted in: at startup and every 6 h (dev: --update-test <version>, --update-source <url>)
        int ti = Array.IndexOf(args, "--tier");
        if (ti >= 0 && ti + 1 < args.Length && int.TryParse(args[ti + 1], out var tv)) TierOverride = Math.Clamp(tv, 0, 4);
        int mi = Array.IndexOf(args, "--map");
        if (mi >= 0 && mi + 1 < args.Length) MapOverride = args[mi + 1];
        if (Dev)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            // Dev "--window WxH" (e.g. 1280x720, 1024x768, 1720x720) for layout tests at other resolutions / aspects.
            DisplayServer.WindowSetSize(DevWindowSize(CmdLine.After("--window")) ?? new Vector2I(1600, 900));
            // Dev "--render-size WxH" (e.g. 2560x1440): lay out at that size, scaled into the window (layouts bigger than the monitor).
            if (DevWindowSize(CmdLine.After("--render-size")) is { } rs)
            {
                GetTree().Root.ContentScaleMode = Window.ContentScaleModeEnum.Viewport;
                GetTree().Root.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
                GetTree().Root.ContentScaleSize = rs;
            }
            DisplayServer.WindowSetPosition(DisplayServer.ScreenGetPosition(Monitor) + new Vector2I(60, 60));
        }
        ApplyFps();
        int i = Array.IndexOf(args, "--mode");
        var make = i >= 0 && i + 1 < args.Length ? ModeRegistry.Find(args[i + 1]) : null;
        // A downloaded update is installed now, before the menu (never mid-drill): "Updating ValTrainer…", then the
        // installer / portable swap starts the new version and this one quits.
        bool updating = make == null && Updater.ShouldApplyAtStartup();
        noTransitions = Dev && args.Contains("--no-transitions");
        if (make != null) SetScreen(new GameSession(make)); // dev "--mode": straight in, as the automated checks expect
        else if (updating) SetScreen(new UpdatingScreen());
        else ShowMenu();
        if (updating) return;
        // Dev-only: "--screen settings|about|stats" opens that screen directly (MenuScreen handles "profile").
        switch (Dev ? CmdLine.After("--screen")?.ToLowerInvariant() : null)
        {
            case "settings": Callable.From(ShowSettings).CallDeferred(); break;
            case "about": SettingsScreen.StartPage = SettingsScreen.Page.About; Callable.From(ShowSettings).CallDeferred(); break;
            case "stats": Callable.From(ShowStats).CallDeferred(); break;
            case "reaction" or "reactions": Callable.From(ShowReactionStats).CallDeferred(); break;
            case "senslab" or "sens-lab": Callable.From(ShowSensLab).CallDeferred(); break;
            case "warmup" or "lockin": Callable.From(ShowLockIn).CallDeferred(); break;
            case "agents": Callable.From(ShowAgents).CallDeferred(); break; // + --agent jett, --agent-go routine|<drill> (AgentsScreen)
        }
        // Dev-only: "--lockin short|standard|training" (or the older "--warmup") starts a Lock-In; "demo", "demo-details",
        // "demo-breath" show those screens with made-up data. See WarmupRunner for --wuquick, --lockin-checkin E,C, --wuquit.
        if (Dev && make == null && (CmdLine.After("--lockin") ?? CmdLine.After("--warmup")) is { } wu) Callable.From(() => Warmup.WarmupRunner.StartDev(wu)).CallDeferred();
        // Dev-only: "--rewind-file run.vtt [--rewind-code CODE] [--rewind-at S]" opens the Rewind of a saved run.
        if (Dev && make == null && CmdLine.After("--rewind-file") is { } rf) Callable.From(() => RewindScreen.OpenDevFile(rf)).CallDeferred();
        // Dev-only: "--plays-file run.vtt" opens its play-by-play list (screenshots of the per-play review).
        if (Dev && make == null && CmdLine.After("--plays-file") is { } pf) Callable.From(() => PlaysScreen.OpenDevFile(pf)).CallDeferred();
        Analysis.CoachWarmUp.Start(); // JIT the coach off the main thread before the first results screen
        // Dev-only: "--screenshot <path prefix> --screenshot-at 3,5.5,9" saves the game's own frame (any screen, DPI-proof,
        // works on a locked desktop) as <prefix>_<n>.png at those seconds after start, then quits after the last one.
        if (Dev && CmdLine.After("--screenshot") is { } shotPrefix)
        {
            var times = (CmdLine.After("--screenshot-at") ?? "3").Split(',')
                .Select(s => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : -1)
                .Where(v => v >= 0).OrderBy(v => v).ToList();
            for (int n = 0; n < times.Count; n++)
            {
                int idx = n;
                bool last = n == times.Count - 1;
                GetTree().CreateTimer(times[n]).Timeout += () =>
                {
                    var img = GetViewport().GetTexture().GetImage();
                    string path = $"{shotPrefix}_{idx}.png";
                    img.SavePng(path);
                    GD.Print($"[screenshot] t={times[idx]:0.0}s {path} ({screen?.GetType().Name})");
                    if (last) GetTree().Quit();
                };
            }
        }
    }

    static Vector2I? DevWindowSize(string? wxh)
    {
        var p = wxh?.ToLowerInvariant().Split('x');
        return p is { Length: 2 } && int.TryParse(p[0], out int w) && int.TryParse(p[1], out int h) && w >= 320 && h >= 240
            ? new Vector2I(w, h) : null;
    }

    /// <summary>First run (no settings file): Low on integrated / virtual / software GPUs and on the Compatibility
    /// renderer, else the default Medium. A quality the user saved is never touched.</summary>
    void PickFirstRunQuality()
    {
        if (!Settings.IsNew) return;
        var (q, reason) = SysInfo.AutoQuality();
        Settings.Quality = q;
        Settings.AutoQualityReason = reason;
    }

    // VALORANT rewrites its config when the game closes or applies a settings change; while ValTrainer sits in a menu,
    // pick those writes up so the sens and the crosshair the drills use are the ones the player sees in-game.
    long valorantStamp;
    double valorantPoll;

    /// <summary>Newest write time of every file <see cref="Reimport"/> reads (0 when there is none).</summary>
    long ValorantStamp()
    {
        long t = 0;
        void Add(string? f)
        {
            try { if (f != null && File.Exists(f)) t = Math.Max(t, File.GetLastWriteTimeUtc(f).Ticks); } catch { }
        }
        try
        {
            foreach (var a in Accounts)
            {
                Add(Path.Combine(a.Folder, "Windows", "RiotUserSettings.ini"));
                Add(Path.Combine(a.Folder, "WindowsClient", "GameUserSettings.ini"));
                Add(Path.Combine(a.Folder, "WindowsClient", "BackupKeybinds.json"));
            }
            if (Paths.ValorantConfigDir is { } root)
            {
                Add(Path.Combine(root, "WindowsClient", "GameUserSettings.ini"));
                Add(Path.Combine(root, "WindowsClient", "RiotLocalMachine.ini"));
            }
        }
        catch { }
        return t;
    }

    public void Reimport()
    {
        float before = Settings.ValorantSens;
        Accounts = ValorantImporter.FindAccounts();
        var acc = Accounts.FirstOrDefault(a => a.Id == Settings.AccountId) ?? Accounts.FirstOrDefault();
        Valorant = ValorantImporter.Load(acc);
        valorantStamp = ValorantStamp();
        bool dirty = false;
        // A CUSTOM sensitivity that still equals the previous import was copied from VALORANT (the source toggle and the
        // Sens Lab do that): follow the new value instead of freezing the copy at whatever the game had at that moment.
        if (Settings.UseSensOverride && before > 0 && MathF.Abs(Settings.SensOverride - before) <= 0.0005f && Valorant.Sensitivity != before)
        {
            Settings.SensOverride = Valorant.Sensitivity;
            dirty = true;
        }
        if (Valorant.Found && Valorant.Sensitivity > 0 && Valorant.Sensitivity != before)
        {
            Settings.ValorantSens = Valorant.Sensitivity;
            dirty = true;
        }
        if (dirty) Settings.Save();
        if (screen is ValTrainer.UI.SettingsScreen ss) ss.RefreshAll();
    }

    void SetScreen(Node n)
    {
        // Crossfade: a still of the outgoing screen lies over the new one and fades out (UI/MinimalKit.cs ScreenFade), so
        // a screen change never shows an empty or half-built frame. Not on the first screen, in headless runs, or when
        // the dev flag --no-transitions asks for instant switches (pixel-exact screenshots).
        Image? still = screen != null && IsInstanceValid(screen) && !noTransitions ? ScreenFade.Capture(GetViewport()) : null;
        screen?.QueueFree();
        screen = n;
        AddChild(n);
        inGame = n is GameSession;
        ApplyFps();
        foreach (var c in GetChildren()) if (c is ScreenFade old) old.QueueFree();
        if (still != null) AddChild(new ScreenFade(still));
    }

    bool noTransitions;

    /// <summary>Tier in effect (dev override or the saved setting).</summary>
    public int Tier => TierOverride ?? Settings.Tier;
    public string MapKey => MapOverride ?? Settings.MapKey;

    /// <summary>Rifle used by drills that accept either Vandal or Phantom (Spray Control keeps its own).</summary>
    public WeaponKind PrimaryRifle => Settings.PrimaryRifle == 1 ? WeaponKind.Phantom : WeaponKind.Vandal;

    /// <summary>Changes the UI language and saves it. Every screen rebuilds itself on the next frame.</summary>
    public void SetLanguage(string lang)
    {
        Loc.SetLanguage(lang);
        Settings.Language = Loc.Language;
        Settings.Save();
    }

    /// <summary>Set when a drill or warm-up is started from the Agents screen: the next <see cref="ShowMenu"/> (results →
    /// MENU, pause → Back to menu, warm-up summary) returns to the Agents screen instead of the main menu.</summary>
    public bool ReturnToAgents;

    public void ShowMenu()
    {
        Warmup.WarmupRunner.Abandon(); // leaving a Lock-In mid-routine (pause → Back to menu)
        Input.MouseMode = Input.MouseModeEnum.Visible;
        if (ReturnToAgents) { ReturnToAgents = false; SetScreen(new AgentsScreen()); return; }
        SetScreen(new MenuScreen());
    }

    public void ShowAgents() => SetScreen(new AgentsScreen());
    public void ShowPlaylists() => SetScreen(new PlaylistsScreen());

    public void ShowSettings() => SetScreen(new SettingsScreen());
    public void ShowStats() => SetScreen(new StatsScreen());
    /// <summary>Reaction times by category (Stats → REACTION TIMES).</summary>
    public void ShowReactionStats() => SetScreen(new ReactionStatsScreen());
    public void ShowProfile() => SetScreen(new ProfileScreen());
    public void ShowSensLab() => SetScreen(new SensLabScreen());
    /// <summary>The Lock-In setup screen (menu LOCK IN).</summary>
    public void ShowLockIn() => SetScreen(new Warmup.WarmupScreen());
    /// <summary>Shows any screen node (Lock-In check-in and step cards, sessions prepared with routine hooks, the hand-off).</summary>
    public void ShowScreen(Node n) => SetScreen(n);

    /// <param name="freePlay">No clock, no score, nothing saved; the drill starts over when its time is up (<see cref="GameSession.FreePlay"/>).</param>
    /// The drill is prepared on the loading screen first (its map's levels load there with a progress bar).
    public void StartMode(Func<TrainingMode> make, bool freePlay = false) => SetScreen(new LoadingScreen(make, freePlay));

    public void Quit()
    {
        Settings.Save();
        GetTree().Quit();
    }

    // ---------------- display ----------------

    public void ApplyFps()
    {
        if (Dev) { Engine.MaxFps = 0; DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled); return; } // benchmarkable
        int cap = FpsCap;
        // Valorant's "unlimited" (FrameRateLimit=0) means "as fast as the monitor": following the refresh
        // rate instead of an uncapped loop is what stops the app from pinning CPU/GPU on menus.
        if (cap <= 0) cap = ScreenRefresh();
        // The menu has no gameplay to smooth: 60 is plenty and leaves the GPU idle between drills.
        if (!inGame) cap = Math.Min(cap, 60);
        Engine.MaxFps = Math.Max(1, cap);
        DisplayServer.WindowSetVsyncMode(VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
    }

    /// <summary>Monitor refresh rate (fallback 144 when the display server can't report it).</summary>
    int ScreenRefresh()
    {
        float hz = DisplayServer.ScreenGetRefreshRate(Monitor);
        return hz > 1f && hz < 1000f ? Mathf.RoundToInt(hz) : 144;
    }

    /// <summary>Mirrors Valorant's display mode, resolution and monitor (overridable in Settings).</summary>
    public void ApplyDisplay()
    {
        int mon = Monitor;
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetCurrentScreen(mon);
        var screenPos = DisplayServer.ScreenGetPosition(mon);
        var screenSize = DisplayServer.ScreenGetSize(mon);
        switch (WindowMode)
        {
            case 0:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                break;
            case 1:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                break;
            default:
                var size = new Vector2I(Math.Min(Valorant.ResX, screenSize.X - 80), Math.Min(Valorant.ResY, screenSize.Y - 100));
                DisplayServer.WindowSetSize(size);
                DisplayServer.WindowSetPosition(screenPos + (screenSize - size) / 2);
                break;
        }
    }

    double fpsLogT;

    int throwTest;

    public override void _Process(double delta)
    {
        if (singleInstanceExit) return;
        SingleInstance.Poll(); // a second launch asked this copy to come to the front (no-op unless this copy holds the lock)
        UpdateCheck.Poll(); // picks up the background update check's answer (cheap when idle)
        Updater.Poll();     // background update download / install (cheap when idle)
        // VALORANT's settings files are only rewritten when the game saves; check them while in a menu (never mid-drill).
        valorantPoll += delta;
        if (valorantPoll >= 2)
        {
            valorantPoll = 0;
            if (screen is ValTrainer.UI.MenuScreen or ValTrainer.UI.SettingsScreen && ValorantStamp() != valorantStamp)
            {
                Reimport();
                Log.Info($"VALORANT settings changed on disk: re-imported (sens {Valorant.Sensitivity}, custom {Settings.UseSensOverride} {Settings.SensOverride}, crosshair '{Valorant.Crosshair.Name}')");
            }
        }
        // Dev runs log FPS so automated checks can read performance from stdout.
        if (!Dev) return;
        if (throwTest > 0)
        {
            // Frame 1: a task that faults and is never awaited; ~60 frames later collect it (fires
            // UnobservedTaskException) and throw from this callback (Godot logs it and keeps running).
            if (--throwTest == 60) System.Threading.Tasks.Task.Run(() => throw new InvalidOperationException("throw-test: exception in a background task"));
            if (throwTest == 0)
            {
                GC.Collect(); GC.WaitForPendingFinalizers();
                throw new InvalidOperationException("throw-test: exception in Main._Process");
            }
        }
        fpsLogT += delta;
        if (fpsLogT >= 2.0) { fpsLogT = 0; GD.Print($"[fps] {Engine.GetFramesPerSecond()}"); }
    }

    public override void _Input(InputEvent e)
    {
        if (singleInstanceExit) return;
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            Settings.WindowModeOverride = WindowMode == 2 ? 1 : 2;
            Settings.Save();
            ApplyDisplay();
        }
    }

    /// <summary>The log's last line on a normal exit (quit, window closed, update restart). A crash never gets here, so a
    /// bug report treats the previous session's log without it as a crash and attaches its end too.</summary>
    public override void _ExitTree()
    {
        MapArt.Clear();
        World.RealMap.ClearPreloaded();
        if (I == this) Log.Info(BugReport.CleanExit);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest && !singleInstanceExit) Settings.Save();
    }
}
