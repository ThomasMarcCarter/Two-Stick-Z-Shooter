using UnityEngine;
using UnityEngine.AI;

// A cardboard box: room clutter you can shove about by walking into it (heavier boxes push
// harder), that zombies path around via a carving navmesh obstacle, and that hides you from
// bullets but not from sight (enemies see over it). Bullets travel at waist height, so they fly
// over the smallest boxes and hit the rest, which take a few hits (more for bigger boxes): each
// puffs cardboard debris, and the last crunches it flat, leaving a scrap of flattened cardboard.
// A box stacked on top rests there under its own physics, and falls if the one below goes. One
// that falls out of the level is removed. A boss's charge or acid crushes it at once (Smash).
public class CardboardBox : MonoBehaviour
{
    [Tooltip("Hits to break it. Bigger boxes take more.")]
    [SerializeField, Min(1)] private int hits = 2;
    [Tooltip("Left lying where it broke. Optional.")]
    [SerializeField] private GameObject brokenRemains;
    [SerializeField] private float remainsScale = 0.45f;

    private static readonly Color Cardboard = new Color(0.62f, 0.45f, 0.26f);
    private static readonly Color CardboardDark = new Color(0.4f, 0.28f, 0.15f);

    private int damage;
    private bool broken;

    // Set when it's placed: the room it belongs to (remains go there too).
    public Transform Room { get; set; }

    // Call once it's placed and scaled: gives it a body to be pushed around, weighted by size.
    public void EnablePhysics(PhysicsMaterial material)
    {
        BoxCollider box = GetComponent<BoxCollider>();

        if (box == null)
            return;

        if (material != null)
            box.sharedMaterial = material;

        Rigidbody body = GetComponent<Rigidbody>();

        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();

        Vector3 size = Vector3.Scale(box.size, transform.lossyScale);
        // Empty cardboard is light: lighter than the player (mass 1), so walking into a box
        // shoves it along, with the biggest ones only a little heavier.
        body.mass = Mathf.Clamp(size.x * size.y * size.z * 1.2f, 0.15f, 1.3f);

        // Slides and spins when shoved, but stays upright and soon comes to rest.
        body.linearDamping = 3f;
        body.angularDamping = 4f;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Awake()
    {
        // Zombies plan routes around it, and the gap closes again if it's broken.
        BoxCollider box = GetComponent<BoxCollider>();

        if (box != null && GetComponent<NavMeshObstacle>() == null)
        {
            NavMeshObstacle obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = box.center;
            obstacle.size = box.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }
    }

    // A box placed overlapping something can be shoved through the floor by the physics push-out;
    // one that's fallen out of the level is just removed.
    private void FixedUpdate()
    {
        if (transform.position.y < -2f)
            Destroy(gameObject);
    }

    // A bullet hit it at point, travelling in direction.
    public void Hit(Vector3 point, Vector3 normal, Vector3 direction, ImpactEffects.Materials materials)
    {
        if (broken)
            return;

        damage++;
        ImpactEffects.Debris(point, normal, 6, Cardboard, CardboardDark, materials);

        if (damage < hits)
        {
            SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("cardboard.hit", 3, ProceduralAudio.CardboardHit)), point, 0.5f, Random.Range(0.9f, 1.1f));
            return;
        }

        Break(direction, materials);
    }

    // Hit by something heavy (a charging boss, a splash of acid): crushed flat at once.
    public void Smash(Vector3 direction, ImpactEffects.Materials materials)
    {
        if (!broken)
            Break(direction, materials);
    }

    private void Break(Vector3 direction, ImpactEffects.Materials materials)
    {
        broken = true;

        Bounds bounds = new Bounds(transform.position, Vector3.zero);

        foreach (Renderer part in GetComponentsInChildren<Renderer>())
            bounds.Encapsulate(part.bounds);

        ImpactEffects.Debris(bounds.center, Vector3.up + direction.normalized * 0.5f, 18, Cardboard, CardboardDark, materials);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("cardboard.break", 2, ProceduralAudio.CardboardBreak)), bounds.center, 0.7f, Random.Range(0.9f, 1.1f));

        if (brokenRemains != null)
        {
            GameObject remains = Instantiate(brokenRemains, new Vector3(transform.position.x, 0f, transform.position.z),
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Room != null ? Room : transform.parent);
            // Sized to the box it came from (boxes are placed at different sizes).
            remains.transform.localScale = Vector3.one * remainsScale * transform.lossyScale.x * Random.Range(0.8f, 1.2f);

            // Just scenery: nothing to bump into.
            foreach (Collider part in remains.GetComponentsInChildren<Collider>())
                Destroy(part);

            ClutterSprites.DressAs(remains, "Box_Flattened");
        }

        Destroy(gameObject);
    }
}
