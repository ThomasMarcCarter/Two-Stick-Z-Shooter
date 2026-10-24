using System;
using System.Collections;
using UnityEngine;

// The Captain (the Airport's floor 2 boss): the pilot, calling in what's left of the sky. Every
// few seconds, while it can see the player, it raises a flare (BossMove.Howl): discs glow on the
// floor round and under the player (FloorMarker), and wreckage crashes down on each a moment later
// (2 damage to anyone still in one, a shake, and boxes smashed). Below half health it marks more
// discs and passengers (a runner each time) stagger in at the room's edges. Keep moving, and hit it
// while it holds the flare up.
[RequireComponent(typeof(Enemy))]
public class BossCaptain : MonoBehaviour, IBossMoves
{
    private const float CallRange = 14f;
    private const float WindUp = 0.8f;
    private const float FallDelay = 1.3f;
    private const float StrikeRadius = 1.4f;
    private static readonly Vector2 CallInterval = new Vector2(4f, 5.5f);
    private static readonly Color FlareColour = new Color(1f, 0.35f, 0.25f);

    private Enemy enemy;
    private Func<Vector3, EnemyKind, Enemy> spawnMinion;
    private float callAt = -1f;
    private float nextCall;
    private Color restGlow;

    private bool Enraged => enemy.Health <= enemy.MaxHealth / 2;

    public BossMove Move => callAt >= 0f ? BossMove.Howl : BossMove.None;
    public float MoveProgress => callAt >= 0f ? Mathf.Clamp01(1f - (callAt - Time.time) / WindUp) : 0f;

    public void Configure(Func<Vector3, EnemyKind, Enemy> spawn)
    {
        spawnMinion = spawn;
    }

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        nextCall = Time.time + 3f;
    }

    private void Start()
    {
        restGlow = Bosses.Glow(BossKind.Captain);
    }

    private void Update()
    {
        if (!enemy.IsActive || enemy.Target == null)
            return;

        if (callAt >= 0f)
        {
            enemy.SetGlow(Color.Lerp(restGlow, FlareColour * 2.5f, 1f - (callAt - Time.time) / WindUp));

            if (Time.time >= callAt)
                Call();

            return;
        }

        if (Time.time >= nextCall && enemy.CanSeeTarget && Bosses.FlatDistance(transform.position, enemy.Target.position) <= CallRange)
        {
            callAt = Time.time + WindUp;
            enemy.ExternalControl = true;
            ScreamSound.PlayWarbledAt(transform.position, 0.5f, 1.2f);
        }
    }

    private void FixedUpdate()
    {
        if (callAt >= 0f)
            enemy.Body.linearVelocity = Vector3.zero;
    }

    // Discs round the player (one right under them), wreckage on each.
    private void Call()
    {
        callAt = -1f;
        nextCall = Time.time + UnityEngine.Random.Range(CallInterval.x, CallInterval.y) * (Enraged ? 0.75f : 1f);
        enemy.ExternalControl = false;
        enemy.SetGlow(restGlow);

        Vector3 centre = Bosses.Flat(enemy.Target.position);
        int count = Enraged ? 6 : 4;
        Transform room = enemy.Room != null ? enemy.Room.transform : null;

        for (int i = 0; i < count; i++)
        {
            Vector3 spot = i == 0 ? centre : centre + Quaternion.Euler(0f, i * 360f / (count - 1) + UnityEngine.Random.Range(-20f, 20f), 0f) * Vector3.forward * UnityEngine.Random.Range(2.2f, 3.6f);
            FloorMarker.Disc(spot, StrikeRadius, FlareColour, FallDelay, room, null);
            StartCoroutine(Fall(spot, FallDelay));
        }

        if (Enraged && spawnMinion != null)
            Bosses.CallMinion(enemy, Bosses.SpawnPointInRoom(enemy, enemy.Target.position, 5f), EnemyKind.Runner, spawnMinion, FlareColour);
    }

    // A dark lump falling the last moment before the strike, then the impact.
    private IEnumerator Fall(Vector3 spot, float delay)
    {
        const float dropTime = 0.35f;
        yield return new WaitForSeconds(delay - dropTime);

        GameObject wreck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wreck.name = "Wreckage";
        Destroy(wreck.GetComponent<Collider>());
        wreck.transform.localScale = new Vector3(0.9f, 0.5f, 0.7f);
        wreck.transform.rotation = UnityEngine.Random.rotation;
        Renderer renderer = wreck.GetComponent<Renderer>();

        if (ConsumablePickup.DefaultMaterial != null)
        {
            renderer.sharedMaterial = ConsumablePickup.DefaultMaterial;
            ConsumableModel.Tint(renderer, new Color(0.3f, 0.3f, 0.32f), new Color(1f, 0.4f, 0.1f) * 0.5f);
        }

        for (float t = 0f; t < dropTime; t += Time.deltaTime)
        {
            wreck.transform.position = spot + Vector3.up * Mathf.Lerp(9f, 0.3f, t / dropTime);
            yield return null;
        }

        Destroy(wreck);
        CameraFollow.Shake(0.25f);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("explosion", 3, ProceduralAudio.Explosion)), spot, 0.6f, 1.2f);

        foreach (Collider part in Physics.OverlapSphere(spot + Vector3.up * 0.5f, StrikeRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (part.attachedRigidbody != null && part.attachedRigidbody.TryGetComponent(out PlayerHealth player))
                player.TakeDamage(2, spot);
            else if (part.attachedRigidbody == null || part.attachedRigidbody.GetComponent<Enemy>() == null)
                Smashable.Break(part, part.ClosestPoint(spot), Bosses.Flat(part.transform.position - spot).normalized);
        }
    }
}
