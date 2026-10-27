using System.Collections;
using UnityEngine;

// The Hazmat (the Hospital's floor 3 boss): a quarantine officer in a sealed suit, slow and
// heavy. Every few seconds, while it can see the player, it raises its sprayer, a yellow-green
// wedge glowing on the floor ahead of it (FloorMarker), and floods the wedge with toxic gas that
// lingers and burns (acid pools; its suit keeps its own gas out). Below half health it sprays
// more often and leaves a trail of gas behind it as it walks. Touching it hurts (no swings).
// Keeping out of its wedges, and its trail, is the fight.
[RequireComponent(typeof(Enemy))]
public class BossHazmat : MonoBehaviour, IBossMoves
{
    private const float SprayRange = 8f;
    private const float SprayWindUp = 1f;
    private const float SprayLength = 7f;
    private const float SprayArc = 50f;
    private static readonly Vector2 SprayInterval = new Vector2(4.5f, 6f);
    private const float TrailEvery = 1.3f;
    private static readonly Color GasColour = new Color(0.7f, 1f, 0.25f);

    private Enemy enemy;
    private float sprayAt = -1f;
    private float nextSpray;
    private float nextTrail;
    private Vector3 direction;
    private Color restGlow;

    private bool Enraged => enemy.Health <= enemy.MaxHealth / 2;

    public BossMove Move => sprayAt >= 0f ? BossMove.Spit : BossMove.None;
    public float MoveProgress => sprayAt >= 0f ? Mathf.Clamp01(1f - (sprayAt - Time.time) / SprayWindUp) : 0f;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        nextSpray = Time.time + 3f;
    }

    private void Start()
    {
        restGlow = Bosses.Glow(BossKind.Hazmat);
    }

    private void Update()
    {
        if (!enemy.IsActive || enemy.Target == null)
            return;

        Transform room = enemy.Room != null ? enemy.Room.transform : null;

        if (sprayAt >= 0f)
        {
            enemy.SetGlow(Color.Lerp(restGlow, GasColour * 2.5f, 1f - (sprayAt - Time.time) / SprayWindUp));
            return;
        }

        // Badly hurt, its suit leaks: a trail of gas behind it.
        if (Enraged && Time.time >= nextTrail)
        {
            nextTrail = Time.time + TrailEvery;
            AcidPool.Create(Bosses.Flat(transform.position), room);
        }

        if (Time.time >= nextSpray && enemy.CanSeeTarget && Bosses.FlatDistance(transform.position, enemy.Target.position) <= SprayRange)
        {
            sprayAt = Time.time + SprayWindUp;
            direction = Bosses.Flat(enemy.Target.position - transform.position).normalized;
            enemy.ExternalControl = true;
            enemy.FacingOverride = direction;
            SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("bloater.swell", 2, ProceduralAudio.BloaterSwell)), transform.position, 0.8f, 1.4f);
            FloorMarker.Sector(Bosses.Flat(transform.position), direction, SprayLength, SprayArc, GasColour, SprayWindUp, room,
                () => { if (this != null && sprayAt >= 0f) Spray(); });
        }
    }

    private void FixedUpdate()
    {
        if (sprayAt >= 0f)
            enemy.Body.linearVelocity = Vector3.zero;
    }

    // Gas over the wedge: rows of pools fanning out from the nozzle.
    private void Spray()
    {
        sprayAt = -1f;
        nextSpray = Time.time + Random.Range(SprayInterval.x, SprayInterval.y) * (Enraged ? 0.65f : 1f);
        enemy.ExternalControl = false;
        enemy.FacingOverride = null;
        enemy.SetGlow(restGlow);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("fire.loop", 1, i => ProceduralAudio.FireLoop())), transform.position, 0.6f, 1.6f);
        StartCoroutine(Flood(Bosses.Flat(transform.position), direction));
    }

    private IEnumerator Flood(Vector3 from, Vector3 along)
    {
        Transform room = enemy.Room != null ? enemy.Room.transform : null;
        Vector3 across = Vector3.Cross(Vector3.up, along);

        for (float d = 1.6f; d <= SprayLength; d += 1.8f)
        {
            // Wider the further it goes, within the wedge.
            float half = Mathf.Tan(SprayArc * 0.5f * Mathf.Deg2Rad) * d * 0.8f;
            int count = Mathf.Max(1, Mathf.RoundToInt(half * 2f / 1.8f) + 1);

            for (int i = 0; i < count; i++)
            {
                float offset = count == 1 ? 0f : Mathf.Lerp(-half, half, i / (float)(count - 1));
                Vector3 point = from + along * d + across * offset;

                if (!Physics.Linecast(from + Vector3.up * 0.5f, point + Vector3.up * 0.5f, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
                    || !hit.collider.CompareTag("Wall"))
                    AcidPool.Create(point, room);
            }

            yield return new WaitForSeconds(0.08f);
        }
    }
}
