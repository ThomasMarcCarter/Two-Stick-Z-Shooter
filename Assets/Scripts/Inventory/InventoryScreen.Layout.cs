using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// The InventoryScreen's layout and building: where each card, mod slot, backpack cell and quick
// bar slot sits (panel space, origin at the centre), and the panel built from them.
public partial class InventoryScreen
{
    private float Left => -size.x / 2f + 24f;
    private float Right => size.x / 2f - 24f;
    private float Top => size.y / 2f - 20f;
    private float Middle => -10f;

    private Rect CardRect(int index)
    {
        float top = Top - 66f - index * (CardHeight + ModBarHeight + 16f);
        return Rect.MinMaxRect(Left, top - CardHeight, Middle - 14f, top);
    }

    private Rect ModBarRect(int index)
    {
        Rect card = CardRect(index);
        return Rect.MinMaxRect(card.xMin, card.yMin - 6f - ModBarHeight, card.xMax, card.yMin - 6f);
    }

    private Rect ModSlotRect(int gun, int slot)
    {
        Rect bar = ModBarRect(gun);
        const float gap = 5f;
        float width = (bar.width - gap * (ModSlots.Length - 1)) / ModSlots.Length;
        return new Rect(bar.xMin + slot * (width + gap), bar.yMin, width, bar.height);
    }

    private float CellSize => (Right - (Middle + 14f) - CellGap * (Backpack.Columns - 1)) / Backpack.Columns;

    private Rect CellRect(int index)
    {
        int column = index % Backpack.Columns;
        int row = index / Backpack.Columns;
        float cell = CellSize;
        float left = Middle + 14f;
        float top = Top - 66f;
        return new Rect(left + column * (cell + CellGap), top - cell - row * (cell + CellGap), cell, cell);
    }

    // The quick-use slots, in a row under the backpack.
    private Rect QuickSlotRect(int index)
    {
        Rect lastCell = CellRect(Backpack.CellCount - 1);
        float cell = CellSize;
        float height = cell * 0.85f;
        float top = lastCell.yMin - 28f;
        return new Rect(Middle + 14f + index * (cell + CellGap), top - height, cell, height);
    }

    // The story slot: the rest of the quick bar's row, past its four slots.
    private Rect StorySlotRect
    {
        get
        {
            Rect first = QuickSlotRect(QuickBar.SlotCount);
            return Rect.MinMaxRect(first.xMin, first.yMin, Right, first.yMax);
        }
    }

    private StorySlot Story => weapons != null ? StorySlot.For(weapons.gameObject) : null;

    private Rect PanelBounds => new Rect(-size.x / 2f, -size.y / 2f, size.x, size.y);

    private QuickBar Quick => weapons != null ? weapons.QuickBar : null;

    private void Build()
    {
        panel = new GameObject("Panel", typeof(RectTransform));
        panel.layer = gameObject.layer;
        panel.transform.SetParent(transform, false);
        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = size;

        panel.AddComponent<CanvasRenderer>();
        CallbackGraphic drawing = panel.AddComponent<CallbackGraphic>();
        drawing.Draw = DrawPanel;

        Color dim = Fade(UiColours.Phosphor, 0.55f);

        MakeText(panel.transform, "> INVENTORY", 26, new Rect(Left, Top - 34f, 400f, 34f), UiColours.Phosphor, TextAlignmentOptions.Left, 10f);
        hint = MakeText(panel.transform, "", 12, new Rect(Right - 360f, Top - 30f, 360f, 20f), dim, TextAlignmentOptions.Right);
        MakeText(panel.transform, "WEAPONS", 13, new Rect(Left, Top - 60f, 300f, 20f), dim, TextAlignmentOptions.Left);
        MakeText(panel.transform, "BACKPACK", 13, new Rect(Middle + 14f, Top - 60f, 200f, 20f), dim, TextAlignmentOptions.Left);
        capacity = MakeText(panel.transform, "", 11, new Rect(Right - 200f, Top - 60f, 200f, 20f), dim, TextAlignmentOptions.Right);

        Rect lastCell = CellRect(Backpack.CellCount - 1);
        MakeText(panel.transform, "QUICK USE", 11, new Rect(Middle + 14f, lastCell.yMin - 24f, 200f, 18f), dim, TextAlignmentOptions.Left);

        for (int i = 0; i < QuickBar.SlotCount; i++)
        {
            Rect slot = QuickSlotRect(i);
            quickMain[i] = MakeText(panel.transform, "", 14, new Rect(slot.xMin + 2f, slot.yMin + 17f, slot.width - 4f, 20f), UiColours.Phosphor, TextAlignmentOptions.Center, 2f);
            quickSub[i] = MakeLabel(panel.transform, new Rect(slot.xMin + 2f, slot.yMin + 6f, slot.width - 4f, 12f), dim);
        }

        Rect story = StorySlotRect;
        MakeText(panel.transform, "STORY ITEM", 11, new Rect(story.xMin, lastCell.yMin - 24f, story.width, 18f), dim, TextAlignmentOptions.Left);
        storyLabel = MakeLabel(panel.transform, new Rect(story.xMin + 2f, story.yMin + 6f, story.width - 4f, 12f), dim);

        Rect lastQuick = QuickSlotRect(QuickBar.SlotCount - 1);
        footer = MakeText(panel.transform, "", 10, new Rect(Middle + 14f, lastQuick.yMin - 40f, Right - Middle - 14f, 34f), dim, TextAlignmentOptions.TopLeft, 3f);
        footer.textWrappingMode = TextWrappingModes.Normal;

        for (int i = 0; i < 2; i++)
        {
            Rect card = CardRect(i);
            cardLabels[i] = MakeText(panel.transform, "", 11, new Rect(card.xMin + 10f, card.yMax - 22f, card.width - 20f, 16f), dim, TextAlignmentOptions.Left);
            cardNames[i] = MakeText(panel.transform, "", 18, new Rect(card.xMin + 170f, card.yMax - 52f, card.width - 180f, 24f), UiColours.Phosphor, TextAlignmentOptions.Left);
            cardDetails[i] = MakeText(panel.transform, "", 12, new Rect(card.xMin + 170f, card.yMin + 10f, card.width - 180f, 42f), dim, TextAlignmentOptions.TopLeft);
            cardDetails[i].textWrappingMode = TextWrappingModes.Normal;

            for (int s = 0; s < ModSlots.Length; s++)
            {
                Rect slot = ModSlotRect(i, s);
                modLabels[i, s] = MakeLabel(panel.transform, new Rect(slot.xMin + 3f, slot.yMin + 3f, slot.width - 6f, 11f), dim);
            }
        }

        // Melee: its stats and upgrades, under the sidearm.
        Rect lastBar = ModBarRect(1);
        meleeStats = MakeText(panel.transform, "", 11, new Rect(Left, lastBar.yMin - 22f, Middle - 14f - Left, 18f), UiColours.Phosphor, TextAlignmentOptions.Left, 3f);
        meleeStats.richText = true;
        meleeUpgrades = MakeText(panel.transform, "", 10, new Rect(Left, lastBar.yMin - 38f, Middle - 14f - Left, 16f), dim, TextAlignmentOptions.Left, 3f);
        meleeUpgrades.richText = true;

        for (int i = 0; i < Backpack.CellCount; i++)
        {
            Rect cell = CellRect(i);
            cellMain[i] = MakeText(panel.transform, "", 15, new Rect(cell.xMin + 2f, cell.yMin + 22f, cell.width - 4f, 22f), UiColours.Phosphor, TextAlignmentOptions.Center, 2f);
            cellSub[i] = MakeLabel(panel.transform, new Rect(cell.xMin + 2f, cell.yMin + 9f, cell.width - 4f, 12f), dim);
        }

        BuildOverlay();
    }

    private void BuildOverlay()
    {
        GameObject overlayObject = new GameObject("Overlay", typeof(RectTransform));
        overlayObject.layer = gameObject.layer;
        overlayObject.transform.SetParent(panel.transform, false);
        overlay = overlayObject.GetComponent<RectTransform>();
        overlay.anchorMin = overlay.anchorMax = new Vector2(0.5f, 0.5f);
        overlay.sizeDelta = size;

        overlayObject.AddComponent<CanvasRenderer>();
        CallbackGraphic drawing = overlayObject.AddComponent<CallbackGraphic>();
        drawing.Draw = DrawOverlay;

        float cell = CellSize;
        ghostMain = MakeText(overlay, "", 15, new Rect(0f, 0f, cell * 0.9f, 22f), UiColours.Phosphor, TextAlignmentOptions.Center, 2f);
        ghostSub = MakeLabel(overlay, new Rect(0f, 0f, cell * 0.9f, 12f), Fade(UiColours.Phosphor, 0.7f));

        tooltipText = MakeText(overlay, "", 14, new Rect(0f, 0f, 260f, 80f), UiColours.Phosphor, TextAlignmentOptions.TopLeft, 3f);
        tooltipText.richText = true;
        tooltipRect = tooltipText.rectTransform;
    }

    // The small caption along the bottom of a slot or cell; long names shrink to fit.
    private TMP_Text MakeLabel(Transform parent, Rect rect, Color colour)
    {
        TMP_Text label = MakeText(parent, "", 8, rect, colour, TextAlignmentOptions.Center, 1f);
        label.enableAutoSizing = true;
        label.fontSizeMin = 5f;
        label.fontSizeMax = 8f;
        return label;
    }

    private TMP_Text MakeText(Transform parent, string text, float fontSize, Rect rect, Color colour, TextAlignmentOptions alignment, float spacing = 6f)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);

        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = Vector2.zero;
        r.anchoredPosition = rect.position;
        r.sizeDelta = rect.size;

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.color = colour;
        label.characterSpacing = spacing;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }
}
