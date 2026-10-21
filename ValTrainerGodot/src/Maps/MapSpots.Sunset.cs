using Godot;

namespace ValTrainer.Maps;

public static partial class MapSpots
{
    /// <summary>
    /// Sunset, A Main → A Site, generated from the real extracted art/collision.
    /// The GLBs and collision.json are external, under <c>maps/sunset/</c>; the floor below
    /// is only the fallback for installs without those optional files.
    /// </summary>
    public static readonly MapSpot Sunset = new MapSpot
    {
        Key = "sunset", Map = "Sunset", Name = "A Main → A Site",
        Drill = "Clear Link, Site, Elbow, Alley, Site 2, Site 3, Site 4",
        StartFeet = V(1.000f, 3.200f, -7.400f), StartYaw = 53.75f, Choke = V(6.200f, 0.200f, -8.680f),
        Palette = new MapPalette(C(132,112,94), C(218,178,132), C(196,100,54), C(170,120,76), C(190,174,150), C(196,190,180)),
        RealMapFolder = "maps/sunset",
        // Only used when the external real-map files are absent.
        Boxes = new[]
        {
            B(-80f, -0.1f, -80f, 80f, 0.2f, 80f, Surf.Floor),
        },
        Enemies = new[]
        {
            E("Link", 7.750f, 3.200f, -22.300f, Stance.Stand),
            E("Site", 6.200f, 0.200f, -8.680f, Stance.Stand),
            E("Elbow", 16.000f, 3.000f, -0.550f, Stance.Stand),
            E("Alley", 16.000f, 3.200f, -38.350f, Stance.Stand),
            E("Site 2", 13.000f, 3.200f, -14.000f, Stance.Stand),
            E("Site 3", 7.000f, 3.200f, -12.500f, Stance.Stand),
            E("Site 4", 8.750f, 3.200f, -17.250f, Stance.Stand),
        },
        Flashes = new[]
        {
            F("Pop over the A Main entrance", V(1.000f, 4.800f, -7.400f), V(5.500f, 4.400f, -7.700f)),
            F("High pop into the site", V(1.000f, 4.800f, -7.400f), V(10.000f, 5.200f, -14.000f)),
            F("Curve around the choke", V(6.200f, 1.800f, -8.680f), V(9.000f, 4.600f, -12.600f)),
            F("Deep pop toward the back", V(1.000f, 4.800f, -7.400f), V(7.000f, 5.400f, -16.000f)),
            F("Pop right of the site", V(1.000f, 4.800f, -7.400f), V(14.000f, 4.600f, -14.000f)),
        },
        Smokes = new[]
        {
            new SmokeSpot("Link", V(7.750f, 3.200f, -22.300f)),
            new SmokeSpot("Elbow", V(16.000f, 3.000f, -0.550f)),
            new SmokeSpot("Site", V(6.200f, 0.200f, -8.680f)),
        },
        PlantSpots = new[]
        {
            V(10.000f, 3.200f, -14.000f),
            V(12.200f, 3.200f, -16.200f),
            V(7.800f, 3.200f, -11.800f),
            V(8.250f, 3.200f, -17.250f),
        },
        RetakeEntries = new[]
        {
            V(7.750f, 3.200f, -22.300f),
            V(16.000f, 3.000f, -0.550f),
            V(16.000f, 3.200f, -38.350f),
        },
    };
}
