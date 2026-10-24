using System.Collections;
using UnityEngine;

// The Glutton (the Shopping Mall's floor 3 boss): a vast, swollen thing from the food court.
// Slow. Every few seconds, while it can see the player, it rears back, a yellow-green wedge
// glowing on the floor ahead of it (FloorMarker), and vomits: a gush of bile along the wedge
// leaves a line of acid pools that burn for a while. Below half health it also curls up and
// rolls at the player, fast and in a straight line, smashing boxes and furniture; rolling into
// the player throws them, rolling into a wall leaves it dazed, taking double damage. When it
// dies it bursts, leaving a pool of acid where it fell. Touching it hurts (no swings).
[RequireComponent(typeof(Enemy))]
public class BossGlutton : MonoBehaviour, IBossMoves
{
    private enum Phase { Stalk, Vomit, Roll, Dazed }

    private const float VomitRange = 9f;
    private const float VomitWindUp = 0.9f;
    private const float VomitLength = 8f;
    private const float VomitArc = 26f;
    private const float PoolSpacing = 1.6f;
    private static readonly Vector2 VomitInterval = new Vector2(4f, 5.5f);
    private const float RollSpeed = 9f;
    private const float RollTime = 1.8f;
    private const float RollCooldown = 6f;
    private const float DazedTime = 2f;
    private static readonly Color BileColour = new Color(0.75f, 0.9f, 0.15f);

    private Enemy enemy;
    private Phase phase;
    private float phaseEnds;
    private float nextVomit;
    private float nextRoll;
    private Vector3 direction;
    private Color restGlow;
    private Vector3 baseScale;
    private float bodyRadius;

    private bool Enraged => enemy.Health <= enemy.MaxHealth / 2;

    public BossMove Move
    {
        get
        {
            switch (phase)
            {
                case Phase.Vomit: return BossMove.Vomit;
                case Phase.Roll: return BossMove.Roll;
                case Phase.Dazed: return BossMove.Dazed;
                default: return BossMove.None;
            }
        }
    }

    public float MoveProgress
    {
        get
        {
            float length = phase == Phase.Vomit ? VomitWindUp : phase == Phase.Roll ? RollTime : DazedTime;
            return phase == Phase.Stalk ? 0f : Mathf.Clamp01(1f - (phaseEnds - Time.time) / length);
        }
    }

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        baseScale = transform.localScale;
        bodyRadius = baseScale.x * 0.5f;
        nextVomit = Time.time + 3f;
        nextRoll = Time.time + 2f;
    }

    private void Start()
    {
        restGlow = Bosses.Glow(BossKind.Glutton);
    }

    private void OnEnable()
    {
        enemy.Died += OnDied;
    }

    private void OnDisable()
    {
        enemy.Died -= OnDied;
    }

    private void Update()
    {
        if (!enemy.IsActive || enemy.Target == null)
            return;

        float distance = Bosses.FlatDistance(transform.position, enemy.Target.position);

        switch (phase)
        {
            case Phase.Stalk:
                if (Enraged && Time.time >= nextRoll && enemy.CanSeeTarget && distance > 3f)
                    BeginRoll();
                else if (Time.time >= nextVomit && enemy.CanSeeTarget && distance <= VomitRange)
                    BeginVomit();
                break;

            case Phase.Vomit:
            {
                float t = 1f - (phaseEnds - Time.time) / VomitWindUp;
                enemy.SetGlow(Color.Lerp(restGlow, BileColour * 2.5f, t));
                transform.localScale = new Vector3(baseScale.x * (1f + 0.08f * t), baseScale.y * (1f - 0.05f * t), baseScale.z * (1f + 0.08f * t));
                break;
            }

            case Phase.Roll:
                if (Time.time >= phaseEnds)
                    EndAttack();
                break;

            case Phase.Dazed:
                enemy.SetGlow(new Color(1f, 0.85f, 0.2f) * (0.4f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 9f))));

                if (Time.time >= phaseEnds)
                {
                    enemy.DamageMultiplier = 1f;
                    EndAttack();
                }
                break;
        }
    }

    private void FixedUpdate()
    {
        if (phase == Phase.Vomit || phase == Phase.Dazed)
        {
            enemy.Body.linearVelocity = Vector3.zero;
            return;
        }

        if (phase != Phase.Roll)
            return;

        // Boxes, trash and furniture in its way are flattened.
        Vector3 eye = Bosses.Flat(enemy.Body.position) + Vector3.up * 0.5f;
        float probe = bodyRadius + RollSpeed * Time.fixedDeltaTime + 0.15f;

        foreach (Collider part in Physics.OverlapSphere(eye + direction * probe * 0.5f, bodyRadius + 0.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (part.attachedRigidbody != enemy.Body)
                Smashable.Break(part, part.ClosestPoint(eye), direction);
        }

        foreach (RaycastHit hit in Physics.SphereCastAll(eye, bodyRadius * 0.7f, direction, probe, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag("Wall"))
            {
                Slam();
                return;
            }
        }

        enemy.Body.linearVelocity = direction * RollSpeed;
    }

    // Rolling into the player: a heavy blow that throws them, and the roll ends.
    private void OnCollisionEnter(Collision collision)
    {
        if (phase != Phase.Roll || !collision.collider.TryGetComponent(out PlayerHealth player))
            return;

        player.TakeDamage(2, transform.position);

        if (player.TryGetComponent(out Rigidbody body))
            body.linearVelocity = direction * 10f;

        CameraFollow.Shake(0.3f);
        EndAttack();
    }

    private void BeginVomit()
    {
        phase = Phase.Vomit;
        phaseEnds = Time.time + VomitWindUp;
        direction = Bosses.Flat(enemy.Target.position - transform.position).normalized;
        enemy.ExternalControl = true;
        enemy.FacingOverride = direction;
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("bloater.swell", 2, ProceduralAudio.BloaterSwell)), transform.position, 1f, 0.55f);

        // The wedge it'll cover, glowing on the floor first.
        FloorMarker.Sector(Bosses.Flat(transform.position), direction, VomitLength, VomitArc, BileColour, VomitWindUp,
            enemy.Room != null ? enemy.Room.transform : null, () => { if (this != null && phase == Phase.Vomit) Vomit(); });
    }

    // Bile along the wedge: acid pools down its middle, one after another.
    private void Vomit()
    {
        transform.localScale = baseScale;
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("throw", 2, ProceduralAudio.Throw)), transform.position, 1f, 0.45f);
        StartCoroutine(SpillAlong(Bosses.Flat(transform.position), direction));
        EndAttack();
        nextVomit = Time.time + Random.Range(VomitInterval.x, VomitInterval.y) * (Enraged ? 0.8f : 1f);
    }

    private IEnumerator SpillAlong(Vector3 from, Vector3 along)
    {
        Transform room = enemy.Room != null ? enemy.Room.transform : null;

        for (float d = bodyRadius + 0.8f; d <= VomitLength; d += PoolSpacing)
        {
            Vector3 point = from + along * d;

            // Stops at a wall.
            foreach (RaycastHit hit in Physics.RaycastAll(from + Vector3.up * 0.5f, along, d, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.CompareTag("Wall"))
                    yield break;
            }

            AcidPool.Create(point, room);
            yield return new WaitForSeconds(0.07f);
        }
    }

    private void BeginRoll()
    {
        phase = Phase.Roll;
        phaseEnds = Time.time + RollTime;
        direction = Bosses.Flat(enemy.Target.position - transform.position).normalized;
        enemy.ExternalControl = true;
        enemy.FacingOverride = direction;
        ScreamSound.PlayAt(transform.position, 0.8f, 0.4f);
    }

    // Into a wall: a thud, and dazed for a while, taking double damage.
    private void Slam()
    {
        phase = Phase.Dazed;
        phaseEnds = Time.time + DazedTime;
        enemy.Body.linearVelocity = Vector3.zero;
        enemy.DamageMultiplier = 2f;
        CameraFollow.Shake(0.4f);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("explosion", 3, ProceduralAudio.Explosion)), transform.position, 0.5f, 0.6f);
    }

    private void EndAttack()
    {
        if (phase == Phase.Roll || phase == Phase.Dazed)
            nextRoll = Time.time + RollCooldown;

        phase = Phase.Stalk;
        enemy.ExternalControl = false;
        enemy.FacingOverride = null;
        enemy.SetGlow(restGlow);
        transform.localScale = baseScale;
    }

    // Bursts: a pool of acid where it fell.
    private void OnDied(Enemy _)
    {
        AcidPool.Create(transform.position, enemy.Room != null ? enemy.Room.transform : null);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("explosion", 3, ProceduralAudio.Explosion)), transform.position, 0.6f, 1.3f);
    }
}
