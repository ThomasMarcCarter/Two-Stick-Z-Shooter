using System.Collections;
using UnityEngine;

// Something hurled in a high arc at a spot on the floor (the Handler's suitcases, a lobber's
// lumps of debris): a disc glows on the floor where it'll land (FloorMarker) while it flies over,
// then it bursts there, hurting the player if they're still in the disc (more dead centre) and
// smashing boxes and furniture. Its flight runs on the thrown thing, so it lands even if the
// thrower dies meanwhile. A suitcase is a tinted block; debris is the pixel lump from
// SpriteVisuals.enemyShots (row 2), turning over as it flies (a grey block without the sprite art).
public static class Lobbed
{
    public enum Style { Suitcase, Debris }

    private const float ArcHeight = 3.5f;

    public static void Throw(Vector3 from, Vector3 to, float flightTime, float radius, Color colour,
        Transform room, int damage, int centreDamage, Style style)
    {
        to.y = 0f;
        FloorMarker.Disc(to, radius, colour, flightTime, room, null);
        GameObject thrown = Make(style, out PixelSprite pixels);

        if (room != null)
            thrown.transform.SetParent(room, true);

        thrown.transform.position = from;
        Room owner = room != null ? room.GetComponent<Room>() : null;
        thrown.AddComponent<Flight>().StartCoroutine(Fly(thrown, pixels, owner, from, to, flightTime, radius, damage, centreDamage));
    }

    private class Flight : MonoBehaviour { }

    private static GameObject Make(Style style, out PixelSprite pixels)
    {
        SpriteSheet sheet = SpriteVisuals.Current != null ? SpriteVisuals.Current.enemyShots : null;
        bool sprite = style == Style.Debris && sheet != null && sheet.texture != null;
        GameObject thrown;
        pixels = null;

        if (sprite)
        {
            thrown = new GameObject("Thrown Debris");
            pixels = PixelSprite.Create("Sprite", thrown.transform, sheet, PixelSprite.Mode.Facing);
        }
        else
        {
            thrown = GameObject.CreatePrimitive(PrimitiveType.Cube);
            thrown.name = style == Style.Suitcase ? "Suitcase" : "Thrown Debris";
            Object.Destroy(thrown.GetComponent<Collider>());
            thrown.transform.localScale = style == Style.Suitcase ? new Vector3(0.6f, 0.4f, 0.3f) : Vector3.one * 0.35f;
            Renderer renderer = thrown.GetComponent<Renderer>();

            if (ConsumablePickup.DefaultMaterial != null)
            {
                renderer.sharedMaterial = ConsumablePickup.DefaultMaterial;
                ConsumableModel.Tint(renderer, style == Style.Suitcase ? new Color(0.45f, 0.26f, 0.14f) : new Color(0.45f, 0.44f, 0.4f), Color.black);
            }
        }

        return thrown;
    }

    private static IEnumerator Fly(GameObject thrown, PixelSprite pixels, Room owner, Vector3 from, Vector3 to, float flightTime, float radius, int damage, int centreDamage)
    {
        for (float t = 0f; t < flightTime; t += Time.deltaTime)
        {
            if (thrown == null)
                yield break;

            float k = t / flightTime;
            Vector3 position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * ArcHeight);
            thrown.transform.position = position;

            if (pixels != null)
            {
                pixels.Position = position;
                pixels.SetFrame(Mathf.FloorToInt(t * 10f) % 4, 2, false);
            }
            else
            {
                thrown.transform.Rotate(new Vector3(260f, 120f, 0f) * Time.deltaTime);
            }

            yield return null;
        }

        // The fight's over (the last zombie died with this in the air): it lands harmlessly.
        bool over = owner != null && !owner.IsLocked;
        Burst(to, radius, over ? 0 : damage, over ? 0 : centreDamage);
        Object.Destroy(thrown);
    }

    private static void Burst(Vector3 point, float radius, int damage, int centreDamage)
    {
        CameraFollow.Shake(0.2f);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("cardboard.break", 3, ProceduralAudio.CardboardBreak)), point, 1f, 0.7f);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("metal.clank", 3, ProceduralAudio.MetalClank)), point, 0.7f, 0.8f);

        foreach (Collider part in Physics.OverlapSphere(point + Vector3.up * 0.5f, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (part.attachedRigidbody != null && part.attachedRigidbody.TryGetComponent(out PlayerHealth player))
            {
                float distance = Bosses.FlatDistance(player.transform.position, point);
                player.TakeDamage(distance < radius * 0.4f ? centreDamage : damage, point);
            }
            else if (part.attachedRigidbody == null || part.attachedRigidbody.GetComponent<Enemy>() == null)
            {
                Smashable.Break(part, part.ClosestPoint(point), Bosses.Flat(part.transform.position - point).normalized);
            }
        }
    }
}
