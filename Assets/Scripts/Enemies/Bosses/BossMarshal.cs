using UnityEngine;

// The Marshal (the Airport's floor 3 boss): an air marshal in body armour with a riot gun. It keeps
// its distance, backing off when the player closes in, and every few seconds levels the gun
// (BossMove.WindUp): a red wedge glows on the floor ahead of it (FloorMarker), and the blast hits
// anyone still in it when it fires (2 damage and a throw back). Below half health it fires three
// blasts in a row, re-aiming each. Get out of the wedge, then close in while it reloads.
[RequireComponent(typeof(Enemy))]
public class BossMarshal : MonoBehaviour, IBossMoves
{
    private const float BlastRange = 9f;
    private const float BlastArc = 36f;
    private const float AimTime = 0.8f;
    private const float BetweenShots = 0.35f;
    private const float Cooldown = 3.2f;
    private const float BackOffRange = 4f;
    private const float BackOffSpeed = 3.2f;
    private const int EnragedVolley = 3;
    private static readonly Color AimColour = new Color(1f, 0.25f, 0.2f);

    private Enemy enemy;
    private float aimEnds = -1f;
    private float nextVolley;
    private int shotsLeft;
    private Vector3 direction;
    private Color restGlow;

    private bool Enraged => enemy.Health <= enemy.MaxHealth / 2;

    public BossMove Move => aimEnds >= 0f ? BossMove.WindUp : BossMove.None;
    public float MoveProgress => aimEnds >= 0f ? Mathf.Clamp01(1f - (aimEnds - Time.time) / AimTime) : 0f;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        nextVolley = Time.time + 2.5f;
    }

    private void Start()
    {
        restGlow = Bosses.Glow(BossKind.Marshal);
    }

    private void Update()
    {
        if (!enemy.IsActive || enemy.Target == null)
            return;

        if (aimEnds >= 0f)
        {
            enemy.SetGlow(Color.Lerp(restGlow, AimColour * 2.5f, 1f - (aimEnds - Time.time) / AimTime));
            return;
        }

        float distance = Bosses.FlatDistance(transform.position, enemy.Target.position);

        // Too close: backs off, still facing the player.
        enemy.ExternalControl = distance < BackOffRange && enemy.CanSeeTarget;

        if (Time.time >= nextVolley && enemy.CanSeeTarget && distance <= BlastRange)
        {
            shotsLeft = Enraged ? EnragedVolley : 1;
            Aim();
        }
    }

    private void FixedUpdate()
    {
        if (!enemy.IsActive || enemy.Target == null)
            return;

        if (aimEnds >= 0f)
        {
            enemy.Body.linearVelocity = Vector3.zero;
            return;
        }

        if (enemy.ExternalControl)
        {
            Vector3 away = Bosses.Flat(transform.position - enemy.Target.position).normalized;
            enemy.Body.linearVelocity = away * BackOffSpeed;
            enemy.FacingOverride = -away;
        }
        else
        {
            enemy.FacingOverride = null;
        }
    }

    // Levels the gun at the player: the wedge glows, then the blast.
    private void Aim()
    {
        direction = Bosses.Flat(enemy.Target.position - transform.position).normalized;
        aimEnds = Time.time + AimTime;
        enemy.ExternalControl = true;
        enemy.FacingOverride = direction;
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("metal.clank", 3, ProceduralAudio.MetalClank)), transform.position, 0.7f, 1.3f);
        FloorMarker.Sector(Bosses.Flat(transform.position), direction, BlastRange, BlastArc, AimColour, AimTime,
            enemy.Room != null ? enemy.Room.transform : null, () => { if (this != null && aimEnds >= 0f) Fire(); });
    }

    private void Fire()
    {
        aimEnds = -1f;
        shotsLeft--;
        CameraFollow.Shake(0.2f);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("explosion", 3, ProceduralAudio.Explosion)), transform.position, 0.5f, 1.8f);
        Noise.Emit(transform.position, Noise.Gunshot, Noise.Kind.Gunshot);

        Transform player = enemy.Target;
        Vector3 to = Bosses.Flat(player.position - transform.position);

        // In the wedge, and no wall in between.
        bool inWedge = to.magnitude <= BlastRange && Vector3.Angle(direction, to) <= BlastArc / 2f;
        Vector3 muzzle = transform.position + Vector3.up * 0.8f;

        foreach (RaycastHit hit in Physics.RaycastAll(muzzle, (player.position - muzzle).normalized, Vector3.Distance(muzzle, player.position), ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag("Wall"))
                inWedge = false;
        }

        if (inWedge && player.TryGetComponent(out PlayerHealth health))
        {
            health.TakeDamage(2, transform.position);

            if (player.TryGetComponent(out Rigidbody body))
                body.linearVelocity = to.normalized * 9f;
        }

        if (shotsLeft > 0)
        {
            Invoke(nameof(Aim), BetweenShots);
            return;
        }

        nextVolley = Time.time + Cooldown;
        enemy.ExternalControl = false;
        enemy.FacingOverride = null;
        enemy.SetGlow(restGlow);
    }
}
