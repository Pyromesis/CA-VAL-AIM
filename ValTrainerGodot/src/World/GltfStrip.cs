using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace ValTrainer.World;

/// <summary>
/// Rewrites a real-map GLB in memory right before Godot imports it. The extracted Valorant levels carry
/// thousands of Unreal materials and a PNG albedo/normal/ARM set per material (Bind alone is 540 MB and
/// 2252 images). An aim trainer only needs the shape, so this pass:
///
/// <list type="bullet">
/// <item>replaces every material with a flat-colour material: the colour baked into the material's
/// <c>extras.vt_color</c> by <c>tools/mapgen/flatten_glb.py</c> (the mean of its real albedo texture), or one of a
/// handful of palette colours guessed from the material name for GLBs without it;</item>
/// <item>removes the image/texture/sampler tables, so Godot never decodes or uploads a map texture;</item>
/// <item>drops UVs, tangents and vertex colours, which no palette material uses.</item>
/// </list>
///
/// The mesh data itself is left untouched (the binary chunk is copied verbatim); only the JSON chunk is
/// rebuilt. On any parse problem the caller falls back to the original file.
/// </summary>
static class GltfStrip
{
    public const string SkipName = "VT_Skip";
    public const string CollisionName = "EnvCollision_MAT";

    enum P
    {
        Floor, Wall, Stone, Wood, Metal, Foliage, Glass, Fence, Light, Accent, Dark, Skip, Collision,
    }

    sealed record Entry(string Name, Color Color, float Roughness, float Alpha = 1f, bool TwoSided = false, float Emission = 0f);

    // Indexed by P. Keep the order in sync with the enum. Every entry is opaque: see-through glass, fences and leaves
    // read as holes in the map (players asked for nothing transparent), so they render solid and double-sided.
    static readonly Entry[] Palette =
    {
        new("VT_Floor", new Color(0.33f, 0.32f, 0.30f), 0.92f),
        new("VT_Wall", new Color(0.50f, 0.46f, 0.41f), 0.95f),
        new("VT_Stone", new Color(0.40f, 0.38f, 0.35f), 0.90f),
        new("VT_Wood", new Color(0.29f, 0.20f, 0.12f), 0.85f),
        new("VT_Metal", new Color(0.22f, 0.25f, 0.28f), 0.60f),
        new("VT_Foliage", new Color(0.15f, 0.28f, 0.12f), 0.95f, 1f, true),
        new("VT_Glass", new Color(0.30f, 0.38f, 0.44f), 0.15f, 1f, true),
        new("VT_Fence", new Color(0.20f, 0.22f, 0.23f), 0.70f, 1f, true),
        new("VT_Light", new Color(1.00f, 0.92f, 0.74f), 0.80f, 1f, false, 0.35f),
        new("VT_Accent", new Color(0.55f, 0.29f, 0.13f), 0.80f),
        new("VT_Dark", new Color(0.11f, 0.12f, 0.13f), 0.80f),
        new(SkipName, new Color(0.5f, 0.5f, 0.5f), 0.9f),
        new(CollisionName, new Color(0.5f, 0.5f, 0.5f), 0.9f),
    };

    static readonly Dictionary<string, Entry> byName = Palette.ToDictionary(e => e.Name, StringComparer.Ordinal);
    static readonly Dictionary<string, StandardMaterial3D> cache = new(StringComparer.Ordinal);

    public static bool IsPaletteName(string name) => byName.ContainsKey(name) || TryTinted(name, out _, out _);

    /// <summary>Prefix of a palette class tinted with a material's real colour: <c>VT_C{class}_{rrggbb}</c>.</summary>
    const string TintPrefix = "VT_C";

    static string TintedName(P cls, Color c) =>
        $"{TintPrefix}{(int)cls}_{(int)MathF.Round(c.R * 255):x2}{(int)MathF.Round(c.G * 255):x2}{(int)MathF.Round(c.B * 255):x2}";

    static bool TryTinted(string name, out Entry entry, out Color color)
    {
        entry = Palette[(int)P.Wall];
        color = default;
        if (!name.StartsWith(TintPrefix, StringComparison.Ordinal)) return false;
        int us = name.IndexOf('_', TintPrefix.Length);
        if (us < 0 || name.Length != us + 7) return false;
        if (!int.TryParse(name.AsSpan(TintPrefix.Length, us - TintPrefix.Length), out int cls) || cls < 0 || cls >= Palette.Length) return false;
        if (!int.TryParse(name.AsSpan(us + 1), System.Globalization.NumberStyles.HexNumber, null, out int rgb)) return false;
        entry = Palette[cls];
        color = Color.Color8((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }

    /// <summary>Shared Godot material for a palette name (one resource per name for the whole map set).</summary>
    public static StandardMaterial3D? Material(string name)
    {
        if (cache.TryGetValue(name, out var cached)) return cached;
        Color albedo;
        if (byName.TryGetValue(name, out Entry? e)) albedo = e.Color;
        else if (TryTinted(name, out e, out albedo)) { }
        else return null;
        bool transparent = e.Alpha < 1f;
        var m = new StandardMaterial3D
        {
            ResourceName = name,
            AlbedoColor = new Color(albedo.R, albedo.G, albedo.B, e.Alpha),
            Roughness = e.Roughness,
            Metallic = 0f,
            // The Unreal export writes gameplay data into COLOR_0 (mostly black); it is not albedo.
            VertexColorUseAsAlbedo = false,
            Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
            CullMode = e.TwoSided ? BaseMaterial3D.CullModeEnum.Disabled : BaseMaterial3D.CullModeEnum.Back,
        };
        if (transparent) m.SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled;
        if (e.Emission > 0f)
        {
            m.EmissionEnabled = true;
            m.Emission = new Color(albedo.R, albedo.G, albedo.B);
            m.EmissionEnergyMultiplier = e.Emission;
        }
        return cache[name] = m;
    }

    static P Classify(string name, string alphaMode)
    {
        string n = name.ToLowerInvariant();
        bool alpha = alphaMode is "BLEND" or "MASK";
        if (n.Contains("envcollision")) return P.Collision;
        // Painted detail that would need its texture to read at all.
        if (n.Contains("decal") || n.Contains("graffiti") || n.Contains("floater") || n.Contains("papers") ||
            n.Contains("stain") || n.Contains("sticker") || n.Contains("poster")) return P.Skip;
        if (n.Contains("glass") || n.Contains("water") || (n.Contains("window") && alpha)) return P.Glass;
        if (n.Contains("chainlink") || n.Contains("chain_link") || n.Contains("fence") || n.Contains("lattice") ||
            n.Contains("grate") || n.Contains("grill")) return P.Fence;
        if (n.Contains("foliage") || n.Contains("plant") || n.Contains("leaf") || n.Contains("leaves") ||
            n.Contains("vine") || n.Contains("ivy") || n.Contains("grass") || n.Contains("moss") ||
            n.Contains("tree") || n.Contains("bush") || n.Contains("flower") || n.Contains("petal") ||
            n.Contains("cabbage") || n.Contains("hedge") || n.Contains("fern") || n.Contains("palm") ||
            n.Contains("crop")) return P.Foliage;
        // "Light" alone is usually a colour name (MatteGreyLight), so only real emitters count.
        if (n.Contains("emissive") || n.Contains("neon") || n.Contains("lamp") || n.Contains("bulb") ||
            n.Contains("lantern") || n.Contains("glow")) return P.Light;
        if (n.Contains("signage") || n.StartsWith("sign") || n.Contains("_sign") || n.Contains("logo") ||
            n.Contains("banner")) return P.Accent;
        if (n.Contains("wood") || n.Contains("plank") || n.Contains("crate") || n.Contains("timber") ||
            n.Contains("bamboo")) return P.Wood;
        if (n.Contains("metal") || n.Contains("steel") || n.Contains("iron") || n.Contains("pipe") ||
            n.Contains("vent") || n.Contains("kingdom") || n.Contains("tech") || n.Contains("wire") ||
            n.Contains("rail") || n.Contains("panel") || n.Contains("gear") || n.Contains("generator") ||
            n.Contains("machine") || n.Contains("fan") || n.Contains("camera") || n.Contains("solar") ||
            n.Contains("antenna") || n.Contains("radiator") || n.Contains("canister") || n.Contains("barrel") ||
            n.Contains("container") || n.Contains("door") || n.Contains("gate") || n.Contains("chain") ||
            n.Contains("trash") || n.Contains("acunit")) return P.Metal;
        if (n.Contains("worldgrid") || n.Contains("floor") || n.Contains("ground") || n.Contains("asphalt") ||
            n.Contains("street") || n.Contains("pavement") || n.Contains("terrain") || n.Contains("dirt") ||
            n.Contains("sand")) return P.Floor;
        if (n.Contains("stone") || n.Contains("rock") || n.Contains("concrete") || n.Contains("brick") ||
            n.Contains("plaster") || n.Contains("tile") || n.Contains("marble") || n.Contains("granite") ||
            n.Contains("trim") || n.Contains("wall") || n.Contains("ceiling") || n.Contains("roof") ||
            n.Contains("stucco") || n.Contains("adobe") || n.Contains("castle") || n.Contains("temple") ||
            n.Contains("shingle") || n.Contains("column") || n.Contains("pillar")) return P.Stone;
        return P.Wall;
    }

    /// <summary>
    /// Copies only the buffer views the geometry actually reads into a new binary chunk. Every PNG of the
    /// original export lives in that chunk (2252 images / 500 MB for Bind), so rebuilding it is what turns a
    /// multi-hundred-megabyte GLB into a few tens of megabytes — both the disk read and Godot's import drop.
    /// Accessor offsets are relative to their view and stay valid; only the view's own offset changes.
    /// </summary>
    static void CompactBinary(JsonObject root, List<(uint Type, byte[] Data)> chunks)
    {
        const uint BinChunk = 0x004E4942; // 'BIN\0'
        if (chunks.Count != 1 || chunks[0].Type != BinChunk) return;
        if (root["buffers"] is not JsonArray { Count: 1 } buffers) return;
        if (root["bufferViews"] is not JsonArray views || views.Count == 0) return;
        byte[] bin = chunks[0].Data;

        var used = new bool[views.Count];
        if (root["accessors"] is JsonArray accessors)
            foreach (JsonNode? an in accessors)
                if (an?["bufferView"] is JsonValue bv && bv.TryGetValue(out int i) && i >= 0 && i < used.Length)
                    used[i] = true;

        int total = 0;
        for (int i = 0; i < views.Count; i++)
        {
            if (!used[i] || views[i] is not JsonObject view) continue;
            if (Offset(view) is not int off || Length(view) is not int len) return;
            if (off < 0 || len < 0 || off + len > bin.Length) return;
            total += (len + 3) & ~3;
        }

        var outBin = new byte[total];
        int at = 0;
        for (int i = 0; i < views.Count; i++)
        {
            if (!used[i] || views[i] is not JsonObject view) continue;
            int off = Offset(view)!.Value, len = Length(view)!.Value;
            Buffer.BlockCopy(bin, off, outBin, at, len);
            view["byteOffset"] = at;
            at += (len + 3) & ~3;
        }

        chunks[0] = (BinChunk, outBin);
        buffers[0]!["byteLength"] = total;

        static int? Offset(JsonObject view) => view["byteOffset"] is JsonValue v && v.TryGetValue(out int x) ? x : 0;
        static int? Length(JsonObject view) => view["byteLength"] is JsonValue v && v.TryGetValue(out int x) ? x : null;
    }

    /// <summary>The sRGB colour flatten_glb.py baked into a material (<c>extras.vt_color</c>), or null.</summary>
    static Color? RealColor(JsonNode? material)
    {
        if (material?["extras"]?["vt_color"] is not JsonArray { Count: >= 3 } c) return null;
        float Ch(int i) => c[i] is JsonValue v && v.TryGetValue(out double d) ? Math.Clamp((float)d, 0f, 1f) : float.NaN;
        float r = Ch(0), g = Ch(1), b = Ch(2);
        return float.IsNaN(r) || float.IsNaN(g) || float.IsNaN(b) ? null : new Color(r, g, b);
    }

    /// <summary>Returns a rewritten GLB, or null when the file cannot be parsed (the caller loads it as-is).</summary>
    public static byte[]? Prepare(string path)
    {
        try
        {
            byte[] all = File.ReadAllBytes(path);
            if (all.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(all) != 0x46546C67) return null;
            uint total = BinaryPrimitives.ReadUInt32LittleEndian(all.AsSpan(8));
            if (total < 20 || total > all.Length) total = (uint)all.Length;

            byte[]? json = null;
            var others = new List<(uint Type, byte[] Data)>();
            int off = 12;
            while (off + 8 <= total)
            {
                uint len = BinaryPrimitives.ReadUInt32LittleEndian(all.AsSpan(off));
                uint type = BinaryPrimitives.ReadUInt32LittleEndian(all.AsSpan(off + 4));
                off += 8;
                if (off + len > total) break;
                if (type == 0x4E4F534A) json = all.AsSpan(off, (int)len).ToArray();
                else others.Add((type, all.AsSpan(off, (int)len).ToArray()));
                off += (int)len;
                off = (off + 3) & ~3;
            }
            if (json == null || others.Count == 0) return null;

            if (JsonNode.Parse(json) is not JsonObject root) return null;

            var map = new int[0];
            // Real colours (flatten_glb.py): one extra material per palette class and colour, after the palette.
            var tinted = new List<(P Cls, Color Color)>();
            var tintIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            if (root["materials"] is JsonArray oldMats)
            {
                map = new int[oldMats.Count];
                for (int i = 0; i < oldMats.Count; i++)
                {
                    string name = oldMats[i]?["name"]?.ToString() ?? "";
                    string alpha = oldMats[i]?["alphaMode"]?.ToString() ?? "OPAQUE";
                    P cls = Classify(name, alpha);
                    map[i] = (int)cls;
                    if (cls is P.Skip or P.Collision || RealColor(oldMats[i]) is not Color real) continue;
                    // 5 bits per channel: thousands of Unreal materials collapse to a few hundred shared ones.
                    var q = new Color(MathF.Round(real.R * 31f) / 31f, MathF.Round(real.G * 31f) / 31f, MathF.Round(real.B * 31f) / 31f);
                    string tn = TintedName(cls, q);
                    if (!tintIndex.TryGetValue(tn, out int ti))
                    {
                        ti = Palette.Length + tinted.Count;
                        tintIndex[tn] = ti;
                        tinted.Add((cls, q));
                    }
                    map[i] = ti;
                }
            }

            if (root["meshes"] is JsonArray meshes)
            {
                foreach (JsonNode? mn in meshes)
                {
                    if (mn?["primitives"] is not JsonArray prims) continue;
                    // Backwards so texture-only decals and collision hulls can be dropped from the mesh itself;
                    // nothing then depends on Godot naming the material exactly like the palette entry.
                    for (int pi = prims.Count - 1; pi >= 0; pi--)
                    {
                        if (prims[pi] is not JsonObject p) continue;
                        int idx = (int)P.Wall;
                        if (p["material"] is JsonValue mv && mv.TryGetValue(out int old) && old >= 0 && old < map.Length)
                            idx = map[old];
                        if (idx is (int)P.Skip or (int)P.Collision)
                        {
                            prims.RemoveAt(pi);
                            continue;
                        }
                        p["material"] = idx;
                        if (p["attributes"] is JsonObject attrs)
                        {
                            attrs.Remove("TANGENT");
                            attrs.Remove("TEXCOORD_0");
                            attrs.Remove("TEXCOORD_1");
                            attrs.Remove("TEXCOORD_2");
                            attrs.Remove("COLOR_0");
                            attrs.Remove("COLOR_1");
                            attrs.Remove("COLOR_2");
                        }
                    }
                }
            }

            root.Remove("images");
            root.Remove("textures");
            root.Remove("samplers");
            CompactBinary(root, others);

            var palette = new JsonArray();
            foreach (Entry e in Palette)
            {
                var m = new JsonObject { ["name"] = e.Name };
                if (e.Name is SkipName or CollisionName)
                {
                    palette.Add(m);
                    continue;
                }
                m["pbrMetallicRoughness"] = new JsonObject
                {
                    ["baseColorFactor"] = new JsonArray { e.Color.R, e.Color.G, e.Color.B, e.Alpha },
                    ["metallicFactor"] = 0,
                    ["roughnessFactor"] = e.Roughness,
                };
                if (e.Alpha < 1f) m["alphaMode"] = "BLEND";
                if (e.TwoSided) m["doubleSided"] = true;
                if (e.Emission > 0f)
                    m["emissiveFactor"] = new JsonArray { e.Color.R, e.Color.G, e.Color.B };
                palette.Add(m);
            }
            foreach (var (cls, color) in tinted)
            {
                Entry e = Palette[(int)cls];
                var m = new JsonObject
                {
                    ["name"] = TintedName(cls, color),
                    ["pbrMetallicRoughness"] = new JsonObject
                    {
                        ["baseColorFactor"] = new JsonArray { color.R, color.G, color.B, e.Alpha },
                        ["metallicFactor"] = 0,
                        ["roughnessFactor"] = e.Roughness,
                    },
                };
                if (e.Alpha < 1f) m["alphaMode"] = "BLEND";
                if (e.TwoSided) m["doubleSided"] = true;
                palette.Add(m);
            }
            root["materials"] = palette;

            // The .NET host ships with reflection-based serialization disabled for JsonSerializerOptions
            // defaults; name the resolver explicitly so ToJsonString does not throw while writing the GLB.
            byte[] jsonOut = Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions
            {
                TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
            }));
            int jsonLen = (jsonOut.Length + 3) & ~3;
            int othersLen = others.Sum(c => 8 + ((c.Data.Length + 3) & ~3));
            int size = 12 + 8 + jsonLen + othersLen;

            var outBytes = new byte[size];
            var span = outBytes.AsSpan();
            BinaryPrimitives.WriteUInt32LittleEndian(span, 0x46546C67);
            BinaryPrimitives.WriteUInt32LittleEndian(span[4..], 2);
            BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)size);
            BinaryPrimitives.WriteUInt32LittleEndian(span[12..], (uint)jsonLen);
            BinaryPrimitives.WriteUInt32LittleEndian(span[16..], 0x4E4F534A);
            jsonOut.CopyTo(span[20..]);
            span.Slice(20 + jsonOut.Length, jsonLen - jsonOut.Length).Fill(0x20);

            int at = 20 + jsonLen;
            foreach (var (type, data) in others)
            {
                int padded = (data.Length + 3) & ~3;
                BinaryPrimitives.WriteUInt32LittleEndian(span[at..], (uint)padded);
                BinaryPrimitives.WriteUInt32LittleEndian(span[(at + 4)..], type);
                data.CopyTo(span[(at + 8)..]);
                at += 8 + padded;
            }
            return outBytes;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[maps] GLB strip failed '{path}': {ex.Message}");
            return null;
        }
    }
}
