using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The FloorGenerator's layout step: grows a tree of rooms of different shapes on the cell grid
// from the start room, then picks which rooms are the boss and treasure rooms. Nothing is built
// here; FloorGenerator.Building.cs turns the plan into rooms.
public partial class FloorGenerator
{
    private List<PlannedRoom> GenerateLayout()
    {
        int target = rng.Next(minRooms, maxRooms + 1);

        for (int attempt = 0; attempt < 200; attempt++)
        {
            List<PlannedRoom> plan = TryGrowLayout(target);

            if (plan != null && AssignRoomTypes(plan))
                return plan;
        }

        Debug.LogWarning("FloorGenerator: couldn't build a valid layout, falling back to a single room.");

        return new List<PlannedRoom>
        {
            new PlannedRoom { Shape = RoomShape.Single, Cells = new List<Vector2Int> { Vector2Int.zero }, Type = RoomType.Start }
        };
    }

    // Isaac-style growth, one room at a time: a new room may only touch the room it grew from,
    // and only at the cell where the door goes. That keeps the floor branchy (a tree, no loops).
    private List<PlannedRoom> TryGrowLayout(int target)
    {
        List<PlannedRoom> plan = new List<PlannedRoom>
        {
            new PlannedRoom { Shape = RoomShape.Single, Cells = new List<Vector2Int> { Vector2Int.zero } }
        };

        Dictionary<Vector2Int, int> owner = new Dictionary<Vector2Int, int> { { Vector2Int.zero, 0 } };
        Queue<int> queue = new Queue<int>();
        queue.Enqueue(0);

        while (queue.Count > 0 && plan.Count < target)
        {
            int parentIndex = queue.Dequeue();
            List<Vector2Int> parentCells = new List<Vector2Int>(plan[parentIndex].Cells);
            Shuffle(parentCells);

            foreach (Vector2Int cell in parentCells)
            {
                foreach (Vector2Int direction in ShuffledDirections())
                {
                    if (plan.Count >= target)
                        break;

                    Vector2Int doorCell = cell + direction;

                    if (owner.ContainsKey(doorCell) || rng.NextDouble() < 0.5)
                        continue;

                    if (!TryPlaceRoom(PickShape(), cell, doorCell, owner, out List<Vector2Int> cells, out RoomShape shape))
                        continue;

                    PlannedRoom child = new PlannedRoom
                    {
                        Shape = shape,
                        Cells = cells,
                        Parent = parentIndex,
                        Depth = plan[parentIndex].Depth + 1,
                        ParentCell = cell,
                        DoorDirection = direction
                    };

                    plan[parentIndex].Children++;
                    plan.Add(child);

                    foreach (Vector2Int c in cells)
                        owner[c] = plan.Count - 1;

                    queue.Enqueue(plan.Count - 1);
                }
            }
        }

        return plan.Count >= target ? plan : null;
    }

    // Boss goes in the 1x1 dead end furthest from the start, treasure in another 1x1 dead end.
    private bool AssignRoomTypes(List<PlannedRoom> plan)
    {
        List<PlannedRoom> deadEnds = new List<PlannedRoom>();

        for (int i = 1; i < plan.Count; i++)
        {
            if (plan[i].Children == 0 && plan[i].Cells.Count == 1)
                deadEnds.Add(plan[i]);
        }

        if (deadEnds.Count < 2)
            return false;

        deadEnds.Sort((a, b) => b.Depth.CompareTo(a.Depth));

        plan[0].Type = RoomType.Start;
        deadEnds[0].Type = RoomType.Boss;
        deadEnds[rng.Next(1, deadEnds.Count)].Type = RoomType.Treasure;

        return true;
    }

    private RoomShape PickShape()
    {
        float total = 0f;

        foreach (RoomShape shape in RoomShape.All)
            total += shape.Weight;

        float roll = (float)rng.NextDouble() * total;

        foreach (RoomShape shape in RoomShape.All)
        {
            roll -= shape.Weight;

            if (roll <= 0f)
                return shape;
        }

        return RoomShape.Single;
    }

    private Vector2Int[] ShuffledDirections()
    {
        Vector2Int[] shuffled = (Vector2Int[])Directions.Clone();
        Shuffle(shuffled);
        return shuffled;
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // Tries the shape in every position that puts one of its cells on doorCell. Falls back to a
    // 1x1 room if the shape doesn't fit anywhere. The door cell always ends up as cells[0].
    private bool TryPlaceRoom(RoomShape shape, Vector2Int parentCell, Vector2Int doorCell,
        Dictionary<Vector2Int, int> owner, out List<Vector2Int> cells, out RoomShape placedShape)
    {
        foreach (RoomShape candidate in new[] { shape, RoomShape.Single })
        {
            List<int> anchors = new List<int>();

            for (int i = 0; i < candidate.Cells.Length; i++)
                anchors.Add(i);

            Shuffle(anchors);

            foreach (int anchor in anchors)
            {
                Vector2Int offset = doorCell - candidate.Cells[anchor];
                cells = new List<Vector2Int> { doorCell };

                foreach (Vector2Int c in candidate.Cells)
                {
                    if (c + offset != doorCell)
                        cells.Add(c + offset);
                }

                if (Fits(cells, parentCell, doorCell, owner))
                {
                    placedShape = candidate;
                    return true;
                }
            }
        }

        cells = null;
        placedShape = null;
        return false;
    }

    // Every cell is free, and the only place the new room touches anything is the door cell
    // against the parent cell.
    private static bool Fits(List<Vector2Int> cells, Vector2Int parentCell, Vector2Int doorCell,
        Dictionary<Vector2Int, int> owner)
    {
        foreach (Vector2Int cell in cells)
        {
            if (owner.ContainsKey(cell))
                return false;

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int neighbour = cell + direction;

                if (!owner.ContainsKey(neighbour))
                    continue;

                if (cell != doorCell || neighbour != parentCell)
                    return false;
            }
        }

        return true;
    }
}
