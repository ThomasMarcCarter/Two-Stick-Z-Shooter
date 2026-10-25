using System.Collections.Generic;
using UnityEngine;

// Wall decals: signs, posters, notices, grime, blood and graffiti painted on the rooms' north walls
// (the only wall faces the tilted camera sees). Each cell's north wall is split into 3 m slots;
// most cells get one of their theme's signs (a stores stencil in a storage room, a red cross in
// the infirmary, a biohazard sign in a lab, a gate sign in a gate lounge, a neon FASHION sign on a shop floor...), the other
// slots now and then get grime, blood or graffiti, and boss arenas are smeared with blood. A
// doorway in a north wall sometimes gets a lit EXIT sign beside it, arrow pointing at the door.
// Slots behind doorways, pillars, wall-mounted loot and tall furniture are left bare.
// Each decal is a quad flat on the wall face with the walls' own pixel grid (16 px per metre
// along, 32 rows up the 2 m face; Tools/Sprites/walldecals.py), lit like the wall, with lit
// pixels (exit signs, neon, screens) in the material's emission map. Uses its own random
// sequence, so the rest of the floor comes out the same for a seed with or without decals.
public partial class FloorGenerator
{
    [Header("Wall Decals")]
    [Tooltip("URP Lit, alpha clipped, WallDecals.png as the base map and WallDecals_Glow.png as emission. Empty = no decals.")]
    [SerializeField] private Material wallDecalMaterial;
    [Tooltip("Chance a cell's north wall gets one of its theme's signs.")]
    [Range(0f, 1f)]
    [SerializeField] private float themeDecalChance = 0.8f;
    [Tooltip("Chance each other free slot gets grime, blood or graffiti.")]
    [Range(0f, 1f)]
    [SerializeField] private float grimeDecalChance = 0.25f;
    [Tooltip("Chance a doorway in a north wall gets a lit EXIT sign beside it.")]
    [Range(0f, 1f)]
    [SerializeField] private float exitSignChance = 0.6f;

    private const float DecalWidth = 3f;            // a cell of the sheet: 48 px at 16 px/m
    private const int DecalColumns = 8;
    private const int DecalCellRows = 32;           // texture rows per cell (the 2 m face)
    private const float DecalGap = 0.01f;           // off the wall face, into the room

    // The sheet's cells in order (Tools/Sprites/walldecals.py's DECALS must match).
    private static readonly string[] DecalNames =
    {
        "Crack", "Crack2", "Damp", "Handprint", "BloodSmear", "BloodSplat", "BulletHoles", "Tally",
        "GraffitiRun", "GraffitiHelp", "GraffitiDead", "GraffitiSafe", "Vent", "FuseBox", "Extinguisher", "Clock",
        "ExitLeft", "ExitRight",
        "Stores", "NoSmoking", "Flammable", "RedCross", "FirstAid", "NoticeBoard", "PinUp", "LightsOut",
        "PegBoard", "SafetyFirst", "Armoury", "TargetPoster", "NoEntry", "Halt", "MapBoard", "Bay",
        "Sale", "NeonFashion", "PosterModel", "NeonBurgers", "PosterDrink", "StaffOnly", "Security", "Cctv",
        "MallDirectory", "Welcome",
        "Ward", "HandWash", "Whiteboard", "Theatre", "InUse", "XRay", "Waiting", "Quiet", "Ticket",
        "PharmacyCross", "Pharmacy", "Reception", "Directions",
        "Gate", "AdPoster", "Baggage", "Belt", "SecurityAir", "NoLiquids", "DutyFree", "Perfume", "CheckIn",
        "FlightClock", "Departures",
        "Operations", "Biohazard", "LabSign"
    };

    private static readonly string[] GrimeDecals =
    {
        "Crack", "Crack", "Crack2", "Crack2", "Damp", "Damp", "Damp", "Handprint", "BloodSmear", "BloodSplat",
        "BulletHoles", "BulletHoles", "Tally", "GraffitiRun", "GraffitiHelp", "GraffitiDead", "GraffitiSafe",
        "Extinguisher", "Extinguisher", "Vent", "Vent", "FuseBox"
    };

    private static readonly string[] BossDecals =
    {
        "BloodSplat", "BloodSplat", "BloodSmear", "BloodSmear", "Handprint", "BulletHoles", "Crack", "GraffitiRun"
    };

    private System.Random decalRng;
    private MaterialPropertyBlock decalBlock;

    // What a theme hangs on its walls. Unthemed rooms get the zone's everyday signs.
    private string[] ThemeDecals(RoomTheme theme)
    {
        switch (theme)
        {
            case RoomTheme.Storage: return new[] { "Stores", "NoSmoking", "Flammable", "Bay" };
            case RoomTheme.Infirmary: return new[] { "RedCross", "RedCross", "FirstAid", "NoticeBoard" };
            case RoomTheme.Barracks: return new[] { "NoticeBoard", "PinUp", "LightsOut", "Clock" };
            case RoomTheme.Workshop: return new[] { "PegBoard", "PegBoard", "SafetyFirst", "Flammable" };
            case RoomTheme.Armoury: return new[] { "Armoury", "Armoury", "TargetPoster", "NoEntry" };
            case RoomTheme.Checkpoint: return new[] { "Halt", "MapBoard", "NoticeBoard" };
            case RoomTheme.ShopFloor: return new[] { "Sale", "NeonFashion", "NeonFashion", "PosterModel" };
            case RoomTheme.FoodCourt: return new[] { "NeonBurgers", "NeonBurgers", "PosterDrink", "Clock" };
            case RoomTheme.StoreRoom: return new[] { "StaffOnly", "StaffOnly", "Bay", "NoSmoking" };
            case RoomTheme.SecurityOffice: return new[] { "Security", "Cctv", "Cctv", "NoEntry" };
            case RoomTheme.Entrance: return new[] { "Welcome", "MallDirectory", "Sale" };
            case RoomTheme.Ward: return new[] { "Ward", "Ward", "HandWash", "Whiteboard" };
            case RoomTheme.Theatre: return new[] { "Theatre", "InUse", "XRay", "HandWash" };
            case RoomTheme.WaitingRoom: return new[] { "Waiting", "Quiet", "Ticket", "Clock" };
            case RoomTheme.Pharmacy: return new[] { "PharmacyCross", "Pharmacy", "Pharmacy" };
            case RoomTheme.Reception: return new[] { "Reception", "Reception", "Directions", "Quiet" };
            case RoomTheme.GateLounge: return new[] { "Gate", "Gate", "AdPoster", "Departures", "FlightClock" };
            case RoomTheme.BaggageHall: return new[] { "Baggage", "Baggage", "Belt", "AdPoster" };
            case RoomTheme.SecurityCheck: return new[] { "SecurityAir", "SecurityAir", "NoLiquids", "NoEntry" };
            case RoomTheme.DutyFree: return new[] { "DutyFree", "DutyFree", "Perfume", "AdPoster" };
            case RoomTheme.CheckIn: return new[] { "CheckIn", "CheckIn", "Departures", "FlightClock" };
            case RoomTheme.Office: return new[] { "Operations", "Operations", "NoticeBoard", "MapBoard", "Clock" };
            case RoomTheme.Lab: return new[] { "Biohazard", "Biohazard", "LabSign", "Whiteboard", "NoEntry" };
        }

        switch (Zone)
        {
            case Zone.ShoppingMall: return new[] { "PosterDrink", "Sale", "PosterModel", "Clock" };
            case Zone.Hospital: return new[] { "HandWash", "Directions", "Quiet", "Clock" };
            case Zone.Airport: return new[] { "AdPoster", "Departures", "FlightClock" };
            default: return new[] { "NoSmoking", "NoticeBoard", "Clock", "FuseBox" };
        }
    }

    // Decorates the north walls of every cell in the room. Run after FurnishRoom, so the slots
    // its pieces hide are left bare.
    private void DecorateWalls(Room room, RoomTheme theme)
    {
        if (wallDecalMaterial == null || wallDecalMaterial.mainTexture == null)
            return;

        List<Vector3> doorways = DoorwayPositions(room);
        List<Vector3> pillarSpots = PillarsOf(room);
        List<Vector3> lootPoints = new List<Vector3>();

        foreach (LootSpawnPoint point in room.GetComponentsInChildren<LootSpawnPoint>())
            lootPoints.Add(point.transform.position);

        List<Vector3> furniture = ClutterOf(room);
        string[] signs = room.Type == RoomType.Boss ? BossDecals : ThemeDecals(theme);
        float signChance = room.Type == RoomType.Boss ? 0.9f : room.Type == RoomType.Normal ? themeDecalChance : 1f;
        float grimeChance = room.Type == RoomType.Boss ? 0.6f : grimeDecalChance;
        HashSet<string> used = new HashSet<string>();

        foreach (Vector2Int cell in room.Cells)
        {
            if (room.Contains(cell + Vector2Int.up))
                continue;

            Vector3 centre = CellToWorld(cell);
            float face = centre.z + roomSize.y / 2f;
            float? door = null;

            foreach (Vector3 doorway in doorways)
            {
                if (Mathf.Abs(doorway.z - face) < 0.5f && Mathf.Abs(doorway.x - centre.x) < roomSize.x / 2f)
                    door = doorway.x;
            }

            // Slots along the wall, 3 m apart; the middle one is the doorway's when there is one.
            List<float> slots = new List<float>();

            for (int k = -2; k <= 2; k++)
            {
                float x = centre.x + k * DecalWidth;

                if (door.HasValue && Mathf.Abs(x - door.Value) < DecalWidth * 0.9f)
                    continue;

                if (SlotBlocked(x, face, pillarSpots, 2f) || SlotBlocked(x, face, lootPoints, 1.6f) || SlotBlocked(x, face, furniture, 1.5f))
                    continue;

                slots.Add(x);
            }

            Shuffle(slots);

            // An EXIT sign beside the door, its arrow pointing at it, takes that side's slot.
            if (door.HasValue && room.Type != RoomType.Boss && decalRng.NextDouble() < exitSignChance)
            {
                bool right = decalRng.NextDouble() < 0.5;
                float x = door.Value + (right ? 2.2f : -2.2f);

                if (!SlotBlocked(x, face, pillarSpots, 1.6f) && !SlotBlocked(x, face, furniture, 1.2f))
                {
                    PlaceDecal(room.transform, right ? "ExitLeft" : "ExitRight", x, face);
                    slots.RemoveAll(s => Mathf.Abs(s - x) < DecalWidth * 0.9f);
                }
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (i == 0 && decalRng.NextDouble() < signChance)
                    PlaceDecal(room.transform, Pick(signs, used), slots[i], face);
                else if (decalRng.NextDouble() < grimeChance)
                    PlaceDecal(room.transform, Pick(GrimeDecals, used), slots[i], face);
            }
        }
    }

    // A decal from the list, trying not to repeat one already on the room's walls.
    private string Pick(string[] names, HashSet<string> used)
    {
        string name = names[decalRng.Next(names.Length)];

        for (int tries = 0; tries < 4 && used.Contains(name); tries++)
            name = names[decalRng.Next(names.Length)];

        used.Add(name);
        return name;
    }

    // Whether something stands against the wall in front of the slot at x.
    private static bool SlotBlocked(float x, float face, List<Vector3> points, float reach)
    {
        foreach (Vector3 point in points)
        {
            if (face - point.z < 1.8f && Mathf.Abs(point.x - x) < reach)
                return true;
        }

        return false;
    }

    private void Shuffle(List<float> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = decalRng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // A decal from the sheet on the north wall face at x, the cell's pixels on the wall's pixel grid.
    private void PlaceDecal(Transform parent, string name, float x, float face)
    {
        int index = System.Array.IndexOf(DecalNames, name);
        Texture texture = wallDecalMaterial.mainTexture;
        int rows = Mathf.Max(1, texture.height / DecalCellRows);

        if (index < 0 || index >= DecalColumns * rows)
            return;

        // Left edge on a whole pixel of the wall texture (16 px per metre, world aligned).
        x = Mathf.Round((x - DecalWidth / 2f) * 16f) / 16f + DecalWidth / 2f;

        GameObject decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
        decal.name = "Wall Decal " + name;
        Destroy(decal.GetComponent<Collider>());
        decal.transform.SetParent(parent, true);
        decal.transform.position = new Vector3(x, wallHeight / 2f, face - DecalGap);
        decal.transform.rotation = Quaternion.identity;
        decal.transform.localScale = new Vector3(DecalWidth, wallHeight, 1f);

        Renderer renderer = decal.GetComponent<Renderer>();
        renderer.sharedMaterial = wallDecalMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        int column = index % DecalColumns;
        int row = index / DecalColumns;
        decalBlock ??= new MaterialPropertyBlock();
        decalBlock.SetVector("_BaseMap_ST", new Vector4(1f / DecalColumns, 1f / rows, (float)column / DecalColumns, 1f - (row + 1f) / rows));
        renderer.SetPropertyBlock(decalBlock);
    }
}
