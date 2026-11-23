using Godot;
using CaValAim.Valorant;

namespace CaValAim.Game;

/// <summary>
/// Valorant movement (ported from the tuned raylib build). Braking matches Riot-measured Phantom stop times (≈105 ms to
/// the accuracy threshold, ≈160 ms to full stop); releasing the keys and counter-strafing stop almost equally fast
/// (counter ≈6% quicker). The max speeds follow the gun in hand (<see cref="Hold"/>): run = the gun's VALORANT run speed,
/// walk / crouch / aimed are fractions of it, and the accuracy deadzone is 27.5% of it.
/// <para>This file is the horizontal model (ground, air control, dash lock). Jumping, crouch-jumps, falling, landing,
/// ability impulses and the collision step are in MovementBody.cs; the numbers are in <see cref="MovementTuning"/>.</para>
/// </summary>
public sealed partial class Mover
{
    /// <summary>Run speed with a rifle in hand (Vandal / Phantom / Sheriff: 5.40 m/s, 80% of knife speed). Bots and drill
    /// numbers written in rifle terms use these constants; the player's own limits follow the gun (<see cref="RunMax"/>).</summary>
    public const float RunSpeed = 5.4f;
    /// <summary>Walk (shift) and crouch max speed as fractions of the run speed (Riot publishes neither).</summary>
    public const float WalkMul = 0.55f, CrouchMul = 0.45f;
    /// <summary>Patch 3.0 deadzone: shots are fully accurate at or below 27.5% of the weapon's max speed.</summary>
    public const float DeadzoneMul = 0.275f;
    public const float WalkSpeed = RunSpeed * WalkMul;
    public const float CrouchSpeed = RunSpeed * CrouchMul;
    public const float AdsMultiplier = 0.76f;
    public const float AccurateSpeed = RunSpeed * DeadzoneMul; // ≈1.49 m/s with a rifle

    /// <summary>Run speed of the gun in hand (<see cref="Core.WeaponDef.RunSpeed"/>: rifles 5.40, Operator 5.13 m/s);
    /// <see cref="RunSpeed"/> with no gun.</summary>
    public float RunMax { get; private set; } = RunSpeed;
    public float WalkMax => RunMax * WalkMul;
    public float CrouchMax => RunMax * CrouchMul;
    /// <summary>The deadzone with the gun in hand (rifles ≈1.49 m/s, Operator ≈1.41 m/s).</summary>
    public float AccurateMax => RunMax * DeadzoneMul;

    /// <summary>Ground acceleration (m/s²): standstill → run speed in ≈0.3 s. Riot publishes no acceleration; this is
    /// frame-counted from a VALORANT Range recording (2026-10-18: strafes reach full speed in 0.27–0.39 s, see the
    /// support folder's research\jump-measure.md). Until 1.16 it was 40 (full speed in 0.135 s), which made running
    /// feel light. Braking (below) is Riot's own measurement and stays.</summary>
    public const float GroundAccel = 18f;
    const float Accel = GroundAccel, BrakeHigh = 38f, BrakeLow = 24f, CounterBonus = 1.06f;

    public const float StandEye = PlayerView.EyeHeight;
    public const float CrouchEye = 1.15f;

    /// <summary>Horizontal velocity (m/s; Y is always 0). The vertical speed is <see cref="VelY"/>.</summary>
    public Vector3 Vel;
    public bool Crouching, Walking;
    public float EyeHeight = StandEye;
    /// <summary>Camera-only vertical offset while a stair or curb is smoothed (metres, negative = camera below the eye).
    /// The body snaps onto the step at once; this eases out at <see cref="StepCamDamping"/> so the picture glides.</summary>
    public float CamStepOffset { get; private set; }
    /// <summary>How fast the stair camera offset settles (1/s): ~63% in 80 ms, visually done in about a quarter second.</summary>
    public const float StepCamDamping = 12.5f;
    public bool Ads;
    /// <summary>Aimed / scoped max speed as a fraction of the run speed: the gun's (<see cref="Core.WeaponDef.AdsMoveMul"/>,
    /// Operator 0.72), <see cref="AdsMultiplier"/> by default.</summary>
    public float AdsSpeedMul = AdsMultiplier;

    /// <summary>The gun in hand sets the max speeds: its run speed and its aimed / scoped fraction (null = no gun: rifle).</summary>
    public void Hold(Core.WeaponDef? w)
    {
        RunMax = w?.RunSpeed ?? RunSpeed;
        AdsSpeedMul = w?.AdsMoveMul ?? AdsMultiplier;
    }

    /// <summary>Horizontal (ground) speed in m/s.</summary>
    public float Speed => new Vector2(Vel.X, Vel.Z).Length();
    /// <summary>Standing on something, past the landing penalty and at most <see cref="AccurateMax"/>: first shots go where you aim.</summary>
    public bool Accurate => Grounded && !LandingPenalty && Speed <= AccurateMax + 0.001f;
    /// <summary>0 = accurate … 1 = running speed, airborne or just landed (crosshair movement-error display).</summary>
    public float MoveError => Grounded && !LandingPenalty ? Mathf.Clamp((Speed - AccurateMax) / (RunMax - AccurateMax), 0, 1) : 1f;

    public readonly List<float> StopTimesMs = new();
    public readonly List<float> CounterStopTimesMs = new();
    public readonly List<float> ReleaseStopTimesMs = new();
    public float LastStopMs = -1;
    public bool LastStopWasCounter;
    /// <summary>This frame's sideways input: +1 = D (right), −1 = A (left), 0 = neither or both. Recorded by the aim
    /// coach to tell mirroring (moving with the target) from anti-mirroring (moving against it).</summary>
    public float InputStrafe { get; private set; }
    bool stopping, stoppingCounter;
    float stopT;

    /// <summary>
    /// DEV / TEST ONLY: when set, every bind the mover (and every other bind check) reads comes from this function instead
    /// of the keyboard and mouse, so headless runs can script movement (see the --movescript dev flag in MovementScript.cs).
    /// Same as <see cref="Keybinds.Override"/>. Null (the default) = the real input.
    /// </summary>
    public static Func<InputBinding, bool>? KeyOverride { get => Keybinds.Override; set => Keybinds.Override = value; }

    static bool Down(ValorantProfile keys, GameAction a) => keys.Binds.IsDown(a);

    /// <summary>Computes this frame's horizontal velocity and returns the position delta (collision is the caller's job).
    /// Prefer <see cref="Step"/>, which also jumps, falls and collides.</summary>
    public Vector3 Update(float dt, float yawDeg, ValorantProfile keys)
    {
        float y = Mathf.DegToRad(yawDeg);
        var fwd = new Vector3(Mathf.Sin(y), 0, -Mathf.Cos(y));
        var right = new Vector3(Mathf.Cos(y), 0, Mathf.Sin(y));

        var wish = Vector3.Zero;
        bool f = Down(keys, GameAction.MoveForward), b = Down(keys, GameAction.MoveBack);
        bool l = Down(keys, GameAction.StrafeLeft), r = Down(keys, GameAction.StrafeRight);
        InputStrafe = (r ? 1f : 0f) - (l ? 1f : 0f);
        if (f) wish += fwd;
        if (b) wish -= fwd;
        if (r) wish += right;
        if (l) wish -= right;
        bool wants = wish.LengthSquared() > 0.001f;
        if (wants) wish = wish.Normalized();

        Crouching = bodyCrouched || Down(keys, GameAction.Crouch);
        Walking = Down(keys, GameAction.Walk);
        float target = Crouching ? CrouchEye : StandEye;
        EyeHeight += (target - EyeHeight) * (1f - Mathf.Exp(-14f * dt));

        float max = (Crouching ? CrouchMax : Walking ? WalkMax : RunMax) * SpeedScale;
        if (Ads) max = Mathf.Min(max, RunMax * AdsSpeedMul * SpeedScale);

        Vector3 motion = Vector3.Zero;
        // Ability dash: no steering and no friction until the lock runs out (gravity still applies in the air). If it
        // ends inside this frame, only the locked fraction uses the unmodified impulse velocity.
        if (SteerLockLeft > 0)
        {
            float locked = Mathf.Min(dt, SteerLockLeft);
            motion = Vel * locked;
            SteerLockLeft = Mathf.Max(0, SteerLockLeft - locked);
            dt -= locked;
            CancelStop();
            if (dt <= 0) return motion;
        }
        if (!Grounded)
        {
            motion += AirMove(dt, wish, wants, max);
            CancelStop(); // counter-strafe stops are measured on the ground only
            return motion;
        }

        float speedBefore = Speed;
        if (boosting && speedBefore > max)
        {
            // With no turn (or thrust straight ahead), speed follows the known scalar decay and can hand off precisely.
            // Opposite/lateral input keeps its existing capped-turn step rather than guessing a different contact time.
            bool linearDecay = !wants || SameDirection(Vel, wish, speedBefore);
            float boostTime = linearDecay ? Mathf.Min(dt, (speedBefore - max) / MovementTuning.ImpulseDecayGround) : dt;
            motion += BoostDecay(boostTime, wish, wants, MovementTuning.ImpulseDecayGround, max);
            CancelStop();
            dt -= boostTime;
            if (dt <= 0) return motion;
            speedBefore = Speed; // normal calibrated ground movement owns the rest of a partially boosted frame
        }
        boosting = false;

        float brake = speedBefore > RunMax * 0.25f ? BrakeHigh : BrakeLow;
        bool reversing = false;
        Vector3 delta;
        float? accurateAt = null;

        if (!wants)
        {
            var dir = speedBefore > 0 ? Vel / speedBefore : Vector3.Zero;
            float speed = speedBefore;
            float distance = Brake(ref speed, dt, 1f, out _);
            if (speedBefore > AccurateMax && speed <= AccurateMax)
                accurateAt = (speedBefore - AccurateMax) / BrakeHigh;
            delta = dir * distance;
            Vel = dir * speed;
        }
        else
        {
            float along = Vel.Dot(wish);
            var lateral = Vel - wish * along;
            float lat = lateral.Length();
            var lateralDir = lat > 0 ? lateral / lat : Vector3.Zero;
            float lateralDistance = Integrate(ref lat, 0, brake, dt);
            float distance;
            if (along < 0)
            {
                reversing = true;
                float back = -along;
                distance = -Brake(ref back, dt, CounterBonus, out float left);
                if (lat == 0 && -along > AccurateMax && back <= AccurateMax)
                    accurateAt = (-along - AccurateMax) / (BrakeHigh * CounterBonus);
                along = -back;
                distance += Integrate(ref along, max, Accel, left);
            }
            else distance = Integrate(ref along, max, along > max ? brake : Accel, dt);
            lateral = lateralDir * lat;
            Vel = wish * along + lateral;
            delta = wish * distance + lateralDir * lateralDistance;
        }

        TrackStop(dt, wants, wish, reversing, speedBefore, accurateAt);
        return motion + delta;
    }

    float Brake(ref float speed, float dt, float multiplier, out float left)
    {
        float knee = RunMax * 0.25f;
        float high = Mathf.Clamp((speed - knee) / (BrakeHigh * multiplier), 0, dt);
        float distance = Integrate(ref speed, knee, BrakeHigh * multiplier, high);
        float low = Mathf.Min(dt - high, speed / (BrakeLow * multiplier));
        distance += Integrate(ref speed, 0, BrakeLow * multiplier, low);
        left = Mathf.Max(0, dt - high - low);
        return distance;
    }

    static float Integrate(ref float speed, float target, float rate, float dt)
    {
        float changing = Mathf.Min(dt, Mathf.Abs(target - speed) / rate);
        float next = Mathf.MoveToward(speed, target, rate * changing);
        float distance = (speed + next) * .5f * changing + next * (dt - changing);
        speed = next;
        return distance;
    }

    /// <summary>
    /// Air control: the keys accelerate you at <see cref="MovementTuning.AirAccel"/> but can't raise your speed above what
    /// you took off with (or your stance's max), so air-strafing turns and brakes but never builds speed (no bunny hops).
    /// No key = no friction: you keep your momentum.
    /// </summary>
    Vector3 AirMove(float dt, Vector3 wish, bool wants, float max)
    {
        var initial = Vel;
        float before = Speed;
        if (boosting && before > max)
        {
            bool linearDecay = !wants || SameDirection(initial, wish, before);
            float boostTime = linearDecay ? Mathf.Min(dt, (before - max) / MovementTuning.ImpulseDecayAir) : dt;
            Vector3 delta = BoostDecay(boostTime, wish, wants, MovementTuning.ImpulseDecayAir, max);
            dt -= boostTime;
            // Once the extra speed is gone, ordinary air control owns the remainder of the frame.
            return dt > 0 ? delta + AirMove(dt, wish, wants, max) : delta;
        }
        boosting = false;
        float cap = Mathf.Max(max, before);
        if (!wants) return initial * dt;
        if (SameDirection(initial, wish, before))
        {
            float speed = before;
            float distance = Integrate(ref speed, cap, MovementTuning.AirAccel, dt);
            var dir = before > 1e-6f ? initial / before : wish;
            Vel = dir * speed;
            return dir * distance;
        }
        Vel += wish * MovementTuning.AirAccel * dt;
        float s = Speed;
        if (s > cap) Vel *= cap / s;
        // Preserve the existing capped turn, but integrate its changing velocity instead of using only its end value.
        return (initial + Vel) * (0.5f * dt);
    }

    /// <summary>After an ability impulse: the extra speed bleeds off at <paramref name="decay"/> m/s²; the keys can steer
    /// (turn) but not add speed. Normal movement resumes once you're back under the stance's max. Returns the exact
    /// straight-line distance, or the integral of the linearly changing velocity while turning.</summary>
    Vector3 BoostDecay(float dt, Vector3 wish, bool wants, float decay, float max)
    {
        var initial = Vel;
        float before = Speed;
        if (!wants || SameDirection(initial, wish, before))
        {
            float speed = before;
            float distance = Integrate(ref speed, max, decay, dt);
            var dir = before > 1e-6f ? initial / before : wish;
            Vel = dir * speed;
            if (Speed <= max + 1e-3f) boosting = false;
            return dir * distance;
        }
        float cap = Mathf.Max(max, before - decay * dt);
        Vel += wish * (Grounded ? Accel : MovementTuning.AirAccel) * dt;
        float s = Speed;
        if (s > cap) Vel *= cap / s;
        if (Speed <= max + 1e-3f) boosting = false;
        return (initial + Vel) * (0.5f * dt);
    }

    static bool SameDirection(Vector3 velocity, Vector3 wish, float speed) =>
        speed < 1e-6f || (velocity - wish * speed).LengthSquared() < 1e-8f;

    void TrackStop(float dt, bool wants, Vector3 wish, bool reversing, float speedBefore, float? accurateAt)
    {
        if (!stopping && speedBefore > RunMax * 0.8f && (!wants || reversing))
        {
            stopping = true;
            stoppingCounter = reversing;
            stopT = 0;
        }
        if (!stopping) return;
        stopT += dt;
        if (reversing) stoppingCounter = true;
        if (Accurate)
        {
            stopping = false;
            float fraction = speedBefore > AccurateMax && speedBefore > Speed
                ? Mathf.Clamp((speedBefore - AccurateMax) / (speedBefore - Speed), 0, 1) : 1f;
            LastStopMs = (stopT - dt + (accurateAt ?? dt * fraction)) * 1000f;
            LastStopWasCounter = stoppingCounter;
            StopTimesMs.Add(LastStopMs);
            if (stoppingCounter) CounterStopTimesMs.Add(LastStopMs); else ReleaseStopTimesMs.Add(LastStopMs);
        }
        else if (wants && !reversing && wish.Dot(Vel) > Speed * 0.7f && Speed > speedBefore)
            stopping = false;
    }

    /// <summary>A stop that was being timed no longer counts (you jumped, dashed or got launched).</summary>
    void CancelStop() { stopping = stoppingCounter = false; stopT = 0; }

    /// <summary>Movement error added to weapon spread (rifles: crouch-walk 0.8°, walk 3°, run 6°; airborne: the weapon's air
    /// error; just landed: at least <see cref="MovementTuning.LandingErr"/>).</summary>
    public float MovementErrorDeg(Core.WeaponDef w)
    {
        if (!Grounded) return w.AirErr;
        if (LandingPenalty) return Mathf.Max(MovementTuning.LandingErr, GroundErrorDeg(w));
        return GroundErrorDeg(w);
    }

    float GroundErrorDeg(Core.WeaponDef w)
    {
        float s = Speed;
        if (s <= AccurateMax) return 0f;
        if (Crouching) return w.CrouchWalkErr;
        if (s <= WalkMax) return w.WalkErr * (s - AccurateMax) / (WalkMax - AccurateMax);
        return w.WalkErr + (w.RunErr - w.WalkErr) * Mathf.Clamp((s - WalkMax) / (RunMax - WalkMax), 0, 1);
    }

    public void ResetStops()
    {
        StopTimesMs.Clear(); CounterStopTimesMs.Clear(); ReleaseStopTimesMs.Clear();
        LastStopMs = -1;
        LastStopWasCounter = false;
        ResetMotion();
    }

    /// <summary>Stops all motion: horizontal and vertical speed, dashes and impulses, a stop being timed and a queued jump.
    /// The ground state is re-checked on the next step (a respawn in the air starts falling from rest).</summary>
    public void ResetMotion()
    {
        Vel = Vector3.Zero;
        stopping = stoppingCounter = false;
        stopT = 0;
        VelY = 0;
        SteerLockLeft = 0;
        boosting = false;
        jumpQueued = false;
        OnRope = false;
        sinceLanded = 99f;
        CamStepOffset = 0f;
    }
}
