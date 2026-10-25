using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using ValTrainer.Core;
using ValTrainer.Game;
using ValTrainer.Modes;
using ValTrainer.World;

namespace ValTrainer.UI;

/// <summary>
/// Between the menu and a drill (Main.StartMode). The drill is built first (its map's collision and the safe spawn);
/// a drill on the shooting range goes straight in. A map drill then reads its level GLBs here instead of inside the
/// session: a worker thread strips each file (<see cref="GltfStrip.Prepare"/>, pure C#) while the main thread turns the
/// previous one into a scene, one level per frame, so the screen keeps drawing. The bar is the real progress, weighted
/// by file size: collision first, then each level's strip and build. The session then takes the prepared levels
/// (<see cref="RealMap.TryBuild"/>) and starts under a crossfade. Esc cancels back to the menu.
/// </summary>
public partial class LoadingScreen : ScreenBase
{
    readonly Func<TrainingMode> make;
    readonly bool freePlay;
    TrainingMode? first;
    string? folder;
    List<RealMap.PartFile> parts = new();
    byte[]?[] stripped = Array.Empty<byte[]?>();
    bool[] strippedDone = Array.Empty<bool>();
    int built;
    long totalBytes, strippedBytes, builtBytes;
    Task? worker;
    readonly CancellationTokenSource cancel = new();
    int phase; // 0: first frame on screen, 1: build the drill, 2: levels, 3: done, 4: started
    float clock, shown, doneAt = -1;
    string title = "", subtitle = "", step = "";
    readonly Stopwatch watch = Stopwatch.StartNew();
    bool started;

    public LoadingScreen(Func<TrainingMode> make, bool freePlay)
    {
        this.make = make;
        this.freePlay = freePlay;
    }

    protected override bool AnimatedBackdrop => false;
    protected override void Back() => Cancel();

    /// <summary>Weight of building the drill (collision + spawn) in the bar; the levels share the rest by size.</summary>
    const float ModeShare = 0.12f;
    /// <summary>Share of a level's weight that its strip (worker) earns; its build earns the rest.</summary>
    const float StripShare = 0.35f;

    float RealProgress
    {
        get
        {
            if (phase <= 1) return 0f;
            if (totalBytes <= 0) return 1f;
            float levels = (StripShare * Interlocked.Read(ref strippedBytes) + (1 - StripShare) * builtBytes) / totalBytes;
            return ModeShare + (1 - ModeShare) * Mathf.Clamp(levels, 0, 1);
        }
    }

    protected override void Build()
    {
        float k = K;
        AddChild(new MinimalBackdrop());
        AddChild(new LoadingView(this) { K = k });
    }

    protected override void Tick(float dt)
    {
        clock += dt;
        switch (phase)
        {
            case 0:
                phase = 1; // let this frame reach the screen before the first blocking step
                return;
            case 1:
                BuildMode();
                return;
            case 2:
                StepLevels();
                break;
        }
        shown = Mn.Approach(shown, RealProgress, dt, 7f);
        if (phase == 3)
        {
            if (doneAt < 0 && shown > 0.995f) { shown = 1f; doneAt = clock; }
            if (doneAt >= 0 && clock - doneAt > 0.18f) Start();
        }
    }

    void BuildMode()
    {
        try
        {
            first = make();
        }
        catch (Exception e)
        {
            Log.Error($"[loading] could not create the drill: {Log.Describe(e)}");
            Cancel();
            return;
        }
        var map = first.Map;
        title = map != null ? map.Map : Loc.T(first.Name);
        subtitle = map != null ? $"{Loc.T(first.Name)}  ·  {map.Name}" : Loc.T(first.Description);
        folder = map?.RealMapFolder;
        parts = RealMap.HasArt(folder) ? RealMap.PartFiles(folder) : new List<RealMap.PartFile>();
        if (parts.Count == 0)
        {
            // The range or a blockout map: nothing heavy left to read. Straight in (the screen-change crossfade
            // covers the hand-over, so this screen never flashes up for a frame).
            Start();
            return;
        }
        totalBytes = parts.Sum(p => p.Bytes);
        stripped = new byte[]?[parts.Count];
        strippedDone = new bool[parts.Count];
        var list = parts.ToArray();
        var token = cancel.Token;
        worker = Task.Run(() =>
        {
            for (int i = 0; i < list.Length && !token.IsCancellationRequested; i++)
            {
                byte[]? data = null;
                try { data = GltfStrip.Prepare(list[i].Path); } catch { /* the build step falls back to the raw file */ }
                stripped[i] = data;
                Volatile.Write(ref strippedDone[i], true);
                Interlocked.Add(ref strippedBytes, list[i].Bytes);
            }
        }, token);
        phase = 2;
        step = Loc.Spanish ? "Leyendo geometría" : "Reading geometry";
        GD.Print($"[loading] {first.Key} on {map!.Key}: {parts.Count} levels, {totalBytes / (1024 * 1024)} MB; drill built in {watch.ElapsedMilliseconds} ms");
    }

    void StepLevels()
    {
        if (built >= parts.Count)
        {
            phase = 3;
            step = Loc.Spanish ? "Listo" : "Ready";
            GD.Print($"[loading] levels ready in {watch.ElapsedMilliseconds} ms");
            return;
        }
        if (!Volatile.Read(ref strippedDone[built]))
        {
            step = (Loc.Spanish ? "Leyendo" : "Reading") + $" {built + 1} / {parts.Count}";
            return;
        }
        var p = parts[built];
        step = (Loc.Spanish ? "Construyendo" : "Building") + $" {built + 1} / {parts.Count}";
        var scene = RealMap.LoadPart(p.Path, p.Part, stripped[built], stripTried: true);
        stripped[built] = null; // the bytes are in the scene now
        if (scene != null) RealMap.StorePreloaded(folder!, p.Part, scene);
        builtBytes += p.Bytes;
        built++;
    }

    void Start()
    {
        if (started || first == null) return;
        started = true;
        phase = 4;
        var ready = first;
        bool used = false;
        // The session's first build uses the drill made here (same random map, same spawn); a restart makes a new one.
        TrainingMode Factory()
        {
            if (!used) { used = true; return ready; }
            return make();
        }
        Main.I.ShowScreen(new GameSession(Factory) { FreePlay = freePlay });
    }

    void Cancel()
    {
        if (started) return;
        started = true;
        cancel.Cancel();
        try { worker?.Wait(2000); } catch { }
        RealMap.ClearPreloaded();
        Main.I.ShowMenu();
    }

    public override void _ExitTree()
    {
        cancel.Cancel();
        if (!started) RealMap.ClearPreloaded();
    }

    /// <summary>The screen's drawing: map name in light display type, the drill under it, a hairline progress bar with
    /// the real percentage and the current step. Fades in only once there is something to load.</summary>
    public partial class LoadingView : Control
    {
        readonly LoadingScreen s;
        public float K = 1f;
        float seen; // seconds since the screen had something to show
        float sweep;

        public LoadingView(LoadingScreen s)
        {
            this.s = s;
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            if (s.phase >= 2) seen += Mathf.Min(dt, 1f / 30f);
            sweep = (sweep + dt * 0.55f) % 1f;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (s.phase < 2) return;
            float k = K, a = Gfx.EaseOutCubic(seen / 0.45f);
            if (s.phase == 4) a = 1f;
            var sz = Size;
            float side = Mathf.Clamp(sz.X * 0.05f, 28 * k, 112 * k);
            float barY = sz.Y * 0.74f, barW = sz.X - side * 2;
            float rise = (1 - a) * 12 * k;

            int ks = UiTheme.Fs(11.5f, k), ts = UiTheme.Fs(72, k), ss = UiTheme.Fs(16, k), ps = UiTheme.Fs(13, k);
            Gfx.Text(this, UiTheme.HudWide, "LOADING", side, barY - 168 * k + rise, ks, Mn.A(UiTheme.Faint, a));
            DrawString(UiTheme.Light, new Vector2(side - 3 * k, barY - 92 * k + rise), s.title, HorizontalAlignment.Left, -1, ts, Mn.A(UiTheme.Text, a));
            Gfx.TextFit(this, UiTheme.Body, s.subtitle, side, barY - 48 * k + rise, ss, Mn.A(UiTheme.Dim, a), barW);

            // hairline track, accent fill, a soft highlight travelling along the filled part while it loads
            DrawRect(new Rect2(side, barY, barW, 1), Mn.A(UiTheme.Line, a));
            float fill = barW * Mathf.Clamp(s.shown, 0, 1);
            if (fill > 0.5f)
            {
                DrawRect(new Rect2(side, barY - 0.5f * k, fill, Mathf.Max(2, 2 * k)), Mn.A(UiTheme.Accent, a));
                if (s.phase < 3)
                {
                    float hx = side + fill * sweep, hw = Mathf.Min(120 * k, fill * 0.4f);
                    Gfx.HGradient(this, new Rect2(hx - hw, barY - 0.5f * k, hw, Mathf.Max(2, 2 * k)), new Color(1, 1, 1, 0f), new Color(1, 1, 1, 0.35f * a));
                }
            }
            string pct = $"{Mathf.RoundToInt(Mathf.Clamp(s.shown, 0, 1) * 100)}%";
            Gfx.TextR(this, UiTheme.Hud, pct, side + barW, barY + 30 * k, ps, Mn.A(UiTheme.TextSoft, a));
            // already in the UI language (no Loc.T: its word fallback would mangle "LEYENDO 3 / 13")
            DrawString(UiTheme.HudWide, new Vector2(side, barY + 30 * k), s.step.ToUpperInvariant(), HorizontalAlignment.Left, -1, UiTheme.Fs(11, k), Mn.A(UiTheme.Faint, a));
            string esc = Loc.Spanish ? "Esc para cancelar" : "Esc to cancel";
            int es = UiTheme.Fs(12, k);
            DrawString(UiTheme.Body, new Vector2(side + barW - Gfx.RawW(UiTheme.Body, esc, es), sz.Y - 30 * k), esc,
                HorizontalAlignment.Left, -1, es, Mn.A(UiTheme.Faint, a * 0.8f));
        }
    }
}
