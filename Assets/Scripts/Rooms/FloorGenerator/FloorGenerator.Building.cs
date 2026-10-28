using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The FloorGenerator's building step: turns each planned room into floor blocks, walls (with
// doorways, frames and hazard stripes) and the corridors between doors. Walls are drawn with the
// zone's room kit (FloorGenerator.RoomKit.cs); the low-level block helpers are in
// FloorGenerator.Blocks.cs.
public partial class FloorGenerator
{
    // Each cell is split into a 3x3 grid of blocks: the floor in the middle, and the strips of
    // wall-and-corridor space around it. The middle is always part of the room; a side strip is
    // part of it when the neighbouring cell on that side is the same room, and a corner when all
    // three cells around that corner are. Walls go wherever a room block meets a non-room block,
    // which gives closed walls for any shape, including the inside corners of L and T rooms.
    private void BuildRoom(PlannedRoom planned, HashSet<(Vector2Int, Vector2Int)> doorways)
    {
        Vector2Int anchor = planned.Cells[0];
        GameObject roomObject = new GameObject($"Room {anchor.x},{anchor.y} {planned.Shape.Name} ({planned.Type})");
        roomObject.transform.SetParent(floorRoot, false);
        roomObject.transform.localPosition = CellToWorld(anchor);

        Room room = roomObject.AddComponent<Room>();
        room.Initialise(planned.Type, planned.Shape.Name, planned.Cells, CellStep, roomSize);
        rooms.Add(room);

        foreach (Vector2Int cell in planned.Cells)
            roomsByCell[cell] = room;

        List<Rect> blocks = new List<Rect>();
        List<RoomEdge> outline = new List<RoomEdge>();
        Material roomFloor = PickFloorMaterial(planned.Type);
        KitMesh floorMesh = modularRooms ? new KitMesh() : null;

        // Pillars and cut-outs (FloorGenerator.Cutouts.cs) don't share a room.
        bool hasPillars = planned.Type == RoomType.Normal && planned.Cells.Count >= minCellsForPillars && rng.NextDouble() < pillarChance;
        Dictionary<Vector2Int, List<Cutout>> cutouts = PlanCutouts(planned, doorways, hasPillars);

        // The boss room's way down goes in an opening in its north wall (PlaceZoneExit).
        Vector2? exitOpening = PlanExitOpening(planned, doorways);

        foreach (Vector2Int cell in planned.Cells)
        {
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (!InRoom(room, cell, i, j))
                        continue;

                    if (i == 0 && j == 0 && cutouts.TryGetValue(cell, out List<Cutout> cuts))
                    {
                        BuildCutCell(room, cell, BlockRect(cell, 0, 0), cuts, doorways, blocks, outline, floorMesh);
                        continue;
                    }

                    Rect block = BlockRect(cell, i, j);
                    blocks.Add(block);
                    BuildFloorBlock(roomObject.transform, block, floorMesh, roomFloor, planned.Type);

                    foreach (Vector2Int direction in Directions)
                    {
                        NeighbourBlock(cell, i, j, direction, out Vector2Int nCell, out int ni, out int nj);

                        if (InRoom(room, nCell, ni, nj))
                            continue;

                        bool doorway = i == 0 && j == 0 && doorways.Contains((cell, direction));
                        Vector2? opening = exitOpening.HasValue && i == 0 && j == 0 && direction == Vector2Int.up && cell == planned.Cells[0]
                            ? exitOpening : null;
                        BuildWall(room, cell, i, j, block, direction, doorway, opening);

                        if (opening.HasValue)
                            AddExitOutline(block, opening.Value, outline);
                        else
                            outline.Add(EdgeOf(block, direction));
                    }
                }
            }
        }

        if (exitOpening.HasValue)
            BuildExitBay(room, BlockRect(planned.Cells[0], 0, 0), exitOpening.Value, blocks, outline, floorMesh);

        if (floorMesh != null)
            CreateFloorMesh(roomObject.transform, floorMesh, roomFloor, planned.Type);

        room.SetGeometry(blocks, outline);

        if (hasPillars)
            BuildPillars(room);

        AddCeilingLights(room);
    }

    // One block of a room's floor: added to the room's floor mesh with the modular rooms, or a
    // block of its own, textured in world space and tinted for the room's type.
    private void BuildFloorBlock(Transform roomTransform, Rect block, KitMesh floorMesh, Material material, RoomType type)
    {
        if (floorMesh != null)
        {
            AddFloorBlock(roomTransform, block, floorMesh);
            return;
        }

        GameObject floor = CreateBlock(
            "Floor",
            roomTransform,
            ToLocal(roomTransform, new Vector3(block.center.x, -0.1f, block.center.y)),
            new Vector3(block.width, 0.2f, block.height),
            material
        );

        ApplyWorldUVs(floor);
        ApplyTint(floor, type);
    }

    private static bool InRoom(Room room, Vector2Int cell, int i, int j)
    {
        if (!room.Contains(cell))
            return false;

        if (i == 0 && j == 0)
            return true;

        if (j == 0)
            return room.Contains(cell + new Vector2Int(i, 0));

        if (i == 0)
            return room.Contains(cell + new Vector2Int(0, j));

        return room.Contains(cell + new Vector2Int(i, 0))
            && room.Contains(cell + new Vector2Int(0, j))
            && room.Contains(cell + new Vector2Int(i, j));
    }

    // World XZ rect of block (i, j) of a cell: i/j of 0 is the floor, -1/+1 the strips either side.
    private Rect BlockRect(Vector2Int cell, int i, int j)
    {
        Vector3 centre = CellToWorld(cell);
        Vector2 step = CellStep;

        (float xMin, float xMax) = BlockSpan(centre.x, roomSize.x, step.x, i);
        (float zMin, float zMax) = BlockSpan(centre.z, roomSize.y, step.y, j);

        return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
    }

    private static (float, float) BlockSpan(float centre, float floor, float step, int index)
    {
        if (index < 0) return (centre - step / 2f, centre - floor / 2f);
        if (index > 0) return (centre + floor / 2f, centre + step / 2f);
        return (centre - floor / 2f, centre + floor / 2f);
    }

    private static RoomEdge EdgeOf(Rect block, Vector2Int direction)
    {
        Vector2 outward = direction;

        if (direction.x > 0) return new RoomEdge { From = new Vector2(block.xMax, block.yMin), To = new Vector2(block.xMax, block.yMax), Outward = outward };
        if (direction.x < 0) return new RoomEdge { From = new Vector2(block.xMin, block.yMin), To = new Vector2(block.xMin, block.yMax), Outward = outward };
        if (direction.y > 0) return new RoomEdge { From = new Vector2(block.xMin, block.yMax), To = new Vector2(block.xMax, block.yMax), Outward = outward };
        return new RoomEdge { From = new Vector2(block.xMin, block.yMin), To = new Vector2(block.xMax, block.yMin), Outward = outward };
    }

    // The floor a room gets: the normal one, or now and then (combat rooms only) a variant.
    private Material PickFloorMaterial(RoomType type)
    {
        if (type != RoomType.Normal || floorVariants == null || floorVariants.Length == 0 || rng.NextDouble() >= floorVariantChance)
            return floorMaterial;

        Material variant = floorVariants[rng.Next(floorVariants.Length)];
        return variant != null ? variant : floorMaterial;
    }

    // A wall just outside one side of a room block. At each end it runs on by a wall's thickness
    // when that end is an outside corner, so corners close; at inside corners and where the wall
    // carries on along the next block it stops at the block edge.
    private void BuildWall(Room room, Vector2Int cell, int i, int j, Rect block, Vector2Int direction, bool doorway, Vector2? opening = null)
    {
        bool alongX = direction.y != 0;
        Vector2Int along = alongX ? Vector2Int.right : Vector2Int.up;

        float start = alongX ? block.xMin : block.yMin;
        float end = alongX ? block.xMax : block.yMax;

        NeighbourBlock(cell, i, j, -along, out Vector2Int c0, out int i0, out int j0);
        NeighbourBlock(cell, i, j, along, out Vector2Int c1, out int i1, out int j1);

        if (!InRoom(room, c0, i0, j0)) start -= wallThickness;
        if (!InRoom(room, c1, i1, j1)) end += wallThickness;

        // Position across the wall: just outside the block on the given side.
        float across = direction.x > 0 ? block.xMax + wallThickness / 2f
            : direction.x < 0 ? block.xMin - wallThickness / 2f
            : direction.y > 0 ? block.yMax + wallThickness / 2f
            : block.yMin - wallThickness / 2f;

        bool south = direction.y < 0;
        Vector3 inward = new Vector3(-direction.x, 0f, -direction.y);

        // North walls on a run get the zone kit's pilasters, alcoves, panels and broken tops
        // (FloorGenerator.RoomKit.cs); a pilaster marks where the wall runs on into the next block.
        KitSpan span = KitSpan.Plain;

        if (UseWallKit && KitFeatures && direction.y > 0)
        {
            NeighbourBlock(c1, i1, j1, direction, out Vector2Int c2, out int i2, out int j2);
            span.Room = room;
            // (Not between the two halves of the strip joining two cells.)
            span.PilasterAtEnd = InRoom(room, c1, i1, j1) && !InRoom(room, c2, i2, j2) && (i == 0 || i1 == 0);

            if (i == 0)
                span.CellMiddle = block.center.x;
        }

        // A gap for the way down (ZoneExit), and the doorway's if there is one, with wall between.
        if (opening.HasValue)
        {
            List<(float, float, bool)> gaps = new List<(float, float, bool)> { (opening.Value.x, opening.Value.y, false) };
            float doorMiddle = alongX ? block.center.x : block.center.y;

            if (doorway)
            {
                gaps.Add((doorMiddle - doorWidth / 2f, doorMiddle + doorWidth / 2f, true));
                CreateDoorFrame(room.transform, alongX, across, doorMiddle, inward);
                CreateDoorStripe(room.transform, alongX, across - (direction.x + direction.y) * (wallThickness / 2f + 0.3f), doorMiddle);
            }

            gaps.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            float from = start;

            for (int g = 0; g <= gaps.Count; g++)
            {
                float to = g < gaps.Count ? gaps[g].Item1 : end;
                KitSpan piece = span;
                piece.DoorAtStart = g > 0;
                piece.DoorAtEnd = g < gaps.Count;
                piece.PilasterAtEnd = span.PilasterAtEnd && g == gaps.Count;
                HideIfSouth(CreateWallSpan(room.transform, alongX, across, from, to, inward, piece), south);

                if (g < gaps.Count)
                    from = gaps[g].Item2;
            }

            return;
        }

        if (!doorway)
        {
            HideIfSouth(CreateWallSpan(room.transform, alongX, across, start, end, inward, span), south);
            return;
        }

        float middle = alongX ? block.center.x : block.center.y;
        KitSpan before = span;
        before.DoorAtEnd = true;
        before.PilasterAtEnd = false;
        KitSpan after = span;
        after.DoorAtStart = true;
        HideIfSouth(CreateWallSpan(room.transform, alongX, across, start, middle - doorWidth / 2f, inward, before), south);
        HideIfSouth(CreateWallSpan(room.transform, alongX, across, middle + doorWidth / 2f, end, inward, after), south);
        CreateDoorFrame(room.transform, alongX, across, middle, new Vector3(-direction.x, 0f, -direction.y));
        CreateDoorStripe(room.transform, alongX, across - (direction.x + direction.y) * (wallThickness / 2f + 0.3f), middle);
        CreateThreshold(room.transform, alongX, across, middle, direction);
    }

    // A steel tread-plate threshold across a doorway (the theme floors' DiamondPlate), over the
    // gap the wall leaves and a little way into the room, so wherever the corridor's deck meets a
    // room's own floor there's a clean edge instead of a seam. `direction` points out of the room.
    private void CreateThreshold(Transform parent, bool alongX, float across, float middle, Vector2Int direction)
    {
        Material plate = SpriteVisuals.Current != null ? SpriteVisuals.Current.ThemeFloor("DiamondPlate") : null;

        if (plate == null)
            return;

        const float into = 0.12f;
        float depth = wallThickness + into + 0.08f;
        float centre = across + (direction.x + direction.y) * (0.04f - into / 2f);
        float width = doorWidth + 0.1f;

        GameObject threshold = GameObject.CreatePrimitive(PrimitiveType.Quad);
        threshold.name = "Door Threshold";
        Destroy(threshold.GetComponent<Collider>());
        threshold.transform.SetParent(parent, false);
        threshold.transform.position = alongX ? new Vector3(middle, 0.008f, centre) : new Vector3(centre, 0.008f, middle);
        threshold.transform.rotation = Quaternion.Euler(90f, alongX ? 0f : 90f, 0f);
        threshold.transform.localScale = new Vector3(width, depth, 1f);

        Renderer renderer = threshold.GetComponent<Renderer>();
        renderer.sharedMaterial = plate;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // The quad's UVs span it once: scale them so the plate's pattern is at the floors' size (8 m a tile).
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetVector("_BaseMap_ST", new Vector4(width / 8f, depth / 8f, 0f, 0f));
        renderer.SetPropertyBlock(block);
    }

    // A worn yellow-and-black stripe painted on the floor across a doorway, on the room side.
    private void CreateDoorStripe(Transform parent, bool alongX, float across, float middle)
    {
        if (hazardStripeMaterial == null)
            return;

        GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Quad);
        stripe.name = "Door Stripe";
        Destroy(stripe.GetComponent<Collider>());
        stripe.transform.SetParent(parent, false);
        stripe.transform.position = alongX ? new Vector3(middle, 0.012f, across) : new Vector3(across, 0.012f, middle);
        stripe.transform.rotation = Quaternion.Euler(90f, alongX ? 0f : 90f, 0f);
        stripe.transform.localScale = new Vector3(doorWidth, 0.45f, 1f);

        Renderer renderer = stripe.GetComponent<Renderer>();
        renderer.sharedMaterial = hazardStripeMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Steel posts either side of a doorway, standing a little proud of the wall on both faces
    // and above it, so doorways read clearly from above, with flashing amber lights on them
    // (DoorBeacon) so they're easy to find in the dark. Just for looks: no colliders (the wall
    // ends do the blocking), so nothing catches on them. `inward` points into the room.
    private void CreateDoorFrame(Transform parent, bool alongX, float across, float middle, Vector3 inward)
    {
        const float postWidth = 0.16f;
        Vector3 size = WallSize(alongX, postWidth, wallThickness + 0.1f) + Vector3.up * 0.06f;
        Vector3[] posts = new Vector3[2];

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float along = middle + side * (doorWidth / 2f + postWidth / 2f - 0.02f);
            posts[i] = alongX
                ? new Vector3(along, (wallHeight + 0.06f) / 2f, across)
                : new Vector3(across, (wallHeight + 0.06f) / 2f, along);

            if (wallTrimMaterial != null)
            {
                GameObject post = CreateBlock("Door Frame", parent, ToLocal(parent, posts[i]), size, wallTrimMaterial);
                Destroy(post.GetComponent<Collider>());
                HideIfSouth(post, inward.z > 0.5f);
            }
        }

        DoorBeacon.Create(parent, posts[0], posts[1], wallHeight + 0.06f, inward, postWidth, wallThickness, ammoMaterial);
    }

    // A wall on the side nearest the camera: drawn only into the shadows (see hideSouthWalls).
    private void HideIfSouth(GameObject wall, bool south)
    {
        if (wall == null || !south || !hideSouthWalls)
            return;

        foreach (Renderer part in wall.GetComponentsInChildren<Renderer>())
            part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
    }

    private GameObject CreateWallSpan(Transform parent, bool alongX, float across, float start, float end, Vector3 inward, KitSpan span)
    {
        if (UseWallKit)
            return CreateKitWall(parent, alongX, across, start, end, inward, span);

        float length = end - start;

        if (length <= 0.001f)
            return null;

        float middle = (start + end) / 2f;
        Vector3 world = alongX
            ? new Vector3(middle, wallHeight / 2f, across)
            : new Vector3(across, wallHeight / 2f, middle);

        return CreateWall("Wall", parent, ToLocal(parent, world), WallSize(alongX, length, wallThickness));
    }

    private void BuildCorridor(Vector2Int cell, Vector2Int direction)
    {
        Vector3 forward = new Vector3(direction.x, 0f, direction.y);
        Vector3 side = new Vector3(direction.y, 0f, direction.x);
        bool alongX = direction.x != 0;

        float roomHalfExtent = alongX ? roomSize.x / 2f : roomSize.y / 2f;
        Vector3 centre = CellToWorld(cell) + forward * (roomHalfExtent + wallThickness + corridorLength / 2f);

        GameObject corridor = new GameObject($"Corridor {cell.x},{cell.y} {DirectionName(direction)}");
        corridor.transform.SetParent(floorRoot, false);
        corridor.transform.localPosition = centre;

        // Floor runs through both doorways and under the side walls.
        float floorLength = corridorLength + wallThickness * 2f;
        float floorWidth = doorWidth + wallThickness * 2f;

        GameObject corridorFloor = CreateBlock(
            "Floor",
            corridor.transform,
            new Vector3(0f, -0.1f, 0f),
            alongX ? new Vector3(floorLength, 0.2f, floorWidth) : new Vector3(floorWidth, 0.2f, floorLength),
            corridorFloorMaterial != null ? corridorFloorMaterial : floorMaterial
        );

        ApplyWorldUVs(corridorFloor);

        // One door in the middle of the corridor, shared by the rooms on either side.
        GameObject doorObject = CreateBlock(
            "Door",
            corridor.transform,
            Vector3.up * wallHeight / 2f,
            WallSize(!alongX, doorWidth, wallThickness),
            doorMaterial
        );

        doorObject.tag = "Wall";
        doorObject.GetComponent<Collider>().sharedMaterial = slidePhysics;
        RaiseCollider(doorObject);
        RoomDoor door = doorObject.AddComponent<RoomDoor>();

        Room from = roomsByCell[cell];
        Room to = roomsByCell[cell + direction];
        from.AddConnection(new RoomConnection { Cell = cell, Direction = direction, Neighbour = to, Door = door });
        to.AddConnection(new RoomConnection { Cell = cell + direction, Direction = -direction, Neighbour = from, Door = door });

        if (corridorLength <= 0f)
            return;

        float sideOffset = doorWidth / 2f + wallThickness / 2f;
        Vector3 wallSize = WallSize(alongX, corridorLength, wallThickness);

        // The corridor's south wall (when it runs east-west) is hidden like the rooms' (hideSouthWalls).
        if (UseWallKit)
        {
            float along = alongX ? centre.x : centre.z;
            float across = alongX ? centre.z : centre.x;
            float offset = (alongX ? side.z : side.x) * sideOffset;
            HideIfSouth(CreateKitWall(corridor.transform, alongX, across + offset, along - corridorLength / 2f, along + corridorLength / 2f, -side, KitSpan.Plain), side.z < -0.5f);
            HideIfSouth(CreateKitWall(corridor.transform, alongX, across - offset, along - corridorLength / 2f, along + corridorLength / 2f, side, KitSpan.Plain), side.z > 0.5f);
            return;
        }

        HideIfSouth(CreateWall("Wall", corridor.transform, side * sideOffset + Vector3.up * wallHeight / 2f, wallSize), side.z < -0.5f);
        HideIfSouth(CreateWall("Wall", corridor.transform, -side * sideOffset + Vector3.up * wallHeight / 2f, wallSize), side.z > 0.5f);
    }

    // The block next to (cell, i, j) in a direction, which may be in the neighbouring cell.
    private static void NeighbourBlock(Vector2Int cell, int i, int j, Vector2Int direction,
        out Vector2Int nCell, out int ni, out int nj)
    {
        nCell = cell;
        ni = i + direction.x;
        nj = j + direction.y;

        if (ni > 1) { ni = -1; nCell.x++; }
        if (ni < -1) { ni = 1; nCell.x--; }
        if (nj > 1) { nj = -1; nCell.y++; }
        if (nj < -1) { nj = 1; nCell.y--; }
    }
}
