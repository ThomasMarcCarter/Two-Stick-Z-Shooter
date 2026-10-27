using UnityEngine;

// The Surgeon (the Hospital's floor 1 boss): quick and precise. When it has the player in sight
// a few metres off, it stops, a narrow red line glows on the floor toward them (FloorMarker) and
// it dashes the length of it, cutting whatever it meets (2 damage and a throw); it stops short at
// a wall. Below half health it chains three dashes, re-aiming between each. Up close it slashes
// with quick scalpel swings (Enemy's swing attack, set up in Bosses.Equip). Sidestepping the line
// is the fight.
[RequireComponent(typeof(Enemy))]
public class BossSurgeon : MonoBehaviour, IBossMoves
{
    private enum Phase { Stalk, Aim, Dash, Recover }

    private const float MinRange = 3f;
    private const float MaxRange = 11f;
    private const float AimTime = 0.65f;
    private const float DashSpeed = 15f;
    private const float DashLength = 11f;
    private const float RecoverTime = 0.45f;
    private const float Cooldown = 3f;
    private const int EnragedChain = 3;
    private static readonly Color LineColour = new Color(1f, 0.2f, 0.2f);

    private Enemy enemy;
    private Phase phase;
    private float phaseEnds;
    private float nextAttack;
    private int dashesLeft;
    private bool hitThisDash;
    private Vector3 direction;
    private Color restGlow;
    private float bodyRadius;

    private bool Enraged => enemy.Health <= enemy.MaxHealth / 2;

    public BossMove Move => phase == Phase.Aim ? BossMove.WindUp : phase == Phase.Dash ? BossMove.Charge : BossMove.None;
    public float MoveProgress => phase == Phase.Aim ? Mathf.Clamp01(1f - (phaseEnds - Time.time) / AimTime) : 0f;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        bodyRadius = transform.localScale.x * 0.5f;
        nextAttack = Time.time + 2.5f;
    }

    private void Start()
    {
        restGlow = Bosses.Glow(BossKind.Surgeon);
    }

    private void Update()
    {
        if (!enemy.IsActive || enemy.Target == null)
            return;

        switch (phase)
        {
            case Phase.Stalk:
            {
                float distance = Bosses.FlatDistance(transform.position, enemy.Target.position);

                if (Time.time >= nextAttack && enemy.CanSeeTarget && distance >= MinRange && distance <= MaxRange)
                {
                    dashesLeft = Enraged ? EnragedChain : 1;
                    BeginAim();
                }
                break;
            }

            case Phase.Aim:
                enemy.SetGlow(Color.Lerp(restGlow, LineColour * 2.5f, 1f - (phaseEnds - Time.time) / AimTime));

                if (Time.time >= phaseEnds)
                    BeginDash();
                break;

            case Phase.Dash:
                if (Time.time >= phaseEnds)
                    EndDash();
                break;

            case Phase.Recover:
                if (Time.time >= phaseEnds)
                {
                    if (dashesLeft > 0)
                        BeginAim();
                    else
                        Finish();
                }
                break;
        }
    }

    private void FixedUpdate()
    {
        if (phase == Phase.Aim || phase == Phase.Recover)
        {
            enemy.Body.linearVelocity = Vector3.zero;
            return;
        }

        if (phase != Phase.Dash)
            return;

        Vector3 eye = Bosses.Flat(enemy.Body.position) + Vector3.up * 0.5f;
        float probe = bodyRadius + DashSpeed * Time.fixedDeltaTime + 0.15f;

        foreach (RaycastHit hit in Physics.SphereCastAll(eye, bodyRadius * 0.7f, direction, probe, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag("Wall"))
            {
                EndDash();
                return;
            }
        }

        enemy.Body.linearVelocity = direction * DashSpeed;
    }

    // Cutting through the player mid-dash: once a dash.
    private void OnCollisionEnter(Collision collision)
    {
        if (phase != Phase.Dash || hitThisDash || !collision.collider.TryGetComponent(out PlayerHealth player))
            return;

        hitThisDash = true;
        player.TakeDamage(2, transform.position);

        if (player.TryGetComponent(out Rigidbody body))
            body.linearVelocity = Vector3.Cross(Vector3.up, direction).normalized * 8f;

        CameraFollow.Shake(0.25f);
    }

    // Stops and marks the line it'll take, aimed at the player as they are now.
    private void BeginAim()
    {
        phase = Phase.Aim;
        phaseEnds = Time.time + AimTime;
        direction = Bosses.Flat(enemy.Target.position - transform.position).normalized;
        enemy.ExternalControl = true;
        enemy.FacingOverride = direction;
        FloorMarker.Sector(Bosses.Flat(transform.position), direction, DashLength, 8f, LineColour, AimTime,
            enemy.Room != null ? enemy.Room.transform : null, null);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("metal.clank", 3, ProceduralAudio.MetalClank)), transform.position, 0.6f, 1.6f);
    }

    private void BeginDash()
    {
        phase = Phase.Dash;
        phaseEnds = Time.time + DashLength / DashSpeed;
        hitThisDash = false;
        dashesLeft--;
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("throw", 2, ProceduralAudio.Throw)), transform.position, 1f, 0.8f);
    }

    private void EndDash()
    {
        phase = Phase.Recover;
        phaseEnds = Time.time + RecoverTime;
        enemy.Body.linearVelocity = Vector3.zero;
        enemy.SetGlow(restGlow);
    }

    private void Finish()
    {
        phase = Phase.Stalk;
        nextAttack = Time.time + Cooldown * (Enraged ? 0.75f : 1f);
        enemy.ExternalControl = false;
        enemy.FacingOverride = null;
    }
}
