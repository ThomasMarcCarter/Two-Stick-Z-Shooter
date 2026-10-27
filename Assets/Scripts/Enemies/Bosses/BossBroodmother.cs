using System;
using UnityEngine;

// The Broodmother (floor 2's boss): huge and slow. Every few seconds, while she can see the
// player, she stops, swells green and lobs a glob of acid at where they're heading, which leaves
// a burning pool for a few seconds; once badly hurt she spits three at a time. At two thirds and
// one third health she bursts, and runners pour out of her. Keeping on the move, and dealing
// with the brood, is the fight.
[RequireComponent(typeof(Enemy))]
public class BossBroodmother : MonoBehaviour, IBossMoves
{
    private const float SpitRange = 15f;
    private const float WindUpTime = 0.55f;
    private static readonly Vector2 SpitInterval = new Vector2(3.2f, 4.6f);
    private const int RunnersPerBurst = 3;

    private Enemy enemy;
    private Func<Vector3, EnemyKind, Enemy> spawnMinion;
    private float nextSpit;
    private float spitAt = -1f;
    private int burstsDone;
    private Color restGlow;
    private Vector3 baseScale;

    public BossMove Move => spitAt >= 0f ? BossMove.Spit : BossMove.None;
    public float MoveProgress => spitAt >= 0f ? Mathf.Clamp01(1f - (spitAt - Time.time) / WindUpTime) : 0f;

    public void Configure(Func<Vector3, EnemyKind, Enemy> spawn)
    {
        spawnMinion = spawn;
    }

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        baseScale = transform.localScale;
        nextSpit = Time.time + 2.5f;
    }

    private void Start()
    {
        restGlow = Bosses.Glow(BossKind.Broodmother);
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

        if (spitAt >= 0f)
        {
            // Swelling up to spit.
            float t = 1f - (spitAt - Time.time) / WindUpTime;
            enemy.SetGlow(Color.Lerp(restGlow, new Color(0.55f, 1f, 0.15f) * 2f, t));
            transform.localScale = baseScale * (1f + 0.12f * t);

            if (Time.time >= spitAt)
                Spit();

            return;
        }

        if (Time.time >= nextSpit && enemy.CanSeeTarget
            && Bosses.FlatDistance(transform.position, enemy.Target.position) <= SpitRange)
        {
            spitAt = Time.time + WindUpTime;
            enemy.SpeedMultiplier = 0f;
            SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("bloater.swell", 2, ProceduralAudio.BloaterSwell)), transform.position, 0.9f, 0.7f);
        }
    }

    private void Spit()
    {
        spitAt = -1f;
        nextSpit = Time.time + UnityEngine.Random.Range(SpitInterval.x, SpitInterval.y);
        enemy.SpeedMultiplier = 1f;
        enemy.SetGlow(restGlow);
        transform.localScale = baseScale;

        Transform room = enemy.Room != null ? enemy.Room.transform : null;
        Vector3 mouth = transform.position + Vector3.up * baseScale.y * 0.6f;
        Vector3 target = Bosses.Flat(enemy.Target.position);

        // Lead the player a little: aim where they're heading.
        Rigidbody body = enemy.Target.GetComponent<Rigidbody>();

        if (body != null)
            target += Bosses.Flat(body.linearVelocity) * 0.5f;

        AcidGlob.Launch(mouth, target, room);

        // Badly hurt: two more, either side.
        if (enemy.Health <= enemy.MaxHealth / 3)
        {
            Vector3 toTarget = target - Bosses.Flat(transform.position);
            Vector3 side = Vector3.Cross(Vector3.up, toTarget.normalized) * 2.5f;
            AcidGlob.Launch(mouth, target + side, room);
            AcidGlob.Launch(mouth, target - side, room);
        }

        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("throw", 2, ProceduralAudio.Throw)), transform.position, 0.8f, 0.6f);
    }

    // At two thirds and one third health, a brood of runners bursts out of her: their spots glow
    // yellow around her first (not acid green: they come with an acid splash).
    private void OnDamaged(Enemy _)
    {
        int threshold = burstsDone == 0 ? enemy.MaxHealth * 2 / 3 : burstsDone == 1 ? enemy.MaxHealth / 3 : -1;

        if (threshold < 0 || enemy.Health > threshold || spawnMinion == null)
            return;

        burstsDone++;
        CameraFollow.Shake(0.3f);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("explosion", 3, ProceduralAudio.Explosion)), transform.position, 0.6f, 1.4f);
        AcidPool.Create(transform.position, enemy.Room != null ? enemy.Room.transform : null);

        for (int i = 0; i < RunnersPerBurst; i++)
        {
            float angle = (i / (float)RunnersPerBurst) * Mathf.PI * 2f + UnityEngine.Random.value;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (baseScale.x * 0.5f + 1f);
            Bosses.CallMinion(enemy, Bosses.Flat(transform.position) + offset, EnemyKind.Runner, spawnMinion, new Color(1f, 0.85f, 0.2f));
        }
    }
}
