using UnityEngine;

// A grenade, molotov or bottle in flight, on an arc that comes down at the point it was thrown
// at. Walls (and anything else solid) stop it short. A molotov or bottle breaks on whatever it
// hits first, zombies included; a grenade flies over zombies, bounces off everything else and
// rolls to a stop, and goes off when its fuse runs out, wherever it is.
public class Thrown : MonoBehaviour
{
    private const float Gravity = 20f;
    private const float Radius = 0.12f;
    private const float GrenadeFuse = 1.8f;

    private ConsumableType type;
    private Transform thrower;
    private Vector3 velocity;
    private float fuseAt;
    private bool resting;
    private Transform model;
    private Vector3 spin;

    public static Thrown Launch(ConsumableType type, Vector3 from, Vector3 target, GameObject thrower)
    {
        GameObject root = new GameObject($"Thrown ({ConsumableInfo.Name(type)})");
        root.transform.position = from;

        Thrown thrown = root.AddComponent<Thrown>();
        thrown.type = type;
        thrown.thrower = thrower.transform.root;
        thrown.fuseAt = Time.time + GrenadeFuse;
        thrown.model = ConsumableModel.Build(type, root.transform, ConsumablePickup.DefaultMaterial, 0.9f);
        thrown.model.localPosition = Vector3.down * 0.12f;
        ItemSprite.ApplyFlying(root, SpriteVisuals.Current != null ? SpriteVisuals.Current.items : null, ItemSprites.Consumable(type));
        thrown.spin = Random.onUnitSphere * 540f;

        // Further throws take a little longer and go a little higher.
        Vector3 flat = target - from;
        float height = flat.y;
        flat.y = 0f;
        float time = Mathf.Lerp(0.35f, 0.75f, flat.magnitude / 10f);
        thrown.velocity = flat / time + Vector3.up * ((height + Radius + 0.5f * Gravity * time * time) / time);
        return thrown;
    }

    private void Update()
    {
        if (type == ConsumableType.Grenade && Time.time >= fuseAt)
        {
            Blast.Explode(transform.position, FloorPickups.RoomAt(transform.position));
            Destroy(gameObject);
            return;
        }

        if (resting)
            return;

        float dt = Time.deltaTime;
        Vector3 step = velocity * dt;
        velocity.y -= Gravity * dt;
        model.Rotate(spin * dt, Space.Self);

        if (Cast(step, out RaycastHit hit))
        {
            transform.position += step.normalized * Mathf.Max(0f, hit.distance - 0.01f);
            Impact(hit.point, hit.normal, hit.collider);
            return;
        }

        transform.position += step;

        // The floor, whether or not it has a collider.
        if (transform.position.y <= Radius)
        {
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, Radius, p.z);
            Impact(new Vector3(p.x, 0f, p.z), Vector3.up, null);
        }
    }

    // Solid things in the way, other than the thrower.
    private bool Cast(Vector3 step, out RaycastHit nearest)
    {
        nearest = default;
        float distance = step.magnitude;

        if (distance < 0.0001f)
            return false;

        bool found = false;

        foreach (RaycastHit hit in Physics.SphereCastAll(transform.position, Radius, step / distance, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.root == thrower || hit.distance <= 0f)
                continue;

            // Grenades are lobbed over zombies rather than bouncing back off the nearest one.
            if (type == ConsumableType.Grenade && hit.collider.GetComponent<Enemy>() != null)
                continue;

            if (!found || hit.distance < nearest.distance)
            {
                nearest = hit;
                found = true;
            }
        }

        return found;
    }

    private void Impact(Vector3 point, Vector3 normal, Collider hit)
    {
        if (type != ConsumableType.Grenade)
        {
            Break(point);
            return;
        }

        // Bounces off, losing most of its speed, until it settles on the floor.
        velocity = Vector3.Reflect(velocity, normal) * 0.35f;
        transform.position += normal * 0.02f;

        if (velocity.magnitude > 2f)
            SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("metal.clank", 3, ProceduralAudio.MetalClank)), point, 0.35f, 0.6f);

        if (normal.y > 0.6f && velocity.magnitude < 1.5f)
        {
            resting = true;
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, Radius, p.z);
        }
    }

    private void Break(Vector3 point)
    {
        // On the floor beneath, a little back from a wall it hit.
        Vector3 back = velocity;
        back.y = 0f;
        Vector3 floor = new Vector3(point.x, 0f, point.z) - Vector3.ClampMagnitude(back, 1f) * 0.25f;
        Transform room = FloorPickups.RoomAt(floor);

        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("glass.break", 3, ProceduralAudio.GlassBreak)), point, 0.9f, Random.Range(0.9f, 1.1f));
        Blast.Shards(point, ConsumableInfo.Colour(type));

        if (type == ConsumableType.Molotov)
        {
            FirePatch.Create(floor, room);
            Noise.Emit(floor, Noise.Gunshot * 0.7f, Noise.Kind.Smash);
        }
        else
        {
            // The point of a bottle: a crash zombies come to look at.
            Noise.Emit(floor, Noise.Gunshot * 1.3f, Noise.Kind.Smash);
        }

        Destroy(gameObject);
    }
}
