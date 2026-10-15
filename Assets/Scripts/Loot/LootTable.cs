using System;
using UnityEngine;

// What loot spawn points can give, and how likely they are to give anything at all. Floor points
// (the middle of a room) and wall points (mounted on a wall, like the spawn room's shotgun) each
// have their own chance and their own list, so wall racks can lean towards weapons.
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

    private static bool Allowed(Entry entry, int floor)
    {
        if (entry == null || entry.weight <= 0f || floor < entry.minFloor)
            return false;

        return entry.kind != ItemKind.Weapon || entry.weapon != null;
    }
}
