using Godot;

namespace ValTrainer.Maps;

public static partial class MapSpots
{
    /// <summary>
    /// Lotus, A Main → A Site, generated from the real extracted art/collision.
    /// The GLBs and collision.json are external, under <c>maps/lotus/</c>; the floor below
    /// is only the fallback for installs without those optional files.
    /// </summary>
    public static readonly MapSpot Lotus = new MapSpot
    {
        Key = "lotus", Map = "Lotus", Name = "A Main → A Site",
        Drill = "Clear Top, Drop, Site, Hut, Tree, Stairs, Site 2",
        StartFeet = V(3.250f, 0.874f, 7.280f), StartYaw = 18.66f, Choke = V(8.665f, 0.874f, -15.173f),
        Palette = new MapPalette(C(130,118,102), C(220,194,154), C(170,70,126), C(186,146,98), C(184,174,158), C(172,198,214)),
        RealMapFolder = "maps/lotus",
        // Only used when the external real-map files are absent.
        Boxes = new[]
        {
            B(-80f, -0.1f, -80f, 80f, 0.2f, 80f, Surf.Floor),
        },
        Enemies = new[]
        {
            E("Top", 11.950f, 0.874f, -37.422f, Stance.Stand),
            E("Drop", 19.331f, 0.874f, -42.281f, Stance.Stand),
            E("Site", 13.975f, 0.874f, -24.472f, Stance.Stand),
            E("Hut", 13.975f, 0.874f, -26.292f, Stance.Stand),
            E("Tree", 8.665f, 0.874f, -15.173f, Stance.Stand),
            E("Stairs", -2.988f, 0.874f, -29.696f, Stance.Stand),
            E("Site 2", 16.975f, 0.874f, -24.472f, Stance.Stand),
        },
        Flashes = new[]
        {
            F("Pop over the A Main entrance", V(3.250f, 2.474f, 7.280f), V(7.687f, 2.074f, -13.460f)),
            F("High pop into the site", V(3.250f, 2.474f, 7.280f), V(13.975f, 2.874f, -24.472f)),
            F("Curve around the choke", V(8.665f, 2.474f, -15.173f), V(12.578f, 2.274f, -22.025f)),
            F("Deep pop toward the back", V(3.250f, 2.474f, 7.280f), V(10.975f, 3.074f, -26.472f)),
            F("Pop right of the site", V(3.250f, 2.474f, 7.280f), V(17.975f, 2.274f, -24.472f)),
        },
        Smokes = new[]
        {
            new SmokeSpot("Tree", V(8.665f, 0.874f, -15.173f)),
            new SmokeSpot("Hut", V(13.975f, 0.874f, -26.292f)),
            new SmokeSpot("Stairs", V(-2.988f, 0.874f, -29.696f)),
        },
        PlantSpots = new[]
        {
            V(13.975f, 0.874f, -24.472f),
            V(16.175f, 0.874f, -27.122f),
            V(11.775f, 0.874f, -22.272f),
            V(11.975f, 0.874f, -26.472f),
        },
        RetakeEntries = new[]
        {
            V(19.331f, 0.874f, -42.281f),
            V(13.975f, 0.874f, -26.292f),
            V(-2.988f, 0.874f, -29.696f),
        },
    };
}
