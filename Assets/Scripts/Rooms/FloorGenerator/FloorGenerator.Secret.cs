using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's side of the secret room (RoomType.Secret): now and then (Drops.secretChance,
// once unlocked) a 1x1 room is added after the layout has grown, north of a combat or treasure
// room cell where it touches nothing else. It's joined by an ordinary corridor, but the doorway is
// bare on both sides (no frame, beacons, stripe or threshold) and the parent room's side is
// plugged with a piece of wall wearing a crack decal (CrackedWall). A blast near the crack opens
// it. Inside: a supply crate of treasure loot without the gun, and a pile of bonus scrap.
// North walls only, since they're the only wall faces the tilted camera shows (the crack must be seen).
public partial class FloorGenerator
{
    private const int SecretScrap = 10;

    // Both sides of each secret doorway: (cell, direction out of it).
    private readonly HashSet<(Vector2Int, Vector2Int)> secretDoors = new HashSet<(Vector2Int, Vector2Int)>();

    // Adds the floor's secret room to the plan, if its chance comes up and there's a spot for it.
    private void AddSecretRoom(List<PlannedRoom> plan)
    {
        if (!Unlocks.IsUnlocked(RoomType.Secret) || rng.NextDouble() >= Drops.secretChance)
            return;

        Dictionary<Vector2Int, int> owner = new Dictionary<Vector2Int, int>();

        for (int i = 0; i < plan.Count; i++)
        {
            foreach (Vector2Int cell in plan[i].Cells)
                owner[cell] = i;
        }

        List<(int parent, Vector2Int cell)> spots = new List<(int, Vector2Int)>();

        for (int i = 0; i < plan.Count; i++)
        {
            if (plan[i].Type != RoomType.Normal && plan[i].Type != RoomType.Treasure)
                continue;

            foreach (Vector2Int cell in plan[i].Cells)
            {
                Vector2Int door = cell + Vector2Int.up;

                if (Fits(new List<Vector2Int> { door }, cell, door, owner))
                    spots.Add((i, cell));
            }
        }

        if (spots.Count == 0)
            return;

        (int parent, Vector2Int parentCell) = spots[rng.Next(spots.Count)];
        Vector2Int secretCell = parentCell + Vector2Int.up;

        plan.Add(new PlannedRoom
        {
            Shape = RoomShape.Single,
            Cells = new List<Vector2Int> { secretCell },
            Type = RoomType.Secret,
            Parent = parent,
            Depth = plan[parent].Depth + 1,
            ParentCell = parentCell,
            DoorDirection = Vector2Int.up
        });

        plan[parent].Children++;
        secretDoors.Add((parentCell, Vector2Int.up));
        secretDoors.Add((secretCell, Vector2Int.down));
    }

    // The plug in the parent room's north wall: a doorway's width of wall with a crack painted
    // across it, under a CrackedWall that hides the room and opens on a blast.
    private void BuildCrackedWalls(List<PlannedRoom> plan)
    {
        foreach (PlannedRoom planned in plan)
        {
            if (planned.Type != RoomType.Secret)
                continue;

            Room parent = roomsByCell[planned.ParentCell];
            Room secret = roomsByCell[planned.Cells[0]];
            Rect block = BlockRect(planned.ParentCell, 0, 0);
            float across = block.yMax + wallThickness / 2f;
            float middle = block.center.x;

            GameObject root = new GameObject("Cracked Wall");
            root.transform.SetParent(parent.transform, false);
            root.transform.position = new Vector3(middle, 0f, across);

            CreateWallSpan(root.transform, true, across, middle - doorWidth / 2f, middle + doorWidth / 2f, Vector3.back, KitSpan.Plain);

            if (wallDecalMaterial != null && wallDecalMaterial.mainTexture != null)
                PlaceDecal(root.transform, "Crack", middle, block.yMax);

            CrackedWall.Attach(root, secret, new Vector3(doorWidth, wallHeight, wallThickness));
        }
    }

    private void FillSecret(Room room)
    {
        Vector3 middle = room.transform.position;
        SupplyCrate crate = SupplyCrate.Create("SECRET ROOM // SUPPLY CRATE", middle, Vector3.back, room.transform);
        AddTreasureLoot(crate, false);

        Vector3 scrap = middle + new Vector3(2f, 0f, -1f);
        SupplyShelfItem.SpawnScrap(SecretScrap, scrap, 0f, scrap, room.transform);
    }
}
