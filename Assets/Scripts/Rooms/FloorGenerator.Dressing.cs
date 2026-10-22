using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's set dressing: cardboard box clusters against walls and in corners,
// trash strewn about combat rooms, pillars in the bigger rooms and the overhead lights. All
// kept clear of doorways and the middle of each cell, so rooms stay walkable.
public partial class FloorGenerator
{
    // ---------- Clutter ----------

    // Cardboard boxes against the walls and in the corners of a room: singles, pairs side by
    // side, and a small box stacked on a bigger one, with the odd flattened sheet on the floor.
    // Kept clear of doorways, pillars, the middle of each cell (loot, treasure and the exit go
    // there) and wall loot points, so the room stays walkable and nothing ends up buried.
    private void SpawnClutter(Room room)
    {
        if (clutterBoxes == null || clutterBoxes.Length == 0 || room.Type == RoomType.Start)
            return;

        // Starts with the theme furniture's footprint, so boxes keep off it too.
        List<Vector3> placed = new List<Vector3>(ClutterOf(room));
        List<Vector3> doorways = DoorwayPositions(room);
        List<Vector3> lootPoints = new List<Vector3>();
        List<Vector3> hatch = HatchSpot(room);

        foreach (LootSpawnPoint point in room.GetComponentsInChildren<LootSpawnPoint>())
            lootPoints.Add(point.transform.position);

        foreach (Vector2Int cell in room.Cells)
        {
            Vector3 centre = CellToWorld(cell);
            int clusters = rng.Next(clutterPerCell.x, clutterPerCell.y + 1);

            for (int c = 0; c < clusters; c++)
            {
                // A wall of this cell that's the room's edge.
                List<Vector2Int> sides = new List<Vector2Int>();

                foreach (Vector2Int side in Directions)
                {
                    if (!room.Contains(cell + side))
                        sides.Add(side);
                }

                if (sides.Count == 0)
                    break;

                Vector2Int wall = sides[rng.Next(sides.Count)];
                Vector3 outward = new Vector3(wall.x, 0f, wall.y);
                Vector3 along = new Vector3(wall.y, 0f, wall.x);
                float halfExtent = wall.x != 0 ? roomSize.x / 2f : roomSize.y / 2f;
                float halfLength = wall.x != 0 ? roomSize.y / 2f : roomSize.x / 2f;

                // Often tucked into a corner, otherwise anywhere along the wall.
                float offset = rng.NextDouble() < 0.5
                    ? (rng.NextDouble() < 0.5 ? -1f : 1f) * (halfLength - 0.7f)
                    : Mathf.Lerp(-halfLength + 0.8f, halfLength - 0.8f, (float)rng.NextDouble());

                Vector3 spot = centre + outward * (halfExtent - 0.85f) + along * offset;

                if (!IsClear(spot, doorways, 2.8f) || !IsClear(spot, PillarsOf(room), 1.9f)
                    || !IsClear(spot, lootPoints, 1.5f) || !IsClear(spot, placed, 2.2f)
                    || Vector3.Distance(Flat(spot), Flat(centre)) < 2.8f)
                {
                    continue;
                }

                placed.Add(spot);
                PlaceCluster(room, spot, outward, along);
            }

            // A few free-standing groups out in the room, off the routes between the doors and the
            // middle of the cell.
            int open = rng.Next(openClutterPerCell.x, openClutterPerCell.y + 1);

            for (int c = 0; c < open; c++)
            {
                Vector3 spot = centre + new Vector3(
                    Mathf.Lerp(-roomSize.x / 2f + 2f, roomSize.x / 2f - 2f, (float)rng.NextDouble()), 0f,
                    Mathf.Lerp(-roomSize.y / 2f + 1.8f, roomSize.y / 2f - 1.8f, (float)rng.NextDouble()));

                if (!IsClear(spot, doorways, 2.8f) || !IsClear(spot, PillarsOf(room), 2f) || !IsClear(spot, lootPoints, 1.5f)
                    || !IsClear(spot, placed, 2.2f) || Vector3.Distance(Flat(spot), Flat(centre)) < 2.8f
                    || !IsClear(spot, hatch, HatchClearance + 0.6f) || NearDoorRoute(spot, centre, doorways, 1.8f))
                {
                    continue;
                }

                placed.Add(spot);
                float turn = (float)rng.NextDouble() * 360f;
                PlaceCluster(room, spot, Quaternion.Euler(0f, turn, 0f) * Vector3.forward, Quaternion.Euler(0f, turn, 0f) * Vector3.right);
            }

            // Flattened sheets lying about on the floor, to walk over.
            int sheets = flattenedBox != null ? rng.Next(flattenedPerCell.x, flattenedPerCell.y + 1) : 0;

            for (int f = 0; f < sheets; f++)
            {
                Vector3 sheet = centre + new Vector3(
                    Mathf.Lerp(-roomSize.x / 2f + 1f, roomSize.x / 2f - 1f, (float)rng.NextDouble()), 0f,
                    Mathf.Lerp(-roomSize.y / 2f + 1f, roomSize.y / 2f - 1f, (float)rng.NextDouble()));

                if (!IsClear(sheet, doorways, 1.6f) || !IsClear(sheet, PillarsOf(room), 1.2f) || Vector3.Distance(Flat(sheet), Flat(centre)) < 1.5f
                    || !IsClear(sheet, hatch, HatchClearance))
                    continue;

                GameObject flat = Instantiate(flattenedBox, sheet, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), room.transform);
                flat.transform.localScale = Vector3.one * RandomRange(flattenedScale.x, flattenedScale.y);

                // A hair apart in height, so overlapping sheets don't flicker against each other.
                flat.transform.position += Vector3.up * RandomRange(0f, 0.01f);

                foreach (Collider part in flat.GetComponentsInChildren<Collider>())
                    Destroy(part);

                flat.name = flattenedBox.name;
                SpriteClutter(flat);
            }
        }

        if (placed.Count > 0)
            clutter[room] = placed;
    }

    // One to three boxes at a spot by a wall: the first squared up to the wall (a little
    // skewed), sometimes a second beside it, sometimes a small one on top.
    private void PlaceCluster(Room room, Vector3 spot, Vector3 outward, Vector3 along)
    {
        Quaternion facing = Quaternion.LookRotation(-outward);
        GameObject first = PlaceBox(room.transform, PickBox(), spot, facing * Quaternion.Euler(0f, RandomRange(-12f, 12f), 0f));

        if (first == null)
            return;

        float width = BoxSize(first).x;

        if (rng.NextDouble() < 0.5)
        {
            GameObject second = PickBox();
            float side = rng.NextDouble() < 0.5 ? -1f : 1f;
            Vector3 next = spot + along * side * (width / 2f + BoxSize(second).x * 0.9f + 0.1f);
            PlaceBox(room.transform, second, next, facing * Quaternion.Euler(0f, RandomRange(-20f, 20f), 0f));
        }

        // A smaller closed box stacked on a closed one. Its own body rests on top.
        if (rng.NextDouble() < 0.35 && first.name.Contains("Closed"))
        {
            GameObject small = PickSmallBox();

            if (small != null)
            {
                float height = BoxSize(first).y;
                GameObject top = PlaceBox(room.transform, small, first.transform.position + Vector3.up * (height + 0.01f),
                    first.transform.rotation * Quaternion.Euler(0f, RandomRange(-30f, 30f), 0f));

                if (top != null && BoxSize(top).x > width)
                    Destroy(top);   // too big to sit on it
            }
        }
    }

    private GameObject PlaceBox(Transform parent, GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        GameObject box = Instantiate(prefab, position, rotation, parent);
        box.name = prefab.name;
        float scale = RandomRange(boxScale.x, boxScale.y);
        BoxCollider body = box.GetComponent<BoxCollider>();

        if (body != null)
        {
            float longest = Mathf.Max(body.size.x, body.size.y, body.size.z);
            scale = Mathf.Min(scale, maxBoxSize / Mathf.Max(0.01f, longest));
        }

        box.transform.localScale = Vector3.one * scale;

        CardboardBox cardboard = box.GetComponent<CardboardBox>();

        if (cardboard != null)
        {
            cardboard.Room = parent;
            cardboard.EnablePhysics(boxPhysics);
        }

        SpriteClutter(box);
        return box;
    }

    // With the sprite art: the prop drawn as its clutter sprite (ClutterSprites).
    private static void SpriteClutter(GameObject prop)
    {
        ClutterSprites.Dress(prop);
    }

    private GameObject PickBox()
    {
        return clutterBoxes[rng.Next(clutterBoxes.Length)];
    }

    private GameObject PickSmallBox()
    {
        List<GameObject> small = new List<GameObject>();

        foreach (GameObject box in clutterBoxes)
        {
            if (box != null && box.name.Contains("Closed") && box.GetComponent<BoxCollider>() != null && box.GetComponent<BoxCollider>().size.y < 0.35f)
                small.Add(box);
        }

        return small.Count > 0 ? small[rng.Next(small.Count)] : null;
    }

    // As placed, scale included.
    private static Vector3 BoxSize(GameObject box)
    {
        BoxCollider body = box.GetComponent<BoxCollider>();
        return body != null ? Vector3.Scale(body.size, box.transform.lossyScale) : Vector3.one * 0.5f;
    }

    // Within distance of the straight route from any doorway to the middle of the cell.
    private static bool NearDoorRoute(Vector3 point, Vector3 centre, List<Vector3> doorways, float distance)
    {
        Vector2 p = new Vector2(point.x, point.z);
        Vector2 c = new Vector2(centre.x, centre.z);

        foreach (Vector3 door in doorways)
        {
            Vector2 d = new Vector2(door.x, door.z);
            Vector2 route = c - d;
            float t = Mathf.Clamp01(Vector2.Dot(p - d, route) / Mathf.Max(0.0001f, route.sqrMagnitude));

            if (Vector2.Distance(p, d + route * t) < distance)
                return true;
        }

        return false;
    }

    // How far litter keeps from the hatch's middle: its sprite is HatchOpening + 0.6 m across, so
    // this clears its corners with a bit to spare (flat litter would draw over it otherwise).
    private const float HatchClearance = 2.2f;

    // Where the room's hatch is or will be: the middle of the boss room (it appears there once the
    // boss is dead), or wherever an exit already stands (the hideout's deploy hatch).
    private static List<Vector3> HatchSpot(Room room)
    {
        List<Vector3> spots = new List<Vector3>();

        if (room.Type == RoomType.Boss)
            spots.Add(room.transform.position);

        foreach (FloorExit exit in room.GetComponentsInChildren<FloorExit>())
            spots.Add(exit.transform.position);

        return spots;
    }

    private List<Vector3> ClutterOf(Room room)
    {
        return clutter.TryGetValue(room, out List<Vector3> list) ? list : new List<Vector3>();
    }

    private float RandomRange(float min, float max)
    {
        return Mathf.Lerp(min, max, (float)rng.NextDouble());
    }

    private static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }

    // ---------- Trash ----------

    // Dresses a room with rubbish from the trash pack: flat litter strewn across the floor, small
    // things scattered about (more of them near the walls), and a big piece or two against the
    // walls. Kept off the doorways, pillars and wall loot points; the big pieces also stay off
    // the routes from the doors and the middle of each cell, and count as clutter, so enemies and
    // crates don't spawn on them. The spawn room gets a little litter and nothing else.
    private void SpawnTrash(Room room)
    {
        List<Vector3> doorways = DoorwayPositions(room);
        List<Vector3> lootPoints = new List<Vector3>();
        List<Vector3> hatch = HatchSpot(room);

        foreach (LootSpawnPoint point in room.GetComponentsInChildren<LootSpawnPoint>())
            lootPoints.Add(point.transform.position);

        if (!clutter.TryGetValue(room, out List<Vector3> taken))
        {
            taken = new List<Vector3>();
            clutter[room] = taken;
        }

        bool spawnRoom = room.Type == RoomType.Start;

        foreach (Vector2Int cell in room.Cells)
        {
            Vector3 centre = CellToWorld(cell);

            // Flat litter, anywhere but the doorways.
            if (floorLitter != null && floorLitter.Length > 0)
            {
                int litter = rng.Next(litterPerCell.x, litterPerCell.y + 1);

                if (spawnRoom)
                    litter /= 3;

                for (int i = 0; i < litter; i++)
                {
                    Vector3 spot = RandomFloorPoint(centre, 0.6f);

                    if (!IsClear(spot, doorways, 1.4f) || !IsClear(spot, PillarsOf(room), 0.9f) || !IsClear(spot, hatch, HatchClearance))
                        continue;

                    GameObject flat = PlaceTrash(room.transform, floorLitter[rng.Next(floorLitter.Length)], spot,
                        Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));

                    // A hair apart in height, so overlapping litter doesn't flicker against itself.
                    if (flat != null)
                        flat.transform.position += Vector3.up * RandomRange(0.002f, 0.012f);
                }
            }

            if (spawnRoom)
                continue;

            // Big pieces against the walls, as cover.
            if (largeTrash != null && largeTrash.Length > 0)
            {
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

                    if (!IsClear(spot, doorways, 3f) || !IsClear(spot, PillarsOf(room), 2f) || !IsClear(spot, lootPoints, 1.6f)
                        || !IsClear(spot, taken, 2.2f) || Vector3.Distance(Flat(spot), Flat(centre)) < 3f
                        || !IsClear(spot, hatch, HatchClearance + 0.6f))
                    {
                        continue;
                    }

                    if (PlaceTrash(room.transform, largeTrash[rng.Next(largeTrash.Length)], spot,
                        Quaternion.LookRotation(-outward) * Quaternion.Euler(0f, RandomRange(-25f, 25f), 0f)) != null)
                    {
                        taken.Add(spot);
                    }
                }
            }

            // Small things scattered about, more of them towards the walls.
            if (smallTrash != null && smallTrash.Length > 0)
            {
                int small = rng.Next(smallTrashPerCell.x, smallTrashPerCell.y + 1);

                for (int i = 0; i < small; i++)
                {
                    Vector3 spot = rng.NextDouble() < 0.6 ? RandomWallPoint(cell, centre, 0.5f) : RandomFloorPoint(centre, 0.8f);

                    if (!IsClear(spot, doorways, 1.8f) || !IsClear(spot, PillarsOf(room), 1f) || !IsClear(spot, lootPoints, 0.9f)
                        || !IsClear(spot, taken, 1f) || !IsClear(spot, hatch, HatchClearance))
                    {
                        continue;
                    }

                    GameObject prefab = smallTrash[rng.Next(smallTrash.Length)];
                    bool tipped = rng.NextDouble() < tippedChance && IsRound(prefab.name);
                    Quaternion rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                    // Lying on its side, rolled to any angle.
                    if (tipped)
                        rotation *= Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                    PlaceTrash(room.transform, prefab, spot, rotation);
                }
            }
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

    // ---------- Pillars and lights ----------

    // Square pillars in some cells of a big room: four in a square, or a diagonal pair, each a
    // quarter of the way in from the cell's centre. That keeps them off the lines between the
    // doors and the middle of the room, and off the centre (where floor loot goes).
    private void BuildPillars(Room room)
    {
        List<Vector3> placed = new List<Vector3>();
        float dx = roomSize.x / 4f;
        float dz = roomSize.y / 4f;

        foreach (Vector2Int cell in room.Cells)
        {
            double roll = rng.NextDouble();

            if (roll < 0.25)
                continue;   // some cells stay open

            Vector3 centre = CellToWorld(cell);
            Vector3[] offsets = roll < 0.6
                ? new[] { new Vector3(dx, 0f, dz), new Vector3(-dx, 0f, dz), new Vector3(dx, 0f, -dz), new Vector3(-dx, 0f, -dz) }
                : rng.NextDouble() < 0.5
                    ? new[] { new Vector3(dx, 0f, dz), new Vector3(-dx, 0f, -dz) }
                    : new[] { new Vector3(-dx, 0f, dz), new Vector3(dx, 0f, -dz) };

            foreach (Vector3 offset in offsets)
            {
                Vector3 position = centre + offset;
                GameObject pillar;

                if (pillarPrefab != null)
                {
                    // Turned to one of the four directions, so the same damage doesn't face the
                    // same way on every pillar.
                    pillar = Instantiate(pillarPrefab, position, Quaternion.Euler(0f, 90f * rng.Next(4), 0f), room.transform);
                    pillar.name = "Pillar";
                    pillar.transform.localScale = new Vector3(pillarSize, pillarHeight, pillarSize);
                }
                else
                {
                    pillar = CreateBlock(
                        "Pillar",
                        room.transform,
                        ToLocal(room.transform, position + Vector3.up * wallHeight / 2f),
                        new Vector3(pillarSize, wallHeight, pillarSize),
                        wallMaterial
                    );
                }

                pillar.tag = "Wall";
                pillar.GetComponent<Collider>().sharedMaterial = slidePhysics;
                FurnitureSprite.Apply(pillar, "Pillar", Vector3.back, new Vector3(pillarSize, pillarHeight, pillarSize), position);
                placed.Add(position);
            }
        }

        if (placed.Count > 0)
            pillars[room] = placed;
    }

    private List<Vector3> PillarsOf(Room room)
    {
        return pillars.TryGetValue(room, out List<Vector3> list) ? list : new List<Vector3>();
    }

    // Overhead fluorescent tubes, spaced along each cell. Just Light components: there's no
    // fitting to see from the camera, only the pools of light on the floor.
    private void AddCeilingLights(Room room)
    {
        if (lightsPerCell <= 0)
            return;

        foreach (Vector2Int cell in room.Cells)
        {
            Vector3 centre = CellToWorld(cell);

            for (int i = 0; i < lightsPerCell; i++)
            {
                // Evenly spaced along the cell's long side.
                float along = (i + 0.5f) / lightsPerCell - 0.5f;
                Vector3 position = centre + new Vector3(along * roomSize.x, lightHeight, 0f);

                GameObject lightObject = new GameObject("Ceiling Light");
                lightObject.transform.SetParent(room.transform, false);
                lightObject.transform.position = position;
                lightObject.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

                Light tube = lightObject.AddComponent<Light>();
                tube.type = LightType.Spot;
                tube.spotAngle = lightAngle;
                tube.innerSpotAngle = lightAngle * 0.45f;
                tube.range = lightRange;
                tube.color = lightColour;
                tube.shadows = LightShadows.None;
                // A little variation between tubes, as real ones age differently.
                tube.intensity = lightIntensity * Mathf.Lerp(0.85f, 1.05f, (float)rng.NextDouble());

                UnityEngine.Rendering.Universal.UniversalAdditionalLightData data =
                    lightObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
                data.renderingLayers = lightRenderingLayers;

                lightObject.AddComponent<FluorescentFlicker>();
            }
        }
    }
}
