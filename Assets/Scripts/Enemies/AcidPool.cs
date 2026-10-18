using System.Collections.Generic;
using UnityEngine;

// A puddle of acid from the Broodmother: a sickly green glow on the floor with bubbles popping
// in it for a few seconds, burning the player while they stand in it (the hit's knockback pushes
// them out). Zombies wade through it unharmed. Fades over its last second.
public class AcidPool : MonoBehaviour
{
    public const float Radius = 1.5f;
    private const float Duration = 5f;
    private const float FadeTime = 1f;
    private const float TickTime = 0.6f;
    private const int BubbleCount = 14;

    private static readonly Color Acid = new Color(0.55f, 1f, 0.15f);

    private readonly List<Transform> bubbles = new List<Transform>();
    private readonly List<float> bubblePhase = new List<float>();
    private readonly List<Renderer> bubbleRenderers = new List<Renderer>();
    private Renderer poolRenderer;
    private MaterialPropertyBlock poolBlock;
    private Light glow;
    private float started;
    private float nextTick;

    public static AcidPool Create(Vector3 position, Transform room)
    {
        GameObject root = new GameObject("Acid Pool");
        root.transform.SetParent(room, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);

        AcidPool pool = root.AddComponent<AcidPool>();
        pool.started = Time.time;
        pool.nextTick = Time.time + 0.3f;

        AmmoVisuals visuals = AmmoVisuals.Current;

        if (visuals != null && visuals.glowMaterial != null)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            disc.name = "Glow";
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(root.transform, false);
            disc.transform.localPosition = Vector3.up * 0.02f;
            disc.transform.localRotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            disc.transform.localScale = Vector3.one * Radius * 2.4f;
            pool.poolRenderer = disc.GetComponent<Renderer>();
            pool.poolRenderer.sharedMaterial = visuals.glowMaterial;
            pool.poolRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pool.poolRenderer.receiveShadows = false;
        }

        Material material = ConsumablePickup.DefaultMaterial;

        for (int i = 0; i < BubbleCount; i++)
        {
            GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = "Bubble";
            Destroy(bubble.GetComponent<Collider>());
            bubble.transform.SetParent(root.transform, false);
            Vector2 spot = Random.insideUnitCircle * Radius * 0.85f;
            bubble.transform.localPosition = new Vector3(spot.x, 0.03f, spot.y);

            Renderer renderer = bubble.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (material != null)
                renderer.sharedMaterial = material;

            pool.bubbles.Add(bubble.transform);
            pool.bubblePhase.Add(Random.value);
            pool.bubbleRenderers.Add(renderer);
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

    private void Update()
    {
        float age = Time.time - started;

        if (age >= Duration)
        {
            Destroy(gameObject);
            return;
        }

        float strength = Mathf.Clamp01((Duration - age) / FadeTime) * Mathf.Clamp01(age / 0.2f);

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
        foreach (Collider collider in Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, Radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!collider.TryGetComponent(out PlayerHealth player))
                continue;

            if (Bosses.FlatDistance(collider.transform.position, transform.position) <= Radius)
                player.TakeDamage(1, transform.position);
        }
    }
}
