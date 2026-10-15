using UnityEngine;
using UnityEngine.AI;

// A bit of room dressing from the trash pack. Pushable props (cans, bottles, bags, bricks...)
// have a light body you and the zombies knock about: they tip over and tumble, and long round
// things (cans, bottles, cups) roll. Bullets that hit them knock them flying. Fixed props (barriers, cinder blocks, tyres) stay put as cover that zombies path around.
// Some props break when shot (a glass bottle into a broken one, a cup crushed flat). Each has
// a surface, which sets what a hit throws off and sounds like.
public class Prop : MonoBehaviour
{
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
    [Tooltip("How hard a bullet shoves a pushable prop.")]
    [SerializeField] private float bulletImpulse = 0.6f;
    [Tooltip("Pushable only: free to tip over and tumble. Off keeps it upright (trash bags just slump along).")]
    [SerializeField] private bool canTip = true;

    private int damage;
    private bool broken;
    private PhysicsMaterial physicsMaterial;

    public Surface Material => surface;

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

        if (brokenVersion == null)
            return;

        damage++;

        if (damage >= hitsToBreak)
            Break(body);

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

    private static void PlaySound(string key, System.Func<int, AudioClip> make, Vector3 at, float volume)
    {
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips(key, 3, make)), at, volume, Random.Range(0.9f, 1.1f));
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

        Rigidbody newBody = remains.GetComponent<Rigidbody>();

        if (body != null && newBody != null)
        {
            newBody.linearVelocity = body.linearVelocity;
            newBody.angularVelocity = body.angularVelocity;
        }

        Destroy(gameObject);
    }
}
