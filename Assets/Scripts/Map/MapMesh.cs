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
