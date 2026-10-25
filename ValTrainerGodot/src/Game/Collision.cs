using System.Runtime.CompilerServices;
using Godot;

namespace ValTrainer.Game;

/// <summary>Axis-aligned box in world meters (map blockouts are made of these).</summary>
public readonly record struct Box(Vector3 Min, Vector3 Max)
{
    public Vector3 Center => (Min + Max) * 0.5f;
    public Vector3 Size => Max - Min;

    /// <summary>Slab ray test. Returns distance along a normalized direction, or +inf.</summary>
    public float Ray(Vector3 o, Vector3 d)
    {
        float tmin = 0f, tmax = float.PositiveInfinity;
        for (int a = 0; a < 3; a++)
        {
            float oa = o[a], da = d[a], lo = Min[a], hi = Max[a];
            if (Mathf.Abs(da) < 1e-8f)
            {
                if (oa < lo || oa > hi) return float.PositiveInfinity;
                continue;
            }
            float inv = 1f / da, t1 = (lo - oa) * inv, t2 = (hi - oa) * inv;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tmin = Mathf.Max(tmin, t1);
            tmax = Mathf.Min(tmax, t2);
            if (tmin > tmax) return float.PositiveInfinity;
        }
        return tmin;
    }

    /// <summary>Outward normal of the face a point on the surface lies on.</summary>
    public Vector3 NormalAt(Vector3 p)
    {
        var c = Center; var h = Size * 0.5f; var d = p - c;
        var n = new Vector3(d.X / Mathf.Max(h.X, 1e-4f), d.Y / Mathf.Max(h.Y, 1e-4f), d.Z / Mathf.Max(h.Z, 1e-4f));
        var a = n.Abs();
        if (a.X >= a.Y && a.X >= a.Z) return new Vector3(Mathf.Sign(n.X), 0, 0);
        if (a.Y >= a.Z) return new Vector3(0, Mathf.Sign(n.Y), 0);
        return new Vector3(0, 0, Mathf.Sign(n.Z));
    }
}

/// <summary>
/// Uniform XZ broadphase over a collision box list: a 4 m grid built once per list. The extracted real maps
/// carry ~17k boxes and the player/bot queries used to scan every one of them several times per frame; the
/// grid turns each query into a handful of cells. Boxes are inserted into every cell they overlap, so a
/// query never misses one. Falls back to a linear scan when the list is tiny or spread over too many cells
/// (a degenerate/huge world would otherwise allocate an enormous grid).
/// </summary>
sealed class CollisionGrid
{
    public const float CellSize = 4f;
    const int MaxCells = 1 << 16;

    public readonly bool Linear;
    public readonly int Nx, Nz;
    /// <summary>CSR offsets: cell c owns <c>Items[Start[c]..Start[c + 1])</c>.</summary>
    public readonly int[] Start;
    public readonly int[] Items;
    /// <summary>Per-box query stamp, so a box spanning several cells is only tested once per query.</summary>
    public readonly int[] Stamp;
    public int Generation;

    readonly float x0, z0;

    public CollisionGrid(IReadOnlyList<Box> world)
    {
        int n = world.Count;
        Stamp = new int[n];
        if (n < 32)
        {
            Linear = true;
            Start = new[] { 0 };
            Items = Array.Empty<int>();
            return;
        }

        float minX = float.PositiveInfinity, minZ = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxZ = float.NegativeInfinity;
        foreach (var b in world)
        {
            minX = Mathf.Min(minX, b.Min.X); maxX = Mathf.Max(maxX, b.Max.X);
            minZ = Mathf.Min(minZ, b.Min.Z); maxZ = Mathf.Max(maxZ, b.Max.Z);
        }
        // One cell of padding: queries at the exact edge clamp into the grid.
        x0 = minX - CellSize;
        z0 = minZ - CellSize;
        Nx = Math.Max(1, (int)MathF.Ceiling((maxX - minX) / CellSize) + 2);
        Nz = Math.Max(1, (int)MathF.Ceiling((maxZ - minZ) / CellSize) + 2);
        if ((long)Nx * Nz > MaxCells)
        {
            Linear = true;
            Start = new[] { 0 };
            Items = Array.Empty<int>();
            Nx = Nz = 0;
            return;
        }

        var counts = new int[Nx * Nz];
        for (int i = 0; i < n; i++)
        {
            var b = world[i];
            int ix0 = Ix(b.Min.X), ix1 = Ix(b.Max.X), iz0 = Iz(b.Min.Z), iz1 = Iz(b.Max.Z);
            for (int z = iz0; z <= iz1; z++)
                for (int x = ix0; x <= ix1; x++)
                    counts[z * Nx + x]++;
        }

        Start = new int[counts.Length + 1];
        int total = 0;
        for (int c = 0; c < counts.Length; c++) { Start[c] = total; total += counts[c]; }
        Start[counts.Length] = total;

        Items = new int[total];
        var cursor = new int[counts.Length];
        Array.Copy(Start, cursor, counts.Length);
        for (int i = 0; i < n; i++)
        {
            var b = world[i];
            int ix0 = Ix(b.Min.X), ix1 = Ix(b.Max.X), iz0 = Iz(b.Min.Z), iz1 = Iz(b.Max.Z);
            for (int z = iz0; z <= iz1; z++)
                for (int x = ix0; x <= ix1; x++)
                    Items[cursor[z * Nx + x]++] = i;
        }
    }

    public int Ix(float x) => Mathf.Clamp((int)MathF.Floor((x - x0) / CellSize), 0, Nx - 1);
    public int Iz(float z) => Mathf.Clamp((int)MathF.Floor((z - z0) / CellSize), 0, Nz - 1);
    public float CellMinX(int ix) => x0 + ix * CellSize;
    public float CellMinZ(int iz) => z0 + iz * CellSize;

    /// <summary>Starts a query; returns the mark for <see cref="Fresh"/>.</summary>
    public int NextGeneration()
    {
        if (++Generation == int.MaxValue) { Array.Clear(Stamp); Generation = 1; }
        return Generation;
    }

    /// <summary>True the first time box <paramref name="i"/> is seen in this query.</summary>
    public bool Fresh(int i, int generation)
    {
        if (Stamp[i] == generation) return false;
        Stamp[i] = generation;
        return true;
    }
}

/// <summary>
/// Player-vs-world collision (ported from the tuned raylib build): upright box (radius × height) on its
/// feet position, axis-separated resolution so you slide along walls, step-up to <see cref="StepHeight"/>.
/// Jumping (see <see cref="Mover"/>): airborne bodies use a tiny step (<see cref="AirStep"/>: no climbing in the air), a
/// shorter body while crouch-jumping, and <see cref="Ceiling"/> stops a rising head under slabs.
/// </summary>
public static class Collision
{
    public const float PlayerRadius = 0.34f;
    public const float PlayerHeight = 1.8f;
    public const float StepHeight = 0.7f;
    /// <summary>Step allowance while airborne: you land on ledges your feet clear, you never climb them mid-air.</summary>
    public const float AirStep = 0.05f;
    /// <summary>Gravity for jumps and falls (m/s²), see <see cref="MovementTuning.Gravity"/>.</summary>
    public const float Gravity = MovementTuning.Gravity;

    /// <summary>Broadphases per box list (weakly keyed: a finished session's list is collected with its grid).</summary>
    static readonly ConditionalWeakTable<object, CollisionGrid> grids = new();

    static CollisionGrid Grid(IReadOnlyList<Box> world) =>
        grids.GetValue(world, key => new CollisionGrid((IReadOnlyList<Box>)key));

    [ThreadStatic] static List<int>? resolveScratch;

    public static void MoveAndSlide(ref Vector3 feet, ref Vector3 vel, Vector3 delta, IReadOnlyList<Box> world) =>
        MoveAndSlide(ref feet, ref vel, delta, world, StepHeight, PlayerHeight);

    /// <summary>Horizontal move with wall sliding. Boxes whose top is within <paramref name="step"/> of the feet don't block
    /// (step-up / landing on them is the vertical step's job); boxes starting at or above <paramref name="height"/> are overhead.</summary>
    public static void MoveAndSlide(ref Vector3 feet, ref Vector3 vel, Vector3 delta, IReadOnlyList<Box> world, float step, float height)
    {
        feet.X += delta.X;
        if (Resolve(ref feet, world, axisX: true, delta.X, step, height)) vel.X = 0;
        feet.Z += delta.Z;
        if (Resolve(ref feet, world, axisX: false, delta.Z, step, height)) vel.Z = 0;
    }

    static bool Resolve(ref Vector3 feet, IReadOnlyList<Box> world, bool axisX, float moved, float step, float height)
    {
        var grid = Grid(world);
        if (grid.Linear)
        {
            bool linearHit = false;
            foreach (var b in world)
                if (ResolveBox(ref feet, b, axisX, moved, step, height)) linearHit = true;
            return linearHit;
        }

        // Swept footprint: from the position before the move to the position after it, expanded by the body radius.
        float prevX = axisX ? feet.X - moved : feet.X;
        float prevZ = axisX ? feet.Z : feet.Z - moved;
        float minX = Mathf.Min(prevX, feet.X) - PlayerRadius, maxX = Mathf.Max(prevX, feet.X) + PlayerRadius;
        float minZ = Mathf.Min(prevZ, feet.Z) - PlayerRadius, maxZ = Mathf.Max(prevZ, feet.Z) + PlayerRadius;
        int ix0 = grid.Ix(minX), ix1 = grid.Ix(maxX), iz0 = grid.Iz(minZ), iz1 = grid.Iz(maxZ);
        int gen = grid.NextGeneration();

        // Resolve in list order (like the old linear scan) so a corner with overlapping boxes stays deterministic.
        var candidates = resolveScratch ??= new List<int>(64);
        candidates.Clear();
        for (int iz = iz0; iz <= iz1; iz++)
            for (int ix = ix0; ix <= ix1; ix++)
            {
                int cell = iz * grid.Nx + ix;
                for (int k = grid.Start[cell]; k < grid.Start[cell + 1]; k++)
                {
                    int i = grid.Items[k];
                    if (grid.Fresh(i, gen)) candidates.Add(i);
                }
            }
        candidates.Sort();

        bool hit = false;
        foreach (int i in candidates)
            if (ResolveBox(ref feet, world[i], axisX, moved, step, height)) hit = true;
        return hit;
    }

    static bool ResolveBox(ref Vector3 feet, Box b, bool axisX, float moved, float step, float height)
    {
        if (b.Max.Y <= feet.Y + step || b.Min.Y >= feet.Y + height) return false;
        float minX = b.Min.X - PlayerRadius, maxX = b.Max.X + PlayerRadius;
        float minZ = b.Min.Z - PlayerRadius, maxZ = b.Max.Z + PlayerRadius;
        if (feet.X <= minX || feet.X >= maxX || feet.Z <= minZ || feet.Z >= maxZ) return false;
        if (axisX)
        {
            float prev = feet.X - moved;
            feet.X = prev <= minX ? minX : prev >= maxX ? maxX : (feet.X - minX < maxX - feet.X ? minX : maxX);
        }
        else
        {
            float prev = feet.Z - moved;
            feet.Z = prev <= minZ ? minZ : prev >= maxZ ? maxZ : (feet.Z - minZ < maxZ - feet.Z ? minZ : maxZ);
        }
        return true;
    }

    /// <summary>Indices of every box whose XZ extent may overlap the rectangle [x0,x1]×[z0,z1] (broadphase only: the caller
    /// still tests the exact extent). Main thread / one thread at a time (the grid's query stamps are shared).</summary>
    public static void Near(IReadOnlyList<Box> world, float x0, float z0, float x1, float z1, List<int> result)
    {
        result.Clear();
        var grid = Grid(world);
        if (grid.Linear)
        {
            for (int i = 0; i < world.Count; i++) result.Add(i);
            return;
        }
        int ix0 = grid.Ix(x0), ix1 = grid.Ix(x1), iz0 = grid.Iz(z0), iz1 = grid.Iz(z1);
        int gen = grid.NextGeneration();
        for (int iz = iz0; iz <= iz1; iz++)
            for (int ix = ix0; ix <= ix1; ix++)
            {
                int cell = iz * grid.Nx + ix;
                for (int k = grid.Start[cell]; k < grid.Start[cell + 1]; k++)
                {
                    int i = grid.Items[k];
                    if (grid.Fresh(i, gen)) result.Add(i);
                }
            }
    }

    /// <summary>Lowest box bottom in the world (the map's underside). Falling far below it means the body left the map.</summary>
    public static float Bottom(IReadOnlyList<Box> world)
    {
        float y = float.PositiveInfinity;
        foreach (var b in world) y = Mathf.Min(y, b.Min.Y);
        return float.IsFinite(y) ? y : 0f;
    }

    /// <summary>Highest walkable surface under the player's footprint; -infinity when there is no floor.</summary>
    public static float Ground(Vector3 feet, IReadOnlyList<Box> world) => Ground(feet, world, StepHeight);

    /// <summary>Highest surface under the footprint whose top is at most <paramref name="step"/> above the feet; -infinity when there is no floor.</summary>
    public static float Ground(Vector3 feet, IReadOnlyList<Box> world, float step)
    {
        // Real maps can have walkable floors below y=0 (Abyss, Corrode, Summit). Start at -inf so
        // those floors are considered; no box under the footprint means there is no floor (fall),
        // never an implicit plane at 0 that would make the player pop upward over a hole.
        float best = float.NegativeInfinity;
        const float r = PlayerRadius - 0.05f;
        var grid = Grid(world);
        if (grid.Linear)
        {
            foreach (var b in world)
            {
                if (b.Max.Y > feet.Y + step) continue;
                if (feet.X + r < b.Min.X || feet.X - r > b.Max.X || feet.Z + r < b.Min.Z || feet.Z - r > b.Max.Z) continue;
                best = Mathf.Max(best, b.Max.Y);
            }
            return best;
        }

        int ix0 = grid.Ix(feet.X - r), ix1 = grid.Ix(feet.X + r), iz0 = grid.Iz(feet.Z - r), iz1 = grid.Iz(feet.Z + r);
        int gen = grid.NextGeneration();
        for (int iz = iz0; iz <= iz1; iz++)
            for (int ix = ix0; ix <= ix1; ix++)
            {
                int cell = iz * grid.Nx + ix;
                for (int k = grid.Start[cell]; k < grid.Start[cell + 1]; k++)
                {
                    int i = grid.Items[k];
                    if (!grid.Fresh(i, gen)) continue;
                    var b = world[i];
                    if (b.Max.Y > feet.Y + step) continue;
                    if (feet.X + r < b.Min.X || feet.X - r > b.Max.X || feet.Z + r < b.Min.Z || feet.Z - r > b.Max.Z) continue;
                    best = Mathf.Max(best, b.Max.Y);
                }
            }
        return best;
    }

    /// <summary>Lowest box bottom over the player's footprint at or above <paramref name="headY"/> (+inf = open sky).
    /// A rising head stops there (jumping under a slab, a door frame or a ledge).</summary>
    public static float Ceiling(Vector3 feet, IReadOnlyList<Box> world, float headY)
    {
        float best = float.PositiveInfinity;
        const float r = PlayerRadius;
        var grid = Grid(world);
        if (grid.Linear)
        {
            foreach (var b in world)
            {
                if (b.Min.Y < headY - 0.01f || b.Min.Y >= best) continue;
                if (feet.X + r <= b.Min.X || feet.X - r >= b.Max.X || feet.Z + r <= b.Min.Z || feet.Z - r >= b.Max.Z) continue;
                best = b.Min.Y;
            }
            return best;
        }

        int ix0 = grid.Ix(feet.X - r), ix1 = grid.Ix(feet.X + r), iz0 = grid.Iz(feet.Z - r), iz1 = grid.Iz(feet.Z + r);
        int gen = grid.NextGeneration();
        for (int iz = iz0; iz <= iz1; iz++)
            for (int ix = ix0; ix <= ix1; ix++)
            {
                int cell = iz * grid.Nx + ix;
                for (int k = grid.Start[cell]; k < grid.Start[cell + 1]; k++)
                {
                    int i = grid.Items[k];
                    if (!grid.Fresh(i, gen)) continue;
                    var b = world[i];
                    if (b.Min.Y < headY - 0.01f || b.Min.Y >= best) continue;
                    if (feet.X + r <= b.Min.X || feet.X - r >= b.Max.X || feet.Z + r <= b.Min.Z || feet.Z - r >= b.Max.Z) continue;
                    best = b.Min.Y;
                }
            }
        return best;
    }

    /// <summary>Distance to the first box hit along a normalized ray, or +inf.</summary>
    public static float FirstHit(Vector3 o, Vector3 d, IReadOnlyList<Box> world, out int index) =>
        FirstHit(o, d, world, out index, float.PositiveInfinity);

    /// <summary>First hit at or before <paramref name="maxT"/>; +inf when nothing is hit in that span.</summary>
    static float FirstHit(Vector3 o, Vector3 d, IReadOnlyList<Box> world, out int index, float maxT)
    {
        float best = float.PositiveInfinity;
        index = -1;
        var grid = Grid(world);
        if (grid.Linear)
        {
            for (int i = 0; i < world.Count; i++)
            {
                float t = world[i].Ray(o, d);
                if (t < best && t <= maxT) { best = t; index = i; }
            }
            return best;
        }

        // 2D DDA over the XZ grid: only the cells the ray actually crosses are tested.
        int gen = grid.NextGeneration();
        int ix = grid.Ix(o.X), iz = grid.Iz(o.Z);
        int stepX = d.X >= 0f ? 1 : -1, stepZ = d.Z >= 0f ? 1 : -1;
        float tDeltaX = Mathf.Abs(d.X) > 1e-8f ? CollisionGrid.CellSize / Mathf.Abs(d.X) : float.PositiveInfinity;
        float tDeltaZ = Mathf.Abs(d.Z) > 1e-8f ? CollisionGrid.CellSize / Mathf.Abs(d.Z) : float.PositiveInfinity;
        float tMaxX = float.PositiveInfinity, tMaxZ = float.PositiveInfinity;
        if (Mathf.Abs(d.X) > 1e-8f)
        {
            float boundary = grid.CellMinX(d.X > 0f ? ix + 1 : ix);
            tMaxX = Mathf.Max(0f, (boundary - o.X) / d.X);
        }
        if (Mathf.Abs(d.Z) > 1e-8f)
        {
            float boundary = grid.CellMinZ(d.Z > 0f ? iz + 1 : iz);
            tMaxZ = Mathf.Max(0f, (boundary - o.Z) / d.Z);
        }

        while (true)
        {
            int cell = iz * grid.Nx + ix;
            for (int k = grid.Start[cell]; k < grid.Start[cell + 1]; k++)
            {
                int i = grid.Items[k];
                if (!grid.Fresh(i, gen)) continue;
                float t = world[i].Ray(o, d);
                if (t < best && t <= maxT) { best = t; index = i; }
            }

            float next = Mathf.Min(tMaxX, tMaxZ);
            if (index >= 0 && best <= next) break;      // nothing closer can be in a later cell
            if (next > maxT || float.IsPositiveInfinity(next)) break;
            if (tMaxX < tMaxZ) { ix += stepX; tMaxX += tDeltaX; }
            else { iz += stepZ; tMaxZ += tDeltaZ; }
            if (ix < 0 || ix >= grid.Nx || iz < 0 || iz >= grid.Nz) break;
        }
        return best;
    }

    public static float FirstHit(Vector3 o, Vector3 d, IReadOnlyList<Box> world) => FirstHit(o, d, world, out _);

    /// <summary>True if the segment a→b passes through any box.</summary>
    public static bool Blocked(Vector3 a, Vector3 b, IReadOnlyList<Box> world)
    {
        var d = b - a;
        float len = d.Length();
        if (len < 1e-4f) return false;
        return FirstHit(a, d / len, world, out _, len) < len;
    }

    /// <summary>Does a standing body at these feet overlap any wall?</summary>
    public static bool Overlaps(Vector3 feet, IReadOnlyList<Box> world, float r = 0.3f)
    {
        var grid = Grid(world);
        if (grid.Linear)
        {
            foreach (var b in world)
                if (b.Max.Y > feet.Y + StepHeight && b.Min.Y < feet.Y + PlayerHeight &&
                    feet.X > b.Min.X - r && feet.X < b.Max.X + r && feet.Z > b.Min.Z - r && feet.Z < b.Max.Z + r)
                    return true;
            return false;
        }

        int ix0 = grid.Ix(feet.X - r), ix1 = grid.Ix(feet.X + r), iz0 = grid.Iz(feet.Z - r), iz1 = grid.Iz(feet.Z + r);
        int gen = grid.NextGeneration();
        for (int iz = iz0; iz <= iz1; iz++)
            for (int ix = ix0; ix <= ix1; ix++)
            {
                int cell = iz * grid.Nx + ix;
                for (int k = grid.Start[cell]; k < grid.Start[cell + 1]; k++)
                {
                    int i = grid.Items[k];
                    if (!grid.Fresh(i, gen)) continue;
                    var b = world[i];
                    if (b.Max.Y > feet.Y + StepHeight && b.Min.Y < feet.Y + PlayerHeight &&
                        feet.X > b.Min.X - r && feet.X < b.Max.X + r && feet.Z > b.Min.Z - r && feet.Z < b.Max.Z + r)
                        return true;
                }
            }
        return false;
    }
}
