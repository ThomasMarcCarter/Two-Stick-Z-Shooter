using System.Collections.Generic;
using UnityEngine;

// Cut-outs: variety inside a room's cells, so rooms of the same shape don't all have the same
// rectangular floor. On a run, a cell of a combat or treasure room (one without pillars) may get:
//   - walled-off corners (one or two of the cell's outside corners filled in, 3-5 m by 2-3 m),
//   - a buttress jutting out of the middle of a doorless wall,
//   - low cover walls (1 m high, 3-4 m long) in two or four of the cell's quarters, clear of the
//     lines from the doors to the middle.
// Corners and buttresses are taken out of the room: no floor there, kit walls round their room
// sides, the cell's own walls cut back where they meet, and the map outline follows. Low walls
// stand on the floor; they stop bullets and zombies but not thrown things. Everything solid is
// also counted as a pillar (points a metre apart over its area), so loot, furniture, boxes, trash,
// enemies, crates, decals and the caged survivor keep off it. Uses the kit's random sequence and
// only builds with the modular room kit (FloorGenerator.RoomKit.cs).
public partial class FloorGenerator
{
    [Header("Room Variety")]
    [Tooltip("Chance each cell of a combat or treasure room (without pillars) gets cut-outs: walled-off corners, a buttress or low cover walls.")]
    [SerializeField, Range(0f, 1f)] private float cutoutChance = 0.55f;
    [Tooltip("Height of the low cover walls. Under a character's height, so they're seen over; thrown things clear them.")]
    [SerializeField] private float lowWallHeight = 1f;

    private struct Cutout
    {
        public Rect Area;      // world XZ
        public bool Low;       // a low wall standing on the floor, not taken out of the room
    }

    // ---------- Planning ----------

    private Dictionary<Vector2Int, List<Cutout>> PlanCutouts(PlannedRoom planned, HashSet<(Vector2Int, Vector2Int)> doorways, bool hasPillars)
    {
        Dictionary<Vector2Int, List<Cutout>> plan = new Dictionary<Vector2Int, List<Cutout>>();

        if (!UseWallKit || !KitFeatures || hasPillars || (planned.Type != RoomType.Normal && planned.Type != RoomType.Treasure))
            return plan;

        HashSet<Vector2Int> cells = new HashSet<Vector2Int>(planned.Cells);

        foreach (Vector2Int cell in planned.Cells)
        {
            if (kitRng.NextDouble() >= cutoutChance)
                continue;

            Rect block = BlockRect(cell, 0, 0);
            List<Cutout> cuts = new List<Cutout>();
            int kind = kitRng.Next(4);

            if (kind <= 1)
                AddCorners(cell, block, cells, doorways, kind + 1, cuts);
            else if (kind == 2)
                AddButtress(cell, block, cells, doorways, cuts);

            // Low walls, or what's left when there was no room for the others.
            if (cuts.Count == 0)
                AddLowWalls(block, cuts);

            plan[cell] = cuts;
        }

        return plan;
    }

    // Fills in up to `count` of the cell's outside corners (where neither neighbour on those
    // sides is part of the room).
    private void AddCorners(Vector2Int cell, Rect block, HashSet<Vector2Int> cells, HashSet<(Vector2Int, Vector2Int)> doorways, int count, List<Cutout> cuts)
    {
        List<Vector2Int> corners = new List<Vector2Int>();

        foreach (Vector2Int corner in new[] { new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1) })
        {
            if (!cells.Contains(cell + new Vector2Int(corner.x, 0)) && !cells.Contains(cell + new Vector2Int(0, corner.y)))
                corners.Add(corner);
        }

        for (int i = corners.Count - 1; i > 0; i--)
        {
            int j = kitRng.Next(i + 1);
            (corners[i], corners[j]) = (corners[j], corners[i]);
        }

        for (int i = 0; i < Mathf.Min(count, corners.Count); i++)
        {
            // Bigger where no doorway is near: a doorway sits in the middle of a side, 1 m each
            // way, and a lone corner can take more of the cell than one of a pair.
            Vector2Int corner = corners[i];
            bool doorAlong = doorways.Contains((cell, new Vector2Int(0, corner.y)));
            bool doorAcross = doorways.Contains((cell, new Vector2Int(corner.x, 0)));
            int maxWidth = doorAlong || count > 1 ? 5 : 7;
            int maxDepth = doorAcross ? 3 : 4;
            float width = 3 + kitRng.Next(maxWidth - 2);
            float depth = 2 + kitRng.Next(maxDepth - 1);
            float x = corner.x > 0 ? block.xMax - width : block.xMin;
            float z = corner.y > 0 ? block.yMax - depth : block.yMin;
            cuts.Add(new Cutout { Area = new Rect(x, z, width, depth) });
        }
    }

    // A block jutting out of the middle of one of the cell's walls that has no doorway.
    private void AddButtress(Vector2Int cell, Rect block, HashSet<Vector2Int> cells, HashSet<(Vector2Int, Vector2Int)> doorways, List<Cutout> cuts)
    {
        List<Vector2Int> sides = new List<Vector2Int>();

        foreach (Vector2Int side in Directions)
        {
            if (!cells.Contains(cell + side) && !doorways.Contains((cell, side)))
                sides.Add(side);
        }

        if (sides.Count == 0)
            return;

        Vector2Int wall = sides[kitRng.Next(sides.Count)];
        Rect area;

        if (wall.y != 0)
        {
            float width = kitRng.NextDouble() < 0.5 ? 4f : 6f;
            float depth = 2f;
            area = new Rect(block.center.x - width / 2f, wall.y > 0 ? block.yMax - depth : block.yMin, width, depth);
        }
        else
        {
            float width = 4f;
            float depth = 2 + kitRng.Next(2);
            area = new Rect(wall.x > 0 ? block.xMax - depth : block.xMin, block.center.y - width / 2f, depth, width);
        }

        cuts.Add(new Cutout { Area = area });
    }

    // Low walls across two opposite quarters of the cell, or all four.
    private void AddLowWalls(Rect block, List<Cutout> cuts)
    {
        float length = kitRng.NextDouble() < 0.5 ? 3f : 4f;
        float dx = block.width * 0.28f;
        float dz = block.height * 0.25f;
        bool all = kitRng.NextDouble() < 0.35;
        bool flip = kitRng.NextDouble() < 0.5;
        float thickness = wallThickness;

        foreach (Vector2 quarter in new[] { new Vector2(1, 1), new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1) })
        {
            bool diagonal = quarter.x == quarter.y;

            if (!all && diagonal == flip)
                continue;

            Vector2 middle = block.center + new Vector2(quarter.x * dx, quarter.y * dz);
            // Whole metres along, so the wall's texture lines up with the rest.
            float x = Mathf.Round(middle.x - length / 2f);
            cuts.Add(new Cutout { Area = new Rect(x, middle.y - thickness / 2f, length, thickness), Low = true });
        }
    }

    // ---------- Building ----------

    // A cell's floor block with its cut-outs: the floor around them, the cell's walls with gaps
    // where cut-outs meet them, and walls round the cut-outs themselves.
    private void BuildCutCell(Room room, Vector2Int cell, Rect block, List<Cutout> cuts, HashSet<(Vector2Int, Vector2Int)> doorways,
        List<Rect> blocks, List<RoomEdge> outline, KitMesh floor)
    {
        List<Rect> solid = new List<Rect>();

        foreach (Cutout cut in cuts)
        {
            if (!cut.Low)
                solid.Add(cut.Area);
        }

        foreach (Rect piece in Subtract(block, solid))
        {
            blocks.Add(piece);
            AddFloorBlock(room.transform, piece, floor);
        }

        foreach (Vector2Int direction in Directions)
        {
            NeighbourBlock(cell, 0, 0, direction, out Vector2Int nCell, out int ni, out int nj);

            if (!InRoom(room, nCell, ni, nj))
                BuildCutWall(room, cell, block, direction, doorways.Contains((cell, direction)), solid, outline);
        }

        foreach (Cutout cut in cuts)
        {
            if (cut.Low)
                BuildLowWall(room, cut.Area, outline);
            else
                BuildCutoutWalls(room, block, cut.Area, outline);

            AddSolidPoints(room, cut.Area);
        }
    }

    // One side of a cut cell: like BuildWall, but with gaps where cut-outs meet the side. Each
    // piece reaches a wall's thickness into a cut-out, so it closes the corner with the cut-out's
    // own wall.
    private void BuildCutWall(Room room, Vector2Int cell, Rect block, Vector2Int direction, bool doorway, List<Rect> solid, List<RoomEdge> outline)
    {
        bool alongX = direction.y != 0;
        Vector2Int along = alongX ? Vector2Int.right : Vector2Int.up;
        float start = alongX ? block.xMin : block.yMin;
        float end = alongX ? block.xMax : block.yMax;
        float side = direction.x > 0 ? block.xMax : direction.x < 0 ? block.xMin : direction.y > 0 ? block.yMax : block.yMin;

        NeighbourBlock(cell, 0, 0, -along, out Vector2Int c0, out int i0, out int j0);
        NeighbourBlock(cell, 0, 0, along, out Vector2Int c1, out int i1, out int j1);

        // What the cut-outs cover of this side, in order along it.
        List<(float, float)> covered = new List<(float, float)>();

        foreach (Rect area in solid)
        {
            float edge = direction.x > 0 ? area.xMax : direction.x < 0 ? area.xMin : direction.y > 0 ? area.yMax : area.yMin;

            if (Mathf.Abs(edge - side) < 0.01f)
                covered.Add(alongX ? (area.xMin, area.xMax) : (area.yMin, area.yMax));
        }

        covered.Sort((a, b) => a.Item1.CompareTo(b.Item1));

        // The map outline: the side less the covered parts.
        float cursor = start;

        foreach ((float a, float b) in covered)
        {
            if (a > cursor + 0.01f)
                outline.Add(SideEdge(direction, side, cursor, a));

            cursor = Mathf.Max(cursor, b);
        }

        if (end > cursor + 0.01f)
            outline.Add(SideEdge(direction, side, cursor, end));

        // The wall pieces, with the doorway's gap too.
        List<(float, float)> pieces = new List<(float, float)>();
        float from = InRoom(room, c0, i0, j0) ? start : start - wallThickness;

        foreach ((float a, float b) in covered)
        {
            if (a > start + 0.01f)
                pieces.Add((from, a + wallThickness));

            from = b - wallThickness;
        }

        bool openEnd = covered.Count == 0 || covered[covered.Count - 1].Item2 < end - 0.01f;

        if (openEnd)
            pieces.Add((from, InRoom(room, c1, i1, j1) ? end : end + wallThickness));

        float across = side + (direction.x + direction.y) * wallThickness / 2f;
        Vector3 inward = new Vector3(-direction.x, 0f, -direction.y);
        bool south = direction.y < 0;
        float middle = alongX ? block.center.x : block.center.y;
        float doorFrom = middle - doorWidth / 2f;
        float doorTo = middle + doorWidth / 2f;

        // A pilaster where the wall runs on into the strip joining the next cell (as BuildWall).
        NeighbourBlock(c1, i1, j1, direction, out Vector2Int c2, out int i2, out int j2);
        bool runsOn = openEnd && InRoom(room, c1, i1, j1) && !InRoom(room, c2, i2, j2);

        for (int p = 0; p < pieces.Count; p++)
        {
            (float a, float b) = pieces[p];
            KitSpan span = KitSpan.Plain;

            if (direction.y > 0)
            {
                span.Room = room;
                span.CellMiddle = block.center.x;
                span.PilasterAtEnd = runsOn && p == pieces.Count - 1;
            }

            if (doorway && a < doorFrom && b > doorTo)
            {
                KitSpan before = span;
                before.DoorAtEnd = true;
                before.PilasterAtEnd = false;
                KitSpan after = span;
                after.DoorAtStart = true;
                HideIfSouth(CreateKitWall(room.transform, alongX, across, a, doorFrom, inward, before), south);
                HideIfSouth(CreateKitWall(room.transform, alongX, across, doorTo, b, inward, after), south);
            }
            else
                HideIfSouth(CreateKitWall(room.transform, alongX, across, a, b, inward, span), south);
        }

        if (doorway)
        {
            CreateDoorFrame(room.transform, alongX, across, middle, inward);
            CreateDoorStripe(room.transform, alongX, across - (direction.x + direction.y) * (wallThickness / 2f + 0.3f), middle);
        }
    }

    // Walls along a cut-out's sides that face into the room, inside the cut-out. The ones across
    // (along X) run its full width; the ones along Z stop short of them, so corners don't overlap.
    private void BuildCutoutWalls(Room room, Rect block, Rect area, List<RoomEdge> outline)
    {
        bool bottom = area.yMin > block.yMin + 0.01f;
        bool top = area.yMax < block.yMax - 0.01f;
        bool left = area.xMin > block.xMin + 0.01f;
        bool right = area.xMax < block.xMax - 0.01f;
        float t = wallThickness;

        if (bottom)
        {
            // Faces the camera like a north wall, so it gets the kit's features too.
            KitSpan span = KitSpan.Plain;
            span.Room = room;
            CreateKitWall(room.transform, true, area.yMin + t / 2f, area.xMin, area.xMax, Vector3.back, span);
            outline.Add(new RoomEdge { From = new Vector2(area.xMin, area.yMin), To = new Vector2(area.xMax, area.yMin), Outward = Vector2.up });
        }

        if (top)
        {
            HideIfSouth(CreateKitWall(room.transform, true, area.yMax - t / 2f, area.xMin, area.xMax, Vector3.forward, KitSpan.Plain), true);
            outline.Add(new RoomEdge { From = new Vector2(area.xMin, area.yMax), To = new Vector2(area.xMax, area.yMax), Outward = Vector2.down });
        }

        float z0 = area.yMin + (bottom ? t : 0f);
        float z1 = area.yMax - (top ? t : 0f);

        // On the south side its side walls are hidden too, like the south wall they stand out of
        // (one left standing would read as a stray post).
        bool southern = !bottom;

        if (left)
        {
            HideIfSouth(CreateKitWall(room.transform, false, area.xMin + t / 2f, z0, z1, Vector3.left, KitSpan.Plain), southern);
            outline.Add(new RoomEdge { From = new Vector2(area.xMin, area.yMin), To = new Vector2(area.xMin, area.yMax), Outward = Vector2.right });
        }

        if (right)
        {
            HideIfSouth(CreateKitWall(room.transform, false, area.xMax - t / 2f, z0, z1, Vector3.right, KitSpan.Plain), southern);
            outline.Add(new RoomEdge { From = new Vector2(area.xMax, area.yMin), To = new Vector2(area.xMax, area.yMax), Outward = Vector2.left });
        }
    }

    // A low cover wall standing on the floor, its face to the camera.
    private void BuildLowWall(Room room, Rect area, List<RoomEdge> outline)
    {
        GameObject wall = CreateKitWall(room.transform, true, area.center.y, area.xMin, area.xMax, Vector3.back, KitSpan.Plain, lowWallHeight);

        if (wall != null)
            wall.name = "Low Wall";

        outline.Add(new RoomEdge { From = new Vector2(area.xMin, area.center.y), To = new Vector2(area.xMax, area.center.y), Outward = Vector2.up });
    }

    // Points a metre apart over the cut-out (edges included), counted as pillars so everything
    // placed later keeps off it.
    private void AddSolidPoints(Room room, Rect area)
    {
        if (!pillars.TryGetValue(room, out List<Vector3> list))
            pillars[room] = list = new List<Vector3>();

        int across = Mathf.Max(1, Mathf.CeilToInt(area.width));
        int deep = Mathf.Max(1, Mathf.CeilToInt(area.height));

        for (int i = 0; i <= across; i++)
        {
            for (int j = 0; j <= deep; j++)
                list.Add(new Vector3(area.xMin + area.width * i / across, 0f, area.yMin + area.height * j / deep));
        }
    }

    private static RoomEdge SideEdge(Vector2Int direction, float side, float a, float b)
    {
        Vector2 outward = direction;

        return direction.y != 0
            ? new RoomEdge { From = new Vector2(a, side), To = new Vector2(b, side), Outward = outward }
            : new RoomEdge { From = new Vector2(side, a), To = new Vector2(side, b), Outward = outward };
    }

    // The block less the given rects, as rects: cut into strips across X at every rect's edges,
    // each strip's free spans along Z, strips with the same spans joined up.
    private static List<Rect> Subtract(Rect block, List<Rect> holes)
    {
        List<float> xs = new List<float> { block.xMin, block.xMax };

        foreach (Rect hole in holes)
        {
            xs.Add(Mathf.Clamp(hole.xMin, block.xMin, block.xMax));
            xs.Add(Mathf.Clamp(hole.xMax, block.xMin, block.xMax));
        }

        xs.Sort();
        List<Rect> result = new List<Rect>();
        List<(float, float)> previous = null;
        List<int> previousRects = new List<int>();

        for (int k = 0; k < xs.Count - 1; k++)
        {
            float x0 = xs[k];
            float x1 = xs[k + 1];

            if (x1 - x0 < 0.001f)
                continue;

            // Free spans along Z in this strip.
            List<(float, float)> spans = new List<(float, float)> { (block.yMin, block.yMax) };

            foreach (Rect hole in holes)
            {
                if (hole.xMin >= x1 - 0.001f || hole.xMax <= x0 + 0.001f)
                    continue;

                List<(float, float)> next = new List<(float, float)>();

                foreach ((float a, float b) in spans)
                {
                    if (hole.yMin > a + 0.001f) next.Add((a, Mathf.Min(b, hole.yMin)));
                    if (hole.yMax < b - 0.001f) next.Add((Mathf.Max(a, hole.yMax), b));
                }

                spans = next;
            }

            if (previous != null && SameSpans(previous, spans))
            {
                // Widen the last strip's rects instead of starting new ones.
                foreach (int index in previousRects)
                    result[index] = Rect.MinMaxRect(result[index].xMin, result[index].yMin, x1, result[index].yMax);

                continue;
            }

            previousRects.Clear();

            foreach ((float a, float b) in spans)
            {
                if (b - a < 0.001f)
                    continue;

                previousRects.Add(result.Count);
                result.Add(Rect.MinMaxRect(x0, a, x1, b));
            }

            previous = spans;
        }

        return result;
    }

    private static bool SameSpans(List<(float, float)> a, List<(float, float)> b)
    {
        if (a.Count != b.Count)
            return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (Mathf.Abs(a[i].Item1 - b[i].Item1) > 0.001f || Mathf.Abs(a[i].Item2 - b[i].Item2) > 0.001f)
                return false;
        }

        return true;
    }
}
