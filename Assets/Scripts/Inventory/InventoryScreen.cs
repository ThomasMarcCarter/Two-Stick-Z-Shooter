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
// a quick slot onto another swaps them, and anywhere else clears it. Beside the quick bar is the
// story slot: the run's story item (a truck part), which can't be moved or dropped.
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
        Story,
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
    private GameObject runItemsPanel;
    private TMP_Text runItemsText;
    private TMP_Text capacity;
    private TMP_Text footer;
    private TMP_Text storyLabel;

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
            weapons = PlayerWeapon.Current;

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
}
