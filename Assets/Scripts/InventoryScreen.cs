using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// Inventory (TAB / LB): the two guns on the left, each with a mod bar of its five attachment
// slots underneath, and the backpack grid on the right holding spare attachments, ammo stacks
// and consumables, with the quick-use bar's four slots under it. In the radar's phosphor style. Everything can be dragged with the cursor (mouse, or
// the right stick on a controller, grabbing with Fire / A): attachments onto a gun's slot (or
// anywhere on its card) to fit them, off a gun into the backpack, between cells to rearrange,
// or out of the panel to drop them on the floor. Ammo and consumable stacks can be moved around
// the grid, merged into another stack of the same kind, or dropped on the floor too. Dragging a
// consumable onto a quick slot sets that slot to it (the stack stays in the backpack); dragging
// a quick slot onto another swaps them, and anywhere else clears it.
// A press and release in place picks an item up to carry, for placing with a second press.
// Hovering a slot or item shows what it is and does.
// The game keeps running while it's open: the player can walk but not shoot, so checking it
// mid-fight is a risk. Inventory, Back or Pause closes it. While open, the panel slides left and
// the minimap grows beside it, to show more of the floor around the player; the equipment panel
// underneath fades out of the way. Plays a backpack sound on opening: the clip set in the
// Inspector, or a generated rustle if none is set.
// The screen is built in code when the game starts, so it only needs adding to the Canvas.
// Split into partials: drag and drop in InventoryScreen.Dragging.cs, refreshing and tooltips in
// InventoryScreen.Contents.cs, mesh drawing in InventoryScreen.Drawing.cs.
public partial class InventoryScreen : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [Header("References")]
    [Tooltip("Found automatically if left empty.")]
    [SerializeField] private PlayerWeapon weapons;
    [Tooltip("Found automatically if left empty.")]
    [SerializeField] private AmmoInventory ammo;
    [Tooltip("Defaults to the TextMeshPro default font if left empty.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Sound")]
    [Tooltip("Played on opening. Leave empty to use a generated backpack rustle.")]
    [SerializeField] private AudioClip openSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;

    [Header("Minimap")]
    [Tooltip("Grown beside the inventory while it's open. Found automatically if left empty.")]
    [SerializeField] private Minimap minimap;
    [Tooltip("Largest size the minimap grows to; it's kept smaller if the screen is too narrow.")]
    [SerializeField] private Vector2 expandedMinimapSize = new Vector2(380f, 450f);
    [SerializeField] private float minimapGap = 24f;
    [SerializeField] private float expandTime = 0.18f;

    [Header("Controller Cursor")]
    [Tooltip("How fast the right stick moves the cursor, in pixels a second on a 720-pixel-tall screen.")]
    [SerializeField] private float cursorSpeed = 700f;

    [Header("Look")]
    [SerializeField] private Vector2 size = new Vector2(820f, 470f);

    private const float CardHeight = 104f;
    private const float ModBarHeight = 48f;
    private const float CellGap = 5f;
    private const float DragThreshold = 10f;

    private static readonly AttachmentSlot[] ModSlots =
    {
        AttachmentSlot.Muzzle, AttachmentSlot.Barrel, AttachmentSlot.Magazine, AttachmentSlot.Trigger, AttachmentSlot.Rail
    };

    // Where something is: a backpack cell, one of a gun's mod slots, a gun's card (any slot),
    // outside the panel (the floor), or nowhere useful.
    private enum SpotKind
    {
        None,
        Cell,
        ModSlot,
        Card,
        QuickSlot,
        Outside
    }

    private struct Spot
    {
        public SpotKind Kind;
        public int Index;       // cell, quick slot, or gun (0 main, 1 sidearm)
        public int Slot;        // mod slot index

        public bool Same(Spot other) => Kind == other.Kind && Index == other.Index && Slot == other.Slot;
    }

    private GameObject panel;
    private RectTransform panelRect;
    private RectTransform minimapRect;
    private CanvasGroup equipmentFade;
    private Vector2 minimapPosition;
    private Vector2 minimapSize;
    private float expand;
    private AudioSource audioSource;
    private int openedFrame;
    private Backpack backpack;
    private Canvas canvas;

    private readonly TMP_Text[] cardLabels = new TMP_Text[2];
    private readonly TMP_Text[] cardNames = new TMP_Text[2];
    private readonly TMP_Text[] cardDetails = new TMP_Text[2];
    private readonly TMP_Text[,] modLabels = new TMP_Text[2, 5];
    private readonly TMP_Text[] cellMain = new TMP_Text[Backpack.CellCount];
    private readonly TMP_Text[] cellSub = new TMP_Text[Backpack.CellCount];
    private readonly TMP_Text[] quickMain = new TMP_Text[QuickBar.SlotCount];
    private readonly TMP_Text[] quickSub = new TMP_Text[QuickBar.SlotCount];
    private TMP_Text hint;
    private TMP_Text meleeStats;
    private TMP_Text meleeUpgrades;
    private TMP_Text capacity;
    private TMP_Text footer;

    // Drawn over everything else: the carried item and the hover tooltip.
    private RectTransform overlay;
    private TMP_Text ghostMain;
    private TMP_Text ghostSub;
    private RectTransform tooltipRect;
    private TMP_Text tooltipText;
    private Color tooltipAccent;

    private Vector2 pointer;
    private bool pointerInside;
    private Spot hover;
    private bool holding;
    private Spot heldFrom;
    private Vector2 pressPoint;
    private bool dragged;
    private bool clickHold;
    private Vector2 padCursor;

    private void Awake()
    {
        IsOpen = false;

        if (weapons == null)
            weapons = FindAnyObjectByType<PlayerWeapon>();

        if (ammo == null)
            ammo = FindAnyObjectByType<AmmoInventory>();

        if (font == null)
            font = TMP_Settings.defaultFontAsset;

        canvas = GetComponentInParent<Canvas>();

        audioSource = GameAudio.Route(gameObject.AddComponent<AudioSource>());
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;
        audioSource.clip = openSound != null ? openSound : ProceduralAudio.BackpackRustle(1234);

        Build();
        panel.SetActive(false);

        if (minimap == null)
            minimap = FindAnyObjectByType<Minimap>();

        if (minimap != null)
        {
            minimapRect = minimap.rectTransform;
            minimapPosition = minimapRect.anchoredPosition;
            minimapSize = minimapRect.sizeDelta;
        }

        // The equipment panel (bottom left) fades out while the inventory is open: the panel
        // covers that corner, and shows the same weapons in more detail.
        EquipmentHud equipment = FindAnyObjectByType<EquipmentHud>();

        if (equipment != null)
        {
            equipmentFade = equipment.GetComponent<CanvasGroup>();

            if (equipmentFade == null)
                equipmentFade = equipment.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private Backpack Bag
    {
        get
        {
            if (backpack == null && weapons != null)
                backpack = weapons.Backpack;

            return backpack;
        }
    }

    // Eases the panel and minimap between their normal and side-by-side layouts.
    private void LateUpdate()
    {
        float target = IsOpen ? 1f : 0f;

        if (Mathf.Approximately(expand, target))
            return;

        expand = Mathf.MoveTowards(expand, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, expandTime));
        ApplyLayout(Mathf.SmoothStep(0f, 1f, expand));
    }

    // The inventory panel and an enlarged minimap side by side, centred together, tops level.
    // The minimap is top-right anchored, so its position is measured from the screen's corner.
    private void ApplyLayout(float t)
    {
        Rect screen = ((RectTransform)transform).rect;
        const float margin = 24f;

        float mapWidth = Mathf.Max(minimapSize.x,
            Mathf.Min(expandedMinimapSize.x, screen.width - size.x - minimapGap - margin * 2f));
        float mapHeight = Mathf.Max(minimapSize.y, Mathf.Min(expandedMinimapSize.y, screen.height - margin * 2f - 40f));

        float group = size.x + minimapGap + mapWidth;
        float panelX = -group / 2f + size.x / 2f;
        Vector2 mapPosition = new Vector2(group / 2f - screen.width / 2f, size.y / 2f - screen.height / 2f);

        panelRect.anchoredPosition = new Vector2(Mathf.Lerp(0f, panelX, t), 0f);

        if (minimapRect != null)
        {
            minimapRect.anchoredPosition = Vector2.Lerp(minimapPosition, mapPosition, t);
            minimapRect.sizeDelta = Vector2.Lerp(minimapSize, new Vector2(mapWidth, mapHeight), t);
        }

        if (equipmentFade != null)
            equipmentFade.alpha = 1f - t;
    }

    private void OnDisable()
    {
        if (IsOpen)
            Close();
    }

    private void Update()
    {
        if (!IsOpen)
        {
            if (GameInput.Inventory.WasPressedThisFrame() && !GameFlow.InputBlocked)
                Open();

            return;
        }

        // Ignore the press that opened it.
        if (Time.frameCount == openedFrame)
            return;

        // Back puts down whatever is being carried before it closes the screen.
        if (holding && GameInput.Cancel.WasPressedThisFrame())
        {
            CancelHold();
        }
        else if (GameInput.Inventory.WasPressedThisFrame() || GameInput.Cancel.WasPressedThisFrame()
            || GameInput.Pause.WasPressedThisFrame())
        {
            Close();
            return;
        }

        if (GameInput.UsingGamepad)
            MovePadCursor();

        UpdatePointer();
        HandleDrag();
        Refresh();
    }

    private void Open()
    {
        IsOpen = true;
        openedFrame = Time.frameCount;
        holding = false;

        Refresh();
        panel.SetActive(true);

        // The controller cursor starts in the middle of the screen.
        padCursor = new Vector2(Screen.width, Screen.height) / 2f;
        GameInput.GamepadPointerOverride = PadCursor;

        audioSource.pitch = Random.Range(0.92f, 1.08f);
        audioSource.PlayOneShot(audioSource.clip, volume);
    }

    private void Close()
    {
        if (holding)
            CancelHold();

        IsOpen = false;
        panel.SetActive(false);

        if (GameInput.GamepadPointerOverride == PadCursor)
            GameInput.GamepadPointerOverride = null;
    }

    private Vector2? PadCursor()
    {
        return padCursor;
    }

    private void MovePadCursor()
    {
        Vector2 input = GameInput.Aim.ReadValue<Vector2>();

        if (input.sqrMagnitude > 0.01f)
            padCursor += Vector2.ClampMagnitude(input, 1f) * cursorSpeed * (Screen.height / 720f) * Time.unscaledDeltaTime;

        padCursor.x = Mathf.Clamp(padCursor.x, 0f, Screen.width);
        padCursor.y = Mathf.Clamp(padCursor.y, 0f, Screen.height);
    }

    // ---------- Layout (panel space, origin at the centre) ----------

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

    // ---------- Pointer ----------

    private void UpdatePointer()
    {
        Camera uiCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, GameInput.Pointer, uiCamera, out pointer);
        pointerInside = PanelBounds.Contains(pointer);
        hover = SpotAt(pointer);
    }

    private Spot SpotAt(Vector2 point)
    {
        if (!PanelBounds.Contains(point))
            return new Spot { Kind = SpotKind.Outside };

        for (int i = 0; i < Backpack.CellCount; i++)
        {
            if (CellRect(i).Contains(point))
                return new Spot { Kind = SpotKind.Cell, Index = i };
        }

        for (int i = 0; i < QuickBar.SlotCount; i++)
        {
            if (QuickSlotRect(i).Contains(point))
                return new Spot { Kind = SpotKind.QuickSlot, Index = i };
        }

        for (int g = 0; g < 2; g++)
        {
            for (int s = 0; s < ModSlots.Length; s++)
            {
                if (ModSlotRect(g, s).Contains(point))
                    return new Spot { Kind = SpotKind.ModSlot, Index = g, Slot = s };
            }

            if (CardRect(g).Contains(point))
                return new Spot { Kind = SpotKind.Card, Index = g };
        }

        return new Spot { Kind = SpotKind.None };
    }

    private Gun GunAt(int index)
    {
        if (weapons == null)
            return null;

        return index == 0 ? weapons.Main.Gun : weapons.Sidearm.Gun;
    }

    private PlayerWeapon.Equipped EquippedAt(int index)
    {
        return index == 0 ? weapons.Main : weapons.Sidearm;
    }

    // What's at a spot: an attachment (on a gun or in the backpack) or a backpack item.
    private WeaponAttachment AttachmentAt(Spot spot)
    {
        if (spot.Kind == SpotKind.ModSlot)
        {
            Gun gun = GunAt(spot.Index);
            return gun != null ? gun.AttachmentIn(ModSlots[spot.Slot]) : null;
        }

        if (spot.Kind == SpotKind.Cell && Bag != null)
        {
            Backpack.Item item = Bag[spot.Index];
            return item != null && item.Kind == Backpack.ItemKind.Attachment ? item.Attachment : null;
        }

        return null;
    }

    private Backpack.Item ItemAt(Spot spot)
    {
        return spot.Kind == SpotKind.Cell && Bag != null ? Bag[spot.Index] : null;
    }

    private bool HasItem(Spot spot)
    {
        if (spot.Kind == SpotKind.QuickSlot)
            return Quick != null && Quick[spot.Index] != null;

        return AttachmentAt(spot) != null || ItemAt(spot) != null;
    }

    // A quick slot as an item, for drawing it like a stack: its kind and how many are carried.
    private Backpack.Item QuickItem(int slot)
    {
        if (Quick == null || !(Quick[slot] is ConsumableType type))
            return null;

        return new Backpack.Item { Kind = Backpack.ItemKind.Consumable, Consumable = type, Amount = Quick.Count(slot) };
    }
}
