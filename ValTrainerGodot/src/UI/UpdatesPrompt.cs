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
        AddChild(new ColorRect { Color = new Color(0.02f, 0.04f, 0.06f, 0.72f), MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var vs = GetViewportRect().Size;
        var panel = new VPanel
        {
            Title = "UPDATES", Caption = $"CA-VAL-AIM {AppInfo.Version}", K = k, Fill = new Color(0.07f, 0.11f, 0.15f, 0.97f),
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
                int fs = UiTheme.Fs(40, k);
                float y = b.Size.Y - 14 * k;
                const string q1 = "CHECK GITHUB FOR ", q2 = "NEW VERSIONS?";
                Gfx.Text(b, UiTheme.Display, q1, 0, y, fs, UiTheme.Text);
                Gfx.Text(b, UiTheme.Display, q2, Gfx.TextW(UiTheme.Display, q1, fs), y, fs, UiTheme.Accent);
            },
        });
        col.AddChild(Text("CA-VAL-AIM can keep itself up to date. If you turn this on, it will:", 16, UiTheme.Dim));
        Bullet(col, "ask github.com for the latest CA-VAL-AIM release, at startup and every 6 hours");
        Bullet(col, "download a newer version in the background and install it when you restart CA-VAL-AIM");
        Bullet(col, "send nothing about you: no settings, stats, recordings or IDs (GitHub sees an ordinary web request)");
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2 * k), MouseFilter = MouseFilterEnum.Ignore });
        col.AddChild(Text("Until you say yes, CA-VAL-AIM makes no network requests. You can change this any time in Settings → About, where CHECK NOW also checks once.", 14, UiTheme.Faint));
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6 * k), MouseFilter = MouseFilterEnum.Ignore });

        var foot = new HBoxContainer();
        foot.AddThemeConstantOverride("separation", (int)(14 * k));
        foot.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        var no = new VButton { Label = "NOT NOW", Kind = VButton.Look.Ghost, K = k, FontPx = 18, CustomMinimumSize = new Vector2(150 * k, 50 * k),
            TooltipText = "No update checks; you can turn them on later in Settings → About" };
        no.Pressed += () => onAnswer(false);
        foot.AddChild(no);
        var yes = new VButton { Label = "ENABLE UPDATES", Kind = VButton.Look.Primary, K = k, FontPx = 20, CustomMinimumSize = new Vector2(230 * k, 50 * k),
            TooltipText = "Check GitHub for new versions and download them in the background" };
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
        row.AddChild(Text(s, 16, UiTheme.Text));
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
