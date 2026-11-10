using CaValAim.Game;
using Godot;

namespace CaValAim.Maps;

// Collision strips at measured crossings through solid walls in the imported map art.
// Coordinates are in the final world frame, after any map mirroring.
internal static class MapCollisionRepairs
{
    internal static Box[] For(string key) => key switch
    {
        "icebox" => new Box[]
        {
            new(new Vector3(-29.795f, 0.250f, 94.490f), new Vector3(-29.705f, 1.800f, 95.010f)),
            new(new Vector3(-29.795f, 0.250f, 94.990f), new Vector3(-29.705f, 1.800f, 95.510f)),
            new(new Vector3(-29.795f, 0.250f, 95.490f), new Vector3(-29.705f, 1.800f, 96.010f)),
        },
        "split" => new Box[]
        {
            new(new Vector3(2.990f, 8.200f, 53.332f), new Vector3(3.510f, 9.750f, 53.422f)),
            new(new Vector3(3.490f, 8.200f, 53.332f), new Vector3(4.010f, 9.750f, 53.422f)),
        },
        "abyss" => new Box[]
        {
            new(new Vector3(51.490f, -2.400f, 48.955f), new Vector3(52.010f, -0.850f, 49.045f)),
            new(new Vector3(51.990f, -2.400f, 48.955f), new Vector3(52.510f, -0.850f, 49.045f)),
            new(new Vector3(52.490f, -2.200f, 48.955f), new Vector3(53.010f, -0.650f, 49.045f)),
            new(new Vector3(-2.010f, -1.750f, 98.955f), new Vector3(-1.490f, -0.200f, 99.045f)),
            new(new Vector3(-1.045f, -1.750f, 98.570f), new Vector3(-0.955f, -0.200f, 99.090f)),
            new(new Vector3(-1.510f, -1.750f, 98.955f), new Vector3(-0.990f, -0.200f, 99.045f)),
        },
        "lotus" => new Box[]
        {
            new(new Vector3(-56.038f, 1.250f, 71.990f), new Vector3(-55.948f, 2.800f, 72.510f)),
            new(new Vector3(-56.032f, 1.250f, 72.490f), new Vector3(-55.942f, 2.800f, 73.010f)),
        },
        "pearl" => new Box[]
        {
            new(new Vector3(-14.236f, 1.700f, 5.990f), new Vector3(-14.146f, 3.250f, 6.510f)),
            new(new Vector3(-14.236f, 1.850f, 6.490f), new Vector3(-14.146f, 3.400f, 7.010f)),
            new(new Vector3(-14.236f, 2.100f, 6.990f), new Vector3(-14.146f, 3.650f, 7.510f)),
        },
        "corrode" => new Box[]
        {
            new(new Vector3(30.990f, -0.850f, 38.538f), new Vector3(31.510f, 0.700f, 38.628f)),
            new(new Vector3(31.490f, -0.850f, 38.538f), new Vector3(32.010f, 0.700f, 38.628f)),
            new(new Vector3(31.990f, -0.850f, 38.538f), new Vector3(32.510f, 0.700f, 38.628f)),
        },
        _ => Array.Empty<Box>(),
    };
}
