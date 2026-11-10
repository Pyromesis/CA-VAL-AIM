using Godot;
using CaValAim.Game;
using CaValAim.Maps;

namespace CaValAim.World;

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

    public sealed record Built(Node3D Root, List<Box> Solid, RevolvingDoor? Door = null);

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
        _ = map.Solid; // Resolve and validate collision before selecting matching art.
        if (map.UsesRealCollision && !string.IsNullOrWhiteSpace(map.RealMapFolder)
            && RealMap.TryBuild(root, map.RealMapFolder, map.HideArt))
        {
            var realSolid = map.Solid.ToList();
            RevolvingDoor? doorNode = map.Door is { } door ? BuildDoor(root, door, map.Palette) : null;
            if (map.Rope is { } rope) BuildRope(root, rope, map.Palette);
            CaValAim.Game.Fx.FlashOrb.Prewarm(root, map.StartFeet + new Vector3(0, -1.2f, -4f));
            if (!EnvDev.Has("nosigns")) MapSigns(root, map);
            Finish(root, th, quality, BoundsOf(realSolid));
            LightRealMap(root);
            return new Built(root, realSolid, doorNode);
        }

        // A missing GLB must not leave the player in an invisible map: make a neutral blockout from
        // whatever collision we have (the real-mesh file when available, the map's Boxes otherwise).
        if (!string.IsNullOrWhiteSpace(map.RealMapFolder))
        {
            var fallback = map.Solid.ToList();
            if (fallback.Count > 0)
            {
                if (Core.CmdLine.Dev) GD.Print($"[maps] {map.Key}: collision blockout ({fallback.Count} boxes)");
                return BuildCollisionFallback(map, root, th, quality, fallback);
            }
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
        CaValAim.Game.Fx.FlashOrb.Prewarm(root, map.StartFeet + new Vector3(0, -1.2f, -4f));
        wb.Backdrop(buildings: !EnvDev.Has("nodecor"));
        wb.Commit();
        if (!EnvDev.Has("nosigns")) MapSigns(root, map);
        Finish(root, th, quality, wb.Bounds);
        return new Built(root, solid);
    }

    /// <summary>
    /// Built twice in code because the extracted art has the door baked shut: a stone frame around the carved
    /// opening, a bronze ring on each face, two buttons, and the two solid stone wings that turn 180° in 10 s.
    /// Collision is added by the session (it follows <see cref="RevolvingDoor.Angle"/> every frame).
    /// </summary>
    static RevolvingDoor BuildDoor(Node3D root, DoorSpot door, MapPalette palette)
    {
        var stone = new StandardMaterial3D { AlbedoColor = Darken(palette.Wall, 0.72f), Roughness = 0.95f };
        var bronze = new StandardMaterial3D { AlbedoColor = palette.Cover, Metallic = 0.55f, Roughness = 0.45f };
        var pivot = new RevolvingDoor
        {
            Name = "RevolvingDoor", Position = door.Pivot,
            Radius = door.Radius, Height = door.Height, Seconds = door.Seconds,
        };
        // The real door is a round stone disc: one disc rotating around its vertical diameter looks exactly
        // like the two-wing version (and covers the carved opening). The collision is still built from wings.
        var disc = new MeshInstance3D
        {
            Name = "Disc",
            Mesh = new CylinderMesh { TopRadius = door.Radius, BottomRadius = door.Radius, Height = 0.22f, RadialSegments = 48 },
            Position = new Vector3(0f, door.Height * 0.5f, 0f),
            RotationDegrees = new Vector3(90f, 0f, 0f),
            MaterialOverride = stone,
        };
        pivot.AddChild(disc);
        // Bronze concentric bands on both faces plus a stud at the centre: the carved door of the real map.
        for (int face = 0; face < 2; face++)
        {
            float z = face == 0 ? -0.14f : 0.14f;
            pivot.AddChild(new MeshInstance3D
            {
                Name = face == 0 ? "InsetS" : "InsetN",
                Mesh = new TorusMesh { InnerRadius = door.Radius - 0.30f, OuterRadius = door.Radius - 0.18f, Rings = 48, RingSegments = 10 },
                Position = new Vector3(0f, door.Height * 0.5f, z),
                RotationDegrees = new Vector3(90f, 0f, 0f),
                MaterialOverride = bronze,
            });
            pivot.AddChild(new MeshInstance3D
            {
                Name = face == 0 ? "HubS" : "HubN",
                Mesh = new SphereMesh { Radius = 0.12f, Height = 0.22f },
                Position = new Vector3(0f, door.Height * 0.5f, z),
                MaterialOverride = bronze,
            });
            for (int k = 0; k < 8; k++)
            {
                float a = Mathf.Pi * 0.25f * k;
                pivot.AddChild(new MeshInstance3D
                {
                    Name = $"FaceStud{face}{k}",
                    Mesh = new SphereMesh { Radius = 0.045f, Height = 0.09f },
                    Position = new Vector3(Mathf.Cos(a) * 1.05f, door.Height * 0.5f + Mathf.Sin(a) * 1.05f, z),
                    MaterialOverride = bronze,
                });
            }
        }
        root.AddChild(pivot);

        // Stepped stone spandrels turn the rectangular cut into the round doorway of the real map. They are
        // visual only: the door's rectangular collision wings already seal the whole opening when closed.
        float left = door.Pivot.X - door.Radius, right = door.Pivot.X + door.Radius;
        float zs = door.Pivot.Z - 0.25f, zn = door.Pivot.Z + 0.25f;
        void Spandrel(float x0, float x1, float y0, float y1) =>
            FrameBox(root, new Vector3(x0, y0, zs), new Vector3(x1, y1, zn), stone);
        for (int k = 0; k < 3; k++)
        {
            float inset = 1.05f - 0.35f * k, y0 = 3.20f - 0.20f * k;
            Spandrel(left, left + inset, y0, door.Height + 0.02f);
            Spandrel(right - inset, right, y0, door.Height + 0.02f);
        }
        Spandrel(left, left + 0.30f, -0.02f, 0.30f);
        Spandrel(right - 0.30f, right, -0.02f, 0.30f);

        // Stone lining that hides the raw cut edges of the wall.
        FrameBox(root, new Vector3(left - 0.06f, -0.02f, zs), new Vector3(left + 0.06f, door.Height + 0.06f, zn), stone);
        FrameBox(root, new Vector3(right - 0.06f, -0.02f, zs), new Vector3(right + 0.06f, door.Height + 0.06f, zn), stone);
        FrameBox(root, new Vector3(left - 0.06f, door.Height - 0.04f, zs), new Vector3(right + 0.06f, door.Height + 0.08f, zn), stone);

        // Bronze ring on both faces, centred on the passage axis.
        var ringMesh = new TorusMesh { InnerRadius = door.Radius - 0.09f, OuterRadius = door.Radius + 0.09f, Rings = 64, RingSegments = 10 };
        for (int i = 0; i < 2; i++)
        {
            float z = door.Pivot.Z + (i == 0 ? -0.20f : 0.20f);
            root.AddChild(new MeshInstance3D
            {
                Name = i == 0 ? "DoorRingS" : "DoorRingN",
                Mesh = ringMesh,
                Position = new Vector3(door.Pivot.X, door.Height * 0.5f, z),
                RotationDegrees = new Vector3(90f, 0f, 0f),
                MaterialOverride = bronze,
            });
        }

        // Buttons on the wall beside the opening, one on each face (both work, like VALORANT).
        var btnMesh = new CylinderMesh { TopRadius = 0.075f, BottomRadius = 0.085f, Height = 0.09f, RadialSegments = 20 };
        var lit = new StandardMaterial3D
        {
            AlbedoColor = palette.Cover,
            Metallic = 0.4f,
            Roughness = 0.35f,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.72f, 0.32f),
            EmissionEnergyMultiplier = 0.7f,
        };
        // Centre of the 0.22 m stone strip left of the opening (x 0.08–0.30): the plate must not float off it.
        float bx = 0.19f;
        pivot.ButtonA = new Vector3(bx, 0.85f, door.Pivot.Z - 0.30f);
        pivot.ButtonB = new Vector3(bx, 0.85f, door.Pivot.Z + 0.30f);
        for (int i = 0; i < 2; i++)
        {
            float sign = i == 0 ? -1f : 1f;
            // The wall slab is 0.34 m thick (faces at pivot.Z ± 0.17): mount the plate on the face and let the
            // button stick out ~0.1 m, instead of floating in front of it. It sits below the bronze ring (whose
            // lower band crosses this strip at y 1.06–1.39) so the button stays visible from both sides.
            float faceZ = door.Pivot.Z + sign * 0.17f;
            FrameBox(root, new Vector3(bx - 0.09f, 0.65f, faceZ - 0.015f), new Vector3(bx + 0.09f, 1.05f, faceZ + 0.015f), bronze);
            root.AddChild(new MeshInstance3D
            {
                Name = i == 0 ? "DoorButtonS" : "DoorButtonN",
                Mesh = btnMesh,
                Position = new Vector3(bx, 0.85f, faceZ + sign * 0.06f),
                RotationDegrees = new Vector3(90f, 0f, 0f),
                MaterialOverride = lit,
            });
        }
        GD.Print($"[door] {door.Pivot} r={door.Radius:0.00} {door.Seconds:0}s buttons {pivot.ButtonA} / {pivot.ButtonB}");
        return pivot;
    }

    static void FrameBox(Node3D root, Vector3 min, Vector3 max, Material mat) =>
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = max - min },
            Position = (min + max) * 0.5f,
            MaterialOverride = mat,
        });

    /// <summary>
    /// The rope ascender is not in the extracted art. Draw the thick line, its bottom loop and the bronze
    /// spool that anchors it to the A Top ledge; walking into it rides it up (MovementBody.Climb).
    /// </summary>
    static void BuildRope(Node3D root, RopeClimb rope, MapPalette palette)
    {
        var node = new Node3D { Name = "RopeAscender", Position = new Vector3(rope.Base.X, rope.Base.Y, rope.Base.Z) };
        var line = new StandardMaterial3D { AlbedoColor = new Color(0.24f, 0.18f, 0.12f), Roughness = 0.85f };
        var bronze = new StandardMaterial3D { AlbedoColor = palette.Cover, Metallic = 0.5f, Roughness = 0.45f };
        float top = rope.TopY + 0.06f;
        node.AddChild(new MeshInstance3D
        {
            Name = "Line",
            Mesh = new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.055f, Height = top - 0.18f, RadialSegments = 10 },
            Position = new Vector3(0f, (top + 0.18f) * 0.5f, 0f),
            MaterialOverride = line,
        });
        node.AddChild(new MeshInstance3D
        {
            Name = "Loop",
            Mesh = new TorusMesh { InnerRadius = 0.13f, OuterRadius = 0.24f, RingSegments = 24, Rings = 10 },
            Position = new Vector3(0f, 1.02f, 0f),
            MaterialOverride = bronze,
        });
        // The spool hangs off the ledge, west of the line (the arm reaches the A Top platform edge at x = -5).
        const float reach = 0.85f;
        node.AddChild(new MeshInstance3D
        {
            Name = "Arm",
            Mesh = new BoxMesh { Size = new Vector3(reach, 0.15f, 0.20f) },
            Position = new Vector3(-reach * 0.5f, top - 0.02f, 0f),
            MaterialOverride = bronze,
        });
        node.AddChild(new MeshInstance3D
        {
            Name = "Spool",
            Mesh = new CylinderMesh { TopRadius = 0.19f, BottomRadius = 0.19f, Height = 0.36f, RadialSegments = 18 },
            Position = new Vector3(-reach + 0.05f, top - 0.05f, 0f),
            RotationDegrees = new Vector3(0f, 0f, 90f),
            MaterialOverride = bronze,
        });
        root.AddChild(node);
    }

    static Color Darken(Color c, float k) => new(c.R * k, c.G * k, c.B * k, c.A);

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
        var batch = new MeshBatch();
        var floor = SurfaceLib.Get(th.Floor);
        var wall = SurfaceLib.Get(th.Wall);
        foreach (var b in solid)
            batch.Box(b.Size.Y <= 0.65f ? floor : wall, b, grime: false);
        RevolvingDoor? door = map.Door is { } entry ? BuildDoor(root, entry, map.Palette) : null;
        if (map.Rope is { } rope) BuildRope(root, rope, map.Palette);
        CaValAim.Game.Fx.FlashOrb.Prewarm(root, map.StartFeet + new Vector3(0, -1.2f, -4f));
        batch.Commit(root, "CollisionBlockout");
        if (!EnvDev.Has("nosigns")) MapSigns(root, map);
        Finish(root, th, quality, BoundsOf(solid));
        return new Built(root, solid, door);
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
