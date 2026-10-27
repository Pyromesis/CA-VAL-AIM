using Godot;
using ValTrainer.Core;

namespace ValTrainer.UI;

/// <summary>
/// Button drawn in code with the NV-OPS language: sharp plates with 45° corner cuts. Looks: Primary (red gradient
/// plate + travelling shimmer + hover glow), Secondary (dark angular plate, hover sweep), Select (cut-corner chip with
/// an animated under-bar), Ghost (text + underline sweep) and Arrow (square &lt; / &gt; stepper).
/// </summary>
public partial class VButton : BaseButton
{
    public enum Look { Primary, Secondary, Select, Ghost, Arrow }

    public Look Kind = Look.Secondary;
    public string Label = "";
    public string? Sub;
    public float K = 1f;
    public float FontPx = 20f, SubPx = 13f;
    /// <summary>&gt;= -1 draws a rank emblem on the left.</summary>
    public int Emblem = -99;
    public bool Selected;
    /// <summary>Arrow look: -1 = &lt;, +1 = &gt;.</summary>
    public int Dir = 1;
    public HorizontalAlignment Align = HorizontalAlignment.Center;
    public Font? TitleFont;
    public bool Silent;
    /// <summary>Gentle breathing glow (the one primary call to action on a screen).</summary>
    public bool Pulse;

    float hover, flash, pulseT, t, selAnim = 1f;

    public VButton()
    {
        FocusMode = FocusModeEnum.None;
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }

    public override void _Ready()
    {
        MouseEntered += () => { if (!Disabled && !Silent) UiTheme.HoverSound(); };
        Pressed += () => { if (!Silent) UiTheme.ClickSound(); flash = 1f; QueueRedraw(); };
    }

    /// <summary>Activates the button as if clicked (used by the in-game overlay, which handles clicks itself).</summary>
    public void Click() => EmitSignal(BaseButton.SignalName.Pressed);

    public void SetSelected(bool on)
    {
        if (Selected == on) return;
        Selected = on;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        t += dt;
        pulseT += dt;
        float target = IsHovered() && !Disabled ? 1f : 0f;
        bool down = GetDrawMode() is DrawMode.Pressed or DrawMode.HoverPressed;
        hover = Mathf.MoveToward(hover, target, dt * 8f);
        selAnim = Mathf.MoveToward(selAnim, Selected ? 1f : 0f, dt * 4.5f);
        // shimmer needs continuous frames on primaries; Pulse breathes; a click flashes
        bool anim = flash > 0.001f || Pulse
            || (Kind == Look.Primary && (hover > 0.002f || target > 0.002f))
            || (Kind == Look.Select && (selAnim > 0.001f && selAnim < 0.999f));
        if (anim)
        {
            flash = Mathf.Max(0, flash - dt * 5f);
            QueueRedraw();
        }
        // pointer-sized lift: only the big action buttons, so tabs and rail rows stay calm
        // (the minimalist language keeps type still; hover is colour and a hairline, never a scale)
        if (!Scale.IsEqualApprox(Vector2.One)) { PivotOffset = Size / 2; Scale = Vector2.One; QueueRedraw(); }
    }

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        float a = Disabled ? 0.38f : 1f;
        bool down = GetDrawMode() is DrawMode.Pressed or DrawMode.HoverPressed;
        float h = hover, k = K;
        Color textCol = UiTheme.Text;

        switch (Kind)
        {
            case Look.Primary:
            {
                // one accent fill per screen: flat, no gradient or glow
                var fill = UiTheme.Accent.Lerp(UiTheme.AccentHover, h * 0.6f).Darkened(down ? 0.12f : 0f);
                DrawRect(r, new Color(fill, a));
                if (flash > 0) DrawRect(r, new Color(1, 1, 1, flash * 0.18f * a));
                textCol = Colors.White;
                break;
            }
            case Look.Secondary:
            {
                // hairline rectangle over the barest wash: hover brightens both, never moves the type
                DrawRect(r, new Color(1, 1, 1, (0.025f + 0.035f * h) * a));
                var line = Mn.A(UiTheme.Text, 0.14f + 0.30f * h);
                float bw = Mathf.Max(1, k);
                DrawRect(new Rect2(0, 0, r.Size.X, bw), new Color(line, line.A * a));
                DrawRect(new Rect2(0, r.Size.Y - bw, r.Size.X, bw), new Color(line, line.A * a));
                DrawRect(new Rect2(0, 0, bw, r.Size.Y), new Color(line, line.A * a));
                DrawRect(new Rect2(r.Size.X - bw, 0, bw, r.Size.Y), new Color(line, line.A * a));
                if (flash > 0) DrawRect(r, new Color(1, 1, 1, flash * 0.14f * a));
                textCol = UiTheme.TextSoft.Lerp(UiTheme.Text, h);
                break;
            }
            case Look.Select:
            {
                DrawRect(r, new Color(1, 1, 1, (0.02f + 0.035f * h) * a));
                float line = Mathf.Max(Selected ? 1f : 0f, h);
                if (line > 0.01f)
                    DrawRect(new Rect2(0, r.Size.Y - Mathf.Max(1.5f, 2 * k), r.Size.X * line, Mathf.Max(1.5f, 2 * k)),
                        new Color(Selected ? UiTheme.Accent : UiTheme.Text, (Selected ? 0.95f : 0.55f * h) * a));
                textCol = Selected ? UiTheme.Text : UiTheme.TextSoft.Lerp(UiTheme.Text, h);
                break;
            }
            case Look.Ghost:
                if (h > 0.01f) DrawRect(r, new Color(1, 1, 1, 0.035f * h * a));
                DrawRect(new Rect2(8 * k, r.Size.Y - 1.4f * k, Mathf.Max(0, (r.Size.X - 16 * k) * Gfx.EaseOutExpo(h)), 1.4f * k),
                    new Color(UiTheme.Accent, h * a));
                textCol = UiTheme.Dim.Lerp(UiTheme.Text, h);
                break;
            case Look.Arrow:
            {
                DrawRect(r, new Color(1, 1, 1, (0.02f + 0.05f * h) * a));
                var line = Mn.A(UiTheme.Text, 0.16f + 0.34f * h);
                float bw = Mathf.Max(1, k);
                DrawRect(new Rect2(0, 0, r.Size.X, bw), new Color(line, line.A * a));
                DrawRect(new Rect2(0, r.Size.Y - bw, r.Size.X, bw), new Color(line, line.A * a));
                Gfx.Chevron(this, r.GetCenter(), r.Size.Y * 0.30f, Dir, UiTheme.TextSoft.Lerp(UiTheme.Text, h), Mathf.Max(1.4f, r.Size.Y * 0.065f));
                return;
            }
        }

        textCol = new Color(textCol, textCol.A * a);
        var font = TitleFont ?? (Kind == Look.Primary ? UiTheme.HudWide : UiTheme.Light);
        int fs = UiTheme.Fs(FontPx, k), ss = UiTheme.Fs(SubPx, k);
        float x = 0, w = r.Size.X;
        float pad = 16 * k;
        if (Emblem >= -1)
        {
            float er = Mathf.Min(r.Size.Y * 0.28f, 19 * k);
            RankEmblem.Draw(this, new Vector2(pad + er, r.Size.Y / 2), er, Emblem, a);
            x = pad + er * 2 + 10 * k;
            w = r.Size.X - x - pad;
        }
        else if (Align != HorizontalAlignment.Center) { x = pad; w = r.Size.X - pad * 2; }

        if (Sub == null)
        {
            Gfx.TextFit(this, font, Label, x, Gfx.Mid(r.Size.Y / 2, fs), fs, textCol, w, Emblem >= -1 ? HorizontalAlignment.Left : Align);
        }
        else
        {
            var align = Emblem >= -1 ? HorizontalAlignment.Left : Align;
            float total = fs * 0.72f + 7 * k + ss * 0.72f, top = (r.Size.Y - total) / 2;
            Gfx.TextFit(this, font, Label, x, top + fs * 0.72f, fs, textCol, w, align);
            var subCol = Kind == Look.Select && Selected ? new Color(UiTheme.TextSoft, 0.85f * a) : new Color(UiTheme.Dim, a);
            Gfx.TextFit(this, UiTheme.Body, Sub, x, top + total, ss, subCol, w, align);
        }
    }
}

/// <summary>Angular translucent panel with an optional uppercase title bar. Children are laid out inside the margins.</summary>
public partial class VPanel : MarginContainer
{
    public string Title = "";
    public string? Caption;
    public float K = 1f;
    public float Pad = 22f;
    public Color Fill = new(0.07f, 0.11f, 0.15f, 0.84f);

    public float TitleH => Title.Length > 0 ? 54 * K : 0;

    public override void _Ready()
    {
        int p = (int)(Pad * K);
        AddThemeConstantOverride("margin_left", p);
        AddThemeConstantOverride("margin_right", p);
        AddThemeConstantOverride("margin_top", (int)(Title.Length > 0 ? TitleH + 8 * K : p));
        AddThemeConstantOverride("margin_bottom", p);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        float k = K;
        var r = new Rect2(Vector2.Zero, Size);
        // flat card: the barest light wash between two hairlines, no shadow or corner cuts
        DrawRect(r, new Color(1, 1, 1, 0.018f));
        DrawRect(new Rect2(0, 0, r.Size.X, 1), UiTheme.Line);
        DrawRect(new Rect2(0, r.Size.Y - 1, r.Size.X, 1), UiTheme.Line);
        if (Title.Length == 0) return;
        float cy = 27 * k;
        int fs = UiTheme.Fs(11, k);
        float tx = Pad * k;
        DrawRect(new Rect2(tx, cy - 6 * k, Mathf.Max(1.5f, 2 * k), 12 * k), UiTheme.Accent);
        Gfx.Text(this, UiTheme.HudWide, Title, tx + 10 * k, Gfx.Mid(cy, fs), fs, UiTheme.TextSoft);
        float tw = Gfx.TextW(UiTheme.HudWide, Loc.T(Title), fs);
        DrawRect(new Rect2(tx + 10 * k + tw + 12 * k, cy - 0.5f, 8 * k, 1), new Color(1, 1, 1, 0.16f));
        if (Caption != null)
        {
            int cs = UiTheme.Fs(10.5f, k);
            Gfx.TextR(this, UiTheme.HudWide, Caption, r.Size.X - Pad * k, Gfx.Mid(cy, cs), cs, UiTheme.Faint);
        }
        DrawRect(new Rect2(Pad * k, TitleH - 1, Mathf.Max(0, r.Size.X - Pad * k * 2), 1), UiTheme.Line);
    }
}

/// <summary>Small tracked-out caption with a red tick ("DIFFICULTY TIER") and an optional dim note on the right.</summary>
public partial class SectionLabel : Control
{
    public string Text = "";
    public string? Note;
    public float K = 1f;

    public SectionLabel() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Ready() => CustomMinimumSize = new Vector2(CustomMinimumSize.X, Mathf.Max(CustomMinimumSize.Y, 30 * K));

    public override void _Draw()
    {
        float k = K, cy = Size.Y / 2;
        int fs = UiTheme.Fs(11, k);
        DrawRect(new Rect2(0, cy - 6 * k, Mathf.Max(1.5f, 2 * k), 12 * k), UiTheme.Accent);
        float w = Gfx.TextW(UiTheme.HudWide, Text, fs);
        Gfx.Text(this, UiTheme.HudWide, Text, 10 * k, Gfx.Mid(cy, fs), fs, UiTheme.TextSoft);
        float lineX = 10 * k + w + 14 * k;
        if (Note != null)
        {
            int ns = UiTheme.Fs(10.5f, k);
            float nw = Gfx.TextR(this, UiTheme.HudWide, Note, Size.X, Gfx.Mid(cy, ns), ns, UiTheme.Faint);
            if (Size.X - nw - 14 * k > lineX)
                Gfx.HGradient(this, new Rect2(lineX, cy, Size.X - nw - 14 * k - lineX, 1), new Color(1, 1, 1, 0.10f), new Color(1, 1, 1, 0.015f));
        }
        else if (Size.X > lineX)
            Gfx.HGradient(this, new Rect2(lineX, cy, Size.X - lineX, 1), new Color(1, 1, 1, 0.10f), new Color(1, 1, 1, 0.015f));
    }
}

/// <summary>Draws one rank emblem centred in its rect.</summary>
public partial class EmblemView : Control
{
    public int Tier = -1;
    public float Fill = 0.45f;
    public EmblemView() => MouseFilter = MouseFilterEnum.Ignore;
    public override void _Draw() => RankEmblem.Draw(this, Size / 2, Mathf.Min(Size.X, Size.Y) * Fill, Tier);
}

/// <summary>Screen backdrop: the same flat canvas as the menu (the old animated navy field is gone), so every
/// screen shares one visual world. Kept as its own type because ScreenBase adds it by name.</summary>
public partial class Backdrop : MinimalBackdrop
{
}

/// <summary>Mini line chart of recent scores.</summary>
public static class Sparkline
{
    public static void Draw(CanvasItem ci, Rect2 r, IReadOnlyList<float> v, float k, Color line)
    {
        if (v.Count == 0 || r.Size.X < 10) return;
        float min = float.MaxValue, max = float.MinValue;
        int best = 0;
        for (int i = 0; i < v.Count; i++)
        {
            if (v[i] < min) min = v[i];
            if (v[i] > max) { max = v[i]; best = i; }
        }
        float span = Mathf.Max(1e-3f, max - min);
        // baseline + mid guide
        ci.DrawRect(new Rect2(r.Position.X, r.End.Y, r.Size.X, 1), new Color(UiTheme.Text, 0.08f));
        Vector2 P(int i) => new(v.Count == 1 ? r.GetCenter().X : r.Position.X + i * r.Size.X / (v.Count - 1),
                                 max == min ? r.GetCenter().Y : r.End.Y - (v[i] - min) / span * r.Size.Y);
        if (v.Count >= 2)
        {
            Span<Vector2> pts = stackalloc Vector2[v.Count];
            Span<Vector2> area = stackalloc Vector2[v.Count + 2];
            for (int i = 0; i < v.Count; i++) { pts[i] = P(i); area[i] = pts[i]; }
            area[v.Count] = new Vector2(r.End.X, r.End.Y);
            area[v.Count + 1] = new Vector2(r.Position.X, r.End.Y);
            Span<Color> cols = stackalloc Color[v.Count + 2];
            for (int i = 0; i < v.Count; i++) cols[i] = new Color(line, 0.22f);
            cols[v.Count] = new Color(line, 0f); cols[v.Count + 1] = new Color(line, 0f);
            try { ci.GradPoly(area, cols); } catch { /* degenerate area: skip fill */ }
            ci.Polyline(pts, line, Mathf.Max(1.5f, 2f * k), true);
        }
        Gfx.Diamond(ci, P(best), 5 * k, 5 * k, UiTheme.Good); // best: a diamond, latest: a dot (shape as well as colour)
        ci.DrawCircle(P(v.Count - 1), 3.5f * k, UiTheme.Text);
    }
}

/// <summary>
/// Base for full-screen menus: rebuilds its node tree whenever the window size changes (so every pixel size
/// is computed from the 1080p design scale and text stays crisp), adds the backdrop, theme and Esc handling.
/// </summary>
public abstract partial class ScreenBase : Control
{
    protected float K = 1f;
    Vector2 builtFor;
    int locRev = Loc.Revision;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Rebuild();
        // enter animation: the screen fades in while a thin accent sweep crosses the top edge
        Modulate = new Color(1, 1, 1, 0);
        var fade = CreateTween();
        fade.TweenProperty(this, "modulate:a", 1f, 0.32f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        // Minimal screens (the menu, the loading screen) add their own staggered entrance; the rest just fade in.
    }

    public override void _Process(double delta)
    {
        if (GetViewportRect().Size != builtFor || locRev != Loc.Revision)
        {
            locRev = Loc.Revision;
            Rebuild();
        }
        Tick((float)delta);
    }

    protected virtual void Tick(float dt) { }

    /// <summary>The shared animated shader backdrop behind the screen (the minimalist main menu draws its own).</summary>
    protected virtual bool AnimatedBackdrop => true;

    protected void Rebuild()
    {
        foreach (var c in GetChildren())
        {
            RemoveChild(c);
            c.QueueFree();
        }
        builtFor = GetViewportRect().Size;
        K = UiTheme.Fit(builtFor);
        Theme = UiTheme.MakeTheme(K);
        if (AnimatedBackdrop) AddChild(new Backdrop());
        Build();
        Loc.Apply(this);
    }

    protected abstract void Build();

    /// <summary>Esc behaviour; default goes back to the menu.</summary>
    protected virtual void Back() => Main.I.ShowMenu();

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            Back();
        }
    }

    // ---- small builders ----

    protected VButton Btn(string label, VButton.Look look, Action onClick, float w = 0, float h = 52, float fontPx = 20)
    {
        var b = new VButton { Label = label, Kind = look, K = K, FontPx = fontPx, CustomMinimumSize = new Vector2(w * K, h * K) };
        b.Pressed += onClick;
        return b;
    }

    protected Label Lbl(string text, Font f, float px, Color c, HorizontalAlignment align = HorizontalAlignment.Left) =>
        new() { Text = Loc.T(text), LabelSettings = UiTheme.Label(f, px, K, c), HorizontalAlignment = align, MouseFilter = MouseFilterEnum.Ignore };

    protected static MarginContainer Margins(Control child, float l, float t, float r, float b)
    {
        var m = new MarginContainer();
        m.AddThemeConstantOverride("margin_left", (int)l);
        m.AddThemeConstantOverride("margin_top", (int)t);
        m.AddThemeConstantOverride("margin_right", (int)r);
        m.AddThemeConstantOverride("margin_bottom", (int)b);
        m.AddChild(child);
        return m;
    }

    protected static VBoxContainer VBox(float sep) { var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", (int)sep); return v; }
    protected static HBoxContainer HBox(float sep) { var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", (int)sep); return h; }

    protected static Control Spacer(float w, float h) => new() { CustomMinimumSize = new Vector2(w, h), MouseFilter = MouseFilterEnum.Ignore };

    protected static Control Expander() => new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };

    /// <summary>Screen title block: accent tick, big light heading, dim caption. Used by every non-menu screen.</summary>
    protected Control Heading(string title, string sub)
    {
        var v = VBox(5 * K);
        var kicker = new DrawBox
        {
            CustomMinimumSize = new Vector2(0, 3 * K),
            OnDraw = d =>
            {
                float k = K;
                float solid = MathF.Min(34 * k, d.Size.X);
                d.DrawRect(new Rect2(0, 0, solid, MathF.Max(1.5f, 2 * k)), UiTheme.Accent);
                if (d.Size.X > solid)
                    Gfx.HGradient(d, new Rect2(solid, 0, d.Size.X - solid, MathF.Max(1.5f, 2 * k)),
                        new Color(1, 1, 1, 0.10f), new Color(1, 1, 1, 0f));
            },
        };
        v.AddChild(kicker);
        v.AddChild(Lbl(title, UiTheme.Light, 46, UiTheme.Text));
        var subLine = Lbl(sub, UiTheme.HudWide, 11.5f, UiTheme.Faint);
        subLine.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        subLine.ClipText = true;
        v.AddChild(subLine);
        return v;
    }
}

/// <summary>A Control whose drawing is supplied by a delegate (one-off decorations).</summary>
public partial class DrawBox : Control
{
    public Action<DrawBox>? OnDraw;
    public DrawBox() => MouseFilter = MouseFilterEnum.Ignore;
    public override void _Draw() => OnDraw?.Invoke(this);
    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }
}

/// <summary>Read-only "label ........ value" row (values are pulled through delegates so <see cref="Control.QueueRedraw"/> refreshes them).</summary>
public partial class InfoRow : Control
{
    public string Label = "";
    public Func<string> Value = () => "";
    public Func<Color>? ValueColor;
    public Func<Color?>? Swatch;
    public Func<string?>? Tag;
    public float K = 1f;
    public float H = 38f;
    public bool Stripe;

    public InfoRow() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, H * K);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    public override void _Draw()
    {
        float k = K, cy = Size.Y / 2, pad = 10 * k;
        if (Stripe) DrawRect(new Rect2(0, 0, Size.X, Size.Y), new Color(1, 1, 1, 0.018f));
        int ls = UiTheme.Fs(14, k), vs = UiTheme.Fs(15, k);
        float lw = Gfx.TextW(UiTheme.Body, Label, ls);
        Gfx.Text(this, UiTheme.Body, Label, pad, Gfx.Mid(cy, ls), ls, UiTheme.Dim);
        float right = Size.X - pad;
        string v = Value();
        float maxW = Mathf.Max(20, right - (pad + lw + 16 * k));
        float vw = Mathf.Min(Gfx.TextW(UiTheme.BodySemi, v, vs), maxW);
        Gfx.TextFit(this, UiTheme.BodySemi, v, right - maxW, Gfx.Mid(cy, vs), vs, ValueColor?.Invoke() ?? UiTheme.Text, maxW, HorizontalAlignment.Right);
        float x = right - vw - 10 * k;
        if (Swatch?.Invoke() is { } sw)
        {
            float s = 13 * k;
            Gfx.Round(this, new Rect2(x - s, cy - s / 2, s, s), 4 * k, sw, new Color(1, 1, 1, 0.25f), 1);
            x -= s + 10 * k;
        }
        if (Tag?.Invoke() is { } tag)
        {
            int ts = UiTheme.Fs(10.5f, k);
            float tw = Gfx.TextW(UiTheme.HudWide, tag, ts) + 14 * k;
            var tr = new Rect2(x - tw, cy - 9 * k, tw, 18 * k);
            Gfx.Round(this, tr, 8 * k, new Color(UiTheme.Warn, 0.10f), new Color(UiTheme.Warn, 0.45f), 1);
            Gfx.Text(this, UiTheme.HudWide, tag, tr.Position.X + 7 * k, Gfx.Mid(cy, ts), ts, UiTheme.Warn);
        }
    }
}

/// <summary>One-shot light sweep along the top edge when a screen opens: a bright head with a fading trail, a flash
/// dot, and a soft full-width afterglow (the "new app" transition accent).</summary>
public partial class IntroSweep : Control
{
    float t;

    public IntroSweep()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        CustomMinimumSize = new Vector2(0, 3);
    }

    public override void _Process(double delta)
    {
        t += (float)delta / 0.62f;
        if (t >= 1f) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float w = GetViewportRect().Size.X;
        float e = Gfx.EaseOutQuart(t);
        float head = e * (w + 520);
        float x0 = Mathf.Max(0, head - 520);
        float x1 = Mathf.Min(w, head);
        if (x1 > x0)
        {
            // main beam with a trailing tail
            Gfx.HGradient(this, new Rect2(x0, 0, x1 - x0, 2), new Color(UiTheme.Accent, 0f), new Color(UiTheme.Accent, 0.95f * (1f - t * 0.35f)));
            Gfx.HGradient(this, new Rect2(x0, 2, x1 - x0, 1), new Color(UiTheme.Accent, 0f), new Color(UiTheme.Accent, 0.35f));
            Gfx.HGradient(this, new Rect2(x0, -1, x1 - x0, 1), new Color(UiTheme.Accent, 0f), new Color(UiTheme.Accent, 0.30f));
        }
        // two trailing sparks
        for (int i = 0; i < 2; i++)
        {
            float lx = Mathf.Max(0, head - 260 * (i + 1));
            if (lx < w) DrawCircle(new Vector2(lx, 1), 1.4f - i * 0.4f, new Color(UiTheme.Accent, 0.55f * (1f - i * 0.4f) * (1f - t * 0.5f)));
        }
        if (head < w) DrawRect(new Rect2(head - 1.4f, 0, 2.8f, 3), new Color(1, 1, 1, 0.85f * (1f - t * 0.3f)));
    }
}
