using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared builders for the code-built CRT screens: a full-screen root on the HUD canvas just below
// the Crosshair (the menu cursor, which has to draw on top), a dark panel with a thin phosphor
// frame, text placed from the panel's top or bottom edge, and phosphor CrtButtons (the station
// and crate screens). Under them, general builders any screen can lay out its own way: a rect,
// a label and a button on a rect (the main menu, settings and radio messages).
public static class CrtPanel
{
    // A full-screen root on the HUD canvas, below the Crosshair, or null without a HUD.
    public static GameObject CreateRoot(string name, out Canvas canvas)
    {
        EquipmentHud hud = Object.FindAnyObjectByType<EquipmentHud>();
        canvas = hud != null ? hud.canvas : null;

        if (canvas == null)
            return null;

        GameObject root = new GameObject(name, typeof(RectTransform));
        root.layer = canvas.gameObject.layer;
        root.transform.SetParent(canvas.transform, false);

        Transform crosshair = canvas.transform.Find("Crosshair");

        if (crosshair != null)
            root.transform.SetSiblingIndex(crosshair.GetSiblingIndex());

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
        return root;
    }

    // The font the rest of the HUD uses.
    public static TMP_FontAsset Font(Canvas canvas)
    {
        TMP_Text sample = canvas.GetComponentInChildren<TMP_Text>(true);
        return sample != null ? sample.font : TMP_Settings.defaultFontAsset;
    }

    // A centred panel in the panel colour with a thin phosphor frame (an Outline effect would copy
    // the whole filled panel in green).
    public static GameObject CreatePanel(Transform root, Vector2 size)
    {
        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.layer = root.gameObject.layer;
        panel.transform.SetParent(root, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = size;

        Image fill = panel.AddComponent<Image>();
        fill.color = UiColours.PanelBackground;
        fill.raycastTarget = false;

        Color frame = UiColours.WithAlpha(UiColours.Phosphor, 0.6f);
        FrameEdge(panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1.5f), frame);
        FrameEdge(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1.5f), frame);
        FrameEdge(panel.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1.5f, 0f), frame);
        FrameEdge(panel.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1.5f, 0f), frame);
        return panel;
    }

    // One edge of a panel's frame, stretched along the panel between two anchors.
    private static void FrameEdge(Transform panel, Vector2 anchorMin, Vector2 anchorMax, Vector2 thickness, Color colour)
    {
        GameObject edge = new GameObject("Frame", typeof(RectTransform));
        edge.layer = panel.gameObject.layer;
        edge.transform.SetParent(panel, false);
        RectTransform rect = edge.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = thickness;
        rect.anchoredPosition = Vector2.zero;

        Image image = edge.AddComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
    }

    // `edge` is 1 to place it from the panel's top, 0 from its bottom.
    public static TMP_Text MakeText(Transform panel, TMP_FontAsset font, string name, string text, float size, Vector2 position, float edge,
        Vector2 box, Color colour, float spacing, TextAlignmentOptions alignment)
    {
        Vector2 anchor = new Vector2(0.5f, edge);
        RectTransform rect = MakeRect(panel, name, anchor, new Vector2(0.5f, 0.5f), position, box);
        return MakeLabel(rect, font, text, size, colour, spacing, alignment);
    }

    // A phosphor button, anchored to the panel's top edge (the caller positions it).
    public static CrtButton MakeButton(Transform panel, TMP_FontAsset font, string name, Vector2 size, TextAlignmentOptions alignment)
    {
        RectTransform rect = MakeRect(panel, name, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        return MakeButton(rect, font, 16f, 4f, alignment, 10f);
    }

    // ---------- General builders (any layout: menus, settings, HUD notices) ----------

    // An empty child of `parent` on its layer, anchored at `anchor` with its pivot at `pivot`.
    public static RectTransform MakeRect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    // A one-line, rich-text label on `rect` that clicks pass through.
    public static TextMeshProUGUI MakeLabel(RectTransform rect, TMP_FontAsset font, string text, float size, Color colour, float spacing,
        TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.color = colour;
        label.characterSpacing = spacing;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.richText = true;
        label.text = text;
        return label;
    }

    // Makes `rect` a phosphor CrtButton: a faint green fill (`fill` alpha) that brightens on hover
    // (`hover`), its label inset `inset` pixels from the sides, green turning white on hover.
    public static CrtButton MakeButton(RectTransform rect, TMP_FontAsset font, float fontSize, float spacing, TextAlignmentOptions alignment,
        float inset, float fill = 0.08f, float hover = 0.35f)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;

        RectTransform labelRect = MakeRect(rect, "Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(inset, 0f);
        labelRect.offsetMax = new Vector2(-inset, 0f);

        TextMeshProUGUI label = MakeLabel(labelRect, font, "", fontSize, Color.white, spacing, alignment);
        label.lineSpacing = 6f;

        CrtButton button = rect.gameObject.AddComponent<CrtButton>();
        button.Setup(image, label,
            UiColours.WithAlpha(UiColours.Phosphor, fill),
            UiColours.WithAlpha(UiColours.Phosphor, hover),
            UiColours.WithAlpha(UiColours.Phosphor, 0.85f),
            Color.white);
        return button;
    }
}
