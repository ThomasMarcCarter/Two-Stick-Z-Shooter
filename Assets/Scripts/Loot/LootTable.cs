using System;
using UnityEngine;

// What loot spawn points can give, and how likely they are to give anything at all. Floor points
// (the middle of a room) and wall points (mounted on a wall, like the spawn room's shotgun) each
// have their own chance and their own list, so wall racks can lean towards weapons.
//   It also holds every other drop chance and count in a run: what zombies drop when they die,
// what a cleared combat room leaves, and what goes in the boss's and the treasure room's supply
// crates. The FloorGenerator does the dropping; this only says how likely and how much.
// Create more with Assets > Create > Roguelike > Loot Table and assign one to the FloorGenerator.
[CreateAssetMenu(menuName = "Roguelike/Loot Table", fileName = "LootTable")]
public class LootTable : ScriptableObject
{
    public enum ItemKind
    {
        Weapon,
        Ammo,           // a set ammo type
        HeldGunAmmo,    // ammo for a gun the player is carrying (any type if they have none)
        Consumable,     // a medkit, grenade and so on
        MeleeUpgrade    // a random melee upgrade the player can still take
    }

    [Serializable]
    public class Entry
    {
        [Tooltip("Only for reading the list in the Inspector.")]
        public string label;
        public ItemKind kind = ItemKind.Weapon;

        [Tooltip("Weapon kind: the gun prefab.")]
        public GameObject weapon;
        [Tooltip("Weapon kind: spawn with a full magazine, rather than a random amount.")]
        public bool fullMagazine;

        [Tooltip("Ammo kind: which type.")]
        public AmmoType ammoType;
        [Tooltip("Ammo kinds: how many normal drops' worth it holds.")]
        public float amountMultiplier = 1f;
        [Tooltip("Ammo kinds: a big crate rather than a small box.")]
        public bool crate;

        [Tooltip("Consumable kind: which one.")]
        public ConsumableType consumable;
        [Tooltip("Consumable kind: how many.")]
        [Min(1)] public int count = 1;

        [Tooltip("Relative odds against the other entries in the same list.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Can't appear before this floor.")]
        [Min(1)] public int minFloor = 1;
    }

    [Header("Floor points (middle of the room)")]
    [Range(0f, 1f)] public float floorChance = 0.35f;
    public Entry[] floorLoot = new Entry[0];

    [Header("Wall points (mounted on a wall)")]
    [Range(0f, 1f)] public float wallChance = 0.25f;
    public Entry[] wallLoot = new Entry[0];

    [Serializable]
    public struct SupplyOdds
    {
        public ConsumableType type;
        [Min(0f)] public float weight;
        [Min(1)] public int minFloor;
    }

    [Header("Ammo")]
    [Tooltip("Chance an enemy drops ammo when it dies (before the hideout's Scavenger upgrade).")]
    [Range(0f, 1f)] public float enemyAmmoDropChance = 0.3f;
    [Tooltip("Chance a combat room has an ammo crate in it.")]
    [Range(0f, 1f)] public float ammoCrateChance = 0.4f;
    [Tooltip("How many times a normal drop a crate (or a boss drop) holds.")]
    public float crateMultiplier = 2.5f;
    [Tooltip("Chance a drop is for one of the player's current guns, rather than any type.")]
    [Range(0f, 1f)] public float heldGunAmmoBias = 0.75f;

    [Header("Room cleared")]
    [Tooltip("Chance a medkit drops when a combat room is cleared.")]
    [Range(0f, 1f)] public float medkitChance = 0.3f;
    [Tooltip("Of those, the chance it's a large medkit (+2) rather than small (+1).")]
    [Range(0f, 1f)] public float largeMedkitChance = 0.15f;
    [Tooltip("Chance a cleared combat room also leaves a grenade, molotov, bottle or adrenaline.")]
    [Range(0f, 1f)] public float supplyChance = 0.3f;
    [Tooltip("Relative odds of each kind of supply drop.")]
    public SupplyOdds[] supplyOdds =
    {
        new SupplyOdds { type = ConsumableType.Bottle, weight = 3f, minFloor = 1 },
        new SupplyOdds { type = ConsumableType.Grenade, weight = 2f, minFloor = 1 },
        new SupplyOdds { type = ConsumableType.Molotov, weight = 2f, minFloor = 1 },
        new SupplyOdds { type = ConsumableType.Adrenaline, weight = 1.5f, minFloor = 1 }
    };
    [Tooltip("Chance a cleared combat room also leaves a melee upgrade.")]
    [Range(0f, 1f)] public float meleeUpgradeChance = 0.12f;

    [Header("Crafting materials")]
    [Tooltip("Chance a zombie drops a material when it dies.")]
    [Range(0f, 1f)] public float enemyMaterialChance = 0.06f;
    [Tooltip("Chance a cleared combat room leaves a material.")]
    [Range(0f, 1f)] public float roomMaterialChance = 0.35f;

    [Header("Boss crate")]
    [Tooltip("Attachments the boss's crate always holds.")]
    [Min(0)] public int bossAttachments = 2;
    [Tooltip("Melee upgrades the boss's crate holds.")]
    [Min(0)] public int bossMeleeUpgrades = 1;
    [Tooltip("Materials the boss's crate holds.")]
    [Min(0)] public int bossMaterials = 3;

    [Header("Treasure crate")]
    [Tooltip("Attachments the treasure room's crate always holds, besides its weapon.")]
    [Min(0)] public int treasureAttachments = 2;
    [Tooltip("Melee upgrades the treasure room's crate always holds.")]
    [Min(0)] public int treasureMeleeUpgrades = 1;
    [Tooltip("Consumables the treasure room's crate always holds.")]
    public ConsumableType[] treasureSupplies = { ConsumableType.Grenade, ConsumableType.LargeMedkit };
    [Tooltip("Materials the treasure room's crate always holds.")]
    [Min(0)] public int treasureMaterials = 2;

    [Header("Attachments")]
    [Tooltip("Chance each attachment is picked from those that fit a gun the player is carrying, rather than any.")]
    [Range(0f, 1f)] public float attachmentFitBias = 0.7f;

    public float ChanceFor(LootSpawnPoint.Placement placement)
    {
        return placement == LootSpawnPoint.Placement.Wall ? wallChance : floorChance;
    }

    public Entry[] EntriesFor(LootSpawnPoint.Placement placement)
    {
        return placement == LootSpawnPoint.Placement.Wall ? wallLoot : floorLoot;
    }

    // Weighted pick among the entries allowed on this floor; null if there are none.
    public Entry Pick(LootSpawnPoint.Placement placement, int floor, float roll)
    {
        Entry[] entries = EntriesFor(placement);
        float total = 0f;

        foreach (Entry entry in entries)
        {
            if (Allowed(entry, floor))
                total += entry.weight;
        }

        if (total <= 0f)
            return null;

        float target = roll * total;

        foreach (Entry entry in entries)
        {
            if (!Allowed(entry, floor))
                continue;

            target -= entry.weight;

            if (target <= 0f)
                return entry;
        }

        // Rounding: fall back to the last allowed entry.
        for (int i = entries.Length - 1; i >= 0; i--)
        {
            if (Allowed(entries[i], floor))
                return entries[i];
        }

        return null;
    }

    // Weighted pick among the supplies allowed on this floor; null if there are none.
    public ConsumableType? PickSupply(int floor, float roll)
    {
        float total = 0f;

        foreach (SupplyOdds odds in supplyOdds)
        {
            if (odds.weight > 0f && floor >= odds.minFloor)
                total += odds.weight;
        }

        if (total <= 0f)
            return null;

        float target = roll * total;
        ConsumableType? last = null;

        foreach (SupplyOdds odds in supplyOdds)
        {
            if (odds.weight <= 0f || floor < odds.minFloor)
                continue;

            last = odds.type;
            target -= odds.weight;

            if (target <= 0f)
                return odds.type;
        }

        return last;
    }

    private static bool Allowed(Entry entry, int floor)
    {
        if (entry == null || entry.weight <= 0f || floor < entry.minFloor)
            return false;

        return entry.kind != ItemKind.Weapon || entry.weapon != null;
    }
}
