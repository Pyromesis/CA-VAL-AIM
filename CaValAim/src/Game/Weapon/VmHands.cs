using Godot;

namespace CaValAim.Game.Weapon;

/// <summary>Curl of one finger: spread (deg, + = toward the thumb) and the three joint flexions (deg).</summary>
public sealed record Finger(float Spread, float C0, float C1, float C2);

/// <summary>
/// Pose of a procedural gloved hand. Hand space (right hand): origin = wrist, -Z = fingers,
/// +Y = back of the hand, -X = thumb side. Thumb: direction + nail-side vector + three flexions.
/// </summary>
public sealed record HandShape(
    Finger Index, Finger Middle, Finger Ring, Finger Little,
    Vector3 ThumbDir, Vector3 ThumbUp, Vector3 ThumbCurl, float ForearmLen = 0.5f);

/// <summary>
/// Where a hand sits on the gun (gun space: grip origin, barrel -Z, +Y up, right-handed layout):
/// wrist position, finger direction, back-of-hand direction and the forearm direction (toward the elbow).
/// </summary>
public sealed record HandPlacement(Vector3 Wrist, Vector3 Fingers, Vector3 Back, Vector3 Elbow, HandShape Shape)
{
    public Transform3D Xform => new(VmMesh.Look(Fingers, Back), Wrist);
    /// <summary>Forearm direction in hand space.</summary>
    public Vector3 ElbowLocal => (Xform.Basis.Inverse() * Elbow).Normalized();
}

/// <summary>
/// Builds a low-poly tactical glove + sleeve as one baked ArrayMesh (4 surfaces: glove, glove pads,
/// sleeve, sleeve accent). <c>mirror</c> builds a left hand.
/// </summary>
public static class VmHands
{
    static Transform3D T(float x, float y, float z) => new(Basis.Identity, new Vector3(x, y, z));
    static Transform3D RX(float deg) => new(new Basis(Vector3.Right, Mathf.DegToRad(deg)), Vector3.Zero);

    /// <param name="elbow">forearm direction in (un-mirrored) hand space.</param>
    public static ArrayMesh Build(HandShape s, bool mirror, Vector3 elbow, Material glove, Material pad, Material sleeve, Material accent)
    {
        var mg = new VmMesh(); var mp = new VmMesh(); var ms = new VmMesh(); var ma = new VmMesh();
        VmOrganicHand.AddHand(mg, mp, s, mirror);
        VmOrganicHand.AddForearm(mg, ms, ma, s.ForearmLen, elbow, mirror);

        var mesh = new ArrayMesh();
        mg.AddTo(mesh, glove);
        mp.AddTo(mesh, pad);
        ms.AddTo(mesh, sleeve);
        ma.AddTo(mesh, accent);
        return mesh;
    }

    /// <summary>Simple spare magazine (for the reload) — straight or curved box mag along -Y.</summary>
    public static ArrayMesh Magazine(Vector3 half, float curveDeg, Material body, Material plate)
    {
        var mb = new VmMesh(); var mp = new VmMesh();
        int n = curveDeg > 0.1f ? 3 : 1;
        var t = Transform3D.Identity;
        float seg = half.Y * 2 / n;
        for (int i = 0; i < n; i++)
        {
            mb.RoundBox(new Vector3(half.X, seg / 2 + 0.002f, half.Z), 0.3f, t * T(0, -seg / 2, 0));
            t = t * T(0, -seg, 0) * RX(curveDeg / n);
        }
        mp.RoundBox(new Vector3(half.X * 1.15f, 0.004f, half.Z * 1.08f), 0.3f, t * T(0, -0.002f, 0));
        var mesh = new ArrayMesh();
        mb.AddTo(mesh, body);
        mp.AddTo(mesh, plate);
        return mesh;
    }

    /// <summary>Brass shell casing along -Z (length len, radius r).</summary>
    public static ArrayMesh Shell(float r, float len, Material brass, Material primer)
    {
        var mb = new VmMesh(); var mp = new VmMesh();
        mb.Tube(0f, r, -len * 0.78f, r * 0.97f, 1, 1, Transform3D.Identity, caps: false, seg: 10);
        mb.Tube(-len * 0.78f, r * 0.97f, -len, r * 0.62f, 1, 1, Transform3D.Identity, caps: true, seg: 10);
        mp.Tube(0.0015f, r * 1.04f, 0f, r * 1.04f, 1, 1, Transform3D.Identity, caps: true, seg: 10);
        var mesh = new ArrayMesh();
        mb.AddTo(mesh, brass);
        mp.AddTo(mesh, primer);
        return mesh;
    }
}
