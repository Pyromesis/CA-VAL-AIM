using Godot;
using ValTrainer.Core;

namespace ValTrainer.UI;

// The minimalist design kit used by the main menu and the loading screen (1.18): a flat canvas, light display type,
// hairlines instead of panels, one accent (UiTheme.Accent) for the primary action and the current selection, and motion
// that eases in and out naturally — every element arrives with a short staggered rise, hover and focus follow the pointer
// with an exponential approach instead of a fixed-speed ramp.

/// <summary>Timing and easing shared by the minimalist widgets.</summary>
public static class Mn
{
    /// <summary>Length of an element's entrance (fade + rise).</summary>
    public const float RevealTime = 0.62f;
    /// <summary>Delay between two consecutive elements of a staggered entrance.</summary>
    public const float Stagger = 0.045f;

    /// <summary>Frame-rate independent exponential approach (a critically damped feel without overshoot).</summary>
    public static float Approach(float cur, float target, float dt, float speed = 14f)
    {
        float next = cur + (target - cur) * (1f - MathF.Exp(-speed * dt));
        return MathF.Abs(next - target) < 0.0015f ? target : next;
    }

    /// <summary>Entrance progress 0..1 for an element revealed <paramref name="delay"/> seconds after its screen opened.</summary>
    public static float Reveal(float t, float delay) => Gfx.EaseOutQuart((t - delay) / RevealTime);

    /// <summary>Fades a stock control in after <paramref name="delay"/> (for Labels and containers the kit doesn't draw).</summary>
    public static T FadeIn<T>(T c, float delay) where T : Control
    {
        c.Modulate = new Color(1, 1, 1, 0);
        c.Ready += () =>
        {
            var tw = c.CreateTween();
            if (delay > 0) tw.TweenInterval(delay);
            tw.TweenProperty(c, "modulate:a", 1f, RevealTime).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart);
        };
        return c;
    }

    public static Color A(Color c, float a) => new(c.R, c.G, c.B, c.A * a);
}

/// <summary>Flat menu canvas: the theme background with a barely-there light falloff from the top-left. Static (no
/// per-frame redraw), so the menu costs nothing while the player reads it.</summary>
public partial class MinimalBackdrop : Control
{
    public MinimalBackdrop()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }

    public override void _Draw()
    {
        var s = Size;
        DrawRect(new Rect2(Vector2.Zero, s), UiTheme.Bg);
        Gfx.VGradient(this, new Rect2(0, 0, s.X, s.Y * 0.5f), new Color(1, 1, 1, 0.018f), new Color(1, 1, 1, 0f));
        Gfx.VGradient(this, new Rect2(0, s.Y * 0.6f, s.X, s.Y * 0.4f), new Color(0, 0, 0, 0f), new Color(0, 0, 0, 0.22f));
    }
}

/// <summary>A line of text that rises into place (titles, captions). Drawn, so it never reflows while animating.</summary>
public partial class MText : Control
{
    public float K = 1f, Delay;
    public string Text = "";
    public Font? Font;
    public float Px = 16f;
    public Color Color = UiTheme.Text;
    public HorizontalAlignment Align = HorizontalAlignment.Left;
    float t;

    public MText() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Ready()
    {
        if (CustomMinimumSize.Y <= 0) CustomMinimumSize = new Vector2(CustomMinimumSize.X, MathF.Ceiling(Px * K * 1.35f));
    }

    public override void _Process(double delta)
    {
        if (t > Delay + Mn.RevealTime) return;
        t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float r = Mn.Reveal(t, Delay);
        if (r <= 0) return;
        int fs = UiTheme.Fs(Px, K);
        float rise = (1 - r) * 10 * K;
        Gfx.TextFit(this, Font ?? UiTheme.Body, Text, 0, Gfx.Mid(Size.Y / 2, fs) + rise, fs, Mn.A(Color, r), Size.X, Align);
    }
}

/// <summary>
/// Button base of the kit: eased hover / focus / press values, keyboard focus, sounds and the staggered entrance.
/// Subclasses only draw.
/// </summary>
public abstract partial class MButtonBase : BaseButton
{
    public float K = 1f, Delay;
    protected float T, Hover, Down;
    protected float R => Mn.Reveal(T, Delay);

    protected MButtonBase()
    {
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        Modulate = new Color(1, 1, 1, 0); // the entrance starts invisible: no full-opacity first frame
    }

    public override void _Ready()
    {
        MouseEntered += UiTheme.HoverSound;
        Pressed += UiTheme.ClickSound;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        T += dt;
        bool hot = !Disabled && (IsHovered() || HasFocus());
        bool down = GetDrawMode() is DrawMode.Pressed or DrawMode.HoverPressed;
        float h = Mn.Approach(Hover, hot ? 1 : 0, dt, 16f), d = Mn.Approach(Down, down ? 1 : 0, dt, 26f);
        bool entering = T < Delay + Mn.RevealTime + 0.05f;
        if (entering || Modulate.A != (Disabled ? 0.45f : 1f)) Modulate = new Color(1, 1, 1, R * (Disabled ? 0.45f : 1f));
        if (h != Hover || d != Down || entering || Animating)
        {
            Hover = h; Down = d;
            QueueRedraw();
        }
    }

    /// <summary>True while a subclass has its own animation running.</summary>
    protected virtual bool Animating => false;

    /// <summary>Applies the entrance (fade + 8 px rise) to everything drawn after it.</summary>
    protected bool BeginDraw()
    {
        float r = R;
        if (r <= 0) return false;
        DrawSetTransform(new Vector2(0, (1 - r) * 8 * K), 0, Vector2.One);
        return true;
    }
}

/// <summary>Text link (navigation, footer): dim text that brightens on hover while a hairline draws under it from the left.</summary>
public partial class MLink : MButtonBase
{
    public string Label = "";
    public Font? Font;
    public float Px = 13f;
    /// <summary>The current destination: full brightness and a steady underline.</summary>
    public bool Active;
    /// <summary>Optional arrow after the label that slides on hover.</summary>
    public bool Arrow;
    public Color? Tint;

    public override void _Ready()
    {
        base._Ready();
        var f = Font ?? UiTheme.HudWide;
        int fs = UiTheme.Fs(Px, K);
        float w = Gfx.TextW(f, Label, fs) + (Arrow ? fs * 1.4f : 0);
        if (CustomMinimumSize.X <= 0) CustomMinimumSize = new Vector2(MathF.Ceiling(w + 2), MathF.Max(CustomMinimumSize.Y, 30 * K));
    }

    public override void _Draw()
    {
        if (!BeginDraw()) return;
        var f = Font ?? UiTheme.HudWide;
        int fs = UiTheme.Fs(Px, K);
        float cy = Size.Y / 2;
        var baseCol = Tint ?? UiTheme.Dim;
        var col = Active ? UiTheme.Text : baseCol.Lerp(UiTheme.Text, Hover);
        float w = Gfx.TextW(f, Label, fs);
        Gfx.Text(this, f, Label, 0, Gfx.Mid(cy, fs), fs, col);
        if (Arrow) Gfx.Text(this, UiTheme.Body, "→", w + fs * 0.45f + 3 * K * Hover, Gfx.Mid(cy, fs) + fs * 0.05f, fs, col);
        float line = Active ? 1f : Hover;
        if (line > 0.01f)
            DrawRect(new Rect2(0, cy + fs * 0.85f, w * line, Mathf.Max(1, 1.2f * K)), Mn.A(Active ? UiTheme.Accent : UiTheme.Text, Active ? 0.9f : 0.55f * line));
    }
}

/// <summary>Rectangular call to action. Primary: the accent fill (one per screen); otherwise a hairline outline.</summary>
public partial class MAction : MButtonBase
{
    public string Label = "";
    public bool Primary;
    public float Px = 14f;

    public override void _Draw()
    {
        if (!BeginDraw()) return;
        var r = new Rect2(Vector2.Zero, Size);
        float k = K;
        int fs = UiTheme.Fs(Px, k);
        float cy = Size.Y / 2;
        Color text;
        if (Primary)
        {
            var fill = UiTheme.Accent.Lerp(UiTheme.AccentHover, Hover * 0.6f).Darkened(0.12f * Down);
            DrawRect(r, fill);
            text = Colors.White;
        }
        else
        {
            DrawRect(r, new Color(1, 1, 1, 0.025f + 0.035f * Hover));
            var line = Mn.A(UiTheme.Text, 0.14f + 0.30f * Hover);
            float bw = Mathf.Max(1, k);
            DrawRect(new Rect2(0, 0, Size.X, bw), line); DrawRect(new Rect2(0, Size.Y - bw, Size.X, bw), line);
            DrawRect(new Rect2(0, 0, bw, Size.Y), line); DrawRect(new Rect2(Size.X - bw, 0, bw, Size.Y), line);
            text = UiTheme.TextSoft.Lerp(UiTheme.Text, Hover);
        }
        float pad = 20 * k;
        Gfx.TextFit(this, UiTheme.HudWide, Label, pad, Gfx.Mid(cy, fs), fs, text, Size.X - pad * 2 - fs);
        Gfx.Text(this, UiTheme.Body, "→", Size.X - pad - fs * 0.9f + 4 * k * Hover, Gfx.Mid(cy, fs) + fs * 0.05f, fs, Mn.A(text, 0.75f + 0.25f * Hover));
        if (HasFocus() && !Primary) DrawRect(new Rect2(0, Size.Y - 2 * k, Size.X, 2 * k), Mn.A(UiTheme.Accent, 0.8f));
    }
}

/// <summary>
/// Segmented choice drawn as tracked caps with an accent underline that glides to the selection (tier, deathmatch
/// difficulty, lobby size, drill categories, quick settings). Mouse, wheel-free; keyboard: Left / Right when focused.
/// Each option may carry a tooltip.
/// </summary>
public partial class MSegment : Control
{
    public float K = 1f, Delay;
    public string[] Options = Array.Empty<string>();
    public string[]? Tips;
    public int Selected;
    public float Px = 12.5f;
    /// <summary>Gap between options in design units.</summary>
    public float Gap = 26f;
    public Action<int>? Changed;
    /// <summary>Small count drawn after an option (drill categories).</summary>
    public int[]? Counts;

    float t, barX, barW;
    bool barSet;
    int hot = -1;
    float[] hover = Array.Empty<float>();

    public MSegment()
    {
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }

    public override void _Ready()
    {
        hover = new float[Options.Length];
        CustomMinimumSize = new Vector2(MathF.Ceiling(TotalWidth()), MathF.Max(CustomMinimumSize.Y, 34 * K));
        MouseExited += () => { hot = -1; };
    }

    int Fs => UiTheme.Fs(Px, K);

    float OptionW(int i)
    {
        float w = Gfx.TextW(UiTheme.HudWide, Options[i], Fs);
        if (Counts != null && i < Counts.Length) w += Gfx.RawW(UiTheme.Hud, Counts[i].ToString(), UiTheme.Fs(Px * 0.8f, K)) + 5 * K;
        return w;
    }

    float TotalWidth()
    {
        float w = 0;
        for (int i = 0; i < Options.Length; i++) w += OptionW(i) + (i > 0 ? Gap * K : 0);
        return w;
    }

    float OptionX(int i)
    {
        float x = 0;
        for (int j = 0; j < i; j++) x += OptionW(j) + Gap * K;
        return x;
    }

    int Hit(Vector2 p)
    {
        for (int i = 0; i < Options.Length; i++)
        {
            float x = OptionX(i), half = Gap * K / 2;
            if (p.X >= x - half && p.X < x + OptionW(i) + half) return i;
        }
        return -1;
    }

    public override string _GetTooltip(Vector2 atPosition)
    {
        int i = Hit(atPosition);
        return Tips != null && i >= 0 && i < Tips.Length ? Loc.T(Tips[i]) : "";
    }

    public void Select(int i, bool notify = true)
    {
        i = Math.Clamp(i, 0, Options.Length - 1);
        if (i == Selected) return;
        Selected = i;
        UiTheme.ClickSound();
        QueueRedraw();
        if (notify) Changed?.Invoke(i);
    }

    public override void _GuiInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseMotion m:
            {
                int h = Hit(m.Position);
                if (h != hot && h >= 0) UiTheme.HoverSound();
                hot = h;
                TooltipText = Tips != null ? " " : ""; // _GetTooltip supplies the text
                break;
            }
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } b:
            {
                int h = Hit(b.Position);
                if (h >= 0) { Select(h); AcceptEvent(); }
                break;
            }
            case InputEventKey { Pressed: true } k when k.Keycode is Key.Left or Key.Right:
                Select(Selected + (k.Keycode == Key.Left ? -1 : 1));
                AcceptEvent();
                break;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        t += dt;
        bool redraw = t < Delay + Mn.RevealTime + 0.05f;
        if (hover.Length != Options.Length) hover = new float[Options.Length];
        for (int i = 0; i < hover.Length; i++)
        {
            float h = Mn.Approach(hover[i], i == hot ? 1 : 0, dt, 16f);
            if (h != hover[i]) { hover[i] = h; redraw = true; }
        }
        if (Options.Length > 0)
        {
            int s = Math.Clamp(Selected, 0, Options.Length - 1);
            float tx = OptionX(s), tw = OptionW(s);
            if (!barSet) { barX = tx; barW = tw; barSet = true; }
            float nx = Mn.Approach(barX, tx, dt, 13f), nw = Mn.Approach(barW, tw, dt, 13f);
            if (nx != barX || nw != barW) { barX = nx; barW = nw; redraw = true; }
        }
        if (redraw) QueueRedraw();
    }

    public override void _Draw()
    {
        float k = K;
        int fs = Fs, cs = UiTheme.Fs(Px * 0.8f, k);
        float cy = Size.Y / 2 - 2 * k;
        for (int i = 0; i < Options.Length; i++)
        {
            float r = Mn.Reveal(t, Delay + i * Mn.Stagger * 0.6f);
            if (r <= 0) continue;
            float x = OptionX(i), rise = (1 - r) * 8 * k;
            bool sel = i == Selected;
            var col = sel ? UiTheme.Text : UiTheme.Faint.Lerp(UiTheme.TextSoft, hover.Length > i ? hover[i] : 0);
            Gfx.Text(this, UiTheme.HudWide, Options[i], x, Gfx.Mid(cy, fs) + rise, fs, Mn.A(col, r));
            if (Counts != null && i < Counts.Length)
                DrawString(UiTheme.Hud, new Vector2(x + Gfx.TextW(UiTheme.HudWide, Options[i], fs) + 5 * k, Gfx.Mid(cy, fs) + rise),
                    Counts[i].ToString(), HorizontalAlignment.Left, -1, cs, Mn.A(UiTheme.Faint, 0.8f * r));
        }
        float ra = Mn.Reveal(t, Delay + 0.1f);
        if (Options.Length > 0 && ra > 0)
        {
            float y = cy + fs * 0.95f;
            DrawRect(new Rect2(barX, y, barW * ra, Mathf.Max(1.5f, 2 * k)), Mn.A(UiTheme.Accent, ra));
        }
        if (HasFocus()) DrawRect(new Rect2(0, Size.Y - 1, Size.X, 1), Mn.A(UiTheme.Text, 0.15f));
    }
}

/// <summary>One drill in the library: name, a quiet second line and the personal best on the right. Hover slides the
/// name a few pixels and draws an accent tick at the left edge; the row background only hints at a surface.</summary>
public partial class MDrillRow : MButtonBase
{
    public string Title = "", Meta = "", Value = "", ValueCaption = "";
    public bool IsNew;

    public override void _Draw()
    {
        if (!BeginDraw()) return;
        float k = K, h = Size.Y;
        if (Hover > 0.01f) DrawRect(new Rect2(0, 0, Size.X, h), new Color(1, 1, 1, 0.035f * Hover));
        DrawRect(new Rect2(0, h * (0.5f - 0.3f * Hover), Mathf.Max(1.5f, 2 * k), h * 0.6f * Hover), Mn.A(UiTheme.Accent, Hover));
        DrawRect(new Rect2(0, h - 1, Size.X, 1), UiTheme.Line);
        int ns = UiTheme.Fs(16.5f, k), ms = UiTheme.Fs(12, k), vs = UiTheme.Fs(15, k);
        float x = 14 * k + 6 * k * Hover;
        float valueW = string.IsNullOrEmpty(Value) ? 0
            : Mathf.Max(Gfx.RawW(UiTheme.Hud, Value, vs), Gfx.TextW(UiTheme.HudWide, ValueCaption, UiTheme.Fs(9.5f, k))) + 24 * k;
        float nameW = Gfx.TextW(UiTheme.Body, Title, ns);
        Gfx.TextFit(this, UiTheme.Body, Title, x, h * 0.44f, ns, UiTheme.TextSoft.Lerp(UiTheme.Text, Hover), Size.X - x - valueW - 8 * k);
        if (IsNew && x + nameW + 50 * k < Size.X - valueW)
            Gfx.Text(this, UiTheme.HudWide, "NEW", x + nameW + 10 * k, h * 0.44f - ns * 0.05f, UiTheme.Fs(9.5f, k), UiTheme.Accent);
        Gfx.TextFit(this, UiTheme.Body, Meta, x, h * 0.44f + ms * 1.45f, ms, UiTheme.Faint, Size.X - x - valueW - 8 * k);
        if (valueW > 0)
        {
            Gfx.TextR(this, UiTheme.Hud, Value, Size.X - 12 * k, h * 0.44f, vs, UiTheme.Text.Lerp(UiTheme.Text, Hover));
            Gfx.TextR(this, UiTheme.HudWide, ValueCaption, Size.X - 12 * k, h * 0.44f + ms * 1.45f, UiTheme.Fs(9.5f, k), UiTheme.Faint);
        }
    }
}

/// <summary>A hairline that draws itself across from the left during the entrance.</summary>
public partial class MRule : Control
{
    public float Delay;
    float t;

    public MRule()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(0, 1);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    public override void _Process(double delta)
    {
        if (t > Delay + 1.2f) return;
        t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float r = Gfx.EaseInOut(Mathf.Clamp((t - Delay) / 0.9f, 0, 1));
        DrawRect(new Rect2(0, 0, Size.X * r, 1), UiTheme.Line);
    }
}

/// <summary>
/// Screen-change crossfade (Main.SetScreen): a still of the previous screen laid over the new one, fading out. The new
/// screen is already built underneath, so there is never an empty or half-built frame between two screens, and a
/// first-frame hitch of the new screen (shader compiles, a drill's world) happens behind the still.
/// </summary>
public partial class ScreenFade : CanvasLayer
{
    readonly float duration;
    float t;
    TextureRect? rect;
    ImageTexture? tex;

    public ScreenFade(Image still, float duration = 0.34f)
    {
        Layer = 120;
        this.duration = duration;
        tex = ImageTexture.CreateFromImage(still);
        rect = new TextureRect
        {
            Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(rect);
    }

    public override void _Process(double delta)
    {
        // The first frame can be long (a world being built): measure from the second one so the fade is always seen.
        t += Mathf.Min((float)delta, 1f / 30f);
        float a = 1f - Gfx.EaseInOut(t / duration);
        if (rect != null) rect.Modulate = new Color(1, 1, 1, a);
        if (t >= duration) QueueFree();
    }

    public override void _ExitTree()
    {
        if (rect != null) rect.Texture = null;
        tex?.Dispose();
        tex = null;
    }

    /// <summary>The frame currently on screen, or null when there is none to show (headless, minimised, first screen).</summary>
    public static Image? Capture(Viewport vp)
    {
        try
        {
            if (DisplayServer.GetName() == "headless") return null;
            var img = vp.GetTexture()?.GetImage();
            return img == null || img.IsEmpty() || img.GetWidth() < 8 || img.GetHeight() < 8 ? null : img;
        }
        catch { return null; }
    }
}
