using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Room themes: what a room was before the outbreak, told by the furniture against its walls.
// Most combat rooms get one (storage, infirmary, barracks or workshop), the treasure room is an
// armoury and the spawn room a checkpoint; the boss arena stays bare. Each piece stands with its
// back to an outer wall, clear of doorways, pillars, wall loot points, the middle of each cell
// and the routes in from the doors. Furniture goes in after the navmesh is built, so each piece
// carves its own hole in it; it's solid cover and counts as clutter (boxes, enemies and crates
// keep off it). The theme shows on the tactical map's room data ("COMBAT // STORAGE"). Needs
// the FurnitureVisuals models; without them rooms just aren't themed.
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
        Checkpoint
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
            default: return new ThemePiece[0];
        }
    }

    private static string ThemeName(RoomTheme theme)
    {
        return theme.ToString().ToUpperInvariant();
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
        switch (room.Type)
        {
            case RoomType.Start: return RoomTheme.Checkpoint;
            case RoomType.Treasure: return RoomTheme.Armoury;
            case RoomType.Boss: return RoomTheme.None;
        }

        if (rng.NextDouble() < roomThemeChance)
        {
            RoomTheme[] themes = { RoomTheme.Storage, RoomTheme.Infirmary, RoomTheme.Barracks, RoomTheme.Workshop };
            return themes[rng.Next(themes.Length)];
        }

        return RoomTheme.None;
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
