using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// The quick-use bar on screen, beside the equipment panel: the four slots with a wireframe
// picture of what's in each (ConsumableSilhouette), how many are left, and the button that uses it. The selected slot (mouse wheel or D-pad to
// change it) is lit like the gun in hand on the equipment panel: tinted, a bright frame and a
// bar under it, and it shows the use button (RMB or RB); on a keyboard the others show 3-6. The slot being used fills up, with a ring at the cursor like a
// reload, and adrenaline's time left shows above the bar. Past the four slots, while the player
// carries one, is the story slot: the run's truck part as a wireframe with its name. Hidden while
// the inventory is open, which has its own copy of the bar. Built in code by EquipmentHud.
public class QuickBarHud : MaskableGraphic
{
    private const float SlotSize = 54f;
    private const float Gap = 6f;
    private const float RingRadius = 22f;
    private const float StoryWidth = 96f;

    private QuickBar bar;
    private StorySlot story;
    private PlayerStamina stamina;
    private TMP_FontAsset font;
    private Color phosphor;
    private CanvasGroup group;

    private TMP_Text header;
    private TMP_Text status;
    private TMP_Text storyName;
    private readonly TMP_Text[] counts = new TMP_Text[QuickBar.SlotCount];
    private readonly TMP_Text[] keys = new TMP_Text[QuickBar.SlotCount];

    public static QuickBarHud Create(Transform parent, PlayerWeapon player, TMP_FontAsset font, Color phosphor, Vector2 position)
    {
        GameObject go = new GameObject("Quick Bar", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(QuickBar.SlotCount * (SlotSize + Gap) - Gap, SlotSize);

        go.AddComponent<CanvasRenderer>();
        QuickBarHud hud = go.AddComponent<QuickBarHud>();
        hud.raycastTarget = false;
        hud.bar = player.QuickBar;
        hud.story = StorySlot.For(player.gameObject);
        hud.stamina = player.GetComponent<PlayerStamina>();
        hud.font = font;
        hud.phosphor = phosphor;
        hud.group = go.AddComponent<CanvasGroup>();
        hud.BuildText();
        return hud;
    }

    private Rect SlotRect(int slot)
    {
        return new Rect(slot * (SlotSize + Gap), 0f, SlotSize, SlotSize);
    }

    // Past the quick slots, with a wider gap.
    private Rect StoryRect => new Rect(QuickBar.SlotCount * (SlotSize + Gap) + Gap * 2f, 0f, StoryWidth, SlotSize);

    private void BuildText()
    {
        Color dim = Fade(phosphor, 0.6f);
        float width = rectTransform.sizeDelta.x;

        header = MakeText("> QUICK USE", 13, new Rect(0f, SlotSize + 3f, width, 18f), phosphor, TextAlignmentOptions.BottomLeft);
        status = MakeText("", 11, new Rect(0f, SlotSize + 22f, width, 16f), phosphor, TextAlignmentOptions.BottomLeft);

        for (int i = 0; i < QuickBar.SlotCount; i++)
        {
            Rect slot = SlotRect(i);
            counts[i] = MakeText("", 11, new Rect(slot.xMin + 4f, slot.yMin + 4f, slot.width - 8f, 14f), dim, TextAlignmentOptions.BottomRight, 1f);
            keys[i] = MakeText("", 9, new Rect(slot.xMin + 4f, slot.yMax - 15f, slot.width - 8f, 12f), dim, TextAlignmentOptions.TopLeft, 1f);
        }

        Rect storySlot = StoryRect;
        storyName = MakeText("", 9, new Rect(storySlot.xMin + 3f, storySlot.yMin + 3f, storySlot.width - 6f, 12f), dim, TextAlignmentOptions.Bottom, 1f);
        storyName.enableAutoSizing = true;
        storyName.fontSizeMin = 6f;
        storyName.fontSizeMax = 9f;
    }

    private TMP_Text MakeText(string text, float size, Rect rect, Color colour, TextAlignmentOptions alignment, float spacing = 6f)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = Vector2.zero;
        r.pivot = Vector2.zero;
        r.anchoredPosition = rect.position;
        r.sizeDelta = rect.size;

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
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

    private void Update()
    {
        if (!Application.isPlaying || bar == null)
            return;

        group.alpha = InventoryScreen.IsOpen ? 0f : 1f;

        for (int i = 0; i < QuickBar.SlotCount; i++)
        {
            ConsumableType? type = bar[i];
            int count = bar.Count(i);

            counts[i].text = type != null ? "x" + count : "";
            keys[i].text = KeyLabel(i);
        }

        storyName.text = story != null && story.Item is StoryItem item ? StoryItems.Name(item) : "";

        status.text = stamina != null && stamina.Boosted
            ? $"<color=#{ColorUtility.ToHtmlStringRGB(ConsumableInfo.Colour(ConsumableType.Adrenaline))}>ADRENALINE  {stamina.BoostLeft:0.0}S</color>"
            : "";

        header.text = GameInput.UsingGamepad
            ? $"> QUICK USE  <size=80%><alpha=#88>[{GameInput.Label(GameInput.QuickPrevious)} / {GameInput.Label(GameInput.QuickNext)}] SELECT"
            : $"> QUICK USE  <size=80%><alpha=#88>[WHEEL] SELECT  [{GameInput.Label(GameInput.QuickUse)}] USE";

        SetVerticesDirty();
    }

    // The selected slot shows the use button (RMB / RB); on a keyboard the others show their own
    // key (3-6).
    private string KeyLabel(int slot)
    {
        if (slot == bar.Selected)
            return GameInput.Label(GameInput.QuickUse);

        return GameInput.UsingGamepad ? "" : GameInput.Label(GameInput.QuickSlots[slot]);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (bar == null)
            return;

        Vector2 origin = rectTransform.rect.min;

        for (int i = 0; i < QuickBar.SlotCount; i++)
        {
            Rect slot = SlotRect(i);
            slot.position += origin;
            ConsumableType? type = bar[i];
            bool selected = i == bar.Selected;

            FillRect(vh, slot, new Color(0f, 0.04f, 0.02f, 0.75f));

            if (type is ConsumableType t)
            {
                Color colour = ConsumableInfo.Colour(t);
                float alpha = bar.Count(i) > 0 ? 1f : 0.3f;
                FillRect(vh, slot, Fade(colour, 0.12f * alpha));
                FillRect(vh, new Rect(slot.xMin, slot.yMax - 3f, slot.width, 3f), Fade(colour, alpha));
                Rect picture = new Rect(slot.xMin + 6f, slot.yMin + 16f, slot.width - 12f, slot.height - 32f);
                ConsumableSilhouette.Draw(vh, t, picture, Fade(Color.Lerp(colour, Color.white, 0.35f), alpha));
            }

            // Filling up from the bottom while it's being used.
            if (bar.UsingSlot == i)
                FillRect(vh, new Rect(slot.xMin, slot.yMin, slot.width, slot.height * bar.UseProgress), Fade(phosphor, 0.3f));

            // Selected: lit, with a bright frame and a bar under it.
            if (selected)
            {
                FillRect(vh, slot, Fade(phosphor, 0.12f));
                FillRect(vh, new Rect(slot.xMin, slot.yMin - 7f, slot.width, 3f), phosphor);
            }

            OutlineRect(vh, slot, selected ? 2.5f : 1f, Fade(phosphor, selected ? 1f : 0.25f));
        }

        DrawStorySlot(vh, origin);

        if (bar.IsUsing)
            DrawUseRing(vh);
    }

    private void DrawStorySlot(VertexHelper vh, Vector2 origin)
    {
        if (story == null || !(story.Item is StoryItem item))
            return;

        Rect slot = StoryRect;
        slot.position += origin;
        Color colour = StoryItems.Colour(item);

        FillRect(vh, slot, new Color(0f, 0.04f, 0.02f, 0.75f));
        FillRect(vh, slot, Fade(colour, 0.14f));
        FillRect(vh, new Rect(slot.xMin, slot.yMax - 3f, slot.width, 3f), colour);
        StoryItemSilhouette.Draw(vh, item, new Rect(slot.xMin + 8f, slot.yMin + 16f, slot.width - 16f, slot.height - 24f), Color.Lerp(colour, Color.white, 0.35f));
        OutlineRect(vh, slot, 1.5f, Fade(colour, 0.8f));
    }

    // At the cursor, like the reload ring, in the item's colour.
    private void DrawUseRing(VertexHelper vh)
    {
        Canvas canvas = this.canvas;
        Camera uiCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, GameInput.Pointer, uiCamera, out Vector2 centre))
            return;

        Color colour = ConsumableInfo.Colour(bar.UsingType);
        Ring(vh, centre, RingRadius, 2f, Fade(phosphor, 0.25f));

        const int segments = 48;
        float fill = bar.UseProgress;
        int lit = Mathf.CeilToInt(segments * fill);

        for (int i = 0; i < lit; i++)
        {
            float a0 = 90f - 360f * fill * i / lit;
            float a1 = 90f - 360f * fill * (i + 1) / lit;
            Vector2 d0 = Direction(a0);
            Vector2 d1 = Direction(a1);
            Quad(vh, centre + d0 * RingRadius, centre + d1 * RingRadius, centre + d1 * (RingRadius - 5f), centre + d0 * (RingRadius - 5f), colour);
        }
    }
}
