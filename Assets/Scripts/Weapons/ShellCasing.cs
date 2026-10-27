using System.Collections.Generic;
using UnityEngine;

// A spent case thrown out of the gun's right side on every shot (brass, or a red shotgun shell):
// it flies up and out, spinning through the sheet's four turns, bounces once off the floor and
// then lies there as a flat sprite at whatever angle it landed. Parented to the room it lands
// in, so a new floor clears them; the oldest go first past the cap. Gun calls Eject from Fire
// when the sprite art has a casing sheet (SpriteVisuals.casing).
public class ShellCasing : MonoBehaviour
{
    private const int MaxCasings = 150;
    private const float Gravity = 9.8f;
    private const float Floor = 0.02f;

    private static readonly Queue<GameObject> casings = new Queue<GameObject>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        casings.Clear();
    }

    private SpriteSheet sheet;
    private PixelSprite sprite;
    private Vector3 velocity;
    private float spin;
    private float angle;
    private int row;
    private bool bounced;
    private bool resting;

    // From the ejection port, for a shot fired along aim.
    public static void Eject(Vector3 from, Vector3 aim, bool shotgun)
    {
        SpriteSheet sheet = SpriteVisuals.Current != null ? SpriteVisuals.Current.casing : null;
        aim.y = 0f;

        if (sheet == null || sheet.texture == null || aim.sqrMagnitude < 0.0001f)
            return;

        aim.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, aim);

        GameObject root = new GameObject("Casing");
        root.transform.position = from;
        root.transform.SetParent(FloorPickups.RoomAt(from), true);

        ShellCasing casing = root.AddComponent<ShellCasing>();
        casing.sheet = sheet;
        casing.row = shotgun ? 1 : 0;
        casing.velocity = right * Random.Range(1.6f, 2.6f) - aim * Random.Range(0.2f, 0.8f) + Vector3.up * Random.Range(1.8f, 2.8f);
        casing.spin = Random.Range(900f, 1600f) * (Random.value < 0.5f ? -1f : 1f);
        casing.angle = Random.Range(0f, 180f);

        casing.sprite = PixelSprite.Create("Sprite", root.transform, sheet, PixelSprite.Mode.Facing);
        // Toward the camera, so the floor doesn't cut the low half of its quad.
        casing.sprite.DepthOffset = -0.3f;
        casing.sprite.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        casing.Show();

        casings.Enqueue(root);

        while (casings.Count > MaxCasings)
        {
            GameObject oldest = casings.Dequeue();

            if (oldest != null)
                Destroy(oldest);
        }
    }

    private void Update()
    {
        if (resting || sprite == null)
            return;

        float dt = Time.deltaTime;
        velocity.y -= Gravity * dt;
        Vector3 position = transform.position + velocity * dt;
        angle += spin * dt;

        if (position.y <= Floor)
        {
            position.y = Floor;

            if (bounced)
            {
                transform.position = position;
                Rest();
                return;
            }

            bounced = true;
            velocity = new Vector3(velocity.x * 0.5f, -velocity.y * 0.3f, velocity.z * 0.5f);
            spin *= 0.5f;
        }

        transform.position = position;
        Show();
    }

    // The turn nearest the spin so far, in the air.
    private void Show()
    {
        int turn = Mathf.RoundToInt(Mathf.Repeat(angle, 180f) / 45f) % 4;
        sprite.Position = transform.position;
        sprite.SetFrame(turn, row, false);
    }

    // Lying on the floor, turned the way it landed.
    private void Rest()
    {
        Destroy(sprite.gameObject);
        transform.rotation = Quaternion.Euler(0f, angle, 0f);
        sprite = PixelSprite.Create("Sprite", transform, sheet, PixelSprite.Mode.Flat);
        sprite.Lift = Random.Range(0.006f, 0.01f);
        sprite.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sprite.SetFrame(0, row, false);
        resting = true;
    }
}
