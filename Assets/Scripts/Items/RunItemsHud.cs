using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// The run items held, as a row of small phosphor pictures above the quick bar (PixelIcon; a plain
// plate shape without the art). Empty until the first one, and hidden while the inventory is open,
// which lists them with what they do. Built in code by EquipmentHud.
public class RunItemsHud : MaskableGraphic
{
    private const float IconSize = 26f;
    private const float Gap = 4f;

    private PlayerItems items;
    private CanvasGroup group;

    public static RunItemsHud Create(Transform parent, PlayerWeapon player, Vector2 position)
    {
        GameObject go = new GameObject("Run Items", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(RunItems.All.Length * (IconSize + Gap), IconSize);

        go.AddComponent<CanvasRenderer>();
        RunItemsHud hud = go.AddComponent<RunItemsHud>();
        hud.raycastTarget = false;
        hud.group = go.AddComponent<CanvasGroup>();
        hud.items = PlayerItems.For(player.gameObject);
        hud.items.Changed += hud.SetVerticesDirty;
        return hud;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (items != null)
            items.Changed -= SetVerticesDirty;
    }

    private void Update()
    {
        if (group != null)
            group.alpha = InventoryScreen.IsOpen ? 0f : 1f;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (items == null)
            return;

        for (int i = 0; i < items.Held.Count; i++)
        {
            Rect slot = new Rect(i * (IconSize + Gap), 0f, IconSize, IconSize);
            Rect inner = new Rect(slot.xMin + 3f, slot.yMin + 3f, slot.width - 6f, slot.height - 6f);
            FillRect(vh, slot, Fade(UiColours.Phosphor, 0.08f));
            OutlineRect(vh, slot, 1f, Fade(UiColours.Phosphor, 0.45f));

            if (!PixelIcon.DrawItem(vh, ItemSprites.RunItem(items.Held[i]), inner, UiColours.Phosphor))
                OutlineRect(vh, new Rect(inner.xMin + 4f, inner.yMin + 2f, inner.width - 8f, inner.height - 4f), 1f, UiColours.Phosphor);
        }
    }
}
