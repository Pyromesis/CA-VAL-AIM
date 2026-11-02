using Godot;
using CaValAim.Core;
using CaValAim.Game;

namespace CaValAim.Modes;

/// <summary>
/// Anti-Mirror: a target strafes left and right and the player has to strafe <em>against</em> it (target right → press A,
/// target left → press D) while holding fire. The drill scores the share of frames in which both were strafing and the
/// player moved opposite to the target: the movement the aim coach reads from <see cref="FrameSample.TargetVx"/> and
/// <see cref="FrameSample.MoveX"/> in every other drill too. Mirroring (moving with the target) scores nothing.
/// </summary>
public sealed class AntiMirrorMode : TrackingDrill
{
    public override string Key => "anti-mirror";
    public override string Name => "Anti-Mirror";
    public override string Description =>
        "Hold Mouse 1 on a strafing target and move against it: target right → A, target left → D. Scores the frames " +
        "you spend anti-mirroring instead of moving with the target.";
    public override bool Movement => true;

    public override string? Prompt => Loc.Spanish
        ? "Muévete al contrario: si el objetivo va a la derecha, pulsa A; si va a la izquierda, pulsa D."
        : "Move against it: target right → press A, target left → press D.";

    float vx, targetVx, nextTurn;
    int strafeFrames, mirrorFrames, antiFrames;
    float strafeTime;

    float Anti => strafeFrames == 0 ? 0f : (float)antiFrames / strafeFrames;
    float Mirror => strafeFrames == 0 ? 0f : (float)mirrorFrames / strafeFrames;

    protected override void Setup()
    {
        target = G.SpawnTarget(Difficulty.TrackRadius(Tier));
        target.Position = new Vector3(0, PlayerView.EyeHeight, -10);
        targetVx = (Rng.Next(2) == 0 ? -1 : 1) * Difficulty.TrackSpeed(Tier) * 0.85f;
        vx = targetVx;
        nextTurn = 0.8f;
    }

    public override void Update(float dt)
    {
        nextTurn -= dt;
        if (nextTurn <= 0)
        {
            var (a, b) = Difficulty.TurnInterval(Tier);
            targetVx = (Rng.Next(2) == 0 ? -1 : 1) * Difficulty.TrackSpeed(Tier) * 0.9f;
            nextTurn = R(a, b) * 1.15f; // a little longer than pure tracking: you need time to read and flip your own key
            DirectionChanged();
        }
        vx = Mathf.MoveToward(vx, targetVx, Difficulty.StrafeDecel * dt);
        var p = target.Position;
        p.X += vx * dt;
        if (Mathf.Abs(p.X) > 5f) { p.X = Mathf.Sign(p.X) * 5f; targetVx = -targetVx; vx = 0; DirectionChanged(); }
        target.Position = p;

        ScoreFrame(dt);

        // Same measurement the coach makes from telemetry: the target's lateral speed along the camera's right axis and
        // the player's A/D input (+1 = D, −1 = A). The target moves along world X, so the dot product is vx·cos(yaw).
        float lateral = vx * Mathf.Cos(Mathf.DegToRad(G.View.ViewYaw));
        float input = G.Mover.InputStrafe;
        if (MathF.Abs(input) >= 0.5f && MathF.Abs(lateral) >= 0.35f)
        {
            strafeFrames++;
            strafeTime += dt;
            if (MathF.Sign(input) == MathF.Sign(lateral)) mirrorFrames++; else antiFrames++;
        }

        // Coverage: half the run has to be spent strafing for the score to count in full, so standing still is not a way
        // to keep a clean anti-mirror percentage.
        float coverage = MathF.Min(1f, strafeTime / MathF.Max(6f, Duration * 0.5f));
        Score = (int)MathF.Round(100f * Anti * coverage);
    }

    public override IEnumerable<string> HudLines()
    {
        bool es = Loc.Spanish;
        if (strafeFrames == 0)
        {
            yield return es ? "Esperando strafe: muévete con A/D cuando el objetivo se mueva."
                            : "Waiting for strafe: move with A/D while the target moves.";
            yield break;
        }
        yield return es
            ? $"Anti-espejo {Anti * 100:0}% · espejo {Mirror * 100:0}% · {strafeTime:0.0} s"
            : $"Anti-mirror {Anti * 100:0}% · mirroring {Mirror * 100:0}% · {strafeTime:0.0} s";
        yield return es
            ? "Cuenta solo cuando ambos hacéis strafe; quedarte quieto no puntúa."
            : "Only counts when both of you strafe; standing still does not score.";
    }

    public override IEnumerable<(string Label, string Value)> ResultLines()
    {
        bool es = Loc.Spanish;
        foreach (var line in base.ResultLines()) yield return line;
        yield return (es ? "Anti-espejo (frames opuestos)" : "Anti-mirror (opposed frames)",
            strafeFrames == 0 ? "—" : $"{Anti * 100:0}%  ({antiFrames}/{strafeFrames})");
        yield return (es ? "Espejo (misma dirección)" : "Mirroring (same direction)",
            strafeFrames == 0 ? "—" : $"{Mirror * 100:0}%  ({mirrorFrames}/{strafeFrames})");
        yield return (es ? "Tiempo haciendo strafe" : "Time strafing", $"{strafeTime:0.0} s");
    }

    public override IEnumerable<(string Key, float Value)> ExtraMetrics()
    {
        foreach (var metric in base.ExtraMetrics()) yield return metric;
        yield return ("strafe.frames", strafeFrames);
        yield return ("strafe.mirror", mirrorFrames);
        yield return ("strafe.anti", antiFrames);
        yield return ("strafe.secs", MathF.Round(strafeTime, 1));
        yield return ("strafe.mirror_pct", Mirror * 100f);
        yield return ("strafe.anti_pct", Anti * 100f);
    }

    public override int Badge()
    {
        if (strafeTime < 8f) return -1;
        float a = Anti;
        if (a >= 0.75f) return Math.Min(4, Tier + 1);
        if (a >= 0.62f) return Tier;
        if (a >= 0.5f) return Tier - 1;
        return -1;
    }
}
