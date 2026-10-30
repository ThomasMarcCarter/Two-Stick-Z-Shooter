using System.Collections.Generic;
using UnityEngine;

// A pool of burning fuel from a molotov: flickering flames and an orange light for a few
// seconds, hurting any zombie standing in it every half second, and the player too if they
// walk through (the hit's knockback pushes them out). Dies down over its last second.
// With the sprite art it's a burning pixel puddle on the floor (SpriteVisuals.firePool, above any
// litter) with a cluster of standing pixel flames flickering out of step across it (flame),
// all unlit so they glow, dying down to embers and char at the end (each sheet's second row).
// Without it, a glow on the floor with flickering block flames.
public class FirePatch : MonoBehaviour
{
    public const float Radius = 1.8f;
    private const float Duration = 6f;
    private const float FadeTime = 1f;
    private const float TickTime = 0.5f;
    private const int FlameCount = 24;
    private const int SpriteFlameCount = 9;
    private const float FlameFps = 12f;
    private const float PoolFps = 6f;
    private const float SpriteLift = 0.025f;   // above floor litter

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
    private float radius = Radius;
    private float life = Duration;
    private PixelSprite poolSprite;
    private readonly List<PixelSprite> flameSprites = new List<PixelSprite>();
    private readonly List<float> flamePhase = new List<float>();
    private readonly List<bool> flameMirror = new List<bool>();

    // scale: its size against a molotov's (a smaller fire lights without the whoomp). life: how long
    // it burns, by default as long as a molotov's.
    public static FirePatch Create(Vector3 position, Transform room, float scale = 1f, float life = Duration)
    {
        GameObject root = new GameObject("Fire");
        root.transform.SetParent(room, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);

        FirePatch fire = root.AddComponent<FirePatch>();
        fire.radius = Radius * scale;
        fire.life = life;
        fire.started = Time.time;
        fire.nextTick = Time.time + 0.1f;
        fire.seed = Random.value * 100f;

        SpriteVisuals sprites = SpriteVisuals.Current;

        if (sprites != null && sprites.flame != null && sprites.firePool != null)
            fire.BuildSprites(sprites.firePool, sprites.flame);
        else
            fire.BuildModel();

        GameObject light = new GameObject("Glow");
        light.transform.SetParent(root.transform, false);
        light.transform.localPosition = Vector3.up * 0.8f;
        fire.glow = light.AddComponent<Light>();
        fire.glow.type = LightType.Point;
        fire.glow.color = new Color(1f, 0.5f, 0.15f);
        fire.glow.range = fire.radius * 3.5f;
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

        if (scale >= 1f)
            SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("explosion", 3, ProceduralAudio.Explosion)), root.transform.position, 0.4f, 1.6f);
        fire.Update();
        return fire;
    }

    // The pixel fire: the burning puddle, and flames standing up out of it (bunched towards the
    // middle, where the fuel is deepest), each its own size and out of step with the rest.
    private void BuildSprites(SpriteSheet pool, SpriteSheet flame)
    {
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        poolSprite = PixelSprite.Create("Burning Fuel", transform, pool, PixelSprite.Mode.Flat);
        poolSprite.Scale = radius * 2f / (pool.frameWidth / Mathf.Max(1f, pool.pixelsPerUnit));
        poolSprite.Lift = SpriteLift;
        poolSprite.UseGlow();

        for (int i = 0; i < SpriteFlameCount; i++)
        {
            GameObject spot = new GameObject("Flame");
            spot.transform.SetParent(transform, false);
            Vector2 offset = Random.insideUnitCircle * radius * Mathf.Lerp(0.15f, 0.75f, i / (float)SpriteFlameCount);
            spot.transform.localPosition = new Vector3(offset.x, 0f, offset.y);

            PixelSprite sprite = PixelSprite.Create("Sprite", spot.transform, flame, PixelSprite.Mode.Facing);
            sprite.Standing = true;
            sprite.Scale = Random.Range(0.75f, 1.25f);
            sprite.UseGlow();
            flameSprites.Add(sprite);
            flamePhase.Add(Random.value * 6f);
            flameMirror.Add(Random.value < 0.5f);
        }
    }

    // The 3D look, without the sprite art.
    private void BuildModel()
    {
        Material material = ConsumablePickup.DefaultMaterial;

        // A soft orange glow on the floor under it, like the ammo pickups' (none if there's no
        // glow material set up).
        AmmoVisuals visuals = AmmoVisuals.Current;

        if (visuals != null && visuals.glowMaterial != null)
        {
            GameObject pool = GameObject.CreatePrimitive(PrimitiveType.Quad);
            pool.name = "Glow";
            Destroy(pool.GetComponent<Collider>());
            pool.transform.SetParent(transform, false);
            pool.transform.localPosition = Vector3.up * 0.015f;
            pool.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            pool.transform.localScale = Vector3.one * radius * 2.6f;
            poolRenderer = pool.GetComponent<Renderer>();
            poolRenderer.sharedMaterial = visuals.glowMaterial;
            poolRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            poolRenderer.receiveShadows = false;
        }

        for (int i = 0; i < FlameCount; i++)
        {
            GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flame.name = "Flame";
            Destroy(flame.GetComponent<Collider>());
            flame.transform.SetParent(transform, false);

            // Bunched towards the middle, where the fuel is deepest.
            Vector2 spot = Random.insideUnitCircle * radius * Mathf.Lerp(0.3f, 0.9f, i / (float)FlameCount);
            flame.transform.localPosition = new Vector3(spot.x, 0f, spot.y);
            flame.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);

            Renderer renderer = flame.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (material != null)
                renderer.sharedMaterial = material;

            flames.Add(flame.transform);
            flameSizes.Add(new Vector3(Random.Range(0.07f, 0.14f), Random.Range(0.4f, 1.1f), Random.Range(0.07f, 0.14f)));
            flameRenderers.Add(renderer);
        }
    }

    private void Update()
    {
        float age = Time.time - started;

        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }

        // 1 while burning, falling to 0 over the last second.
        float strength = Mathf.Clamp01((life - age) / FadeTime) * Mathf.Clamp01(age / 0.15f);

        // Pixel fire: burning loops, then the dying frames over the last second.
        float dying = 1f - (life - age) / FadeTime;
        int dyingFrame = Mathf.Min(3, Mathf.FloorToInt(Mathf.Max(0f, dying) * 4f));

        if (poolSprite != null)
            poolSprite.SetFrame(dying < 0f ? Mathf.FloorToInt(age * PoolFps) % 4 : dyingFrame, dying < 0f ? 0 : 1, false);

        for (int i = 0; i < flameSprites.Count; i++)
        {
            int frame = dying < 0f ? Mathf.FloorToInt(age * FlameFps + flamePhase[i]) % 6 : dyingFrame;
            flameSprites[i].SetFrame(frame, dying < 0f ? 0 : 1, flameMirror[i]);
        }

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

        foreach (Collider collider in Physics.OverlapSphere(centre, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 offset = collider.transform.position - transform.position;
            offset.y = 0f;

            if (offset.magnitude > radius)
                continue;

            if (collider.TryGetComponent(out Enemy enemy) && done.Add(enemy))
                enemy.TakeDamage(1);
            else if (collider.TryGetComponent(out PlayerHealth player) && done.Add(player))
                player.TakeDamage(1, transform.position, true);
        }
    }
}
