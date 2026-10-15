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
public class InventoryScreen : MonoBehaviour
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
    [SerializeField] private Color phosphor = new Color(0.35f, 1f, 0.55f, 1f);
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

        audioSource = gameObject.AddComponent<AudioSource>();
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

        Color dim = Fade(phosphor, 0.55f);

        MakeText(panel.transform, "> INVENTORY", 26, new Rect(Left, Top - 34f, 400f, 34f), phosphor, TextAlignmentOptions.Left, 10f);
        hint = MakeText(panel.transform, "", 12, new Rect(Right - 360f, Top - 30f, 360f, 20f), dim, TextAlignmentOptions.Right);
        MakeText(panel.transform, "WEAPONS", 13, new Rect(Left, Top - 60f, 300f, 20f), dim, TextAlignmentOptions.Left);
        MakeText(panel.transform, "BACKPACK", 13, new Rect(Middle + 14f, Top - 60f, 200f, 20f), dim, TextAlignmentOptions.Left);
        capacity = MakeText(panel.transform, "", 11, new Rect(Right - 200f, Top - 60f, 200f, 20f), dim, TextAlignmentOptions.Right);

        Rect lastCell = CellRect(Backpack.CellCount - 1);
        MakeText(panel.transform, "QUICK USE", 11, new Rect(Middle + 14f, lastCell.yMin - 24f, 200f, 18f), dim, TextAlignmentOptions.Left);

        for (int i = 0; i < QuickBar.SlotCount; i++)
        {
            Rect slot = QuickSlotRect(i);
            quickMain[i] = MakeText(panel.transform, "", 14, new Rect(slot.xMin + 2f, slot.yMin + 17f, slot.width - 4f, 20f), phosphor, TextAlignmentOptions.Center, 2f);
            quickSub[i] = MakeText(panel.transform, "", 8, new Rect(slot.xMin + 2f, slot.yMin + 6f, slot.width - 4f, 12f), dim, TextAlignmentOptions.Center, 1f);
        }

        Rect lastQuick = QuickSlotRect(QuickBar.SlotCount - 1);
        footer = MakeText(panel.transform, "", 10, new Rect(Middle + 14f, lastQuick.yMin - 40f, Right - Middle - 14f, 34f), dim, TextAlignmentOptions.TopLeft, 3f);
        footer.textWrappingMode = TextWrappingModes.Normal;

        for (int i = 0; i < 2; i++)
        {
            Rect card = CardRect(i);
            cardLabels[i] = MakeText(panel.transform, "", 11, new Rect(card.xMin + 10f, card.yMax - 22f, card.width - 20f, 16f), dim, TextAlignmentOptions.Left);
            cardNames[i] = MakeText(panel.transform, "", 18, new Rect(card.xMin + 170f, card.yMax - 52f, card.width - 180f, 24f), phosphor, TextAlignmentOptions.Left);
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
        meleeStats = MakeText(panel.transform, "", 11, new Rect(Left, lastBar.yMin - 22f, Middle - 14f - Left, 18f), phosphor, TextAlignmentOptions.Left, 3f);
        meleeStats.richText = true;
        meleeUpgrades = MakeText(panel.transform, "", 10, new Rect(Left, lastBar.yMin - 38f, Middle - 14f - Left, 16f), dim, TextAlignmentOptions.Left, 3f);
        meleeUpgrades.richText = true;

        for (int i = 0; i < Backpack.CellCount; i++)
        {
            Rect cell = CellRect(i);
            cellMain[i] = MakeText(panel.transform, "", 15, new Rect(cell.xMin + 2f, cell.yMin + 22f, cell.width - 4f, 22f), phosphor, TextAlignmentOptions.Center, 2f);
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
        ghostMain = MakeText(overlay, "", 15, new Rect(0f, 0f, cell * 0.9f, 22f), phosphor, TextAlignmentOptions.Center, 2f);
        ghostSub = MakeLabel(overlay, new Rect(0f, 0f, cell * 0.9f, 12f), Fade(phosphor, 0.7f));

        tooltipText = MakeText(overlay, "", 14, new Rect(0f, 0f, 260f, 80f), phosphor, TextAlignmentOptions.TopLeft, 3f);
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

    // ---------- Dragging ----------

    private static bool GrabPressed => GameInput.Fire.WasPressedThisFrame() || GameInput.Submit.WasPressedThisFrame();
    private static bool GrabReleased => GameInput.Fire.WasReleasedThisFrame() || GameInput.Submit.WasReleasedThisFrame();

    // Press on an item to pick it up. Release somewhere else to put it there; release where it
    // was picked up to carry it, and press again to put it down.
    private void HandleDrag()
    {
        if (Bag == null)
            return;

        if (GrabPressed)
        {
            if (holding)
            {
                Place(hover);
                return;
            }

            if (!HasItem(hover))
                return;

            holding = true;
            heldFrom = hover;
            pressPoint = pointer;
            dragged = false;
            clickHold = false;
            SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("inventory-lift", 2, i => ProceduralAudio.BackpackRustle(300 + i))), 0.2f, 1.3f);
            return;
        }

        if (!holding || clickHold)
            return;

        if ((pointer - pressPoint).magnitude > DragThreshold)
            dragged = true;

        if (GrabReleased)
        {
            // A click without moving: keep carrying it until the next press.
            if (!dragged && hover.Same(heldFrom))
                clickHold = true;
            else
                Place(hover);
        }
    }

    private void CancelHold()
    {
        holding = false;
        clickHold = false;
    }

    private void Place(Spot target)
    {
        Spot from = heldFrom;
        CancelHold();

        if (target.Same(from))
            return;

        WeaponAttachment attachment = AttachmentAt(from);

        if (attachment != null)
        {
            PlaceAttachment(attachment, from, target);
            return;
        }

        // A quick slot: onto another to swap them, anywhere else to clear it.
        if (from.Kind == SpotKind.QuickSlot)
        {
            if (target.Kind == SpotKind.QuickSlot)
                Quick.Swap(from.Index, target.Index);
            else
                Quick.Clear(from.Index);

            PlayMove();
            return;
        }

        // An ammo or consumable stack: moved within the backpack, put on the quick bar, or
        // dropped on the floor.
        if (from.Kind != SpotKind.Cell)
            return;

        Backpack.Item stack = Bag[from.Index];

        if (stack == null)
            return;

        if (target.Kind == SpotKind.Cell)
        {
            Bag.Move(from.Index, target.Index);
            PlayMove();
        }
        else if (target.Kind == SpotKind.QuickSlot)
        {
            if (stack.Kind != Backpack.ItemKind.Consumable)
            {
                PlayDeny();
                return;
            }

            Quick.Assign(target.Index, stack.Consumable);
            PlayFit();
        }
        else if (target.Kind == SpotKind.Outside)
        {
            int amount = Bag.TakeStack(from.Index);

            if (amount <= 0)
                return;

            Vector3 position = FloorPickups.DropPoint(weapons.transform);

            if (stack.Kind == Backpack.ItemKind.Ammo)
                AmmoPickup.Drop(stack.AmmoType, amount, position, RoomAt(position));
            else
                ConsumablePickup.Drop(stack.Consumable, amount, position, RoomAt(position));

            PlayMove();
        }
    }

    private void PlaceAttachment(WeaponAttachment attachment, Spot from, Spot target)
    {
        switch (target.Kind)
        {
            case SpotKind.ModSlot:
            case SpotKind.Card:
                FitToGun(attachment, from, target);
                return;

            case SpotKind.Cell:
                ToCell(attachment, from, target.Index);
                return;

            case SpotKind.Outside:
                DropOnFloor(attachment, from);
                return;
        }
    }

    // Onto a gun: dropped on the matching slot or anywhere on the card. Whatever was in that
    // slot goes back where this one came from.
    private void FitToGun(WeaponAttachment attachment, Spot from, Spot target)
    {
        Gun gun = GunAt(target.Index);

        if (gun == null || !attachment.Fits(gun) || (target.Kind == SpotKind.ModSlot && ModSlots[target.Slot] != attachment.slot))
        {
            PlayDeny();
            return;
        }

        // Already on this gun.
        if (from.Kind == SpotKind.ModSlot && from.Index == target.Index)
            return;

        RemoveFrom(attachment, from);
        WeaponAttachment replaced = gun.Attach(attachment);

        if (replaced != null)
            Return(replaced, from);

        PlayFit();
    }

    // Into a backpack cell. Onto another attachment: they swap, if the other one can go where
    // this one came from.
    private void ToCell(WeaponAttachment attachment, Spot from, int cell)
    {
        Backpack.Item there = Bag[cell];

        if (from.Kind == SpotKind.Cell)
        {
            Bag.Move(from.Index, cell);
            PlayMove();
            return;
        }

        // From a gun.
        Gun gun = GunAt(from.Index);

        if (there == null)
        {
            if (gun == null || !Bag.PlaceAttachment(attachment, cell))
            {
                PlayDeny();
                return;
            }

            gun.Detach(attachment);
            PlayMove();
            return;
        }

        if (there.Kind == Backpack.ItemKind.Attachment && there.Attachment.slot == attachment.slot && there.Attachment.Fits(gun))
        {
            WeaponAttachment other = Bag.TakeAttachment(cell);
            gun.Attach(other);
            Bag.PlaceAttachment(attachment, cell);
            PlayFit();
            return;
        }

        PlayDeny();
    }

    private void DropOnFloor(WeaponAttachment attachment, Spot from)
    {
        RemoveFrom(attachment, from);

        Vector3 position = FloorPickups.DropPoint(weapons.transform);
        AttachmentPickup.Spawn(attachment, position, RoomAt(position), AttachmentPickup.DefaultMaterial);
        PlayMove();
    }

    private static Transform RoomAt(Vector3 position)
    {
        return FloorPickups.RoomAt(position);
    }

    private void RemoveFrom(WeaponAttachment attachment, Spot from)
    {
        if (from.Kind == SpotKind.Cell)
            Bag.TakeAttachment(from.Index);
        else if (from.Kind == SpotKind.ModSlot && GunAt(from.Index) != null)
            GunAt(from.Index).Detach(attachment);
    }

    // A swapped-off attachment goes back to where the new one came from: the same backpack
    // cell, or the other gun if it fits there; otherwise any free cell, or the floor.
    private void Return(WeaponAttachment attachment, Spot from)
    {
        if (from.Kind == SpotKind.Cell && Bag.PlaceAttachment(attachment, from.Index))
            return;

        if (from.Kind == SpotKind.ModSlot)
        {
            Gun gun = GunAt(from.Index);

            if (gun != null && attachment.Fits(gun) && gun.AttachmentIn(attachment.slot) == null)
            {
                gun.Attach(attachment);
                return;
            }
        }

        if (Bag.AddAttachment(attachment))
            return;

        DropOnFloor(attachment, new Spot { Kind = SpotKind.None });
    }

    private static void PlayFit()
    {
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-fit", 3, ProceduralAudio.AttachmentFit)), 0.5f, Random.Range(0.95f, 1.05f));
    }

    private static void PlayMove()
    {
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("inventory-lift", 2, i => ProceduralAudio.BackpackRustle(300 + i))), 0.25f, 1.1f);
    }

    private static void PlayDeny()
    {
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-deny", 1, i => ProceduralAudio.AttachmentDeny())), 0.35f);
    }

    // ---------- Contents ----------

    private void Refresh()
    {
        if (weapons == null || ammo == null)
            return;

        hint.text = InputHint.Format("[{Inventory}] CLOSE");

        PlayerWeapon.Equipped[] slots = { weapons.Main, weapons.Sidearm };
        string[] slotNames = { "MAIN", "SIDEARM" };
        Gun.Slot[] slotIds = { Gun.Slot.Main, Gun.Slot.Sidearm };

        for (int i = 0; i < 2; i++)
        {
            Gun gun = slots[i].Gun;
            bool inHand = weapons.ActiveSlot == slotIds[i] && gun != null;

            cardLabels[i].text = slotNames[i] + (inHand ? "  // IN HAND" : "");
            cardNames[i].text = gun != null ? slots[i].Name : "-- EMPTY --";
            cardDetails[i].text = gun != null
                ? $"MAG  {gun.GetCurrentAmmo()} / {gun.GetMagazineSize()}\nAMMO {AmmoInfo.Name(gun.AmmoType)}  ({gun.Reserve} SPARE)"
                : "";

            for (int s = 0; s < ModSlots.Length; s++)
            {
                WeaponAttachment fitted = gun != null ? gun.AttachmentIn(ModSlots[s]) : null;
                bool lifted = holding && heldFrom.Kind == SpotKind.ModSlot && heldFrom.Index == i && heldFrom.Slot == s;
                modLabels[i, s].text = fitted != null && !lifted ? AttachmentLabel(fitted) : WeaponAttachment.SlotName(ModSlots[s]);
                modLabels[i, s].alpha = gun != null ? 1f : 0.35f;
            }
        }

        for (int i = 0; i < Backpack.CellCount; i++)
        {
            Backpack.Item item = Bag != null ? Bag[i] : null;
            bool lifted = holding && heldFrom.Kind == SpotKind.Cell && heldFrom.Index == i;
            SetItemText(cellMain[i], cellSub[i], lifted ? null : item);
        }

        for (int i = 0; i < QuickBar.SlotCount; i++)
        {
            bool lifted = holding && heldFrom.Kind == SpotKind.QuickSlot && heldFrom.Index == i;
            Backpack.Item item = lifted ? null : QuickItem(i);
            SetItemText(quickMain[i], quickSub[i], item);

            if (item == null && Quick != null)
                quickSub[i].text = GameInput.UsingGamepad ? "" : GameInput.Label(GameInput.QuickSlots[i]);
        }

        RefreshMelee();

        if (Bag != null)
            capacity.text = $"{Bag.UsedCells} / {Backpack.CellCount} SLOTS";

        footer.text = "DRAG MODS ONTO A GUN, CONSUMABLES ONTO A QUICK SLOT\nDRAG ANYTHING OUT OF THE PANEL TO DROP IT";

        RefreshOverlay();
    }

    private void RefreshMelee()
    {
        PlayerMelee melee = weapons.Melee;

        if (melee == null)
            return;

        meleeStats.text = $"MELEE [{GameInput.Label(GameInput.Melee)}]  <alpha=#99><size=90%>"
            + $"DMG {melee.Damage} // HITS {melee.MaxTargets} // REACH {melee.Reach:0.0#}M // {melee.Cooldown:0.0#}S";

        string upgrades = "";

        foreach (MeleeUpgrade upgrade in MeleeUpgrades.All)
        {
            int level = melee.Level(upgrade);

            if (level <= 0)
                continue;

            string colour = ColorUtility.ToHtmlStringRGB(MeleeUpgrades.Colour(upgrade));
            upgrades += $"<color=#{colour}>{MeleeUpgrades.Name(upgrade)} {MeleeUpgrades.Level(level)}</color>  ";
        }

        meleeUpgrades.text = upgrades.Length > 0 ? upgrades : "NO UPGRADES YET // FIND THEM ON THE FLOOR";
    }

    private void SetItemText(TMP_Text main, TMP_Text sub, Backpack.Item item)
    {
        if (item == null)
        {
            main.text = "";
            sub.text = "";
            return;
        }

        if (item.Kind == Backpack.ItemKind.Attachment)
        {
            main.text = "";   // drawn as a picture instead (DrawItem)
            sub.text = AttachmentLabel(item.Attachment);
        }
        else if (item.Kind == Backpack.ItemKind.Consumable)
        {
            main.text = ConsumableInfo.ShortName(item.Consumable);
            main.color = Fade(Color.Lerp(ConsumableInfo.Colour(item.Consumable), Color.white, 0.35f), item.Amount > 0 ? 1f : 0.4f);
            sub.text = "x" + item.Amount;
        }
        else
        {
            main.text = item.Amount.ToString();
            main.color = Color.Lerp(AmmoInfo.Colour(item.AmmoType), Color.white, 0.3f);
            sub.text = AmmoInfo.Name(item.AmmoType);
        }
    }

    private static string AttachmentLabel(WeaponAttachment attachment)
    {
        return attachment.name.ToUpperInvariant();
    }

    // The colour an attachment's picture is drawn in: its own colour, lifted towards white.
    private static Color AttachmentInk(WeaponAttachment attachment, float alpha)
    {
        return Fade(Color.Lerp(attachment.colour, Color.white, 0.4f), alpha);
    }

    // The carried item under the cursor, or the tooltip for whatever is under it.
    private void RefreshOverlay()
    {
        Vector2 ghostOrigin = GhostRect.position;
        Backpack.Item held = holding ? HeldAsItem() : null;

        SetItemText(ghostMain, ghostSub, held);
        ghostMain.rectTransform.anchoredPosition = ghostOrigin + new Vector2(0f, 22f);
        ghostSub.rectTransform.anchoredPosition = ghostOrigin + new Vector2(0f, 9f);

        string tip = holding ? null : TooltipFor(hover);
        tooltipText.gameObject.SetActive(tip != null);

        if (tip == null)
            return;

        tooltipText.text = tip;
        Vector2 textSize = tooltipText.GetPreferredValues(tip, 320f, 0f);
        textSize.x = Mathf.Min(textSize.x, 320f);
        tooltipRect.sizeDelta = textSize;

        // Beside the cursor, kept inside the panel's sides.
        Vector2 position = pointer + new Vector2(18f, -18f - textSize.y);

        if (position.x + textSize.x + 12f > size.x / 2f)
            position.x = pointer.x - 18f - textSize.x;

        position.y = Mathf.Max(position.y, -size.y / 2f + 10f);
        tooltipRect.anchoredPosition = position;
    }

    // Where the carried item is drawn: hanging off the cursor's lower right, so neither covers
    // the other.
    private Rect GhostRect
    {
        get
        {
            float cell = CellSize * 0.9f;
            return new Rect(pointer.x + 12f, pointer.y - 12f - cell, cell, cell);
        }
    }

    // The carried thing as a backpack item, for drawing it the same way.
    private Backpack.Item HeldAsItem()
    {
        WeaponAttachment attachment = AttachmentAt(heldFrom);

        if (attachment != null)
            return new Backpack.Item { Kind = Backpack.ItemKind.Attachment, Attachment = attachment };

        if (heldFrom.Kind == SpotKind.QuickSlot)
            return QuickItem(heldFrom.Index);

        return ItemAt(heldFrom);
    }

    private string TooltipFor(Spot spot)
    {
        if (spot.Kind == SpotKind.QuickSlot)
        {
            Backpack.Item quick = QuickItem(spot.Index);
            string key = GameInput.UsingGamepad ? GameInput.Label(GameInput.QuickUse) + " (WHEN SELECTED)" : GameInput.Label(GameInput.QuickSlots[spot.Index]);

            if (quick == null)
            {
                tooltipAccent = Fade(phosphor, 0.5f);
                return $"QUICK SLOT {spot.Index + 1}\n<alpha=#88><size=85%>EMPTY // DRAG A CONSUMABLE HERE</size>";
            }

            return ConsumableTooltip(quick.Consumable, $"QUICK SLOT {spot.Index + 1} // USE WITH [{key}]");
        }

        if (spot.Kind == SpotKind.ModSlot)
        {
            Gun gun = GunAt(spot.Index);

            if (gun == null)
                return null;

            WeaponAttachment fitted = gun.AttachmentIn(ModSlots[spot.Slot]);

            if (fitted == null)
            {
                tooltipAccent = Fade(phosphor, 0.5f);
                return $"{WeaponAttachment.SlotName(ModSlots[spot.Slot])} SLOT\n<alpha=#88><size=85%>EMPTY // DRAG A MOD HERE TO FIT IT</size>";
            }

            return AttachmentTooltip(fitted, "ON " + EquippedAt(spot.Index).Name);
        }

        Backpack.Item item = ItemAt(spot);

        if (item == null)
            return null;

        if (item.Kind == Backpack.ItemKind.Attachment)
        {
            PlayerWeapon.Equipped target = weapons.TargetFor(item.Attachment);
            return AttachmentTooltip(item.Attachment, target != null ? "FITS " + target.Name : "FITS NO GUN YOU CARRY");
        }

        if (item.Kind == Backpack.ItemKind.Consumable)
        {
            int slot = -1;

            for (int i = 0; i < QuickBar.SlotCount && Quick != null; i++)
            {
                if (Quick[i] == item.Consumable)
                    slot = i;
            }

            string where = $"{item.Amount} / {ConsumableInfo.StackSize(item.Consumable)} IN STACK // "
                + (slot >= 0 ? $"ON QUICK SLOT {slot + 1}" : "NOT ON THE QUICK BAR");
            return ConsumableTooltip(item.Consumable, where);
        }

        tooltipAccent = AmmoInfo.Colour(item.AmmoType);
        string users = null;

        foreach (PlayerWeapon.Equipped equipped in new[] { weapons.Main, weapons.Sidearm })
        {
            if (equipped.Gun != null && equipped.Gun.AmmoType == item.AmmoType)
                users = users == null ? equipped.Name : users + ", " + equipped.Name;
        }

        return $"{AmmoInfo.Name(item.AmmoType)}  <alpha=#99><size=85%>// {item.Amount} / {Backpack.StackSize(item.AmmoType)} IN STACK</size>\n"
            + $"<alpha=#CC><size=85%>CARRYING {ammo.Count(item.AmmoType)} IN ALL</size>\n"
            + $"<alpha=#88><size=85%>{(users != null ? "IN USE // " + users : "NOT USED BY YOUR GUNS")}</size>";
    }

    private string ConsumableTooltip(ConsumableType type, string where)
    {
        tooltipAccent = ConsumableInfo.Colour(type);
        return $"{ConsumableInfo.Name(type)}  <alpha=#99><size=85%>// CARRYING {Bag.ConsumableCount(type)}</size>\n"
            + $"<alpha=#FF><size=85%>{ConsumableInfo.Describe(type)}</size>\n"
            + $"<alpha=#88><size=80%>{where}</size>";
    }

    private string AttachmentTooltip(WeaponAttachment attachment, string where)
    {
        tooltipAccent = attachment.colour;
        string effect = attachment.Describe();
        return $"{attachment.name.ToUpperInvariant()}  <alpha=#99><size=85%>// {WeaponAttachment.SlotName(attachment.slot)}</size>\n"
            + $"<alpha=#FF><size=85%>{(effect.Length > 0 ? effect : "NO EFFECT")}</size>\n"
            + $"<alpha=#88><size=80%>{attachment.FitDescription()} // {where}</size>";
    }

    // ---------- Drawing ----------

    private void DrawPanel(VertexHelper vh, Rect bounds)
    {
        FillRect(vh, bounds, new Color(0f, 0.04f, 0.02f, 0.97f));
        OutlineRect(vh, bounds, 1.5f, Fade(phosphor, 0.6f));

        // Divider between the columns.
        FillRect(vh, new Rect(Middle - 0.5f, bounds.yMin + 24f, 1f, bounds.height - 100f), Fade(phosphor, 0.2f));

        if (weapons == null || ammo == null)
            return;

        WeaponAttachment carried = holding ? AttachmentAt(heldFrom) : null;
        PlayerWeapon.Equipped[] slots = { weapons.Main, weapons.Sidearm };
        Gun.Slot[] slotIds = { Gun.Slot.Main, Gun.Slot.Sidearm };

        for (int i = 0; i < 2; i++)
        {
            Rect card = CardRect(i);
            Gun gun = slots[i].Gun;
            bool inHand = weapons.ActiveSlot == slotIds[i] && gun != null;
            bool canTake = carried != null && carried.Fits(gun);

            FillRect(vh, card, Fade(phosphor, inHand ? 0.1f : 0.04f));
            OutlineRect(vh, card, inHand || canTake ? 2f : 1f, Fade(phosphor, inHand || canTake ? 0.9f : 0.4f));

            if (gun != null)
            {
                Rect icon = new Rect(card.xMin + 12f, card.yMin + 12f, 148f, card.height - 40f);
                WeaponSilhouette.Draw(vh, gun.HudIcon, icon, Fade(phosphor, inHand ? 1f : 0.6f));
            }

            for (int s = 0; s < ModSlots.Length; s++)
                DrawModSlot(vh, i, s, gun, carried);
        }

        for (int i = 0; i < Backpack.CellCount; i++)
        {
            Rect cell = CellRect(i);
            Backpack.Item item = Bag != null ? Bag[i] : null;
            bool lifted = holding && heldFrom.Kind == SpotKind.Cell && heldFrom.Index == i;
            bool hovered = hover.Kind == SpotKind.Cell && hover.Index == i;

            FillRect(vh, cell, Fade(phosphor, 0.03f));
            OutlineRect(vh, cell, hovered ? 2f : 1f, Fade(phosphor, hovered ? 0.9f : 0.25f));

            if (item != null && !lifted)
                DrawItem(vh, cell, item, 1f);
        }

        // The quick bar: lit up as a target while a consumable is carried.
        Backpack.Item carriedItem = holding ? HeldAsItem() : null;
        bool consumableCarried = carriedItem != null && carriedItem.Kind == Backpack.ItemKind.Consumable;

        for (int i = 0; i < QuickBar.SlotCount; i++)
        {
            Rect slot = QuickSlotRect(i);
            Backpack.Item item = QuickItem(i);
            bool lifted = holding && heldFrom.Kind == SpotKind.QuickSlot && heldFrom.Index == i;
            bool hovered = hover.Kind == SpotKind.QuickSlot && hover.Index == i;
            bool selected = Quick != null && Quick.Selected == i && GameInput.UsingGamepad;

            FillRect(vh, slot, Fade(phosphor, consumableCarried ? 0.1f : 0.03f));

            if (item != null && !lifted)
                DrawItem(vh, slot, item, item.Amount > 0 ? 1f : 0.4f);

            bool bright = hovered || consumableCarried || selected;
            OutlineRect(vh, slot, bright ? 2f : 1f, Fade(phosphor, bright ? 0.9f : 0.3f));
        }
    }

    private void DrawModSlot(VertexHelper vh, int gunIndex, int slotIndex, Gun gun, WeaponAttachment carried)
    {
        Rect rect = ModSlotRect(gunIndex, slotIndex);
        AttachmentSlot slot = ModSlots[slotIndex];
        WeaponAttachment fitted = gun != null ? gun.AttachmentIn(slot) : null;
        bool lifted = holding && heldFrom.Kind == SpotKind.ModSlot && heldFrom.Index == gunIndex && heldFrom.Slot == slotIndex;
        bool hovered = hover.Kind == SpotKind.ModSlot && hover.Index == gunIndex && hover.Slot == slotIndex;
        bool target = carried != null && carried.slot == slot && carried.Fits(gun);

        if (gun == null)
        {
            DashedOutlineRect(vh, rect, 1f, 4f, Fade(phosphor, 0.15f));
            return;
        }

        FillRect(vh, rect, Fade(phosphor, target ? 0.12f : 0.03f));

        if (fitted != null && !lifted)
        {
            FillRect(vh, rect, Fade(fitted.colour, 0.16f));
            FillRect(vh, new Rect(rect.xMin, rect.yMax - 3f, rect.width, 3f), fitted.colour);
            AttachmentSilhouette.Draw(vh, fitted, new Rect(rect.xMin + 5f, rect.yMin + 15f, rect.width - 10f, rect.height - 21f), AttachmentInk(fitted, 1f));
        }

        float thickness = hovered || target ? 2f : 1f;
        OutlineRect(vh, rect, thickness, Fade(phosphor, hovered || target ? 0.95f : 0.3f));
    }

    // An item in a cell (or the one being carried): attachments as a wireframe picture under a
    // bar in their colour, ammo with a swatch and a fill bar for how full the stack is.
    private void DrawItem(VertexHelper vh, Rect cell, Backpack.Item item, float alpha)
    {
        if (item.Kind == Backpack.ItemKind.Attachment)
        {
            Color colour = item.Attachment.colour;
            FillRect(vh, cell, Fade(colour, 0.15f * alpha));
            FillRect(vh, new Rect(cell.xMin, cell.yMax - 4f, cell.width, 4f), Fade(colour, alpha));
            Rect picture = new Rect(cell.xMin + 5f, cell.yMin + 22f, cell.width - 10f, cell.height - 30f);
            AttachmentSilhouette.Draw(vh, item.Attachment, picture, AttachmentInk(item.Attachment, alpha));
            return;
        }

        if (item.Kind == Backpack.ItemKind.Consumable)
        {
            Color colour = ConsumableInfo.Colour(item.Consumable);
            FillRect(vh, cell, Fade(colour, 0.12f * alpha));
            FillRect(vh, new Rect(cell.xMin, cell.yMax - 4f, cell.width, 4f), Fade(colour, alpha));

            // A pip per item in the stack, filled for the ones there.
            int size = ConsumableInfo.StackSize(item.Consumable);
            float pip = Mathf.Min(6f, (cell.width - 12f - (size - 1) * 2f) / size);
            float start = cell.center.x - (size * pip + (size - 1) * 2f) / 2f;

            for (int i = 0; i < size; i++)
            {
                Rect dot = new Rect(start + i * (pip + 2f), cell.yMin + 3f, pip, 3f);
                FillRect(vh, dot, i < item.Amount ? Fade(colour, 0.9f * alpha) : Fade(phosphor, 0.15f * alpha));
            }

            return;
        }

        Color ammoColour = AmmoInfo.Colour(item.AmmoType);
        FillRect(vh, new Rect(cell.xMin + 6f, cell.yMax - 12f, 8f, 8f), Fade(ammoColour, alpha));

        Rect bar = new Rect(cell.xMin + 6f, cell.yMin + 4f, cell.width - 12f, 4f);
        float fill = item.Amount / (float)Backpack.StackSize(item.AmmoType);
        FillRect(vh, bar, Fade(phosphor, 0.15f * alpha));
        FillRect(vh, new Rect(bar.xMin, bar.yMin, bar.width * fill, bar.height), Fade(ammoColour, 0.9f * alpha));
    }

    private void DrawOverlay(VertexHelper vh, Rect bounds)
    {
        if (holding)
        {
            Backpack.Item held = HeldAsItem();

            if (held != null)
            {
                Rect ghost = GhostRect;
                FillRect(vh, ghost, new Color(0f, 0.06f, 0.03f, 0.92f));
                DrawItem(vh, ghost, held, 1f);

                // Outside the panel, whatever is carried will be dropped on the floor.
                bool dropping = !pointerInside;
                OutlineRect(vh, ghost, 2f, dropping ? new Color(1f, 0.4f, 0.3f, 1f) : phosphor);
            }

            return;
        }

        if (!tooltipText.gameObject.activeSelf)
            return;

        Vector2 position = tooltipRect.anchoredPosition;
        Vector2 textSize = tooltipRect.sizeDelta;
        Rect box = new Rect(position.x - 10f, position.y - 8f, textSize.x + 22f, textSize.y + 16f);
        FillRect(vh, box, new Color(0f, 0.05f, 0.03f, 0.96f));
        OutlineRect(vh, box, 1.5f, Fade(phosphor, 0.7f));
        FillRect(vh, new Rect(box.xMin + 3f, box.yMin + 5f, 3f, box.height - 10f), tooltipAccent);
    }
}
