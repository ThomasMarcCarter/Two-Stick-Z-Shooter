using System.Collections.Generic;
using UnityEngine;

// Floor lines: paint, tape, inlays and carpet laid across a whole room a metre tile at a time, so
// they join up from tile to tile and cell to cell instead of being stamped about (2026-10-27).
// Each room theme has its own floor plan: yellow bay boxes in a storage room, hazard tape round an
// armoury, a pedestrian walkway through a base room from door to door, coloured wayfinding lines
// through the hospital, a carpet in a shop or gate lounge, chequered tiles in a food court, queue
// lanes at check-in and security. Rings follow the room's shape (L and T rooms, cut-outs, round
// pillars) and break at the doors; walkways run in from each door and link the room's cells.
// The tiles sit on whole metres of the floor's pixel grid (16 px per metre) and each one picks
// its picture from Walls/FloorLines.png by which neighbours it joins (Tools/Sprites/floorlines.py),
// all one mesh per room, under the floor decals and everything else on the floor.
public partial class FloorGenerator
{
    // FloorLines.png's rows, in Tools/Sprites/floorlines.py's STYLES order.
    private enum FloorLineStyle
    {
        HazardTape, YellowLine, WhiteLine, YellowWalkway, WayBlue, WayGreen, WayRed, RedLine, InlayBand,
        CarpetRed, CarpetBlue, CarpetGrey, Chequer, Hatched
    }

    [Header("Floor Lines")]
    [Tooltip("URP Lit, alpha clipped, FloorLines.png as the base map. Empty = no floor lines.")]
    [SerializeField] private Material floorLineMaterial;

    private const int FloorLineTilePixels = 16;
    private const float FloorLineHeight = 0.0026f;    // under the floor decals (0.004)
    private const float FloorLineLayer = 0.0003f;     // each style painted in a room a little higher
    private const int LinkNorth = 1, LinkEast = 2, LinkSouth = 4, LinkWest = 8;

    private static bool IsAreaStyle(FloorLineStyle style) => style >= FloorLineStyle.CarpetRed;

    // One room's floor plan: which metre tiles are floor, how deep into the room each is, and the
    // tiles each style paints with the neighbours it joins.
    private sealed class LinePlan
    {
        public readonly HashSet<Vector2Int> Floor = new HashSet<Vector2Int>();
        public readonly Dictionary<Vector2Int, int> Depth = new Dictionary<Vector2Int, int>();
        public readonly List<(Vector2Int Tile, Vector2Int Direction)> Doors = new List<(Vector2Int, Vector2Int)>();
        public readonly List<FloorLineStyle> Order = new List<FloorLineStyle>();
        public readonly Dictionary<FloorLineStyle, Dictionary<Vector2Int, int>> Links = new Dictionary<FloorLineStyle, Dictionary<Vector2Int, int>>();
        public float DoorGap;

        public Dictionary<Vector2Int, int> Of(FloorLineStyle style)
        {
            if (!Links.TryGetValue(style, out Dictionary<Vector2Int, int> links))
            {
                links = new Dictionary<Vector2Int, int>();
                Links[style] = links;
                Order.Add(style);
            }

            return links;
        }

        public int DepthOf(Vector2Int tile) => Depth.TryGetValue(tile, out int depth) ? depth : 0;

        // In front of a doorway: kept clear by everything but the walkways, which lead to them.
        public bool NearDoor(Vector2Int tile)
        {
            foreach ((Vector2Int door, Vector2Int direction) in Doors)
            {
                Vector2Int off = tile - door;
                int along = Mathf.Abs(direction.x != 0 ? off.y : off.x);
                int across = Mathf.Abs(direction.x != 0 ? off.x : off.y);

                if (along <= DoorGap && across < 4)
                    return true;
            }

            return false;
        }

        // Joins two side-by-side tiles with a line, if both are floor (and clear of the doors).
        public void Link(FloorLineStyle style, Vector2Int a, Vector2Int b, bool clearDoors = true)
        {
            if (!Floor.Contains(a) || !Floor.Contains(b) || (clearDoors && (NearDoor(a) || NearDoor(b))))
                return;

            Dictionary<Vector2Int, int> links = Of(style);
            links.TryGetValue(a, out int maskA);
            links.TryGetValue(b, out int maskB);
            links[a] = maskA | LinkBit(b - a);
            links[b] = maskB | LinkBit(a - b);
        }

        // A straight line between two tiles in a row or column.
        public void Line(FloorLineStyle style, Vector2Int from, Vector2Int to, bool clearDoors = true)
        {
            Vector2Int step = new Vector2Int(System.Math.Sign(to.x - from.x), System.Math.Sign(to.y - from.y));

            for (Vector2Int tile = from; tile != to; tile += step)
                Link(style, tile, tile + step, clearDoors);
        }

        public void Outline(FloorLineStyle style, Vector2Int min, Vector2Int max)
        {
            Line(style, min, new Vector2Int(max.x, min.y));
            Line(style, new Vector2Int(max.x, min.y), max);
            Line(style, max, new Vector2Int(min.x, max.y));
            Line(style, new Vector2Int(min.x, max.y), min);
        }

        // An area style over a box of tiles (the edges are worked out when it's built).
        public void Fill(FloorLineStyle style, Vector2Int min, Vector2Int max)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    Vector2Int tile = new Vector2Int(x, y);

                    if (Floor.Contains(tile) && !NearDoor(tile))
                        Of(style)[tile] = 0;
                }
            }
        }

        // An area style over every tile at least `depth` tiles in from the walls.
        public void FillInside(FloorLineStyle style, int depth)
        {
            foreach (Vector2Int tile in Floor)
            {
                if (DepthOf(tile) >= depth)
                    Of(style)[tile] = 0;
            }
        }

        // A line round the room `depth` tiles in from the walls, following its shape, broken at the doors.
        public void Ring(FloorLineStyle style, int depth)
        {
            foreach (Vector2Int tile in Floor)
            {
                if (DepthOf(tile) != depth)
                    continue;

                foreach (Vector2Int next in new[] { tile + Vector2Int.right, tile + Vector2Int.up })
                {
                    if (DepthOf(next) == depth)
                        Link(style, tile, next);
                }
            }
        }
    }

    private static int LinkBit(Vector2Int direction)
    {
        if (direction.y > 0) return LinkNorth;
        if (direction.x > 0) return LinkEast;
        if (direction.y < 0) return LinkSouth;
        return LinkWest;
    }

    // Lays the room's floor lines for its theme. Run with PaintFloor, after the pillars are up.
    private void PaintFloorLines(Room room, RoomTheme theme)
    {
        if (floorLineMaterial == null || floorLineMaterial.mainTexture == null || room.Type == RoomType.Boss)
            return;

        LinePlan plan = MeasureFloor(room);
        PlanFloorLines(plan, room, theme);
        BuildFloorLines(room, plan);
    }

    // The theme's floor plan; unthemed rooms get their zone's.
    private void PlanFloorLines(LinePlan plan, Room room, RoomTheme theme)
    {
        switch (theme)
        {
            case RoomTheme.Storage:
            case RoomTheme.StoreRoom:
                Bays(plan, room, FloorLineStyle.YellowLine);
                return;
            case RoomTheme.Workshop:
                plan.Ring(FloorLineStyle.HazardTape, 1);
                CellBoxes(plan, room, FloorLineStyle.Hatched, 3, 1);
                return;
            case RoomTheme.Armoury:
                plan.Ring(FloorLineStyle.HazardTape, 1);
                plan.Ring(FloorLineStyle.YellowLine, 3);
                return;
            case RoomTheme.Checkpoint:
                plan.Ring(FloorLineStyle.HazardTape, 2);
                Walkways(plan, room, FloorLineStyle.YellowWalkway, 4);
                return;
            case RoomTheme.Lab:
                plan.Ring(FloorLineStyle.HazardTape, 1);
                CellBoxes(plan, room, FloorLineStyle.Hatched, 2, 1);
                return;
            case RoomTheme.Infirmary:
                plan.Ring(FloorLineStyle.WhiteLine, 1);
                Walkways(plan, room, FloorLineStyle.WayGreen, 3);
                return;
            // Offices, food courts and gate lounges have their own floors now (UseThemeFloor), so no carpet laid over them.
            case RoomTheme.Office:
            case RoomTheme.SecurityOffice:
                return;
            case RoomTheme.Barracks:
                return;
            case RoomTheme.ShopFloor:
            case RoomTheme.DutyFree:
                plan.Ring(FloorLineStyle.InlayBand, 1);
                return;
            case RoomTheme.FoodCourt:
                return;
            case RoomTheme.Entrance:
                plan.Ring(FloorLineStyle.InlayBand, 1);
                plan.Ring(FloorLineStyle.InlayBand, 3);
                return;
            case RoomTheme.Ward:
            case RoomTheme.PatientWing:
                Walkways(plan, room, FloorLineStyle.WayGreen, 2);
                return;
            case RoomTheme.WaitingRoom:
            case RoomTheme.Reception:
                Walkways(plan, room, FloorLineStyle.WayBlue, 2);
                return;
            case RoomTheme.Pharmacy:
                Walkways(plan, room, FloorLineStyle.WayRed, 2);
                return;
            case RoomTheme.Theatre:
                plan.Ring(FloorLineStyle.RedLine, 2);
                return;
            case RoomTheme.GateLounge:
                return;
            case RoomTheme.BaggageHall:
                plan.Ring(FloorLineStyle.HazardTape, 1);
                Walkways(plan, room, FloorLineStyle.YellowWalkway, 3);
                return;
            case RoomTheme.SecurityCheck:
            case RoomTheme.CheckIn:
                QueueLanes(plan, room, FloorLineStyle.YellowLine);
                return;
        }

        switch (Zone)
        {
            case Zone.ShoppingMall: plan.Ring(FloorLineStyle.InlayBand, 2); break;
            case Zone.Hospital: Walkways(plan, room, FloorLineStyle.WayBlue, 2); break;
            case Zone.Airport: Walkways(plan, room, FloorLineStyle.WhiteLine, 2); break;
            default: Walkways(plan, room, FloorLineStyle.YellowWalkway, 2); break;
        }
    }

    // The room's floor in metre tiles (each centred on a whole metre): tiles wholly on its floor
    // blocks and clear of its pillars, and how many tiles in from the nearest edge each one is.
    private LinePlan MeasureFloor(Room room)
    {
        LinePlan plan = new LinePlan { DoorGap = doorWidth / 2f + 0.6f };
        List<Vector3> pillarSpots = PillarsOf(room);
        float pillarReach = pillarSize / 2f + 0.5f;

        foreach (Rect block in room.Blocks)
        {
            for (int x = Mathf.CeilToInt(block.xMin); x <= Mathf.FloorToInt(block.xMax); x++)
            {
                for (int y = Mathf.CeilToInt(block.yMin); y <= Mathf.FloorToInt(block.yMax); y++)
                {
                    Vector2Int tile = new Vector2Int(x, y);

                    if (!plan.Floor.Contains(tile) && TileOnFloor(room, tile) && !NearPillar(tile, pillarSpots, pillarReach))
                        plan.Floor.Add(tile);
                }
            }
        }

        // Depth: 1 against an edge (or a pillar), counting in square rings from there.
        Queue<Vector2Int> open = new Queue<Vector2Int>();

        foreach (Vector2Int tile in plan.Floor)
        {
            if (TouchesEdge(plan.Floor, tile))
            {
                plan.Depth[tile] = 1;
                open.Enqueue(tile);
            }
        }

        while (open.Count > 0)
        {
            Vector2Int tile = open.Dequeue();

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    Vector2Int next = tile + new Vector2Int(dx, dy);

                    if (plan.Floor.Contains(next) && !plan.Depth.ContainsKey(next))
                    {
                        plan.Depth[next] = plan.Depth[tile] + 1;
                        open.Enqueue(next);
                    }
                }
            }
        }

        // The tile just inside each doorway, and which way the door faces.
        foreach (RoomConnection connection in room.Connections)
        {
            Vector3 centre = CellToWorld(connection.Cell);
            Vector2Int middle = new Vector2Int(Mathf.RoundToInt(centre.x), Mathf.RoundToInt(centre.z));
            Vector2Int inset = new Vector2Int(Mathf.RoundToInt(roomSize.x / 2f) - 1, Mathf.RoundToInt(roomSize.y / 2f) - 1);
            plan.Doors.Add((middle + Vector2Int.Scale(connection.Direction, inset), connection.Direction));
        }

        return plan;
    }

    // All four corners of the tile (just inside) on the room's floor blocks.
    private static bool TileOnFloor(Room room, Vector2Int tile)
    {
        const float half = 0.48f;

        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 point = new Vector2(tile.x + (corner % 2 == 0 ? -half : half), tile.y + (corner < 2 ? -half : half));
            bool inside = false;

            foreach (Rect block in room.Blocks)
            {
                if (block.Contains(point))
                {
                    inside = true;
                    break;
                }
            }

            if (!inside)
                return false;
        }

        return true;
    }

    private static bool NearPillar(Vector2Int tile, List<Vector3> pillarSpots, float reach)
    {
        foreach (Vector3 pillar in pillarSpots)
        {
            if (Mathf.Abs(pillar.x - tile.x) < reach && Mathf.Abs(pillar.z - tile.y) < reach)
                return true;
        }

        return false;
    }

    private static bool TouchesEdge(HashSet<Vector2Int> floor, Vector2Int tile)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (!floor.Contains(tile + new Vector2Int(dx, dy)))
                    return true;
            }
        }

        return false;
    }

    private Vector2Int CellTile(Vector2Int cell)
    {
        Vector3 centre = CellToWorld(cell);
        return new Vector2Int(Mathf.RoundToInt(centre.x), Mathf.RoundToInt(centre.z));
    }

    // Lines in from each door, straight on across the room until `stopDepth` from the far wall
    // (clear of any ring), and between the middles of the room's neighbouring cells.
    private void Walkways(LinePlan plan, Room room, FloorLineStyle style, int stopDepth)
    {
        foreach ((Vector2Int door, Vector2Int direction) in plan.Doors)
        {
            Vector2Int tile = door;

            while (true)
            {
                Vector2Int next = tile - direction;
                bool pastDoor = (next - door).sqrMagnitude > 9;

                if (!plan.Floor.Contains(next) || (pastDoor && plan.DepthOf(next) < stopDepth))
                    break;

                plan.Link(style, tile, next, false);
                tile = next;
            }
        }

        foreach (Vector2Int cell in room.Cells)
        {
            foreach (Vector2Int step in new[] { Vector2Int.right, Vector2Int.up })
            {
                if (room.Contains(cell + step))
                    plan.Line(style, CellTile(cell), CellTile(cell + step), false);
            }
        }
    }

    // A box in the middle of each cell, halfX x halfZ tiles out from it: filled for an area style.
    private void CellBoxes(LinePlan plan, Room room, FloorLineStyle style, int halfX, int halfZ)
    {
        foreach (Vector2Int cell in room.Cells)
        {
            Vector2Int middle = CellTile(cell);
            Vector2Int half = new Vector2Int(halfX, halfZ);

            if (IsAreaStyle(style))
                plan.Fill(style, middle - half, middle + half);
            else
                plan.Outline(style, middle - half, middle + half);
        }
    }

    // Rows of 3 m storage bays along each cell's north and south walls, the aisle left clear.
    private void Bays(LinePlan plan, Room room, FloorLineStyle style)
    {
        int edgeZ = Mathf.RoundToInt(roomSize.y / 2f) - 1;
        int bays = Mathf.Max(1, (Mathf.RoundToInt(roomSize.x) - 4) / 3);

        foreach (Vector2Int cell in room.Cells)
        {
            Vector2Int middle = CellTile(cell);
            int startX = middle.x - bays * 3 / 2;

            for (int i = 0; i < bays; i++)
            {
                int x = startX + i * 3;
                plan.Outline(style, new Vector2Int(x, middle.y + 1), new Vector2Int(x + 3, middle.y + edgeZ));
                plan.Outline(style, new Vector2Int(x, middle.y - edgeZ), new Vector2Int(x + 3, middle.y - 1));
            }
        }
    }

    // A queue snaking back and forth through the middle of each cell, a lane every 2 m.
    private void QueueLanes(LinePlan plan, Room room, FloorLineStyle style)
    {
        int halfX = Mathf.RoundToInt(roomSize.x / 2f) - 4;
        int halfZ = Mathf.RoundToInt(roomSize.y / 2f) - 2;

        foreach (Vector2Int cell in room.Cells)
        {
            Vector2Int middle = CellTile(cell);
            int turn = -1;

            for (int z = -halfZ; z <= halfZ; z += 2)
            {
                plan.Line(style, new Vector2Int(middle.x - halfX, middle.y + z), new Vector2Int(middle.x + halfX, middle.y + z));

                if (z + 2 <= halfZ)
                {
                    int x = middle.x + turn * halfX;
                    plan.Line(style, new Vector2Int(x, middle.y + z), new Vector2Int(x, middle.y + z + 2));
                }

                turn = -turn;
            }
        }
    }

    // One mesh for the room: a quad per painted tile, its picture picked by the neighbours it
    // joins (area styles join every neighbour of the same style).
    private void BuildFloorLines(Room room, LinePlan plan)
    {
        if (plan.Order.Count == 0)
            return;

        Texture texture = floorLineMaterial.mainTexture;
        int rows = Mathf.Max(1, texture.height / FloorLineTilePixels);
        // A hair in from each tile's edges, so point sampling never picks up the next tile.
        float inset = 0.02f / texture.width;
        Vector3 origin = room.transform.position;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        // Carpets and tiles first, lines over them.
        plan.Order.Sort((a, b) => IsAreaStyle(b).CompareTo(IsAreaStyle(a)));

        for (int layer = 0; layer < plan.Order.Count; layer++)
        {
            FloorLineStyle style = plan.Order[layer];
            Dictionary<Vector2Int, int> links = plan.Links[style];
            float height = FloorLineHeight + layer * FloorLineLayer;

            foreach (KeyValuePair<Vector2Int, int> entry in links)
            {
                int mask = entry.Value;

                if (IsAreaStyle(style))
                {
                    mask = 0;

                    foreach (Vector2Int direction in Directions)
                    {
                        if (links.ContainsKey(entry.Key + direction))
                            mask |= LinkBit(direction);
                    }
                }
                else if (mask == 0)
                    continue;

                float u0 = (float)mask / 16f + inset;
                float u1 = (mask + 1f) / 16f - inset;
                float v0 = 1f - ((int)style + 1f) / rows + inset;
                float v1 = 1f - (float)(int)style / rows - inset;
                Vector3 centre = new Vector3(entry.Key.x, height, entry.Key.y) - origin;
                int first = vertices.Count;

                vertices.Add(centre + new Vector3(-0.5f, 0f, -0.5f));
                vertices.Add(centre + new Vector3(-0.5f, 0f, 0.5f));
                vertices.Add(centre + new Vector3(0.5f, 0f, 0.5f));
                vertices.Add(centre + new Vector3(0.5f, 0f, -0.5f));
                uvs.Add(new Vector2(u0, v0));
                uvs.Add(new Vector2(u0, v1));
                uvs.Add(new Vector2(u1, v1));
                uvs.Add(new Vector2(u1, v0));
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
        }

        if (vertices.Count == 0)
            return;

        Mesh mesh = new Mesh { name = "Floor Lines" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        floorMeshes.Add(mesh);

        GameObject lines = new GameObject("Floor Lines");
        lines.transform.SetParent(room.transform, false);
        lines.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = lines.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = floorLineMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
