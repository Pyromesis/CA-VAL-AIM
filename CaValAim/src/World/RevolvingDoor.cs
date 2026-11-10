using Godot;
using CaValAim.Game;

namespace CaValAim.World;

/// <summary>
/// Lotus' stone door (A Rubble ↔ A Tree, and C Mound ↔ B Main): a pair of stone wings around a central
/// vertical axis. It is closed by default; the button on either side of the frame starts a single 180° turn
/// that takes <see cref="Seconds"/> and stops back in the closed position. The wings are solid, so the
/// only way through is the short window while they sweep past edge-on (exactly like the real door).
/// </summary>
public partial class RevolvingDoor : Node3D
{
    /// <summary>Distance from the axis to each wing tip; half the carved opening's width.</summary>
    public float Radius = 1.75f;
    public float Height = 3.45f;
    /// <summary>Seconds for the 180° turn (VALORANT: 10 s).</summary>
    public float Seconds = 10f;

    /// <summary>Total turn so far, radians. 0 / π / 2π all look identical (the door is symmetric).</summary>
    public float Angle { get; private set; }
    public bool Turning { get; private set; }
    float target;

    /// <summary>World position of the two buttons: one on each face of the wall (south / north).</summary>
    public Vector3 ButtonA, ButtonB;

    /// <summary>Number of collision boxes <see cref="BuildBoxes"/> returns (fixed, so the collision grid never reallocates).</summary>
    public int BoxCount => Segments * 2;
    const int Segments = 10;

    /// <summary>Starts the 180° turn. False when it is already turning.</summary>
    public bool Toggle()
    {
        if (Turning) return false;
        Turning = true;
        target = Angle + Mathf.Pi;
        return true;
    }

    /// <summary>Advances the turn (driven by the session, so pausing freezes the door too).</summary>
    public void Advance(float dt)
    {
        if (!Turning) return;
        Angle = Mathf.MoveToward(Angle, target, Mathf.Pi / Mathf.Max(0.1f, Seconds) * dt);
        if (Angle >= target - 1e-4f)
        {
            Angle = target;
            Turning = false;
        }
        Rotation = new Vector3(0f, Angle, 0f);
    }

    /// <summary>
    /// The door's collision for the current angle: both wings, split into short AABBs along their length
    /// (the world is AABB-only). <paramref name="skip"/> is the body position: a box whose volume (grown by
    /// <paramref name="skipMargin"/>) already contains it is moved far away instead, so the turning wing lets the
    /// player slip through its pass window instead of teleporting them to its edge.
    /// </summary>
    public void BuildBoxes(List<Box> into, Vector3 skip, float skipMargin = 0.04f)
    {
        into.Clear();
        float step = Radius / Segments;
        float halfAlong = step * 0.5f + 0.02f;
        float halfThick = 0.13f;
        float cos = Mathf.Cos(Angle), sin = Mathf.Sin(Angle);
        for (int wing = 0; wing < 2; wing++)
        {
            float sign = wing == 0 ? 1f : -1f;
            for (int i = 0; i < Segments; i++)
            {
                float r = (i + 0.5f) * step;
                var c = Position + new Vector3(sign * cos * r, 0f, -sign * sin * r);
                float ex = Mathf.Abs(cos) * halfAlong + Mathf.Abs(sin) * halfThick;
                float ez = Mathf.Abs(sin) * halfAlong + Mathf.Abs(cos) * halfThick;
                var b = new Box(new Vector3(c.X - ex, Position.Y, c.Z - ez), new Vector3(c.X + ex, Position.Y + Height, c.Z + ez));
                bool inside = skip.X > b.Min.X - skipMargin && skip.X < b.Max.X + skipMargin
                    && skip.Z > b.Min.Z - skipMargin && skip.Z < b.Max.Z + skipMargin
                    && skip.Y + 1.7f > b.Min.Y && skip.Y < b.Max.Y;
                into.Add(inside ? new Box(new Vector3(0f, -5000f, 0f), new Vector3(0.001f, -4999f, 0.001f)) : b);
            }
        }
    }

    /// <summary>A box that covers the whole swept volume: it is only used once to register these indices in
    /// every broadphase cell the door can ever touch (the list itself stays a fixed size afterwards).</summary>
    public Box SweptBounds =>
        new(new Vector3(Position.X - Radius - 0.4f, Position.Y, Position.Z - Radius - 0.4f),
            new Vector3(Position.X + Radius + 0.4f, Position.Y + Height, Position.Z + Radius + 0.4f));
}
