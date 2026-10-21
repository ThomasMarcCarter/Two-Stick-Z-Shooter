using System;
using UnityEngine;
using UnityEngine.AI;

// Placeholder enemy: a block that hunts the player once its room is locked, attacks them when
// it gets close, and dies after enough hits. Rooms count these to decide when they're cleared.
//
// It only chases what it can see, and only so far: the player has to be within sight range and
// not behind a wall or pillar. Once spotted it hunts them; when sight is broken it goes to where
// the player was last seen, then carries on the way they were moving for a few seconds, which
// takes it around the corner after them. If that turns up nothing it forgets, and wanders the
// room slowly until it spots the player again. Being shot gives the player away too, and it
// can hear further than it can see: footsteps and gunshots within hearing range (through
// walls) bring it to investigate where the sound came from.
// It moves by physics but plans its route over the floor's navmesh, so it goes around pillars
// and corners rather than into them; along a wall it steers to slide past, and if it still
// gets wedged it side-steps out. A blow (melee, a blast) knocks it back and staggers it for a
// moment, less so for heavy kinds, and bosses barely (so they can't be stun-locked). What kind
// it is (walker, runner, brute and so on) only changes its numbers and colour here; the
// screamer's and bloater's extra behaviour is in their own components (EnemyScreamer,
// EnemyBloater).
// Attacks are swings: once the player is within reach it starts one (its length set by kind),
// and the hit lands at the moment the arms come down, if the player is still in reach then, so
// backing off during the wind-up dodges it. The model's swing (CharacterAnimator) is driven by
// the same clock. A blow that staggers it cancels the swing. Bosses hurt on contact instead,
// except the Butcher's cleaver chops and the Howler's claws, which use the same timed swing (AttackPeriodOverride).
[RequireComponent(typeof(Rigidbody))]
public class Enemy : MonoBehaviour
{
    private enum State
    {
        Chase,      // can see the player
        Search,     // lost sight; heading for where they were last seen, then the way they went
        Wander      // forgotten; drifting around the room
    }

    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float hitFlashTime = 0.08f;
    [SerializeField] private int contactDamage = 1;

    // Sight, hearing and memory ranges are shared by all enemies: see EnemySenses, set on the
    // FloorGenerator.
    [Header("Senses")]
    [Tooltip("How often line of sight to the player is checked.")]
    [SerializeField] private float sightInterval = 0.1f;

    [Header("Wandering")]
    [SerializeField, Range(0f, 1f)] private float wanderSpeedFraction = 0.35f;
    [SerializeField] private Vector2 wanderPause = new Vector2(0.5f, 1.8f);

    // Starts a swing with the player this far beyond its edge; the blow lands within HitReach.
    private const float AttackReach = 1.3f;
    private const float HitReach = 1.45f;

    // Below this speed while trying to move, for this long, counts as wedged.
    private const float StuckSpeed = 0.25f;
    private const float StuckTime = 0.5f;
    private const float EyeHeight = 0.5f;

    // Close enough to a search spot to count as there. Generous, because a group of enemies
    // heading for the same spot can't all stand on it.
    private float ArriveDistance => bodyRadius + 1f;

    private static readonly RaycastHit[] hits = new RaycastHit[16];

    public event Action<Enemy> Died;

    // Took damage and survived it (the killing blow raises Died instead).
    public event Action<Enemy> Damaged;

    // Caught sight of the player, having not been chasing them (for the screamer).
    public event Action<Enemy> SpottedPlayer;

    private static EnemySenses Senses => EnemySenses.Current;

    private int health;
    private Room room;
    private Transform target;
    private Rigidbody targetBody;
    private PlayerMovement targetMovement;
    private Rigidbody rb;
    private Renderer[] bodyRenderers = new Renderer[0];
    private MaterialPropertyBlock flashBlock;
    private float flashTimer;
    private float bodyRadius = 0.5f;

    private State state = State.Wander;
    private bool canSee;
    private float nextSightCheck;
    private Vector3 lastSeenPosition;
    private Vector3 lastSeenHeading;
    private float lastSeenTime = -100f;
    private bool reachedLastSeen;
    private float forgetAt;
    private Vector3 searchPoint;

    private Vector3 wanderPoint;
    private bool hasWanderPoint;
    private float wanderWaitUntil;
    private float wanderGiveUpAt;

    // Route over the navmesh to the current destination, refreshed as it moves.
    private const float RepathInterval = 0.25f;
    private NavMeshPath path;
    private Vector3[] corners = new Vector3[0];
    private int corner;
    private Vector3 pathGoal;
    private float nextRepath;

    private float stuckFor;
    private Vector3 sidestep;
    private float sidestepUntil;

    private Color? tint;
    private Color emission;
    private float knockbackScale = 1f;
    private float staggerUntil;
    private float staggerImmuneUntil;

    // Bosses: how much of a blow's push they take, how much of its stagger, and how long after a
    // stagger before another can land, so repeated melee hits can't stun-lock them.
    private const float HeavyKnockback = 0.15f;
    private const float HeavyStagger = 0.4f;
    private const float HeavyStaggerCooldown = 2.5f;

    private float attackTime = -1f;
    private int strikesDone;

    public EnemyKind Kind { get; private set; } = EnemyKind.Walker;
    public Room Room => room;
    public Transform Target => target;
    public bool IsChasing => state == State.Chase;
    public int Health => health;
    public bool IsStaggered => Time.time < staggerUntil;

    // A boss: shrugs off most of a blow's push, flinches only briefly, and can't be staggered
    // again for a while after (see Knock).
    public bool Heavy { get; set; }

    // Scales its speed, e.g. a bloater slowing as it swells. 1 normally.
    public float SpeedMultiplier { get; set; } = 1f;

    // Scales the damage it takes, e.g. a boss dazed after charging into a wall. 1 normally.
    public float DamageMultiplier { get; set; } = 1f;

    // Scales how far it can see (walls still block it). Bosses see across their whole room.
    public float SightRangeMultiplier { get; set; } = 1f;

    // While true it still sees and hears, but doesn't move itself: a boss's attack (a charge,
    // a dodge) is driving its body instead.
    public bool ExternalControl { get; set; }

    // A direction the model must face whatever it's doing (a boss locked into a charge), or null
    // to face where it's going as usual (FaceMovement).
    public Vector3? FacingOverride { get; set; }

    // Hurts with timed swings (true) or by touching the player (false, for bosses).
    public bool SwingAttacks { get; set; } = true;

    // How far through its current swing it is (0 to 1), or -1 when not attacking.
    public float AttackTime => attackTime;

    // A swing length to use instead of its kind's (a boss with its own attack); 0 for the kind's.
    public float AttackPeriodOverride { get; set; }

    public bool CanSeeTarget => canSee;
    public int MaxHealth => maxHealth;
    public Rigidbody Body => rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        Renderer own = GetComponent<Renderer>();
        bodyRenderers = own != null ? new[] { own } : new Renderer[0];
        health = maxHealth;

        RefreshBodyRadius();

        // Spread the sight checks of a room's enemies over different frames.
        nextSightCheck = UnityEngine.Random.value * sightInterval;
    }

    private void OnEnable()
    {
        Noise.Register(this);
    }

    private void OnDisable()
    {
        Noise.Unregister(this);
    }

    // A noise of the given loudness (a multiple of hearing range) at a point. If it's close
    // enough to hear and the enemy isn't already on the player, it goes to look.
    public void Hear(Vector3 position, float loudness)
    {
        if (room == null || !room.IsLocked || health <= 0 || state == State.Chase)
            return;

        if (FlatDistance(rb.position, position) > Senses.hearingRange * loudness)
            return;

        Investigate(position);
    }

    // Head for a spot the player gave away (a noise, or where a shot came from), then look
    // around there like after losing sight of them.
    private void Investigate(Vector3 position)
    {
        state = State.Search;
        lastSeenPosition = Flat(position);
        lastSeenHeading = Vector3.zero;
        lastSeenTime = Time.time;
        forgetAt = Time.time + Senses.maxSearchTime;
        reachedLastSeen = false;
        hasWanderPoint = false;
    }

    public void Initialise(Room owningRoom, Transform chaseTarget, int startingHealth, float speed, int damage)
    {
        room = owningRoom;
        target = chaseTarget;
        targetBody = chaseTarget != null ? chaseTarget.GetComponent<Rigidbody>() : null;
        targetMovement = chaseTarget != null ? chaseTarget.GetComponent<PlayerMovement>() : null;
        maxHealth = startingHealth;
        health = startingHealth;
        moveSpeed = speed;
        contactDamage = damage;
    }

    // Its kind's colour and how much a blow moves it. Called once, after Initialise.
    public void SetKind(EnemyKind kind)
    {
        Kind = kind;
        knockbackScale = EnemyKinds.KnockbackScale(kind);
        tint = EnemyKinds.Tint(kind);
        emission = EnemyKinds.Glow(kind);
        ApplyTint();
    }

    // Overrides its look, for bosses (which aren't one of the normal kinds).
    public void SetLook(Color? bodyTint, Color glow)
    {
        tint = bodyTint;
        emission = glow;
        ApplyTint();
    }

    // Changes how much it glows, e.g. a boss lighting up as it winds up an attack.
    public void SetGlow(Color glow)
    {
        emission = glow;

        if (flashTimer <= 0f)
            ApplyTint();
    }

    // The renderers its colour, glow and hit flash go on: its block, or the model that replaced
    // it (CharacterVisuals).
    // Its radius from its collider, again after the collider changes (CharacterVisuals.FitHitbox).
    public void RefreshBodyRadius()
    {
        Collider body = GetComponent<Collider>();

        if (body != null)
            bodyRadius = Mathf.Max(body.bounds.extents.x, body.bounds.extents.z);
    }

    // Drawn as a sprite (CharacterVisuals.AttachSpriteToEnemy): the art has its own colours, so
    // the kind's tint isn't laid over it, glows show faintly, and a hit flashes it white.
    public bool SpriteBody { get; set; }

    private const float SpriteGlow = 0.3f;
    private static readonly Color SpriteFlash = new Color(0.8f, 0.8f, 0.8f);

    public void SetBodyRenderers(Renderer[] renderers)
    {
        bodyRenderers = renderers ?? new Renderer[0];
        ApplyTint();
    }

    private void ApplyTint()
    {
        if (SpriteBody)
        {
            flashBlock ??= new MaterialPropertyBlock();
            flashBlock.SetColor("_BaseColor", Color.white);
            flashBlock.SetColor("_EmissionColor", emission * SpriteGlow);

            foreach (Renderer renderer in bodyRenderers)
                renderer.SetPropertyBlock(flashBlock);

            return;
        }

        if (tint == null)
        {
            foreach (Renderer renderer in bodyRenderers)
                renderer.SetPropertyBlock(null);

            return;
        }

        flashBlock ??= new MaterialPropertyBlock();
        flashBlock.SetColor("_BaseColor", tint.Value);
        flashBlock.SetColor("_EmissionColor", emission);

        foreach (Renderer renderer in bodyRenderers)
            renderer.SetPropertyBlock(flashBlock);
    }

    // Knocked back along a velocity (scaled down for heavy kinds), and staggered: it doesn't
    // steer for that long, sliding to a stop instead. Bosses (Heavy) take far less of both, and
    // ignore blows for a few seconds after one staggers them.
    public void Knock(Vector3 velocity, float stagger)
    {
        if (health <= 0)
            return;

        velocity.y = 0f;

        if (Heavy)
        {
            // Still recovering from the last flinch: the blow lands but doesn't move it.
            if (Time.time < staggerImmuneUntil)
                return;

            velocity *= HeavyKnockback;
            stagger *= HeavyStagger;
            staggerImmuneUntil = Time.time + stagger + HeavyStaggerCooldown;
        }

        rb.linearVelocity = velocity * knockbackScale + Vector3.up * rb.linearVelocity.y;
        staggerUntil = Mathf.Max(staggerUntil, Time.time + stagger * Mathf.Lerp(0.4f, 1f, knockbackScale));

        if (IsStaggered)
            attackTime = -1f;
    }

    // Touching the player hurts them (bosses only; zombies swing); PlayerHealth's invulnerability
    // window stops this from landing every physics step while they stay in contact.
    private void OnCollisionStay(Collision collision)
    {
        if (room == null || !room.IsLocked || SwingAttacks)
            return;

        if (collision.collider.TryGetComponent(out PlayerHealth playerHealth))
            playerHealth.TakeDamage(contactDamage, transform.position);
    }

    // Runs a whole swing once started, landing its blow(s) on time, and starts another while the
    // player stays close.
    private void UpdateAttack()
    {
        if (attackTime < 0f)
        {
            if (WithinReach(AttackReach) && state == State.Chase && !IsStaggered && !ExternalControl && health > 0)
            {
                attackTime = 0f;
                strikesDone = 0;
            }

            return;
        }

        float period = AttackPeriodOverride > 0f ? AttackPeriodOverride : EnemyKinds.AttackPeriod(Kind);
        attackTime += Time.fixedDeltaTime / period;
        float[] strikes = EnemyKinds.StrikeTimes(Kind);

        while (strikesDone < strikes.Length && attackTime >= strikes[strikesDone])
        {
            strikesDone++;

            if (WithinReach(HitReach) && target.TryGetComponent(out PlayerHealth playerHealth))
                playerHealth.TakeDamage(contactDamage, transform.position);
        }

        if (attackTime >= 1f)
            attackTime = -1f;
    }

    // Whether the player is within this distance of its edge.
    private bool WithinReach(float reach)
    {
        return target != null && FlatDistance(rb.position, target.position) < bodyRadius + reach;
    }

    private void Update()
    {
        if (flashTimer <= 0f)
            return;

        flashTimer -= Time.deltaTime;

        if (flashTimer <= 0f)
            ApplyTint();
    }

    private void FixedUpdate()
    {
        // Dormant until the player is locked in with it.
        if (room == null || !room.IsLocked || target == null)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        UpdateSenses();

        if (SwingAttacks)
            UpdateAttack();

        if (ExternalControl)
            return;

        // Reeling from a blow: slide to a stop rather than steer.
        if (IsStaggered)
        {
            Vector3 flat = Flat(rb.linearVelocity);
            flat = Vector3.MoveTowards(flat, Vector3.zero, 18f * Time.fixedDeltaTime);
            rb.linearVelocity = flat + Vector3.up * rb.linearVelocity.y;
            return;
        }

        Vector3 destination;
        float speed;

        if (!ChooseDestination(out destination, out speed))
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            stuckFor = 0f;
            return;
        }

        Vector3 toDestination = NextWaypoint(destination) - rb.position;
        toDestination.y = 0f;

        if (toDestination.sqrMagnitude < 0.0001f)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        Vector3 direction = Steer(toDestination.normalized);
        direction = Unstick(direction);

        rb.linearVelocity = direction * speed * SpeedMultiplier + Vector3.up * rb.linearVelocity.y;
    }

    // ---------- Senses ----------

    private void UpdateSenses()
    {
        if (Time.time >= nextSightCheck)
        {
            nextSightCheck = Time.time + sightInterval;
            canSee = HasLineOfSight();

            // Only a successful check counts as a sighting, so what's remembered is where the
            // player actually was when last seen, not wherever they got to before the next check.
            if (canSee)
                RecordSighting();
        }

        if (canSee)
        {
            bool spotted = state != State.Chase;
            state = State.Chase;

            if (spotted)
                SpottedPlayer?.Invoke(this);
        }
        else if (state == State.Chase)
        {
            state = State.Search;
            forgetAt = lastSeenTime + Senses.maxSearchTime;
        }

        if (state == State.Search && Time.time > forgetAt)
            state = State.Wander;
    }

    private void RecordSighting()
    {
        lastSeenPosition = Flat(target.position);
        lastSeenTime = Time.time;
        reachedLastSeen = false;
        hasWanderPoint = false;

        // Which way they were heading, if they were moving at all; standing still leaves no
        // trail to follow.
        Vector3 velocity = targetBody != null ? Flat(targetBody.linearVelocity) : Vector3.zero;
        lastSeenHeading = velocity.sqrMagnitude > 0.25f ? velocity.normalized : Vector3.zero;
    }

    // Within sight range, with a clear line from eye height to the player through nothing
    // tagged as a wall (walls, closed doors and pillars). Other enemies and pickups don't block it.
    private bool HasLineOfSight()
    {
        // A sneaking player is harder to pick out, so has to be closer to be seen.
        float range = Senses.SightRangeFor(targetMovement != null && targetMovement.IsSneaking) * SightRangeMultiplier;

        if (FlatDistance(rb.position, target.position) > range)
            return false;

        Vector3 from = Flat(rb.position) + Vector3.up * EyeHeight;
        Vector3 to = Flat(target.position) + Vector3.up * EyeHeight;
        return !WallBetween(from, to);
    }

    private static bool WallBetween(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;

        if (distance < 0.001f)
            return false;

        int count = Physics.RaycastNonAlloc(from, delta / distance, hits, distance, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.CompareTag("Wall"))
                return true;
        }

        return false;
    }

    // ---------- Decisions ----------

    // Where to head for now, and how fast. False means stand still.
    private bool ChooseDestination(out Vector3 destination, out float speed)
    {
        speed = moveSpeed;

        switch (state)
        {
            case State.Chase:
                destination = Flat(target.position);
                return true;

            case State.Search:
                if (!reachedLastSeen)
                {
                    if (FlatDistance(rb.position, lastSeenPosition) > ArriveDistance)
                    {
                        destination = lastSeenPosition;
                        return true;
                    }

                    // At the corner they went round: carry on the way they were going, as far
                    // as the room allows, for a short while.
                    reachedLastSeen = true;
                    forgetAt = Mathf.Min(forgetAt, Time.time + Senses.memoryTime);
                    searchPoint = FurthestClearPoint(lastSeenPosition, lastSeenHeading, Senses.followDistance);
                }

                if (FlatDistance(rb.position, searchPoint) > ArriveDistance)
                {
                    destination = searchPoint;
                    return true;
                }

                // Followed the trail to its end without spotting them: give up early.
                state = State.Wander;
                break;
        }

        return Wander(out destination, out speed);
    }

    private bool Wander(out Vector3 destination, out float speed)
    {
        speed = moveSpeed * wanderSpeedFraction;
        destination = rb.position;

        if (Time.time < wanderWaitUntil)
            return false;

        bool arrived = hasWanderPoint && FlatDistance(rb.position, wanderPoint) < 0.5f;

        if (!hasWanderPoint || arrived || Time.time > wanderGiveUpAt)
        {
            if (arrived)
            {
                // A pause before setting off again.
                hasWanderPoint = false;
                wanderWaitUntil = Time.time + UnityEngine.Random.Range(wanderPause.x, wanderPause.y);
                return false;
            }

            hasWanderPoint = TryPickWanderPoint(out wanderPoint);
            wanderGiveUpAt = Time.time + 5f;

            if (!hasWanderPoint)
            {
                wanderWaitUntil = Time.time + 0.5f;
                return false;
            }
        }

        destination = wanderPoint;
        return true;
    }

    // A random spot on the room's floor, away from the walls, that's on the navmesh (so not
    // inside a pillar). The route there is worked out when walking it.
    private bool TryPickWanderPoint(out Vector3 point)
    {
        point = rb.position;

        if (room == null || room.Blocks.Count == 0)
            return false;

        float margin = bodyRadius + 0.8f;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Rect block = room.Blocks[UnityEngine.Random.Range(0, room.Blocks.Count)];

            if (block.width <= margin * 2f || block.height <= margin * 2f)
                continue;

            Vector3 candidate = new Vector3(
                UnityEngine.Random.Range(block.xMin + margin, block.xMax - margin),
                0f,
                UnityEngine.Random.Range(block.yMin + margin, block.yMax - margin));

            if (FlatDistance(candidate, rb.position) < 2f)
                continue;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            {
                point = Flat(hit.position);
                return true;
            }
        }

        return false;
    }

    // From `from`, up to `distance` along `heading`, stopping where the navmesh ends (at a
    // wall or pillar).
    private Vector3 FurthestClearPoint(Vector3 from, Vector3 heading, float distance)
    {
        if (heading.sqrMagnitude < 0.01f)
            return from;

        Vector3 end = Flat(from) + heading * distance;

        if (!NavMesh.SamplePosition(from, out NavMeshHit start, 1.5f, NavMesh.AllAreas))
            return end;

        return NavMesh.Raycast(start.position, end, out NavMeshHit hit, NavMesh.AllAreas) ? Flat(hit.position) : end;
    }

    // ---------- Navigation ----------

    // The next point to head for on the way to `destination`: the next corner of a navmesh
    // route there, or the destination itself if there's no route (no navmesh, or off it).
    private Vector3 NextWaypoint(Vector3 destination)
    {
        destination = Flat(destination);

        if (Time.time >= nextRepath || (destination - pathGoal).sqrMagnitude > 1f)
            Repath(destination);

        // Skip corners already reached.
        while (corner < corners.Length && FlatDistance(rb.position, corners[corner]) < 0.4f)
            corner++;

        return corner < corners.Length ? Flat(corners[corner]) : destination;
    }

    private void Repath(Vector3 destination)
    {
        nextRepath = Time.time + RepathInterval;
        pathGoal = destination;
        corners = new Vector3[0];
        corner = 0;

        if (!NavMesh.SamplePosition(Flat(rb.position), out NavMeshHit from, 1.5f, NavMesh.AllAreas)
            || !NavMesh.SamplePosition(destination, out NavMeshHit to, 1.5f, NavMesh.AllAreas))
        {
            return;
        }

        path ??= new NavMeshPath();

        if (NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) && path.status != NavMeshPathStatus.PathInvalid)
        {
            corners = path.corners;
            corner = corners.Length > 1 ? 1 : 0;   // the first corner is where it's standing
        }
    }

    // ---------- Movement ----------

    // If a wall is right ahead, turn to run along it instead of into it, so the enemy slides
    // around corners rather than grinding against them.
    private Vector3 Steer(Vector3 direction)
    {
        Vector3 eye = Flat(rb.position) + Vector3.up * EyeHeight;
        float probe = bodyRadius + 0.35f;

        if (!Physics.SphereCast(eye, bodyRadius * 0.8f, direction, out RaycastHit hit, probe, ~0, QueryTriggerInteraction.Ignore)
            || !hit.collider.CompareTag("Wall"))
        {
            return direction;
        }

        Vector3 normal = Flat(hit.normal).normalized;
        Vector3 along = direction - normal * Vector3.Dot(direction, normal);

        // Heading straight into it: pick a side.
        if (along.sqrMagnitude < 0.01f)
            along = Vector3.Cross(Vector3.up, normal);

        return along.normalized;
    }

    // Wedged (trying to move but barely going anywhere): side-step for a moment.
    private Vector3 Unstick(Vector3 direction)
    {
        if (Time.time < sidestepUntil)
            return sidestep;

        float speed = Flat(rb.linearVelocity).magnitude;
        stuckFor = speed < StuckSpeed ? stuckFor + Time.fixedDeltaTime : 0f;

        // Up against the player counts as arrived, not stuck.
        bool touchingPlayer = state == State.Chase && FlatDistance(rb.position, target.position) < bodyRadius + 0.8f;

        if (stuckFor < StuckTime || touchingPlayer)
            return direction;

        stuckFor = 0f;
        float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        sidestep = (Vector3.Cross(Vector3.up, direction) * side - direction * 0.3f).normalized;
        sidestepUntil = Time.time + 0.35f;
        hasWanderPoint = false;
        return sidestep;
    }

    private static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }

    // ---------- Health ----------

    public void TakeDamage(int amount)
    {
        // Can't be hurt until the player is locked in with it, so a room can't be cleared
        // by shooting through an open door.
        if (health <= 0 || room == null || !room.IsLocked)
            return;

        health -= Mathf.Max(1, Mathf.RoundToInt(amount * DamageMultiplier));
        Flash();

        // Being shot gives the shooter away, even from out of sight.
        if (state != State.Chase && target != null)
            Investigate(target.position);

        if (health <= 0)
        {
            Die();
            return;
        }

        Damaged?.Invoke(this);
    }

    // Alive and locked in with the player, so it can be hurt.
    public bool IsActive => room != null && room.IsLocked && health > 0;

    // Dies straight away, whether or not its room is locked. For the debug menu.
    public void Kill()
    {
        if (health <= 0)
            return;

        health = 0;
        Die();
    }

    // Gone, leaving its body on the floor for a while (sprite art: Corpse).
    private void Die()
    {
        Died?.Invoke(this);

        if (SpriteBody)
            Corpse.Leave(this);

        Destroy(gameObject);
    }

    private void Flash()
    {
        flashBlock ??= new MaterialPropertyBlock();
        flashBlock.SetColor("_BaseColor", Color.white);
        flashBlock.SetColor("_EmissionColor", SpriteBody ? SpriteFlash : emission);

        foreach (Renderer renderer in bodyRenderers)
            renderer.SetPropertyBlock(flashBlock);

        flashTimer = hitFlashTime;
    }
}
