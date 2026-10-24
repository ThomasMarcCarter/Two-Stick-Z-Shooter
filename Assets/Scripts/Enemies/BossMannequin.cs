using UnityEngine;

// The Mannequin (the Shopping Mall's floor 2 boss): a shop-window dummy that only moves while it
// isn't watched. While the player aims at it (it's within WatchAngle of where they face and in
// sight) it freezes mid-step and can be shot; the moment they look away it rushes at them, faster
// than any zombie. Every so often, while unwatched, it twitches (BossMove.Vanish), drops out of
// sight, and a pale disc on the floor behind the player shows where it'll step out a moment
// later. Below half health it rushes faster and vanishes more often. Keeping it in your sights
// while dealing with everything else is the fight.
[RequireComponent(typeof(Enemy))]
public class BossMannequin : MonoBehaviour, IBossMoves
{
    private const float WatchAngle = 28f;          // degrees either side of the player's aim
    private const float RushMultiplier = 2f;       // speed when unwatched
    private const float EnragedRushMultiplier = 2.6f;
    private const float VanishWindUp = 0.6f;
    private const float AppearDelay = 0.9f;        // the marker's warning before it steps out
    private const float BehindDistance = 3.2f;
    private static readonly Vector2 VanishInterval = new Vector2(9f, 13f);
    private static readonly Color AppearColour = new Color(1f, 0.92f, 0.8f);

    private Enemy enemy;
    private float vanishAt = -1f;
    private float nextVanish;
    private bool hidden;
    private Color restGlow;
    private Vector3 baseScale;

    private bool Enraged => enemy.Health <= enemy.MaxHealth / 2;

    // Frozen: the player has it in their sights.
    public bool Watched { get; private set; }

    public BossMove Move => vanishAt >= 0f ? BossMove.Vanish : BossMove.None;
    public float MoveProgress => vanishAt >= 0f ? Mathf.Clamp01(1f - (vanishAt - Time.time) / VanishWindUp) : 0f;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        baseScale = transform.localScale;
        nextVanish = Time.time + 7f;
    }

    private void Start()
    {
        restGlow = Bosses.Glow(BossKind.Mannequin);
    }

    private void Update()
    {
        if (!enemy.IsActive || enemy.Target == null || hidden)
            return;

        Watched = IsWatched();

        if (vanishAt >= 0f)
        {
            // Twitching: jerks about, then gone.
            float jerk = Mathf.Sin(Time.time * 55f) * 0.06f;
            transform.localScale = new Vector3(baseScale.x * (1f + jerk), baseScale.y, baseScale.z * (1f - jerk));

            if (Time.time >= vanishAt)
                Vanish();

            return;
        }

        // Frozen stiff while watched; a rush when not.
        enemy.SpeedMultiplier = Watched ? 0f : Enraged ? EnragedRushMultiplier : RushMultiplier;
        enemy.SetGlow(Watched ? restGlow * 0.5f : restGlow);

        float interval = Enraged ? 0.7f : 1f;

        if (!Watched && Time.time >= nextVanish && enemy.CanSeeTarget)
        {
            vanishAt = Time.time + VanishWindUp;
            nextVanish = Time.time + Random.Range(VanishInterval.x, VanishInterval.y) * interval;
            enemy.ExternalControl = true;
            ScreamSound.PlayWarbledAt(transform.position, 0.4f, 1.6f);
        }
    }

    private void FixedUpdate()
    {
        if (!hidden && (Watched || vanishAt >= 0f))
            enemy.Body.linearVelocity = Vector3.zero;
    }

    // Well inside one of its room's floor blocks (not in a doorway or a wall).
    private bool InsideRoom(Vector3 point)
    {
        if (enemy.Room == null)
            return false;

        foreach (Rect block in enemy.Room.Blocks)
        {
            if (point.x > block.xMin + 1f && point.x < block.xMax - 1f && point.z > block.yMin + 1f && point.z < block.yMax - 1f)
                return true;
        }

        return false;
    }

    // In the player's sights: roughly where they face, and with nothing in the way.
    private bool IsWatched()
    {
        Transform player = enemy.Target;
        Vector3 toBoss = Bosses.Flat(transform.position - player.position);

        if (toBoss.sqrMagnitude < 0.01f)
            return true;

        return enemy.CanSeeTarget && Vector3.Angle(Bosses.Flat(player.forward), toBoss) <= WatchAngle;
    }

    // Drops out of sight; a pale disc behind the player marks where it'll step out.
    private void Vanish()
    {
        vanishAt = -1f;
        hidden = true;
        transform.localScale = baseScale;
        SetVisible(false);

        Transform player = enemy.Target;
        Vector3 behind = Bosses.Flat(player.position - Bosses.Flat(player.forward).normalized * BehindDistance);
        Vector3 spot = UnityEngine.AI.NavMesh.SamplePosition(behind, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas)
            && InsideRoom(hit.position)
            ? Bosses.Flat(hit.position)
            : Bosses.SpawnPointInRoom(enemy, player.position, 3f);

        FloorMarker.Disc(spot, 0.8f, AppearColour, AppearDelay, enemy.Room != null ? enemy.Room.transform : null, () => Appear(spot));
    }

    private void Appear(Vector3 spot)
    {
        if (this == null || enemy == null || enemy.Health <= 0)
            return;

        enemy.Body.position = spot + Vector3.up * transform.position.y;
        hidden = false;
        enemy.ExternalControl = false;
        SetVisible(true);
        enemy.Body.linearVelocity = Vector3.zero;
        CameraFollow.Shake(0.15f);
        ScreamSound.PlayWarbledAt(spot, 0.6f, 1.3f);
    }

    // Its sprite (or model) and shadow, and whether it can be hit while gone.
    private void SetVisible(bool visible)
    {
        foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
        {
            if (part.gameObject != gameObject)
                part.enabled = visible;
        }

        foreach (Collider part in GetComponents<Collider>())
            part.enabled = visible;

        // With its collider off it'd drop through the floor, so it's held still while gone.
        enemy.Body.isKinematic = !visible;
    }
}
