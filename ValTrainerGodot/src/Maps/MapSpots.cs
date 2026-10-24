using Godot;

namespace ValTrainer.Maps;

/// <summary>
/// One site-attack drill per map (one partial file per map). Every map plays on its full extracted geometry
/// (<c>maps/&lt;key&gt;/</c>, see <see cref="World.RealMap"/>); Breeze, Haven, Icebox and Split also keep the true-scale
/// blockout they were traced as, used only when that folder is missing (<see cref="Real"/>).
/// </summary>
public static partial class MapSpots
{
    static Color C(int r, int g, int b) => Color.Color8((byte)r, (byte)g, (byte)b, 255);
    static BoxDef B(float x0, float y0, float z0, float x1, float y1, float z1, Surf s = Surf.Wall) =>
        new(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), s);
    static EnemySpot E(string n, float x, float y, float z, Stance s = Stance.Stand) => new(n, new Vector3(x, y, z), s);
    static FlashSpot F(string n, Vector3 from, Vector3 pop) => new(n, from, pop);
    static Vector3 V(float x, float y, float z) => new(x, y, z);

    // Lazy: the map fields live in other partial files (MapSpots.<Map>.cs), whose initialisation order is unspecified.
    static MapSpot[]? all;
    public static MapSpot[] All => all ??= new[]
    {
        Ascent, Bind, Real(Breeze, BreezeBlockout), Real(Haven, HavenBlockout), Real(Icebox, IceboxBlockout), Real(Split, SplitBlockout),
        Fracture, Abyss, Lotus, Sunset, Pearl, Summit, Corrode,
    };

    /// <summary>The drill on the extracted map when its art is installed, else the traced blockout of the same map.</summary>
    static MapSpot Real(MapSpot real, MapSpot blockout) => World.RealMap.HasArt(real.RealMapFolder) ? real : blockout;

    public static MapSpot ByKey(string? key) => All.FirstOrDefault(m => m.Key == key) ?? Ascent;
}
