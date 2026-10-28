using UnityEngine;

// A lit screen that stutters (room life, 2026-10-28): a desk's CRT, a nurse station, a patient
// monitor, a kiosk now and then blinks its glow layer a few times, and once in a while dies out
// for a second or two before coming back. FloorGenerator adds it to some of the lit pieces it
// places (FloorGenerator.RoomLife.cs). Unity's Random drives it, so layouts don't change.
public class ScreenFlicker : MonoBehaviour
{
    private Renderer glow;
    private float next;
    private int blinks;
    private float blinkUntil;

    public static void Add(GameObject piece)
    {
        // Not a screen already smashed (FurnitureDamage).
        if (piece == null || !piece.TryGetComponent(out FurnitureSprite sprite) || sprite.GlowRenderer == null || sprite.Broken)
            return;

        ScreenFlicker flicker = piece.AddComponent<ScreenFlicker>();
        flicker.glow = sprite.GlowRenderer;
        flicker.next = Time.time + Random.Range(1f, 8f);
    }

    private void Update()
    {
        if (glow == null)
            return;

        if (blinks > 0 && Time.time >= blinkUntil)
        {
            blinks--;
            glow.enabled = !glow.enabled;
            blinkUntil = Time.time + Random.Range(0.03f, 0.12f);

            if (blinks == 0)
                glow.enabled = true;
        }

        if (blinks > 0 || Time.time < next)
            return;

        if (Random.value < 0.12f)
        {
            // A power dip: out for a second or two, then a stutter back on.
            glow.enabled = false;
            blinks = 3;
            blinkUntil = Time.time + Random.Range(0.8f, 2.2f);
        }
        else
        {
            blinks = Random.Range(2, 7);
            blinkUntil = Time.time;
        }

        next = Time.time + Random.Range(3f, 11f);
    }
}
