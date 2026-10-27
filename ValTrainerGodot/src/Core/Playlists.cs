using System.Text.Json;
using Godot;
using ValTrainer.Game;
using ValTrainer.Modes;

namespace ValTrainer.Core;

/// <summary>A named set of drills played back to back (the clips are drill keys, in order).</summary>
public sealed class Playlist
{
    public string Id = Guid.NewGuid().ToString("N")[..10];
    public string Name = "";
    public List<string> Modes = new();
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
        if (p.Modes.Remove(key)) added = false;
        else { p.Modes.Add(key); added = true; }
        Save();
        return added;
    }
}

/// <summary>Plays a playlist: every drill after the last one hands over to the next session; the final drill keeps its
/// normal results screen. Nothing is changed about scoring: each run is an ordinary GameSession.</summary>
public static class PlaylistRunner
{
    public static void Play(Playlist p)
    {
        var modes = p.Modes.Where(k => ModeRegistry.Find(k) != null).Select(k => ModeRegistry.Find(k)!).ToList();
        if (modes.Count == 0) return;
        Main.I.ReturnToAgents = false;
        int next = 0;
        void StartNext()
        {
            if (next >= modes.Count) { Main.I.ShowMenu(); return; }
            var make = modes[next++];
            bool last = next >= modes.Count;
            var session = new GameSession(make);
            if (!last)
                session.OnFinished = _ => Callable.From(StartNext).CallDeferred();
            Main.I.ShowScreen(session);
        }
        StartNext();
    }
}
