using System.Collections.Generic;
using UnityEngine;

// A puddle of acid, left by a Broodmother's glob, the Broodmother herself, or a bloater bursting:
// a glowing pixel-art puddle bubbling on the floor for a few seconds (SpriteVisuals.acidPool,
// unlit, at a random turn, drawn above any floor litter), burning the player while they stand in
// it (the hit's knockback pushes them out). A bloater's pool (hurtsZombies) burns zombies in it
// too, the same 1 damage a tick; the Broodmother's spares her brood. Dries up over its last
// second (the sheet's drying frames). The splash eats through the clutter where it lands:
// boxes, trash and furniture break (Smashable) and flat litter and cardboard sheets dissolve.
// Without the sprite art it's a green glow with bubble spheres instead.
public class AcidPool : MonoBehaviour
{
    public const float Radius = 1.5f;
    private const float Duration = 5f;
    private const float FadeTime = 1f;
    private const float TickTime = 0.6f;
    private const int BubbleCount = 14;
    private const float BubbleFps = 6f;
    private const float SpriteLift = 0.03f;    // above floor litter (which lies up to ~0.02 m up)

    private static readonly Color Acid = new Color(0.55f, 1f, 0.15f);

    private readonly List<Transform> bubbles = new List<Transform>();
    private readonly List<float> bubblePhase = new List<float>();
    private readonly List<Renderer> bubbleRenderers = new List<Renderer>();
    private Renderer poolRenderer;
    private MaterialPropertyBlock poolBlock;
    private Light glow;
    private PixelSprite sprite;
    private bool hurtsZombies;
    private float started;
    private float nextTick;

    public static AcidPool Create(Vector3 position, Transform room, bool hurtsZombies = false)
    {
        GameObject root = new GameObject("Acid Pool");
        root.transform.SetParent(room, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);

        // The splash eats through boxes, trash and furniture where it lands, then the litter.
        Smashable.BreakAround(root.transform.position + Vector3.up * 0.3f, Radius);
        Dissolve(root.transform.position, room);

        AcidPool pool = root.AddComponent<AcidPool>();
        pool.hurtsZombies = hurtsZombies;
        pool.started = Time.time;
        pool.nextTick = Time.time + 0.3f;

        SpriteSheet sheet = SpriteVisuals.Current != null ? SpriteVisuals.Current.acidPool : null;

        if (sheet != null && sheet.texture != null)
        {
            root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            pool.sprite = PixelSprite.Create("Puddle", root.transform, sheet, PixelSprite.Mode.Flat);
            pool.sprite.Scale = Radius * 2f / (sheet.frameWidth / Mathf.Max(1f, sheet.pixelsPerUnit));
            pool.sprite.Lift = SpriteLift + Random.Range(0f, 0.004f);   // overlapping pools don't flicker
            pool.sprite.UseGlow();
        }
        else
        {
            pool.BuildModel();
        }

        GameObject light = new GameObject("Glow Light");
        light.transform.SetParent(root.transform, false);
        light.transform.localPosition = Vector3.up * 0.6f;
        pool.glow = light.AddComponent<Light>();
        pool.glow.type = LightType.Point;
        pool.glow.color = Acid;
        pool.glow.range = Radius * 3f;
        pool.glow.shadows = LightShadows.None;

        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("bloater.swell", 2, ProceduralAudio.BloaterSwell)), root.transform.position, 0.6f, 1.6f);
        pool.Update();
        return pool;
    }

    // The 3D look, without the sprite art: a glow disc with bubble spheres.
    private void BuildModel()
    {
        AmmoVisuals visuals = AmmoVisuals.Current;

        if (visuals != null && visuals.glowMaterial != null)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            disc.name = "Glow";
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(transform, false);
            disc.transform.localPosition = Vector3.up * 0.02f;
            disc.transform.localRotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            disc.transform.localScale = Vector3.one * Radius * 2.4f;
            poolRenderer = disc.GetComponent<Renderer>();
            poolRenderer.sharedMaterial = visuals.glowMaterial;
            poolRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            poolRenderer.receiveShadows = false;
        }

        Material material = ConsumablePickup.DefaultMaterial;

        for (int i = 0; i < BubbleCount; i++)
        {
            GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = "Bubble";
            Destroy(bubble.GetComponent<Collider>());
            bubble.transform.SetParent(transform, false);
            Vector2 spot = Random.insideUnitCircle * Radius * 0.85f;
            bubble.transform.localPosition = new Vector3(spot.x, 0.03f, spot.y);

            Renderer renderer = bubble.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (material != null)
                renderer.sharedMaterial = material;

            bubbles.Add(bubble.transform);
            bubblePhase.Add(Random.value);
            bubbleRenderers.Add(renderer);
        }
    }

    private void Update()
    {
        float age = Time.time - started;

        if (age >= Duration)
        {
            Destroy(gameObject);
            return;
        }

        float strength = Mathf.Clamp01((Duration - age) / FadeTime) * Mathf.Clamp01(age / 0.2f);

        // Bubbling on a loop, then drying up over the last second.
        if (sprite != null)
        {
            float drying = 1f - (Duration - age) / FadeTime;

            if (drying < 0f)
                sprite.SetFrame(Mathf.FloorToInt(age * BubbleFps) % 4, 0, false);
            else
                sprite.SetFrame(Mathf.Min(3, Mathf.FloorToInt(drying * 4f)), 1, false);
        }

        // Bubbles swell and pop on their own cycles.
        for (int i = 0; i < bubbles.Count; i++)
        {
            float cycle = Mathf.Repeat(Time.time * 1.3f + bubblePhase[i], 1f);
            float size = Mathf.Sin(cycle * Mathf.PI) * 0.18f * strength;
            bubbles[i].localScale = new Vector3(size, size * 0.7f, size);
            ConsumableModel.Tint(bubbleRenderers[i], Acid * 0.4f, Acid * 2.5f * strength);
        }

        if (poolRenderer != null)
        {
            poolBlock ??= new MaterialPropertyBlock();
            Color colour = Acid * 1.6f * Mathf.Lerp(0.8f, 1f, Mathf.PerlinNoise(started, Time.time * 2f));
            colour.a = 0.85f * strength;
            poolBlock.SetColor("_Color", colour);
            poolRenderer.SetPropertyBlock(poolBlock);
        }

        glow.intensity = 2.5f * strength;

        if (Time.time >= nextTick)
        {
            nextTick = Time.time + TickTime;
            Burn();
        }
    }

    private void Burn()
    {
        HashSet<Enemy> burnt = hurtsZombies ? new HashSet<Enemy>() : null;

        foreach (Collider collider in Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, Radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (Bosses.FlatDistance(collider.transform.position, transform.position) > Radius)
                continue;

            if (collider.TryGetComponent(out PlayerHealth player))
                player.TakeDamage(1, transform.position);
            else if (burnt != null && collider.TryGetComponent(out Enemy enemy) && enemy.IsActive && burnt.Add(enemy))
                enemy.TakeDamage(1);
        }
    }

    // Flat litter and cardboard sheets in the room under the pool (no colliders to break, or too
    // flat to matter) melt away.
    private static void Dissolve(Vector3 centre, Transform room)
    {
        ItemSprite[] items = room != null
            ? room.GetComponentsInChildren<ItemSprite>()
            : FindObjectsByType<ItemSprite>();

        foreach (ItemSprite item in items)
        {
            if (item.IsClutter && Bosses.FlatDistance(item.transform.position, centre) <= Radius)
                Destroy(item.gameObject);
        }
    }
}
