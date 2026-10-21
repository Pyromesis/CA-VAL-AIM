using Godot;

namespace ValTrainer.Maps;

public static partial class MapSpots
{
    /// <summary>
    /// Summit, A Main → A Site, generated from the real extracted art/collision.
    /// The GLBs and collision.json are external, under <c>maps/summit/</c>; the floor below
    /// is only the fallback for installs without those optional files.
    /// </summary>
    public static readonly MapSpot Summit = new MapSpot
    {
        Key = "summit", Map = "Summit", Name = "A Main → A Site",
        Drill = "Clear Garden, Site, Art, Cave, Hall, Link, Site 2",
        StartFeet = V(-0.200f, -0.800f, 2.050f), StartYaw = 51.53f, Choke = V(18.445f, 0.200f, -13.485f),
        Palette = new MapPalette(C(120,128,136), C(206,216,222), C(94,138,176), C(156,174,190), C(182,188,194), C(186,206,226)),
        RealMapFolder = "maps/summit",
        // Only used when the external real-map files are absent.
        Boxes = new[]
        {
            B(-80f, -0.1f, -80f, 80f, 0.2f, 80f, Surf.Floor),
        },
        Enemies = new[]
        {
            E("Garden", 12.350f, -0.800f, -20.400f, Stance.Stand),
            E("Site", 29.750f, -0.800f, -21.750f, Stance.Stand),
            E("Art", 10.750f, 1.200f, -35.000f, Stance.Stand),
            E("Cave", 39.250f, 2.200f, -41.600f, Stance.Stand),
            E("Hall", 4.150f, 2.200f, -38.000f, Stance.Stand),
            E("Link", -5.750f, 0.200f, -27.500f, Stance.Stand),
            E("Site 2", 32.750f, -0.800f, -21.750f, Stance.Stand),
        },
        Flashes = new[]
        {
            F("Pop over the A Main entrance", V(-0.200f, 0.800f, 2.050f), V(16.363f, 0.400f, -11.963f)),
            F("High pop into the site", V(-0.200f, 0.800f, 2.050f), V(29.750f, 1.200f, -21.750f)),
            F("Curve around the choke", V(18.445f, 1.800f, -13.485f), V(26.775f, 0.600f, -19.575f)),
            F("Deep pop toward the back", V(-0.200f, 0.800f, 2.050f), V(26.750f, 1.400f, -23.750f)),
            F("Pop right of the site", V(-0.200f, 0.800f, 2.050f), V(33.750f, 0.600f, -21.750f)),
        },
        Smokes = new[]
        {
            new SmokeSpot("Link", V(-5.750f, 0.200f, -27.500f)),
            new SmokeSpot("Garden", V(12.350f, -0.800f, -20.400f)),
            new SmokeSpot("Art", V(10.750f, 1.200f, -35.000f)),
        },
        PlantSpots = new[]
        {
            V(29.750f, -0.800f, -21.750f),
            V(31.950f, -0.800f, -24.100f),
            V(27.750f, -0.800f, -17.100f),
            V(27.750f, -0.800f, -23.750f),
        },
        RetakeEntries = new[]
        {
            V(10.750f, 1.200f, -35.000f),
            V(39.250f, 2.200f, -41.600f),
            V(4.150f, 2.200f, -38.000f),
        },
    };
}
