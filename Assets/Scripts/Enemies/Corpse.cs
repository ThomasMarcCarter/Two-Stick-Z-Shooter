using UnityEngine;

// What's left of a zombie (or boss) drawn as a sprite: its sheet's "dead" frame, lying where it
// fell, facing the way it was going, for a few seconds after the kill. Then it sinks out of sight
// through the floor (the floor hides it as it goes down) and is removed. No collider: it's only
// to look at. Parented to the room, so a new floor clears them. Left by Enemy when it dies.
public class Corpse : MonoBehaviour
{
    private const float Lifetime = 8f;
    private const float SinkTime = 1f;
    private const float SinkDepth = 0.6f;

    private PixelSprite sprite;
    private float sinkAt;

    public static void Leave(Enemy enemy)
    {
        SpriteCharacter body = enemy != null ? enemy.GetComponent<SpriteCharacter>() : null;
        SpriteSheet sheet = body != null && body.Sprite != null ? body.Sprite.Sheet : null;
        SpriteSheet.Clip dead = sheet != null ? sheet.Find("dead") : null;

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
        corpse.sprite.SetFrame(0, dead.row, left);
        corpse.sinkAt = Time.time + Lifetime;
    }

    private void Update()
    {
        if (Time.time < sinkAt)
            return;

        float t = (Time.time - sinkAt) / SinkTime;
        sprite.Lift = -SinkDepth * Mathf.Clamp01(t);

        if (t >= 1f)
            Destroy(gameObject);
    }
}
