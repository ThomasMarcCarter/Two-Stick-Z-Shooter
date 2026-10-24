using System;
using UnityEngine;

// The Gurney (the Hospital's floor 2 boss): a swollen patient strapped to a runaway hospital bed.
// It can't steer: it rolls in a straight line at speed and glances off the walls (a clank, and a
// little faster each time, up to a cap), flattening boxes and furniture and ramming the player
// (2 damage and a throw). Every few seconds its wheels lock and it screeches to a stop, its spot
// glowing, and patients (walkers; a runner too once it's badly hurt) climb in at the room's edges;
// then it sets off again, aimed at the player. Hurting it is easiest while it's stopped.
[RequireComponent(typeof(Enemy))]
public class BossGurney : MonoBehaviour, IBossMoves
{
    private const float StartSpeed = 6.5f;
    private const float MaxSpeed = 10f;
    private const float SpeedPerBounce = 0.4f;
    private const float RollTime = 6f;
    private const float StopTime = 1.6f;
    private static readonly Color CallColour = new Color(0.6f, 0.8f, 1f);

    private Enemy enemy;
    private Func<Vector3, EnemyKind, Enemy> spawnMinion;
    private Vector3 direction;
    private float speed = StartSpeed;
    private bool stopped = true;
    private float phaseEnds;
    private Color restGlow;
    private float bodyRadius;
    private float lastRam;

    private bool Enraged => enemy.Health <= enemy.MaxHealth / 3;

    public BossMove Move => stopped ? BossMove.Dazed : BossMove.Roll;
    public float MoveProgress => stopped ? Mathf.Clamp01(1f - (phaseEnds - Time.time) / StopTime) : 0f;

    public void Configure(Func<Vector3, EnemyKind, Enemy> spawn)
    {
        spawnMinion = spawn;
    }

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        bodyRadius = transform.localScale.x * 0.5f;
        phaseEnds = Time.time + 1.5f;
    }

    private void Start()
    {
        restGlow = Bosses.Glow(BossKind.Gurney);
    }

    private void Update()
    {
        if (!enemy.IsActive || enemy.Target == null)
            return;

        enemy.ExternalControl = true;

        if (Time.time < phaseEnds)
            return;

        if (stopped)
            SetOff();
        else
            Stop();
    }

    private void FixedUpdate()
    {
        if (!enemy.IsActive)
            return;

        if (stopped)
        {
            enemy.Body.linearVelocity = Vector3.zero;
            return;
        }

        Vector3 eye = Bosses.Flat(enemy.Body.position) + Vector3.up * 0.5f;
        float probe = bodyRadius + speed * Time.fixedDeltaTime + 0.15f;

        foreach (Collider part in Physics.OverlapSphere(eye + direction * probe * 0.5f, bodyRadius + 0.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (part.attachedRigidbody != enemy.Body)
                Smashable.Break(part, part.ClosestPoint(eye), direction);
        }

        // Off a wall: glances away along its face, a little faster.
        foreach (RaycastHit hit in Physics.SphereCastAll(eye, bodyRadius * 0.8f, direction, probe, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.CompareTag("Wall"))
                continue;

            Vector3 normal = Bosses.Flat(hit.normal).sqrMagnitude > 0.01f ? Bosses.Flat(hit.normal).normalized : -direction;
            direction = Bosses.Flat(Vector3.Reflect(direction, normal)).normalized;
            speed = Mathf.Min(MaxSpeed, speed + SpeedPerBounce);
            CameraFollow.Shake(0.15f);
            SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("metal.clank", 3, ProceduralAudio.MetalClank)), transform.position, 0.7f, 0.7f);
            break;
        }

        enemy.FacingOverride = direction;
        enemy.Body.linearVelocity = direction * speed;
    }

    // Rammed: a heavy blow that throws the player out of its way.
    private void OnCollisionEnter(Collision collision)
    {
        if (stopped || Time.time - lastRam < 0.8f || !collision.collider.TryGetComponent(out PlayerHealth player))
            return;

        lastRam = Time.time;
        player.TakeDamage(2, transform.position);

        if (player.TryGetComponent(out Rigidbody body))
            body.linearVelocity = (Bosses.Flat(player.transform.position - transform.position).normalized + direction).normalized * 10f;

        CameraFollow.Shake(0.3f);
    }

    // Aimed at the player, off it goes.
    private void SetOff()
    {
        stopped = false;
        phaseEnds = Time.time + RollTime;
        direction = Bosses.Flat(enemy.Target.position - transform.position).normalized;
        enemy.SetGlow(restGlow);
        ScreamSound.PlayAt(transform.position, 0.7f, 0.6f);
    }

    // Wheels locked: a screech, and patients called in.
    private void Stop()
    {
        stopped = true;
        phaseEnds = Time.time + StopTime;
        enemy.Body.linearVelocity = Vector3.zero;
        enemy.SetGlow(CallColour * 1.5f);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("metal.clank", 3, ProceduralAudio.MetalClank)), transform.position, 0.9f, 0.5f);

        if (spawnMinion == null)
            return;

        int count = Enraged ? 3 : 2;

        for (int i = 0; i < count; i++)
            Bosses.CallMinion(enemy, Bosses.SpawnPointInRoom(enemy, enemy.Target.position, 5f), i == 2 ? EnemyKind.Runner : EnemyKind.Walker,
                spawnMinion, CallColour);
    }
}
