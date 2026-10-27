using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's trash: flat litter strewn across the floor, small things scattered about
// (more of them near the walls, round ones sometimes tipped over) and big pieces against the
// walls as cover. Kept off doorways, pillars, wall loot points and the hatch.
public partial class FloorGenerator
{
    // Dresses a room with rubbish from the trash pack: flat litter strewn across the floor, small
    // things scattered about (more of them near the walls), and a big piece or two against the
    // walls. Kept off the doorways, pillars and wall loot points; the big pieces also stay off
    // the routes from the doors and the middle of each cell, and count as clutter, so enemies and
    // crates don't spawn on them. The spawn room gets a little litter and nothing else.
    private void SpawnTrash(Room room)
    {
        if (!clutter.TryGetValue(room, out List<Vector3> taken))
        {
            taken = new List<Vector3>();
            clutter[room] = taken;
        }

        RoomSpots spots = SpotsIn(room, taken);
        bool spawnRoom = room.Type == RoomType.Start;

        foreach (Vector2Int cell in room.Cells)
        {
            Vector3 centre = CellToWorld(cell);
            ScatterLitter(room, centre, spots, spawnRoom ? 3 : 1);

            if (spawnRoom)
                continue;

            PlaceLargeTrash(room, cell, centre, spots);
            ScatterSmallTrash(room, cell, centre, spots);
        }
    }

    // Flat litter, anywhere but the doorways (a `fraction` of the usual amount).
    private void ScatterLitter(Room room, Vector3 centre, RoomSpots spots, int fraction)
    {
        if (floorLitter == null || floorLitter.Length == 0)
            return;

        int litter = rng.Next(litterPerCell.x, litterPerCell.y + 1) / fraction;

        for (int i = 0; i < litter; i++)
        {
            Vector3 spot = RandomFloorPoint(centre, 0.6f);

            if (!IsClear(spot, spots.Doorways, 1.4f) || !IsClear(spot, PillarsOf(room), 0.9f) || !IsClear(spot, spots.Hatch, HatchClearance))
                continue;

            GameObject flat = PlaceTrash(room.transform, floorLitter[rng.Next(floorLitter.Length)], spot,
                Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));

            // A hair apart in height, so overlapping litter doesn't flicker against itself.
            if (flat != null)
                flat.transform.position += Vector3.up * RandomRange(0.002f, 0.012f);
        }
    }

    // Big pieces against the cell's outside walls, as cover. They count as clutter.
    private void PlaceLargeTrash(Room room, Vector2Int cell, Vector3 centre, RoomSpots spots)
    {
        if (largeTrash == null || largeTrash.Length == 0)
            return;

        int large = rng.Next(largeTrashPerCell.x, largeTrashPerCell.y + 1);

        for (int i = 0; i < large; i++)
        {
            Vector2Int wall = Directions[rng.Next(Directions.Length)];

            if (room.Contains(cell + wall))
                continue;

            Vector3 outward = new Vector3(wall.x, 0f, wall.y);
            Vector3 along = new Vector3(wall.y, 0f, wall.x);
            float halfExtent = wall.x != 0 ? roomSize.x / 2f : roomSize.y / 2f;
            float halfLength = wall.x != 0 ? roomSize.y / 2f : roomSize.x / 2f;
            Vector3 spot = centre + outward * (halfExtent - 1.1f) + along * RandomRange(-halfLength + 1.2f, halfLength - 1.2f);

            if (!IsClear(spot, spots.Doorways, 3f) || !IsClear(spot, PillarsOf(room), 2f) || !IsClear(spot, spots.LootPoints, 1.6f)
                || !IsClear(spot, spots.Placed, 2.2f) || Vector3.Distance(Flat(spot), Flat(centre)) < 3f
                || !IsClear(spot, spots.Hatch, HatchClearance + 0.6f))
            {
                continue;
            }

            if (PlaceTrash(room.transform, largeTrash[rng.Next(largeTrash.Length)], spot,
                Quaternion.LookRotation(-outward) * Quaternion.Euler(0f, RandomRange(-25f, 25f), 0f)) != null)
            {
                spots.Placed.Add(spot);
            }
        }
    }

    // Small things scattered about, more of them towards the walls; round ones sometimes lying
    // on their side, rolled to any angle.
    private void ScatterSmallTrash(Room room, Vector2Int cell, Vector3 centre, RoomSpots spots)
    {
        if (smallTrash == null || smallTrash.Length == 0)
            return;

        int small = rng.Next(smallTrashPerCell.x, smallTrashPerCell.y + 1);

        for (int i = 0; i < small; i++)
        {
            Vector3 spot = rng.NextDouble() < 0.6 ? RandomWallPoint(cell, centre, 0.5f) : RandomFloorPoint(centre, 0.8f);

            if (!IsClear(spot, spots.Doorways, 1.8f) || !IsClear(spot, PillarsOf(room), 1f) || !IsClear(spot, spots.LootPoints, 0.9f)
                || !IsClear(spot, spots.Placed, 1f) || !IsClear(spot, spots.Hatch, HatchClearance))
            {
                continue;
            }

            GameObject prefab = smallTrash[rng.Next(smallTrash.Length)];
            bool tipped = rng.NextDouble() < tippedChance && IsRound(prefab.name);
            Quaternion rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

            if (tipped)
                rotation *= Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

            PlaceTrash(room.transform, prefab, spot, rotation);
        }
    }

    // Cans, bottles and cups can be found lying on their sides.
    private static bool IsRound(string name)
    {
        return name.Contains("Can") && !name.Contains("Gas") || name.Contains("Bottle") || name.Contains("Cup") || name.Contains("Tin");
    }

    private Vector3 RandomFloorPoint(Vector3 centre, float margin)
    {
        return centre + new Vector3(
            RandomRange(-roomSize.x / 2f + margin, roomSize.x / 2f - margin), 0f,
            RandomRange(-roomSize.y / 2f + margin, roomSize.y / 2f - margin));
    }

    // Somewhere just in from one of this cell's outer walls (or anywhere, if it has none).
    private Vector3 RandomWallPoint(Vector2Int cell, Vector3 centre, float inset)
    {
        List<Vector2Int> walls = new List<Vector2Int>();

        foreach (Vector2Int side in Directions)
        {
            if (!roomsByCell.ContainsKey(cell + side) || roomsByCell[cell + side] != roomsByCell[cell])
                walls.Add(side);
        }

        if (walls.Count == 0)
            return RandomFloorPoint(centre, 0.8f);

        Vector2Int wall = walls[rng.Next(walls.Count)];
        Vector3 outward = new Vector3(wall.x, 0f, wall.y);
        Vector3 along = new Vector3(wall.y, 0f, wall.x);
        float halfExtent = wall.x != 0 ? roomSize.x / 2f : roomSize.y / 2f;
        float halfLength = wall.x != 0 ? roomSize.y / 2f : roomSize.x / 2f;
        return centre + outward * (halfExtent - inset - RandomRange(0f, 1.2f)) + along * RandomRange(-halfLength + 0.5f, halfLength - 0.5f);
    }

    // Placed, sized, sat on the floor whatever way up it is, and given its physics.
    private GameObject PlaceTrash(Transform parent, GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        GameObject item = Instantiate(prefab, position, rotation, parent);
        item.name = prefab.name;
        item.transform.localScale = Vector3.one * RandomRange(trashScale.x, trashScale.y);

        // With the sprite art, the pack's oversized bottles and cups come down to size.
        if (SpriteVisuals.Current != null)
            item.transform.localScale *= ClutterSprites.SizeFactor(prefab.name);

        // Rest the lowest point on the floor (it may be on its side).
        Bounds bounds = new Bounds(item.transform.position, Vector3.zero);
        bool any = false;

        foreach (Renderer part in item.GetComponentsInChildren<Renderer>())
        {
            if (!any)
                bounds = part.bounds;
            else
                bounds.Encapsulate(part.bounds);

            any = true;
        }

        if (any)
            item.transform.position += Vector3.up * (0.001f - bounds.min.y);

        Prop prop = item.GetComponent<Prop>();

        if (prop != null)
            prop.Configure(boxPhysics);

        CardboardBox box = item.GetComponent<CardboardBox>();

        if (box != null)
        {
            box.Room = parent;
            box.EnablePhysics(boxPhysics);
        }

        SpriteClutter(item);
        return item;
    }
}
