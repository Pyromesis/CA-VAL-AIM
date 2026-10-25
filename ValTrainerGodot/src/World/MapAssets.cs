using System.Text.Json;
using Godot;
using ValTrainer.Game;

namespace ValTrainer.World;

/// <summary>
/// Locates the optional real-map assets that live next to the executable (or in the source tree while
/// developing). These files are deliberately external: the extracted Riot geometry and textures are not
/// redistributed with the game, and a normal install without them still uses the traced blockouts.
/// </summary>
public static class MapAssets
{
    /// <summary>First existing file for a path relative to the asset root (e.g. "maps/ascent/collision.json").</summary>
    public static string? Find(string relative)
    {
        foreach (var root in Roots())
        {
            try
            {
                string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path)) return path;
            }
            catch { /* a bad root must never break map loading */ }
        }
        return null;
    }

    static IEnumerable<string> Roots()
    {
        var roots = new List<string>();
        void Add(string? root)
        {
            if (string.IsNullOrWhiteSpace(root)) return;
            try
            {
                string full = Path.GetFullPath(root);
                if (!roots.Contains(full, StringComparer.OrdinalIgnoreCase)) roots.Add(full);
            }
            catch { }
        }

        // Dev-only "--maps-dir <dir>": a folder holding maps/<key>/ (e.g. a freshly built map set) is searched first.
        if (Core.CmdLine.DevAfter("--maps-dir") is { Length: > 0 } dev) Add(dev);
        try { Add(ProjectSettings.GlobalizePath("res://")); } catch { }
        try { Add(ProjectSettings.GlobalizePath("res://assets")); } catch { }
        Add(OS.GetExecutablePath().GetBaseDir());
        Add(OS.GetExecutablePath().GetBaseDir() + "/assets");
        Add(AppContext.BaseDirectory);
        Add(AppContext.BaseDirectory + "/assets");
        Add(Directory.GetCurrentDirectory());
        Add(Directory.GetCurrentDirectory() + "/assets");
        return roots;
    }

    /// <summary>
    /// Reads the compact collision file generated offline from the real meshes:
    /// <c>{ "boxes": [[minX,minY,minZ,maxX,maxY,maxZ], ...] }</c> in metres.
    /// Returns null on any malformed/missing file so the caller can fall back to the blockout.
    /// </summary>
    public static Box[]? LoadCollision(string relative)
    {
        string? path = Find(relative);
        if (path == null) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("boxes", out var list) || list.ValueKind != JsonValueKind.Array)
                return null;
            var boxes = new List<Box>(list.GetArrayLength());
            foreach (var e in list.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() < 6) continue;
                float[] v = new float[6];
                int i = 0;
                foreach (var n in e.EnumerateArray())
                {
                    if (i == 6) break;
                    if (n.ValueKind != JsonValueKind.Number || !n.TryGetSingle(out v[i])) { i = -1; break; }
                    i++;
                }
                if (i != 6) continue;
                var min = new Vector3(v[0], v[1], v[2]);
                var max = new Vector3(v[3], v[4], v[5]);
                if (min.X > max.X || min.Y > max.Y || min.Z > max.Z) continue;
                boxes.Add(new Box(min, max));
            }
            return boxes.Count > 0 ? boxes.ToArray() : null;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[maps] could not read collision '{path}': {ex.Message}");
            return null;
        }
    }

    /// <summary>The "moves" of a <c>spawn_exclude.json</c>: authored spot (from), where SpawnSafety put it (at) and the
    /// clean replacement (to). Missing or malformed: an empty list.</summary>
    public static List<(Vector3 From, Vector3 At, Vector3 To)> LoadMoves(string relative)
    {
        var list = new List<(Vector3, Vector3, Vector3)>();
        string? path = Find(relative);
        if (path == null) return list;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("moves", out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
            static Vector3? V(JsonElement m, string k) =>
                m.TryGetProperty(k, out var e) && e.ValueKind == JsonValueKind.Array && e.GetArrayLength() >= 3
                && e[0].TryGetSingle(out float x) && e[1].TryGetSingle(out float y) && e[2].TryGetSingle(out float z)
                && float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z) ? new Vector3(x, y, z) : null;
            foreach (var m in arr.EnumerateArray())
                if (V(m, "from") is { } f && V(m, "at") is { } a && V(m, "to") is { } t) list.Add((f, a, t));
        }
        catch (Exception ex) { GD.PushWarning($"[maps] could not read spot moves '{path}': {ex.Message}"); }
        return list;
    }

    /// <summary>
    /// Reads a point list <c>{ "points": [[x,y,z], ...] }</c> (metres, map frame), e.g. <c>spawn_exclude.json</c> written by
    /// tools/mapgen/audit_spawns.py. Missing or malformed file: an empty array.
    /// </summary>
    public static Vector3[] LoadPoints(string relative)
    {
        string? path = Find(relative);
        if (path == null) return Array.Empty<Vector3>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("points", out var list) || list.ValueKind != JsonValueKind.Array)
                return Array.Empty<Vector3>();
            var pts = new List<Vector3>(list.GetArrayLength());
            foreach (var e in list.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() < 3) continue;
                if (e[0].TryGetSingle(out float x) && e[1].TryGetSingle(out float y) && e[2].TryGetSingle(out float z)
                    && float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z))
                    pts.Add(new Vector3(x, y, z));
            }
            return pts.ToArray();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[maps] could not read points '{path}': {ex.Message}");
            return Array.Empty<Vector3>();
        }
    }
}
