using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Builds an Isaac/Gungeon-style floor: rooms of different shapes (1x1, long, L, T, 2x2) on a
// grid, joined by doors and short corridors. The start room is always at the world origin.
// Beating a floor's boss opens a hatch to the next floor; the last floor's boss leaves a trophy.
// Floors are rebuilt in place, so the player (and their health, gun and ammo) carries over.
public class FloorGenerator : MonoBehaviour
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    [Header("Floors")]
    [SerializeField] private int totalFloors = 3;
    [SerializeField] private Material exitMaterial;
    [SerializeField] private Material trophyMaterial;
    [Tooltip("Seconds after the boss dies before the hatch or trophy can be used.")]
    [SerializeField] private float exitArmDelay = 1f;
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
    [SerializeField] private float wallThickness = 0.5f;
    [SerializeField] private float doorWidth = 2f;
    [SerializeField] private float corridorLength = 3f;

    [Header("Materials")]
    [SerializeField] private Material floorMaterial;
    [Tooltip("World units one repeat of the floor texture covers. The floor's texture tiles across the world, so blocks of any size show it at the same scale and line up.")]
    [SerializeField] private float floorTextureSize = 4f;
    [SerializeField] private Material wallMaterial;
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
    [Tooltip("Height of a pillar. A little over the walls looks like it holds the ceiling.")]
    [SerializeField] private float pillarHeight = 2.5f;
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
    [Tooltip("Big pieces: barriers, cinder blocks, tyres, trash bags.")]
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
    [SerializeField] private int bossHealth = 25;
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
    [Tooltip("Chance an enemy drops ammo when it dies. Bosses always drop.")]
    [SerializeField, Range(0f, 1f)] private float enemyAmmoDropChance = 0.3f;
    [Tooltip("Chance a combat room has an ammo crate in it.")]
    [SerializeField, Range(0f, 1f)] private float ammoCrateChance = 0.4f;
    [Tooltip("How many times a normal drop a crate (or a boss drop) holds.")]
    [SerializeField] private float crateMultiplier = 2.5f;
    [Tooltip("Chance a drop is for one of the player's current guns, rather than any type.")]
    [SerializeField, Range(0f, 1f)] private float heldGunAmmoBias = 0.75f;

    [Header("Health")]
    [Tooltip("Chance a medkit drops when a combat room is cleared.")]
    [SerializeField, Range(0f, 1f)] private float medkitChance = 0.3f;
    [Tooltip("Of those, the chance it's a large medkit (+2) rather than small (+1).")]
    [SerializeField, Range(0f, 1f)] private float largeMedkitChance = 0.15f;

    [Header("Supplies")]
    [Tooltip("Chance a cleared combat room also leaves a grenade, molotov, bottle or adrenaline.")]
    [SerializeField, Range(0f, 1f)] private float supplyChance = 0.3f;
    [Tooltip("Relative odds of each kind of supply drop.")]
    [SerializeField] private SupplyOdds[] supplyOdds =
    {
        new SupplyOdds { type = ConsumableType.Bottle, weight = 3f, minFloor = 1 },
        new SupplyOdds { type = ConsumableType.Grenade, weight = 2f, minFloor = 1 },
        new SupplyOdds { type = ConsumableType.Molotov, weight = 2f, minFloor = 1 },
        new SupplyOdds { type = ConsumableType.Adrenaline, weight = 1.5f, minFloor = 1 }
    };
    [Header("Melee Upgrades")]
    [Tooltip("Chance a cleared combat room also leaves a melee upgrade.")]
    [SerializeField, Range(0f, 1f)] private float meleeUpgradeChance = 0.12f;
    [Tooltip("Melee upgrades the boss drops.")]
    [SerializeField] private int bossMeleeUpgrades = 1;
    [Tooltip("Melee upgrades the treasure room always has.")]
    [SerializeField] private int treasureMeleeUpgrades = 1;

    [Tooltip("Consumables the treasure room always has, besides its weapon and attachments.")]
    [SerializeField] private ConsumableType[] treasureSupplies = { ConsumableType.Grenade, ConsumableType.LargeMedkit };

    [Serializable]
    private struct SupplyOdds
    {
        public ConsumableType type;
        [Min(0f)] public float weight;
        [Min(1)] public int minFloor;
    }

    [Header("Room Cleared Sound")]
    [Tooltip("Played when the last enemy in a room dies. Leave empty to use a generated chime.")]
    [SerializeField] private AudioClip roomClearedSound;
    [SerializeField, Range(0f, 1f)] private float roomClearedVolume = 0.45f;

    [Header("Treasure")]
    [SerializeField] private GameObject pickupPrefab;
    [SerializeField] private GameObject[] treasureWeapons;

    [Header("Attachments")]
    [Tooltip("Every weapon attachment that can turn up, with its stats and drop odds.")]
    [SerializeField] private WeaponAttachment[] attachments = WeaponAttachment.Defaults();
    [Tooltip("Attachments always waiting in the treasure room, either side of its weapon.")]
    [SerializeField, Min(0)] private int treasureAttachments = 2;
    [Tooltip("Attachments the boss always drops.")]
    [SerializeField, Min(0)] private int bossAttachments = 2;
    [Tooltip("Chance each one is picked from those that fit a gun the player is carrying, rather than any.")]
    [SerializeField, Range(0f, 1f)] private float attachmentFitBias = 0.7f;

    [Header("Loot")]
    [Tooltip("What loot spawn points can give, and their chances. No loot spawns if empty.")]
    [SerializeField] private LootTable lootTable;
    [Tooltip("Combat rooms of one cell get one floor point; bigger rooms get this many.")]
    [SerializeField, Min(0)] private int floorPointsLargeRoom = 2;
    [Tooltip("Wall points per combat room, on walls with no door.")]
    [SerializeField, Min(0)] private int wallPointsPerRoom = 2;
    [Tooltip("How far out from the wall's face ammo on a wall point sits.")]
    [SerializeField] private float wallAmmoGap = 0.8f;

    [Header("Spawn Room")]
    [Tooltip("Pickup mounted on a wall of the spawn room each floor, on a side with no door.")]
    [SerializeField] private Transform spawnRoomPickup;
    [SerializeField] private float pickupMountHeight = 1.1f;
    [Tooltip("How far out from the wall's face the mounted pickup sits.")]
    [SerializeField] private float pickupWallGap = 0.35f;

    [Header("Player")]
    [SerializeField] private Transform player;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1f, -2f);

    [Header("Hideout")]
    [Tooltip("The main weapons the hideout's Armory upgrade starts a run with, by level (1, 2, 3).")]
    [SerializeField] private GameObject[] armoryWeapons;

    public event Action<Room> RoomEntered;

    public IReadOnlyList<Room> Rooms => rooms;
    public Room CurrentRoom { get; private set; }
    public int CurrentSeed { get; private set; }
    public int FloorNumber { get; private set; } = 1;
    public int TotalFloors => totalFloors;
    public int EnemiesKilled { get; private set; }

    // Floor 0 is the hideout: one quiet room with the upgrade terminal and the hatch to floor 1.
    public bool IsHideout => FloorNumber == 0;
    public string FloorName => IsHideout ? "HIDEOUT" : $"FLOOR {FloorNumber}";

    // When floor 1 of this run began, for the victory screen's mission time.
    public float RunStartTime { get; private set; }

    private bool runPerksApplied;

    // The ammo drop chance after the hideout's Scavenger upgrade.
    private float EnemyAmmoDropChance => Mathf.Clamp01(enemyAmmoDropChance + 0.1f * MetaProgress.Level(MetaUpgrade.Scavenger));
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
        EnemySenses.Use(enemySenses);
        AmmoVisuals.Use(ammoVisuals);
        ConsumableVisuals.Use(consumableVisuals);
        CharacterVisuals.Use(characterVisuals);

        // Thrown things and effects are built from the pickups' material, even before any
        // pickup has spawned.
        if (ammoMaterial != null)
            ConsumablePickup.DefaultMaterial = ammoMaterial;
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

        if (IsHideout)
        {
            GenerateHideout();
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

        // Once all the walls, doors and pillars are up, and before anything moves in.
        BuildNavMesh();

        // Enemies go in last, once every room knows where its doors are.
        foreach (Room room in rooms)
        {
            // Loot first, then boxes kept clear of it, then enemies kept clear of both.
            List<Vector3> lootFloorPoints = SpawnLoot(room);
            SpawnClutter(room);
            SpawnTrash(room);
            List<Vector3> placed = SpawnEnemies(room);
            SpawnTreasure(room);
            SpawnTreasureAttachments(room);
            placed.AddRange(lootFloorPoints);
            SpawnAmmoCrate(room, placed);
        }

        MountSpawnPickup();
        PlacePlayer();

        if (FloorNumber == 1 && !runPerksApplied)
            StartCoroutine(BeginRun());

        Debug.Log($"Generated floor {FloorNumber}: {rooms.Count} rooms, seed {CurrentSeed}");
    }

    // ---------- Hideout ----------

    // Floor 0: a single quiet room with the upgrade terminal against the far wall and the hatch
    // down to floor 1 in a near corner, well away from the walk to the terminal. No enemies,
    // loot or clutter.
    private void GenerateHideout()
    {
        PlannedRoom room = new PlannedRoom { Shape = RoomShape.Single, Cells = new List<Vector2Int> { Vector2Int.zero }, Type = RoomType.Start };

        floorRoot = new GameObject("Hideout").transform;
        floorRoot.SetParent(transform, false);
        BuildRoom(room, new HashSet<(Vector2Int, Vector2Int)>());
        BuildNavMesh();

        Room hideout = rooms[0];
        Vector3 centre = CellToWorld(Vector2Int.zero);

        // The spawn room's wall gun waits for floor 1 (MountSpawnPickup brings it back).
        if (spawnRoomPickup != null)
            spawnRoomPickup.gameObject.SetActive(false);
        float halfDepth = roomSize.y / 2f;

        TerminalStation.Create(centre + new Vector3(0f, 0f, halfDepth - 1f), Vector3.back, hideout.transform);

        SpawnExit(hideout, FloorExit.Kind.NextFloor);
        Transform hatch = hideout.transform.Find("Hatch");

        if (hatch != null)
            hatch.position = centre + new Vector3(roomSize.x * 0.28f, 0f, -halfDepth * 0.45f);

        PlacePlayer();
        Debug.Log("Generated the hideout");
    }

    // The start of a run (floor 1): applies the hideout's upgrades to the player and starts the
    // clock and the run's scrap tally. Waits a frame so the player's own start-up (starting gun,
    // starting ammo) has happened first.
    private System.Collections.IEnumerator BeginRun()
    {
        runPerksApplied = true;
        RunStartTime = Time.time;
        MetaProgress.BeginRun();
        yield return null;

        if (player == null)
            yield break;

        int toughness = MetaProgress.Level(MetaUpgrade.Toughness);
        int conditioning = MetaProgress.Level(MetaUpgrade.Conditioning);
        int cache = MetaProgress.Level(MetaUpgrade.AmmoCache);
        int kit = MetaProgress.Level(MetaUpgrade.FieldKit);
        int armory = MetaProgress.Level(MetaUpgrade.Armory);

        if (toughness > 0 && player.TryGetComponent(out PlayerHealth health))
            health.AddMaxHealth(toughness);

        if (conditioning > 0 && player.TryGetComponent(out PlayerStamina stamina))
            stamina.ScaleMax(1f + 0.15f * conditioning);

        AmmoInventory ammo = player.GetComponent<AmmoInventory>();

        if (cache > 0 && ammo != null)
            ammo.Add(AmmoType.NineMil, MetaUpgrades.AmmoCacheRounds(cache));

        Backpack backpack = Backpack.For(player.gameObject);

        if (kit >= 1)
            backpack.AddConsumable(ConsumableType.Medkit, 1, true);

        if (kit >= 2)
            backpack.AddConsumable(ConsumableType.Grenade, 1, true);

        if (armory > 0 && armoryWeapons != null && armoryWeapons.Length > 0 && player.TryGetComponent(out PlayerWeapon weapons))
        {
            Gun gun = weapons.Give(armoryWeapons[Mathf.Min(armory, armoryWeapons.Length) - 1]);

            // A spare magazine's worth for it.
            if (gun != null && ammo != null)
                ammo.Add(gun.AmmoType, gun.GetMagazineSize());
        }
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

    // A room shows on the maps once you've been in it or through a door next to it.
    public bool IsKnown(Room room)
    {
        if (room.Visited)
            return true;

        foreach (RoomConnection connection in room.Connections)
        {
            if (connection.Neighbour.Visited)
                return true;
        }

        return false;
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

    private void OnBossRoomCleared(Room room)
    {
        PlayRoomCleared();
        SpawnExit(room, FloorNumber < totalFloors ? FloorExit.Kind.NextFloor : FloorExit.Kind.Trophy);
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

        if (UnityEngine.Random.value < medkitChance)
        {
            bool large = UnityEngine.Random.value < largeMedkitChance;
            ConsumablePickup.Spawn(large ? ConsumableType.LargeMedkit : ConsumableType.Medkit, 1, position, room.transform, ammoMaterial);
        }

        Vector3 side = Vector3.Cross(Vector3.up, toCentre.sqrMagnitude > 0.01f ? toCentre.normalized : Vector3.forward);

        if (UnityEngine.Random.value < supplyChance && PickSupply(UnityEngine.Random.value) is ConsumableType supply)
            ConsumablePickup.Spawn(supply, 1, position + side * 0.9f, room.transform, ammoMaterial);

        if (UnityEngine.Random.value < meleeUpgradeChance)
            SpawnMeleeUpgrade(position - side * 0.9f, room.transform, UnityEngine.Random.value);
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

    private void PlayRoomCleared()
    {
        AudioClip clip = roomClearedSound != null
            ? roomClearedSound
            : SoundFX.Pick(SoundFX.Clips("room.cleared", 1, i => ProceduralAudio.RoomCleared()));
        SoundFX.Play2D(clip, roomClearedVolume);
    }

    // Called by FloorExit when the player uses the hatch or picks up the trophy.
    public void UseExit(FloorExit.Kind kind)
    {
        if (kind == FloorExit.Kind.Trophy)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            int scrap = MetaProgress.EndRun(true, totalFloors, FloorNumber, EnemiesKilled);
            victoryScreen.Show(FloorNumber, EnemiesKilled, Time.time - RunStartTime, health.Health, health.MaxHealth, scrap);
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
        {
            if (pickup.transform != spawnRoomPickup)
                Destroy(pickup.gameObject);
        }

        foreach (Projectile projectile in FindObjectsByType<Projectile>())
            Destroy(projectile.gameObject);
    }

    // A glowing hatch in the floor, or a spinning gold trophy, in the middle of the boss room.
    private void SpawnExit(Room room, FloorExit.Kind kind)
    {
        bool trophy = kind == FloorExit.Kind.Trophy;

        GameObject exit = new GameObject(trophy ? "Trophy" : "Hatch");
        exit.transform.SetParent(room.transform, false);
        exit.transform.position = room.transform.position;

        BoxCollider trigger = exit.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(2f, 2f, 2f);
        trigger.center = Vector3.up;

        Transform spinning = null;

        if (trophy)
        {
            spinning = new GameObject("Cup").transform;
            spinning.SetParent(exit.transform, false);
            spinning.localPosition = Vector3.up * 0.9f;

            CreateVisual("Base", spinning, new Vector3(0f, -0.4f, 0f), new Vector3(0.7f, 0.15f, 0.7f), trophyMaterial);
            CreateVisual("Stem", spinning, new Vector3(0f, -0.15f, 0f), new Vector3(0.18f, 0.4f, 0.18f), trophyMaterial);
            CreateVisual("Bowl", spinning, new Vector3(0f, 0.25f, 0f), new Vector3(0.6f, 0.45f, 0.6f), trophyMaterial);
            CreateVisual("Handle L", spinning, new Vector3(-0.38f, 0.28f, 0f), new Vector3(0.12f, 0.3f, 0.12f), trophyMaterial);
            CreateVisual("Handle R", spinning, new Vector3(0.38f, 0.28f, 0f), new Vector3(0.12f, 0.3f, 0.12f), trophyMaterial);
        }
        else
        {
            CreateVisual("Hatch Frame", exit.transform, new Vector3(0f, 0.02f, 0f), new Vector3(2.4f, 0.04f, 2.4f), wallMaterial);
            CreateVisual("Hatch", exit.transform, new Vector3(0f, 0.03f, 0f), new Vector3(2f, 0.04f, 2f), exitMaterial);
        }

        // A glow so it can be found in the dark.
        Light glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(exit.transform, false);
        glow.transform.localPosition = Vector3.up * 1.5f;
        glow.type = LightType.Point;
        glow.color = trophy ? new Color(1f, 0.8f, 0.3f) : new Color(0.35f, 1f, 0.55f);
        glow.range = 5f;
        glow.intensity = 3f;
        glow.shadows = LightShadows.None;

        exit.AddComponent<FloorExit>().Initialise(this, kind, exitArmDelay, spinning);
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
                        floorMaterial
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

        if (!doorway)
        {
            CreateWallSpan(room.transform, alongX, across, start, end);
            return;
        }

        float middle = alongX ? block.center.x : block.center.y;
        CreateWallSpan(room.transform, alongX, across, start, middle - doorWidth / 2f);
        CreateWallSpan(room.transform, alongX, across, middle + doorWidth / 2f, end);
    }

    private void CreateWallSpan(Transform parent, bool alongX, float across, float start, float end)
    {
        float length = end - start;

        if (length <= 0.001f)
            return;

        float middle = (start + end) / 2f;
        Vector3 world = alongX
            ? new Vector3(middle, wallHeight / 2f, across)
            : new Vector3(across, wallHeight / 2f, middle);

        CreateWall("Wall", parent, ToLocal(parent, world), WallSize(alongX, length, wallThickness));
    }

    // Returns where enemies were placed, so a crate can keep clear of them.
    private List<Vector3> SpawnEnemies(Room room)
    {
        List<Vector3> placed = new List<Vector3>();

        if (room.Type == RoomType.Boss)
        {
            room.Cleared += OnBossRoomCleared;
            SpawnBoss(room);
            return placed;
        }

        if (room.Type != RoomType.Normal)
            return placed;

        room.Cleared += OnCombatRoomCleared;

        int count = rng.Next(minEnemiesPerRoom, maxEnemiesPerRoom + 1) + extraEnemiesPerCell * (room.Cells.Count - 1);
        List<Vector3> doorways = DoorwayPositions(room);
        Dictionary<EnemyKind, int> kindsPlaced = new Dictionary<EnemyKind, int>();

        for (int i = 0; i < count; i++)
        {
            if (TryFindSpawnPoint(room, doorways, placed, out Vector3 position))
            {
                placed.Add(position);
                EnemyKind kind = PickEnemyKind((float)rng.NextDouble(), kindsPlaced);
                kindsPlaced[kind] = kindsPlaced.TryGetValue(kind, out int already) ? already + 1 : 1;
                SpawnNormalEnemy(room, position, kind);
            }
        }

        return placed;
    }

    // A combat-room zombie of a kind, scaled from the walker's numbers.
    private Enemy SpawnNormalEnemy(Room room, Vector3 position, EnemyKind kind)
    {
        int health = Mathf.Max(1, Mathf.RoundToInt(enemyHealth * EnemyKinds.HealthMultiplier(kind)));
        float speed = enemySpeed * EnemyKinds.SpeedMultiplier(kind);
        int damage = enemyContactDamage + EnemyKinds.ExtraDamage(kind);
        float size = 0.9f * EnemyKinds.SizeMultiplier(kind);
        return SpawnEnemy(room, position, size, health, speed, damage, kind == EnemyKind.Walker ? "Enemy" : EnemyKinds.Name(kind), kind);
    }

    // The floor's boss (see Bosses: a different one each floor), in the middle of its room,
    // scaled from the boss health and speed set here.
    private void SpawnBoss(Room room)
    {
        BossKind kind = Bosses.ForFloor(FloorNumber);
        int health = Mathf.Max(1, Mathf.RoundToInt(bossHealth * Bosses.HealthMultiplier(kind)));
        float speed = bossSpeed * Bosses.SpeedMultiplier(kind);
        Enemy boss = SpawnEnemy(room, room.transform.position, Bosses.Size(kind), health, speed, bossContactDamage, "Boss", EnemyKind.Walker);
        if (CharacterVisuals.Current != null)
            CharacterVisuals.AttachToEnemy(boss, CharacterVisuals.Current.For(kind), CharacterAnimator.Style.Walker);

        Bosses.Equip(boss, kind, (position, minionKind) => SpawnMinion(room, position, minionKind));
    }

    // A zombie a boss calls in mid-fight. Capped, so a long fight doesn't flood the room.
    private Enemy SpawnMinion(Room room, Vector3 position, EnemyKind kind)
    {
        const int maxAlive = 8;   // the boss and seven others

        if (room.Enemies.Count >= maxAlive)
            return null;

        return SpawnNormalEnemy(room, position, kind);
    }

    // Weighted pick among the kinds allowed on this floor that aren't at their room limit.
    private EnemyKind PickEnemyKind(float roll, Dictionary<EnemyKind, int> placed)
    {
        bool Allowed(EnemyKindOdds odds) =>
            odds.weight > 0f && FloorNumber >= odds.minFloor
            && (odds.maxPerRoom <= 0 || !placed.TryGetValue(odds.kind, out int count) || count < odds.maxPerRoom);

        float total = 0f;

        foreach (EnemyKindOdds odds in enemyKinds)
        {
            if (Allowed(odds))
                total += odds.weight;
        }

        if (total <= 0f)
            return EnemyKind.Walker;

        float target = roll * total;

        foreach (EnemyKindOdds odds in enemyKinds)
        {
            if (!Allowed(odds))
                continue;

            target -= odds.weight;

            if (target <= 0f)
                return odds.kind;
        }

        return EnemyKind.Walker;
    }

    // Debug: one of each kind in a row at a point, in whichever room it's in. They only come
    // alive if that room is (or gets) locked.
    public void SpawnEnemyKinds(Vector3 position, Vector3 right)
    {
        if (!TryGetRoom(position, out Room room))
            return;

        EnemyKind[] kinds = (EnemyKind[])Enum.GetValues(typeof(EnemyKind));

        for (int i = 0; i < kinds.Length; i++)
            SpawnNormalEnemy(room, position + right * ((i - (kinds.Length - 1) / 2f) * 1.8f), kinds[i]);
    }

    // Some combat rooms have an ammo crate, somewhere clear of the doorways and the enemies.
    private void SpawnAmmoCrate(Room room, List<Vector3> enemies)
    {
        if (room.Type != RoomType.Normal || ammoMaterial == null || rng.NextDouble() >= ammoCrateChance)
            return;

        if (!TryFindSpawnPoint(room, DoorwayPositions(room), enemies, out Vector3 position))
            return;

        AmmoType type = PickAmmoType((float)rng.NextDouble(), (float)rng.NextDouble());
        AmmoPickup.Spawn(type, DropAmount(type, crateMultiplier, (float)rng.NextDouble()), position, room.transform, ammoMaterial, true);
    }

    // A dead enemy sometimes leaves ammo; a dead boss always leaves crate-sized drops, one for
    // each gun the player has (or one of anything if they somehow have none).
    private void DropAmmo(Room room, Enemy enemy, bool boss)
    {
        if (ammoMaterial == null)
            return;

        Vector3 position = enemy.transform.position;

        if (!boss)
        {
            if (UnityEngine.Random.value < EnemyAmmoDropChance)
            {
                AmmoType type = PickAmmoType(UnityEngine.Random.value, UnityEngine.Random.value);
                AmmoPickup.Spawn(type, DropAmount(type, 1f, UnityEngine.Random.value), position, room.transform, ammoMaterial, false);
            }

            return;
        }

        List<AmmoType> types = HeldAmmoTypes();

        if (types.Count == 0)
            types.Add(AmmoInfo.All[UnityEngine.Random.Range(0, AmmoInfo.All.Length)]);

        for (int i = 0; i < types.Count; i++)
        {
            Vector3 offset = new Vector3((i - (types.Count - 1) / 2f) * 1.2f, 0f, -1.5f);
            AmmoPickup.Spawn(types[i], DropAmount(types[i], crateMultiplier, UnityEngine.Random.value), position + offset, room.transform, ammoMaterial, true);
        }
    }

    // Usually ammo for a gun the player is carrying, so drops are useful; sometimes any type.
    private AmmoType PickAmmoType(float biasRoll, float pickRoll)
    {
        List<AmmoType> held = HeldAmmoTypes();
        List<AmmoType> pool = held.Count > 0 && biasRoll < heldGunAmmoBias ? held : new List<AmmoType>(AmmoInfo.All);
        return pool[Mathf.Min(pool.Count - 1, (int)(pickRoll * pool.Count))];
    }

    private List<AmmoType> HeldAmmoTypes()
    {
        List<AmmoType> types = new List<AmmoType>();
        PlayerWeapon weapons = player != null ? player.GetComponent<PlayerWeapon>() : null;

        if (weapons == null)
            return types;

        foreach (PlayerWeapon.Equipped equipped in new[] { weapons.Main, weapons.Sidearm })
        {
            if (equipped.Gun != null && !types.Contains(equipped.Gun.AmmoType))
                types.Add(equipped.Gun.AmmoType);
        }

        return types;
    }

    private static int DropAmount(AmmoType type, float multiplier, float roll)
    {
        Vector2Int range = AmmoInfo.DropAmount(type);
        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(range.x, range.y, roll) * multiplier));
    }

    // ---------- Clutter ----------

    // Cardboard boxes against the walls and in the corners of a room: singles, pairs side by
    // side, and a small box stacked on a bigger one, with the odd flattened sheet on the floor.
    // Kept clear of doorways, pillars, the middle of each cell (loot, treasure and the exit go
    // there) and wall loot points, so the room stays walkable and nothing ends up buried.
    private void SpawnClutter(Room room)
    {
        if (clutterBoxes == null || clutterBoxes.Length == 0 || room.Type == RoomType.Start)
            return;

        List<Vector3> placed = new List<Vector3>();
        List<Vector3> doorways = DoorwayPositions(room);
        List<Vector3> lootPoints = new List<Vector3>();

        foreach (LootSpawnPoint point in room.GetComponentsInChildren<LootSpawnPoint>())
            lootPoints.Add(point.transform.position);

        foreach (Vector2Int cell in room.Cells)
        {
            Vector3 centre = CellToWorld(cell);
            int clusters = rng.Next(clutterPerCell.x, clutterPerCell.y + 1);

            for (int c = 0; c < clusters; c++)
            {
                // A wall of this cell that's the room's edge.
                List<Vector2Int> sides = new List<Vector2Int>();

                foreach (Vector2Int side in Directions)
                {
                    if (!room.Contains(cell + side))
                        sides.Add(side);
                }

                if (sides.Count == 0)
                    break;

                Vector2Int wall = sides[rng.Next(sides.Count)];
                Vector3 outward = new Vector3(wall.x, 0f, wall.y);
                Vector3 along = new Vector3(wall.y, 0f, wall.x);
                float halfExtent = wall.x != 0 ? roomSize.x / 2f : roomSize.y / 2f;
                float halfLength = wall.x != 0 ? roomSize.y / 2f : roomSize.x / 2f;

                // Often tucked into a corner, otherwise anywhere along the wall.
                float offset = rng.NextDouble() < 0.5
                    ? (rng.NextDouble() < 0.5 ? -1f : 1f) * (halfLength - 0.7f)
                    : Mathf.Lerp(-halfLength + 0.8f, halfLength - 0.8f, (float)rng.NextDouble());

                Vector3 spot = centre + outward * (halfExtent - 0.85f) + along * offset;

                if (!IsClear(spot, doorways, 2.8f) || !IsClear(spot, PillarsOf(room), 1.9f)
                    || !IsClear(spot, lootPoints, 1.5f) || !IsClear(spot, placed, 2.2f)
                    || Vector3.Distance(Flat(spot), Flat(centre)) < 2.8f)
                {
                    continue;
                }

                placed.Add(spot);
                PlaceCluster(room, spot, outward, along);
            }

            // A few free-standing groups out in the room, off the routes between the doors and the
            // middle of the cell.
            int open = rng.Next(openClutterPerCell.x, openClutterPerCell.y + 1);

            for (int c = 0; c < open; c++)
            {
                Vector3 spot = centre + new Vector3(
                    Mathf.Lerp(-roomSize.x / 2f + 2f, roomSize.x / 2f - 2f, (float)rng.NextDouble()), 0f,
                    Mathf.Lerp(-roomSize.y / 2f + 1.8f, roomSize.y / 2f - 1.8f, (float)rng.NextDouble()));

                if (!IsClear(spot, doorways, 2.8f) || !IsClear(spot, PillarsOf(room), 2f) || !IsClear(spot, lootPoints, 1.5f)
                    || !IsClear(spot, placed, 2.2f) || Vector3.Distance(Flat(spot), Flat(centre)) < 2.8f
                    || NearDoorRoute(spot, centre, doorways, 1.8f))
                {
                    continue;
                }

                placed.Add(spot);
                float turn = (float)rng.NextDouble() * 360f;
                PlaceCluster(room, spot, Quaternion.Euler(0f, turn, 0f) * Vector3.forward, Quaternion.Euler(0f, turn, 0f) * Vector3.right);
            }

            // Flattened sheets lying about on the floor, to walk over.
            int sheets = flattenedBox != null ? rng.Next(flattenedPerCell.x, flattenedPerCell.y + 1) : 0;

            for (int f = 0; f < sheets; f++)
            {
                Vector3 sheet = centre + new Vector3(
                    Mathf.Lerp(-roomSize.x / 2f + 1f, roomSize.x / 2f - 1f, (float)rng.NextDouble()), 0f,
                    Mathf.Lerp(-roomSize.y / 2f + 1f, roomSize.y / 2f - 1f, (float)rng.NextDouble()));

                if (!IsClear(sheet, doorways, 1.6f) || !IsClear(sheet, PillarsOf(room), 1.2f) || Vector3.Distance(Flat(sheet), Flat(centre)) < 1.5f)
                    continue;

                GameObject flat = Instantiate(flattenedBox, sheet, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), room.transform);
                flat.transform.localScale = Vector3.one * RandomRange(flattenedScale.x, flattenedScale.y);

                // A hair apart in height, so overlapping sheets don't flicker against each other.
                flat.transform.position += Vector3.up * RandomRange(0f, 0.01f);

                foreach (Collider part in flat.GetComponentsInChildren<Collider>())
                    Destroy(part);
            }
        }

        if (placed.Count > 0)
            clutter[room] = placed;
    }

    // One to three boxes at a spot by a wall: the first squared up to the wall (a little
    // skewed), sometimes a second beside it, sometimes a small one on top.
    private void PlaceCluster(Room room, Vector3 spot, Vector3 outward, Vector3 along)
    {
        Quaternion facing = Quaternion.LookRotation(-outward);
        GameObject first = PlaceBox(room.transform, PickBox(), spot, facing * Quaternion.Euler(0f, RandomRange(-12f, 12f), 0f));

        if (first == null)
            return;

        float width = BoxSize(first).x;

        if (rng.NextDouble() < 0.5)
        {
            GameObject second = PickBox();
            float side = rng.NextDouble() < 0.5 ? -1f : 1f;
            Vector3 next = spot + along * side * (width / 2f + BoxSize(second).x * 0.9f + 0.1f);
            PlaceBox(room.transform, second, next, facing * Quaternion.Euler(0f, RandomRange(-20f, 20f), 0f));
        }

        // A smaller closed box stacked on a closed one. Its own body rests on top.
        if (rng.NextDouble() < 0.35 && first.name.Contains("Closed"))
        {
            GameObject small = PickSmallBox();

            if (small != null)
            {
                float height = BoxSize(first).y;
                GameObject top = PlaceBox(room.transform, small, first.transform.position + Vector3.up * (height + 0.01f),
                    first.transform.rotation * Quaternion.Euler(0f, RandomRange(-30f, 30f), 0f));

                if (top != null && BoxSize(top).x > width)
                    Destroy(top);   // too big to sit on it
            }
        }
    }

    private GameObject PlaceBox(Transform parent, GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        GameObject box = Instantiate(prefab, position, rotation, parent);
        box.name = prefab.name;
        box.transform.localScale = Vector3.one * RandomRange(boxScale.x, boxScale.y);

        CardboardBox cardboard = box.GetComponent<CardboardBox>();

        if (cardboard != null)
        {
            cardboard.Room = parent;
            cardboard.EnablePhysics(boxPhysics);
        }

        return box;
    }

    private GameObject PickBox()
    {
        return clutterBoxes[rng.Next(clutterBoxes.Length)];
    }

    private GameObject PickSmallBox()
    {
        List<GameObject> small = new List<GameObject>();

        foreach (GameObject box in clutterBoxes)
        {
            if (box != null && box.name.Contains("Closed") && box.GetComponent<BoxCollider>() != null && box.GetComponent<BoxCollider>().size.y < 0.35f)
                small.Add(box);
        }

        return small.Count > 0 ? small[rng.Next(small.Count)] : null;
    }

    // As placed, scale included.
    private static Vector3 BoxSize(GameObject box)
    {
        BoxCollider body = box.GetComponent<BoxCollider>();
        return body != null ? Vector3.Scale(body.size, box.transform.lossyScale) : Vector3.one * 0.5f;
    }

    // Within distance of the straight route from any doorway to the middle of the cell.
    private static bool NearDoorRoute(Vector3 point, Vector3 centre, List<Vector3> doorways, float distance)
    {
        Vector2 p = new Vector2(point.x, point.z);
        Vector2 c = new Vector2(centre.x, centre.z);

        foreach (Vector3 door in doorways)
        {
            Vector2 d = new Vector2(door.x, door.z);
            Vector2 route = c - d;
            float t = Mathf.Clamp01(Vector2.Dot(p - d, route) / Mathf.Max(0.0001f, route.sqrMagnitude));

            if (Vector2.Distance(p, d + route * t) < distance)
                return true;
        }

        return false;
    }

    private List<Vector3> ClutterOf(Room room)
    {
        return clutter.TryGetValue(room, out List<Vector3> list) ? list : new List<Vector3>();
    }

    private float RandomRange(float min, float max)
    {
        return Mathf.Lerp(min, max, (float)rng.NextDouble());
    }

    private static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }

    // ---------- Trash ----------

    // Dresses a room with rubbish from the trash pack: flat litter strewn across the floor, small
    // things scattered about (more of them near the walls), and a big piece or two against the
    // walls. Kept off the doorways, pillars and wall loot points; the big pieces also stay off
    // the routes from the doors and the middle of each cell, and count as clutter, so enemies and
    // crates don't spawn on them. The spawn room gets a little litter and nothing else.
    private void SpawnTrash(Room room)
    {
        List<Vector3> doorways = DoorwayPositions(room);
        List<Vector3> lootPoints = new List<Vector3>();

        foreach (LootSpawnPoint point in room.GetComponentsInChildren<LootSpawnPoint>())
            lootPoints.Add(point.transform.position);

        if (!clutter.TryGetValue(room, out List<Vector3> taken))
        {
            taken = new List<Vector3>();
            clutter[room] = taken;
        }

        bool spawnRoom = room.Type == RoomType.Start;

        foreach (Vector2Int cell in room.Cells)
        {
            Vector3 centre = CellToWorld(cell);

            // Flat litter, anywhere but the doorways.
            if (floorLitter != null && floorLitter.Length > 0)
            {
                int litter = rng.Next(litterPerCell.x, litterPerCell.y + 1);

                if (spawnRoom)
                    litter /= 3;

                for (int i = 0; i < litter; i++)
                {
                    Vector3 spot = RandomFloorPoint(centre, 0.6f);

                    if (!IsClear(spot, doorways, 1.4f) || !IsClear(spot, PillarsOf(room), 0.9f))
                        continue;

                    GameObject flat = PlaceTrash(room.transform, floorLitter[rng.Next(floorLitter.Length)], spot,
                        Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));

                    // A hair apart in height, so overlapping litter doesn't flicker against itself.
                    if (flat != null)
                        flat.transform.position += Vector3.up * RandomRange(0.002f, 0.012f);
                }
            }

            if (spawnRoom)
                continue;

            // Big pieces against the walls, as cover.
            if (largeTrash != null && largeTrash.Length > 0)
            {
                int large = rng.Next(largeTrashPerCell.x, largeTrashPerCell.y + 1);

                for (int i = 0; i < large; i++)
                {
                    Vector2Int wall = Directions[rng.Next(Directions.Length)];

                    if (room.Contains(cell + wall))
                        continue;

                    Vector3 outward = new Vector3(wall.x, 0f, wall.y);
                    Vector3 along = new Vector3(wall.y, 0f, wall.x);
                    float halfExtent = wall.x != 0 ? roomSize.x / 2f : roomSize.y / 2f;
                    float halfLength = wall.x != 0 ? roomSize.y / 2f : roomSize.x / 2f;
                    Vector3 spot = centre + outward * (halfExtent - 1.1f) + along * RandomRange(-halfLength + 1.2f, halfLength - 1.2f);

                    if (!IsClear(spot, doorways, 3f) || !IsClear(spot, PillarsOf(room), 2f) || !IsClear(spot, lootPoints, 1.6f)
                        || !IsClear(spot, taken, 2.2f) || Vector3.Distance(Flat(spot), Flat(centre)) < 3f)
                    {
                        continue;
                    }

                    if (PlaceTrash(room.transform, largeTrash[rng.Next(largeTrash.Length)], spot,
                        Quaternion.LookRotation(-outward) * Quaternion.Euler(0f, RandomRange(-25f, 25f), 0f)) != null)
                    {
                        taken.Add(spot);
                    }
                }
            }

            // Small things scattered about, more of them towards the walls.
            if (smallTrash != null && smallTrash.Length > 0)
            {
                int small = rng.Next(smallTrashPerCell.x, smallTrashPerCell.y + 1);

                for (int i = 0; i < small; i++)
                {
                    Vector3 spot = rng.NextDouble() < 0.6 ? RandomWallPoint(cell, centre, 0.5f) : RandomFloorPoint(centre, 0.8f);

                    if (!IsClear(spot, doorways, 1.8f) || !IsClear(spot, PillarsOf(room), 1f) || !IsClear(spot, lootPoints, 0.9f)
                        || !IsClear(spot, taken, 1f))
                    {
                        continue;
                    }

                    GameObject prefab = smallTrash[rng.Next(smallTrash.Length)];
                    bool tipped = rng.NextDouble() < tippedChance && IsRound(prefab.name);
                    Quaternion rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                    // Lying on its side, rolled to any angle.
                    if (tipped)
                        rotation *= Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                    PlaceTrash(room.transform, prefab, spot, rotation);
                }
            }
        }
    }

    // Cans, bottles and cups can be found lying on their sides.
    private static bool IsRound(string name)
    {
        return name.Contains("Can") && !name.Contains("Gas") || name.Contains("Bottle") || name.Contains("Cup") || name.Contains("Tin");
    }

    private Vector3 RandomFloorPoint(Vector3 centre, float margin)
    {
        return centre + new Vector3(
            RandomRange(-roomSize.x / 2f + margin, roomSize.x / 2f - margin), 0f,
            RandomRange(-roomSize.y / 2f + margin, roomSize.y / 2f - margin));
    }

    // Somewhere just in from one of this cell's outer walls (or anywhere, if it has none).
    private Vector3 RandomWallPoint(Vector2Int cell, Vector3 centre, float inset)
    {
        List<Vector2Int> walls = new List<Vector2Int>();

        foreach (Vector2Int side in Directions)
        {
            if (!roomsByCell.ContainsKey(cell + side) || roomsByCell[cell + side] != roomsByCell[cell])
                walls.Add(side);
        }

        if (walls.Count == 0)
            return RandomFloorPoint(centre, 0.8f);

        Vector2Int wall = walls[rng.Next(walls.Count)];
        Vector3 outward = new Vector3(wall.x, 0f, wall.y);
        Vector3 along = new Vector3(wall.y, 0f, wall.x);
        float halfExtent = wall.x != 0 ? roomSize.x / 2f : roomSize.y / 2f;
        float halfLength = wall.x != 0 ? roomSize.y / 2f : roomSize.x / 2f;
        return centre + outward * (halfExtent - inset - RandomRange(0f, 1.2f)) + along * RandomRange(-halfLength + 0.5f, halfLength - 0.5f);
    }

    // Placed, sized, sat on the floor whatever way up it is, and given its physics.
    private GameObject PlaceTrash(Transform parent, GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        GameObject item = Instantiate(prefab, position, rotation, parent);
        item.name = prefab.name;
        item.transform.localScale = Vector3.one * RandomRange(trashScale.x, trashScale.y);

        // Rest the lowest point on the floor (it may be on its side).
        Bounds bounds = new Bounds(item.transform.position, Vector3.zero);
        bool any = false;

        foreach (Renderer part in item.GetComponentsInChildren<Renderer>())
        {
            if (!any)
                bounds = part.bounds;
            else
                bounds.Encapsulate(part.bounds);

            any = true;
        }

        if (any)
            item.transform.position += Vector3.up * (0.001f - bounds.min.y);

        Prop prop = item.GetComponent<Prop>();

        if (prop != null)
            prop.Configure(boxPhysics);

        CardboardBox box = item.GetComponent<CardboardBox>();

        if (box != null)
        {
            box.Room = parent;
            box.EnablePhysics(boxPhysics);
        }

        return item;
    }

    // ---------- Pillars, lights and navigation ----------

    // Square pillars in some cells of a big room: four in a square, or a diagonal pair, each a
    // quarter of the way in from the cell's centre. That keeps them off the lines between the
    // doors and the middle of the room, and off the centre (where floor loot goes).
    private void BuildPillars(Room room)
    {
        List<Vector3> placed = new List<Vector3>();
        float dx = roomSize.x / 4f;
        float dz = roomSize.y / 4f;

        foreach (Vector2Int cell in room.Cells)
        {
            double roll = rng.NextDouble();

            if (roll < 0.25)
                continue;   // some cells stay open

            Vector3 centre = CellToWorld(cell);
            Vector3[] offsets = roll < 0.6
                ? new[] { new Vector3(dx, 0f, dz), new Vector3(-dx, 0f, dz), new Vector3(dx, 0f, -dz), new Vector3(-dx, 0f, -dz) }
                : rng.NextDouble() < 0.5
                    ? new[] { new Vector3(dx, 0f, dz), new Vector3(-dx, 0f, -dz) }
                    : new[] { new Vector3(-dx, 0f, dz), new Vector3(dx, 0f, -dz) };

            foreach (Vector3 offset in offsets)
            {
                Vector3 position = centre + offset;
                GameObject pillar;

                if (pillarPrefab != null)
                {
                    // Turned to one of the four directions, so the same damage doesn't face the
                    // same way on every pillar.
                    pillar = Instantiate(pillarPrefab, position, Quaternion.Euler(0f, 90f * rng.Next(4), 0f), room.transform);
                    pillar.name = "Pillar";
                    pillar.transform.localScale = new Vector3(pillarSize, pillarHeight, pillarSize);
                }
                else
                {
                    pillar = CreateBlock(
                        "Pillar",
                        room.transform,
                        ToLocal(room.transform, position + Vector3.up * wallHeight / 2f),
                        new Vector3(pillarSize, wallHeight, pillarSize),
                        wallMaterial
                    );
                }

                pillar.tag = "Wall";
                pillar.GetComponent<Collider>().sharedMaterial = slidePhysics;
                placed.Add(position);
            }
        }

        if (placed.Count > 0)
            pillars[room] = placed;
    }

    private List<Vector3> PillarsOf(Room room)
    {
        return pillars.TryGetValue(room, out List<Vector3> list) ? list : new List<Vector3>();
    }

    // Overhead fluorescent tubes, spaced along each cell. Just Light components: there's no
    // fitting to see from the camera, only the pools of light on the floor.
    private void AddCeilingLights(Room room)
    {
        if (lightsPerCell <= 0)
            return;

        foreach (Vector2Int cell in room.Cells)
        {
            Vector3 centre = CellToWorld(cell);

            for (int i = 0; i < lightsPerCell; i++)
            {
                // Evenly spaced along the cell's long side.
                float along = (i + 0.5f) / lightsPerCell - 0.5f;
                Vector3 position = centre + new Vector3(along * roomSize.x, lightHeight, 0f);

                GameObject lightObject = new GameObject("Ceiling Light");
                lightObject.transform.SetParent(room.transform, false);
                lightObject.transform.position = position;
                lightObject.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

                Light tube = lightObject.AddComponent<Light>();
                tube.type = LightType.Spot;
                tube.spotAngle = lightAngle;
                tube.innerSpotAngle = lightAngle * 0.45f;
                tube.range = lightRange;
                tube.color = lightColour;
                tube.shadows = LightShadows.None;
                // A little variation between tubes, as real ones age differently.
                tube.intensity = lightIntensity * Mathf.Lerp(0.85f, 1.05f, (float)rng.NextDouble());

                UnityEngine.Rendering.Universal.UniversalAdditionalLightData data =
                    lightObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
                data.renderingLayers = lightRenderingLayers;

                lightObject.AddComponent<FluorescentFlicker>();
            }
        }
    }

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

    // Doorway centres on the room's walls, in world space.
    private List<Vector3> DoorwayPositions(Room room)
    {
        List<Vector3> doorways = new List<Vector3>();

        foreach (RoomConnection connection in room.Connections)
        {
            Vector3 direction = new Vector3(connection.Direction.x, 0f, connection.Direction.y);
            float halfExtent = connection.Direction.x != 0 ? roomSize.x / 2f : roomSize.y / 2f;
            doorways.Add(CellToWorld(connection.Cell) + direction * halfExtent);
        }

        return doorways;
    }

    // Random point on the floor of one of the room's cells, away from the walls, doorways and
    // other enemies. Gives up after a few tries rather than forcing a bad spot.
    private bool TryFindSpawnPoint(Room room, List<Vector3> doorways, List<Vector3> placed, out Vector3 position)
    {
        const float wallMargin = 1.5f;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector3 centre = CellToWorld(room.Cells[rng.Next(room.Cells.Count)]);

            position = centre + new Vector3(
                Mathf.Lerp(-roomSize.x / 2f + wallMargin, roomSize.x / 2f - wallMargin, (float)rng.NextDouble()),
                0f,
                Mathf.Lerp(-roomSize.y / 2f + wallMargin, roomSize.y / 2f - wallMargin, (float)rng.NextDouble())
            );

            if (IsClear(position, doorways, doorwayClearance) && IsClear(position, placed, enemySpacing)
                && IsClear(position, PillarsOf(room), pillarClearance)
                && IsClear(position, ClutterOf(room), clutterClearance))
                return true;
        }

        position = default;
        return false;
    }

    private static bool IsClear(Vector3 position, List<Vector3> points, float distance)
    {
        foreach (Vector3 point in points)
        {
            if (Vector3.Distance(position, point) < distance)
                return false;
        }

        return true;
    }

    private Enemy SpawnEnemy(Room room, Vector3 worldPosition, float size, int health, float speed, int contactDamage, string name, EnemyKind kind)
    {
        GameObject enemyObject = CreateBlock(
            name,
            room.transform,
            ToLocal(room.transform, worldPosition + Vector3.up * size / 2f),
            Vector3.one * size,
            enemyMaterial
        );

        // The collider stops just short of the floor: enemies are held at their height anyway,
        // and a box resting on the floor catches on the seams between floor tiles.
        BoxCollider bodyCollider = enemyObject.GetComponent<BoxCollider>();
        bodyCollider.sharedMaterial = slidePhysics;
        bodyCollider.center = new Vector3(0f, 0.04f, 0f);
        bodyCollider.size = new Vector3(1f, 0.92f, 1f);

        Rigidbody body = enemyObject.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        Enemy enemy = enemyObject.AddComponent<Enemy>();
        enemy.Initialise(room, player, health, speed, contactDamage);
        enemy.SetKind(kind);

        // Bosses get their own model in SpawnBoss.
        if (CharacterVisuals.Current != null && name != "Boss")
            CharacterVisuals.AttachToEnemy(enemy, CharacterVisuals.Current.For(kind), CharacterVisuals.StyleFor(kind));
        bool boss = name == "Boss";

        if (kind == EnemyKind.Bloater)
            enemyObject.AddComponent<EnemyBloater>();
        else if (kind == EnemyKind.Screamer)
            enemyObject.AddComponent<EnemyScreamer>();

        // Heavier, lower steps than the player's, heard from where the enemy is. The boss
        // takes long, slow, booming strides.
        Footsteps steps = enemyObject.AddComponent<Footsteps>();

        if (boss)
            steps.Configure(2.2f, 0.55f, 0.25f, 0.35f, new Vector2(1f, 3f), 1f);
        else if (kind == EnemyKind.Brute)
            steps.Configure(1.8f, 0.65f, 0.12f, 0.22f, new Vector2(1f, 3f), 1f);
        else if (kind == EnemyKind.Runner)
            steps.Configure(1f, 1f, 0.05f, 0.12f, new Vector2(2f, 6f), 1f);
        else
            steps.Configure(1.2f, 0.8f, 0.06f, 0.13f, new Vector2(1.5f, 4f), 1f);

        // A voice from one of the sound packs, deeper for the boss.
        if (enemySoundPacks != null && enemySoundPacks.Length > 0)
        {
            ZombieSoundPack pack = enemySoundPacks[UnityEngine.Random.Range(0, enemySoundPacks.Length)];

            if (pack != null)
                enemyObject.AddComponent<EnemyVoice>().Configure(pack, boss ? 0.7f : VoicePitch(kind));
        }
        enemy.Died += dead =>
        {
            EnemiesKilled++;
            lastDeaths[room] = dead.transform.position;
            if (bloodSplatMaterials != null && bloodSplatMaterials.Length > 0)
            {
                Material splat = bloodSplatMaterials[UnityEngine.Random.Range(0, bloodSplatMaterials.Length)];
                ImpactEffects.FloorSplat(dead.transform.position, boss ? 3f : 1.5f, room.transform, splat);
            }
            DropAmmo(room, dead, boss);

            if (boss)
            {
                DropAttachments(room, dead.transform.position);
                DropMeleeUpgrades(room, dead.transform.position);
            }
        };
        room.AddEnemy(enemy);
        return enemy;
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
            floorMaterial
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
        RoomDoor door = doorObject.AddComponent<RoomDoor>();

        Room from = roomsByCell[cell];
        Room to = roomsByCell[cell + direction];
        from.AddConnection(new RoomConnection { Cell = cell, Direction = direction, Neighbour = to, Door = door });
        to.AddConnection(new RoomConnection { Cell = cell + direction, Direction = -direction, Neighbour = from, Door = door });

        if (corridorLength <= 0f)
            return;

        float sideOffset = doorWidth / 2f + wallThickness / 2f;
        Vector3 wallSize = WallSize(alongX, corridorLength, wallThickness);

        CreateWall("Wall", corridor.transform, side * sideOffset + Vector3.up * wallHeight / 2f, wallSize);
        CreateWall("Wall", corridor.transform, -side * sideOffset + Vector3.up * wallHeight / 2f, wallSize);
    }

    // Hangs the spawn room's pickup on the middle of a wall that has no door, facing into the
    // room. North is preferred so it's in view when the floor starts. If every wall has a door,
    // it goes on the north wall a quarter of the way along, clear of the doorway in the middle.
    private void MountSpawnPickup()
    {
        if (spawnRoomPickup == null || rooms.Count == 0)
            return;

        spawnRoomPickup.gameObject.SetActive(true);

        Room start = rooms[0];

        foreach (Vector2Int side in new[] { Vector2Int.up, Vector2Int.left, Vector2Int.right, Vector2Int.down })
        {
            bool hasDoor = false;

            foreach (RoomConnection connection in start.Connections)
            {
                if (connection.Cell == start.GridPosition && connection.Direction == side)
                    hasDoor = true;
            }

            if (!hasDoor)
            {
                MountOnWall(start.GridPosition, side, 0f);
                return;
            }
        }

        MountOnWall(start.GridPosition, Vector2Int.up, -roomSize.x / 4f);
    }

    // alongOffset slides the mount sideways along the wall from its middle.
    private void MountOnWall(Vector2Int cell, Vector2Int side, float alongOffset)
    {
        Vector3 outward = new Vector3(side.x, 0f, side.y);
        Vector3 along = new Vector3(side.y, 0f, side.x);
        float halfExtent = side.x != 0 ? roomSize.x / 2f : roomSize.y / 2f;

        Vector3 position = CellToWorld(cell) + outward * (halfExtent - pickupWallGap) + along * alongOffset;
        position.y = pickupMountHeight;

        spawnRoomPickup.SetPositionAndRotation(position, Quaternion.LookRotation(-outward));
    }

    private void PlacePlayer()
    {
        if (player == null)
            return;

        Vector3 spawn = CellToWorld(Vector2Int.zero) + spawnOffset;

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

    private void CreateWall(string name, Transform parent, Vector3 localPosition, Vector3 size)
    {
        GameObject wall = CreateBlock(name, parent, localPosition, size, wallMaterial);
        wall.tag = "Wall";
        wall.GetComponent<Collider>().sharedMaterial = slidePhysics;
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
