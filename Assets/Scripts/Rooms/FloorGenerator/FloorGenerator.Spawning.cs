using System;
using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's enemy side: placing a combat room's zombies (kind odds per floor, kept
// apart and away from the doors), the boss and the minions it calls, building each enemy's
// body and wiring up its death (drops, materials, room clear), and the ammo crates and ammo
// drops that keep the player supplied.
public partial class FloorGenerator
{
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

        if (IsTutorial)
            room.Cleared += OnTutorialRoomCleared;
        else
            room.Cleared += OnCombatRoomCleared;

        // New Game+ adds one more per loop (MetaProgress.Loop).
        int count = IsTutorial ? TutorialZombies : rng.Next(minEnemiesPerRoom, maxEnemiesPerRoom + 1) + extraEnemiesPerCell * (room.Cells.Count - 1) + MetaProgress.Loop
            + (PlayerItems.Has(RunItem.DinnerBell) ? DinnerBellZombies : 0);
        List<Vector3> doorways = DoorwayPositions(room);
        Dictionary<EnemyKind, int> kindsPlaced = new Dictionary<EnemyKind, int>();

        for (int i = 0; i < count; i++)
        {
            if (TryFindSpawnPoint(room, doorways, placed, out Vector3 position))
            {
                placed.Add(position);
                EnemyKind kind = IsTutorial ? EnemyKind.Walker : PickEnemyKind((float)rng.NextDouble(), kindsPlaced);
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

    // The floor's boss (a different one each floor, per zone: Zones.Boss), in the middle of its room,
    // scaled from the boss health and speed set here.
    private void SpawnBoss(Room room)
    {
        BossKind kind = Zones.Boss(Zone, FloorNumber);
        int health = Mathf.Max(1, Mathf.RoundToInt(bossHealth * Bosses.HealthMultiplier(kind)));
        float speed = bossSpeed * Bosses.SpeedMultiplier(kind);
        Enemy boss = SpawnEnemy(room, room.transform.position, Bosses.Size(kind), health, speed, bossContactDamage, "Boss", EnemyKind.Walker);
        boss.Heavy = true;
        SpriteSheet bossSprite = SpriteVisuals.Current != null ? SpriteVisuals.Current.For(kind) : null;

        if (bossSprite != null)
            CharacterVisuals.AttachSpriteToEnemy(boss, bossSprite);
        else if (CharacterVisuals.Current != null)
            CharacterVisuals.AttachToEnemy(boss, CharacterVisuals.Current.For(kind), Bosses.AnimationStyle(kind));

        Bosses.Equip(boss, kind, (position, minionKind) => SpawnMinion(room, position, minionKind));
        PaintBossArena(room);
    }

    // A big worn ring painted on the floor round where the boss starts, so the boss room reads as
    // an arena from above.
    private void PaintBossArena(Room room)
    {
        if (bossArenaMaterial == null)
            return;

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ring.name = "Boss Arena";
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(room.transform, false);
        ring.transform.position = new Vector3(room.transform.position.x, 0.013f, room.transform.position.z);
        ring.transform.rotation = Quaternion.Euler(90f, (float)rng.NextDouble() * 360f, 0f);
        float size = Mathf.Min(roomSize.x, roomSize.y) * 0.85f;
        ring.transform.localScale = new Vector3(size, size, 1f);

        Renderer renderer = ring.GetComponent<Renderer>();
        renderer.sharedMaterial = bossArenaMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // A zombie a boss calls in mid-fight. Capped, so a long fight doesn't flood the room.
    private Enemy SpawnMinion(Room room, Vector3 position, EnemyKind kind)
    {
        if (room.Enemies.Count >= Bosses.MaxAliveInFight)
            return null;

        return SpawnNormalEnemy(room, position, kind);
    }

    // Weighted pick among the kinds allowed on this floor that aren't at their room limit.
    private EnemyKind PickEnemyKind(float roll, Dictionary<EnemyKind, int> placed)
    {
        // Bullet-hell kinds: one of each and two in all per room on floor 1, three on floor 2, four below.
        int ranged = 0;

        foreach (KeyValuePair<EnemyKind, int> pair in placed)
        {
            if (EnemyKinds.IsRanged(pair.Key))
                ranged += pair.Value;
        }

        int rangedCap = Mathf.Min(4, FloorNumber + 1);

        bool Allowed(EnemyKindOdds odds)
        {
            if (odds.weight <= 0f || FloorNumber < odds.minFloor)
                return false;

            int limit = odds.maxPerRoom;

            if (EnemyKinds.IsRanged(odds.kind))
            {
                if (ranged >= rangedCap)
                    return false;

                if (FloorNumber == 1)
                    limit = 1;
            }

            return limit <= 0 || !placed.TryGetValue(odds.kind, out int count) || count < limit;
        }

        float total = 0f;

        foreach (EnemyKindOdds odds in ZoneEnemyKinds)
        {
            if (Allowed(odds))
                total += odds.weight;
        }

        if (total <= 0f)
            return EnemyKind.Walker;

        float target = roll * total;

        foreach (EnemyKindOdds odds in ZoneEnemyKinds)
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
        if (room.Type != RoomType.Normal || ammoMaterial == null || rng.NextDouble() >= Drops.ammoCrateChance)
            return;

        if (!TryFindSpawnPoint(room, DoorwayPositions(room), enemies, out Vector3 position))
            return;

        AmmoType type = PickAmmoType((float)rng.NextDouble(), (float)rng.NextDouble());
        AmmoPickup.Spawn(type, DropAmount(type, Drops.crateMultiplier, (float)rng.NextDouble()), position, room.transform, ammoMaterial, true);
    }

    private const float GunnerAmmoChance = 0.25f;

    // A dead enemy sometimes leaves ammo (a boss's goes in its supply crate instead).
    private void DropAmmo(Room room, Enemy enemy)
    {
        // A gunner sometimes leaves a little for the gun it was holding.
        if (ammoMaterial != null && enemy.Kind == EnemyKind.Gunner && UnityEngine.Random.value < GunnerAmmoChance)
        {
            AmmoType held = EnemyGunner.AmmoFor(Zone);
            AmmoPickup.Spawn(held, DropAmount(held, 0.6f, UnityEngine.Random.value), InsideRoom(room, enemy.transform.position), room.transform, ammoMaterial, false);
            return;
        }

        if (ammoMaterial == null || UnityEngine.Random.value >= EnemyAmmoDropChance)
            return;

        AmmoType type = PickAmmoType(UnityEngine.Random.value, UnityEngine.Random.value);
        AmmoPickup.Spawn(type, DropAmount(type, 1f, UnityEngine.Random.value), InsideRoom(room, enemy.transform.position), room.transform, ammoMaterial, false);
    }

    // Usually ammo for a gun the player is carrying, so drops are useful; sometimes any type.
    private AmmoType PickAmmoType(float biasRoll, float pickRoll)
    {
        List<AmmoType> held = HeldAmmoTypes();
        List<AmmoType> pool = held.Count > 0 && biasRoll < Drops.heldGunAmmoBias ? held : new List<AmmoType>(AmmoInfo.All);
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

        // New Game+ loops make everything tougher and hit harder (MetaProgress.Toughness).
        float tough = IsHideout ? 1f : MetaProgress.Toughness;
        Enemy enemy = enemyObject.AddComponent<Enemy>();
        enemy.Initialise(room, player, Mathf.Max(1, Mathf.FloorToInt(health * tough + 0.5f)), speed, Mathf.FloorToInt(contactDamage * tough + 0.5f));
        enemy.SetKind(kind);

        // Bosses get their own model in SpawnBoss. Kinds with a sprite sheet are drawn as sprites
        // when the sprite art is on, dressed for the zone.
        bool boss = name == "Boss";
        SpriteSheet sprite = !boss && SpriteVisuals.Current != null ? SpriteVisuals.Current.For(kind, Zone) : null;

        if (sprite != null)
            CharacterVisuals.AttachSpriteToEnemy(enemy, sprite);
        else if (CharacterVisuals.Current != null && !boss)
            CharacterVisuals.AttachToEnemy(enemy, CharacterVisuals.Current.For(kind), CharacterVisuals.StyleFor(kind));

        if (kind == EnemyKind.Bloater)
            enemyObject.AddComponent<EnemyBloater>();
        else if (kind == EnemyKind.Screamer)
            enemyObject.AddComponent<EnemyScreamer>();
        else if (kind == EnemyKind.Pustule)
            enemyObject.AddComponent<EnemyPustule>().Floor = FloorNumber;
        else
            AddRangedAttack(enemyObject, kind);

        AddEnemySounds(enemyObject, kind, boss);
        enemy.Died += dead => OnEnemyDied(room, dead, boss);
        room.AddEnemy(enemy);
        return enemy;
    }

    // The bullet-hell kinds' attacks, told the floor (patterns get denser deeper down) and zone.
    private void AddRangedAttack(GameObject enemyObject, EnemyKind kind)
    {
        EnemyRangedAttack attack;

        switch (kind)
        {
            case EnemyKind.Spitter: attack = enemyObject.AddComponent<EnemySpitter>(); break;
            case EnemyKind.Retcher: attack = enemyObject.AddComponent<EnemyRetcher>(); break;
            case EnemyKind.Gunner: attack = enemyObject.AddComponent<EnemyGunner>(); break;
            case EnemyKind.Lobber: attack = enemyObject.AddComponent<EnemyLobber>(); break;
            case EnemyKind.Leaper: attack = enemyObject.AddComponent<EnemyLeaper>(); break;
            default: return;
        }

        attack.Floor = FloorNumber;
        attack.Zone = Zone;
    }

    // Footsteps (heavier, lower steps than the player's, heard from where the enemy is; the boss
    // takes long, slow, booming strides) and a voice from one of the sound packs, deeper for
    // the boss.
    private void AddEnemySounds(GameObject enemyObject, EnemyKind kind, bool boss)
    {
        Footsteps steps = enemyObject.AddComponent<Footsteps>();

        if (boss)
            steps.Configure(2.2f, 0.55f, 0.25f, 0.35f, new Vector2(1f, 3f), 1f);
        else if (kind == EnemyKind.Brute)
            steps.Configure(1.8f, 0.65f, 0.12f, 0.22f, new Vector2(1f, 3f), 1f);
        else if (kind == EnemyKind.Runner)
            steps.Configure(1f, 1f, 0.05f, 0.12f, new Vector2(2f, 6f), 1f);
        else
            steps.Configure(1.2f, 0.8f, 0.06f, 0.13f, new Vector2(1.5f, 4f), 1f);

        if (enemySoundPacks != null && enemySoundPacks.Length > 0)
        {
            ZombieSoundPack pack = enemySoundPacks[UnityEngine.Random.Range(0, enemySoundPacks.Length)];

            if (pack != null)
                enemyObject.AddComponent<EnemyVoice>().Configure(pack, boss ? 0.7f : VoicePitch(kind));
        }
    }

    // An enemy is down: counted, its blood on the floor (the sprite art's pixel pool, else a
    // splat decal), and its drops; a boss leaves its supply crate instead.
    private void OnEnemyDied(Room room, Enemy dead, bool boss)
    {
        EnemiesKilled++;
        lastDeaths[room] = dead.transform.position;
        PlayerItems.EnemyKilled(dead, boss, room.transform);

        if (!ImpactEffects.FloorPool(dead.transform.position, boss ? 3f : 1.6f, room.transform)
            && bloodSplatMaterials != null && bloodSplatMaterials.Length > 0)
        {
            Material splat = bloodSplatMaterials[UnityEngine.Random.Range(0, bloodSplatMaterials.Length)];
            ImpactEffects.FloorSplat(dead.transform.position, boss ? 3f : 1.5f, room.transform, splat);
        }

        if (boss)
        {
            SpawnBossCrate(room, dead.transform.position);
            return;
        }

        DropAmmo(room, dead);
        DropMaterials(room, dead.transform.position);
        DropPlate(room, dead.transform.position);
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
            case EnemyKind.Leaper: return 1.15f;
            case EnemyKind.Retcher: return 0.85f;
            default: return 1f;
        }
    }
}
