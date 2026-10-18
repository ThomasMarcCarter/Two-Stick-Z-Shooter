using UnityEngine;

// The Butcher (floor 1's boss): plods after the player like a big zombie, but when it has them
// in sight at a distance it stops, roars and glows red for a moment, then charges in a straight
// line at several times its speed. Running into the player hurts and throws them; running into
// a wall stuns it for a couple of seconds, during which it takes double damage. Sidestepping
// the charge so it hits the wall is the way to beat it.
[RequireComponent(typeof(Enemy))]
public class BossButcher : MonoBehaviour
{
    private enum Phase { Stalk, WindUp, Charge, Dazed }

    private const float MinRange = 3.5f;
    private const float MaxRange = 15f;
    private const float WindUpTime = 0.9f;
    private const float AimLockTime = 0.25f;   // stops tracking the player this long before charging
    private const float ChargeSpeed = 12f;
    private const float MaxChargeTime = 1.6f;
    private const float DazedTime = 2.2f;
    private const float Cooldown = 3.5f;

    private Enemy enemy;
    private Phase phase = Phase.Stalk;
    private float phaseEnds;
    private float nextCharge;
    private Vector3 chargeDirection;
    private Color restGlow;
    private Vector3 baseScale;
    private float bodyRadius = 1f;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        baseScale = transform.localScale;
        nextCharge = Time.time + 2f;
        Collider body = GetComponent<Collider>();

        if (body != null)
            bodyRadius = Mathf.Max(body.bounds.extents.x, body.bounds.extents.z);
    }

    private void Start()
    {
        restGlow = Bosses.Glow(BossKind.Butcher);
    }

    private void Update()
    {
        if (!enemy.IsActive || enemy.Target == null)
            return;

        switch (phase)
        {
            case Phase.Stalk:
                float distance = Bosses.FlatDistance(transform.position, enemy.Target.position);

                if (Time.time >= nextCharge && enemy.CanSeeTarget && !enemy.IsStaggered && distance >= MinRange && distance <= MaxRange)
                    BeginWindUp();
                break;

            case Phase.WindUp:
            {
                // Glows hotter and shudders; tracks the player, then locks its aim just before
                // it goes, so a well-timed sidestep makes it miss.
                float t = 1f - (phaseEnds - Time.time) / WindUpTime;
                enemy.SetGlow(Color.Lerp(restGlow, new Color(1f, 0.15f, 0.05f) * 2.5f, t));
                float shake = Mathf.Sin(Time.time * 70f) * 0.04f * t;
                transform.localScale = new Vector3(baseScale.x * (1f + shake), baseScale.y * (1f - shake), baseScale.z * (1f + shake));

                if (phaseEnds - Time.time > AimLockTime)
                    chargeDirection = Bosses.Flat(enemy.Target.position - transform.position).normalized;

                if (Time.time >= phaseEnds)
                    BeginCharge();
                break;
            }

            case Phase.Charge:
                if (Time.time >= phaseEnds)
                    EndAttack(Cooldown * 0.6f);
                break;

            case Phase.Dazed:
                // Stars: a flickering yellow glow while it reels.
                enemy.SetGlow(new Color(1f, 0.85f, 0.2f) * (0.4f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 9f))));

                if (Time.time >= phaseEnds)
                {
                    enemy.DamageMultiplier = 1f;
                    EndAttack(Cooldown);
                }
                break;
        }
    }

    private void FixedUpdate()
    {
        if (phase == Phase.WindUp || phase == Phase.Dazed)
        {
            enemy.Body.linearVelocity = Vector3.zero;
            return;
        }

        if (phase != Phase.Charge)
            return;

        // Into a wall: thud, and dazed.
        Vector3 eye = Bosses.Flat(enemy.Body.position) + Vector3.up * 0.5f;
        float probe = bodyRadius + ChargeSpeed * Time.fixedDeltaTime + 0.15f;

        if (Physics.SphereCast(eye, bodyRadius * 0.7f, chargeDirection, out RaycastHit hit, probe, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.CompareTag("Wall"))
        {
            Slam();
            return;
        }

        enemy.Body.linearVelocity = chargeDirection * ChargeSpeed;
    }

    // Hitting the player mid-charge: a heavy blow that throws them, and the charge ends.
    private void OnCollisionEnter(Collision collision)
    {
        if (phase != Phase.Charge || !collision.collider.TryGetComponent(out PlayerHealth player))
            return;

        player.TakeDamage(2, transform.position);
        CameraFollow.Shake(0.3f);
        EndAttack(Cooldown);
    }

    private void BeginWindUp()
    {
        phase = Phase.WindUp;
        phaseEnds = Time.time + WindUpTime;
        enemy.ExternalControl = true;
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("screamer", 3, ProceduralAudio.Scream)), transform.position, 1f, 0.45f);
        Noise.Emit(transform.position, Noise.Gunshot, Noise.Kind.Scream);
    }

    private void BeginCharge()
    {
        phase = Phase.Charge;
        phaseEnds = Time.time + MaxChargeTime;
        transform.localScale = baseScale;
        enemy.SetGlow(new Color(1f, 0.15f, 0.05f) * 1.5f);
    }

    // How many times it has charged into a wall (for tests and tuning).
    public int Slams { get; private set; }

    private void Slam()
    {
        Slams++;
        phase = Phase.Dazed;
        phaseEnds = Time.time + DazedTime;
        enemy.Body.linearVelocity = Vector3.zero;
        enemy.DamageMultiplier = 2f;

        CameraFollow.Shake(0.5f);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("explosion", 3, ProceduralAudio.Explosion)), transform.position, 0.8f, 0.55f);
        Noise.Emit(transform.position, Noise.Gunshot * 1.5f, Noise.Kind.Smash);

        if (ImpactEffects.TryGetMaterials(out ImpactEffects.Materials materials))
        {
            for (int i = 0; i < 5; i++)
                ImpactEffects.Sparks(transform.position + chargeDirection * bodyRadius + Vector3.up * 0.5f, (-chargeDirection + Random.insideUnitSphere * 0.6f + Vector3.up * 0.4f).normalized, materials);
        }
    }

    private void EndAttack(float cooldown)
    {
        phase = Phase.Stalk;
        nextCharge = Time.time + cooldown;
        enemy.ExternalControl = false;
        transform.localScale = baseScale;
        enemy.SetGlow(restGlow);
    }
}
