using System.Collections.Generic;
using UnityEngine;

// Floor decals: markings painted on the room floors and the mess left on them. Now and then a
// cell gets one of its theme's markings (bay lines and KEEP CLEAR boxes in a storage room, a
// compass star inlaid on a shop floor, coloured wayfinding lines across a hospital ward, WAIT HERE
// lines at a security check...) and, separately, some mess (oil, cracks, scorches, puddles,
// blood dragged across the floor); boss arenas get blood and scorch marks clear of the painted
// arena ring. Each is a flat 3 x 3 m quad just above the floor, turned a random quarter turn
// (ones with writing on stay the right way up), on
// the floor texture's pixel grid (16 px per metre; Tools/Sprites/floordecals.py), lit like the
// floor and under everything else on it (blood, litter, door stripes). Kept off doorways, the
// hatch and each other. Uses the wall decals' random sequence (FloorGenerator.WallArt.cs), so
// the rest of the floor comes out the same for a seed.
public partial class FloorGenerator
{
    [Header("Floor Decals")]
    [Tooltip("URP Lit, alpha clipped, FloorDecals.png as the base map. Empty = no floor decals.")]
    [SerializeField] private Material floorDecalMaterial;
    [Tooltip("Chance a cell gets one of its theme's floor markings.")]
    [Range(0f, 1f)]
    [SerializeField] private float themeFloorDecalChance = 0.45f;
    [Tooltip("Chance a cell gets some mess on the floor (oil, cracks, scorches, blood).")]
    [Range(0f, 1f)]
    [SerializeField] private float grimeFloorDecalChance = 0.45f;

    private const float FloorDecalSize = 3f;          // a cell of the sheet: 48 px at 16 px/m
    private const int FloorDecalColumns = 8;
    private const int FloorDecalCellPixels = 48;
    private const float FloorDecalHeight = 0.004f;    // under blood (0.006+), litter and door stripes

    // The sheet's cells in order (Tools/Sprites/floordecals.py's DECALS must match).
    private static readonly string[] FloorDecalNames =
    {
        "OilStain", "Drain", "FloorCrack", "Scorch", "BloodDrag", "BloodyPrints", "Puddle",
        "BayLines", "HazardBox", "FloorArrow", "HelipadH",
        "MallStar", "SpiltDrink", "WetFootprints",
        "Wayfinding", "WayfindingTurn", "BiohazardSpill",
        "QueueLine", "GateNumber"
    };

    private static readonly string[] FloorGrime =
    {
        "OilStain", "FloorCrack", "FloorCrack", "Scorch", "BloodDrag", "BloodyPrints", "Puddle", "Drain"
    };

    private static readonly string[] BossFloorDecals = { "BloodDrag", "BloodDrag", "BloodyPrints", "Scorch", "FloorCrack" };

    // Ones with writing on, kept the right way up to the camera instead of turned at random.
    private static readonly string[] UprightFloorDecals = { "BayLines", "HazardBox", "QueueLine", "GateNumber", "HelipadH", "Wayfinding" };

    // What a theme paints on its floor (empty: nothing but mess). Unthemed rooms get the zone's.
    private string[] ThemeFloorDecals(RoomTheme theme)
    {
        switch (theme)
        {
            case RoomTheme.Storage: return new[] { "BayLines", "BayLines", "HazardBox", "FloorArrow" };
            case RoomTheme.Workshop: return new[] { "OilStain", "HazardBox", "BayLines", "Drain" };
            case RoomTheme.Infirmary: return new[] { "Drain", "Puddle" };
            case RoomTheme.Armoury: return new[] { "HazardBox", "FloorArrow" };
            case RoomTheme.Checkpoint: return new[] { "HazardBox", "FloorArrow", "HelipadH" };
            case RoomTheme.Lab: return new[] { "BiohazardSpill", "Drain", "HazardBox" };
            case RoomTheme.ShopFloor: return new[] { "MallStar", "SpiltDrink", "WetFootprints" };
            case RoomTheme.FoodCourt: return new[] { "SpiltDrink", "SpiltDrink", "WetFootprints" };
            case RoomTheme.StoreRoom: return new[] { "BayLines", "FloorArrow", "HazardBox" };
            case RoomTheme.Entrance: return new[] { "MallStar", "WetFootprints" };
            case RoomTheme.Ward:
            case RoomTheme.WaitingRoom:
            case RoomTheme.Reception: return new[] { "Wayfinding", "Wayfinding", "Drain" };
            case RoomTheme.Theatre: return new[] { "Drain", "BiohazardSpill", "Wayfinding" };
            case RoomTheme.Pharmacy: return new[] { "Wayfinding" };
            case RoomTheme.GateLounge: return new[] { "GateNumber", "QueueLine" };
            case RoomTheme.BaggageHall: return new[] { "FloorArrow", "HazardBox" };
            case RoomTheme.SecurityCheck: return new[] { "QueueLine", "QueueLine", "FloorArrow" };
            case RoomTheme.CheckIn: return new[] { "QueueLine", "FloorArrow" };
            case RoomTheme.Barracks:
            case RoomTheme.Office:
            case RoomTheme.SecurityOffice:
            case RoomTheme.DutyFree: return new string[0];
        }

        switch (Zone)
        {
            case Zone.ShoppingMall: return new[] { "WetFootprints", "SpiltDrink" };
            case Zone.Hospital: return new[] { "Wayfinding", "Puddle" };
            case Zone.Airport: return new[] { "FloorArrow", "QueueLine" };
            default: return new[] { "OilStain", "Drain", "FloorArrow" };
        }
    }

    // Paints the room's floor. Run after FurnishRoom (with its theme) and the boss arena ring.
    private void PaintFloor(Room room, RoomTheme theme)
    {
        if (floorDecalMaterial == null || floorDecalMaterial.mainTexture == null)
            return;

        List<Vector3> keepClear = DoorwayPositions(room);
        List<Vector3> hatch = HatchSpot(room);
        List<Vector3> painted = new List<Vector3>();
        bool boss = room.Type == RoomType.Boss;
        string[] markings = boss ? BossFloorDecals : ThemeFloorDecals(theme);
        float markingChance = boss ? 1f : room.Type == RoomType.Normal ? themeFloorDecalChance : 0.8f;
        float grimeChance = boss ? 0.8f : grimeFloorDecalChance;

        foreach (Vector2Int cell in room.Cells)
        {
            Vector3 centre = CellToWorld(cell);

            if (markings.Length > 0 && decalRng.NextDouble() < markingChance)
                TryPaintFloor(room, markings[decalRng.Next(markings.Length)], centre, keepClear, hatch, painted, boss);

            if (decalRng.NextDouble() < grimeChance)
                TryPaintFloor(room, FloorGrime[decalRng.Next(FloorGrime.Length)], centre, keepClear, hatch, painted, boss);
        }
    }

    private void TryPaintFloor(Room room, string name, Vector3 centre, List<Vector3> doorways, List<Vector3> hatch, List<Vector3> painted, bool boss)
    {
        // Clear of the painted arena ring in a boss room (85% of the room's short side across).
        float hatchClearance = boss ? Mathf.Min(roomSize.x, roomSize.y) * 0.43f + 1.2f : HatchClearance + 1.5f;
        Vector2 range = roomSize / 2f - Vector2.one * 1.6f;

        // Wayfinding lines run the width of the cell, wall to wall, a tile at a time.
        if (name == "Wayfinding")
        {
            float z = centre.z + RandomDecal(-range.y, range.y);
            int tiles = Mathf.FloorToInt(roomSize.x / FloorDecalSize);

            for (int i = 0; i < tiles; i++)
            {
                Vector3 tile = new Vector3(centre.x + (i - (tiles - 1) / 2f) * FloorDecalSize, 0f, z);

                if (IsClear(tile, doorways, 2.2f) && IsClear(tile, hatch, hatchClearance))
                {
                    PlaceFloorDecal(room.transform, name, tile);
                    painted.Add(tile);
                }
            }

            return;
        }

        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector3 spot = centre + new Vector3(RandomDecal(-range.x, range.x), 0f, RandomDecal(-range.y, range.y));

            if (!IsClear(spot, doorways, 2.6f) || !IsClear(spot, hatch, hatchClearance) || !IsClear(spot, painted, 3.2f))
                continue;

            PlaceFloorDecal(room.transform, name, spot);
            painted.Add(spot);
            return;
        }
    }

    private float RandomDecal(float min, float max)
    {
        return Mathf.Lerp(min, max, (float)decalRng.NextDouble());
    }

    private void PlaceFloorDecal(Transform parent, string name, Vector3 spot)
    {
        int index = System.Array.IndexOf(FloorDecalNames, name);
        Texture texture = floorDecalMaterial.mainTexture;
        int rows = Mathf.Max(1, texture.height / FloorDecalCellPixels);

        if (index < 0 || index >= FloorDecalColumns * rows)
            return;

        // Corners on whole pixels of the floor texture (16 px per metre, world aligned).
        float half = FloorDecalSize / 2f;
        spot.x = Mathf.Round((spot.x - half) * 16f) / 16f + half;
        spot.z = Mathf.Round((spot.z - half) * 16f) / 16f + half;

        GameObject decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
        decal.name = "Floor Decal " + name;
        Destroy(decal.GetComponent<Collider>());
        decal.transform.SetParent(parent, true);
        decal.transform.position = new Vector3(spot.x, FloorDecalHeight, spot.z);
        int turns = System.Array.IndexOf(UprightFloorDecals, name) >= 0 ? 0 : decalRng.Next(4);
        decal.transform.rotation = Quaternion.Euler(90f, turns * 90f, 0f);
        decal.transform.localScale = new Vector3(FloorDecalSize, FloorDecalSize, 1f);

        Renderer renderer = decal.GetComponent<Renderer>();
        renderer.sharedMaterial = floorDecalMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        int column = index % FloorDecalColumns;
        int row = index / FloorDecalColumns;
        decalBlock ??= new MaterialPropertyBlock();
        decalBlock.SetVector("_BaseMap_ST", new Vector4(1f / FloorDecalColumns, 1f / rows, (float)column / FloorDecalColumns, 1f - (row + 1f) / rows));
        renderer.SetPropertyBlock(decalBlock);
    }
}
