using Godot;
using CaValAim.Game;
using CaValAim.World;

namespace CaValAim.Maps;

public enum Surf { Floor, Wall, Trim, Cover, Elevated }
public enum Stance { Stand, Crouch }

/// <summary>Per-surface tint colors that evoke the real map (textures come from the environment builder).</summary>
public sealed record MapPalette(Color Floor, Color Wall, Color Trim, Color Cover, Color Elevated, Color Sky);

public sealed record BoxDef(Vector3 Min, Vector3 Max, Surf Surf = Surf.Wall)
{
    public Box Bounds => new(Min, Max);
}

/// <summary>Where a defender holds. <c>Feet</c> includes floor height (heaven spots are elevated).</summary>
public sealed record EnemySpot(string Name, Vector3 Feet, Stance Stance);

/// <summary>An attacker utility lineup: thrown from <c>From</c>, pops at <c>Pop</c>.</summary>
public sealed record FlashSpot(string Name, Vector3 From, Vector3 Pop);

/// <summary>A stone door at <c>Pivot</c> (floor level): two wings of <c>Radius</c> that turn 180° around Y
/// when the button is pressed, in <c>Seconds</c> (VALORANT: 10 s).</summary>
public sealed record DoorSpot(Vector3 Pivot, float Radius, float Height, float Seconds = 10f);

/// <summary>A standard controller smoke for this site's execute: smoke centre (floor level) that cuts a main defender sightline.</summary>
public sealed record SmokeSpot(string Name, Vector3 Center);

/// <summary>
/// A simplified blockout of one famous Valorant location. Distances come from the in-game callout
/// coordinates (valorant-api.com); wall shapes are approximations. Coordinates: meters, player looks
/// down -Z at yaw 0, +X is right. <c>Choke</c> is the doorway attackers come through.
/// </summary>
public sealed class MapSpot
{
    public required string Key { get; init; }
    public required string Map { get; init; }
    public required string Name { get; init; }
    public required string Drill { get; init; }
    public required Vector3 StartFeet { get; init; }
    public float StartYaw { get; init; }
    public required Vector3 Choke { get; init; }
    public required MapPalette Palette { get; init; }
    public required BoxDef[] Boxes { get; init; }
    public required EnemySpot[] Enemies { get; init; }
    public required FlashSpot[] Flashes { get; init; }

    /// <summary>Optional external real-map folder ("maps/ascent"). It holds collision.json and the art GLBs.
    /// Null keeps the traced blockout. Missing files fall back to <see cref="Boxes"/> automatically.</summary>
    public string? RealMapFolder { get; init; }

    /// <summary>Openings carved out of the loaded collision after the fact (a window, a doorway): the extractor
    /// turns some of them into solid slabs, which makes a visible hole behave like a wall. Each cut is an AABB
    /// subtracted from every colliding box; only the real-map path uses them.</summary>
    public Box[] Cuts { get; init; } = Array.Empty<Box>();

    /// <summary>Solid boxes added to the loaded collision (art walls the extractor dropped).</summary>
    public Box[] ExtraSolid { get; init; } = Array.Empty<Box>();

    /// <summary>Art triangles whose centre falls inside these boxes are left out when the real map is built:
    /// used to remove a closed prop the trainer keeps open (Lotus' revolving door). Real-map path only.</summary>
    public Box[] HideArt { get; init; } = Array.Empty<Box>();

    /// <summary>A revolving door drawn over an opening carved by <see cref="Cuts"/>. Visual only: the wings have
    /// no collision, so the player walks through the spinning door.</summary>
    public DoorSpot? Door { get; init; }

    /// <summary>A climbable rope (Lotus A Top). Walk into it and hold forward/jump to ride it up.</summary>
    public Game.RopeClimb? Rope { get; init; }

    /// <summary>Where the spike is usually planted on this site (feet positions on walkable floor). Used by the post-plant
    /// and retake drills. Empty = the drills derive a spot from the site-level defender spots.</summary>
    public Vector3[] PlantSpots { get; init; } = Array.Empty<Vector3>();
    /// <summary>Where retaking defenders enter the site from (their spawn side): walkable floor points inside the closed area,
    /// out of sight of the site. Empty = the drills derive them (far defender spots / navigation grid).</summary>
    public Vector3[] RetakeEntries { get; init; } = Array.Empty<Vector3>();
    /// <summary>The standard controller smokes for this attack (e.g. Ascent A: Heaven, Tree). Empty = the smoke drill derives them.</summary>
    public SmokeSpot[] Smokes { get; init; } = Array.Empty<SmokeSpot>();
    /// <summary>The map's look (surfaces, sky, sun). Null = the built-in theme for this key (Ascent / Bind / Haven / Split).
    /// New maps define their theme here, next to their geometry, so every map lives in its own file.</summary>
    public Func<MapPalette, World.Theme>? Look { get; init; }

    Box[]? solid;
    public bool UsesRealCollision { get; private set; }
    public IReadOnlyList<Box> Solid => solid ??= LoadSolid();

    Box[] LoadSolid()
    {
        if (!string.IsNullOrWhiteSpace(RealMapFolder))
        {
            Box[]? real = MapAssets.LoadCollision(RealMapFolder + "/collision.json");
            if (real is { Length: > 0 })
            {
                UsesRealCollision = true;
                if (Cuts.Length > 0) real = Subtract(real, Cuts);
                if (ExtraSolid.Length > 0) real = real.Concat(ExtraSolid).ToArray();
                var repairs = MapCollisionRepairs.For(Key);
                if (repairs.Length > 0) real = real.Concat(repairs).ToArray();
                if (!SpawnAudit.Unfiltered)
                    Game.SpawnSafety.SetMoves(real, MapAssets.LoadMoves(RealMapFolder + "/spawn_exclude.json"));
                return real;
            }
        }
        return Boxes.Select(b => b.Bounds).ToArray();
    }

    /// <summary>Removes every <paramref name="cut"/> volume from <paramref name="source"/> (axis-aligned subtraction).</summary>
    static Box[] Subtract(Box[] source, Box[] cuts)
    {
        var result = new List<Box>(source.Length);
        foreach (var b in source)
        {
            var pieces = new List<Box> { b };
            foreach (var c in cuts)
            {
                for (int i = pieces.Count - 1; i >= 0; i--)
                {
                    var p = pieces[i];
                    if (p.Max.X <= c.Min.X || p.Min.X >= c.Max.X ||
                        p.Max.Y <= c.Min.Y || p.Min.Y >= c.Max.Y ||
                        p.Max.Z <= c.Min.Z || p.Min.Z >= c.Max.Z) continue;
                    pieces.RemoveAt(i);
                    void Add(float x0, float y0, float z0, float x1, float y1, float z1)
                    {
                        if (x1 - x0 > 1e-4f && y1 - y0 > 1e-4f && z1 - z0 > 1e-4f)
                            pieces.Add(new Box(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1)));
                    }
                    // Below / above the cut, then the four side slabs around it.
                    Add(p.Min.X, p.Min.Y, p.Min.Z, p.Max.X, Mathf.Min(p.Max.Y, c.Min.Y), p.Max.Z);
                    Add(p.Min.X, Mathf.Max(p.Min.Y, c.Max.Y), p.Min.Z, p.Max.X, p.Max.Y, p.Max.Z);
                    Add(p.Min.X, Mathf.Max(p.Min.Y, c.Min.Y), p.Min.Z, Mathf.Min(p.Max.X, c.Min.X), Mathf.Min(p.Max.Y, c.Max.Y), p.Max.Z);
                    Add(Mathf.Max(p.Min.X, c.Max.X), Mathf.Max(p.Min.Y, c.Min.Y), p.Min.Z, p.Max.X, Mathf.Min(p.Max.Y, c.Max.Y), p.Max.Z);
                    Add(Mathf.Max(p.Min.X, c.Min.X), Mathf.Max(p.Min.Y, c.Min.Y), p.Min.Z, Mathf.Min(p.Max.X, c.Max.X), Mathf.Min(p.Max.Y, c.Max.Y), Mathf.Min(p.Max.Z, c.Min.Z));
                    Add(Mathf.Max(p.Min.X, c.Min.X), Mathf.Max(p.Min.Y, c.Min.Y), Mathf.Max(p.Min.Z, c.Max.Z), Mathf.Min(p.Max.X, c.Max.X), Mathf.Min(p.Max.Y, c.Max.Y), p.Max.Z);
                }
            }
            result.AddRange(pieces);
        }
        return result.ToArray();
    }

    /// <summary>Converting Unreal's left-handed coordinates mirrors the layout; flip X back so sides match the game.</summary>
    public MapSpot Mirrored()
    {
        static Vector3 F(Vector3 v) => new(-v.X, v.Y, v.Z);
        static Box BX(Box b) => new(new Vector3(-b.Max.X, b.Min.Y, b.Min.Z), new Vector3(-b.Min.X, b.Max.Y, b.Max.Z));
        return new MapSpot
        {
            Key = Key, Map = Map, Name = Name, Drill = Drill,
            StartFeet = F(StartFeet), StartYaw = -StartYaw, Choke = F(Choke), Palette = Palette,
            RealMapFolder = RealMapFolder,
            Cuts = Cuts.Select(BX).ToArray(), ExtraSolid = ExtraSolid.Select(BX).ToArray(),
            HideArt = HideArt.Select(BX).ToArray(),
            Door = Door is { } d ? d with { Pivot = F(d.Pivot) } : null,
            Rope = Rope is { } r ? r with { Base = F(r.Base), Exit = F(r.Exit) } : null,
            Boxes = Boxes.Select(b => b with { Min = new(-b.Max.X, b.Min.Y, b.Min.Z), Max = new(-b.Min.X, b.Max.Y, b.Max.Z) }).ToArray(),
            Enemies = Enemies.Select(e => e with { Feet = F(e.Feet) }).ToArray(),
            Flashes = Flashes.Select(f => f with { From = F(f.From), Pop = F(f.Pop) }).ToArray(),
            PlantSpots = PlantSpots.Select(F).ToArray(),
            RetakeEntries = RetakeEntries.Select(F).ToArray(),
            Smokes = Smokes.Select(s => s with { Center = F(s.Center) }).ToArray(),
            Look = Look,
        };
    }

    public bool LineOfSight(Vector3 a, Vector3 b) => !Collision.Blocked(a, b, Solid);
}
