using System.Collections.Generic;
using UnityEngine;

// A pool of burning fuel from a molotov: flickering flames and an orange light for a few
// seconds, hurting any zombie standing in it every half second, and the player too if they
// walk through (the hit's knockback pushes them out). Dies down over its last second.
public class FirePatch : MonoBehaviour
{
    public const float Radius = 1.8f;
    private const float Duration = 6f;
    private const float FadeTime = 1f;
    private const float TickTime = 0.5f;
    private const int FlameCount = 24;

    private readonly List<Transform> flames = new List<Transform>();
    private readonly List<Vector3> flameSizes = new List<Vector3>();
    private readonly List<Renderer> flameRenderers = new List<Renderer>();
    private Renderer poolRenderer;
    private MaterialPropertyBlock poolBlock;
    private Light glow;
    private AudioSource crackle;
    private float started;
    private float nextTick;
    private float seed;

    public static FirePatch Create(Vector3 position, Transform room)
    {
        GameObject root = new GameObject("Fire");
        root.transform.SetParent(room, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);

        FirePatch fire = root.AddComponent<FirePatch>();
        fire.started = Time.time;
        fire.nextTick = Time.time + 0.1f;
        fire.seed = Random.value * 100f;
        Material material = ConsumablePickup.DefaultMaterial;

        // A soft orange glow on the floor under it, like the ammo pickups' (none if there's no
        // glow material set up).
        AmmoVisuals visuals = AmmoVisuals.Current;

        if (visuals != null && visuals.glowMaterial != null)
        {
            GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Quad);
            pool.name = "Glow";
            Destroy(pool.GetComponent<Collider>());
            pool.transform.SetParent(root.transform, false);
            pool.transform.localPosition = Vector3.up * 0.015f;
            pool.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            pool.transform.localScale = Vector3.one * Radius * 2.6f;
            fire.poolRenderer = pool.GetComponent<Renderer>();
            fire.poolRenderer.sharedMaterial = visuals.glowMaterial;
            fire.poolRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fire.poolRenderer.receiveShadows = false;
        }

        for (int i = 0; i < FlameCount; i++)
        {
            GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flame.name = "Flame";
            Destroy(flame.GetComponent<Collider>());
            flame.transform.SetParent(root.transform, false);

            // Bunched towards the middle, where the fuel is deepest.
            Vector2 spot = Random.insideUnitCircle * Radius * Mathf.Lerp(0.3f, 0.9f, i / (float)FlameCount);
            flame.transform.localPosition = new Vector3(spot.x, 0f, spot.y);
            flame.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);

            Renderer renderer = flame.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (material != null)
                renderer.sharedMaterial = material;

            fire.flames.Add(flame.transform);
            fire.flameSizes.Add(new Vector3(Random.Range(0.07f, 0.14f), Random.Range(0.4f, 1.1f), Random.Range(0.07f, 0.14f)));
            fire.flameRenderers.Add(renderer);
        }

        GameObject light = new GameObject("Glow");
        light.transform.SetParent(root.transform, false);
        light.transform.localPosition = Vector3.up * 0.8f;
        fire.glow = light.AddComponent<Light>();
        fire.glow.type = LightType.Point;
        fire.glow.color = new Color(1f, 0.5f, 0.15f);
        fire.glow.range = Radius * 3.5f;
        fire.glow.shadows = LightShadows.None;

        fire.crackle = GameAudio.Route(root.AddComponent<AudioSource>());
        fire.crackle.clip = SoundFX.Pick(SoundFX.Clips("fire.loop", 1, i => ProceduralAudio.FireLoop()));
        fire.crackle.loop = true;
        fire.crackle.spatialBlend = 1f;
        fire.crackle.minDistance = 2f;
        fire.crackle.maxDistance = 18f;
        fire.crackle.rolloffMode = AudioRolloffMode.Linear;
        fire.crackle.volume = 0.5f;
        fire.crackle.Play();

        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("explosion", 3, ProceduralAudio.Explosion)), root.transform.position, 0.4f, 1.6f);
        fire.Update();
        return fire;
    }

    private void Update()
    {
        float age = Time.time - started;

        if (age >= Duration)
        {
            Destroy(gameObject);
            return;
        }

        // 1 while burning, falling to 0 over the last second.
        float strength = Mathf.Clamp01((Duration - age) / FadeTime) * Mathf.Clamp01(age / 0.15f);

        for (int i = 0; i < flames.Count; i++)
        {
            float flicker = Mathf.PerlinNoise(seed + i * 3.1f, Time.time * 6f);
            Vector3 size = flameSizes[i];
            size.y *= Mathf.Lerp(0.4f, 1.3f, flicker) * strength;
            flames[i].localScale = size;
            flames[i].localPosition = new Vector3(flames[i].localPosition.x, size.y / 2f, flames[i].localPosition.z);

            Color heat = Color.Lerp(new Color(1f, 0.25f, 0.03f), new Color(1f, 0.75f, 0.3f), flicker);
            ConsumableModel.Tint(flameRenderers[i], heat, heat * 14f * strength);
        }

        // The glow beneath flickers with the flames.
        float embers = Mathf.Lerp(0.5f, 1f, Mathf.PerlinNoise(seed + 50f, Time.time * 4f)) * strength;
        if (poolRenderer != null)
        {
            poolBlock ??= new MaterialPropertyBlock();
            Color colour = new Color(1f, 0.35f, 0.06f) * 2f * embers;
            colour.a = 0.85f * strength;
            poolBlock.SetColor("_Color", colour);
            poolRenderer.SetPropertyBlock(poolBlock);
        }

        glow.intensity = Mathf.Lerp(3f, 6f, Mathf.PerlinNoise(seed, Time.time * 8f)) * strength;
        crackle.volume = 0.5f * strength;

        if (Time.time >= nextTick)
        {
            nextTick = Time.time + TickTime;
            Burn();
        }
    }

    private void Burn()
    {
        Vector3 centre = transform.position + Vector3.up * 0.5f;
        HashSet<Object> done = new HashSet<Object>();

        foreach (Collider collider in Physics.OverlapSphere(centre, Radius, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 offset = collider.transform.position - transform.position;
            offset.y = 0f;

            if (offset.magnitude > Radius)
                continue;

            if (collider.TryGetComponent(out Enemy enemy) && done.Add(enemy))
                enemy.TakeDamage(1);
            else if (collider.TryGetComponent(out PlayerHealth player) && done.Add(player))
                player.TakeDamage(1, transform.position);
        }
    }
}
