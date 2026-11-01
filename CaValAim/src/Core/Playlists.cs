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
            p.Modes.RemoveAll(string.IsNullOrEmpty);
            p.Recoil ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (p.Recoil.Comparer != StringComparer.OrdinalIgnoreCase)
                p.Recoil = new Dictionary<string, bool>(p.Recoil, StringComparer.OrdinalIgnoreCase);
            p.Sens ??= new List<float>();
            p.Minutes ??= new List<float>();
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
        p.Sens.Clear();
        p.Minutes.Clear();
        foreach (var run in entries)
        {
            if (string.IsNullOrWhiteSpace(run.Key)) continue;
            p.Modes.Add(run.Key);
            p.Recoil[run.Key] = run.Recoil;
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
            if (at < p.Sens.Count) p.Sens.RemoveAt(at);
            if (at < p.Minutes.Count) p.Minutes.RemoveAt(at);
            p.Recoil.Remove(key);
            added = false;
        }
        else
        {
            p.Modes.Add(key);
            p.Sens.Add(1f);
            p.Minutes.Add(0f);
            added = true;
        }
        Save();
        return added;
    }
}

/// <summary>Plays a playlist: every drill after the last one hands over to the next session; the final drill keeps its
/// normal results screen. Nothing is changed about scoring: each run is an ordinary GameSession.</summary>
public static class PlaylistRunner
{
    /// <summary>The player's camera-climb setting before a coach playlist changed it (null = not changed).</summary>
    static bool? savedRecoil;

    /// <summary>Puts the recoil setting back after a playlist (also called when leaving to the menu mid-playlist).</summary>
    public static void Restore()
    {
        if (savedRecoil is not { } on) return;
        savedRecoil = null;
        try { Main.I.Settings.RecoilEnabled = on; } catch { /* shutting down */ }
    }

    public static void Play(Playlist p, Action<int>? runStarted = null, Action<int>? runFinished = null)
    {
        var modes = new List<(Func<TrainingMode> Make, string Key, int Index)>();
        for (int i = 0; i < p.Modes.Count; i++)
        {
            string k = p.Modes[i];
            if (ModeRegistry.Find(k) is { } make) modes.Add((make, k, i));
        }
        if (modes.Count == 0) return;
        Main.I.ReturnToAgents = false;
        int next = 0;
        void StartNext()
        {
            if (next >= modes.Count) { Restore(); Main.I.ShowMenu(); return; }
            var (make, key, index) = modes[next++];
            // A coach playlist sets the camera climb its plan asked for, step by step; the player's own playlists carry
            // no entry and leave the pause-menu setting alone.
            if (p.Recoil.TryGetValue(key, out bool recoil))
            {
                savedRecoil ??= Main.I.Settings.RecoilEnabled;
                Main.I.Settings.RecoilEnabled = recoil;
            }
            bool last = next >= modes.Count;
            var session = new GameSession(make);
            // Community routines: only the runs whose source says so get a boosted sensitivity. The applied value is
            // clamped by SensShift, and boosted runs stay out of the personal bests (they are practice, not scores).
            float sensFactor = p.SensAt(index);
            if (sensFactor > 1.001f)
            {
                session.SessionSens = SensShift.Applied(Main.I.Sens, Main.I.Settings.Dpi, sensFactor);
                session.KeepInStats = false;
            }
            float minutes = p.MinutesAt(index);
            // Dev-only: shorten every run of a routine so the whole playlist can be smoke-tested quickly.
            if (Main.I.Dev && float.TryParse(CmdLine.After("--routine-minutes"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var devMinutes) && devMinutes > 0)
                minutes = devMinutes;
            if (minutes > 0) session.DurationOverride = minutes * 60f;
            int runIndex = index;
            if (Main.I.Dev)
                GD.Print($"[playlist] run {runIndex} {key} sens={sensFactor:0.##}x min={minutes:0.#} " +
                         $"recoil={(p.Recoil.TryGetValue(key, out bool rc) ? rc ? "on" : "off" : "keep")}");
            runStarted?.Invoke(runIndex);
            // Every run reports its finish, including the last one, which then keeps its normal results screen. The
            // recoil setting is restored either way; Main.ShowMenu covers leaving mid-routine.
            session.OnFinished = _ =>
            {
                runFinished?.Invoke(runIndex);
                if (!last) Callable.From(StartNext).CallDeferred();
                else Restore();
            };
            Main.I.ShowScreen(session);
        }
        StartNext();
    }
}
