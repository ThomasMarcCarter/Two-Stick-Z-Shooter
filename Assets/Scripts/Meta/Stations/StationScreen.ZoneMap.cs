using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The truck bay's zone map: a schematic CRT map of the region in place of a list. One road comes
// in from the west and runs through the four zones in the order they open (Military Base →
// Shopping Mall → Hospital → Airport), past a river, the town blocks round the Mall and Hospital,
// and the coast by the Airport's runways. Each zone is a dot with a selectable name box under it:
// open zones in green, locked ones in red with the road to them dashed. The zone the survivors
// are parked in is ringed in amber with the hideout's house over it. The highlighted zone (or
// the current one, with none highlighted) gets corner brackets, its survivor, goal and lock
// reason show in the readout under the map, and while the truck runs the road from here to it
// marches in amber. Left/right (or the mouse) moves between zones, past the Airport to CLOSE.
public partial class StationScreen
{
    private const float MapHeight = 330f;
    private static readonly Vector2 MarkerSize = new Vector2(170f, 40f);

    // Where things sit on the map, as fractions of its width and height (y up).
    private static readonly Vector2[] ZonePoints =
    {
        new Vector2(0.24f, 0.30f),   // Military Base
        new Vector2(0.46f, 0.66f),   // Shopping Mall
        new Vector2(0.68f, 0.32f),   // Hospital
        new Vector2(0.88f, 0.70f),   // Airport
    };

    // The road into each zone from the one before it (the first from off the map), corners only.
    private static readonly Vector2[][] Roads =
    {
        new[] { new Vector2(0f, 0.30f), ZonePoints[0] },
        new[] { ZonePoints[0], new Vector2(0.34f, 0.30f), new Vector2(0.34f, 0.66f), ZonePoints[1] },
        new[] { ZonePoints[1], new Vector2(0.58f, 0.66f), new Vector2(0.58f, 0.32f), ZonePoints[2] },
        new[] { ZonePoints[2], new Vector2(0.76f, 0.32f), new Vector2(0.76f, 0.70f), ZonePoints[3] },
    };

    private static readonly Vector2[] River =
    {
        new Vector2(0.42f, 1f), new Vector2(0.40f, 0.85f), new Vector2(0.43f, 0.72f), new Vector2(0.41f, 0.55f),
        new Vector2(0.44f, 0.40f), new Vector2(0.42f, 0.20f), new Vector2(0.45f, 0f),
    };

    private static readonly Vector2[] Coast =
    {
        new Vector2(0.62f, 1f), new Vector2(0.66f, 0.92f), new Vector2(0.74f, 0.90f), new Vector2(0.80f, 0.95f),
        new Vector2(0.90f, 0.88f), new Vector2(1f, 0.90f),
    };

    private RectTransform zoneMap;
    private TMP_Text zoneReadout;
    private readonly List<CrtButton> zoneMarkers = new List<CrtButton>();
    private readonly List<Rect> townBlocks = new List<Rect>();
    private int highlightedZone = -1;

    private void BuildZoneMap()
    {
        if (zoneMap == null)
            CreateZoneMap();

        zoneMap.gameObject.SetActive(true);
        zoneReadout.gameObject.SetActive(true);

        foreach (CrtButton row in rows)
            row.gameObject.SetActive(false);

        // Map, readout, close button, then the stats and status lines at the bottom.
        const float top = 109f;
        float readoutY = top + MapHeight + 32f;
        float closeY = readoutY + 52f;
        zoneMap.anchoredPosition = new Vector2(0f, -top);
        zoneReadout.rectTransform.anchoredPosition = new Vector2(0f, -readoutY);
        closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -closeY);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelWidth, closeY + 40f + 90f);

        // One column per zone, left to right, so left/right walks the road; CLOSE comes last.
        CrtButton[][] columns = new CrtButton[zoneMarkers.Count + 1][];

        for (int i = 0; i < zoneMarkers.Count; i++)
            columns[i] = new[] { zoneMarkers[i] };

        columns[zoneMarkers.Count] = new[] { closeButton };
        menu = new CrtMenu(columns, (int)MetaProgress.ChosenZone, 0);
        highlightedZone = -1;
    }

    private void HideZoneMap()
    {
        if (zoneMap == null)
            return;

        zoneMap.gameObject.SetActive(false);
        zoneReadout.gameObject.SetActive(false);
    }

    private void CreateZoneMap()
    {
        zoneMap = CrtPanel.MakeRect(panel.transform, "Zone Map", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
            new Vector2(RowWidth, MapHeight));
        zoneMap.gameObject.AddComponent<CanvasRenderer>();
        CallbackGraphic graphic = zoneMap.gameObject.AddComponent<CallbackGraphic>();
        graphic.Draw = DrawZoneMap;

        Vector2 size = zoneMap.sizeDelta;
        Color dim = UiColours.WithAlpha(Phosphor, 0.55f);

        for (int i = 0; i < Zones.All.Length; i++)
        {
            RectTransform rect = CrtPanel.MakeRect(zoneMap, Zones.Name(Zones.All[i]), Vector2.zero, new Vector2(0.5f, 0.5f),
                MarkerCentre(i, size), MarkerSize);
            zoneMarkers.Add(CrtPanel.MakeButton(rect, font, 15f, 3f, TextAlignmentOptions.Center, 4f, 0.08f, 0.4f));
        }

        // Fixed text on the map: the compass, the scale and the sea.
        MapText("N", new Vector2(0.035f, 0.80f) * size, 14f, dim);
        MapText("5 KM", new Vector2(0.93f, 0.05f) * size + new Vector2(0f, 10f), 10f, dim);
        MapText("~ SEA ~", new Vector2(0.86f, 0.96f) * size, 11f, UiColours.WithAlpha(Phosphor, 0.35f));
        MapText("REGION 7 // GRID 41-C", new Vector2(0.16f, 0.95f) * size, 10f, UiColours.WithAlpha(Phosphor, 0.35f));

        zoneReadout = MakeText("Zone Readout", "", 16, Vector2.zero, 1f, new Vector2(RowWidth, 44f), Phosphor, 4f, TextAlignmentOptions.Left);
        zoneReadout.lineSpacing = 6f;

        // Town blocks round the Mall and the Hospital, the same every time.
        System.Random random = new System.Random(41);

        foreach (int town in new[] { 1, 2 })
        {
            for (int i = 0; i < 14; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = 0.05f + (float)random.NextDouble() * 0.09f;
                Vector2 centre = ZonePoints[town] + new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance * 1.6f);
                Vector2 block = new Vector2(0.015f + (float)random.NextDouble() * 0.025f, 0.03f + (float)random.NextDouble() * 0.05f);
                townBlocks.Add(new Rect(centre - block / 2f, block));
            }
        }
    }

    private void MapText(string text, Vector2 position, float size, Color colour)
    {
        RectTransform rect = CrtPanel.MakeRect(zoneMap, text, Vector2.zero, new Vector2(0.5f, 0.5f), position, new Vector2(200f, 20f));
        CrtPanel.MakeLabel(rect, font, text, size, colour, 3f, TextAlignmentOptions.Center);
    }

    // Name boxes sit just under their dot.
    private static Vector2 MarkerCentre(int zone, Vector2 size)
    {
        return ZonePoints[zone] * size + new Vector2(0f, -34f);
    }

    // Each zone's name box: its name over OPEN / LOCKED / LAST RUN.
    private void RefreshZoneMap()
    {
        for (int i = 0; i < Zones.All.Length; i++)
        {
            Zone zone = Zones.All[i];
            string state = !Zones.IsUnlocked(zone) ? $"<color={UiColours.DangerHex}>LOCKED</color>"
                : zone == MetaProgress.ChosenZone ? $"<color={UiColours.AmberHex}>OPEN // LAST RUN</color>"
                : $"<color={UiColours.AmberHex}>OPEN</color>";
            zoneMarkers[i].Label.text = $"{Zones.Name(zone)}\n<size=70%>{state}</size>";
        }

        RefreshZoneReadout();
    }

    // The zone under the highlight (or the last run's, with nothing highlighted) in the readout.
    private void UpdateZoneHighlight()
    {
        int highlighted = menu.Highlighted < Zones.All.Length ? menu.Highlighted : -1;

        if (highlighted == highlightedZone)
            return;

        highlightedZone = highlighted;
        RefreshZoneReadout();
    }

    private int ShownZone => highlightedZone >= 0 ? highlightedZone : (int)MetaProgress.ChosenZone;

    private void RefreshZoneReadout()
    {
        zoneReadout.text = ZoneText(Zones.All[ShownZone]);
    }

    // ---------- Drawing ----------

    private void DrawZoneMap(VertexHelper vh, Rect rect)
    {
        float time = Time.unscaledTime;
        int shown = ShownZone;
        int here = (int)MetaProgress.ChosenZone;

        DrawTerrain(vh, rect);
        DrawRoads(vh, rect, here, shown, time);
        DrawZoneMarkers(vh, rect, here, shown, time);
        DrawMapFurniture(vh, rect, time);
    }

    // A point given as a fraction of the map, in the mesh's pixels.
    private static Vector2 MapPoint(Rect rect, Vector2 point) => new Vector2(rect.xMin + point.x * rect.width, rect.yMin + point.y * rect.height);

    private static List<Vector2> MapPoints(Rect rect, IList<Vector2> points)
    {
        List<Vector2> mapped = new List<Vector2>(points.Count);

        foreach (Vector2 point in points)
            mapped.Add(MapPoint(rect, point));

        return mapped;
    }

    // The land under the roads.
    private void DrawTerrain(VertexHelper vh, Rect rect)
    {
        // Grid and frame.
        Color grid = UiColours.WithAlpha(Phosphor, 0.06f);

        for (float x = rect.xMin + 41f; x < rect.xMax; x += 41f)
            MapMesh.Line(vh, new Vector2(x, rect.yMin), new Vector2(x, rect.yMax), 1f, grid);

        for (float y = rect.yMin + 41f; y < rect.yMax; y += 41f)
            MapMesh.Line(vh, new Vector2(rect.xMin, y), new Vector2(rect.xMax, y), 1f, grid);

        MapMesh.OutlineRect(vh, rect, 1.5f, UiColours.WithAlpha(Phosphor, 0.45f));

        // Sea, coast and river.
        List<Vector2> sea = MapPoints(rect, Coast);
        sea.Add(MapPoint(rect, new Vector2(1f, 1f)));
        sea.Add(MapPoint(rect, new Vector2(Coast[0].x, 1f)));
        MapMesh.Polygon(vh, sea, UiColours.WithAlpha(Phosphor, 0.05f));
        MapMesh.Polyline(vh, MapPoints(rect, Coast), 1.5f, UiColours.WithAlpha(Phosphor, 0.45f), false);
        MapMesh.Polyline(vh, MapPoints(rect, River), 4f, UiColours.WithAlpha(Phosphor, 0.12f), false);
        MapMesh.Polyline(vh, MapPoints(rect, River), 1.5f, UiColours.WithAlpha(Phosphor, 0.3f), false);

        // Town blocks, the base's fence and the airport's runways.
        foreach (Rect block in townBlocks)
            MapMesh.OutlineRect(vh, new Rect(MapPoint(rect, block.min), block.size * rect.size), 1f, UiColours.WithAlpha(Phosphor, 0.16f));

        Vector2 fence = new Vector2(0.10f, 0.20f) * rect.size;
        MapMesh.DashedOutlineRect(vh, new Rect(MapPoint(rect, ZonePoints[0]) - fence / 2f, fence), 1f, 4f, UiColours.WithAlpha(Phosphor, 0.3f));
        MapMesh.Line(vh, MapPoint(rect, new Vector2(0.81f, 0.83f)), MapPoint(rect, new Vector2(0.97f, 0.76f)), 5f, UiColours.WithAlpha(Phosphor, 0.14f));
        MapMesh.Line(vh, MapPoint(rect, new Vector2(0.83f, 0.58f)), MapPoint(rect, new Vector2(0.97f, 0.64f)), 5f, UiColours.WithAlpha(Phosphor, 0.14f));
    }

    // Roads: solid to open zones, dashed red to locked ones; the road from here to the zone in the
    // readout marches in amber while the truck can drive it.
    private void DrawRoads(VertexHelper vh, Rect rect, int here, int shown, float time)
    {
        bool route = TruckBay.Running && shown != here && Zones.IsUnlocked(Zones.All[shown]);

        for (int i = 0; i < Roads.Length; i++)
        {
            List<Vector2> road = MapPoints(rect, Roads[i]);

            if (!Zones.IsUnlocked(Zones.All[i]))
            {
                for (int j = 0; j < road.Count - 1; j++)
                    MapMesh.DashedLine(vh, road[j], road[j + 1], 2f, 5f, UiColours.WithAlpha(UiColours.Danger, 0.45f));
            }
            else
            {
                MapMesh.Polyline(vh, road, 2.5f, UiColours.WithAlpha(Phosphor, 0.5f), false);

                if (route && i > Mathf.Min(here, shown) && i <= Mathf.Max(here, shown))
                    MarchingLine(vh, road, 3f, 8f, time * 30f, UiColours.Amber);
            }
        }
    }

    // The zones' markers, drawn over the roads.
    private void DrawZoneMarkers(VertexHelper vh, Rect rect, int here, int shown, float time)
    {
        // Dark boxes behind the zone names so roads and blocks don't show through.
        for (int i = 0; i < ZonePoints.Length; i++)
            MapMesh.FillRect(vh, new Rect(MarkerCentre(i, rect.size) + rect.min - MarkerSize / 2f, MarkerSize), Background);

        // The hideout, an amber house over the zone it's in.
        Vector2 home = MapPoint(rect, ZonePoints[here]) + new Vector2(0f, 24f);
        MapMesh.FillRect(vh, new Rect(home - new Vector2(6f, 6f), new Vector2(12f, 9f)), UiColours.Amber);
        MapMesh.Triangle(vh, home + new Vector2(-8f, 3f), home + new Vector2(0f, 10f), home + new Vector2(8f, 3f), UiColours.Amber);

        // Zone dots: filled green when open, a red cross when locked, an amber ring on the zone
        // the survivors are in and corner brackets round the one in the readout.
        for (int i = 0; i < ZonePoints.Length; i++)
        {
            Vector2 dot = MapPoint(rect, ZonePoints[i]);
            Zone zone = Zones.All[i];

            if (Zones.IsUnlocked(zone))
            {
                MapMesh.FillRect(vh, new Rect(dot - new Vector2(5f, 5f), new Vector2(10f, 10f)), Phosphor);
            }
            else
            {
                MapMesh.OutlineRect(vh, new Rect(dot - new Vector2(6f, 6f), new Vector2(12f, 12f)), 1.5f, UiColours.Danger);
                MapMesh.Line(vh, dot + new Vector2(-4f, -4f), dot + new Vector2(4f, 4f), 1.5f, UiColours.Danger);
                MapMesh.Line(vh, dot + new Vector2(-4f, 4f), dot + new Vector2(4f, -4f), 1.5f, UiColours.Danger);
            }

            if (zone == MetaProgress.ChosenZone)
                MapMesh.Ring(vh, dot, 13f + Mathf.Sin(time * 4f) * 2f, 1.5f, UiColours.WithAlpha(UiColours.Amber, 0.8f));

            if (i == shown)
                Brackets(vh, dot, 20f, Color.white);
        }
    }

    // The map's scan line, compass and scale bar.
    private void DrawMapFurniture(VertexHelper vh, Rect rect, float time)
    {
        // A slow scan line sweeping across the map.
        float sweep = rect.xMin + Mathf.Repeat(time * 0.12f, 1f) * rect.width;
        MapMesh.Line(vh, new Vector2(sweep, rect.yMin), new Vector2(sweep, rect.yMax), 2f, UiColours.WithAlpha(Phosphor, 0.08f));

        // Compass arrow under the N, and the scale bar under "5 KM".
        Vector2 north = MapPoint(rect, new Vector2(0.035f, 0.80f));
        MapMesh.Triangle(vh, north + new Vector2(-5f, -26f), north + new Vector2(0f, -12f), north + new Vector2(5f, -26f), UiColours.WithAlpha(Phosphor, 0.55f));
        MapMesh.Line(vh, north + new Vector2(0f, -40f), north + new Vector2(0f, -14f), 1.5f, UiColours.WithAlpha(Phosphor, 0.55f));
        Vector2 scale = MapPoint(rect, new Vector2(0.93f, 0.05f));
        MapMesh.Line(vh, scale - new Vector2(30f, 0f), scale + new Vector2(30f, 0f), 1.5f, UiColours.WithAlpha(Phosphor, 0.55f));
        MapMesh.Line(vh, scale - new Vector2(30f, 4f), scale - new Vector2(30f, -4f), 1.5f, UiColours.WithAlpha(Phosphor, 0.55f));
        MapMesh.Line(vh, scale + new Vector2(30f, -4f), scale + new Vector2(30f, 4f), 1.5f, UiColours.WithAlpha(Phosphor, 0.55f));
    }

    // Dashes running along a polyline, shifted by `offset` pixels so they march when it grows.
    private static void MarchingLine(VertexHelper vh, List<Vector2> points, float thickness, float dash, float offset, Color colour)
    {
        float phase = Mathf.Repeat(offset, dash * 2f) - dash * 2f;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 from = points[i];
            Vector2 to = points[i + 1];
            float length = Vector2.Distance(from, to);
            Vector2 direction = (to - from) / Mathf.Max(length, 0.0001f);

            for (float start = phase; start < length; start += dash * 2f)
            {
                float a = Mathf.Max(start, 0f);
                float b = Mathf.Min(start + dash, length);

                if (b > a)
                    MapMesh.Line(vh, from + direction * a, from + direction * b, thickness, colour);
            }

            // Carry the dash pattern round the corner.
            phase = Mathf.Repeat(phase - length, dash * 2f) - dash * 2f;
        }
    }

    // Four corner brackets round a point, like a targeting box.
    private static void Brackets(VertexHelper vh, Vector2 centre, float size, Color colour)
    {
        float h = size / 2f;
        const float arm = 6f;

        foreach (Vector2 corner in new[] { new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f) })
        {
            Vector2 c = centre + corner * h;
            MapMesh.Line(vh, c, c - new Vector2(corner.x * arm, 0f), 1.5f, colour);
            MapMesh.Line(vh, c, c - new Vector2(0f, corner.y * arm), 1.5f, colour);
        }
    }
}
