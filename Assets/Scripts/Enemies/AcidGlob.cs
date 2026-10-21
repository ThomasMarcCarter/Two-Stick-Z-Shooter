using UnityEngine;

// A glob of acid in flight: an arc from the Broodmother to where the player was heading,
// leaving an AcidPool where it lands. With the sprite art it's a glowing pixel globule
// (SpriteVisuals.acidGlob) wobbling as it goes, its drips trailing behind it on screen, with a
// shadow on the floor under it so you can see where it'll come down, and it lands with a small
// acid splash. Without the sprite art it's a glowing green sphere.
public class AcidGlob : MonoBehaviour
{
    private const float FlightTime = 0.85f;
    private const float ArcHeight = 3f;
    private const float WobbleFps = 10f;
    private const float SplashWidth = 2.4f;   // metres across the splash frame

    private static readonly Color Acid = new Color(0.55f, 1f, 0.15f);

    private Vector3 from;
    private Vector3 to;
    private float launched;
    private Transform room;
    private PixelSprite sprite;
    private PixelSprite shadow;
    private Vector3 lastScreen;

    public static void Launch(Vector3 from, Vector3 to, Transform room)
    {
        SpriteSheet sheet = SpriteVisuals.Current != null ? SpriteVisuals.Current.acidGlob : null;
        GameObject glob;

        if (sheet != null && sheet.texture != null)
        {
            glob = new GameObject("Acid Glob");
        }
        else
        {
            glob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glob.name = "Acid Glob";
            Destroy(glob.GetComponent<Collider>());
            glob.transform.localScale = Vector3.one * 0.35f;

            Renderer renderer = glob.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (ConsumablePickup.DefaultMaterial != null)
            {
                renderer.sharedMaterial = ConsumablePickup.DefaultMaterial;
                ConsumableModel.Tint(renderer, new Color(0.3f, 0.6f, 0.05f), Acid * 3f);
            }
        }

        AcidGlob acid = glob.AddComponent<AcidGlob>();
        acid.from = from;
        acid.to = new Vector3(to.x, 0f, to.z);
        acid.launched = Time.time;
        acid.room = room;

        if (sheet != null && sheet.texture != null)
        {
            acid.sprite = PixelSprite.Create("Sprite", glob.transform, sheet, PixelSprite.Mode.Facing);
            acid.sprite.UseGlow();

            // The shadow is lit like the floor it lies on, and doesn't follow the glob up.
            GameObject shadowRoot = new GameObject("Shadow");
            shadowRoot.transform.SetParent(glob.transform, false);
            acid.shadow = PixelSprite.Create("Shadow", shadowRoot.transform, sheet, PixelSprite.Mode.Flat);
            acid.shadow.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            acid.shadow.SetFrame(0, 1, false);

            Light light = glob.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Acid;
            light.range = 3f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
        }

        acid.Update();
    }

    private void Update()
    {
        float t = (Time.time - launched) / FlightTime;

        if (t >= 1f)
        {
            AcidPool.Create(to, room);

            if (sprite != null)
                SpriteEffect.Play(SpriteVisuals.Current.acidBurst, 0, 18f, to, SplashWidth, room);

            Destroy(gameObject);
            return;
        }

        Vector3 position = Vector3.Lerp(from, to, t);
        position.y = Mathf.Lerp(from.y, 0.2f, t) + Mathf.Sin(t * Mathf.PI) * ArcHeight;
        transform.position = position;

        if (sprite == null)
            return;

        sprite.Position = position;
        sprite.SetFrame(Mathf.FloorToInt((Time.time - launched) * WobbleFps) % 4, 0, false);

        // Turned to the way it's moving on screen, so the drips trail behind it.
        Camera camera = Camera.main;

        if (camera != null)
        {
            Vector3 screen = camera.WorldToScreenPoint(position);
            Vector2 moved = screen - lastScreen;

            if (lastScreen != Vector3.zero && moved.sqrMagnitude > 0.01f)
                sprite.Angle = Mathf.Atan2(moved.y, moved.x) * Mathf.Rad2Deg;

            lastScreen = screen;
        }

        // The shadow stays on the floor, growing as the glob comes down.
        shadow.transform.parent.position = new Vector3(position.x, 0f, position.z);
        shadow.Scale = Mathf.Lerp(0.5f, 1f, 1f - Mathf.Clamp01((position.y - 0.2f) / ArcHeight));
    }
}
