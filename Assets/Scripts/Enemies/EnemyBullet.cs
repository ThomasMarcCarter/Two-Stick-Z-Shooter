using System.Collections.Generic;
using UnityEngine;

// A zombie's projectile, flying flat at bullet height: a glob of bile (spitters, retchers,
// pustules) or a tracer round (gunners). They're big, bright and slow enough to see coming and
// step or roll out of the way of. One that reaches the player does 1 damage (through
// PlayerHealth, so i-frames and a dodge roll's untouchable start let it pass); walls, doors and
// anything solid stop it with a splash, while zombies don't (they never hit each other). Each
// gives off a little light so it reads in the dark. Fire/Fan/Ring are the patterns the kinds use;
// every live one is cleared when its room is cleared, and at most MaxLive fly at once.
public class EnemyBullet : MonoBehaviour
{
    public enum Look { Bile, Round }

    private const float Radius = 0.2f;
    private const float Lifetime = 4f;
    private const int MaxLive = 80;
    private const float WobbleFps = 10f;

    private static readonly List<EnemyBullet> live = new List<EnemyBullet>();

    private Vector3 velocity;
    private Look look;
    private float diesAt;
    private PixelSprite sprite;
    private bool passed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        live.Clear();
    }

    public static int LiveCount
    {
        get
        {
            live.RemoveAll(b => b == null);
            return live.Count;
        }
    }

    public static EnemyBullet Fire(Vector3 from, Vector3 direction, float speed, Look look, Transform room)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return null;

        live.RemoveAll(b => b == null);

        if (live.Count >= MaxLive)
        {
            Destroy(live[0].gameObject);
            live.RemoveAt(0);
        }

        SpriteSheet sheet = SpriteVisuals.Current != null ? SpriteVisuals.Current.enemyShots : null;
        bool art = sheet != null && sheet.texture != null;
        Color colour = Colour(look);
        GameObject shot;

        if (art)
        {
            shot = new GameObject(look == Look.Bile ? "Bile Glob" : "Enemy Round");
        }
        else
        {
            shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shot.name = look == Look.Bile ? "Bile Glob" : "Enemy Round";
            Destroy(shot.GetComponent<Collider>());
            shot.transform.localScale = Vector3.one * Radius * 2f;
            Renderer renderer = shot.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (ConsumablePickup.DefaultMaterial != null)
            {
                renderer.sharedMaterial = ConsumablePickup.DefaultMaterial;
                ConsumableModel.Tint(renderer, colour * 0.6f, colour * 3f);
            }
        }

        if (room != null)
            shot.transform.SetParent(room, true);

        shot.transform.position = new Vector3(from.x, Gun.ShotHeight, from.z);
        EnemyBullet bullet = shot.AddComponent<EnemyBullet>();
        bullet.velocity = direction.normalized * speed;
        bullet.look = look;
        bullet.diesAt = Time.time + Lifetime;

        if (art)
        {
            bullet.sprite = PixelSprite.Create("Sprite", shot.transform, sheet, PixelSprite.Mode.Facing);
            bullet.sprite.UseGlow();
            bullet.sprite.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bullet.Turn();
        }

        Light light = shot.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = colour;
        light.range = 1.2f;
        light.intensity = 1f;
        light.shadows = LightShadows.None;

        live.Add(bullet);
        bullet.Draw();
        return bullet;
    }

    // count shots spread evenly across spreadDegrees, centred on direction.
    public static void Fan(Vector3 from, Vector3 direction, int count, float spreadDegrees, float speed, Look look, Transform room)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = count > 1 ? Mathf.Lerp(-spreadDegrees / 2f, spreadDegrees / 2f, i / (count - 1f)) : 0f;
            Fire(from, Quaternion.Euler(0f, angle, 0f) * direction, speed, look, room);
        }
    }

    // count shots evenly all the way round, the first startAngle degrees on from world forward.
    public static void Ring(Vector3 from, int count, float startAngle, float speed, Look look, Transform room)
    {
        for (int i = 0; i < count; i++)
            Fire(from, Quaternion.Euler(0f, startAngle + i * 360f / count, 0f) * Vector3.forward, speed, look, room);
    }

    public static void ClearAll()
    {
        foreach (EnemyBullet bullet in live)
        {
            if (bullet != null)
                Destroy(bullet.gameObject);
        }

        live.Clear();
    }

    private static Color Colour(Look look) => look == Look.Bile ? new Color(0.6f, 1f, 0.2f) : new Color(1f, 0.6f, 0.2f);

    private void FixedUpdate()
    {
        if (Time.time >= diesAt)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 step = velocity * Time.fixedDeltaTime;
        Vector3 position = transform.position;

        RaycastHit[] hits = Physics.SphereCastAll(position, Radius, velocity.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        // Nearest first, so a prop between it and the player stops it.
        foreach (RaycastHit hit in hits)
        {
            Rigidbody body = hit.collider.attachedRigidbody;

            if (body != null && body.GetComponent<Enemy>() != null)
                continue;

            if (body != null && body.TryGetComponent(out PlayerHealth player))
            {
                // Untouchable (a roll, or just hit): it flies on through.
                if (player.IsInvulnerable || player.IsDead)
                {
                    if (!passed)
                    {
                        passed = true;
                        PlayerItems.ShotPassed(player);
                    }

                    continue;
                }

                player.TakeDamage(1, position - velocity.normalized);
            }

            Splash(hit.distance > 0f ? position + velocity.normalized * hit.distance : position);
            return;
        }

        transform.position = position + step;
    }

    private void Update()
    {
        Draw();
    }

    private void Draw()
    {
        if (sprite == null)
            return;

        sprite.Position = transform.position;
        int frames = look == Look.Bile ? 4 : 2;
        sprite.SetFrame(Mathf.FloorToInt(Time.time * WobbleFps) % frames, look == Look.Bile ? 0 : 1, false);
    }

    // Turned to its direction of travel on screen (the art points right).
    private void Turn()
    {
        Camera camera = Camera.main;

        if (camera == null)
            return;

        Vector3 a = camera.WorldToScreenPoint(transform.position);
        Vector3 b = camera.WorldToScreenPoint(transform.position + velocity.normalized);
        sprite.Angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
    }

    private void Splash(Vector3 point)
    {
        if (ImpactEffects.TryGetMaterials(out ImpactEffects.Materials materials))
        {
            Color colour = Colour(look);
            ImpactEffects.Debris(point, -velocity.normalized, 6, colour, colour * 0.6f, materials);
        }

        Destroy(gameObject);
    }
}
