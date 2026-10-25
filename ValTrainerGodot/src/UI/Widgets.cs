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
        var want = Kind is Look.Primary or Look.Secondary && !Disabled
            ? Vector2.One * (1f + 0.018f * hover - (down ? 0.012f : 0f))
            : Vector2.One;
        if (Scale.DistanceTo(want) > 0.0005f)
        {
            PivotOffset = Size / 2;
            Scale = Scale.Lerp(want, Mathf.Min(1f, dt * 14f));
            QueueRedraw();
        }
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
                // hovering glow behind the plate
                if (h > 0.01f) Gfx.Glow(this, r.GetCenter(), Mathf.Max(r.Size.X, r.Size.Y) * (0.55f + 0.25f * h),
                    new Color(UiTheme.Accent, (0.06f + 0.10f * h) * a), 8);
                if (Pulse) Gfx.Glow(this, r.GetCenter(), (Mathf.Max(r.Size.X, r.Size.Y) * 0.62f) * (1f + 0.05f * Gfx.Wave(pulseT, 0.2f)),
                    new Color(UiTheme.Accent, (0.05f + 0.03f * Gfx.Wave(pulseT, 0.2f)) * a), 7);

                // gradient fill clipped to the cut-corner plate
                float cut = 9 * k;
                float x0 = 0, y0 = 0, x1 = r.Size.X, y1 = r.Size.Y;
                var fill = UiTheme.AccentDark.Lerp(UiTheme.Accent, 0.42f);
                if (down) fill = UiTheme.AccentDark.Lerp(Colors.Black, 0.25f);
                Span<Vector2> p = stackalloc Vector2[8];
                Span<Color> c = stackalloc Color[8];
                p[0] = new(x0, y0); c[0] = new(fill, a);
                p[1] = new(x1 - cut, y0); p[2] = new(x1, y0 + cut); p[3] = new(x1, y1); p[4] = new(x0 + cut, y1); p[5] = new(x0, y1 - cut);
                p[6] = p[0]; p[7] = p[1];
                c[1] = new(fill, a); c[2] = new(fill, a); c[3] = new(fill.Darkened(0.22f), a);
                c[4] = new(fill.Darkened(0.22f), a); c[5] = new(fill.Darkened(0.22f), a);
                c[6] = c[0]; c[7] = c[1];
                this.GradPoly(p, c);
                // inner top highlight
                DrawRect(new Rect2(6 * k, 1.2f * k, Mathf.Max(0, r.Size.X - 22 * k), 1),
                    new Color(1, 1, 1, (0.22f - 0.12f * (down ? 1f : 0f)) * a));
                // travelling shimmer band
                float band2 = 34 * k, span = r.Size.X + band2 * 2;
                float cx = (t * 0.32f % 1f) * span - band2;
                float sx0 = Mathf.Max(4 * k, cx - band2), sx1 = Mathf.Min(r.Size.X - 4 * k, cx + band2);
                if (sx1 > sx0 && !down)
                    Gfx.HGradient(this, new Rect2(sx0, cut * 0.7f, sx1 - sx0, r.Size.Y - cut * 1.4f),
                        new Color(1, 1, 1, 0f), new Color(1, 1, 1, (0.05f + 0.06f * h) * a));
                // border + click flash
                Span<Vector2> bo = stackalloc Vector2[7];
                bo[0] = new(x0, y0); bo[1] = new(x1 - cut, y0); bo[2] = new(x1, y0 + cut); bo[3] = new(x1, y1);
                bo[4] = new(x0 + cut, y1); bo[5] = new(x0, y1 - cut); bo[6] = bo[0];
                this.Polyline(bo, new Color(1, 1, 1, (0.07f + 0.16f * h) * a), 1f);
                if (flash > 0) DrawRect(new Rect2(Vector2.Zero, r.Size), new Color(1, 1, 1, flash * 0.18f * a));
                break;
            }
            case Look.Secondary:
            {
                float cut = 8 * k;
                Gfx.PlateEx(this, r, cut, true, false, false, true,
                    new Color(1, 1, 1, (0.042f + 0.045f * h) * a),
                    UiTheme.Fade(UiTheme.LineStrong.Lerp(UiTheme.Accent, h * 0.65f), a), 1);
                // hover sweep: a soft light band travels right as the pointer lands
                float sw = r.Size.X * 0.55f, sx = r.Size.X * h;
                if (h > 0.01f)
                    Gfx.PlateEx(this, new Rect2(Mathf.Max(0, sx - sw), 0, Mathf.Min(r.Size.X, sx) - Mathf.Max(0, sx - sw), r.Size.Y), 0,
                        false, false, false, false, new Color(1, 1, 1, 0.05f * h * a), default);
                // right edge accent chevron appears on hover
                if (h > 0.01f)
                    Gfx.Chevron(this, new Vector2(r.Size.X - 14 * k, r.Size.Y / 2), r.Size.Y * 0.3f, 1,
                        new Color(UiTheme.Accent, h * 0.9f * a), 2.2f * k);
                break;
            }
            case Look.Select:
            {
                if (Selected || selAnim > 0.001f)
                {
                    Gfx.PlateEx(this, r, 5 * k, true, false, false, true,
                        new Color(UiTheme.AccentSoft, UiTheme.AccentSoft.A * (0.55f + 0.45f * selAnim) * a),
                        new Color(UiTheme.Accent, 0.5f * a), 1);
                    // animated under-bar sweeping in from the left
                    float bw = r.Size.X * 0.86f * Gfx.EaseOutExpo(selAnim);
                    DrawRect(new Rect2(12 * k, r.Size.Y - 2.6f * k, Mathf.Max(0, bw), 2.6f * k), new Color(UiTheme.Accent, a));
                }
                if (!Selected)
                {
                    var fill = new Color(1, 1, 1, 0.03f + 0.05f * h);
                    Gfx.PlateEx(this, r, 5 * k, true, false, false, true, new Color(fill, fill.A * a),
                        UiTheme.Fade(UiTheme.Line.Lerp(UiTheme.LineStrong, h), a), 1);
                }
                break;
            }
            case Look.Ghost:
                if (h > 0.01f) Gfx.Round(this, r, 4 * k, new Color(1, 1, 1, 0.05f * h * a));
                // underline sweep
                DrawRect(new Rect2(8 * k, r.Size.Y - 2 * k, Mathf.Max(0, (r.Size.X - 16 * k) * Gfx.EaseOutExpo(h)), 1.6f * k),
                    new Color(UiTheme.Accent, h * a));
                textCol = UiTheme.Dim.Lerp(UiTheme.Text, h);
                break;
            case Look.Arrow:
            {
                Gfx.PlateEx(this, r, 6 * k, Dir < 0, false, Dir > 0, false,
                    new Color(1, 1, 1, (0.045f + 0.09f * h) * a),
                    UiTheme.Fade(UiTheme.LineStrong.Lerp(UiTheme.Accent, h * 0.8f), a), 1);
                Gfx.Chevron(this, r.GetCenter(), r.Size.Y * 0.34f, Dir, new Color(UiTheme.TextSoft, a), Mathf.Max(1.5f, r.Size.Y * 0.08f));
                return;
            }
        }

        textCol = new Color(textCol, textCol.A * a);
        var font = TitleFont ?? UiTheme.Display;
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
        // soft drop shadow
        for (int i = 3; i >= 1; i--)
            Gfx.PlateEx(this, new Rect2(r.Position - new Vector2(i * 2.4f * k, i * 2.4f * k) + new Vector2(0, i * k), r.Size + new Vector2(i * 4.8f * k, i * 4.8f * k)),
                18 * k, false, true, true, false, default, new Color(0, 0, 0, 0.05f * i), i * 1.4f);
        // angular body: top-right and bottom-left corners cut, glass fill, dark outer line
        Gfx.PlateEx(this, r, 18 * k, false, true, true, false, new Color(Fill, Fill.A), new Color(0, 0, 0, 0.5f), 1.4f);
        Gfx.PlateEx(this, r, 18 * k, false, true, true, false, default, new Color(1, 1, 1, 0.10f), 1);
        // hairline trimmed inside the top edge plus a corner tick (circuit accent)
        DrawRect(new Rect2(16 * k, 1, Mathf.Max(0, r.Size.X - 34 * k), 1), new Color(1, 1, 1, 0.07f));
        DrawRect(new Rect2(r.Position.X, r.Position.Y + 26 * k, 2 * k, 9 * k), new Color(UiTheme.Teal, 0.55f));
        DrawRect(new Rect2(r.End.X - 2 * k, r.End.Y - 35 * k, 2 * k, 9 * k), new Color(UiTheme.Teal, 0.35f));
        if (Title.Length == 0) return;
        float cy = 27 * k;
        int fs = UiTheme.Fs(18, k);
        float tx = Pad * k;
        Gfx.Diamond(this, new Vector2(tx + 3 * k, cy), 4.2f * k, 4.2f * k, UiTheme.Accent);
        Gfx.Text(this, UiTheme.DisplayWide, Title, tx + 14 * k, Gfx.Mid(cy, fs), fs, UiTheme.Text);
        float tw = Gfx.TextW(UiTheme.DisplayWide, Loc.T(Title), fs);
        DrawRect(new Rect2(tx + 14 * k + tw + 12 * k, cy - 0.5f, 8 * k, 1), new Color(1, 1, 1, 0.16f));
        if (Caption != null)
        {
            int cs = UiTheme.Fs(12.5f, k);
            Gfx.TextR(this, UiTheme.Body, Caption, r.Size.X - Pad * k, Gfx.Mid(cy, cs), cs, UiTheme.Faint);
        }
        DrawRect(new Rect2(Pad * k, TitleH - 1, Mathf.Max(0, r.Size.X - Pad * k * 2), 1), new Color(1, 1, 1, 0.06f));
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
        int fs = UiTheme.Fs(13, k);
        Gfx.Diamond(this, new Vector2(3.5f * k, cy), 5.5f * k, 5.5f * k, UiTheme.Accent);
        Gfx.Diamond(this, new Vector2(3.5f * k, cy), 8f * k, 8f * k, new Color(UiTheme.Accent, 0.22f));
        float w = Gfx.TextW(UiTheme.HudWide, Text, fs);
        Gfx.Text(this, UiTheme.HudWide, Text, 14 * k, Gfx.Mid(cy, fs), fs, UiTheme.Dim.Lerp(UiTheme.Text, 0.35f));
        float lineX = 14 * k + w + 14 * k;
        if (Note != null)
        {
            int ns = UiTheme.Fs(12, k);
            float nw = Gfx.TextR(this, UiTheme.Body, Note, Size.X, Gfx.Mid(cy, ns), ns, UiTheme.Faint);
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

/// <summary>
/// Animated app backdrop: a deep navy "energy field" — two drifting aurora ribbons, slow diagonal light beams, a
/// perspective grid floor scrolling toward the viewer, two parallax dust layers and film grain, all in one full-screen
/// canvas shader (one draw call; the glows drift with the pointer for depth).
/// </summary>
public partial class Backdrop : Control
{
    ShaderMaterial? mat;
    Vector2 mouse = new(0.5f, 0.5f);

    public Backdrop()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _Ready()
    {
        var shader = new Shader
        {
            Code = @"
shader_type canvas_item;
render_mode unshaded;

uniform vec2 mouse = vec2(0.5);
uniform vec4 accent : source_color = vec4(1.0, 0.275, 0.333, 1.0);
uniform vec4 accent2 : source_color = vec4(0.22, 0.77, 0.65, 1.0);

float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float vnoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + vec2(1.0, 0.0));
    float c = hash21(i + vec2(0.0, 1.0));
    float d = hash21(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++) {
        v += a * vnoise(p);
        p *= 2.07;
        a *= 0.5;
    }
    return v;
}

// narrow band around a position, 0..1
float band(float x, float c, float w) { return exp(-pow((x - c) * w, 2.0)); }

void fragment() {
    vec2 uv = UV;
    vec2 par = (mouse - 0.5);
    float t = TIME;

    // base: ink navy with a soft top-down lift and a whisper of hue on the left
    vec3 col = mix(vec3(0.037, 0.050, 0.070), vec3(0.010, 0.014, 0.021), smoothstep(0.0, 1.0, uv.y));
    col += vec3(0.012, 0.020, 0.016) * smoothstep(0.7, 0.0, uv.x) * (1.0 - uv.y);

    // ---- aurora ribbons ----
    vec2 a1 = (uv + par * 0.035) * vec2(1.7, 1.15) + vec2(t * 0.021, -t * 0.013);
    float n1 = fbm(a1 * 1.4);
    float rib1 = exp(-pow((uv.y - 0.40 - 0.13 * sin(uv.x * 2.6 + t * 0.42) - 0.10 * n1) * 4.6, 2.0));
    col += accent.rgb * rib1 * (0.085 + 0.030 * fbm(a1 * 3.0 + t * 0.05));
    vec2 a2 = (uv * 1.0 + par * 0.020) * vec2(2.5, 1.5) + vec2(-t * 0.016, t * 0.024) + 9.1;
    float n2 = fbm(a2);
    float rib2 = exp(-pow((uv.y - 0.68 - 0.16 * sin(uv.x * 1.9 - t * 0.33) - 0.12 * n2) * 5.2, 2.0));
    col += accent2.rgb * rib2 * 0.050;

    // ---- corner glows (breathing) ----
    vec2 d1 = (uv - vec2(0.88, 0.02)) * vec2(1.0, 1.5);
    col += accent.rgb * exp(-dot(d1, d1) * 3.3) * (0.20 + 0.08 * sin(t * 0.7) + 0.16 * n1);
    vec2 d2 = (uv - vec2(0.04, 0.90)) * vec2(1.15, 1.3);
    col += accent2.rgb * exp(-dot(d2, d2) * 4.5) * (0.075 + 0.035 * sin(t * 0.55 + 2.0));

    // ---- sweeping diagonal light beams ----
    float ca = cos(-0.42), sa = sin(-0.42);
    float px = (uv.x - 0.5) * ca - (uv.y - 0.5) * sa;
    float py = (uv.x - 0.5) * sa + (uv.y - 0.5) * ca;
    float b1 = band(px, fract(t * 0.021) * 1.6 - 0.3, 9.0);
    float b2 = band(px, fract(t * 0.013 + 0.52) * 1.8 - 0.4, 13.0);
    float b3 = band(px, fract(0.30 - t * 0.008) * 2.2 - 0.6, 7.0);
    float shimmer = 0.55 + 0.45 * fbm(vec2(px * 2.2, py * 1.5 + t * 0.08));
    vec3 beamtint = mix(accent.rgb, accent2.rgb, 0.5 + 0.5 * sin(t * 0.1));
    col += beamtint * (b1 * 0.055 + b2 * 0.038 + b3 * 0.048) * shimmer;

    // ---- perspective grid floor (below the horizon) ----
    float horizon = 0.74;
    float d = uv.y - horizon;
    if (d > 0.0) {
        float persp = 1.0 / max(d, 0.025);
        float cv = fract((uv.x - 0.5) * persp * 0.55 + (uv.x - 0.5) * 3.0);
        float rows = fract(persp * 0.055 - t * 0.02);
        float lines = pow(1.0 - abs(2.0 * cv - 1.0), 14.0) * 0.6
                    + pow(1.0 - abs(2.0 * rows - 1.0), 14.0) * 0.5;
        float fade = smoothstep(0.0, 0.05, d) * (1.0 - smoothstep(0.20, 0.30, d) * 0.4);
        col += beamtint * lines * fade * 0.040;
        // glow pooling on the horizon under the aurora
        col += accent.rgb * smoothstep(0.10, 0.0, d) * (0.03 + 0.02 * n1);
    }

    // ---- dust: two parallax layers ----
    vec2 gp = (uv + par * 0.045) * vec2(96.0, 56.0) + vec2(t * 0.30, -t * 0.15);
    vec2 gi = floor(gp); vec2 gf = fract(gp) - 0.5;
    float rn = hash21(gi);
    float st1 = smoothstep(0.055, 0.0, length(gf)) * step(0.972, rn) * (0.35 + 0.65 * abs(sin(t * 1.7 + rn * 53.0)));
    col += st1 * vec3(1.0, 0.82, 0.76) * 0.16;
    vec2 gp2 = (uv + par * 0.020) * vec2(44.0, 26.0) + vec2(-t * 0.06, t * 0.045);
    vec2 gi2 = floor(gp2); vec2 gf2 = fract(gp2) - 0.5;
    float rn2 = hash21(gi2 + 7.0);
    float st2 = smoothstep(0.045, 0.0, length(gf2)) * step(0.991, rn2) * (0.35 + 0.65 * abs(sin(t * 1.1 + rn2 * 91.0)));
    col += st2 * mix(accent.rgb, vec3(1.0), 0.5) * 0.22;

    // ---- slow horizontal scanline sweep ----
    float scan = fract(t * 0.055);
    col += vec3(1.0, 0.95, 0.93) * band(uv.y, scan, 46.0) * 0.016;

    // grain + vignette
    float gr = hash21(uv * vec2(1920.0, 1080.0) + fract(t) * 97.0);
    col += (gr - 0.5) * 0.013;
    vec2 q = uv - 0.5;
    col *= 1.0 - dot(q, q) * 0.62;

    COLOR = vec4(col, 1.0);
}
",
        };
        mat = new ShaderMaterial { Shader = shader };
        mat.SetShaderParameter("accent", UiTheme.Accent);
        mat.SetShaderParameter("accent2", UiTheme.Teal);
        var art = new ColorRect { Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore, Material = mat };
        AddChild(art);
        art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _Process(double delta)
    {
        if (mat == null) return;
        var vp = GetViewportRect().Size;
        if (vp.X < 2 || vp.Y < 2) return;
        mouse = mouse.Lerp(GetViewport().GetMousePosition() / vp, (float)delta * 1.6f);
        mat.SetShaderParameter("mouse", mouse);
    }
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
        AddChild(new IntroSweep());
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

    /// <summary>Screen title block: big uppercase heading + dim subtitle, used by Settings and Stats.</summary>
    protected Control Heading(string title, string sub)
    {
        var v = VBox(4 * K);
        // NV-OPS header: signal-diamond kicker, title, caption, then a short accent rail that fades out
        var kit = new DrawBox
        {
            CustomMinimumSize = new Vector2(0, 9 * K),
            OnDraw = d =>
            {
                float k = K;
                Gfx.Diamond(d, new Vector2(4 * k, d.Size.Y / 2), 3 * k, 3 * k, UiTheme.Accent);
                d.DrawRect(new Rect2(12 * k, d.Size.Y / 2 - 0.5f, MathF.Min(46 * k, MathF.Max(0, d.Size.X - 24 * k)), 1), new Color(UiTheme.Text, 0.25f));
            },
        };
        v.AddChild(kit);
        v.AddChild(Lbl(title, UiTheme.Display, 46, UiTheme.Text));
        v.AddChild(Lbl(sub, UiTheme.HudWide, 12.5f, UiTheme.Faint));
        var rail = new DrawBox
        {
            CustomMinimumSize = new Vector2(56 * K, 2.5f * K),
            OnDraw = d =>
            {
                float k = K;
                float solid = MathF.Max(1f, d.Size.X - 12 * k);
                d.DrawRect(new Rect2(0, 0, solid, d.Size.Y), UiTheme.Accent);
                Gfx.HGradient(d, new Rect2(solid, 0, 12 * k, d.Size.Y), new Color(UiTheme.Accent, 0.5f), new Color(UiTheme.Accent, 0f));
            },
        };
        v.AddChild(rail);
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
