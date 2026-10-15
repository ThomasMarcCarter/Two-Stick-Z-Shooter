using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

public enum MapRoomState
{
    Unexplored,
    Hostile,
    Cleared
}

// How rooms and markers look on the tactical map. Shared by the map itself and its legend so
// the two always match.
public static class MapSymbols
{
    public static MapRoomState StateOf(Room room)
    {
        if (!room.Visited)
            return MapRoomState.Unexplored;

        // A room with nobody left in it counts as cleared even if the player only stepped in
        // the doorway and never triggered the lock.
        return room.IsCleared || room.Enemies.Count == 0 ? MapRoomState.Cleared : MapRoomState.Hostile;
    }

    // A simple rectangular room, as used by the legend.
    public static void DrawRoom(VertexHelper vh, Rect rect, MapRoomState state, Color phosphor, Color hostile)
    {
        DrawRoomShape(vh, new[] { rect }, RectEdges(rect), rect, state, phosphor, hostile);
    }

    // A room of any shape, from its floor blocks and outline edges (already in map space).
    // The status mark goes in the top-right corner of markRect.
    // Unexplored: faint fill, dashed outline.
    // Hostile:    red fill and outline with a "!" in the corner.
    // Cleared:    green fill, solid outline and a tick in the corner.
    public static void DrawRoomShape(VertexHelper vh, IReadOnlyList<Rect> blocks, IReadOnlyList<RoomEdge> edges,
        Rect markRect, MapRoomState state, Color phosphor, Color hostile)
    {
        float mark = Mathf.Clamp(markRect.height * 0.18f, 4f, 9f);
        Vector2 corner = new Vector2(markRect.xMax - mark - 4f, markRect.yMax - mark - 4f);

        switch (state)
        {
            case MapRoomState.Unexplored:
                FillBlocks(vh, blocks, Fade(phosphor, 0.03f));

                foreach (RoomEdge edge in edges)
                {
                    Vector2 inset = -edge.Outward * 0.75f;
                    DashedLine(vh, edge.From + inset, edge.To + inset, 1.5f, 6f, Fade(phosphor, 0.45f));
                }
                break;

            case MapRoomState.Hostile:
                FillBlocks(vh, blocks, Fade(hostile, 0.16f));
                DrawEdges(vh, edges, 2f, hostile);
                FillRect(vh, new Rect(corner.x + mark * 0.4f, corner.y + mark * 0.35f, mark * 0.25f, mark * 0.75f), hostile);
                FillRect(vh, new Rect(corner.x + mark * 0.4f, corner.y, mark * 0.25f, mark * 0.2f), hostile);
                break;

            case MapRoomState.Cleared:
                FillBlocks(vh, blocks, Fade(phosphor, 0.2f));
                DrawEdges(vh, edges, 2f, Fade(phosphor, 0.9f));
                Line(vh, corner + new Vector2(0f, mark * 0.5f), corner + new Vector2(mark * 0.35f, mark * 0.15f), 2f, phosphor);
                Line(vh, corner + new Vector2(mark * 0.35f, mark * 0.15f), corner + new Vector2(mark, mark), 2f, phosphor);
                break;
        }
    }

    public static void FillBlocks(VertexHelper vh, IReadOnlyList<Rect> blocks, Color colour)
    {
        foreach (Rect block in blocks)
            FillRect(vh, block, colour);
    }

    // Solid outline, drawn on the inside of each edge.
    public static void DrawEdges(VertexHelper vh, IReadOnlyList<RoomEdge> edges, float thickness, Color colour)
    {
        foreach (RoomEdge edge in edges)
        {
            Vector2 inset = -edge.Outward * thickness / 2f;
            Line(vh, edge.From + inset, edge.To + inset, thickness, colour);
        }
    }

    // Outline pushed outwards by `offset`, with each segment stretched so the corners join.
    public static void DrawEdgesOutside(VertexHelper vh, IReadOnlyList<RoomEdge> edges, float offset, float thickness, Color colour)
    {
        foreach (RoomEdge edge in edges)
        {
            Vector2 along = (edge.To - edge.From).normalized;
            Vector2 shift = edge.Outward * offset;
            Line(vh, edge.From + shift - along * offset, edge.To + shift + along * offset, thickness, colour);
        }
    }

    public static RoomEdge[] RectEdges(Rect rect)
    {
        return new[]
        {
            new RoomEdge { From = new Vector2(rect.xMin, rect.yMin), To = new Vector2(rect.xMax, rect.yMin), Outward = Vector2.down },
            new RoomEdge { From = new Vector2(rect.xMin, rect.yMax), To = new Vector2(rect.xMax, rect.yMax), Outward = Vector2.up },
            new RoomEdge { From = new Vector2(rect.xMin, rect.yMin), To = new Vector2(rect.xMin, rect.yMax), Outward = Vector2.left },
            new RoomEdge { From = new Vector2(rect.xMax, rect.yMin), To = new Vector2(rect.xMax, rect.yMax), Outward = Vector2.right }
        };
    }

    public static void DrawSpawn(VertexHelper vh, Vector2 position, float size, Color colour)
    {
        OutlineRect(vh, new Rect(position - Vector2.one * size / 2f, Vector2.one * size), Mathf.Max(1.5f, size * 0.15f), colour);
        FillRect(vh, new Rect(position - Vector2.one * size * 0.2f, Vector2.one * size * 0.4f), colour);
    }

    public static void DrawTreasure(VertexHelper vh, Vector2 position, float size, Color colour)
    {
        float half = size / 2f;
        Quad(vh,
            position + Vector2.up * half, position + Vector2.right * half,
            position + Vector2.down * half, position + Vector2.left * half,
            colour);
    }

    public static void DrawBoss(VertexHelper vh, Vector2 position, float size, Color colour)
    {
        float half = size / 2f;
        float thickness = Mathf.Max(2f, size * 0.2f);
        Line(vh, position + new Vector2(-half, -half), position + new Vector2(half, half), thickness, colour);
        Line(vh, position + new Vector2(-half, half), position + new Vector2(half, -half), thickness, colour);
    }

    // Small "+" used to flag a room that has weapons in it.
    public static void DrawItems(VertexHelper vh, Vector2 position, float size, Color colour)
    {
        float thickness = Mathf.Max(2f, size * 0.3f);
        FillRect(vh, new Rect(position.x - size / 2f, position.y - thickness / 2f, size, thickness), colour);
        FillRect(vh, new Rect(position.x - thickness / 2f, position.y - size / 2f, thickness, size), colour);
    }

    // Small cartridge (pointed tip on a body) used to flag a room with ammo lying in it.
    public static void DrawAmmo(VertexHelper vh, Vector2 position, float size, Color colour)
    {
        float halfWidth = Mathf.Max(1.5f, size * 0.22f);
        float bottom = position.y - size / 2f;
        float shoulder = position.y + size * 0.12f;
        float tip = position.y + size / 2f;

        FillRect(vh, Rect.MinMaxRect(position.x - halfWidth, bottom, position.x + halfWidth, shoulder), colour);
        Triangle(vh, new Vector2(position.x - halfWidth, shoulder), new Vector2(position.x, tip),
            new Vector2(position.x + halfWidth, shoulder), colour);
        // Rim at the base.
        FillRect(vh, Rect.MinMaxRect(position.x - halfWidth * 1.35f, bottom, position.x + halfWidth * 1.35f, bottom + Mathf.Max(1f, size * 0.12f)), colour);
    }

    // Small medkit (a box with a cross knocked out of it) used to flag a room with a medkit in it.
    public static void DrawMedkit(VertexHelper vh, Vector2 position, float size, Color colour)
    {
        float half = size / 2f;
        float arm = size * 0.32f;
        float bar = Mathf.Max(1.5f, size * 0.2f);

        // Four corner blocks around a cross-shaped gap.
        FillRect(vh, Rect.MinMaxRect(position.x - half, position.y + bar / 2f, position.x - bar / 2f, position.y + half), colour);
        FillRect(vh, Rect.MinMaxRect(position.x + bar / 2f, position.y + bar / 2f, position.x + half, position.y + half), colour);
        FillRect(vh, Rect.MinMaxRect(position.x - half, position.y - half, position.x - bar / 2f, position.y - bar / 2f), colour);
        FillRect(vh, Rect.MinMaxRect(position.x + bar / 2f, position.y - half, position.x + half, position.y - bar / 2f), colour);
        OutlineRect(vh, new Rect(position.x - half, position.y - half, size, size), 1f, colour);

        // The cross, faintly, so it reads as a shape not just a gap.
        Color faint = Fade(colour, 0.25f);
        FillRect(vh, Rect.MinMaxRect(position.x - arm, position.y - bar / 2f, position.x + arm, position.y + bar / 2f), faint);
        FillRect(vh, Rect.MinMaxRect(position.x - bar / 2f, position.y - arm, position.x + bar / 2f, position.y + arm), faint);
    }

    // The icon for a special room: spawn square, treasure diamond or boss X. Normal rooms have none.
    public static void DrawMarker(VertexHelper vh, RoomType type, Vector2 position, float size, Color phosphor, Color hostile)
    {
        switch (type)
        {
            case RoomType.Start:
                DrawSpawn(vh, position, size, phosphor);
                break;
            case RoomType.Treasure:
                DrawTreasure(vh, position, size, phosphor);
                break;
            case RoomType.Boss:
                DrawBoss(vh, position, size * 0.9f, hostile);
                break;
        }
    }

    // A marker pinned to the edge of a map: the room's icon with an arrowhead pointing at the room.
    public static void DrawEdgeMarker(VertexHelper vh, RoomType type, Vector2 position, Vector2 direction, float size,
        Color phosphor, Color hostile)
    {
        Color arrowColour = type == RoomType.Boss ? hostile : phosphor;
        Vector2 side = new Vector2(-direction.y, direction.x);
        Vector2 tip = position + direction * size * 0.9f;

        Triangle(vh, tip, position + direction * size * 0.25f + side * size * 0.45f,
            position + direction * size * 0.25f - side * size * 0.45f, arrowColour);

        DrawMarker(vh, type, position - direction * size * 0.45f, size * 0.75f, phosphor, hostile);
    }

    // Where a point outside a rect should be pinned: along the line from the rect's centre to the
    // point, stopping at the rect shrunk by the inset. Returns false if the point is already inside.
    public static bool PinToEdge(Rect rect, float inset, Vector2 point, out Vector2 pinned, out Vector2 direction)
    {
        Vector2 centre = rect.center;
        Vector2 offset = point - centre;
        Vector2 halfExtents = rect.size / 2f - Vector2.one * inset;

        direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector2.up;

        if (Mathf.Abs(offset.x) <= halfExtents.x && Mathf.Abs(offset.y) <= halfExtents.y)
        {
            pinned = point;
            return false;
        }

        float scale = Mathf.Min(
            Mathf.Abs(offset.x) > 0.001f ? halfExtents.x / Mathf.Abs(offset.x) : float.MaxValue,
            Mathf.Abs(offset.y) > 0.001f ? halfExtents.y / Mathf.Abs(offset.y) : float.MaxValue
        );

        pinned = centre + offset * scale;
        return true;
    }

    public static void DrawPlayer(VertexHelper vh, Vector2 position, Vector2 forward, float size, Color colour)
    {
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector2.up;
        Vector2 side = new Vector2(-forward.y, forward.x);

        Triangle(vh,
            position + forward * size,
            position - forward * size * 0.6f + side * size * 0.6f,
            position - forward * size * 0.6f - side * size * 0.6f,
            colour);
    }
}
