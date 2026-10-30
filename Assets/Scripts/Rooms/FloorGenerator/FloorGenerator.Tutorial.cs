using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's training floor, played once when a new profile first starts: a spawn room,
// a treasure room to the east and a combat room to the north with a couple of walkers. Killing them
// opens a hatch back to the hideout. Tutorial shows the prompts. Nothing found here goes home.
public partial class FloorGenerator
{
    private const int TutorialZombies = 2;

    // The hatch's patch in the middle of the combat room, kept clear of furniture and loot.
    private const float TutorialHatchClearance = 3.2f;

    private bool tutorialRun;

    public bool IsTutorial => tutorialRun && FloorNumber == 1;

    // The floor's first room of a type, or null.
    public Room FirstRoom(RoomType type)
    {
        foreach (Room room in rooms)
        {
            if (room.Type == type)
                return room;
        }

        return null;
    }

    private static List<PlannedRoom> TutorialLayout()
    {
        return new List<PlannedRoom>
        {
            new PlannedRoom { Shape = RoomShape.Single, Cells = new List<Vector2Int> { Vector2Int.zero }, Type = RoomType.Start, Children = 2 },
            TutorialRoom(RoomType.Treasure, Vector2Int.right),
            TutorialRoom(RoomType.Normal, Vector2Int.up)
        };
    }

    private static PlannedRoom TutorialRoom(RoomType type, Vector2Int direction)
    {
        return new PlannedRoom
        {
            Shape = RoomShape.Single,
            Cells = new List<Vector2Int> { direction },
            Type = type,
            Parent = 0,
            Depth = 1,
            ParentCell = Vector2Int.zero,
            DoorDirection = direction
        };
    }

    private void ReserveTutorialHatch()
    {
        Room room = FirstRoom(RoomType.Normal);
        Vector3 centre = room.transform.position;
        float half = TutorialHatchClearance / 2f;
        AddSolidPoints(room, new Rect(centre.x - half, centre.z - half, TutorialHatchClearance, TutorialHatchClearance));
    }

    private void OnTutorialRoomCleared(Room room)
    {
        EnemyBullet.ClearAll();
        PlayRoomCleared();
        SpawnExit(room, FloorExit.Kind.Hideout);
    }
}
