using System.Collections.Generic;
using System.Globalization;
using Godot;
using CaValAim.Analysis;
using CaValAim.Audio;
using CaValAim.Core;
using CaValAim.Game;
using CaValAim.Maps;
using CaValAim.Modes;
using CaValAim.Valorant;

namespace CaValAim.UI;

/// <summary>
/// Shows the session's live metrics, drill cues, and pause and results interfaces.
/// </summary>
public partial class Hud : Control
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    readonly GameSession s;
    public Hud(GameSession session) => s = session;

    // overlay (pause / results) state
    GameSession.St lastState = (GameSession.St)(-1);
    TrainingMode? lastMode;
    DrawBox? overlay;
    Vector2 overlaySize;
    SettingsScreen? pauseOptions;
    LineEdit? sensEdit;
    float overlayAt;
    int locRev = Loc.Revision;

    // aim coach: cue shown during the countdown (from a "Practise" button or the last run's review)
    CoachData.Focus? focus, nextFocus;

    // animation bookkeeping (real time, so it keeps moving while paused)
    float lastStopSeen = -2f, stopPopAt = -99f;

    // player-centred minimap: a clipped square canvas so the rotated art never paints outside it
    Control? minimapFrame;
    MinimapCanvas? minimap;
    readonly List<Vector3> minimapSpots = new();
    readonly Dictionary<int, float> seenAt = new();

    // tiny string caches so steady-state frames don't allocate
    sealed class Cache
    {
        long key = long.MinValue;
        string val = "";
        public string Get(long k, Func<long, string> f)
        {
            if (k != key) { key = k; val = f(k); }
            return val;
        }
    }
    readonly Cache cClock = new(), cScore = new(), cRight = new(), cRightLabel = new(), cFps = new(), cHp = new(), cShield = new(),
        cAmmo = new(), cReserve = new(), cSpeed = new(), cStop = new(), cTier = new(), cCount = new();

    static float RealNow => Time.GetTicksMsec() / 1000f;

    static string L(string en, string es) => Loc.Spanish ? es : en;

    /// <summary>Every button under an overlay node, last child first, so a pop-up added on top wins over the page behind it.</summary>
    static IEnumerable<BaseButton> ButtonsDeep(Node n)
    {
        var kids = n.GetChildren();
        for (int i = kids.Count - 1; i >= 0; i--)
        {
            var c = kids[i];
            if (c is Control { Visible: false }) continue;
            if (c is BaseButton b) yield return b;
            foreach (var x in ButtonsDeep(c)) yield return x;
        }
    }

    // Cached labels for live rendering.
    string modeTitle = "", subSrc = "", subUpper = "";

    string Upper(ref string src, ref string upper, string value)
    {
        if (!ReferenceEquals(src, value) && src != value) { src = value; upper = value.ToUpperInvariant(); }
        return upper;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        // The minimap rotates with the view, so it lives in its own clipping frame: the rotated art would otherwise
        // paint outside the square. Godot clips children, not the control itself, hence the wrapper.
        minimapFrame = new Control { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true, Visible = false };
        AddChild(minimapFrame);
        minimap = new MinimapCanvas(this) { MouseFilter = MouseFilterEnum.Ignore };
        minimapFrame.AddChild(minimap);
        minimap.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
        if (s.Mode == null) return;
        var size = GetViewportRect().Size;
        float hudK = UiTheme.S(size);
        if (minimapFrame != null && minimap != null)
        {
            bool show = s.Mode.Map != null && !s.Mode.Is2D && s.State == GameSession.St.Running;
            minimapFrame.Visible = show;
            if (show)
            {
                float w = Mathf.Min(230 * hudK, size.X * 0.24f);
                minimapFrame.Position = new Vector2(size.X - 26 * hudK - w, 50 * hudK);
                minimapFrame.Size = new Vector2(w, w);
                minimap.QueueRedraw();
            }
        }
        if (locRev != Loc.Revision)
        {
            locRev = Loc.Revision;
            modeTitle = Loc.T(s.Mode.Name);
            if (overlay != null) RebuildOverlay(size, keepTime: true);
        }
        if (s.State != lastState || s.Mode != lastMode || (overlay != null && size != overlaySize))
        {
            bool modeChanged = s.Mode != lastMode;
            lastState = s.State;
            lastMode = s.Mode;
            if (modeChanged)
            {
                lastStopSeen = -2f;
                modeTitle = Loc.T(s.Mode.Name);
                focus = CoachData.TakeFocus(s.Mode.Key) ?? nextFocus;
                nextFocus = null;
            }
            RebuildOverlay(size);
        }
        // Rewind insights finished building: re-lay the results once so the review can show its "Show me" links.
        if (insightsPending && overlay != null && s.State == GameSession.St.Results && RewindData.Ready(s.Telemetry) is { } ri)
        {
            insightsPending = false;
            var rv = CoachData.ReviewFor(s);
            if (rv != null && rv.Issues.Any(i => ri.Moments.Any(m => m.Code == i.Id))) RebuildOverlay(size, keepTime: true);
        }
        if (overlay != null)
        {
            overlay.QueueRedraw();
            overlay.Modulate = new Color(1, 1, 1, Gfx.EaseOut((RealNow - overlayAt) / 0.2f));
        }
    }

    /// <summary>
    /// Overlay buttons are activated here, before <see cref="GameSession"/> sees the click: otherwise the press would
    /// leave a pending "fire" edge that shoots on resume. Presses are swallowed while an overlay is up.
    /// </summary>
    public override void _Input(InputEvent e)
    {
        if (overlay == null || pauseOptions != null || s.State != GameSession.St.Paused) return;
        bool typing = sensEdit != null && IsInstanceValid(sensEdit) && sensEdit.HasFocus();
        // Esc while typing a sensitivity: stop typing (don't resume the drill).
        if (typing && e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            sensEdit!.Text = Main.I.Sens.ToString("0.000", Inv);
            sensEdit.ReleaseFocus();
            return;
        }
        if (e is InputEventMouseButton { Pressed: true } mb && mb.ButtonIndex is MouseButton.Left or MouseButton.Right)
        {
            GetViewport().SetInputAsHandled();
            if (mb.ButtonIndex != MouseButton.Left) return;
            // The sensitivity box: clicks are swallowed above (no fire edge on resume), so focus it by hand.
            if (sensEdit != null && IsInstanceValid(sensEdit) && sensEdit.Editable &&
                sensEdit.IsVisibleInTree() && sensEdit.GetGlobalRect().HasPoint(mb.Position))
            {
                if (!sensEdit.HasFocus()) { sensEdit.GrabFocus(); sensEdit.SelectAll(); sensEdit.CaretColumn = sensEdit.Text.Length; }
                return;
            }
            if (typing) sensEdit!.ReleaseFocus(); // clicking anywhere else applies what was typed
            // The weapon picker is a full-screen pop-up: only its own tiles may take the click, never the pause controls
            // behind the scrim. Clicks outside it close it (the picker handles that itself).
            MWeaponPicker? picker = null;
            foreach (var c in overlay.GetChildren())
                if (c is MWeaponPicker { Visible: true } p) { picker = p; break; }
            if (picker != null)
            {
                foreach (var b in ButtonsDeep(picker))
                    if (!b.Disabled && b.IsVisibleInTree() && b.GetGlobalRect().HasPoint(mb.Position))
                    {
                        b.EmitSignal(BaseButton.SignalName.Pressed);
                        return;
                    }
                return;
            }
            // The overlay swallows the press so the session never sees a stale fire edge: dispatch it to the button under
            // the pointer by hand (the minimal widgets are all BaseButtons, and pop-ups are deeper in the tree).
            foreach (var b in ButtonsDeep(overlay))
                if (!b.Disabled && b.IsVisibleInTree() && b.GetGlobalRect().HasPoint(mb.Position))
                {
                    b.EmitSignal(BaseButton.SignalName.Pressed);
                    break;
                }
        }
    }

    // =====================================================================================
    // In-game HUD
    // =====================================================================================

    public override void _Draw()
    {
        var m = s.Mode;
        if (m == null) return;
        var size = GetViewportRect().Size;
        float k = UiTheme.S(size);

        if (m.Is2D) m.Draw2D(this, size);
        if (s.State == GameSession.St.Results) return;
        if (!m.Is2D) m.Draw2D(this, size); // 3D drills can add their own overlay (e.g. the spray chart)

        if (!m.Is2D) DrawDamage(size, k);
        bool countdown = s.State == GameSession.St.Countdown;
        if (!countdown)
        {
            DrawTopBar(size, k, m);
            DrawModeInfo(size, k, m);
        }
        else DrawCountdownBackdrop(size);
        if (Main.I.Valorant.ShowFps)
        {
            int fps = (int)Engine.GetFramesPerSecond();
            int fs = UiTheme.Fs(16, k);
            TR(UiTheme.Hud, cFps.Get(fps, v => $"{v} FPS"), size.X - 24 * k, 30 * k, fs, new Color(UiTheme.Text, 0.85f));
        }
        if (!m.Is2D && m.Map is { } mapSpot)
        {
            if (s.State == GameSession.St.Countdown || (s.State == GameSession.St.Running && s.Now < 3f))
                DrawSpotCallouts(size, k, mapSpot);
            if (s.State == GameSession.St.Countdown) DrawBriefing(size, k, mapSpot);
        }
        if (s.State == GameSession.St.Running && s.GuideMode >= 1) DrawGuide(size, k, m);
        if (!m.Is2D)
        {
            if (m.ShowScore) DrawVitals(size, k);
            if (s.Gun != null && !m.EffectiveInfiniteAmmo) DrawAmmo(size, k);
            if (m.Movement) DrawSpeed(size, k);
            DrawReload(size, k);
        }
        if ((m.ShowInstructions ? m.Prompt ?? m.StatusPrompt ?? s.WorldPrompt : m.StatusPrompt ?? s.WorldPrompt) is { } p) DrawPrompt(size, k, Main.I.Valorant.Binds.Hint(p));
        if (s.BannerTime > 0 && s.BannerText.Length > 0) DrawBanner(size, k);
        // Measured feedback is optional and stays away from the crosshair; task instructions remain countdown-only.
        if (s.State == GameSession.St.Running)
        {
            if (s.SensSearch is { Active: true } search) DrawSensitivitySearch(size, k, search);
            else if (Main.I.Settings.LiveCoach && s.Live?.Current is { } cue) DrawMeasuredCoach(size, k, cue);
        }
        if (m.KillBanner.Time > 0 && m.KillBanner.Count > 0) DrawKillBanner(size, k, m.KillBanner.Count, m.KillBanner.Time);
        if (countdown) DrawCountdown(size, k, m);
        if (focus != null && m.ShowInstructions) DrawFocus(size, k, focus);
    }

    readonly Cache cGuide = new();

    /// <summary>
    /// Guide line (Settings → Guide line, GuideSession.cs): the straight crosshair path to the target the drill is focused on
    /// (always the one nearest the crosshair), with a ring around it and the flick distance in degrees. A target off screen
    /// or behind you gets an arrow on the screen edge instead. It stays faint and stops short of the target so it never
    /// hides what you are aiming at.
    /// </summary>
    void DrawGuide(Vector2 size, float k, TrainingMode m)
    {
        if (m.Is2D)
        {
            if (m.AimGuide2D(size) is not { } aim) return;
            DrawGuideTarget(size / 2, aim.Point, aim.Radius, aim.Point.DistanceTo(size / 2) * 70 / size.Y, k);
            return;
        }
        if (m.Focus is not { } f) return;
        var cam = GetViewport()?.GetCamera3D();
        if (cam == null) return;
        var c = size / 2;
        var col = UiTheme.Teal;
        bool behind = cam.IsPositionBehind(f.Point);
        var p = cam.UnprojectPosition(f.Point);
        if (behind) p = c + (c - p) * 64f; // the projection of a point behind the camera is mirrored through the centre
        var bounds = new Rect2(Vector2.Zero, size).Grow(-44 * k);
        if (behind || !bounds.HasPoint(p))
        {
            var dir = (p - c).Normalized();
            if (!dir.IsFinite() || dir == Vector2.Zero) return;
            // Where the ray from the centre leaves the inset screen rectangle.
            float tx = dir.X > 0 ? (bounds.End.X - c.X) / dir.X : dir.X < 0 ? (bounds.Position.X - c.X) / dir.X : float.MaxValue;
            float ty = dir.Y > 0 ? (bounds.End.Y - c.Y) / dir.Y : dir.Y < 0 ? (bounds.Position.Y - c.Y) / dir.Y : float.MaxValue;
            var at = c + dir * Mathf.Min(tx, ty);
            var side = new Vector2(-dir.Y, dir.X);
            float a = 0.55f + 0.3f * Mathf.Sin(RealNow * 6f);
            DrawColoredPolygon(new[] { at + dir * 16 * k, at - dir * 6 * k + side * 13 * k, at - dir * 6 * k - side * 13 * k },
                new Color(col, a));
            return;
        }
        // Target radius on screen: project a point one radius to the side of its centre.
        var edge = cam.UnprojectPosition(f.Point + cam.GlobalBasis.X * Mathf.Max(0.02f, f.Radius));
        float rpx = Mathf.Max(5 * k, edge.DistanceTo(p));
        float degrees = Mathf.RadToDeg((-cam.GlobalBasis.Z).AngleTo((f.Point - cam.GlobalPosition).Normalized()));
        DrawGuideTarget(c, p, rpx, degrees, k);
    }

    void DrawGuideTarget(Vector2 c, Vector2 p, float rpx, float deg, float k)
    {
        var col = UiTheme.Teal;
        float dist = c.DistanceTo(p);
        float ring = rpx + 4 * k;
        var lineCol = new Color(col, 0.55f);
        if (dist > ring + 18 * k)
        {
            var dir = (p - c) / dist;
            DrawDashedLine(c + dir * 16 * k, p - dir * ring, lineCol, Mathf.Max(1.5f, 2f * k), 9 * k, true, true);
        }
        DrawArc(p, ring, 0, Mathf.Tau, 40, new Color(col, dist <= ring ? 0.35f : 0.75f), Mathf.Max(1.5f, 2f * k), true);
        if (dist > ring + 40 * k)
        {
            // Angular distance from the crosshair (the length of the flick the line describes).
            int fs = UiTheme.Fs(13, k);
            TL(UiTheme.HudWide, cGuide.Get((long)MathF.Round(deg), v => $"{v}°"), p.X + ring + 5 * k, p.Y - ring * 0.4f, fs, new Color(UiTheme.Text, 0.8f));
        }
    }

    /// <summary>"COACH FOCUS" plate: the problem this run should work on and its first fix cue (fades out after the start).</summary>
    void DrawFocus(Vector2 size, float k, CoachData.Focus f)
    {
        float a = s.State == GameSession.St.Running ? Mathf.Clamp(1f - s.Now / 2f, 0, 1) : Mathf.Clamp((3f - s.Countdown) / 0.3f, 0, 1);
        if (a <= 0.01f) return;
        float cx = size.X / 2, cy = size.Y * 0.74f;
        int cs = UiTheme.Fs(12, k), ts = UiTheme.Fs(25, k), qs = UiTheme.Fs(18, k);
        float w = Mathf.Max(Gfx.TextW(UiTheme.Display, f.Title, ts), Gfx.TextW(UiTheme.Body, f.Cue, qs)) + 90 * k;
        w = Mathf.Min(w, size.X * 0.72f);
        float h = f.Cue.Length > 0 ? 100 * k : 72 * k;
        var r = new Rect2(cx - w / 2, cy - h / 2, w, h);
        DrawRect(r, new Color(0.03f, 0.05f, 0.08f, 0.7f * a));
        DrawRect(new Rect2(r.Position, new Vector2(4 * k, h)), new Color(UiTheme.Accent, a));
        Gfx.Brackets(this, r, 10 * k, new Color(UiTheme.Text, 0.35f * a), Mathf.Max(1, 2 * k));
        float y = r.Position.Y;
        TC(UiTheme.HudWide, "COACH FOCUS", cx, Gfx.Mid(y + 20 * k, cs), cs, new Color(UiTheme.Accent, a));
        float inner = w - 50 * k;
        Gfx.TextFit(this, UiTheme.Display, f.Title, cx - inner / 2, Gfx.Mid(y + 48 * k, ts), ts, new Color(UiTheme.Text, a), inner, HorizontalAlignment.Center);
        if (f.Cue.Length > 0)
            Gfx.TextFit(this, UiTheme.Body, f.Cue, cx - inner / 2, Gfx.Mid(y + 78 * k, qs), qs, new Color(UiTheme.Text, 0.85f * a), inner, HorizontalAlignment.Center);
    }

    void DrawTopBar(Vector2 size, float k, TrainingMode m)
    {
        float cx = size.X / 2, top = 20 * k;
        var plate = new Color(0.045f, 0.063f, 0.075f, 0.9f);
        int small = UiTheme.Fs(15, k);
        if (s.FreePlay)
        {
            DrawRect(new Rect2(cx - 134 * k, top, 268 * k, 43 * k), plate);
            string caption = s.CoachSuspended ? L("Warm-up", "Calentamiento")
                : L("Free play", "Práctica libre");
            TC(UiTheme.Body, caption, cx, Gfx.Mid(top + 21 * k, small), small, UiTheme.Text);
            string exit = L("Esc to stop", "Esc para salir");
            if (!s.CoachSuspended) exit = $"{Loc.Score(s.TrainingPoints)} pts  ·  {exit}";
            TC(UiTheme.Body, exit, cx, top + 61 * k, small, UiTheme.Text);
            return;
        }

        float remain = s.ClockSeconds;
        bool timed = s.HasTimeLimit;
        int secs = timed ? Mathf.CeilToInt(remain) : (int)remain;
        bool hurry = timed && remain < 10f && s.State == GameSession.St.Running;
        bool showStats = m.ShowScore && !s.CoachSuspended;
        float half = (showStats ? 164 : 54) * k;
        float height = (showStats ? 60 : 44) * k;
        var clockColor = hurry ? UiTheme.Warn : UiTheme.Text;
        DrawRect(new Rect2(cx - half, top, 2 * half, height), plate);
        int clockSize = UiTheme.Fs(30, k);
        TC(UiTheme.Hud, cClock.Get(secs, v => $"{v / 60}:{v % 60:00}"), cx,
            Gfx.Mid(top + (showStats ? 27 : 21) * k, clockSize), clockSize, clockColor);
        if (timed && s.RunLength > 0)
        {
            float progress = Mathf.Clamp(remain / s.RunLength, 0, 1);
            DrawRect(new Rect2(cx - 31 * k, top + height - 9 * k, 62 * k, 2 * k), new Color(UiTheme.Text, 0.14f));
            DrawRect(new Rect2(cx - 31 * k, top + height - 9 * k, 62 * k * progress, 2 * k), new Color(clockColor, 0.75f));
        }
        if (!s.Demo && s.SensComparisonTrial == null && m.Key != "sensfinder")
        {
            string points = s.CoachSuspended ? L("Warm-up", "Calentamiento")
                : $"{Loc.Score(s.TrainingPoints)} {L("practice pts", "pts de práctica")}";
            TC(UiTheme.Body, points, cx, top + height + 19 * k, small, UiTheme.Text);
        }
        if (!showStats) return;

        int valueSize = UiTheme.Fs(24, k);
        void Stat(float x, string value, string label)
        {
            TC(UiTheme.Hud, value, x, Gfx.Mid(top + 22 * k, valueSize), valueSize, UiTheme.Text);
            TC(UiTheme.Body, label, x, Gfx.Mid(top + 44 * k, small), small, UiTheme.Dim);
        }

        for (int side = -1; side <= 1; side += 2)
            DrawRect(new Rect2(cx + side * 54 * k, top + 12 * k, 1, 36 * k), new Color(UiTheme.Text, 0.13f));
        int accuracy = Mathf.RoundToInt(m.Accuracy * 100);
        string right = m.UsesHeadshots ? cRight.Get(m.Kills, v => v.ToString(Inv))
            : cRight.Get(accuracy + 1000, v => $"{v - 1000}%");
        string label = m.UsesHeadshots ? cRightLabel.Get(2 * accuracy + (Loc.Spanish ? 1 : 0),
            v => (v % 2 == 0 ? "Kills" : "Bajas") + $" · {v / 2}%")
            : L("Accuracy", "Precisión");
        Stat(cx - 110 * k, cScore.Get(m.Score, v => Loc.Score(v)), L("Score", "Puntuación"));
        Stat(cx + 110 * k, right, label);
    }

    void DrawModeInfo(Vector2 size, float k, TrainingMode m)
    {
        float x = 26 * k, y = 34 * k;
        int ns = UiTheme.Fs(23, k);
        TL(UiTheme.Hud, modeTitle, x, Gfx.Mid(y, ns), ns, UiTheme.Text);
        y += 24 * k;
        int ts = UiTheme.Fs(14, k);
        if (m.ShowScore)
            TL(UiTheme.Body, cTier.Get(s.Tier, v => Difficulty.Get((int)v).Name),
                x, Gfx.Mid(y, ts), ts, UiTheme.Text);
        if (m.Subtitle is { } sub)
        {
            y += 24 * k;
            TL(UiTheme.HudWide, Upper(ref subSrc, ref subUpper, sub), x, Gfx.Mid(y, ts), ts, new Color(UiTheme.Text, 0.85f));
        }
        if (PlaylistRunner.TaskCount > 0)
        {
            y += 24 * k;
            string progress = Loc.Spanish
                ? $"TAREA {PlaylistRunner.TaskNumber}/{PlaylistRunner.TaskCount} · REPETICIÓN {PlaylistRunner.RunOfStep}/{PlaylistRunner.RunsInStep}"
                : $"TASK {PlaylistRunner.TaskNumber}/{PlaylistRunner.TaskCount} · RUN {PlaylistRunner.RunOfStep}/{PlaylistRunner.RunsInStep}";
            TL(UiTheme.HudWide, progress, x, Gfx.Mid(y, ts), ts, UiTheme.Teal);
        }
        y += 34 * k;
        int ls = UiTheme.Fs(18, k);
        foreach (var rawLine in m.HudLines())
        {
            var line = Main.I.Valorant.Binds.Hint(rawLine);
            // movement drills show their last stop under the speed meter instead
            if (m.Movement && line.StartsWith("Last stop", StringComparison.Ordinal)) continue;
            float w = Gfx.TextW(UiTheme.Body, line, ls);
            DrawRect(new Rect2(x, y - 14 * k, w + 26 * k, 28 * k), new Color(0.04f, 0.07f, 0.1f, 0.5f));
            DrawRect(new Rect2(x, y - 14 * k, 2 * k, 28 * k), new Color(UiTheme.Accent, 0.8f));
            TL(UiTheme.Body, line, x + 13 * k, Gfx.Mid(y, ls), ls, UiTheme.Text);
            y += 32 * k;
        }
    }

    /// <summary>
    /// The in-game minimap: the map art around the player, rotated with the view (player arrow up), the drilled site
    /// letter and the defenders you have spotted. Drawn by <see cref="MinimapCanvas"/> inside a clipping frame.
    /// Hidden during the countdown, which uses the bigger <see cref="DrawBriefing"/> instead.
    /// </summary>
    void DrawMinimapArt(CanvasItem ci, Rect2 rect)
    {
        if (s.Mode?.Map is not { } map) return;
        float k = UiTheme.S(GetViewportRect().Size);
        minimapSpots.Clear();
        for (int i = 0; i < map.Enemies.Length; i++)
        {
            var e = map.Enemies[i];
            var head = e.Feet + new Vector3(0, e.Stance == Stance.Crouch ? 0.85f : 1.4f, 0);
            if (s.LineOfSight(s.View.Eye, head)) seenAt[i] = RealNow;
            // VALORANT keeps a spotted defender on the map for a moment after he breaks the sightline.
            if (seenAt.TryGetValue(i, out float t) && RealNow - t < 1.2f) minimapSpots.Add(e.Feet);
        }
        foreach (var stale in new List<int>(seenAt.Keys))
            if (RealNow - seenAt[stale] >= 1.2f) seenAt.Remove(stale);
        MapArt.Minimap(ci, rect, map, k, s.View.Eye, s.View.ViewYaw, 0.94f, minimapSpots, rotate: true);
    }

    sealed partial class MinimapCanvas : Control
    {
        readonly Hud h;
        public MinimapCanvas(Hud hud) => h = hud;
        public override void _Draw() => h.DrawMinimapArt(this, new Rect2(Vector2.Zero, Size));
    }

    /// <summary>Countdown briefing: the official minimap with every defender callout marked, plus the approach.</summary>
    void DrawBriefing(Vector2 size, float k, MapSpot map)
    {
        float w = Mathf.Min(size.Y * 0.54f, size.X * 0.34f);
        var r = new Rect2(size.X - 40 * k - w, (size.Y - w) / 2, w, w);
        MapArt.Panel(this, r, map, k, callouts: true, player: false, s.View.Eye, s.View.ViewYaw, 1f);
        int ts = UiTheme.Fs(22, k), ss = UiTheme.Fs(13, k);
        RawR(UiTheme.Display, map.Map.ToUpperInvariant(), r.End.X, r.Position.Y - 16 * k, ts, UiTheme.Text);
        RawR(UiTheme.HudWide, $"{MapArt.StartName(map.Key)} → {MapArt.SiteName(map.Key)}", r.End.X, r.End.Y + 20 * k, ss,
            new Color(UiTheme.Teal, 0.95f));
    }

    /// <summary>World-space callout tags over the defender spots (countdown + the first seconds of the run).</summary>
    void DrawSpotCallouts(Vector2 size, float k, MapSpot map)
    {
        var cam = GetViewport()?.GetCamera3D();
        if (cam == null) return;
        float alpha = s.State == GameSession.St.Countdown ? 0.95f : Mathf.Clamp(1f - s.Now / 3f, 0, 1) * 0.95f;
        if (alpha <= 0.02f) return;

        var spots = new List<(string Name, Vector3 At)>();
        foreach (var e in map.Enemies)
            spots.Add((e.Name, e.Feet + new Vector3(0, e.Stance == Stance.Crouch ? 0.85f : 1.4f, 0)));
        spots.Add((MapArt.StartName(map.Key), map.StartFeet + new Vector3(0, 1.7f, 0)));
        spots.Add((MapArt.SiteName(map.Key), map.Choke + new Vector3(0, 1.9f, 0)));

        var eye = s.View.Eye;
        spots.Sort((a, b) => eye.DistanceSquaredTo(a.At).CompareTo(eye.DistanceSquaredTo(b.At)));
        bool briefing = s.State == GameSession.St.Countdown;
        int fs = UiTheme.Fs(13, k);
        var used = new List<Vector2>();
        var bounds = new Rect2(Vector2.Zero, size).Grow(-24 * k);
        foreach (var (name, at) in spots)
        {
            // Briefing: every callout, with the ones behind walls dimmed. Running: only visible spots.
            bool los = s.LineOfSight(eye, at);
            if (!briefing && !los) continue;
            float a = los ? alpha : alpha * 0.42f;
            if (cam.IsPositionBehind(at)) continue;
            var p = cam.UnprojectPosition(at);
            if (!bounds.HasPoint(p)) continue;
            bool clash = false;
            foreach (var u in used) if (u.DistanceTo(p) < 58 * k) { clash = true; break; }
            if (clash) continue;
            used.Add(p);
            Gfx.Diamond(this, p, 3.5f * k, 3.5f * k, new Color(UiTheme.Teal, a));
            string text = name.ToUpperInvariant();
            var pos = p + new Vector2(7 * k, 4 * k);
            DrawStringOutline(UiTheme.HudWide, pos, text, HorizontalAlignment.Left, -1, fs, Mathf.Max(1, fs / 7),
                new Color(0.02f, 0.03f, 0.05f, 0.85f * a));
            DrawString(UiTheme.HudWide, pos, text, HorizontalAlignment.Left, -1, fs, new Color(UiTheme.Text, a));
        }
    }

    void DrawVitals(Vector2 size, float k)
    {
        float hp = Mathf.Max(0, s.Player.Hp), shield = Mathf.Max(0, s.Player.Shield);
        var healthColor = hp < 30 ? UiTheme.Warn : UiTheme.Text;
        var shieldColor = new Color(0.66f, 0.85f, 0.91f);
        float x = 37 * k, y = size.Y - 37 * k;
        int large = UiTheme.Fs(44, k), medium = UiTheme.Fs(28, k), small = UiTheme.Fs(15, k);
        DrawBottomScrim(size, k, left: true);
        TL(UiTheme.Body, L("HEALTH", "VIDA"), x, y - 46 * k, small, UiTheme.Text);
        TL(UiTheme.Hud, cHp.Get(Mathf.CeilToInt(hp), v => v.ToString(Inv)), x, y, large, healthColor);
        DrawLine(new Vector2(x + 97 * k, y - 34 * k), new Vector2(x + 97 * k, y),
            new Color(UiTheme.Text, 0.26f), Mathf.Max(1, k));
        TL(UiTheme.Body, L("SHIELD", "ESCUDO"), x + 115 * k, y - 46 * k, small, shieldColor);
        TL(UiTheme.Hud, cShield.Get(Mathf.CeilToInt(shield), v => v.ToString(Inv)), x + 115 * k, y,
            medium, shield > 0 ? shieldColor : UiTheme.Dim);
        DrawStatusLine(x, y + 9 * k, 85 * k, k, hp / 100f, healthColor);
        DrawStatusLine(x + 115 * k, y + 9 * k, 66 * k, k, shield / 50f, shieldColor);
    }

    void DrawAmmo(Vector2 size, float k)
    {
        var gun = s.Gun!;
        float right = size.X - 37 * k, y = size.Y - 37 * k;
        int large = UiTheme.Fs(44, k), small = UiTheme.Fs(16, k);
        bool low = gun.Ammo <= Math.Max(1, gun.Def.Mag / 5);
        var amountColor = gun.Reloading ? UiTheme.Dim : low ? UiTheme.Warn : UiTheme.Text;
        DrawBottomScrim(size, k, left: false);
        TR(UiTheme.Body, gun.Def.Name.ToUpperInvariant(), right, y - 46 * k, small, UiTheme.Text);
        string reserve = cReserve.Get(gun.Reserve, v => $"/ {v}");
        float width = TR(UiTheme.Body, reserve, right, y, UiTheme.Fs(24, k), UiTheme.Text);
        TR(UiTheme.Hud, cAmmo.Get(gun.Ammo, v => v.ToString(Inv)), right - width - 9 * k, y, large, amountColor);
        DrawStatusLine(right - 116 * k, y + 9 * k, 116 * k, k,
            gun.Ammo / (float)Math.Max(1, gun.Def.Mag), amountColor);
    }

    void DrawStatusLine(float x, float y, float width, float k, float progress, Color color)
    {
        float line = Mathf.Max(1, 2 * k);
        DrawRect(new Rect2(x, y, width, line), new Color(UiTheme.Text, 0.23f));
        DrawRect(new Rect2(x, y, width * Mathf.Clamp(progress, 0, 1), line), color);
    }

    void DrawBottomScrim(Vector2 size, float k, bool left)
    {
        float edge = left ? 0 : size.X;
        float inner = left ? 340 * k : size.X - 300 * k;
        Span<Vector2> points = stackalloc Vector2[]
        {
            new(edge, size.Y - 152 * k), new(inner, size.Y - 152 * k),
            new(inner, size.Y), new(edge, size.Y)
        };
        var end = new Color(0.025f, 0.036f, 0.042f, 0.74f);
        Span<Color> colors = stackalloc Color[] { new(end, 0), new(end, 0), new(end, 0), end };
        this.GradPoly(points, colors);
    }

    void DrawReload(Vector2 size, float k)
    {
        var gun = s.Gun;
        if (gun == null || !gun.Reloading) return;
        var c = size / 2;
        float p = Mathf.Clamp(1f - gun.ReloadLeft / Mathf.Max(0.01f, gun.Def.Reload), 0, 1);
        int fs = UiTheme.Fs(14, k);
        TC(UiTheme.HudWide, "RELOADING", c.X, c.Y + 72 * k, fs, UiTheme.Text);
        float w = 150 * k;
        DrawRect(new Rect2(c.X - w / 2, c.Y + 82 * k, w, 4 * k), new Color(0, 0, 0, 0.45f));
        DrawRect(new Rect2(c.X - w / 2, c.Y + 82 * k, w * p, 4 * k), UiTheme.Text);
    }

    void DrawSpeed(Vector2 size, float k)
    {
        var mv = s.Mover;
        float cx = size.X / 2, by = size.Y - 30 * k - 74 * k;
        var plate = new Rect2(cx - 128 * k, by + 16 * k, 256 * k, 58 * k);
        DrawRect(plate, UiTheme.Plate);
        var r = plate.Grow(-14 * k);
        int ls = UiTheme.Fs(12, k), vs = UiTheme.Fs(17, k);
        TL(UiTheme.HudWide, "SPEED", r.Position.X, r.Position.Y + 10 * k, ls, UiTheme.Dim);
        TR(UiTheme.Hud, cSpeed.Get((int)(mv.Speed * 10), v => $"{v / 10f:0.0} m/s"), r.End.X, r.Position.Y + 11 * k, vs,
            mv.Accurate ? UiTheme.Good : UiTheme.Text);
        float barY = r.Position.Y + 20 * k, bh = 8 * k;
        DrawRect(new Rect2(r.Position.X, barY, r.Size.X, bh), new Color(0, 0, 0, 0.5f));
        DrawRect(new Rect2(r.Position.X, barY, r.Size.X * Mathf.Clamp(mv.Speed / mv.RunMax, 0, 1), bh), mv.Accurate ? UiTheme.Good : UiTheme.Accent);
        float tx = r.Position.X + r.Size.X * (mv.AccurateMax / mv.RunMax);
        DrawRect(new Rect2(tx - 1 * k, barY - 4 * k, 2 * k, bh + 8 * k), UiTheme.Text);

        // last stop feedback (pops when a new stop is measured)
        if (mv.LastStopMs != lastStopSeen)
        {
            if (mv.LastStopMs >= 0 && lastStopSeen > -2f) stopPopAt = RealNow;
            lastStopSeen = mv.LastStopMs;
        }
        if (mv.LastStopMs >= 0)
        {
            long key = (long)mv.LastStopMs * 2 + (mv.LastStopWasCounter ? 1 : 0);
            string t = cStop.Get(key, v => Loc.Spanish
                ? $"ÚLTIMA PARADA {v / 2} MS · {((v & 1) == 1 ? "CONTRA-STRIFE" : "TECLAS SOLTADAS")}"
                : $"LAST STOP {v / 2} MS · {((v & 1) == 1 ? "COUNTER-STRAFE" : "RELEASED KEYS")}");
            float age = RealNow - stopPopAt, sc = age < 0.22f ? 1.25f - 0.25f * Gfx.EaseOut(age / 0.22f) : 1f;
            int fs = UiTheme.Fs(17, k);
            ScaledTextC(UiTheme.HudWide, t, new Vector2(cx, plate.Position.Y - 22 * k), fs, mv.LastStopWasCounter ? UiTheme.Good : UiTheme.Warn, sc);
        }
    }

    void DrawDamage(Vector2 size, float k)
    {
        float hp = s.Player.Hp;
        if (hp < 30)
        {
            Gfx.EdgeVignette(this, size, 240 * k, new Color(0.7f, 0.02f, 0.06f, hp <= 0 ? 0.5f : 0.26f));
            DrawRect(new Rect2(Vector2.Zero, size), new Color(0.55f, 0f, 0.02f, hp <= 0 ? 0.12f : 0.05f));
        }
        float since = s.Now - s.Player.LastHitAt;
        if (since < 0 || since >= 0.6f) return;
        float a = 1f - since / 0.6f;
        Gfx.EdgeVignette(this, size, 150 * k, new Color(0.85f, 0.04f, 0.08f, 0.38f * a));
        var d = s.Player.LastHitFrom - s.View.Eye;
        if (d.X * d.X + d.Z * d.Z < 1e-4f) return;
        float yawTo = Mathf.RadToDeg(Mathf.Atan2(d.X, -d.Z));
        float rel = Mathf.Wrap(yawTo - s.View.ViewYaw, -180f, 180f); // + = to the right
        float ang = Mathf.DegToRad(rel) - Mathf.Pi / 2;
        var c = size / 2;
        float rad = 120 * k;
        DrawArc(c, rad, ang - 0.44f, ang + 0.44f, 28, new Color(UiTheme.Accent, 0.28f * a), 16 * k, true);
        DrawArc(c, rad, ang - 0.38f, ang + 0.38f, 28, new Color(1f, 0.22f, 0.26f, 0.95f * a), 5 * k, true);
        // pointer notch
        var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        var perp = new Vector2(-dir.Y, dir.X);
        Span<Vector2> tri = stackalloc Vector2[3];
        tri[0] = c + dir * (rad + 18 * k); tri[1] = c + dir * (rad + 4 * k) + perp * 9 * k; tri[2] = c + dir * (rad + 4 * k) - perp * 9 * k;
        this.FillPoly(tri, new Color(1f, 0.22f, 0.26f, 0.95f * a));
    }

    void DrawPrompt(Vector2 size, float k, string p)
    {
        int fs = UiTheme.Fs(24, k);
        float w = Gfx.TextW(UiTheme.Body, p, fs), cy = size.Y * 0.64f;
        var r = new Rect2(size.X / 2 - w / 2 - 22 * k, cy - 22 * k, w + 44 * k, 44 * k);
        Gfx.HGradient(this, new Rect2(r.Position.X - 60 * k, r.Position.Y, 60 * k, r.Size.Y), new Color(0, 0, 0, 0), new Color(0.03f, 0.05f, 0.08f, 0.6f));
        DrawRect(r, new Color(0.03f, 0.05f, 0.08f, 0.6f));
        Gfx.HGradient(this, new Rect2(r.End.X, r.Position.Y, 60 * k, r.Size.Y), new Color(0.03f, 0.05f, 0.08f, 0.6f), new Color(0, 0, 0, 0));
        TC(UiTheme.Body, p, size.X / 2, Gfx.Mid(cy, fs), fs, UiTheme.Text);
    }

    void DrawBanner(Vector2 size, float k)
    {
        const float life = 1.4f;
        float age = life - s.BannerTime;
        float alpha = Mathf.Clamp(s.BannerTime / 0.35f, 0, 1) * Mathf.Clamp(age / 0.06f, 0, 1);
        float sc = age < 0.2f ? 1.35f - 0.35f * Gfx.EaseOut(age / 0.2f) : 1f;
        float stripe = Mathf.Clamp(age / 0.18f, 0, 1);
        var col = s.BannerColor;
        int fs = UiTheme.Fs(56, k);
        float cy = size.Y * 0.30f, cx = size.X / 2;
        float w = Gfx.TextW(UiTheme.Display, s.BannerText, fs) + 220 * k, h = 78 * k;
        float half = w / 2 * Gfx.EaseOut(stripe);
        var dark = new Color(0.03f, 0.05f, 0.08f, 0.62f * alpha);
        var clear = new Color(0.03f, 0.05f, 0.08f, 0);
        Gfx.HGradient(this, new Rect2(cx - half, cy - h / 2, half, h), clear, dark);
        Gfx.HGradient(this, new Rect2(cx, cy - h / 2, half, h), dark, clear);
        var line = new Color(col, 0.85f * alpha);
        var lineClear = new Color(col, 0);
        Gfx.HGradient(this, new Rect2(cx - half, cy - h / 2, half, 2 * k), lineClear, line);
        Gfx.HGradient(this, new Rect2(cx, cy - h / 2, half, 2 * k), line, lineClear);
        Gfx.HGradient(this, new Rect2(cx - half, cy + h / 2 - 2 * k, half, 2 * k), lineClear, line);
        Gfx.HGradient(this, new Rect2(cx, cy + h / 2 - 2 * k, half, 2 * k), line, lineClear);
        Gfx.Diamond(this, new Vector2(cx, cy - h / 2), 6 * k, 6 * k, line);
        ScaledTextC(UiTheme.Display, s.BannerText, new Vector2(cx, Gfx.Mid(cy, fs)), fs, new Color(col, alpha), sc);
    }

    void DrawKillBanner(Vector2 size, float k, int count, float t)
    {
        const float life = 1.4f;
        float age = life - t;
        float pop = age < 0.14f ? 1.15f - 0.15f * (age / 0.14f) : 1f;
        float alpha = Mathf.Clamp(t / 0.35f, 0, 1);
        var c = new Vector2(size.X / 2, size.Y - 262 * k);
        float r = 36 * k;
        var red = new Color(UiTheme.Accent, alpha);
        var gold = new Color(UiTheme.TierColor(4), alpha);
        var main = count >= 5 ? gold : red;

        DrawSetTransform(c, 0, new Vector2(pop, pop));
        var o = Vector2.Zero;
        if (age < 0.45f)
        {
            float g = age / 0.45f;
            DrawArc(o, r * (1.1f + g * 1.4f), 0, Mathf.Tau, 48, new Color(main, (1 - g) * 0.55f * alpha), 3 * k, true);
        }
        // side blades
        Span<Vector2> blade = stackalloc Vector2[4];
        for (int side = -1; side <= 1; side += 2)
        {
            blade[0] = new(side * r * 1.08f, -r * 0.34f); blade[1] = new(side * r * 1.9f, 0);
            blade[2] = new(side * r * 1.08f, r * 0.34f); blade[3] = new(side * r * 1.3f, 0);
            this.FillPoly(blade, main);
        }
        Gfx.Diamond(this, o, r, r, new Color(0.05f, 0.08f, 0.11f, 0.88f * alpha));
        Gfx.DiamondLine(this, o, r, r, main, 3 * k);
        Gfx.Diamond(this, o, r * 0.44f, r * 0.44f, main);
        Gfx.Diamond(this, o, r * 0.14f, r * 0.14f, new Color(1, 1, 1, 0.9f * alpha));
        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) / 2f) * 22 * k;
            Gfx.ChevronDown(this, new Vector2(x, r + 18 * k), 18 * k, 11 * k, 4 * k, main);
        }
        DrawSetTransformMatrix(Transform2D.Identity);
    }

    void DrawCountdownBackdrop(Vector2 size)
    {
        Span<Vector2> points = stackalloc Vector2[]
        {
            Vector2.Zero, new(size.X, 0), new(size.X, size.Y * 0.50f), new(0, size.Y * 0.50f)
        };
        var dark = new Color(0.025f, 0.032f, 0.043f, 0.86f);
        Span<Color> colors = stackalloc Color[] { dark, dark, new(dark, 0), new(dark, 0) };
        this.GradPoly(points, colors);
    }

    void DrawCountdown(Vector2 size, float k, TrainingMode m)
    {
        int n = Mathf.Max(1, Mathf.CeilToInt(s.Countdown));
        float cx = size.X / 2, y = size.Y * 0.125f;
        var tint = new Color(0.65f, 0.90f, 0.84f);
        int caption = UiTheme.Fs(17, k), title = UiTheme.Fs(30, k), number = UiTheme.Fs(104, k);
        Gfx.TextC(this, UiTheme.HudWide, L("STARTING IN", "EMPIEZA EN"), cx, y, caption, tint);
        Gfx.TextC(this, UiTheme.Hud, modeTitle, cx, y + 37 * k, title, UiTheme.Text);
        Gfx.TextC(this, UiTheme.Light, cCount.Get(n, v => v.ToString(Inv)), cx, y + 146 * k, number, UiTheme.Text);

        float track = y + 167 * k;
        for (int segment = 1; segment <= 3; segment++)
        {
            float x = cx + (segment - 2) * 36 * k - 15 * k;
            DrawStatusLine(x, track, 30 * k, k, s.Countdown - (segment - 1), tint);
        }

        float nextLine = track + 32 * k;
        if (m.Description.Length > 0)
        {
            float width = Mathf.Min(size.X - 38 * k, 740 * k);
            int fontSize = UiTheme.Fs(18, k);
            var font = UiTheme.Body;
            string description = Loc.T(m.Description);
            var position = new Vector2(cx - width / 2, nextLine);
            DrawMultilineString(font, position + Vector2.Down, description,
                HorizontalAlignment.Center, width, fontSize, 2, new Color(0, 0, 0, 0.8f));
            DrawMultilineString(font, position, description,
                HorizontalAlignment.Center, width, fontSize, 2, UiTheme.Text);
            nextLine += CoachDraw.TextH(font, description, width, fontSize, 2) + 4 * k;
        }
        if (m.MadeBy is { } by)
        {
            TC(UiTheme.Body, $"@{by}", cx, nextLine, caption, UiTheme.Text);
            nextLine += 20 * k;
        }
        if (s.CanSwitchWeapon || s.CanSwapRifle)
        {
            var binds = Main.I.Valorant.Binds;
            string key1 = binds.Short(GameAction.EquipPrimary);
            string other = s.Weapon?.Kind == WeaponKind.Phantom ? "VANDAL" : "PHANTOM";
            string hint;
            if (s.CanSwitchWeapon)
            {
                string primary = Weapons.Get(s.PrimarySlot)?.Name.ToUpperInvariant() ?? "";
                string sidearm = Weapons.Get(s.SidearmSlot)?.Name.ToUpperInvariant() ?? "";
                hint = $"{key1} {primary} · {binds.Short(GameAction.EquipSecondary)} {sidearm} · {binds.Short(GameAction.EquipMelee)} {Loc.T("KNIFE")}";
                if (s.CanSwapRifle) hint += Loc.Spanish ? $" · {key1} OTRA VEZ: {other}" : $" · {key1} AGAIN: {other}";
            }
            else
            {
                hint = $"{s.Weapon?.Name.ToUpperInvariant()} · {key1} {(Loc.Spanish ? "PARA CAMBIAR A" : "TO SWITCH TO")} {other}";
            }
            TC(UiTheme.Body, hint, cx, nextLine + 4 * k, UiTheme.Fs(14, k), tint);
        }
    }

    // ---- outlined text (readable over bright scenes, like VALORANT's shadowed HUD text) ----

    static Color Shadow(Color c) => new(0.02f, 0.03f, 0.05f, 0.5f * c.A);

    void TL(Font f, string t, float x, float baseline, int fs, Color c)
    {
        t = Loc.T(t);
        var pos = new Vector2(x, baseline);
        DrawStringOutline(f, pos, t, HorizontalAlignment.Left, -1, fs, Math.Max(2, fs / 9), Shadow(c));
        DrawString(f, pos, t, HorizontalAlignment.Left, -1, fs, c);
    }

    float TC(Font f, string t, float cx, float baseline, int fs, Color c)
    {
        float w = Gfx.TextW(f, t, fs);
        TL(f, t, cx - w / 2, baseline, fs, c);
        return w;
    }

    float TR(Font f, string t, float right, float baseline, int fs, Color c)
    {
        float w = Gfx.TextW(f, t, fs);
        TL(f, t, right - w, baseline, fs, c);
        return w;
    }

    /// <summary>Right-aligned text that must not pass through <see cref="Loc"/> (map callout names).</summary>
    float RawR(Font f, string t, float right, float baseline, int fs, Color c)
    {
        float w = f.GetStringSize(t, HorizontalAlignment.Left, -1, fs).X;
        var pos = new Vector2(right - w, baseline);
        DrawStringOutline(f, pos, t, HorizontalAlignment.Left, -1, fs, Math.Max(2, fs / 9), Shadow(c));
        DrawString(f, pos, t, HorizontalAlignment.Left, -1, fs, c);
        return w;
    }

    /// <summary>Centred text scaled around its own centre (pop animations).</summary>
    void ScaledTextC(Font f, string t, Vector2 baselineCentre, int fs, Color c, float scale)
    {
        float w = Gfx.TextW(f, t, fs);
        if (Mathf.Abs(scale - 1f) < 0.001f)
        {
            TL(f, t, baselineCentre.X - w / 2, baselineCentre.Y, fs, c);
            return;
        }
        // scale about the visual centre of the caps (baseline - 0.36 em)
        var pivot = new Vector2(baselineCentre.X, baselineCentre.Y - fs * 0.36f);
        DrawSetTransform(pivot, 0, new Vector2(scale, scale));
        TL(f, t, -w / 2, fs * 0.36f, fs, c);
        DrawSetTransformMatrix(Transform2D.Identity);
    }

    // =====================================================================================
    // Pause & results overlays (child controls with real buttons)
    // =====================================================================================

    void RebuildOverlay(Vector2 size, bool keepTime = false)
    {
        if (overlay != null) { RemoveChild(overlay); overlay.QueueFree(); overlay = null; }
        sensEdit = null;
        insightsPending = false;
        if (s.State != GameSession.St.Paused) CloseOptions();
        if (s.State is not (GameSession.St.Paused or GameSession.St.Results)) return;

        overlaySize = size;
        if (!keepTime) overlayAt = RealNow;
        float k = UiTheme.S(size);
        overlay = new DrawBox { MouseFilter = MouseFilterEnum.Stop };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        overlay.Theme = UiTheme.MakeTheme(k);
        AddChild(overlay);
        if (s.State == GameSession.St.Paused)
        {
            BuildPause(size, k);
            overlay.Visible = pauseOptions == null;
        }
        else BuildResults(size, k);
        Loc.Apply(overlay);
    }

    void OpenOptions()
    {
        if (pauseOptions != null) return;
        overlay!.Hide();
        pauseOptions = new SettingsScreen { BackToPause = () => CloseOptions() };
        AddChild(pauseOptions);
    }

    public bool CloseOptions()
    {
        if (pauseOptions == null) return false;
        RemoveChild(pauseOptions);
        pauseOptions.QueueFree();
        pauseOptions = null;
        if (s.State == GameSession.St.Paused) RebuildOverlay(GetViewportRect().Size, keepTime: true);
        return true;
    }

    // ---------------- pause ----------------

    void BuildPause(Vector2 size, float k)
    {
        var m = s.Mode;
        var app = Main.I;
        var st = app.Settings;
        var tier = Difficulty.Get(s.Tier);

        // Flat wash: the drill stays visible underneath, the overlay only quiets it.
        overlay!.OnDraw = d => d.DrawRect(new Rect2(Vector2.Zero, size), new Color(0.012f, 0.015f, 0.020f, 0.86f));

        float pageW = Mathf.Min(size.X - 120 * k, 1120 * k);
        var page = new VBoxContainer { CustomMinimumSize = new Vector2(pageW, 0), MouseFilter = MouseFilterEnum.Ignore };
        page.AddThemeConstantOverride("separation", 0);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        center.AddChild(page);
        overlay.AddChild(center);

        MText Cap(string text, float delay) => new()
        {
            K = k, Text = text, Font = UiTheme.HudWide, Px = 10.5f, Color = UiTheme.Faint, Delay = delay,
            CustomMinimumSize = new Vector2(0, 18 * k),
        };
        void Gap(VBoxContainer col, float h) => col.AddChild(new Control
        {
            CustomMinimumSize = new Vector2(0, h * k), MouseFilter = MouseFilterEnum.Ignore,
        });
        MLink Link(string label, Action act, float delay, float px = 12f)
        {
            var l = new MLink { K = k, Label = label, Px = px, Delay = delay };
            l.Pressed += act;
            return l;
        }
        HBoxContainer ArrowRow(float gap = 10f)
        {
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", (int)(gap * k));
            return row;
        }
        MLink Arrow(string label, Action act, float delay, float px = 21f)
        {
            var l = Link(label, act, delay, px);
            l.CustomMinimumSize = new Vector2(28 * k, 36 * k);
            l.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            return l;
        }
        MText Value(string text, float px, float delay) => new()
        {
            K = k, Text = text, Font = UiTheme.Light, Px = px, Color = UiTheme.Text, Delay = delay,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(0, 36 * k),
        };

        // ---------------------------------------------------------------- header
        page.AddChild(new MText { K = k, Text = "PAUSED", Font = UiTheme.HudWide, Px = 11, Color = UiTheme.Accent, Delay = 0.02f });
        Gap(page, 4);
        page.AddChild(new MText
        {
            K = k, Text = m.Name, Font = UiTheme.Light, Px = 50, Color = UiTheme.Text, Delay = 0.05f,
            CustomMinimumSize = new Vector2(0, 62 * k),
        });
        Gap(page, 2);
        page.AddChild(new MText
        {
            K = k, Text = $"{Loc.T(m.Category).ToUpperInvariant()}  ·  {tier.Name.ToUpperInvariant()}", Font = UiTheme.Body,
            Px = 13, Color = UiTheme.Dim, Delay = 0.09f,
        });
        // Routine task navigation: jump to any other task without saving the abandoned run. Shown only while the
        // session belongs to a multi-task routine/playlist.
        if (PlaylistRunner.CanNavigate)
        {
            Gap(page, 8);
            var nav = ArrowRow(6f);
            nav.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
            var prev = Arrow("‹", () => PlaylistRunner.JumpTask(-1), 0.1f, 19f);
            prev.Disabled = !PlaylistRunner.CanGoPrev;
            prev.TooltipText = L("Previous task of the routine", "Tarea anterior de la rutina");
            nav.AddChild(prev);
            string pass = PlaylistRunner.RunsInStep > 1
                ? $"  ·  {L("PASS", "PASADA")} {PlaylistRunner.RunOfStep}/{PlaylistRunner.RunsInStep}"
                : "";
            nav.AddChild(new MText
            {
                K = k, Text = $"{L("TASK", "TAREA")} {PlaylistRunner.TaskNumber}/{PlaylistRunner.TaskCount}{pass}",
                Font = UiTheme.HudWide, Px = 11, Color = UiTheme.Accent, Delay = 0.1f,
                Align = HorizontalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(150 * k, 30 * k),
            });
            var next = Arrow("›", () => PlaylistRunner.JumpTask(1), 0.1f, 19f);
            next.Disabled = !PlaylistRunner.CanGoNext;
            next.TooltipText = L("Next task of the routine", "Tarea siguiente de la rutina");
            nav.AddChild(next);
            nav.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
            page.AddChild(nav);
            page.AddChild(new MText
            {
                K = k, Text = PlaylistRunner.TaskLabel, Font = UiTheme.Body, Px = 11.5f, Color = UiTheme.Faint,
                Align = HorizontalAlignment.Center, Delay = 0.11f, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 18 * k),
            });
        }
        Gap(page, 20);
        page.AddChild(new MRule { Delay = 0.12f });
        Gap(page, 22);

        // ---------------------------------------------------------------- two columns
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Ignore };
        body.AddThemeConstantOverride("separation", (int)(56 * k));
        page.AddChild(body);

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        left.AddThemeConstantOverride("separation", 0);
        body.AddChild(left);
        body.AddChild(new ColorRect
        {
            Color = UiTheme.Line, CustomMinimumSize = new Vector2(1, 0), SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        right.AddThemeConstantOverride("separation", 0);
        body.AddChild(right);

        // ---- left: the run, then what the next run plays at ----
        left.AddChild(Cap(L("RUN", "PARTIDA"), 0.14f));
        Gap(left, 8);
        var resume = new MAction
        {
            K = k, Primary = true, Label = L("RESUME", "REANUDAR"), Px = 13, Delay = 0.16f,
            CustomMinimumSize = new Vector2(0, 48 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = L("Back to the drill (Esc)", "Volver al ejercicio (Esc)"),
        };
        resume.Pressed += s.Resume;
        left.AddChild(resume);
        Gap(left, 8);
        var restart = new MAction
        {
            K = k, Label = L("RESTART", "REINICIAR"), Px = 13, Delay = 0.18f,
            CustomMinimumSize = new Vector2(0, 44 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = L("Play this drill again from the countdown", "Jugar este ejercicio otra vez desde la cuenta atrás"),
        };
        restart.Pressed += s.Restart;
        left.AddChild(restart);
        Gap(left, 8);
        var options = new MAction
        {
            K = k, Label = L("OPTIONS", "OPCIONES"), Px = 13, Delay = 0.19f,
            CustomMinimumSize = new Vector2(0, 44 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        options.Pressed += OpenOptions;
        left.AddChild(options);

        if (s.CanToggleWarmup)
        {
            var warmup = Link(s.CoachSuspended ? L("RESTART WITH COACH", "REINICIAR CON COACH")
                : L("RESTART AS WARM-UP", "REINICIAR COMO CALENTAMIENTO"), s.ToggleWarmup, .19f, 11);
            warmup.TooltipText = L("This starts a fresh round and sets the mode for future free drills.",
                "Inicia una ronda nueva y aplica el modo a los siguientes ejercicios libres.");
            left.AddChild(warmup);
        }
        Gap(left, 8);
        var menu = new MAction
        {
            K = k, Label = L("BACK TO MENU", "VOLVER AL MENÚ"), Px = 13, Delay = 0.2f,
            CustomMinimumSize = new Vector2(0, 44 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = L("Leave this run without saving it", "Salir de esta partida sin guardarla"),
        };
        menu.Pressed += Main.I.ShowMenu;
        left.AddChild(menu);

        Gap(left, 20);
        left.AddChild(new MRule { Delay = 0.24f });
        Gap(left, 14);

        left.AddChild(Cap(L("DIFFICULTY", "DIFICULTAD"), 0.26f));
        var tierRow = ArrowRow();
        MText tierNote = null!; // declared first: StepTier's lambda may run before the note below is built
        var tierText = Value(tier.Name, 23, 0.27f);
        var tPrev = Arrow("‹", () => StepTier(-1), 0.27f);
        var tNext = Arrow("›", () => StepTier(1), 0.27f);
        tierRow.AddChild(tPrev);
        tierRow.AddChild(tierText);
        tierRow.AddChild(tNext);
        left.AddChild(tierRow);
        tierNote = new MText
        {
            K = k, Font = UiTheme.Body, Px = 11.5f, Color = UiTheme.Faint, Delay = 0.28f,
            CustomMinimumSize = new Vector2(0, 18 * k),
        };
        left.AddChild(tierNote);
        // Functions read controls declared above; call once they all exist.
        void StepTier(int dir)
        {
            int next = Math.Clamp(st.Tier + dir, 0, Difficulty.Tiers.Length - 1);
            if (next == st.Tier) return;
            st.Tier = next;
            st.Save();
            PaintTier();
        }
        void PaintTier()
        {
            var t = Difficulty.Get(st.Tier);
            tierText.Text = t.Name;
            tierText.QueueRedraw();
            tierNote.Text = $"{Loc.T(t.Ranks)}  ·  ~{t.Bot.ReactMs:0} ms  ·  " + L("from the next run", "desde la próxima partida");
            tierNote.QueueRedraw();
        }
        PaintTier();

        Gap(left, 12);
        left.AddChild(Cap(L("PROGRESSIVE DIFFICULTY", "DIFICULTAD PROGRESIVA"), 0.31f));
        Gap(left, 2);
        var progressive = new MToggle
        {
            K = k, On = st.ProgressiveDifficulty, Delay = 0.32f, CustomMinimumSize = new Vector2(130 * k, 30 * k),
            TooltipText = L("After each scored run: a strong run raises the tier, a poor run lowers it",
                "Tras cada partida puntuada: una buena sube el nivel, una mala lo baja"),
        };
        progressive.Changed = on => { st.ProgressiveDifficulty = on; st.Save(); };
        left.AddChild(progressive);
        left.AddChild(new MText
        {
            K = k, Font = UiTheme.Body, Px = 11.5f, Color = UiTheme.Faint, Delay = 0.33f,
            Text = L("strong run → one tier up · poor run → one tier down",
                "buena partida → sube un nivel · mala → baja uno"),
        });

        Gap(left, 12);
        left.AddChild(Cap(L("RECOIL", "RETROCESO"), 0.34f));
        Gap(left, 2);
        bool forcedRecoil = m.ForcedRecoil;
        var recoil = new MToggle
        {
            K = k, On = forcedRecoil || (s.SessionRecoil ?? st.RecoilEnabled), Delay = 0.35f,
            CustomMinimumSize = new Vector2(130 * k, 30 * k),
            Disabled = forcedRecoil || s.SessionRecoil != null,
            TooltipText = forcedRecoil
                ? L("Always on: this drill simulates a match", "Siempre activo: este ejercicio simula una partida")
                : s.SessionRecoil != null ? L("Fixed for this exercise", "Fijo para este ejercicio")
                : L("The camera climbs with the spray, as in VALORANT. Off: the shots still spread",
                    "La cámara sube con el spray, como en VALORANT. Apagado: los disparos siguen dispersando"),
        };
        recoil.Changed = on =>
        {
            if (m.ForcedRecoil || s.SessionRecoil != null) return;
            st.RecoilEnabled = on;
            st.Save();
            if (s.Gun != null && s.Gun.RecoilOn != on)
            {
                s.Gun.RecoilOn = on;
                s.Event("recoil_changed");
            }
        };
        left.AddChild(recoil);
        left.AddChild(new MText
        {
            K = k, Font = UiTheme.Body, Px = 11.5f, Color = UiTheme.Faint, Delay = 0.36f,
            Text = forcedRecoil
                ? L("locked on · real magazine · simulates a match", "bloqueado · cargador real · simula una partida")
                : L("camera stays put · spread still applies", "la cámara no sube · la dispersión sigue"),
        });

        Gap(left, 12);
        left.AddChild(Cap(L("WEAPON", "ARMA"), 0.37f));
        Gap(left, 2);
        var weapon = new MWeaponSelector(overlay, k) { SizeFlagsHorizontal = SizeFlags.ShrinkBegin, Delay = 0.38f };
        left.AddChild(weapon);
        left.AddChild(new MText
        {
            K = k, Font = UiTheme.Body, Px = 11.5f, Color = UiTheme.Faint, Delay = 0.39f,
            Text = L("every drill · applies from the next run", "cada ejercicio · se aplica en la próxima partida"),
        });

        // ---- right: sensitivity, audio, graphics ----
        right.AddChild(Cap(L("SENSITIVITY", "SENSIBILIDAD"), 0.14f));
        Gap(right, 6);
        var sensRow = ArrowRow();
        MText sensNote = null!; // declared first: the arrow/use lambdas can run before the note is built
        MLink useVal = null!;
        bool blindSens = m.Key == "sensfinder" || s.SensComparisonTrial != null;
        bool fixedSens = s.SessionSens.HasValue || blindSens || s.SensSearch?.Active == true;
        bool CategoryOverride() => st.CategorySensitivities?.ContainsKey(s.SensSection) == true;
        var sPrev = Arrow("‹", () => AdjustSens(-1), 0.17f);
        sPrev.Disabled = fixedSens;
        sensRow.AddChild(sPrev);
        sensEdit = new LineEdit
        {
            CustomMinimumSize = new Vector2(0, 42 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MaxLength = 8, Alignment = HorizontalAlignment.Center, SelectAllOnFocus = true,
            Editable = !fixedSens,
            TooltipText = fixedSens ? L("Sensitivity fixed for this round", "Sensibilidad fija para esta ronda")
                : L("Click and type a sensitivity, then Enter. Shift-click the arrows for ±0.001",
                "Haz clic y escribe una sensibilidad, luego Enter. Mayús + flechas para ±0.001"),
        };
        SettingsScreen.StyleEdit(sensEdit, k, 24, UiTheme.Light);
        StyleBoxFlat HairBox(Color border) => new()
        {
            BgColor = new Color(1, 1, 1, 0.02f), DrawCenter = true, BorderColor = border, BorderWidthBottom = 1,
            ContentMarginLeft = 6 * k, ContentMarginRight = 6 * k, ContentMarginTop = 4 * k, ContentMarginBottom = 4 * k,
        };
        sensEdit.AddThemeStyleboxOverride("normal", HairBox(Mn.A(UiTheme.Text, 0.18f)));
        sensEdit.AddThemeStyleboxOverride("focus", HairBox(UiTheme.Accent));
        sensRow.AddChild(sensEdit);
        var sNext = Arrow("›", () => AdjustSens(1), 0.17f);
        sNext.Disabled = fixedSens;
        sensRow.AddChild(sNext);
        right.AddChild(sensRow);
        sensNote = new MText
        {
            K = k, Font = UiTheme.HudWide, Px = 10.5f, Color = UiTheme.Faint, Delay = 0.18f,
            CustomMinimumSize = new Vector2(0, 18 * k),
        };
        right.AddChild(sensNote);
        useVal = Link(CategoryOverride() ? L("USE GENERAL SENSITIVITY", "USAR SENSIBILIDAD GENERAL")
            : L("USE VALORANT SENSITIVITY", "USAR SENSIBILIDAD DE VALORANT"), () =>
        {
            if (CategoryOverride()) app.SetCategorySens(s.SensSection, null);
            else { st.UseSensOverride = false; st.Save(); }
            PaintSens();
        }, 0.2f, 10.5f);
        useVal.CustomMinimumSize = new Vector2(0, 24 * k);
        right.AddChild(useVal);

        if (!blindSens && !s.CoachSuspended)
        {
            var searchButton = Link(st.LiveSensitivityEnabled
                ? L("STOP SENSITIVITY SEARCH", "DETENER BÚSQUEDA DE SENSIBILIDAD")
                : L("FIND SENSITIVITY", "BUSCAR SENSIBILIDAD"), () =>
            {
                s.ToggleSensitivitySearch();
                Callable.From(() => RebuildOverlay(size, keepTime: true)).CallDeferred();
            }, 0.22f, 12);
            searchButton.Tint = UiTheme.Teal;
            right.AddChild(searchButton);
        }
        if (!s.CoachSuspended && !s.CanSearchSensitivity && !blindSens && s.SessionSens == null && s.OnFinished == null && SensitivityProfiles.Supports(s.SensSection))
            right.AddChild(Link(L("SENSITIVITY A/B TEST", "PRUEBA DE SENSIBILIDAD A/B"),
                () => app.ShowScreen(new SensLabScreen { SelectedSection = s.SensSection }), .22f, 12));

        void StoreSens(float value)
        {
            if (CategoryOverride()) app.SetCategorySens(s.SensSection, new CategorySensitivity(value, st.Dpi, false));
            else { st.UseSensOverride = true; st.SensOverride = value; st.Save(); }
        }
        void AdjustSens(int d)
        {
            float step = Input.IsKeyPressed(Key.Shift) ? 0.001f : 0.01f;
            StoreSens(Math.Clamp(MathF.Round((s.CurrentSens + d * step) * 1000f) / 1000f, SensInput.Min, SensInput.Max));
            PaintSens();
        }
        void PaintSens()
        {
            float effective = s.CurrentSens;
            if (sensEdit != null && IsInstanceValid(sensEdit) && !sensEdit.HasFocus())
                sensEdit.Text = blindSens ? "A/B" : effective.ToString("0.0000", Inv);
            if (fixedSens)
            {
                sensNote.Text = blindSens ? L("BLIND TEST IN PROGRESS", "PRUEBA CIEGA EN CURSO")
                    : s.SensSearch?.Active == true ? L("TEMPORARY SEARCH IN PROGRESS", "BÚSQUEDA TEMPORAL EN CURSO")
                    : L($"FIXED FOR THIS ROUND · {PlayerView.Cm360(effective, st.Dpi):0.0} CM/360",
                        $"FIJA PARA ESTA RONDA · {PlayerView.Cm360(effective, st.Dpi):0.0} CM/360");
                sensNote.QueueRedraw();
                useVal.Hide();
                return;
            }
            string note = CategoryOverride()
                ? $"{TrainingCatalog.SectionName(s.SensSection).ToUpperInvariant()} · {PlayerView.Cm360(effective, st.Dpi):0.0} CM/360"
                : st.UseSensOverride ? $"CUSTOM · {PlayerView.Cm360(effective, st.Dpi):0.0} CM/360"
                : $"FROM VALORANT · {PlayerView.Cm360(effective, st.Dpi):0.0} CM/360";
            if (st.LiveSensitivityEnabled && !s.CanSearchSensitivity && !s.CoachSuspended)
                note = L("SEARCH ON · NEXT COMPATIBLE DRILL", "BÚSQUEDA ACTIVA · PRÓXIMO EJERCICIO COMPATIBLE");
            sensNote.Text = Loc.Numeric(L(note, note.Replace("CUSTOM", "PERSONALIZADA").Replace("FROM VALORANT", "DE VALORANT")));
            sensNote.QueueRedraw();
            useVal.Label = CategoryOverride() ? L("USE GENERAL SENSITIVITY", "USAR SENSIBILIDAD GENERAL")
                : L("USE VALORANT SENSITIVITY", "USAR SENSIBILIDAD DE VALORANT");
            useVal.Visible = CategoryOverride() || st.UseSensOverride;
        }
        // Enter or clicking away applies a valid value; anything else snaps back to the current sensitivity.
        void ApplySens()
        {
            if (fixedSens || sensEdit == null || !IsInstanceValid(sensEdit)) return;
            if (SensInput.TryParse(sensEdit.Text, out var v) && Math.Abs(v - s.CurrentSens) > .00005f) StoreSens(v);
            PaintSens();
        }
        sensEdit.TextSubmitted += _ => sensEdit?.ReleaseFocus();
        sensEdit.FocusExited += ApplySens;
        sensEdit.Text = s.CurrentSens.ToString("0.0000", Inv);
        PaintSens();

        // Dev self-check "--pause-jump <-1|1>" (with --pause-at): jump to the previous/next routine task while paused.
        if (Main.I.Dev && CmdLine.DevAfter("--pause-jump") is { } jump
            && int.TryParse(jump, out int direction) && direction != 0)
            GetTree().CreateTimer(1.0).Timeout += () => PlaylistRunner.JumpTask(Math.Sign(direction));

        // Dev self-check "--pause-type <value>" (with --pause-at): click the box, type, Enter, through the real input path.
        if (Main.I.Dev && CmdLine.DevAfter("--pause-type") is { } typed)
            GetTree().CreateTimer(1.0).Timeout += () =>
            {
                if (sensEdit == null || !IsInstanceValid(sensEdit)) return;
                var at = sensEdit.GetGlobalRect().GetCenter();
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = at, GlobalPosition = at });
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at });
                foreach (char c in typed + "\n")
                {
                    var key = c == '\n' ? Key.Enter : c == '.' ? Key.Period : Key.Key0 + (c - '0');
                    Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Unicode = c == '\n' ? 0 : c, Pressed = true });
                    Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
                }
                GetTree().CreateTimer(0.5).Timeout += () => Log.Info($"[pause-type] typed {typed}: sens now {Main.I.Sens.ToString("0.000", Inv)} (custom {st.UseSensOverride}), state {s.State}");
            };

        Gap(right, 14);
        right.AddChild(new MRule { Delay = 0.24f });
        Gap(right, 14);

        right.AddChild(Cap(L("AUDIO", "AUDIO"), 0.26f));
        Gap(right, 2);
        var volText = Value($"{st.Volume * 100:0}%", 22, 0.28f);
        var volRow = ArrowRow();
        MBar volBar = null!; // declared first: the arrow lambdas can run before the bar is built
        volRow.AddChild(Arrow("‹", () => SetVolume(st.Volume - 0.05f), 0.28f));
        volRow.AddChild(volText);
        volRow.AddChild(Arrow("›", () => SetVolume(st.Volume + 0.05f), 0.28f));
        right.AddChild(volRow);
        volBar = new MBar { K = k, Value = st.Volume, Delay = 0.3f, CustomMinimumSize = new Vector2(0, 10 * k) };
        right.AddChild(volBar);
        void SetVolume(float value)
        {
            st.Volume = Math.Clamp(MathF.Round(value * 100f) / 100f, 0f, 1f);
            Sfx.Volume = st.Volume;
            st.Save();
            volText.Text = $"{st.Volume * 100:0}%";
            volText.QueueRedraw();
            volBar.Value = st.Volume;
            volBar.QueueRedraw();
            Sfx.I?.Play("ui_click", 0.8f); // previews the game volume
        }

        Gap(right, 14);
        right.AddChild(Cap(L("GRAPHICS", "GRÁFICOS"), 0.34f));
        Gap(right, 4);
        var qRow = ArrowRow(18f);
        var qLinks = new List<MLink>();
        string[] qNames = { "LOW", "MED", "HIGH", "ULTRA" };
        for (int i = 0; i < qNames.Length; i++)
        {
            int qi = i;
            var q = Link(qNames[i], () =>
            {
                st.Quality = qi;
                st.Save();
                for (int j = 0; j < qLinks.Count; j++)
                {
                    qLinks[j].Active = j == qi;
                    qLinks[j].QueueRedraw();
                }
            }, 0.36f + i * 0.01f, 11f);
            q.Active = st.Quality == qi;
            q.CustomMinimumSize = new Vector2(0, 26 * k);
            q.TooltipText = qi == 0 ? L("Competitive/Low: highest FPS", "Competitivo/Bajo: más FPS")
                : qi == 3 ? L("Ultra: best looking, heaviest", "Ultra: mejor aspecto, el más exigente")
                : $"{L("Graphics preset", "Ajuste gráfico")} {qNames[qi]}";
            qLinks.Add(q);
            qRow.AddChild(q);
        }
        right.AddChild(qRow);

        Gap(right, 8);
        var vsRow = ArrowRow();
        var vsLabel = new MText
        {
            K = k, Text = "VSYNC", Font = UiTheme.HudWide, Px = 10.5f, Color = UiTheme.Dim, Delay = 0.4f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(0, 26 * k),
        };
        vsRow.AddChild(vsLabel);
        var vsLinks = new List<MLink>();
        foreach (bool on in new[] { true, false })
        {
            bool state = on;
            var v = Link(on ? "ON" : "OFF", () => SetVSync(state), 0.4f, 11f);
            v.CustomMinimumSize = new Vector2(0, 26 * k);
            v.Active = app.VSync == state;
            vsLinks.Add(v);
            vsRow.AddChild(v);
        }
        right.AddChild(vsRow);
        void PaintVSync()
        {
            vsLinks[0].Active = app.VSync;
            vsLinks[1].Active = !app.VSync;
            foreach (var v in vsLinks) v.QueueRedraw();
        }
        void SetVSync(bool on)
        {
            st.VSyncOverride = on ? 1 : 0;
            st.Save();
            app.ApplyFps();
            PaintVSync();
        }

        Gap(right, 6);
        var fpsRow = ArrowRow();
        var fpsLabel = new MText
        {
            K = k, Text = "FPS CAP", Font = UiTheme.HudWide, Px = 10.5f, Color = UiTheme.Dim, Delay = 0.43f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(0, 36 * k),
        };
        fpsRow.AddChild(fpsLabel);
        int[] caps = { 0, 60, 144, 165, 240, 360 };
        var fpsText = Value(app.FpsCap <= 0 ? L("Unlimited", "Sin límite") : app.FpsCap.ToString(), 15, 0.44f);
        fpsText.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        fpsText.CustomMinimumSize = new Vector2(92 * k, 36 * k);
        fpsText.Align = HorizontalAlignment.Right;
        fpsRow.AddChild(Arrow("‹", () => StepFps(-1), 0.44f, 16f));
        fpsRow.AddChild(fpsText);
        fpsRow.AddChild(Arrow("›", () => StepFps(1), 0.44f, 16f));
        right.AddChild(fpsRow);
        int CapIndex()
        {
            int cap = app.FpsCap;
            if (cap <= 0) return 0;
            int best = 0;
            for (int i = 1; i < caps.Length; i++) if (Math.Abs(caps[i] - cap) < Math.Abs(caps[best] - cap)) best = i;
            return best;
        }
        void StepFps(int d)
        {
            int i = (CapIndex() + d + caps.Length) % caps.Length;
            st.FpsCapOverride = caps[i];
            st.Save();
            app.ApplyFps();
            fpsText.Text = app.FpsCap <= 0 ? L("Unlimited", "Sin límite") : app.FpsCap.ToString();
            fpsText.QueueRedraw();
        }
        right.AddChild(new MText
        {
            K = k, Font = UiTheme.Body, Px = 11.5f, Color = UiTheme.Faint, Delay = 0.46f,
            Text = L("VSync and FPS cap apply immediately", "VSync y el límite de FPS se aplican al momento"),
        });

        // ---------------------------------------------------------------- footer
        Gap(page, 22);
        page.AddChild(new MRule { Delay = 0.5f });
        Gap(page, 10);
        page.AddChild(new MText
        {
            K = k, Text = L("ESC  —  RESUME", "ESC  —  REANUDAR"), Font = UiTheme.HudWide, Px = 10.5f, Color = UiTheme.Dim,
            Align = HorizontalAlignment.Center, Delay = 0.52f, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 18 * k),
        });
    }

    // ---------------- results ----------------

    void BuildResults(Vector2 size, float k)
    {
        var m = s.Mode;
        var review = CoachData.ReviewFor(s);
        bool hasReview = CoachData.HasContent(review);
        if (hasReview) nextFocus = FocusFor(review!, m.Key);
        bool canRewind = RewindData.CanRewind(s.Telemetry);
        var rewind = canRewind ? RewindData.Ready(s.Telemetry) : null;
        insightsPending = canRewind && rewind == null && hasReview;
        var extra = ExtraButtons(m);
        if (s.SensSearch is { } tested && !SensLab.SameSens(tested.Candidate, Main.I.SensFor(s.SensSection)))
            extra.Insert(0, (L("Validate candidate A/B", "Validar candidata A/B"),
                () => PracticeScreen.Compare(tested.Candidate, s.SensSection)));

        k = Mathf.Min(k, size.X / 1000f);
        float width = size.X - 108 * k;
        var screen = new PanelContainer { Size = size };
        var paint = new StyleBoxFlat { BgColor = new Color(0.039f, 0.051f, 0.063f) };
        paint.ContentMarginLeft = paint.ContentMarginRight = 54 * k;
        paint.ContentMarginTop = 42 * k;
        paint.ContentMarginBottom = 30 * k;
        screen.AddThemeStyleboxOverride("panel", paint);
        overlay!.AddChild(screen);
        screen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var page = Column(22);
        screen.AddChild(page);
        var header = Column(3);
        page.AddChild(header);
        AddText(header, L("ROUND RESULTS", "RESULTADOS"), 16, UiTheme.Teal);
        AddText(header, m.Name, 60, UiTheme.Text, display: true);
        AddText(header, string.Join("  ·  ", new[] { Loc.T(Difficulty.Get(s.Tier).Name), m.Subtitle }
            .Where(v => !string.IsNullOrEmpty(v))), 20, UiTheme.Dim);

        bool showScore = m.ShowScore && s.SensSearch == null && !s.CoachSuspended && s.SensComparisonTrial == null;
        var record = s.Record;
        bool showAccuracy = showScore && record is { Shots: > 0, Accuracy: >= 0 };
        if (s.CoachSuspended)
            AddText(page, L("Warm-up", "Calentamiento"), 26, UiTheme.Text, display: true);
        else if (record != null && (showScore || record.PracticeRank))
        {
            var metrics = Row(24);
            page.AddChild(metrics);
            if (showScore)
            {
                string? best = null;
                if (s.PrevBest >= 0)
                {
                    int delta = record.Score - s.PrevBest;
                    best = delta > 0 ? L("New personal best", "Nuevo récord personal")
                        : $"{L("Best", "Récord")} {Loc.Score(s.PrevBest)}  ({Loc.Delta(delta)})";
                }
                Metric(metrics, L("Score", "Puntuación"), Loc.Score(record.Score), UiTheme.Text, best);
            }
            int rank = Math.Clamp(record.Badge, 0, Difficulty.Tiers.Length - 1);
            Metric(metrics, record.PracticeRank ? L("Practice rank", "Rango de práctica") : L("Rank", "Rango"),
                Loc.T(Difficulty.Get(rank).Name), UiTheme.TierColor(rank));
            if (showAccuracy)
                Metric(metrics, L("Accuracy", "Precisión"), Loc.Numeric($"{record.Accuracy * 100:0.0}%"), UiTheme.Text,
                    $"{record.Hits}/{record.Shots} {L("hits", "aciertos")}");
            if (review?.Feedback is { } points)
                Metric(metrics, L("Practice points", "Puntos de práctica"), Loc.Score(points.Score), UiTheme.Text,
                    $"+{points.Earned} / −{points.Lost}");
        }
        Rule(page);

        var body = Row(37);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        page.AddChild(body);
        var summary = Column(16);
        summary.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        summary.SizeFlagsStretchRatio = hasReview ? 0.68f : 1f;
        body.AddChild(summary);
        AddText(summary, L("Round details", "Detalles de la ronda"), 29, UiTheme.Text, display: true);
        var stats = AddScroll(summary);
        foreach (var (label, value) in m.ResultLines())
            if (!showAccuracy || label != "Accuracy") AddStat(label, value);
        if (review?.Feedback is { } feedback)
        {
            if (!showScore)
                AddStat(L("Practice points", "Puntos de práctica"), $"{Loc.Score(feedback.Score)}  (+{feedback.Earned} / −{feedback.Lost})");
            foreach (var setting in feedback.Sensitivities.OrderByDescending(x => x.Hits + x.Misses))
                AddStat($"{L("Sens.", "Sens.")} {setting.Sensitivity:0.0000}",
                    $"{setting.Total:+0;-0;0} pts  ·  {setting.Hits} {L("hits", "aciertos")} / {setting.Misses} {L("misses", "fallos")}");
        }
        if (s.SensSearch is { } search)
            AddStat(L("Sensitivity search", "Búsqueda de sensibilidad"), !SensLab.SameSens(search.Baseline, search.Candidate)
                ? $"{search.Baseline:0.0000} → {search.Candidate:0.0000}"
                : L("Collect more consistent first-shot samples", "Faltan primeros tiros con un patrón consistente"));
        if (s.Live is { History.Count: > 0 } live)
            AddStat(L("Live coach", "Coach en directo"), string.Join(" · ", live.History.Where(h => !h.Positive)
                .Select(h => h.Headline).Distinct().DefaultIfEmpty(live.History[^1].Headline)));
        if (s.ProgressiveFrom is int from && s.ProgressiveTo is int to)
            AddStat(L("Next difficulty", "Próxima dificultad"),
                $"{Loc.T(Difficulty.Get(from).Name)} → {Loc.T(Difficulty.Get(to).Name)}");
        if (review?.Sens is { } sens)
        {
            Rule(stats);
            AddText(stats, L("Sensitivity", "Sensibilidad"), 24, UiTheme.Text, display: true);
            AddText(stats, SensLabText.VerdictSentence(sens), 17, UiTheme.Text);
            string segment = review.Metrics.GetValueOrDefault("sens.changed") > 0
                ? L("Latest segment", "Último tramo") + $" · {review.Metrics.GetValueOrDefault("current.sens"):0.0000}"
                : $"{s.Record?.Sens:0.0000}";
            AddText(stats, $"{segment} · {SensLabText.Metrics(sens)} · {SensLabText.EvidenceCaption(sens.Confidence)}", 15, UiTheme.Dim);
            AddText(stats, SensLabText.Advice(sens), 16, UiTheme.Dim);
            ActionButton(stats, L("Practise sensitivity", "Practicar sensibilidad"),
                () => PracticeScreen.Start(CoachPlan.FromRun(review), CoachData.Ready ?? new SkillProfile()));
        }

        var extras = new List<(VButton Button, string Label)>();
        foreach (var (label, action) in extra)
        {
            VButton? button = null;
            button = ActionButton(stats, label, () =>
            {
                try { action(); }
                catch (Exception error) { GD.PushWarning($"Result action failed: {error.Message}"); return; }
                foreach (var (item, title) in extras)
                {
                    item.Label = item == button ? $"✓  {title}" : title;
                    item.QueueRedraw();
                }
            });
            extras.Add((button, label));
        }

        if (hasReview) BuildReview(body, review!);
        Rule(page);
        var actions = Row(12);
        actions.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        page.AddChild(actions);
        ActionButton(actions, L("Play again · R", "Otra vez · R"), s.Restart, VButton.Look.Primary, minimumWidth: 192);
        if (canRewind) ActionButton(actions, L("Rewind", "Rebobinar"), () => OpenRewind(null), minimumWidth: 144);
        ActionButton(actions, L("Menu · Esc", "Menú · Esc"), Main.I.ShowMenu, minimumWidth: 144);
        if (canRewind && Main.I.Dev && !devRewindShown && CmdLine.After("--rewind-at") != null)
        {
            devRewindShown = true;
            Callable.From(() => OpenRewind(CmdLine.After("--rewind-code"))).CallDeferred();
        }

        VBoxContainer Column(float gap)
        {
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", (int)(gap * k));
            return col;
        }

        HBoxContainer Row(float gap)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", (int)(gap * k));
            return row;
        }

        void Rule(Control parent)
        {
            parent.AddChild(new ColorRect { Color = UiTheme.Line, CustomMinimumSize = new Vector2(0, 1), MouseFilter = MouseFilterEnum.Ignore });
        }

        void AddText(Control parent, string text, float px, Color color, bool display = false)
        {
            var label = new Label
            {
                Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore,
                LabelSettings = new LabelSettings { Font = display ? UiTheme.Hud : UiTheme.Body,
                    FontColor = color, FontSize = UiTheme.Fs(px, k) },
            };
            parent.AddChild(label);
        }

        void Metric(Control parent, string label, string value, Color color, string? detail = null)
        {
            var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            panel.AddThemeStyleboxOverride("panel", Surface(new Color(0.068f, 0.087f, 0.103f), 18));
            parent.AddChild(panel);
            var column = Column(2);
            panel.AddChild(column);
            AddText(column, label, 17, UiTheme.Dim);
            AddText(column, value, 48, color, display: true);
            if (detail != null) AddText(column, detail, 15, UiTheme.Dim);
        }

        StyleBoxFlat Surface(Color fill, float padding)
        {
            var style = new StyleBoxFlat { BgColor = fill };
            style.SetCornerRadiusAll((int)(6 * k));
            style.ContentMarginLeft = style.ContentMarginRight = padding * k;
            style.ContentMarginTop = style.ContentMarginBottom = padding * k;
            return style;
        }

        VBoxContainer AddScroll(Control parent)
        {
            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                FollowFocus = true,
            };
            parent.AddChild(scroll);
            var content = Column(11);
            content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(content);
            return content;
        }

        void AddStat(string caption, string value)
        {
            var row = Row(14);
            stats.AddChild(row);
            AddText(row, caption, 17, UiTheme.Dim);
            AddText(row, Loc.Numeric(value), 18, UiTheme.Text);
        }

        VButton ActionButton(Control parent, string label, Action action, VButton.Look kind = VButton.Look.Secondary,
            float minimumWidth = 0)
        {
            float labelWidth = Gfx.TextW(UiTheme.Body, Loc.T(label), UiTheme.Fs(17, k)) + 32 * k;
            var button = new VButton
            {
                K = k, Label = label, Kind = kind, FontPx = 17,
                SizeFlagsHorizontal = parent is HBoxContainer ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin,
                CustomMinimumSize = new Vector2(Mathf.Clamp(labelWidth, minimumWidth * k, width * 0.38f), 47 * k),
            };
            button.Pressed += action;
            parent.AddChild(button);
            return button;
        }

        void BuildReview(Control parent, RunReview coach)
        {
            var col = Column(16);
            col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            parent.AddChild(col);
            AddText(col, L("Coach review", "Revisión del coach"), 29, UiTheme.Text, display: true);
            var detail = AddScroll(col);
            if (coach.NextTip.Length > 0)
            {
                var callout = new PanelContainer();
                callout.AddThemeStyleboxOverride("panel", Surface(new Color(0.069f, 0.118f, 0.123f), 18));
                detail.AddChild(callout);
                var tip = Column(6);
                callout.AddChild(tip);
                AddText(tip, L("Next attempt", "Próximo intento"), 16, UiTheme.Teal);
                AddText(tip, coach.NextTip, 25, UiTheme.Text, display: true);
            }
            if (coach.Summary.Length > 0)
                AddText(detail, coach.Summary, 17, UiTheme.Dim);
            if (coach.Strengths.Count > 0)
            {
                Rule(detail);
                AddText(detail, L("What went well", "Lo que salió bien"), 17, UiTheme.Good, display: true);
                foreach (var strength in coach.Strengths)
                    AddText(detail, strength, 16, UiTheme.Text);
            }
            foreach (var issue in coach.Issues)
            {
                Rule(detail);
                AddText(detail, issue.Title, 19, UiTheme.Text, display: true);
                if (issue.Evidence.Length > 0) AddText(detail, issue.Evidence, 16, UiTheme.Dim);
                foreach (var fix in issue.Fixes ?? Array.Empty<string>())
                    AddText(detail, "•  " + Loc.T(fix), 16, UiTheme.Text);
                var drill = (issue.Drills ?? Array.Empty<string>()).FirstOrDefault(CoachData.HasDrill);
                bool canShowMoment = rewind?.Moments.Any(moment => moment.Code == issue.Id) == true;
                if (drill != null || canShowMoment)
                {
                    var links = Row(10);
                    links.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
                    detail.AddChild(links);
                    if (drill != null)
                        ActionButton(links, L("Practise", "Practicar") + $" · {CoachData.DrillName(drill)}",
                            () => CoachData.StartDrill(drill, CoachData.FocusFrom(issue)));
                    if (canShowMoment)
                        ActionButton(links, L("Show moment", "Ver momento"), () => OpenRewind(issue.Id));
                }
            }
            if (coach.Timeline?.Actionable(6).ToList() is { Count: > 0 } findings)
            {
                Rule(detail);
                AddText(detail, L("During the round", "Durante la ronda"), 18, UiTheme.Text, display: true);
                foreach (var finding in findings)
                {
                    AddText(detail, finding.Title(), 16, TimelineUi.Tone(finding.Tone));
                    AddText(detail, finding.Detail(), 15, UiTheme.Dim);
                }
            }
            if (coach.Plays is { HasData: true } plays)
                ActionButton(detail, L("Play by play", "Jugada a jugada"), () => OpenPlays(plays));
            ActionButton(detail, L("Coach profile", "Perfil del coach"), Main.I.ShowProfile);
        }
    }

    // ---------------- coach review (results) ----------------

    static readonly bool DevFakeButtons = OS.GetCmdlineUserArgs().Concat(OS.GetCmdlineArgs()).Contains("--fakebuttons");

    /// <summary>The results are up and the run's insights are still building (see <see cref="_Process"/>).</summary>
    bool insightsPending;
    static bool devRewindShown;

    /// <summary>Rewind of the run that just finished (in the session's world, so PLAY AGAIN frees it), optionally
    /// focused on one moment code (a coach issue's "Show me").</summary>
    void OpenRewind(string? code)
    {
        if (s.World == null || !IsInstanceValid(s.World) || !RewindData.CanRewind(s.Telemetry)) return;
        if (s.World.GetNodeOrNull("Rewind") is { } old) old.Free();
        RewindScreen.Open(s.World, s.Telemetry, null, code);
    }

    /// <summary>Play-by-play over the results: the results panel hides and stops taking input while the list is up
    /// (it comes back when the list closes, so PLAY AGAIN / REWIND / MENU are still there).</summary>
    void OpenPlays(PlayAnalysis plays)
    {
        if (!plays.HasData) return;
        if (overlay != null) overlay.Visible = false;
        SetProcessInput(false);
        var list = PlaysScreen.Open(this, s.Telemetry, plays, RewindData.Ready(s.Telemetry));
        list.Closed = () =>
        {
            if (!IsInstanceValid(this)) return;
            SetProcessInput(true);
            if (overlay != null) overlay.Visible = true;
        };
    }

    static List<(string Label, Action Act)> ExtraButtons(TrainingMode m)
    {
        List<(string Label, Action Act)> list;
        try { list = m.ResultButtons().Take(3).ToList(); }
        catch (Exception ex) { GD.PushWarning($"ResultButtons failed: {ex.Message}"); list = new(); }
        // Dev-only layout check of the extra-button row (--dev --fakecoach --fakebuttons).
        if (list.Count == 0 && CoachData.Fake && DevFakeButtons) list.Add(("Use 0.190 in CA-VAL-AIM", () => { }));
        return list;
    }

    /// <summary>Focus for the next run of this drill: the first issue it trains (or the top issue).</summary>
    static CoachData.Focus? FocusFor(RunReview r, string modeKey)
    {
        var d = r.Issues.FirstOrDefault(i => (i.Drills ?? Array.Empty<string>()).Contains(modeKey)) ?? r.Issues.FirstOrDefault();
        return d == null ? null : CoachData.FocusFrom(d)! with { Mode = modeKey };
    }

}
