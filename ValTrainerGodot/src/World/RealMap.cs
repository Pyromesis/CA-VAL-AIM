using System.Diagnostics;
using System.Text.Json;
using Godot;

namespace ValTrainer.World;

/// <summary>
/// Loads the textured art GLBs of an extracted map at runtime. They are extracted from the installed
/// game with CUE4Parse, converted through Blender and kept outside the distribution.
///
/// Blender's glTF export is Y-up with <c>(x, z, -y)</c> world axes. Ascent uses its original hard-coded
/// frame; every newer map carries a <c>manifest.json</c> with the same proper 90° Y rotation and the
/// translation that puts its A Main callout at the game origin. The collision file is already in that
/// final frame.
/// </summary>
public static class RealMap
{
    public static readonly string[] Parts =
    {
        "Ascent_Art_A",
        "Ascent_Art_AtkPathA",
        "Ascent_Art_Atk",
        "Ascent_Art_APathMid",
        "Ascent_Art_DefPathA",
        "Ascent_Art_Def",
    };

    static readonly Vector3 RootPosition = new(50f, -3.8f, 61f);

    sealed record Manifest(Vector3 Origin, string[] Levels);

    static Manifest? ReadManifest(string folder)
    {
        string? path = MapAssets.Find(folder + "/manifest.json");
        if (path == null) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var r = doc.RootElement;
            if (!r.TryGetProperty("origin_x", out var x) ||
                !r.TryGetProperty("origin_z", out var z) ||
                !r.TryGetProperty("levels", out var levels) ||
                levels.ValueKind != JsonValueKind.Array)
                return null;
            float ox = x.GetSingle();
            float oy = r.TryGetProperty("origin_y", out var y) ? y.GetSingle() : 0f;
            float oz = z.GetSingle();
            var parts = new List<string>();
            foreach (var e in levels.EnumerateArray())
                if (e.ValueKind == JsonValueKind.String && e.GetString() is { Length: > 0 } s)
                    parts.Add(s);
            return parts.Count == 0 ? null : new Manifest(new Vector3(ox, oy, oz), parts.ToArray());
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[maps] could not read real-map manifest '{path}': {ex.Message}");
            return null;
        }
    }

    /// <summary>Adds the real geometry under <paramref name="root"/>. False = no GLB could be loaded.</summary>
    public static bool TryBuild(Node3D root, string folder)
    {
        Manifest? manifest = ReadManifest(folder);
        string[] parts = manifest?.Levels ?? Parts;
        var art = new Node3D
        {
            Name = "RealMap",
            Position = manifest?.Origin ?? RootPosition,
            RotationDegrees = new Vector3(0, 90f, 0),
        };

        int loaded = 0;
        long bytes = 0;
        var clock = Stopwatch.StartNew();
        foreach (string part in parts)
        {
            string? path = MapAssets.Find($"{folder}/{part}.glb");
            if (path == null)
            {
                GD.PushWarning($"[maps] real-map part missing: {folder}/{part}.glb");
                continue;
            }

            try
            {
                var doc = new GltfDocument();
                var state = new GltfState();
                Error err = doc.AppendFromFile(path, state);
                if (err != Error.Ok)
                {
                    GD.PushWarning($"[maps] glTF read failed ({err}): {path}");
                    continue;
                }

                Node scene = doc.GenerateScene(state);
                if (scene == null)
                {
                    GD.PushWarning($"[maps] glTF scene empty: {path}");
                    continue;
                }
                scene.Name = part;
                art.AddChild(scene);
                loaded++;
                bytes += new FileInfo(path).Length;

                // Unreal content includes two-sided decals and thin props; keeping culling disabled is
                // cheap enough here and avoids holes on the extracted exterior shell.
                foreach (Node n in Walk(scene))
                    if (n is MeshInstance3D mi) DoubleSided(mi);
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[maps] glTF load failed: {path}: {ex.Message}");
            }
        }

        if (loaded == 0)
        {
            art.QueueFree();
            return false;
        }

        root.AddChild(art);
        GD.Print($"[maps] real {folder} loaded: {loaded}/{parts.Length} GLBs, {bytes / (1024 * 1024)} MB, {clock.ElapsedMilliseconds} ms");
        return true;
    }

    static IEnumerable<Node> Walk(Node n)
    {
        yield return n;
        foreach (Node child in n.GetChildren())
            foreach (Node descendant in Walk(child))
                yield return descendant;
    }

    static void DoubleSided(MeshInstance3D mi)
    {
        Mesh? mesh = mi.Mesh;
        if (mesh == null) return;
        int surfaces = mesh.GetSurfaceCount();
        for (int i = 0; i < surfaces; i++)
        {
            Material? mat = mi.GetSurfaceOverrideMaterial(i) ?? mesh.SurfaceGetMaterial(i);
            if (mat is BaseMaterial3D bm && bm.CullMode != BaseMaterial3D.CullModeEnum.Disabled)
                bm.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        }
    }
}
