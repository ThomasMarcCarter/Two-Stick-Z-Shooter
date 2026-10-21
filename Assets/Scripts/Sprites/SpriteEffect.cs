using UnityEngine;

// A one-shot pixel-art effect: one row of a sheet played through once, standing up from the
// floor at a point (like a character, so it isn't cut by the floor), then gone. Drawn unlit so it
// glows (PixelSprite.UseGlow), nudged toward the camera so a body standing on the spot doesn't
// hide it, and randomly mirrored for variety. Used for grenade explosions, the acid burst of a
// bloater and the splash of a Broodmother's glob.
public class SpriteEffect : MonoBehaviour
{
    private const float TowardCamera = 0.6f;

    private PixelSprite sprite;
    private int row;
    private int frames;
    private float fps;
    private bool mirror;
    private float started;

    // `width` is how wide the whole frame is shown, in metres. Returns null without the sheet.
    public static SpriteEffect Play(SpriteSheet sheet, int row, float fps, Vector3 position, float width, Transform room)
    {
        if (sheet == null || sheet.texture == null)
            return null;

        GameObject root = new GameObject(sheet.name + " Effect");
        root.transform.SetParent(room, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);

        SpriteEffect effect = root.AddComponent<SpriteEffect>();
        effect.sprite = PixelSprite.Create("Sprite", root.transform, sheet, PixelSprite.Mode.Facing);
        effect.sprite.Standing = true;
        effect.sprite.Position = root.transform.position;
        effect.sprite.Scale = width / (sheet.frameWidth / Mathf.Max(1f, sheet.pixelsPerUnit));
        effect.sprite.DepthOffset = -TowardCamera;
        effect.sprite.UseGlow();
        effect.row = row;
        effect.frames = Mathf.Max(1, sheet.texture.width / sheet.frameWidth);
        effect.fps = fps;
        effect.mirror = Random.value < 0.5f;
        effect.started = Time.time;
        effect.Update();
        return effect;
    }

    private void Update()
    {
        int frame = Mathf.FloorToInt((Time.time - started) * fps);

        if (frame >= frames)
        {
            Destroy(gameObject);
            return;
        }

        sprite.SetFrame(frame, row, mirror);
    }
}
