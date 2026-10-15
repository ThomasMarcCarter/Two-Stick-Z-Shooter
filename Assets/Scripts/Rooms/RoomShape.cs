using System.Collections.Generic;
using UnityEngine;

// A room footprint on the floor grid, as a set of cells. Rooms can be 1x1, long, L, T or 2x2.
public class RoomShape
{
    public string Name { get; }
    public Vector2Int[] Cells { get; }
    public float Weight { get; }

    private RoomShape(string name, Vector2Int[] cells, float weight)
    {
        Name = name;
        Cells = cells;
        Weight = weight;
    }

    public static readonly RoomShape Single = new RoomShape("1x1", new[] { Vector2Int.zero }, 6f);

    // Every shape with its rotations. Weights are per entry, so a shape's total chance is split
    // across its rotations.
    public static readonly IReadOnlyList<RoomShape> All = BuildCatalogue();

    private static List<RoomShape> BuildCatalogue()
    {
        List<RoomShape> shapes = new List<RoomShape>
        {
            Single,
            new RoomShape("2x1", Offsets((0, 0), (1, 0)), 1.2f),
            new RoomShape("1x2", Offsets((0, 0), (0, 1)), 1.2f),
            new RoomShape("2x2", Offsets((0, 0), (1, 0), (0, 1), (1, 1)), 0.6f)
        };

        AddRotations(shapes, "L", Offsets((0, 0), (1, 0), (0, 1)), 2f);
        AddRotations(shapes, "T", Offsets((0, 0), (1, 0), (2, 0), (1, 1)), 1.2f);

        return shapes;
    }

    private static void AddRotations(List<RoomShape> shapes, string name, Vector2Int[] cells, float totalWeight)
    {
        Vector2Int[] current = cells;

        for (int i = 0; i < 4; i++)
        {
            shapes.Add(new RoomShape(name, Normalise(current), totalWeight / 4f));
            current = Rotate(current);
        }
    }

    // 90 degrees anticlockwise.
    private static Vector2Int[] Rotate(Vector2Int[] cells)
    {
        Vector2Int[] rotated = new Vector2Int[cells.Length];

        for (int i = 0; i < cells.Length; i++)
            rotated[i] = new Vector2Int(-cells[i].y, cells[i].x);

        return rotated;
    }

    // Shift so the lowest x and y are 0.
    private static Vector2Int[] Normalise(Vector2Int[] cells)
    {
        Vector2Int min = cells[0];

        foreach (Vector2Int cell in cells)
            min = Vector2Int.Min(min, cell);

        Vector2Int[] normalised = new Vector2Int[cells.Length];

        for (int i = 0; i < cells.Length; i++)
            normalised[i] = cells[i] - min;

        return normalised;
    }

    private static Vector2Int[] Offsets(params (int x, int y)[] cells)
    {
        Vector2Int[] result = new Vector2Int[cells.Length];

        for (int i = 0; i < cells.Length; i++)
            result[i] = new Vector2Int(cells[i].x, cells[i].y);

        return result;
    }
}
