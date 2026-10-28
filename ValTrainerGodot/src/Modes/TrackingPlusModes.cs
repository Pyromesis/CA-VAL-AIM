using Godot;
using ValTrainer.Core;
using ValTrainer.Game;

namespace ValTrainer.Modes;

/// <summary>Shared scoring of the hold-Mouse-1 tracking drills: time on target while firing, re-acquire times after the
/// target changes direction, and the per-run statistics the coach and the stats screens read.</summary>
public abstract class TrackingDrill : TrainingMode
{
    public override AimFocus? Focus => target != null ? new AimFocus(1, target.GlobalPosition, target.Radius) : null;
    public override WeaponKind Weapon => WeaponKind.None;
    public override string Category => "Aim";
    public override string SimKind => "track";

    protected Target target = null!;
    protected float held, onTarget;
    bool onNow, wasOn;
    /// <summary>Time of the last direction change the player hasn't caught up with yet (-1 = none pending).</summary>
    float changeAt = -1f;
    protected readonly List<float> Reacquire = new();
    int lostAfterChange;

    /// <summary>The drill changed the target's direction (call when a reversal / burst / jump starts).</summary>
    protected void DirectionChanged()
    {
        if (changeAt < 0) changeAt = Now;
        Event("track_change");
    }

    protected void ScoreFrame(float dt)
    {
        onNow = !float.IsPositiveInfinity(target.Ray(G.View.Eye, G.View.Forward));
        target.SetHot(onNow && G.TriggerHeld);
        if (G.TriggerHeld)
        {
            held += dt;
            if (onNow) { onTarget += dt; if ((int)(onTarget * 10) != (int)((onTarget - dt) * 10)) G.Sound("tick", 0.15f, 1.6f); }
        }
        // Re-acquire: after a direction change, the time until the crosshair is back on the target and stays there for
        // 100 ms (a reaction measured while tracking). A change you never lost the target on counts as 0.
        if (changeAt >= 0)
        {
            if (!onNow) { stableSince = -1f; lost = true; }
            else
            {
                if (stableSince < 0) stableSince = Now;
                if (Now - stableSince >= 0.1f)
                {
                    float ms = lost ? (stableSince - changeAt) * 1000f : 0f;
                    Reacquire.Add(ms);
                    if (lost) { lostAfterChange++; Event("reacquire", ms); }
                    changeAt = -1f; lost = false; stableSince = -1f;
                }
            }
            if (Now - changeAt > 2f) { changeAt = -1f; lost = false; stableSince = -1f; }
        }
        wasOn = onNow;
        Score = (int)(onTarget * 100);
    }

    float stableSince = -1f;
    bool lost;

    public override float Accuracy => held <= 0 ? 0 : onTarget / held;

    float ReacquireMedian => Reacquire.Count == 0 ? 0 : Median(Reacquire.Where(x => x > 0).DefaultIfEmpty(0).ToList());

    public override IEnumerable<(string Label, string Value)> ResultLines()
    {
        bool es = Loc.Spanish;
        yield return (es ? "En el objetivo mientras disparas" : "On target while firing", $"{Accuracy * 100:0.0}%");
        yield return (es ? "Tiempo en el objetivo" : "Time on target", es ? $"{onTarget:0.0} s de {Duration:0} s" : $"{onTarget:0.0} s of {Duration:0} s");
        if (Reacquire.Count > 0)
            yield return (es ? "Recuperación tras un cambio" : "Re-acquire after a change", es
                ? $"{ReacquireMedian:0} ms de mediana · perdido {lostAfterChange} de {Reacquire.Count} veces"
                : $"{ReacquireMedian:0} ms median · lost it {lostAfterChange} of {Reacquire.Count} times");
    }

    public override IEnumerable<(string Key, float Value)> ExtraMetrics()
    {
        yield return ("trackx.on_pct", Accuracy);
        if (Reacquire.Count > 0)
        {
            yield return ("trackx.reacquire_ms", ReacquireMedian);
            yield return ("trackx.lost_pct", (float)lostAfterChange / Reacquire.Count);
        }
    }

    public override int Badge()
    {
        float a = Accuracy;
        if (a >= 0.55f) return Math.Min(4, Tier + 1);
        if (a >= 0.42f) return Tier;
        if (a >= 0.32f) return Tier - 1;
        return -1;
    }
}

/// <summary>
/// Air Tracking: the target strafes and jumps like a player using Jett / Raze / Omen mobility — VALORANT's own jump arc
/// (0.90 m apex, 0.71 s in the air, constant gravity) with air strafes that keep the take-off speed. Track it through the
/// arc: the vertical part of tracking that ground strafes never train.
/// </summary>
public sealed class AirTrackingMode : TrackingDrill
{
    public override string Key => "airtrack";
    public override string Name => "Air Tracking";
    public override string Description => "Hold Mouse 1 on a target that strafes and jumps with VALORANT's jump arc. Track it up, over and down.";

    float vx, targetVx, vy, nextTurn, nextJump;
    const float Floor = PlayerView.EyeHeight - 0.2f;

    protected override void Setup()
    {
        target = G.SpawnTarget(Difficulty.TrackRadius(Tier) * 1.1f);
        target.Position = new Vector3(0, Floor, -12);
        targetVx = Difficulty.TrackSpeed(Tier);
        nextJump = 1.2f;
    }

    public override void Update(float dt)
    {
        var p = target.Position;
        bool grounded = p.Y <= Floor + 1e-3f && vy <= 0;
        nextTurn -= dt;
        nextJump -= dt;
        if (nextTurn <= 0)
        {
            var (a, b) = Difficulty.TurnInterval(Tier);
            targetVx = (Rng.Next(2) == 0 ? -1 : 1) * Difficulty.TrackSpeed(Tier);
            nextTurn = R(a, b) * (grounded ? 1f : 1.5f);
            DirectionChanged();
        }
        if (grounded && nextJump <= 0)
        {
            vy = MovementTuning.JumpSpeed * Difficulty.T(Tier, 0.9f, 1f, 1f, 1.1f, 1.25f); // higher tiers: Jett-like updrafts
            nextJump = R(0.9f, 2.2f) / Difficulty.T(Tier, 0.7f, 0.85f, 1f, 1.15f, 1.3f);
            DirectionChanged();
        }
        // ground: VALORANT braking/acceleration; air: keep the take-off speed, steer slowly (no bunny-hop gain)
        float accel = grounded ? Difficulty.StrafeDecel : 6f;
        vx = Mathf.MoveToward(vx, targetVx, accel * dt);
        p.X += vx * dt;
        if (Mathf.Abs(p.X) > 6.5f) { p.X = Mathf.Sign(p.X) * 6.5f; targetVx = -targetVx; vx = 0; DirectionChanged(); }
        vy -= MovementTuning.Gravity * dt;
        p.Y += vy * dt;
        if (p.Y < Floor) { p.Y = Floor; vy = 0; }
        target.Position = p;
        ScoreFrame(dt);
    }
}

/// <summary>
/// Reactive Tracking: unpredictable A-D with stop-and-go, short dashes at knife speed and Jett-dash bursts on the higher
/// tiers. Besides time on target it measures how fast you re-acquire after every change of direction: your reaction
/// time while tracking, which the Reaction Lab can't capture.
/// </summary>
public sealed class ReactiveTrackingMode : TrackingDrill
{
    public override string Key => "reacttrack";
    public override string Name => "Reactive Tracking";
    public override string Description => "Hold Mouse 1 on a target that reverses, stops and bursts at random. Measures how fast you re-acquire after each change.";

    float vx, targetVx, nextChange, burstLeft;

    protected override void Setup()
    {
        target = G.SpawnTarget(Difficulty.TrackRadius(Tier));
        target.Position = new Vector3(0, PlayerView.EyeHeight, -10);
        targetVx = Difficulty.TrackSpeed(Tier);
        nextChange = 0.8f;
    }

    public override void Update(float dt)
    {
        nextChange -= dt;
        burstLeft -= dt;
        if (nextChange <= 0)
        {
            float speed = Difficulty.TrackSpeed(Tier);
            double r = Rng.NextDouble();
            if (r < Difficulty.T(Tier, 0.05f, 0.08f, 0.12f, 0.16f, 0.2f)) { speed = 12f; burstLeft = 0.18f; }        // dash
            else if (r < 0.35) speed = 6.75f;                                                                           // knife run
            else if (r < 0.5) speed = 0f;                                                                               // stop
            targetVx = (Rng.Next(2) == 0 ? -1 : 1) * speed;
            nextChange = R(0.25f, 1.1f) / Difficulty.T(Tier, 0.6f, 0.8f, 1f, 1.15f, 1.3f);
            DirectionChanged();
        }
        float decel = burstLeft > 0 ? 200f : Difficulty.StrafeDecel;
        if (burstLeft <= 0 && Mathf.Abs(targetVx) > 7f) targetVx = Mathf.Sign(targetVx) * Difficulty.TrackSpeed(Tier);
        vx = Mathf.MoveToward(vx, targetVx, decel * dt);
        var p = target.Position;
        p.X += vx * dt;
        if (Mathf.Abs(p.X) > 6.5f) { p.X = Mathf.Sign(p.X) * 6.5f; targetVx = -targetVx; vx = 0; DirectionChanged(); }
        target.Position = p;
        ScoreFrame(dt);
    }
}
