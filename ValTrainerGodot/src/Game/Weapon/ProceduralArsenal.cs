using Godot;
using ValTrainer.Core;

namespace ValTrainer.Game.Weapon;

/// <summary>
/// First-person models for the rest of VALORANT's arsenal, built on the CPU from boxes, lofts and faceted tubes like
/// <see cref="ProceduralRifle"/> (self-made: no third-party or Riot asset is used). They are stylised stand-ins with each
/// gun's recognisable silhouette — the Ghost's suppressor, the Shorty's twin barrels, the Judge's drum, the Bucky's pump,
/// the Ares / Odin shrouds and boxes, the Marshal's scope and bolt, the Outlaw's stacked barrels — not replicas.
/// <para>Each gun is authored in the gun space of the pose family it is held with (<see cref="Weapons.FamilyOf"/>): grip at
/// the origin, barrel along -Z; sidearms in the Sheriff frame (bore ≈ 0.055 m up, muzzle ≈ -0.15 m), long guns in the
/// Vandal frame (bore ≈ 0.078 m, handguard under -0.33 m), snipers in the Operator frame (muzzle ≈ -0.85 m), so the hands,
/// magazine well, ejection and muzzle flash of that pose line up. A "Magazine" node (hidden by the reload) and a "Muzzle"
/// marker are provided like the rifles'.</para>
/// </summary>
public static class ProceduralArsenal
{
    static readonly Transform3D I = Transform3D.Identity;
    static Transform3D T(float x, float y, float z) => new(Basis.Identity, new Vector3(x, y, z));
    static Transform3D RX(float deg) => new(new Basis(Vector3.Right, Mathf.DegToRad(deg)), Vector3.Zero);
    static Transform3D RY(float deg) => new(new Basis(Vector3.Up, Mathf.DegToRad(deg)), Vector3.Zero);
    static Transform3D RZ(float deg) => new(new Basis(Vector3.Back, Mathf.DegToRad(deg)), Vector3.Zero);

    public static bool Has(WeaponKind k) => k is WeaponKind.Classic or WeaponKind.Shorty or WeaponKind.Frenzy or WeaponKind.Ghost
        or WeaponKind.Bandit or WeaponKind.Stinger or WeaponKind.Spectre or WeaponKind.Bucky or WeaponKind.Judge or WeaponKind.Bulldog
        or WeaponKind.Guardian or WeaponKind.Warden or WeaponKind.Marshal or WeaponKind.Outlaw or WeaponKind.Ares or WeaponKind.Odin
        or WeaponKind.Knife;

    /// <summary>Mesh buckets of one gun (one material slot each).</summary>
    sealed class Kit
    {
        public readonly VmMesh Body = new(), Poly = new(), Metal = new(), Accent = new(), Dark = new(), White = new(), Mag = new(), MagTrim = new();
        public Color BodyCol = new(0.29f, 0.29f, 0.30f), AccentCol = new(0.72f, 0.06f, 0.07f), PolyCol = new(0.185f, 0.19f, 0.20f);
        public Vector3 Muzzle;
    }

    public static Node3D Build(WeaponKind kind)
    {
        var k = new Kit();
        switch (kind)
        {
            case WeaponKind.Classic: Pistol(k, slide: 0.165f, h: 0.030f, frameCol: 0, ported: false); k.AccentCol = new Color(0.75f, 0.12f, 0.10f); break;
            case WeaponKind.Ghost:
                Pistol(k, slide: 0.160f, h: 0.028f, frameCol: 1, ported: false);
                k.Metal.FacetTubeZ(-0.330f, -0.150f, 0.0145f, 0.0145f, 12, 1f, 1f, T(0f, 0.055f, 0f));        // suppressor
                for (int i = 0; i < 4; i++) k.Dark.FacetTubeZ(-0.30f + i * 0.035f, -0.296f + i * 0.035f, 0.0150f, 0.0150f, 12, 1f, 1f, T(0f, 0.055f, 0f));
                k.Muzzle = new Vector3(0f, 0.055f, -0.335f);
                k.AccentCol = new Color(0.10f, 0.55f, 0.62f);
                break;
            case WeaponKind.Frenzy:
                Pistol(k, slide: 0.140f, h: 0.032f, frameCol: 1, ported: true, longMag: true);
                k.AccentCol = new Color(0.95f, 0.55f, 0.10f);
                break;
            case WeaponKind.Bandit:
                Pistol(k, slide: 0.185f, h: 0.036f, frameCol: 0, ported: true);
                k.Metal.Box(new Vector3(0.017f, 0.006f, 0.040f), T(0f, 0.090f, -0.090f));                    // top rib
                k.AccentCol = new Color(0.80f, 0.68f, 0.30f);
                break;
            case WeaponKind.Shorty: Shorty(k); break;
            case WeaponKind.Knife: Knife(k); break;
            case WeaponKind.Stinger: Smg(k, suppressed: false); break;
            case WeaponKind.Spectre: Smg(k, suppressed: true); break;
            case WeaponKind.Bucky: Bucky(k); break;
            case WeaponKind.Judge: Judge(k); break;
            case WeaponKind.Bulldog: Rifle(k, barrel: -0.600f, optic: 1, stock: 0); k.AccentCol = new Color(0.85f, 0.62f, 0.12f); break;
            case WeaponKind.Guardian: Rifle(k, barrel: -0.760f, optic: 2, stock: 1); k.AccentCol = new Color(0.20f, 0.50f, 0.85f); break;
            case WeaponKind.Warden: Rifle(k, barrel: -0.720f, optic: 2, stock: 2); k.AccentCol = new Color(0.55f, 0.85f, 0.35f); break;
            case WeaponKind.Marshal: Sniper(k, outlaw: false); break;
            case WeaponKind.Outlaw: Sniper(k, outlaw: true); break;
            case WeaponKind.Ares: Heavy(k, odin: false); break;
            case WeaponKind.Odin: Heavy(k, odin: true); break;
        }
        return Assemble(k);
    }

    // ------------------------------------------------------------------ sidearms (Sheriff frame)

    static void Pistol(Kit k, float slide, float h, int frameCol, bool ported, bool longMag = false)
    {
        float z0 = -slide + 0.045f, z1 = 0.045f, y = 0.055f;
        // slide (tapered toward the muzzle) with cocking serrations and sights
        k.Body.LoftZ(z0, z1, new Vector2(0.0135f, h * 0.5f), new Vector2(0.0145f, h * 0.5f), y + 0.004f, y + 0.004f, I);
        for (int i = 0; i < 6; i++) k.Dark.Box(new Vector3(0.0008f, h * 0.36f, 0.0016f), T(0.0148f, y + 0.004f, z1 - 0.012f - i * 0.006f));
        for (int i = 0; i < 6; i++) k.Dark.Box(new Vector3(0.0008f, h * 0.36f, 0.0016f), T(-0.0148f, y + 0.004f, z1 - 0.012f - i * 0.006f));
        if (ported)
            for (int i = 0; i < 3; i++) k.Dark.Box(new Vector3(0.0035f, 0.0012f, 0.008f), T(0f, y + 0.004f + h * 0.5f, z0 + 0.018f + i * 0.016f));
        k.Metal.Box(new Vector3(0.0028f, 0.0045f, 0.004f), T(0f, y + h * 0.5f + 0.008f, z0 + 0.010f));                   // front post
        k.Metal.Box(new Vector3(0.0120f, 0.0045f, 0.005f), T(0f, y + h * 0.5f + 0.008f, z1 - 0.008f));                   // rear sight
        k.Dark.Box(new Vector3(0.0030f, 0.0040f, 0.0055f), T(0f, y + h * 0.5f + 0.009f, z1 - 0.008f));
        k.Dark.Box(new Vector3(0.006f, 0.010f, 0.018f), T(0.0142f, y + 0.008f, -0.030f));                                  // ejection port
        k.Metal.FacetTubeZ(z0 - 0.006f, z0 + 0.002f, 0.0065f, 0.0065f, 10, 1f, 1f, T(0f, y + 0.002f, 0f));            // barrel crown
        k.Dark.FacetTubeZ(z0 - 0.0065f, z0 - 0.0055f, 0.0042f, 0.0042f, 10, 1f, 1f, T(0f, y + 0.002f, 0f));
        // frame + dust cover + accent stripe
        var frame = frameCol == 0 ? k.Poly : k.Body;
        frame.LoftZ(z0 + 0.012f, z1 - 0.010f, new Vector2(0.0125f, 0.010f), new Vector2(0.0130f, 0.011f), 0.030f, 0.030f, I);
        k.Accent.Box(new Vector3(0.0009f, 0.0025f, (z1 - z0) * 0.32f), T(0.0137f, 0.030f, (z0 + z1) * 0.5f - 0.01f));
        k.Accent.Box(new Vector3(0.0009f, 0.0025f, (z1 - z0) * 0.32f), T(-0.0137f, 0.030f, (z0 + z1) * 0.5f - 0.01f));
        // grip (raked back) with texture panels, trigger guard and trigger
        k.Poly.LoftY(-0.095f, 0.022f, new Vector2(0.0135f, 0.0205f), new Vector2(0.0150f, 0.0230f), 0.040f, 0.012f, I);
        k.Dark.Box(new Vector3(0.0008f, 0.034f, 0.016f), T(0.0152f, -0.040f, 0.026f) * RX(15f));
        k.Dark.Box(new Vector3(0.0008f, 0.034f, 0.016f), T(-0.0152f, -0.040f, 0.026f) * RX(15f));
        k.Metal.Box(new Vector3(0.0028f, 0.0028f, 0.026f), T(0f, 0.004f, -0.020f));
        k.Metal.Box(new Vector3(0.0028f, 0.012f, 0.0028f), T(0f, 0.012f, -0.046f));
        k.Metal.Box(new Vector3(0.0022f, 0.010f, 0.0028f), T(0f, 0.016f, -0.010f) * RX(-12f));
        // magazine (base plate under the grip; a long one sticks out)
        float magBottom = longMag ? -0.135f : -0.100f;
        k.Mag.LoftY(magBottom, -0.060f, new Vector2(0.0118f, 0.0175f), new Vector2(0.0120f, 0.0180f), 0.044f, 0.036f, I);
        k.MagTrim.Box(new Vector3(0.0140f, 0.0040f, 0.0205f), T(0f, magBottom - 0.002f, 0.045f));
        k.Muzzle = new Vector3(0f, y + 0.002f, z0 - 0.008f);
    }

    static void Shorty(Kit k)
    {
        // Two short side-by-side barrels on a pistol grip.
        for (int s = -1; s <= 1; s += 2)
        {
            k.Metal.FacetTubeZ(-0.235f, 0.030f, 0.0145f, 0.0145f, 14, 1f, 1f, T(s * 0.0145f, 0.058f, 0f));
            k.Dark.FacetTubeZ(-0.2365f, -0.2340f, 0.0105f, 0.0105f, 14, 1f, 1f, T(s * 0.0145f, 0.058f, 0f));
        }
        k.Body.LoftZ(-0.020f, 0.060f, new Vector2(0.030f, 0.020f), new Vector2(0.028f, 0.022f), 0.050f, 0.050f, I);   // breech block
        k.Poly.LoftZ(-0.215f, -0.080f, new Vector2(0.027f, 0.011f), new Vector2(0.029f, 0.012f), 0.033f, 0.033f, I);  // fore-end
        for (int i = 0; i < 5; i++) k.Dark.Box(new Vector3(0.030f, 0.0010f, 0.004f), T(0f, 0.0215f, -0.200f + i * 0.026f));
        k.Accent.Box(new Vector3(0.0010f, 0.004f, 0.060f), T(0.0295f, 0.050f, 0.020f));
        k.Accent.Box(new Vector3(0.0010f, 0.004f, 0.060f), T(-0.0295f, 0.050f, 0.020f));
        k.Metal.Box(new Vector3(0.004f, 0.012f, 0.010f), T(0f, 0.080f, 0.050f));                                       // hammer spur
        k.Poly.LoftY(-0.100f, 0.030f, new Vector2(0.0140f, 0.021f), new Vector2(0.0160f, 0.024f), 0.045f, 0.014f, I);
        k.Metal.Box(new Vector3(0.0028f, 0.0028f, 0.026f), T(0f, 0.006f, -0.020f));
        k.Metal.Box(new Vector3(0.0022f, 0.010f, 0.0028f), T(0f, 0.018f, -0.010f) * RX(-12f));
        k.Mag.Box(new Vector3(0.010f, 0.010f, 0.010f), T(0f, 0.058f, 0.050f));                                       // shells at the breech
        k.AccentCol = new Color(0.80f, 0.35f, 0.12f);
        k.Muzzle = new Vector3(0f, 0.058f, -0.240f);
    }

    static void Knife(Kit k)
    {
        // Tanto-style blade along -Z with a fuller, a guard and a wrapped handle (held in the pistol grip).
        k.Metal.LoftZ(-0.205f, -0.020f, new Vector2(0.0018f, 0.004f), new Vector2(0.0030f, 0.017f), 0.056f, 0.048f, I);
        k.Metal.LoftZ(-0.235f, -0.205f, new Vector2(0.0008f, 0.001f), new Vector2(0.0018f, 0.004f), 0.064f, 0.056f, I);
        k.Dark.Box(new Vector3(0.0032f, 0.0018f, 0.075f), T(0f, 0.052f, -0.095f));                                    // fuller
        k.Accent.Box(new Vector3(0.0035f, 0.0012f, 0.11f), T(0f, 0.0395f, -0.105f));                                   // edge glint
        k.Body.Box(new Vector3(0.012f, 0.026f, 0.007f), T(0f, 0.045f, -0.014f));                                       // guard
        k.Poly.LoftY(-0.080f, 0.032f, new Vector2(0.0110f, 0.0150f), new Vector2(0.0120f, 0.0165f), 0.030f, 0.004f, I);
        for (int i = 0; i < 6; i++) k.Dark.Box(new Vector3(0.0125f, 0.0012f, 0.017f), T(0f, 0.020f - i * 0.016f, 0.010f + i * 0.0045f) * RX(15f));
        k.Metal.Box(new Vector3(0.010f, 0.008f, 0.012f), T(0f, -0.085f, 0.032f));                                     // pommel
        k.AccentCol = new Color(0.62f, 0.86f, 0.95f);
        k.BodyCol = new Color(0.22f, 0.23f, 0.25f);
        k.Muzzle = new Vector3(0f, 0.060f, -0.235f);
    }

    // ------------------------------------------------------------------ long guns (Vandal frame)

    /// <summary>Upper receiver, rail and grip shared by the long guns (grip at the origin, bore ≈ 0.078 m).</summary>
    static void Receiver(Kit k, float front, float rear, float height = 0.062f)
    {
        k.Body.LoftZ(front, rear, new Vector2(0.026f, 0.034f), new Vector2(0.027f, 0.040f), height, height - 0.002f, I);
        k.Dark.LoftZ(front + 0.012f, rear - 0.012f, new Vector2(0.021f, 0.010f), new Vector2(0.023f, 0.012f), 0.022f, 0.024f, I);
        k.Dark.Box(new Vector3(0.0015f, 0.011f, 0.028f), T(0.0272f, 0.072f, -0.070f));                               // ejection port
        k.Metal.Box(new Vector3(0.012f, 0.0035f, (rear - front) * 0.42f), T(0f, height + 0.046f, (front + rear) * 0.5f));
        for (float z = front + 0.01f; z < rear - 0.01f; z += 0.014f)
            k.Metal.Box(new Vector3(0.0095f, 0.0022f, 0.0022f), T(0f, height + 0.0515f, z));
        // pistol grip, guard, trigger
        k.Poly.LoftY(-0.145f, 0.012f, new Vector2(0.019f, 0.025f), new Vector2(0.024f, 0.033f), 0.056f, 0.020f, I);
        k.Metal.Box(new Vector3(0.0032f, 0.019f, 0.0038f), T(0f, -0.010f, -0.082f));
        k.Metal.Box(new Vector3(0.0032f, 0.0032f, 0.052f), T(0f, -0.029f, -0.034f));
        k.Metal.Box(new Vector3(0.0024f, 0.013f, 0.003f), T(0f, -0.004f, -0.044f) * RX(-14f));
    }

    static void Barrel(Kit k, float z0, float z1, float r, float y = 0.078f)
    {
        k.Metal.FacetTubeZ(z0, z1, r, r * 1.1f, 12, 1f, 1f, T(0f, y, 0f));
        k.Dark.FacetTubeZ(z0 - 0.002f, z0 + 0.002f, r * 0.62f, r * 0.62f, 12, 1f, 1f, T(0f, y, 0f));
        k.Muzzle = new Vector3(0f, y, z0 - 0.004f);
    }

    static void StraightMag(Kit k, float z, float len, float rake, float w = 0.016f, float d = 0.026f)
    {
        k.Mag.LoftY(0.012f - len, 0.012f, new Vector2(w, d), new Vector2(w + 0.0005f, d + 0.001f), z - rake, z, I);
        k.MagTrim.Box(new Vector3(w + 0.002f, 0.005f, d + 0.002f), T(0f, 0.010f - len, z - rake));
    }

    static void Smg(Kit k, bool suppressed)
    {
        Receiver(k, -0.300f, 0.060f, 0.058f);
        k.Poly.LoftZ(-0.360f, -0.300f, new Vector2(0.024f, 0.028f), new Vector2(0.026f, 0.032f), 0.058f, 0.058f, I);      // nose
        if (suppressed)
        {
            // Spectre: long integral suppressor and an angled magazine
            k.Metal.FacetTubeZ(-0.640f, -0.355f, 0.021f, 0.021f, 14, 1f, 1f, T(0f, 0.070f, 0f));
            for (int i = 0; i < 5; i++) k.Dark.FacetTubeZ(-0.610f + i * 0.052f, -0.604f + i * 0.052f, 0.0218f, 0.0218f, 14, 1f, 1f, T(0f, 0.070f, 0f));
            k.Muzzle = new Vector3(0f, 0.070f, -0.645f);
            StraightMag(k, -0.150f, 0.150f, 0.030f);
            k.AccentCol = new Color(0.18f, 0.72f, 0.66f);
            // retractable wire stock
            k.Metal.Box(new Vector3(0.003f, 0.003f, 0.120f), T(0.015f, 0.060f, 0.130f));
            k.Metal.Box(new Vector3(0.003f, 0.003f, 0.120f), T(-0.015f, 0.060f, 0.130f));
            k.Poly.Box(new Vector3(0.019f, 0.040f, 0.006f), T(0f, 0.050f, 0.252f));
        }
        else
        {
            // Stinger: short barrel, front vertical grip, folding stock, straight short magazine
            Barrel(k, -0.470f, -0.355f, 0.0095f, 0.070f);
            k.Poly.LoftY(-0.090f, 0.025f, new Vector2(0.012f, 0.014f), new Vector2(0.013f, 0.016f), -0.300f, -0.300f, I);
            StraightMag(k, -0.140f, 0.110f, 0.008f);
            k.Poly.LoftZ(0.060f, 0.230f, new Vector2(0.006f, 0.022f), new Vector2(0.006f, 0.026f), 0.050f, 0.040f, T(0.026f, 0f, 0f));
            k.AccentCol = new Color(0.95f, 0.42f, 0.18f);
        }
        k.Metal.Box(new Vector3(0.0032f, 0.016f, 0.005f), T(0f, 0.122f, -0.280f));                                     // front sight
        k.Metal.Box(new Vector3(0.012f, 0.010f, 0.008f), T(0f, 0.118f, 0.020f));                                         // rear sight
        k.Accent.Box(new Vector3(0.0012f, 0.003f, 0.08f), T(0.0272f, 0.090f, -0.120f));
        k.Accent.Box(new Vector3(0.0012f, 0.003f, 0.08f), T(-0.0272f, 0.090f, -0.120f));
    }

    static void Bucky(Kit k)
    {
        Receiver(k, -0.200f, 0.080f, 0.060f);
        k.Metal.FacetTubeZ(-0.820f, -0.200f, 0.0130f, 0.0135f, 14, 1f, 1f, T(0f, 0.086f, 0f));                         // barrel
        k.Metal.FacetTubeZ(-0.640f, -0.200f, 0.0120f, 0.0120f, 14, 1f, 1f, T(0f, 0.052f, 0f));                         // magazine tube
        k.Dark.FacetTubeZ(-0.822f, -0.818f, 0.0090f, 0.0090f, 14, 1f, 1f, T(0f, 0.086f, 0f));
        k.Muzzle = new Vector3(0f, 0.086f, -0.825f);
        // pump (the support hand sits on it), with grooves
        k.Poly.FacetTubeZ(-0.420f, -0.255f, 0.0215f, 0.0215f, 10, 1f, 0.9f, T(0f, 0.052f, 0f));
        for (int i = 0; i < 7; i++) k.Dark.FacetTubeZ(-0.405f + i * 0.022f, -0.401f + i * 0.022f, 0.0222f, 0.0222f, 10, 1f, 0.9f, T(0f, 0.052f, 0f));
        // wooden stock
        k.Poly.LoftZ(0.070f, 0.300f, new Vector2(0.020f, 0.022f), new Vector2(0.022f, 0.040f), 0.068f, 0.052f, I);
        k.Dark.Box(new Vector3(0.023f, 0.044f, 0.005f), T(0f, 0.052f, 0.302f));
        k.PolyCol = new Color(0.36f, 0.22f, 0.13f);
        k.Metal.Box(new Vector3(0.0032f, 0.010f, 0.004f), T(0f, 0.104f, -0.800f));                                     // bead
        k.Mag.Box(new Vector3(0.007f, 0.007f, 0.012f), T(0.020f, 0.060f, -0.060f));                                     // shell in the port
        k.AccentCol = new Color(0.86f, 0.70f, 0.20f);
    }

    static void Judge(Kit k)
    {
        Receiver(k, -0.260f, 0.070f, 0.064f);
        // wide shroud with vents over a short barrel
        k.Body.LoftZ(-0.540f, -0.260f, new Vector2(0.030f, 0.026f), new Vector2(0.032f, 0.030f), 0.075f, 0.072f, I);
        for (int i = 0; i < 5; i++)
        {
            k.Dark.Box(new Vector3(0.0012f, 0.012f, 0.022f), T(0.0305f, 0.076f, -0.505f + i * 0.050f));
            k.Dark.Box(new Vector3(0.0012f, 0.012f, 0.022f), T(-0.0305f, 0.076f, -0.505f + i * 0.050f));
        }
        Barrel(k, -0.600f, -0.540f, 0.018f, 0.078f);
        // drum magazine (separate node) and a carry-style top rail
        k.Mag.FacetTubeZ(-0.040f, 0.040f, 0.058f, 0.058f, 18, 1f, 1f, T(0f, -0.050f, -0.160f) * RY(90f));
        k.MagTrim.FacetTubeZ(-0.042f, -0.038f, 0.030f, 0.030f, 18, 1f, 1f, T(0f, -0.050f, -0.160f) * RY(90f));
        k.MagTrim.FacetTubeZ(0.038f, 0.042f, 0.030f, 0.030f, 18, 1f, 1f, T(0f, -0.050f, -0.160f) * RY(90f));
        k.Poly.LoftZ(0.060f, 0.270f, new Vector2(0.020f, 0.020f), new Vector2(0.021f, 0.042f), 0.074f, 0.058f, I);
        k.Dark.Box(new Vector3(0.022f, 0.046f, 0.005f), T(0f, 0.058f, 0.272f));
        k.AccentCol = new Color(0.78f, 0.20f, 0.85f);
    }

    /// <summary>Bulldog / Guardian / Warden. optic: 1 = red dot, 2 = magnified scope. stock: 0 fixed, 1 thin marksman, 2 skeleton.</summary>
    static void Rifle(Kit k, float barrel, int optic, int stock)
    {
        Receiver(k, -0.250f, 0.090f);
        k.Poly.FacetTubeZ(-0.470f, -0.250f, 0.034f, 0.039f, 8, 1f, 0.82f, T(0f, 0.062f, 0f));                          // handguard
        for (int i = 0; i < 4; i++)
        {
            k.Dark.Box(new Vector3(0.0015f, 0.008f, 0.020f), T(0.038f, 0.062f, -0.440f + i * 0.050f));
            k.Dark.Box(new Vector3(0.0015f, 0.008f, 0.020f), T(-0.038f, 0.062f, -0.440f + i * 0.050f));
        }
        Barrel(k, barrel, -0.465f, 0.0105f);
        if (barrel < -0.70f) k.Metal.FacetTubeZ(barrel - 0.035f, barrel + 0.010f, 0.016f, 0.015f, 6, 1f, 1f, T(0f, 0.078f, 0f));  // brake
        if (optic == 1)
        {
            k.Body.Box(new Vector3(0.016f, 0.016f, 0.030f), T(0f, 0.132f, -0.040f));
            k.Accent.Box(new Vector3(0.010f, 0.010f, 0.002f), T(0f, 0.134f, -0.071f));
        }
        else
        {
            k.Metal.FacetTubeZ(-0.130f, 0.050f, 0.017f, 0.017f, 14, 1f, 1f, T(0f, 0.145f, 0f));
            k.Metal.FacetTubeZ(-0.160f, -0.120f, 0.022f, 0.018f, 14, 1f, 1f, T(0f, 0.145f, 0f));
            k.Dark.FacetTubeZ(-0.162f, -0.158f, 0.0175f, 0.0175f, 14, 1f, 1f, T(0f, 0.145f, 0f));
            k.Metal.Box(new Vector3(0.010f, 0.012f, 0.012f), T(0f, 0.120f, -0.090f));
            k.Metal.Box(new Vector3(0.010f, 0.012f, 0.012f), T(0f, 0.120f, 0.020f));
        }
        StraightMag(k, -0.165f, stock == 1 ? 0.090f : 0.140f, stock == 1 ? 0.004f : 0.018f);
        switch (stock)
        {
            case 0:
                k.Poly.LoftZ(0.088f, 0.290f, new Vector2(0.023f, 0.030f), new Vector2(0.021f, 0.052f), 0.068f, 0.058f, I);
                break;
            case 1:
                k.Poly.LoftZ(0.088f, 0.300f, new Vector2(0.018f, 0.012f), new Vector2(0.017f, 0.012f), 0.095f, 0.095f, I);
                k.Poly.LoftZ(0.250f, 0.305f, new Vector2(0.019f, 0.046f), new Vector2(0.018f, 0.050f), 0.062f, 0.060f, I);
                k.Poly.Box(new Vector3(0.006f, 0.006f, 0.100f), T(0f, 0.045f, 0.190f) * RX(-16f));
                break;
            default:
                k.Metal.FacetTubeZ(0.088f, 0.240f, 0.014f, 0.013f, 10, 1f, 1f, T(0f, 0.080f, 0f));
                k.Poly.LoftZ(0.220f, 0.300f, new Vector2(0.020f, 0.048f), new Vector2(0.019f, 0.052f), 0.064f, 0.062f, I);
                break;
        }
        k.Accent.Box(new Vector3(0.0012f, 0.003f, 0.07f), T(0.0390f, 0.070f, -0.360f));
        k.Accent.Box(new Vector3(0.0012f, 0.003f, 0.07f), T(-0.0390f, 0.070f, -0.360f));
    }

    static void Heavy(Kit k, bool odin)
    {
        Receiver(k, -0.300f, 0.100f, 0.066f);
        float len = odin ? -0.820f : -0.760f;
        // perforated barrel shroud around a thick barrel
        k.Body.FacetTubeZ(len + 0.08f, -0.290f, odin ? 0.036f : 0.031f, odin ? 0.038f : 0.033f, 12, 1f, 1f, T(0f, 0.076f, 0f));
        for (int r = 0; r < 6; r++)
            for (int i = 0; i < 2; i++)
            {
                float z = -0.330f - r * (odin ? 0.072f : 0.064f);
                k.Dark.Box(new Vector3(0.0015f, 0.010f, 0.016f), T((i == 0 ? 1 : -1) * (odin ? 0.0375f : 0.0325f), 0.080f, z));
            }
        Barrel(k, len, len + 0.08f, odin ? 0.014f : 0.012f, 0.076f);
        // carry handle and top cover
        k.Metal.Box(new Vector3(0.004f, 0.022f, 0.006f), T(0f, 0.128f, -0.260f));
        k.Metal.Box(new Vector3(0.004f, 0.022f, 0.006f), T(0f, 0.128f, -0.120f));
        k.Metal.Box(new Vector3(0.006f, 0.004f, 0.080f), T(0f, 0.150f, -0.190f));
        k.Body.Box(new Vector3(0.028f, 0.010f, 0.110f), T(0f, 0.110f, -0.080f));
        // folded bipod legs under the shroud
        k.Metal.Box(new Vector3(0.003f, 0.003f, 0.150f), T(0.012f, 0.030f, len + 0.180f));
        k.Metal.Box(new Vector3(0.003f, 0.003f, 0.150f), T(-0.012f, 0.030f, len + 0.180f));
        // magazine: Odin a big box hanging left, Ares a square box underneath
        if (odin)
        {
            k.Mag.Box(new Vector3(0.045f, 0.055f, 0.050f), T(-0.020f, -0.045f, -0.170f));
            k.MagTrim.Box(new Vector3(0.046f, 0.006f, 0.051f), T(-0.020f, -0.098f, -0.170f));
        }
        else
        {
            k.Mag.Box(new Vector3(0.030f, 0.045f, 0.040f), T(0f, -0.035f, -0.165f));
            k.MagTrim.Box(new Vector3(0.031f, 0.006f, 0.041f), T(0f, -0.078f, -0.165f));
        }
        k.Poly.LoftZ(0.098f, 0.300f, new Vector2(0.024f, 0.032f), new Vector2(0.022f, 0.052f), 0.070f, 0.058f, I);
        k.Dark.Box(new Vector3(0.023f, 0.054f, 0.005f), T(0f, 0.058f, 0.302f));
        k.AccentCol = odin ? new Color(0.92f, 0.26f, 0.18f) : new Color(0.20f, 0.65f, 0.95f);
        k.BodyCol = new Color(0.26f, 0.27f, 0.28f);
    }

    // ------------------------------------------------------------------ snipers (Operator frame)

    static void Sniper(Kit k, bool outlaw)
    {
        Receiver(k, -0.260f, 0.080f, 0.058f);
        if (outlaw)
        {
            // two stacked barrels in a break-action frame
            k.Metal.FacetTubeZ(-0.800f, -0.250f, 0.0125f, 0.0130f, 12, 1f, 1f, T(0f, 0.088f, 0f));
            k.Metal.FacetTubeZ(-0.780f, -0.250f, 0.0125f, 0.0130f, 12, 1f, 1f, T(0f, 0.060f, 0f));
            k.Dark.FacetTubeZ(-0.802f, -0.798f, 0.0085f, 0.0085f, 12, 1f, 1f, T(0f, 0.088f, 0f));
            k.Body.Box(new Vector3(0.020f, 0.024f, 0.020f), T(0f, 0.074f, -0.560f));
            k.Muzzle = new Vector3(0f, 0.088f, -0.805f);
            k.Mag.Box(new Vector3(0.008f, 0.008f, 0.014f), T(0.020f, 0.074f, -0.230f));                                 // shells
            k.AccentCol = new Color(0.90f, 0.62f, 0.15f);
        }
        else
        {
            Barrel(k, -0.860f, -0.250f, 0.0090f);
            k.Metal.FacetTubeZ(-0.875f, -0.840f, 0.0135f, 0.0130f, 8, 1f, 1f, T(0f, 0.078f, 0f));
            // bolt handle on the right
            k.Metal.Box(new Vector3(0.030f, 0.004f, 0.004f), T(0.040f, 0.090f, 0.050f) * RZ(-25f));
            k.Metal.RoundBox(new Vector3(0.007f, 0.007f, 0.007f), 0.6f, T(0.068f, 0.078f, 0.050f));
            StraightMag(k, -0.140f, 0.060f, 0.002f, 0.014f, 0.030f);
            k.AccentCol = new Color(0.35f, 0.80f, 0.40f);
        }
        // scope with rings
        k.Metal.FacetTubeZ(-0.210f, 0.040f, 0.0180f, 0.0180f, 16, 1f, 1f, T(0f, 0.150f, 0f));
        k.Metal.FacetTubeZ(-0.280f, -0.200f, 0.0260f, 0.0190f, 16, 1f, 1f, T(0f, 0.150f, 0f));
        k.Metal.FacetTubeZ(0.030f, 0.075f, 0.0200f, 0.0230f, 16, 1f, 1f, T(0f, 0.150f, 0f));
        k.Accent.FacetTubeZ(-0.283f, -0.279f, 0.0220f, 0.0220f, 16, 1f, 1f, T(0f, 0.150f, 0f));                        // lens glint
        k.Dark.Box(new Vector3(0.012f, 0.026f, 0.012f), T(0f, 0.120f, -0.160f));
        k.Dark.Box(new Vector3(0.012f, 0.026f, 0.012f), T(0f, 0.120f, 0.010f));
        // long thin stock with cheek rest
        k.Poly.LoftZ(0.070f, 0.330f, new Vector2(0.019f, 0.028f), new Vector2(0.020f, 0.050f), 0.072f, 0.060f, I);
        k.Poly.Box(new Vector3(0.016f, 0.010f, 0.080f), T(0f, 0.108f, 0.200f));
        k.Dark.Box(new Vector3(0.021f, 0.052f, 0.005f), T(0f, 0.060f, 0.332f));
        k.Poly.FacetTubeZ(-0.500f, -0.250f, 0.024f, 0.028f, 8, 1f, 0.8f, T(0f, 0.050f, 0f));                         // fore-end
    }

    // ------------------------------------------------------------------ assembly

    static Node3D Assemble(Kit k)
    {
        var root = new Node3D { Name = "Gun" };
        void Add(string name, VmMesh m, string mat, Color c, float metallic, float rough)
        {
            if (!m.Empty) root.AddChild(Part(name, m, mat, c, metallic, rough));
        }
        Add("Body", k.Body, "gunbody", k.BodyCol, 0.30f, 0.42f);
        Add("Polymer", k.Poly, "gunpoly", k.PolyCol, 0.0f, 0.55f);
        Add("Metal", k.Metal, "gunmetal", new Color(0.360f, 0.368f, 0.380f), 0.55f, 0.30f);
        Add("Accent", k.Accent, "gunaccent", k.AccentCol, 0.22f, 0.34f);
        Add("Dark", k.Dark, "gundark", new Color(0.085f, 0.088f, 0.094f), 0.0f, 0.72f);
        Add("Markings", k.White, "gunwhite", new Color(0.820f, 0.800f, 0.740f), 0.0f, 0.58f);
        var magazine = new Node3D { Name = "Magazine" };
        if (!k.Mag.Empty) magazine.AddChild(Part("MagazineBody", k.Mag, "gunmag", new Color(0.235f, 0.240f, 0.252f), 0.05f, 0.52f));
        if (!k.MagTrim.Empty) magazine.AddChild(Part("MagazineTrim", k.MagTrim, "gunmetal", new Color(0.310f, 0.315f, 0.325f), 0.35f, 0.40f));
        root.AddChild(magazine);
        root.AddChild(new Marker3D { Name = "Muzzle", Position = k.Muzzle });
        return root;
    }

    static MeshInstance3D Part(string name, VmMesh mesh, string material, Color albedo, float metallic, float roughness)
    {
        var mi = new MeshInstance3D { Name = name, Mesh = new ArrayMesh() };
        var sm = new StandardMaterial3D { ResourceName = material, AlbedoColor = albedo, Metallic = metallic, Roughness = roughness };
        mesh.AddTo((ArrayMesh)mi.Mesh, sm);
        return mi;
    }
}
