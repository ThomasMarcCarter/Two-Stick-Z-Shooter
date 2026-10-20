using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// Military-radar style minimap: green phosphor scope with a scrolling grid and range rings.
// Shows explored rooms around the player, enemies in the current room as blips, and markers for
// the spawn, treasure and boss rooms that stay pinned to the edge when out of range. Drawn as one UI mesh.
public class Minimap : MaskableGraphic
{
    [Header("References")]
    [SerializeField] private FloorGenerator floor;
    [SerializeField] private Transform player;
    [SerializeField] private TMP_Text statusText;

    [Header("Scale")]
    [SerializeField] private float pixelsPerUnit = 3f;

    [Header("Look")]
    [SerializeField] private Color phosphor = UiColours.Phosphor;
    [SerializeField] private Color background = new Color(0f, 0.07f, 0.03f, 0.85f);
    [SerializeField] private Color hostile = new Color(1f, 0.25f, 0.2f, 1f);
    [SerializeField] private float gridSpacing = 16f;
    [SerializeField] private float pulseInterval = 1.2f;

    private Rect clip;
    private FloorExit hatch;
    private string lastStatus;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    private void Update()
    {
        // Graphic runs in edit mode too; only animate while playing.
        if (!Application.isPlaying)
            return;

        UpdateStatus();
        SetVerticesDirty();
    }

    private void UpdateStatus()
    {
        if (statusText == null || floor == null)
            return;

        Room room = floor.CurrentRoom;
        string status;

        if (room == null)
            status = "NO SIGNAL";
        else if (room.IsLocked)
            status = $"<color=#{ColorUtility.ToHtmlStringRGB(hostile)}>! ALERT</color>  HOSTILES {room.Enemies.Count}";
        else
            status = room.Type == RoomType.Boss ? "AREA CLEAR  // BOSS DOWN" : "AREA CLEAR";

        if (status == lastStatus)
            return;

        lastStatus = status;
        statusText.text = status;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect bounds = rectTransform.rect;
        clip = bounds;

        Vector2 centre = bounds.center;
        float radius = Mathf.Min(bounds.width, bounds.height) * 0.5f - 6f;
        Vector2 playerMap = player != null
            ? new Vector2(player.position.x, player.position.z) * pixelsPerUnit
            : Vector2.zero;

        AddRect(vh, bounds, background);
        DrawGrid(vh, bounds, centre, playerMap);
        DrawRings(vh, centre, radius);

        if (floor != null && player != null)
        {
            DrawRooms(vh, centre, playerMap);
            DrawMarkers(vh, centre, playerMap);
            DrawStations(vh, centre, playerMap);
            DrawEnemies(vh, centre, playerMap);
            DrawDetectionRings(vh, centre, playerMap);
            DrawPlayer(vh, centre);
        }

        DrawFrame(vh, bounds);
    }

    // ---------- Layers ----------

    private void DrawGrid(VertexHelper vh, Rect bounds, Vector2 centre, Vector2 playerMap)
    {
        // Grid is fixed to the world, so it scrolls as the player moves.
        Color gridColour = Fade(phosphor, 0.07f);
        Vector2 origin = centre - playerMap;

        for (float x = bounds.xMin + Mathf.Repeat(origin.x - bounds.xMin, gridSpacing); x < bounds.xMax; x += gridSpacing)
            AddRect(vh, new Rect(x - 0.5f, bounds.yMin, 1f, bounds.height), gridColour);

        for (float y = bounds.yMin + Mathf.Repeat(origin.y - bounds.yMin, gridSpacing); y < bounds.yMax; y += gridSpacing)
            AddRect(vh, new Rect(bounds.xMin, y - 0.5f, bounds.width, 1f), gridColour);
    }

    private void DrawRings(VertexHelper vh, Vector2 centre, float radius)
    {
        Color ringColour = Fade(phosphor, 0.18f);

        Ring(vh, centre, radius / 3f, 1f, ringColour);
        Ring(vh, centre, radius * 2f / 3f, 1f, ringColour);
        Ring(vh, centre, radius, 1f, ringColour);

        Color crossColour = Fade(phosphor, 0.12f);
        AddRect(vh, new Rect(centre.x - radius, centre.y - 0.5f, radius * 2f, 1f), crossColour);
        AddRect(vh, new Rect(centre.x - 0.5f, centre.y - radius, 1f, radius * 2f), crossColour);
    }

    private void DrawRooms(VertexHelper vh, Vector2 centre, Vector2 playerMap)
    {
        Room current = floor.CurrentRoom;

        foreach (Room room in floor.Rooms)
        {
            if (!floor.IsKnown(room))
                continue;

            bool isCurrent = room == current;

            float fillAlpha = isCurrent ? 0.28f : room.Visited ? 0.12f : 0.03f;

            foreach (Rect block in room.Blocks)
                AddRect(vh, MapRect(block, centre, playerMap), Fade(phosphor, fillAlpha));

            Color outline = room.IsLocked && Blink()
                ? hostile
                : Fade(phosphor, room.Visited ? 0.9f : 0.35f);

            float thickness = isCurrent ? 2f : 1f;

            foreach (RoomEdge edge in room.Outline)
                AddRect(vh, EdgeRect(edge, thickness, centre, playerMap), outline);

            DrawCorridors(vh, room, centre, playerMap);
        }
    }

    // Corridors running east or north only, so each one is drawn once.
    private void DrawCorridors(VertexHelper vh, Room room, Vector2 centre, Vector2 playerMap)
    {
        Color colour = Fade(phosphor, 0.5f);

        foreach (RoomConnection connection in room.Connections)
        {
            if (connection.Direction != Vector2Int.right && connection.Direction != Vector2Int.up)
                continue;

            if (!FloorGenerator.IsCorridorKnown(room, connection.Neighbour))
                continue;

            floor.GetCorridorEnds(connection, out Vector2 from, out Vector2 to);
            Vector2 a = MapPoint(from, centre, playerMap);
            Vector2 b = MapPoint(to, centre, playerMap);

            Rect corridor = connection.Direction == Vector2Int.right
                ? Rect.MinMaxRect(a.x, a.y - 1.5f, b.x, a.y + 1.5f)
                : Rect.MinMaxRect(a.x - 1.5f, a.y, a.x + 1.5f, b.y);

            AddRect(vh, corridor, colour);
        }
    }

    // Spawn, treasure and boss markers, drawn after the rooms so nothing covers them. Each one
    // stays visible once the room is known: out of range, it's pinned to the radar edge with an
    // arrow pointing towards the room.
    private void DrawMarkers(VertexHelper vh, Vector2 centre, Vector2 playerMap)
    {
        const float edgeInset = 9f;

        foreach (Room room in floor.Rooms)
        {
            // No spawn marker in the hideout: its stations are its icons.
            if (room.Type == RoomType.Normal || !floor.IsKnown(room) || floor.IsHideout)
                continue;

            Vector2 position = MapPoint(room.transform.position, centre, playerMap);

            if (MapSymbols.PinToEdge(clip, edgeInset, position, out Vector2 pinned, out Vector2 direction))
                MapSymbols.DrawEdgeMarker(vh, room.Type, pinned, direction, 8f, phosphor, hostile);
            else
                MapSymbols.DrawMarker(vh, room.Type, position, 10f, phosphor, hostile);
        }
    }

    // The hideout's stations, as small icons (abandoned survivor areas dim), and its deploy hatch.
    private void DrawStations(VertexHelper vh, Vector2 centre, Vector2 playerMap)
    {
        if (floor.IsHideout)
        {
            if (hatch == null)
                hatch = FindAnyObjectByType<FloorExit>();

            if (hatch != null)
            {
                Vector2 at = MapPoint(hatch.Position, centre, playerMap);

                if (InsideClip(at, 7f))
                    MapSymbols.DrawHatch(vh, at, 11f, phosphor);
            }
        }

        foreach (StationMarker marker in StationMarker.All)
        {
            Vector2 position = MapPoint(marker.Position, centre, playerMap);

            if (InsideClip(position, 7f))
                MapSymbols.DrawStation(vh, marker.Station, position, 11f, phosphor, marker.Abandoned);
        }
    }

    private void DrawEnemies(VertexHelper vh, Vector2 centre, Vector2 playerMap)
    {
        Room room = floor.CurrentRoom;

        if (room == null)
            return;

        foreach (Enemy enemy in room.Enemies)
        {
            if (enemy == null)
                continue;

            Vector2 blip = MapPoint(enemy.transform.position, centre, playerMap);

            if (!InsideClip(blip, 5f))
                continue;

            float blipSize = enemy.transform.localScale.x > 1.5f ? 9f : 5f;
            AddRect(vh, new Rect(blip - Vector2.one * blipSize / 2f, Vector2.one * blipSize), hostile);
        }
    }

    private void DrawPlayer(VertexHelper vh, Vector2 centre)
    {
        float pulse = Mathf.Repeat(Time.time, pulseInterval) / pulseInterval;
        Ring(vh, centre, 4f + pulse * 22f, 1.5f, Fade(phosphor, (1f - pulse) * 0.6f));

        // Arrow pointing where the player is aiming.
        Vector2 forward = new Vector2(player.forward.x, player.forward.z);
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector2.up;
        Vector2 side = new Vector2(-forward.y, forward.x);

        int index = vh.currentVertCount;
        vh.AddVert(centre + forward * 8f, phosphor, Vector2.zero);
        vh.AddVert(centre - forward * 5f + side * 5f, phosphor, Vector2.zero);
        vh.AddVert(centre - forward * 5f - side * 5f, phosphor, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
    }

    private void DrawFrame(VertexHelper vh, Rect bounds)
    {
        Room room = floor != null ? floor.CurrentRoom : null;
        bool alert = room != null && room.IsLocked;

        Outline(vh, bounds, 1f, alert && Blink() ? hostile : Fade(phosphor, 0.5f));

        // Heavier corner brackets.
        Color bracket = alert ? hostile : phosphor;
        const float length = 14f;
        const float thickness = 3f;

        AddRect(vh, new Rect(bounds.xMin, bounds.yMax - thickness, length, thickness), bracket);
        AddRect(vh, new Rect(bounds.xMin, bounds.yMax - length, thickness, length), bracket);
        AddRect(vh, new Rect(bounds.xMax - length, bounds.yMax - thickness, length, thickness), bracket);
        AddRect(vh, new Rect(bounds.xMax - thickness, bounds.yMax - length, thickness, length), bracket);
        AddRect(vh, new Rect(bounds.xMin, bounds.yMin, length, thickness), bracket);
        AddRect(vh, new Rect(bounds.xMin, bounds.yMin, thickness, length), bracket);
        AddRect(vh, new Rect(bounds.xMax - length, bounds.yMin, length, thickness), bracket);
        AddRect(vh, new Rect(bounds.xMax - thickness, bounds.yMin, thickness, length), bracket);
    }

    // Debug: the detection rings (noises and sight range), cut off at the radar's edge.
    private void DrawDetectionRings(VertexHelper vh, Vector2 centre, Vector2 playerMap)
    {
        const int segments = 72;

        foreach (NoiseDebug.RingInfo ring in NoiseDebug.VisibleRings)
        {
            Vector2 middle = MapPoint(ring.Centre, centre, playerMap);
            float radius = ring.Radius * pixelsPerUnit;
            Vector2 previous = middle + new Vector2(radius, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 point = middle + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                if (InsideClip(previous, 1f) && InsideClip(point, 1f))
                    Line(vh, previous, point, 1.5f, ring.Colour);

                previous = point;
            }
        }
    }

    // ---------- Helpers ----------

    private Vector2 MapPoint(Vector3 world, Vector2 centre, Vector2 playerMap)
    {
        return MapPoint(new Vector2(world.x, world.z), centre, playerMap);
    }

    // World XZ to radar space.
    private Vector2 MapPoint(Vector2 world, Vector2 centre, Vector2 playerMap)
    {
        return centre + world * pixelsPerUnit - playerMap;
    }

    private Rect MapRect(Rect world, Vector2 centre, Vector2 playerMap)
    {
        Vector2 min = MapPoint(world.min, centre, playerMap);
        Vector2 max = MapPoint(world.max, centre, playerMap);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // A strip along an outline edge, on the inside of the room.
    private Rect EdgeRect(RoomEdge edge, float thickness, Vector2 centre, Vector2 playerMap)
    {
        Vector2 a = MapPoint(edge.From, centre, playerMap);
        Vector2 b = MapPoint(edge.To, centre, playerMap);
        Rect line = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        if (edge.Outward.x > 0) return Rect.MinMaxRect(line.xMin - thickness, line.yMin, line.xMin, line.yMax);
        if (edge.Outward.x < 0) return Rect.MinMaxRect(line.xMin, line.yMin, line.xMin + thickness, line.yMax);
        if (edge.Outward.y > 0) return Rect.MinMaxRect(line.xMin, line.yMin - thickness, line.xMax, line.yMin);
        return Rect.MinMaxRect(line.xMin, line.yMin, line.xMax, line.yMin + thickness);
    }

    private bool InsideClip(Vector2 point, float margin)
    {
        return point.x >= clip.xMin + margin && point.x <= clip.xMax - margin
            && point.y >= clip.yMin + margin && point.y <= clip.yMax - margin;
    }

    private static bool Blink()
    {
        return Mathf.Repeat(Time.time * 4f, 1f) < 0.5f;
    }

    // Axis-aligned rect, clipped to the minimap bounds.
    private void AddRect(VertexHelper vh, Rect rect, Color colour)
    {
        float xMin = Mathf.Max(rect.xMin, clip.xMin);
        float yMin = Mathf.Max(rect.yMin, clip.yMin);
        float xMax = Mathf.Min(rect.xMax, clip.xMax);
        float yMax = Mathf.Min(rect.yMax, clip.yMax);

        if (xMin >= xMax || yMin >= yMax)
            return;

        FillRect(vh, Rect.MinMaxRect(xMin, yMin, xMax, yMax), colour);
    }

    private void Outline(VertexHelper vh, Rect rect, float thickness, Color colour)
    {
        AddRect(vh, new Rect(rect.xMin, rect.yMin, rect.width, thickness), colour);
        AddRect(vh, new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), colour);
        AddRect(vh, new Rect(rect.xMin, rect.yMin, thickness, rect.height), colour);
        AddRect(vh, new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), colour);
    }
}
