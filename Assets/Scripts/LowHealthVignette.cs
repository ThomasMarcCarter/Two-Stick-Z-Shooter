using UnityEngine;
using UnityEngine.UI;

// A red glow round the edges of the screen that pulses like a heartbeat while the player is low
// on health (a third of their maximum or less), faster and stronger on their last point. It
// fades in and out rather than snapping, and goes once they're healed or dead. Drawn as four
// soft-edged bands (opaque at the screen edge, clear towards the middle) under the rest of the
// HUD. Created at runtime by HealthDisplay, stretched over the whole HUD canvas.
public class LowHealthVignette : MaskableGraphic
{
    private static readonly Color Red = new Color(0.85f, 0.04f, 0.02f, 1f);
    private const float LowFraction = 1f / 3f;
    private const float BandFraction = 0.2f;   // how far in from each edge the glow reaches

    private PlayerHealth player;
    private float strength;

    public static LowHealthVignette Create(Transform canvas, PlayerHealth player)
    {
        GameObject go = new GameObject("Low Health Vignette", typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = canvas.gameObject.layer;
        go.transform.SetParent(canvas, false);
        go.transform.SetAsFirstSibling();   // under every other HUD element

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        LowHealthVignette vignette = go.AddComponent<LowHealthVignette>();
        vignette.player = player;
        vignette.raycastTarget = false;
        return vignette;
    }

    private void Update()
    {
        if (player == null)
            return;

        bool low = !player.IsDead && player.Health <= player.MaxHealth * LowFraction;
        float lastPoint = player.Health <= 1 ? 1f : 0f;
        strength = Mathf.MoveTowards(strength, low ? 1f : 0f, Time.unscaledDeltaTime * 2.5f);

        // A heartbeat: a double thump each beat, quicker on the last point of health.
        float rate = Mathf.Lerp(1.1f, 1.7f, lastPoint);
        float beat = Mathf.Repeat(Time.unscaledTime * rate, 1f);
        float thump = Mathf.Max(Pulse(beat, 0f), 0.7f * Pulse(beat, 0.22f));
        float alpha = strength * Mathf.Lerp(0.05f, 0.2f + 0.08f * lastPoint, thump);

        Color shown = new Color(Red.r, Red.g, Red.b, alpha);

        if (color != shown)
            color = shown;
    }

    // A quick rise and slower fall starting at `at` in the beat.
    private static float Pulse(float beat, float at)
    {
        float t = beat - at;

        if (t < 0f || t > 0.35f)
            return 0f;

        return t < 0.06f ? t / 0.06f : 1f - (t - 0.06f) / 0.29f;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float bandX = r.width * BandFraction;
        float bandY = r.height * BandFraction;

        // Each band runs from the screen edge inwards in strips, fading off with the cube of the
        // distance so there's no visible inner edge.
        Band(vh, new Vector2(r.xMin, r.yMin), Vector2.up * r.height, Vector2.right * bandX);
        Band(vh, new Vector2(r.xMax, r.yMin), Vector2.up * r.height, Vector2.left * bandX);
        Band(vh, new Vector2(r.xMin, r.yMin), Vector2.right * r.width, Vector2.up * bandY);
        Band(vh, new Vector2(r.xMin, r.yMax), Vector2.right * r.width, Vector2.down * bandY);
    }

    // A band along the edge from `start` along `along`, reaching `inward` into the screen.
    private void Band(VertexHelper vh, Vector2 start, Vector2 along, Vector2 inward)
    {
        const int strips = 8;

        for (int i = 0; i < strips; i++)
        {
            float near = i / (float)strips;
            float far = (i + 1) / (float)strips;
            Color a = Fade(1f - near);
            Color b = Fade(1f - far);
            int first = vh.currentVertCount;
            vh.AddVert(start + inward * near, a, Vector2.zero);
            vh.AddVert(start + along + inward * near, a, Vector2.zero);
            vh.AddVert(start + along + inward * far, b, Vector2.zero);
            vh.AddVert(start + inward * far, b, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2);
            vh.AddTriangle(first, first + 2, first + 3);
        }
    }

    // The glow's colour `edge` of the way out to the screen edge (1 at the edge, 0 at the band's
    // inner end).
    private Color Fade(float edge)
    {
        return new Color(color.r, color.g, color.b, color.a * edge * edge * edge);
    }
}
