using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's set dressing: cardboard box clusters against walls and in corners,
// pillars in the bigger rooms and the overhead lights (trash is in FloorGenerator.Trash.cs).
// All kept clear of doorways and the middle of each cell, so rooms stay walkable.
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
        RoomSpots spots = SpotsIn(room, new List<Vector3>(ClutterOf(room)));

        foreach (Vector2Int cell in room.Cells)
        {
            Vector3 centre = CellToWorld(cell);
            PlaceWallClusters(room, cell, centre, spots);
            PlaceOpenClusters(room, centre, spots);
            PlaceFlattenedSheets(room, centre, spots);
        }

        if (spots.Placed.Count > 0)
            clutter[room] = spots.Placed;
    }

    // What dressing a room keeps clear of: its doorways, wall loot points and hatch, and what's
    // already been placed (which the placing adds to).
    private class RoomSpots
    {
        public List<Vector3> Doorways;
        public List<Vector3> LootPoints;
        public List<Vector3> Hatch;
        public List<Vector3> Placed;
    }

    private RoomSpots SpotsIn(Room room, List<Vector3> placed)
    {
        RoomSpots spots = new RoomSpots
        {
            Doorways = DoorwayPositions(room),
            LootPoints = new List<Vector3>(),
            Hatch = HatchSpot(room),
            Placed = placed
        };

        foreach (LootSpawnPoint point in room.GetComponentsInChildren<LootSpawnPoint>())
            spots.LootPoints.Add(point.transform.position);

        return spots;
    }

    // Groups of boxes against the cell's walls that are the room's edge, often tucked into a
    // corner, otherwise anywhere along the wall.
    private void PlaceWallClusters(Room room, Vector2Int cell, Vector3 centre, RoomSpots spots)
    {
        int clusters = rng.Next(clutterPerCell.x, clutterPerCell.y + 1);
        List<Vector2Int> sides = new List<Vector2Int>();

        foreach (Vector2Int side in Directions)
        {
            if (!room.Contains(cell + side))
                sides.Add(side);
        }

        if (sides.Count == 0)
            return;

        for (int c = 0; c < clusters; c++)
        {
            Vector2Int wall = sides[rng.Next(sides.Count)];
            Vector3 outward = new Vector3(wall.x, 0f, wall.y);
            Vector3 along = new Vector3(wall.y, 0f, wall.x);
            float halfExtent = wall.x != 0 ? roomSize.x / 2f : roomSize.y / 2f;
            float halfLength = wall.x != 0 ? roomSize.y / 2f : roomSize.x / 2f;

            float offset = rng.NextDouble() < 0.5
                ? (rng.NextDouble() < 0.5 ? -1f : 1f) * (halfLength - 0.7f)
                : Mathf.Lerp(-halfLength + 0.8f, halfLength - 0.8f, (float)rng.NextDouble());

            Vector3 spot = centre + outward * (halfExtent - 0.85f) + along * offset;

            if (!IsClear(spot, spots.Doorways, 2.8f) || !IsClear(spot, PillarsOf(room), 1.9f)
                || !IsClear(spot, spots.LootPoints, 1.5f) || !IsClear(spot, spots.Placed, 2.2f)
                || Vector3.Distance(Flat(spot), Flat(centre)) < 2.8f)
            {
                continue;
            }

            spots.Placed.Add(spot);
            PlaceCluster(room, spot, outward, along);
        }
    }

    // A few free-standing groups out in the room, off the routes between the doors and the
    // middle of the cell.
    private void PlaceOpenClusters(Room room, Vector3 centre, RoomSpots spots)
    {
        int open = rng.Next(openClutterPerCell.x, openClutterPerCell.y + 1);

        for (int c = 0; c < open; c++)
        {
            Vector3 spot = centre + new Vector3(
                Mathf.Lerp(-roomSize.x / 2f + 2f, roomSize.x / 2f - 2f, (float)rng.NextDouble()), 0f,
                Mathf.Lerp(-roomSize.y / 2f + 1.8f, roomSize.y / 2f - 1.8f, (float)rng.NextDouble()));

            if (!IsClear(spot, spots.Doorways, 2.8f) || !IsClear(spot, PillarsOf(room), 2f) || !IsClear(spot, spots.LootPoints, 1.5f)
                || !IsClear(spot, spots.Placed, 2.2f) || Vector3.Distance(Flat(spot), Flat(centre)) < 2.8f
                || !IsClear(spot, spots.Hatch, HatchClearance + 0.6f) || NearDoorRoute(spot, centre, spots.Doorways, 1.8f))
            {
                continue;
            }

            spots.Placed.Add(spot);
            float turn = (float)rng.NextDouble() * 360f;
            PlaceCluster(room, spot, Quaternion.Euler(0f, turn, 0f) * Vector3.forward, Quaternion.Euler(0f, turn, 0f) * Vector3.right);
        }
    }

    // Flattened sheets lying about on the floor, to walk over.
    private void PlaceFlattenedSheets(Room room, Vector3 centre, RoomSpots spots)
    {
        int sheets = flattenedBox != null ? rng.Next(flattenedPerCell.x, flattenedPerCell.y + 1) : 0;

        for (int f = 0; f < sheets; f++)
        {
            Vector3 sheet = centre + new Vector3(
                Mathf.Lerp(-roomSize.x / 2f + 1f, roomSize.x / 2f - 1f, (float)rng.NextDouble()), 0f,
                Mathf.Lerp(-roomSize.y / 2f + 1f, roomSize.y / 2f - 1f, (float)rng.NextDouble()));

            if (!IsClear(sheet, spots.Doorways, 1.6f) || !IsClear(sheet, PillarsOf(room), 1.2f) || Vector3.Distance(Flat(sheet), Flat(centre)) < 1.5f
                || !IsClear(sheet, spots.Hatch, HatchClearance))
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
    // fitting to see from the camera, only the pools of light on the floor (and the dust drifting
    // through them: LightAtmosphere).
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
                LightAtmosphere.Dress(tube, lightHeight, Mathf.Min(2.4f, roomSize.y * 0.4f));
            }
        }
    }
}
