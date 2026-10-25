using Godot;

namespace ValTrainer.Game;

/// <summary>
/// Puts a spawn (the player's or a bot's) on real, open floor. Map spots are authored by hand against one collision file;
/// a regenerated file, or a spot authored a little off, used to leave the body knee-deep in a slab, inside a wall, on top
/// of a crate it was never meant to stand on, or with no floor at all under it (the player then fell through the map).
/// A spot is accepted when:
/// <list type="bullet">
/// <item>there is a walkable box top under the footprint (<see cref="Collision.Ground"/>) at the chosen height,</item>
/// <item>the standing body overlaps no box (<see cref="Collision.Overlaps"/> with the player radius),</item>
/// <item>the head has room (no box bottom below feet + <see cref="Collision.PlayerHeight"/>),</item>
/// <item>and, when the map has a minimap mask, the point lies inside the drawn playable area.</item>
/// </list>
/// Otherwise the nearest acceptable point within <see cref="SearchRadius"/> is used (rings of 0.5 m, preferring the
/// authored height).
/// </summary>
public static class SpawnSafety
{
    public const float SearchRadius = 4f;
    /// <summary>A floor this far above / below the authored feet still counts as "the floor of this spot".</summary>
    public const float HeightWindow = 1.2f;

    /// <summary>Minimap test: -1 = unknown (no mask), 0 = outside, 1 = inside the playable area.</summary>
    public static Func<Vector3, int>? PlayableTest;

    /// <summary>Is a body standing at <paramref name="feet"/> (exactly on a floor) in a valid spot?</summary>
    public static bool Standable(Vector3 feet, IReadOnlyList<Box> world, Func<Vector3, int>? playable = null)
    {
        float g = Collision.Ground(feet, world, 0.02f);
        if (!float.IsFinite(g) || Mathf.Abs(g - feet.Y) > 0.03f) return false;
        if (Collision.Overlaps(feet, world, Collision.PlayerRadius)) return false;
        if (Collision.Ceiling(feet, world, feet.Y + 0.05f) < feet.Y + Collision.PlayerHeight + 0.05f) return false;
        if (Shadowed(feet, world)) return false;
        return (playable ?? PlayableTest)?.Invoke(feet) != 0;
    }

    /// <summary>Lowest clearance a real walkway has under a floor slab above it. A floor with another floor slab closer than
    /// this above it is the hidden ground under a raised floor (Bind: under Hookah, 2.1 m below its floor): standing there
    /// is "under the map".</summary>
    public const float ShadowClearance = 2.6f;

    /// <summary>Is there a floor slab (a thin box, ≤ 0.45 m) whose top is 0.5 – <see cref="ShadowClearance"/> m above the
    /// feet, right over them?</summary>
    public static bool Shadowed(Vector3 feet, IReadOnlyList<Box> world)
    {
        var near = new List<int>(32);
        Collision.Near(world, feet.X, feet.Z, feet.X, feet.Z, near);
        foreach (int i in near)
        {
            var b = world[i];
            if (b.Max.Y - b.Min.Y > 0.45f) continue;
            if (b.Max.Y <= feet.Y + 0.5f || b.Max.Y > feet.Y + ShadowClearance) continue;
            if (feet.X < b.Min.X || feet.X > b.Max.X || feet.Z < b.Min.Z || feet.Z > b.Max.Z) continue;
            return true;
        }
        return false;
    }

    /// <summary>The floor heights within <see cref="HeightWindow"/> of <paramref name="feet"/>.Y under the footprint, nearest first.</summary>
    static IEnumerable<float> FloorsNear(Vector3 feet, IReadOnlyList<Box> world, float up = HeightWindow)
    {
        const float r = Collision.PlayerRadius - 0.05f;
        var tops = new List<float>(8);
        var near = new List<int>(64);
        Collision.Near(world, feet.X - r, feet.Z - r, feet.X + r, feet.Z + r, near);
        foreach (int i in near)
        {
            var b = world[i];
            if (feet.X + r < b.Min.X || feet.X - r > b.Max.X || feet.Z + r < b.Min.Z || feet.Z - r > b.Max.Z) continue;
            float y = b.Max.Y;
            if (y < feet.Y - HeightWindow || y > feet.Y + up) continue;
            if (tops.Any(t => Mathf.Abs(t - y) < 0.01f)) continue;
            tops.Add(y);
        }
        return tops.OrderBy(t => Mathf.Abs(t - feet.Y));
    }

    /// <summary>
    /// The nearest valid standing point to <paramref name="feet"/> (see the class remarks). <paramref name="ok"/> is false
    /// when nothing valid was found within <see cref="SearchRadius"/>; the authored point is then returned unchanged.
    /// </summary>
    public static Vector3 Resolve(Vector3 feet, IReadOnlyList<Box> world, out bool ok, Func<Vector3, int>? playable = null)
    {
        ok = true;
        if (world.Count == 0) return feet;
        // An authored spot the offline art check rejected (inside art, under a floor, on an invisible slab) goes to the
        // clean point the check picked for it (maps/<key>/spawn_exclude.json "moves", see SetMoves).
        List<(Vector3 From, Vector3 At, Vector3 To)>? list;
        lock (moves) moves.TryGetValue(KeyOf(world), out list);
        if (list != null)
        {
            // the closest listed spot wins (two authored spots can sit within 0.6 m of each other)
            float best = 0.6f;
            Vector3? moved = null;
            foreach (var (from, at, to) in list)
            {
                float dd = Mathf.Min(feet.DistanceTo(from), feet.DistanceTo(at));
                if (dd < best) { best = dd; moved = to; }
            }
            if (moved is { } m) feet = m;
        }
        // First the floors near the authored height; then, for spots authored on the hidden ground under a raised floor
        // (Shadowed), the floor right above them (up to ShadowClearance + a slab).
        foreach (float up in new[] { HeightWindow, ShadowClearance + 0.3f })
            if (Search(feet, world, playable, up) is { } found) return found;
        ok = false;
        return feet;
    }

    static Vector3? Search(Vector3 feet, IReadOnlyList<Box> world, Func<Vector3, int>? playable, float up)
    {
        foreach (float y in FloorsNear(feet, world, up))
        {
            var p = new Vector3(feet.X, y, feet.Z);
            if (Standable(p, world, playable)) return p;
        }
        for (int ring = 1; ring <= (int)(SearchRadius / 0.5f); ring++)
        {
            int n = 8 * ring;
            Vector3? best = null;
            float bestDy = float.MaxValue;
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.Tau / n;
                var q = new Vector3(feet.X + Mathf.Cos(a) * 0.5f * ring, feet.Y, feet.Z + Mathf.Sin(a) * 0.5f * ring);
                foreach (float y in FloorsNear(q, world, up))
                {
                    var p = new Vector3(q.X, y, q.Z);
                    if (!Standable(p, world, playable)) continue;
                    float dy = Mathf.Abs(y - feet.Y);
                    if (dy < bestDy) { bestDy = dy; best = p; }
                    break;
                }
            }
            if (best is { } found) return found;
        }
        return null;
    }

    public static Vector3 Resolve(Vector3 feet, IReadOnlyList<Box> world) => Resolve(feet, world, out _);

    /// <summary>Every standable point (0.25 m grid, the floors within <see cref="HeightWindow"/>) within
    /// <paramref name="radius"/> of <paramref name="feet"/>, nearest first (dev spawn audit).</summary>
    public static List<Vector3> StandableNear(Vector3 feet, IReadOnlyList<Box> world, float radius)
    {
        var list = new List<(float D, Vector3 P)>();
        int n = (int)(radius / 0.25f);
        for (int iz = -n; iz <= n; iz++)
            for (int ix = -n; ix <= n; ix++)
            {
                var q = new Vector3(feet.X + ix * 0.25f, feet.Y, feet.Z + iz * 0.25f);
                float d = new Vector2(ix, iz).Length() * 0.25f;
                if (d > radius) continue;
                foreach (float y in FloorsNear(q, world))
                {
                    var p = new Vector3(q.X, y, q.Z);
                    if (Standable(p, world, _ => -1)) list.Add((d + Mathf.Abs(y - feet.Y), p));
                }
            }
        return list.OrderBy(x => x.D).Select(x => x.P).ToList();
    }

    // Keyed by the collision's content (count, first and last box), not the list instance: a session works on its own
    // copy of the map's boxes (Environments.BuildMap), and it must see the same moves.
    static readonly Dictionary<(int, Box, Box), List<(Vector3 From, Vector3 At, Vector3 To)>> moves = new();

    static (int, Box, Box) KeyOf(IReadOnlyList<Box> world) => (world.Count, world[0], world[world.Count - 1]);

    /// <summary>Registers the spot moves of a map's collision (its authored and resolved positions → the replacement).</summary>
    public static void SetMoves(IReadOnlyList<Box> world, List<(Vector3 From, Vector3 At, Vector3 To)> list)
    {
        if (world.Count == 0) return;
        lock (moves)
        {
            if (list.Count > 0) moves[KeyOf(world)] = list;
            else moves.Remove(KeyOf(world));
        }
    }
}
