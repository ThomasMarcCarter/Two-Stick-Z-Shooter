using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Mesh-building helpers shared by the radar minimap and the fullscreen tactical map.
public static class MapMesh
{
    public static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color colour)
    {
        int index = vh.currentVertCount;

        vh.AddVert(a, colour, Vector2.zero);
        vh.AddVert(b, colour, Vector2.zero);
        vh.AddVert(c, colour, Vector2.zero);
        vh.AddVert(d, colour, Vector2.zero);

        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    public static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color colour)
    {
        int index = vh.currentVertCount;

        vh.AddVert(a, colour, Vector2.zero);
        vh.AddVert(b, colour, Vector2.zero);
        vh.AddVert(c, colour, Vector2.zero);

        vh.AddTriangle(index, index + 1, index + 2);
    }

    public static void FillRect(VertexHelper vh, Rect rect, Color colour)
    {
        Quad(vh,
            new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax),
            new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin),
            colour);
    }

    public static void OutlineRect(VertexHelper vh, Rect rect, float thickness, Color colour)
    {
        FillRect(vh, new Rect(rect.xMin, rect.yMin, rect.width, thickness), colour);
        FillRect(vh, new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), colour);
        FillRect(vh, new Rect(rect.xMin, rect.yMin + thickness, thickness, rect.height - thickness * 2f), colour);
        FillRect(vh, new Rect(rect.xMax - thickness, rect.yMin + thickness, thickness, rect.height - thickness * 2f), colour);
    }

    public static void DashedOutlineRect(VertexHelper vh, Rect rect, float thickness, float dash, Color colour)
    {
        DashedLine(vh, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), thickness, dash, colour);
        DashedLine(vh, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMax), thickness, dash, colour);
        DashedLine(vh, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax), thickness, dash, colour);
        DashedLine(vh, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), thickness, dash, colour);
    }

    public static void DashedLine(VertexHelper vh, Vector2 from, Vector2 to, float thickness, float dash, Color colour)
    {
        float length = Vector2.Distance(from, to);
        Vector2 direction = (to - from) / Mathf.Max(length, 0.0001f);

        for (float start = 0f; start < length; start += dash * 2f)
        {
            float end = Mathf.Min(start + dash, length);
            Line(vh, from + direction * start, from + direction * end, thickness, colour);
        }
    }

    public static void Line(VertexHelper vh, Vector2 from, Vector2 to, float thickness, Color colour)
    {
        Vector2 direction = (to - from).normalized;
        Vector2 side = new Vector2(-direction.y, direction.x) * thickness / 2f;

        Quad(vh, from - side, from + side, to + side, to - side, colour);
    }

    // Joined line segments through the points, closed back to the first one if asked.
    public static void Polyline(VertexHelper vh, IList<Vector2> points, float thickness, Color colour, bool closed)
    {
        int count = closed ? points.Count : points.Count - 1;

        for (int i = 0; i < count; i++)
        {
            Vector2 from = points[i];
            Vector2 to = points[(i + 1) % points.Count];
            Vector2 direction = (to - from).normalized * thickness * 0.5f;
            Line(vh, from - direction, to + direction, thickness, colour);   // overlap the joints so corners stay solid
        }
    }

    // A filled simple polygon (convex or not), split into triangles by ear clipping.
    public static void Polygon(VertexHelper vh, IList<Vector2> points, Color colour)
    {
        int count = points.Count;

        if (count < 3)
            return;

        float area = 0f;

        for (int i = 0; i < count; i++)
            area += Cross(points[i], points[(i + 1) % count]);

        List<int> remaining = new List<int>(count);

        for (int i = 0; i < count; i++)
            remaining.Add(area >= 0f ? i : count - 1 - i);   // walk anticlockwise

        int guard = count * count;

        while (remaining.Count > 3 && guard-- > 0)
        {
            bool clipped = false;

            for (int i = 0; i < remaining.Count; i++)
            {
                Vector2 a = points[remaining[(i + remaining.Count - 1) % remaining.Count]];
                Vector2 b = points[remaining[i]];
                Vector2 c = points[remaining[(i + 1) % remaining.Count]];

                if (Cross(b - a, c - b) <= 0f || AnyInside(points, remaining, a, b, c))
                    continue;

                Triangle(vh, a, b, c, colour);
                remaining.RemoveAt(i);
                clipped = true;
                break;
            }

            if (!clipped)
                break;   // degenerate outline: fill what's left as a fan below
        }

        for (int i = 1; i < remaining.Count - 1; i++)
            Triangle(vh, points[remaining[0]], points[remaining[i]], points[remaining[i + 1]], colour);
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private static bool AnyInside(IList<Vector2> points, List<int> remaining, Vector2 a, Vector2 b, Vector2 c)
    {
        foreach (int index in remaining)
        {
            Vector2 p = points[index];

            if (p == a || p == b || p == c)
                continue;

            if (Cross(b - a, p - a) >= 0f && Cross(c - b, p - b) >= 0f && Cross(a - c, p - c) >= 0f)
                return true;
        }

        return false;
    }

    public static void Ring(VertexHelper vh, Vector2 centre, float radius, float thickness, Color colour)
    {
        const int segments = 48;

        for (int i = 0; i < segments; i++)
        {
            Vector2 d0 = Direction(i * 360f / segments);
            Vector2 d1 = Direction((i + 1) * 360f / segments);

            Quad(vh,
                centre + d0 * radius, centre + d0 * (radius - thickness),
                centre + d1 * (radius - thickness), centre + d1 * radius,
                colour);
        }
    }

    public static Color Fade(Color colour, float alpha)
    {
        colour.a *= alpha;
        return colour;
    }

    public static Vector2 Direction(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }
}
