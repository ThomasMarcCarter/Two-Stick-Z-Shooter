using System;
using UnityEngine;

// Appended only. The Military Base's three, then the Shopping Mall's, the Hospital's and the
// Airport's (Zones.Boss picks per zone).
public enum BossKind
{
    Butcher,        // base floor 1: winds up and charges; dazed if it hits a wall
    Broodmother,    // base floor 2: lobs acid, bursts out runners as it's hurt
    Howler,         // base floor 3: fast, dodges, howls a shockwave that calls the horde
    Warden,         // mall floor 1: riot shield stops shots from the front; whistles for guards
    Mannequin,      // mall floor 2: freezes while aimed at, rushes when not; appears behind you
    Glutton,        // mall floor 3: vomits lines of acid; rolls at you once hurt; bursts
    Surgeon,        // hospital floor 1: marks a line and dashes along it; chains dashes once hurt
    Gurney,         // hospital floor 2: a runaway bed that glances off walls; stops to call patients
    Hazmat,         // hospital floor 3: floods wedges with toxic gas; leaks a trail once hurt
    Handler,        // airport floor 1: hurls suitcases that burst where they land
    Captain,        // airport floor 2: calls wreckage down on discs round the player
    Marshal         // airport floor 3: keeps its distance; riot-gun blasts over a wedge
}

// What a boss is doing, for its model's animation (CharacterAnimator's boss styles).
public enum BossMove
{
    None,       // walking / stalking
    WindUp,     // Butcher about to charge
    Charge,     // Butcher charging
    Dazed,      // Butcher stunned after hitting a wall
    Sweep,      // Butcher winding up a wide cleaver sweep at a player too close to charge
    Spit,       // Broodmother swelling up to lob acid
    Howl,       // Howler rearing up to howl
    Dodge,      // Howler darting sideways
    Whistle,    // Warden blowing its whistle for guards
    Vanish,     // Mannequin twitching before it vanishes (and steps out behind the player)
    Vomit,      // Glutton rearing back to vomit acid
    Roll        // Glutton curled up and rolling
}

// Implemented by each boss behaviour so the animator can pose the model to match.
public interface IBossMoves
{
    BossMove Move { get; }

    // How far through the move it is, 0 to 1 (moves without a set length stay at 0).
    float MoveProgress { get; }
}

// The floor bosses (which one each floor of a zone gets is Zones.Boss): their numbers (as multiples of the FloorGenerator's
// boss health and speed), looks, and the behaviour component that makes each one different. The
// FloorGenerator spawns the body like any enemy, then calls Equip.
public static class Bosses
{
    public static string Name(BossKind kind)
    {
        switch (kind)
        {
            case BossKind.Broodmother: return "THE BROODMOTHER";
            case BossKind.Howler: return "THE HOWLER";
            case BossKind.Warden: return "THE WARDEN";
            case BossKind.Mannequin: return "THE MANNEQUIN";
            case BossKind.Glutton: return "THE GLUTTON";
            case BossKind.Surgeon: return "THE SURGEON";
            case BossKind.Gurney: return "THE GURNEY";
            case BossKind.Hazmat: return "THE HAZMAT";
            case BossKind.Handler: return "THE HANDLER";
            case BossKind.Captain: return "THE CAPTAIN";
            case BossKind.Marshal: return "THE MARSHAL";
            default: return "THE BUTCHER";
        }
    }

    public static float HealthMultiplier(BossKind kind)
    {
        switch (kind)
        {
            case BossKind.Broodmother: return 1.4f;
            case BossKind.Howler: return 1.6f;
            case BossKind.Warden: return 1.3f;
            case BossKind.Mannequin: return 1.3f;
            case BossKind.Glutton: return 1.9f;
            case BossKind.Surgeon: return 1.4f;
            case BossKind.Gurney: return 1.7f;
            case BossKind.Hazmat: return 2.1f;
            case BossKind.Handler: return 1.7f;
            case BossKind.Captain: return 1.9f;
            case BossKind.Marshal: return 2.3f;
            default: return 1.2f;
        }
    }

    public static float SpeedMultiplier(BossKind kind)
    {
        switch (kind)
        {
            case BossKind.Broodmother: return 0.7f;
            case BossKind.Howler: return 1.5f;
            case BossKind.Warden: return 0.85f;
            case BossKind.Mannequin: return 1.4f;
            case BossKind.Glutton: return 0.6f;
            case BossKind.Surgeon: return 1.3f;
            case BossKind.Gurney: return 1f;
            case BossKind.Hazmat: return 0.65f;
            case BossKind.Handler: return 0.9f;
            case BossKind.Captain: return 1f;
            case BossKind.Marshal: return 1f;
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
            case BossKind.Warden: return 2f;
            case BossKind.Mannequin: return 1.7f;
            case BossKind.Glutton: return 2.8f;
            case BossKind.Surgeon: return 1.7f;
            case BossKind.Gurney: return 2.4f;
            case BossKind.Hazmat: return 2.2f;
            case BossKind.Handler: return 2.2f;
            case BossKind.Captain: return 1.8f;
            case BossKind.Marshal: return 1.9f;
            default: return 2.1f;
        }
    }

    public static Color Tint(BossKind kind)
    {
        switch (kind)
        {
            case BossKind.Broodmother: return new Color(0.38f, 0.52f, 0.16f);
            case BossKind.Howler: return new Color(0.45f, 0.3f, 0.6f);
            case BossKind.Warden: return new Color(0.2f, 0.28f, 0.5f);
            case BossKind.Mannequin: return new Color(0.78f, 0.7f, 0.6f);
            case BossKind.Glutton: return new Color(0.6f, 0.5f, 0.2f);
            case BossKind.Surgeon: return new Color(0.3f, 0.55f, 0.5f);
            case BossKind.Gurney: return new Color(0.6f, 0.7f, 0.8f);
            case BossKind.Hazmat: return new Color(0.75f, 0.65f, 0.2f);
            case BossKind.Handler: return new Color(0.8f, 0.45f, 0.15f);
            case BossKind.Captain: return new Color(0.25f, 0.3f, 0.5f);
            case BossKind.Marshal: return new Color(0.3f, 0.32f, 0.36f);
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
            case BossKind.Warden: return new Color(0.3f, 0.5f, 1f) * 0.3f;
            case BossKind.Mannequin: return new Color(1f, 0.95f, 0.85f) * 0.2f;
            case BossKind.Glutton: return new Color(0.8f, 0.9f, 0.2f) * 0.3f;
            case BossKind.Surgeon: return new Color(1f, 0.25f, 0.2f) * 0.25f;
            case BossKind.Gurney: return new Color(0.6f, 0.8f, 1f) * 0.3f;
            case BossKind.Hazmat: return new Color(0.7f, 1f, 0.25f) * 0.3f;
            case BossKind.Handler: return new Color(1f, 0.55f, 0.2f) * 0.3f;
            case BossKind.Captain: return new Color(1f, 0.35f, 0.25f) * 0.25f;
            case BossKind.Marshal: return new Color(1f, 0.25f, 0.2f) * 0.25f;
            default: return new Color(1f, 0.2f, 0.1f) * 0.25f;
        }
    }

    // Adds the kind's behaviour and look to a freshly spawned boss body. spawnMinion puts a
    // zombie of a kind at a point in the boss's room (for the Broodmother and Howler).
    public static void Equip(Enemy boss, BossKind kind, Func<Vector3, EnemyKind, Enemy> spawnMinion)
    {
        boss.SetLook(Tint(kind), Glow(kind));
        boss.SightRangeMultiplier = 3f;
        boss.SwingAttacks = false;

        switch (kind)
        {
            case BossKind.Broodmother:
                boss.gameObject.AddComponent<BossBroodmother>().Configure(spawnMinion);
                break;
            case BossKind.Howler:
                boss.gameObject.AddComponent<BossHowler>().Configure(spawnMinion);

                // Up close it claws with quick swings (so its attack animation plays).
                boss.SwingAttacks = true;
                boss.AttackPeriodOverride = 0.8f;
                break;
            case BossKind.Warden:
                boss.gameObject.AddComponent<BossWarden>().Configure(spawnMinion);

                // Up close it clubs with its baton.
                boss.SwingAttacks = true;
                boss.AttackPeriodOverride = 1f;
                break;
            case BossKind.Mannequin:
                boss.gameObject.AddComponent<BossMannequin>();

                // Up close it grabs with stiff swipes.
                boss.SwingAttacks = true;
                boss.AttackPeriodOverride = 0.9f;
                break;
            case BossKind.Glutton:
                boss.gameObject.AddComponent<BossGlutton>();
                break;
            case BossKind.Surgeon:
                boss.gameObject.AddComponent<BossSurgeon>();

                // Up close, quick scalpel slashes.
                boss.SwingAttacks = true;
                boss.AttackPeriodOverride = 0.7f;
                break;
            case BossKind.Gurney:
                boss.gameObject.AddComponent<BossGurney>().Configure(spawnMinion);
                break;
            case BossKind.Hazmat:
                boss.gameObject.AddComponent<BossHazmat>();
                break;
            case BossKind.Handler:
                boss.gameObject.AddComponent<BossHandler>();

                // Up close it shoves with heavy swings.
                boss.SwingAttacks = true;
                boss.AttackPeriodOverride = 1.1f;
                break;
            case BossKind.Captain:
                boss.gameObject.AddComponent<BossCaptain>().Configure(spawnMinion);
                break;
            case BossKind.Marshal:
                boss.gameObject.AddComponent<BossMarshal>();

                // Up close, a rifle-butt swing.
                boss.SwingAttacks = true;
                boss.AttackPeriodOverride = 1f;
                break;
            default:
                boss.gameObject.AddComponent<BossButcher>();

                // Up close it hacks with the cleaver, the hit landing as the blade comes down.
                boss.SwingAttacks = true;
                boss.AttackPeriodOverride = 1.1f;
                break;
        }

        BossHealthBar.Track(boss, Name(kind), Tint(kind));
    }

    // Shared helpers for the behaviours.

    // The animation style for each boss's model.
    public static CharacterAnimator.Style AnimationStyle(BossKind kind)
    {
        switch (kind)
        {
            case BossKind.Broodmother:
            case BossKind.Glutton:
            case BossKind.Gurney:
            case BossKind.Hazmat:
                return CharacterAnimator.Style.Broodmother;
            case BossKind.Captain:
                return CharacterAnimator.Style.Howler;
            case BossKind.Howler:
            case BossKind.Mannequin:
            case BossKind.Surgeon:
                return CharacterAnimator.Style.Howler;
            default:
                return CharacterAnimator.Style.Butcher;
        }
    }

    // Most zombies a boss fight has at once (the boss included), counting ones still on the way.
    public const int MaxAliveInFight = 8;

    // How long a called zombie's spot glows on the floor before it climbs in.
    public const float MinionWarning = 1.2f;

    // A zombie called in mid-fight: a glowing disc on the floor where it'll appear (FloorMarker),
    // then the zombie once it fills, if the boss is still alive. Nothing if the fight already has
    // its fill of zombies.
    public static void CallMinion(Enemy boss, Vector3 position, EnemyKind kind, Func<Vector3, EnemyKind, Enemy> spawn, Color colour)
    {
        if (boss == null || spawn == null || boss.Room == null || boss.Room.Enemies.Count + FloorMarker.PendingSpawns >= MaxAliveInFight)
            return;

        FloorMarker.Spawn(Flat(position), colour, MinionWarning, boss.Room.transform,
            () => boss != null && boss.Health > 0 ? spawn(position, kind) : null);
    }

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
