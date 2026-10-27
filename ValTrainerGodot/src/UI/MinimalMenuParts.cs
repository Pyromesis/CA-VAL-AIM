using Godot;
using ValTrainer.Core;
using ValTrainer.Maps;

namespace ValTrainer.UI;

// The rest of the menu in the minimalist style (1.18): the panel used by pop-ups (drill preview), the map selector and
// its picker, the update link of the footer and the update strip under the header. Same rules as MinimalKit.cs: flat
// surfaces, hairlines, light type, one accent, eased entrances.

/// <summary>Flat pop-up surface: a slightly lifted background, a hairline border and an optional light title.</summary>
public partial class MPanel : MarginContainer
{
    public string Title = "";
    public string? Caption;
    public float K = 1f;
    public float Pad = 30f;
    float t;

    public float TitleH => Title.Length > 0 ? 64 * K : 0;

    public override void _Ready()
    {
        int p = (int)(Pad * K);
        AddThemeConstantOverride("margin_left", p);
        AddThemeConstantOverride("margin_right", p);
        AddThemeConstantOverride("margin_top", (int)(Title.Length > 0 ? TitleH + 10 * K : p));
        AddThemeConstantOverride("margin_bottom", p);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        if (t > 1f) return;
        t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float k = K;
        var r = new Rect2(Vector2.Zero, Size);
        DrawRect(r, new Color(0.055f, 0.067f, 0.086f, 0.985f));
        float bw = Mathf.Max(1, k);
        var line = UiTheme.Line;
        DrawRect(new Rect2(0, 0, r.Size.X, bw), line); DrawRect(new Rect2(0, r.Size.Y - bw, r.Size.X, bw), line);
        DrawRect(new Rect2(0, 0, bw, r.Size.Y), line); DrawRect(new Rect2(r.Size.X - bw, 0, bw, r.Size.Y), line);
        if (Title.Length == 0) return;
        float x = Pad * k, rv = Mn.Reveal(t, 0.05f);
        int fs = UiTheme.Fs(34, k), cs = UiTheme.Fs(11, k);
        if (Caption != null)
            DrawString(UiTheme.HudWide, new Vector2(x, 26 * k), Loc.T(Caption), HorizontalAlignment.Left, -1, cs, Mn.A(UiTheme.Faint, rv));
        DrawString(UiTheme.Light, new Vector2(x - 2 * k, 26 * k + fs * 1.05f + (1 - rv) * 6 * k), Loc.T(Title), HorizontalAlignment.Left, -1, fs, Mn.A(UiTheme.Text, rv));
        DrawRect(new Rect2(x, TitleH + 2 * k, 22 * k * rv, Mathf.Max(1, 1.5f * k)), Mn.A(UiTheme.Accent, rv));
    }
}

/// <summary>The menu's map choice: ‹ NAME › with the art status under the name; the name opens the map picker.</summary>
public partial class MMapSelector : HBoxContainer
{
    readonly Control host;
    readonly float k;
    readonly MMapName name;
    MMapPicker? picker;
    public Action? Changed;
    public float Delay;

    public MMapSelector(Control host, float k)
    {
        this.host = host;
        this.k = k;
        AddThemeConstantOverride("separation", (int)(14 * k));
        var prev = new MLink { K = k, Label = "‹", Font = UiTheme.Light, Px = 26, TooltipText = "Previous map" };
        prev.Pressed += () => Step(-1);
        name = new MMapName { K = k, CustomMinimumSize = new Vector2(170 * k, 46 * k), TooltipText = "Map for the map, utility and agent drills · click to see every map" };
        name.Pressed += () => { if (IsOpen) Close(); else Open(); };
        var next = new MLink { K = k, Label = "›", Font = UiTheme.Light, Px = 26, TooltipText = "Next map" };
        next.Pressed += () => Step(1);
        AddChild(prev); AddChild(name); AddChild(next);
    }

    public override void _Ready()
    {
        foreach (var c in GetChildren()) if (c is MButtonBase b) b.Delay = Delay;
    }

    public bool IsOpen => picker != null && IsInstanceValid(picker) && picker.IsInsideTree();

    void Step(int dir)
    {
        var opts = MapSelector.Options();
        int i = opts.FindIndex(o => string.Equals(o.Key, Main.I.MapKey, StringComparison.OrdinalIgnoreCase));
        if (i < 0) i = 0;
        Select(opts[(i + dir + opts.Count) % opts.Count].Key);
    }

    public void Select(string key)
    {
        Main.I.Settings.MapKey = key;
        Main.I.Settings.Save();
        name.Bump();
        Changed?.Invoke();
    }

    public void Open()
    {
        if (IsOpen || !IsInsideTree()) return;
        picker = new MMapPicker(k, Main.I.MapKey, key => { Select(key); Close(); }, Close);
        host.AddChild(picker);
    }

    public void Close()
    {
        if (picker != null && IsInstanceValid(picker)) picker.Dismiss();
        picker = null;
    }

    public override void _ExitTree() => Close();
}

/// <summary>The selected map's name in light type with its art status in small caps; a change cross-fades it.</summary>
public partial class MMapName : MButtonBase
{
    float swap = 1f;
    public void Bump() { swap = 0f; QueueRedraw(); }
    protected override bool Animating => swap < 1f;

    public override void _Process(double delta)
    {
        if (swap < 1f) swap = Mathf.Min(1f, swap + (float)delta / 0.28f);
        base._Process(delta);
    }

    public override void _Draw()
    {
        if (!BeginDraw()) return;
        float k = K, cy = Size.Y / 2;
        string key = Main.I.MapKey;
        string nm = MapSelector.NameOf(key);
        nm = nm.Length > 1 ? nm[0] + nm[1..].ToLowerInvariant() : nm;
        float e = Gfx.EaseOutCubic(swap);
        int fs = UiTheme.Fs(24, k), cs = UiTheme.Fs(10, k);
        DrawString(UiTheme.Light, new Vector2(0, cy + fs * 0.12f + (1 - e) * 6 * k), nm, HorizontalAlignment.Left, -1, fs,
            Mn.A(UiTheme.TextSoft.Lerp(UiTheme.Text, Hover), e));
        DrawString(UiTheme.HudWide, new Vector2(1, cy + fs * 0.12f + cs * 1.6f), Loc.T(MapSelector.ArtLabel(key)), HorizontalAlignment.Left, -1, cs,
            Mn.A(UiTheme.Faint, e));
        float w = UiTheme.Light.GetStringSize(nm, HorizontalAlignment.Left, -1, fs).X;
        if (Hover > 0.01f) DrawRect(new Rect2(0, cy + fs * 0.12f + cs * 2.3f, w * Hover, Mathf.Max(1, k)), Mn.A(UiTheme.Text, 0.4f * Hover));
    }
}

/// <summary>Full-screen map picker: the screen dims, the maps rise in as a grid of flat minimap tiles; click one, click
/// outside or press Esc to close.</summary>
public partial class MMapPicker : Control
{
    readonly float k;
    readonly string current;
    readonly Action<string> pick;
    readonly Action close;
    float t, outT = -1f;

    public MMapPicker(float k, string current, Action<string> pick, Action close)
    {
        this.k = k; this.current = current; this.pick = pick; this.close = close;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Ready()
    {
        var opts = MapSelector.Options();
        var vs = GetViewportRect().Size;
        int cols = vs.X / k >= 1500 ? 7 : 5;
        float tileW = Mathf.Min(190 * k, (vs.X - 200 * k) / cols - 16 * k), tileH = tileW * 0.72f + 34 * k;
        var grid = new GridContainer { Columns = cols, MouseFilter = MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", (int)(16 * k));
        grid.AddThemeConstantOverride("v_separation", (int)(18 * k));
        int i = 0;
        foreach (var (key, nm) in opts)
        {
            var tile = new MMapTile(key, nm) { K = k, Selected = key.Equals(current, StringComparison.OrdinalIgnoreCase), Delay = 0.05f + i++ * 0.025f, CustomMinimumSize = new Vector2(tileW, tileH) };
            var kk = key;
            tile.Pressed += () => pick(kk);
            grid.AddChild(tile);
        }
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", (int)(22 * k));
        col.AddChild(new MText { K = k, Text = "CHOOSE A MAP", Font = UiTheme.HudWide, Px = 11.5f, Color = UiTheme.Faint, Delay = 0.02f, CustomMinimumSize = new Vector2(400 * k, 0) });
        col.AddChild(grid);
        center.AddChild(col);
        AddChild(center);
    }

    public void Dismiss()
    {
        if (outT >= 0) return;
        outT = 0f;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { AcceptEvent(); close(); }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        t += dt;
        if (outT >= 0)
        {
            outT += dt;
            Modulate = new Color(1, 1, 1, 1f - Gfx.EaseOutCubic(outT / 0.2f));
            if (outT >= 0.2f) QueueFree();
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float a = Gfx.EaseOutCubic(t / 0.25f);
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(UiTheme.Bg, 0.9f * a));
    }
}

/// <summary>One map in the picker: its minimap, flat, with the name under it; the current map has the accent line.</summary>
public partial class MMapTile : MButtonBase
{
    readonly string key, label;
    public bool Selected;

    public MMapTile(string key, string label) { this.key = key; this.label = label; }

    public override void _Draw()
    {
        if (!BeginDraw()) return;
        float k = K;
        var img = new Rect2(0, 0, Size.X, Size.Y - 34 * k);
        DrawRect(img, new Color(1, 1, 1, 0.025f + 0.03f * Hover));
        MapSwatch.Draw(this, img.Grow(-6 * k), key, k);
        if (Hover < 0.99f) DrawRect(img, new Color(UiTheme.Bg, 0.35f * (1 - Hover)));
        int fs = UiTheme.Fs(14, k), cs = UiTheme.Fs(9.5f, k);
        string nm = label.Length > 1 ? label[0] + label[1..].ToLowerInvariant() : label;
        DrawString(UiTheme.Body, new Vector2(0, img.End.Y + 18 * k), Loc.T(nm), HorizontalAlignment.Left, -1, fs, Selected ? UiTheme.Text : UiTheme.TextSoft.Lerp(UiTheme.Text, Hover));
        DrawString(UiTheme.HudWide, new Vector2(0, img.End.Y + 31 * k), Loc.T(MapSelector.ArtLabel(key)), HorizontalAlignment.Left, -1, cs, UiTheme.Faint);
        if (Selected) DrawRect(new Rect2(0, img.End.Y + 2 * k, Size.X, Mathf.Max(1.5f, 2 * k)), UiTheme.Accent);
        else if (Hover > 0.01f) DrawRect(new Rect2(0, img.End.Y + 2 * k, Size.X * Hover, Mathf.Max(1, k)), Mn.A(UiTheme.Text, 0.5f * Hover));
    }
}

/// <summary>
/// The weapon choice shared by the main menu and the pause overlay: ‹ NAME › (DRILL DEFAULT = each drill's own gun), the
/// class under it, and a click opens the full arsenal. The pick is global: every drill can be played with every gun.
/// </summary>
public partial class MWeaponSelector : HBoxContainer
{
    readonly Control host;
    readonly float k;
    readonly MWeaponName name;
    MWeaponPicker? picker;
    public Action? Changed;
    public float Delay;

    public MWeaponSelector(Control host, float k)
    {
        this.host = host;
        this.k = k;
        AddThemeConstantOverride("separation", (int)(12 * k));
        var prev = new MLink { K = k, Label = "‹", Font = UiTheme.Light, Px = 24, TooltipText = "Previous weapon" };
        prev.Pressed += () => Step(-1);
        name = new MWeaponName { K = k, CustomMinimumSize = new Vector2(210 * k, 42 * k), TooltipText = "Weapon for every drill · click for the full arsenal" };
        name.Pressed += () => { if (IsOpen) Close(); else Open(); };
        var next = new MLink { K = k, Label = "›", Font = UiTheme.Light, Px = 24, TooltipText = "Next weapon" };
        next.Pressed += () => Step(1);
        AddChild(prev); AddChild(name); AddChild(next);
    }

    static int Count => Weapons.AllPlayable.Length + 1; // + DRILL DEFAULT at index 0
    static int Current => Math.Clamp(Main.I.Settings.WeaponOverride + 1, 0, Count - 1);

    public override void _Ready()
    {
        foreach (var c in GetChildren()) if (c is MButtonBase b) b.Delay = Delay;
    }

    public bool IsOpen => picker != null && IsInstanceValid(picker) && picker.IsInsideTree();

    void Step(int dir)
    {
        int i = (Current + dir + Count) % Count;
        Select(i - 1);
    }

    public void Select(int pick)
    {
        Main.I.Settings.WeaponOverride = Math.Clamp(pick, -1, Weapons.AllPlayable.Length - 1);
        Main.I.Settings.Save();
        name.Bump();
        Changed?.Invoke();
    }

    public void Open()
    {
        if (IsOpen || !IsInsideTree()) return;
        picker = new MWeaponPicker(k, Main.I.Settings.WeaponOverride, p => { Select(p); Close(); }, Close);
        host.AddChild(picker);
    }

    public void Close()
    {
        if (picker != null && IsInstanceValid(picker)) picker.Dismiss();
        picker = null;
    }

    public override void _ExitTree() => Close();
}

/// <summary>The selected weapon in light type with its class underneath (or DRILL DEFAULT); a change cross-fades.</summary>
public partial class MWeaponName : MButtonBase
{
    float swap = 1f;
    public void Bump() { swap = 0f; QueueRedraw(); }
    protected override bool Animating => swap < 1f;

    public override void _Process(double delta)
    {
        if (swap < 1f) swap = Mathf.Min(1f, swap + (float)delta / 0.28f);
        base._Process(delta);
    }

    public override void _Draw()
    {
        if (!BeginDraw()) return;
        float k = K, cy = Size.Y / 2, e = Gfx.EaseOutCubic(swap);
        int pick = Main.I.Settings.WeaponOverride;
        bool def = pick < 0 || pick >= Weapons.AllPlayable.Length;
        string nm = def ? "Drill default" : Weapons.AllPlayable[pick].Name;
        string cls = def ? "each drill's own weapon" : Weapons.ClassName(Weapons.AllPlayable[pick].Class);
        int fs = UiTheme.Fs(21, k), cs = UiTheme.Fs(10, k);
        DrawString(UiTheme.Light, new Vector2(0, cy + fs * 0.12f + (1 - e) * 6 * k), nm, HorizontalAlignment.Left, -1, fs,
            Mn.A(UiTheme.TextSoft.Lerp(UiTheme.Text, Hover), e));
        DrawString(UiTheme.HudWide, new Vector2(1, cy + fs * 0.12f + cs * 1.7f), Loc.T(cls), HorizontalAlignment.Left, -1, cs,
            Mn.A(UiTheme.Faint, e));
        float w = UiTheme.Light.GetStringSize(nm, HorizontalAlignment.Left, -1, fs).X;
        if (Hover > 0.01f) DrawRect(new Rect2(0, cy + fs * 0.12f + cs * 2.4f, w * Hover, Mathf.Max(1, k)), Mn.A(UiTheme.Text, 0.4f * Hover));
    }
}

/// <summary>Full-screen arsenal: every playable weapon plus DRILL DEFAULT. Click one, click outside or Esc to close.</summary>
public partial class MWeaponPicker : Control
{
    readonly float k;
    readonly int current;
    readonly Action<int> pick;
    readonly Action close;
    float t, outT = -1f;

    public MWeaponPicker(float k, int current, Action<int> pick, Action close)
    {
        this.k = k; this.current = current; this.pick = pick; this.close = close;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
    }

    public override void _Ready()
    {
        GrabFocus();
        var vs = GetViewportRect().Size;
        int cols = vs.X / k >= 1300 ? 4 : 3;
        float tileW = Mathf.Min(250 * k, (vs.X - 160 * k) / cols - 14 * k), tileH = 58 * k;
        var grid = new GridContainer { Columns = cols, MouseFilter = MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", (int)(14 * k));
        grid.AddThemeConstantOverride("v_separation", (int)(10 * k));
        var tile = new MWeaponTile(-1, Loc.T("Drill default"), Loc.T("each drill's own weapon")) { K = k, Selected = current < 0, Delay = 0.04f, CustomMinimumSize = new Vector2(tileW, tileH) };
        tile.Pressed += () => pick(-1);
        grid.AddChild(tile);
        int i = 0;
        foreach (var w in Weapons.AllPlayable)
        {
            int idx = i++;
            var wt = new MWeaponTile(idx, w.Name, Loc.T(Weapons.ClassName(w.Class))) { K = k, Selected = idx == current, Delay = 0.05f + i * 0.012f, CustomMinimumSize = new Vector2(tileW, tileH) };
            wt.Pressed += () => pick(idx);
            grid.AddChild(wt);
        }
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", (int)(18 * k));
        col.AddChild(new MText { K = k, Text = Loc.T("CHOOSE A WEAPON"), Font = UiTheme.HudWide, Px = 11.5f, Color = UiTheme.Faint, Delay = 0.02f, CustomMinimumSize = new Vector2(420 * k, 0) });
        col.AddChild(grid);
        col.AddChild(new MText { K = k, Text = Loc.T("Every drill · every gun · Esc closes"), Font = UiTheme.Body, Px = 12, Color = UiTheme.Faint, Delay = 0.3f });
        center.AddChild(col);
        AddChild(center);
    }

    public void Dismiss()
    {
        if (outT >= 0) return;
        outT = 0f;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>
    /// Esc and clicks outside the tiles close the picker before the session / menu sees them. The in-game HUD swallows
    /// mouse presses (so a pause-resume click never fires), which is why the picker handles its own dismissal here.
    /// </summary>
    public override void _Input(InputEvent e)
    {
        if (outT >= 0) return;
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            close();
            return;
        }
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb && !OnTile(mb.Position))
        {
            GetViewport().SetInputAsHandled();
            close();
        }
    }

    bool OnTile(Vector2 p)
    {
        bool Hit(Node n)
        {
            foreach (var c in n.GetChildren())
            {
                if (c is Control { Visible: false }) continue;
                if (c is BaseButton b && b.GetGlobalRect().HasPoint(p)) return true;
                if (Hit(c)) return true;
            }
            return false;
        }
        return Hit(this);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { AcceptEvent(); close(); }
        else if (e is InputEventKey { Pressed: true, Keycode: Key.Escape }) { AcceptEvent(); close(); }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        t += dt;
        if (outT >= 0)
        {
            outT += dt;
            Modulate = new Color(1, 1, 1, 1f - Gfx.EaseOutCubic(outT / 0.2f));
            if (outT >= 0.2f) QueueFree();
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float a = Gfx.EaseOutCubic(t / 0.25f);
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(UiTheme.Bg, 0.94f * a));
    }
}

/// <summary>One weapon in the picker: name + class, accent underline when selected.</summary>
public partial class MWeaponTile : MButtonBase
{
    readonly int index;
    readonly string label, sub;
    public bool Selected;

    public MWeaponTile(int index, string label, string sub) { this.index = index; this.label = label; this.sub = sub; }

    public override void _Draw()
    {
        if (!BeginDraw()) return;
        float k = K, h = Size.Y;
        var r = new Rect2(0, 0, Size.X, h);
        DrawRect(r, new Color(1, 1, 1, 0.025f + 0.04f * Hover));
        DrawRect(new Rect2(0, h - 1, Size.X, 1), UiTheme.Line);
        int fs = UiTheme.Fs(16, k), cs = UiTheme.Fs(9.5f, k);
        Gfx.TextFit(this, UiTheme.Body, label, 13 * k, h * 0.46f, fs,
            Selected ? UiTheme.Text : UiTheme.TextSoft.Lerp(UiTheme.Text, Hover), Size.X - 26 * k);
        Gfx.TextFit(this, UiTheme.HudWide, sub, 13 * k, h * 0.46f + cs * 1.6f, cs, UiTheme.Faint, Size.X - 26 * k);
        if (Selected) DrawRect(new Rect2(0, h - 2 * k, Size.X, Mathf.Max(1.5f, 2 * k)), UiTheme.Accent);
        else if (Hover > 0.01f) DrawRect(new Rect2(0, h - 1, Size.X * Hover, 1), Mn.A(UiTheme.Text, 0.45f * Hover));
        _ = index;
    }
}

/// <summary>The footer's update control as a text link: CHECK FOR UPDATES → UPDATE TO x.y.z → DOWNLOADING 42% →
/// RESTART TO UPDATE (in the accent). Same behaviour as <see cref="UpdateNowButton"/>.</summary>
public partial class MUpdateLink : MLink
{
    int seen = -1;

    public override void _Ready()
    {
        Pressed += UpdateNowButton.DoAct;
        Refresh();
        base._Ready();
    }

    public override void _Process(double delta)
    {
        if (seen != UpdateCheck.Revision || Updater.Status == Updater.State.Downloading) Refresh();
        base._Process(delta);
    }

    void Refresh()
    {
        seen = UpdateCheck.Revision;
        var (label, tip, disabled, primary) = UpdateNowButton.Compute();
        if (Label != label) { Label = label; QueueRedraw(); }
        Tint = primary ? UiTheme.Accent : null;
        Disabled = disabled;
        TooltipText = tip;
    }
}

/// <summary>The update strip under the header, minimal: one line of text, a hairline, and while downloading a thin
/// progress line in the accent. Its buttons are added by the menu as links on the right.</summary>
public partial class MUpdateStrip : Control
{
    public float K = 1f;
    public string Version = "";
    public UpdateBanner.Stage Kind;
    float t;

    public MUpdateStrip() => MouseFilter = MouseFilterEnum.Pass;

    public override void _Process(double delta)
    {
        t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float k = K, cy = Size.Y / 2, r = Mn.Reveal(t, 0f);
        string v = Version;
        string head = Kind switch
        {
            UpdateBanner.Stage.Ready => $"CA-VAL-AIM {v} is ready",
            UpdateBanner.Stage.Applying => $"Installing CA-VAL-AIM {v}…",
            UpdateBanner.Stage.Downloading => $"Downloading CA-VAL-AIM {v} · {Updater.Progress * 100:0}%",
            _ => $"CA-VAL-AIM {v} is available",
        };
        string sub = Kind switch
        {
            UpdateBanner.Stage.Ready => "restart to update" + (Updater.NeedsElevation ? " · Windows will ask for permission" : ""),
            UpdateBanner.Stage.Applying => "it restarts by itself",
            _ => $"you have {AppInfo.Version}",
        };
        int fs = UiTheme.Fs(15, k), ss = UiTheme.Fs(13, k);
        bool ready = Kind is UpdateBanner.Stage.Ready or UpdateBanner.Stage.Applying;
        DrawCircle(new Vector2(4 * k, cy), 3 * k, Mn.A(ready ? UiTheme.Accent : UiTheme.TextSoft, r));
        DrawString(UiTheme.Body, new Vector2(16 * k, Gfx.Mid(cy, fs)), Loc.T(head), HorizontalAlignment.Left, -1, fs, Mn.A(UiTheme.Text, r));
        float w = UiTheme.Body.GetStringSize(Loc.T(head), HorizontalAlignment.Left, -1, fs).X;
        DrawString(UiTheme.Body, new Vector2(28 * k + w, Gfx.Mid(cy, ss)), Loc.T(sub), HorizontalAlignment.Left, -1, ss, Mn.A(UiTheme.Faint, r));
        DrawRect(new Rect2(0, Size.Y - 1, Size.X * r, 1), UiTheme.Line);
        if (Kind == UpdateBanner.Stage.Downloading)
            DrawRect(new Rect2(0, Size.Y - 1.5f * k, Size.X * Mathf.Clamp(Updater.Progress, 0, 1), Mathf.Max(1.5f, 2 * k)), UiTheme.Accent);
    }
}
