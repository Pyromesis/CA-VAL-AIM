using System.Globalization;
using Godot;
using CaValAim.Core;
using CaValAim.Maps;
using CaValAim.Modes;
using CaValAim.Valorant;

namespace CaValAim.Game;

/// <summary>
/// DEV-ONLY movement self-checks. They run inside a session start (any --mode) and quit with the number of failures:
/// <list type="bullet">
/// <item><c>--movecheck</c>: physics on a synthetic course at 120 Hz: jump apex / air time vs <see cref="MovementTuning"/>,
/// running-jump distance, crouch-jump ledges, ceiling, ledge drops, fall damage, impulses (dash, air dash, updraft),
/// steer lock, teleport, speed scale, jump toggle, events, airborne / landing weapon error, and a
/// comparison of release and counter-strafe timing across frame rates.</item>
/// <item><c>--jumpaudit [all|&lt;map key&gt;|&lt;mode key&gt;]</c>: what a player can reach on a map (or a range drill's layout) by
/// walking, step-ups and (crouch-)jumps; lists reachable wall tops and spots outside the floor (map escapes), each
/// re-played with the real physics. Exit code = layouts with an escape.</item>
/// </list>
/// Example: <c>godot --headless --path CaValAim -- --dev --mode counterstrafe --movecheck</c>.
/// </summary>
public static class MovementCheck
{
    public static bool Requested => CmdLine.Dev && (CmdLine.Has("--movecheck") || CmdLine.Has("--jumpaudit"));

    const float Dt = 1f / 120f;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static int fails, passes;

    public static int Run(ValorantProfile _)
    {
        fails = passes = 0;
        var saved = Mover.KeyOverride;
        try
        {
            if (CmdLine.Has("--movecheck")) Physics();
            if (CmdLine.Has("--jumpaudit")) Audit(CmdLine.After("--jumpaudit"));
        }
        catch (Exception e) { Fail("exception", e.ToString()); }
        finally { Mover.KeyOverride = saved; }
        GD.Print($"[movecheck] RESULT: {(fails == 0 ? "PASS" : "FAIL")} ({passes} passed, {fails} failed)");
        return fails;
    }

    static void Check(string name, bool ok, string detail)
    {
        if (ok) passes++; else fails++;
        GD.Print($"[movecheck] {(ok ? "PASS" : "FAIL")} {name}: {detail}");
    }
    static void Fail(string name, string detail) => Check(name, false, detail);
    static string F(float v, string fmt = "0.000") => v.ToString(fmt, Inv);

    // ------------------------------------------------------------------ test rig

    /// <summary>A body on a synthetic course, driven by scripted keys.</summary>
    sealed class Rig
    {
        public readonly Mover M = new();
        public readonly List<Box> World;
        public readonly HashSet<InputBinding> Held = new();
        public readonly ValorantProfile Keys = new();
        public Vector3 Feet;
        public float Yaw, T, MaxFeet, MaxEye;
        public int Jumps, Lands;

        public Rig(List<Box> world, Vector3 feet, float yaw = 0)
        {
            World = world; Feet = feet; Yaw = yaw;
            M.Jumped += () => Jumps++;
            M.Landed += () => Lands++;
            M.Place(ref Feet, world);
            MaxFeet = Feet.Y; MaxEye = Eye.Y;
            Mover.KeyOverride = k => Held.Contains(k);
        }

        public Vector3 Eye => Feet + Vector3.Up * M.EyeHeight;

        public void Step(int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                M.Step(Dt, Yaw, Keys, ref Feet, World);
                T += Dt;
                MaxFeet = Mathf.Max(MaxFeet, Feet.Y);
                MaxEye = Mathf.Max(MaxEye, Eye.Y);
            }
        }

        public void Hold(params Key[] k) { foreach (var x in k) Held.Add(x); }
        public void Release(params Key[] k) { foreach (var x in k) Held.Remove(x); }

        /// <summary>Taps jump (one frame) then steps until landing (or <paramref name="max"/> seconds).</summary>
        public void JumpAndLand(float max = 3f, Action<Rig>? each = null)
        {
            int lands = Lands;
            Hold(Key.Space); Step(); Release(Key.Space);
            float t0 = T;
            while (Lands == lands && T - t0 < max) { each?.Invoke(this); Step(); }
        }

        public void RunUntil(Func<Rig, bool> done, float max = 5f)
        {
            float t0 = T;
            while (!done(this) && T - t0 < max) Step();
        }
    }

    static Box Floor(float half = 60) => new(new Vector3(-half, -0.1f, -half), new Vector3(half, 0, half));

    // ------------------------------------------------------------------ physics battery

    static void LandingAudioChecks()
    {
        var r = new Rig(new List<Box> { Floor() }, Vector3.Zero);
        r.JumpAndLand();
        Check("standing jump landing audible", r.M.AudibleLanding, F(r.M.LastLandingSpeed));
        r = new Rig(new List<Box> { Floor() }, Vector3.Zero);
        r.Hold(Key.Ctrl); r.Step(30);
        r.JumpAndLand(each: g => { if (g.M.AirTime > 0.1f) g.Release(Key.Ctrl); });
        Check("released crouch-jump landing silent", !r.M.AudibleLanding && r.M.LastJumpFromCrouch, F(r.M.LastLandingSpeed));
        r.Feet = new Vector3(0, 3, 0);
        r.M.Place(ref r.Feet, r.World);
        r.Step(120);
        Check("high drop audible after crouch jump", r.M.AudibleLanding && !r.M.LastJumpFromCrouch, F(r.M.LastLandingSpeed));
        r = new Rig(new List<Box> { Floor() }, new Vector3(0, 0.8f, 0));
        r.Step(120);
        Check("low walk-off silent", !r.M.AudibleLanding, F(r.M.LastLandingSpeed));

    }

    static void Physics()
    {
        LandingAudioChecks();
        float apex = MovementTuning.JumpApex, air = MovementTuning.JumpAirTime, tuck = MovementTuning.CrouchTuck;
        GD.Print($"[movecheck] tuning: jump {F(MovementTuning.JumpSpeed, "0.00")} m/s, gravity {F(MovementTuning.Gravity, "0.0")} m/s² → apex {F(apex)} m, " +
                 $"air {F(air)} s; crouch-jump +{F(tuck, "0.00")} m (reach {F(MovementTuning.CrouchJumpReach, "0.00")} m); air control {F(MovementTuning.AirAccel, "0")} m/s²");

        // Standing jump on flat ground.
        var r = new Rig(new List<Box> { Floor() }, Vector3.Zero);
        float eye0 = r.Eye.Y;
        r.JumpAndLand();
        var a = r.M.LastAir;
        Check("standing jump apex (feet)", Mathf.Abs(r.MaxFeet - apex) < 0.01f, $"{F(r.MaxFeet)} m, expected {F(apex)} m");
        Check("standing jump apex (eye)", Mathf.Abs(r.MaxEye - eye0 - apex) < 0.01f, $"{F(r.MaxEye - eye0)} m");
        Check("standing jump air time", Mathf.Abs(a.AirTime - air) < Dt * 1.5f, $"{F(a.AirTime)} s, expected {F(air)} s");
        Check("one jump / one landing event", r.Jumps == 1 && r.Lands == 1 && a.FromJump, $"jumps {r.Jumps}, lands {r.Lands}");

        // Holding the key doesn't re-jump.
        r = new Rig(new List<Box> { Floor() }, Vector3.Zero);
        r.Hold(Key.Space); r.Step((int)(2f / Dt)); r.Release(Key.Space);
        Check("holding jump jumps once", r.Jumps == 1, $"{r.Jumps} jumps in 2 s of holding");

        // Running jump (strafe right at full speed).
        r = new Rig(new List<Box> { Floor() }, Vector3.Zero);
        r.Hold(Key.D); r.Step(120);
        float x0 = r.Feet.X, v0 = r.M.Speed;
        r.JumpAndLand();
        float dist = r.Feet.X - x0;
        Check("running jump distance", Mathf.Abs(dist - v0 * air) < 0.06f, $"{F(dist, "0.00")} m at {F(v0, "0.00")} m/s (expected {F(v0 * air, "0.00")})");
        Check("no speed gain in the air", r.M.Speed <= Mover.RunSpeed + 1e-3f, $"{F(r.M.Speed, "0.00")} m/s after landing");

        // Air control: reverse in the air (jump-peek back-out).
        r = new Rig(new List<Box> { Floor() }, Vector3.Zero);
        r.Hold(Key.D); r.Step(120);
        x0 = r.Feet.X;
        float far = x0;
        r.JumpAndLand(each: g => { if (g.T > 0) { g.Release(Key.D); g.Hold(Key.A); } far = Mathf.Max(far, g.Feet.X); });
        r.Release(Key.A);
        Check("air control turns a jump back", far - x0 < 1.0f && r.Feet.X < far, $"out {F(far - x0, "0.00")} m, landed {F(r.Feet.X - x0, "0.00")} m from take-off");

        // Crouch-jump: legs tucked in the air.
        r = new Rig(new List<Box> { Floor() }, Vector3.Zero);
        r.JumpAndLand(each: g => { if (g.T > 0.1f) g.Hold(Key.Ctrl); });
        r.Release(Key.Ctrl);
        Check("crouch-jump feet apex", Mathf.Abs(r.MaxFeet - (apex + tuck)) < 0.01f, $"{F(r.MaxFeet)} m, expected {F(apex + tuck)} m");

        // Ledges: a plain jump clears (apex - 5 cm), needs a crouch-jump (apex + 15 cm), and too high (reach + 10 cm).
        LedgeTest("plain jump onto a ledge at apex − 0.05 m", apex - 0.05f, crouch: false, expect: true);
        LedgeTest("plain jump fails at apex + 0.15 m", apex + 0.15f, crouch: false, expect: false);
        LedgeTest("crouch-jump onto a ledge at apex + 0.15 m", apex + 0.15f, crouch: true, expect: true);
        LedgeTest("crouch-jump fails at reach + 0.10 m", MovementTuning.CrouchJumpReach + 0.1f, crouch: true, expect: false);
        LedgeTest("step-up 0.6 m without jumping", 0.6f, crouch: false, expect: true, jump: false);

        // Ceiling: a slab 2.3 m up stops the head.
        var slab = new Box(new Vector3(-3, 2.3f, -3), new Vector3(3, 2.6f, 3));
        r = new Rig(new List<Box> { Floor(), slab }, Vector3.Zero);
        r.JumpAndLand();
        float headTop = r.MaxFeet + Collision.PlayerHeight;
        Check("ceiling stops a rising head", headTop <= slab.Min.Y + 1e-3f && r.M.HitCeiling && r.M.LastAir.AirTime < air,
            $"head top {F(headTop)} m under a slab at {F(slab.Min.Y, "0.00")} m, air {F(r.M.LastAir.AirTime)} s");

        // Walking down a curb stays grounded; a ledge is a fall.
        var curb = new Box(new Vector3(-5, 0, -1), new Vector3(0, 0.3f, 1));
        r = new Rig(new List<Box> { Floor(), curb }, new Vector3(-1, 0.3f, 0));
        bool alwaysGrounded = true;
        r.Hold(Key.D);
        r.RunUntil(g => { alwaysGrounded &= g.M.Grounded; return g.Feet.X > 1.5f; });
        Check("walking off a 0.3 m curb stays on the ground", alwaysGrounded && r.Lands == 0 && Mathf.Abs(r.Feet.Y) < 1e-4f, $"lands {r.Lands}, feet {F(r.Feet.Y)}");
        var ledge = new Box(new Vector3(-5, 0, -1), new Vector3(0, 1f, 1));
        r = new Rig(new List<Box> { Floor(), ledge }, new Vector3(-1, 1f, 0));
        r.Hold(Key.D);
        r.RunUntil(g => g.Lands > 0 || g.T > 3f);
        Check("walking off a 1 m ledge falls and lands", r.Lands == 1 && !r.M.LastAir.FromJump && Mathf.Abs(r.M.LastAir.Drop - 1f) < 0.01f,
            $"lands {r.Lands}, drop {F(r.M.LastAir.Drop)} m, air {F(r.M.LastAir.AirTime)} s (expected {F(Mathf.Sqrt(2f / MovementTuning.Gravity))})");

        // Stairs: five 0.2 m steps. The body snaps up each one; the camera trails below and settles when you stop.
        var stair = new List<Box> { Floor() };
        for (int i = 0; i < 5; i++)
            stair.Add(new Box(new Vector3(1f + i * 0.35f, 0, -2), new Vector3(1.35f + i * 0.35f, 0.2f * (i + 1), 2)));
        r = new Rig(stair, Vector3.Zero);
        float trail = 0f;
        r.Hold(Key.D);
        r.RunUntil(g => { trail = Mathf.Min(trail, g.M.CamStepOffset); return g.Feet.Y >= 0.95f; }, 3f);
        r.Release(Key.D);
        r.Step(120);
        Check("stair camera glides instead of snapping", trail < -0.1f && Mathf.Abs(r.M.CamStepOffset) < 0.01f && r.Feet.Y >= 0.95f,
            $"camera trailed {F(trail)} m, settled {F(r.M.CamStepOffset)} m on step {F(r.Feet.Y)} m");
        Check("fall damage thresholds", MovementTuning.FallDamage(7.9f) == 0 && Mathf.Abs(MovementTuning.FallDamage(8f) - 15f) < 0.01f &&
            Mathf.Abs(MovementTuning.FallDamage(17f) - 100f) < 0.01f, $"7.9 m → {F(MovementTuning.FallDamage(7.9f), "0")}, 8 m → {F(MovementTuning.FallDamage(8f), "0")}, 17 m → {F(MovementTuning.FallDamage(17f), "0")}");

        // Weapon error: airborne and the landing penalty.
        r = new Rig(new List<Box> { Floor() }, Vector3.Zero);
        r.Hold(Key.Space); r.Step(); r.Release(Key.Space); r.Step(10);
        float airErr = r.M.MovementErrorDeg(Weapons.Vandal), opAir = r.M.MovementErrorDeg(Weapons.Operator);
        r.RunUntil(g => g.M.Grounded); r.Step();
        float landErr = r.M.MovementErrorDeg(Weapons.Vandal);
        bool landInacc = !r.M.Accurate;
        r.Step((int)(MovementTuning.LandingErrTime / Dt) + 2);
        Check("airborne / landing weapon error", airErr == Weapons.Vandal.AirErr && opAir == Weapons.Operator.AirErr && landErr >= MovementTuning.LandingErr && landInacc &&
            r.M.MovementErrorDeg(Weapons.Vandal) == 0 && r.M.Accurate,
            $"Vandal air {F(airErr, "0.0")}°, Operator air {F(opAir, "0.0")}°, after landing {F(landErr, "0.0")}°, then {F(r.M.MovementErrorDeg(Weapons.Vandal), "0.0")}°");

        // Impulses.
        r = new Rig(new List<Box> { Floor() }, Vector3.Zero);
        r.M.Impulse(new Vector3(12, 0, 0), 0.25f, r.Feet);
        r.Hold(Key.A); // steering is locked during the dash
        r.Step(30);
        float lockDist = r.Feet.X;
        r.Release(Key.A);
        r.RunUntil(g => g.M.Speed < 0.01f);
        Check("ground dash: 12 m/s locked 0.25 s", Mathf.Abs(lockDist - 3f) < 0.02f, $"{F(lockDist, "0.00")} m during the lock, {F(r.Feet.X, "0.00")} m total slide");
        r = new Rig(new List<Box> { Floor() }, Vector3.Zero);
        r.M.Impulse(new Vector3(0, 8, 0), 0, r.Feet);
        r.RunUntil(g => g.Lands > 0);
        float up = 64f / (2 * MovementTuning.Gravity);
        Check("updraft impulse (8 m/s up)", Mathf.Abs(r.MaxFeet - up) < 0.02f && r.Jumps == 0 && r.Lands == 1, $"apex {F(r.MaxFeet)} m (expected {F(up)}), jumps {r.Jumps}, lands {r.Lands}");
        r = new Rig(new List<Box> { Floor() }, Vector3.Zero);
        r.Hold(Key.Space); r.Step(); r.Release(Key.Space);
        r.Step(30);
        x0 = r.Feet.X;
        r.M.Impulse(new Vector3(10, 0, 0), 0.2f, r.Feet);
        r.Step(24);
        Check("air dash: 10 m/s locked 0.2 s", Mathf.Abs(r.Feet.X - x0 - 2f) < 0.02f && !r.M.Grounded, $"{F(r.Feet.X - x0, "0.00")} m in the air");
        r.RunUntil(g => g.M.Grounded);
        Check("air dash decays without the keys", r.M.Speed < 10f && r.M.Speed > Mover.RunSpeed, $"{F(r.M.Speed, "0.00")} m/s at landing");

        // Teleport / placement.
        r = new Rig(new List<Box> { Floor(), new(new Vector3(4, 0, -1), new Vector3(6, 1.5f, 1)) }, Vector3.Zero);
        var p = new Vector3(5, 1.6f, 0);
        r.M.Place(ref p, r.World);
        bool onBox = r.M.Grounded && Mathf.Abs(p.Y - 1.5f) < 1e-4f;
        p = new Vector3(-5, 3, 0);
        r.M.Place(ref p, r.World);
        r.Feet = p;
        bool falls = !r.M.Grounded;
        r.RunUntil(g => g.M.Grounded);
        Check("teleport: snaps onto floor, else falls from rest", onBox && falls && Mathf.Abs(r.Feet.Y) < 1e-4f && r.Jumps == 0,
            $"on box {onBox}, mid-air falls {falls}, landed at {F(r.Feet.Y)} m");

        // Speed scale and jump toggle.
        r = new Rig(new List<Box> { Floor() }, Vector3.Zero);
        r.M.SpeedScale = 1.25f; r.Hold(Key.D); r.Step(120);
        float fast = r.M.Speed;
        r.M.SpeedScale = 0.5f; r.Step(120);
        float slow = r.M.Speed;
        Check("speed scale 1.25 / 0.5", Mathf.Abs(fast - 6.75f) < 0.01f && Mathf.Abs(slow - 2.7f) < 0.01f, $"{F(fast, "0.00")} / {F(slow, "0.00")} m/s");

        // The gun in hand sets the speeds: Operator run 5.13, scoped 72% = 3.69, deadzone 27.5% of 5.13 = 1.41.
        r = new Rig(new List<Box> { Floor() }, Vector3.Zero);
        r.M.Hold(Weapons.Operator); r.Hold(Key.D); r.Step(120);
        float opRun = r.M.Speed;
        r.M.Ads = true; r.Step(120);
        float opScoped = r.M.Speed;
        Check("Operator in hand: run / scoped / deadzone", Mathf.Abs(opRun - 5.13f) < 0.01f && Mathf.Abs(opScoped - 5.13f * 0.72f) < 0.01f
            && Mathf.Abs(r.M.AccurateMax - 5.13f * 0.275f) < 1e-4f, $"{F(opRun, "0.00")} / {F(opScoped, "0.00")} m/s, accurate ≤ {F(r.M.AccurateMax)} m/s");
        r = new Rig(new List<Box> { Floor() }, Vector3.Zero) { };
        r.M.JumpEnabled = false;
        r.JumpAndLand(0.5f);
        Check("jump disabled", r.Jumps == 0 && r.M.Grounded && r.MaxFeet == 0, $"jumps {r.Jumps}");

        GroundRegression();
    }

    static void LedgeTest(string name, float h, bool crouch, bool expect, bool jump = true)
    {
        var box = new Box(new Vector3(1.0f, 0, -2), new Vector3(4, h, 2));
        var r = new Rig(new List<Box> { Floor(), box }, new Vector3(-1.5f, 0, 0));
        r.Hold(Key.D);
        r.RunUntil(g => g.Feet.X >= 1.0f - Collision.PlayerRadius - 0.02f, 3f);
        if (jump)
        {
            r.JumpAndLand(each: g => { if (crouch && g.M.AirTime > 0.08f) g.Hold(Key.Ctrl); });
            r.Release(Key.Ctrl);
        }
        r.Step(60);
        bool on = r.Feet.Y >= h - 1e-3f;
        Check(name, on == expect, $"ledge {F(h, "0.00")} m: feet end at {F(r.Feet.Y, "0.00")} m ({(on ? "on top" : "below")})");
    }

    // ------------------------------------------------------------------ flat ground timing at different frame rates

    static void GroundRegression()
    {
        var keys = new ValorantProfile();
        var floor = new List<Box> { Floor() };
        (float StopMs, float Travel, float Accelerate) Measure(int hz, bool counter)
        {
            var mover = new Mover();
            var feet = Vector3.Zero;
            Key held = Key.D;
            float dt = 1f / hz;
            Mover.KeyOverride = binding => held != Key.None && binding == (InputBinding)held;
            for (int i = 0; i < hz; i++) mover.Step(dt, 0, keys, ref feet, floor);
            float atRelease = feet.X;
            held = counter ? Key.A : Key.None;
            float farthest = feet.X;
            for (int i = 0; i < hz / 3; i++)
            {
                mover.Step(dt, 0, keys, ref feet, floor);
                farthest = Math.Max(farthest, feet.X);
            }
            return (mover.StopTimesMs.Single(), farthest - atRelease, atRelease);
        }

        foreach (int hz in new[] { 30, 60, 120, 240 })
        {
            var release = Measure(hz, false);
            var counter = Measure(hz, true);
            Check($"ground timing at {hz} Hz", MathF.Abs(release.StopMs - 103f) < 1f
                && MathF.Abs(counter.StopMs - 97.2f) < 2f
                && MathF.Abs(release.Travel - .394f) < .01f
                && counter.Travel < release.Travel && MathF.Abs(release.Accelerate - 4.59f) < .01f,
                $"release {F(release.StopMs)} ms / {F(release.Travel)} m, counter {F(counter.StopMs)} ms / {F(counter.Travel)} m");
        }
        Mover.KeyOverride = null;
    }

    // ------------------------------------------------------------------ jump audit

    /// <summary>A walkable level in a grid cell: feet height, the box that carries it (-1 = ground plane) and the head room.</summary>
    readonly record struct Level(float Y, int Box, float Ceil, bool Stand);

    static void Audit(string? which)
    {
        which = string.IsNullOrWhiteSpace(which) || which.StartsWith("--", StringComparison.Ordinal) ? "all" : which.ToLowerInvariant();
        var layouts = new List<(string Name, List<Box> World, BoxDef[]? Defs, Vector3 Start)>();
        if (which == "all") foreach (var m in MapSpots.All) layouts.Add((m.Key, m.Solid.ToList(), m.Boxes, m.StartFeet));
        else if (MapSpots.All.FirstOrDefault(m => m.Key == which) is { } map) layouts.Add((map.Key, map.Solid.ToList(), map.Boxes, map.StartFeet));
        else if (ModeRegistry.Find(which) is { } make)
        {
            var mode = make();
            if (mode.Map is { } mm) layouts.Add((mm.Key, mm.Solid.ToList(), mm.Boxes, mode.StartFeet));
            else layouts.Add(($"range:{mode.Key}", RangeWorld(mode.ExtraSolids), null, mode.StartFeet));
        }
        else { Fail("jumpaudit", $"unknown map or mode '{which}'"); return; }
        foreach (var l in layouts) AuditLayout(l.Name, l.World, l.Defs, l.Start);
    }

    /// <summary>The shooting range's solids (as built by Environments.BuildRange) plus a drill's extra boxes.</summary>
    static List<Box> RangeWorld(IEnumerable<Box> extra)
    {
        const float hw = CaValAim.World.Environments.RangeHalfWidth, back = CaValAim.World.Environments.RangeBack, front = CaValAim.World.Environments.RangeFront, h = 16f;
        var w = new List<Box>
        {
            new(new Vector3(-hw, -0.1f, front), new Vector3(hw, 0, back)),
            new(new Vector3(-hw, 0, front - 1), new Vector3(hw, h, front)),
            new(new Vector3(-hw, 0, back), new Vector3(hw, h, back + 1)),
            new(new Vector3(-hw - 1, 0, front), new Vector3(-hw, h, back)),
            new(new Vector3(hw, 0, front), new Vector3(hw + 1, h, back)),
        };
        w.AddRange(extra);
        return w;
    }

    static void AuditLayout(string name, List<Box> world, BoxDef[]? defs, Vector3 start)
    {
        const float cell = 0.25f;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        float minX = world.Min(b => b.Min.X) - 1.5f, maxX = world.Max(b => b.Max.X) + 1.5f;
        float minZ = world.Min(b => b.Min.Z) - 1.5f, maxZ = world.Max(b => b.Max.Z) + 1.5f;
        int nx = (int)((maxX - minX) / cell) + 1, nz = (int)((maxZ - minZ) / cell) + 1;
        Vector3 Center(int i, int j, float y = 0) => new(minX + (i + 0.5f) * cell, y, minZ + (j + 0.5f) * cell);

        // Map footprint: every floor / elevated-floor box (outside all of them = out of the map).
        var floors = defs?.Where(d => d.Surf is Surf.Floor or Surf.Elevated).Select(d => d.Bounds).ToList()
                     ?? new List<Box> { world[0] };
        bool Inside(Vector3 p) => floors.Any(f => p.X >= f.Min.X && p.X <= f.Max.X && p.Z >= f.Min.Z && p.Z <= f.Max.Z);

        // Walkable levels per cell: box tops (and the ground plane) under the footprint where a crouched body fits.
        var levels = new List<Level>[nx * nz];
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                var c = Center(i, j);
                var list = new List<Level>(2);
                foreach (float top in world.Where(b => Supports(b, c)).Select(b => b.Max.Y).Append(0f).Distinct().OrderBy(y => y))
                {
                    var feet = new Vector3(c.X, top, c.Z);
                    // Stable like the game's ground step: nothing else within a step above under the footprint lifts you.
                    if (Mathf.Abs(Collision.Ground(feet, world) - top) > 1e-3f) continue;
                    if (Blocked(world, feet, MovementTuning.CrouchHeight)) continue; // not even a crouched body fits
                    float ceil = Collision.Ceiling(feet, world, top + Collision.StepHeight);
                    int bi = world.FindIndex(b => Supports(b, c) && Mathf.Abs(b.Max.Y - top) < 1e-4f);
                    list.Add(new Level(top, bi, ceil, !Blocked(world, feet, Collision.PlayerHeight)));
                }
                levels[i * nz + j] = list;
            }

        // Flood fill: walk (8-neighbours: step up 0.7 m, any drop) and hop (onto levels up to the crouch-jump reach, ≤ 1 m away).
        var seen = new HashSet<(int, int, int)>();
        var from = new Dictionary<(int, int, int), (int, int, int)>();
        var walkOnly = new HashSet<(int, int, int)>();
        int si = (int)((start.X - minX) / cell), sj = (int)((start.Z - minZ) / cell);
        var sl = levels[si * nz + sj];
        int sk = sl.FindIndex(l => Mathf.Abs(l.Y - start.Y) < 0.75f);
        if (sk < 0) { Fail($"jumpaudit {name}", "no walkable level at the start"); return; }
        for (int pass = 0; pass < 2; pass++)
        {
            bool jumps = pass == 1;
            var q = new Queue<(int, int, int)>();
            var s0 = (si, sj, sk);
            if (!jumps) { seen.Add(s0); q.Enqueue(s0); }
            else foreach (var n in seen) q.Enqueue(n);
            while (q.Count > 0)
            {
                var (i, j, k) = q.Dequeue();
                var lv = levels[i * nz + j][k];
                // walking
                for (int di = -1; di <= 1; di++)
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        if (di == 0 && dj == 0) continue;
                        int a = i + di, b = j + dj;
                        if (a < 0 || b < 0 || a >= nx || b >= nz) continue;
                        // The game's ground step: up to 0.7 m up (repeatedly, as each frame steps again), any drop down.
                        float target = Collision.Ground(Center(a, b, lv.Y), world);
                        for (int it = 0; it < 6; it++)
                        {
                            float next = Collision.Ground(Center(a, b, target), world);
                            if (next <= target + 1e-4f) break;
                            target = next;
                        }
                        int tk = levels[a * nz + b].FindIndex(l => Mathf.Abs(l.Y - target) < 1e-3f);
                        if (tk < 0 || !levels[a * nz + b][tk].Stand) continue; // a standing body must fit to walk there
                        Visit((a, b, tk), (i, j, k), q);
                    }
                if (!jumps) continue;
                // hops onto higher levels within reach (limited by the head room at take-off)
                float reach = Mathf.Min(MovementTuning.JumpApex + MovementTuning.CrouchTuck, Mathf.Max(0, lv.Ceil - lv.Y - MovementTuning.CrouchHeight)) + Collision.AirStep;
                for (int di = -4; di <= 4; di++)
                    for (int dj = -4; dj <= 4; dj++)
                    {
                        int a = i + di, b = j + dj;
                        if (a < 0 || b < 0 || a >= nx || b >= nz || di * di + dj * dj > 16) continue;
                        var ls = levels[a * nz + b];
                        for (int tk = 0; tk < ls.Count; tk++)
                            if (ls[tk].Y > lv.Y + Collision.StepHeight - 0.01f && ls[tk].Y <= lv.Y + reach) Visit((a, b, tk), (i, j, k), q);
                    }
            }
            if (!jumps) walkOnly.UnionWith(seen);
        }

        void Visit((int, int, int) n, (int, int, int) parent, Queue<(int, int, int)> q)
        {
            if (!seen.Add(n)) return;
            from[n] = parent;
            q.Enqueue(n);
        }

        // Findings: wall tops and off-floor spots reachable (only by jumping, or at all).
        var wallTops = new Dictionary<int, (int, int, int)>();
        var outside = new List<(int, int, int)>();
        foreach (var n in seen)
        {
            var (i, j, k) = n;
            var lv = levels[i * nz + j][k];
            var c = Center(i, j, lv.Y);
            if (!Inside(c)) outside.Add(n);
            if (lv.Box >= 0 && defs != null && lv.Box < defs.Length && defs[lv.Box].Surf == Surf.Wall && lv.Y > 0.05f && !wallTops.ContainsKey(lv.Box)) wallTops[lv.Box] = n;
            if (lv.Box >= 0 && defs == null && lv.Box >= 5 && lv.Y >= 2f && !wallTops.ContainsKey(lv.Box)) wallTops[lv.Box] = n; // range: tall extra boxes
        }
        GD.Print($"[jumpaudit] {name}: {walkOnly.Count} spots by walking, +{seen.Count - walkOnly.Count} by jumping; " +
                 $"{wallTops.Count} wall top(s), {outside.Count} spot(s) outside the floor reachable ({sw.ElapsedMilliseconds} ms)");
        // What jumping adds, by surface (box, top height, cells of 0.25 m).
        var extra = seen.Where(n => !walkOnly.Contains(n)).Select(n => levels[n.Item1 * nz + n.Item2][n.Item3])
            .GroupBy(l => (l.Box, Y: MathF.Round(l.Y, 2))).OrderByDescending(g => g.Count()).Take(8)
            .Select(g => $"#{g.Key.Box}{(defs != null && g.Key.Box >= 0 && g.Key.Box < defs.Length ? "/" + defs[g.Key.Box].Surf : "")}@{F(g.Key.Y, "0.00")}m×{g.Count()}");
        GD.Print($"[jumpaudit]   jump-only surfaces: {string.Join(", ", extra)}");
        foreach (var (box, n) in wallTops.OrderBy(w => w.Key))
        {
            var b = world[box];
            var (i, j, k) = n;
            var lv = levels[i * nz + j][k];
            bool sim = from.TryGetValue(n, out var par) && Replay(world, levels, par, n, Center, nz);
            GD.Print($"[jumpaudit]   wall top box #{box} ({F(b.Min.X, "0.0")},{F(b.Min.Z, "0.0")})–({F(b.Max.X, "0.0")},{F(b.Max.Z, "0.0")}) " +
                     $"top {F(lv.Y, "0.00")} m at ({F(Center(i, j).X, "0.0")}, {F(Center(i, j).Z, "0.0")}){(sim ? " — CONFIRMED by a simulated jump" : walkOnly.Contains(n) ? " — walkable" : " — not confirmed by simulation")}");
        }
        if (outside.Count > 0)
        {
            var (i, j, k) = outside.OrderBy(n => levels[n.Item1 * nz + n.Item2][n.Item3].Y).First();
            GD.Print($"[jumpaudit]   e.g. outside the floor at ({F(Center(i, j).X, "0.0")}, {F(Center(i, j).Z, "0.0")}) height {F(levels[i * nz + j][k].Y, "0.00")} m");
        }
        Check($"jumpaudit {name}: no way out of the map", outside.Count == 0, outside.Count == 0 ? "sealed" : $"{outside.Count} reachable spots outside the floor");
    }

    static bool Supports(Box b, Vector3 c)
    {
        const float r = Collision.PlayerRadius - 0.05f;
        return !(c.X + r < b.Min.X || c.X - r > b.Max.X || c.Z + r < b.Min.Z || c.Z - r > b.Max.Z);
    }

    /// <summary>A box blocks a body of height <paramref name="h"/> standing at these feet, by the game's own rule: boxes whose top
    /// is within a step (0.7 m) of the feet don't block, boxes starting above the head don't either.</summary>
    static bool Blocked(List<Box> world, Vector3 feet, float h)
    {
        const float r = Collision.PlayerRadius;
        foreach (var b in world)
            if (b.Max.Y > feet.Y + Collision.StepHeight && b.Min.Y < feet.Y + h &&
                feet.X > b.Min.X - r && feet.X < b.Max.X + r && feet.Z > b.Min.Z - r && feet.Z < b.Max.Z + r)
                return true;
        return false;
    }

    /// <summary>Re-plays a hop with the real physics: from the take-off spot, run at the target, jump, crouch in the air.
    /// Tries a standing start and a 1 m run-up.</summary>
    static bool Replay(List<Box> world, List<Level>[] levels, (int I, int J, int K) a, (int I, int J, int K) b, Func<int, int, float, Vector3> center, int nz)
    {
        var la = levels[a.I * nz + a.J][a.K];
        var lb = levels[b.I * nz + b.J][b.K];
        var p0 = center(a.I, a.J, la.Y);
        var p1 = center(b.I, b.J, lb.Y);
        var dir = new Vector3(p1.X - p0.X, 0, p1.Z - p0.Z);
        if (dir.LengthSquared() < 1e-6f) return false;
        dir = dir.Normalized();
        float yaw = Mathf.RadToDeg(Mathf.Atan2(dir.X, -dir.Z));
        foreach (float runUp in new[] { 0f, 1f })
            foreach (float crouchAt in new[] { 0.05f, 0.2f })
            {
                var startFeet = p0 - dir * runUp;
                if (Blocked(world, startFeet, Collision.PlayerHeight)) continue;
                var r = new Rig(world, startFeet, yaw);
                r.Hold(Key.W);
                if (runUp > 0) r.RunUntil(g => (g.Feet - p0).Dot(dir) >= 0, 1f);
                r.JumpAndLand(1.5f, g => { if (g.M.AirTime > crouchAt) g.Hold(Key.Ctrl); });
                r.Release(Key.Ctrl);
                r.Step(30);
                if (r.Feet.Y >= lb.Y - 0.02f) { Mover.KeyOverride = null; return true; }
            }
        Mover.KeyOverride = null;
        return false;
    }
}
