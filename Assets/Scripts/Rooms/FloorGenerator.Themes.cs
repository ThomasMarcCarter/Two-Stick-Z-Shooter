using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Room themes: what a room was before the outbreak, told by the furniture against its walls.
// Most combat rooms get one (at the Military Base: storage, infirmary, barracks or workshop; at
// the Shopping Mall: shop floor, food court or storeroom; at the Hospital: ward, operating
// theatre or waiting room; at the Airport: gate lounge, baggage hall or security check), the
// treasure room is an armoury (the mall's security office, the hospital's pharmacy, the airport's
// duty free) and the spawn room a checkpoint (the mall's entrance, the hospital's reception, the
// airport's check-in); the boss arena stays bare. Each piece stands with its
// back to an outer wall, clear of doorways, pillars, wall loot points, the middle of each cell
// and the routes in from the doors. Furniture goes in after the navmesh is built, so each piece
// carves its own hole in it; it's solid cover and counts as clutter (boxes, enemies and crates
// keep off it). The theme shows on the tactical map's room data ("COMBAT // STORAGE"). Needs
// the FurnitureVisuals models (the mall's pieces are sprites only, with a box for a shadow);
// without them rooms just aren't themed.
public partial class FloorGenerator
{
    private enum RoomTheme
    {
        None,
        Storage,
        Infirmary,
        Barracks,
        Workshop,
        Armoury,
        Checkpoint,
        ShopFloor,
        FoodCourt,
        StoreRoom,
        SecurityOffice,
        Entrance,
        Ward,
        Theatre,
        WaitingRoom,
        Pharmacy,
        Reception,
        GateLounge,
        BaggageHall,
        SecurityCheck,
        DutyFree,
        CheckIn
    }

    // A piece of furniture a theme can place: the model and the space it takes (width along the
    // wall, height, depth out from it).
    private readonly struct ThemePiece
    {
        public readonly string Model;
        public readonly Vector3 Size;

        public ThemePiece(string model, float width, float height, float depth)
        {
            Model = model;
            Size = new Vector3(width, height, depth);
        }
    }

    private static readonly ThemePiece CratePiece = new ThemePiece("Crate", 1f, 0.9f, 1f);
    private static readonly ThemePiece DrumRedPiece = new ThemePiece("DrumRed", 0.6f, 1f, 0.6f);
    private static readonly ThemePiece DrumBluePiece = new ThemePiece("DrumBlue", 0.6f, 1f, 0.6f);
    private static readonly ThemePiece ShelvingPiece = new ThemePiece("Shelving", 2.4f, 2f, 0.6f);
    private static readonly ThemePiece AmmoCratePiece = new ThemePiece("AmmoCrate", 1.1f, 0.6f, 0.7f);
    private static readonly ThemePiece HospitalBedPiece = new ThemePiece("HospitalBed", 0.9f, 0.88f, 2f);
    private static readonly ThemePiece MedCabinetPiece = new ThemePiece("MedCabinet", 1.2f, 1.2f, 0.5f);
    private static readonly ThemePiece ScreenPiece = new ThemePiece("Screen", 2.2f, 2f, 0.4f);
    private static readonly ThemePiece BunkBedPiece = new ThemePiece("BunkBed", 2f, 1.9f, 0.9f);
    private static readonly ThemePiece LockerPiece = new ThemePiece("Locker", 0.7f, 2f, 0.6f);
    private static readonly ThemePiece TyresPiece = new ThemePiece("Tyres", 0.9f, 0.55f, 0.9f);
    private static readonly ThemePiece BenchPiece = new ThemePiece("Bench", 2.4f, 0.5f, 0.5f);
    private static readonly ThemePiece GunRackPiece = new ThemePiece("GunRack", 2.6f, 2f, 0.3f);
    private static readonly ThemePiece SandbagsPiece = new ThemePiece("Sandbags", 3f, 0.7f, 0.8f);
    private static readonly ThemePiece CounterPiece = new ThemePiece("Counter", 2.4f, 1.15f, 0.7f);

    // The Shopping Mall's (sprite only).
    private static readonly ThemePiece ClothesRackPiece = new ThemePiece("ClothesRack", 1.6f, 1.5f, 0.6f);
    private static readonly ThemePiece DisplayTablePiece = new ThemePiece("DisplayTable", 1.6f, 0.8f, 0.9f);
    private static readonly ThemePiece StoreShelfPiece = new ThemePiece("StoreShelf", 2.4f, 1.8f, 0.6f);
    private static readonly ThemePiece CheckoutPiece = new ThemePiece("Checkout", 2.2f, 1.1f, 0.8f);
    private static readonly ThemePiece FoodTablePiece = new ThemePiece("FoodTable", 1.7f, 0.8f, 0.8f);
    private static readonly ThemePiece FoodCounterPiece = new ThemePiece("FoodCounter", 2.6f, 1.2f, 0.8f);
    private static readonly ThemePiece TrashBinPiece = new ThemePiece("TrashBin", 0.6f, 1f, 0.6f);
    private static readonly ThemePiece PlanterPiece = new ThemePiece("Planter", 1.2f, 0.9f, 1.2f);
    private static readonly ThemePiece PalletPiece = new ThemePiece("Pallet", 1.2f, 1.2f, 1f);

    // The Hospital's (sprite only).
    private static readonly ThemePiece IVStandPiece = new ThemePiece("IVStand", 0.5f, 1.9f, 0.5f);
    private static readonly ThemePiece OperatingTablePiece = new ThemePiece("OperatingTable", 0.9f, 1f, 2f);
    private static readonly ThemePiece SurgicalLampPiece = new ThemePiece("SurgicalLamp", 0.7f, 1.9f, 0.9f);
    private static readonly ThemePiece TrolleyPiece = new ThemePiece("Trolley", 0.9f, 0.9f, 0.5f);
    private static readonly ThemePiece PharmacyShelfPiece = new ThemePiece("PharmacyShelf", 2.2f, 1.9f, 0.5f);
    private static readonly ThemePiece VendingMachinePiece = new ThemePiece("VendingMachine", 0.9f, 1.9f, 0.8f);
    private static readonly ThemePiece ReceptionDeskPiece = new ThemePiece("ReceptionDesk", 2.4f, 1.1f, 0.8f);
    private static readonly ThemePiece WheelchairPiece = new ThemePiece("Wheelchair", 0.7f, 1.1f, 0.6f);

    // The Airport's (sprite only).
    private static readonly ThemePiece GateSeatsPiece = new ThemePiece("GateSeats", 2.3f, 1f, 0.6f);
    private static readonly ThemePiece DepartureBoardPiece = new ThemePiece("DepartureBoard", 2.4f, 2.2f, 0.3f);
    private static readonly ThemePiece BaggageCarouselPiece = new ThemePiece("BaggageCarousel", 3f, 0.9f, 1.2f);
    private static readonly ThemePiece LuggagePiece = new ThemePiece("Luggage", 1f, 0.9f, 0.6f);
    private static readonly ThemePiece XRayScannerPiece = new ThemePiece("XRayScanner", 2.2f, 1.6f, 0.9f);
    private static readonly ThemePiece MetalDetectorPiece = new ThemePiece("MetalDetector", 1.2f, 2.3f, 0.6f);
    private static readonly ThemePiece CheckInDeskPiece = new ThemePiece("CheckInDesk", 2.2f, 1.3f, 0.8f);

    // What each theme is furnished with (picked from at random, so repeats are likely).
    private static ThemePiece[] PiecesFor(RoomTheme theme)
    {
        switch (theme)
        {
            case RoomTheme.Storage: return new[] { CratePiece, CratePiece, DrumRedPiece, DrumBluePiece, ShelvingPiece, AmmoCratePiece };
            case RoomTheme.Infirmary: return new[] { HospitalBedPiece, HospitalBedPiece, MedCabinetPiece, ScreenPiece };
            case RoomTheme.Barracks: return new[] { BunkBedPiece, BunkBedPiece, LockerPiece, LockerPiece };
            case RoomTheme.Workshop: return new[] { ShelvingPiece, TyresPiece, DrumRedPiece, BenchPiece, CratePiece };
            case RoomTheme.Armoury: return new[] { GunRackPiece, AmmoCratePiece, AmmoCratePiece, CratePiece };
            case RoomTheme.Checkpoint: return new[] { SandbagsPiece, BenchPiece, DrumBluePiece };
            case RoomTheme.ShopFloor: return new[] { ClothesRackPiece, ClothesRackPiece, DisplayTablePiece, StoreShelfPiece, CheckoutPiece };
            case RoomTheme.FoodCourt: return new[] { FoodTablePiece, FoodTablePiece, FoodTablePiece, FoodCounterPiece, TrashBinPiece, PlanterPiece };
            case RoomTheme.StoreRoom: return new[] { PalletPiece, PalletPiece, StoreShelfPiece, ShelvingPiece, CratePiece };
            case RoomTheme.SecurityOffice: return new[] { LockerPiece, LockerPiece, CounterPiece, GunRackPiece };
            case RoomTheme.Entrance: return new[] { PlanterPiece, BenchPiece, TrashBinPiece };
            case RoomTheme.Ward: return new[] { HospitalBedPiece, HospitalBedPiece, HospitalBedPiece, IVStandPiece, MedCabinetPiece };
            case RoomTheme.Theatre: return new[] { OperatingTablePiece, SurgicalLampPiece, TrolleyPiece, MedCabinetPiece, IVStandPiece };
            case RoomTheme.WaitingRoom: return new[] { BenchPiece, BenchPiece, VendingMachinePiece, PlanterPiece, WheelchairPiece, TrashBinPiece };
            case RoomTheme.Pharmacy: return new[] { PharmacyShelfPiece, PharmacyShelfPiece, ReceptionDeskPiece, MedCabinetPiece };
            case RoomTheme.Reception: return new[] { ReceptionDeskPiece, BenchPiece, PlanterPiece, WheelchairPiece };
            case RoomTheme.GateLounge: return new[] { GateSeatsPiece, GateSeatsPiece, GateSeatsPiece, DepartureBoardPiece, PlanterPiece, TrashBinPiece };
            case RoomTheme.BaggageHall: return new[] { BaggageCarouselPiece, BaggageCarouselPiece, LuggagePiece, LuggagePiece, CratePiece };
            case RoomTheme.SecurityCheck: return new[] { XRayScannerPiece, MetalDetectorPiece, LuggagePiece, TrashBinPiece };
            case RoomTheme.DutyFree: return new[] { StoreShelfPiece, StoreShelfPiece, CheckoutPiece, PlanterPiece };
            case RoomTheme.CheckIn: return new[] { CheckInDeskPiece, CheckInDeskPiece, LuggagePiece, GateSeatsPiece };
            default: return new ThemePiece[0];
        }
    }

    private static string ThemeName(RoomTheme theme)
    {
        switch (theme)
        {
            case RoomTheme.ShopFloor: return "SHOP FLOOR";
            case RoomTheme.FoodCourt: return "FOOD COURT";
            case RoomTheme.StoreRoom: return "STOREROOM";
            case RoomTheme.SecurityOffice: return "SECURITY OFFICE";
            case RoomTheme.Theatre: return "OPERATING THEATRE";
            case RoomTheme.WaitingRoom: return "WAITING ROOM";
            case RoomTheme.GateLounge: return "GATE LOUNGE";
            case RoomTheme.BaggageHall: return "BAGGAGE HALL";
            case RoomTheme.SecurityCheck: return "SECURITY CHECK";
            case RoomTheme.DutyFree: return "DUTY FREE";
            case RoomTheme.CheckIn: return "CHECK-IN";
            default: return theme.ToString().ToUpperInvariant();
        }
    }

    private static string TypeLabel(RoomType type)
    {
        switch (type)
        {
            case RoomType.Start: return "SPAWN";
            case RoomType.Treasure: return "TREASURE";
            case RoomType.Boss: return "BOSS";
            default: return "COMBAT";
        }
    }

    private RoomTheme PickTheme(Room room)
    {
        bool mall = Zone == Zone.ShoppingMall;
        bool hospital = Zone == Zone.Hospital;
        bool airport = Zone == Zone.Airport;

        switch (room.Type)
        {
            case RoomType.Start: return mall ? RoomTheme.Entrance : hospital ? RoomTheme.Reception : airport ? RoomTheme.CheckIn : RoomTheme.Checkpoint;
            case RoomType.Treasure: return mall ? RoomTheme.SecurityOffice : hospital ? RoomTheme.Pharmacy : airport ? RoomTheme.DutyFree : RoomTheme.Armoury;
            case RoomType.Boss: return RoomTheme.None;
        }

        if (rng.NextDouble() < roomThemeChance)
        {
            RoomTheme[] themes = CombatThemes(Zone);
            return themes[rng.Next(themes.Length)];
        }

        return RoomTheme.None;
    }

    // The themes a zone's combat rooms can have.
    private static RoomTheme[] CombatThemes(Zone zone)
    {
        if (zone == Zone.Airport)
            return new[] { RoomTheme.GateLounge, RoomTheme.GateLounge, RoomTheme.BaggageHall, RoomTheme.SecurityCheck };

        if (zone == Zone.ShoppingMall)
            return new[] { RoomTheme.ShopFloor, RoomTheme.ShopFloor, RoomTheme.FoodCourt, RoomTheme.StoreRoom };

        if (zone == Zone.Hospital)
            return new[] { RoomTheme.Ward, RoomTheme.Ward, RoomTheme.Theatre, RoomTheme.WaitingRoom };

        return new[] { RoomTheme.Storage, RoomTheme.Infirmary, RoomTheme.Barracks, RoomTheme.Workshop };
    }

    // Picks the room's theme, names it and stands its furniture against the walls.
    private void FurnishRoom(Room room)
    {
        if (furnitureVisuals == null)
            return;

        RoomTheme theme = PickTheme(room);
        ThemePiece[] pieces = PiecesFor(theme);

        if (pieces.Length == 0)
            return;

        room.DisplayName = $"{TypeLabel(room.Type)} // {ThemeName(theme)}";

        List<Vector3> doorways = DoorwayPositions(room);
        List<Vector3> lootPoints = new List<Vector3>();

        foreach (LootSpawnPoint point in room.GetComponentsInChildren<LootSpawnPoint>())
            lootPoints.Add(point.transform.position);

        if (!clutter.TryGetValue(room, out List<Vector3> taken))
        {
            taken = new List<Vector3>();
            clutter[room] = taken;
        }

        Vector2Int count = room.Type == RoomType.Start ? new Vector2Int(1, 2) : furniturePerCell;

        foreach (Vector2Int cell in room.Cells)
        {
            Vector3 centre = CellToWorld(cell);
            List<Vector2Int> walls = new List<Vector2Int>();

            foreach (Vector2Int side in Directions)
            {
                if (!room.Contains(cell + side))
                    walls.Add(side);
            }

            if (walls.Count == 0)
                continue;

            int wanted = rng.Next(count.x, count.y + 1);

            for (int attempt = 0; attempt < wanted * 4 && wanted > 0; attempt++)
            {
                ThemePiece piece = pieces[rng.Next(pieces.Length)];
                Vector2Int wall = walls[rng.Next(walls.Count)];

                if (TryPlaceFurniture(room, piece, centre, wall, doorways, lootPoints, taken))
                    wanted--;
            }
        }
    }

    // One piece, back to the given wall of a cell, somewhere along it that's clear.
    private bool TryPlaceFurniture(Room room, ThemePiece piece, Vector3 centre, Vector2Int wall, List<Vector3> doorways,
        List<Vector3> lootPoints, List<Vector3> taken)
    {
        if (!HasPropModel(piece.Model))
            return false;

        Vector3 outward = new Vector3(wall.x, 0f, wall.y);
        Vector3 along = new Vector3(wall.y, 0f, wall.x);
        float halfExtent = wall.x != 0 ? roomSize.x / 2f : roomSize.y / 2f;
        float halfLength = wall.x != 0 ? roomSize.y / 2f : roomSize.x / 2f;
        float range = halfLength - piece.Size.x / 2f - 0.4f;

        if (range <= 0f)
            return false;

        Vector3 spot = centre + outward * (halfExtent - piece.Size.z / 2f - 0.15f) + along * RandomRange(-range, range);

        // The footprint as points about a metre apart, so long pieces are kept clear along
        // their whole length (and enemies, crates and boxes keep off all of it).
        List<Vector3> footprint = new List<Vector3>();
        int across = Mathf.Max(1, Mathf.CeilToInt(piece.Size.x));
        int deep = Mathf.Max(1, Mathf.CeilToInt(piece.Size.z));

        for (int i = 0; i < across; i++)
        {
            for (int j = 0; j < deep; j++)
            {
                footprint.Add(spot + along * ((i + 0.5f) / across - 0.5f) * piece.Size.x
                    + outward * ((j + 0.5f) / deep - 0.5f) * piece.Size.z);
            }
        }

        foreach (Vector3 point in footprint)
        {
            if (!IsClear(point, doorways, 3f) || !IsClear(point, PillarsOf(room), 1.6f) || !IsClear(point, lootPoints, 1.4f)
                || !IsClear(point, taken, 1.5f) || Vector3.Distance(Flat(point), Flat(centre)) < 2.8f
                || NearDoorRoute(point, centre, doorways, 1.5f))
            {
                return false;
            }
        }

        GameObject furniture = PropModel(piece.Model, piece.Model, room.transform, spot, -outward, piece.Size);

        if (furniture == null)
            return false;

        // Zombies path round it (the navmesh was baked before it went in).
        NavMeshObstacle obstacle = furniture.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.up * piece.Size.y / 2f;
        obstacle.size = piece.Size;
        obstacle.carving = true;

        // A boss's charge or acid wrecks it.
        furniture.AddComponent<Smashable>();

        taken.AddRange(footprint);
        return true;
    }
}
