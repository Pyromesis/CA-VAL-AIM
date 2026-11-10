using System.Globalization;
using System.Threading.Tasks;
using Godot;

namespace CaValAim.Game.Weapon;

/// <summary>
/// Builds the gloved hand as one continuous organic skin. The anatomy (palm, thenar, hypothenar,
/// fingers, thumb and the padded guards) is a smooth union of distance primitives, extracted with
/// surface nets (one welded vertex per surface cell), so no capsule or box silhouette survives and
/// the mesh stays watertight under lighting. Normals come from the field gradient. Results are
/// cached per hand pose.
/// </summary>
static class VmOrganicHand
{
    const float Cell = 0.0022f;

    /// <summary>12 edges of a cube, corners indexed x + 2y + 4z.</summary>
    static readonly int[,] CubeEdges =
    {
        { 0, 1 }, { 0, 2 }, { 0, 4 }, { 1, 3 }, { 1, 5 }, { 2, 3 },
        { 2, 6 }, { 3, 7 }, { 4, 5 }, { 4, 6 }, { 5, 7 }, { 6, 7 },
    };

    sealed class Surface
    {
        public Vector3[] V = Array.Empty<Vector3>();
        public Vector3[] N = Array.Empty<Vector3>();
        public int[] Glove = Array.Empty<int>();
        public int[] Pad = Array.Empty<int>();
    }

    // ---------------- distance primitives ----------------

    readonly struct Prim
    {
        public readonly int Type;      // 0 tapered capsule, 1 ellipsoid, 2 rounded box, 3 oval shield
        public readonly Vector3 A, B;  // endpoints / centre, radii / half extents
        public readonly Vector3 C;     // capsule radii, box rounding or shield (bevel, slope, dome)
        public readonly float K;       // smooth-union radius
        public readonly Vector3 Min, Max;

        public Prim(int type, Vector3 a, Vector3 b, Vector3 c, float k, Vector3 min, Vector3 max)
        {
            Type = type; A = a; B = b; C = c; K = k; Min = min; Max = max;
        }
    }

    static Vector3 VMin(Vector3 a, Vector3 b) =>
        new(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y), Mathf.Min(a.Z, b.Z));

    static Vector3 VMax(Vector3 a, Vector3 b) =>
        new(Mathf.Max(a.X, b.X), Mathf.Max(a.Y, b.Y), Mathf.Max(a.Z, b.Z));

    static Prim Capsule(Vector3 a, Vector3 b, float ra, float rb, float k)
    {
        float r = Mathf.Max(ra, rb) + k + 0.003f;
        var g = new Vector3(r, r, r);
        return new Prim(0, a, b, new Vector3(ra, rb, 0f), k, VMin(a, b) - g, VMax(a, b) + g);
    }

    static Prim Ball(Vector3 c, Vector3 r, float k)
    {
        var g = r + new Vector3(k + 0.003f, k + 0.003f, k + 0.003f);
        return new Prim(1, c, r, Vector3.Zero, k, c - g, c + g);
    }

    static Prim Plate(Vector3 c, Vector3 h, float round, float k)
    {
        var g = h + new Vector3(round + k + 0.003f, round + k + 0.003f, round + k + 0.003f);
        return new Prim(2, c, h, new Vector3(round, 0f, 0f), k, c - g, c + g);
    }

    /// <summary>Oval armour plate: flat-ish top, beveled rim, optional tilt (slope) and crown (dome).</summary>
    static Prim Shield(Vector3 c, Vector3 r, float bevel, float slope, float dome, float k)
    {
        float pad = r.Y + bevel + dome + Mathf.Abs(slope) * r.Z + k + 0.003f;
        float gx = r.X + bevel + k + 0.003f, gz = r.Z + bevel + k + 0.003f;
        return new Prim(3, c, r, new Vector3(bevel, slope, dome), k,
            c - new Vector3(gx, pad, gz), c + new Vector3(gx, pad, gz));
    }

    static float Taper(Vector3 p, Vector3 a, Vector3 b, float ra, float rb)
    {
        var ab = b - a;
        float d = ab.LengthSquared();
        float h = d > 1e-12f ? Mathf.Clamp((p - a).Dot(ab) / d, 0f, 1f) : 0f;
        return (p - (a + ab * h)).Length() - Mathf.Lerp(ra, rb, h);
    }

    static float Ellip(Vector3 p, Vector3 c, Vector3 r)
    {
        var q = new Vector3((p.X - c.X) / r.X, (p.Y - c.Y) / r.Y, (p.Z - c.Z) / r.Z);
        float k0 = q.Length();
        if (k0 < 1e-5f) return -Mathf.Min(r.X, Mathf.Min(r.Y, r.Z));
        var qr = new Vector3(q.X / r.X, q.Y / r.Y, q.Z / r.Z);
        return k0 * (k0 - 1f) / Mathf.Max(qr.Length(), 1e-6f);
    }

    static float Box(Vector3 p, Vector3 c, Vector3 h, float r)
    {
        var q = new Vector3(Mathf.Abs(p.X - c.X), Mathf.Abs(p.Y - c.Y), Mathf.Abs(p.Z - c.Z)) - h
                + new Vector3(r, r, r);
        var outside = new Vector3(Mathf.Max(q.X, 0f), Mathf.Max(q.Y, 0f), Mathf.Max(q.Z, 0f));
        return outside.Length() + Mathf.Min(Mathf.Max(q.X, Mathf.Max(q.Y, q.Z)), 0f) - r;
    }

    static float ShieldSdf(Vector3 p, Vector3 c, Vector3 r, float bevel, float slope, float dome)
    {
        float x = (p.X - c.X) / r.X;
        float z = (p.Z - c.Z) / r.Z;
        float rr = x * x + z * z;
        float yc = c.Y + slope * (p.Z - c.Z) + dome * Mathf.Max(0f, 1f - rr);
        float dy = Mathf.Abs(p.Y - yc) - r.Y;
        float e = (Mathf.Sqrt(rr) - 1f) * Mathf.Min(r.X, r.Z);
        return Mathf.Max(e, dy) - bevel;
    }

    static float Eval(in Prim p, Vector3 q) => p.Type switch
    {
        0 => Taper(q, p.A, p.B, p.C.X, p.C.Y),
        1 => Ellip(q, p.A, p.B),
        2 => Box(q, p.A, p.B, p.C.X),
        _ => ShieldSdf(q, p.A, p.B, p.C.X, p.C.Y, p.C.Z),
    };

    static float SMin(float a, float b, float k)
    {
        float h = Mathf.Clamp(0.5f + 0.5f * (b - a) / k, 0f, 1f);
        return Mathf.Lerp(b, a, h) - k * h * (1f - h);
    }

    static float Group(Prim[] ps, Vector3 q)
    {
        float d = 1f;
        for (int i = 0; i < ps.Length; i++)
        {
            ref readonly var p = ref ps[i];
            if (q.X <= p.Min.X || q.X >= p.Max.X || q.Y <= p.Min.Y || q.Y >= p.Max.Y ||
                q.Z <= p.Min.Z || q.Z >= p.Max.Z) continue;
            d = SMin(d, Eval(p, q), p.K);
        }
        return d;
    }

    sealed class Field
    {
        public Prim[] Palm = Array.Empty<Prim>();
        public Prim[][] Fingers = Array.Empty<Prim[]>();
        public Prim[] Thumb = Array.Empty<Prim>();
        public Prim[] Pads = Array.Empty<Prim>();
        public Vector3 Min, Max;

        public float Base(Vector3 q)
        {
            float d = Group(Palm, q);
            float fingers = 1f;
            foreach (var f in Fingers) fingers = Mathf.Min(fingers, Group(f, q));
            d = SMin(d, fingers, 0.0045f);
            return SMin(d, Group(Thumb, q), 0.0055f);
        }

        public float Sdf(Vector3 q) => SMin(Base(q), Group(Pads, q), 0.0022f);
        public bool IsPad(Vector3 q) => Group(Pads, q) < Base(q) - 0.0004f;
    }

    // ---------------- pose -> field ----------------

    static Transform3D T(Vector3 p) => new(Basis.Identity, p);
    static Transform3D T(float x, float y, float z) => new(Basis.Identity, new Vector3(x, y, z));
    static Transform3D RX(float deg) => new(new Basis(Vector3.Right, Mathf.DegToRad(deg)), Vector3.Zero);
    static Transform3D RY(float deg) => new(new Basis(Vector3.Up, Mathf.DegToRad(deg)), Vector3.Zero);

    static Prim[] FingerField(Finger f, Vector3 b, float[] len, float[] rad, List<Prim> pads)
    {
        var list = new List<Prim>();
        var pt = new Vector3[4];
        var rr = new float[4];
        var up = new Vector3[3];
        var dir = new Vector3[3];
        var t = T(b) * RY(f.Spread) * RX(-f.C0);
        pt[0] = b;
        rr[0] = rad[0];
        for (int i = 0; i < 3; i++)
        {
            dir[i] = (t.Basis * Vector3.Forward).Normalized();
            up[i] = (t.Basis * Vector3.Up).Normalized();
            pt[i + 1] = pt[i] + dir[i] * len[i];
            rr[i + 1] = i == 2 ? rad[2] * 0.78f : rad[i + 1];
            float next = i == 0 ? f.C1 : i == 1 ? f.C2 : 0f;
            t = t * T(0, 0, -len[i]) * RX(-next);
        }

        for (int i = 0; i < 3; i++) list.Add(Capsule(pt[i], pt[i + 1], rr[i], rr[i + 1], 0.0022f));
        // The finger root continues into the palm as a taper (no separate metacarpal ball: four balls
        // were exactly what made the knuckle row read as marbles).
        list.Add(Capsule(pt[0] - dir[0] * 0.010f, pt[0], rad[0] * 1.04f, rad[0], 0.0035f));
        for (int i = 1; i < 3; i++) list.Add(Ball(pt[i], new Vector3(rr[i], rr[i], rr[i]) * 1.02f, 0.0025f));
        list.Add(Ball(pt[3], new Vector3(rr[3], rr[3], rr[3]), 0.0025f));

        // Padded ridge on the back of each phalanx, part of the same skin.
        for (int i = 0; i < 3; i++)
        {
            float off = rr[i] * 0.80f;
            var a = pt[i] + up[i] * off - dir[i] * 0.0015f;
            var c = pt[i + 1] + up[i] * off + dir[i] * 0.0010f;
            pads.Add(Capsule(a, c, 0.0032f, 0.0028f, 0.0016f));
        }
        return list.ToArray();
    }

    static Prim[] ThumbField(HandShape s, List<Prim> pads)
    {
        var list = new List<Prim>();
        var pt = new Vector3[4];
        var rr = new float[4];
        var up = new Vector3[3];
        var dir = new Vector3[3];
        float[] len = { 0.036f, 0.027f, 0.023f };
        float[] rad = { 0.0134f, 0.0118f, 0.0104f };
        var t = T(-0.026f, -0.008f, -0.022f) * new Transform3D(VmMesh.Look(s.ThumbDir, s.ThumbUp), Vector3.Zero);
        t *= RX(-s.ThumbCurl.X);
        float[] curl = { s.ThumbCurl.Y, s.ThumbCurl.Z, 0f };
        pt[0] = new Vector3(-0.026f, -0.008f, -0.022f);
        rr[0] = rad[0];
        for (int i = 0; i < 3; i++)
        {
            dir[i] = (t.Basis * Vector3.Forward).Normalized();
            up[i] = (t.Basis * Vector3.Up).Normalized();
            pt[i + 1] = pt[i] + dir[i] * len[i];
            rr[i + 1] = i == 2 ? rad[2] * 0.80f : rad[i + 1];
            t = t * T(0, 0, -len[i]) * RX(-curl[i]);
        }
        for (int i = 0; i < 3; i++) list.Add(Capsule(pt[i], pt[i + 1], rr[i], rr[i + 1], 0.0030f));
        list.Add(Ball(pt[1], new Vector3(rr[1], rr[1], rr[1]) * 1.04f, 0.0025f));
        list.Add(Ball(pt[2], new Vector3(rr[2], rr[2], rr[2]) * 1.02f, 0.0025f));
        list.Add(Ball(pt[3], new Vector3(rr[3], rr[3], rr[3]), 0.0025f));

        // Nail-side pad across the distal phalanges.
        for (int i = 1; i < 3; i++)
        {
            float off = rr[i] * 0.78f;
            var a = pt[i] + up[i] * off - dir[i] * 0.001f;
            var c = pt[i + 1] + up[i] * off + dir[i] * 0.001f;
            pads.Add(Capsule(a, c, 0.0030f, 0.0027f, 0.0016f));
        }
        return list.ToArray();
    }

    static Field FieldFor(HandShape s)
    {
        var palm = new List<Prim>
        {
            // Palm core: a full, slightly arched block between the wrist and the metacarpal heads.
            Ball(new Vector3(0.001f, -0.0015f, -0.042f), new Vector3(0.0305f, 0.0135f, 0.041f), 0.008f),
            Ball(new Vector3(0f, -0.001f, 0.004f), new Vector3(0.0245f, 0.0155f, 0.022f), 0.008f),
            // Thenar (thumb) and hypothenar (little finger) eminences: the palm's two muscle masses.
            Capsule(new Vector3(-0.024f, -0.006f, -0.016f), new Vector3(-0.031f, -0.009f, -0.052f),
                0.0165f, 0.0130f, 0.008f),
            Capsule(new Vector3(0.028f, -0.003f, -0.018f), new Vector3(0.031f, -0.002f, -0.058f),
                0.0110f, 0.0095f, 0.008f),
            // Ulnar border: wrist -> hypothenar -> little-finger metacarpal, one continuous curve (the
            // old edge was a straight vertical slab with a hard corner at the little finger).
            Capsule(new Vector3(0.022f, -0.002f, 0.010f), new Vector3(0.030f, -0.002f, -0.030f),
                0.0115f, 0.0125f, 0.008f),
            Capsule(new Vector3(0.030f, -0.002f, -0.056f), new Vector3(0.0278f, -0.001f, -0.084f),
                0.0095f, 0.0082f, 0.006f),
            // Ulnar web: fills the notch where the little finger meets the hypothenar mass.
            Ball(new Vector3(0.0258f, 0.0025f, -0.0825f), new Vector3(0.0115f, 0.0068f, 0.0100f), 0.0045f),
            // Domed back of the hand, longest along the metacarpals, plus the knuckle ridge.
            Ball(new Vector3(0f, 0.0045f, -0.058f), new Vector3(0.0305f, 0.0115f, 0.033f), 0.007f),
            Capsule(new Vector3(-0.031f, 0.0065f, -0.0885f), new Vector3(0.030f, 0.0060f, -0.0870f),
                0.0055f, 0.0050f, 0.005f),
            // Volar pads: the heel of the palm and the thenar cushion.
            Ball(new Vector3(0f, -0.008f, -0.010f), new Vector3(0.025f, 0.010f, 0.018f), 0.007f),
            Ball(new Vector3(-0.018f, -0.009f, -0.040f), new Vector3(0.015f, 0.010f, 0.020f), 0.006f),
        };

        var pads = new List<Prim>();
        var fingers = new List<Prim[]>
        {
            FingerField(s.Index, new Vector3(-0.0285f, 0f, -0.092f), new[] { 0.040f, 0.025f, 0.021f },
                new[] { 0.0098f, 0.0093f, 0.0088f }, pads),
            FingerField(s.Middle, new Vector3(-0.0095f, 0.001f, -0.095f), new[] { 0.044f, 0.028f, 0.022f },
                new[] { 0.0102f, 0.0096f, 0.0090f }, pads),
            FingerField(s.Ring, new Vector3(0.0095f, 0f, -0.092f), new[] { 0.041f, 0.026f, 0.021f },
                new[] { 0.0097f, 0.0092f, 0.0087f }, pads),
            FingerField(s.Little, new Vector3(0.0275f, -0.001f, -0.085f), new[] { 0.032f, 0.020f, 0.018f },
                new[] { 0.0087f, 0.0082f, 0.0078f }, pads),
        };
        var thumb = ThumbField(s, pads);

        // Back-of-hand armour: a rigid oval shield with a flat top and a beveled rim, sitting on the
        // metacarpal dome. Wider than long (1.3:1), like a real glove plate, not a round medallion.
        pads.Add(Shield(new Vector3(0.000f, 0.0135f, -0.058f), new Vector3(0.0258f, 0.0022f, 0.0196f),
            0.0014f, -0.035f, 0f, 0.0050f));

        // Knuckle guard: one continuous low oval across the metacarpal heads, plus a short ulnar
        // extension that ties the plate into the little-finger root (the old gap read as a hole).
        pads.Add(Ball(new Vector3(0.000f, 0.0108f, -0.0872f), new Vector3(0.0308f, 0.0034f, 0.0095f), 0.0030f));
        pads.Add(Capsule(new Vector3(0.0180f, 0.0102f, -0.0870f), new Vector3(0.0292f, 0.0094f, -0.0845f),
            0.0034f, 0.0030f, 0.0025f));

        Vector3 min = new(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new(float.MinValue, float.MinValue, float.MinValue);
        void Bounds(Prim p) { min = VMin(min, p.Min); max = VMax(max, p.Max); }
        foreach (var p in palm) Bounds(p);
        foreach (var f in fingers) foreach (var p in f) Bounds(p);
        foreach (var p in thumb) Bounds(p);
        foreach (var p in pads) Bounds(p);

        return new Field
        {
            Palm = palm.ToArray(), Fingers = fingers.ToArray(), Thumb = thumb, Pads = pads.ToArray(),
            Min = min - new Vector3(0.012f, 0.012f, 0.012f), Max = max + new Vector3(0.012f, 0.012f, 0.012f),
        };
    }

    // ---------------- mesh extraction ----------------

    static readonly Dictionary<string, Surface> cache = new();

    static string Key(HandShape s)
    {
        static string F(float f) => f.ToString("0.0000", CultureInfo.InvariantCulture);
        static string G(Finger f) => $"{F(f.Spread)},{F(f.C0)},{F(f.C1)},{F(f.C2)}";
        return $"{G(s.Index)}|{G(s.Middle)}|{G(s.Ring)}|{G(s.Little)}|" +
               $"{F(s.ThumbDir.X)},{F(s.ThumbDir.Y)},{F(s.ThumbDir.Z)}|" +
               $"{F(s.ThumbUp.X)},{F(s.ThumbUp.Y)},{F(s.ThumbUp.Z)}|" +
               $"{F(s.ThumbCurl.X)},{F(s.ThumbCurl.Y)},{F(s.ThumbCurl.Z)}";
    }

    static Surface SurfaceFor(HandShape s)
    {
        string key = Key(s);
        if (cache.TryGetValue(key, out var m)) return m;
        m = Extract(FieldFor(s));
        cache[key] = m;
        return m;
    }

    public static void AddHand(VmMesh glove, VmMesh pad, HandShape s, bool mirror)
    {
        var surf = SurfaceFor(s);
        var t = mirror ? Transform3D.Identity.Scaled(new Vector3(-1, 1, 1)) : Transform3D.Identity;
        glove.Add(surf.V, surf.N, surf.Glove, t, fixWinding: false);
        pad.Add(surf.V, surf.N, surf.Pad, t, fixWinding: false);
    }

    static Surface Extract(Field f)
    {
        const float h = Cell;
        int nx = Mathf.CeilToInt((f.Max.X - f.Min.X) / h) + 1;
        int ny = Mathf.CeilToInt((f.Max.Y - f.Min.Y) / h) + 1;
        int nz = Mathf.CeilToInt((f.Max.Z - f.Min.Z) / h) + 1;
        int cx = nx - 1, cy = ny - 1, cz = nz - 1;
        if (cx < 2 || cy < 2 || cz < 2) return new Surface();
        var vals = new float[nx * ny * nz];
        Parallel.For(0, nz, z =>
        {
            float pz = f.Min.Z + z * h;
            int zo = nx * ny * z;
            for (int y = 0; y < ny; y++)
            {
                float py = f.Min.Y + y * h;
                int yo = zo + nx * y;
                for (int x = 0; x < nx; x++)
                    vals[yo + x] = f.Sdf(new Vector3(f.Min.X + x * h, py, pz));
            }
        });

        // Surface nets: one vertex per cell that straddles the isosurface, placed on the average of its
        // edge crossings and projected back onto the SDF. Quads span the sign-changing grid edges, so
        // neighbouring cells always share an edge: no T-junctions and no pinholes under lighting.
        var cell = new int[cx * cy * cz];
        Array.Fill(cell, -1);
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var padVert = new List<bool>();
        var d = new float[8];
        var cp = new Vector3[8];

        for (int z = 0; z < cz; z++)
        for (int y = 0; y < cy; y++)
        for (int x = 0; x < cx; x++)
        {
            bool anyIn = false, anyOut = false;
            for (int k = 0; k < 8; k++)
            {
                int gx = x + (k & 1), gy = y + ((k >> 1) & 1), gz = z + ((k >> 2) & 1);
                d[k] = vals[gx + nx * (gy + ny * gz)];
                cp[k] = new Vector3(f.Min.X + gx * h, f.Min.Y + gy * h, f.Min.Z + gz * h);
                if (d[k] < 0f) anyIn = true; else anyOut = true;
            }
            if (!anyIn || !anyOut) continue;

            var sum = Vector3.Zero;
            int crossings = 0;
            for (int e = 0; e < CubeEdges.GetLength(0); e++)
            {
                int a = CubeEdges[e, 0], b = CubeEdges[e, 1];
                if ((d[a] < 0f) == (d[b] < 0f)) continue;
                float t = d[a] / (d[a] - d[b]);
                sum += cp[a] + (cp[b] - cp[a]) * t;
                crossings++;
            }
            if (crossings == 0) continue;

            var v = sum / crossings;
            for (int it = 0; it < 3; it++)
            {
                float dv = f.Sdf(v);
                if (Mathf.Abs(dv) < 1e-5f) break;
                var g = Gradient(f, v, h);
                v -= g * dv;
                v = new Vector3(
                    Mathf.Clamp(v.X, f.Min.X + x * h, f.Min.X + (x + 1) * h),
                    Mathf.Clamp(v.Y, f.Min.Y + y * h, f.Min.Y + (y + 1) * h),
                    Mathf.Clamp(v.Z, f.Min.Z + z * h, f.Min.Z + (z + 1) * h));
            }
            int vi = verts.Count;
            verts.Add(v);
            norms.Add(Gradient(f, v, h));
            padVert.Add(f.IsPad(v));
            cell[x + cx * (y + cy * z)] = vi;
        }

        var gloveIdx = new List<int>();
        var padIdx = new List<int>();
        int CellAt(int x, int y, int z) => cell[x + cx * (y + cy * z)];

        void Quad(int a, int b, int c, int d)
        {
            if (a < 0 || b < 0 || c < 0 || d < 0) return;
            var pa = verts[a]; var pb = verts[b]; var pc = verts[c]; var pd = verts[d];
            var fn = (pb - pa).Cross(pc - pa) + (pc - pa).Cross(pd - pa);
            var avg = norms[a] + norms[b] + norms[c] + norms[d];
            if (fn.Dot(avg) > 0f) (b, d) = (d, b);
            int pads = (padVert[a] ? 1 : 0) + (padVert[b] ? 1 : 0) + (padVert[c] ? 1 : 0) + (padVert[d] ? 1 : 0);
            var dst = pads >= 3 ? padIdx : gloveIdx;
            dst.Add(a); dst.Add(b); dst.Add(c);
            dst.Add(a); dst.Add(c); dst.Add(d);
        }

        // One quad per sign-changing grid edge, joining its four surrounding cells.
        for (int z = 1; z < cz; z++)
        for (int y = 1; y < cy; y++)
        for (int x = 0; x < cx; x++)
        {
            int i0 = x + nx * (y + ny * z), i1 = (x + 1) + nx * (y + ny * z);
            if ((vals[i0] < 0f) == (vals[i1] < 0f)) continue;
            Quad(CellAt(x, y - 1, z - 1), CellAt(x, y, z - 1), CellAt(x, y, z), CellAt(x, y - 1, z));
        }
        for (int z = 1; z < cz; z++)
        for (int y = 0; y < cy; y++)
        for (int x = 1; x < cx; x++)
        {
            int i0 = x + nx * (y + ny * z), i1 = x + nx * ((y + 1) + ny * z);
            if ((vals[i0] < 0f) == (vals[i1] < 0f)) continue;
            Quad(CellAt(x - 1, y, z - 1), CellAt(x, y, z - 1), CellAt(x, y, z), CellAt(x - 1, y, z));
        }
        for (int z = 0; z < cz; z++)
        for (int y = 1; y < cy; y++)
        for (int x = 1; x < cx; x++)
        {
            int i0 = x + nx * (y + ny * z), i1 = x + nx * (y + ny * (z + 1));
            if ((vals[i0] < 0f) == (vals[i1] < 0f)) continue;
            Quad(CellAt(x - 1, y - 1, z), CellAt(x, y - 1, z), CellAt(x, y, z), CellAt(x - 1, y, z));
        }

        var surf = new Surface { V = verts.ToArray(), N = norms.ToArray(), Glove = gloveIdx.ToArray(), Pad = padIdx.ToArray() };
        if (VmTest.Enabled) Dump(surf, f, h);
        return surf;
    }

    static void Dump(Surface s, Field f, float h)
    {
        int tri = (s.Glove.Length + s.Pad.Length) / 3;
        var edges = new Dictionary<long, int>();
        var dirs = new Dictionary<(int, int, int), int>();
        var cents = new Dictionary<(int, int, int), int>();
        int neg = 0, pos = 0, flat = 0, degen = 0, dup = 0, coincident = 0;
        int n = s.V.Length;
        for (int pass = 0; pass < 2; pass++)
        {
            var ind = pass == 0 ? s.Glove : s.Pad;
            for (int i = 0; i + 2 < ind.Length; i += 3)
            {
                int a = ind[i], b = ind[i + 1], c = ind[i + 2];
                var pa = s.V[a]; var pb = s.V[b]; var pc = s.V[c];
                var fn = (pb - pa).Cross(pc - pa);
                if (fn.LengthSquared() < 1e-12f) { degen++; continue; }
                var avg = s.N[a] + s.N[b] + s.N[c];
                float d = fn.Normalized().Dot(avg.Normalized());
                if (d < -0.2f) neg++; else if (d > 0.2f) pos++; else flat++;
                long E(int x, int y) => x < y ? (long)x * n + y : (long)y * n + x;
                foreach (var (x, y) in new[] { (a, b), (b, c), (c, a) })
                {
                    long k = E(x, y);
                    edges[k] = edges.TryGetValue(k, out int v) ? v + 1 : 1;
                }
                var sorted = (Mathf.Min(a, Mathf.Min(b, c)), a + b + c - Mathf.Min(a, Mathf.Min(b, c)) - Mathf.Max(a, Mathf.Max(b, c)), Mathf.Max(a, Mathf.Max(b, c)));
                if (!dirs.TryAdd(sorted, 1)) dup++;
                var cen = (pa + pb + pc) / 3f;
                var ck = ((int)Mathf.Round(cen.X * 10000f), (int)Mathf.Round(cen.Y * 10000f), (int)Mathf.Round(cen.Z * 10000f));
                if (!cents.TryAdd(ck, 1)) coincident++;
            }
        }
        int e1 = 0, e2 = 0, e3 = 0;
        foreach (var v in edges.Values) { if (v == 1) e1++; else if (v == 2) e2++; else e3++; }

        // Weld by position (1e-5 m) to tell a failed dedup key apart from genuinely open geometry.
        var pmap = new Dictionary<(int, int, int), int>();
        var remap = new int[n];
        for (int i = 0; i < n; i++)
        {
            var p = s.V[i];
            var k = ((int)Mathf.Round(p.X * 100000f), (int)Mathf.Round(p.Y * 100000f), (int)Mathf.Round(p.Z * 100000f));
            if (!pmap.TryGetValue(k, out int w)) { w = pmap.Count; pmap[k] = w; }
            remap[i] = w;
        }
        var wedges = new Dictionary<long, int>();
        int wn = pmap.Count;
        for (int pass = 0; pass < 2; pass++)
        {
            var ind = pass == 0 ? s.Glove : s.Pad;
            for (int i = 0; i + 2 < ind.Length; i += 3)
            {
                int a = remap[ind[i]], b = remap[ind[i + 1]], c = remap[ind[i + 2]];
                long E(int x, int y) => x < y ? (long)x * wn + y : (long)y * wn + x;
                foreach (var (x, y) in new[] { (a, b), (b, c), (c, a) })
                {
                    long k = E(x, y);
                    wedges[k] = wedges.TryGetValue(k, out int v) ? v + 1 : 1;
                }
            }
        }
        int we1 = 0, we2 = 0, we3 = 0;
        foreach (var v in wedges.Values) { if (v == 1) we1++; else if (v == 2) we2++; else we3++; }

        // Connected components over shared edges.
        var parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(int x, int y) { x = Find(x); y = Find(y); if (x != y) parent[y] = x; }
        foreach (var (k, _) in edges)
        {
            long a = k / n, b = k % n;
            Union((int)a, (int)b);
        }
        var sizes = new Dictionary<int, int>();
        for (int i = 0; i < n; i++) { int r = Find(i); sizes[r] = sizes.TryGetValue(r, out int v) ? v + 1 : 1; }
        var big = sizes.Values.OrderByDescending(v => v).Take(4).ToArray();

        GD.Print($"[hand] v={n} tri={tri} degen={degen} dup={dup} coincident={coincident} " +
                 $"edges1={e1} edges2={e2} edges3+={e3} faceNormal/vertexNormal neg={neg} pos={pos} flat={flat} cell={h}");
        GD.Print($"[hand] weld v={wn} edges1={we1} edges2={we2} edges3+={we3} parts={sizes.Count} sizes=[{string.Join(",", big)}]");
    }

    static Vector3 Gradient(Field f, Vector3 p, float h)
    {
        float e = h * 0.55f;
        float dx = f.Sdf(new Vector3(p.X + e, p.Y, p.Z)) - f.Sdf(new Vector3(p.X - e, p.Y, p.Z));
        float dy = f.Sdf(new Vector3(p.X, p.Y + e, p.Z)) - f.Sdf(new Vector3(p.X, p.Y - e, p.Z));
        float dz = f.Sdf(new Vector3(p.X, p.Y, p.Z + e)) - f.Sdf(new Vector3(p.X, p.Y, p.Z - e));
        var g = new Vector3(dx, dy, dz);
        return g.LengthSquared() > 1e-16f ? g.Normalized() : Vector3.Up;
    }

    // ---------------- forearm + cuff ----------------

    public static void AddForearm(VmMesh glove, VmMesh sleeve, VmMesh accent, float length, Vector3 elbow, bool mirror)
    {
        length = Mathf.Max(0.20f, length);
        if (elbow.LengthSquared() < 1e-6f) elbow = new Vector3(0.35f, -0.45f, 0.82f);
        var fa = new Transform3D(VmMesh.Look(-elbow, Mathf.Abs(elbow.Y) > 0.95f ? Vector3.Forward : Vector3.Up),
            Vector3.Zero);
        var t = mirror ? VmMesh.Mirror(fa) : fa;

        // Wrist -> glove gauntlet -> raised cuff lip -> 14 mm accent strap -> bloused sleeve -> forearm
        // belly -> elbow taper. Radii are anatomically narrow at the wrist; the sleeve blouses just past the
        // cuff, swells at the forearm belly and loses girth toward the elbow instead of staying a straight tube.
        var key = new List<(float T, float R)>
        {
            (-0.012f, 0.0196f), (0.002f, 0.0202f), (0.016f, 0.0209f), (0.030f, 0.0216f),
            (0.042f, 0.0222f), (0.0445f, 0.0250f), (0.046f, 0.0268f), (0.049f, 0.0262f),
            (0.056f, 0.0266f), (0.0585f, 0.0300f), (0.060f, 0.0298f), (0.061f, 0.0284f),
        };
        const float strapStart = 0.0455f, strapEnd = 0.060f;
        float span = Mathf.Max(0.10f, length - strapEnd);
        foreach (var (u, r) in new[]
                 {
                     (0.040f, 0.0306f), (0.090f, 0.0334f), (0.150f, 0.0342f), (0.240f, 0.0346f),
                     (0.340f, 0.0354f), (0.440f, 0.0366f), (0.550f, 0.0372f), (0.650f, 0.0370f),
                     (0.750f, 0.0360f), (0.840f, 0.0346f), (0.920f, 0.0326f), (1f, 0.0300f),
                 })
            key.Add((strapEnd + u * span, r));

        float KeyRadius(float z)
        {
            if (z <= key[0].T) return key[0].R;
            for (int i = 0; i < key.Count - 1; i++)
            {
                var (ta, ra) = key[i];
                var (tb, rb) = key[i + 1];
                if (z > tb) continue;
                return Mathf.Lerp(ra, rb, Mathf.SmoothStep(0f, 1f, (z - ta) / Mathf.Max(1e-5f, tb - ta)));
            }
            return key[^1].R;
        }

        const int seg = 24;
        const float sx = 1.16f, sy = 0.86f;
        const float ringStep = 0.0055f;
        var prof = new List<(float T, float R)>();
        for (float z = key[0].T; z < length - 0.002f; z += ringStep) prof.Add((z, KeyRadius(z)));
        prof.Add((length, KeyRadius(length)));

        // Cloth: two soft wrinkle frequencies travelling down the sleeve, strongest just past the cuff and
        // fading into the elbow, plus a slow angular wobble so the cross-section is not a perfect lathe.
        var verts = new List<Vector3>(prof.Count * (seg + 1));
        var centers = new List<Vector3>(prof.Count);
        for (int i = 0; i < prof.Count; i++)
        {
            float z = prof[i].T;
            float u = Mathf.Clamp((z - strapEnd) / span, 0f, 1f);
            float env = Mathf.SmoothStep(0.03f, 0.16f, u) * Mathf.SmoothStep(1f, 0.88f, u);
            float fold = 0.60f * Mathf.Sin(u * 24.0f + 0.6f) + 0.40f * Mathf.Sin(u * 13.0f + 2.4f);
            float r = prof[i].R * (1f + 0.070f * fold * env);
            var ctr = new Vector3(0.005f * u * u, -0.013f * u * u, z);
            centers.Add(ctr);
            for (int j = 0; j <= seg; j++)
            {
                float a = j / (float)seg * Mathf.Tau;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float wob = 0.016f * Mathf.Sin(3f * a) + 0.009f * Mathf.Sin(5f * a + 1.7f);
                float rr = r * (1f + wob * env * (0.55f + 0.45f * Mathf.Max(0f, -s)));
                verts.Add(ctr + new Vector3(c * rr * sx, s * rr * sy, 0f));
            }
        }

        // Numeric normals from the sampled surface: the folds have to shade, not just displace.
        var norms = new List<Vector3>(verts.Count);
        for (int i = 0; i < prof.Count; i++)
        {
            int ip = Mathf.Min(i + 1, prof.Count - 1), im = Mathf.Max(i - 1, 0);
            for (int j = 0; j <= seg; j++)
            {
                int jp = (j + 1) % seg, jm = (j - 1 + seg) % seg;
                var du = verts[ip * (seg + 1) + j] - verts[im * (seg + 1) + j];
                var dv = verts[i * (seg + 1) + jp] - verts[i * (seg + 1) + jm];
                var n = dv.Cross(du);
                norms.Add(n.LengthSquared() > 1e-14f ? n.Normalized() : Vector3.Up);
            }
        }

        var gi = new List<int>();
        var si = new List<int>();
        var ai = new List<int>();
        for (int i = 0; i < prof.Count - 1; i++)
        {
            float mid = (prof[i].T + prof[i + 1].T) * 0.5f;
            var dst = mid < strapStart ? gi : mid < strapEnd ? ai : si;
            for (int j = 0; j < seg; j++)
            {
                int k = i * (seg + 1) + j;
                dst.Add(k); dst.Add(k + seg + 1); dst.Add(k + 1);
                dst.Add(k + 1); dst.Add(k + seg + 1); dst.Add(k + seg + 2);
            }
        }

        // Caps: the start is buried in the wrist, the end closes the sleeve far behind the camera.
        foreach (var (ring, dir, dst) in new[]
                 {
                     (0, -1f, gi), (prof.Count - 1, 1f, si),
                 })
        {
            int c0 = verts.Count;
            var ctr = centers[ring];
            int first = verts.Count + 1;
            verts.Add(ctr); norms.Add(new Vector3(0, 0, dir));
            for (int j = 0; j <= seg; j++)
            {
                var p = verts[ring * (seg + 1) + j];
                verts.Add(p); norms.Add(new Vector3(0, 0, dir));
            }
            for (int j = 0; j < seg; j++) { dst.Add(c0); dst.Add(first + j); dst.Add(first + j + 1); }
        }

        glove.Add(verts.ToArray(), norms.ToArray(), gi.ToArray(), t);
        accent.Add(verts.ToArray(), norms.ToArray(), ai.ToArray(), t);
        sleeve.Add(verts.ToArray(), norms.ToArray(), si.ToArray(), t);

        // Buckle on the strap: a raised plate with a dark keeper bar. It sits proud of the oxblood band.
        accent.RoundBox(new Vector3(0.0050f, 0.0024f, 0.0045f), 0.35f, t * T(0f, 0.0250f, 0.0525f));
        glove.RoundBox(new Vector3(0.0026f, 0.0013f, 0.0020f), 0.45f, t * T(0f, 0.0270f, 0.0525f));
    }
}
