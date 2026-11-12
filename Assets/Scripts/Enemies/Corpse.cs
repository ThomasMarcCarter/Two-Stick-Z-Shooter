using UnityEngine;

// What's left of a zombie (or boss) drawn as a sprite: one of its sheet's "corpse" frames (the
// dead frame bloodied, dirtied and tinted a few ways by Tools/Sprites/corpses.py; the plain "dead"
// frame without them), lying where it fell, facing the way it was going, for a few seconds after
// the kill. Then it sinks out of sight
// through the floor (the floor hides it as it goes down) and is removed. No collider: it's only
// to look at. Parented to the room, so a new floor clears them. Left by Enemy when it dies.
// First the body topples (2026-10-27): its last standing frame tips over about its feet toward
// where the corpse's head will lie, then the corpse frame drops in with a small squash.
// Knocked back (2026-11-12): the body slides a little the way the killing bullet or swing was
// going while it falls, stopping short of anything solid in the way.
public class Corpse : MonoBehaviour
{
    private const float Lifetime = 4f;
    private const float SinkTime = 0.6f;
    private const float SinkDepth = 0.6f;
    private const float FallTime = 0.22f;
    private const float FallAngle = 82f;
    private const float LandTime = 0.12f;
    private const float SlideDistance = 0.9f;
    private const float SlideTime = 0.3f;

    private PixelSprite sprite;
    private float sinkAt;
    private PixelSprite falling;
    private float fallSign;
    private float fellAt;
    private float landedAt = -1f;
    private Vector3 slideFrom;
    private Vector3 slide;

    public static void Leave(Enemy enemy)
    {
        SpriteCharacter body = enemy != null ? enemy.GetComponent<SpriteCharacter>() : null;
        SpriteSheet sheet = body != null && body.Sprite != null ? body.Sprite.Sheet : null;
        SpriteSheet.Clip dead = sheet != null ? sheet.Find("corpse") ?? sheet.Find("dead") : null;

        if (dead == null)
            return;

        GameObject root = new GameObject("Corpse");
        root.transform.SetParent(enemy.Room != null ? enemy.Room.transform : null, true);
        root.transform.position = new Vector3(enemy.transform.position.x, 0f, enemy.transform.position.z);

        Corpse corpse = root.AddComponent<Corpse>();
        corpse.sprite = PixelSprite.Create("Sprite", root.transform, sheet, PixelSprite.Mode.Upright, body.Sprite.Renderer.renderingLayerMask);
        corpse.sprite.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Lying head to the right in the art: mirrored when it fell facing left.
        bool left = body.CurrentFacing.Mirror;
        corpse.sprite.SetFrame(Random.Range(0, Mathf.Max(1, dead.frames)), dead.row, left);
        corpse.sinkAt = Time.time + FallTime + Lifetime;

        // The standing body, tipping over toward the corpse's head (to the right unless mirrored).
        corpse.falling = PixelSprite.Create("Falling", root.transform, sheet, PixelSprite.Mode.Facing, body.Sprite.Renderer.renderingLayerMask);
        corpse.falling.Standing = true;
        corpse.falling.Position = root.transform.position;
        corpse.falling.SetFrame(body.Sprite.Column, body.Sprite.Row, body.Sprite.Mirrored);
        corpse.fallSign = left ? 1f : -1f;
        corpse.fellAt = Time.time;
        corpse.sprite.Renderer.enabled = false;
        corpse.slideFrom = root.transform.position;
        corpse.slide = SlideFor(enemy);
    }

    // How far the body is thrown along the killing blow: less for heavy bodies, and never into a
    // wall or anything else solid.
    private static Vector3 SlideFor(Enemy enemy)
    {
        Vector3 direction = enemy.LastHitDirection;

        if (direction.sqrMagnitude < 0.01f)
            return Vector3.zero;

        float distance = SlideDistance * (enemy.Heavy ? 0.3f : 1f);
        Vector3 from = enemy.transform.position;
        from.y = 0.4f;

        if (Physics.Raycast(from, direction, out RaycastHit hit, distance + 0.35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(0f, hit.distance - 0.35f);

        return direction * distance;
    }

    // Easing out along the slide, the falling body with it.
    private void Slide()
    {
        float t = Mathf.Clamp01((Time.time - fellAt) / SlideTime);
        transform.position = slideFrom + slide * (1f - (1f - t) * (1f - t));

        if (falling != null)
            falling.Position = transform.position;
    }

    private void Update()
    {
        if (slide != Vector3.zero)
            Slide();

        if (falling != null)
        {
            Fall();
            return;
        }

        if (landedAt >= 0f)
        {
            float s = 1f - Mathf.Clamp01((Time.time - landedAt) / LandTime);
            sprite.Stretch = new Vector2(1f + 0.12f * s, 1f - 0.25f * s);
        }

        if (Time.time < sinkAt)
            return;

        float t = (Time.time - sinkAt) / SinkTime;
        sprite.Lift = -SinkDepth * Mathf.Clamp01(t);

        if (t >= 1f)
            Destroy(gameObject);
    }

    // Tipping faster as it goes, like a falling body; then the corpse takes over.
    private void Fall()
    {
        float t = Mathf.Clamp01((Time.time - fellAt) / FallTime);
        falling.Angle = fallSign * FallAngle * t * t;

        if (t < 1f)
            return;

        Destroy(falling.gameObject);
        falling = null;
        sprite.Renderer.enabled = true;
        landedAt = Time.time;
    }
}
