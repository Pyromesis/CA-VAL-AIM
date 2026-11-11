using System.Text.Json;

namespace CaValAim.Core;

public enum FavoriteKind { Drill, Routine, Playlist }
public sealed record Favorite(FavoriteKind Kind, string Key);

/// <summary>Stable identities only: names and playable settings always come from their original library.</summary>
public static class Favorites
{
    public static event Action? Changed;
    static List<Favorite>? items;
    static bool readFailed;
    static string FileName => Path.Combine(Paths.DataDir, "favorites.json");
    public static IReadOnlyList<Favorite> All
    {
        get
        {
            if (items != null) return items;
            try { items = File.Exists(FileName) ? JsonSerializer.Deserialize<List<Favorite>>(File.ReadAllText(FileName)) : new(); }
            catch (Exception e) when (Paths.IsCorruptData(e)) { Paths.KeepCorrupt(FileName, e); }
            catch (Exception e) { readFailed = true; Log.Error($"Favorites could not be loaded: {e.Message}"); }
            items ??= new();
            items = items.Where(f => f != null && Enum.IsDefined(f.Kind) && !string.IsNullOrWhiteSpace(f.Key)).Distinct().ToList();
            return items;
        }
    }
    public static bool Has(FavoriteKind kind, string key) => All.Contains(new Favorite(kind, key));
    public static void Toggle(FavoriteKind kind, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        _ = All;
        if (readFailed) return;
        var item = new Favorite(kind, key);
        bool removed = items!.Remove(item);
        if (!removed) items.Add(item);
        if (!AppSettings.ReadOnly && !Paths.TryWriteAllText(FileName, JsonSerializer.Serialize(items)))
        {
            if (removed) items.Add(item); else items.Remove(item);
            Log.Error("Favorites could not be saved.");
        }
        Changed?.Invoke();
    }
}
