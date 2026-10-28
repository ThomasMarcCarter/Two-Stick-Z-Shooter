using System.Collections.Generic;
using UnityEngine;

// Room life (2026-10-28): small moving things added once a room is furnished and its floor
// painted. Some of the lit screens stutter (ScreenFlicker), a broken wall panel's wires spark in
// workshops, often in labs and now and then elsewhere (SparkingCable, on a north wall, the only
// walls the camera sees), some rooms have water dripping from the ceiling onto a puddle
// (CeilingDrip; most often in the Hospital), flies circle some of the rotten beds (FlySwarm) and
// in some a draught stirs the loose paper on the
// floor (PaperFlutter, added after the trash is down). Boss arenas and the hideout get none. Placement uses
// the decals' random sequence (decalRng), so the rest of the layout comes out the same.
public partial class FloorGenerator
{
    private const float ScreenFlickerChance = 0.45f;

    // Pieces with a lit screen or sign that can stutter.
    private static readonly HashSet<string> FlickerScreens = new HashSet<string>
    {
        "Desk", "CubiclePod", "NurseStation", "BedMonitor", "ServerRack", "MallKiosk", "InfoKiosk", "Checkout",
        "CheckInDesk", "VendingMachine", "ReceptionDesk", "XRayScanner", "DepartureBoard", "FoodCounter"
    };

    private void AddRoomLife(Room room, RoomTheme theme)
    {
        if (IsHideout || room.Type == RoomType.Boss || decalRng == null)
            return;

        foreach (Transform child in room.transform)
        {
            if (FlickerScreens.Contains(child.name) && decalRng.NextDouble() < ScreenFlickerChance)
                ScreenFlicker.Add(child.gameObject);

            if (decalRng.NextDouble() < FlyChance(child))
                FlySwarm.Create(child, child.position);
        }

        List<Vector3> doorways = DoorwayPositions(room);
        List<Vector3> lootPoints = new List<Vector3>();

        foreach (LootSpawnPoint point in room.GetComponentsInChildren<LootSpawnPoint>())
            lootPoints.Add(point.transform.position);

        int cables = theme == RoomTheme.Workshop ? 1 + decalRng.Next(2) : decalRng.NextDouble() < CableChance(theme) ? 1 : 0;

        for (int i = 0; i < cables; i++)
            PlaceSparkingCable(room, doorways, lootPoints);

        if (decalRng.NextDouble() < DripChance())
        {
            int drips = decalRng.NextDouble() < 0.3 ? 2 : 1;

            for (int i = 0; i < drips; i++)
                PlaceCeilingDrip(room, doorways, lootPoints);
        }
    }

    private const double DraughtChance = 0.35;

    // In some rooms a draught catches the loose sheets of paper and newspaper (not stacks or bags).
    private void AddDraught(Room room)
    {
        if (IsHideout || room.Type == RoomType.Boss || decalRng == null || decalRng.NextDouble() >= DraughtChance)
            return;

        float angle = (float)decalRng.NextDouble() * 360f;
        Vector3 draught = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

        foreach (Transform child in room.transform)
        {
            string name = child.name;
            bool paper = (name.StartsWith("Paper_") && !name.StartsWith("Paper_Bag")) || (name.StartsWith("News_") && !name.StartsWith("News_Stack"));

            if (paper)
                PaperFlutter.Add(child.gameObject, draught, point => OnFloor(room, point, 0.3f));
        }
    }

    // Flies over the rotten beds: bloodied, bile-soaked or stripped patient beds and ward beds, a
    // bloodied gurney or one with a body bag on it.
    private static double FlyChance(Transform piece)
    {
        if (!piece.TryGetComponent(out FurnitureSprite sprite))
            return 0;

        switch (piece.name)
        {
            case "PatientBed": return sprite.Column >= 1 && sprite.Column <= 3 ? 0.5 : 0;
            case "HospitalBed": return sprite.Column >= 3 ? 0.4 : 0;
            case "Gurney": return sprite.Column >= 1 ? 0.6 : 0;
            default: return 0;
        }
    }

    private static double CableChance(RoomTheme theme)
    {
        switch (theme)
        {
            case RoomTheme.Lab: return 0.6;
            case RoomTheme.Storage:
            case RoomTheme.StoreRoom:
            case RoomTheme.BaggageHall: return 0.25;
            default: return 0.1;
        }
    }

    private double DripChance()
    {
        switch (Zone)
        {
            case Zone.Hospital: return 0.45;
            case Zone.Airport: return 0.2;
            default: return 0.3;
        }
    }

    // A broken service panel on a cell's north wall, its wires sparking, clear of doorways, the
    // wall's loot and the furniture standing against it. Gives up quietly when there's no room.
    private void PlaceSparkingCable(Room room, List<Vector3> doorways, List<Vector3> lootPoints)
    {
        clutter.TryGetValue(room, out List<Vector3> taken);

        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector2Int cell = room.Cells[decalRng.Next(room.Cells.Count)];

            if (room.Contains(cell + Vector2Int.up))
                continue;

            Vector3 centre = CellToWorld(cell);
            float x = Mathf.Lerp(-roomSize.x / 2f + 1.5f, roomSize.x / 2f - 1.5f, (float)decalRng.NextDouble());
            Vector3 wall = new Vector3(centre.x + x, 0f, centre.z + roomSize.y / 2f - 0.08f);

            // Clear of the furniture against the wall too, which would hide it.
            if (!IsClear(wall, doorways, 2.5f) || !IsClear(wall, lootPoints, 1.2f) || !IsClear(wall, PillarsOf(room), 1.5f)
                || (taken != null && !IsClear(wall, taken, 1.6f)))
                continue;

            GameObject panel = new GameObject("Loose Cable");
            panel.transform.SetParent(room.transform, false);
            panel.transform.position = wall;

            if (FurnitureSprite.Apply(panel, "LooseCable", Vector3.back, new Vector3(0.8f, 0.9f, 0.15f), wall + Vector3.up * 0.9f) == null)
            {
                Destroy(panel);
                return;
            }

            SparkingCable.Create(panel.transform, wall + new Vector3(0f, 1.1f, -0.12f), Vector3.back);
            return;
        }
    }

    // A drip from the ceiling onto a puddle somewhere on the room's floor, off the doorways and
    // the furniture.
    private void PlaceCeilingDrip(Room room, List<Vector3> doorways, List<Vector3> lootPoints)
    {
        clutter.TryGetValue(room, out List<Vector3> taken);

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 centre = CellToWorld(room.Cells[decalRng.Next(room.Cells.Count)]);
            Vector3 spot = centre + new Vector3(Mathf.Lerp(-roomSize.x / 2f + 1.5f, roomSize.x / 2f - 1.5f, (float)decalRng.NextDouble()), 0f,
                Mathf.Lerp(-roomSize.y / 2f + 1.5f, roomSize.y / 2f - 1.5f, (float)decalRng.NextDouble()));

            if (!OnFloor(room, spot, 0.5f) || !IsClear(spot, doorways, 2f) || !IsClear(spot, lootPoints, 1f) || !IsClear(spot, PillarsOf(room), 1.2f)
                || (taken != null && !IsClear(spot, taken, 1f)))
            {
                continue;
            }

            if (floorDecalMaterial != null && floorDecalMaterial.mainTexture != null)
                PlaceFloorDecal(room.transform, "Puddle", spot);

            CeilingDrip.Create(room.transform, spot);
            return;
        }
    }
}
