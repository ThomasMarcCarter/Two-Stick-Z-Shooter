using UnityEngine;

// What's left of a zombie (or boss) drawn as a sprite: one of its sheet's "corpse" frames (the
// dead frame bloodied, dirtied and tinted a few ways by Tools/Sprites/corpses.py; the plain "dead"
// frame without them), lying where it fell, facing the way it was going, for a few seconds after
// the kill. Then it sinks out of sight
// through the floor (the floor hides it as it goes down) and is removed. No collider: it's only
// to look at. Parented to the room, so a new floor clears them. Left by Enemy when it dies.
// First the body topples (2026-10-27): its last standing frame tips over about its feet toward
// where the corpse's head will lie, then the corpse frame drops in with a small squash.
public class Corpse : MonoBehaviour
{
    private const float Lifetime = 4f;
    private const float SinkTime = 0.6f;
    private const float SinkDepth = 0.6f;
    private const float FallTime = 0.22f;
    private const float FallAngle = 82f;
    private const float LandTime = 0.12f;

    private PixelSprite sprite;
    private float sinkAt;
    private PixelSprite falling;
    private float fallSign;
    private float fellAt;
    private float landedAt = -1f;

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
    }

    private void Update()
    {
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
