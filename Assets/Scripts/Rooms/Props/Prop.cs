using UnityEngine;
using UnityEngine.AI;

// A bit of room dressing from the trash pack. Pushable props (cans, bottles, bags, bricks...)
// have a light body you and the zombies knock about: they tip over and tumble, and long round
// things (cans, bottles, cups) roll. Bullets that hit them knock them flying. Fixed props (barriers, cinder blocks, tyres) stay put as cover that zombies path around.
// Some props break when shot (a glass bottle into a broken one, a cup crushed flat). Each has
// a surface, which sets what a hit throws off and sounds like. Anything that can tip and is
// still standing is knocked over when the player or a zombie walks into it. A boss's charge or
// acid (Smash) breaks it, throws it aside or, for fixed cover, wrecks it. A prop that bursts
// into litter (trash bags) splits open after its hits instead, scattering flat litter about.
public class Prop : MonoBehaviour
{
    // The floor's flat litter, for props that burst into it (set by the FloorGenerator).
    public static GameObject[] LitterPrefabs { get; set; } = new GameObject[0];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        LitterPrefabs = new GameObject[0];
    }

    public enum Surface
    {
        Paper,
        Plastic,
        Metal,
        Glass,
        Concrete,
        Rubber
    }

    [SerializeField] private Surface surface = Surface.Plastic;
    [Tooltip("Pushed about when walked into (otherwise it stays where it was placed).")]
    [SerializeField] private bool pushable = true;
    [Tooltip("Pushable only: weight, as a multiple of its size. The player weighs 1.")]
    [SerializeField] private float density = 0.6f;
    [Tooltip("What it turns into when shot (optional), and how many hits that takes.")]
    [SerializeField] private GameObject brokenVersion;
    [SerializeField, Min(1)] private int hitsToBreak = 1;
    [Tooltip("Without a broken version: splits open after hitsToBreak hits and scatters flat litter (trash bags).")]
    [SerializeField] private bool burstsIntoLitter;
    [SerializeField] private Vector2Int litterPieces = new Vector2Int(4, 7);
    [Tooltip("How hard a bullet shoves a pushable prop.")]
    [SerializeField] private float bulletImpulse = 0.6f;
    [Tooltip("Pushable only: free to tip over and tumble. Off keeps it upright (trash bags just slump along).")]
    [SerializeField] private bool canTip = true;
    [Tooltip("Pushable, tippable props: how hard a bump from the player or a zombie spins a standing one over.")]
    [SerializeField] private float toppleSpin = 9f;

    private int damage;
    private bool broken;
    private PhysicsMaterial physicsMaterial;
    private Rigidbody tipBody;

    // Set when a zone's item is built on this prop (ZoneClutter), so it sounds like what it is.
    public Surface Material { get => surface; set => surface = value; }

    // Once it's placed and sized: a body if it's pushable, a gap in the navmesh if it's big
    // enough for the zombies to need to go round it.
    public void Configure(PhysicsMaterial physics)
    {
        BoxCollider box = GetComponent<BoxCollider>();

        if (box == null)
            return;

        physicsMaterial = physics;

        if (physics != null)
            box.sharedMaterial = physics;

        Vector3 size = Vector3.Scale(box.size, transform.lossyScale);

        if (pushable)
        {
            Rigidbody body = GetComponent<Rigidbody>();

            if (body == null)
                body = gameObject.AddComponent<Rigidbody>();

            body.mass = Mathf.Clamp(size.x * size.y * size.z * density, 0.05f, 2f);
            body.interpolation = RigidbodyInterpolation.Interpolate;

            if (canTip)
            {
                tipBody = body;

                // Free to fall over and tumble; light damping so a shove carries it far enough
                // to topple, and round things keep rolling a little.
                body.linearDamping = 0.6f;
                body.angularDamping = 0.8f;
                body.constraints = RigidbodyConstraints.None;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                MakeRound(box, physics);
            }
            else
            {
                body.linearDamping = 2.5f;
                body.angularDamping = 3f;
                body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
        }

        if (!pushable || size.x * size.z > 0.6f)
        {
            NavMeshObstacle obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = box.center;
            obstacle.size = box.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }
    }

    // Walked into while standing: tipped over away from whoever bumped it.
    private void OnCollisionEnter(Collision collision)
    {
        if (tipBody == null || collision.rigidbody == null || Vector3.Dot(transform.up, Vector3.up) < 0.7f)
            return;

        if (collision.rigidbody.GetComponent<PlayerMovement>() == null && collision.rigidbody.GetComponent<Enemy>() == null)
            return;

        Vector3 push = transform.position - collision.rigidbody.position;
        push.y = 0f;

        if (push.sqrMagnitude < 0.0001f)
            return;

        tipBody.AddTorque(Vector3.Cross(Vector3.up, push.normalized) * toppleSpin, ForceMode.VelocityChange);
    }

    // A box collider tumbles from face to face; something long and round (a can, a bottle, a
    // cup) should roll, so it gets a capsule along its length instead.
    private void MakeRound(BoxCollider box, PhysicsMaterial physics)
    {
        Vector3 s = box.size;
        int axis = s.x >= s.y && s.x >= s.z ? 0 : s.y >= s.z ? 1 : 2;
        float length = s[axis];
        float a = s[(axis + 1) % 3];
        float b = s[(axis + 2) % 3];

        // Round in cross-section (the other two sides about equal) and clearly longer than wide.
        if (Mathf.Min(a, b) / Mathf.Max(a, b) < 0.75f || length < Mathf.Max(a, b) * 1.2f)
            return;

        CapsuleCollider capsule = gameObject.AddComponent<CapsuleCollider>();
        capsule.direction = axis;
        capsule.center = box.center;
        capsule.radius = (a + b) / 4f;
        capsule.height = length;
        capsule.sharedMaterial = physics;

        box.enabled = false;
        Destroy(box);
    }

    // A bullet hit it at point, travelling in direction.
    public void Hit(Vector3 point, Vector3 normal, Vector3 direction, ImpactEffects.Materials materials)
    {
        if (broken)
            return;

        Effects(point, normal);

        Rigidbody body = GetComponent<Rigidbody>();

        if (body != null)
            body.AddForceAtPosition(direction.normalized * bulletImpulse, point, ForceMode.Impulse);

        if (brokenVersion == null && !burstsIntoLitter)
            return;

        damage++;

        if (damage >= hitsToBreak)
        {
            if (brokenVersion != null)
                Break(body);
            else
                Burst(direction, materials);
        }

        void Effects(Vector3 at, Vector3 facing)
        {
            switch (surface)
            {
                case Surface.Glass:
                    ImpactEffects.Debris(at, facing, 10, new Color(0.75f, 0.9f, 0.85f), new Color(0.35f, 0.55f, 0.45f), materials);
                    PlaySound("prop.glass", ProceduralAudio.GlassBreak, at, 0.6f);
                    break;
                case Surface.Metal:
                    ImpactEffects.Sparks(at, facing, materials);
                    PlaySound("prop.metal", ProceduralAudio.MetalClank, at, 0.5f);
                    break;
                case Surface.Concrete:
                    ImpactEffects.WallHit(at, facing, transform, materials);
                    PlaySound("impact.wall", i => ProceduralAudio.ImpactWall(i, false), at, 0.4f);
                    break;
                case Surface.Paper:
                    ImpactEffects.Debris(at, facing, 6, new Color(0.85f, 0.82f, 0.72f), new Color(0.55f, 0.5f, 0.4f), materials);
                    PlaySound("cardboard.hit", ProceduralAudio.CardboardHit, at, 0.45f);
                    break;
                default:
                    ImpactEffects.Debris(at, facing, 5, new Color(0.3f, 0.3f, 0.32f), new Color(0.12f, 0.12f, 0.14f), materials);
                    PlaySound("cardboard.hit", ProceduralAudio.CardboardHit, at, 0.35f);
                    break;
            }
        }
    }

    // Hit by something heavy (a charging boss, a splash of acid): breaks if it has a broken
    // version, a loose prop is thrown aside, and fixed cover (a barrier, tyres) is wrecked.
    public void Smash(Vector3 point, Vector3 direction, ImpactEffects.Materials materials)
    {
        if (broken)
            return;

        if (brokenVersion != null || burstsIntoLitter)
            damage = hitsToBreak - 1;

        Hit(point, -direction, direction, materials);

        if (broken || this == null)
            return;

        Rigidbody body = GetComponent<Rigidbody>();

        if (body != null && !body.isKinematic)
        {
            body.AddForce(direction.normalized * 5f + Vector3.up * 2f, ForceMode.VelocityChange);
            return;
        }

        broken = true;
        ImpactEffects.Debris(point, Vector3.up + direction.normalized * 0.8f, 16, new Color(0.4f, 0.4f, 0.38f), new Color(0.18f, 0.18f, 0.17f), materials);
        Destroy(gameObject);
    }

    private static void PlaySound(string key, System.Func<int, AudioClip> make, Vector3 at, float volume)
    {
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips(key, 3, make)), at, volume, Random.Range(0.9f, 1.1f));
    }

    // Split open: a puff of torn plastic and the contents strewn across the floor around it.
    private void Burst(Vector3 direction, ImpactEffects.Materials materials)
    {
        broken = true;

        Bounds bounds = new Bounds(transform.position, Vector3.zero);

        foreach (Collider part in GetComponentsInChildren<Collider>())
            bounds.Encapsulate(part.bounds);

        ImpactEffects.Debris(bounds.center, Vector3.up + direction.normalized * 0.4f, 16, new Color(0.12f, 0.12f, 0.13f), new Color(0.3f, 0.28f, 0.22f), materials);
        PlaySound("cardboard.break", ProceduralAudio.CardboardBreak, bounds.center, 0.6f);

        if (LitterPrefabs != null && LitterPrefabs.Length > 0)
        {
            float spread = Mathf.Max(0.5f, Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.4f);
            int pieces = Random.Range(litterPieces.x, litterPieces.y + 1);

            for (int i = 0; i < pieces; i++)
            {
                GameObject prefab = LitterPrefabs[Random.Range(0, LitterPrefabs.Length)];

                if (prefab == null)
                    continue;

                // Thrown out more the way it was hit.
                Vector2 scatter = Random.insideUnitCircle * spread + new Vector2(direction.x, direction.z).normalized * Random.Range(0f, 0.4f);
                GameObject litter = Instantiate(prefab, new Vector3(bounds.center.x + scatter.x, 0f, bounds.center.z + scatter.y),
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform.parent);
                litter.name = prefab.name;

                // Just scenery: nothing to bump into.
                foreach (Collider part in litter.GetComponentsInChildren<Collider>())
                    Destroy(part);

                ClutterSprites.Dress(litter);
            }
        }

        Destroy(gameObject);
    }

    // Swapped for its broken version where it stands, carrying on with whatever push it had.
    private void Break(Rigidbody body)
    {
        broken = true;

        GameObject remains = Instantiate(brokenVersion, transform.position, transform.rotation, transform.parent);
        remains.transform.localScale = transform.localScale;
        remains.name = brokenVersion.name;

        Prop prop = remains.GetComponent<Prop>();

        if (prop != null)
            prop.Configure(physicsMaterial);

        ClutterSprites.Dress(remains);

        Rigidbody newBody = remains.GetComponent<Rigidbody>();

        if (body != null && newBody != null)
        {
            newBody.linearVelocity = body.linearVelocity;
            newBody.angularVelocity = body.angularVelocity;
        }

        Destroy(gameObject);
    }
}
