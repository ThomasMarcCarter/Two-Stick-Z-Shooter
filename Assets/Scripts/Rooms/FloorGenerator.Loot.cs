using System;
using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's loot side: what goes on the floor for the player to pick up. Melee
// upgrades and supplies (consumables) from cleared rooms, loot spawn points rolled against
// the LootTable, the treasure room's haul, and weapon attachments (treasure, boss drops, and
// the picks for the Gunsmith upgrade). Debug spawners for each are here too.
public partial class FloorGenerator
{
    // ---------- Melee upgrades ----------

    // A random upgrade the player can still take (not maxed, and not already lying on this
    // floor so many times it would max out); null if every one is maxed.
    private MeleeUpgrade? PickMeleeUpgrade(float roll)
    {
        PlayerMelee melee = player != null ? player.GetComponent<PlayerMelee>() : null;
        List<MeleeUpgrade> open = new List<MeleeUpgrade>();

        foreach (MeleeUpgrade upgrade in MeleeUpgrades.All)
        {
            int lying = 0;

            foreach (MeleeUpgradePickup pickup in FindObjectsByType<MeleeUpgradePickup>())
            {
                if (pickup.Upgrade == upgrade)
                    lying++;
            }

            int level = melee != null ? melee.Level(upgrade) : 0;

            if (level + lying < MeleeUpgrades.MaxLevel(upgrade))
                open.Add(upgrade);
        }

        if (open.Count == 0)
            return null;

        return open[Mathf.Min(open.Count - 1, (int)(roll * open.Count))];
    }

    private void SpawnMeleeUpgrade(Vector3 position, Transform parent, float roll)
    {
        if (PickMeleeUpgrade(roll) is MeleeUpgrade upgrade)
            MeleeUpgradePickup.Spawn(upgrade, position, parent, ammoMaterial);
    }

    // Further out from the hatch than the boss's attachments.
    private void DropMeleeUpgrades(Room room, Vector3 position)
    {
        Vector3 centre = new Vector3(room.transform.position.x, 0f, room.transform.position.z);
        Vector3 fromCentre = new Vector3(position.x, 0f, position.z) - centre;
        Vector3 outward = fromCentre.sqrMagnitude > 0.01f ? fromCentre.normalized : Vector3.back;
        Vector3 spot = centre + outward * Mathf.Clamp(fromCentre.magnitude - 1.5f, 2.8f, 3.5f) + outward * 1.6f;
        Vector3 across = new Vector3(-outward.z, 0f, outward.x);

        for (int i = 0; i < bossMeleeUpgrades; i++)
            SpawnMeleeUpgrade(spot + across * ((i - (bossMeleeUpgrades - 1) / 2f) * 1.6f), room.transform, UnityEngine.Random.value);
    }

    // Debug: one of each upgrade in a row at a point.
    public void SpawnAllMeleeUpgrades(Vector3 position, Vector3 right)
    {
        Transform parent = TryGetRoom(position, out Room room) ? room.transform : transform;
        MeleeUpgrade[] all = MeleeUpgrades.All;

        for (int i = 0; i < all.Length; i++)
            MeleeUpgradePickup.Spawn(all[i], position + right * ((i - (all.Length - 1) / 2f) * 1.1f), parent, ammoMaterial);
    }

    // Weighted pick among the supplies allowed on this floor; null if there are none.
    private ConsumableType? PickSupply(float roll)
    {
        float total = 0f;

        foreach (SupplyOdds odds in supplyOdds)
        {
            if (odds.weight > 0f && FloorNumber >= odds.minFloor)
                total += odds.weight;
        }

        if (total <= 0f)
            return null;

        float target = roll * total;
        ConsumableType? last = null;

        foreach (SupplyOdds odds in supplyOdds)
        {
            if (odds.weight <= 0f || FloorNumber < odds.minFloor)
                continue;

            last = odds.type;
            target -= odds.weight;

            if (target <= 0f)
                return odds.type;
        }

        return last;
    }

    // Debug: a consumable at a point, in whichever room it's in (or loose if none).
    public void SpawnConsumable(ConsumableType type, int amount, Vector3 position)
    {
        Transform parent = TryGetRoom(position, out Room room) ? room.transform : transform;
        ConsumablePickup.Spawn(type, amount, position, parent, ammoMaterial);
    }

    // ---------- Loot ----------

    // Combat rooms get loot spawn points: one in the middle of a one-cell room, or one in the
    // middle of each of a few cells in a bigger room, plus a couple on walls with no door. Each
    // is rolled against the loot table. Returns the floor points, so a crate can keep clear.
    private List<Vector3> SpawnLoot(Room room)
    {
        List<Vector3> floorPoints = new List<Vector3>();

        if (room.Type != RoomType.Normal)
            return floorPoints;

        List<Vector2Int> cells = new List<Vector2Int>(room.Cells);
        Shuffle(cells);
        int floorCount = Mathf.Min(cells.Count, cells.Count > 1 ? floorPointsLargeRoom : 1);

        for (int i = 0; i < floorCount; i++)
        {
            Vector3 position = CellToWorld(cells[i]);
            floorPoints.Add(position);
            RollLoot(room, LootSpawnPoint.Create(room.transform, position, LootSpawnPoint.Placement.Floor, Vector3.zero));
        }

        // Every outside wall of the room's cells that has no doorway in it.
        List<(Vector2Int cell, Vector2Int side)> walls = new List<(Vector2Int, Vector2Int)>();

        foreach (Vector2Int cell in room.Cells)
        {
            foreach (Vector2Int side in Directions)
            {
                if (!room.Contains(cell + side) && !HasDoorway(room, cell, side))
                    walls.Add((cell, side));
            }
        }

        Shuffle(walls);

        for (int i = 0; i < Mathf.Min(wallPointsPerRoom, walls.Count); i++)
        {
            (Vector2Int cell, Vector2Int side) = walls[i];
            Vector3 outward = new Vector3(side.x, 0f, side.y);
            Vector3 along = new Vector3(side.y, 0f, side.x);
            float halfExtent = side.x != 0 ? roomSize.x / 2f : roomSize.y / 2f;
            float halfLength = side.x != 0 ? roomSize.y / 2f : roomSize.x / 2f;

            // Somewhere along the middle half of the wall, so it isn't jammed into a corner.
            float offset = Mathf.Lerp(-halfLength / 2f, halfLength / 2f, (float)rng.NextDouble());
            Vector3 position = CellToWorld(cell) + outward * halfExtent + along * offset;

            RollLoot(room, LootSpawnPoint.Create(room.transform, position, LootSpawnPoint.Placement.Wall, outward));
        }

        return floorPoints;
    }

    private static bool HasDoorway(Room room, Vector2Int cell, Vector2Int side)
    {
        foreach (RoomConnection connection in room.Connections)
        {
            if (connection.Cell == cell && connection.Direction == side)
                return true;
        }

        return false;
    }

    // Debug: fills every empty loot point in a room, skipping the chance roll. Returns how many
    // items spawned.
    public int ForceLoot(Room room)
    {
        if (room == null || lootTable == null)
            return 0;

        if (rng == null)
            rng = new System.Random();

        int spawned = 0;

        foreach (LootSpawnPoint point in room.GetComponentsInChildren<LootSpawnPoint>())
        {
            if (point.Item != null)
                continue;

            LootTable.Entry entry = lootTable.Pick(point.Kind, FloorNumber, (float)rng.NextDouble());

            if (entry == null)
                continue;

            point.Item = SpawnLootItem(room, point, entry);

            if (point.Item != null)
                spawned++;
        }

        return spawned;
    }

    // Rolls the point's chance, then picks what it gives from the table.
    private void RollLoot(Room room, LootSpawnPoint point)
    {
        if (lootTable == null || rng.NextDouble() >= lootTable.ChanceFor(point.Kind))
            return;

        LootTable.Entry entry = lootTable.Pick(point.Kind, FloorNumber, (float)rng.NextDouble());

        if (entry != null)
            point.Item = SpawnLootItem(room, point, entry);
    }

    private GameObject SpawnLootItem(Room room, LootSpawnPoint point, LootTable.Entry entry)
    {
        bool wall = point.Kind == LootSpawnPoint.Placement.Wall;

        if (entry.kind == LootTable.ItemKind.Weapon)
        {
            Gun gun = entry.weapon.GetComponent<Gun>();

            if (gun == null || pickupPrefab == null)
                return null;

            Vector3 position = point.transform.position;
            Quaternion rotation = Quaternion.identity;

            // Mounted like the spawn room's gun: up on the wall, facing into the room.
            if (wall)
            {
                position += -point.Outward * pickupWallGap + Vector3.up * pickupMountHeight;
                rotation = Quaternion.LookRotation(-point.Outward);
            }

            GameObject pickup = Instantiate(pickupPrefab, position, rotation, room.transform);
            int magazine = gun.GetMagazineSize();
            int ammo = entry.fullMagazine ? magazine : 1 + rng.Next(magazine);
            pickup.GetComponent<GunPickup>().SetWeaponPrefab(entry.weapon, ammo);
            return pickup;
        }

        if (ammoMaterial == null)
            return null;

        if (entry.kind == LootTable.ItemKind.MeleeUpgrade)
        {
            Vector3 at = point.transform.position - (wall ? point.Outward * wallAmmoGap : Vector3.zero);
            MeleeUpgrade? upgrade = PickMeleeUpgrade((float)rng.NextDouble());
            return upgrade != null ? MeleeUpgradePickup.Spawn(upgrade.Value, at, room.transform, ammoMaterial).gameObject : null;
        }

        if (entry.kind == LootTable.ItemKind.Consumable)
        {
            Vector3 at = point.transform.position - (wall ? point.Outward * wallAmmoGap : Vector3.zero);
            return ConsumablePickup.Spawn(entry.consumable, entry.count, at, room.transform, ammoMaterial).gameObject;
        }

        AmmoType type = entry.kind == LootTable.ItemKind.HeldGunAmmo
            ? PickAmmoType(0f, (float)rng.NextDouble())   // roll 0 always takes the held-gun pool when there is one
            : entry.ammoType;

        // Ammo on a wall point sits on the floor against the wall.
        Vector3 spot = point.transform.position - (wall ? point.Outward * wallAmmoGap : Vector3.zero);
        int amount = DropAmount(type, entry.amountMultiplier, (float)rng.NextDouble());
        return AmmoPickup.Spawn(type, amount, spot, room.transform, ammoMaterial, entry.crate).gameObject;
    }

    // Treasure rooms get a weapon pickup in the middle, with a full magazine.
    private void SpawnTreasure(Room room)
    {
        if (room.Type != RoomType.Treasure || pickupPrefab == null || treasureWeapons == null || treasureWeapons.Length == 0)
            return;

        GameObject weapon = treasureWeapons[rng.Next(treasureWeapons.Length)];
        Gun gun = weapon != null ? weapon.GetComponent<Gun>() : null;

        if (gun == null)
            return;

        GameObject pickup = Instantiate(pickupPrefab, room.transform.position, Quaternion.identity, room.transform);
        pickup.GetComponent<GunPickup>().SetWeaponPrefab(weapon, gun.GetMagazineSize());

        // Melee upgrades behind the weapon.
        for (int i = 0; i < treasureMeleeUpgrades && ammoMaterial != null; i++)
        {
            Vector3 offset = new Vector3((i - (treasureMeleeUpgrades - 1) / 2f) * 1.2f, 0f, 1.6f);
            SpawnMeleeUpgrade(room.transform.position + offset, room.transform, (float)rng.NextDouble());
        }

        // The supplies sit in a row in front of the weapon.
        if (ammoMaterial == null || treasureSupplies == null)
            return;

        for (int i = 0; i < treasureSupplies.Length; i++)
        {
            Vector3 offset = new Vector3((i - (treasureSupplies.Length - 1) / 2f) * 1.1f, 0f, -1.6f);
            ConsumablePickup.Spawn(treasureSupplies[i], 1, room.transform.position + offset, room.transform, ammoMaterial);
        }
    }

    // ---------- Attachments ----------

    // The treasure room's attachments, either side of its weapon, alternating left and right.
    private void SpawnTreasureAttachments(Room room)
    {
        if (room.Type != RoomType.Treasure)
            return;

        List<WeaponAttachment> picked = PickAttachments(treasureAttachments, () => (float)rng.NextDouble());

        for (int i = 0; i < picked.Count; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Vector3 offset = Vector3.right * side * (1.8f + (i / 2) * 1.2f);
            AttachmentPickup.Spawn(picked[i], room.transform.position + offset, room.transform, ammoMaterial);
        }
    }

    // The boss's attachments, side by side where it fell, drawn in towards the middle of the
    // room so they don't end up in a wall, but not onto the hatch that opens there.
    private void DropAttachments(Room room, Vector3 position)
    {
        List<WeaponAttachment> picked = PickAttachments(bossAttachments, () => UnityEngine.Random.value);
        Vector3 centre = new Vector3(room.transform.position.x, 0f, room.transform.position.z);
        Vector3 fromCentre = new Vector3(position.x, 0f, position.z) - centre;

        if (fromCentre.sqrMagnitude < 0.01f)
            fromCentre = Vector3.back;

        Vector3 outward = fromCentre.normalized;
        Vector3 spot = centre + outward * Mathf.Clamp(fromCentre.magnitude - 1.5f, 2.8f, 3.5f);
        Vector3 across = new Vector3(-outward.z, 0f, outward.x);

        // Spread across, side by side at the same distance from the hatch.
        for (int i = 0; i < picked.Count; i++)
        {
            Vector3 offset = across * ((i - (picked.Count - 1) / 2f) * 1.8f);
            AttachmentPickup.Spawn(picked[i], spot + offset, room.transform, ammoMaterial);
        }
    }

    // Debug: one of every attachment in a row at a point.
    public void SpawnAllAttachments(Vector3 position, Vector3 right)
    {
        if (attachments == null)
            return;

        Transform parent = TryGetRoom(position, out Room room) ? room.transform : transform;

        for (int i = 0; i < attachments.Length; i++)
        {
            if (attachments[i] != null)
                AttachmentPickup.Spawn(attachments[i], position + right * ((i - (attachments.Length - 1) / 2f) * 1.1f), parent, ammoMaterial);
        }
    }

    // A number of different attachments, by weight, allowed on this floor. Each one usually
    // comes from those that fit a gun the player has, so a find is rarely useless.
    private List<WeaponAttachment> PickAttachments(int count, Func<float> roll)
    {
        List<WeaponAttachment> picked = new List<WeaponAttachment>();

        if (attachments == null)
            return picked;

        List<Gun> guns = new List<Gun>();
        PlayerWeapon weapons = player != null ? player.GetComponent<PlayerWeapon>() : null;

        if (weapons != null)
        {
            if (weapons.Main.Gun != null)
                guns.Add(weapons.Main.Gun);

            if (weapons.Sidearm.Gun != null)
                guns.Add(weapons.Sidearm.Gun);
        }

        // Nothing already fitted to a carried gun, unless there's another gun it could go on.
        foreach (Gun gun in guns)
        {
            foreach (WeaponAttachment fitted in gun.Attachments)
            {
                if (!guns.Exists(other => other != gun && fitted.Fits(other) && other.AttachmentIn(fitted.slot) != fitted))
                    picked.Add(fitted);
            }
        }

        int alreadyFitted = picked.Count;

        for (int n = 0; n < count; n++)
        {
            bool preferFitting = roll() < attachmentFitBias;
            List<WeaponAttachment> pool = new List<WeaponAttachment>();

            foreach (WeaponAttachment attachment in attachments)
            {
                if (attachment == null || attachment.weight <= 0f || FloorNumber < attachment.minFloor || picked.Contains(attachment))
                    continue;

                if (preferFitting && !guns.Exists(attachment.Fits))
                    continue;

                pool.Add(attachment);
            }

            // Nothing fits what they're carrying: anything will do.
            if (pool.Count == 0 && preferFitting)
            {
                foreach (WeaponAttachment attachment in attachments)
                {
                    if (attachment != null && attachment.weight > 0f && FloorNumber >= attachment.minFloor && !picked.Contains(attachment))
                        pool.Add(attachment);
                }
            }

            if (pool.Count == 0)
                break;

            float total = 0f;

            foreach (WeaponAttachment attachment in pool)
                total += attachment.weight;

            float target = roll() * total;
            WeaponAttachment choice = pool[pool.Count - 1];

            foreach (WeaponAttachment attachment in pool)
            {
                target -= attachment.weight;

                if (target <= 0f)
                {
                    choice = attachment;
                    break;
                }
            }

            picked.Add(choice);
        }

        // The fitted ones were only there to be skipped.
        picked.RemoveRange(0, alreadyFitted);
        return picked;
    }
}
