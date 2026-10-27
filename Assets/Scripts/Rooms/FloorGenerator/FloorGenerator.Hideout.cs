using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's hideout: building floor 0. (The run-start upgrades are in
// FloorGenerator.RunStart.cs, caged survivors in .Survivors.cs, material drops in .Loot.cs and
// the furniture helpers in .Furniture.cs.)
//   The hideout is one big hall: the deploy hatch in the middle, the generator on the north
// wall, the intel desk, a shooting range, a fire, a barracks corner with the stash, the truck
// bay (the military truck the mechanic is putting back together), and an area along the walls
// for each survivor (the mechanic's workshop, the medic's med bay, the
// quartermaster's supply room, the trader's stall). An area lies abandoned under dust sheets and
// empty boxes until its survivor is rescued on a run; then they tidy it up and set up there.
//   Each zone has its own hideout with that same layout (the survivors and the truck travel
// together), on the zone's floor and walls and dressed for the place: a hangar at the base, a
// boarded-up store at the mall, a barred-up emergency room at the hospital, a baggage sorting
// room at the airport (FloorGenerator.HideoutZones.cs).
public partial class FloorGenerator
{
    [Header("Hideout")]
    [Tooltip("The main weapons the workbench's Armory upgrade starts a run with, by level (1, 2, 3).")]
    [SerializeField] private GameObject[] armoryWeapons;
    [Tooltip("Tint on the closed doors of wings whose survivor hasn't been rescued yet.")]
    [SerializeField] private Color sealedDoorTint = new Color(0.55f, 0.12f, 0.08f);

    // A survivor's area of the hall: the wall it backs onto (`Back` points at it), where along
    // that wall its middle is (`Along`, on the axis `Side` gives), how deep it reaches out from
    // the wall and how wide it is either side of its middle, and whose it is.
    private struct HideoutArea
    {
        public string Name;
        public NpcKind Owner;
        public Vector3 Back;
        public float Along;
        public float Depth;
        public float Width;
    }

    private static readonly Vector2Int[] HallCells = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) };

    private static readonly HideoutArea[] Areas =
    {
        new HideoutArea { Name = "WORKSHOP", Owner = NpcKind.Mechanic, Back = Vector3.forward, Along = -13.5f, Depth = 3.5f, Width = 4f },
        new HideoutArea { Name = "SUPPLY ROOM", Owner = NpcKind.Quartermaster, Back = Vector3.forward, Along = 13.5f, Depth = 3.5f, Width = 4f },
        new HideoutArea { Name = "MED BAY", Owner = NpcKind.Medic, Back = Vector3.left, Along = 0f, Depth = 3.5f, Width = 4f },
        new HideoutArea { Name = "TRADE POST", Owner = NpcKind.Trader, Back = Vector3.back, Along = -13.5f, Depth = 3f, Width = 3f },
        new HideoutArea { Name = "KITCHEN", Owner = NpcKind.Cook, Back = Vector3.right, Along = -4.5f, Depth = 3f, Width = 3f }
    };

    // The middle of the hall's four cells.
    private Vector3 HallCentre => (CellToWorld(HallCells[0]) + CellToWorld(HallCells[3])) / 2f;

    // From the hall's middle to its walls, east-west and north-south.
    private Vector2 HallHalf => new Vector2(CellStep.x / 2f + roomSize.x / 2f, CellStep.y / 2f + roomSize.y / 2f);

    // Where the player starts in the hideout: the south side of the hall, facing the hatch.
    private Vector3 HideoutSpawn => HallCentre + new Vector3(0f, 1f, -5.5f);

    // Along the wall an area backs onto (left to right, looking at the wall).
    private static Vector3 Side(HideoutArea area) => new Vector3(area.Back.z, 0f, -area.Back.x);

    private Vector3 Middle(HideoutArea area)
    {
        float toWall = Mathf.Abs(area.Back.x) * HallHalf.x + Mathf.Abs(area.Back.z) * HallHalf.y;
        return HallCentre + area.Back * (toWall - area.Depth) + Side(area) * area.Along;
    }

    // One big hall, no side rooms: the deploy hatch in the middle, the generator and the intel
    // desk, a shooting range and a fire, the barracks corner with the stash, and an area along
    // the walls for each survivor, left abandoned until they're rescued.
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

        BuildRoom(new PlannedRoom { Shape = square, Cells = new List<Vector2Int>(HallCells), Type = RoomType.Start },
            new HashSet<(Vector2Int, Vector2Int)>());

        Room hall = rooms[0];
        hall.DisplayName = Zones.HideoutName(Zone);

        TraderStock.Restock(rng, attachments, treasureWeapons);

        // The trader's shelves are full: the cook stumbles in, bitten, and the medic takes them in.
        // Once the medic's cured them, they set up their kitchen.
        bool cookArrived = Npcs.CookDue;

        if (cookArrived)
            MetaProgress.CookArrives();

        bool cookCured = MetaProgress.IsRescued(NpcKind.Cook) && !Npcs.CookSick && !MetaProgress.CookCureSeen;

        if (cookCured)
            MetaProgress.SeeCookCure();

        BuildHall(hall);
        BuildBarracksCorner(hall);

        foreach (HideoutArea area in Areas)
            BuildArea(area, hall);

        // Goals finished and new ones begun since the last visit, one after another.
        List<(NpcKind who, string line)> scenes = Npcs.DueScenes();

        if (scenes.Count > 0)
            StartCoroutine(HideoutScenes(scenes, cookArrived || cookCured ? 8f : 0f));

        if (cookArrived)
            StartCoroutine(HideoutRadio(Npcs.Name(NpcKind.Medic),
                "ROSA, A COOK FROM THE MALL, JUST STUMBLED IN. BITTEN. I CAN KEEP THEM STABLE, BUT A CURE MEANS SUPPLIES FROM THE HOSPITAL."));
        else if (cookCured)
            StartCoroutine(HideoutRadio(Npcs.Name(NpcKind.Cook), Npcs.Freed(NpcKind.Cook)));

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

        BuildDeployHatch(hall, centre + new Vector3(0f, 0f, 2.5f));
        BuildGeneratorCorner(parent, centre + new Vector3(-4.5f, 0f, HallHalf.y - 1f));

        // The intel desk east of the hatch: a planning table with a lit map and the radio (the
        // Recon upgrade), on a rug.
        Vector3 table = centre + new Vector3(8f, 0f, 3f);
        StationConsole.Create(HideoutStation.IntelDesk, table, Vector3.back, parent);

        BuildShootingRange(parent, centre);

        // Whatever the zone's hideout has: the north wall's doors and windows, seats either side
        // of the walk to the hatch, things piled along the south wall.
        DressHideout(hall);

        // Four pillars framing the middle of the hall round the hatch.
        foreach (Vector2 corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            HideoutPillar(hall, centre + new Vector3(corner.x * 7f, 0f, corner.y > 0 ? 7f : -3.5f));

        BuildFireCorner(parent, centre + new Vector3(-9f, 0f, -5.5f));
        BuildRug(parent, table);
        BuildTruckBay(hall);
        SpawnTrash(hall);
    }

    // The hatch down to floor 1 (labelled by its FloorExit, "[E] DEPLOY"), with hazard posts and
    // a low rail round it (gaps front and back to walk in) under a green spot light.
    private void BuildDeployHatch(Room hall, Vector3 hatchAt)
    {
        Transform parent = hall.transform;
        SpawnExit(hall, FloorExit.Kind.NextFloor);
        Transform hatch = parent.Find("Hatch");

        if (hatch != null)
            hatch.position = hatchAt;

        Color yellow = new Color(0.85f, 0.7f, 0.1f);
        Color dark = new Color(0.08f, 0.08f, 0.08f);

        foreach (Vector2 corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
        {
            Vector3 post = hatchAt + new Vector3(corner.x * 1.9f, 0f, corner.y * 1.9f);
            GameObject postProp = HideoutProp("Post", parent, post + Vector3.up * 0.5f, new Vector3(0.18f, 1f, 0.18f), yellow, yellow * 0.15f, true);

            // The sprite has the stripes painted on.
            if (FurnitureSprite.Apply(postProp, "HatchPost", Vector3.back, new Vector3(0.18f, 1f, 0.18f), post) != null)
                continue;

            HideoutProp("Post Stripe", parent, post + Vector3.up * 0.75f, new Vector3(0.2f, 0.12f, 0.2f), dark, Color.black, false);
            HideoutProp("Post Stripe", parent, post + Vector3.up * 0.35f, new Vector3(0.2f, 0.12f, 0.2f), dark, Color.black, false);
        }

        foreach (float x in new[] { -1.9f, 1.9f })
        {
            GameObject rail = HideoutProp("Rail", parent, hatchAt + new Vector3(x, 0.7f, 0f), new Vector3(0.08f, 0.08f, 3.8f), yellow, Color.black, false);

            // As sprites, in short lengths: one long upright sprite would be drawn over anyone
            // standing beside its far end.
            if (!FurnitureSprite.Has("Rail"))
                continue;

            for (int i = 0; i < 4; i++)
            {
                GameObject length = new GameObject("Rail Length");
                length.transform.SetParent(rail.transform, false);
                Vector3 at = hatchAt + new Vector3(x, 0.66f, -1.425f + i * 0.95f);
                length.transform.position = at;
                FurnitureSprite.Apply(length, "Rail", Vector3.back, new Vector3(0.08f, 0.08f, 0.95f), at);
            }

            rail.GetComponent<Renderer>().enabled = false;
        }

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
    }

    // The generator, against the north wall between the workshop and the hatch, with fuel drums.
    private void BuildGeneratorCorner(Transform parent, Vector3 generator)
    {
        StationConsole.Create(HideoutStation.Generator, generator, Vector3.back, parent);

        Color drum = new Color(0.45f, 0.12f, 0.08f);
        Vector3 drumSize = new Vector3(0.6f, 1f, 0.6f);
        Furniture("DrumRed", "Drum", parent, generator + new Vector3(2.1f, 0f, 0.2f), new Vector3(-0.4f, 0f, -1f), drumSize, drum, true, PrimitiveType.Cylinder);
        Furniture("DrumRed", "Drum", parent, generator + new Vector3(2.5f, 0f, -0.6f), new Vector3(1f, 0f, -0.6f), drumSize, drum, true, PrimitiveType.Cylinder);
        Furniture("DrumBlue", "Drum", parent, generator + new Vector3(3.0f, 0f, 0.1f), Vector3.back, drumSize, new Color(0.2f, 0.25f, 0.3f), true, PrimitiveType.Cylinder);
    }

    // A shooting range down the east side: a training dummy, sandbags behind it, and a
    // painted firing line to stand at (guns don't use ammo in the hideout).
    private void BuildShootingRange(Transform parent, Vector3 centre)
    {
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
    }

    // A burning barrel with crates round it to sit on, west of the walk in: the hub's warm corner.
    private void BuildFireCorner(Transform parent, Vector3 fire)
    {
        Color crate = new Color(0.3f, 0.33f, 0.2f);
        GameObject barrel = Furniture("FireBarrel", "Fire Barrel", parent, fire, Vector3.back, new Vector3(0.6f, 0.9f, 0.6f), new Color(0.2f, 0.12f, 0.08f), true, PrimitiveType.Cylinder);
        Renderer flame = HideoutProp("Flame", parent, fire + Vector3.up * 0.95f, new Vector3(0.5f, 0.24f, 0.5f), new Color(1f, 0.5f, 0.15f), new Color(1f, 0.45f, 0.1f) * 2f, false, PrimitiveType.Cylinder).GetComponent<Renderer>();

        // The barrel's sprite has its flames drawn in.
        if (barrel.GetComponent<FurnitureSprite>() != null)
            flame.enabled = false;

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
    }

    // A rug under the planning table, as a flat sprite when there is one.
    private void BuildRug(Transform parent, Vector3 table)
    {
        GameObject rug = HideoutProp("Rug", parent, table + Vector3.up * 0.01f, new Vector3(4f, 0.02f, 2.6f), new Color(0.35f, 0.12f, 0.1f), Color.black, false);
        SpriteVisuals.FurnitureEntry rugSprite = SpriteVisuals.Current != null ? SpriteVisuals.Current.ForFurniture("Rug") : null;

        // On an unscaled holder: the block is squashed flat, which would stretch a sprite under it.
        if (rugSprite != null)
        {
            GameObject flat = new GameObject("Rug Sprite");
            flat.transform.SetParent(parent, false);
            flat.transform.position = rug.transform.position;
            ItemSprite.ApplyFlat(flat, rugSprite.sheet, Vector2Int.zero, 4f);
            rug.GetComponent<Renderer>().enabled = false;
        }
    }

    // The truck bay in the middle of the south wall, behind the spawn: the military truck the mechanic is
    // putting back together, side on, nose to the east. It's drawn as the wreck until parts come
    // home (one per won run), then in stages: parts in the engine bay, its tyres back on, and
    // running, headlights lit. Its label lists the parts fitted and missing.
    private void BuildTruckBay(Room hall)
    {
        Vector3 size = new Vector3(2.6f, 2.9f, 7.7f);
        Vector3 at = HallCentre + new Vector3(0.5f, 0f, -HallHalf.y + size.x / 2f + 0.6f);
        GameObject truck = HideoutProp("Truck", hall.transform, at + Vector3.up * size.y / 2f, RotateSize(Quaternion.LookRotation(Vector3.right), size),
            new Color(0.34f, 0.38f, 0.23f), Color.black, true);

        int parts = StoryItems.TruckPartsRecovered;
        int stage = parts >= 5 ? 3 : parts >= 4 ? 2 : parts >= 1 ? 1 : 0;
        FurnitureSprite sprite = FurnitureSprite.Apply(truck, "Truck", Vector3.right, size, at);

        if (sprite != null)
            sprite.Column = stage;

        // A work lamp hung over the bay.
        Light lamp = new GameObject("Truck Bay Lamp").AddComponent<Light>();
        lamp.transform.SetParent(hall.transform, false);
        lamp.transform.position = at + new Vector3(0f, 4.5f, 1.5f);
        lamp.transform.rotation = Quaternion.LookRotation(at + Vector3.up - lamp.transform.position);
        lamp.type = LightType.Spot;
        lamp.spotAngle = 100f;
        lamp.range = 9f;
        lamp.intensity = 7f;
        lamp.color = new Color(1f, 0.88f, 0.65f);
        lamp.shadows = LightShadows.None;

        // Running: its headlights throw light across the floor ahead of it.
        if (stage == 3)
        {
            Light beam = new GameObject("Headlights").AddComponent<Light>();
            beam.transform.SetParent(truck.transform, true);
            beam.transform.position = at + new Vector3(size.z / 2f + 0.2f, 1.3f, 0f);
            beam.transform.rotation = Quaternion.LookRotation(new Vector3(1f, -0.35f, 0f));
            beam.type = LightType.Spot;
            beam.spotAngle = 70f;
            beam.range = 9f;
            beam.intensity = 6f;
            beam.color = new Color(1f, 0.93f, 0.7f);
            beam.shadows = LightShadows.None;
        }

        GameObject label = new GameObject("Truck Bay");
        label.transform.SetParent(hall.transform, false);
        label.transform.position = at + Vector3.forward * (size.x / 2f + 0.3f);
        ProximityLabel.Add(label, 2.4f, 4.5f, StoryItems.Colour(StoryItem.Battery), TruckText);
        StationMarker.Add(label, HideoutStation.TruckBay, false);
        TruckBay.Create(label, truck.transform);

        // Once rescued, the mechanic works on it, at its tail end (clear of the way it drives
        // out, and not just north of it, where the tall truck would hide them).
        if (MetaProgress.IsRescued(NpcKind.Mechanic))
            HideoutNpc.Create(NpcKind.Mechanic, at + new Vector3(-size.z / 2f - 0.9f, 0f, 0.8f), Vector3.right, hall.transform);
    }

    // Off to a zone in the truck (from its zone map): it drives away, then that zone's hideout is
    // built behind the transition (TruckBay has already made it the chosen zone).
    public void DriveTo(Zone zone)
    {
        TruckBay bay = floorRoot != null ? floorRoot.GetComponentInChildren<TruckBay>() : null;

        if (IsHideout && bay != null)
            bay.DriveTo(zone, transition, ArriveAtHideout);
    }

    // Runs while the transition screen is fully black: the hideout again, in the new zone.
    private void ArriveAtHideout()
    {
        ClearLooseObjects();
        Generate();

        if (cameraFollow != null)
            cameraFollow.SnapToTarget();
    }

    private static string TruckText()
    {
        NpcKind mechanic = NpcKind.Mechanic;
        string header = $"MILITARY TRUCK  <size=70%><alpha=#99>// {StoryItems.Progress(mechanic)}</size>\n<size=65%>";

        foreach (StoryItem part in StoryItems.Goal(mechanic))
            header += MetaProgress.HasStoryItem(part) ? $"<color={StoryItems.Hex(part)}>{StoryItems.Name(part)}</color>  " : $"<alpha=#55>{StoryItems.Name(part)}<alpha=#FF>  ";

        string zoneMap = $"\n<size=80%><color=#FFFFFF>{InputHint.Format("[{Interact}]")} ZONE MAP</color></size>";

        if (StoryItems.Complete(mechanic))
            return header + $"\n<color={UiColours.AmberHex}>RUNNING // READY TO DRIVE</color></size>" + zoneMap;

        return header + "\n<alpha=#99>ONE PART COMES HOME WITH EACH COMPLETED RUN // FROM THE SUPPLY ROOM UNDER THE LAST FLOOR</size>" + zoneMap;
    }
}
