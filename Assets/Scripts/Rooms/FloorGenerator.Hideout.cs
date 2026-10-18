using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's hideout and meta-progression side: building the hideout (floor 0),
// applying the station upgrades at the start of a run, crafting-material drops, banking them at
// checkpoints, and the caged survivor waiting on each floor until they're rescued.
//   The hideout is a hub of rooms, Gungeon-Breach style: a big central hall with the deploy
// hatch in the middle and the generator in a corner, a barracks with the stash, and a wing for
// each survivor (the mechanic's workbench, the medic's station, the quartermaster's ammo press).
// A wing is sealed, dark and empty until its survivor has been rescued on a run; after that the
// survivor lives there by their station.
public partial class FloorGenerator
{
    [Header("Hideout")]
    [Tooltip("The main weapons the workbench's Armory upgrade starts a run with, by level (1, 2, 3).")]
    [SerializeField] private GameObject[] armoryWeapons;
    [Tooltip("Tint on the closed doors of wings whose survivor hasn't been rescued yet.")]
    [SerializeField] private Color sealedDoorTint = new Color(0.55f, 0.12f, 0.08f);

    [Header("Crafting Materials")]
    [Tooltip("Chance a zombie drops a material when it dies.")]
    [SerializeField, Range(0f, 1f)] private float enemyMaterialChance = 0.06f;
    [Tooltip("Chance a cleared combat room leaves a material.")]
    [SerializeField, Range(0f, 1f)] private float roomMaterialChance = 0.35f;
    [Tooltip("Materials a boss drops.")]
    [SerializeField, Min(0)] private int bossMaterials = 3;
    [Tooltip("Materials always waiting in the treasure room.")]
    [SerializeField, Min(0)] private int treasureMaterials = 2;

    [Header("Survivors")]
    [Tooltip("Chance a floor holds a caged survivor, once one's signal is findable (see Npcs). At most one a run.")]
    [SerializeField, Range(0f, 1f)] private float survivorChance = 0.25f;
    [Tooltip("Seconds after a floor starts before the survivor's distress call comes over the radio.")]
    [SerializeField] private float distressCallDelay = 3f;

    // Debug: the next floor generated holds the next findable survivor, chance or not.
    private static bool forceSurvivor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        forceSurvivor = false;
    }

    // One of the hideout's rooms: its cell, the cell and direction its door leads in from (in
    // the hall), and the survivor whose wing it is (none for the barracks).
    private struct HideoutWing
    {
        public string Name;
        public Vector2Int Cell;
        public Vector2Int FromCell;
        public Vector2Int Direction;
        public NpcKind? Owner;
    }

    private static readonly Vector2Int[] HallCells = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) };

    private static readonly HideoutWing[] Wings =
    {
        new HideoutWing { Name = "WORKSHOP", Cell = new Vector2Int(-1, 0), FromCell = new Vector2Int(0, 0), Direction = Vector2Int.left, Owner = NpcKind.Mechanic },
        new HideoutWing { Name = "MED BAY", Cell = new Vector2Int(2, 0), FromCell = new Vector2Int(1, 0), Direction = Vector2Int.right, Owner = NpcKind.Medic },
        new HideoutWing { Name = "SUPPLY ROOM", Cell = new Vector2Int(1, 2), FromCell = new Vector2Int(1, 1), Direction = Vector2Int.up, Owner = NpcKind.Quartermaster },
        new HideoutWing { Name = "BARRACKS", Cell = new Vector2Int(0, 2), FromCell = new Vector2Int(0, 1), Direction = Vector2Int.up, Owner = null }
    };

    // The middle of the hall's four cells.
    private Vector3 HallCentre => (CellToWorld(HallCells[0]) + CellToWorld(HallCells[3])) / 2f;

    // Where the player starts in the hideout: the south side of the hall, facing the hatch.
    private Vector3 HideoutSpawn => HallCentre + new Vector3(0f, 1f, -5.5f);

    // ---------- Building the hideout ----------

    private void GenerateHideout()
    {
        floorRoot = new GameObject("Hideout").transform;
        floorRoot.SetParent(transform, false);

        RoomShape square = RoomShape.Single;

        foreach (RoomShape shape in RoomShape.All)
        {
            if (shape.Name == "2x2")
                square = shape;
        }

        List<PlannedRoom> plan = new List<PlannedRoom>
        {
            new PlannedRoom { Shape = square, Cells = new List<Vector2Int>(HallCells), Type = RoomType.Start }
        };

        HashSet<(Vector2Int, Vector2Int)> doorways = new HashSet<(Vector2Int, Vector2Int)>();

        foreach (HideoutWing wing in Wings)
        {
            plan.Add(new PlannedRoom { Shape = RoomShape.Single, Cells = new List<Vector2Int> { wing.Cell }, Type = RoomType.Normal });
            doorways.Add((wing.FromCell, wing.Direction));
            doorways.Add((wing.Cell, -wing.Direction));
        }

        foreach (PlannedRoom planned in plan)
            BuildRoom(planned, doorways);

        foreach (HideoutWing wing in Wings)
            BuildCorridor(wing.FromCell, wing.Direction);

        Room hall = rooms[0];
        hall.DisplayName = "HALL";

        TraderStock.Restock(rng);
        BuildHall(hall);

        for (int i = 0; i < Wings.Length; i++)
            BuildWing(Wings[i], rooms[i + 1]);

        BuildNavMesh();
        PlacePlayer();
        Debug.Log($"Generated the hideout: {MetaProgress.RescuedCount()} of {Npcs.All.Length} survivors rescued");
    }

    // The deploy hatch in the middle (framed by hazard posts, under a green light), the generator
    // in the north-west corner, a planning table and supplies stacked about.
    private void BuildHall(Room hall)
    {
        Vector3 centre = HallCentre;
        Transform parent = hall.transform;

        SpawnExit(hall, FloorExit.Kind.NextFloor);
        Transform hatch = parent.Find("Hatch");
        Vector3 hatchAt = centre + new Vector3(0f, 0f, 2.5f);

        // Labelled by its FloorExit ("[E] DEPLOY").
        if (hatch != null)
            hatch.position = hatchAt;

        // Hazard posts and a low rail round the hatch, gaps front and back to walk in.
        Color yellow = new Color(0.85f, 0.7f, 0.1f);
        Color dark = new Color(0.08f, 0.08f, 0.08f);

        foreach (Vector2 corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
        {
            Vector3 post = hatchAt + new Vector3(corner.x * 1.9f, 0f, corner.y * 1.9f);
            HideoutProp("Post", parent, post + Vector3.up * 0.5f, new Vector3(0.18f, 1f, 0.18f), yellow, yellow * 0.15f, true);
            HideoutProp("Post Stripe", parent, post + Vector3.up * 0.75f, new Vector3(0.2f, 0.12f, 0.2f), dark, Color.black, false);
            HideoutProp("Post Stripe", parent, post + Vector3.up * 0.35f, new Vector3(0.2f, 0.12f, 0.2f), dark, Color.black, false);
        }

        HideoutProp("Rail", parent, hatchAt + new Vector3(-1.9f, 0.7f, 0f), new Vector3(0.08f, 0.08f, 3.8f), yellow, Color.black, false);
        HideoutProp("Rail", parent, hatchAt + new Vector3(1.9f, 0.7f, 0f), new Vector3(0.08f, 0.08f, 3.8f), yellow, Color.black, false);

        Light beam = new GameObject("Hatch Light").AddComponent<Light>();
        beam.transform.SetParent(parent, false);
        beam.transform.position = hatchAt + Vector3.up * 4f;
        beam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        beam.type = LightType.Spot;
        beam.spotAngle = 55f;
        beam.range = 7f;
        beam.intensity = 9f;
        beam.color = new Color(0.4f, 1f, 0.6f);
        beam.shadows = LightShadows.None;

        // The generator, against the west wall at the north end.
        Vector3 west = centre + new Vector3(-(CellStep.x / 2f + roomSize.x / 2f) + 1f, 0f, CellStep.y / 2f + 2.5f);
        StationConsole.Create(HideoutStation.Generator, west, Vector3.right, parent);

        // Fuel drums by the generator.
        Color drum = new Color(0.45f, 0.12f, 0.08f);
        Vector3 drumSize = new Vector3(0.6f, 1f, 0.6f);
        Furniture("DrumRed", "Drum", parent, west + new Vector3(0.2f, 0f, -2.3f), new Vector3(1f, 0f, 0.4f), drumSize, drum, true, PrimitiveType.Cylinder);
        Furniture("DrumRed", "Drum", parent, west + new Vector3(0.9f, 0f, -2.6f), new Vector3(0.6f, 0f, -1f), drumSize, drum, true, PrimitiveType.Cylinder);
        Furniture("DrumBlue", "Drum", parent, west + new Vector3(0.3f, 0f, -3.1f), Vector3.right, drumSize, new Color(0.2f, 0.25f, 0.3f), true, PrimitiveType.Cylinder);

        // The intel desk east of the hatch: a planning table with a lit map and the radio (the
        // Recon upgrade).
        Vector3 table = centre + new Vector3(8f, 0f, 3f);
        Color wood = new Color(0.35f, 0.25f, 0.15f);
        StationConsole.Create(HideoutStation.IntelDesk, table, Vector3.back, parent);

        // A shooting range down the east side: a training dummy, sandbags behind it, and a
        // painted firing line to stand at (guns don't use ammo in the hideout).
        Vector3 dummy = centre + new Vector3(13f, 0f, -1.5f);
        TrainingDummy.Create(dummy, Vector3.left, parent);
        Furniture("Sandbags", "Range Backstop", parent, dummy + new Vector3(1.6f, 0f, 0f), Vector3.left, new Vector3(3f, 0.9f, 0.8f), new Color(0.45f, 0.4f, 0.3f));

        if (hazardStripeMaterial != null)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Quad);
            line.name = "Firing Line";
            Destroy(line.GetComponent<Collider>());
            line.transform.SetParent(parent, false);
            line.transform.position = centre + new Vector3(8.5f, 0.012f, -1.5f);
            line.transform.rotation = Quaternion.Euler(90f, 90f, 0f);
            line.transform.localScale = new Vector3(3f, 0.35f, 1f);
            line.GetComponent<Renderer>().sharedMaterial = hazardStripeMaterial;
        }

        // Benches either side of the walk from the spawn to the hatch.
        Furniture("Bench", "Bench", parent, centre + new Vector3(-3.5f, 0f, -2f), Vector3.right, new Vector3(2.4f, 0.5f, 0.5f), wood);
        Furniture("Bench", "Bench", parent, centre + new Vector3(3.5f, 0f, -2f), Vector3.left, new Vector3(2.4f, 0.5f, 0.5f), wood);

        // Supply crates and sandbags along the walls. The crates are stacked against the sealed
        // med bay door and are cleared away once the medic has been rescued.
        Color crate = new Color(0.3f, 0.33f, 0.2f);
        Color sand = new Color(0.45f, 0.4f, 0.3f);
        float eastWall = centre.x + CellStep.x / 2f + roomSize.x / 2f - 0.9f;
        float southWall = centre.z - CellStep.y / 2f - roomSize.y / 2f + 0.9f;

        if (!MetaProgress.IsRescued(NpcKind.Medic))
        {
            Furniture("Crate", "Crate", parent, new Vector3(eastWall, 0f, centre.z - 6f), Vector3.left, new Vector3(1.1f, 0.9f, 1.1f), crate);
            Furniture("Crate", "Crate", parent, new Vector3(eastWall, 0.9f, centre.z - 6f), new Vector3(-1f, 0f, 0.3f), new Vector3(0.8f, 0.6f, 0.8f), crate * 1.1f);
            Furniture("Crate", "Crate", parent, new Vector3(eastWall - 0.2f, 0f, centre.z - 7.4f), Vector3.left, new Vector3(1.2f, 0.9f, 1f), crate);
        }


        Furniture("Sandbags", "Sandbags", parent, new Vector3(centre.x - 9f, 0f, southWall), Vector3.forward, new Vector3(3f, 0.7f, 0.8f), sand);
        Furniture("Sandbags", "Sandbags", parent, new Vector3(centre.x + 9f, 0f, southWall), Vector3.forward, new Vector3(3f, 0.7f, 0.8f), sand);

        // Four pillars framing the middle of the hall round the hatch.
        foreach (Vector2 corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            HideoutPillar(hall, centre + new Vector3(corner.x * 7f, 0f, corner.y > 0 ? 7f : -3.5f));

        // A burning barrel with crates round it to sit on, west of the walk in: the hub's warm
        // corner.
        Vector3 fire = centre + new Vector3(-9f, 0f, -5.5f);
        Furniture("FireBarrel", "Fire Barrel", parent, fire, Vector3.back, new Vector3(0.6f, 0.9f, 0.6f), new Color(0.2f, 0.12f, 0.08f), true, PrimitiveType.Cylinder);
        Renderer flame = HideoutProp("Flame", parent, fire + Vector3.up * 0.95f, new Vector3(0.5f, 0.24f, 0.5f), new Color(1f, 0.5f, 0.15f), new Color(1f, 0.45f, 0.1f) * 2f, false, PrimitiveType.Cylinder).GetComponent<Renderer>();
        Light warmth = new GameObject("Fire Light").AddComponent<Light>();
        warmth.transform.SetParent(parent, false);
        warmth.transform.position = fire + Vector3.up * 1.6f;
        warmth.type = LightType.Point;
        warmth.range = 7f;
        warmth.intensity = 4f;
        warmth.color = new Color(1f, 0.55f, 0.2f);
        warmth.shadows = LightShadows.None;
        FireFlicker.Add(warmth, flame);

        for (int i = 0; i < 3; i++)
        {
            float angle = i * 2.1f + 0.5f;
            Vector3 seat = fire + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.5f;
            Furniture("Crate", "Seat", parent, seat, fire - seat, new Vector3(0.6f, 0.5f, 0.5f), crate * 0.9f);
        }

        // A rug under the planning table.
        HideoutProp("Rug", parent, table + Vector3.up * 0.01f, new Vector3(4f, 0.02f, 2.6f), new Color(0.35f, 0.12f, 0.1f), Color.black, false);

        // A trophy on a shelf for each victory (up to five).
        int trophies = Mathf.Min(5, MetaProgress.Victories);

        if (trophies > 0)
        {
            Vector3 shelf = new Vector3(eastWall + 0.5f, 1.3f, centre.z);
            HideoutProp("Trophy Shelf", parent, shelf, new Vector3(0.4f, 0.08f, 3f), new Color(0.35f, 0.25f, 0.15f), Color.black, false);

            for (int i = 0; i < trophies; i++)
            {
                Vector3 spot = shelf + new Vector3(0f, 0.04f, (i - (trophies - 1) / 2f) * 0.55f);

                if (PropModel("Trophy", "Trophy", parent, spot, Vector3.left, new Vector3(0.3f, 0.36f, 0.22f), false) == null)
                    HideoutProp("Trophy", parent, spot + Vector3.up * 0.18f, new Vector3(0.22f, 0.36f, 0.22f),
                        new Color(1f, 0.8f, 0.3f), new Color(0.5f, 0.35f, 0.05f), false, PrimitiveType.Cylinder);
            }
        }

        PlaceHideoutClutter(hall, new[]
        {
            (new Vector3(centre.x - 14f, 0f, southWall + 0.2f), Vector3.back),
            (new Vector3(centre.x + 14f, 0f, southWall + 0.2f), Vector3.back),
            (new Vector3(eastWall + 0.1f, 0f, centre.z + 8f), Vector3.right)
        });

        SpawnTrash(hall);
    }

    // A survivor's wing (lit, with their station and them by it) once they're rescued; until then
    // its door is sealed shut, with a note saying who's needed, and the room beyond is dark and
    // bare. The barracks is always open, with bunks and the stash.
    private void BuildWing(HideoutWing wing, Room room)
    {
        room.DisplayName = wing.Name;
        bool open = wing.Owner == null || MetaProgress.IsRescued(wing.Owner.Value);
        RoomDoor door = null;

        foreach (RoomConnection connection in room.Connections)
            door = connection.Door;

        if (!open)
        {
            NpcKind owner = wing.Owner.Value;

            if (door != null)
            {
                door.SetOpen(false);
                door.GetComponent<Renderer>().enabled = true;
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", sealedDoorTint);
                door.GetComponent<Renderer>().SetPropertyBlock(block);

                // The note sits on the hall side of the door.
                GameObject sign = new GameObject("Sealed Sign");
                sign.transform.SetParent(rooms[0].transform, false);
                sign.transform.position = door.transform.position - new Vector3(wing.Direction.x, 0f, wing.Direction.y) * 2.5f;
                ProximityLabel.Add(sign, 1.2f, 4.5f, sealedDoorTint * 1.6f, () => SealedText(wing.Name, owner));
            }

            // Dark beyond the door.
            foreach (Light light in room.GetComponentsInChildren<Light>())
                Destroy(light.gameObject);

            return;
        }

        Vector3 centre = CellToWorld(wing.Cell);
        Vector3 inward = new Vector3(-wing.Direction.x, 0f, -wing.Direction.y);   // from this room towards the hall
        Vector3 far = -inward;                                                    // to the back wall
        Vector3 side = new Vector3(far.z, 0f, -far.x);
        float depth = wing.Direction.x != 0 ? roomSize.x / 2f : roomSize.y / 2f;
        float width = wing.Direction.x != 0 ? roomSize.y / 2f : roomSize.x / 2f;

        if (wing.Owner is NpcKind kind)
        {
            Vector3 station = centre + far * (depth - 1.3f);
            StationConsole.Create(Npcs.Station(kind), station, inward, room.transform);
            HideoutNpc.Create(kind, station + inward * 2.2f + side * 1.8f, inward, room.transform);
            DressWing(kind, room, centre, far, side, depth, width);
        }
        else
        {
            BuildBarracks(room, centre, far, side, depth, width);
        }

        // No trash pass here: the wings are combat-type rooms to the trash code, which would strew
        // them with barriers and tyres. Lived-in rooms stay tidy.
    }

    // What a sealed wing's sign says: how many more completed runs until the survivor's signal
    // can be picked up, or that it's out there now.
    private static string SealedText(string wing, NpcKind owner, string state = "SEALED")
    {
        string header = wing + $"  <size=70%><alpha=#99>// {state}</size>\n";
        int needed = Npcs.RequiredVictories(owner) - MetaProgress.Victories;

        if (needed > 0)
            return header + $"<size=70%><alpha=#BB>NO SIGNAL // COMPLETE {needed} MORE RUN{(needed == 1 ? "" : "S")} TO PICK UP THE {Npcs.Role(owner)}</size>";

        return header + $"<size=70%><color={UiColours.AmberHex}>DISTRESS SIGNAL ACTIVE // FIND THE {Npcs.Role(owner)} ON A RUN</color></size>";
    }

    // A few things to make each wing its own: shelves of parts for the workshop, beds and
    // supplies for the med bay, crates and racks for the supply room.
    private void DressWing(NpcKind kind, Room room, Vector3 centre, Vector3 far, Vector3 side, float depth, float width)
    {
        Transform parent = room.transform;
        Quaternion facing = Quaternion.LookRotation(-far);
        Color metal = new Color(0.22f, 0.23f, 0.24f);

        switch (kind)
        {
            case NpcKind.Mechanic:
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 shelf = centre + side * s * (width - 0.6f) + far * (depth - 3.5f);
                    Furniture("Shelving", "Shelving", parent, shelf, -side * s, new Vector3(2.4f, 2f, 0.6f), metal);

                    // The model has its bins and boxes on the shelves already.
                    if (!HasPropModel("Shelving"))
                    {
                        HideoutProp("Parts Bin", parent, shelf + Vector3.up * 1.3f - side * s * 0.05f, RotateSize(facing, new Vector3(0.65f, 0.3f, 0.6f)), new Color(0.6f, 0.4f, 0.1f), Color.black, false);
                        HideoutProp("Parts Bin", parent, shelf + Vector3.up * 0.6f + far * 0.4f, RotateSize(facing, new Vector3(0.65f, 0.3f, 0.6f)), new Color(0.2f, 0.3f, 0.5f), Color.black, false);
                    }
                }
                // In the front corner opposite the clutter by the door.
                Furniture("Tyres", "Tyres", parent, centre - side * (width - 1.2f) - far * (depth - 2f), far, new Vector3(0.9f, 0.55f, 0.9f),
                    new Color(0.06f, 0.06f, 0.06f), true, PrimitiveType.Cylinder);
                break;

            case NpcKind.Medic:
                for (int i = 0; i < 2; i++)
                {
                    // Head against the wall.
                    Vector3 bed = centre - side * (width - 1.2f) + far * (depth - 2.5f - i * 2.6f);

                    if (PropModel("HospitalBed", "Bed", parent, bed, side, new Vector3(0.9f, 0.88f, 2f)) == null)
                    {
                        HideoutProp("Bed", parent, bed + Vector3.up * 0.35f, RotateSize(Quaternion.LookRotation(side), new Vector3(0.9f, 0.25f, 2f)), new Color(0.75f, 0.77f, 0.75f), Color.black, true);
                        HideoutProp("Bed Frame", parent, bed + Vector3.up * 0.15f, RotateSize(Quaternion.LookRotation(side), new Vector3(0.85f, 0.3f, 1.9f)), metal, Color.black, true);
                    }
                }
                Furniture("Screen", "Curtain", parent, centre - side * (width - 2.6f) + far * (depth - 3.8f), side, new Vector3(2.2f, 2f, 0.4f), new Color(0.5f, 0.65f, 0.6f));
                Furniture("MedCabinet", "Supplies", parent, centre + side * (width - 0.3f) + far * (depth - 3f), -side, new Vector3(1.2f, 1.2f, 0.5f), new Color(0.8f, 0.8f, 0.8f));
                break;

            default:
                for (int i = 0; i < 3; i++)
                {
                    Vector3 crate = centre - side * (width - 0.9f) + far * (depth - 1.5f - i * 1.3f);
                    Furniture("AmmoCrate", "Ammo Crate", parent, crate, side, new Vector3(1.1f, 0.6f, 0.7f), new Color(0.3f, 0.35f, 0.2f));
                    if (i != 1)
                        Furniture("AmmoCrate", "Ammo Crate", parent, crate + Vector3.up * 0.6f, side, new Vector3(0.9f, 0.5f, 0.6f), new Color(0.33f, 0.37f, 0.22f));
                }

                // The rack against the other side wall, rifles stood in it.
                Vector3 rack = centre + side * (width - 0.25f) + far * (depth - 3.5f);
                Furniture("GunRack", "Gun Rack", parent, rack, -side, new Vector3(2.6f, 2f, 0.3f), new Color(0.3f, 0.22f, 0.14f));

                if (!HasPropModel("GunRack"))
                {
                    for (int g = -2; g <= 2; g++)
                        HideoutProp("Rifle", parent, rack + Vector3.up * 1.1f + far * (g * 0.45f) - side * 0.25f, new Vector3(0.06f, 1.1f, 0.06f), new Color(0.1f, 0.1f, 0.1f), Color.black, false);
                }
                break;
        }

        PlaceHideoutClutter(room, new[] { (centre - far * (depth - 1f) + side * (width - 1.5f), -far) });
    }

    // Bunks along the walls, lockers, and the stash: a locker whose label lists the banked
    // materials and the records. The trader's stall stands by the door once they're rescued.
    private void BuildBarracks(Room room, Vector3 centre, Vector3 far, Vector3 side, float depth, float width)
    {
        Transform parent = room.transform;
        BuildTradePost(room, centre - side * (width - 2.2f) - far * (depth - 2.6f), side, -side);
        Color metal = new Color(0.25f, 0.27f, 0.25f);
        Color blanket = new Color(0.25f, 0.3f, 0.2f);

        for (int s = -1; s <= 1; s += 2)
        {
            for (int i = 0; i < 2; i++)
            {
                // Head against the side wall, the ladder at the end towards the middle.
                Vector3 bunk = centre + side * s * (width - 1.2f) + far * (depth - 1.4f - i * 2.8f);

                if (PropModel("BunkBed", "Bunk", parent, bunk, -far * s, new Vector3(2f, 1.9f, 0.9f)) != null)
                    continue;

                Vector3 size = RotateSize(Quaternion.LookRotation(far), new Vector3(2f, 0.15f, 0.9f));
                HideoutProp("Bunk", parent, bunk + Vector3.up * 0.45f, size, blanket, Color.black, true);
                HideoutProp("Bunk", parent, bunk + Vector3.up * 1.45f, size, blanket, Color.black, true);
                HideoutProp("Bunk Frame", parent, bunk + Vector3.up * 1f, size + Vector3.up * 1.8f - new Vector3(0.1f, 0f, 0.1f), metal, Color.black, true);
            }
        }

        // The stash: a row of lockers against the back wall, the middle one lit.
        Quaternion facing = Quaternion.LookRotation(-far);

        for (int i = -2; i <= 2; i++)
        {
            Vector3 locker = centre + far * (depth - 0.4f) + side * i * 0.75f;

            if (PropModel(i == 0 ? "StashLocker" : "Locker", "Locker", parent, locker, -far, new Vector3(0.7f, 2f, 0.6f)) == null)
                HideoutProp("Locker", parent, locker + Vector3.up * 1f, RotateSize(facing, new Vector3(0.7f, 2f, 0.6f)),
                    i == 0 ? new Color(0.25f, 0.4f, 0.3f) : metal, i == 0 ? new Color(0.05f, 0.25f, 0.1f) : Color.black, true);
        }

        GameObject stash = new GameObject("Stash");
        stash.transform.SetParent(parent, false);
        stash.transform.position = centre + far * (depth - 0.9f);
        ProximityLabel.Add(stash, 2.3f, 3.5f, UiColours.Amber, StashText);

        Light glow = new GameObject("Stash Light").AddComponent<Light>();
        glow.transform.SetParent(stash.transform, false);
        glow.transform.localPosition = Vector3.up * 1.8f - far * 0.6f;
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.85f, 0.45f);
        glow.range = 4f;
        glow.intensity = 2f;
        glow.shadows = LightShadows.None;
    }

    // The trader's stall, facing into the room, with the trader behind it; until they're
    // rescued, a bare counter with a note like a sealed wing's.
    private void BuildTradePost(Room room, Vector3 position, Vector3 facing, Vector3 behind)
    {
        if (MetaProgress.IsRescued(NpcKind.Trader))
        {
            StationConsole.Create(HideoutStation.TradePost, position, facing, room.transform);
            HideoutNpc.Create(NpcKind.Trader, position + behind * 1.1f, facing, room.transform);
            return;
        }

        Furniture("Counter", "Empty Counter", room.transform, position, facing, new Vector3(2.4f, 1.15f, 0.7f), new Color(0.3f, 0.22f, 0.14f));

        GameObject sign = new GameObject("Trade Post Sign");
        sign.transform.SetParent(room.transform, false);
        sign.transform.position = position + facing * 0.8f;
        ProximityLabel.Add(sign, 1.4f, 3.5f, sealedDoorTint * 1.6f, () => SealedText("TRADE POST", NpcKind.Trader, "UNMANNED"));
    }

    private static string StashText()
    {
        string text = $"STASH  <size=70%><alpha=#99>// {MetaProgress.Scrap} SCRAP</size>\n<size=65%>";

        foreach (MaterialType type in Materials.All)
            text += $"<color={Materials.Hex(type)}>{Materials.Name(type)} {MetaProgress.MaterialCount(type)}</color>  ";

        return text + $"\n<alpha=#99>RUNS {MetaProgress.Runs} // WINS {MetaProgress.Victories} // DEEPEST {MetaProgress.BestFloor} // KILLS {MetaProgress.Kills}</size>";
    }

    // One of the floors' pillars, placed by hand.
    private void HideoutPillar(Room room, Vector3 position)
    {
        GameObject pillar;

        if (pillarPrefab != null)
        {
            pillar = Instantiate(pillarPrefab, position, Quaternion.Euler(0f, 90f * rng.Next(4), 0f), room.transform);
            pillar.name = "Pillar";
            pillar.transform.localScale = new Vector3(pillarSize, pillarHeight, pillarSize);
        }
        else
        {
            pillar = CreateBlock("Pillar", room.transform, ToLocal(room.transform, position + Vector3.up * wallHeight / 2f),
                new Vector3(pillarSize, wallHeight, pillarSize), wallMaterial);
        }

        pillar.tag = "Wall";
        pillar.GetComponent<Collider>().sharedMaterial = slidePhysics;
    }

    // Cardboard box clusters at given spots against walls (position, the wall's outward direction).
    private void PlaceHideoutClutter(Room room, (Vector3 spot, Vector3 outward)[] spots)
    {
        if (clutterBoxes == null || clutterBoxes.Length == 0)
            return;

        foreach ((Vector3 spot, Vector3 outward) in spots)
            PlaceCluster(room, spot, outward, new Vector3(outward.z, 0f, outward.x));
    }

    // A size given in a facing's own axes, as a world-aligned box size (facings are always square
    // to the grid here).
    private static Vector3 RotateSize(Quaternion facing, Vector3 size)
    {
        Vector3 rotated = facing * size;
        return new Vector3(Mathf.Abs(rotated.x), Mathf.Abs(rotated.y), Mathf.Abs(rotated.z));
    }

    // A piece of furniture: its model if there is one (see PropModel), else a tinted block the
    // same size. `position` is the floor point under its centre, `size` is in its own axes
    // (width, height, depth) and `front` is the way it faces.
    private GameObject Furniture(string model, string name, Transform parent, Vector3 position, Vector3 front, Vector3 size,
        Color colour, bool solid = true, PrimitiveType shape = PrimitiveType.Cube)
    {
        GameObject prop = PropModel(model, name, parent, position, front, size, solid);

        if (prop != null)
            return prop;

        return HideoutProp(name, parent, position + Vector3.up * size.y / 2f, RotateSize(Quaternion.LookRotation(front), size),
            colour, Color.black, solid, shape);
    }

    private bool HasPropModel(string model)
    {
        return furnitureVisuals != null && furnitureVisuals.ModelFor(model) != null;
    }

    // A furniture model stood on the floor at `position`, facing `front`, stretched to fit `size`
    // (its own width, height, depth) so it takes the same space the block did; solid ones get a
    // box collider that size (the navmesh is built from colliders). Null without a model.
    private GameObject PropModel(string model, string name, Transform parent, Vector3 position, Vector3 front, Vector3 size,
        bool solid = true)
    {
        GameObject prefab = furnitureVisuals != null ? furnitureVisuals.ModelFor(model) : null;

        if (prefab == null)
            return null;

        // Built at the world origin first, so the model's bounds are in the root's own space.
        GameObject root = new GameObject(name);
        GameObject body = Instantiate(prefab, root.transform);
        body.name = "Model";

        Renderer[] renderers = body.GetComponentsInChildren<Renderer>();

        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Vector3 fit = new Vector3(size.x / Mathf.Max(bounds.size.x, 0.001f), size.y / Mathf.Max(bounds.size.y, 0.001f),
                size.z / Mathf.Max(bounds.size.z, 0.001f));
            body.transform.localScale = Vector3.Scale(body.transform.localScale, fit);
            body.transform.localPosition = -Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), fit);
        }

        root.transform.SetParent(parent, true);
        root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(front, Vector3.up));

        if (solid)
        {
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = Vector3.up * size.y / 2f;
            box.size = size;
        }

        return root;
    }

    // A solid (or not) tinted block of the hideout's furniture, from the pickups' material.
    private GameObject HideoutProp(string name, Transform parent, Vector3 position, Vector3 size, Color colour, Color emission,
        bool solid, PrimitiveType shape = PrimitiveType.Cube)
    {
        GameObject prop = GameObject.CreatePrimitive(shape);
        prop.name = name;
        prop.transform.SetParent(parent, true);
        prop.transform.position = position;
        prop.transform.localScale = shape == PrimitiveType.Cylinder ? new Vector3(size.x, size.y / 2f, size.z) : size;

        if (!solid)
            Destroy(prop.GetComponent<Collider>());

        Renderer renderer = prop.GetComponent<Renderer>();
        renderer.shadowCastingMode = solid ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

        Material material = ConsumablePickup.DefaultMaterial != null ? ConsumablePickup.DefaultMaterial : ammoMaterial;

        if (material != null)
        {
            renderer.sharedMaterial = material;
            ConsumableModel.Tint(renderer, colour, emission);
        }

        return prop;
    }

    // ---------- Recon ----------

    // The Recon upgrade: the boss room (level 1), or every room (level 2), shows on the maps from
    // the start of each floor. Scouted rooms still show as unexplored until entered.
    private void ApplyRecon()
    {
        int recon = MetaProgress.Level(MetaUpgrade.Recon);

        if (recon <= 0)
            return;

        foreach (Room room in rooms)
        {
            if (recon >= 2 || room.Type == RoomType.Boss)
                room.Scouted = true;
        }
    }

    // ---------- Run start ----------

    // The start of a run (floor 1): applies the station upgrades to the player and starts the
    // clock and the run's scrap tally. Waits a frame so the player's own start-up (starting gun,
    // starting ammo) has happened first.
    private System.Collections.IEnumerator BeginRun()
    {
        runPerksApplied = true;
        RunStartTime = Time.time;
        MetaProgress.BeginRun();
        TraderStock.Settle();
        yield return null;

        if (player == null)
            yield break;

        int toughness = MetaProgress.Level(MetaUpgrade.Toughness);
        int conditioning = MetaProgress.Level(MetaUpgrade.Conditioning);
        int cache = MetaProgress.Level(MetaUpgrade.AmmoCache);
        int kit = MetaProgress.Level(MetaUpgrade.FieldKit);
        int armory = MetaProgress.Level(MetaUpgrade.Armory);
        int gunsmith = MetaProgress.Level(MetaUpgrade.Gunsmith);

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

        // Attachments in the backpack, usually ones that fit the guns carried.
        if (gunsmith > 0)
        {
            foreach (WeaponAttachment attachment in PickAttachments(gunsmith, () => UnityEngine.Random.value))
                backpack.AddAttachment(attachment);
        }
    }

    // ---------- Crafting materials ----------

    // A random material, or null if materials are off. Deeper floors lean a little towards the
    // scarcer ones (wiring, fuel).
    private MaterialType PickMaterial(float roll)
    {
        float scarce = Mathf.Clamp01((FloorNumber - 1) * 0.15f);
        float[] weights = { 1.2f, 0.8f + scarce, 1f, 1f, 0.7f + scarce };
        float total = 0f;

        foreach (float weight in weights)
            total += weight;

        float target = roll * total;

        for (int i = 0; i < weights.Length; i++)
        {
            target -= weights[i];

            if (target <= 0f)
                return (MaterialType)i;
        }

        return MaterialType.Parts;
    }

    // A zombie sometimes drops a material; a boss drops several in a ring.
    private void DropMaterials(Room room, Vector3 position, bool boss)
    {
        if (!boss)
        {
            if (UnityEngine.Random.value < enemyMaterialChance)
                MaterialPickup.Spawn(PickMaterial(UnityEngine.Random.value), 1, position + new Vector3(0.6f, 0f, 0.4f), room.transform);

            return;
        }

        for (int i = 0; i < bossMaterials; i++)
        {
            float angle = (i / (float)Mathf.Max(1, bossMaterials)) * Mathf.PI * 2f + 0.4f;
            Vector3 spot = position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2.2f;
            MaterialPickup.Spawn(PickMaterial(UnityEngine.Random.value), 1, spot, room.transform);
        }
    }

    // A cleared room sometimes leaves a material where its last zombie fell.
    private void DropRoomMaterial(Room room, Vector3 position)
    {
        if (UnityEngine.Random.value < roomMaterialChance)
            MaterialPickup.Spawn(PickMaterial(UnityEngine.Random.value), 1, position + new Vector3(0f, 0f, -0.9f), room.transform);
    }

    private void SpawnTreasureMaterials(Room room)
    {
        if (room.Type != RoomType.Treasure)
            return;

        for (int i = 0; i < treasureMaterials; i++)
        {
            Vector3 offset = new Vector3((i - (treasureMaterials - 1) / 2f) * 1.1f, 0f, 2.8f);
            MaterialPickup.Spawn(PickMaterial((float)rng.NextDouble()), 1, room.transform.position + offset, room.transform);
        }
    }

    // Debug: one of each material in a row at a point.
    public void SpawnAllMaterials(Vector3 position, Vector3 right)
    {
        Transform parent = TryGetRoom(position, out Room room) ? room.transform : transform;
        MaterialType[] all = Materials.All;

        for (int i = 0; i < all.Length; i++)
            MaterialPickup.Spawn(all[i], 2, position + right * ((i - (all.Length - 1) / 2f) * 1.1f), parent);
    }

    // ---------- Caged survivors ----------

    // Whether this run has already turned up a survivor (at most one a run).
    private bool survivorThisRun;

    // Sometimes a floor holds a survivor: the earliest one whose signal is findable (enough runs
    // completed, not yet rescued), at survivorChance per floor and at most one a run. Caged in
    // the combat room furthest from the start (so it takes some getting to), in the cell its door
    // opens into, towards the back, with a radio distress call once the floor starts. Placed
    // before the navmesh is built so zombies path round the cage.
    private void PlaceCagedSurvivor(List<PlannedRoom> plan)
    {
        NpcKind? kind = Npcs.NextToFind();

        if (kind == null || survivorThisRun || (!forceSurvivor && rng.NextDouble() >= survivorChance))
            return;

        PlannedRoom best = null;

        foreach (PlannedRoom planned in plan)
        {
            if (planned.Type == RoomType.Normal && (best == null || planned.Depth > best.Depth))
                best = planned;
        }

        if (best == null || !roomsByCell.TryGetValue(best.Cells[0], out Room room))
            return;

        room.DisplayName = "SURVIVOR";
        Vector3 centre = CellToWorld(best.Cells[0]);
        List<Vector3> doors = DoorwayPositions(room);
        Vector3 exit = centre + Vector3.back * 3f;
        float nearest = float.MaxValue;

        // The doorway nearest this cell: the one they'll walk out of.
        foreach (Vector3 door in doors)
        {
            float distance = Vector3.Distance(door, centre);

            if (distance < nearest)
            {
                nearest = distance;
                exit = door;
            }
        }

        // Off the middle (that's where loot and the crate like to go), towards the back wall.
        Vector3 away = centre - exit;
        away.y = 0f;
        Vector3 spot = centre + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * 2.2f;

        CagedSurvivor.Create(kind.Value, room, spot, exit, ConsumablePickup.DefaultMaterial != null ? ConsumablePickup.DefaultMaterial : ammoMaterial);
        survivorThisRun = true;
        forceSurvivor = false;
        StartCoroutine(DistressCall(kind.Value));

        // Counted as a pillar, so zombies, boxes, trash and crates keep off it.
        if (!pillars.TryGetValue(room, out List<Vector3> list))
            pillars[room] = list = new List<Vector3>();

        list.Add(spot);
    }

    // The radio crackles with their call for help a few seconds into the floor (after the
    // floor transition has faded out).
    private System.Collections.IEnumerator DistressCall(NpcKind kind)
    {
        yield return new WaitForSeconds(distressCallDelay);
        RadioMessage.Show("DISTRESS SIGNAL", Npcs.DistressCall(kind));
    }

    // Debug: the next floor generated holds the next findable survivor, chance or not.
    public static void ForceSurvivorOnNextFloor()
    {
        forceSurvivor = true;
    }
}
