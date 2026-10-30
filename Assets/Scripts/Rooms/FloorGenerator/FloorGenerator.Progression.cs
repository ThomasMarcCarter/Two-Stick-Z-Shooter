using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The FloorGenerator's floor progression: what happens when a room is cleared (the chime, the
// combat room's drops, the boss room's way down and uplink terminal), and taking the way down
// to the next floor or the supply room. The zone's way down (ZoneExit) is set into an opening
// in the boss room's north wall, with a walled bay behind it for the longer ones.
public partial class FloorGenerator
{
    // How far north of the boss room's north wall the camera may look once the player's near it,
    // so the way down set into the wall shows whole (its sprites stand tall up the screen).
    private const float ExitCameraReach = 5f;

    // Where each boss room's way down is: the middle of its opening on the north wall's room face.
    private readonly Dictionary<Room, Vector3> exitSpots = new Dictionary<Room, Vector3>();

    // The hatch model's opening (square) and the height of its rim, where the doors hinge.
    private const float HatchOpening = 2.2f;
    private const float HatchRimHeight = 0.18f;

    // The boss is dead: the hatch down opens, and an uplink turns up to send materials home. Not
    // on the last floor: its hatch goes down to the supply room, and climbing out of there brings
    // everything home anyway.
    private void OnBossRoomCleared(Room room)
    {
        EnemyBullet.ClearAll();
        PlayRoomCleared();
        ZoneExit zoneExit = room.GetComponentInChildren<ZoneExit>();

        if (zoneExit != null)
            zoneExit.Unlock();
        else
            SpawnExit(room, FloorExit.Kind.NextFloor);

        if (FloorNumber < TotalFloors)
            SpawnUplink(room);
    }

    // The uplink terminal, its back to a wall of the boss room's middle cell that has no door,
    // facing in.
    private void SpawnUplink(Room room)
    {
        Vector2Int cell = MiddleCell(room);
        Vector2Int wall = UplinkWall(room, cell);
        Vector3 outward = new Vector3(wall.x, 0f, wall.y);
        float halfExtent = wall.x != 0 ? roomSize.x / 2f : roomSize.y / 2f;
        UplinkTerminal.Create(uplinkModel, CellToWorld(cell) + outward * (halfExtent - 0.9f), -outward, room.transform);
    }

    // The room's cell nearest its middle.
    private Vector2Int MiddleCell(Room room)
    {
        Vector3 centre = room.transform.position;
        Vector2Int cell = room.Cells[0];

        foreach (Vector2Int candidate in room.Cells)
        {
            if (Vector3.Distance(CellToWorld(candidate), centre) < Vector3.Distance(CellToWorld(cell), centre))
                cell = candidate;
        }

        return cell;
    }

    // Which wall of that cell the uplink stands against: the first outside wall with no door (east
    // or west first: the zone's way down has the north wall, PlaceZoneExit).
    private static Vector2Int UplinkWall(Room room, Vector2Int cell)
    {
        foreach (Vector2Int side in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.down, Vector2Int.up })
        {
            bool door = false;

            foreach (RoomConnection connection in room.Connections)
                door |= connection.Cell == cell && connection.Direction == side;

            if (!room.Contains(cell + side) && !door)
                return side;
        }

        return Vector2Int.up;
    }

    // A normal room's last enemy is down: the cleared chime, and sometimes a medkit where it
    // fell, and sometimes a supply (grenade, molotov and so on) beside it.
    private void OnCombatRoomCleared(Room room)
    {
        EnemyBullet.ClearAll();
        // A room that ended up with no enemies is "cleared" on entry; there was no fight.
        if (!lastDeaths.ContainsKey(room))
            return;

        PlayRoomCleared();

        if (ammoMaterial == null)
            return;

        Vector3 position = lastDeaths.TryGetValue(room, out Vector3 death) ? death : room.transform.position;

        // Nudged towards the middle of the room, so it doesn't sit on top of an ammo drop
        // from the same enemy or against a wall.
        Vector3 toCentre = room.transform.position - position;
        toCentre.y = 0f;
        position += Vector3.ClampMagnitude(toCentre, 1f);

        if (UnityEngine.Random.value < Drops.medkitChance)
        {
            bool large = UnityEngine.Random.value < Drops.largeMedkitChance;
            ConsumablePickup.Spawn(large ? ConsumableType.LargeMedkit : ConsumableType.Medkit, 1, InsideRoom(room, position), room.transform, ammoMaterial);
        }

        Vector3 side = Vector3.Cross(Vector3.up, toCentre.sqrMagnitude > 0.01f ? toCentre.normalized : Vector3.forward);

        if (UnityEngine.Random.value < Drops.supplyChance && Drops.PickSupply(FloorNumber, UnityEngine.Random.value) is ConsumableType supply)
            ConsumablePickup.Spawn(supply, 1, InsideRoom(room, position + side * 0.9f), room.transform, ammoMaterial);

        if (UnityEngine.Random.value < Drops.meleeUpgradeChance)
            SpawnMeleeUpgrade(InsideRoom(room, position - side * 0.9f), room.transform, UnityEngine.Random.value);

        DropRoomMaterial(room, position);
    }

    private void PlayRoomCleared()
    {
        AudioClip clip = roomClearedSound != null
            ? roomClearedSound
            : SoundFX.Pick(SoundFX.Clips("room.cleared", 1, i => ProceduralAudio.RoomCleared()));
        SoundFX.Play2D(clip, roomClearedVolume);
    }

    // Called by FloorExit when the player climbs down a hatch or out of the supply room.
    public void UseExit(FloorExit.Kind kind)
    {
        if (kind == FloorExit.Kind.Extraction)
        {
            Extract();
            return;
        }

        if (kind == FloorExit.Kind.Hideout)
        {
            RunFlow.ExitToHideout();
            return;
        }

        // A hideout's deploy hatch leads down into its own zone (MetaProgress.ChosenZone, read
        // when floor 1 is built); other zones' hideouts are a drive away (TruckBay).
        // Under the last floor: the supply room.
        if (FloorNumber >= TotalFloors && !IsHideout)
        {
            transition.Play("> DESCENDING TO THE SUPPLY ROOM", $"{ZoneName} // SUPPLY ROOM // NO HOSTILES DETECTED", EnterSupplyRoom);
            return;
        }

        transition.Play(ZoneName, FloorNumber + 1, TotalFloors, AdvanceFloor);
    }

    // Runs while the transition screen is fully black.
    private void AdvanceFloor()
    {
        FloorNumber++;
        ClearLooseObjects();
        Generate();

        if (cameraFollow != null)
            cameraFollow.SnapToTarget();
    }

    // Things from the old floor that aren't part of its rooms, so they'd be left floating in the
    // new layout: weapons the player dropped, and bullets still in the air.
    private void ClearLooseObjects()
    {
        foreach (GunPickup pickup in FindObjectsByType<GunPickup>())
            Destroy(pickup.gameObject);

        foreach (Projectile projectile in FindObjectsByType<Projectile>())
            Destroy(projectile.gameObject);
    }

    // A hatch in the floor, whose doors swing open, in the middle of the boss room. Used with
    // Interact (FloorExit), so it has no collider.
    private void SpawnExit(Room room, FloorExit.Kind kind)
    {
        GameObject exit = new GameObject("Hatch");
        exit.transform.SetParent(room.transform, false);
        exit.transform.position = room.transform.position;

        Transform[] doors = null;

        if (hatchModel != null && hatchDoorModel != null)
        {
            VisualOnly(Instantiate(hatchModel, exit.transform, false), "Frame");

            // Two doors hinged on the left and right of the opening, each lying over half of it;
            // the right one is turned round so it reaches back to the middle.
            doors = new Transform[2];

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Transform hinge = new GameObject(i == 0 ? "Door Hinge L" : "Door Hinge R").transform;
                hinge.SetParent(exit.transform, false);
                hinge.localPosition = new Vector3(side * HatchOpening / 2f, HatchRimHeight, 0f);
                hinge.localRotation = i == 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
                VisualOnly(Instantiate(hatchDoorModel, hinge, false), "Door");
                doors[i] = hinge;
            }
        }
        else
        {
            CreateVisual("Hatch Frame", exit.transform, new Vector3(0f, 0.02f, 0f), new Vector3(2.4f, 0.04f, 2.4f), wallMaterial);
            CreateVisual("Hatch", exit.transform, new Vector3(0f, 0.03f, 0f), new Vector3(2f, 0.04f, 2f), exitMaterial);
        }

        // A glow so it can be found in the dark: up out of the shaft for the hatch.
        Light glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(exit.transform, false);
        glow.transform.localPosition = Vector3.up * 0.6f;
        glow.type = LightType.Point;
        glow.color = new Color(0.35f, 1f, 0.55f);
        glow.range = 5f;
        glow.intensity = 3f;
        glow.shadows = LightShadows.None;

        // With the sprite art the hatch is drawn flat from above (FloorExit opens it), a little
        // above the floor litter (up to ~2 cm) so a stray sheet or box remains goes under it.
        if (SpriteVisuals.Current != null)
            ItemSprite.ApplyFlat(exit, SpriteVisuals.Current.hatch, Vector2Int.zero, HatchOpening + 0.6f, 0.03f);

        // The hideout's deploy hatch is waiting open.
        exit.AddComponent<FloorExit>().Initialise(this, kind, IsHideout ? 0f : exitArmDelay, doors);
    }

    // The span along the boss room's north wall left open for its way down: the middle, or towards
    // the west corner when the room's door is in that wall (the uplink then takes the east wall).
    // Null when there's no zone exit to build (no room kit or art: the old hatch instead).
    private Vector2? PlanExitOpening(PlannedRoom planned, HashSet<(Vector2Int, Vector2Int)> doorways)
    {
        if (planned.Type != RoomType.Boss || !UseWallKit || !KitFeatures || !ZoneExit.HasArt(Zone))
            return null;

        Vector2Int cell = planned.Cells[0];
        float width = ZoneExits.For(Zone).Opening;
        Rect block = BlockRect(cell, 0, 0);
        float x = doorways.Contains((cell, Vector2Int.up)) ? block.center.x - (roomSize.x / 2f - width / 2f - 1.2f) : block.center.x;
        return new Vector2(x - width / 2f, x + width / 2f);
    }

    // The north edge on the maps, less the opening when the way down stands in a bay (which the
    // bay's own edges then go round).
    private void AddExitOutline(Rect block, Vector2 opening, List<RoomEdge> outline)
    {
        if (!ZoneExits.For(Zone).Bay)
        {
            outline.Add(EdgeOf(block, Vector2Int.up));
            return;
        }

        outline.Add(new RoomEdge { From = new Vector2(block.xMin, block.yMax), To = new Vector2(opening.x, block.yMax), Outward = Vector2.up });
        outline.Add(new RoomEdge { From = new Vector2(opening.y, block.yMax), To = new Vector2(block.xMax, block.yMax), Outward = Vector2.up });
    }

    // Behind the opening: the escalator's and the chute's bay (floor, side walls, back wall); the
    // stairwell and lift fill it themselves. Remembers the spot for PlaceZoneExit.
    private void BuildExitBay(Room room, Rect block, Vector2 opening, List<Rect> blocks, List<RoomEdge> outline, KitMesh floor)
    {
        ZoneExitInfo info = ZoneExits.For(Zone);
        float face = block.yMax;
        exitSpots[room] = new Vector3((opening.x + opening.y) / 2f, 0f, face);

        if (!info.Bay)
            return;

        float back = face + info.Depth;
        float t = wallThickness;
        Rect bay = Rect.MinMaxRect(opening.x, face, opening.y, back);
        blocks.Add(bay);

        if (floor != null)
            AddFloorBlock(room.transform, bay, floor);

        // Side walls from behind the room's wall to the back wall, then the back wall across.
        CreateKitWall(room.transform, false, opening.x - t / 2f, face + t, back, Vector3.right, KitSpan.Plain);
        CreateKitWall(room.transform, false, opening.y + t / 2f, face + t, back, Vector3.left, KitSpan.Plain);
        CreateKitWall(room.transform, true, back + t / 2f, opening.x - t, opening.y + t, Vector3.back, KitSpan.Plain);

        outline.Add(new RoomEdge { From = new Vector2(opening.x, face), To = new Vector2(opening.x, back), Outward = Vector2.left });
        outline.Add(new RoomEdge { From = new Vector2(opening.y, face), To = new Vector2(opening.y, back), Outward = Vector2.right });
        outline.Add(new RoomEdge { From = new Vector2(opening.x, back), To = new Vector2(opening.y, back), Outward = Vector2.up });
    }

    // The zone's way down (ZoneExit) in the opening left for it. A strip of floor in front of it is
    // counted as a pillar, so nothing is placed in the way.
    private void PlaceZoneExit(Room room)
    {
        if (!exitSpots.TryGetValue(room, out Vector3 wall))
            return;

        ZoneExit exit = ZoneExit.Create(Zone, room.transform, wall, slidePhysics);

        if (exit == null)
            return;

        GameObject entry = new GameObject("Exit Entry");
        entry.transform.SetParent(exit.transform, false);
        entry.transform.position = exit.Entry;
        entry.AddComponent<FloorExit>().Initialise(this, exit);

        float width = exit.Info.Opening;
        AddSolidPoints(room, new Rect(wall.x - width / 2f, wall.z - 1.4f, width, 1.4f));
    }
}
