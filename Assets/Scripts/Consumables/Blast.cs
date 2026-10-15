using System.Collections.Generic;
using UnityEngine;

// Something going off: a grenade, or a bloater bursting. Damage to zombies in the radius that
// aren't behind a wall (most at the middle), a shove that staggers them, boxes and props knocked
// about, the player hurt if too close, and a bang that carries. Shown as a quick ball of fire
// (or gas) and a flash of light, with a shake of the camera. Also the glass shards for a molotov
// or bottle breaking.
public class Blast : MonoBehaviour
{
    // How big and how bad a blast is.
    public struct Settings
    {
        public float Radius;
        public int MaxDamage;
        public float PlayerRadius;
        public int PlayerDamage;        // inside half the player radius
        public int PlayerDamageOuter;   // further out
        public float Push;              // knockback at the middle, falling off outwards
        public float Loudness;          // as a multiple of a gunshot
        public float Shake;
        public bool BreaksProps;
        public Color HotColour;
        public Color CoolColour;
        public Color LightColour;
    }

    public static Settings Grenade => new Settings
    {
        Radius = 3.5f,
        MaxDamage = 6,
        PlayerRadius = 2.6f,
        PlayerDamage = 2,
        PlayerDamageOuter = 1,
        Push = 10f,
        Loudness = 1.8f,
        Shake = 0.45f,
        BreaksProps = true,
        HotColour = new Color(1f, 0.9f, 0.6f),
        CoolColour = new Color(0.8f, 0.15f, 0.02f),
        LightColour = new Color(1f, 0.65f, 0.3f)
    };

    private const float Life = 0.45f;

    private Transform ball;
    private Light flash;
    private Renderer ballRenderer;
    private float started;
    private Settings settings;

    public static void Explode(Vector3 position, Transform room)
    {
        Explode(position, room, Grenade);
    }

    public static void Explode(Vector3 position, Transform room, Settings settings)
    {
        Vector3 centre = new Vector3(position.x, 0.5f, position.z);
        HashSet<Object> done = new HashSet<Object>();
        bool haveMaterials = ImpactEffects.TryGetMaterials(out ImpactEffects.Materials materials);
        float radius = settings.Radius;

        foreach (Collider collider in Physics.OverlapSphere(centre, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 target = collider.bounds.center;
            Vector3 away = target - centre;
            away.y = 0f;
            float distance = away.magnitude;
            Vector3 direction = distance > 0.01f ? away / distance : Vector3.forward;

            if (collider.TryGetComponent(out Enemy enemy))
            {
                if (!done.Add(enemy) || !enemy.IsActive || Blocked(centre, target, collider))
                    continue;

                float falloff = Mathf.Clamp01(distance / radius);
                int damage = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(settings.MaxDamage, 1f, falloff)));
                enemy.Knock(direction * Mathf.Lerp(settings.Push, settings.Push * 0.3f, falloff), 0.5f);
                enemy.TakeDamage(damage);
                continue;
            }

            if (collider.TryGetComponent(out PlayerHealth player))
            {
                if (done.Add(player) && distance <= settings.PlayerRadius && !Blocked(centre, target, collider))
                    player.TakeDamage(distance < settings.PlayerRadius * 0.5f ? settings.PlayerDamage : settings.PlayerDamageOuter, centre);

                continue;
            }

            if (!haveMaterials || !settings.BreaksProps)
                continue;

            CardboardBox box = collider.GetComponentInParent<CardboardBox>();

            // Hit a few times over, enough to break a box or prop outright.
            if (box != null && done.Add(box))
            {
                for (int i = 0; i < 4; i++)
                    box.Hit(collider.ClosestPoint(centre), -direction, direction, materials);

                continue;
            }

            Prop prop = collider.GetComponentInParent<Prop>();

            if (prop != null && done.Add(prop))
            {
                for (int i = 0; i < 4 && prop != null; i++)
                    prop.Hit(collider.ClosestPoint(centre), -direction, direction, materials);
            }
        }

        Noise.Emit(centre, Noise.Gunshot * settings.Loudness, Noise.Kind.Explosion);
        float volume = Mathf.Clamp01(0.4f + settings.Radius / 6f);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("explosion", 3, ProceduralAudio.Explosion)), centre, volume, Random.Range(0.92f, 1.05f) * (settings.Radius < 3f ? 1.35f : 1f));
        CameraFollow.Shake(settings.Shake);

        if (haveMaterials && settings.BreaksProps)
        {
            for (int i = 0; i < 6; i++)
                ImpactEffects.Sparks(centre, (Random.onUnitSphere + Vector3.up).normalized, materials);
        }

        Create(centre, room, settings);
    }

    // A wall between the blast and what it would hit.
    private static bool Blocked(Vector3 from, Vector3 to, Collider target)
    {
        foreach (RaycastHit hit in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != target && hit.collider.CompareTag("Wall"))
                return true;
        }

        return false;
    }

    // Glass breaking: a few glints of its colour.
    public static void Shards(Vector3 point, Color colour)
    {
        if (ImpactEffects.TryGetMaterials(out ImpactEffects.Materials materials))
            ImpactEffects.Debris(point + Vector3.up * 0.1f, Vector3.up, 10, colour, Color.Lerp(colour, Color.white, 0.6f), materials);
    }

    private static void Create(Vector3 centre, Transform room, Settings settings)
    {
        GameObject root = new GameObject("Blast");
        root.transform.SetParent(room, true);
        root.transform.position = centre;

        Blast blast = root.AddComponent<Blast>();
        blast.started = Time.time;
        blast.settings = settings;

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Fireball";
        Destroy(sphere.GetComponent<Collider>());
        sphere.transform.SetParent(root.transform, false);
        blast.ball = sphere.transform;
        blast.ballRenderer = sphere.GetComponent<Renderer>();
        blast.ballRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (ConsumablePickup.DefaultMaterial != null)
            blast.ballRenderer.sharedMaterial = ConsumablePickup.DefaultMaterial;

        GameObject light = new GameObject("Flash");
        light.transform.SetParent(root.transform, false);
        light.transform.localPosition = Vector3.up * 1f;
        blast.flash = light.AddComponent<Light>();
        blast.flash.type = LightType.Point;
        blast.flash.color = settings.LightColour;
        blast.flash.range = settings.Radius * 3f;
        blast.flash.shadows = LightShadows.None;

        blast.Update();
    }

    private void Update()
    {
        float t = (Time.time - started) / Life;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // Swells fast, then shrinks away as it cools.
        float full = settings.Radius * 0.7f;
        float size = t < 0.15f ? Mathf.Lerp(0.4f, full, t / 0.15f) : Mathf.Lerp(full, 0f, (t - 0.15f) / 0.85f);
        ball.localScale = Vector3.one * size;
        Color heat = Color.Lerp(settings.HotColour, settings.CoolColour, t);
        ConsumableModel.Tint(ballRenderer, heat * 0.5f, heat * Mathf.Lerp(12f, 1f, t));
        flash.intensity = Mathf.Lerp(18f, 0f, Mathf.Sqrt(t)) * Mathf.Clamp01(settings.Radius / 3.5f);
    }
}
