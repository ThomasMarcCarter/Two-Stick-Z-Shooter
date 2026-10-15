using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// A wireframe-style drawing for the HUD and inventory: a list of outlines in the drawing's own
// coordinates (y down, like the reference images they're traced from), fitted and centred into
// whatever rect it's drawn in. Solid parts get a faint fill under a bright outline; holes and
// slots are outline only; wires are open lines. Built once as read-only data.
public sealed class LineArt
{
    private struct Part
    {
        public Vector2[] Points;
        public bool Fill;
        public bool Closed;
    }

    private readonly Rect source;
    private readonly List<Part> parts = new List<Part>();

    public LineArt(float xMin, float yMin, float xMax, float yMax)
    {
        source = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    // A solid part: x0, y0, x1, y1, ...
    public LineArt Shape(params float[] xy) => Add(xy, true, true);

    // An outline with no fill (a hole, port or slot).
    public LineArt Hole(params float[] xy) => Add(xy, false, true);

    // An open line.
    public LineArt Wire(params float[] xy) => Add(xy, false, false);

    public LineArt Box(float x0, float y0, float x1, float y1) => Shape(x0, y0, x1, y0, x1, y1, x0, y1);

    public LineArt Circle(float cx, float cy, float radius, bool fill = true, int segments = 18)
    {
        float[] xy = new float[segments * 2];

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            xy[i * 2] = cx + Mathf.Cos(angle) * radius;
            xy[i * 2 + 1] = cy + Mathf.Sin(angle) * radius;
        }

        return Add(xy, fill, true);
    }

    private LineArt Add(float[] xy, bool fill, bool closed)
    {
        Vector2[] points = new Vector2[xy.Length / 2];

        for (int i = 0; i < points.Length; i++)
            points[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);

        parts.Add(new Part { Points = points, Fill = fill, Closed = closed });
        return this;
    }

    public void Draw(VertexHelper vh, Rect area, Color colour)
    {
        float scale = Mathf.Min(area.width / source.width, area.height / source.height);
        float thickness = Mathf.Clamp(Mathf.Min(area.width, area.height * 2f) / 100f, 1f, 1.6f);
        Color fill = Fade(colour, 0.18f);
        List<Vector2> mapped = new List<Vector2>(24);

        foreach (Part part in parts)
        {
            mapped.Clear();

            foreach (Vector2 p in part.Points)
                mapped.Add(area.center + new Vector2(p.x - source.center.x, source.center.y - p.y) * scale);

            if (part.Fill)
                Polygon(vh, mapped, fill);

            Polyline(vh, mapped, thickness, colour, part.Closed);
        }
    }
}
