using System.Globalization;
using Godot;
using ValTrainer.Core;
using ValTrainer.Game;
using ValTrainer.Maps;
using ValTrainer.Modes;
using ValTrainer.World;

namespace ValTrainer.UI;

// Widgets shared by the main menu, the Agents screen and the map picker (moved out of MenuScreen.cs in 1.18 when the
// menu was redesigned).

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
        if (hover != target) { hover = Mathf.MoveToward(hover, target, dt * 8f); QueueRedraw(); }
        if (reveal < 1f || hover > 0.001f || (IsNew && Tag == null && t > 0.2f)) QueueRedraw();
    }

    public override void _Draw()
    {
        float k = K;
        DrawSetTransform(new Vector2(0, (1f - reveal) * 18 * k), 0f, Vector2.One);
        var r = new Rect2(Vector2.Zero, Size);
        bool down = GetDrawMode() is DrawMode.Pressed or DrawMode.HoverPressed;
        var cc = Tint ?? CategoryColor(info.Category);
        Mn.Card(this, r, k, hover);
        if (down) DrawRect(r, new Color(1, 1, 1, 0.03f));
        // category rule along the top edge, brighter under the pointer
        DrawRect(new Rect2(0, 0, r.Size.X, Mathf.Max(1.5f, 2 * k)), new Color(cc, 0.22f + 0.5f * hover));

        float pad = 18 * k, cy = TwoLines ? 26 * k : 21 * k, right = r.Size.X - 16 * k;
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
        if (community != null && tag != null && Gfx.TextW(UiTheme.Light, name, ns) > right - pad - tagW - comW - 14 * k) { tag = null; tagW = 0; }
        float nameMax = Mathf.Max(20 * k, right - pad - (tag != null ? tagW + 8 * k : 0) - (community != null ? comW + 6 * k : 0));
        float nw = Mathf.Min(Gfx.TextW(UiTheme.Light, name, ns), nameMax);
        Gfx.TextFit(this, UiTheme.Light, name, pad, Gfx.Mid(cy, ns), ns, UiTheme.Text, nameMax);
        if (tag != null)
        {
            var tc = Tag != null ? cc : UiTheme.Accent;
            var tr = new Rect2(pad + nw + 8 * k, cy - 9 * k, tagW, 18 * k);
            DrawRect(tr, new Color(tc, 0.10f + 0.05f * hover));
            DrawRect(tr, new Color(tc, 0.40f + 0.3f * hover), false, 1);
            Gfx.Text(this, UiTheme.HudWide, tag, tr.Position.X + 6 * k, Gfx.Mid(cy, ts), ts, tc);
        }
        if (community != null)
        {
            var cr = new Rect2(pad + nw + 8 * k + (tag != null ? tagW + 6 * k : 0), cy - 9 * k, comW, 18 * k);
            DrawRect(cr, new Color(UiTheme.Good, 0.09f));
            DrawRect(cr, new Color(UiTheme.Good, 0.40f), false, 1);
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
                float numW = Gfx.TextW(UiTheme.Hud, pbText, ps);
                float labW = Gfx.TextW(UiTheme.HudWide, "PB", pl);
                float chipW = numW + labW + 26 * k, chipH = 24 * k;
                var chip = new Rect2(r.Size.X - 16 * k - chipW, r.Size.Y - 14 * k - chipH, chipW, chipH);
                DrawRect(chip, new Color(1, 1, 1, 0.03f + 0.02f * hover));
                DrawRect(chip, new Color(1, 1, 1, 0.10f + 0.12f * hover), false, 1);
                Gfx.TextR(this, UiTheme.Hud, pbText, chip.End.X - 10 * k, Gfx.Mid(chip.GetCenter().Y, ps), ps,
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
                x -= Gfx.TextR(this, UiTheme.Hud, pbText, x, Gfx.Mid(lineY, vs), vs, UiTheme.Good) + 6 * k;
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
        !HasArt(key) ? "TRACED" : FullArt(key) ? "REAL ART" : "PARTIAL ART";

    /// <summary>Tag colour: teal for complete art, amber for a partial extraction, grey for a traced fallback.</summary>
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
        string rest = Loc.Spanish
            ? $" · {total - art} TRAZADOS · ALEATORIO ELIGE UNO NUEVO EN CADA PARTIDA"
            : $" · {total - art} TRACED · RANDOM PICKS A NEW ONE EVERY RUN";
        float restW = Gfx.TextW(UiTheme.HudWide, rest, ns);
        float right = panel.End.X - pad;
        Gfx.TextR(this, UiTheme.HudWide, rest, right, Gfx.Mid(cy, ns), ns, UiTheme.Faint);
        Gfx.TextR(this, UiTheme.HudWide, Loc.Spanish ? $"{art} ARTE REAL" : $"{art} REAL ART", right - restW,
            Gfx.Mid(cy, ns), ns, UiTheme.Teal);
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
            Stage.Ready => $"CA-VAL-AIM {v} IS READY",
            Stage.Applying => $"INSTALLING CA-VAL-AIM {v}…",
            _ => $"CA-VAL-AIM {v} IS AVAILABLE",
        };
        string sub = Kind switch
        {
            Stage.Downloading => $"Downloading update {Updater.Progress * 100:0}%",
            Stage.Ready => "restart to update · you have " + AppInfo.Version + (Updater.NeedsElevation ? " · Windows will ask for permission" : ""),
            Stage.Applying => "CA-VAL-AIM restarts by itself",
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

    void Act() => DoAct();

    /// <summary>What the update control does when pressed (shared with the minimal menu's <see cref="MUpdateLink"/>).</summary>
    public static void DoAct()
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
        var (label, tip, disabled, primary) = Compute();
        var look = primary ? Look.Primary : Look.Secondary;
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

    /// <summary>The update control's label, tooltip, disabled state and whether it is the primary action right now.</summary>
    public static (string Label, string Tip, bool Disabled, bool Primary) Compute()
    {
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
            tip = "CA-VAL-AIM is installing the update and will restart";
        }
        else if (Updater.Status == Updater.State.Ready)
        {
            label = "RESTART TO UPDATE";
            look = Look.Primary;
            tip = Updater.NeedsElevation
                ? "Installs the update and restarts CA-VAL-AIM. Windows will ask for permission"
                : "Installs the update and restarts CA-VAL-AIM (settings and stats are kept)";
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
            tip = "Asking GitHub for the newest CA-VAL-AIM release…";
        }
        else if (UpdateCheck.NewerAvailable)
        {
            bool canDownload = Updater.Status is not (Updater.State.Off or Updater.State.Failed)
                && UpdateCheck.LatestAssets is { } assets
                && (Updater.Kind == InstallKind.Portable ? assets.PortableUrl != null : assets.SetupUrl != null);
            label = canDownload ? Loc.T("UPDATE TO") + " " + UpdateCheck.Latest : Loc.T("OPEN DOWNLOAD");
            look = Look.Secondary;
            tip = canDownload
                ? $"Downloads CA-VAL-AIM {UpdateCheck.Latest} and verifies it against SHA256SUMS.txt, then offers to restart"
                : "Opens the release page so you can download the installer";
        }
        else if (UpdateCheck.Status == UpdateCheck.State.UpToDate)
        {
            label = "UP TO DATE";
            tip = "You have the newest CA-VAL-AIM" + (UpdateCheck.Message != null ? " · " + UpdateCheck.Message : "");
        }
        else
        {
            tip = UpdateCheck.CanCheck
                ? "Ask GitHub now whether a newer CA-VAL-AIM release is out"
                : UpdateCheck.Message ?? "Updates aren't available in this build";
            if (!UpdateCheck.CanCheck) disabled = true;
            if (UpdateCheck.Status == UpdateCheck.State.Failed && UpdateCheck.Message != null)
                tip = "Last check failed: " + UpdateCheck.Message;
        }
        return (label, tip, disabled, look == Look.Primary);
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
