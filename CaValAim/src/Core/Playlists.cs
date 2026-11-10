using System.Text.Json;
using Godot;
using CaValAim.Game;
using CaValAim.Modes;

namespace CaValAim.Core;

/// <summary>A named set of drills played back to back (the clips are drill keys, in order).</summary>
public sealed class Playlist
{
    public string Id = Guid.NewGuid().ToString("N")[..10];
    public string Name = "";
    public List<string> Modes = new();
    /// <summary>
    /// Per-drill recoil setting for playback (mode key → camera climb). A playlist built by the coach carries one entry
    /// per drill; the player's own playlists leave it empty and keep whatever the pause menu has. It is a map, so editing
    /// the playlist by hand never has to keep a second list in sync.
    /// </summary>
    public Dictionary<string, bool> Recoil = new(StringComparer.OrdinalIgnoreCase);
    public List<bool?> RecoilByRun = new();
    /// <summary>
    /// Sensitivity multiplier per run, aligned with <see cref="Modes"/> (1 or a missing entry = the player's own sens).
    /// Only community routines use it, and only on the steps their source explicitly says to raise it. A parallel list
    /// (instead of a map) is needed because a routine may repeat the same drill at two different sensitivities.
    /// </summary>
    public List<float> Sens = new();
    /// <summary>Length of each run in minutes, aligned with <see cref="Modes"/> (0 = the drill's normal 60 s).</summary>
    public List<float> Minutes = new();

    public float SensAt(int i) => i >= 0 && i < Sens.Count && Sens[i] > 0 ? Sens[i] : 1f;
    public float MinutesAt(int i) => i >= 0 && i < Minutes.Count && Minutes[i] > 0 ? Minutes[i] : 0f;
    public bool? RecoilAt(int i) => i < RecoilByRun.Count && RecoilByRun[i] is { } value ? value
        : Recoil.TryGetValue(Modes[i], out bool legacy) ? legacy : null;
    public float TotalMinutes => Modes.Select((_, i) => MinutesAt(i) > 0 ? MinutesAt(i) : 1f).Sum();
}

/// <summary>One run of a playlist with the per-run settings the coach/routine wants.</summary>
public readonly record struct PlaylistRun(string Key, bool Recoil, float Sens = 1f, float Minutes = 0f);

/// <summary>Clamps the routine sensitivity boosts: never below 15 cm/360, and at most 1.5× when the player is already
/// above 2.85 cm/360 per 1× (i.e. below 35 cm/360 at the source sens), as the Voltaic bardOZ routine specifies.</summary>
public static class SensShift
{
    public const float MinCm360 = 15f;
    public const float HighSensCm360 = 35f;
    public const float HighSensFactorCap = 1.5f;

    public static float FactorFor(float sens, int dpi, float requested)
    {
        if (requested <= 0 || MathF.Abs(requested - 1f) < 0.001f) return 1f;
        float cm = PlayerView.Cm360(sens, dpi);
        if (cm <= 0) return requested;
        if (cm < HighSensCm360 && requested > HighSensFactorCap) requested = HighSensFactorCap;
        if (cm / requested < MinCm360) requested = cm / MinCm360;
        return MathF.Max(1f, requested);
    }

    public static float Applied(float sens, int dpi, float requested) => sens * FactorFor(sens, dpi, requested);
}

/// <summary>User playlists, stored next to settings.json (playlists.json). Small and UI-thread only.</summary>
public static class PlaylistStore
{
    static readonly JsonSerializerOptions Json = new() { IncludeFields = true, WriteIndented = true };
    static List<Playlist>? all;
    static string FilePath => Path.Combine(Paths.DataDir, "playlists.json");

    public static IReadOnlyList<Playlist> All
    {
        get
        {
            Load();
            return all!;
        }
    }

    static void Load()
    {
        if (all != null) return;
        all = new List<Playlist>();
        try
        {
            if (File.Exists(FilePath))
                all = JsonSerializer.Deserialize<List<Playlist>>(File.ReadAllText(FilePath), Json) ?? new List<Playlist>();
        }
        catch (Exception e) when (Paths.IsCorruptData(e))
        {
            Paths.KeepCorrupt(FilePath, e);
        }
        catch (Exception e)
        {
            Log.Error($"playlists.json couldn't be opened ({e.Message}); starting with no playlists");
            all.Clear();
        }
        foreach (var p in all)
        {
            p.Name ??= "";
            p.Modes ??= new List<string>();
            p.RecoilByRun ??= new List<bool?>();
            p.Recoil ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (p.Recoil.Comparer != StringComparer.OrdinalIgnoreCase)
                p.Recoil = new Dictionary<string, bool>(p.Recoil, StringComparer.OrdinalIgnoreCase);
            p.Sens ??= new List<float>();
            p.Minutes ??= new List<float>();
            for (int i = p.Modes.Count - 1; i >= 0; i--)
            {
                if (!string.IsNullOrEmpty(p.Modes[i])) continue;
                p.Modes.RemoveAt(i);
                if (i < p.RecoilByRun.Count) p.RecoilByRun.RemoveAt(i);
                if (i < p.Sens.Count) p.Sens.RemoveAt(i);
                if (i < p.Minutes.Count) p.Minutes.RemoveAt(i);
            }
            if (p.RecoilByRun.Count > p.Modes.Count) p.RecoilByRun.RemoveRange(p.Modes.Count, p.RecoilByRun.Count - p.Modes.Count);
            if (p.Sens.Count > p.Modes.Count) p.Sens.RemoveRange(p.Modes.Count, p.Sens.Count - p.Modes.Count);
            if (p.Minutes.Count > p.Modes.Count) p.Minutes.RemoveRange(p.Modes.Count, p.Minutes.Count - p.Modes.Count);
        }
    }

    public static void Save()
    {
        if (AppSettings.ReadOnly) return;
        try { Paths.TryWriteAllText(FilePath, JsonSerializer.Serialize(All, Json)); }
        catch (Exception e) { Log.Error($"Saving playlists failed: {e.Message}"); }
    }

    public static Playlist Create(string name)
    {
        Load();
        string clean = (name ?? "").Trim();
        if (clean.Length == 0) clean = $"Playlist {all!.Count + 1}";
        if (clean.Length > 40) clean = clean[..40];
        var p = new Playlist { Name = clean };
        all!.Add(p);
        Save();
        return p;
    }

    /// <summary>
    /// Creates the named playlist or, when one with that name already exists, empties and rewrites it (the coach's
    /// daily plan updates its own playlist instead of piling up a new one every time the button is pressed). The entries
    /// are played in order and may repeat a drill; <paramref name="recoil"/> sets the camera climb per run.
    /// </summary>
    public static Playlist Replace(string name, IEnumerable<(string Key, bool RecoilOn)> entries) =>
        ReplaceRuns(name, entries.Select(e => new PlaylistRun(e.Key, e.RecoilOn)));

    /// <summary>
    /// Same as <see cref="Replace(string, IEnumerable{ValueTuple{string, bool}})"/>, with the per-run sensitivity and
    /// minutes a built-in routine carries. Replaces the playlist with that name, so pressing a routine's "save as
    /// playlist" twice updates it instead of piling up copies.
    /// </summary>
    public static Playlist ReplaceRuns(string name, IEnumerable<PlaylistRun> entries)
    {
        Load();
        string clean = (name ?? "").Trim();
        if (clean.Length == 0) clean = "Playlist";
        if (clean.Length > 40) clean = clean[..40];
        var list = all!;
        var p = list.FirstOrDefault(x => string.Equals(x.Name, clean, StringComparison.OrdinalIgnoreCase));
        if (p == null)
        {
            p = new Playlist { Name = clean };
            list.Add(p);
        }
        p.Modes.Clear();
        p.Recoil.Clear();
        p.RecoilByRun.Clear();
        p.Sens.Clear();
        p.Minutes.Clear();
        foreach (var run in entries)
        {
            if (string.IsNullOrWhiteSpace(run.Key)) continue;
            p.Modes.Add(run.Key);
            p.Recoil[run.Key] = run.Recoil;
            p.RecoilByRun.Add(run.Recoil);
            p.Sens.Add(run.Sens > 0 ? run.Sens : 1f);
            p.Minutes.Add(run.Minutes > 0 ? run.Minutes : 0f);
        }
        Save();
        return p;
    }

    public static void Delete(Playlist p)
    {
        Load();
        all!.Remove(p);
        Save();
    }

    public static bool Contains(Playlist p, string key) => p.Modes.Contains(key);

    /// <summary>Adds the drill at the end, or removes it if it is already in the playlist.</summary>
    public static bool Toggle(Playlist p, string key)
    {
        bool added;
        int at = p.Modes.IndexOf(key);
        if (at >= 0)
        {
            p.Modes.RemoveAt(at);
            if (at < p.RecoilByRun.Count) p.RecoilByRun.RemoveAt(at);
            if (at < p.Sens.Count) p.Sens.RemoveAt(at);
            if (at < p.Minutes.Count) p.Minutes.RemoveAt(at);
            if (!p.Modes.Contains(key, StringComparer.OrdinalIgnoreCase)) p.Recoil.Remove(key);
            added = false;
        }
        else
        {
            while (p.RecoilByRun.Count < p.Modes.Count) p.RecoilByRun.Add(null);
            while (p.Sens.Count < p.Modes.Count) p.Sens.Add(1f);
            while (p.Minutes.Count < p.Modes.Count) p.Minutes.Add(0f);
            p.Modes.Add(key);
            p.RecoilByRun.Add(null);
            p.Sens.Add(1f);
            p.Minutes.Add(0f);
            added = true;
        }
        Save();
        return added;
    }
}

/// <summary>Plays a playlist: every drill after the last one hands over to the next session; the final drill keeps its
/// normal results screen. Nothing is changed about scoring: each run is an ordinary GameSession. A routine can also be
/// navigated while paused: <see cref="JumpTask"/> starts the first run of another task without saving the abandoned
/// one.</summary>
public static class PlaylistRunner
{
    sealed class Plan
    {
        public Func<TrainingMode> Make = null!;
        public string Key = "";
        public int Index;       // index in the source playlist (sensitivity/minutes/recoil)
        public int Step;        // routine step (task); a plain playlist gives every run its own step
        public string Label = "";
        public int RunOfStep = 1, RunsInStep = 1;
    }

    static List<Plan>? plans;
    static Playlist? playing;
    static Action<int>? started, finished;
    static int taskIndex;
    static string taskLabel = "";

    /// <summary>True when the current session belongs to a routine/playlist with more than one task.</summary>
    public static bool CanNavigate => plans is { Count: > 1 } && TaskCount > 1;
    public static int TaskCount { get; private set; }
    public static int TaskNumber => taskIndex + 1;
    public static int RunOfStep { get; private set; } = 1;
    public static int RunsInStep { get; private set; } = 1;
    public static string TaskLabel => taskLabel;
    public static bool CanGoPrev => CanNavigate && taskIndex > 0;
    public static bool CanGoNext => CanNavigate && taskIndex < TaskCount - 1;

    /// <summary>Clears the playback queue when it finishes or the player leaves it.</summary>
    public static void Restore()
    {
        plans = null;
        playing = null;
        started = finished = null;
        taskIndex = 0;
        taskLabel = "";
        TaskCount = 0;
    }

    /// <summary>
    /// Plays the playlist. <paramref name="steps"/> maps every run to its routine step and <paramref name="labels"/>
    /// gives the pause menu's task name (a routine passes its official scenario names); both are optional for the
    /// player's own playlists, where every run is its own task.
    /// </summary>
    public static void Play(Playlist p, Action<int>? runStarted = null, Action<int>? runFinished = null,
        IReadOnlyList<int>? steps = null, IReadOnlyList<string>? labels = null)
    {
        var list = new List<Plan>();
        for (int i = 0; i < p.Modes.Count; i++)
        {
            string k = p.Modes[i];
            if (ModeRegistry.Find(k) is not { } make) continue;
            string name;
            try { name = make().Name; } catch { name = k; }
            list.Add(new Plan
            {
                Make = make, Key = k, Index = i,
                Step = steps != null && i < steps.Count ? steps[i] : list.Count,
                Label = labels != null && i < labels.Count && labels[i].Length > 0 ? labels[i] : name,
            });
        }
        if (list.Count == 0) return;

        // The distinct steps, in playback order: the pause menu moves between them, not between individual repetitions.
        var order = new List<int>();
        foreach (var plan in list) if (!order.Contains(plan.Step)) order.Add(plan.Step);
        for (int s = 0; s < order.Count; s++)
        {
            var group = list.Where(x => x.Step == order[s]).ToList();
            for (int r = 0; r < group.Count; r++)
            {
                group[r].RunOfStep = r + 1;
                group[r].RunsInStep = group.Count;
            }
        }

        plans = list;
        playing = p;
        started = runStarted;
        finished = runFinished;
        TaskCount = order.Count;
        Main.I.ReturnToAgents = false;
        StartRun(0);
    }

    /// <summary>Jumps to the previous (-1) or next (+1) task of the current routine, starting its first run.</summary>
    public static void JumpTask(int direction)
    {
        if (plans is not { } list || TaskCount < 2) return;
        var order = new List<int>();
        foreach (var plan in list) if (!order.Contains(plan.Step)) order.Add(plan.Step);
        int target = Math.Clamp(taskIndex + direction, 0, order.Count - 1);
        if (target == taskIndex) return;
        int step = order[target];
        int index = list.FindIndex(x => x.Step == step);
        if (index < 0) return;
        if (Main.I.Dev) GD.Print($"[playlist] jump task {taskIndex + 1} → {target + 1} (run {index})");
        StartRun(index);
    }

    static void StartRun(int index)
    {
        if (plans is not { } list || playing is not { } p || index < 0 || index >= list.Count)
        {
            Restore();
            Main.I.ShowMenu();
            return;
        }
        var plan = list[index];
        taskIndex = 0;
        var seen = new List<int>();
        foreach (var x in list)
        {
            if (!seen.Contains(x.Step)) seen.Add(x.Step);
            if (x.Step == plan.Step) { taskIndex = seen.Count - 1; break; }
        }
        taskLabel = plan.Label;
        RunOfStep = plan.RunOfStep;
        RunsInStep = plan.RunsInStep;

        // A coach playlist sets the camera climb its plan asked for, step by step; the player's own playlists carry
        // no entry and leave the pause-menu setting alone.
        bool? plannedRecoil = p.RecoilAt(plan.Index);
        bool last = index == list.Count - 1;
        var session = new GameSession(plan.Make) { SessionRecoil = plannedRecoil };
        // Community routines: only the runs whose source says so get a boosted sensitivity. The applied value is
        // clamped by SensShift, and boosted runs stay out of the personal bests (they are practice, not scores).
        float sensFactor = p.SensAt(plan.Index);
        if (sensFactor > 1.001f)
        {
            session.SessionSens = SensShift.Applied(Main.I.Sens, Main.I.Settings.Dpi, sensFactor);
            session.KeepInStats = false;
        }
        float minutes = p.MinutesAt(plan.Index);
        // Dev-only: shorten every run of a routine so the whole playlist can be smoke-tested quickly.
        if (Main.I.Dev && float.TryParse(CmdLine.After("--routine-minutes"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var devMinutes) && devMinutes > 0)
            minutes = devMinutes;
        if (minutes > 0) session.DurationOverride = minutes * 60f;
        int runIndex = plan.Index;
        if (Main.I.Dev)
            GD.Print($"[playlist] run {runIndex} task {taskIndex + 1}/{TaskCount} {plan.Key} " +
                     $"sens={sensFactor:0.##}x min={minutes:0.#} " +
                     $"recoil={(plannedRecoil is { } rc ? rc ? "on" : "off" : "keep")}");
        started?.Invoke(runIndex);
        // Every run reports its finish, including the last one, which then keeps its normal results screen.
        session.OnFinished = _ =>
        {
            finished?.Invoke(runIndex);
            if (!last) Callable.From(() => StartRun(index + 1)).CallDeferred();
            else Restore();
        };
        Main.I.ShowScreen(session);
    }
}
