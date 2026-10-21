using Godot;

namespace ValTrainer.Maps;

public static partial class MapSpots
{
    /// <summary>
    /// Pearl, A Main → A Site, generated from the real extracted art/collision.
    /// The GLBs and collision.json are external, under <c>maps/pearl/</c>; the floor below
    /// is only the fallback for installs without those optional files.
    /// </summary>
    public static readonly MapSpot Pearl = new MapSpot
    {
        Key = "pearl", Map = "Pearl", Name = "A Main → A Site",
        Drill = "Clear Dugout, Site, Site 2, Site 3, Site 4, Site 5, Site 6",
        StartFeet = V(-0.700f, 2.200f, 6.150f), StartYaw = 64.63f, Choke = V(10.816f, 0.253f, -1.521f),
        Palette = new MapPalette(C(112,122,120), C(222,214,192), C(64,150,146), C(184,142,90), C(180,174,158), C(170,194,208)),
        RealMapFolder = "maps/pearl",
        // Only used when the external real-map files are absent.
        Boxes = new[]
        {
            B(-80f, -0.1f, -80f, 80f, 0.2f, 80f, Surf.Floor),
        },
        Enemies = new[]
        {
            E("Dugout", 20.150f, 0.000f, -11.903f, Stance.Stand),
            E("Site", 17.445f, 0.285f, -2.453f, Stance.Stand),
            E("Site 2", 20.445f, 0.285f, -2.453f, Stance.Stand),
            E("Site 3", 14.445f, 0.285f, -0.953f, Stance.Stand),
            E("Site 4", 17.445f, 0.285f, -5.453f, Stance.Stand),
            E("Site 5", 20.945f, 0.285f, -5.453f, Stance.Stand),
            E("Site 6", 13.445f, 0.285f, -5.453f, Stance.Stand),
        },
        Flashes = new[]
        {
            F("Pop over the A Main entrance", V(-0.700f, 3.800f, 6.150f), V(9.595f, 1.485f, -1.349f)),
            F("High pop into the site", V(-0.700f, 3.800f, 6.150f), V(17.445f, 2.285f, -2.453f)),
            F("Curve around the choke", V(10.816f, 1.853f, -1.521f), V(15.701f, 1.685f, -2.207f)),
            F("Deep pop toward the back", V(-0.700f, 3.800f, 6.150f), V(14.445f, 2.485f, -4.453f)),
            F("Pop right of the site", V(-0.700f, 3.800f, 6.150f), V(21.445f, 1.685f, -2.453f)),
        },
        Smokes = new[]
        {
            new SmokeSpot("Dugout", V(20.150f, 0.000f, -11.903f)),
            new SmokeSpot("Site", V(17.445f, 0.285f, -2.453f)),
            new SmokeSpot("Site 2", V(20.445f, 0.285f, -2.453f)),
        },
        PlantSpots = new[]
        {
            V(17.445f, 0.285f, -2.453f),
            V(19.645f, 0.285f, -4.653f),
            V(15.245f, 0.285f, -0.253f),
            V(15.445f, 0.285f, -4.453f),
        },
        RetakeEntries = new[]
        {
            V(-3.750f, 1.200f, -22.803f),
            V(20.445f, 0.285f, -0.453f),
            V(14.445f, 0.285f, -0.453f),
        },
    };
}
