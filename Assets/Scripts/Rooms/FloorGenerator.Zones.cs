using System;
using UnityEngine;

// Per-zone settings: what a zone of the map (Zones) changes about its floors. Each entry names a
// zone and overrides only what it sets: how many floors come before the supply room, the odds of
// each kind of zombie, the loot table, the floor and boss music, and the room floors and walls.
// Anything left empty, and any zone without an entry, uses the FloorGenerator's own settings,
// which are the Military Base's. The hideout always keeps its own (the base's) floors and walls.
// Bosses and room themes per zone are in code (Zones.Boss, CombatThemes).
public partial class FloorGenerator
{
    [Serializable]
    private class ZoneSettings
    {
        public Zone zone;
        [Tooltip("Floors before the supply room. 0 uses Total Floors.")]
        [Min(0)] public int floors;
        [Tooltip("This zone's odds of each kind of zombie. Empty uses Enemy Kinds.")]
        public EnemyKindOdds[] enemyKinds = new EnemyKindOdds[0];
        [Tooltip("This zone's loot table. Empty uses Loot.")]
        public LootTable lootTable;
        [Tooltip("Looping track for this zone's floors. Empty uses Background Music.")]
        public AudioClip backgroundMusic;
        [Tooltip("This zone's boss fight track. Empty uses Boss Music.")]
        public AudioClip bossMusic;
        [Tooltip("Room floors. Empty uses the floor material (or the sprite art's).")]
        public Material floor;
        [Tooltip("The floor some combat rooms have instead. Empty uses the floor variants.")]
        public Material floorVariant;
        [Tooltip("Walls, laid out like the base's (pixel walls: 32 rows of face under 4 of cap). Empty uses the wall material.")]
        public Material wall;
    }

    [Header("Zones")]
    [Tooltip("What each zone of the map changes from the settings above (which are the Military Base's). Only what's set is changed.")]
    [SerializeField] private ZoneSettings[] zoneSettings = new ZoneSettings[0];

    // The floors and walls the generator started with (after the sprite art swapped its own in),
    // put back for zones that don't change them.
    private Material baseFloor;
    private Material[] baseFloorVariants;
    private Material baseWall;
    private bool baseArtKept;

    // Floors and walls for the floor about to be built: the zone's, or the base's.
    private void UseZoneArt()
    {
        if (!baseArtKept)
        {
            baseFloor = floorMaterial;
            baseFloorVariants = floorVariants;
            baseWall = wallMaterial;
            baseArtKept = true;
        }

        // The hideout and the roof keep the base's bare concrete.
        ZoneSettings settings = IsHideout || IsEvac ? null : CurrentZoneSettings;
        floorMaterial = settings != null && settings.floor != null ? settings.floor : baseFloor;
        floorVariants = settings != null && settings.floorVariant != null ? new[] { settings.floorVariant } : baseFloorVariants;
        wallMaterial = settings != null && settings.wall != null ? settings.wall : baseWall;
    }

    // The current zone's entry, or null if it has none.
    private ZoneSettings CurrentZoneSettings
    {
        get
        {
            Zone zone = Zone;

            foreach (ZoneSettings settings in zoneSettings)
            {
                if (settings != null && settings.zone == zone)
                    return settings;
            }

            return null;
        }
    }

    private int ZoneFloors
    {
        get
        {
            ZoneSettings settings = CurrentZoneSettings;
            return settings != null && settings.floors > 0 ? settings.floors : totalFloors;
        }
    }

    private EnemyKindOdds[] ZoneEnemyKinds
    {
        get
        {
            ZoneSettings settings = CurrentZoneSettings;
            return settings != null && settings.enemyKinds != null && settings.enemyKinds.Length > 0 ? settings.enemyKinds : enemyKinds;
        }
    }

    private LootTable ZoneLootTable
    {
        get
        {
            ZoneSettings settings = CurrentZoneSettings;
            return settings != null && settings.lootTable != null ? settings.lootTable : lootTable;
        }
    }

    private AudioClip ZoneBackgroundMusic
    {
        get
        {
            ZoneSettings settings = CurrentZoneSettings;
            return settings != null && settings.backgroundMusic != null ? settings.backgroundMusic : backgroundMusic;
        }
    }

    private AudioClip ZoneBossMusic
    {
        get
        {
            ZoneSettings settings = CurrentZoneSettings;
            return settings != null && settings.bossMusic != null ? settings.bossMusic : bossMusic;
        }
    }
}
