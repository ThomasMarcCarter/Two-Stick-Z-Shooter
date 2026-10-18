using System;
using UnityEngine;

public enum BossKind
{
    Butcher,        // floor 1: winds up and charges; dazed if it hits a wall
    Broodmother,    // floor 2: lobs acid, bursts out runners as it's hurt
    Howler          // floor 3: fast, dodges, howls a shockwave that calls the horde
}

// The floor bosses: which one each floor gets, their numbers (as multiples of the FloorGenerator's
// boss health and speed), looks, and the behaviour component that makes each one different. The
// FloorGenerator spawns the body like any enemy, then calls Equip. Floors past the third cycle
// round again.
public static class Bosses
{
    public static BossKind ForFloor(int floor)
    {
        int count = Enum.GetValues(typeof(BossKind)).Length;
        return (BossKind)(((Mathf.Max(1, floor) - 1) % count + count) % count);
    }

    public static string Name(BossKind kind)
    {
        switch (kind)
        {
            case BossKind.Broodmother: return "THE BROODMOTHER";
            case BossKind.Howler: return "THE HOWLER";
            default: return "THE BUTCHER";
        }
    }

    public static float HealthMultiplier(BossKind kind)
    {
        switch (kind)
        {
            case BossKind.Broodmother: return 1.4f;
            case BossKind.Howler: return 1.3f;
            default: return 1.2f;
        }
    }

    public static float SpeedMultiplier(BossKind kind)
    {
        switch (kind)
        {
            case BossKind.Broodmother: return 0.7f;
            case BossKind.Howler: return 1.5f;
            default: return 1f;
        }
    }

    // Body size (the cube's side).
    public static float Size(BossKind kind)
    {
        switch (kind)
        {
            case BossKind.Broodmother: return 2.5f;
            case BossKind.Howler: return 1.6f;
            default: return 2.1f;
        }
    }

    public static Color Tint(BossKind kind)
    {
        switch (kind)
        {
            case BossKind.Broodmother: return new Color(0.38f, 0.52f, 0.16f);
            case BossKind.Howler: return new Color(0.45f, 0.3f, 0.6f);
            default: return new Color(0.45f, 0.14f, 0.11f);
        }
    }

    // Resting glow; each behaviour brightens it to telegraph an attack.
    public static Color Glow(BossKind kind)
    {
        switch (kind)
        {
            case BossKind.Broodmother: return new Color(0.5f, 0.9f, 0.1f) * 0.35f;
            case BossKind.Howler: return new Color(0.7f, 0.35f, 1f) * 0.35f;
            default: return new Color(1f, 0.2f, 0.1f) * 0.25f;
        }
    }

    // Adds the kind's behaviour and look to a freshly spawned boss body. spawnMinion puts a
    // zombie of a kind at a point in the boss's room (for the Broodmother and Howler).
    public static void Equip(Enemy boss, BossKind kind, Func<Vector3, EnemyKind, Enemy> spawnMinion)
    {
        boss.SetLook(Tint(kind), Glow(kind));
        boss.SightRangeMultiplier = 3f;

        switch (kind)
        {
            case BossKind.Broodmother:
                boss.gameObject.AddComponent<BossBroodmother>().Configure(spawnMinion);
                break;
            case BossKind.Howler:
                boss.gameObject.AddComponent<BossHowler>().Configure(spawnMinion);
                break;
            default:
                boss.gameObject.AddComponent<BossButcher>();
                break;
        }

        BossHealthBar.Track(boss, Name(kind), Tint(kind));
    }

    // Shared helpers for the behaviours.

    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    public static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }

    // A clear spot on the boss room's floor at least minDistance from `avoid` (the player), for
    // minions to climb out of. Falls back to beside the boss.
    public static Vector3 SpawnPointInRoom(Enemy boss, Vector3 avoid, float minDistance)
    {
        Room room = boss.Room;

        if (room != null && room.Blocks.Count > 0)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Rect block = room.Blocks[UnityEngine.Random.Range(0, room.Blocks.Count)];
                Vector3 candidate = new Vector3(
                    UnityEngine.Random.Range(block.xMin + 1.5f, block.xMax - 1.5f), 0f,
                    UnityEngine.Random.Range(block.yMin + 1.5f, block.yMax - 1.5f));

                if (FlatDistance(candidate, avoid) >= minDistance
                    && UnityEngine.AI.NavMesh.SamplePosition(candidate, out UnityEngine.AI.NavMeshHit hit, 1f, UnityEngine.AI.NavMesh.AllAreas))
                    return Flat(hit.position);
            }
        }

        Vector2 side = UnityEngine.Random.insideUnitCircle.normalized * 2f;
        return Flat(boss.transform.position) + new Vector3(side.x, 0f, side.y);
    }
}
