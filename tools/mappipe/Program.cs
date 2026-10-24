using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Actor;
using CUE4Parse.UE4.Assets.Exports.Component;
using CUE4Parse.UE4.Assets.Exports.Component.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Options;
using CUE4Parse_Conversion.Textures;
using CUE4Parse.UE4.Assets.Exports.Texture;

namespace MapPipe;

/// <summary>
/// Dumps one Valorant map from the game's IoStore packages: every static mesh instance of every
/// requested level (mesh asset + world transform in Unreal space) and one GLB per unique mesh.
/// The GLB composition, collision and manifest are done by <c>compose_map.py</c>.
/// </summary>
public static class Program
{
    const string DefaultGame = @"C:\Riot Games\VALORANT\live\ShooterGame\Content\Paks";

    static DefaultFileProvider? provider;

    public static int Main(string[] args)
    {
        var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i + 1 < args.Length; i += 2)
            opts[args[i].TrimStart('-')] = args[i + 1];
        string Get(string k, string d) => opts.TryGetValue(k, out var v) ? v : d;

        var map = Get("map", "");
        var outDir = Get("out", "");
        var usmap = Get("usmap", "");
        // The game's pak AES key: --key or the VALORANT_AES_KEY environment variable (never stored in the repository).
        var aesKey = Get("key", Environment.GetEnvironmentVariable("VALORANT_AES_KEY") ?? "");
        if (map.Length == 0 || outDir.Length == 0 || !File.Exists(usmap) || aesKey.Length == 0)
        {
            Console.Error.WriteLine("usage: MapPipe --map <Token> --out <dir> --usmap <file> --key <0xAES> [--game <paks>] [--levels a,b] [--list 1] [--actors <dir>] [--max-instances N] [--no-mesh-export 1]");
            return 2;
        }

        var game = Get("game", DefaultGame);
        var maxInstances = int.TryParse(Get("max-instances", "0"), out var mi) ? mi : 0;
        var exportMeshes = Get("no-mesh-export", "0") != "1";

        provider = new DefaultFileProvider(game, SearchOption.TopDirectoryOnly,
            new VersionContainer(EGame.GAME_Valorant), StringComparer.OrdinalIgnoreCase);
        provider.MappingsContainer = new FileUsmapTypeMappingsProvider(usmap);
        provider.Initialize();
        provider.SubmitKeys(new Dictionary<FGuid, FAesKey> { [new FGuid()] = new FAesKey(aesKey) });

        var prefix = $"ShooterGame/Content/Maps/{map}/{map}";
        if (Get("list", "0") == "1")
        {
            foreach (var key in provider.Files.Keys
                         .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && k.EndsWith(".umap", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                Console.WriteLine(Path.GetFileNameWithoutExtension(key));
            return 0;
        }
        var levels = Get("levels", "");
        var levelFiles = levels.Length > 0
            ? levels.Split(',').Select(l => l.Trim()).Where(l => l.Length > 0)
                .Select(l => prefix + (l.StartsWith('_') ? l : "_" + l) + ".umap").ToArray()
            : provider.Files.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && k.EndsWith(".umap", StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();

        Directory.CreateDirectory(Path.Combine(outDir, "levels"));
        var meshDir = Path.Combine(outDir, "meshes");
        Directory.CreateDirectory(meshDir);

        var wanted = new Dictionary<string, UStaticMesh>(StringComparer.OrdinalIgnoreCase);
        var overrides = new Dictionary<string, UMaterialInterface>(StringComparer.OrdinalIgnoreCase);
        var levelList = new List<string>();
        int total = 0;
        var dumpActors = Get("actors", "");

        foreach (var file in levelFiles)
        {
            if (!provider.Files.ContainsKey(file)) { Console.Error.WriteLine("missing " + file); continue; }
            var name = Path.GetFileNameWithoutExtension(file);
            if (dumpActors.Length > 0) { DumpActors(file, name, dumpActors); continue; }
            var instances = new List<(string Mesh, double[] M, string[]? Mats)>();
            try
            {
                var pkg = provider.LoadPackage(file);
                foreach (var comp in pkg.GetExports().OfType<UStaticMeshComponent>())
                {
                    var mesh = comp.GetLoadedStaticMesh();
                    if (mesh?.RenderData?.LODs is not { Length: > 0 }) continue;
                    var meshKey = MeshKey(mesh);
                    // Per-component material overrides: Riot dresses shared meshes per placement (Bind's B site floor is
                    // a plain plane whose sand is an override), so the slot list travels with each instance.
                    string[]? mats = null;
                    if (comp.OverrideMaterials is { Length: > 0 } ovs)
                    {
                        mats = new string[ovs.Length];
                        for (int si = 0; si < ovs.Length; si++)
                        {
                            var omi = ovs[si] is { IsNull: false } ov ? ov.Load<UMaterialInterface>() : null;
                            mats[si] = omi?.Name ?? "";
                            if (omi != null) overrides[omi.Name] = omi;
                        }
                        if (mats.All(m => m.Length == 0)) mats = null;
                        if (Get("mats", "0") == "1" && mats != null)
                            Console.WriteLine($"MAT {name} {meshKey} -> {string.Join(" | ", mats.Select(m => m.Length == 0 ? "-" : m))}");
                    }
                    var world = ToMatrix(comp.GetComponentTransform());
                    if (comp is UInstancedStaticMeshComponent ism)
                    {
                        foreach (var inst in ism.GetInstances())
                        {
                            var m = Mul(world, ToMatrix(inst.TransformData));
                            instances.Add((meshKey, m, mats));
                            wanted[meshKey] = mesh;
                        }
                    }
                    else
                    {
                        instances.Add((meshKey, world, mats));
                        wanted[meshKey] = mesh;
                    }
                    if (maxInstances > 0 && instances.Count >= maxInstances) break;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"{name}: {e.GetType().Name}: {e.Message}");
                continue;
            }

            levelList.Add(name);
            total += instances.Count;
            Console.WriteLine($"{name,-40} instances={instances.Count,6} meshes={instances.Select(i => i.Mesh).Distinct().Count(),5}");
            WriteLevel(Path.Combine(outDir, "levels", name + ".json"), name, instances);
        }

        Console.WriteLine($"total instances={total} unique meshes={wanted.Count}");
        var meshFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (exportMeshes)
        {
            var options = new ExportOptions(meshFormat: EMeshFormat.Gltf2, exportMaterials: true,
                textureFormat: ETextureFormat.Png, textureQuality: 90, exportMorphTargets: false);
            var session = new ExportSession(null);
            foreach (var m in wanted.Values.Distinct()) session.Add(m);
            Console.WriteLine($"exporting {session.TotalQueued} meshes to {meshDir} ...");
            var task = session.RunAsync(meshDir, options, null, CancellationToken.None);
            task.Wait();
            var results = task.Result;
            Console.WriteLine("mesh export done: " + results.Count + " results");

            var byObject = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int failed = 0;
            foreach (var r in results)
            {
                if (!r.Success || r.DiskFilePaths == null || r.DiskFilePaths.Count == 0)
                {
                    failed++;
                    Console.Error.WriteLine("export failed: " + r.ObjectPath + " :: " + r.Error?.Message);
                    continue;
                }
                var glb = r.DiskFilePaths.FirstOrDefault(p => p.EndsWith(".glb", StringComparison.OrdinalIgnoreCase));
                if (glb == null) continue;
                var obj = r.ObjectPath;
                int dot = obj.LastIndexOf('.');
                if (dot > 0) obj = obj[..dot];
                byObject[obj] = Path.GetRelativePath(meshDir, glb).Replace('\\', '/');
            }
            Console.WriteLine($"glb paths: {byObject.Count}, failed: {failed}");

            foreach (var key in wanted.Keys)
            {
                var owner = wanted[key].Owner?.Name;
                if (owner == null) continue;
                var p = owner.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase) ? owner[6..] : owner.TrimStart('/');
                var objectPath = "ShooterGame/Content/" + p;
                if (byObject.TryGetValue(objectPath, out var rel)) meshFiles[key] = rel;
                else Console.Error.WriteLine("no glb for " + key + " (" + objectPath + ")");
            }
        }

        // The colour of every material in the map (override and the meshes' own), measured from the game's textures and
        // tints (MaterialColor), for compose_map.py; override materials are not exported as files at all.
        // "slots" names each mesh's own material slots in order.
        foreach (var mesh in wanted.Values)
            foreach (var sm in mesh.StaticMaterials ?? Array.Empty<FStaticMaterial>())
                if (sm.MaterialInterface?.Load<UMaterialInterface>() is { } own) overrides.TryAdd(own.Name, own);
        var materialColors = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (mname, omat) in overrides)
        {
            try { materialColors[mname] = MaterialColor(omat); }
            catch (Exception e) { Console.Error.WriteLine($"material {mname}: {e.Message}"); }
        }
        Console.WriteLine($"materials: {overrides.Count}, coloured: {materialColors.Count}");
        File.WriteAllText(Path.Combine(outDir, "map.json"), JsonSerializer.Serialize(new
        {
            slots = wanted.ToDictionary(kv => kv.Key,
                kv => kv.Value.StaticMaterials?.Select(sm => sm.MaterialInterface?.Name ?? "").ToArray() ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase),
            materialColors,
            token = map,
            levels = levelList,
            meshes = wanted.Keys.OrderBy(k => k).ToArray(),
            meshNames = wanted.ToDictionary(kv => kv.Key, kv => kv.Value.Name, StringComparer.OrdinalIgnoreCase),
            meshFiles
        }, new JsonSerializerOptions { WriteIndented = false }));
        return 0;
    }

    /// <summary>Prints every export of a level: class, path, name and world transform when it has one.</summary>
    static void DumpActors(string file, string name, string outDir)
    {
        Directory.CreateDirectory(outDir);
        using var f = File.Create(Path.Combine(outDir, name + ".txt"));
        using var w = new StreamWriter(f);
        var pkg = provider!.LoadPackage(file);
        var exports = pkg.GetExports().ToArray();
        w.WriteLine($"# {name}: {exports.Length} exports");
        foreach (var g in exports.GroupBy(e => e.GetType().Name).OrderByDescending(g => g.Count()))
            w.WriteLine($"class {g.Key,-40} {g.Count()}");

        var level = exports.OfType<ULevel>().FirstOrDefault();
        if (level != null)
        {
            w.WriteLine($"# level.Actors: {level.Actors.Length}");
            foreach (var idx in level.Actors)
            {
                UObject? a;
                try { a = idx.Load(); } catch (Exception e) { w.WriteLine("load fail: " + e.Message); continue; }
                if (a is not AActor actor) continue;
                string extra = "";
                var root = actor.GetOrDefault<USceneComponent>("RootComponent");
                if (root != null)
                {
                    var t = root.GetComponentTransform();
                    var l = t.Translation;
                    var fwd = t.Rotation.RotateVector(new FVector(1, 0, 0));
                    extra = $" loc=({l.X:F1},{l.Y:F1},{l.Z:F1}) fwd=({fwd.X:F2},{fwd.Y:F2},{fwd.Z:F2})";
                }
                string props = "";
                if (actor.Properties != null)
                {
                    foreach (var p in actor.Properties)
                    {
                        var n = p.Name.Text;
                        if (!n.Contains("Name", StringComparison.OrdinalIgnoreCase) &&
                            !n.Contains("Text", StringComparison.OrdinalIgnoreCase) &&
                            !n.Contains("Callout", StringComparison.OrdinalIgnoreCase) &&
                            !n.Contains("Label", StringComparison.OrdinalIgnoreCase)) continue;
                        object? v = null; try { v = p.Tag?.GenericValue; } catch { }
                        props += $" {n}({p.PropertyType.Text})={v}";
                    }
                }
                w.WriteLine($"{"ACTOR",-8} {actor.GetType().Name,-32} {actor.Name,-40} label='{actor.ActorLabel}'{extra}{props}");
            }
        }
        w.Flush();
        Console.WriteLine($"{name}: {exports.Length} exports -> {Path.Combine(outDir, name + ".txt")}");
    }

    /// <summary>sRGB mean colour of a material (its diffuse texture decoded at ≤64 px, times DiffuseColor).</summary>
    static object MaterialColor(UMaterialInterface mi)
    {
        var p = new CMaterialParams2();
        mi.GetParams(p, EMaterialDepth.AllLayers);
        if (Environment.GetEnvironmentVariable("MAPPIPE_MAT_DEBUG") is { Length: > 0 } dbg && mi.Name.Contains(dbg, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var (k, v) in p.Textures) Console.WriteLine($"  TEX {mi.Name} {k} = {v}");
            foreach (var (k, v) in p.Colors) Console.WriteLine($"  COL {mi.Name} {k} = {v}");
        }
        // Valorant's environment materials are either one diffuse texture × DiffuseColor, or a vertex-painted blend of
        // layers ("Diffuse A" × "Layer A Tint", "Diffuse B" × "Layer B Tint", …; Bind's B site sand is dirt + a tinted
        // white layer). The paint is per vertex and not read here, so the layers are averaged evenly. The generic
        // texture slots (PM_Diffuse, Albedo_DF…) of layered materials are previews or engine placeholders.
        var layers = new List<double[]>();
        foreach (var L in new[] { "A", "B", "C", "D" })
        {
            var col = Mean(Tex($"Diffuse {L}") ?? Tex($"Diffuse {L} Low"));
            var tint = Col($"Layer {L} Tint") ?? Col($"Layer {L} Color");
            if (col == null && tint == null) continue;
            col ??= new[] { 1.0, 1.0, 1.0 };
            if (tint != null) col = new[] { col[0] * tint[0], col[1] * tint[1], col[2] * tint[2] };
            layers.Add(col);
        }
        double[] rgb;
        bool textured;
        if (layers.Count > 0)
        {
            rgb = new[] { layers.Average(l => l[0]), layers.Average(l => l[1]), layers.Average(l => l[2]) };
            textured = true;
        }
        else
        {
            var mean = Mean(Tex("PM_Diffuse") ?? Tex("Diffuse") ?? Tex("DiffuseTexture") ?? Tex("BaseColor") ?? Tex("Base Color") ?? Tex("Albedo"));
            textured = mean != null;
            rgb = mean ?? new[] { 0.5, 0.5, 0.5 };
        }
        if ((Col("DiffuseColor") ?? Col("Diffuse Color")) is { } dc)
            rgb = new[] { rgb[0] * dc[0], rgb[1] * dc[1], rgb[2] * dc[2] };
        return new { color = new[] { Srgb(rgb[0]), Srgb(rgb[1]), Srgb(rgb[2]) }, textured, translucent = p.IsTranslucent };

        UTexture2D? Tex(string key)
        {
            if (!p.Textures.ContainsKey(key) || !p.TryGetTexture2d(out var t, key) || t is not UTexture2D t2) return null;
            // Engine placeholders (EnvBaseMat/Albedo_DF, VFX masks) are flat white: they carry no colour of their own.
            var path = t2.GetPathName();
            return path.Contains("/EnvBaseMat/", StringComparison.OrdinalIgnoreCase) ? null : t2;
        }

        double[]? Col(string key) => p.Colors.TryGetValue(key, out var c)
            ? new[] { Math.Min(1.0, (double)c.R), Math.Min(1.0, (double)c.G), Math.Min(1.0, (double)c.B) } : null;

        static double[]? Mean(UTexture2D? t)
        {
            if (t == null || TextureDecoder.Decode(t, 64, ETexturePlatform.DesktopMobile) is not { } ct) return null;
            using var bmp = TextureEncoder.ToSkBitmap(ct);
            double sr = 0, sg = 0, sb = 0, sw = 0;
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    double wgt = c.Alpha / 255.0;
                    if (wgt <= 0) continue;
                    sr += Lin(c.Red) * wgt; sg += Lin(c.Green) * wgt; sb += Lin(c.Blue) * wgt; sw += wgt;
                }
            return sw > 0 ? new[] { sr / sw, sg / sw, sb / sw } : null;
        }

        static double Lin(byte v) { double c = v / 255.0; return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        static double Srgb(double c) { c = Math.Clamp(c, 0, 1); return Math.Round(c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055, 4); }
    }

    static void WriteLevel(string path, string name, List<(string Mesh, double[] M, string[]? Mats)> instances)
    {
        using var f = File.Create(path);
        using var w = new Utf8JsonWriter(f);
        w.WriteStartObject();
        w.WriteString("level", name);
        w.WriteStartArray("instances");
        foreach (var (mesh, m, mats) in instances)
        {
            w.WriteStartObject();
            w.WriteString("mesh", mesh);
            w.WriteStartArray("m");
            foreach (var v in m) w.WriteNumberValue(Math.Round(v, 4));
            w.WriteEndArray();
            if (mats != null)
            {
                w.WriteStartArray("mats");
                foreach (var n in mats) w.WriteStringValue(n);
                w.WriteEndArray();
            }
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    static string MeshKey(UStaticMesh mesh)
    {
        var n = mesh.Owner?.Name ?? mesh.Name;
        n = n.Replace("/Game/", "").Replace('\\', '/');
        var bad = Path.GetInvalidFileNameChars().Concat(new[] { '/', ':', '.' }).ToHashSet();
        return new string(n.Select(c => bad.Contains(c) ? '_' : c).ToArray());
    }

    /// <summary>FTransform -> row-major 4x4 in Unreal space (centimetres).</summary>
    static double[] ToMatrix(FTransform t)
    {
        var q = t.Rotation;
        double x = q.X, y = q.Y, z = q.Z, w = q.W;
        double xx = x * x, yy = y * y, zz = z * z, xy = x * y, xz = x * z, yz = y * z, wx = w * x, wy = w * y, wz = w * z;
        double sx = t.Scale3D.X, sy = t.Scale3D.Y, sz = t.Scale3D.Z;
        return new[]
        {
            (1 - 2 * (yy + zz)) * sx, 2 * (xy - wz) * sy, 2 * (xz + wy) * sz, t.Translation.X,
            2 * (xy + wz) * sx, (1 - 2 * (xx + zz)) * sy, 2 * (yz - wx) * sz, t.Translation.Y,
            2 * (xz - wy) * sx, 2 * (yz + wx) * sy, (1 - 2 * (xx + yy)) * sz, t.Translation.Z,
            0, 0, 0, 1
        };
    }

    static double[] Mul(double[] a, double[] b)
    {
        var r = new double[16];
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                r[i * 4 + j] = a[i * 4] * b[j] + a[i * 4 + 1] * b[4 + j] + a[i * 4 + 2] * b[8 + j] + a[i * 4 + 3] * b[12 + j];
        return r;
    }
}
