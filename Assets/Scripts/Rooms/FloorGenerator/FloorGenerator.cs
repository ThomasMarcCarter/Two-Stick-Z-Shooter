using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Builds an Isaac/Gungeon-style floor: rooms of different shapes (1x1, long, L, T, 2x2) on a
// grid, joined by doors and short corridors. The start room is always at the world origin.
// Beating a floor's boss opens a hatch to the next floor; under the last floor's boss room is the
// supply room, whose ladder out ends the run as a win (FloorGenerator.SupplyRoom.cs).
// Floors are rebuilt in place, so the player (and their health, gun and ammo) carries over.
// Split into partials: the layout plan in FloorGenerator.Layout.cs, building rooms, walls and
// corridors in FloorGenerator.Building.cs (wall modules in .RoomKit.cs, cut-outs in .Cutouts.cs,
// block helpers in .Blocks.cs), room-clear drops and the way down in .Progression.cs, enemies in
// .Spawning.cs, loot in .Loot.cs, boxes/trash/pillars/lights in .Dressing.cs, room themes in
// .Themes.cs, wall and floor decals in .WallArt.cs/.FloorArt.cs, the hideout (floor 0) in
// .Hideout.cs/.HideoutZones.cs, the supply room in .SupplyRoom.cs, the evac roof in .Evac.cs,
// and what each zone of the map changes in .Zones.cs.
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
    [Tooltip("How far across a flattened cardboard sheet on the floor is, in metres.")]
    [SerializeField] private Vector2 flattenedScale = new Vector2(0.6f, 1f);
    [Tooltip("Friction for boxes, so they don't skate when shoved.")]
    [SerializeField] private PhysicsMaterial boxPhysics;
    [Tooltip("How far enemies and crates spawn from a group of boxes.")]
    [SerializeField] private float clutterClearance = 1.4f;

    [Header("Trash")]
    [Tooltip("Flat litter lying on the floor: newspapers, shards, wrappers and the like.")]
    [SerializeField] private GameObject[] floorLitter = new GameObject[0];
    [Tooltip("Small things that get knocked about: cans, bottles, cups, bags.")]
    [SerializeField] private GameObject[] smallTrash = new GameObject[0];
    [Tooltip("Big pieces: trash bags (the tyres were taken out: huge and out of place).")]
    [SerializeField] private GameObject[] largeTrash = new GameObject[0];
    [Tooltip("Fixed cover the zones' big items are built on (ZoneClutter's \"Wheel_1\"); never placed as itself.")]
    [SerializeField] private GameObject coverStandIn;
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
        new EnemyKindOdds { kind = EnemyKind.Brute, weight = 1.5f, minFloor = 2, maxPerRoom = 2 },
        new EnemyKindOdds { kind = EnemyKind.Spitter, weight = 2f, minFloor = 1, maxPerRoom = 2 },
        new EnemyKindOdds { kind = EnemyKind.Gunner, weight = 1.5f, minFloor = 1, maxPerRoom = 1 },
        new EnemyKindOdds { kind = EnemyKind.Lobber, weight = 1.2f, minFloor = 1, maxPerRoom = 1 },
        new EnemyKindOdds { kind = EnemyKind.Pustule, weight = 1.5f, minFloor = 1, maxPerRoom = 2 },
        new EnemyKindOdds { kind = EnemyKind.Leaper, weight = 1.5f, minFloor = 1, maxPerRoom = 2 },
        new EnemyKindOdds { kind = EnemyKind.Retcher, weight = 1f, minFloor = 2, maxPerRoom = 1 }
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
    [Tooltip("Chance each quarter of a cell gets an island (cubicles, lab or work table) in themes that have them.")]
    [SerializeField, Range(0f, 1f)] private float islandChance = 0.6f;
    [Tooltip("Chance each parking bay of a Military Base storage room has a car parked in it.")]
    [SerializeField, Range(0f, 1f)] private float carChance = 0.55f;
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
    public AudioClip BackgroundMusic => ZoneBackgroundMusic;
    public AudioClip BossMusic => ZoneBossMusic;
    public int CurrentSeed { get; private set; }
    public int FloorNumber { get; private set; } = 1;
    public int TotalFloors => ZoneFloors;
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
    public string FloorName => IsHideout ? "HIDEOUT" : IsTutorial ? "TRAINING" : IsEvac ? "ROOF" : IsSupplyRoom ? "SUPPLY ROOM" : $"FLOOR {FloorNumber}";

    // The zone of the map this run is in, read from the save when floor 1 is built. In the
    // hideout, the zone the next run will deploy to.
    public Zone Zone => IsHideout ? MetaProgress.ChosenZone : runZone;
    public string ZoneName => Zones.Name(Zone);

    // Where the player is, zone and floor: "MILITARY BASE // FLOOR 2", "MILITARY BASE // SUPPLY
    // ROOM", "SHOPPING MALL // HIDEOUT".
    public string LocationName => $"{ZoneName} // {FloorName}";

    private Zone runZone;

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
            if (ZoneLootTable != null)
                return ZoneLootTable;

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
        Gun.Fired += OnGunFired;
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

        tutorialRun = FloorNumber == 1 && !MetaProgress.TutorialDone;

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
        decalRng = new System.Random(CurrentSeed ^ 0x5eed);
        kitRng = new System.Random(CurrentSeed ^ 0x4b17);

        // Guns fire for free in the hideout (its shooting range), never on a run.
        Gun.FreeAmmo = IsHideout;

        // A new run starts in the zone chosen in the hideout and stays there.
        if (FloorNumber == 1)
            runZone = MetaProgress.ChosenZone;

        UseZoneArt();
        UseZoneClutter();

        if (IsHideout)
        {
            Tutorial.Attach(this);
            GenerateHideout();
            return;
        }

        if (IsEvac)
        {
            GenerateEvac();
            return;
        }

        if (IsSupplyRoom)
        {
            GenerateSupplyRoom();
            return;
        }

        List<PlannedRoom> plan = GenerateLayout();
        BuildStructure(plan);

        // The cage is solid, so it goes in before the navmesh.
        if (!IsTutorial)
            PlaceCagedSurvivor(plan);

        // Once all the walls, doors and pillars are up, and before anything moves in.
        BuildNavMesh();

        // Enemies go in last, once every room knows where its doors are.
        foreach (Room room in rooms)
            FillRoom(room);

        PlacePlayer();
        ApplyRecon();
        Tutorial.Attach(this);

        if (FloorNumber == 1 && !runPerksApplied)
            StartCoroutine(BeginRun());

        Debug.Log($"Generated floor {FloorNumber}: {rooms.Count} rooms, seed {CurrentSeed}");
    }

    // The floor's solid parts from the plan: every room (with doorways left in its walls where
    // the plan joins rooms), the corridors between them, and the boss room's way down.
    private void BuildStructure(List<PlannedRoom> plan)
    {
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
            BuildRoom(planned, doorways);

        foreach (PlannedRoom planned in plan)
        {
            if (planned.Parent >= 0)
                BuildCorridor(planned.ParentCell, planned.DoorDirection);
        }

        if (IsTutorial)
            ReserveTutorialHatch();

        // The boss room's way down (shut until the boss is dead) is solid too.
        foreach (Room room in rooms)
        {
            if (room.Type == RoomType.Boss)
                PlaceZoneExit(room);
        }
    }

    // Everything that goes into a built room: loot first, then furniture, decals, boxes and
    // trash kept clear of it, then enemies kept clear of all that, then the crates.
    private void FillRoom(Room room)
    {
        List<Vector3> lootFloorPoints = SpawnLoot(room);
        RoomTheme theme = FurnishRoom(room);
        DressLowWalls(room, theme);
        DecorateWalls(room, theme);
        PaintFloor(room, theme);
        AddRoomLife(room, theme);
        SpawnClutter(room);
        SpawnTrash(room);
        AddDraught(room);
        List<Vector3> placed = SpawnEnemies(room);
        SpawnTreasureCrate(room);
        placed.AddRange(lootFloorPoints);
        SpawnAmmoCrate(room, placed);
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
        wallFeatures.Clear();
        exitSpots.Clear();

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

        // Far enough north to see the way down set into the boss room's north wall (ZoneExit).
        if (exitSpots.TryGetValue(room, out Vector3 exit))
            area.yMax = Mathf.Max(area.yMax, exit.z + ExitCameraReach);

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
        Gun.Fired -= OnGunFired;

        if (navMesh.valid)
            navMesh.Remove();
    }

    private void PlacePlayer()
    {
        // Back from walking into the last floor's way down (ZoneExit).
        ZoneExit.FinishDescent();

        if (player == null)
            return;

        Vector3 spawn = IsHideout ? HideoutSpawn : IsEvac ? EvacSpawn : IsSupplyRoom ? SupplyRoomSpawn : CellToWorld(Vector2Int.zero) + spawnOffset;

        Rigidbody playerBody = player.GetComponent<Rigidbody>();

        if (playerBody != null)
        {
            playerBody.position = spawn;
            playerBody.linearVelocity = Vector3.zero;
        }

        player.position = spawn;
    }
}
