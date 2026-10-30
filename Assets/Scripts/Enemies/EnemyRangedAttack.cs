using UnityEngine;

// What every ranged zombie (spitter, retcher, gunner, lobber, leaper) does before it attacks: once
// it's chasing the player and can see them (so sneaking past one still works; they do see further
// than other zombies), in range and off
// cooldown, it stops dead, turns to face them and winds up: the sprite runs through the first
// third of its attack clip and its glow swells, so there's always a moment to see it coming. Then
// Release (the kind's own attack) fires and the clip plays out. A stagger or the fight ending
// cancels a wind-up. Each kind sets the numbers; the aim can lock a little before release so a
// sidestep beats it.
[RequireComponent(typeof(Enemy))]
public abstract class EnemyRangedAttack : MonoBehaviour
{
    private const float FollowThrough = 0.3f;

    // They pick the player out from further off than other zombies (10.5 m, 7.35 m sneaking),
    // or they'd never get to use their range.
    private const float SightMultiplier = 1.75f;

    // The floor and zone it was spawned on (FloorGenerator.SpawnEnemy), for patterns that get
    // denser deeper down and the gunner's ammo.
    public int Floor { get; set; } = 1;
    public Zone Zone { get; set; }

    protected Enemy enemy;
    protected Vector3 aim = Vector3.forward;

    private float releaseAt = -1f;
    private float poseUntil;
    private float nextAttack;
    private Color restGlow;

    protected abstract float Range { get; }
    protected abstract float WindUp { get; }
    protected abstract Vector2 Interval { get; }
    protected virtual float MinRange => 0f;
    protected virtual float AimLock => 0f;
    protected virtual Color WindUpGlow => new Color(0.6f, 1f, 0.2f) * 1.5f;

    public bool WindingUp => releaseAt >= 0f;

    protected Transform RoomTransform => enemy.Room != null ? enemy.Room.transform : null;

    protected abstract void Release();
    protected virtual void OnWindUp() { }

    protected virtual void Awake()
    {
        enemy = GetComponent<Enemy>();
        enemy.SightRangeMultiplier = SightMultiplier;
        nextAttack = Time.time + Random.Range(1f, 2f);
    }

    protected virtual void Start()
    {
        restGlow = EnemyKinds.Glow(enemy.Kind);
    }

    protected virtual void Update()
    {
        if (WindingUp)
        {
            if (!enemy.IsActive || enemy.IsStaggered || enemy.Target == null)
            {
                Cancel();
                return;
            }

            float k = Mathf.Clamp01(1f - (releaseAt - Time.time) / WindUp);
            enemy.PoseTime = k * 0.33f;
            enemy.SetGlow(Color.Lerp(restGlow, WindUpGlow, k));

            if (Time.time < releaseAt - AimLock)
                aim = ToTarget();

            enemy.FacingOverride = aim;

            if (Time.time >= releaseAt)
            {
                releaseAt = -1f;
                EndWindUp();
                poseUntil = Time.time + FollowThrough;
                Release();
            }

            return;
        }

        if (Time.time < poseUntil)
            enemy.PoseTime = 0.33f + 0.67f * (1f - (poseUntil - Time.time) / FollowThrough);
        else if (enemy.PoseTime >= 0f)
            enemy.PoseTime = -1f;

        if (!Ready())
            return;

        releaseAt = Time.time + WindUp;
        enemy.ExternalControl = true;
        aim = ToTarget();
        OnWindUp();
    }

    protected virtual void FixedUpdate()
    {
        if (WindingUp)
            enemy.Body.linearVelocity = Vector3.zero;
    }

    private void OnDisable()
    {
        if (WindingUp)
            Cancel();
    }

    protected virtual bool Ready()
    {
        if (Time.time < nextAttack || !enemy.IsActive || enemy.Target == null || !enemy.IsChasing || !enemy.CanSeeTarget
            || enemy.IsStaggered || enemy.AttackTime >= 0f || enemy.ExternalControl)
            return false;

        float distance = FlatDistance(transform.position, enemy.Target.position);
        return distance >= MinRange && distance <= Range;
    }

    protected Vector3 ToTarget()
    {
        Vector3 to = enemy.Target != null ? enemy.Target.position - transform.position : transform.forward;
        to.y = 0f;
        return to.sqrMagnitude > 0.0001f ? to.normalized : Vector3.forward;
    }

    // Where the player will be in `seconds`, going the way they're going.
    protected Vector3 Lead(float seconds)
    {
        Vector3 point = enemy.Target.position;

        if (enemy.Target.TryGetComponent(out Rigidbody body))
            point += body.linearVelocity * seconds;

        point.y = 0f;
        return point;
    }

    private void EndWindUp()
    {
        enemy.ExternalControl = false;
        enemy.FacingOverride = null;
        enemy.SetGlow(restGlow);
        nextAttack = Time.time + Random.Range(Interval.x, Interval.y);
    }

    private void Cancel()
    {
        releaseAt = -1f;
        EndWindUp();
        enemy.PoseTime = -1f;
    }

    protected static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
