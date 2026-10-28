using System.Collections.Generic;
using UnityEngine;

// Floor decals: one-off things on the room floors and the mess left on them. The room's painted
// lines, tape and carpets are laid first (FloorGenerator.FloorLines.cs); then now and then a cell
// gets one of its theme's own marks (an oil stain in a workshop, a spilt drink in a food court,
// a biohazard spill in a lab), a few themes get a fixture in each cell's middle (an inlaid star on
// a mall entrance floor, a drain in an operating theatre) and, separately, some of its zone's
// mess (RoomKits.FloorGrime: rubble and oil on the base, shattered shop glass in the mall, spilt
// pills in the hospital, trodden paper in the airport, and blood and cracks everywhere); boss
// arenas get blood and scorch marks clear of the painted arena ring. Each is a flat 3 x 3 m quad
// just above the floor, turned a random quarter turn, on the floor texture's pixel grid (16 px
// per metre; Tools/Sprites/floordecals.py), lit like the floor and under everything else on it
// (blood, litter, door stripes). Kept off doorways, the hatch and each other. Uses the wall
// decals' random sequence (FloorGenerator.WallArt.cs), so the rest of the floor comes out the
// same for a seed.
public partial class FloorGenerator
{
    [Header("Floor Decals")]
    [Tooltip("URP Lit, alpha clipped, FloorDecals.png as the base map. Empty = no floor decals.")]
    [SerializeField] private Material floorDecalMaterial;
    [Tooltip("Chance a cell gets one of its theme's own floor marks.")]
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
        "MallStar", "SpiltDrink", "WetFootprints", "BiohazardSpill",
        "Rubble", "TileDamage", "ShatteredGlass", "ScatteredPills", "PaperScatter"
    };

    private static readonly string[] BossFloorDecals = { "BloodDrag", "BloodDrag", "BloodyPrints", "Scorch", "FloorCrack" };

    // A theme's own marks, scattered (empty: nothing but the zone's mess).
    private static string[] ThemeFloorDecals(RoomTheme theme)
    {
        switch (theme)
        {
            case RoomTheme.Workshop: return new[] { "OilStain", "OilStain", "Drain" };
            case RoomTheme.Storage:
            case RoomTheme.BaggageHall: return new[] { "OilStain" };
            case RoomTheme.Infirmary: return new[] { "Puddle", "Drain" };
            case RoomTheme.PatientWing: return new[] { "BloodDrag", "BloodyPrints", "ScatteredPills", "Puddle" };
            case RoomTheme.Lab: return new[] { "BiohazardSpill", "Drain" };
            case RoomTheme.Theatre: return new[] { "BiohazardSpill" };
            case RoomTheme.FoodCourt: return new[] { "SpiltDrink", "SpiltDrink", "WetFootprints" };
            case RoomTheme.ShopFloor:
            case RoomTheme.Entrance: return new[] { "WetFootprints" };
            default: return new string[0];
        }
    }

    // A themed room's own floor in place of the zone's concrete or tiles (SpriteVisuals.themeFloors,
    // Tools/Sprites/themefloors.py): carparks and stores polished concrete, workshops tread plate,
    // offices carpet tiles, barracks parquet, labs vinyl, mall floors terrazzo, and so on. Rooms
    // without a theme (and the operating theatre, which keeps the hospital's mint tiles) keep theirs.
    private void UseThemeFloor(Room room, RoomTheme theme)
    {
        string name = ThemeFloorName(theme);
        Material material = name != null && SpriteVisuals.Current != null ? SpriteVisuals.Current.ThemeFloor(name) : null;
        Transform floor = material != null ? room.transform.Find("Floor Mesh") : null;

        if (floor != null && floor.TryGetComponent(out Renderer renderer))
        {
            renderer.sharedMaterial = material;
            BuildFloorBorder(room, material);
        }
    }

    private const float FloorBorderWidth = 0.5f;       // 8 px of the 16 px/m floor
    private static readonly Color FloorBorderShade = new Color(0.45f, 0.43f, 0.41f, 1f);

    // A band of the room's own floor, darkened, round its edge (gaps at the doorways), so a
    // patterned floor reads as laid with border tiles rather than just running into the walls.
    private void BuildFloorBorder(Room room, Material material)
    {
        List<Vector3> doors = DoorwayPositions(room);
        KitMesh mesh = new KitMesh();
        Vector3 origin = room.transform.position;
        float size = floorTextureSize > 0f ? floorTextureSize : 4f;
        float gap = doorWidth / 2f + 0.1f;

        foreach (RoomEdge edge in room.Outline)
        {
            Vector2 along = edge.To - edge.From;
            float length = along.magnitude;

            if (length < 0.01f)
                continue;

            along /= length;
            Vector2 inward = -edge.Outward.normalized;

            // The edge less any doorway on it.
            List<Vector2> spans = new List<Vector2> { new Vector2(0f, length) };

            foreach (Vector3 door in doors)
            {
                Vector2 d = new Vector2(door.x, door.z) - edge.From;
                float t = Vector2.Dot(d, along);

                if (Mathf.Abs(d.x * along.y - d.y * along.x) > 0.8f || t < -gap || t > length + gap)
                    continue;

                List<Vector2> cut = new List<Vector2>();

                foreach (Vector2 span in spans)
                {
                    if (t - gap > span.x) cut.Add(new Vector2(span.x, Mathf.Min(span.y, t - gap)));
                    if (t + gap < span.y) cut.Add(new Vector2(Mathf.Max(span.x, t + gap), span.y));
                }

                spans = cut;
            }

            foreach (Vector2 span in spans)
            {
                if (span.y - span.x < 0.05f)
                    continue;

                Vector2 a = edge.From + along * span.x, b = edge.From + along * span.y;
                Vector2 c = b + inward * FloorBorderWidth, d2 = a + inward * FloorBorderWidth;
                Vector3 A = new Vector3(a.x, 0.002f, a.y), B = new Vector3(b.x, 0.002f, b.y), C = new Vector3(c.x, 0.002f, c.y), D = new Vector3(d2.x, 0.002f, d2.y);
                mesh.Quad(A - origin, B - origin, C - origin, D - origin, a / size, b / size, c / size, d2 / size, Vector3.up);
            }
        }

        GameObject border = new GameObject("Floor Border");
        border.transform.SetParent(room.transform, false);
        Mesh built = mesh.Build("Floor Border");
        floorMeshes.Add(built);
        border.AddComponent<MeshFilter>().sharedMesh = built;
        MeshRenderer renderer = border.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", FloorBorderShade);
        renderer.SetPropertyBlock(block);
    }

    private static string ThemeFloorName(RoomTheme theme)
    {
        switch (theme)
        {
            case RoomTheme.Storage:
            case RoomTheme.StoreRoom:
            case RoomTheme.Checkpoint:
            case RoomTheme.BaggageHall: return "ConcreteBays";
            case RoomTheme.Workshop: return "DiamondPlate";
            case RoomTheme.Armoury:
            case RoomTheme.SecurityCheck: return "RubberMat";
            case RoomTheme.Infirmary:
            case RoomTheme.Ward:
            case RoomTheme.PatientWing: return "LinoGreen";
            case RoomTheme.WaitingRoom:
            case RoomTheme.Reception: return "LinoBlue";
            case RoomTheme.Barracks: return "WoodBasket";
            case RoomTheme.Office:
            case RoomTheme.SecurityOffice: return "CarpetTiles";
            case RoomTheme.Lab:
            case RoomTheme.Pharmacy: return "LabVinyl";
            case RoomTheme.ShopFloor:
            case RoomTheme.Entrance: return "Terrazzo";
            case RoomTheme.FoodCourt: return "Chequer";
            case RoomTheme.DutyFree:
            case RoomTheme.CheckIn: return "Marble";
            case RoomTheme.GateLounge: return "AirCarpet";
            default: return null;
        }
    }

    // A fixture in the middle of every cell of the theme's rooms, or null.
    private static string CentreFloorDecal(RoomTheme theme)
    {
        switch (theme)
        {
            case RoomTheme.Entrance: return "MallStar";
            case RoomTheme.Theatre: return "Drain";
            default: return null;
        }
    }

    // Paints the room's floor. Run after FurnishRoom (with its theme) and the boss arena ring.
    private void PaintFloor(Room room, RoomTheme theme)
    {
        UseThemeFloor(room, theme);
        PaintFloorLines(room, theme);

        if (floorDecalMaterial == null || floorDecalMaterial.mainTexture == null)
            return;

        List<Vector3> keepClear = DoorwayPositions(room);

        // Off pillars and cut-outs too (a decal would float over the void where one's taken out).
        keepClear.AddRange(PillarsOf(room));
        List<Vector3> hatch = HatchSpot(room);
        List<Vector3> painted = new List<Vector3>();
        bool boss = room.Type == RoomType.Boss;
        string[] markings = boss ? BossFloorDecals : ThemeFloorDecals(theme);
        float markingChance = boss ? 1f : room.Type == RoomType.Normal ? themeFloorDecalChance : 0.8f;
        float grimeChance = boss ? 0.8f : grimeFloorDecalChance;
        string[] grime = RoomKits.For(Zone).FloorGrime;

        string fixture = boss ? null : CentreFloorDecal(theme);

        foreach (Vector2Int cell in room.Cells)
        {
            Vector3 centre = CellToWorld(cell);

            if (fixture != null && IsClear(centre, keepClear, 2.6f) && IsClear(centre, hatch, HatchClearance + 1.5f))
            {
                PlaceFloorDecal(room.transform, fixture, centre);
                painted.Add(centre);
            }

            if (markings.Length > 0 && decalRng.NextDouble() < markingChance)
                TryPaintFloor(room, markings[decalRng.Next(markings.Length)], centre, keepClear, hatch, painted, boss);

            if (decalRng.NextDouble() < grimeChance)
                TryPaintFloor(room, grime[decalRng.Next(grime.Length)], centre, keepClear, hatch, painted, boss);
        }
    }

    private void TryPaintFloor(Room room, string name, Vector3 centre, List<Vector3> doorways, List<Vector3> hatch, List<Vector3> painted, bool boss)
    {
        // Clear of the painted arena ring in a boss room (85% of the room's short side across).
        float hatchClearance = boss ? Mathf.Min(roomSize.x, roomSize.y) * 0.43f + 1.2f : HatchClearance + 1.5f;
        Vector2 range = roomSize / 2f - Vector2.one * 1.6f;

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
        decal.transform.rotation = Quaternion.Euler(90f, decalRng.Next(4) * 90f, 0f);
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
