using System;
using System.Collections.Generic;
using UnityEngine;

public enum RoomType
{
    Start,
    Normal,
    Treasure,
    Boss
}

// A door between two rooms, seen from one side: which of this room's cells it leaves from,
// in which direction, and the room on the other side.
public struct RoomConnection
{
    public Vector2Int Cell;
    public Vector2Int Direction;
    public Room Neighbour;
    public RoomDoor Door;
}

// One segment of a room's outline in world XZ, with the direction pointing out of the room.
public struct RoomEdge
{
    public Vector2 From;
    public Vector2 To;
    public Vector2 Outward;
}

// One room on the floor, covering one or more grid cells (1x1, long, L, T or 2x2). Built at
// runtime by FloorGenerator. When the player steps clear of the doors into an uncleared room,
// the doors lock until every enemy in it is dead.
public class Room : MonoBehaviour
{
    [Tooltip("How far past the door the player must be before it locks behind them.")]
    [SerializeField] private float lockMargin = 1f;

    // The cell the room was attached by; its position is the room's transform position.
    public Vector2Int GridPosition => cells[0];
    public RoomType Type { get; private set; }
    public string ShapeName { get; private set; }
    public bool Visited { get; private set; }

    // On the maps from the start (the Recon upgrade) without having been entered; it still
    // shows as unexplored until it is.
    public bool Scouted { get; set; }
    public bool IsLocked { get; private set; }
    public bool IsCleared { get; private set; }

    // A name for the maps instead of the room type (the hideout's areas), or null.
    public string DisplayName { get; set; }

    public IReadOnlyList<Vector2Int> Cells => cells;
    public IReadOnlyList<RoomConnection> Connections => connections;
    public IReadOnlyList<Enemy> Enemies => enemies;

    // Floor pieces and outline in world XZ, for the maps.
    public IReadOnlyList<Rect> Blocks => blocks;
    public IReadOnlyList<RoomEdge> Outline => outline;

    // The whole grid area the room covers, up to the doors. Used for the camera bounds.
    public Rect Bounds { get; private set; }

    // The floor of the room's first cell, in world XZ. Map markers sit relative to this.
    public Rect AnchorRect { get; private set; }

    public event Action<Room> PlayerEntered;
    public event Action<Room> Locked;
    public event Action<Room> Cleared;

    private readonly List<Vector2Int> cells = new List<Vector2Int>();
    private readonly HashSet<Vector2Int> cellSet = new HashSet<Vector2Int>();
    private readonly List<RoomConnection> connections = new List<RoomConnection>();
    private readonly List<Enemy> enemies = new List<Enemy>();
    private readonly List<Rect> blocks = new List<Rect>();
    private readonly List<RoomEdge> outline = new List<RoomEdge>();
    private Vector2 cellStep;

    // cells[0] is the anchor cell.
    public void Initialise(RoomType type, string shapeName, IEnumerable<Vector2Int> roomCells,
        Vector2 step, Vector2 floorSize)
    {
        Type = type;
        ShapeName = shapeName;
        cellStep = step;

        foreach (Vector2Int cell in roomCells)
        {
            cells.Add(cell);
            cellSet.Add(cell);
        }

        Vector2 min = Vector2.positiveInfinity;
        Vector2 max = Vector2.negativeInfinity;

        foreach (Vector2Int cell in cells)
        {
            Vector2 centre = Vector2.Scale(cell, step);
            min = Vector2.Min(min, centre - step / 2f);
            max = Vector2.Max(max, centre + step / 2f);
        }

        Bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

        Vector2 anchor = Vector2.Scale(cells[0], step);
        AnchorRect = new Rect(anchor - floorSize / 2f, floorSize);
    }

    public void SetGeometry(List<Rect> floorBlocks, List<RoomEdge> edges)
    {
        blocks.AddRange(floorBlocks);
        outline.AddRange(edges);
    }

    public bool Contains(Vector2Int cell) => cellSet.Contains(cell);

    public void AddConnection(RoomConnection connection)
    {
        connections.Add(connection);
    }

    public void AddEnemy(Enemy enemy)
    {
        enemies.Add(enemy);
        enemy.Died += OnEnemyDied;
    }

    public void SetDoorsOpen(bool open)
    {
        foreach (RoomConnection connection in connections)
        {
            connection.Door.SetOpen(open);
        }
    }

    // Shows the room on the maps without the player entering it. For the debug menu.
    public void MarkVisited()
    {
        Visited = true;
    }

    // Called by FloorGenerator when the player crosses into this room's area.
    public void NotifyPlayerEntered()
    {
        Visited = true;
        PlayerEntered?.Invoke(this);
    }

    // Called by FloorGenerator every frame the player is in this room.
    public void UpdatePlayer(Vector3 playerPosition)
    {
        if (IsCleared || IsLocked)
            return;

        // Wait until the player is clear of the door so it doesn't close on them.
        if (!IsWellInside(playerPosition))
            return;

        if (enemies.Count == 0)
        {
            MarkCleared();
            return;
        }

        IsLocked = true;
        SetDoorsOpen(false);
        Locked?.Invoke(this);
    }

    // Inside one of the room's cells and at least lockMargin from every edge of that cell that
    // leads out of the room (where the doors are). Edges shared with the room's own cells don't count.
    private bool IsWellInside(Vector3 worldPosition)
    {
        Vector2 position = new Vector2(worldPosition.x, worldPosition.z);
        Vector2Int cell = new Vector2Int(
            Mathf.RoundToInt(position.x / cellStep.x),
            Mathf.RoundToInt(position.y / cellStep.y));

        if (!Contains(cell))
            return false;

        Vector2 local = position - Vector2.Scale(cell, cellStep);

        if (!Contains(cell + Vector2Int.right) && cellStep.x / 2f - local.x < lockMargin) return false;
        if (!Contains(cell + Vector2Int.left) && cellStep.x / 2f + local.x < lockMargin) return false;
        if (!Contains(cell + Vector2Int.up) && cellStep.y / 2f - local.y < lockMargin) return false;
        if (!Contains(cell + Vector2Int.down) && cellStep.y / 2f + local.y < lockMargin) return false;

        return true;
    }

    private void OnEnemyDied(Enemy enemy)
    {
        enemies.Remove(enemy);

        if (enemies.Count == 0 && IsLocked)
        {
            IsLocked = false;
            SetDoorsOpen(true);
            MarkCleared();
        }
    }

    private void MarkCleared()
    {
        IsCleared = true;
        Cleared?.Invoke(this);
    }
}
