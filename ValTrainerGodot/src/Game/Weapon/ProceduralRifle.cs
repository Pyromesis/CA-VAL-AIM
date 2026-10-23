using Godot;
using ValTrainer.Core;

namespace ValTrainer.Game.Weapon;

/// <summary>
/// Hand-built first-person Vandal and Phantom. The old viewmodels were two generic CC0 FBX rifles with flat
/// materials, so they read as placeholder art up close. These meshes are authored in gun space (grip origin,
/// barrel -Z) with the silhouettes, rails, vents, sights, curved magazine and cut-out stocks of the two rifles.
/// <para>Every part is a flat-shaded mesh surface with a named material slot; <see cref="Viewmodel"/> then swaps
/// those slots for the viewmodel shader's metal / polymer / accent finishes. Keeping the mesh CPU-built means
/// the weapons travel in the DLL and need no new pck assets. The magazine is a separate node so a reload hides
/// it cleanly instead of clipping a box out of the receiver.</para>
/// </summary>
public static class ProceduralRifle
{
    static readonly Transform3D I = Transform3D.Identity;

    static Transform3D T(float x, float y, float z) => new(Basis.Identity, new Vector3(x, y, z));
    static Transform3D RX(float deg) => new(new Basis(Vector3.Right, Mathf.DegToRad(deg)), Vector3.Zero);
    static Transform3D RY(float deg) => new(new Basis(Vector3.Up, Mathf.DegToRad(deg)), Vector3.Zero);

    public static Node3D Build(WeaponKind kind)
    {
        bool phantom = kind == WeaponKind.Phantom;
        var body = new VmMesh();
        var poly = new VmMesh();
        var metal = new VmMesh();
        var accent = new VmMesh();
        var dark = new VmMesh();
        var white = new VmMesh();
        var mag = new VmMesh();
        var magTrim = new VmMesh();

        float hgFront = phantom ? -0.490f : -0.470f;

        // ------------------------------------------------ receiver ------------------------------------------------
        // The receiver is a tapered slab (wider at the rear) with a separate lower chassis, so the side view gets
        // a shoulder line instead of one extruded box.
        body.LoftZ(-0.250f, 0.096f, new Vector2(0.027f, 0.036f), new Vector2(0.026f, 0.044f), 0.062f, 0.060f, I);
        body.LoftZ(-0.268f, -0.250f, new Vector2(0.023f, 0.030f), new Vector2(0.027f, 0.036f), 0.060f, 0.062f, I);
        dark.LoftZ(-0.238f, 0.082f, new Vector2(0.021f, 0.010f), new Vector2(0.023f, 0.012f), 0.022f, 0.024f, I);
        // raised spine under the rail
        metal.LoftZ(-0.232f, 0.060f, new Vector2(0.016f, 0.006f), new Vector2(0.016f, 0.007f), 0.104f, 0.106f, I);

        // ejection port (right side) with its dark opening and machined lips
        dark.Box(new Vector3(0.0015f, 0.012f, 0.030f), T(0.0276f, 0.073f, -0.072f));
        metal.Box(new Vector3(0.0012f, 0.0022f, 0.032f), T(0.0286f, 0.059f, -0.072f));
        metal.Box(new Vector3(0.0012f, 0.0022f, 0.032f), T(0.0286f, 0.087f, -0.072f));
        // bolt / forward assist
        metal.FacetTubeZ(-0.002f, 0.002f, 0.0062f, 0.0062f, 10, 1f, 1f, T(0.0282f, 0.042f, 0.018f) * RY(90));
        dark.FacetTubeZ(-0.001f, 0.001f, 0.0032f, 0.0032f, 8, 1f, 1f, T(0.0292f, 0.042f, 0.018f) * RY(90));

        if (phantom)
        {
            // charging handle at the rear, in a shallow channel
            metal.Box(new Vector3(0.014f, 0.004f, 0.020f), T(0f, 0.102f, 0.078f));
            dark.Box(new Vector3(0.021f, 0.005f, 0.004f), T(0f, 0.100f, 0.064f));
            // angular polymer cheek panels
            poly.Box(new Vector3(0.0022f, 0.017f, 0.055f), T(0.0278f, 0.048f, 0.026f));
            poly.Box(new Vector3(0.0022f, 0.017f, 0.055f), T(-0.0278f, 0.048f, 0.026f));
        }
        else
        {
            // Vandal: side charging handle with a machined knob
            metal.FacetTubeZ(0f, 0.026f, 0.0048f, 0.0048f, 8, 1f, 1f, T(0.030f, 0.079f, 0.050f) * RY(90));
            metal.RoundBox(new Vector3(0.006f, 0.007f, 0.007f), 0.55f, T(0.056f, 0.079f, 0.050f));
            dark.Box(new Vector3(0.0012f, 0.002f, 0.040f), T(0.0288f, 0.038f, 0.016f));
        }

        // top rail: a low base with short Picatinny teeth (the old block was a saw blade)
        Rail(metal, -0.246f, 0.058f, 0.111f);

        // ------------------------------------------------ handguard -----------------------------------------------
        if (phantom)
        {
            // slim 12-facet polymer tube with an oval vent bank
            poly.FacetTubeZ(hgFront, -0.246f, 0.033f, 0.037f, 12, 1f, 0.88f, T(0f, 0.062f, 0f));
            poly.FacetTubeZ(hgFront - 0.012f, hgFront, 0.028f, 0.033f, 12, 1f, 0.88f, T(0f, 0.062f, 0f));
            for (int i = 0; i < 5; i++)
            {
                float z = -0.440f + i * 0.040f;
                dark.Box(new Vector3(0.0015f, 0.009f, 0.006f), T(0.0366f, 0.062f, z));
                dark.Box(new Vector3(0.0015f, 0.009f, 0.006f), T(-0.0366f, 0.062f, z));
            }
            // accessory rail underneath
            metal.Box(new Vector3(0.011f, 0.004f, 0.090f), T(0f, 0.030f, -0.360f));
            for (int i = 0; i < 5; i++)
                metal.Box(new Vector3(0.008f, 0.0014f, 0.0025f), T(0f, 0.0272f, -0.440f + i * 0.036f));
        }
        else
        {
            // Vandal: octagonal, slightly flattened handguard with three angled heat slots per side
            poly.FacetTubeZ(hgFront, -0.246f, 0.035f, 0.041f, 8, 1f, 0.80f, T(0f, 0.062f, 0f));
            poly.FacetTubeZ(hgFront - 0.014f, hgFront, 0.030f, 0.035f, 8, 1f, 0.80f, T(0f, 0.062f, 0f));
            for (int i = 0; i < 3; i++)
            {
                float z = -0.430f + i * 0.055f;
                dark.Box(new Vector3(0.0015f, 0.010f, 0.024f), T(0.0392f, 0.060f, z) * RX(22f));
                dark.Box(new Vector3(0.0015f, 0.010f, 0.024f), T(-0.0392f, 0.060f, z) * RX(22f));
            }
            metal.FacetTubeZ(-0.250f, -0.240f, 0.042f, 0.042f, 8, 1f, 0.80f, T(0f, 0.062f, 0f));
            // short top rail on the handguard, one step below the receiver rail
            Rail(metal, -0.452f, -0.258f, 0.100f);
            // underside rail and heat-shield ribs
            metal.Box(new Vector3(0.011f, 0.004f, 0.095f), T(0f, 0.029f, -0.355f));
            for (int i = 0; i < 3; i++)
                metal.Box(new Vector3(0.020f, 0.0022f, 0.003f), T(0f, 0.034f, -0.300f - i * 0.048f));
        }

        // ------------------------------------------------ barrel / muzzle -----------------------------------------
        if (phantom)
        {
            // suppressor with three cooling rings
            metal.FacetTubeZ(-0.690f, -0.486f, 0.0225f, 0.0215f, 10, 1f, 1f, T(0f, 0.078f, 0f));
            for (int i = 0; i < 3; i++)
                metal.FacetTubeZ(-0.606f + i * 0.042f, -0.598f + i * 0.042f, 0.025f, 0.025f, 10, 1f, 1f, T(0f, 0.078f, 0f));
            dark.FacetTubeZ(-0.692f, -0.687f, 0.016f, 0.016f, 10, 1f, 1f, T(0f, 0.078f, 0f));
        }
        else
        {
            metal.FacetTubeZ(-0.628f, -0.468f, 0.0100f, 0.0118f, 10, 1f, 1f, T(0f, 0.078f, 0f));
            // muzzle brake with side slots
            metal.FacetTubeZ(-0.674f, -0.626f, 0.016f, 0.0152f, 6, 1f, 1f, T(0f, 0.078f, 0f));
            dark.FacetTubeZ(-0.676f, -0.672f, 0.013f, 0.013f, 6, 1f, 1f, T(0f, 0.078f, 0f));
            for (int i = 0; i < 3; i++)
            {
                dark.Box(new Vector3(0.002f, 0.005f, 0.008f), T(0.016f, 0.086f, -0.658f));
                dark.Box(new Vector3(0.002f, 0.005f, 0.008f), T(-0.016f, 0.086f, -0.658f));
            }
            // gas block in front of the handguard
            metal.Box(new Vector3(0.015f, 0.014f, 0.014f), T(0f, 0.091f, -0.482f));
        }

        // front sight: narrow centred post, protected by two low wings
        metal.Box(new Vector3(0.017f, 0.011f, 0.012f), T(0f, 0.101f, -0.490f));
        metal.Box(new Vector3(0.0032f, 0.021f, 0.005f), T(0f, 0.141f, -0.490f));
        metal.Box(new Vector3(0.0022f, 0.003f, 0.004f), T(0f, 0.1605f, -0.490f));
        metal.Box(new Vector3(0.0035f, 0.017f, 0.0065f), T(0.0112f, 0.125f, -0.490f));
        metal.Box(new Vector3(0.0035f, 0.017f, 0.0065f), T(-0.0112f, 0.125f, -0.490f));
        dark.Box(new Vector3(0.005f, 0.0018f, 0.0018f), T(0f, 0.1585f, -0.490f));

        // ------------------------------------------------ rear sight -----------------------------------------------
        metal.Box(new Vector3(0.019f, 0.004f, 0.026f), T(0f, 0.119f, 0.030f));
        metal.Box(new Vector3(0.0032f, 0.011f, 0.008f), T(0.0122f, 0.134f, 0.030f));
        metal.Box(new Vector3(0.0032f, 0.011f, 0.008f), T(-0.0122f, 0.134f, 0.030f));
        metal.Box(new Vector3(0.011f, 0.0032f, 0.006f), T(0f, 0.1465f, 0.030f));
        dark.Box(new Vector3(0.006f, 0.003f, 0.002f), T(0f, 0.137f, 0.030f));

        // ------------------------------------------------ stock ---------------------------------------------------
        if (phantom)
        {
            // buffer tube and an adjustable frame stock with a filled cheek piece and a rubber butt pad
            metal.FacetTubeZ(0.082f, 0.200f, 0.015f, 0.0135f, 10, 1f, 1f, T(0f, 0.082f, 0f));
            poly.LoftZ(0.178f, 0.292f, new Vector2(0.022f, 0.011f), new Vector2(0.020f, 0.013f), 0.093f, 0.091f, I);
            poly.LoftZ(0.188f, 0.292f, new Vector2(0.018f, 0.008f), new Vector2(0.017f, 0.009f), 0.036f, 0.034f, I);
            poly.LoftZ(0.280f, 0.302f, new Vector2(0.021f, 0.047f), new Vector2(0.019f, 0.050f), 0.064f, 0.061f, I);
            poly.Box(new Vector3(0.017f, 0.027f, 0.009f), T(0f, 0.066f, 0.190f));
            poly.Box(new Vector3(0.007f, 0.007f, 0.088f), T(0f, 0.061f, 0.240f) * RX(-13f));
            dark.Box(new Vector3(0.022f, 0.004f, 0.086f), T(0f, 0.105f, 0.240f));
            dark.Box(new Vector3(0.020f, 0.045f, 0.004f), T(0f, 0.061f, 0.304f));
            metal.Box(new Vector3(0.017f, 0.003f, 0.012f), T(0f, 0.070f, 0.205f));
            accent.Box(new Vector3(0.0012f, 0.0035f, 0.065f), T(0.0215f, 0.097f, 0.235f));
            accent.Box(new Vector3(0.0012f, 0.0035f, 0.065f), T(-0.0215f, 0.097f, 0.235f));
        }
        else
        {
            // fixed skeleton stock: cheek comb, lower spar, butt plate and a diagonal brace leave a real cut-out
            poly.LoftZ(0.088f, 0.292f, new Vector2(0.023f, 0.011f), new Vector2(0.021f, 0.012f), 0.095f, 0.098f, I);
            poly.LoftZ(0.092f, 0.292f, new Vector2(0.019f, 0.008f), new Vector2(0.018f, 0.009f), 0.031f, 0.033f, I);
            poly.LoftZ(0.274f, 0.300f, new Vector2(0.022f, 0.051f), new Vector2(0.020f, 0.053f), 0.060f, 0.058f, I);
            poly.Box(new Vector3(0.018f, 0.032f, 0.009f), T(0f, 0.062f, 0.098f));
            poly.Box(new Vector3(0.0075f, 0.0075f, 0.098f), T(0f, 0.060f, 0.180f) * RX(-19f));
            dark.Box(new Vector3(0.022f, 0.004f, 0.078f), T(0f, 0.1065f, 0.180f));
            dark.Box(new Vector3(0.021f, 0.051f, 0.004f), T(0f, 0.058f, 0.302f));
            metal.Box(new Vector3(0.016f, 0.003f, 0.012f), T(0f, 0.070f, 0.288f));
            accent.Box(new Vector3(0.0012f, 0.0032f, 0.070f), T(0.0225f, 0.097f, 0.185f));
            accent.Box(new Vector3(0.0012f, 0.0032f, 0.070f), T(-0.0225f, 0.097f, 0.185f));
        }

        // ------------------------------------------------ grip / trigger ------------------------------------------
        float gripZTop = 0.020f;
        float gripZBottom = phantom ? 0.060f : 0.056f;
        poly.LoftY(-0.150f, 0.012f, new Vector2(0.019f, 0.025f), new Vector2(0.024f, 0.033f), gripZBottom, gripZTop, I);
        poly.Box(new Vector3(0.017f, 0.009f, 0.028f), T(0f, 0.006f, gripZTop + 0.030f));
        dark.Box(new Vector3(0.0012f, 0.045f, 0.014f), T(0.0222f, -0.068f, gripZTop + 0.024f));
        dark.Box(new Vector3(0.0012f, 0.045f, 0.014f), T(-0.0222f, -0.068f, gripZTop + 0.024f));
        // trigger guard
        metal.Box(new Vector3(0.0032f, 0.019f, 0.0038f), T(0f, -0.010f, -0.082f));
        metal.Box(new Vector3(0.0032f, 0.0032f, 0.052f), T(0f, -0.029f, -0.034f));
        metal.Box(new Vector3(0.0032f, 0.009f, 0.0038f), T(0f, -0.021f, 0.012f));
        metal.Box(new Vector3(0.0024f, 0.013f, 0.003f), T(0f, -0.004f, -0.044f) * RX(-14f));
        // magazine release
        metal.FacetTubeZ(-0.001f, 0.001f, 0.0065f, 0.0065f, 10, 1f, 1f, T(0.0235f, 0.010f, -0.048f));

        // ------------------------------------------------ magazine -------------------------------------------------
        // Separate node: the reload hides it wholesale. Both magazines grow down from the well and rake forward.
        float seg = phantom ? 0.036f : 0.034f;
        float rake = phantom ? 0.006f : 0.016f;
        float y = 0.012f;
        for (int i = 0; i < 5; i++)
        {
            float y2 = y - seg;
            float zc = -0.168f - rake * i;
            mag.LoftY(y2, y, new Vector2(0.017f, 0.027f), new Vector2(0.0175f, 0.028f), zc - rake * 0.4f, zc + rake * 0.4f, I);
            dark.Box(new Vector3(0.0012f, 0.0022f, 0.020f), T(0.0185f, y - 0.014f, zc));
            dark.Box(new Vector3(0.0012f, 0.0022f, 0.020f), T(-0.0185f, y - 0.014f, zc));
            y = y2;
        }
        // floor plate + front rib
        magTrim.Box(new Vector3(0.0195f, 0.006f, 0.030f), T(0f, y - 0.002f, -0.168f - rake * 4.4f));
        magTrim.Box(new Vector3(0.003f, phantom ? 0.100f : 0.095f, 0.0035f), T(0f, 0.012f - (phantom ? 0.058f : 0.056f), -0.192f - rake * 1.6f));

        // ------------------------------------------------ finish marks -------------------------------------------
        if (phantom)
        {
            accent.Box(new Vector3(0.0012f, 0.0022f, 0.062f), T(0.0368f, 0.074f, -0.350f));
            accent.Box(new Vector3(0.0012f, 0.0022f, 0.062f), T(-0.0368f, 0.074f, -0.350f));
            accent.Box(new Vector3(0.0012f, 0.0028f, 0.045f), T(0.0278f, 0.090f, -0.018f));
            accent.Box(new Vector3(0.0012f, 0.0028f, 0.045f), T(-0.0278f, 0.090f, -0.018f));
            white.Box(new Vector3(0.0012f, 0.003f, 0.016f), T(0.0279f, 0.064f, 0.024f));
            white.Box(new Vector3(0.0012f, 0.003f, 0.016f), T(-0.0279f, 0.064f, 0.024f));
            accent.RoundBox(new Vector3(0.0025f, 0.0025f, 0.0025f), 0.9f, T(0.028f, 0.058f, -0.010f));
        }
        else
        {
            accent.Box(new Vector3(0.0012f, 0.0028f, 0.075f), T(0.0396f, 0.070f, -0.345f));
            accent.Box(new Vector3(0.0012f, 0.0028f, 0.075f), T(-0.0396f, 0.070f, -0.345f));
            accent.Box(new Vector3(0.0012f, 0.0028f, 0.058f), T(0.0278f, 0.091f, -0.020f));
            accent.Box(new Vector3(0.0012f, 0.0028f, 0.058f), T(-0.0278f, 0.091f, -0.020f));
            // stencil marks on both sides of the receiver
            white.Box(new Vector3(0.0012f, 0.003f, 0.014f), T(0.0279f, 0.060f, 0.022f));
            white.Box(new Vector3(0.0012f, 0.003f, 0.014f), T(-0.0279f, 0.060f, 0.022f));
            white.Box(new Vector3(0.0012f, 0.003f, 0.009f), T(0.0279f, 0.054f, 0.006f) * RX(28f));
            white.Box(new Vector3(0.0012f, 0.003f, 0.009f), T(-0.0279f, 0.054f, 0.006f) * RX(28f));
            // side screws
            foreach (float z in new[] { -0.118f, -0.182f, 0.042f })
            {
                metal.FacetTubeZ(-0.001f, 0.001f, 0.0032f, 0.0032f, 8, 1f, 1f, T(0.0278f, 0.035f, z) * RY(90));
                metal.FacetTubeZ(-0.001f, 0.001f, 0.0032f, 0.0032f, 8, 1f, 1f, T(-0.0278f, 0.035f, z) * RY(90));
            }
        }

        // ------------------------------------------------ assembly -------------------------------------------------
        // Albedo is deliberately mid-gray: the viewmodel is lit by a dim sky ambient with the sun usually behind the
        // camera, so physically dark values read as pure black on screen. Metallic stays below 0.6 for the same reason.
        Color bodyCol = phantom ? new Color(0.255f, 0.270f, 0.300f) : new Color(0.300f, 0.292f, 0.280f);
        var root = new Node3D { Name = "Gun" };
        root.AddChild(Part("Body", body, "gunbody", bodyCol, 0.30f, 0.42f));
        root.AddChild(Part("Polymer", poly, "gunpoly", new Color(0.185f, 0.190f, 0.200f), 0.0f, 0.55f));
        root.AddChild(Part("Metal", metal, "gunmetal", new Color(0.360f, 0.368f, 0.380f), 0.55f, 0.30f));
        root.AddChild(Part("Accent", accent, "gunaccent", phantom ? new Color(0.050f, 0.560f, 0.600f) : new Color(0.720f, 0.055f, 0.065f), 0.22f, 0.34f));
        root.AddChild(Part("Dark", dark, "gundark", new Color(0.085f, 0.088f, 0.094f), 0.0f, 0.72f));
        root.AddChild(Part("Markings", white, "gunwhite", new Color(0.820f, 0.800f, 0.740f), 0.0f, 0.58f));
        var magazine = new Node3D { Name = "Magazine" };
        magazine.AddChild(Part("MagazineBody", mag, "gunmag", new Color(0.235f, 0.240f, 0.252f), 0.05f, 0.52f));
        magazine.AddChild(Part("MagazineTrim", magTrim, "gunmetal", new Color(0.310f, 0.315f, 0.325f), 0.35f, 0.40f));
        root.AddChild(magazine);

        root.AddChild(new Marker3D { Name = "Muzzle", Position = new Vector3(0f, 0.078f, phantom ? -0.700f : -0.674f) });
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
        m.Box(new Vector3(0.0125f, 0.0035f, (z1 - z0) * 0.5f), T(0f, y, (z0 + z1) * 0.5f));
        for (float z = z0 + 0.008f; z < z1 - 0.005f; z += 0.013f)
            m.Box(new Vector3(0.0095f, 0.0022f, 0.0022f), T(0f, y + 0.0055f, z));
    }
}
