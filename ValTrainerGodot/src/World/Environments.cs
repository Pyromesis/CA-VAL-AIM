using Godot;
using ValTrainer.Game;
using ValTrainer.Maps;

namespace ValTrainer.World;

/// <summary>
/// Builds the 3D world for a session: palette-tinted PBR boxes (merged per material), trims and wall props,
/// a backdrop outside the play space, HDRI sky, sun and the graphics quality preset.
/// Contract: returns the visual root (added under the session) and the solid boxes for collision/LOS —
/// exactly the map's BoxDefs (or the range walls + the mode's extra solids); decoration never adds blockers.
/// Dev switches (quality override, perf log, fixed camera): see <see cref="EnvDev"/>.
/// </summary>
public static class Environments
{
    public const float RangeHalfWidth = 40f, RangeBack = 12f, RangeFront = -80f;
    const float RangeWallHeight = 16f;

    public sealed record Built(Node3D Root, List<Box> Solid);

    static int Quality(int requested) => EnvDev.Quality(requested);

    /// <summary>The shooting range (player at the origin looking down -Z) plus any extra solids from the mode.</summary>
    public static Built BuildRange(IEnumerable<Box> extra, int quality)
    {
        quality = Quality(quality);
        SurfaceLib.Lite = quality == 0;
        var root = new Node3D { Name = "Range" };
        var floor = new Box(new Vector3(-RangeHalfWidth, -0.1f, RangeFront), new Vector3(RangeHalfWidth, 0, RangeBack));
        var walls = new List<Box>
        {
            new(new Vector3(-RangeHalfWidth, 0, RangeFront - 1), new Vector3(RangeHalfWidth, RangeWallHeight, RangeFront)),          // back wall
            new(new Vector3(-RangeHalfWidth, 0, RangeBack), new Vector3(RangeHalfWidth, RangeWallHeight, RangeBack + 1)),            // wall behind
            new(new Vector3(-RangeHalfWidth - 1, 0, RangeFront), new Vector3(-RangeHalfWidth, RangeWallHeight, RangeBack)),          // left
            new(new Vector3(RangeHalfWidth, 0, RangeFront), new Vector3(RangeHalfWidth + 1, RangeWallHeight, RangeBack)),            // right
        };
        var extras = extra.ToList();
        var solid = new List<Box> { floor };
        solid.AddRange(walls);
        solid.AddRange(extras);

        var th = Themes.Range;
        var wb = new WorldBuilder(th, root, solid, 1234);
        wb.Floor(floor);
        foreach (var w in walls) wb.Wall(w, decorate: true);
        foreach (var b in extras) wb.Crate(b, th.Cover, th.CoverFrame);
        wb.RangeDressing(RangeHalfWidth, RangeFront, RangeBack);
        wb.Backdrop(buildings: false);
        wb.Commit();
        Finish(root, th, quality, wb.Bounds);
        return new Built(root, solid);
    }

    public static Built BuildMap(MapSpot map, int quality)
    {
        quality = Quality(quality);
        SurfaceLib.Lite = quality == 0;
        var root = new Node3D { Name = map.Key };
        var th = Themes.For(map);

        // Real extracted Ascent: the GLBs are external and optional. When present they replace the
        // traced blockout completely, and the collision file generated from the same meshes is used
        // for movement/LOS. When they are absent the normal blockout path below remains untouched.
        if (!string.IsNullOrWhiteSpace(map.RealMapFolder) && RealMap.TryBuild(root, map.RealMapFolder))
        {
            var realSolid = map.Solid.ToList();
            ValTrainer.Game.Fx.FlashOrb.Prewarm(root, map.StartFeet + new Vector3(0, -1.2f, -4f));
            if (!EnvDev.Has("nosigns")) MapSigns(root, map);
            Finish(root, th, quality, BoundsOf(realSolid));
            LightRealMap(root);
            return new Built(root, realSolid);
        }

        // A missing GLB must not leave the player in an invisible map: make a neutral blockout from
        // whatever collision we have (the real-mesh file when available, the map's Boxes otherwise).
        if (!string.IsNullOrWhiteSpace(map.RealMapFolder))
        {
            var fallback = map.Solid.ToList();
            if (fallback.Count > 0) return BuildCollisionFallback(map, root, th, quality, fallback);
        }

        var solid = new List<Box>();
        foreach (var b in map.Boxes) solid.Add(b.Bounds);
        var wb = new WorldBuilder(th, root, solid, map.Key.Aggregate(17, (h, ch) => h * 31 + ch) & 0xffff); // stable across runs
        foreach (var e in map.Enemies) wb.Avoid(e.Feet);
        foreach (var f in map.Flashes) { wb.Avoid(f.Pop); wb.Avoid(f.From); }
        wb.Avoid(map.Choke);

        var walls = new List<Box>();
        foreach (var b in map.Boxes)
        {
            switch (b.Surf)
            {
                case Surf.Floor: wb.Floor(b.Bounds); break;
                case Surf.Trim: wb.Trim(b.Bounds); break;
                case Surf.Cover: wb.Crate(b.Bounds, th.Cover, th.CoverFrame); break;
                case Surf.Elevated: wb.Elevated(b.Bounds); break;
                default: wb.Wall(b.Bounds); walls.Add(b.Bounds); break;
            }
        }
        if (!EnvDev.Has("nodecor")) wb.DecorateWalls(walls);
        // Compile the flash effect shaders under the floor, in front of the spawn, before the first throw.
        ValTrainer.Game.Fx.FlashOrb.Prewarm(root, map.StartFeet + new Vector3(0, -1.2f, -4f));
        wb.Backdrop(buildings: !EnvDev.Has("nodecor"));
        wb.Commit();
        if (!EnvDev.Has("nosigns")) MapSigns(root, map);
        Finish(root, th, quality, wb.Bounds);
        return new Built(root, solid);
    }

    /// <summary>
    /// The extracted art is darker than the blockout because many materials are metallic and the real
    /// corridors are in building shadow. Keep the sky reflection even on Low and add a soft fill so the
    /// playable floor/details stay readable; expensive GI presets are left untouched.
    /// </summary>
    static void LightRealMap(Node3D root)
    {
        foreach (Node n in root.GetChildren())
        {
            if (n is not WorldEnvironment { Environment: { } env }) continue;
            // Flat palette materials have no texture albedo, so the blockout-grade boost that made the
            // textured art readable now only washes every surface out.
            env.AmbientLightEnergy = Mathf.Min(env.AmbientLightEnergy, 0.8f);
            env.ReflectedLightSource = Godot.Environment.ReflectionSource.Sky;
            env.TonemapExposure = Mathf.Min(env.TonemapExposure, 1f);
            env.FogDensity *= 0.8f;
        }

        var fill = new DirectionalLight3D
        {
            Name = "RealMapFill",
            LightColor = new Color(0.82f, 0.9f, 1f),
            LightEnergy = 0.35f,
            LightAngularDistance = 25f,
            ShadowEnabled = false,
            RotationDegrees = new Vector3(-48, 155, 0),
        };
        root.AddChild(fill);
    }

    static Aabb BoundsOf(IReadOnlyList<Box> solid)
    {
        if (solid.Count == 0) return new Aabb(new Vector3(-40, 0, -40), new Vector3(80, 12, 80));
        Vector3 min = solid[0].Min, max = solid[0].Max;
        foreach (var b in solid)
        {
            min = new Vector3(Mathf.Min(min.X, b.Min.X), Mathf.Min(min.Y, b.Min.Y), Mathf.Min(min.Z, b.Min.Z));
            max = new Vector3(Mathf.Max(max.X, b.Max.X), Mathf.Max(max.Y, b.Max.Y), Mathf.Max(max.Z, b.Max.Z));
        }
        return new Aabb(min, max - min);
    }

    /// <summary>Neutral box fallback built from the collision file when the extracted art is absent.</summary>
    static Built BuildCollisionFallback(MapSpot map, Node3D root, Theme th, int quality, List<Box> solid)
    {
        var wb = new WorldBuilder(th, root, solid, map.Key.Aggregate(17, (h, ch) => h * 31 + ch) & 0xffff);
        foreach (var b in solid)
        {
            if (b.Size.Y <= 0.65f) wb.Floor(b);
            else wb.Wall(b);
        }
        ValTrainer.Game.Fx.FlashOrb.Prewarm(root, map.StartFeet + new Vector3(0, -1.2f, -4f));
        wb.Commit();
        if (!EnvDev.Has("nosigns")) MapSigns(root, map);
        Finish(root, th, quality, wb.Bounds);
        return new Built(root, solid);
    }

    /// <summary>
    /// Floating callout signs at the spawn and the site choke ("A MAIN", "A SITE"). The blockouts are still
    /// single-site geometry; these tags are the in-world read of which part of the minimap the drill covers.
    /// </summary>
    static void MapSigns(Node3D root, MapSpot map)
    {
        var (start, site) = Areas(map);
        var dir = map.StartFeet - map.Choke;
        dir.Y = 0;
        if (dir.LengthSquared() < 0.01f) dir = new Vector3(0, 0, 1);
        dir = dir.Normalized();
        // site sign on the approach side of the choke so the arch/walls never hide it; the spawn sign sits
        // behind the player (it is only read when turning around) so it can never fill the view
        Sign(root, site, map.Choke + dir * 2.4f + new Vector3(0, 2.45f, 0), 0.9f);
        Sign(root, start, map.StartFeet + dir * 1.8f + new Vector3(0, 2.15f, 0), 0.8f);
    }

    static (string Start, string Site) Areas(MapSpot map)
    {
        string start = "", site = "";
        int i = map.Name.IndexOf('\u2192');
        if (i > 0)
        {
            start = map.Name[..i].Trim();
            site = map.Name[(i + 1)..].Trim();
        }
        if (start.Length == 0) start = map.Map;
        if (site.Length == 0) site = "SITE";
        return (start.ToUpperInvariant(), site.ToUpperInvariant());
    }

    static void Sign(Node3D root, string text, Vector3 at, float scale)
    {
        root.AddChild(new Label3D
        {
            Text = text,
            FontSize = 96,
            PixelSize = 0.006f * scale,
            Modulate = new Color(0.35f, 0.85f, 0.75f, 0.62f),
            OutlineModulate = new Color(0.02f, 0.03f, 0.05f, 0.85f),
            OutlineSize = 14,
            Shaded = false,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            DoubleSided = true,
            AlphaCut = Label3D.AlphaCutMode.Discard,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = at,
        });
    }

    static void Finish(Node3D root, Theme th, int quality, Aabb bounds)
    {
        WorldLighting.Build(root, th, quality, bounds, EnvDev.SkyYaw);
        if (EnvDev.Perf || EnvDev.Cam != null) root.AddChild(new EnvPerfProbe { Quality = quality, Log = EnvDev.Perf, Cam = EnvDev.Cam });
    }
}
