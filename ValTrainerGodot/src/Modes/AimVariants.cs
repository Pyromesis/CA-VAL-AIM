using Godot;
using ValTrainer.Core;
using ValTrainer.Game;

namespace ValTrainer.Modes;

/// <summary>The four aim families of the library (1.18): what each new drill trains.</summary>
public enum AimFamily { Flick, Track, Micro, Reflex }

/// <summary>Where flick / micro targets appear.</summary>
public enum AimPattern { Random, CenterOut, Horizontal, Vertical, Wide, Cluster, Ladder }

/// <summary>How a tracking target moves.</summary>
public enum TrackMotion { Strafe, Orbit, Bob, Figure8, Zigzag, Jump, Wander, Surge, Approach }

/// <summary>
/// One parameterised drill of the 60-drill aim set (15 per family). Distances in metres, angles in degrees, times in
/// seconds. Each family reads the fields it needs; the rest keep their defaults.
/// </summary>
public sealed record AimVariant(string Id, AimFamily Family, string Name, string Description)
{
    // shared
    public float Radius { get; init; } = 0.14f;
    public float DistMin { get; init; } = 10f;
    public float DistMax { get; init; } = 14f;
    // flick / micro / reflex placement
    public AimPattern Pattern { get; init; } = AimPattern.Random;
    public int Count { get; init; } = 1;
    public float Yaw { get; init; } = 25f;
    public float PitchMin { get; init; } = -3f;
    public float PitchMax { get; init; } = 8f;
    public float MinAngle { get; init; } = 6f;
    /// <summary>Seconds a target stays before it counts as missed (0 = until hit).</summary>
    public float Life { get; init; }
    /// <summary>Sideways drift of flick / micro targets (m/s, 0 = still).</summary>
    public float Drift { get; init; }
    // micro
    public float OffMin { get; init; } = 1.5f;
    public float OffMax { get; init; } = 5f;
    // tracking
    public TrackMotion Motion { get; init; } = TrackMotion.Strafe;
    public float Speed { get; init; } = 4f;
    public float TurnMin { get; init; } = 0.4f;
    public float TurnMax { get; init; } = 1.2f;
    public float StopChance { get; init; }
    public float Width { get; init; } = 6f;
    public float Height { get; init; } = 1.2f;
    // reflex
    public float DelayMin { get; init; } = 0.6f;
    public float DelayMax { get; init; } = 1.8f;
    public float Window { get; init; } = 0.6f;

    public string KeyBase => Family switch
    {
        AimFamily.Flick => "flickx", AimFamily.Track => "trackv", AimFamily.Micro => "microx", _ => "reflex",
    };

    public string Key => KeyBase + ":" + Id;

    public string Category => Family switch
    {
        AimFamily.Flick => "Flick", AimFamily.Track => "Tracking", AimFamily.Micro => "Micro", _ => "Reflex",
    };

    /// <summary>
    /// The same drill at a skill tier (0 Rookie … 4 Radiant). Authored numbers are the tier-2 baseline (Regular): lower
    /// tiers get bigger/slower targets, shorter flicks and longer windows; higher tiers get smaller/faster targets, wider
    /// flicks and tighter windows. Values are clamped so no tier can produce an unplayable drill.
    /// </summary>
    public AimVariant At(int tier)
    {
        tier = Math.Clamp(tier, 0, 4);
        float radiusMul = new[] { 1.42f, 1.18f, 1f, 0.84f, 0.70f }[tier];
        float distMul = new[] { 0.82f, 0.91f, 1f, 1.07f, 1.14f }[tier];
        float angleMul = new[] { 0.78f, 0.89f, 1f, 1.08f, 1.16f }[tier];
        float lifeMul = new[] { 1.40f, 1.18f, 1f, 0.84f, 0.70f }[tier];
        float speedMul = new[] { 0.62f, 0.81f, 1f, 1.13f, 1.26f }[tier];
        float windowMul = new[] { 1.45f, 1.22f, 1f, 0.82f, 0.68f }[tier];
        float offMul = new[] { 1.32f, 1.15f, 1f, 0.86f, 0.74f }[tier];

        float radius = Math.Clamp(Radius * radiusMul, 0.035f, 0.8f);
        float dmin = Math.Clamp(DistMin * distMul, 3f, 80f);
        float dmax = Math.Clamp(Math.Max(DistMax * distMul, dmin + 0.5f), 3.5f, 90f);
        float yaw = Math.Clamp(Yaw * angleMul, 1f, 170f);
        float offMin = Math.Clamp(OffMin * offMul, 0.2f, 14f);
        float offMax = Math.Clamp(Math.Max(OffMax * offMul, offMin + 0.1f), 0.3f, 18f);
        float speed = Math.Clamp(Speed * speedMul, 0.5f, 14f);
        float width = Math.Clamp(Width * (tier <= 1 ? 0.85f : tier >= 3 ? 1.12f : 1f), 1.5f, 30f);
        float height = Math.Clamp(Height * (tier <= 1 ? 0.8f : tier >= 3 ? 1.15f : 1f), 0.25f, 4f);
        float turnMin = Math.Clamp(TurnMin * (tier <= 1 ? 1.25f : tier >= 3 ? 0.82f : 1f), 0.12f, 4f);
        float turnMax = Math.Clamp(Math.Max(TurnMax * (tier <= 1 ? 1.25f : tier >= 3 ? 0.82f : 1f), turnMin + 0.05f), 0.2f, 6f);
        float delayMin = Math.Clamp(DelayMin * (tier <= 1 ? 1.2f : tier >= 3 ? 0.85f : 1f), 0.2f, 7f);
        float delayMax = Math.Clamp(Math.Max(DelayMax * (tier <= 1 ? 1.2f : tier >= 3 ? 0.85f : 1f), delayMin + 0.1f), 0.3f, 9f);

        return this with
        {
            Radius = radius,
            DistMin = dmin,
            DistMax = dmax,
            Yaw = yaw,
            MinAngle = Math.Clamp(MinAngle * angleMul, 0f, 120f),
            Life = Life > 0 ? Math.Clamp(Life * lifeMul, 0.25f, 12f) : 0f,
            Drift = Drift > 0 ? Math.Clamp(Drift * speedMul, 0.15f, 10f) : 0f,
            OffMin = offMin,
            OffMax = offMax,
            Speed = speed,
            Width = width,
            Height = height,
            TurnMin = turnMin,
            TurnMax = turnMax,
            DelayMin = delayMin,
            DelayMax = delayMax,
            Window = Math.Clamp(Window * windowMul, 0.18f, 4f),
        };
    }

    public TrainingMode Create() => Family switch
    {
        AimFamily.Flick => new FlickVariantMode(this),
        AimFamily.Track => new TrackVariantMode(this),
        AimFamily.Micro => new MicroVariantMode(this),
        _ => new ReflexVariantMode(this),
    };
}

/// <summary>The 60 drills: 15 flicking, 15 tracking, 15 micro-adjustment and 15 reflex drills.</summary>
public static class AimVariants
{
    static AimVariant F(string id, string name, string desc) => new(id, AimFamily.Flick, name, desc);
    static AimVariant T(string id, string name, string desc) => new(id, AimFamily.Track, name, desc);
    static AimVariant M(string id, string name, string desc) => new(id, AimFamily.Micro, name, desc);
    static AimVariant X(string id, string name, string desc) => new(id, AimFamily.Reflex, name, desc);

    public static readonly AimVariant[] All =
    {
        // ---- flicking: distance, size, spread and pattern ----
        F("close", "Close Flicks", "Big targets at 5–7 m, 20–40° apart: fast, wide arm flicks.") with { Radius = 0.3f, DistMin = 5, DistMax = 7, Yaw = 40, MinAngle = 20 },
        F("mid", "Mid-range Flicks", "Head-size targets at 12–18 m, 8–25° away: the VALORANT duel distance.") with { DistMin = 12, DistMax = 18, Yaw = 25, MinAngle = 8 },
        F("long", "Long Flicks", "Heads at 30–45 m: small angles, tiny targets, precise stops.") with { DistMin = 30, DistMax = 45, Yaw = 12, PitchMin = -1, PitchMax = 4, MinAngle = 3 },
        F("wide", "Wide Flicks", "Targets up to 70° to either side: full-arm flicks, then stop dead.") with { Radius = 0.22f, Yaw = 70, MinAngle = 30 },
        F("behind", "Turn Flicks", "Targets up to 150° away, often behind you: turn, find, click.") with { Radius = 0.3f, Pattern = AimPattern.Wide, Yaw = 150, MinAngle = 60, DistMin = 8, DistMax = 10 },
        F("horizontal", "Head-line Flicks", "Every target sits at head height: horizontal flicks only, keep the crosshair level.") with { Pattern = AimPattern.Horizontal, Yaw = 35, MinAngle = 10 },
        F("vertical", "Vertical Flicks", "Targets above and below the crosshair: train the up and down flick.") with { Radius = 0.18f, Pattern = AimPattern.Vertical, PitchMin = -6, PitchMax = 22, MinAngle = 8 },
        F("centreout", "Centre-out Flicks", "Centre target, then a random one: flick out and back like Spidershot, head-size.") with { Pattern = AimPattern.CenterOut, Yaw = 30, MinAngle = 6 },
        F("triple", "Triple Flicks", "Three targets at once: shoot the nearest each time, no wasted path.") with { Count = 3, Radius = 0.2f, Yaw = 30, MinAngle = 5 },
        F("timed", "Timed Flicks", "Each head disappears after 0.9 s: commit to the flick.") with { Life = 0.9f, Yaw = 25, MinAngle = 8 },
        F("blitz", "Blitz Flicks", "Heads vanish after 0.6 s at 8–12 m: speed under pressure.") with { Life = 0.6f, DistMin = 8, DistMax = 12, Yaw = 20, MinAngle = 6 },
        F("drift", "Drifting Flicks", "Targets slide sideways at walking speed: flick to a moving head.") with { Drift = 2.5f, Yaw = 25, MinAngle = 8 },
        F("tiny", "Tiny Flicks", "Half-size heads (r 0.07 m) at 12–16 m: precision on arrival.") with { Radius = 0.07f, DistMin = 12, DistMax = 16, Yaw = 20, MinAngle = 6 },
        F("big", "Big Flicks", "Body-size targets: pure flick speed, no fine aim needed.") with { Radius = 0.45f, Yaw = 35, MinAngle = 12 },
        F("mixed", "Mixed-range Flicks", "Each head at a new distance from 6 to 40 m: adapt the flick to the size.") with { DistMin = 6, DistMax = 40, Yaw = 30, MinAngle = 6 },

        // ---- tracking: motion, speed and size ----
        T("slow", "Slow Strafe Track", "A large target walking A-D at 2.5 m/s: smooth, steady tracking.") with { Radius = 0.4f, Speed = 2.5f, TurnMin = 0.8f, TurnMax = 1.8f },
        T("fast", "Fast Strafe Track", "Knife-speed A-D (6.75 m/s) with quick reversals.") with { Radius = 0.3f, Speed = 6.75f, TurnMin = 0.3f, TurnMax = 0.8f },
        T("close", "Close Track", "A strafing target at 5 m: big angular speed, use the arm.") with { Radius = 0.3f, DistMin = 5, DistMax = 5, Speed = 5.4f, Width = 3.5f },
        T("long", "Long-range Track", "A small target strafing at 30 m: tiny, smooth corrections.") with { Radius = 0.25f, DistMin = 30, DistMax = 30, Speed = 5.4f, Width = 8 },
        T("stopgo", "Stop-and-go Track", "Strafes with frequent stops: stay on it when it plants its feet.") with { Radius = 0.3f, Speed = 5.4f, StopChance = 0.35f },
        T("orbit", "Orbit Track", "The target circles in front of you: track a curve, not a line.") with { Radius = 0.3f, Motion = TrackMotion.Orbit, Speed = 3.5f, Width = 3f },
        T("bob", "Bobbing Track", "Strafes while bobbing up and down: track on two axes.") with { Radius = 0.3f, Motion = TrackMotion.Bob, Speed = 4f, Height = 0.8f },
        T("figure8", "Figure-8 Track", "Traces a figure eight: constant change of direction on both axes.") with { Radius = 0.3f, Motion = TrackMotion.Figure8, Speed = 3f, Width = 3.5f, Height = 1.2f },
        T("zigzag", "Zigzag Track", "Diagonal zigzags: up-left, down-right, at random.") with { Radius = 0.3f, Motion = TrackMotion.Zigzag, Speed = 4.5f, TurnMin = 0.35f, TurnMax = 0.9f },
        T("jumper", "Jumper Track", "Strafes and jumps with VALORANT's jump arc, often.") with { Radius = 0.32f, Motion = TrackMotion.Jump, Speed = 5.4f, TurnMin = 0.5f, TurnMax = 1.1f },
        T("wander", "Wander Track", "Drifts in any direction and changes course smoothly.") with { Radius = 0.3f, Motion = TrackMotion.Wander, Speed = 3.5f },
        T("surge", "Surge Track", "Speeds up and slows down all the time: match its pace.") with { Radius = 0.3f, Motion = TrackMotion.Surge, Speed = 6.5f },
        T("approach", "Approach Track", "Strafes while walking toward you and back: the size keeps changing.") with { Radius = 0.3f, Motion = TrackMotion.Approach, DistMin = 5, DistMax = 20, Speed = 4.5f },
        T("tinyfast", "Tiny Fast Track", "A head-size target at full run speed: the hardest strafe.") with { Radius = 0.16f, Speed = 5.4f, TurnMin = 0.25f, TurnMax = 0.7f },
        T("wide", "Wide Track", "Strafes across a 24 m lane: long, even sweeps.") with { Radius = 0.3f, Speed = 5.4f, Width = 12f, TurnMin = 1.5f, TurnMax = 3f },

        // ---- micro-adjustment: offset, size and pattern ----
        M("tiny", "Micro Steps", "Real heads 0.5–2° from the last one: the smallest corrections.") with { OffMin = 0.5f, OffMax = 2f, DistMin = 12, DistMax = 16 },
        M("small", "Small Adjusts", "Heads 1–3° apart at 10–15 m: settle and click.") with { OffMin = 1f, OffMax = 3f },
        M("medium", "Medium Adjusts", "Heads 3–6° apart: the gap between a micro and a flick.") with { OffMin = 3f, OffMax = 6f },
        M("far", "Far Micro", "Heads at 30–40 m, 0.5–2° apart: pixel-level corrections.") with { OffMin = 0.5f, OffMax = 2f, DistMin = 30, DistMax = 40 },
        M("pinpoint", "Pinpoint", "Half-size heads (r 0.07 m), 1–3° apart: stop exactly on it.") with { Radius = 0.07f, OffMin = 1f, OffMax = 3f },
        M("line", "Head-line Micro", "Steps along a horizontal line at head height: tiny side-to-side corrections.") with { Pattern = AimPattern.Horizontal, OffMin = 1f, OffMax = 3f },
        M("ladder", "Ladder Micro", "Steps up and down a column: vertical micro-corrections.") with { Pattern = AimPattern.Ladder, OffMin = 0.8f, OffMax = 2.5f },
        M("cluster", "Cluster Micro", "Heads appear in a small patch around the centre, never far from the last.") with { Pattern = AimPattern.Cluster, OffMin = 0.8f, OffMax = 3f },
        M("drift", "Drifting Micro", "Each head slides slowly: correct onto a moving point.") with { OffMin = 1f, OffMax = 3f, Drift = 0.6f },
        M("timed", "Timed Micro", "Each head stays 0.8 s: small, decisive corrections.") with { OffMin = 1f, OffMax = 4f, Life = 0.8f },
        M("quick", "Quick Micro", "Each head stays 0.5 s, 0.5–2.5° away: precision at speed.") with { OffMin = 0.5f, OffMax = 2.5f, Life = 0.5f },
        M("close", "Close Micro", "Heads at 5–7 m, 2–5° apart: corrections at close range.") with { OffMin = 2f, OffMax = 5f, DistMin = 5, DistMax = 7 },
        M("mixed", "Mixed Micro", "0.5–6° steps at changing distances: read each correction.") with { OffMin = 0.5f, OffMax = 6f, DistMin = 8, DistMax = 30 },
        M("double", "Double Micro", "Two heads close together: tap one, correct to the other.") with { Count = 2, OffMin = 1f, OffMax = 3.5f },
        M("body", "Body-to-Head", "Heads appear 2–4° above where you are: the body-to-head correction.") with { Pattern = AimPattern.Ladder, OffMin = 2f, OffMax = 4f, PitchMin = 0f },

        // ---- reflex: delay, window, spread and size ----
        X("centre", "Centre Reflex", "A head pops right at the centre: pure reaction, no flick.") with { Yaw = 3, PitchMin = -1, PitchMax = 3, MinAngle = 0, Window = 0.6f },
        X("near", "Near Reflex", "Heads pop 3–10° away: react and make the small flick.") with { Yaw = 10, MinAngle = 3, Window = 0.7f },
        X("wide", "Wide Reflex", "Heads pop up to 40° away: spot, flick, click.") with { Yaw = 40, MinAngle = 12, Window = 1.0f },
        X("peripheral", "Peripheral Reflex", "Heads pop near the edge of the screen: react to what you see from the corner of your eye.") with { Yaw = 45, MinAngle = 30, PitchMin = -2, PitchMax = 10, Window = 1.0f },
        X("quick", "Quick Reflex", "Each head is up for 0.4 s only.") with { Yaw = 15, MinAngle = 4, Window = 0.4f },
        X("lightning", "Lightning Reflex", "0.3 s windows, close to the centre: the fastest reactions.") with { Yaw = 6, MinAngle = 1, Window = 0.3f },
        X("long", "Long-range Reflex", "Heads pop at 30–40 m: react to a tiny target.") with { DistMin = 30, DistMax = 40, Yaw = 12, MinAngle = 3, Window = 0.8f },
        X("big", "Big Reflex", "Body-size targets pop anywhere in front: the reaction, not the precision.") with { Radius = 0.4f, Yaw = 35, MinAngle = 8, Window = 0.7f },
        X("tiny", "Tiny Reflex", "Half-size heads pop close by: react and land precisely.") with { Radius = 0.07f, Yaw = 10, MinAngle = 3, Window = 0.9f },
        X("rapid", "Rapid Reflex", "Short pauses (0.3–0.8 s) between pop-ups: stay ready.") with { DelayMin = 0.3f, DelayMax = 0.8f, Yaw = 15, MinAngle = 4, Window = 0.6f },
        X("patient", "Patient Reflex", "Long, random pauses (1.5–4 s): don't anticipate.") with { DelayMin = 1.5f, DelayMax = 4f, Yaw = 15, MinAngle = 4, Window = 0.6f },
        X("vertical", "Vertical Reflex", "Heads pop above and below: react on the vertical axis.") with { Yaw = 6, PitchMin = -6, PitchMax = 18, MinAngle = 5, Window = 0.8f },
        X("double", "Double Reflex", "Two heads pop together: hit both before they vanish.") with { Count = 2, Yaw = 18, MinAngle = 4, Window = 1.1f },
        X("close", "Close Reflex", "Big heads at 4–6 m, wide angles: a close-range surprise.") with { Radius = 0.2f, DistMin = 4, DistMax = 6, Yaw = 35, MinAngle = 10, Window = 0.7f },
        X("mixed", "Mixed Reflex", "Every pop-up has a new distance, size and window.") with { Radius = 0.14f, DistMin = 6, DistMax = 35, Yaw = 25, MinAngle = 3, Window = 0.75f },
    };

    public static AimVariant? Find(string keyBase, string id) =>
        All.FirstOrDefault(v => v.KeyBase.Equals(keyBase, StringComparison.OrdinalIgnoreCase) && v.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Factory for "flickx:close" style keys; an unknown id falls back to the family's first drill.</summary>
    public static TrainingMode Make(string keyBase, string id) =>
        (Find(keyBase, id) ?? All.First(v => v.KeyBase.Equals(keyBase, StringComparison.OrdinalIgnoreCase))).Create();

    public static IEnumerable<Func<TrainingMode>> Factories() => All.Select(v => (Func<TrainingMode>)v.Create);
}

/// <summary>Placement helpers shared by the variant drills.</summary>
static class VariantMath
{
    /// <summary>Lowest pitch that keeps a target of radius <paramref name="r"/> above the floor at distance <paramref name="d"/>.</summary>
    public static float Floor(float eyeY, float d, float r) =>
        Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp((r + 0.2f - eyeY) / Mathf.Max(0.5f, d), -1f, 1f)));
}

/// <summary>Flicking drill from an <see cref="AimVariant"/>: 1–3 static (or drifting) targets, hit to respawn.</summary>
public sealed class FlickVariantMode : TrainingMode
{
    AimVariant v;
    public FlickVariantMode(AimVariant v) => this.v = v;

    public override string Key => v.Key;
    public override string Name => v.Name;
    public override string Description => v.Description;
    public override string Category => v.Category;
    public override string SimKind => "static";
    public override bool UsesHeadshots => false;

    Target[] t = Array.Empty<Target>();
    int[] ids = Array.Empty<int>();
    float[] drift = Array.Empty<float>();
    int nextId = 1, expired;
    bool atCentre;

    public override AimFocus? Focus
    {
        get
        {
            int best = -1; float bestA = float.MaxValue;
            for (int i = 0; i < t.Length; i++)
            {
                float a = AngleFromCrosshair(t[i].GlobalPosition);
                if (a < bestA) { bestA = a; best = i; }
            }
            return best < 0 ? null : new AimFocus(ids[best], t[best].GlobalPosition, t[best].Radius);
        }
    }

    protected override void Setup()
    {
        v = v.At(Tier); // the tier chosen in the menu / pause menu scales the authored drill
        int n = Math.Clamp(v.Count, 1, 3);
        t = new Target[n]; ids = new int[n]; drift = new float[n];
        for (int i = 0; i < n; i++) { t[i] = G.SpawnTarget(v.Radius); Spawn(i, first: true); }
    }

    void Spawn(int i, bool first = false)
    {
        float d = R(v.DistMin, v.DistMax);
        float lo = Mathf.Max(v.PitchMin, VariantMath.Floor(G.View.Eye.Y, d, v.Radius));
        float hi = Mathf.Max(lo + 0.5f, v.PitchMax);
        float yaw = 0, pitch = 1f;
        if (v.Pattern == AimPattern.CenterOut && !atCentre && !first)
        {
            atCentre = true; // back to the centre
        }
        else
        {
            atCentre = false;
            for (int k = 0; k < 40; k++)
            {
                yaw = G.View.ViewYaw * (v.Pattern == AimPattern.Wide ? 1f : 0f) + R(-v.Yaw, v.Yaw);
                pitch = v.Pattern switch
                {
                    AimPattern.Horizontal => R(0f, 1.2f),
                    _ => R(lo, hi),
                };
                if (v.Pattern == AimPattern.Vertical) yaw = R(-Mathf.Min(8f, v.Yaw), Mathf.Min(8f, v.Yaw));
                var p = G.View.Eye + PlayerView.Dir(yaw, pitch) * d;
                bool farEnough = first || AngleFromCrosshair(p) >= v.MinAngle;
                bool apart = true;
                for (int j = 0; j < t.Length; j++) if (j != i && t[j] != null && t[j].Position.DistanceTo(p) < v.Radius * 4f) apart = false;
                if (farEnough && apart) break;
            }
        }
        t[i].Position = atCentre ? G.View.Eye + PlayerView.Dir(0f, 1f) * d : G.View.Eye + PlayerView.Dir(yaw, pitch) * d;
        t[i].SpawnTime = Now;
        drift[i] = v.Drift > 0 ? (Rng.Next(2) == 0 ? -1 : 1) * v.Drift : 0f;
        ids[i] = nextId++;
        if (!first) Event("target_spawn", ids[i], AngleFromCrosshair(t[i].Position));
    }

    public override void Update(float dt)
    {
        for (int i = 0; i < t.Length; i++)
        {
            if (drift[i] != 0)
            {
                var p = t[i].Position;
                p.X += drift[i] * dt;
                float lim = Mathf.Max(2f, Mathf.Abs(p.Z) * Mathf.Tan(Mathf.DegToRad(Mathf.Min(70f, v.Yaw))));
                if (Mathf.Abs(p.X) > lim) { p.X = Mathf.Sign(p.X) * lim; drift[i] = -drift[i]; }
                t[i].Position = p;
            }
            if (v.Life > 0 && Now - t[i].SpawnTime > v.Life)
            {
                expired++; Score -= 30; G.Sound("fail", 0.4f); Event("target_expired", ids[i]);
                Spawn(i);
            }
        }
    }

    public override float OnBullet(Vector3 o, Vector3 d, bool accurate)
    {
        Shots++;
        int best = -1; float bestD = float.PositiveInfinity;
        for (int i = 0; i < t.Length; i++)
        {
            float h = t[i].Ray(o, d);
            if (h < bestD && h < G.BulletWallDist) { bestD = h; best = i; }
        }
        if (best < 0) { Score -= 20; return float.PositiveInfinity; }
        float ms = (Now - t[best].SpawnTime) * 1000f;
        Hits++; Kills++;
        LastShotZone = HitZone.Head;
        KillTimes.Add(ms);
        Event("target_hit", ids[best], ms);
        Score += 100 + (int)Mathf.Max(0, (900 - ms) / 6);
        G.Sound(v.Radius <= 0.16f ? "head" : "body", 0.8f);
        t[best].Hit();
        Spawn(best);
        return bestD;
    }

    public override IEnumerable<(string Label, string Value)> ResultLines()
    {
        bool es = Loc.Spanish;
        yield return (es ? "Precisión" : "Accuracy", $"{Accuracy * 100:0.0}%  ({Hits}/{Shots})");
        yield return (es ? "Tiempo medio de impacto" : "Median time to hit", KillTimes.Count > 0 ? $"{Median(KillTimes):0} ms" : "—");
        if (v.Life > 0) yield return (es ? "Objetivos caducados" : "Expired targets", expired.ToString());
    }
}

/// <summary>Micro-adjustment drill from an <see cref="AimVariant"/>: each new head a small step from the last.</summary>
public sealed class MicroVariantMode : TrainingMode
{
    AimVariant v;
    public MicroVariantMode(AimVariant v) => this.v = v;

    public override string Key => v.Key;
    public override string Name => v.Name;
    public override string Description => v.Description;
    public override string Category => v.Category;
    public override string SimKind => "static";

    Target[] t = Array.Empty<Target>();
    int[] ids = Array.Empty<int>();
    float[] drift = Array.Empty<float>();
    Vector3[] home = Array.Empty<Vector3>();
    float yaw, pitch = 2f, lastMs = -1;
    int nextId = 1, expired;
    const float PatchYaw = 14f, PatchLo = -2f, PatchHi = 9f;

    public override AimFocus? Focus
    {
        get
        {
            int best = -1; float bestA = float.MaxValue;
            for (int i = 0; i < t.Length; i++)
            {
                float a = AngleFromCrosshair(t[i].GlobalPosition);
                if (a < bestA) { bestA = a; best = i; }
            }
            return best < 0 ? null : new AimFocus(ids[best], t[best].GlobalPosition, t[best].Radius);
        }
    }

    protected override void Setup()
    {
        v = v.At(Tier);
        int n = Math.Clamp(v.Count, 1, 2);
        t = new Target[n]; ids = new int[n]; drift = new float[n]; home = new Vector3[n];
        for (int i = 0; i < n; i++) { t[i] = G.SpawnTarget(v.Radius); Place(i, i == 0 ? 0f : 2f, 2f, first: true); }
    }

    void Place(int i, float y, float p, bool first = false)
    {
        float d = R(v.DistMin, v.DistMax);
        p = Mathf.Max(p, VariantMath.Floor(G.View.Eye.Y, d, v.Radius));
        if (i == 0) { yaw = y; pitch = p; }
        t[i].Position = home[i] = G.View.Eye + PlayerView.Dir(y, p) * d;
        t[i].SpawnTime = Now;
        drift[i] = v.Drift > 0 ? (Rng.Next(2) == 0 ? -1 : 1) * v.Drift : 0f;
        ids[i] = nextId++;
        if (!first) Event("target_spawn", ids[i], AngleFromCrosshair(t[i].Position));
    }

    /// <summary>The next head: a step of OffMin–OffMax degrees from the last, in the pattern's direction, kept inside a
    /// small patch in front of the player.</summary>
    void Next(int i)
    {
        float off = R(v.OffMin, v.OffMax);
        float ny = yaw, np = pitch;
        for (int k = 0; k < 24; k++)
        {
            float th = v.Pattern switch
            {
                AimPattern.Horizontal => Rng.Next(2) == 0 ? 0f : Mathf.Pi,
                AimPattern.Ladder => v.PitchMin >= 0 ? Mathf.Pi / 2 : (Rng.Next(2) == 0 ? Mathf.Pi / 2 : -Mathf.Pi / 2),
                _ => R(0, Mathf.Tau),
            };
            ny = yaw + Mathf.Cos(th) * off;
            np = pitch + Mathf.Sin(th) * off;
            float lim = v.Pattern == AimPattern.Cluster ? 4f : PatchYaw;
            float hi = v.Pattern == AimPattern.Cluster ? 5f : PatchHi;
            if (Mathf.Abs(ny) <= lim && np >= PatchLo && np <= hi) break;
            if (k == 23)
            {
                // edge of the patch: step back toward its centre
                var c = new Vector2(-yaw, 2f - pitch);
                var dir = c.Length() > 1e-3f ? c.Normalized() : Vector2.Right;
                ny = yaw + dir.X * off; np = pitch + dir.Y * off;
            }
        }
        if (v.Pattern == AimPattern.Ladder && v.PitchMin >= 0 && np > PatchHi) np = PatchLo + 1f; // body-to-head: start low again
        if (i == 0) Place(0, ny, np);
        else
        {
            // the second head of a pair: a small step from the first one
            float th = R(0, Mathf.Tau), o2 = R(v.OffMin, v.OffMax);
            Place(i, yaw + Mathf.Cos(th) * o2, pitch + Mathf.Sin(th) * o2);
        }
    }

    public override void Update(float dt)
    {
        for (int i = 0; i < t.Length; i++)
        {
            if (drift[i] != 0) // slides back and forth around its spot (amplitude = Drift m/s over ~0.6 s)
                t[i].Position = home[i] + Vector3.Right * (drift[i] * 0.6f * Mathf.Sin((Now - t[i].SpawnTime) * 2.6f));
            if (v.Life > 0 && Now - t[i].SpawnTime > v.Life)
            {
                expired++; Score -= 30; G.Sound("fail", 0.4f); Event("target_expired", ids[i]);
                Next(i);
            }
        }
    }

    public override float OnBullet(Vector3 o, Vector3 d, bool accurate)
    {
        Shots++;
        int best = -1; float bestD = float.PositiveInfinity;
        for (int i = 0; i < t.Length; i++)
        {
            float h = t[i].Ray(o, d);
            if (h < bestD && h < G.BulletWallDist) { bestD = h; best = i; }
        }
        if (best < 0) { Score -= 20; return float.PositiveInfinity; }
        float ms = (Now - t[best].SpawnTime) * 1000f;
        Hits++; Kills++;
        LastShotZone = HitZone.Head;
        KillTimes.Add(ms);
        lastMs = ms;
        Event("target_hit", ids[best], ms);
        Score += 100 + (int)Mathf.Max(0, (700 - ms) / 4);
        G.Sound("head", 0.7f);
        t[best].Hit();
        Next(best);
        return bestD;
    }

    public override IEnumerable<string> HudLines()
    {
        if (lastMs >= 0) yield return Loc.Spanish ? $"Última cabeza {lastMs:0} ms" : $"Last head {lastMs:0} ms";
    }

    public override IEnumerable<(string Label, string Value)> ResultLines()
    {
        bool es = Loc.Spanish;
        yield return (es ? "Precisión" : "Accuracy", $"{Accuracy * 100:0.0}%  ({Hits}/{Shots})");
        yield return (es ? "Tiempo medio de impacto" : "Median time to hit", KillTimes.Count > 0 ? $"{Median(KillTimes):0} ms" : "—");
        if (v.Life > 0) yield return (es ? "Cabezas caducadas" : "Expired heads", expired.ToString());
    }
}

/// <summary>Reflex drill from an <see cref="AimVariant"/>: heads pop up after a random pause for a short window.</summary>
public sealed class ReflexVariantMode : TrainingMode
{
    AimVariant v;
    public ReflexVariantMode(AimVariant v) => this.v = v;

    public override string Key => v.Key;
    public override string Name => v.Name;
    public override string Description => v.Description;
    public override string Category => v.Category;
    public override string SimKind => "static";

    Target[] heads = Array.Empty<Target>();
    bool[] up = Array.Empty<bool>();
    int[] ids = Array.Empty<int>();
    float nextAt = 0.8f, window, lastMs = -1;
    int nextId = 1, shown, expired;

    public override AimFocus? Focus
    {
        get
        {
            int best = -1; float bestA = float.MaxValue;
            for (int i = 0; i < heads.Length; i++)
            {
                if (!up[i]) continue;
                float a = AngleFromCrosshair(heads[i].GlobalPosition);
                if (a < bestA) { bestA = a; best = i; }
            }
            return best < 0 ? null : new AimFocus(ids[best], heads[best].GlobalPosition, heads[best].Radius);
        }
    }

    protected override void Setup()
    {
        v = v.At(Tier);
        int n = Math.Clamp(v.Count, 1, 2);
        heads = new Target[n]; up = new bool[n]; ids = new int[n];
        for (int i = 0; i < n; i++)
        {
            heads[i] = G.SpawnTarget(v.Radius);
            heads[i].Visible = false;
            heads[i].Position = new Vector3(0, -5, 0);
        }
    }

    void PopUp()
    {
        float maxA = 0;
        for (int i = 0; i < heads.Length; i++)
        {
            float d = R(v.DistMin, v.DistMax);
            float lo = Mathf.Max(v.PitchMin, VariantMath.Floor(G.View.Eye.Y, d, v.Radius));
            float hi = Mathf.Max(lo + 0.5f, v.PitchMax);
            var pos = Vector3.Zero;
            float a = 0;
            for (int k = 0; k < 40; k++)
            {
                pos = G.View.Eye + PlayerView.Dir(G.View.ViewYaw + R(-v.Yaw, v.Yaw), R(lo, hi)) * d;
                a = AngleFromCrosshair(pos);
                bool apart = i == 0 || heads[0].Position.DistanceTo(pos) > v.Radius * 6f;
                if (a >= v.MinAngle && apart) break;
            }
            heads[i].Position = pos;
            heads[i].Visible = true;
            heads[i].SpawnTime = Now;
            up[i] = true;
            ids[i] = nextId++;
            maxA = Mathf.Max(maxA, a);
            shown++;
            Event("target_spawn", ids[i], a);
        }
        window = v.Window + 0.008f * maxA;
    }

    void Hide(int i)
    {
        up[i] = false;
        heads[i].Visible = false;
        heads[i].Position = new Vector3(0, -5, 0);
        if (!up.Any(u => u)) nextAt = Now + R(v.DelayMin, v.DelayMax);
    }

    public override void Update(float dt)
    {
        if (!up.Any(u => u))
        {
            if (Now >= nextAt) PopUp();
            return;
        }
        for (int i = 0; i < heads.Length; i++)
        {
            if (!up[i] || Now - heads[i].SpawnTime <= window) continue;
            expired++; Score -= 30; Event("target_expired", ids[i]);
            Hide(i);
        }
    }

    public override float OnBullet(Vector3 o, Vector3 d, bool accurate)
    {
        Shots++;
        int best = -1; float bestD = float.PositiveInfinity;
        for (int i = 0; i < heads.Length; i++)
        {
            if (!up[i]) continue;
            float h = heads[i].Ray(o, d);
            if (h < bestD && h < G.BulletWallDist) { bestD = h; best = i; }
        }
        if (best < 0) { Score -= 20; return float.PositiveInfinity; }
        float ms = (Now - heads[best].SpawnTime) * 1000f;
        Hits++; Kills++;
        LastShotZone = HitZone.Head;
        KillTimes.Add(ms);
        lastMs = ms;
        Event("target_hit", ids[best], ms);
        Score += 100 + (int)Mathf.Max(0, (800 - ms) / 4);
        G.Sound("head", 0.8f);
        heads[best].Hit();
        Hide(best);
        return bestD;
    }

    public override float Accuracy => Shots == 0 ? 0 : (float)Hits / Shots;

    public override IEnumerable<string> HudLines()
    {
        if (lastMs >= 0) yield return Loc.Spanish ? $"Último {lastMs:0} ms" : $"Last {lastMs:0} ms";
    }

    public override IEnumerable<(string Label, string Value)> ResultLines()
    {
        bool es = Loc.Spanish;
        yield return (es ? "Aciertos" : "Hit", es ? $"{Hits} de {shown} cabezas" : $"{Hits} of {shown} pop-ups");
        yield return (es ? "Tiempo medio de impacto" : "Median time to hit", KillTimes.Count > 0 ? $"{Median(KillTimes):0} ms" : "—");
        yield return (es ? "Precisión" : "Accuracy", $"{Accuracy * 100:0.0}%  ({Hits}/{Shots})");
        yield return (es ? "Cabezas perdidas" : "Missed pop-ups", expired.ToString());
    }
}

/// <summary>Tracking drill from an <see cref="AimVariant"/>: hold Mouse 1 on a target that moves in its own pattern.</summary>
public sealed class TrackVariantMode : TrackingDrill
{
    AimVariant v;
    public TrackVariantMode(AimVariant v) => this.v = v;

    public override string Key => v.Key;
    public override string Name => v.Name;
    public override string Description => v.Description;
    public override string Category => v.Category;

    float vx, vy, targetVx, targetVy, nextTurn, phase, dir = 1f, z, vz, speedNow;
    const float Ground = PlayerView.EyeHeight - 0.2f;
    float BaseY => PlayerView.EyeHeight;

    protected override void Setup()
    {
        v = v.At(Tier);
        target = G.SpawnTarget(v.Radius);
        z = -(v.DistMin + v.DistMax) / 2f;
        target.Position = new Vector3(0, BaseY, z);
        targetVx = v.Speed;
        speedNow = v.Speed;
        nextTurn = 0.6f;
    }

    public override void Update(float dt)
    {
        var p = target.Position;
        nextTurn -= dt;
        phase += dt;
        switch (v.Motion)
        {
            case TrackMotion.Strafe:
            case TrackMotion.Approach:
            case TrackMotion.Surge:
            {
                if (nextTurn <= 0)
                {
                    float s = v.Motion == TrackMotion.Surge ? R(v.Speed * 0.25f, v.Speed) : v.Speed;
                    targetVx = Rng.NextDouble() < v.StopChance ? 0 : (Rng.Next(2) == 0 ? -1 : 1) * s;
                    nextTurn = R(v.TurnMin, v.TurnMax);
                    DirectionChanged();
                }
                vx = Mathf.MoveToward(vx, targetVx, Difficulty.StrafeDecel * dt);
                p.X += vx * dt;
                if (v.Motion == TrackMotion.Approach)
                {
                    // walk toward you and back between DistMin and DistMax
                    if (vz == 0) vz = 2.5f;
                    z += vz * dt;
                    if (-z < v.DistMin) { z = -v.DistMin; vz = -vz; }
                    if (-z > v.DistMax) { z = -v.DistMax; vz = -vz; }
                    p.Z = z;
                }
                p.Y = BaseY;
                break;
            }
            case TrackMotion.Orbit:
            {
                if (nextTurn <= 0) { dir = -dir; nextTurn = R(1.5f, 3f); DirectionChanged(); }
                float w = v.Speed / Mathf.Max(0.5f, v.Width) * dir;
                phase += w * dt - dt; // phase in radians
                p.X = Mathf.Cos(phase) * v.Width;
                p.Y = BaseY + 0.6f + Mathf.Sin(phase) * v.Width * 0.45f;
                break;
            }
            case TrackMotion.Bob:
            {
                if (nextTurn <= 0) { targetVx = (Rng.Next(2) == 0 ? -1 : 1) * v.Speed; nextTurn = R(v.TurnMin, v.TurnMax); DirectionChanged(); }
                vx = Mathf.MoveToward(vx, targetVx, Difficulty.StrafeDecel * dt);
                p.X += vx * dt;
                p.Y = BaseY + 0.3f + Mathf.Sin(phase * 2.2f) * v.Height * 0.5f;
                break;
            }
            case TrackMotion.Figure8:
            {
                float w = v.Speed / Mathf.Max(0.5f, v.Width);
                p.X = Mathf.Sin(phase * w) * v.Width;
                p.Y = BaseY + 0.5f + Mathf.Sin(phase * w * 2f) * v.Height * 0.5f;
                if (nextTurn <= 0) { nextTurn = R(1f, 2f); DirectionChanged(); }
                break;
            }
            case TrackMotion.Zigzag:
            {
                if (nextTurn <= 0)
                {
                    targetVx = (Rng.Next(2) == 0 ? -1 : 1) * v.Speed;
                    targetVy = (Rng.Next(2) == 0 ? -1 : 1) * v.Speed * 0.45f;
                    nextTurn = R(v.TurnMin, v.TurnMax);
                    DirectionChanged();
                }
                vx = Mathf.MoveToward(vx, targetVx, Difficulty.StrafeDecel * dt);
                vy = Mathf.MoveToward(vy, targetVy, Difficulty.StrafeDecel * dt);
                p.X += vx * dt;
                p.Y += vy * dt;
                if (p.Y < BaseY - 0.6f || p.Y > BaseY + 2f) { p.Y = Mathf.Clamp(p.Y, BaseY - 0.6f, BaseY + 2f); targetVy = -targetVy; vy = 0; }
                break;
            }
            case TrackMotion.Jump:
            {
                bool grounded = p.Y <= Ground + 1e-3f && vy <= 0;
                if (nextTurn <= 0) { targetVx = (Rng.Next(2) == 0 ? -1 : 1) * v.Speed; nextTurn = R(v.TurnMin, v.TurnMax); DirectionChanged(); }
                if (grounded && Rng.NextDouble() < 1.6 * dt) { vy = MovementTuning.JumpSpeed; DirectionChanged(); }
                vx = Mathf.MoveToward(vx, targetVx, (grounded ? Difficulty.StrafeDecel : 6f) * dt);
                p.X += vx * dt;
                vy -= MovementTuning.Gravity * dt;
                p.Y += vy * dt;
                if (p.Y < Ground) { p.Y = Ground; vy = 0; }
                break;
            }
            case TrackMotion.Wander:
            {
                if (nextTurn <= 0)
                {
                    float th = R(0, Mathf.Tau);
                    targetVx = Mathf.Cos(th) * v.Speed;
                    targetVy = Mathf.Sin(th) * v.Speed * 0.5f;
                    nextTurn = R(0.6f, 1.5f);
                    DirectionChanged();
                }
                vx = Mathf.MoveToward(vx, targetVx, 8f * dt);
                vy = Mathf.MoveToward(vy, targetVy, 8f * dt);
                p.X += vx * dt;
                p.Y += vy * dt;
                if (p.Y < BaseY - 0.6f || p.Y > BaseY + 2f) { p.Y = Mathf.Clamp(p.Y, BaseY - 0.6f, BaseY + 2f); targetVy = -targetVy; vy = -vy; }
                break;
            }
        }
        if (Mathf.Abs(p.X) > v.Width && v.Motion is not (TrackMotion.Orbit or TrackMotion.Figure8))
        {
            p.X = Mathf.Sign(p.X) * v.Width;
            targetVx = -targetVx; vx = 0;
            DirectionChanged();
        }
        if (!p.IsFinite()) p = new Vector3(0, BaseY, z);
        target.Position = p;
        ScoreFrame(dt);
    }
}
