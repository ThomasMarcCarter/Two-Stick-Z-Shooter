using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Pickup pop (2026-11-12): taking something off the floor pops its pixel picture up where it lay
// (a quick overshooting grow), then flies it on an arc to the part of the HUD it went to,
// shrinking and fading as it lands: ammo, attachments and melee upgrades to the gun panel,
// consumables and materials to the quick bar, armour plates to the vitals. Phosphor-tinted like
// every HUD picture (PixelIcon, from the pickup's own floor sprite); a plain diamond without the
// art. Runs on unscaled time; a pickup topped up again within ThrottleTime doesn't pop twice.
// Made on the HUD canvas just under the full-screen map, so menus cover it.
public class PickupPop : MaskableGraphic
{
    public enum Target { Weapon, QuickBar, Vitals }

    private const float PopTime = 0.14f;
    private const float FlyTime = 0.42f;
    private const float Size = 46f;
    private const float PopScale = 1.3f;
    private const float LandScale = 0.5f;
    private const float ThrottleTime = 0.35f;

    private static readonly Dictionary<GameObject, float> lastPop = new Dictionary<GameObject, float>();

    private SpriteSheet sheet;
    private Vector2Int frame;
    private Vector2 from;
    private Vector2 to;
    private Vector2 bend;
    private float born;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        lastPop.Clear();
    }

    public static void Fly(GameObject pickup, Target target)
    {
        if (pickup == null || GameFlow.ScreenOverWorld)
            return;

        if (lastPop.TryGetValue(pickup, out float last) && Time.unscaledTime - last < ThrottleTime)
            return;

        // Taken pickups are gone; forgetting them all now and then keeps this small.
        if (lastPop.Count > 64)
            lastPop.Clear();

        lastPop[pickup] = Time.unscaledTime;

        Camera camera = Camera.main;
        Crosshair crosshair = FindAnyObjectByType<Crosshair>();
        Canvas canvas = crosshair != null ? crosshair.canvas : null;

        if (camera == null || canvas == null)
            return;

        RectTransform canvasRect = (RectTransform)canvas.rootCanvas.transform;
        Camera uiCamera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
        Vector3 screen = camera.WorldToScreenPoint(pickup.transform.position + Vector3.up * 0.3f);

        if (screen.z < 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out Vector2 start))
            return;

        GameObject go = new GameObject("Pickup Pop", typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = canvasRect.gameObject.layer;
        go.transform.SetParent(canvasRect, false);
        Transform map = canvasRect.Find("Fullscreen Map");

        if (map != null)
            go.transform.SetSiblingIndex(map.GetSiblingIndex());

        PickupPop pop = go.AddComponent<PickupPop>();
        pop.raycastTarget = false;
        pop.color = UiColours.Phosphor;
        pop.rectTransform.sizeDelta = new Vector2(Size, Size);

        ItemSprite item = pickup.GetComponentInChildren<ItemSprite>();
        PixelSprite sprite = item != null ? item.Sprite : null;

        if (sprite != null)
        {
            pop.sheet = sprite.Sheet;
            pop.frame = new Vector2Int(sprite.Column, sprite.Row);
        }

        pop.from = start;
        pop.to = TargetPoint(canvasRect, target);
        pop.bend = (start + pop.to) * 0.5f + Vector2.up * 140f;
        pop.born = Time.unscaledTime;
        pop.Place(start, 0f, 1f);
    }

    // Where each part of the HUD sits, in the canvas's own space (centred).
    private static Vector2 TargetPoint(RectTransform canvasRect, Target target)
    {
        string name = target == Target.Vitals ? "Health" : target == Target.QuickBar ? "Quick Bar" : null;
        RectTransform part = name != null ? canvasRect.Find(name) as RectTransform : null;

        if (part != null && part.rect.width > 1f)
            return canvasRect.InverseTransformPoint(part.TransformPoint(part.rect.center));

        // The gun panel (and a fallback for the others) is drawn in the bottom-left corner.
        Rect area = canvasRect.rect;
        return target == Target.QuickBar
            ? new Vector2(area.xMin + area.width * 0.37f, area.yMin + 70f)
            : new Vector2(area.xMin + 200f, area.yMin + 90f);
    }

    private void Update()
    {
        if (GameFlow.ScreenOverWorld)
        {
            Destroy(gameObject);
            return;
        }

        float age = Time.unscaledTime - born;

        if (age < PopTime)
        {
            float t = Mathf.Clamp01(age / PopTime);
            Place(from, PopScale * EaseOutBack(t), 1f);
            return;
        }

        float f = (age - PopTime) / FlyTime;

        if (f >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        float e = f * f;
        Vector2 a = Vector2.Lerp(from, bend, e);
        Vector2 b = Vector2.Lerp(bend, to, e);
        Place(Vector2.Lerp(a, b, e), Mathf.Lerp(PopScale, LandScale, e), Mathf.Lerp(1f, 0.35f, e));
    }

    private void Place(Vector2 position, float scale, float alpha)
    {
        rectTransform.anchoredPosition = position;
        rectTransform.localScale = Vector3.one * scale;
        canvasRenderer.SetAlpha(alpha);
    }

    private static float EaseOutBack(float t)
    {
        const float c = 1.7f;
        t -= 1f;
        return 1f + (c + 1f) * t * t * t + c * t * t;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect area = GetPixelAdjustedRect();

        if (sheet != null && PixelIcon.Draw(vh, sheet, frame, area, color))
            return;

        // No art: a small diamond.
        Vector2 c = area.center;
        float r = area.width * 0.3f;
        vh.AddVert(c + new Vector2(0f, r), color, Vector2.zero);
        vh.AddVert(c + new Vector2(r, 0f), color, Vector2.zero);
        vh.AddVert(c + new Vector2(0f, -r), color, Vector2.zero);
        vh.AddVert(c + new Vector2(-r, 0f), color, Vector2.zero);
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }
}
