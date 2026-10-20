using UnityEngine;

// A bullet. Drawn as a glowing tracer streak rather than a solid round: a short trail, hot and
// wide at the bullet and fading to nothing behind it, so it reads as a motion-blurred blur of
// light. The round's own mesh is hidden (its collider still does the hitting). On impact the
// streak is left behind to fade out instead of vanishing.
public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private int damage = 1;

    [Header("Tracer")]
    [Tooltip("Soft-edged streak material. Without one the round's mesh is shown instead.")]
    [SerializeField] private Material tracerMaterial;
    [SerializeField] private Color tracerColour = new Color(1f, 0.85f, 0.45f);
    [Tooltip("Width at the bullet, tapering to nothing at the tail.")]
    [SerializeField] private float tracerWidth = 0.09f;
    [Tooltip("How long the streak is, in seconds of flight (so faster rounds draw longer streaks).")]
    [SerializeField] private float tracerTime = 0.07f;

    [Header("Impact Effects")]
    [Tooltip("Sparks off walls (tinted bright, so they bloom).")]
    [SerializeField] private Material sparkMaterial;
    [Tooltip("Blood spray off zombies.")]
    [SerializeField] private Material bloodMaterial;
    [Tooltip("Scorch marks left on whatever was hit.")]
    [SerializeField] private Material scorchMaterial;

    [Header("Impact Sounds")]
    [SerializeField, Range(0f, 1f)] private float wallImpactVolume = 0.35f;
    [Tooltip("Chance a wall hit has a ricochet whine.")]
    [SerializeField, Range(0f, 1f)] private float ricochetChance = 0.25f;

    // Hits closer together than this make one sound, so a shotgun blast is one big hit
    // rather than eight stacked ones.
    private const float ImpactInterval = 0.03f;

    private Rigidbody rb;
    private TrailRenderer tracer;
    private Vector3 lastPosition;
    private Transform shooterRoot;
    private bool spent;
    private float tracerLift;

    private static readonly RaycastHit[] sweepHits = new RaycastHit[8];

    private void Awake()
    {
        if (tracerMaterial == null)
            return;

        // The streak replaces the round's mesh; the collider stays.
        foreach (MeshRenderer part in GetComponentsInChildren<MeshRenderer>())
            part.enabled = false;

        // On a child, so it can be left behind on impact. Trail widths are in world units, so
        // the round's own (non-uniform) scale doesn't matter.
        GameObject streak = new GameObject("Tracer");
        streak.transform.SetParent(transform, false);

        tracer = streak.AddComponent<TrailRenderer>();
        tracer.sharedMaterial = tracerMaterial;
        tracer.time = tracerTime;
        tracer.minVertexDistance = 0.05f;
        tracer.widthMultiplier = tracerWidth;
        tracer.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        tracer.textureMode = LineTextureMode.Stretch;
        tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tracer.receiveShadows = false;
        tracer.numCapVertices = 2;

        // White-hot at the bullet, the round's colour along the streak, gone at the tail.
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.Lerp(Color.white, tracerColour, 0.35f), 0f), new GradientColorKey(tracerColour, 0.4f), new GradientColorKey(tracerColour, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
        tracer.colorGradient = gradient;
    }

    // The bullet's about to be destroyed: let its streak finish fading where it is.
    private void LeaveTracer()
    {
        if (tracer == null)
            return;

        tracer.transform.SetParent(null, true);
        tracer.emitting = false;
        tracer.autodestruct = true;
        tracer = null;
    }

    // bonusDamage: extra damage from the gun's attachments (a match barrel). tracerLift: how far
    // above the round its streak is drawn, so it comes out of a gun held higher than shots fly
    // (see Gun.ShotHeight and Gun.AimHeight). Wall and blood impacts are drawn at that height too,
    // where the streak ends.
    public void Initialize(Vector3 direction, GameObject shooter, float speedMultiplier = 1f, int bonusDamage = 0, float tracerLift = 0f)
    {
        damage += bonusDamage;
        this.tracerLift = tracerLift;

        if (tracer != null)
            tracer.transform.position = transform.position + Vector3.up * tracerLift;

        rb = GetComponent<Rigidbody>();

        Collider projectileCollider = GetComponent<Collider>();
        Collider shooterCollider = shooter.GetComponent<Collider>();

        Physics.IgnoreCollision(projectileCollider, shooterCollider);
        shooterRoot = shooter.transform.root;

        rb.linearVelocity = direction.normalized * speed * speedMultiplier;
        lastPosition = transform.position;

        Destroy(gameObject, lifetime);
    }

    // Fast rounds can move further in one physics step than a wall is thick, and pass through
    // without touching it. So before each step, look along the stretch about to be covered and
    // take the hit there if something's in the way.
    private void FixedUpdate()
    {
        lastPosition = transform.position;

        if (rb == null || spent)
            return;

        Vector3 step = rb.linearVelocity * Time.fixedDeltaTime;
        float distance = step.magnitude;

        if (distance < 0.0001f)
            return;

        int count = Physics.RaycastNonAlloc(transform.position, step / distance, sweepHits, distance, ~0, QueryTriggerInteraction.Ignore);
        Collider nearest = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider hit = sweepHits[i].collider;

            if (hit.transform.root == shooterRoot || sweepHits[i].distance >= nearestDistance)
                continue;

            if (hit.CompareTag("Wall") || hit.GetComponent<Enemy>() != null || hit.GetComponentInParent<CardboardBox>() != null
                || hit.GetComponentInParent<Prop>() != null || hit.GetComponent<TrainingDummy>() != null)
            {
                nearest = hit;
                nearestDistance = sweepHits[i].distance;
            }
        }

        // Enemies drawn as sprites are hit where they look to be (SpriteCharacter.Footprint).
        SpriteCharacter sprite = NearestSprite(transform.position, step, distance, ref nearestDistance);

        if (sprite != null)
        {
            Vector3 point = transform.position + step / distance * nearestDistance;
            HitEnemy(sprite.Enemy, point, -step / distance);
            return;
        }

        if (nearest != null)
            Hit(nearest);
    }

    // The first sprite enemy whose footprint this step crosses before `nearestDistance`, which
    // it shortens to where it's crossed.
    private SpriteCharacter NearestSprite(Vector3 start, Vector3 step, float distance, ref float nearestDistance)
    {
        SpriteCharacter found = null;

        foreach (SpriteCharacter sprite in SpriteCharacter.Enemies)
        {
            if (sprite == null || sprite.Enemy == null || sprite.transform.root == shooterRoot)
                continue;

            if (!sprite.Footprint(start.y, out Vector3 from, out Vector3 to, out float radius))
                continue;

            float along = ClosestAlong(start, step, from, to, out float gap) * distance;

            if (gap < radius && along < nearestDistance)
            {
                nearestDistance = along;
                found = sprite;
            }
        }

        return found;
    }

    // Closest approach (across the floor) of the segment start..start+step to from..to: how far
    // along the first (0 to 1) and how far apart they are there.
    private static float ClosestAlong(Vector3 start, Vector3 step, Vector3 from, Vector3 to, out float gap)
    {
        Vector2 p = new Vector2(start.x, start.z), d1 = new Vector2(step.x, step.z);
        Vector2 q = new Vector2(from.x, from.z), d2 = new Vector2(to.x - from.x, to.z - from.z);
        Vector2 r = p - q;
        float a = Vector2.Dot(d1, d1), e = Vector2.Dot(d2, d2), f = Vector2.Dot(d2, r);
        float s, t;

        if (a < 1e-8f)
        {
            s = 0f;
            t = e > 1e-8f ? Mathf.Clamp01(f / e) : 0f;
        }
        else
        {
            float c = Vector2.Dot(d1, r);

            if (e < 1e-8f)
            {
                t = 0f;
                s = Mathf.Clamp01(-c / a);
            }
            else
            {
                float b = Vector2.Dot(d1, d2);
                float denominator = a * e - b * b;
                s = denominator > 1e-8f ? Mathf.Clamp01((b * f - c * e) / denominator) : 0f;
                t = (b * s + f) / e;

                if (t < 0f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else if (t > 1f)
                {
                    t = 1f;
                    s = Mathf.Clamp01((b - c) / a);
                }
            }
        }

        gap = Vector2.Distance(p + d1 * s, q + d2 * t);
        return s;
    }

    // Triggers don't say where they were touched, so trace back along the bullet's path to the
    // point and facing of the surface it went into. Falls back to the closest point on it.
    private void FindHit(Collider other, out Vector3 point, out Vector3 normal)
    {
        Vector3 travel = rb != null ? rb.linearVelocity : transform.forward;
        Vector3 direction = travel.sqrMagnitude > 0.0001f ? travel.normalized : transform.forward;
        Vector3 from = lastPosition - direction * 0.5f;
        // Far enough to reach a hit found by the sweep, up to a step ahead.
        float distance = Vector3.Distance(from, transform.position) + 1f + travel.magnitude * Time.fixedDeltaTime * 2f;

        if (other.Raycast(new Ray(from, direction), out RaycastHit hit, distance))
        {
            point = hit.point;
            normal = hit.normal;
            return;
        }

        point = other.ClosestPoint(transform.position);
        normal = -direction;
    }

    private ImpactEffects.Materials EffectMaterials => new ImpactEffects.Materials
    {
        Sparks = sparkMaterial,
        Blood = bloodMaterial,
        Scorch = scorchMaterial
    };

    private void OnTriggerEnter(Collider other)
    {
        Hit(other);
    }

    // Each round hits once, whether the sweep or the trigger finds the target first.
    private void Hit(Collider other)
    {
        if (spent)
            return;

        if (other.TryGetComponent(out Enemy enemy))
        {
            FindHit(other, out Vector3 point, out Vector3 normal);
            HitEnemy(enemy, point, normal);
            return;
        }

        if (other.TryGetComponent(out TrainingDummy dummy))
        {
            spent = true;
            FindHit(other, out Vector3 dummyPoint, out Vector3 _);
            dummy.Hit(damage, dummyPoint, rb != null ? rb.linearVelocity : transform.forward);
            LeaveTracer();
            Destroy(gameObject);
            return;
        }

        Prop prop = other.GetComponentInParent<Prop>();

        if (prop != null)
        {
            spent = true;
            FindHit(other, out Vector3 propPoint, out Vector3 propNormal);
            prop.Hit(propPoint, propNormal, rb != null ? rb.linearVelocity : transform.forward, EffectMaterials);
            LeaveTracer();
            Destroy(gameObject);
            return;
        }

        CardboardBox box = other.GetComponentInParent<CardboardBox>();

        if (box != null)
        {
            spent = true;
            FindHit(other, out Vector3 boxPoint, out Vector3 boxNormal);
            box.Hit(boxPoint, boxNormal, rb != null ? rb.linearVelocity : transform.forward, EffectMaterials);
            LeaveTracer();
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Wall"))
        {
            spent = true;
            FindHit(other, out Vector3 point, out Vector3 normal);
            ImpactEffects.WallHit(point + Vector3.up * tracerLift, normal, other.transform, EffectMaterials);

            bool ricochet = Random.value < ricochetChance;
            AudioClip[] clips = ricochet
                ? SoundFX.Clips("impact.ricochet", 3, i => ProceduralAudio.ImpactWall(100 + i, true))
                : SoundFX.Clips("impact.wall", 4, i => ProceduralAudio.ImpactWall(i, false));

            PlayImpact("impact.wall", clips, wallImpactVolume);
            LeaveTracer();
            Destroy(gameObject);
        }
    }

    // Blood where it went in (drawn up at the tracer's height), and the damage.
    private void HitEnemy(Enemy enemy, Vector3 point, Vector3 normal)
    {
        if (spent || enemy == null)
            return;

        ImpactEffects.FleshHit(point + Vector3.up * tracerLift, normal, rb != null ? rb.linearVelocity : transform.forward, enemy.transform, EffectMaterials);

        // The enemy's own voice reacts to the hit (EnemyVoice).
        spent = true;
        enemy.TakeDamage(damage);
        LeaveTracer();
        Destroy(gameObject);
    }

    private void PlayImpact(string key, AudioClip[] clips, float volume)
    {
        if (SoundFX.Throttled(key, ImpactInterval))
            return;

        SoundFX.PlayAt(SoundFX.Pick(clips), transform.position, volume, Random.Range(0.88f, 1.12f));
    }
}
