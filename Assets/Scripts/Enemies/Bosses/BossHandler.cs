using UnityEngine;

// The Handler (the Airport's floor 1 boss): a baggage handler, big and strong. Every few seconds,
// while it can see the player, it hefts a suitcase (BossMove.Spit) and hurls it at where they're
// heading: a disc glows on the floor where it'll land (FloorMarker) while the case flies over in an
// arc, then it bursts, hurting anyone in the disc (1 damage, 2 dead centre) and smashing boxes and
// furniture. Below half health it throws three at once, fanned round the first. Up close it shoves
// with heavy swings (Enemy's swing attack, set up in Bosses.Equip).
[RequireComponent(typeof(Enemy))]
public class BossHandler : MonoBehaviour, IBossMoves
{
    private const float ThrowRange = 13f;
    private const float WindUp = 0.6f;
    private const float FlightTime = 0.9f;
    private const float BurstRadius = 1.6f;
    private static readonly Vector2 ThrowInterval = new Vector2(2.8f, 4f);
    private static readonly Color CaseColour = new Color(1f, 0.55f, 0.2f);

    private Enemy enemy;
    private float throwAt = -1f;
    private float nextThrow;
    private Color restGlow;

    private bool Enraged => enemy.Health <= enemy.MaxHealth / 2;

    public BossMove Move => throwAt >= 0f ? BossMove.Spit : BossMove.None;
    public float MoveProgress => throwAt >= 0f ? Mathf.Clamp01(1f - (throwAt - Time.time) / WindUp) : 0f;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        nextThrow = Time.time + 2.5f;
    }

    private void Start()
    {
        restGlow = Bosses.Glow(BossKind.Handler);
    }

    private void Update()
    {
        if (!enemy.IsActive || enemy.Target == null)
            return;

        if (throwAt >= 0f)
        {
            enemy.SetGlow(Color.Lerp(restGlow, CaseColour * 2f, 1f - (throwAt - Time.time) / WindUp));

            if (Time.time >= throwAt)
                Throw();

            return;
        }

        if (Time.time >= nextThrow && enemy.CanSeeTarget && Bosses.FlatDistance(transform.position, enemy.Target.position) <= ThrowRange)
        {
            throwAt = Time.time + WindUp;
            enemy.ExternalControl = true;
            enemy.FacingOverride = Bosses.Flat(enemy.Target.position - transform.position).normalized;
        }
    }

    private void FixedUpdate()
    {
        if (throwAt >= 0f)
            enemy.Body.linearVelocity = Vector3.zero;
    }

    private void Throw()
    {
        throwAt = -1f;
        nextThrow = Time.time + Random.Range(ThrowInterval.x, ThrowInterval.y);
        enemy.ExternalControl = false;
        enemy.FacingOverride = null;
        enemy.SetGlow(restGlow);

        // Where they're heading.
        Vector3 target = Bosses.Flat(enemy.Target.position);

        if (enemy.Target.TryGetComponent(out Rigidbody body))
            target += Bosses.Flat(body.linearVelocity) * 0.6f;

        Launch(target);

        if (Enraged)
        {
            Vector3 side = Vector3.Cross(Vector3.up, (target - Bosses.Flat(transform.position)).normalized) * 3f;
            Launch(target + side);
            Launch(target - side);
        }

        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("throw", 2, ProceduralAudio.Throw)), transform.position, 1f, 0.5f);
    }

    private void Launch(Vector3 target)
    {
        Transform room = enemy.Room != null ? enemy.Room.transform : null;
        Lobbed.Throw(transform.position + Vector3.up * transform.localScale.y * 0.7f, target, FlightTime, BurstRadius, CaseColour,
            room, 1, 2, Lobbed.Style.Suitcase);
    }
}
