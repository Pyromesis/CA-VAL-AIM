using System.Diagnostics;
using System.Text.Json;
using Godot;

namespace ValTrainer.World;

/// <summary>
/// Loads the art GLBs of an extracted map at runtime. They are extracted from the installed game with
/// CUE4Parse, converted through Blender and kept outside the distribution.
///
/// Blender's glTF export is Y-up with <c>(x, z, -y)</c> world axes. Ascent uses its original hard-coded
/// frame; every newer map carries a <c>manifest.json</c> with the same proper 90° Y rotation and the
/// translation that puts its A Main callout at the game origin. The collision file is already in that
/// final frame.
///
/// The geometry is loaded through <see cref="GltfStrip"/>, which drops every texture and collapses the
/// Unreal material set to flat colours: an aim trainer needs the map shape, not its art detail.
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

    static readonly Dictionary<string, bool> artCache = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, bool> completeCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True when the external folder holds art GLBs. The menu uses this to label a map REAL ART.
    /// </summary>
    public static bool HasArt(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return false;
        if (artCache.TryGetValue(folder, out bool known)) return known;
        if (!TryParts(folder, out string[] parts, out _)) return false;
        bool has = false;
        foreach (string part in parts)
        {
            if (MapAssets.Find($"{folder}/{part}.glb") == null) continue;
            has = true;
            break;
        }
        artCache[folder] = has;
        return has;
    }

    /// <summary>
    /// True when every level named by the manifest exists and contains geometry. A partial extraction
    /// (Corrode's empty AtkPathMid, a missing Ascent manifest) is flagged so the menu can say so instead
    /// of pretending the map is complete.
    /// </summary>
    public static bool IsComplete(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return false;
        if (completeCache.TryGetValue(folder, out bool known)) return known;
        if (!TryParts(folder, out string[] parts, out _)) return false;
        bool complete = false;
        foreach (string part in parts)
        {
            if (IsBackdrop(part)) continue; // painted scenery, not part of the playable map
            string? path = MapAssets.Find($"{folder}/{part}.glb");
            if (path == null || new FileInfo(path).Length < 4096)
            {
                complete = false;
                break;
            }
            complete = true;
        }
        completeCache[folder] = complete;
        return complete;
    }

    static bool TryParts(string folder, out string[] parts, out Manifest? manifest)
    {
        manifest = ReadManifest(folder);
        parts = manifest?.Levels ??
                (folder.EndsWith("ascent", StringComparison.OrdinalIgnoreCase) ? Parts : Array.Empty<string>());
        return parts.Length > 0;
    }

    /// <summary>True for a painted backdrop level that must never be rendered inside the map.</summary>
    static bool IsBackdrop(string level) =>
        level.Contains("vista", StringComparison.OrdinalIgnoreCase) ||
        level.Contains("skybox", StringComparison.OrdinalIgnoreCase) ||
        level.Contains("backdrop", StringComparison.OrdinalIgnoreCase);

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
            var list = new List<string>();
            foreach (var e in levels.EnumerateArray())
                if (e.ValueKind == JsonValueKind.String && e.GetString() is { Length: > 0 } s)
                    list.Add(s);
            return list.Count == 0 ? null : new Manifest(new Vector3(ox, oy, oz), list.ToArray());
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
        if (!TryParts(folder, out string[] parts, out Manifest? manifest)) return false;
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
            // Vista/Skybox/Backdrop levels are painted scenery meant to be seen from outside the
            // playable area. Bind's Duality_Art_Vista contains dunes that cut through Hookah and
            // B site (a sand surface 3 m over the site), which hides the real floor; the Godot
            // sky already provides the horizon. The collision generator skips these levels too.
            if (IsBackdrop(part))
            {
                GD.Print($"[maps] skip backdrop level: {part}");
                continue;
            }

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
                byte[]? stripped = GltfStrip.Prepare(path);
                Error err = stripped != null
                    ? doc.AppendFromBuffer(stripped, Path.GetDirectoryName(path) + "/", state)
                    : doc.AppendFromFile(path, state);
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

                foreach (Node n in Walk(scene))
                    if (n is MeshInstance3D mi) FixMaterials(mi);
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

    /// <summary>
    /// Every surface in a stripped GLB already carries one of the shared palette materials. Collision
    /// hulls and texture-only decals are removed outright; anything unexpected (a GLB that failed to
    /// strip) keeps a flat tint and loses its textures here.
    /// </summary>
    static void FixMaterials(MeshInstance3D mi)
    {
        if (mi.Mesh is not ArrayMesh mesh) return;
        var remove = new List<int>();
        int surfaces = mesh.GetSurfaceCount();
        for (int i = 0; i < surfaces; i++)
        {
            Material? mat = mi.GetSurfaceOverrideMaterial(i) ?? mesh.SurfaceGetMaterial(i);
            string name = mat?.ResourceName ?? "";
            if (name == GltfStrip.CollisionName || name == GltfStrip.SkipName)
            {
                remove.Add(i);
                continue;
            }
            if (GltfStrip.Material(name) is { } palette)
            {
                mi.SetSurfaceOverrideMaterial(i, palette);
                continue;
            }
            if (mat is not BaseMaterial3D bm) continue;
            // Fallback when the GLB could not be rewritten: strip its textures and clamp Unreal's
            // metallic=1/roughness=1 (which renders black on the low preset).
            bm.VertexColorUseAsAlbedo = false;
            bm.AlbedoTexture = null;
            bm.NormalEnabled = false;
            bm.OrmTexture = null;
            bm.AlbedoColor = new Color(0.62f, 0.60f, 0.56f);
            if (bm.Metallic > 0.15f) bm.Metallic = 0.15f;
            if (bm.Roughness < 0.75f) bm.Roughness = 0.75f;
        }
        for (int k = remove.Count - 1; k >= 0; k--) mesh.SurfaceRemove(remove[k]);
    }
}
