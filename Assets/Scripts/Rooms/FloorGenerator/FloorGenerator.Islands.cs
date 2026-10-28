using System.Collections.Generic;
using UnityEngine;

// Room themes' islands and chairs: most themes stand pieces out in the room as well as against
// its walls (an office's cubicle pods and briefing table, a lab's equipment tables, a workshop's
// work tables, clothes racks on a shop floor, bunks in barracks...). Spots on a grid over the whole
// room (5 x 4.4 m, so big rooms and the floor where their cells join fill too) each get one at
// `islandChance` (small pieces in a group of 2-3), on the floor and clear of doorways, pillars,
// cut-outs, loot points and each door's route to the middle of its cell, so there's always a way
// through to fight in. Offices also get desk chairs: one at most wall desks, one at each cubicle
// desk, round the briefing table, and now and then one knocked over in the open. Also the
// Hospital's patient wing bed bays and the Military Base storage rooms' parked cars.
public partial class FloorGenerator
{
    private const float TippedChairChance = 0.15f;

    private static readonly ThemePiece CarPiece = new ThemePiece("Car", 1.9f, 1.6f, 3.6f);
    private static readonly ThemePiece PatientBedPiece = new ThemePiece("PatientBed", 0.9f, 1f, 2f);
    private static readonly ThemePiece BedCurtainPiece = new ThemePiece("BedCurtain", 2.4f, 2.1f, 2.3f);
    private static readonly ThemePiece BedMonitorPiece = new ThemePiece("BedMonitor", 0.5f, 1.4f, 0.45f);
    private const float BayWidth = 2.4f;
    private const int KnockedBed = 4;

    // Island spots: a grid this far apart over the whole room (two rows, three across in one cell),
    // so a big room's middle and the floor where its cells join fill up too.
    private const float IslandStepX = 5f;
    private const float IslandStepZ = 4.4f;

    private static readonly Vector2[] Quarters = { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) };

    private void PlaceIslands(Room room, RoomTheme theme, List<Vector3> doorways, List<Vector3> lootPoints, List<Vector3> taken)
    {
        ThemePiece[] islands = theme == RoomTheme.None ? UnthemedIslands(Zone) : WithBaseLabBeds(IslandsFor(theme), theme);

        if (islands.Length == 0)
            return;

        List<(Vector3 door, Vector3 centre)> routes = DoorRoutes(room, doorways);
        List<Vector3> spots = new List<Vector3>();

        // A patient wing's walls are lined with beds, so its islands go down the middle instead.
        // The spawn room keeps its middle clear, where the player arrives.
        foreach (Vector3 middle in theme == RoomTheme.PatientWing ? MiddleRowSpots(room) : IslandGrid(room))
        {
            if (room.Type != RoomType.Start || !NearCellCentre(room, middle, 3.5f))
                spots.Add(middle);
        }

        int placed = 0;

        foreach (Vector3 middle in spots)
        {
            if (rng.NextDouble() <= islandChance)
                placed += PlaceIsland(room, islands[rng.Next(islands.Length)], middle, doorways, routes, lootPoints, taken);
        }

        // Never a bare middle: when the dice left it empty, the first spot anything fits in gets it.
        for (int i = 0; placed == 0 && i < spots.Count * islands.Length; i++)
            placed += PlaceIsland(room, islands[i % islands.Length], spots[i / islands.Length], doorways, routes, lootPoints, taken);

        if (theme != RoomTheme.Office)
            return;

        foreach (Vector2Int cell in room.Cells)
        {
            if (rng.NextDouble() >= 0.5)
                continue;

            Vector3 spot = CellToWorld(cell) + new Vector3(RandomRange(-roomSize.x / 2f + 1.5f, roomSize.x / 2f - 1.5f), 0f,
                RandomRange(-roomSize.y / 2f + 1.5f, roomSize.y / 2f - 1.5f));

            if (IsClear(spot, lootPoints, 1f) && !NearRoutes(spot, routes, 1f))
                PlaceChair(room, spot, Vector3.back, doorways, taken, true, true);
        }
    }

    // One island (or a small group of a small piece) at a spot, turned to the camera or away from
    // it (all the islands are drawn both ways). Returns how many pieces went in.
    private int PlaceIsland(Room room, ThemePiece piece, Vector3 middle, List<Vector3> doorways,
        List<(Vector3 door, Vector3 centre)> routes, List<Vector3> lootPoints, List<Vector3> taken)
    {
        if (!HasPropModel(piece.Model))
            return 0;

        // Small pieces (crates, trolleys, drip stands) come in a little group, so a spot isn't one lone box.
        int group = piece.Size.x < 1.2f ? rng.Next(2, 4) : 1;
        int placed = 0;

        for (int k = 0; k < group; k++)
        {
            Vector3 spot = middle + Vector3.right * (k - (group - 1) / 2f) * (piece.Size.x + 0.6f);
            List<Vector3> footprint = Footprint(spot, Vector3.right, Vector3.forward, piece.Size);

            if (!IslandClear(room, footprint, doorways, routes, lootPoints, taken))
                continue;

            Vector3 front = rng.Next(2) == 0 ? Vector3.back : Vector3.forward;

            if (SpawnFurniture(room, piece, spot, front) == null)
                continue;

            taken.AddRange(footprint);
            ChairsRound(room, piece, spot, front, doorways, taken);
            placed++;
        }

        return placed;
    }

    // Grid points over the room's floor, centred on it, kept 3 m off its outer edges across and
    // 2.8 m deep (wall furniture and the walk round it stay clear).
    private List<Vector3> IslandGrid(Room room)
    {
        List<Vector3> spots = new List<Vector3>();

        if (room.Blocks.Count == 0)
            return spots;

        Rect bounds = room.Blocks[0];

        foreach (Rect block in room.Blocks)
            bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, block.xMin), Mathf.Min(bounds.yMin, block.yMin),
                Mathf.Max(bounds.xMax, block.xMax), Mathf.Max(bounds.yMax, block.yMax));

        int columns = Mathf.Max(1, Mathf.FloorToInt((bounds.width - 6f) / IslandStepX) + 1);
        int rows = Mathf.Max(1, Mathf.FloorToInt((bounds.height - 5.6f) / IslandStepZ) + 1);

        for (int i = 0; i < columns; i++)
        {
            for (int j = 0; j < rows; j++)
                spots.Add(new Vector3(bounds.center.x + (i - (columns - 1) / 2f) * IslandStepX, 0f,
                    bounds.center.y + (j - (rows - 1) / 2f) * IslandStepZ));
        }

        return spots;
    }

    // One row down the middle of each cell, a grid step apart.
    private List<Vector3> MiddleRowSpots(Room room)
    {
        List<Vector3> spots = new List<Vector3>();

        foreach (Vector2Int cell in room.Cells)
        {
            for (int i = -1; i <= 1; i++)
                spots.Add(CellToWorld(cell) + Vector3.right * i * IslandStepX);
        }

        return spots;
    }

    // Each doorway with the middle of its own cell: the routes in that are kept clear.
    private List<(Vector3 door, Vector3 centre)> DoorRoutes(Room room, List<Vector3> doorways)
    {
        List<(Vector3, Vector3)> routes = new List<(Vector3, Vector3)>();

        foreach (Vector3 door in doorways)
        {
            Vector3 nearest = door;
            float best = float.MaxValue;

            foreach (Vector2Int cell in room.Cells)
            {
                Vector3 centre = CellToWorld(cell);
                float distance = Vector3.Distance(Flat(centre), Flat(door));

                if (distance < best)
                {
                    best = distance;
                    nearest = centre;
                }
            }

            routes.Add((door, nearest));
        }

        return routes;
    }

    private bool NearCellCentre(Room room, Vector3 point, float distance)
    {
        foreach (Vector2Int cell in room.Cells)
        {
            if (Vector3.Distance(Flat(point), Flat(CellToWorld(cell))) < distance)
                return true;
        }

        return false;
    }

    private static bool NearRoutes(Vector3 point, List<(Vector3 door, Vector3 centre)> routes, float distance)
    {
        foreach ((Vector3 door, Vector3 centre) in routes)
        {
            if (NearSegment(point, door, centre, distance))
                return true;
        }

        return false;
    }

    // Whether the point is on the floor with `margin` to spare all round (L and T shapes, cut-outs).
    private static bool OnFloor(Room room, Vector3 point, float margin)
    {
        foreach (Vector2 corner in new[] { new Vector2(-margin, -margin), new Vector2(margin, -margin), new Vector2(-margin, margin), new Vector2(margin, margin) })
        {
            Vector2 p = new Vector2(point.x, point.z) + corner;
            bool inside = false;

            foreach (Rect block in room.Blocks)
                inside |= block.Contains(p);

            if (!inside)
                return false;
        }

        return true;
    }

    // A patient wing: bed bays 2.4 m wide down the cell's north and south walls, each a ward bed
    // (messy, bloodied, bile-soaked, stripped, or knocked over with its sheet across the floor),
    // mostly with a curtain round it, a drip stand at one side and a monitor at the other. Bays
    // near doorways, pillars and loot are left empty. The beds are solid; curtains, drips and
    // monitors are only drawn, so they don't snag anyone.
    private void LineBedBays(Room room, Vector2Int cell, Vector3 centre, List<Vector3> doorways, List<Vector3> lootPoints,
        List<Vector3> taken)
    {
        int bays = Mathf.FloorToInt((roomSize.x - 1f) / BayWidth);

        foreach (Vector2Int wall in new[] { Vector2Int.up, Vector2Int.down })
        {
            if (room.Contains(cell + wall))
                continue;

            Vector3 outward = new Vector3(0f, 0f, wall.y);
            float back = roomSize.y / 2f - 0.15f;

            for (int i = 0; i < bays; i++)
            {
                if (rng.NextDouble() > 0.9)
                    continue;

                float x = (i + 0.5f) * BayWidth - bays * BayWidth / 2f;
                int state = PickBedState();
                Vector3 bed = centre + Vector3.right * x + outward * (back - PatientBedPiece.Size.z / 2f - (state == KnockedBed ? 0.3f : 0f));
                List<Vector3> footprint = Footprint(bed, Vector3.right, outward, PatientBedPiece.Size);
                bool clear = true;

                foreach (Vector3 point in footprint)
                    clear &= IsClear(point, doorways, 3f) && IsClear(point, PillarsOf(room), 1.6f) && IsClear(point, lootPoints, 1.4f) && IsClear(point, taken, 1f);

                if (!clear)
                    continue;

                SetColumn(SpawnFurniture(room, PatientBedPiece, bed, -outward), state);
                taken.AddRange(footprint);

                if (rng.NextDouble() < 0.8)
                {
                    Vector3 curtain = centre + Vector3.right * x + outward * (back - BedCurtainPiece.Size.z / 2f);
                    SetColumn(SpawnFurniture(room, BedCurtainPiece, curtain, -outward, false), rng.Next(3));
                }

                float side = rng.Next(2) == 0 ? -1f : 1f;
                Vector3 head = centre + outward * (back - 0.45f);

                if (rng.NextDouble() < 0.8)
                    SpawnFurniture(room, IVStandPiece, head + Vector3.right * (x + side * 0.78f), -outward, false);

                if (rng.NextDouble() < (state == KnockedBed ? 0.3 : 0.65))
                    SpawnFurniture(room, BedMonitorPiece, head + Vector3.right * (x - side * 0.78f), -outward, false);
            }
        }
    }

    // Columns of the PatientBed sheet: 0 messy, 1 bloodied, 2 bile, 3 stripped, 4 knocked over.
    private int PickBedState()
    {
        double roll = rng.NextDouble();
        return roll < 0.3 ? 0 : roll < 0.55 ? 1 : roll < 0.7 ? 2 : roll < 0.85 ? 3 : KnockedBed;
    }

    private static void SetColumn(GameObject piece, int column)
    {
        if (piece != null && piece.TryGetComponent(out FurnitureSprite sprite))
            sprite.Column = Mathf.Clamp(column, 0, sprite.Frames - 1);
    }

    // The Military Base storage rooms' parking bays (FloorGenerator.FloorLines.cs `Bays`: 3 m wide
    // along the north and south walls) get a parked car now and then, nose in or reversed in: a
    // jeep, a hatchback, a wrecked saloon or a pickup (the sheet's columns).
    private void PlaceCars(Room room, RoomTheme theme, Vector3 centre, List<Vector3> doorways, List<Vector3> lootPoints,
        List<Vector3> taken)
    {
        if (theme != RoomTheme.Storage || !HasPropModel(CarPiece.Model))
            return;

        int bays = Mathf.Max(1, (Mathf.RoundToInt(roomSize.x) - 4) / 3);
        float firstBay = Mathf.Round(centre.x) - bays * 3 / 2 + 1.5f;

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < bays; i++)
            {
                if (rng.NextDouble() > carChance)
                    continue;

                Vector3 spot = new Vector3(firstBay + i * 3f, 0f, centre.z + side * (roomSize.y / 2f - 0.15f - CarPiece.Size.z / 2f));
                List<Vector3> footprint = Footprint(spot, Vector3.right, Vector3.forward, CarPiece.Size);

                if (!IslandClear(room, footprint, doorways, DoorRoutes(room, doorways), lootPoints, taken, 0.8f))
                    continue;

                Vector3 front = rng.Next(2) == 0 ? Vector3.back : Vector3.forward;
                GameObject car = SpawnFurniture(room, CarPiece, spot, front);

                if (car == null)
                    continue;

                SetColumn(car, rng.Next(4));
                taken.AddRange(footprint);
            }
        }
    }

    private bool IslandClear(Room room, List<Vector3> footprint, List<Vector3> doorways, List<(Vector3 door, Vector3 centre)> routes,
        List<Vector3> lootPoints, List<Vector3> taken, float routeClearance = 1.2f)
    {
        foreach (Vector3 point in footprint)
        {
            if (!OnFloor(room, point, 1.2f) || !IsClear(point, doorways, 3f) || !IsClear(point, PillarsOf(room), 1.6f)
                || !IsClear(point, lootPoints, 1.4f) || !IsClear(point, taken, 1.5f) || NearRoutes(point, routes, routeClearance))
            {
                return false;
            }
        }

        return true;
    }

    // Chairs at a cubicle pod's four desks (inside its footprint, so only drawn) or round a
    // briefing table (solid, where there's room).
    private void ChairsRound(Room room, ThemePiece piece, Vector3 spot, Vector3 front, List<Vector3> doorways, List<Vector3> taken)
    {
        Quaternion turn = Quaternion.LookRotation(front);

        if (piece.Model == CubiclePodPiece.Model)
        {
            foreach (Vector2 quarter in Quarters)
            {
                if (rng.NextDouble() < 0.85)
                    PlaceChair(room, spot + turn * new Vector3(quarter.x * 0.74f, 0f, quarter.y * 1.15f), turn * new Vector3(0f, 0f, -quarter.y),
                        doorways, taken, false);
            }
        }
        else if (piece.Model == MeetingTablePiece.Model)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int seat = -1; seat <= 1; seat++)
                {
                    if (rng.NextDouble() < 0.7)
                        PlaceChair(room, spot + turn * new Vector3(seat * 0.75f, 0f, side * 0.95f), turn * new Vector3(0f, 0f, -side), doorways, taken, true);
                }
            }
        }
    }

    // A desk chair facing `front`, sometimes knocked over (any way round). Solid ones keep off
    // doorways, pillars and other pieces, and go on the clutter list.
    private void PlaceChair(Room room, Vector3 spot, Vector3 front, List<Vector3> doorways, List<Vector3> taken, bool solid,
        bool tipped = false)
    {
        tipped |= rng.NextDouble() < TippedChairChance;

        if (tipped)
            front = Quaternion.Euler(0f, rng.Next(4) * 90f, 0f) * front;

        if (solid && (!IsClear(spot, doorways, 2f) || !IsClear(spot, PillarsOf(room), 1.2f) || !IsClear(spot, taken, 0.6f)))
            return;

        if (SpawnFurniture(room, tipped ? DeskChairTippedPiece : DeskChairPiece, spot, front, solid) != null && solid)
            taken.Add(spot);
    }
}
