// What each zone builds its room walls from (FloorGenerator.RoomKit.cs): how often a metre of
// the rooms' north walls becomes a flat panel, a 2 m alcove set back into the wall or a broken
// top, where the pilasters stand along each cell's wall, and the alcove's opening. The modules'
// art is the zone's wall kit atlas (Tools/Sprites/wallkit.py); the opening heights here match it
// (the face is 32 texture rows over the 2 m wall, so a row is 1/16 m).
public struct RoomKit
{
    public float PanelChance;
    public float AlcoveChance;
    public float BrokenChance;
    public float[] PilasterOffsets;   // from the middle of each cell's wall, in metres
    public float AlcoveSill;          // top of the alcove's sill, in metres
    public float AlcoveLintel;        // underside of its lintel
    public float AlcoveDepth;         // how far it's set back into the wall
}

public static class RoomKits
{
    public static RoomKit For(Zone zone)
    {
        switch (zone)
        {
            // Shop windows under coloured fascias, marble and timber cladding, marble columns.
            case Zone.ShoppingMall:
                return new RoomKit
                {
                    PanelChance = 0.16f, AlcoveChance = 0.14f, BrokenChance = 0.03f,
                    PilasterOffsets = new[] { -4f, 4f },
                    AlcoveSill = 0.5f, AlcoveLintel = 1.625f, AlcoveDepth = 0.3f
                };

            // Windows into the next ward, wipe-clean panels and tiling; no columns, damp breaks.
            case Zone.Hospital:
                return new RoomKit
                {
                    PanelChance = 0.14f, AlcoveChance = 0.1f, BrokenChance = 0.04f,
                    PilasterOffsets = new float[0],
                    AlcoveSill = 0.75f, AlcoveLintel = 1.75f, AlcoveDepth = 0.2f
                };

            // Full-height glazing onto the apron, cladding and louvres, white steel columns.
            case Zone.Airport:
                return new RoomKit
                {
                    PanelChance = 0.12f, AlcoveChance = 0.12f, BrokenChance = 0.04f,
                    PilasterOffsets = new[] { -4f, 4f },
                    AlcoveSill = 0.125f, AlcoveLintel = 1.875f, AlcoveDepth = 0.25f
                };

            // Roller-shutter bays, bolted plates and bare re-poured concrete, hazard-striped columns,
            // and the most damage.
            default:
                return new RoomKit
                {
                    PanelChance = 0.12f, AlcoveChance = 0.08f, BrokenChance = 0.08f,
                    PilasterOffsets = new[] { -4f, 4f },
                    AlcoveSill = 0.125f, AlcoveLintel = 1.75f, AlcoveDepth = 0.25f
                };
        }
    }
}
