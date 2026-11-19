using CaValAim.Core;

namespace CaValAim.Analysis;

/// <summary>A bounded, temporary sensitivity experiment driven by first-shot misses.</summary>
public sealed class LiveSensitivity(float baseline, float startedAt, float? lastCandidate = null)
{
    public readonly record struct Evidence(float From, float At, int Attempts, int Hits, int Over, int Under);
    public float Baseline { get; } = baseline;
    public float Candidate { get; private set; } = lastCandidate ?? baseline;
    public float Since { get; private set; } = startedAt;
    public bool Active { get; private set; } = true;
    public int Steps { get; private set; }
    public Evidence LastEvidence { get; private set; }
    int direction;

    public void Stop() => Active = false;

    public bool TryAdjust(Evidence e, float now)
    {
        if (!Active || e.From < Since || e.At < Since + 8 || e.At <= LastEvidence.At) return false;
        LastEvidence = e;
        int biased = e.Over + e.Under;
        if (e.Attempts < 8 || biased < 5 || biased * 5 < e.Attempts || Math.Max(e.Over, e.Under) * 4 < biased * 3)
            return false;
        int next = e.Under > e.Over ? 1 : -1;
        float step = direction != 0 && direction != next ? .015f : .03f;
        float low = Math.Max(SensInput.Min, Baseline * .88f), high = Math.Min(SensInput.Max, Baseline * 1.12f);
        float value = Math.Clamp(MathF.Round(Candidate * (1f + next * step), 4), low, high);
        if (Math.Abs(value - Candidate) < .0001f) return false;
        Candidate = value;
        Since = now;
        direction = next;
        Steps++;
        return true;
    }
}
