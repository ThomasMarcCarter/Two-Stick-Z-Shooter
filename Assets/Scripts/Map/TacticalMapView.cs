using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// The pannable map inside the fullscreen map screen. Draws every known room with its
// explored/cleared state, corridors, markers, the player, and the selected room.
// Clipping to the viewport comes from a RectMask2D on the parent.
public class TacticalMapView : MaskableGraphic
{
    [Header("References")]
    [SerializeField] private FloorGenerator floor;
    [SerializeField] private Transform player;

    [Header("Scale")]
    [SerializeField] private float pixelsPerUnit = 8f;
    [SerializeField] private float gridSpacing = 32f;

    // The map legend and the full-screen map's readout draw in the same colours.
    public Color Phosphor => UiColours.Phosphor;
    public Color Hostile => UiColours.Hostile;

    // Map-space offset of the world origin from the centre of the view, in pixels.
    public Vector2 Pan { get; set; }
    public Room Selected { get; set; }

    // The station under the cursor, drawn highlighted (its readout is in the side panel).
    public StationMarker HoveredStation { get; set; }

    private const float StationIconSize = 18f;

    private FloorExit hatch;

    private readonly Dictionary<Room, int> itemCounts = new Dictionary<Room, int>();
    private readonly Dictionary<Room, int> ammoCounts = new Dictionary<Room, int>();
    private readonly Dictionary<Room, int> medkitCounts = new Dictionary<Room, int>();
    private readonly List<Rect> blockBuffer = new List<Rect>();
    private readonly List<RoomEdge> edgeBuffer = new List<RoomEdge>();

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    private void Update()
    {
        // Redraw while visible so the selection pulse and player marker animate.
        if (Application.isPlaying)
            SetVerticesDirty();
    }

    public void CentreOn(Vector3 worldPosition)
    {
        Pan = -new Vector2(worldPosition.x, worldPosition.z) * pixelsPerUnit;
    }

    // Weapons, ammo and medkits lying in each room, for the markers in explored rooms.
    public void SetItemCounts(Dictionary<Room, int> weapons, Dictionary<Room, int> ammo, Dictionary<Room, int> medkits)
    {
        itemCounts.Clear();
        ammoCounts.Clear();
        medkitCounts.Clear();

        foreach (KeyValuePair<Room, int> entry in medkits)
            medkitCounts[entry.Key] = entry.Value;

        foreach (KeyValuePair<Room, int> entry in weapons)
            itemCounts[entry.Key] = entry.Value;

        foreach (KeyValuePair<Room, int> entry in ammo)
            ammoCounts[entry.Key] = entry.Value;
    }

    // GameInput.Pointer (already through the CRT mapping) to a point in this graphic's local
    // space. Returns whether it's inside the view.
    public bool PointerToLocal(Vector2 pointer, out Vector2 local)
    {
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, pointer, uiCamera, out local);
        return rectTransform.rect.Contains(local);
    }

    // The known room under a point in local space, judged by the room's floor.
    public Room RoomAt(Vector2 local)
    {
        if (floor == null)
            return null;

        Vector2 world = (local - rectTransform.rect.center - Pan) / pixelsPerUnit;

        foreach (Room room in floor.Rooms)
        {
            if (!floor.IsKnown(room))
                continue;

            foreach (Rect block in room.Blocks)
            {
                if (block.Contains(world))
                    return room;
            }
        }

        return null;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect bounds = rectTransform.rect;
        DrawGrid(vh, bounds);

        if (floor == null)
            return;

        foreach (Room room in floor.Rooms)
        {
            if (floor.IsKnown(room))
                DrawCorridors(vh, room);
        }

        foreach (Room room in floor.Rooms)
        {
            if (floor.IsKnown(room))
                DrawRoom(vh, room);
        }

        DrawStations(vh);
        DrawOffscreenMarkers(vh, bounds);

        if (player != null)
            DrawPlayerMarker(vh);

        OutlineRect(vh, bounds, 1.5f, Fade(UiColours.Phosphor, 0.6f));
    }

    private void DrawGrid(VertexHelper vh, Rect bounds)
    {
        Color gridColour = Fade(UiColours.Phosphor, 0.06f);
        Vector2 origin = bounds.center + Pan;

        for (float x = bounds.xMin + Mathf.Repeat(origin.x - bounds.xMin, gridSpacing); x < bounds.xMax; x += gridSpacing)
            FillRect(vh, new Rect(x - 0.5f, bounds.yMin, 1f, bounds.height), gridColour);

        for (float y = bounds.yMin + Mathf.Repeat(origin.y - bounds.yMin, gridSpacing); y < bounds.yMax; y += gridSpacing)
            FillRect(vh, new Rect(bounds.xMin, y - 0.5f, bounds.width, 1f), gridColour);
    }

    private void DrawRoom(VertexHelper vh, Room room)
    {
        ToMapSpace(room, blockBuffer, edgeBuffer);
        Rect anchor = MapRect(room.AnchorRect);
        MapRoomState state = MapSymbols.StateOf(room);

        // The hideout gets neither a status mark nor a spawn marker: its stations are its icons.
        MapSymbols.DrawRoomShape(vh, blockBuffer, edgeBuffer, anchor, state, UiColours.Phosphor, UiColours.Hostile, !floor.IsHideout);

        if (room == floor.CurrentRoom)
            MapSymbols.DrawEdges(vh, edgeBuffer, 3f, state == MapRoomState.Hostile ? UiColours.Hostile : UiColours.Phosphor);

        if (room == Selected)
        {
            // Brighter fill plus a pulsing frame just outside the room.
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
            MapSymbols.FillBlocks(vh, blockBuffer, Fade(UiColours.Phosphor, 0.15f));
            MapSymbols.DrawEdgesOutside(vh, edgeBuffer, 5f, 2f, Fade(UiColours.Phosphor, pulse));
        }

        float iconSize = Mathf.Min(anchor.width, anchor.height) * 0.22f;

        if (!floor.IsHideout)
            MapSymbols.DrawMarker(vh, room.Type, anchor.center, iconSize, UiColours.Phosphor, UiColours.Hostile);

        // Item markers along the bottom-left corner, only once the room has been explored:
        // "+" for weapons, a cartridge for ammo, then a medkit.
        if (room.Visited)
        {
            float size = Mathf.Clamp(anchor.height * 0.14f, 5f, 10f);
            Vector2 slot = new Vector2(anchor.xMin + size + 4f, anchor.yMin + size + 4f);

            if (itemCounts.TryGetValue(room, out int weapons) && weapons > 0)
            {
                MapSymbols.DrawItems(vh, slot, size, UiColours.Phosphor);
                slot.x += size * 1.8f;
            }

            if (ammoCounts.TryGetValue(room, out int ammo) && ammo > 0)
            {
                MapSymbols.DrawAmmo(vh, slot, size * 1.2f, UiColours.Phosphor);
                slot.x += size * 1.6f;
            }

            if (medkitCounts.TryGetValue(room, out int medkits) && medkits > 0)
                MapSymbols.DrawMedkit(vh, slot, size * 1.1f, UiColours.Phosphor);
        }
    }

    // The hideout's stations: an icon each, with a row of pips under it for its upgrade levels;
    // and its deploy hatch.
    private void DrawStations(VertexHelper vh)
    {
        if (floor.IsHideout)
        {
            if (hatch == null)
                hatch = FindAnyObjectByType<FloorExit>();

            if (hatch != null)
                MapSymbols.DrawHatch(vh, MapPoint(new Vector2(hatch.Position.x, hatch.Position.z)), StationIconSize, UiColours.Phosphor);
        }

        foreach (StationMarker marker in StationMarker.All)
        {
            Vector2 position = MapPoint(new Vector2(marker.Position.x, marker.Position.z));
            bool hovered = marker == HoveredStation;
            float size = hovered ? StationIconSize * 1.25f : StationIconSize;

            if (hovered)
                Ring(vh, position, size / 2f + 4f + Mathf.Sin(Time.unscaledTime * 5f) * 1.5f, 1.5f, Fade(UiColours.Phosphor, 0.7f));

            MapSymbols.DrawStation(vh, marker.Station, position, size, UiColours.Phosphor, marker.Abandoned, hovered);

            if (!marker.Abandoned)
            {
                // Under the icon, slid along as needed to stay inside the room's outline.
                Vector2 pips = position + Vector2.down * (size / 2f + 6f);

                if (floor.TryGetRoom(marker.Position, out Room room) && room.Blocks.Count > 0)
                {
                    Rect floorArea = room.Blocks[0];

                    foreach (Rect block in room.Blocks)
                        floorArea = Rect.MinMaxRect(Mathf.Min(floorArea.xMin, block.xMin), Mathf.Min(floorArea.yMin, block.yMin),
                            Mathf.Max(floorArea.xMax, block.xMax), Mathf.Max(floorArea.yMax, block.yMax));

                    Rect inside = MapRect(floorArea);
                    float half = (marker.MaxLevel * 6f - 2f) / 2f + 3f;
                    pips.x = Mathf.Clamp(pips.x, Mathf.Min(inside.xMin + half, inside.center.x), Mathf.Max(inside.xMax - half, inside.center.x));
                }

                MapSymbols.DrawLevelPips(vh, pips, marker.Level, marker.MaxLevel, 4f, Stations.Colour(marker.Station));
            }
        }
    }

    // The station whose icon is under a point in local space, or null.
    public StationMarker StationAt(Vector2 local)
    {
        StationMarker nearest = null;
        float best = StationIconSize * 0.75f;

        foreach (StationMarker marker in StationMarker.All)
        {
            float distance = Vector2.Distance(local, MapPoint(new Vector2(marker.Position.x, marker.Position.z)));

            if (distance < best)
            {
                best = distance;
                nearest = marker;
            }
        }

        return nearest;
    }

    // Special rooms panned out of view get pinned to the viewport edge, pointing at the room.
    private void DrawOffscreenMarkers(VertexHelper vh, Rect bounds)
    {
        const float edgeInset = 16f;

        foreach (Room room in floor.Rooms)
        {
            if (room.Type == RoomType.Normal || !floor.IsKnown(room) || floor.IsHideout)
                continue;

            if (MapSymbols.PinToEdge(bounds, edgeInset, MapRect(room.AnchorRect).center, out Vector2 pinned, out Vector2 direction))
                MapSymbols.DrawEdgeMarker(vh, room.Type, pinned, direction, 14f, UiColours.Phosphor, UiColours.Hostile);
        }
    }

    // Corridors running east or north only, so each one is drawn once.
    private void DrawCorridors(VertexHelper vh, Room room)
    {
        Color colour = Fade(UiColours.Phosphor, 0.5f);
        const float width = 4f;

        foreach (RoomConnection connection in room.Connections)
        {
            if (connection.Direction != Vector2Int.right && connection.Direction != Vector2Int.up)
                continue;

            if (!FloorGenerator.IsCorridorKnown(room, connection.Neighbour))
                continue;

            floor.GetCorridorEnds(connection, out Vector2 from, out Vector2 to);
            Vector2 a = MapPoint(from);
            Vector2 b = MapPoint(to);

            if (connection.Direction == Vector2Int.right)
                FillRect(vh, Rect.MinMaxRect(a.x, a.y - width / 2f, b.x, a.y + width / 2f), colour);
            else
                FillRect(vh, Rect.MinMaxRect(a.x - width / 2f, a.y, a.x + width / 2f, b.y), colour);
        }
    }

    private void DrawPlayerMarker(VertexHelper vh)
    {
        Vector2 position = MapPoint(new Vector2(player.position.x, player.position.z));
        Vector2 forward = new Vector2(player.forward.x, player.forward.z);

        float pulse = Mathf.Repeat(Time.unscaledTime, 1.2f) / 1.2f;
        Ring(vh, position, 6f + pulse * 26f, 1.5f, Fade(UiColours.Phosphor, (1f - pulse) * 0.6f));
        MapSymbols.DrawPlayer(vh, position, forward, 10f, UiColours.Phosphor);
    }

    private void ToMapSpace(Room room, List<Rect> blocks, List<RoomEdge> edges)
    {
        blocks.Clear();
        edges.Clear();

        foreach (Rect block in room.Blocks)
            blocks.Add(MapRect(block));

        foreach (RoomEdge edge in room.Outline)
            edges.Add(new RoomEdge { From = MapPoint(edge.From), To = MapPoint(edge.To), Outward = edge.Outward });
    }

    private Rect MapRect(Rect world)
    {
        Vector2 min = MapPoint(world.min);
        Vector2 max = MapPoint(world.max);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // World XZ to map space.
    private Vector2 MapPoint(Vector2 world)
    {
        return rectTransform.rect.center + Pan + world * pixelsPerUnit;
    }
}
