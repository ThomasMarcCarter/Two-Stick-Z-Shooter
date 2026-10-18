using System;
using UnityEngine;

// The Howler (floor 3's boss): lean and fast. Shot at, it often darts sideways out of the line
// of fire. Every so often, while it can see the player, it rears up glowing violet and howls:
// a shockwave that throws the player back (and hurts if they're close), a noise the whole floor
// hears, and zombies clawing in from the edges of the room. Below half health it howls more
// often and calls more. Hit it while it rears up, then get clear.
[RequireComponent(typeof(Enemy))]
public class BossHowler : MonoBehaviour, IBossMoves
{
    private const float HowlWindUp = 0.8f;
    private const float HowlRange = 7f;
    private const float HowlPush = 11f;
    private const float DodgeSpeed = 10f;
    private const float DodgeTime = 0.22f;
    private const float DodgeChance = 0.4f;
    private const float DodgeCooldown = 1.4f;

    private Enemy enemy;
    private Func<Vector3, EnemyKind, Enemy> spawnMinion;
    private float nextHowl;
    private float howlAt = -1f;
    private float dodgeUntil = -1f;
    private float nextDodge;
    private Vector3 dodgeVelocity;
    private Color restGlow;
    private Vector3 baseScale;

    private bool Enraged => enemy.Health <= enemy.MaxHealth / 2;

    public BossMove Move => howlAt >= 0f ? BossMove.Howl : dodgeUntil >= 0f ? BossMove.Dodge : BossMove.None;

    public float MoveProgress
    {
        get
        {
            if (howlAt >= 0f)
                return Mathf.Clamp01(1f - (howlAt - Time.time) / HowlWindUp);

            return dodgeUntil >= 0f ? Mathf.Clamp01(1f - (dodgeUntil - Time.time) / DodgeTime) : 0f;
        }
    }

    public void Configure(Func<Vector3, EnemyKind, Enemy> spawn)
    {
        spawnMinion = spawn;
    }

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        baseScale = transform.localScale;
        nextHowl = Time.time + 4f;
    }

    private void Start()
    {
        restGlow = Bosses.Glow(BossKind.Howler);
    }

    private void OnEnable()
    {
        enemy.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        enemy.Damaged -= OnDamaged;
    }

    private void Update()
    {
        if (!enemy.IsActive || enemy.Target == null)
            return;

        if (howlAt >= 0f)
        {
            // Rearing up (the model's animation does most of it): stretches a little taller and glows
            // brighter until the howl.
            float t = 1f - (howlAt - Time.time) / HowlWindUp;
            enemy.SetGlow(Color.Lerp(restGlow, new Color(0.75f, 0.4f, 1f) * 2.5f, t));
            transform.localScale = new Vector3(baseScale.x * (1f - 0.04f * t), baseScale.y * (1f + 0.1f * t), baseScale.z * (1f - 0.04f * t));

            if (Time.time >= howlAt)
                Howl();

            return;
        }

        if (dodgeUntil >= 0f && Time.time >= dodgeUntil)
        {
            dodgeUntil = -1f;
            enemy.ExternalControl = false;
        }

        if (Time.time >= nextHowl && enemy.CanSeeTarget && dodgeUntil < 0f)
        {
            howlAt = Time.time + HowlWindUp;
            enemy.ExternalControl = true;
            SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("screamer", 3, ProceduralAudio.Scream)), transform.position, 0.7f, 0.75f);
        }
    }

    private void FixedUpdate()
    {
        if (howlAt >= 0f)
            enemy.Body.linearVelocity = Vector3.zero;
        else if (dodgeUntil >= 0f)
            enemy.Body.linearVelocity = dodgeVelocity;
    }

    // Shot: sometimes darts sideways, across the line of fire.
    private void OnDamaged(Enemy _)
    {
        if (howlAt >= 0f || dodgeUntil >= 0f || Time.time < nextDodge || UnityEngine.Random.value > DodgeChance)
            return;

        Vector3 toPlayer = Bosses.Flat(enemy.Target.position - transform.position).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, toPlayer) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);

        // Prefer the side with room to move.
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, side, 2.5f, ~0, QueryTriggerInteraction.Ignore))
            side = -side;

        dodgeVelocity = (side + toPlayer * 0.2f).normalized * DodgeSpeed;
        dodgeUntil = Time.time + DodgeTime;
        nextDodge = Time.time + DodgeCooldown;
        enemy.ExternalControl = true;
    }

    private void Howl()
    {
        howlAt = -1f;
        nextHowl = Time.time + (Enraged ? 6.5f : 9f);
        enemy.ExternalControl = false;
        enemy.SetGlow(restGlow);
        transform.localScale = baseScale;

        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("screamer", 3, ProceduralAudio.Scream)), transform.position, 1f, 0.55f);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("explosion", 3, ProceduralAudio.Explosion)), transform.position, 0.5f, 0.5f);
        Noise.Emit(transform.position, Noise.Gunshot * 2f, Noise.Kind.Scream);
        CameraFollow.Shake(0.4f);

        // The shockwave: throws the player back, and hurts up close.
        Transform player = enemy.Target;
        float distance = Bosses.FlatDistance(player.position, transform.position);

        if (distance <= HowlRange)
        {
            if (distance <= HowlRange * 0.45f && player.TryGetComponent(out PlayerHealth health))
                health.TakeDamage(1, transform.position);

            if (player.TryGetComponent(out Rigidbody body))
            {
                Vector3 away = Bosses.Flat(player.position - transform.position).normalized;
                body.linearVelocity = away * HowlPush * Mathf.Lerp(1f, 0.5f, distance / HowlRange);
            }
        }

        // The call: zombies climb in from the room's edges, away from the player.
        if (spawnMinion == null)
            return;

        int walkers = 2;
        int runners = Enraged ? 1 : 0;

        for (int i = 0; i < walkers + runners; i++)
            spawnMinion(Bosses.SpawnPointInRoom(enemy, player.position, 5f), i < walkers ? EnemyKind.Walker : EnemyKind.Runner);
    }
}
