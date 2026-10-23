using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Builds an Isaac/Gungeon-style floor: rooms of different shapes (1x1, long, L, T, 2x2) on a
// grid, joined by doors and short corridors. The start room is always at the world origin.
// Beating a floor's boss opens a hatch to the next floor; under the last floor's boss room is the
// supply room, whose ladder out ends the run as a win (FloorGenerator.SupplyRoom.cs).
// Floors are rebuilt in place, so the player (and their health, gun and ammo) carries over.
// Split into partials: enemies in FloorGenerator.Spawning.cs, loot in FloorGenerator.Loot.cs,
// boxes/trash/pillars/lights in FloorGenerator.Dressing.cs, and the hideout (floor 0), run-start
// upgrades, crafting materials and caged survivors in FloorGenerator.Hideout.cs, and the supply
// room in FloorGenerator.SupplyRoom.cs.
public partial class FloorGenerator : MonoBehaviour
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    [Header("Floors")]
    [SerializeField] private int totalFloors = 3;
    [SerializeField] private Material exitMaterial;
    [Tooltip("Seconds after the boss dies before the hatch can be used (its doors swing open over it).")]
    [SerializeField] private float exitArmDelay = 1.6f;
    [Tooltip("The hatch's frame (coaming, shaft, lamps). Without it the hatch is a flat block.")]
    [SerializeField] private GameObject hatchModel;
    [Tooltip("One hatch door leaf, pivot on its hinge edge, lying along +X. Two are placed.")]
    [SerializeField] private GameObject hatchDoorModel;
    [Tooltip("The supply uplink terminal that turns up in the boss room, for sending materials home.")]
    [SerializeField] private GameObject uplinkModel;
    [SerializeField] private FloorTransition transition;
    [SerializeField] private VictoryScreen victoryScreen;
    [SerializeField] private CameraFollow cameraFollow;

    [Header("Layout")]
    [SerializeField] private int minRooms = 8;
    [SerializeField] private int maxRooms = 12;
    [SerializeField] private bool randomSeed = true;
    [SerializeField] private int seed;

    [Header("Room Shape")]
    [Tooltip("Floor size of one grid cell. Bigger rooms are made of several cells.")]
    [SerializeField] private Vector2 roomSize = new Vector2(16f, 10f);
    [SerializeField] private float wallHeight = 2f;
    [Tooltip("How high walls and doors block things, above what's drawn: an invisible extension of their colliders, so thrown grenades and molotovs can't be lobbed over (a long throw peaks around 2.1 m) without tall walls hiding the floor from the tilted camera.")]
    [SerializeField] private float wallBlockHeight = 6f;
    [SerializeField] private float wallThickness = 0.5f;
    [SerializeField] private float doorWidth = 2f;
    [SerializeField] private float corridorLength = 3f;

    [Header("Materials")]
    [SerializeField] private Material floorMaterial;
    [Tooltip("Other floors a combat room can have instead (e.g. grimy tiles), for variety.")]
    [SerializeField] private Material[] floorVariants = new Material[0];
    [Tooltip("Chance a combat room uses one of the floor variants.")]
    [SerializeField, Range(0f, 1f)] private float floorVariantChance = 0.35f;
    [Tooltip("World units one repeat of the floor texture covers. The floor's texture tiles across the world, so blocks of any size show it at the same scale and line up.")]
    [SerializeField] private float floorTextureSize = 4f;
    [SerializeField] private Material wallMaterial;
    [Tooltip("World units one repeat of the wall texture covers along a wall. Vertically it spans the wall's height once (baseboard, painted band, blockwork, and a cap strip at the top that wall tops are mapped to).")]
    [SerializeField] private float wallTextureLength = 4f;

    [Tooltip("Walls on the south side of rooms and corridors (the side nearest the camera) aren't drawn, only their shadows, so they never hide the player. They still block.")]
    [SerializeField] private bool hideSouthWalls = true;

    [Header("Sprite Art (art pass)")]
    [Tooltip("Draw the player, walkers and the pistol as pixel-art sprites, with pixel-art rooms and effects (Sprite Visuals). Off keeps the 3D models and textures.")]
    [SerializeField] private bool spriteArt = true;
    [SerializeField] private SpriteVisuals spriteVisuals;

    // The pixel-art wall texture: 32 rows of face (the wall's full height) under 4 rows of cap.
    private const float PixelWallFace = 32f / 36f;
    private const float PixelWallCap = 34f / 36f;
    private bool pixelWalls;
    [Tooltip("The steel frame posts either side of each doorway.")]
    [SerializeField] private Material wallTrimMaterial;
    [Tooltip("Corridor floors (steel deck plates), so they read apart from the rooms. Falls back to the floor material.")]
    [SerializeField] private Material corridorFloorMaterial;
    [Tooltip("Painted hazard stripe on the floor just inside each doorway (transparent).")]
    [SerializeField] private Material hazardStripeMaterial;
    [Tooltip("Worn arena ring painted on the boss room's floor where the boss stands (transparent).")]
    [SerializeField] private Material bossArenaMaterial;
    [SerializeField] private Material doorMaterial;
    [SerializeField] private Color treasureTint = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color bossTint = new Color(0.8f, 0.25f, 0.25f);
    [Tooltip("Physics material for walls, doors and enemies. Frictionless, so bodies slide along walls instead of catching on them.")]
    [SerializeField] private PhysicsMaterial slidePhysics;

    [Header("Pillars")]
    [Tooltip("Chance a combat room of at least Min Cells For Pillars gets pillars.")]
    [SerializeField, Range(0f, 1f)] private float pillarChance = 0.6f;
    [SerializeField, Min(1)] private int minCellsForPillars = 2;
    [Tooltip("The pillar model, normalised to a 1 x 1 x 1 block. Without one, plain wall-coloured blocks are used.")]
    [SerializeField] private GameObject pillarPrefab;
    [Tooltip("Width of a pillar (it's square).")]
    [SerializeField] private float pillarSize = 0.8f;
    [Tooltip("Height of a pillar. Tall enough that a character standing right in front of one (south of it) never looks taller than it: camera-facing sprites show their full height while the tilt squashes the pillar's (2.8 m clears the player at the 30° orthographic tilt; a 20° perspective tilt needed 4.2 m).")]
    [SerializeField] private float pillarHeight = 2.8f;
    [Tooltip("How far enemies, crates and other spawns keep from a pillar's centre.")]
    [SerializeField] private float pillarClearance = 1.8f;

    [Header("Clutter")]
    [Tooltip("Cardboard boxes scattered against the walls of rooms (not the spawn room).")]
    [SerializeField] private GameObject[] clutterBoxes = new GameObject[0];
    [Tooltip("Flattened sheet left lying on the floor now and then.")]
    [SerializeField] private GameObject flattenedBox;
    [Tooltip("Groups of boxes against the walls, per room cell (min, max).")]
    [SerializeField] private Vector2Int clutterPerCell = new Vector2Int(3, 6);
    [Tooltip("Free-standing groups out in the room, per cell (min, max), kept off the routes between doors.")]
    [SerializeField] private Vector2Int openClutterPerCell = new Vector2Int(0, 2);
    [Tooltip("Flattened sheets on the floor, per cell (min, max).")]
    [SerializeField] private Vector2Int flattenedPerCell = new Vector2Int(2, 5);
    [Tooltip("Boxes are placed at a random size between these, times the model's own size.")]
    [SerializeField] private Vector2 boxScale = new Vector2(1.4f, 1.8f);
    [Tooltip("No box comes out bigger than this along its longest side, in metres (the big models at the top of the scale range were huge).")]
    [SerializeField] private float maxBoxSize = 1.1f;
    [SerializeField] private Vector2 flattenedScale = new Vector2(0.8f, 1.3f);
    [Tooltip("Friction for boxes, so they don't skate when shoved.")]
    [SerializeField] private PhysicsMaterial boxPhysics;
    [Tooltip("How far enemies and crates spawn from a group of boxes.")]
    [SerializeField] private float clutterClearance = 1.4f;

    [Header("Trash")]
    [Tooltip("Flat litter lying on the floor: newspapers, shards, wrappers and the like.")]
    [SerializeField] private GameObject[] floorLitter = new GameObject[0];
    [Tooltip("Small things that get knocked about: cans, bottles, cups, bags.")]
    [SerializeField] private GameObject[] smallTrash = new GameObject[0];
    [Tooltip("Big pieces: cinder blocks, tyres, trash bags.")]
    [SerializeField] private GameObject[] largeTrash = new GameObject[0];
    [SerializeField] private Vector2Int litterPerCell = new Vector2Int(8, 14);
    [SerializeField] private Vector2Int smallTrashPerCell = new Vector2Int(6, 12);
    [SerializeField] private Vector2Int largeTrashPerCell = new Vector2Int(1, 2);
    [Tooltip("Random size multiplier for trash.")]
    [SerializeField] private Vector2 trashScale = new Vector2(0.9f, 1.25f);
    [Tooltip("Chance a can, bottle or cup is lying on its side.")]
    [SerializeField, Range(0f, 1f)] private float tippedChance = 0.4f;

    [Header("Ceiling Lights")]
    [Tooltip("Fluorescent tubes per room cell. Only their light is seen, not a fitting.")]
    [SerializeField, Range(0, 4)] private int lightsPerCell = 2;
    [SerializeField] private float lightHeight = 3.2f;
    [SerializeField] private float lightIntensity = 7f;
    [SerializeField] private float lightRange = 8f;
    [SerializeField, Range(1f, 179f)] private float lightAngle = 120f;
    [SerializeField] private Color lightColour = new Color(0.85f, 1f, 0.93f);
    [Tooltip("Rendering layers the ceiling lights affect. The world only by default, so they don't hot-spot the player's head.")]
    [SerializeField] private uint lightRenderingLayers = 1;

    [Header("Enemy Senses")]
    [Tooltip("Sight, hearing and memory for every enemy, and how far each of the player's noises carries. Changes in Play mode apply straight away.")]
    [SerializeField] private EnemySenses enemySenses = new EnemySenses();

    [Header("Enemies")]
    [Tooltip("Blood left on the floor where an enemy dies: one of these at random each time.")]
    [SerializeField] private Material[] bloodSplatMaterials = new Material[0];
    [Tooltip("Voices for enemies: each one picks one of these at random when it spawns.")]
    [SerializeField] private ZombieSoundPack[] enemySoundPacks = new ZombieSoundPack[0];
    [SerializeField] private Material enemyMaterial;
    [SerializeField] private int minEnemiesPerRoom = 2;
    [SerializeField] private int maxEnemiesPerRoom = 8;
    [Tooltip("Extra enemies for each cell a room has beyond the first.")]
    [SerializeField] private int extraEnemiesPerCell = 2;
    [SerializeField] private int enemyHealth = 3;
    [SerializeField] private float enemySpeed = 3f;
    [SerializeField] private int enemyContactDamage = 1;
    [Tooltip("Boss base health, scaled per boss (Bosses.HealthMultiplier).")]
    [SerializeField] private int bossHealth = 100;
    [SerializeField] private float bossSpeed = 1.8f;
    [SerializeField] private int bossContactDamage = 2;
    [Header("Enemy Kinds")]
    [Tooltip("Relative odds of each kind of zombie in a combat room, and the first floor it can turn up on. Health, speed and damage above are the walker's; other kinds scale from them (see EnemyKinds).")]
    [SerializeField] private EnemyKindOdds[] enemyKinds =
    {
        new EnemyKindOdds { kind = EnemyKind.Walker, weight = 10f, minFloor = 1, maxPerRoom = 0 },
        new EnemyKindOdds { kind = EnemyKind.Runner, weight = 2.5f, minFloor = 1, maxPerRoom = 3 },
        new EnemyKindOdds { kind = EnemyKind.Bloater, weight = 1.5f, minFloor = 1, maxPerRoom = 2 },
        new EnemyKindOdds { kind = EnemyKind.Screamer, weight = 1f, minFloor = 1, maxPerRoom = 1 },
        new EnemyKindOdds { kind = EnemyKind.Brute, weight = 1.5f, minFloor = 2, maxPerRoom = 2 }
    };

    [Serializable]
    private struct EnemyKindOdds
    {
        public EnemyKind kind;
        [Min(0f)] public float weight;
        [Min(1)] public int minFloor;
        [Tooltip("Most of this kind in one room. 0 for no limit.")]
        [Min(0)] public int maxPerRoom;
    }

    [Tooltip("Enemies never spawn within this distance of a doorway, so the player has room to step in.")]
    [SerializeField] private float doorwayClearance = 5f;
    [SerializeField] private float enemySpacing = 1.5f;

    [Header("Ammo")]
    [SerializeField] private Material ammoMaterial;
    [Tooltip("Models for ammo drops and crates. Without one they're plain glowing blocks.")]
    [SerializeField] private AmmoVisuals ammoVisuals;
    [Tooltip("Player, zombie and boss models. Without one they stay placeholder blocks.")]
    [SerializeField] private CharacterVisuals characterVisuals;
    [Tooltip("Models for consumable pickups and thrown items. Without one they're built from primitives.")]
    [SerializeField] private ConsumableVisuals consumableVisuals;
    [Tooltip("Models for crafting-material pickups. Without one they're built from primitives.")]
    [SerializeField] private MaterialVisuals materialVisuals;
    [Tooltip("Models for the hideout stations' bodies. Without one they're built from primitives.")]
    [SerializeField] private StationVisuals stationVisuals;
    [Tooltip("Furniture models (drums, crates, bunks, lockers...) for the hideout and the themed rooms of a floor. "
        + "Without one the hideout's are tinted blocks and rooms get no theme furniture.")]
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("hideoutPropVisuals")] private FurnitureVisuals furnitureVisuals;
    [Tooltip("Chance a combat room is themed (storage, infirmary, barracks, workshop) and furnished to match.")]
    [SerializeField, Range(0f, 1f)] private float roomThemeChance = 0.7f;
    [Tooltip("Furniture pieces per cell of a themed room (min, max).")]
    [SerializeField] private Vector2Int furniturePerCell = new Vector2Int(1, 3);
    [Header("Room Cleared Sound")]
    [Tooltip("Played when the last enemy in a room dies. Leave empty to use a generated chime.")]
    [SerializeField] private AudioClip roomClearedSound;
    [SerializeField, Range(0f, 1f)] private float roomClearedVolume = 0.45f;

    [Header("Footsteps")]
    [Tooltip("Footstep clips for the player and enemies, one picked at random per step. Leave empty for generated steps.")]
    [SerializeField] private AudioClip[] footstepSounds;

    [Header("Suppressed Shot")]
    [Tooltip("Played by every gun with a suppressor instead of its own shot. Leave empty to play the gun's own shot quieter.")]
    [SerializeField] private AudioClip suppressedShotSound;

    [Header("Scream")]
    [Tooltip("The scream for screamers, the Butcher (pitched down) and the Howler (warbled). Leave empty for a generated scream.")]
    [SerializeField] private AudioClip screamSound;

    [Header("Music")]
    [Tooltip("Looping track for the main menu and the hideout.")]
    [SerializeField] private AudioClip hideoutMusic;
    [Tooltip("Looping track for the floors.")]
    [SerializeField] private AudioClip backgroundMusic;
    [Tooltip("Looping track that takes over while a boss fight is on.")]
    [SerializeField] private AudioClip bossMusic;

    [Header("Treasure")]
    [SerializeField] private GameObject pickupPrefab;
    [SerializeField] private GameObject[] treasureWeapons;

    [Header("Attachments")]
    [Tooltip("Every weapon attachment that can turn up, with its stats and drop odds.")]
    [SerializeField] private WeaponAttachment[] attachments = WeaponAttachment.Defaults();

    [Header("Loot")]
    [Tooltip("What loot spawn points can give, and every drop chance and count (enemy, room-clear, boss and treasure drops). "
        + "Without one, loot points stay empty and the other drops use the defaults.")]
    [SerializeField] private LootTable lootTable;
    [Tooltip("Combat rooms of one cell get one floor point; bigger rooms get this many.")]
    [SerializeField, Min(0)] private int floorPointsLargeRoom = 2;
    [Tooltip("Wall points per combat room, on walls with no door.")]
    [SerializeField, Min(0)] private int wallPointsPerRoom = 2;
    [Tooltip("How far out from the wall's face ammo on a wall point sits.")]
    [SerializeField] private float wallAmmoGap = 0.8f;

    [Header("Wall Pickups")]
    [Tooltip("Height of guns mounted on wall loot points.")]
    [SerializeField] private float pickupMountHeight = 1.1f;
    [Tooltip("How far out from the wall's face the mounted pickup sits.")]
    [SerializeField] private float pickupWallGap = 0.35f;

    [Header("Player")]
    [SerializeField] private Transform player;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1f, -2f);

    public event Action<Room> RoomEntered;

    public IReadOnlyList<Room> Rooms => rooms;
    public Room CurrentRoom { get; private set; }
    public AudioClip HideoutMusic => hideoutMusic;
    public AudioClip BackgroundMusic => backgroundMusic;
    public AudioClip BossMusic => bossMusic;
    public int CurrentSeed { get; private set; }
    public int FloorNumber { get; private set; } = 1;
    public int TotalFloors => totalFloors;
    public int EnemiesKilled { get; private set; }

    // Floors of this run whose boss is dead: the ones before this, and this one once its boss
    // room is cleared. What the run's scrap counts.
    public int FloorsCleared
    {
        get
        {
            foreach (Room room in rooms)
            {
                if (room.Type == RoomType.Boss && room.IsCleared)
                    return FloorNumber;
            }

            // The supply room lies under the last floor, whose boss is dead.
            return IsSupplyRoom ? FloorNumber : Mathf.Max(0, FloorNumber - 1);
        }
    }

    // Floor 0 is the hideout: the hub with the stations, the rescued survivors and the hatch to floor 1.
    public bool IsHideout => FloorNumber == 0;
    public string FloorName => IsHideout ? "HIDEOUT" : IsSupplyRoom ? "SUPPLY ROOM" : $"FLOOR {FloorNumber}";

    // When floor 1 of this run began, for the victory screen's mission time.
    public float RunStartTime { get; private set; }

    private bool runPerksApplied;

    // The ammo drop chance after the hideout's Scavenger upgrade.
    private float EnemyAmmoDropChance => Mathf.Clamp01(Drops.enemyAmmoDropChance + 0.1f * MetaProgress.Level(MetaUpgrade.Scavenger));

    // The drop chances and counts: the assigned loot table, or one with the default values.
    private LootTable fallbackDrops;
    private LootTable Drops
    {
        get
        {
            if (lootTable != null)
                return lootTable;

            if (fallbackDrops == null)
                fallbackDrops = ScriptableObject.CreateInstance<LootTable>();

            return fallbackDrops;
        }
    }
    public Vector2 RoomSize => roomSize;
    public float WallThickness => wallThickness;

    // Distance between the centres of neighbouring cells.
    public Vector2 CellStep => roomSize + Vector2.one * (wallThickness * 2f + corridorLength);

    private readonly List<Room> rooms = new List<Room>();
    private readonly Dictionary<Vector2Int, Room> roomsByCell = new Dictionary<Vector2Int, Room>();
    private readonly Dictionary<Room, Vector3> lastDeaths = new Dictionary<Room, Vector3>();
    private readonly Dictionary<Room, List<Vector3>> pillars = new Dictionary<Room, List<Vector3>>();
    private readonly Dictionary<Room, List<Vector3>> clutter = new Dictionary<Room, List<Vector3>>();
    private NavMeshDataInstance navMesh;
    private readonly List<Mesh> floorMeshes = new List<Mesh>();
    private Transform floorRoot;
    private System.Random rng;

    // A room as planned by the layout step, before anything is built.
    private class PlannedRoom
    {
        public RoomShape Shape;
        public List<Vector2Int> Cells;
        public RoomType Type = RoomType.Normal;
        public int Parent = -1;
        public int Depth;
        public int Children;

        // The door to the parent: from ParentCell (in the parent) towards Cells[0] (in this room).
        public Vector2Int ParentCell;
        public Vector2Int DoorDirection;
    }

    private void Awake()
    {
        UseSpriteArt();
        Prop.LitterPrefabs = floorLitter;
        EnemySenses.Use(enemySenses);
        AmmoVisuals.Use(ammoVisuals);
        ConsumableVisuals.Use(consumableVisuals);
        MaterialVisuals.Use(materialVisuals);
        StationVisuals.Use(stationVisuals);
        CharacterVisuals.Use(characterVisuals);
        Footsteps.UseClips(footstepSounds);
        ScreamSound.Use(screamSound);
        Gun.SuppressedShot = suppressedShotSound;

        // Thrown things and effects are built from the pickups' material, even before any
        // pickup has spawned.
        if (ammoMaterial != null)
            ConsumablePickup.DefaultMaterial = ammoMaterial;
    }

    // With the sprite art on: makes its visuals current (characters and guns with sheets become
    // sprites) and swaps the room and effect materials for the pixel-art ones it has.
    private void UseSpriteArt()
    {
        SpriteVisuals visuals = spriteArt ? spriteVisuals : null;
        SpriteVisuals.Use(visuals);
        pixelWalls = false;

        if (visuals == null)
            return;


        if (visuals.floor != null)
            floorMaterial = visuals.floor;

        if (visuals.floorVariant != null)
            floorVariants = new[] { visuals.floorVariant };

        if (visuals.corridorFloor != null)
            corridorFloorMaterial = visuals.corridorFloor;

        if (visuals.door != null)
            doorMaterial = visuals.door;

        if (visuals.wall != null)
        {
            wallMaterial = visuals.wall;
            pixelWalls = true;
        }
    }

    // Keeps the live settings pointing at this copy if it's edited during Play.
    private void OnValidate()
    {
        if (Application.isPlaying)
            EnemySenses.Use(enemySenses);
    }

    private void Start()
    {
        if (RunFlow.ConsumeHideoutRequest())
            FloorNumber = 0;

        // The player's capsule is 2 tall around its middle; the model stands on the floor.
        CharacterVisuals.AttachToPlayer(player, 1f);

        Generate();
    }

    private void Update()
    {
        TrackCurrentRoom();
    }

    // The current room is whichever room owns the cell the player stands in, the same rule the
    // camera uses. Locking is checked here too, so it works for rooms of any shape.
    private void TrackCurrentRoom()
    {
        if (player == null || !TryGetRoom(player.position, out Room room))
            return;

        if (room != CurrentRoom)
        {
            CurrentRoom = room;
            room.NotifyPlayerEntered();
            RoomEntered?.Invoke(room);
        }

        room.UpdatePlayer(player.position);
    }

    public void Generate()
    {
        Clear();

        CurrentSeed = randomSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : seed;
        rng = new System.Random(CurrentSeed);

        // Guns fire for free in the hideout (its shooting range), never on a run.
        Gun.FreeAmmo = IsHideout;

        if (IsHideout)
        {
            GenerateHideout();
            return;
        }

        if (IsSupplyRoom)
        {
            GenerateSupplyRoom();
            return;
        }

        List<PlannedRoom> plan = GenerateLayout();

        floorRoot = new GameObject($"Floor {FloorNumber}").transform;
        floorRoot.SetParent(transform, false);

        // Which cell edges get a doorway, so walls can leave a gap there.
        HashSet<(Vector2Int, Vector2Int)> doorways = new HashSet<(Vector2Int, Vector2Int)>();

        foreach (PlannedRoom planned in plan)
        {
            if (planned.Parent < 0)
                continue;

            doorways.Add((planned.ParentCell, planned.DoorDirection));
            doorways.Add((planned.Cells[0], -planned.DoorDirection));
        }

        foreach (PlannedRoom planned in plan)
        {
            BuildRoom(planned, doorways);
        }

        foreach (PlannedRoom planned in plan)
        {
            if (planned.Parent >= 0)
                BuildCorridor(planned.ParentCell, planned.DoorDirection);
        }

        // The cage is solid, so it goes in before the navmesh.
        PlaceCagedSurvivor(plan);

        // Once all the walls, doors and pillars are up, and before anything moves in.
        BuildNavMesh();

        // Enemies go in last, once every room knows where its doors are.
        foreach (Room room in rooms)
        {
            // Loot first, then boxes kept clear of it, then enemies kept clear of both.
            List<Vector3> lootFloorPoints = SpawnLoot(room);
            FurnishRoom(room);
            SpawnClutter(room);
            SpawnTrash(room);
            List<Vector3> placed = SpawnEnemies(room);
            SpawnTreasureCrate(room);
            placed.AddRange(lootFloorPoints);
            SpawnAmmoCrate(room, placed);
        }

        PlacePlayer();
        ApplyRecon();

        if (FloorNumber == 1 && !runPerksApplied)
            StartCoroutine(BeginRun());

        Debug.Log($"Generated floor {FloorNumber}: {rooms.Count} rooms, seed {CurrentSeed}");
    }

    public void Clear()
    {
        if (floorRoot != null)
        {
            Destroy(floorRoot.gameObject);
        }

        rooms.Clear();
        roomsByCell.Clear();
        lastDeaths.Clear();
        pillars.Clear();
        clutter.Clear();

        foreach (Mesh mesh in floorMeshes)
            Destroy(mesh);

        floorMeshes.Clear();
        CurrentRoom = null;

        if (navMesh.valid)
            navMesh.Remove();
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        Vector2 step = CellStep;
        return new Vector3(cell.x * step.x, 0f, cell.y * step.y);
    }

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        Vector2 step = CellStep;

        return new Vector2Int(
            Mathf.RoundToInt(worldPosition.x / step.x),
            Mathf.RoundToInt(worldPosition.z / step.y)
        );
    }

    public bool TryGetRoom(Vector3 worldPosition, out Room room)
    {
        return roomsByCell.TryGetValue(WorldToCell(worldPosition), out room);
    }

    // A room shows on the maps once you've been in it or through a door next to it, or if it was
    // scouted (Recon).
    public bool IsKnown(Room room)
    {
        if (room.Visited || room.Scouted)
            return true;

        foreach (RoomConnection connection in room.Connections)
        {
            if (connection.Neighbour.Visited)
                return true;
        }

        return false;
    }

    // Whether the corridor between two rooms shows on the maps: once either side has been
    // entered, or both were scouted.
    public static bool IsCorridorKnown(Room room, Room neighbour)
    {
        return room.Visited || neighbour.Visited || (room.Scouted && neighbour.Scouted);
    }

    // The area the camera may show: the whole room up to its doors. Neighbouring rooms' areas
    // meet at the doors, so the camera slides over to the next room as the player walks through.
    public bool TryGetCameraArea(Vector3 worldPosition, out Rect area)
    {
        if (!TryGetRoom(worldPosition, out Room room))
        {
            area = default;
            return false;
        }

        area = room.Bounds;
        return true;
    }

    // Where a connection's corridor runs, in world XZ: from the edge of one room's floor to the
    // edge of the other's. Used by the maps.
    public void GetCorridorEnds(RoomConnection connection, out Vector2 from, out Vector2 to)
    {
        Vector2 direction = connection.Direction;
        float halfExtent = connection.Direction.x != 0 ? roomSize.x / 2f : roomSize.y / 2f;
        Vector3 start = CellToWorld(connection.Cell);
        Vector3 end = CellToWorld(connection.Cell + connection.Direction);

        from = new Vector2(start.x, start.z) + direction * halfExtent;
        to = new Vector2(end.x, end.z) - direction * halfExtent;
    }

    // ---------- Floor progression ----------

    // The boss is dead: the hatch down opens, and an uplink turns up to send materials home. Not
    // on the last floor: its hatch goes down to the supply room, and climbing out of there brings
    // everything home anyway.
    private void OnBossRoomCleared(Room room)
    {
        PlayRoomCleared();
        SpawnExit(room, FloorExit.Kind.NextFloor);

        if (FloorNumber < totalFloors)
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

    // Which wall of that cell the uplink stands against: the first outside wall with no door.
    private static Vector2Int UplinkWall(Room room, Vector2Int cell)
    {
        foreach (Vector2Int side in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.left, Vector2Int.down })
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

    // A drop point pulled in off the walls: kept inside the room's cell it's in (or the room's
    // nearest cell), at least `margin` from the walls, so nothing lands in or behind one.
    public Vector3 InsideRoom(Room room, Vector3 position, float margin = 0.8f)
    {
        Vector2Int cell = WorldToCell(position);

        if (!room.Contains(cell))
        {
            float best = float.MaxValue;

            foreach (Vector2Int candidate in room.Cells)
            {
                float distance = (CellToWorld(candidate) - position).sqrMagnitude;

                if (distance < best)
                {
                    best = distance;
                    cell = candidate;
                }
            }
        }

        Vector3 centre = CellToWorld(cell);
        Vector2 half = new Vector2(Mathf.Max(0f, roomSize.x / 2f - margin), Mathf.Max(0f, roomSize.y / 2f - margin));
        return new Vector3(
            Mathf.Clamp(position.x, centre.x - half.x, centre.x + half.x),
            position.y,
            Mathf.Clamp(position.z, centre.z - half.y, centre.z + half.y));
    }

    // Higher for small, quick kinds, lower for big ones.
    private static float VoicePitch(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Runner: return 1.2f;
            case EnemyKind.Brute: return 0.75f;
            case EnemyKind.Bloater: return 0.85f;
            case EnemyKind.Screamer: return 1.3f;
            default: return 1f;
        }
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

        // Under the last floor: the supply room.
        if (FloorNumber >= totalFloors && !IsHideout)
        {
            transition.Play("> DESCENDING TO THE SUPPLY ROOM", "SUPPLY ROOM // NO HOSTILES DETECTED", EnterSupplyRoom);
            return;
        }

        transition.Play(FloorNumber + 1, totalFloors, AdvanceFloor);
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

    // The hatch model's opening (square) and the height of its rim, where the doors hinge.
    private const float HatchOpening = 2.2f;
    private const float HatchRimHeight = 0.18f;

    // A model instance that's only for looking at: no colliders, named.
    private static void VisualOnly(GameObject instance, string name)
    {
        instance.name = name;

        foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
            Destroy(collider);
    }

    // A block with no collider, so only the exit's trigger affects the player.
    private void CreateVisual(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        GameObject block = CreateBlock(name, parent, localPosition, size, material);
        Destroy(block.GetComponent<Collider>());
    }

    // ---------- Layout ----------

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

    // ---------- Building ----------

    // Each cell is split into a 3x3 grid of blocks: the floor in the middle, and the strips of
    // wall-and-corridor space around it. The middle is always part of the room; a side strip is
    // part of it when the neighbouring cell on that side is the same room, and a corner when all
    // three cells around that corner are. Walls go wherever a room block meets a non-room block,
    // which gives closed walls for any shape, including the inside corners of L and T rooms.
    private void BuildRoom(PlannedRoom planned, HashSet<(Vector2Int, Vector2Int)> doorways)
    {
        Vector2Int anchor = planned.Cells[0];
        GameObject roomObject = new GameObject($"Room {anchor.x},{anchor.y} {planned.Shape.Name} ({planned.Type})");
        roomObject.transform.SetParent(floorRoot, false);
        roomObject.transform.localPosition = CellToWorld(anchor);

        Room room = roomObject.AddComponent<Room>();
        room.Initialise(planned.Type, planned.Shape.Name, planned.Cells, CellStep, roomSize);
        rooms.Add(room);

        foreach (Vector2Int cell in planned.Cells)
            roomsByCell[cell] = room;

        List<Rect> blocks = new List<Rect>();
        List<RoomEdge> outline = new List<RoomEdge>();
        Material roomFloor = PickFloorMaterial(planned.Type);

        foreach (Vector2Int cell in planned.Cells)
        {
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    if (!InRoom(room, cell, i, j))
                        continue;

                    Rect block = BlockRect(cell, i, j);
                    blocks.Add(block);

                    GameObject floor = CreateBlock(
                        "Floor",
                        roomObject.transform,
                        ToLocal(roomObject.transform, new Vector3(block.center.x, -0.1f, block.center.y)),
                        new Vector3(block.width, 0.2f, block.height),
                        roomFloor
                    );

                    ApplyWorldUVs(floor);
                    ApplyTint(floor, planned.Type);

                    foreach (Vector2Int direction in Directions)
                    {
                        NeighbourBlock(cell, i, j, direction, out Vector2Int nCell, out int ni, out int nj);

                        if (InRoom(room, nCell, ni, nj))
                            continue;

                        bool doorway = i == 0 && j == 0 && doorways.Contains((cell, direction));
                        BuildWall(room, cell, i, j, block, direction, doorway);
                        outline.Add(EdgeOf(block, direction));
                    }
                }
            }
        }

        room.SetGeometry(blocks, outline);

        if (planned.Type == RoomType.Normal && planned.Cells.Count >= minCellsForPillars && rng.NextDouble() < pillarChance)
            BuildPillars(room);

        AddCeilingLights(room);
    }

    private static bool InRoom(Room room, Vector2Int cell, int i, int j)
    {
        if (!room.Contains(cell))
            return false;

        if (i == 0 && j == 0)
            return true;

        if (j == 0)
            return room.Contains(cell + new Vector2Int(i, 0));

        if (i == 0)
            return room.Contains(cell + new Vector2Int(0, j));

        return room.Contains(cell + new Vector2Int(i, 0))
            && room.Contains(cell + new Vector2Int(0, j))
            && room.Contains(cell + new Vector2Int(i, j));
    }

    // The block next to (cell, i, j) in a direction, which may be in the neighbouring cell.
    private static void NeighbourBlock(Vector2Int cell, int i, int j, Vector2Int direction,
        out Vector2Int nCell, out int ni, out int nj)
    {
        nCell = cell;
        ni = i + direction.x;
        nj = j + direction.y;

        if (ni > 1) { ni = -1; nCell.x++; }
        if (ni < -1) { ni = 1; nCell.x--; }
        if (nj > 1) { nj = -1; nCell.y++; }
        if (nj < -1) { nj = 1; nCell.y--; }
    }

    // World XZ rect of block (i, j) of a cell: i/j of 0 is the floor, -1/+1 the strips either side.
    private Rect BlockRect(Vector2Int cell, int i, int j)
    {
        Vector3 centre = CellToWorld(cell);
        Vector2 step = CellStep;

        (float xMin, float xMax) = BlockSpan(centre.x, roomSize.x, step.x, i);
        (float zMin, float zMax) = BlockSpan(centre.z, roomSize.y, step.y, j);

        return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
    }

    private static (float, float) BlockSpan(float centre, float floor, float step, int index)
    {
        if (index < 0) return (centre - step / 2f, centre - floor / 2f);
        if (index > 0) return (centre + floor / 2f, centre + step / 2f);
        return (centre - floor / 2f, centre + floor / 2f);
    }

    private static RoomEdge EdgeOf(Rect block, Vector2Int direction)
    {
        Vector2 outward = direction;

        if (direction.x > 0) return new RoomEdge { From = new Vector2(block.xMax, block.yMin), To = new Vector2(block.xMax, block.yMax), Outward = outward };
        if (direction.x < 0) return new RoomEdge { From = new Vector2(block.xMin, block.yMin), To = new Vector2(block.xMin, block.yMax), Outward = outward };
        if (direction.y > 0) return new RoomEdge { From = new Vector2(block.xMin, block.yMax), To = new Vector2(block.xMax, block.yMax), Outward = outward };
        return new RoomEdge { From = new Vector2(block.xMin, block.yMin), To = new Vector2(block.xMax, block.yMin), Outward = outward };
    }

    // The floor a room gets: the normal one, or now and then (combat rooms only) a variant.
    private Material PickFloorMaterial(RoomType type)
    {
        if (type != RoomType.Normal || floorVariants == null || floorVariants.Length == 0 || rng.NextDouble() >= floorVariantChance)
            return floorMaterial;

        Material variant = floorVariants[rng.Next(floorVariants.Length)];
        return variant != null ? variant : floorMaterial;
    }

    // A wall just outside one side of a room block. At each end it runs on by a wall's thickness
    // when that end is an outside corner, so corners close; at inside corners and where the wall
    // carries on along the next block it stops at the block edge.
    private void BuildWall(Room room, Vector2Int cell, int i, int j, Rect block, Vector2Int direction, bool doorway)
    {
        bool alongX = direction.y != 0;
        Vector2Int along = alongX ? Vector2Int.right : Vector2Int.up;

        float start = alongX ? block.xMin : block.yMin;
        float end = alongX ? block.xMax : block.yMax;

        NeighbourBlock(cell, i, j, -along, out Vector2Int c0, out int i0, out int j0);
        NeighbourBlock(cell, i, j, along, out Vector2Int c1, out int i1, out int j1);

        if (!InRoom(room, c0, i0, j0)) start -= wallThickness;
        if (!InRoom(room, c1, i1, j1)) end += wallThickness;

        // Position across the wall: just outside the block on the given side.
        float across = direction.x > 0 ? block.xMax + wallThickness / 2f
            : direction.x < 0 ? block.xMin - wallThickness / 2f
            : direction.y > 0 ? block.yMax + wallThickness / 2f
            : block.yMin - wallThickness / 2f;

        bool south = direction.y < 0;

        if (!doorway)
        {
            HideIfSouth(CreateWallSpan(room.transform, alongX, across, start, end), south);
            return;
        }

        float middle = alongX ? block.center.x : block.center.y;
        HideIfSouth(CreateWallSpan(room.transform, alongX, across, start, middle - doorWidth / 2f), south);
        HideIfSouth(CreateWallSpan(room.transform, alongX, across, middle + doorWidth / 2f, end), south);
        CreateDoorFrame(room.transform, alongX, across, middle, new Vector3(-direction.x, 0f, -direction.y));
        CreateDoorStripe(room.transform, alongX, across - (direction.x + direction.y) * (wallThickness / 2f + 0.3f), middle);
    }

    // A worn yellow-and-black stripe painted on the floor across a doorway, on the room side.
    private void CreateDoorStripe(Transform parent, bool alongX, float across, float middle)
    {
        if (hazardStripeMaterial == null)
            return;

        GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Quad);
        stripe.name = "Door Stripe";
        Destroy(stripe.GetComponent<Collider>());
        stripe.transform.SetParent(parent, false);
        stripe.transform.position = alongX ? new Vector3(middle, 0.012f, across) : new Vector3(across, 0.012f, middle);
        stripe.transform.rotation = Quaternion.Euler(90f, alongX ? 0f : 90f, 0f);
        stripe.transform.localScale = new Vector3(doorWidth, 0.45f, 1f);

        Renderer renderer = stripe.GetComponent<Renderer>();
        renderer.sharedMaterial = hazardStripeMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Steel posts either side of a doorway, standing a little proud of the wall on both faces
    // and above it, so doorways read clearly from above, with flashing amber lights on them
    // (DoorBeacon) so they're easy to find in the dark. Just for looks: no colliders (the wall
    // ends do the blocking), so nothing catches on them. `inward` points into the room.
    private void CreateDoorFrame(Transform parent, bool alongX, float across, float middle, Vector3 inward)
    {
        const float postWidth = 0.16f;
        Vector3 size = WallSize(alongX, postWidth, wallThickness + 0.1f) + Vector3.up * 0.06f;
        Vector3[] posts = new Vector3[2];

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float along = middle + side * (doorWidth / 2f + postWidth / 2f - 0.02f);
            posts[i] = alongX
                ? new Vector3(along, (wallHeight + 0.06f) / 2f, across)
                : new Vector3(across, (wallHeight + 0.06f) / 2f, along);

            if (wallTrimMaterial != null)
            {
                GameObject post = CreateBlock("Door Frame", parent, ToLocal(parent, posts[i]), size, wallTrimMaterial);
                Destroy(post.GetComponent<Collider>());
                HideIfSouth(post, inward.z > 0.5f);
            }
        }

        DoorBeacon.Create(parent, posts[0], posts[1], wallHeight + 0.06f, inward, postWidth, wallThickness, ammoMaterial);
    }

    // A wall on the side nearest the camera: drawn only into the shadows (see hideSouthWalls).
    private void HideIfSouth(GameObject wall, bool south)
    {
        if (wall == null || !south || !hideSouthWalls)
            return;

        foreach (Renderer part in wall.GetComponentsInChildren<Renderer>())
            part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
    }

    private GameObject CreateWallSpan(Transform parent, bool alongX, float across, float start, float end)
    {
        float length = end - start;

        if (length <= 0.001f)
            return null;

        float middle = (start + end) / 2f;
        Vector3 world = alongX
            ? new Vector3(middle, wallHeight / 2f, across)
            : new Vector3(across, wallHeight / 2f, middle);

        return CreateWall("Wall", parent, ToLocal(parent, world), WallSize(alongX, length, wallThickness));
    }

    // ---------- Navigation ----------

    // A navmesh over this floor's rooms and corridors, for enemies to path around pillars and
    // corners. Walls, doors and pillars are obstacles (and their tops aren't walkable). Built
    // from the floor's colliders before enemies and pickups exist, so they aren't baked in.
    private void BuildNavMesh()
    {
        if (navMesh.valid)
            navMesh.Remove();

        // The floor was built this frame; make sure physics knows where its colliders are.
        Physics.SyncTransforms();

        List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(floorRoot, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);

        const int notWalkable = 1;

        for (int i = 0; i < sources.Count; i++)
        {
            NavMeshBuildSource source = sources[i];

            if (source.component != null && source.component.CompareTag("Wall"))
            {
                source.area = notWalkable;
                sources[i] = source;
            }
        }

        // Everything built is a block with a renderer, and renderer bounds are always current.
        Bounds bounds = new Bounds(floorRoot.position, Vector3.zero);
        bool first = true;

        foreach (Renderer part in floorRoot.GetComponentsInChildren<Renderer>())
        {
            if (first)
                bounds = part.bounds;
            else
                bounds.Encapsulate(part.bounds);

            first = false;
        }

        bounds.Expand(new Vector3(2f, 4f, 2f));

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = 0.5f;
        settings.agentHeight = 1f;
        settings.agentClimb = 0.2f;

        NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);

        if (data != null)
            navMesh = NavMesh.AddNavMeshData(data);
    }

    private void OnDestroy()
    {
        if (navMesh.valid)
            navMesh.Remove();
    }

    private void BuildCorridor(Vector2Int cell, Vector2Int direction)
    {
        Vector3 forward = new Vector3(direction.x, 0f, direction.y);
        Vector3 side = new Vector3(direction.y, 0f, direction.x);
        bool alongX = direction.x != 0;

        float roomHalfExtent = alongX ? roomSize.x / 2f : roomSize.y / 2f;
        Vector3 centre = CellToWorld(cell) + forward * (roomHalfExtent + wallThickness + corridorLength / 2f);

        GameObject corridor = new GameObject($"Corridor {cell.x},{cell.y} {DirectionName(direction)}");
        corridor.transform.SetParent(floorRoot, false);
        corridor.transform.localPosition = centre;

        // Floor runs through both doorways and under the side walls.
        float floorLength = corridorLength + wallThickness * 2f;
        float floorWidth = doorWidth + wallThickness * 2f;

        GameObject corridorFloor = CreateBlock(
            "Floor",
            corridor.transform,
            new Vector3(0f, -0.1f, 0f),
            alongX ? new Vector3(floorLength, 0.2f, floorWidth) : new Vector3(floorWidth, 0.2f, floorLength),
            corridorFloorMaterial != null ? corridorFloorMaterial : floorMaterial
        );

        ApplyWorldUVs(corridorFloor);

        // One door in the middle of the corridor, shared by the rooms on either side.
        GameObject doorObject = CreateBlock(
            "Door",
            corridor.transform,
            Vector3.up * wallHeight / 2f,
            WallSize(!alongX, doorWidth, wallThickness),
            doorMaterial
        );

        doorObject.tag = "Wall";
        doorObject.GetComponent<Collider>().sharedMaterial = slidePhysics;
        RaiseCollider(doorObject);
        RoomDoor door = doorObject.AddComponent<RoomDoor>();

        Room from = roomsByCell[cell];
        Room to = roomsByCell[cell + direction];
        from.AddConnection(new RoomConnection { Cell = cell, Direction = direction, Neighbour = to, Door = door });
        to.AddConnection(new RoomConnection { Cell = cell + direction, Direction = -direction, Neighbour = from, Door = door });

        if (corridorLength <= 0f)
            return;

        float sideOffset = doorWidth / 2f + wallThickness / 2f;
        Vector3 wallSize = WallSize(alongX, corridorLength, wallThickness);

        // The corridor's south wall (when it runs east-west) is hidden like the rooms' (hideSouthWalls).
        HideIfSouth(CreateWall("Wall", corridor.transform, side * sideOffset + Vector3.up * wallHeight / 2f, wallSize), side.z < -0.5f);
        HideIfSouth(CreateWall("Wall", corridor.transform, -side * sideOffset + Vector3.up * wallHeight / 2f, wallSize), side.z > 0.5f);
    }

    private void PlacePlayer()
    {
        if (player == null)
            return;

        Vector3 spawn = IsHideout ? HideoutSpawn : IsSupplyRoom ? SupplyRoomSpawn : CellToWorld(Vector2Int.zero) + spawnOffset;

        Rigidbody playerBody = player.GetComponent<Rigidbody>();

        if (playerBody != null)
        {
            playerBody.position = spawn;
            playerBody.linearVelocity = Vector3.zero;
        }

        player.position = spawn;
    }

    // ---------- Helpers ----------

    private static Vector3 ToLocal(Transform parent, Vector3 world)
    {
        return parent.InverseTransformPoint(world);
    }

    private GameObject CreateWall(string name, Transform parent, Vector3 localPosition, Vector3 size)
    {
        GameObject wall = CreateBlock(name, parent, localPosition, size, wallMaterial);
        wall.tag = "Wall";
        wall.GetComponent<Collider>().sharedMaterial = slidePhysics;
        RaiseCollider(wall);
        ApplyWallUVs(wall);
        return wall;
    }

    // Stretches a floor-standing wall block's collider up to wallBlockHeight, leaving the block
    // drawn at wallHeight (see wallBlockHeight).
    private void RaiseCollider(GameObject block)
    {
        if (!block.TryGetComponent(out BoxCollider box) || wallBlockHeight <= wallHeight || wallHeight <= 0f)
            return;

        float scale = wallBlockHeight / wallHeight;
        box.size = new Vector3(1f, scale, 1f);
        box.center = new Vector3(0f, (scale - 1f) / 2f, 0f);
    }

    // Like ApplyWorldUVs, but for the painted wall texture: along each face it tiles by world
    // position (so spans line up), and up it runs from the floor to the top of the wall once, so
    // every wall shows the same baseboard, band and blockwork. Tops and bottoms are mapped onto
    // the cap strip at the top of the texture.
    private void ApplyWallUVs(GameObject wall)
    {
        MeshFilter filter = wall.GetComponent<MeshFilter>();

        if (filter == null || filter.sharedMesh == null || wallTextureLength <= 0f || wallHeight <= 0f)
            return;

        Mesh mesh = Instantiate(filter.sharedMesh);
        mesh.name = "Wall (world UVs)";
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uvs = new Vector2[vertices.Length];
        Transform t = wall.transform;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = t.TransformPoint(vertices[i]);
            Vector3 normal = normals[i];

            if (Mathf.Abs(normal.y) > 0.5f)
                uvs[i] = new Vector2((world.x + world.z) / wallTextureLength, pixelWalls ? PixelWallCap : 0.985f);
            else
            {
                float along = Mathf.Abs(normal.x) > 0.5f ? world.z : world.x;
                uvs[i] = new Vector2(along / wallTextureLength, Mathf.Clamp01(world.y / wallHeight) * (pixelWalls ? PixelWallFace : 0.95f));
            }
        }

        mesh.uv = uvs;
        mesh.RecalculateTangents();
        filter.sharedMesh = mesh;
        floorMeshes.Add(mesh);
    }

    private Vector3 WallSize(bool alongX, float length, float thickness)
    {
        return alongX
            ? new Vector3(length, wallHeight, thickness)
            : new Vector3(thickness, wallHeight, length);
    }

    private static GameObject CreateBlock(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = size;

        if (material != null)
        {
            block.GetComponent<Renderer>().sharedMaterial = material;
        }

        return block;
    }

    // A cube's texture covers each face once, so on blocks of different sizes it would stretch
    // differently and not line up. Instead map it by world position (top faces by X/Z, sides by
    // the wall they face), so it tiles evenly across every block and runs seamlessly from one
    // block to the next. Run once the block is in place.
    private void ApplyWorldUVs(GameObject block)
    {
        MeshFilter filter = block.GetComponent<MeshFilter>();

        if (filter == null || filter.sharedMesh == null || floorTextureSize <= 0f)
            return;

        Mesh mesh = Instantiate(filter.sharedMesh);
        mesh.name = "Floor (world UVs)";
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uvs = new Vector2[vertices.Length];
        Transform t = block.transform;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = t.TransformPoint(vertices[i]);
            Vector3 normal = normals[i];

            Vector2 uv = Mathf.Abs(normal.y) > 0.5f ? new Vector2(world.x, world.z)
                : Mathf.Abs(normal.x) > 0.5f ? new Vector2(world.z, world.y)
                : new Vector2(world.x, world.y);

            uvs[i] = uv / floorTextureSize;
        }

        mesh.uv = uvs;
        mesh.RecalculateTangents();   // for the normal map, now the UVs run a different way
        filter.sharedMesh = mesh;
        floorMeshes.Add(mesh);
    }

    private void ApplyTint(GameObject floor, RoomType type)
    {
        Color? tint = type switch
        {
            RoomType.Treasure => treasureTint,
            RoomType.Boss => bossTint,
            _ => null
        };

        if (tint == null)
            return;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", tint.Value);
        floor.GetComponent<Renderer>().SetPropertyBlock(block);
    }

    private static string DirectionName(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return "North";
        if (direction == Vector2Int.right) return "East";
        if (direction == Vector2Int.down) return "South";
        return "West";
    }
}
