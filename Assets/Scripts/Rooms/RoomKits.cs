// What each zone builds and dresses its room walls with (FloorGenerator.RoomKit.cs and
// FloorGenerator.WallArt.cs): how often a metre of the rooms' north walls becomes a flat panel, a
// 2 m alcove set back into the wall, a broken top or a run of the zone's fixture (pipes, a display
// shelf, a handrail, a wayfinding band), where the pilasters stand along each cell's wall, the
// alcove's opening, and which wall decals the zone hangs: its everyday signs for unthemed rooms,
// the grime, graffiti and fittings that turn up between signs, and how boss arenas are smeared,
// and the mess left on the floor (FloorGenerator.FloorArt.cs).
// The modules' art is the zone's wall kit atlas (Tools/Sprites/wallkit.py); the opening heights
// here match it (the face is 32 texture rows over the 2 m wall, so a row is 1/16 m).
public struct RoomKit
{
    public float PanelChance;
    public float AlcoveChance;
    public float BrokenChance;
    public float FixtureChance;
    public int FixtureRun;            // longest run of the fixture, in metres (at least 2)
    public float[] PilasterOffsets;   // from the middle of each cell's wall, in metres
    public float AlcoveSill;          // top of the alcove's sill, in metres
    public float AlcoveLintel;        // underside of its lintel
    public float AlcoveDepth;         // how far it's set back into the wall

    public string[] EverydaySigns;    // wall decals for rooms without a theme
    public string[] Grime;            // between the signs, now and then
    public string[] BossGrime;        // boss arenas
    public float GrimeScale;          // times FloorGenerator's grimeDecalChance
    public string[] FloorGrime;       // floor decals: the mess on the floor
}

public static class RoomKits
{
    public static RoomKit For(Zone zone)
    {
        switch (zone)
        {
            // Shop windows under coloured fascias, marble and timber cladding, marble columns,
            // wall displays; tagged and looted, posters peeling.
            case Zone.ShoppingMall:
                return new RoomKit
                {
                    PanelChance = 0.16f, AlcoveChance = 0.14f, BrokenChance = 0.03f,
                    FixtureChance = 0.06f, FixtureRun = 3,
                    PilasterOffsets = new[] { -4f, 4f },
                    AlcoveSill = 0.5f, AlcoveLintel = 1.625f, AlcoveDepth = 0.3f,
                    EverydaySigns = new[] { "PosterDrink", "Sale", "PosterModel", "Clock", "MallDirectory" },
                    Grime = new[]
                    {
                        "GraffitiRun", "GraffitiHelp", "GraffitiDead", "GraffitiSafe", "GraffitiRun", "GraffitiSafe",
                        "Crack", "Damp", "Handprint", "BloodSmear", "BloodSplat", "BulletHoles", "Extinguisher", "Vent"
                    },
                    BossGrime = new[] { "BloodSplat", "BloodSmear", "BloodSmear", "Handprint", "GraffitiDead", "GraffitiRun", "Crack" },
                    GrimeScale = 1.2f,
                    FloorGrime = new[] { "ShatteredGlass", "ShatteredGlass", "SpiltDrink", "TileDamage", "TileDamage", "PaperScatter", "BloodDrag", "BloodyPrints", "FloorCrack", "Puddle" }
                };

            // Windows into the next ward, wipe-clean panels and tiling, handrails over bumper
            // guards; no columns. Damp, bloody handprints, the last stand of the staff.
            case Zone.Hospital:
                return new RoomKit
                {
                    PanelChance = 0.14f, AlcoveChance = 0.1f, BrokenChance = 0.04f,
                    FixtureChance = 0.14f, FixtureRun = 4,
                    PilasterOffsets = new float[0],
                    AlcoveSill = 0.75f, AlcoveLintel = 1.75f, AlcoveDepth = 0.2f,
                    EverydaySigns = new[] { "HandWash", "Directions", "Quiet", "Clock", "Biohazard" },
                    Grime = new[]
                    {
                        "Damp", "Damp", "Damp", "Crack", "Crack2", "Handprint", "Handprint", "BloodSmear", "BloodSmear",
                        "BloodSplat", "GraffitiHelp", "Tally", "Extinguisher", "Extinguisher", "Vent"
                    },
                    BossGrime = new[] { "BloodSmear", "BloodSmear", "BloodSplat", "Handprint", "Handprint", "GraffitiHelp", "Damp" },
                    GrimeScale = 1f,
                    FloorGrime = new[] { "ScatteredPills", "ScatteredPills", "BiohazardSpill", "Puddle", "Puddle", "TileDamage", "BloodDrag", "BloodDrag", "BloodyPrints", "PaperScatter" }
                };

            // Full-height glazing onto the apron, cladding and louvres, white steel columns,
            // wayfinding bands. The cleanest zone: cracks and the odd bullet hole.
            case Zone.Airport:
                return new RoomKit
                {
                    PanelChance = 0.12f, AlcoveChance = 0.12f, BrokenChance = 0.04f,
                    FixtureChance = 0.08f, FixtureRun = 4,
                    PilasterOffsets = new[] { -4f, 4f },
                    AlcoveSill = 0.125f, AlcoveLintel = 1.875f, AlcoveDepth = 0.25f,
                    EverydaySigns = new[] { "AdPoster", "Departures", "FlightClock", "AdPoster" },
                    Grime = new[]
                    {
                        "Crack", "Crack", "Crack2", "Crack2", "BulletHoles", "BloodSmear", "Handprint",
                        "GraffitiHelp", "Extinguisher", "Extinguisher", "Vent", "Vent"
                    },
                    BossGrime = new[] { "BloodSplat", "BloodSmear", "BulletHoles", "BulletHoles", "Crack", "Handprint" },
                    GrimeScale = 0.7f,
                    FloorGrime = new[] { "PaperScatter", "PaperScatter", "PaperScatter", "TileDamage", "FloorCrack", "Puddle", "ShatteredGlass", "BloodDrag", "BloodyPrints" }
                };

            // Roller-shutter bays, bolted plates and bare re-poured concrete, hazard-striped columns,
            // pipe runs, and the most damage: tally marks, fuse boxes, a firefight's bullet holes.
            default:
                return new RoomKit
                {
                    PanelChance = 0.12f, AlcoveChance = 0.08f, BrokenChance = 0.08f,
                    FixtureChance = 0.1f, FixtureRun = 4,
                    PilasterOffsets = new[] { -4f, 4f },
                    AlcoveSill = 0.125f, AlcoveLintel = 1.75f, AlcoveDepth = 0.25f,
                    EverydaySigns = new[] { "NoSmoking", "NoticeBoard", "Clock", "FuseBox" },
                    Grime = new[]
                    {
                        "Crack", "Crack", "Crack2", "Crack2", "Damp", "Damp", "Damp", "Handprint", "BloodSmear", "BloodSplat",
                        "BulletHoles", "BulletHoles", "Tally", "GraffitiRun", "GraffitiHelp", "GraffitiDead", "GraffitiSafe",
                        "Extinguisher", "Extinguisher", "Vent", "Vent", "FuseBox"
                    },
                    BossGrime = new[] { "BloodSplat", "BloodSplat", "BloodSmear", "BloodSmear", "Handprint", "BulletHoles", "Crack", "GraffitiRun" },
                    GrimeScale = 1f,
                    FloorGrime = new[] { "OilStain", "FloorCrack", "FloorCrack", "Rubble", "Rubble", "Scorch", "BloodDrag", "BloodyPrints", "Puddle", "Drain" }
                };
        }
    }
}
