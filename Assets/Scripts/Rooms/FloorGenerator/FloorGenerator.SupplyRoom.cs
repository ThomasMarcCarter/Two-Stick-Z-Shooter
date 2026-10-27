using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's supply room: where the last floor's hatch leads once its boss is dead. One
// quiet room, no zombies: shelving along the north wall stocked with scrap piles and crafting
// material stacks (more the better the run went: kills and floors), the truck part the
// mechanic needs on a lit counter in the middle (the next one missing, in order; once the truck
// has them all, bonus scrap and rare materials instead), and a ladder up to the extraction
// point, which ends the run as a win and brings everything home. The ladder stays shut until
// the part is taken. On the first run to get this far the mechanic is here too, caged.
public partial class FloorGenerator
{
    [Header("Supply Room")]
    [Tooltip("Scrap on the supply room's shelves: this much, plus Supply Scrap Per Kill for each kill of the run, give or take 20%.")]
    [SerializeField] private int supplyScrapBase = 12;
    [SerializeField] private float supplyScrapPerKill = 0.25f;
    [Tooltip("Crafting materials on the shelves: this many, one more per this many kills, plus up to two.")]
    [SerializeField] private int supplyMaterialsBase = 3;
    [SerializeField] private int supplyKillsPerMaterial = 12;
    [Tooltip("What sits on the part's counter once the truck has every part: this much scrap...")]
    [SerializeField] private int supplyBonusScrap = 25;
    [Tooltip("...and this many each of wiring and fuel (the scarce ones).")]
    [SerializeField] private int supplyBonusMaterials = 2;

    // Below the last floor (its boss is dead); FloorNumber stays the last floor's.
    public bool IsSupplyRoom { get; private set; }

    // Where the player arrives, at the foot of the ladder down from the boss room: south of the
    // middle, facing the shelves.
    private Vector3 SupplyRoomSpawn => CellToWorld(Vector2Int.zero) + new Vector3(0f, 1f, -2.5f);

    // The truck part on the counter, until it's taken (the ladder waits for it).
    private SupplyShelfItem supplyPart;

    // Debug: straight down to the supply room, as if the last floor's boss were dead.
    public void DebugEnterSupplyRoom()
    {
        if (IsHideout || IsSupplyRoom)
            return;

        FloorNumber = TotalFloors;
        transition.Play("> DESCENDING TO THE SUPPLY ROOM", $"{ZoneName} // SUPPLY ROOM // NO HOSTILES DETECTED", EnterSupplyRoom);
    }

    // Runs while the transition screen is fully black.
    private void EnterSupplyRoom()
    {
        IsSupplyRoom = true;
        ClearLooseObjects();
        Generate();

        if (cameraFollow != null)
            cameraFollow.SnapToTarget();
    }

    private void GenerateSupplyRoom()
    {
        floorRoot = new GameObject("Supply Room").transform;
        floorRoot.SetParent(transform, false);

        BuildRoom(new PlannedRoom { Shape = RoomShape.Single, Cells = new List<Vector2Int> { Vector2Int.zero }, Type = RoomType.Start },
            new HashSet<(Vector2Int, Vector2Int)>());

        Room room = rooms[0];
        room.DisplayName = "SUPPLY ROOM";
        clutter[room] = new List<Vector3>();

        Vector3 centre = CellToWorld(Vector2Int.zero);
        float north = centre.z + roomSize.y / 2f;
        Transform parent = room.transform;
        Color metal = new Color(0.22f, 0.23f, 0.24f);

        // Shelving against the north wall: two to the west of the counter, one to the east, and
        // the ladder in the north-east corner. The mall's is shop shelving round a checkout, the
        // hospital's pharmacy shelves round a reception desk.
        bool mall = Zone == Zone.ShoppingMall;
        bool hospital = Zone == Zone.Hospital;
        string shelving = mall ? "StoreShelf" : hospital ? "PharmacyShelf" : "Shelving";
        float[] shelfX = { -5.6f, -2.8f, 2.8f };
        Vector3 shelfSize = new Vector3(2.4f, 2f, 0.6f);

        foreach (float x in shelfX)
        {
            Vector3 at = new Vector3(centre.x + x, 0f, north - shelfSize.z / 2f - 0.05f);
            Furniture(shelving, "Shelving", parent, at, Vector3.back, shelfSize, metal);
            clutter[room].Add(at);
        }

        // The counter in the middle, under its own light.
        Vector3 counterSize = new Vector3(2f, 1.1f, 0.7f);
        Vector3 counter = new Vector3(centre.x, 0f, north - counterSize.z / 2f - 0.6f);
        Furniture(mall ? "Checkout" : hospital ? "ReceptionDesk" : Zone == Zone.Airport ? "CheckInDesk" : "Counter", "Counter", parent, counter, Vector3.back, counterSize, new Color(0.3f, 0.24f, 0.16f));
        clutter[room].Add(counter);

        Light spot = new GameObject("Counter Light").AddComponent<Light>();
        spot.transform.SetParent(parent, false);
        spot.transform.position = counter + new Vector3(0f, 3.6f, -0.8f);
        spot.transform.rotation = Quaternion.LookRotation(counter + Vector3.up - spot.transform.position);
        spot.type = LightType.Spot;
        spot.spotAngle = 45f;
        spot.range = 6f;
        spot.intensity = 10f;
        spot.color = new Color(1f, 0.82f, 0.5f);
        spot.shadows = LightShadows.None;

        Vector3 ladder = new Vector3(centre.x + 6.1f, 0f, north - 0.15f);
        SpawnLadder(room, ladder);
        clutter[room].Add(ladder + Vector3.back * 0.6f);

        // The mechanic, on the first run that gets this far (at the Military Base): caged west of
        // the middle. Solid, so in before the navmesh, and they walk out to the ladder once freed.
        if (Zone == Zone.MilitaryBase && !MetaProgress.IsRescued(NpcKind.Mechanic))
        {
            Vector3 cage = centre + new Vector3(-5f, 0f, -2f);
            CagedSurvivor.Create(NpcKind.Mechanic, room, cage, ladder + Vector3.back * 1.2f,
                ConsumablePickup.DefaultMaterial != null ? ConsumablePickup.DefaultMaterial : ammoMaterial);
            clutter[room].Add(cage);

            if (!pillars.TryGetValue(room, out List<Vector3> list))
                pillars[room] = list = new List<Vector3>();

            list.Add(cage);
        }

        BuildNavMesh();
        // Just in front of the shelving's face (it stands 5 cm off the wall), so they're drawn over it.
        StockShelves(room, shelfX, north - shelfSize.z - 0.15f, centre.x);
        StockCounter(room, counter + Vector3.back * (counterSize.z / 2f + 0.02f), counterSize.y);
        SpawnTrash(room);
        PlacePlayer();
        Debug.Log($"Generated the supply room: {(supplyPart != null ? StoryItems.Name(supplyPart.Story) : "bonus loot")} on the counter");
    }

    // Scrap piles and material stacks on the shelves, two to a unit (low and high), shuffled.
    // How much depends on the run: a base amount, more for its kills, and a little luck.
    private void StockShelves(Room room, float[] shelfX, float front, float centreX)
    {
        int kills = EnemiesKilled;
        int scrap = Mathf.Max(3, Mathf.RoundToInt((supplyScrapBase + supplyScrapPerKill * kills) * Random.Range(0.8f, 1.2f)));
        int materials = supplyMaterialsBase + kills / Mathf.Max(1, supplyKillsPerMaterial) + Random.Range(0, 3);

        List<(float x, float height)> slots = new List<(float, float)>();

        foreach (float x in shelfX)
        {
            slots.Add((x - 0.55f, 0.45f));
            slots.Add((x + 0.55f, 1.25f));
        }

        Shuffle(slots);

        // Three slots of scrap, the rest materials, split as evenly as they go.
        int scrapSlots = Mathf.Min(3, slots.Count);
        int materialSlots = slots.Count - scrapSlots;

        for (int i = 0; i < slots.Count; i++)
        {
            Vector3 at = new Vector3(centreX + slots[i].x, 0f, front);
            Vector3 reach = at + Vector3.back * 0.9f;

            if (i < scrapSlots)
            {
                int share = scrap / scrapSlots + (i < scrap % scrapSlots ? 1 : 0);
                SupplyShelfItem.SpawnScrap(share, at, slots[i].height, reach, room.transform);
                continue;
            }

            int slot = i - scrapSlots;
            int amount = materials / materialSlots + (slot < materials % materialSlots ? 1 : 0);

            if (amount > 0)
                SupplyShelfItem.SpawnMaterial(PickMaterial(Random.value), amount, at, slots[i].height, reach, room.transform);
        }
    }

    // The next missing piece of the zone's survivor's goal on the counter (the Military Base: the
    // truck's parts); once they have them all, or have no goal yet, bonus scrap and the scarce
    // materials instead.
    private void StockCounter(Room room, Vector3 front, float height)
    {
        Vector3 reach = front + Vector3.back * 1f;
        StoryItem? part = StoryItems.Next(Zones.Owner(Zone));

        if (part != null)
        {
            supplyPart = SupplyShelfItem.SpawnStory(part.Value, front, height, reach, room.transform);
            return;
        }

        supplyPart = null;
        SupplyShelfItem.SpawnScrap(supplyBonusScrap, front + Vector3.left * 0.6f, height, reach + Vector3.left * 0.6f, room.transform);
        SupplyShelfItem.SpawnMaterial(MaterialType.Wiring, supplyBonusMaterials, front, height, reach, room.transform);
        SupplyShelfItem.SpawnMaterial(MaterialType.Fuel, supplyBonusMaterials, front + Vector3.right * 0.6f, height, reach + Vector3.right * 0.6f, room.transform);
    }

    // The ladder up to the extraction point, against the north wall: shut until the truck part
    // is off the counter. Drawn from the story sheet, or a pair of rails and rungs without it.
    private void SpawnLadder(Room room, Vector3 foot)
    {
        GameObject ladder = new GameObject("Ladder");
        ladder.transform.SetParent(room.transform, false);
        ladder.transform.position = foot;

        SpriteVisuals visuals = SpriteVisuals.Current;

        if (visuals == null || visuals.storyItems == null
            || ItemSprite.Apply(ladder, visuals.storyItems, new Vector2Int(0, 2), false, 0f, true) == null)
        {
            Color yellow = new Color(0.85f, 0.7f, 0.1f);

            foreach (float x in new[] { -0.3f, 0.3f })
                HideoutProp("Ladder Rail", ladder.transform, foot + new Vector3(x, 1.2f, 0f), new Vector3(0.06f, 2.4f, 0.06f), yellow, yellow * 0.1f, false);

            for (int i = 0; i < 7; i++)
                HideoutProp("Ladder Rung", ladder.transform, foot + new Vector3(0f, 0.3f + i * 0.32f, 0f), new Vector3(0.6f, 0.04f, 0.04f), yellow, Color.black, false);
        }

        // A green lamp over it, like the hatches.
        Light glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(ladder.transform, false);
        glow.transform.localPosition = new Vector3(0f, 2.2f, -0.6f);
        glow.type = LightType.Point;
        glow.color = new Color(0.35f, 1f, 0.55f);
        glow.range = 4f;
        glow.intensity = 2.5f;
        glow.shadows = LightShadows.None;

        FloorExit exit = ladder.AddComponent<FloorExit>();
        exit.Initialise(this, FloorExit.Kind.Extraction, 0f, null);
        exit.Blocked = () => supplyPart != null ? $"TAKE THE {StoryItems.Name(supplyPart.Story)} FIRST" : null;
    }

    // Climbing out: the run's won. Whatever materials are in the backpack and the truck part in
    // the story slot come home with it, and the rest of the run's scrap with the winning bonus.
    private void Extract()
    {
        int[] recovered = Backpack.For(player.gameObject).TakeAllMaterials();
        MetaProgress.AddMaterials(recovered);

        StoryItem? part = StorySlot.For(player.gameObject).Take();
        string story = "";

        if (part != null)
        {
            MetaProgress.AddStoryItem(part.Value);
            story = $"{StoryItems.Name(part.Value)} RECOVERED // {StoryItems.Progress(StoryItems.Owner(part.Value))}";
        }

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        // The rest of the run's scrap comes home with the bonus; the screen shows the run's total.
        int scrap = MetaProgress.EndRun(true, TotalFloors, FloorNumber, EnemiesKilled) + MetaProgress.ScrapSentHome;

        // The flight log: the evac frequency. The radio's complete and the chopper's coming: up to
        // the roof for the ending instead of the victory screen.
        if (part == StoryItem.FlightLog)
        {
            transition.Play("> UP TO THE ROOF", $"{ZoneName} // ROOF // EVAC INBOUND", EnterEvac);
            return;
        }

        victoryScreen.Show(ZoneName, FloorNumber, EnemiesKilled, Time.time - RunStartTime, health.Health, health.MaxHealth, scrap,
            Materials.Describe(recovered), story);
    }
}
