using Godot;
using CaValAim.Game;
using CaValAim.Game.Weapon;
using CaValAim.Audio;

namespace CaValAim.Analysis;

/// <summary>Regression cases for real movement paths, timing and spatial audio.</summary>
static class GameplayChecks
{
    public static void Run(Action<string> report)
    {
        void Check(bool ok, string label) => report($"  {(ok ? "PASS" : "FAIL")} gameplay: {label}");
        foreach (var (name, make) in new (string, Action<VmMesh>)[]
        {
            ("box", m => m.Box(Vector3.One, Transform3D.Identity)),
            ("beveled receiver", m => m.ChamferZ(-1, 1, Vector2.One, Vector2.One, 0, 0, 0.2f)),
            ("descending magazine", m => m.ChamferPathY(new[] { (1f, 1f, 1f, 0f), (-1f, 1f, 1f, 0f) }, 0.2f, Transform3D.Identity)),
        })
        {
            var source = new VmMesh();
            make(source);
            using var mesh = new ArrayMesh();
            using var material = new StandardMaterial3D();
            source.AddTo(mesh, material);
            var arrays = mesh.SurfaceGetArrays(0);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            bool exterior = indices.Length > 0;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                var face = (vertices[b] - vertices[a]).Cross(vertices[c] - vertices[a]);
                exterior &= face.Dot(vertices[a] + vertices[b] + vertices[c]) < 0
                    && normals[a].Dot(vertices[a]) > 0;
            }
            Check(exterior, $"{name} faces are visible from outside");
        }
        foreach (bool indexed in new[] { false, true })
        {
            var world = new List<Box>
            {
                new(new Vector3(1, 0, -5), new Vector3(1.03f, 3, 5)),
            };
            if (indexed)
                for (int i = 0; i < 40; i++)
                    world.Add(new Box(new Vector3(20 + i, 0, 20), new Vector3(20.1f + i, 3, 21)));
            var feet = Vector3.Zero;
            var velocity = new Vector3(30, 0, 2);
            Collision.MoveAndSlide(ref feet, ref velocity, new Vector3(4, 0, 1), world);
            Check(Mathf.Abs(feet.X - (1 - Collision.PlayerRadius)) < 0.001f
                && Mathf.Abs(feet.Z - 1) < 0.001f && velocity.X == 0 && velocity.Z == 2,
                $"swept thin wall, slide preserved (grid={indexed})");
            feet = new Vector3(4, 0, 0); velocity = new Vector3(-30, 0, 0);
            Collision.MoveAndSlide(ref feet, ref velocity, new Vector3(-5, 0, 0), world);
            Check(Mathf.Abs(feet.X - (1.03f + Collision.PlayerRadius)) < 0.001f,
                $"reverse sweep stops on near face (grid={indexed})");
        }
        var ceiling = new List<Box> { new(new Vector3(-5, 1.4f, -5), new Vector3(5, 2, 5)) };
        var crouched = Vector3.Zero; var vel = new Vector3(2, 0, 0);
        Collision.MoveAndSlide(ref crouched, ref vel, new Vector3(1, 0, 0), ceiling, 0, MovementTuning.CrouchHeight);
        Check(crouched.X == 1 && Collision.Ceiling(crouched, ceiling, 1.2f) == 1.4f,
            "crouched passage preserves low ceiling");
        var keys = new CaValAim.Valorant.ValorantProfile();
        var oldInput = Mover.KeyOverride;
        try
        {
            bool crouchHeld = true;
            Mover.KeyOverride = key => crouchHeld && key == keys.Binds.Primary(CaValAim.Valorant.GameAction.Crouch);
            var floor = new Box(new Vector3(-10, -1, -10), new Vector3(10, 0, 10));
            var tunnel = new List<Box>(ceiling) { floor };
            var mover = new Mover();
            var feet = Vector3.Zero;
            mover.Place(ref feet, tunnel);
            mover.Step(1f / 60, 0, keys, ref feet, tunnel);
            crouchHeld = false;
            mover.Step(1f / 60, 0, keys, ref feet, tunnel);
            Check(mover.BodyCrouched && mover.Crouching && mover.BodyHeight == MovementTuning.CrouchHeight,
                "releasing crouch under a roof keeps both body and movement crouched");
            mover.Step(1f / 60, 0, keys, ref feet, new[] { floor });
            Check(!mover.BodyCrouched && !mover.Crouching,
                "leaving the low roof restores standing automatically");
        }
        finally { Mover.KeyOverride = oldInput; }
        var stair = new List<Box> { new(new Vector3(-2, 2, -2), new Vector3(2, 3, 2)) };
        Check(Mathf.Abs(Collision.HeadroomStep(Vector3.Zero, stair, 0.7f, 1.8f) - 0.2f) < 0.001f,
            "step height respects headroom");
        float RecoverAt(int fps)
        {
            float h = 4;
            for (int i = 0; i < fps / 2; i++) h = WeaponController.Recover(h, 1f / fps, 0.375f);
            return h;
        }
        Check(Mathf.Abs(RecoverAt(30) - RecoverAt(144)) < 0.0001f
            && Mathf.Abs(RecoverAt(30) - 4f / 9f) < 0.0001f,
            "recoil recovery agrees at 30 and 144 FPS");
        var health = new Modes.ContactHealth();
        health.Tick(true, 0.25f, 0.5f, 0.3f, 1f);
        health.Tick(false, 0.2f, 0.5f, 0.3f, 1f);
        Check(Mathf.Abs(health.Progress - 0.25f) < 0.0001f, "regen preserves damage during grace period");
        health.Tick(false, 0.3f, 0.5f, 0.3f, 1f);
        Check(Mathf.Abs(health.Progress - 0.15f) < 0.0001f, "regen heals only the part of a frame beyond grace");
        health.Tick(true, 0.35f, 0.5f, 0.3f, 1f);
        Check(Mathf.Abs(health.Progress - 0.5f) < 0.0001f, "reacquiring finishes remaining target health");
        health.Reset();
        Check(health.Progress == 0, "respawn resets target health");
        float previous = 1;
        bool monotonic = true;
        for (int distance = 0; distance <= 45; distance++)
        {
            float gain = Sfx.DistanceGain(distance, 40, 0.45f);
            monotonic &= gain >= 0 && gain <= previous;
            previous = gain;
        }
        Check(monotonic && Sfx.DistanceGain(40, 40, 0.45f) == 0,
            "spatial audio fades monotonically to silence");
    }
}
