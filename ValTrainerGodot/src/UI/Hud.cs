using System.Collections.Generic;
using System.Globalization;
using Godot;
using ValTrainer.Analysis;
using ValTrainer.Audio;
using ValTrainer.Core;
using ValTrainer.Game;
using ValTrainer.Maps;
using ValTrainer.Modes;
using ValTrainer.Valorant;

namespace ValTrainer.UI;

/// <summary>
/// In-game HUD in VALORANT's layout: top-centre timer flanked by score (teal) and kills/accuracy (red), drill
/// info top-left, health + shield and ammo + reserve bottom-centre, reload progress, speed meter for movement
/// drills, damage direction arc + vignette, kill banner, session banners, countdown, pause menu and results
/// (with a procedurally drawn rank emblem). Reads everything from the session; no game logic here.
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
    readonly List<BaseButton> overlayButtons = new();
    BaseButton? resetSens;
    LineEdit? sensEdit;
    float overlayAt;
    int locRev = Loc.Revision;
    List<(string Label, string Value)> resultLines = new();

    // aim coach: cue shown during the countdown (from a "Practise" button or the last run's review)
    CoachData.Focus? focus, nextFocus;

    // animation bookkeeping (real time, so it keeps moving while paused)
    float lastStopSeen = -2f, stopPopAt = -99f;

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

    // uppercase labels cached per mode / subtitle / weapon (avoid per-frame string allocations)
    string nameUpper = "", subSrc = "", subUpper = "", gunSrc = "", gunUpper = "";

    string Upper(ref string src, ref string upper, string value)
    {
        if (!ReferenceEquals(src, value) && src != value) { src = value; upper = value.ToUpperInvariant(); }
        return upper;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
        if (s.Mode == null) return;
        var size = GetViewportRect().Size;
        if (locRev != Loc.Revision)
        {
            locRev = Loc.Revision;
            nameUpper = Loc.T(s.Mode.Name).ToUpperInvariant();
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
                nameUpper = Loc.T(s.Mode.Name).ToUpperInvariant();
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
            if (resetSens != null) resetSens.Visible = Main.I.Settings.UseSensOverride;
        }
    }

    /// <summary>
    /// Overlay buttons are activated here, before <see cref="GameSession"/> sees the click: otherwise the press would
    /// leave a pending "fire" edge that shoots on resume. Presses are swallowed while an overlay is up.
    /// </summary>
    public override void _Input(InputEvent e)
    {
        if (overlay == null) return;
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
            if (sensEdit != null && IsInstanceValid(sensEdit) && sensEdit.IsVisibleInTree() && sensEdit.GetGlobalRect().HasPoint(mb.Position))
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
        DrawTopBar(size, k, m);
        DrawModeInfo(size, k, m);
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
            else if (s.State == GameSession.St.Running) DrawMinimap(size, k, mapSpot);
        }
        if (!m.Is2D && s.State == GameSession.St.Running && s.GuideMode >= 1) DrawGuide(size, k, m);
        if (!m.Is2D)
        {
            if (m.ShowScore) DrawVitals(size, k);
            if (s.Gun != null && !m.EffectiveInfiniteAmmo) DrawAmmo(size, k);
            if (m.Movement) DrawSpeed(size, k);
            DrawReload(size, k);
        }
        if (m.Prompt is { } p) DrawPrompt(size, k, Main.I.Valorant.Binds.Hint(p));
        if (s.BannerTime > 0 && s.BannerText.Length > 0) DrawBanner(size, k);
        if (s.State == GameSession.St.Running && s.Live?.Current is { } tip) DrawLiveTip(size, k, tip);
        if (m.KillBanner.Time > 0 && m.KillBanner.Count > 0) DrawKillBanner(size, k, m.KillBanner.Count, m.KillBanner.Time);
        if (s.State == GameSession.St.Countdown) DrawCountdown(size, k, m);
        if (focus != null && (s.State == GameSession.St.Countdown || (s.State == GameSession.St.Running && s.Now < 2f))) DrawFocus(size, k, focus);
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
            var eye = cam.GlobalPosition;
            float deg = Mathf.RadToDeg((-cam.GlobalBasis.Z).AngleTo((f.Point - eye).Normalized()));
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
        float cx = size.X / 2, top = 14 * k, bh = 56 * k, tw = 128 * k, sw = 168 * k, gap = 6 * k;

        if (s.FreePlay)
        {
            // free play: no clock and no score (the drill starts over by itself when its time is up)
            float fw = 210 * k;
            var fr = new Rect2(cx - fw / 2, top, fw, bh);
            DrawRect(fr, UiTheme.Plate);
            DrawRect(new Rect2(fr.Position.X, fr.End.Y - 2 * k, fw, 2 * k), new Color(UiTheme.Teal, 0.7f));
            int ffs = UiTheme.Fs(24, k), fls = UiTheme.Fs(12, k);
            TC(UiTheme.HudWide, "FREE PLAY", cx, Gfx.Mid(top + bh / 2, ffs), ffs, UiTheme.Text);
            TC(UiTheme.HudWide, "NO SCORE · ESC TO STOP", cx, top + bh + 18 * k, fls, UiTheme.Dim);
            return;
        }

        // timer
        float remain = m.Timed ? Mathf.Max(0, m.Duration - s.Now) : s.Now;
        int secs = m.Timed ? Mathf.CeilToInt(remain) : (int)remain;
        var tr = new Rect2(cx - tw / 2, top, tw, bh);
        DrawRect(tr, UiTheme.Plate);
        DrawRect(new Rect2(tr.Position.X, tr.End.Y - 2 * k, tw, 2 * k), new Color(UiTheme.Text, 0.18f));
        bool hurry = m.Timed && remain < 10f && s.State == GameSession.St.Running;
        int fs = UiTheme.Fs(42, k);
        string clock = cClock.Get(secs, v => $"{v / 60}:{v % 60:00}");
        float sc = 1f;
        if (hurry) { float f = remain - Mathf.Floor(remain); sc = 1f + 0.14f * Mathf.Max(0, (f - 0.7f) / 0.3f); }
        var clockCol = hurry ? UiTheme.Accent : m.Timed ? UiTheme.Text : UiTheme.Dim;
        ScaledTextC(UiTheme.Display, clock, new Vector2(cx, Gfx.Mid(top + bh / 2, fs)), fs, clockCol, sc);
        if (m.Timed)
        {
            float frac = m.Duration > 0 ? remain / m.Duration : 0;
            DrawRect(new Rect2(tr.Position.X, tr.End.Y - 2 * k, tw * frac, 2 * k), hurry ? UiTheme.Accent : UiTheme.Text);
        }

        if (!m.ShowScore) return; // drills without a meaningful score/accuracy (Sens Finder): clock only

        // score (teal) — left
        var lr = new Rect2(tr.Position.X - gap - sw, top, sw, bh);
        Gfx.Slant(this, lr, 20 * k, true, new Color(UiTheme.Teal, 0.85f));
        int vs = UiTheme.Fs(40, k), ls = UiTheme.Fs(12, k);
        TC(UiTheme.Display, cScore.Get(m.Score, v => Loc.Score(v)), lr.GetCenter().X + 8 * k, Gfx.Mid(top + bh / 2, vs), vs, UiTheme.Text);
        TC(UiTheme.HudWide, "SCORE", lr.GetCenter().X + 8 * k, top + bh + 18 * k, ls, UiTheme.Teal);

        // kills / accuracy (red) — right
        var rr = new Rect2(tr.End.X + gap, top, sw, bh);
        Gfx.Slant(this, rr, 20 * k, false, new Color(UiTheme.Accent, 0.85f));
        int accTenths = (int)(m.Accuracy * 1000);
        string right = m.UsesHeadshots ? cRight.Get(m.Kills, v => v.ToString(Inv)) : cRight.Get(accTenths + 1_000_000L, v => $"{(v - 1_000_000) / 10f:0}%");
        TC(UiTheme.Display, right, rr.GetCenter().X - 8 * k, Gfx.Mid(top + bh / 2, vs), vs, UiTheme.Text);
        string rl = m.UsesHeadshots
            ? cRightLabel.Get(accTenths / 10, v => Loc.Spanish ? $"BAJAS · {v}% PREC." : $"KILLS · {v}% ACC")
            : "ACCURACY";
        TC(UiTheme.HudWide, rl, rr.GetCenter().X - 8 * k, top + bh + 18 * k, ls, UiTheme.Accent);
    }

    void DrawModeInfo(Vector2 size, float k, TrainingMode m)
    {
        float x = 30 * k, y = 22 * k;
        DrawRect(new Rect2(x, y, 4 * k, 36 * k), UiTheme.Accent);
        int ns = UiTheme.Fs(34, k);
        TL(UiTheme.Display, nameUpper, x + 14 * k, Gfx.Mid(y + 18 * k, ns), ns, UiTheme.Text);
        y += 56 * k;
        var tier = Difficulty.Get(s.Tier);
        int ts = UiTheme.Fs(13, k);
        if (m.ShowScore) RankEmblem.Draw(this, new Vector2(x + 9 * k, y), 9 * k, tier.Id);
        if (m.ShowScore) TL(UiTheme.HudWide, cTier.Get(tier.Id, v => $"{Difficulty.Get((int)v).Name.ToUpperInvariant()} · {Difficulty.Get((int)v).Ranks.ToUpperInvariant()}"),
            x + 26 * k, Gfx.Mid(y, ts), ts, UiTheme.TierColor(tier.Id));
        if (m.Subtitle is { } sub)
        {
            y += 24 * k;
            TL(UiTheme.HudWide, Upper(ref subSrc, ref subUpper, sub), x + 26 * k, Gfx.Mid(y, ts), ts, new Color(UiTheme.Text, 0.85f));
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
    /// Minimap art of the played site, always in the top-right corner while a map drill runs. The blockout
    /// stays the 3D world; the panel is the official minimap read of it: site wash, spawn/choke and the
    /// player arrow. Hidden during the countdown, which uses the bigger <see cref="DrawBriefing"/> instead.
    /// </summary>
    void DrawMinimap(Vector2 size, float k, MapSpot map)
    {
        float w = Mathf.Min(210 * k, size.X * 0.24f);
        var r = new Rect2(size.X - 26 * k - w, 50 * k, w, w);
        MapArt.Panel(this, r, map, k, callouts: false, player: true, s.View.Eye, s.View.ViewYaw, 0.92f);
        int fs = UiTheme.Fs(13, k);
        RawR(UiTheme.HudWide, $"{map.Map.ToUpperInvariant()} · {MapArt.SiteName(map.Key)}", r.End.X, r.End.Y + 18 * k, fs,
            new Color(UiTheme.Text, 0.85f));
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
        float cx = size.X / 2, bh = 74 * k, by = size.Y - 30 * k - bh, bw = 300 * k;
        var r = new Rect2(cx - 140 * k - bw, by, bw, bh);
        Gfx.PlateEx(this, r, 12 * k, true, false, false, true, UiTheme.Plate, new Color(UiTheme.Text, 0.09f), 1f);
        float hp = Mathf.Max(0, s.Player.Hp), sh = Mathf.Max(0, s.Player.Shield);
        bool low = hp < 30;

        // shield icon + value
        float sx = r.Position.X + 26 * k, sy = r.Position.Y + 30 * k, ss = 12 * k;
        Span<Vector2> shield = stackalloc Vector2[5];
        shield[0] = new(sx - ss, sy - ss); shield[1] = new(sx + ss, sy - ss); shield[2] = new(sx + ss, sy + ss * 0.2f);
        shield[3] = new(sx, sy + ss * 1.25f); shield[4] = new(sx - ss, sy + ss * 0.2f);
        this.FillPoly(shield, new Color(UiTheme.Text, sh > 0 ? 0.8f : 0.25f));
        int svs = UiTheme.Fs(24, k);
        TL(UiTheme.Hud, cShield.Get(Mathf.CeilToInt(sh), v => v.ToString(Inv)), sx + ss + 10 * k, Gfx.Mid(sy, svs), svs, sh > 0 ? UiTheme.Text : UiTheme.Faint);

        // health (big, right side of the plate)
        int hs = UiTheme.Fs(66, k);
        TR(UiTheme.Display, cHp.Get(Mathf.CeilToInt(hp), v => v.ToString(Inv)), r.End.X - 22 * k, Gfx.Mid(r.Position.Y + 32 * k, hs), hs,
            low ? UiTheme.Accent : UiTheme.Text);

        // bars: shield (thin) over health
        float bx = r.Position.X + 16 * k, bwid = r.Size.X - 32 * k, hy = r.End.Y - 11 * k;
        DrawRect(new Rect2(bx, hy, bwid, 4 * k), new Color(1, 1, 1, 0.12f));
        DrawRect(new Rect2(bx, hy, bwid * Mathf.Clamp(hp / 100f, 0, 1), 4 * k), low ? UiTheme.Accent : UiTheme.Text);
        if (sh > 0) DrawRect(new Rect2(bx, hy - 4 * k, bwid * Mathf.Clamp(sh / 50f, 0, 1), 2 * k), new Color(0.62f, 0.8f, 1f, 0.85f));
    }

    void DrawAmmo(Vector2 size, float k)
    {
        var gun = s.Gun!;
        float cx = size.X / 2, bh = 74 * k, by = size.Y - 30 * k - bh, bw = 300 * k;
        var r = new Rect2(cx + 140 * k, by, bw, bh);
        Gfx.PlateEx(this, r, 12 * k, true, false, false, true, UiTheme.Plate, new Color(UiTheme.Text, 0.09f), 1f);
        bool low = gun.Ammo <= Math.Max(1, gun.Def.Mag / 5);
        int as_ = UiTheme.Fs(66, k), rs = UiTheme.Fs(26, k), ns = UiTheme.Fs(12, k);
        string ammo = cAmmo.Get(gun.Ammo, v => v.ToString(Inv));
        float x = r.Position.X + 22 * k, baseY = Gfx.Mid(r.Position.Y + 32 * k, as_);
        var ac = gun.Reloading ? UiTheme.Faint : low ? UiTheme.Accent : UiTheme.Text;
        TL(UiTheme.Display, ammo, x, baseY, as_, ac);
        x += Gfx.TextW(UiTheme.Display, ammo, as_) + 10 * k;
        TL(UiTheme.Hud, cReserve.Get(gun.Reserve, v => $"/ {v}"), x, baseY, rs, UiTheme.Dim);
        TR(UiTheme.HudWide, Upper(ref gunSrc, ref gunUpper, gun.Def.Name), r.End.X - 22 * k, r.Position.Y + 24 * k, ns, UiTheme.Dim);
        // magazine bar
        float bx = r.Position.X + 16 * k, bwid = r.Size.X - 32 * k, hy = r.End.Y - 11 * k;
        DrawRect(new Rect2(bx, hy, bwid, 4 * k), new Color(1, 1, 1, 0.12f));
        float frac = gun.Reloading ? 1f - gun.ReloadLeft / Mathf.Max(0.01f, gun.Def.Reload) : gun.Ammo / (float)Math.Max(1, gun.Def.Mag);
        DrawRect(new Rect2(bx, hy, bwid * Mathf.Clamp(frac, 0, 1), 4 * k), gun.Reloading ? UiTheme.Warn : low ? UiTheme.Accent : UiTheme.Text);
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

    void DrawCountdown(Vector2 size, float k, TrainingMode m)
    {
        int n = Mathf.Max(1, Mathf.CeilToInt(s.Countdown));
        float frac = Mathf.Clamp(s.Countdown - (n - 1), 0, 1); // 1 → 0 within each second
        var c = new Vector2(size.X / 2, size.Y * 0.3f);
        float r = 104 * k;
        DrawCircle(c, r + 18 * k, new Color(0.03f, 0.05f, 0.08f, 0.45f));
        DrawArc(c, r, 0, Mathf.Tau, 72, new Color(UiTheme.Text, 0.12f), 3 * k, true);
        DrawArc(c, r, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * frac, 72, UiTheme.Accent, 4 * k, true);
        float pulse = frac > 0.78f ? (frac - 0.78f) / 0.22f : 0f;
        float sc = 1f + 0.4f * pulse * pulse;
        int fs = UiTheme.Fs(150, k);
        ScaledTextC(UiTheme.Display, cCount.Get(n, v => v.ToString(Inv)), new Vector2(c.X, Gfx.Mid(c.Y, fs)), fs, new Color(UiTheme.Text, 1f - 0.5f * pulse), sc);
        int ls = UiTheme.Fs(14, k);
        TC(UiTheme.HudWide, "GET READY", c.X, c.Y - r - 34 * k, ls, UiTheme.Dim);
        int ns = UiTheme.Fs(30, k), ds = UiTheme.Fs(16, k);
        TC(UiTheme.Display, nameUpper, c.X, c.Y + r + 54 * k, ns, UiTheme.Text);
        TC(UiTheme.Body, m.Description, c.X, c.Y + r + 82 * k, ds, UiTheme.Dim);
        if (m.MadeBy is { } by) TC(UiTheme.HudWide, $"MADE BY THE COMMUNITY · @{by}", c.X, c.Y + r + 110 * k, ls, UiTheme.Good);
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
            TC(UiTheme.HudWide, hint, c.X, c.Y + r + (m.MadeBy != null ? 136 : 110) * k, ls, UiTheme.Teal);
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
        overlayButtons.Clear();
        resetSens = null;
        sensEdit = null;
        insightsPending = false;
        if (s.State is not (GameSession.St.Paused or GameSession.St.Results)) return;

        overlaySize = size;
        if (!keepTime) overlayAt = RealNow;
        float k = UiTheme.S(size);
        overlay = new DrawBox { MouseFilter = MouseFilterEnum.Stop };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        overlay.Theme = UiTheme.MakeTheme(k);
        AddChild(overlay);
        if (s.State == GameSession.St.Paused) BuildPause(size, k);
        else BuildResults(size, k);
        Loc.Apply(overlay);
    }

    VButton OverlayButton(string label, VButton.Look look, Rect2 r, float k, Action a, float fontPx = 22)
    {
        var b = new VButton { Label = label, Kind = look, K = k, FontPx = fontPx, Position = r.Position, Size = r.Size, CustomMinimumSize = r.Size };
        b.Pressed += a;
        overlay!.AddChild(b);
        overlayButtons.Add(b);
        return b;
    }

    static void Dimmer(CanvasItem ci, Vector2 size, float k, float a, float t = 0f)
    {
        ci.DrawRect(new Rect2(Vector2.Zero, size), new Color(0.02f, 0.03f, 0.05f, a));
        // vignette: gradient bands pressing in the top and bottom edges
        float m = Mathf.Min(size.Y * 0.34f, 190 * k);
        var edge = new Color(0, 0, 0, Mathf.Clamp(a - 0.32f, 0, 0.6f));
        Span<Vector2> q = stackalloc Vector2[4];
        Span<Color> c = stackalloc Color[4];
        q[0] = new(0, 0); q[1] = new(size.X, 0); q[2] = new(size.X, m); q[3] = new(0, m);
        c[0] = new Color(0, 0, 0, 0); c[1] = c[0]; c[2] = edge; c[3] = c[2];
        ci.GradPoly(q, c);
        q[0] = new(0, size.Y - m); q[1] = new(size.X, size.Y - m); q[2] = new(size.X, size.Y); q[3] = new(0, size.Y);
        c[0] = edge; c[1] = c[0]; c[2] = new Color(0, 0, 0, 0); c[3] = c[2];
        ci.GradPoly(q, c);
        // hairline hatch in both diagonals
        var line = new Color(1, 1, 1, 0.011f);
        float step = 62 * k;
        for (float x = -size.Y; x < size.X; x += step)
            ci.DrawLine(new Vector2(x, size.Y), new Vector2(x + size.Y * 0.58f, 0), line, 1f);
        var line2 = new Color(1, 1, 1, 0.006f);
        for (float x = 0; x < size.X + size.Y; x += step * 1.7f)
            ci.DrawLine(new Vector2(x, 0), new Vector2(x - size.Y * 0.58f, size.Y), line2, 1f);
        // a slow accent light bundle sweeping across at 45°
        if (t > 0)
        {
            float cx = Mathf.Lerp(-size.Y * 0.6f, size.X + size.Y * 0.3f, (t * 0.05f) % 1f);
            for (int i = 0; i < 13; i++)
            {
                float fall = 1f - MathF.Abs(i - 6f) / 7f;
                ci.DrawLine(new Vector2(cx + (i - 6) * 16 * k, size.Y + 12), new Vector2(cx + (i - 6) * 16 * k + size.Y * 0.58f, -12),
                    new Color(UiTheme.Accent, 0.018f * fall), 1.5f);
            }
        }
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
            tierNote.Text = $"{t.Ranks}  ·  ~{t.Bot.ReactMs:0} ms  ·  " + L("from the next run", "desde la próxima partida");
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
        left.AddChild(Cap(L("WEAPON", "ARMA"), 0.35f));
        Gap(left, 2);
        var weapon = new MWeaponSelector(overlay, k) { SizeFlagsHorizontal = SizeFlags.ShrinkBegin, Delay = 0.36f };
        left.AddChild(weapon);
        left.AddChild(new MText
        {
            K = k, Font = UiTheme.Body, Px = 11.5f, Color = UiTheme.Faint, Delay = 0.37f,
            Text = L("every drill · applies from the next run", "cada ejercicio · se aplica en la próxima partida"),
        });

        // ---- right: sensitivity, audio, graphics ----
        right.AddChild(Cap(L("SENSITIVITY", "SENSIBILIDAD"), 0.14f));
        Gap(right, 6);
        var sensRow = ArrowRow();
        MText sensNote = null!; // declared first: the arrow/use lambdas can run before the note is built
        MLink useVal = null!;
        var sPrev = Arrow("‹", () => AdjustSens(-1), 0.17f);
        sensRow.AddChild(sPrev);
        sensEdit = new LineEdit
        {
            CustomMinimumSize = new Vector2(0, 42 * k), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MaxLength = 8, Alignment = HorizontalAlignment.Center, SelectAllOnFocus = true,
            TooltipText = L("Click and type a sensitivity, then Enter. Shift-click the arrows for ±0.001",
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
        sensRow.AddChild(sNext);
        right.AddChild(sensRow);
        sensNote = new MText
        {
            K = k, Font = UiTheme.HudWide, Px = 10.5f, Color = UiTheme.Faint, Delay = 0.18f,
            CustomMinimumSize = new Vector2(0, 18 * k),
        };
        right.AddChild(sensNote);
        useVal = Link(L("USE VALORANT SENSITIVITY", "USAR SENSIBILIDAD DE VALORANT"), () =>
        {
            st.UseSensOverride = false;
            st.Save();
            PaintSens();
        }, 0.2f, 10.5f);
        useVal.CustomMinimumSize = new Vector2(0, 24 * k);
        resetSens = useVal;
        right.AddChild(useVal);

        void AdjustSens(int d)
        {
            if (!st.UseSensOverride) { st.UseSensOverride = true; st.SensOverride = app.Valorant.Sensitivity; }
            float step = Input.IsKeyPressed(Key.Shift) ? 0.001f : 0.01f;
            st.SensOverride = MathF.Max(0.001f, MathF.Round((st.SensOverride + d * step) * 1000f) / 1000f);
            st.Save();
            PaintSens();
        }
        void PaintSens()
        {
            if (sensEdit != null && IsInstanceValid(sensEdit) && !sensEdit.HasFocus())
                sensEdit.Text = app.Sens.ToString("0.000", Inv);
            string note = st.UseSensOverride
                ? $"CUSTOM · {PlayerView.Cm360(app.Sens, st.Dpi):0.0} CM/360"
                : $"FROM VALORANT · {PlayerView.Cm360(app.Sens, st.Dpi):0.0} CM/360";
            sensNote.Text = Loc.Numeric(L(note, note.Replace("CUSTOM", "PERSONALIZADA").Replace("FROM VALORANT", "DE VALORANT")));
            sensNote.QueueRedraw();
            useVal.Visible = st.UseSensOverride;
        }
        // Enter or clicking away applies a valid value; anything else snaps back to the current sensitivity.
        void ApplySens()
        {
            if (sensEdit == null || !IsInstanceValid(sensEdit)) return;
            if (SensInput.TryParse(sensEdit.Text, out var v) && (!st.UseSensOverride || v != st.SensOverride))
            {
                st.UseSensOverride = true;
                st.SensOverride = v;
                st.Save();
            }
            PaintSens();
        }
        sensEdit.TextSubmitted += _ => sensEdit?.ReleaseFocus();
        sensEdit.FocusExited += ApplySens;
        sensEdit.Text = app.Sens.ToString("0.000", Inv);
        PaintSens();

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
        resultLines = m.ResultLines().ToList();
        if (s.Live is { History.Count: > 0 } live)
            resultLines.Add((Loc.Spanish ? "Coach en directo" : "Live coach",
                string.Join(" · ", live.History.Where(h => !h.Positive).Select(h => h.Headline).Distinct().DefaultIfEmpty(live.History[^1].Headline))));
        if (s.ProgressiveFrom is int pf && s.ProgressiveTo is int pt)
            resultLines.Add((L("Progressive difficulty", "Dificultad progresiva"),
                $"{(pt > pf ? "↑" : "↓")}  {Loc.T(Difficulty.Get(pf).Name)} → {Loc.T(Difficulty.Get(pt).Name)}  ·  " +
                L("applies to the next run", "se aplica en la próxima partida")));
        var rec = s.Record;
        int prev = s.PrevBest;
        int tierId = s.Tier;
        int badge = rec?.Badge ?? m.Badge();
        int score = rec?.Score ?? m.Score;
        bool first = prev < 0, pb = !first && score > prev;
        var tier = Difficulty.Get(tierId);
        string scoreText = Loc.Score(score);
        string modeName = m.Name.ToUpperInvariant();
        string tl = $"{tier.Name.ToUpperInvariant()} · {tier.Ranks.ToUpperInvariant()}";
        string? subUp = m.Subtitle?.ToUpperInvariant();
        string chip = pb ? "NEW PERSONAL BEST" : $"FIRST RUN AT {tier.Name.ToUpperInvariant()}";
        string prevText = pb ? $"previous best {Loc.Score(prev)}" : $"{tier.Name} best: {Loc.Score(prev)}  ({Loc.Delta(score - prev)})";
        string badgeName = badge >= 0 ? Difficulty.Tiers[badge].Name.ToUpperInvariant() : "BELOW ROOKIE";
        string badgeRanks = badge >= 0 ? Difficulty.Tiers[badge].Ranks : "keep practising";
        bool showScore = m.ShowScore;
        float scoreH = showScore ? 292 * k : 40 * k; // score + badge block (hidden for drills like the Sens Finder)

        // aim coach review (right-hand panel) and mode-specific extra buttons
        var review = CoachData.ReviewFor(s);
        bool hasReview = CoachData.HasContent(review);
        if (hasReview) nextFocus = FocusFor(review!, m.Key);
        var extra = ExtraButtons(m);
        var sens = review?.Sens;
        bool showSens = showScore && sens is { HasData: true } && extra.Count == 0;
        if (hasReview) k = Mathf.Min(k, size.X / 1764f); // both panels side by side must fit (4:3 / 5:4 windows)
        float sensH = showSens ? 128 * k : 0;
        // Rewind: any run with a target in its frames; its insights build on a worker (the review's "Show me" links
        // appear when they're ready: _Process re-lays the overlay once).
        var tel = s.Telemetry;
        bool canRewind = RewindData.CanRewind(tel);
        var rins = canRewind ? RewindData.Ready(tel) : null;
        insightsPending = canRewind && rins == null && hasReview;
        Func<string, bool>? showable = rins == null ? null : id => rins.Moments.Any(mo => mo.Code == id);

        bool twoCol = resultLines.Count > 5;
        int lineRows = twoCol ? (resultLines.Count + 1) / 2 : resultLines.Count;
        float pw = (hasReview ? 900 : 1060) * k, rw = hasReview ? 760 * k : 0, gap = hasReview ? 24 * k : 0;
        float extraH = extra.Count > 0 ? 72 * k : 0;
        float maxH = size.Y - 60 * k;
        float ph = Mathf.Min(maxH, 120 * k + scoreH + lineRows * 40 * k + 40 * k + 104 * k + extraH + sensH);
        var rl = hasReview ? LayoutReview(review!, rw, maxH - 22 * k, k, showable, OpenRewind,
            review!.Plays is { HasData: true } ? () => OpenPlays(review.Plays!) : null) : null;
        if (rl != null) ph = Mathf.Min(maxH, Mathf.Max(ph, rl.Height + 22 * k));
        float left = size.X / 2 - (pw + gap + rw) / 2;
        var panel = new Rect2(left, size.Y / 2 - ph / 2, pw, ph);
        var rpanel = new Rect2(panel.End.X + gap, panel.Position.Y, rw, ph);
        float btnY = panel.End.Y - 90 * k;
        float sensTop = panel.Position.Y + 120 * k + scoreH + lineRows * 40 * k + 18 * k;
        if (canRewind)
        {
            // PLAY AGAIN · REWIND · MENU (the mode's own buttons keep the row above)
            float bx0 = panel.GetCenter().X - 401 * k;
            OverlayButton("PLAY AGAIN  (R)", VButton.Look.Primary, new Rect2(bx0, btnY, 280 * k, 58 * k), k, s.Restart);
            OverlayButton("REWIND", VButton.Look.Secondary, new Rect2(bx0 + 296 * k, btnY, 210 * k, 58 * k), k, () => OpenRewind(null));
            OverlayButton("MENU  (ESC)", VButton.Look.Secondary, new Rect2(bx0 + 522 * k, btnY, 280 * k, 58 * k), k, Main.I.ShowMenu);
            if (Main.I.Dev && !devRewindShown && CmdLine.After("--rewind-at") != null)
            {
                devRewindShown = true; // dev screenshots: --rewind-at S [--rewind-code CODE]
                Callable.From(() => OpenRewind(CmdLine.After("--rewind-code"))).CallDeferred();
            }
        }
        else
        {
            OverlayButton("PLAY AGAIN  (R)", VButton.Look.Primary, new Rect2(panel.GetCenter().X - 330 * k, btnY, 320 * k, 58 * k), k, s.Restart);
            OverlayButton("MENU  (ESC)", VButton.Look.Secondary, new Rect2(panel.GetCenter().X + 10 * k, btnY, 320 * k, 58 * k), k, Main.I.ShowMenu);
        }
        if (extra.Count > 0)
        {
            float eg = 20 * k;
            float ew = Mathf.Min(320 * k, (pw - 88 * k - (extra.Count - 1) * eg) / extra.Count);
            float ex = panel.GetCenter().X - (extra.Count * ew + (extra.Count - 1) * eg) / 2;
            var extraBtns = new List<(VButton B, string Up)>();
            foreach (var (label, act) in extra)
            {
                string up = label.ToUpperInvariant();
                VButton? eb = null;
                eb = OverlayButton(up, VButton.Look.Secondary, new Rect2(ex, btnY - 70 * k, ew, 52 * k), k, () =>
                {
                    try { act(); } catch (Exception ex2) { GD.PushWarning($"Result button failed: {ex2.Message}"); }
                    // only the last choice is ticked ("Use …" then "Keep my sens" undoes the first)
                    foreach (var (b, u) in extraBtns) { b.Label = b == eb ? "✓  " + u : u; b.QueueRedraw(); }
                }, 19);
                extraBtns.Add((eb, up));
                ex += ew + eg;
            }
        }
        if (showSens)
            OverlayButton("SENS LAB  ›", VButton.Look.Ghost,
                new Rect2(panel.End.X - 44 * k - 150 * k, sensTop - 2 * k, 150 * k, 28 * k), k, Main.I.ShowSensLab, 12);
        if (rl != null)
            foreach (var (r, label, sub, act, look) in rl.Buttons)
            {
                var b = OverlayButton(label, look, new Rect2(rpanel.Position + r.Position, r.Size), k, act, look == VButton.Look.Ghost ? 14 : 15);
                b.Sub = sub;
                b.SubPx = 11.5f;
                if (look == VButton.Look.Ghost) { b.TitleFont = UiTheme.HudWide; b.Align = HorizontalAlignment.Left; }
            }

        overlay!.OnDraw = d =>
        {
            float t = RealNow - overlayAt;
            Dimmer(d, size, k, 0.86f, RealNow);
            var pr = panel;
            float x0 = pr.Position.X + 44 * k, x1 = pr.End.X - 44 * k, y = pr.Position.Y;
            float p0 = pr.Position.X, p1 = pr.End.X, py1 = pr.End.Y;
            float cut = 26 * k, pulse = Gfx.Wave(RealNow, 0.5f);

            // marching dash runs sliding along hairlines (same language the pause plate uses)
            void RunX(float ex, float ey, float len, Color c, float h, float speed)
            {
                float db = 62 * k, span = len + db;
                float xx = ((RealNow * speed * k) % span) - db;
                for (int i = 0; i < 9; i++)
                {
                    float fall = 1f - MathF.Abs(i - 4f) / 5f;
                    d.DrawRect(new Rect2(ex + xx + (i - 4) * 9 * k, ey, 3 * k, h), new Color(c, c.A * fall * 0.85f));
                }
            }

            // ---- plate: halo glow, per-corner cuts, inner hairline, breathing outer brackets ----
            for (int i = 0; i < 2; i++)
            {
                float e = (7 + i * 10 + pulse * 3) * k;
                Gfx.PlateEx(d, new Rect2(p0 - e, pr.Position.Y - e, pr.Size.X + e * 2, pr.Size.Y + e * 2), cut + e,
                    true, false, false, true, new Color(0, 0, 0, 0), new Color(UiTheme.Accent, 0.05f / (i + 1)), 1f);
            }
            Gfx.PlateEx(d, pr, cut, true, false, false, true,
                new Color(0.05f, 0.09f, 0.13f, 0.97f), new Color(UiTheme.Text, 0.14f + 0.06f * pulse), 1.5f);
            Gfx.PlateEx(d, new Rect2(p0 + 4 * k, pr.Position.Y + 4 * k, pr.Size.X - 8 * k, pr.Size.Y - 8 * k), cut - 4 * k,
                true, false, false, true, new Color(0, 0, 0, 0), new Color(UiTheme.Accent, 0.11f), 1f);
            float dp = (3f + 1.6f * pulse) * k;
            Gfx.Diamond(d, new Vector2(p0, pr.Position.Y + cut), dp, dp, UiTheme.Accent);
            Gfx.Diamond(d, new Vector2(p1 - cut, py1), dp, dp, UiTheme.Teal);
            float bb = 9 * k + pulse * 2.5f * k;
            Gfx.Brackets(d, new Rect2(p0 - bb, pr.Position.Y - bb, pr.Size.X + bb * 2, pr.Size.Y + bb * 2),
                (15 + 3 * pulse) * k, new Color(UiTheme.Text, 0.28f + 0.14f * pulse), 2 * k);
            // accent run along the top edge with a fading tail; teal echo at the bottom right
            float abw = (110 + 60 * pulse) * k;
            d.DrawRect(new Rect2(p0 + cut, pr.Position.Y, abw, 3 * k), UiTheme.Accent);
            Span<Vector2> abq = stackalloc Vector2[4];
            Span<Color> abc = stackalloc Color[4];
            abq[0] = new(p0 + cut + abw, pr.Position.Y);
            abq[1] = abq[0] + new Vector2(80 * k, 0);
            abq[2] = abq[1] + new Vector2(0, 3 * k);
            abq[3] = abq[0] + new Vector2(0, 3 * k);
            abc[0] = new Color(UiTheme.Accent, 0.4f); abc[3] = abc[0];
            abc[1] = new Color(0, 0, 0, 0); abc[2] = abc[1];
            d.GradPoly(abq, abc);
            d.DrawRect(new Rect2(p1 - cut - 80 * k, py1 - 3 * k, 80 * k, 3 * k), new Color(UiTheme.Teal, 0.7f));
            // slow scan shimmer drifting inside the plate
            float scanY = pr.Position.Y + ((RealNow * 36 * k) % pr.Size.Y);
            float band = MathF.Min(34 * k, scanY - pr.Position.Y - 2 * k);
            if (band > 0)
            {
                Span<Vector2> sq = stackalloc Vector2[4];
                Span<Color> sc = stackalloc Color[4];
                sq[0] = new(p0 + 3 * k, scanY - band);
                sq[1] = new(p1 - 3 * k, scanY - band);
                sq[2] = sq[1] + new Vector2(0, band);
                sq[3] = sq[0] + new Vector2(0, band);
                sc[0] = new Color(0, 0, 0, 0); sc[1] = sc[0];
                sc[2] = new Color(UiTheme.Accent, 0.025f); sc[3] = sc[2];
                d.GradPoly(sq, sc);
            }
            d.DrawRect(new Rect2(p0 + 3 * k, scanY, pr.Size.X - 6 * k, 1.5f), new Color(UiTheme.Accent, 0.08f));

            // ---- header: kicker, big mode name sliding in on an accent rail, tier read-out on the right ----
            int hs = UiTheme.Fs(13, k), ks = UiTheme.Fs(12, k), ts = UiTheme.Fs(46, k);
            float entry = Gfx.EaseOutExpo(Mathf.Clamp(t / 0.5f, 0, 1));
            float move = (1f - entry) * 22 * k;
            float kkw = Gfx.TextW(UiTheme.HudWide, "// RESULTS", ks);
            Gfx.Text(d, UiTheme.HudWide, "// RESULTS", x0, y + 38 * k, ks, new Color(UiTheme.Teal, 0.75f + 0.25f * pulse));
            Gfx.Diamond(d, new Vector2(x0 + kkw + 10 * k, y + 38 * k - ks * 0.36f), 2.4f * k, 2.4f * k, new Color(UiTheme.Teal, 0.7f));
            Gfx.Text(d, UiTheme.Display, modeName, x0 + move, y + 100 * k, ts, new Color(UiTheme.Text, 0.35f + 0.65f * entry));
            float uW = 90f * k * entry;
            if (uW > 1)
            {
                d.DrawRect(new Rect2(x0, y + 128 * k, uW, 2 * k), UiTheme.Accent);
                Span<Vector2> uq = stackalloc Vector2[4];
                Span<Color> uc = stackalloc Color[4];
                uq[0] = new(x0 + uW, y + 128 * k);
                uq[1] = uq[0] + new Vector2(46 * k, 0);
                uq[2] = uq[1] + new Vector2(0, 2 * k);
                uq[3] = uq[0] + new Vector2(0, 2 * k);
                uc[0] = new Color(UiTheme.Accent, 0.45f * entry); uc[3] = uc[0];
                uc[1] = new Color(0, 0, 0, 0); uc[2] = uc[1];
                d.GradPoly(uq, uc);
            }
            // tier chip (right): read-out over an emblem, with a hairline rail tying it to the frame
            if (showScore)
            {
            float tw = Gfx.TextW(UiTheme.HudWide, tl, hs);
            var tc2 = UiTheme.TierColor(tierId);
            float ta = entry;
            Gfx.Text(d, UiTheme.HudWide, "PLAYED AT", x1 - tw, y + 50 * k, UiTheme.Fs(11, k), new Color(UiTheme.Faint, ta));
            Gfx.TextR(d, UiTheme.HudWide, tl, x1, y + 72 * k, hs, new Color(tc2, ta));
            RankEmblem.Draw(d, new Vector2(x1 - tw - 22 * k, y + 66 * k), 12 * k, tierId, ta);
            }
            if (subUp != null) Gfx.TextR(d, UiTheme.HudWide, subUp, x1, y + 96 * k, UiTheme.Fs(11, k), new Color(UiTheme.Dim, entry));
            // header divider: accent fading right + marching dashes
            float dy = y + 120 * k;
            Span<Vector2> dvq = stackalloc Vector2[4];
            Span<Color> dvc = stackalloc Color[4];
            dvq[0] = new(p0 + 4 * k, dy); dvq[1] = new(p1 - 4 * k, dy);
            dvq[2] = dvq[1] + new Vector2(0, 1.4f); dvq[3] = dvq[0] + new Vector2(0, 1.4f);
            dvc[0] = new Color(UiTheme.Accent, 0.35f); dvc[3] = dvc[0];
            dvc[1] = new Color(UiTheme.Text, 0.09f); dvc[2] = dvc[1];
            d.GradPoly(dvq, dvc);
            Gfx.Diamond(d, new Vector2(x0 - 10 * k, dy), 3.5f * k, 3.5f * k, UiTheme.Accent);
            RunX(x0 + 4 * k, dy - 2 * k, p1 - x0 - 16 * k, new Color(UiTheme.Text, 0.26f), 4 * k, 48);

            // score block (left)
            float sy = y + 120 * k;
            if (showScore)
            {
            int ls = UiTheme.Fs(13, k), ss = UiTheme.Fs(140, k), ps = UiTheme.Fs(15, k);
            float ex = pr.Position.X + pr.Size.X * 0.73f;
            float hcy = sy + 40 * k - ls * 0.36f;
            Gfx.Diamond(d, new Vector2(x0 + 3 * k, hcy), (2.8f + 1.4f * pulse) * k, (2.8f + 1.4f * pulse) * k, UiTheme.Accent);
            Gfx.Text(d, UiTheme.HudWide, "SCORE", x0 + 14 * k, sy + 40 * k, ls, UiTheme.Dim);
            float hw = Gfx.TextW(UiTheme.HudWide, "SCORE", ls);
            float rail = MathF.Max(0f, ex - 44 * k - (x0 + 14 * k + hw + 10 * k));
            d.DrawRect(new Rect2(x0 + 14 * k + hw + 10 * k, hcy - 0.5f, rail, 1), new Color(UiTheme.Text, 0.10f));
            float countT = Mathf.Clamp(t / 0.6f, 0, 1);
            string shown = countT >= 1 ? scoreText : Loc.Score((int)(score * Gfx.EaseOut(countT)));
            Gfx.Text(d, UiTheme.Display, shown, x0 - 4 * k, sy + 170 * k, ss, new Color(UiTheme.Text, entry));
            if (pb || first)
            {
                var cr = new Rect2(x0, sy + 202 * k, Gfx.TextW(UiTheme.HudWide, chip, ps) + 22 * k, 30 * k);
                float glow = pb ? 0.5f + 0.5f * Mathf.Sin(t * 5f) : 0f;
                if (pb) Gfx.PlateEx(d, cr.Grow(5 * k), 12 * k, true, false, false, true,
                    new Color(0, 0, 0, 0), new Color(UiTheme.Good, 0.10f + 0.08f * glow), 1f);
                Gfx.PlateEx(d, cr, 8 * k, true, false, false, true,
                    new Color(UiTheme.Good, 0.16f + 0.1f * glow), UiTheme.Good, 1f);
                Gfx.Text(d, UiTheme.HudWide, chip, cr.Position.X + 11 * k, Gfx.Mid(cr.GetCenter().Y, ps), ps, UiTheme.Good);
                if (pb) Gfx.Text(d, UiTheme.Body, prevText, cr.End.X + 14 * k, Gfx.Mid(cr.GetCenter().Y, ps), ps, UiTheme.Dim);
            }
            else Gfx.Text(d, UiTheme.Body, prevText,
                x0, Gfx.Mid(sy + 217 * k, ps + 2), ps + 2, UiTheme.Dim);

            // rank emblem block (right): pulse halo + pop-in emblem
            float ey = sy + 90 * k, er = 54 * k;
            float pop = Gfx.EaseOutBack(Mathf.Clamp((t - 0.15f) / 0.45f, 0, 1));
            var tc = UiTheme.TierColor(badge);
            if (pop > 0.01f)
            {
                float ring = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
                d.DrawCircle(new Vector2(ex, ey), er * 1.35f, new Color(tc, 0.06f + 0.04f * ring));
                d.DrawArc(new Vector2(ex, ey), er * 1.35f, 0, Mathf.Tau, 64, new Color(tc, 0.35f), Mathf.Max(1, 2 * k), true);
                // four signal ticks orbiting the halo at 45° steps, fading with the pulse
                for (int i = 0; i < 4; i++)
                {
                    float ang = Mathf.Tau * i / 4f + Mathf.Pi / 4f;
                    var dir = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
                    var tcol = new Color(UiTheme.Text, 0.14f + 0.12f * ring);
                    d.DrawLine(new Vector2(ex, ey) + dir * (er * 1.35f + 8 * k),
                        new Vector2(ex, ey) + dir * (er * 1.35f + (16 + 4 * pulse) * k), tcol, 1.5f);
                }
                d.DrawSetTransform(new Vector2(ex, ey), 0, new Vector2(pop, pop));
                RankEmblem.Draw(d, Vector2.Zero, er, badge);
                d.DrawSetTransformMatrix(Transform2D.Identity);
            }
            float ty = ey + er * 1.35f;
            Gfx.TextC(d, UiTheme.HudWide, "PERFORMED AT", ex, ty + 22 * k, UiTheme.Fs(12, k), UiTheme.Dim);
            int bs = UiTheme.Fs(36, k);
            Gfx.TextC(d, UiTheme.Display, badgeName, ex, ty + 58 * k, bs, tc);
            Gfx.TextC(d, UiTheme.Body, badgeRanks, ex, ty + 82 * k, UiTheme.Fs(15, k), UiTheme.Dim);
            }

            // result lines: divider + staggered rows sliding in from the left
            float ly = sy + scoreH;
            float ldy = ly - 10 * k;
            Span<Vector2> lq = stackalloc Vector2[4];
            Span<Color> lc = stackalloc Color[4];
            lq[0] = new(x0, ldy); lq[1] = new(x1, ldy);
            lq[2] = lq[1] + new Vector2(0, 1.4f); lq[3] = lq[0] + new Vector2(0, 1.4f);
            lc[0] = new Color(UiTheme.Accent, 0.40f + 0.2f * pulse); lc[3] = lc[0];
            lc[1] = new Color(0.0f, 0.0f, 0.0f, 0f); lc[2] = lc[1];
            d.GradPoly(lq, lc);
            RunX(x0 + 6 * k, ldy - 2 * k, x1 - x0 - 12 * k, new Color(UiTheme.Text, 0.24f), 4 * k, 42);
            int rs = UiTheme.Fs(18, k);
            float colW = twoCol ? (x1 - x0 - 40 * k) / 2 : x1 - x0;
            if (twoCol)
            {
                float vx = x0 + colW + 20 * k;
                d.DrawRect(new Rect2(vx, ly + 2 * k, 1, lineRows * 40 * k - 4 * k), new Color(UiTheme.Text, 0.09f));
                Gfx.Diamond(d, new Vector2(vx, ly + 2 * k), 2.6f * k, 2.6f * k, new Color(UiTheme.Teal, 0.8f));
                Gfx.Diamond(d, new Vector2(vx, ly + lineRows * 40 * k - 2 * k), 2.6f * k, 2.6f * k, new Color(UiTheme.Teal, 0.8f));
            }
            for (int i = 0; i < resultLines.Count; i++)
            {
                int ci = twoCol ? i / lineRows : 0, ri = twoCol ? i % lineRows : i;
                float lx0 = x0 + ci * (colW + 40 * k), lyy = ly + ri * 40 * k + 20 * k;
                float ra = Gfx.EaseOutExpo(Mathf.Clamp((t - 0.25f - i * 0.045f) / 0.35f, 0, 1));
                float lx = lx0 + (1f - ra) * 12 * k;
                if (ri % 2 == 0) d.DrawRect(new Rect2(lx0, lyy - 20 * k, colW, 40 * k), new Color(1, 1, 1, 0.025f * ra));
                var (label, value) = resultLines[i];
                value = Loc.Numeric(value);
                float vw = Mathf.Min(Gfx.TextW(UiTheme.Body, value, rs), colW * 0.6f);
                Gfx.TextFit(d, UiTheme.Body, label, lx + 12 * k, Gfx.Mid(lyy, rs), rs, new Color(UiTheme.Dim, ra), colW - vw - 36 * k);
                Gfx.TextFit(d, UiTheme.Body, value, lx + colW - 12 * k - colW * 0.6f, Gfx.Mid(lyy, rs), rs, new Color(UiTheme.Text, ra), colW * 0.6f, HorizontalAlignment.Right);
            }

            if (showSens && sens is { } sf)
            {
                float st = sensTop;
                float sdy = st - 10 * k;
                Span<Vector2> sdq = stackalloc Vector2[4];
                Span<Color> sdc = stackalloc Color[4];
                sdq[0] = new(x0, sdy); sdq[1] = new(x1, sdy);
                sdq[2] = sdq[1] + new Vector2(0, 1.4f); sdq[3] = sdq[0] + new Vector2(0, 1.4f);
                sdc[0] = new Color(UiTheme.Teal, 0.35f); sdc[3] = sdc[0];
                sdc[1] = new Color(UiTheme.Text, 0.09f); sdc[2] = sdc[1];
                d.GradPoly(sdq, sdc);
                Gfx.Diamond(d, new Vector2(x0 - 10 * k, sdy), 3 * k, 3 * k, UiTheme.Teal);
                int cap = UiTheme.Fs(12, k), body = UiTheme.Fs(14, k);
                Gfx.Diamond(d, new Vector2(x0 + 3 * k, st + 8 * k), (2.6f + 1.2f * pulse) * k, (2.6f + 1.2f * pulse) * k, UiTheme.Accent);
                Gfx.Text(d, UiTheme.HudWide, "SENSITIVITY LAB", x0 + 14 * k, Gfx.Mid(st + 8 * k, cap), cap, UiTheme.Dim);
                float cy = st + 32 * k;
                var vc = SensLabText.VerdictColor(sf.Verdict);
                float cw = CoachDraw.Chip(d, SensLabText.Verdict(sf), x0, cy, k, vc, filled: true);
                Gfx.TextFit(d, UiTheme.Display, SensLabText.VerdictSentence(sf), x0 + cw + 10 * k,
                    Gfx.Mid(cy, UiTheme.Fs(17, k)), UiTheme.Fs(17, k), UiTheme.Text, x1 - x0 - cw - 10 * k);
                string sm = SensLabText.Metrics(sf) + " · " + SensLabText.ConfidenceCaption(sf.Confidence);
                Gfx.TextFit(d, UiTheme.Body, sm, x0, Gfx.Mid(st + 56 * k, body), body, UiTheme.Dim, x1 - x0 - 160 * k);
                CoachDraw.Para(d, UiTheme.Body, SensLabText.Advice(sf), x0, st + 75 * k, x1 - x0, UiTheme.Fs(13.5f, k), UiTheme.Faint, 2);
            }

            if (rl != null)
            {
                var rp = rpanel;
                float rp0 = rp.Position.X, rp1x = rp.End.X, rpy1 = rp.End.Y, rcut = 22 * k;
                Gfx.PlateEx(d, rp, rcut, true, false, false, true,
                    new Color(0.05f, 0.09f, 0.13f, 0.97f), new Color(UiTheme.Text, 0.14f + 0.06f * pulse), 1.5f);
                Gfx.PlateEx(d, new Rect2(rp0 + 3 * k, rp.Position.Y + 3 * k, rp.Size.X - 6 * k, rp.Size.Y - 6 * k), rcut - 3 * k,
                    true, false, false, true, new Color(0, 0, 0, 0), new Color(UiTheme.Teal, 0.10f), 1f);
                float rbw = (60 + 34 * pulse) * k;
                d.DrawRect(new Rect2(rp0 + rcut, rp.Position.Y, rbw, 3 * k), UiTheme.Accent);
                Span<Vector2> tq2 = stackalloc Vector2[4];
                Span<Color> tc2b = stackalloc Color[4];
                tq2[0] = new(rp0 + rcut + rbw, rp.Position.Y);
                tq2[1] = tq2[0] + new Vector2(56 * k, 0);
                tq2[2] = tq2[1] + new Vector2(0, 3 * k);
                tq2[3] = tq2[0] + new Vector2(0, 3 * k);
                tc2b[0] = new Color(UiTheme.Accent, 0.4f); tc2b[3] = tc2b[0];
                tc2b[1] = new Color(0, 0, 0, 0); tc2b[2] = tc2b[1];
                d.GradPoly(tq2, tc2b);
                d.DrawRect(new Rect2(rp1x - rcut - 56 * k, rpy1 - 3 * k, 56 * k, 3 * k), new Color(UiTheme.Teal, 0.7f));
                Gfx.Diamond(d, new Vector2(rp0, rp.Position.Y + rcut), (2.6f + 1.2f * pulse) * k, (2.6f + 1.2f * pulse) * k, UiTheme.Accent);
                Gfx.Diamond(d, new Vector2(rp1x - rcut, rpy1), (2.6f + 1.2f * pulse) * k, (2.6f + 1.2f * pulse) * k, UiTheme.Teal);
                foreach (var op in rl.Ops) op(d, rp.Position);
            }
        };
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

    /// <summary>Pre-measured review panel: draw ops (relative to the panel origin) and its buttons.</summary>
    sealed class ReviewLayout
    {
        public float Height;
        public readonly List<Action<CanvasItem, Vector2>> Ops = new();
        public readonly List<(Rect2 R, string Label, string? Sub, Action Act, VButton.Look Look)> Buttons = new();
    }

    /// <summary>Lays the review out, dropping detail (cues, issues, strengths, summary lines) until it fits <paramref name="maxH"/>.</summary>
    static ReviewLayout LayoutReview(RunReview rv, float w, float maxH, float k, Func<string, bool>? showable = null, Action<string>? showMe = null,
        Action? playByPlay = null)
    {
        (int Issues, int Cues, int Strengths, int SumLines)[] variants = { (3, 2, 3, 6), (3, 1, 2, 5), (2, 2, 2, 5), (2, 1, 1, 4), (1, 1, 1, 3), (1, 0, 0, 3) };
        ReviewLayout? l = null;
        foreach (var v in variants)
        {
            l = TryLayoutReview(rv, w, k, v.Issues, v.Cues, v.Strengths, v.SumLines, showable, showMe, playByPlay);
            if (l.Height <= maxH) break;
        }
        return l!;
    }

    static ReviewLayout TryLayoutReview(RunReview rv, float w, float k, int maxIssues, int maxCues, int maxStrengths, int sumLines,
        Func<string, bool>? showable = null, Action<string>? showMe = null, Action? playByPlay = null)
    {
        var L = new ReviewLayout();
        var body = UiTheme.Body;
        float pad = 36 * k, x = pad, tw = w - pad * 2;
        int hs = UiTheme.Fs(13, k), ss = UiTheme.Fs(17, k), cs = UiTheme.Fs(12, k), ts = UiTheme.Fs(23, k), es = UiTheme.Fs(14.5f, k);

        // header tag
        string caption = rv.Issues.Count > 0 ? "WHAT TO FIX NEXT RUN" : "NO CLEAR PROBLEMS THIS RUN";
        L.Ops.Add((d, o) =>
        {
            var tag = new Rect2(o.X + x, o.Y + 34 * k, Gfx.TextW(UiTheme.HudWide, "COACH REVIEW", hs) + 18 * k, 22 * k);
            d.DrawRect(tag, UiTheme.Accent);
            Gfx.Text(d, UiTheme.HudWide, "COACH REVIEW", tag.Position.X + 9 * k, Gfx.Mid(tag.GetCenter().Y, hs), hs, UiTheme.Text);
            Gfx.TextR(d, UiTheme.HudWide, caption, o.X + w - pad, Gfx.Mid(tag.GetCenter().Y, cs), cs, rv.Issues.Count > 0 ? UiTheme.Faint : UiTheme.Good);
        });
        float y = 78 * k;

        if (rv.Summary.Length > 0)
        {
            float sy = y;
            float h = CoachDraw.TextH(body, rv.Summary, tw, ss, sumLines);
            L.Ops.Add((d, o) => CoachDraw.Para(d, body, rv.Summary, o.X + x, o.Y + sy, tw, ss, UiTheme.Text, sumLines));
            y += h + 22 * k;
        }

        void Section(string title)
        {
            float sy = y;
            L.Ops.Add((d, o) =>
            {
                float cy = o.Y + sy + 8 * k;
                d.DrawRect(new Rect2(o.X + x, cy - 3 * k, 6 * k, 6 * k), UiTheme.Accent);
                float tw0 = Gfx.TextW(UiTheme.HudWide, title, cs);
                Gfx.Text(d, UiTheme.HudWide, title, o.X + x + 14 * k, Gfx.Mid(cy, cs), cs, UiTheme.Dim);
                d.DrawRect(new Rect2(o.X + x + 28 * k + tw0, cy, tw - 28 * k - tw0, 1), new Color(UiTheme.Text, 0.1f));
            });
            y += 28 * k;
        }

        var issues = rv.Issues.Take(maxIssues).ToList();
        if (issues.Count > 0)
        {
            Section(issues.Count == 1 ? "TOP ISSUE" : "TOP ISSUES");
            foreach (var iss in issues)
            {
                float iy = y;
                var sc = CoachSkills.SeverityColor(iss.Severity);
                string sev = CoachSkills.SeverityName(iss.Severity);
                string? drill = (iss.Drills ?? Array.Empty<string>()).FirstOrDefault(CoachData.HasDrill);
                float bw = drill != null ? 160 * k : 0;
                if (drill != null)
                {
                    var focusCue = CoachData.FocusFrom(iss);
                    L.Buttons.Add((new Rect2(x + tw - bw, iy, bw, 42 * k), "PRACTISE", CoachData.DrillName(drill), () => CoachData.StartDrill(drill, focusCue), VButton.Look.Secondary));
                }
                // "Show me": this run has moments for the issue → a rewind focused on them
                float smw = 0;
                if (showMe != null && showable != null && showable(iss.Id))
                {
                    smw = Gfx.TextW(UiTheme.HudWide, "SHOW ME ›", UiTheme.Fs(14, k)) + 44 * k;
                    string id = iss.Id;
                    L.Buttons.Add((new Rect2(x + tw - bw - (bw > 0 ? 10 * k : 0) - smw, iy + 5 * k, smw, 32 * k), "SHOW ME ›", null, () => showMe(id), VButton.Look.Ghost));
                    smw += 10 * k;
                }
                L.Ops.Add((d, o) =>
                {
                    float cy = o.Y + iy + 21 * k;
                    float cw = CoachDraw.Chip(d, sev, o.X + x, cy, k, sc, filled: iss.Severity == Severity.Major);
                    float avail = tw - cw - 10 * k - (bw > 0 ? bw + 12 * k : 0) - smw;
                    int fs = ts;
                    while (fs > UiTheme.Fs(16, k) && Gfx.TextW(UiTheme.Display, iss.Title, fs) > avail) fs--;
                    Gfx.TextFit(d, UiTheme.Display, iss.Title, o.X + x + cw + 10 * k, Gfx.Mid(cy, fs), fs, UiTheme.Text, avail);
                });
                y += 50 * k;
                if (iss.Evidence.Length > 0)
                {
                    float ey = y;
                    float h = CoachDraw.TextH(body, iss.Evidence, tw, es, 2);
                    L.Ops.Add((d, o) => CoachDraw.Para(d, body, iss.Evidence, o.X + x, o.Y + ey, tw, es, UiTheme.Dim, 2));
                    y += h + 6 * k;
                }
                foreach (var cue in (iss.Fixes ?? Array.Empty<string>()).Take(maxCues))
                {
                    float qy = y;
                    float h = CoachDraw.TextH(body, cue, tw - 20 * k, es, 2);
                    L.Ops.Add((d, o) =>
                    {
                        Gfx.Diamond(d, new Vector2(o.X + x + 5 * k, o.Y + qy + es * 0.62f), 3.5f * k, 3.5f * k, sc);
                        CoachDraw.Para(d, body, cue, o.X + x + 20 * k, o.Y + qy, tw - 20 * k, es, UiTheme.Text, 2);
                    });
                    y += h + 4 * k;
                }
                y += 16 * k;
            }
        }

        var good = rv.Strengths.Take(maxStrengths).ToList();
        if (good.Count > 0)
        {
            Section("WHAT WENT WELL");
            foreach (var g in good)
            {
                float gy = y;
                float h = CoachDraw.TextH(body, g, tw - 20 * k, es, 2);
                L.Ops.Add((d, o) =>
                {
                    Span<Vector2> pts = stackalloc Vector2[3];
                    float bx = o.X + x, by = o.Y + gy + es * 0.62f;
                    pts[0] = new(bx, by); pts[1] = new(bx + 4 * k, by + 4 * k); pts[2] = new(bx + 11 * k, by - 5 * k);
                    d.Polyline(pts, UiTheme.Good, Mathf.Max(1.5f, 2 * k), true);
                    CoachDraw.Para(d, body, g, o.X + x + 20 * k, o.Y + gy, tw - 20 * k, es, UiTheme.Text, 2);
                });
                y += h + 6 * k;
            }
        }

        y += 10 * k;
        if (rv.Plays is { HasData: true } plays && playByPlay != null)
        {
            string label = (Loc.Spanish ? "JUGADA A JUGADA" : "PLAY BY PLAY") + $"  ({plays.Plays.Count})  ›";
            L.Buttons.Add((new Rect2(x - 2 * k, y, 360 * k, 32 * k), label, null, playByPlay, VButton.Look.Ghost));
            y += 32 * k + 9 * k;
        }
        L.Buttons.Add((new Rect2(x - 2 * k, y, 300 * k, 32 * k), "FULL COACH PROFILE  ›", null, () => Main.I.ShowProfile(), VButton.Look.Ghost));
        y += 32 * k + 26 * k;
        L.Height = y;
        return L;
    }
}
