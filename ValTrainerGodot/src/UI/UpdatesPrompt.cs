using Godot;
using ValTrainer.Core;

namespace ValTrainer.UI;

/// <summary>
/// Update checks are automatic (see <see cref="UpdateCheck"/> and <see cref="Updater"/>): at startup and every six
/// hours CA-VAL-AIM asks GitHub for the newest release, shows it in the menu and downloads it in the background. An
/// explicit "off" in Settings → About (or an old "NOT NOW" answer) is respected.
/// <para>The old first-run opt-in panel is kept for UI tests only: <c>--updates-prompt</c> shows it on demand,
/// <c>--updates-consent yes|no</c> sets the answer up front and <c>--updates-answer yes|no</c> presses one of its
/// buttons after 2 s.</para>
/// </summary>
public static class UpdatesPrompt
{
    /// <summary>The menu should ask (cleared once answered).</summary>
    public static bool Pending;

    /// <summary>Called once from Main._Ready (after the settings are loaded, before <see cref="UpdateCheck.StartupCheck"/>).</summary>
    public static void Prepare(AppSettings s)
    {
        if (s.UpdatesConsent == null)
        {
            // Automatic by default. Settings written before this default (or by the old prompt) keep their answer.
            s.UpdatesConsent = s.CheckUpdates;
            s.Save();
        }
        if (CmdLine.DevAfter("--updates-consent")?.ToLowerInvariant() is { } a)
        {
            bool yes = a is "yes" or "true" or "on" or "1";
            s.UpdatesConsent = yes;
            s.CheckUpdates = yes;
            Log.Info($"Updates: --updates-consent {(yes ? "yes" : "no")}");
            return;
        }
        if (!CmdLine.Dev || !CmdLine.Has("--updates-prompt")) return;
        // automated runs on the real settings (read-only) never stop at a prompt
        if (!Main.I.SavesData) return;
        Pending = true;
        Log.Info("Updates: --updates-prompt: asking whether to check GitHub for new versions (UI test only)");
    }

    /// <summary>ENABLE UPDATES (true) / NOT NOW (false). Saved right away; turning on starts the first check.</summary>
    public static void Answer(bool yes)
    {
        Pending = false;
        var s = Main.I.Settings;
        s.UpdatesConsent = yes;
        s.CheckUpdates = yes;
        s.Save();
        Log.Info(yes ? "Updates: turned on by the player (ENABLE UPDATES)" : "Updates: left off by the player (NOT NOW); no update requests");
        UpdateCheck.EnabledChanged();
    }
}

/// <summary>
/// The "a newer version is out" modal: shown once per version and session, as soon as the background check answers
/// (the check is asynchronous, so the menu asks again every frame). UPDATE NOW downloads the release, verifies it and
/// restarts CA-VAL-AIM to install it; KEEP PLAYING closes the modal and keeps the strip under the header for the rest
/// of the session while the download finishes in the background.
/// </summary>
public static class UpdateOffer
{
    /// <summary>Version the player put off this session (the modal won't come back for it).</summary>
    public static string? DeferredVersion;
    /// <summary>The player pressed UPDATE NOW: restart as soon as the download is verified.</summary>
    public static bool WantsNow;

    public static void Later(string version)
    {
        DeferredVersion = version;
        WantsNow = false;
        Log.Info($"Updates: {version} offered, KEEP PLAYING (the download goes on; the menu strip keeps it for this session)");
    }

    public static void Now(string version)
    {
        WantsNow = true;
        Log.Info($"Updates: {version} offered, UPDATE NOW");
    }
}

/// <summary>Small modal over the menu: "CHECK GITHUB FOR NEW VERSIONS?" with what it does, ENABLE UPDATES / NOT NOW.</summary>
public partial class UpdatesPromptPanel : Control
{
    readonly float k;
    readonly Action<bool> onAnswer;

    public UpdatesPromptPanel(float k, Action<bool> onAnswer)
    {
        this.k = k;
        this.onAnswer = onAnswer;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop; // the menu underneath doesn't get clicks
    }

    public override void _Ready()
    {
        AddChild(new ColorRect { Color = new Color(UiTheme.Bg, 0.88f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });
        Modulate = new Color(1, 1, 1, 0);
        Ready += () => CreateTween().TweenProperty(this, "modulate:a", 1f, 0.25f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var vs = GetViewportRect().Size;
        var panel = new MPanel
        {
            Title = "Updates", Caption = $"CA-VAL-AIM {AppInfo.Version}", K = k,
            CustomMinimumSize = new Vector2(Mathf.Min(780 * k, vs.X - 80 * k), 0),
        };
        center.AddChild(panel);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", (int)(10 * k));
        panel.AddChild(col);

        col.AddChild(new DrawBox
        {
            CustomMinimumSize = new Vector2(0, 58 * k),
            OnDraw = b =>
            {
                int fs = UiTheme.Fs(30, k);
                float y = b.Size.Y - 16 * k;
                Gfx.Text(b, UiTheme.Light, "Check GitHub for new versions?", 0, y, fs, UiTheme.Text);
            },
        });
        col.AddChild(Text(Loc.T("CA-VAL-AIM can keep itself up to date. If you turn this on, it will:"), 16, UiTheme.Dim));
        Bullet(col, "ask github.com for the latest CA-VAL-AIM release, at startup and every 6 hours");
        Bullet(col, "download a newer version in the background and install it when you restart CA-VAL-AIM");
        Bullet(col, "send nothing about you: no settings, stats, recordings or IDs (GitHub sees an ordinary web request)");
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2 * k), MouseFilter = MouseFilterEnum.Ignore });
        col.AddChild(Text(Loc.T("Until you say yes, CA-VAL-AIM makes no network requests. You can change this any time in Settings → About, where CHECK NOW also checks once."), 14, UiTheme.Faint));
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6 * k), MouseFilter = MouseFilterEnum.Ignore });

        var foot = new HBoxContainer();
        foot.AddThemeConstantOverride("separation", (int)(14 * k));
        foot.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        var no = new MLink { Label = "NOT NOW", K = k, Px = 12.5f, Delay = 0.1f, SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = Loc.T("No update checks; you can turn them on later in Settings → About") };
        no.Pressed += () => onAnswer(false);
        foot.AddChild(no);
        var yes = new MAction { Label = "ENABLE UPDATES", Primary = true, K = k, Px = 14f, Delay = 0.15f, CustomMinimumSize = new Vector2(250 * k, 50 * k),
            TooltipText = Loc.T("Check GitHub for new versions and download them in the background") };
        yes.Pressed += () => onAnswer(true);
        foot.AddChild(yes);
        col.AddChild(foot);

        // Dev: "--updates-answer yes|no" presses ENABLE UPDATES / NOT NOW after 2 s (automated tests of the prompt)
        if (CmdLine.DevAfter("--updates-answer")?.ToLowerInvariant() is { } ans)
            GetTree().CreateTimer(2.0).Timeout += () =>
            {
                if (!IsInstanceValid(this) || !IsInsideTree()) return;
                Log.Info($"Updates: --updates-answer: {(ans == "yes" ? "ENABLE UPDATES" : "NOT NOW")}");
                onAnswer(ans == "yes");
            };
    }

    Label Text(string s, float px, Color c) => new()
    {
        Text = s, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill,
        LabelSettings = UiTheme.Label(UiTheme.Body, px, k, c), MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(40 * k, 0),
    };

    void Bullet(VBoxContainer col, string s)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", (int)(10 * k));
        row.AddChild(new Control { CustomMinimumSize = new Vector2(12 * k, 0), MouseFilter = MouseFilterEnum.Ignore });
        row.AddChild(new DrawBox
        {
            CustomMinimumSize = new Vector2(8 * k, 22 * k),
            OnDraw = b => Gfx.Diamond(b, new Vector2(4 * k, 12 * k), 3.5f * k, 3.5f * k, UiTheme.Accent),
        });
        row.AddChild(Text(Loc.T(s), 16, UiTheme.Text));
        col.AddChild(row);
    }

    /// <summary>Esc = NOT NOW (never a yes by accident).</summary>
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            onAnswer(false);
        }
    }
}

/// <summary>
/// The startup "a newer version is out" modal: version, what the update does and UPDATE NOW / KEEP PLAYING. It follows
/// the real <see cref="Updater"/> state live (downloading with a progress line, downloaded and verified, restarting), so
/// UPDATE NOW restarts on its own as soon as the file is verified, while KEEP PLAYING closes the modal and lets the
/// download finish in the background (the strip under the menu header offers the restart). Esc = KEEP PLAYING.
/// </summary>
public partial class UpdateOfferPanel : Control
{
    readonly float k;
    readonly string ver;
    readonly Action onClose;

    Label? state;
    DrawBox? bar;
    MLink? laterLink;
    MAction? goButton;
    float shown = -1, t;
    bool done, restarting;

    static bool Es => Loc.Spanish;
    static string S(string en, string es) => Es ? es : en;

    public UpdateOfferPanel(float k, string version, Action onClose)
    {
        this.k = k;
        ver = version;
        this.onClose = onClose;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop; // the menu underneath doesn't get clicks
    }

    public override void _Ready()
    {
        AddChild(new ColorRect { Color = new Color(UiTheme.Bg, 0.88f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });
        Modulate = new Color(1, 1, 1, 0);
        Ready += () => CreateTween().TweenProperty(this, "modulate:a", 1f, 0.25f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var vs = GetViewportRect().Size;
        var panel = new MPanel
        {
            Title = Loc.T("Update available"), Caption = $"CA-VAL-AIM {AppInfo.Version}", K = k,
            CustomMinimumSize = new Vector2(Mathf.Min(760 * k, vs.X - 80 * k), 0),
        };
        center.AddChild(panel);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", (int)(10 * k));
        panel.AddChild(col);

        col.AddChild(new DrawBox
        {
            CustomMinimumSize = new Vector2(0, 52 * k),
            OnDraw = b =>
            {
                int fs = UiTheme.Fs(28, k);
                Gfx.Text(b, UiTheme.Light, S($"Version {ver} is available", $"Ya está disponible la versión {ver}"),
                    0, b.Size.Y - 12 * k, fs, UiTheme.Text);
            },
        });
        col.AddChild(Text(S($"You have {AppInfo.Version}. The update installs in place: settings, stats, maps and recordings stay.",
                             $"Tu versión es la {AppInfo.Version}. La actualización se instala encima: ajustes, estadísticas, mapas y grabaciones se conservan."),
            15.5f, UiTheme.Dim));
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2 * k), MouseFilter = MouseFilterEnum.Ignore });
        Bullet(col, "Downloads and installs the new version in place.");
        Bullet(col, "CA-VAL-AIM closes for a few seconds and opens again on the new version.");
        Bullet(col, "KEEP PLAYING closes this and lets you train; the strip under the header shows the progress and offers the restart.");
        if (Updater.NeedsElevation)
            col.AddChild(Text(Loc.T("Windows will ask for administrator permission to install it."), 14, UiTheme.Faint));
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4 * k), MouseFilter = MouseFilterEnum.Ignore });

        state = Text("", 14, UiTheme.Faint);
        col.AddChild(state);
        bar = new DrawBox
        {
            CustomMinimumSize = new Vector2(0, 3 * k),
            Visible = false,
            OnDraw = b =>
            {
                float w = b.Size.X * Mathf.Clamp(shown, 0f, 1f);
                b.DrawRect(new Rect2(0, 0, b.Size.X, Mathf.Max(1, k)), Mn.A(UiTheme.Text, 0.12f));
                b.DrawRect(new Rect2(0, 0, w, Mathf.Max(1, k)), UiTheme.Accent);
            },
        };
        col.AddChild(bar);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6 * k), MouseFilter = MouseFilterEnum.Ignore });

        var foot = new HBoxContainer();
        foot.AddThemeConstantOverride("separation", (int)(20 * k));
        string? url = UpdateCheck.LatestUrl ?? (AppInfo.HasRepo ? AppInfo.LatestReleaseUrl : null);
        if (url != null && AppInfo.HasRepo)
        {
            var whats = new MLink { Label = Loc.T("WHAT'S NEW"), K = k, Px = 12.5f, Delay = 0.1f, SizeFlagsVertical = SizeFlags.ShrinkCenter,
                TooltipText = Loc.T("Opens the release notes in your browser") };
            whats.Pressed += () => AppInfo.OpenGitHub(url);
            foot.AddChild(whats);
        }
        foot.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        laterLink = new MLink { Label = S("KEEP PLAYING", "SEGUIR JUGANDO"), K = k, Px = 12.5f, Delay = 0.12f, SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = S("Closes this and lets you train; the strip under the header keeps showing the update and offers the restart",
                            "Cierra el aviso y te deja entrenar; la franja superior sigue mostrando la actualización y ofrece el reinicio") };
        laterLink.Pressed += Later;
        foot.AddChild(laterLink);
        goButton = new MAction { Label = Loc.T("UPDATE NOW"), Primary = true, K = k, Px = 14f, Delay = 0.16f,
            CustomMinimumSize = new Vector2(250 * k, 50 * k), TooltipText = Loc.T("Downloads, verifies and installs the update") };
        goButton.Pressed += Now;
        foot.AddChild(goButton);
        col.AddChild(foot);

        Refresh();
        // Dev: "--update-offer-answer now|keep" presses one of the buttons after 2 s (automated tests of this modal).
        if (CmdLine.DevAfter("--update-offer-answer")?.ToLowerInvariant() is { } ans)
            GetTree().CreateTimer(2.0).Timeout += () =>
            {
                if (!IsInstanceValid(this) || !IsInsideTree() || done) return;
                bool keep = ans is "keep" or "play" or "later";
                Log.Info($"Updates: --update-offer-answer: {(keep ? "KEEP PLAYING" : "UPDATE NOW")}");
                if (keep) Later(); else Now();
            };
    }

    public override void _Process(double delta)
    {
        t += (float)delta;
        Refresh();
        // Once the player said UPDATE NOW, restart as soon as the verified file is ready.
        if (!restarting && UpdateOffer.WantsNow && !done && Updater.ReadyVersion == ver) Apply();
    }

    bool downloading => Updater.Status == Updater.State.Downloading && Updater.Target == ver;
    bool ready => Updater.ReadyVersion == ver;
    bool applying => Updater.Status == Updater.State.Applying && Updater.Target == ver;
    bool failed => Updater.Status == Updater.State.Failed && Updater.Target == ver;

    /// <summary>Redraws the state line / progress / primary label when the updater moved on.</summary>
    void Refresh()
    {
        if (state == null || bar == null || goButton == null || laterLink == null || done) return;
        string text;
        bool hasBar = false;
        float p = shown;
        if (applying) text = S("Restarting CA-VAL-AIM…", "Reiniciando CA-VAL-AIM…");
        else if (ready) text = S("Downloaded and verified. Restart to finish.", "Descargada y verificada. Reinicia para terminar.");
        else if (downloading)
        {
            p = Updater.Progress;
            text = S($"Downloading… {Mathf.RoundToInt(p * 100)}%", $"Descargando… {Mathf.RoundToInt(p * 100)}%");
            hasBar = true;
        }
        else if (failed)
            text = S("The automatic download failed: get the installer from the release page.",
                     "La descarga automática falló: descarga el instalador desde la página de la versión.");
        else text = S("Not downloaded yet.", "Aún no se ha descargado.");
        if (state.Text != text) state.Text = text;
        if (bar.Visible != hasBar) bar.Visible = hasBar;
        if (hasBar && Mathf.Abs(p - shown) > 0.005f) { shown = p; bar.QueueRedraw(); }

        string label = applying ? S("RESTARTING…", "REINICIANDO…")
            : ready ? Loc.T("RESTART NOW")
            : downloading ? S($"UPDATING… {Mathf.RoundToInt(Updater.Progress * 100)}%", $"ACTUALIZANDO… {Mathf.RoundToInt(Updater.Progress * 100)}%")
            : Loc.T("UPDATE NOW");
        if (goButton.Label != label) { goButton.Label = label; goButton.QueueRedraw(); }
        // Downloading: the primary action has nothing left to do (the download runs on its own), so it shows the
        // progress instead of offering UPDATE NOW again; KEEP PLAYING stays available to go back to the menu.
        bool busy = applying || downloading;
        if (goButton.Disabled != busy) goButton.Disabled = busy;
        if (laterLink.Disabled != applying) laterLink.Disabled = applying;
    }

    void Later()
    {
        if (done || applying) return;
        UpdateOffer.Later(ver);
        Close();
    }

    void Now()
    {
        if (done || applying) return;
        UpdateOffer.Now(ver);
        if (ready) { Apply(); return; }
        if (Updater.UpdateNow()) return; // downloading: _Process restarts when the file is verified
        // This copy can't install it by itself (or the release has no installer): the release page is the manual path.
        string? url = UpdateCheck.LatestUrl ?? (AppInfo.HasRepo ? AppInfo.LatestReleaseUrl : null);
        if (AppInfo.OpenGitHub(url)) { Close(); return; }
        UpdateOffer.WantsNow = false;
        if (state != null)
            state.Text = S("This copy can't install the update by itself. Get the installer from the release page.",
                           "Esta copia no puede instalar la actualización por sí sola. Descarga el instalador desde la página de la versión.");
    }

    void Apply()
    {
        if (restarting) return;
        restarting = true;
        UpdateOffer.WantsNow = false;
        Refresh();
        Log.Info($"Updates: installing {ver} now (RESTART NOW)");
        Updater.RestartToUpdate();
    }

    void Close()
    {
        if (done) return;
        done = true;
        onClose();
        QueueFree();
    }

    Label Text(string s, float px, Color c) => new()
    {
        Text = s, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill,
        LabelSettings = UiTheme.Label(UiTheme.Body, px, k, c), MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(40 * k, 0),
    };

    void Bullet(VBoxContainer col, string s)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", (int)(10 * k));
        row.AddChild(new Control { CustomMinimumSize = new Vector2(12 * k, 0), MouseFilter = MouseFilterEnum.Ignore });
        row.AddChild(new DrawBox
        {
            CustomMinimumSize = new Vector2(8 * k, 22 * k),
            OnDraw = b => Gfx.Diamond(b, new Vector2(4 * k, 12 * k), 3.5f * k, 3.5f * k, UiTheme.Accent),
        });
        row.AddChild(Text(Loc.T(s), 16, UiTheme.Text));
        col.AddChild(row);
    }

    /// <summary>Esc = KEEP PLAYING (never starts an update by accident).</summary>
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            Later();
        }
    }
}
