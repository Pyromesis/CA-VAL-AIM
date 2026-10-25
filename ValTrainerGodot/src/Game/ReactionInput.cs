using Godot;

namespace ValTrainer.Game;

/// <summary>
/// Input timestamps for reaction measurements (<see cref="Modes.ReactionLabMode"/>). Clicks and mouse movement are stamped
/// with <see cref="Time.GetTicksUsec"/> when the event reaches <c>_Input</c> (agile input flushing is on), not when the
/// next frame processes it, so a reaction time is not rounded up to the frame time.
/// </summary>
public partial class GameSession
{
    /// <summary>Microsecond timestamp of the last fire press (0 = none yet this run).</summary>
    public ulong LastFirePressUsec { get; private set; }

    readonly ulong[] moveUsec = new ulong[512];
    readonly float[] moveMag = new float[512];
    int moveHead, moveCount;

    void StampFire() => LastFirePressUsec = Time.GetTicksUsec();

    void StampMove(Vector2 rel)
    {
        moveUsec[moveHead] = Time.GetTicksUsec();
        moveMag[moveHead] = Mathf.Abs(rel.X) + Mathf.Abs(rel.Y);
        moveHead = (moveHead + 1) % moveUsec.Length;
        moveCount = Math.Min(moveCount + 1, moveUsec.Length);
    }

    /// <summary>
    /// The time of the first mouse movement after <paramref name="since"/> once at least <paramref name="minCounts"/> mouse
    /// counts have accumulated (a deliberate move, not sensor jitter), or 0 when there was none.
    /// </summary>
    public ulong FirstMoveAfter(ulong since, float minCounts)
    {
        float sum = 0;
        ulong first = 0;
        for (int i = 0; i < moveCount; i++)
        {
            int idx = (moveHead - moveCount + i + moveUsec.Length) % moveUsec.Length;
            if (moveUsec[idx] <= since) continue;
            if (first == 0) first = moveUsec[idx];
            sum += moveMag[idx];
            if (sum >= minCounts) return first;
        }
        return 0;
    }
}
