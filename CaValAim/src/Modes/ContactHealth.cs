namespace CaValAim.Modes;

/// <summary>Damage measured as seconds on target; health recovers after a grace period.</summary>
public sealed class ContactHealth
{
    public float Progress { get; private set; }
    float offTime;
    public void Reset() { Progress = offTime = 0; }

    public void Tick(bool hit, float dt, float timeToKill, float delay, float regenSeconds)
    {
        if (hit)
        {
            Progress = Math.Min(timeToKill, Progress + dt);
            offTime = 0;
            return;
        }
        float before = offTime;
        offTime += dt;
        float healingTime = Math.Max(0, offTime - delay) - Math.Max(0, before - delay);
        Progress = Math.Max(0, Progress - healingTime * timeToKill / regenSeconds);
    }
}
