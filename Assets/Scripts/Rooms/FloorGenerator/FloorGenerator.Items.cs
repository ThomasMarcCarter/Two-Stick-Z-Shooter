using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's side of the run items (RunItems): the powerup cases beside treasure and boss
// crates, armour plates for a player wearing the carrier, and the Dinner Bell's extra zombies
// and loot.
public partial class FloorGenerator
{
    private const int DinnerBellZombies = 2;

    private readonly HashSet<RunItem> floorOffers = new HashSet<RunItem>();
    private int offersFloor = -1;

    private void ResetFloorOffers()
    {
        if (offersFloor == FloorNumber)
            return;

        offersFloor = FloorNumber;
        floorOffers.Clear();
    }

    // A case of run items the player doesn't hold and no other case on this floor has offered, near
    // a crate (clear of `avoid`, the boss room's hatch). The training floor has none.
    private void SpawnPowerupCase(Room room, Vector3 near, Vector3 avoid)
    {
        if (IsTutorial)
            return;

        ResetFloorOffers();

        List<RunItem> pool = new List<RunItem>();
        List<RunItem> lockedToo = new List<RunItem>();

        foreach (RunItem item in RunItems.Normal)
        {
            if (PlayerItems.Has(item) || floorOffers.Contains(item))
                continue;

            lockedToo.Add(item);

            if (Unlocks.IsUnlocked(item))
                pool.Add(item);
        }

        if (pool.Count == 0)
            pool = lockedToo;   // never an empty case

        List<RunItem> offers = new List<RunItem>();

        for (int i = 0; i < Drops.runItemOffers && pool.Count > 0; i++)
        {
            int pick = UnityEngine.Random.Range(0, pool.Count);
            offers.Add(pool[pick]);
            floorOffers.Add(pool[pick]);
            pool.RemoveAt(pick);
        }

        if (offers.Count > 0)
            PowerupCase.Spawn(offers, PowerupCaseSpot(room, near, avoid), room.transform);
    }

    // Round the crate, inside the room and clear of anything solid; beside the crate failing that.
    private Vector3 PowerupCaseSpot(Room room, Vector3 near, Vector3 avoid)
    {
        Physics.SyncTransforms();

        foreach (float reach in new[] { 2.2f, 3f })
        {
            for (int step = 0; step < 12; step++)
            {
                float angle = step * Mathf.PI / 6f;
                Vector3 spot = near + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach;
                spot.y = 0f;

                if (TryGetRoom(spot, out Room inside) && inside == room && Vector3.Distance(spot, avoid) > 2.2f
                    && InsideRoom(room, spot) == spot && IsClearForCrate(spot) && IsClear(spot, PillarsOf(room), 1.4f))
                    return spot;
            }
        }

        return InsideRoom(room, near + Vector3.left * 1.6f);
    }

    // Crates only hold plates once the player has somewhere to put them.
    private static void AddPlateToCrate(SupplyCrate crate)
    {
        if (PlayerItems.Has(RunItem.PlateCarrier))
            crate.Add(CrateItem.ForArmorPlate());
    }

    private void DropPlate(Room room, Vector3 position)
    {
        if (PlayerItems.Has(RunItem.PlateCarrier) && UnityEngine.Random.value < Drops.enemyPlateChance)
            ArmorPlatePickup.Spawn(InsideRoom(room, position + new Vector3(-0.6f, 0f, 0.4f)), room.transform);
    }

    // Debug: an armour plate at a point.
    public void SpawnArmorPlate(Vector3 position)
    {
        ArmorPlatePickup.Spawn(position, TryGetRoom(position, out Room room) ? room.transform : transform);
    }

    // Bad Omen taken mid-floor: each combat room not yet entered gets an extra elite.
    public void RingBadOmen()
    {
        if (IsTutorial)
            return;

        foreach (Room room in Rooms)
        {
            if (room.Type != RoomType.Normal || room.Visited || room.IsCleared)
                continue;

            List<Vector3> placed = new List<Vector3>();

            foreach (Enemy enemy in room.Enemies)
                placed.Add(enemy.transform.position);

            if (TryFindSpawnPoint(room, DoorwayPositions(room), placed, out Vector3 position))
                SpawnNormalEnemy(room, position, PickEnemyKind(UnityEngine.Random.value, new Dictionary<EnemyKind, int>())).SetElite(Elites.Random());
        }
    }

    // Taken mid-floor: the combat rooms not yet entered get their extra zombies now (later floors
    // count them in when they're built, SpawnEnemies).
    public void RingDinnerBell()
    {
        foreach (Room room in Rooms)
        {
            if (room.Type != RoomType.Normal || room.Visited || room.IsCleared)
                continue;

            List<Vector3> placed = new List<Vector3>();

            foreach (Enemy enemy in room.Enemies)
                placed.Add(enemy.transform.position);

            List<Vector3> doorways = DoorwayPositions(room);

            for (int i = 0; i < DinnerBellZombies; i++)
            {
                if (TryFindSpawnPoint(room, doorways, placed, out Vector3 position))
                {
                    placed.Add(position);
                    SpawnNormalEnemy(room, position, PickEnemyKind(UnityEngine.Random.value, new Dictionary<EnemyKind, int>()));
                }
            }
        }
    }

    // The Junk Drawer's throwable from a cleared combat room.
    private void DropJunk(Room room, Vector3 position, Vector3 side)
    {
        if (!PlayerItems.Has(RunItem.JunkDrawer) || room.Type != RoomType.Normal)
            return;

        ConsumableType[] junk = { ConsumableType.Grenade, ConsumableType.Molotov, ConsumableType.Bottle };
        Vector3 spot = position + Vector3.Cross(side, Vector3.up) * 1.4f;
        ConsumablePickup.Spawn(junk[UnityEngine.Random.Range(0, junk.Length)], 1, InsideRoom(room, spot), room.transform, ammoMaterial);
    }

    // The Dinner Bell's extra loot from a cleared room: a supply and a material.
    private void DropDinnerBellLoot(Room room, Vector3 position, Vector3 side)
    {
        if (!PlayerItems.Has(RunItem.DinnerBell))
            return;

        ConsumableType supply = Drops.PickSupply(FloorNumber, UnityEngine.Random.value) ?? ConsumableType.Medkit;
        ConsumablePickup.Spawn(supply, 1, InsideRoom(room, position + side * 1.8f), room.transform, ammoMaterial);
        MaterialPickup.Spawn(PickMaterial(UnityEngine.Random.value), 1, InsideRoom(room, position - side * 1.8f), room.transform);
    }
}
