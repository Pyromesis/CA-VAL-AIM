using Godot;
using CaValAim.Core;

namespace CaValAim.Game.Weapon;

/// <summary>
/// Hand-built first-person Vandal and Phantom. The old viewmodels were two generic CC0 FBX rifles with flat
/// materials, so they read as placeholder art up close. These meshes are authored in gun space (grip origin,
/// barrel -Z) with the silhouettes, rails, vents, sights, curved magazines and cut-out stocks of the two rifles.
/// <para>Every part is a flat-shaded mesh surface with a named material slot; <see cref="Viewmodel"/> then swaps
/// those slots for the viewmodel shader's metal / polymer / accent finishes. Keeping the mesh CPU-built means
/// the weapons travel in the DLL and need no new pck assets. The magazine is a separate node so a reload hides
/// it cleanly instead of clipping a box out of the receiver.</para>
/// <para>The bodies use <see cref="VmMesh.ChamferZ"/>: a rectangular loft with the four long edges cut at 45°,
/// so the receiver, handguard and stock catch highlights along every edge instead of reading as one flat slab.
/// The silhouette is still deliberately stylised: the Vandal is the angular, skeleton-stocked rifle and the
/// Phantom the slimmer suppressed one.</para>
/// </summary>
public static class ProceduralRifle
{
    static readonly Transform3D I = Transform3D.Identity;

    static Transform3D T(float x, float y, float z) => new(Basis.Identity, new Vector3(x, y, z));
    static Transform3D RX(float deg) => new(new Basis(Vector3.Right, Mathf.DegToRad(deg)), Vector3.Zero);
    static Transform3D RY(float deg) => new(new Basis(Vector3.Up, Mathf.DegToRad(deg)), Vector3.Zero);
    static Transform3D RZ(float deg) => new(new Basis(Vector3.Back, Mathf.DegToRad(deg)), Vector3.Zero);

    const float Bore = 0.078f;

    sealed class Kit
    {
        public readonly VmMesh Body = new(), Poly = new(), Metal = new(), Accent = new(), Dark = new(), White = new();
        public readonly VmMesh Rubber = new(), Steel = new(), Mag = new(), MagTrim = new();
        public Color BodyCol = new(0.300f, 0.292f, 0.280f);
        public Color PolyCol = new(0.185f, 0.190f, 0.200f);
        public Color AccentCol = new(0.720f, 0.055f, 0.065f);
    }

    public static Node3D Build(WeaponKind kind)
    {
        bool phantom = kind == WeaponKind.Phantom;
        var k = new Kit();
        if (phantom)
        {
            k.BodyCol = new Color(0.290f, 0.310f, 0.350f);
            k.AccentCol = new Color(0.050f, 0.560f, 0.600f);
        }
        else
        {
            k.BodyCol = new Color(0.340f, 0.322f, 0.298f);
        }

        Receiver(k, phantom);
        Handguard(k, phantom);
        BarrelAndMuzzle(k, phantom);
        Sights(k, phantom);
        Stock(k, phantom);
        Grip(k, phantom);
        Magazine(k, phantom);
        Finish(k, phantom);
        return Assemble(k, phantom);
    }

    // ------------------------------------------------------------------ receiver

    static void Receiver(Kit k, bool phantom)
    {
        // Lower + upper receiver as one chamfered body, plus a raised upper shell.
        k.Body.ChamferZ(-0.255f, 0.090f, new Vector2(0.0255f, 0.0315f), new Vector2(0.0275f, 0.0345f), 0.046f, 0.045f, 0.006f);
        k.Body.ChamferZ(-0.252f, 0.076f, new Vector2(0.0225f, 0.0155f), new Vector2(0.0245f, 0.0175f), 0.079f, 0.079f, 0.005f);
        k.Dark.ChamferZ(-0.250f, 0.072f, new Vector2(0.0212f, 0.0022f), new Vector2(0.0232f, 0.0022f), 0.0625f, 0.0625f, 0.001f);

        // Panel seams: short dark lines that follow the receiver taper so they sit on the surface
        // (long strips at a constant x floated up to 1.2 mm off the sloped side).
        foreach (float sx in new[] { 1f, -1f })
        {
            k.Dark.Box(new Vector3(0.0010f, 0.0012f, 0.060f), T(sx * 0.0261f, 0.036f, -0.200f));
            k.Dark.Box(new Vector3(0.0010f, 0.0012f, 0.080f), T(sx * 0.0272f, 0.070f, 0.028f));
        }

        // Ejection port: a recessed dark opening with a steel bolt face and a case deflector.
        k.Dark.Box(new Vector3(0.0016f, 0.0125f, 0.031f), T(0.0282f, 0.070f, -0.068f));
        k.Steel.Box(new Vector3(0.0011f, 0.0090f, 0.024f), T(0.0270f, 0.070f, -0.068f));
        k.Metal.Box(new Vector3(0.0032f, 0.0028f, 0.011f), T(0.0300f, 0.081f, -0.040f) * RZ(-28f));
        k.Dark.Box(new Vector3(0.0012f, 0.0070f, 0.004f), T(0.0292f, 0.070f, -0.086f));

        // Forward assist (right) and bolt catch (left).
        k.Steel.FacetTubeZ(0f, 0.010f, 0.0055f, 0.0055f, 10, 1f, 1f, T(0.0285f, 0.058f, 0.010f) * RY(90));
        k.Dark.FacetTubeZ(-0.001f, 0.001f, 0.0035f, 0.0035f, 10, 1f, 1f, T(0.0390f, 0.058f, 0.010f) * RY(90));
        k.Metal.Box(new Vector3(0.0022f, 0.0080f, 0.0060f), T(-0.0300f, 0.045f, 0.000f));

        // Ambidextrous safety selector.
        foreach (float sx in new[] { 1f, -1f })
        {
            k.Metal.FacetTubeZ(-0.001f, 0.001f, 0.0060f, 0.0060f, 12, 1f, 1f, T(sx * 0.0290f, 0.034f, 0.018f) * RY(90));
            k.Metal.Box(new Vector3(0.0012f, 0.0030f, 0.012f), T(sx * 0.0310f, 0.034f, 0.025f));
            k.Dark.Box(new Vector3(0.0012f, 0.0016f, 0.006f), T(sx * 0.0312f, 0.038f, 0.024f));
        }

        // Mag well flare and a steel magazine funnel.
        k.Body.ChamferZ(-0.202f, -0.132f, new Vector2(0.0300f, 0.0200f), new Vector2(0.0260f, 0.0180f), 0.019f, 0.024f, 0.004f);
        k.Dark.ChamferZ(-0.200f, -0.190f, new Vector2(0.0270f, 0.0160f), new Vector2(0.0260f, 0.0155f), 0.006f, 0.006f, 0.003f);

        // Trigger guard: a real loop tied into the receiver underside (the old bars stopped ~14 mm short
        // and floated under the gun), with an 8 mm bow and a visible trigger blade.
        k.Metal.Box(new Vector3(0.0040f, 0.0430f, 0.0035f), T(0f, 0.0000f, -0.082f));
        k.Metal.Box(new Vector3(0.0040f, 0.0430f, 0.0035f), T(0f, 0.0000f, -0.012f));
        k.Metal.Box(new Vector3(0.0040f, 0.0035f, 0.0350f), T(0f, -0.0360f, -0.047f));
        k.Metal.Box(new Vector3(0.0040f, 0.0035f, 0.0140f), T(0f, -0.0310f, -0.073f) * RX(26f));
        k.Metal.Box(new Vector3(0.0040f, 0.0035f, 0.0140f), T(0f, -0.0310f, -0.021f) * RX(-26f));
        k.Dark.Box(new Vector3(0.0034f, 0.0150f, 0.0024f), T(0f, -0.0090f, -0.050f) * RX(-13f));
        k.Metal.FacetTubeZ(-0.001f, 0.001f, 0.0060f, 0.0060f, 10, 1f, 1f, T(0.0290f, 0.010f, -0.048f) * RY(90));

        // Charging handle: side handle on the Vandal, rear latch on the Phantom.
        if (phantom)
        {
            k.Steel.Box(new Vector3(0.014f, 0.004f, 0.020f), T(0f, 0.101f, 0.078f));
            k.Dark.Box(new Vector3(0.022f, 0.005f, 0.004f), T(0f, 0.099f, 0.064f));
            k.Dark.Box(new Vector3(0.023f, 0.004f, 0.070f), T(0f, 0.098f, 0.040f));
        }
        else
        {
            k.Steel.FacetTubeZ(0f, 0.026f, 0.0048f, 0.0048f, 8, 1f, 1f, T(0.030f, 0.079f, 0.050f) * RY(90));
            k.Metal.RoundBox(new Vector3(0.0070f, 0.0070f, 0.0070f), 0.55f, T(0.058f, 0.079f, 0.050f));
            k.Dark.Box(new Vector3(0.0012f, 0.0020f, 0.040f), T(0.0288f, 0.038f, 0.016f));
        }

        // Top rail on the receiver: a full-width riser closes the gap between the sloped upper shell
        // and the flat rail base (the old rail floated up to ~1.3 mm above the shell at its front end).
        k.Metal.ChamferZ(-0.250f, 0.060f, new Vector2(0.0190f, 0.0046f), new Vector2(0.0190f, 0.0046f),
            0.0922f, 0.0942f, 0.0016f);
        Rail(k.Metal, -0.246f, 0.044f, 0.099f);
        k.Dark.Box(new Vector3(0.0010f, 0.0012f, 0.300f), T(0.0192f, 0.0935f, -0.080f));
        k.Dark.Box(new Vector3(0.0010f, 0.0012f, 0.300f), T(-0.0192f, 0.0935f, -0.080f));
    }

    // ------------------------------------------------------------------ handguard

    static void Handguard(Kit k, bool phantom)
    {
        if (phantom)
        {
            k.Poly.FacetTubeZ(-0.482f, -0.255f, 0.033f, 0.037f, 12, 1f, 0.88f, T(0f, 0.060f, 0f));
            k.Poly.FacetTubeZ(-0.494f, -0.482f, 0.028f, 0.033f, 12, 1f, 0.88f, T(0f, 0.060f, 0f));
            for (int i = 0; i < 5; i++)
            {
                float z = -0.445f + i * 0.040f;
                k.Dark.Box(new Vector3(0.0014f, 0.0090f, 0.0055f), T(0.0364f, 0.060f, z));
                k.Dark.Box(new Vector3(0.0014f, 0.0090f, 0.0055f), T(-0.0364f, 0.060f, z));
            }
            for (int i = 0; i < 3; i++)
                k.Dark.Box(new Vector3(0.012f, 0.0012f, 0.010f), T(0f, 0.0305f, -0.425f + i * 0.045f));
            k.Metal.Box(new Vector3(0.011f, 0.0035f, 0.100f), T(0f, 0.029f, -0.360f));
            for (int i = 0; i < 5; i++)
                k.Metal.Box(new Vector3(0.008f, 0.0014f, 0.0025f), T(0f, 0.0264f, -0.438f + i * 0.036f));
            Rail(k.Metal, -0.474f, -0.258f, 0.092f);
        }
        else
        {
            k.Poly.ChamferZ(-0.470f, -0.255f, new Vector2(0.0335f, 0.0275f), new Vector2(0.0350f, 0.0295f), 0.060f, 0.060f, 0.009f);
            k.Poly.ChamferZ(-0.484f, -0.470f, new Vector2(0.0290f, 0.0235f), new Vector2(0.0335f, 0.0275f), 0.060f, 0.060f, 0.007f);
            k.Metal.ChamferZ(-0.490f, -0.482f, new Vector2(0.0310f, 0.0250f), new Vector2(0.0300f, 0.0240f), 0.060f, 0.060f, 0.005f);
            for (int i = 0; i < 3; i++)
            {
                float z = -0.440f + i * 0.050f;
                k.Dark.Box(new Vector3(0.0014f, 0.0080f, 0.020f), T(0.0352f, 0.058f, z));
                k.Dark.Box(new Vector3(0.0014f, 0.0080f, 0.020f), T(-0.0352f, 0.058f, z));
                k.Dark.Box(new Vector3(0.0080f, 0.0012f, 0.020f), T(0f, 0.0320f - i * 0.0005f, z));
            }
            // Full-width riser under the rail: the handguard top is sloped, so without it the flat rail
            // base showed a thin sky slit along part of its length.
            k.Metal.ChamferZ(-0.466f, -0.256f, new Vector2(0.0185f, 0.0048f), new Vector2(0.0185f, 0.0048f),
                0.0860f, 0.0880f, 0.0016f);
            Rail(k.Metal, -0.462f, -0.258f, 0.092f);
            k.Metal.Box(new Vector3(0.011f, 0.0035f, 0.090f), T(0f, 0.0290f, -0.358f));
            for (int i = 0; i < 4; i++)
                k.Metal.Box(new Vector3(0.008f, 0.0014f, 0.0025f), T(0f, 0.0254f, -0.430f + i * 0.036f));
            for (int i = 0; i < 3; i++)
                k.Metal.Box(new Vector3(0.018f, 0.0022f, 0.0030f), T(0f, 0.0318f, -0.300f - i * 0.048f));
        }

        // Accent strip along both sides of the handguard.
        float ax = phantom ? 0.0368f : 0.0356f;
        k.Accent.Box(new Vector3(0.0012f, 0.0024f, 0.078f), T(ax, 0.073f, -0.354f));
        k.Accent.Box(new Vector3(0.0012f, 0.0024f, 0.078f), T(-ax, 0.073f, -0.354f));
    }

    // ------------------------------------------------------------------ barrel / muzzle

    static void BarrelAndMuzzle(Kit k, bool phantom)
    {
        if (phantom)
        {
            // Integral suppressor with a knurled rear collar and three heat rings.
            k.Metal.FacetTubeZ(-0.700f, -0.486f, 0.0225f, 0.0215f, 12, 1f, 1f, T(0f, Bore, 0f));
            k.Metal.FacetTubeZ(-0.506f, -0.486f, 0.0260f, 0.0250f, 12, 1f, 1f, T(0f, Bore, 0f));
            for (int i = 0; i < 8; i++)
                k.Dark.Box(new Vector3(0.0012f, 0.0024f, 0.0035f), T(0.0258f, Bore + 0.010f, -0.503f + i * 0.0032f) * RZ(-20f));
            for (int i = 0; i < 3; i++)
                k.Metal.FacetTubeZ(-0.606f + i * 0.042f, -0.598f + i * 0.042f, 0.0252f, 0.0252f, 12, 1f, 1f, T(0f, Bore, 0f));
            k.Dark.FacetTubeZ(-0.704f, -0.699f, 0.0160f, 0.0160f, 12, 1f, 1f, T(0f, Bore, 0f));
            k.Steel.FacetTubeZ(-0.700f, -0.698f, 0.0145f, 0.0145f, 12, 1f, 1f, T(0f, Bore, 0f));
        }
        else
        {
            // Barrel, gas block, gas tube and a six-port muzzle brake.
            k.Metal.FacetTubeZ(-0.632f, -0.470f, 0.0105f, 0.0120f, 12, 1f, 1f, T(0f, Bore, 0f));
            k.Steel.FacetTubeZ(-0.492f, -0.470f, 0.0122f, 0.0122f, 12, 1f, 1f, T(0f, Bore, 0f));
            k.Metal.ChamferZ(-0.508f, -0.486f, new Vector2(0.0140f, 0.0140f), new Vector2(0.0140f, 0.0130f), 0.076f, 0.076f, 0.004f);
            k.Metal.FacetTubeZ(-0.486f, -0.286f, 0.0042f, 0.0042f, 8, 1f, 1f, T(0f, 0.092f, 0f));
            k.Metal.FacetTubeZ(-0.674f, -0.632f, 0.0160f, 0.0152f, 6, 1f, 1f, T(0f, Bore, 0f));
            k.Dark.FacetTubeZ(-0.676f, -0.672f, 0.0130f, 0.0130f, 6, 1f, 1f, T(0f, Bore, 0f));
            for (int i = 0; i < 3; i++)
            {
                k.Dark.Box(new Vector3(0.0020f, 0.0050f, 0.0080f), T(0.0160f, 0.086f, -0.658f));
                k.Dark.Box(new Vector3(0.0020f, 0.0050f, 0.0080f), T(-0.0160f, 0.086f, -0.658f));
            }
            k.Dark.Box(new Vector3(0.0030f, 0.0030f, 0.0060f), T(0f, 0.094f, -0.658f));
            k.Steel.FacetTubeZ(-0.674f, -0.673f, 0.0140f, 0.0140f, 6, 1f, 1f, T(0f, Bore, 0f));
        }
    }

    // ------------------------------------------------------------------ sights

    static void Sights(Kit k, bool phantom)
    {
        // Vandal irons: a protected post on the gas block and a thin ghost ring on the receiver. The old
        // front post stood 64 mm above the rail (a spike in profile); both sights are now low enough to
        // read as folding combat irons while the rear ring still defines the 0.62 H ADS silhouette.
        k.Metal.ChamferZ(-0.500f, -0.478f, new Vector2(0.0175f, 0.0095f), new Vector2(0.0165f, 0.0085f), 0.0960f, 0.0955f, 0.0030f);
        k.Metal.Box(new Vector3(0.0030f, 0.0160f, 0.0040f), T(0f, 0.1160f, -0.490f));
        k.Steel.Box(new Vector3(0.0020f, 0.0020f, 0.0032f), T(0f, 0.1440f, -0.490f));
        k.Metal.Box(new Vector3(0.0032f, 0.0135f, 0.0055f), T(0.0105f, 0.1145f, -0.490f));
        k.Metal.Box(new Vector3(0.0032f, 0.0135f, 0.0055f), T(-0.0105f, 0.1145f, -0.490f));
        k.Dark.Box(new Vector3(0.0042f, 0.0016f, 0.0016f), T(0f, 0.1410f, -0.490f));

        // Rear sight: a 12-segment ghost ring in a low bracket, above the rail but below the old tower.
        k.Metal.ChamferZ(0.008f, 0.048f, new Vector2(0.0180f, 0.0045f), new Vector2(0.0170f, 0.0040f), 0.0998f, 0.0992f, 0.0016f);
        k.Metal.Box(new Vector3(0.0026f, 0.0095f, 0.0060f), T(0.0115f, 0.1125f, 0.030f));
        k.Metal.Box(new Vector3(0.0026f, 0.0095f, 0.0060f), T(-0.0115f, 0.1125f, 0.030f));
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.Tau / 12f;
            k.Metal.Box(new Vector3(0.0016f, 0.0016f, 0.0040f),
                T(Mathf.Cos(a) * 0.0084f, 0.1245f + Mathf.Sin(a) * 0.0084f, 0.030f) * RZ(i * 30f));
        }
        k.Dark.Box(new Vector3(0.0055f, 0.0014f, 0.0014f), T(0f, 0.1245f, 0.030f));
    }

    // ------------------------------------------------------------------ stock

    static void Stock(Kit k, bool phantom)
    {
        if (phantom)
        {
            // Buffer tube and collar, then a single solid wedge stock: a gentle top line from the tube to
            // the butt, full side panels and a thick rubber pad. One closed loft, so no sky can show
            // between comb and body (the old three-plate frame did).
            k.Metal.FacetTubeZ(0.060f, 0.212f, 0.0155f, 0.0145f, 16, 1f, 1f, T(0f, 0.079f, 0f));
            k.Dark.ChamferZ(0.088f, 0.110f, new Vector2(0.0225f, 0.0140f), new Vector2(0.0225f, 0.0140f), 0.0820f, 0.0820f, 0.003f);
            k.Poly.ChamferZ(0.100f, 0.300f, new Vector2(0.0200f, 0.0074f), new Vector2(0.0210f, 0.0520f), 0.0890f, 0.0700f, 0.004f);
            k.Rubber.ChamferZ(0.300f, 0.314f, new Vector2(0.0210f, 0.0520f), new Vector2(0.0195f, 0.0510f), 0.0700f, 0.0705f, 0.004f);
            // Metal reinforcement plate with teal accent on both sides, plus the lower side slot.
            k.Dark.Box(new Vector3(0.0012f, 0.0230f, 0.0860f), T(0.0213f, 0.0930f, 0.234f));
            k.Dark.Box(new Vector3(0.0012f, 0.0230f, 0.0860f), T(-0.0213f, 0.0930f, 0.234f));
            k.Metal.Box(new Vector3(0.0012f, 0.0195f, 0.0800f), T(0.0216f, 0.0930f, 0.234f));
            k.Metal.Box(new Vector3(0.0012f, 0.0195f, 0.0800f), T(-0.0216f, 0.0930f, 0.234f));
            k.Accent.Box(new Vector3(0.0012f, 0.0026f, 0.0740f), T(0.0220f, 0.1010f, 0.234f));
            k.Accent.Box(new Vector3(0.0012f, 0.0026f, 0.0740f), T(-0.0220f, 0.1010f, 0.234f));
            k.Dark.Box(new Vector3(0.0012f, 0.0140f, 0.0560f), T(0.0216f, 0.0480f, 0.250f));
            k.Dark.Box(new Vector3(0.0012f, 0.0140f, 0.0560f), T(-0.0216f, 0.0480f, 0.250f));
            // Adjustment lever under the stock and a QD socket at the butt.
            k.Metal.Box(new Vector3(0.0140f, 0.0070f, 0.0280f), T(0f, 0.0200f, 0.230f));
            k.Dark.Box(new Vector3(0.0100f, 0.0030f, 0.0160f), T(0f, 0.0158f, 0.234f));
            k.Metal.FacetTubeZ(0f, 0.004f, 0.0050f, 0.0050f, 10, 1f, 1f, T(0.0210f, 0.074f, 0.272f) * RY(90));
        }
        else
        {
            // Fixed stock: a solid comb over a small triangular cut-out, with a thick diagonal brace and
            // a junction block that closes the old notch where the receiver met the stock.
            // The comb starts on a ramp that grows out of the receiver top; a vertical front cap at the
            // old z0 stood 12 mm above the receiver and read as a fin from the gameplay camera.
            k.Poly.ChamferZ(0.060f, 0.135f, new Vector2(0.0215f, 0.0035f), new Vector2(0.0230f, 0.0190f), 0.0965f, 0.0955f, 0.002f);
            k.Poly.ChamferZ(0.128f, 0.286f, new Vector2(0.0230f, 0.0190f), new Vector2(0.0215f, 0.0180f), 0.0955f, 0.0965f, 0.004f);
            k.Poly.ChamferZ(0.086f, 0.286f, new Vector2(0.0190f, 0.0080f), new Vector2(0.0180f, 0.0080f), 0.0240f, 0.0250f, 0.003f);
            k.Poly.ChamferZ(0.100f, 0.128f, new Vector2(0.0230f, 0.0420f), new Vector2(0.0225f, 0.0410f), 0.0640f, 0.0655f, 0.005f);
            k.Poly.ChamferZ(0.258f, 0.290f, new Vector2(0.0225f, 0.0450f), new Vector2(0.0210f, 0.0440f), 0.0670f, 0.0675f, 0.004f);
            k.Poly.Box(new Vector3(0.0105f, 0.0105f, 0.0700f), T(0f, 0.052f, 0.190f) * RX(-24f));
            // Cheek pad: full comb width so the polymer top edge does not show as a raised ledge beside
            // it (the gameplay camera looks almost along the stock top; any exposed ledge read as a fin).
            k.Rubber.ChamferZ(0.132f, 0.262f, new Vector2(0.0234f, 0.0022f), new Vector2(0.0229f, 0.0018f), 0.1123f, 0.1127f, 0.0018f);
            k.Rubber.ChamferZ(0.289f, 0.299f, new Vector2(0.0210f, 0.0440f), new Vector2(0.0200f, 0.0430f), 0.0675f, 0.0680f, 0.004f);
            k.Metal.Box(new Vector3(0.0160f, 0.0030f, 0.0120f), T(0f, 0.1135f, 0.280f));
            k.Metal.Box(new Vector3(0.0030f, 0.0030f, 0.0200f), T(0.0200f, 0.048f, 0.120f) * RX(-15f));
            // Inset reinforcement plate on both sides of the comb, with the accent line on top of it.
            k.Dark.Box(new Vector3(0.0012f, 0.0145f, 0.0545f), T(0.0228f, 0.096f, 0.190f));
            k.Dark.Box(new Vector3(0.0012f, 0.0145f, 0.0545f), T(-0.0228f, 0.096f, 0.190f));
            k.Metal.Box(new Vector3(0.0012f, 0.0115f, 0.0490f), T(0.0230f, 0.096f, 0.190f));
            k.Metal.Box(new Vector3(0.0012f, 0.0115f, 0.0490f), T(-0.0230f, 0.096f, 0.190f));
            k.Accent.Box(new Vector3(0.0012f, 0.0022f, 0.0430f), T(0.0234f, 0.096f, 0.190f));
            k.Accent.Box(new Vector3(0.0012f, 0.0022f, 0.0430f), T(-0.0234f, 0.096f, 0.190f));
            k.Metal.FacetTubeZ(0f, 0.004f, 0.0050f, 0.0050f, 10, 1f, 1f, T(0.0230f, 0.072f, 0.258f) * RY(90));
            k.Dark.FacetTubeZ(-0.001f, 0.001f, 0.0030f, 0.0030f, 10, 1f, 1f, T(0.0272f, 0.072f, 0.258f) * RY(90));
        }
    }

    // ------------------------------------------------------------------ grip

    static void Grip(Kit k, bool phantom)
    {
        // Four tapered lofts: beavertail, palm swell, waist and flared base. 124 mm tall with a 19° rake
        // (the old grip was 162 mm and nearly vertical, so it read as a long paddle).
        k.Poly.ChamferY(0.014f, -0.020f, new Vector2(0.0190f, 0.0230f), new Vector2(0.0198f, 0.0250f), 0.0500f, 0.0550f, 0.0040f);
        k.Poly.ChamferY(-0.020f, -0.055f, new Vector2(0.0198f, 0.0250f), new Vector2(0.0202f, 0.0258f), 0.0550f, 0.0660f, 0.0040f);
        k.Poly.ChamferY(-0.055f, -0.085f, new Vector2(0.0202f, 0.0258f), new Vector2(0.0194f, 0.0242f), 0.0660f, 0.0780f, 0.0040f);
        k.Poly.ChamferY(-0.085f, -0.104f, new Vector2(0.0194f, 0.0242f), new Vector2(0.0188f, 0.0232f), 0.0780f, 0.0880f, 0.0035f);
        k.Metal.ChamferY(-0.104f, -0.110f, new Vector2(0.0188f, 0.0232f), new Vector2(0.0180f, 0.0224f), 0.0880f, 0.0890f, 0.0030f);
        // Beavertail shelf under the receiver.
        k.Poly.Box(new Vector3(0.0200f, 0.0050f, 0.0240f), T(0f, 0.012f, 0.036f) * RX(8f));
        // Checkering: a recessed side panel with a small mark, plus a grooved front strap and rubber backstrap.
        foreach (float sx in new[] { 1f, -1f })
        {
            k.Dark.Box(new Vector3(0.0012f, 0.0220f, 0.0140f), T(sx * 0.0201f, -0.058f, 0.070f) * RX(-19f));
            k.White.Box(new Vector3(0.0012f, 0.0040f, 0.0090f), T(sx * 0.0210f, -0.058f, 0.070f) * RX(-19f));
            for (int i = 0; i < 3; i++)
                k.Dark.Box(new Vector3(0.0011f, 0.0013f, 0.0150f), T(sx * 0.0204f, -0.028f - i * 0.010f, 0.062f + i * 0.003f));
        }
        k.Dark.Box(new Vector3(0.0140f, 0.0012f, 0.0300f), T(0f, -0.064f, 0.0430f) * RX(-19f));
        k.Rubber.Box(new Vector3(0.0140f, 0.0300f, 0.0012f), T(0f, -0.060f, 0.0930f) * RX(-19f));
    }

    // ------------------------------------------------------------------ magazine

    static void Magazine(Kit k, bool phantom)
    {
        // Separate node: the reload hides it wholesale. One continuous path loft (no stacked caps) with a
        // progressive curve, a solid floorplate and pressed spines instead of per-segment ladder ribs.
        const int steps = 12;
        float drop = 0.180f;
        float curve = phantom ? 0.030f : 0.062f;
        var path = new List<(float Y, float Hx, float Hz, float Z)>();
        for (int i = 0; i <= steps; i++)
        {
            float u = i / (float)steps;
            path.Add((0.014f - u * drop, Mathf.Lerp(0.0175f, 0.0160f, u),
                Mathf.Lerp(0.0300f, 0.0285f, u), -0.164f - u * u * curve));
        }
        k.Mag.ChamferPathY(path, 0.0030f, I);

        var front = new List<(float Y, float Hx, float Hz, float Z)>();
        var rear = new List<(float Y, float Hx, float Hz, float Z)>();
        foreach (var p in path)
        {
            front.Add((p.Y, 0.0090f, 0.0015f, p.Z - p.Hz + 0.0010f));
            rear.Add((p.Y, 0.0070f, 0.0012f, p.Z + p.Hz - 0.0010f));
        }
        k.MagTrim.ChamferPathY(front, 0.0018f, I);
        k.MagTrim.ChamferPathY(rear, 0.0016f, I);

        // Floorplate, wider and deeper than the body, with a front lip.
        float by = 0.014f - drop, bz = -0.164f - curve;
        k.MagTrim.ChamferY(by - 0.012f, by + 0.004f, new Vector2(0.0195f, 0.0330f), new Vector2(0.0190f, 0.0320f), bz, bz, 0.0040f);
        k.MagTrim.Box(new Vector3(0.0180f, 0.0060f, 0.0040f), T(0f, by - 0.006f, bz - 0.0300f));
        k.Dark.Box(new Vector3(0.0020f, 0.0060f, 0.0030f), T(0.0176f, 0.002f, -0.176f));

        // Witness holes on the right side, following the curve.
        for (int i = 0; i < 4; i++)
        {
            float y = -0.030f - i * 0.030f;
            float u = (0.014f - y) / drop;
            k.Dark.Box(new Vector3(0.0012f, 0.0040f, 0.0016f), T(0.0176f, y, -0.164f - u * u * curve));
        }
    }

    // ------------------------------------------------------------------ finish marks

    static void Finish(Kit k, bool phantom)
    {
        if (phantom)
        {
            k.Accent.Box(new Vector3(0.0012f, 0.0028f, 0.052f), T(0.0278f, 0.090f, -0.018f));
            k.Accent.Box(new Vector3(0.0012f, 0.0028f, 0.052f), T(-0.0278f, 0.090f, -0.018f));
            k.White.Box(new Vector3(0.0012f, 0.0024f, 0.0100f), T(0.0279f, 0.064f, 0.022f));
            k.White.Box(new Vector3(0.0012f, 0.0024f, 0.0100f), T(-0.0279f, 0.064f, 0.022f));
            k.Accent.RoundBox(new Vector3(0.0025f, 0.0025f, 0.0025f), 0.9f, T(0.0280f, 0.058f, -0.010f));
            k.White.Box(new Vector3(0.0012f, 0.0024f, 0.0040f), T(0.0279f, 0.064f, 0.007f));
            k.White.Box(new Vector3(0.0012f, 0.0024f, 0.0040f), T(-0.0279f, 0.064f, 0.007f));
        }
        else
        {
            k.Accent.Box(new Vector3(0.0012f, 0.0028f, 0.058f), T(0.0278f, 0.091f, -0.020f));
            k.Accent.Box(new Vector3(0.0012f, 0.0028f, 0.058f), T(-0.0278f, 0.091f, -0.020f));
            k.Accent.Box(new Vector3(0.0012f, 0.0022f, 0.044f), T(0.0300f, 0.026f, -0.168f));
            k.Accent.Box(new Vector3(0.0012f, 0.0022f, 0.044f), T(-0.0300f, 0.026f, -0.168f));
            k.White.Box(new Vector3(0.0012f, 0.0022f, 0.0090f), T(0.0279f, 0.058f, 0.016f));
            k.White.Box(new Vector3(0.0012f, 0.0022f, 0.0040f), T(-0.0279f, 0.058f, 0.016f));
            k.White.Box(new Vector3(0.0012f, 0.0022f, 0.0040f), T(0.0279f, 0.058f, 0.003f));
            k.White.Box(new Vector3(0.0012f, 0.0022f, 0.0040f), T(-0.0279f, 0.058f, 0.003f));
            foreach (float z in new[] { -0.118f, -0.182f, 0.042f })
            {
                k.Metal.FacetTubeZ(-0.001f, 0.001f, 0.0032f, 0.0032f, 8, 1f, 1f, T(0.0282f, 0.034f, z) * RY(90));
                k.Metal.FacetTubeZ(-0.001f, 0.001f, 0.0032f, 0.0032f, 8, 1f, 1f, T(-0.0282f, 0.034f, z) * RY(90));
            }
        }
    }

    // ------------------------------------------------------------------ assembly

    static Node3D Assemble(Kit k, bool phantom)
    {
        // Albedo is deliberately mid-gray: the viewmodel is lit by a dim sky ambient with the sun usually behind the
        // camera, so physically dark values read as pure black on screen. Metallic stays below 0.6 for the same reason.
        var root = new Node3D { Name = "Gun" };
        root.AddChild(Part("Body", k.Body, "gunbody", k.BodyCol, 0.30f, 0.42f));
        root.AddChild(Part("Polymer", k.Poly, "gunpoly", new Color(0.205f, 0.210f, 0.222f), 0.0f, 0.55f));
        root.AddChild(Part("Metal", k.Metal, "gunmetal", new Color(0.380f, 0.388f, 0.400f), 0.55f, 0.30f));
        root.AddChild(Part("Accent", k.Accent, "gunaccent", k.AccentCol, 0.22f, 0.34f));
        root.AddChild(Part("Dark", k.Dark, "gundark", new Color(0.095f, 0.098f, 0.104f), 0.0f, 0.72f));
        root.AddChild(Part("Markings", k.White, "gunwhite", new Color(0.820f, 0.800f, 0.740f), 0.0f, 0.58f));
        root.AddChild(Part("Rubber", k.Rubber, "gunrubber", new Color(0.045f, 0.046f, 0.050f), 0.0f, 0.92f));
        root.AddChild(Part("Steel", k.Steel, "gunsteel", new Color(0.520f, 0.530f, 0.550f), 0.75f, 0.24f));
        var magazine = new Node3D { Name = "Magazine" };
        magazine.AddChild(Part("MagazineBody", k.Mag, "gunmag", new Color(0.270f, 0.275f, 0.285f), 0.05f, 0.52f));
        magazine.AddChild(Part("MagazineTrim", k.MagTrim, "gunmetal", new Color(0.310f, 0.315f, 0.325f), 0.35f, 0.40f));
        root.AddChild(magazine);
        root.AddChild(new Marker3D { Name = "Muzzle", Position = new Vector3(0f, Bore, phantom ? -0.700f : -0.674f) });
        return root;
    }

    /// <summary>One named material surface. The Viewmodel re-material pass reads the name and albedo.</summary>
    static MeshInstance3D Part(string name, VmMesh mesh, string material, Color albedo, float metallic, float roughness)
    {
        var mi = new MeshInstance3D { Name = name, Mesh = new ArrayMesh() };
        var sm = new StandardMaterial3D { ResourceName = material, AlbedoColor = albedo, Metallic = metallic, Roughness = roughness };
        mesh.AddTo((ArrayMesh)mi.Mesh, sm);
        return mi;
    }

    /// <summary>Low Picatinny base with short teeth, aligned to the top of the receiver.</summary>
    static void Rail(VmMesh m, float z0, float z1, float y)
    {
        m.Box(new Vector3(0.0125f, 0.0032f, (z1 - z0) * 0.5f), T(0f, y, (z0 + z1) * 0.5f));
        for (float z = z0 + 0.008f; z < z1 - 0.005f; z += 0.013f)
            m.Box(new Vector3(0.0095f, 0.0022f, 0.0022f), T(0f, y + 0.0050f, z));
    }
}
