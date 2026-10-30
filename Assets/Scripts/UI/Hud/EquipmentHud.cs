using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static MapMesh;

// Bottom-left equipment panel in the radar's phosphor style. The main gun is always on top and the
// sidearm underneath; switching guns swaps which one is big and lit rather than their order. The
// gun in hand gets the big panel: its silhouette, name, ammo count, spare rounds of its ammo type,
// a pip per round and a reload bar, with a bright solid frame over a phosphor-tinted fill, a
// selection bar down its left side and its slot name in bold (">> MAIN [1]"); it flashes for a
// moment when the player switches. The other one shrinks to a small strip (icon, name, ammo on
// one line), unlit and faded right down. Picking up ammo flashes a short
// "+12 9MM" notice above the panel. Each slot shows the button that switches to it, following the
// device in use and any rebinds. Frames, icons and pips are one mesh; the text is created at
// runtime, so the component only needs adding to the Canvas. Also builds the quick-use bar
// beside it (QuickBarHud).
public partial class EquipmentHud : MaskableGraphic
{
    [Header("References")]
    [Tooltip("Found automatically if left empty.")]
    [SerializeField] private PlayerWeapon weapons;
    [Tooltip("Defaults to the TextMeshPro default font if left empty.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Layout")]
    [SerializeField] private Vector2 margin = new Vector2(40f, 36f);
    [SerializeField] private float width = 300f;
    [Tooltip("Height of each gun's panel.")]
    [FormerlySerializedAs("activeHeight")]
    [SerializeField] private float slotHeight = 92f;
    [Tooltip("Height of the panel of the gun not in hand.")]
    [SerializeField] private float stowedHeight = 36f;
    [SerializeField] private float gap = 6f;

    [Header("Look")]
    [Tooltip("At or below this fraction of a magazine the ammo turns red.")]
    [SerializeField, Range(0f, 1f)] private float lowAmmo = 0.25f;
    [Tooltip("How long the \"+12 9MM\" pickup notice stays up, in seconds.")]
    [SerializeField] private float pickupNoticeTime = 2.5f;

    // The text on one gun's panel.
    private class PanelText
    {
        public TMP_Text Slot;
        public TMP_Text Name;
        public TMP_Text Ammo;
        public TMP_Text Status;
    }

    private TMP_Text header;
    private readonly PanelText mainText = new PanelText();
    private readonly PanelText sidearmText = new PanelText();
    private TMP_Text pickupNotice;

    // How bright the panel of the gun not in hand is drawn.
    private const float Unlit = 0.3f;

    private AmmoInventory ammo;
    private float pickupNoticeUntil;
    private Gun.Slot shownSlot;
    private float switchedAt = -10f;
    private const float SwitchFlashTime = 0.35f;

    private string lastTexts;
    private bool built;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    // Done on the first update in Play mode rather than in Awake: graphics also run in edit mode,
    // and with scene reload off for entering Play mode, an object that already woke up in the
    // editor doesn't get Awake again.
    private void Build()
    {
        built = true;

        if (weapons == null)
            weapons = PlayerWeapon.Current;

        if (font == null)
            font = TMP_Settings.defaultFontAsset;

        if (weapons != null)
            ammo = weapons.GetComponent<AmmoInventory>();

        if (ammo != null)
            ammo.PickedUp += ShowPickup;

        // Bottom-left corner, sized for both panels.
        rectTransform.anchorMin = rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = Vector2.zero;
        rectTransform.anchoredPosition = margin;
        rectTransform.sizeDelta = new Vector2(width, slotHeight + gap + stowedHeight);

        BuildText();

        // The quick-use bar sits just to the right, along the bottom.
        // Drawn straight after this panel, so the menus and screens that cover the HUD (main
        // menu, transitions, the full map) cover it too, rather than it landing on top of them.
        if (weapons != null)
        {
            QuickBarHud bar = QuickBarHud.Create(transform.parent, weapons, font, UiColours.Phosphor, margin + new Vector2(width + 20f, 0f));
            bar.transform.SetSiblingIndex(transform.GetSiblingIndex() + 1);
            RunItemsHud runItems = RunItemsHud.Create(transform.parent, weapons, margin + new Vector2(width + 20f, 96f));
            runItems.transform.SetSiblingIndex(transform.GetSiblingIndex() + 2);
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (!built)
            Build();

        if (weapons == null)
            return;

        if (weapons.ActiveSlot != shownSlot)
        {
            shownSlot = weapons.ActiveSlot;
            switchedAt = Time.unscaledTime;
            Layout();
            lastTexts = null;
        }

        UpdateText();
        SetVerticesDirty();   // pips, reload bar, blink and the switch flash change every frame

        // Fade the pickup notice out over its last half second.
        float left = pickupNoticeUntil - Time.unscaledTime;
        Color noticeColour = pickupNotice.color;
        noticeColour.a = Mathf.Clamp01(left * 2f);
        pickupNotice.color = noticeColour;
    }

    // ---------- Layout ----------

    private bool InHand(Gun.Slot slot) => weapons != null && weapons.ActiveSlot == slot;
    private float PanelHeight(Gun.Slot slot) => InHand(slot) ? slotHeight : stowedHeight;
    private float PanelWidth(Gun.Slot slot) => InHand(slot) ? width : width * 0.8f;

    private Rect PanelRect(Gun.Slot slot) => slot == Gun.Slot.Main
        ? new Rect(0f, PanelHeight(Gun.Slot.Sidearm) + gap, PanelWidth(Gun.Slot.Main), PanelHeight(Gun.Slot.Main))
        : new Rect(0f, 0f, PanelWidth(Gun.Slot.Sidearm), PanelHeight(Gun.Slot.Sidearm));

    private static Rect IconArea(Rect panel, bool inHand) => inHand
        ? new Rect(panel.xMin + 10f, panel.yMin + 24f, 104f, panel.height - 38f)
        : new Rect(panel.xMin + 8f, panel.yMin + 5f, 52f, panel.height - 10f);

    private Rect PipArea(Rect panel) => new Rect(panel.xMin + 10f, panel.yMin + 8f, width - 20f, 7f);

    private void BuildText()
    {
        header = MakeText("Header", "> EQUIPMENT", 13, Rect.zero, UiColours.Phosphor, TextAlignmentOptions.BottomLeft);
        BuildPanelText(mainText, "Main");
        BuildPanelText(sidearmText, "Sidearm");
        pickupNotice = MakeText("Pickup Notice", "", 13, Rect.zero, UiColours.Phosphor, TextAlignmentOptions.BottomLeft);
        Layout();
    }

    private void BuildPanelText(PanelText text, string name)
    {
        text.Slot = MakeText(name + " Slot", "", 11, Rect.zero, UiColours.Phosphor, TextAlignmentOptions.TopLeft);
        text.Name = MakeText(name + " Name", "", 16, Rect.zero, UiColours.Phosphor, TextAlignmentOptions.Left);
        text.Ammo = MakeText(name + " Ammo", "", 26, Rect.zero, UiColours.Phosphor, TextAlignmentOptions.Left);
        text.Status = MakeText(name + " Status", "", 11, Rect.zero, UiColours.Phosphor, TextAlignmentOptions.Right);
        text.Ammo.characterSpacing = 2f;
    }

    // Puts the text where the panels are now: again whenever the gun in hand changes, as the
    // panels change size.
    private void Layout()
    {
        Rect top = PanelRect(Gun.Slot.Main);
        Place(header, new Rect(0f, top.yMax + 3f, width, 18f));
        Place(pickupNotice, new Rect(0f, top.yMax + 22f, width, 18f));
        LayoutPanel(mainText, Gun.Slot.Main);
        LayoutPanel(sidearmText, Gun.Slot.Sidearm);
    }

    private void LayoutPanel(PanelText text, Gun.Slot slot)
    {
        Rect panel = PanelRect(slot);
        bool inHand = InHand(slot);
        text.Ammo.gameObject.SetActive(inHand);
        text.Status.gameObject.SetActive(inHand);

        if (inHand)
        {
            Place(text.Slot, new Rect(8f, panel.yMax - 18f, width - 16f, 16f));
            text.Slot.fontSize = 11;
            Place(text.Name, new Rect(124f, panel.yMax - 40f, width - 132f, 20f));
            text.Name.fontSize = 16;
            text.Name.alignment = TextAlignmentOptions.Left;
            Place(text.Ammo, new Rect(124f, panel.yMin + 22f, width - 132f, 32f));
            Place(text.Status, new Rect(124f, panel.yMin + 16f, width - 132f, 14f));
        }
        else
        {
            // One line beside a small icon: "AK-47  30/30  +60".
            Place(text.Slot, new Rect(66f, panel.yMax - 16f, panel.width - 72f, 14f));
            text.Slot.fontSize = 10;
            Place(text.Name, new Rect(66f, panel.yMin + 4f, panel.width - 72f, 18f));
            text.Name.fontSize = 13;
            text.Name.alignment = TextAlignmentOptions.BottomLeft;
        }
    }

    private static void Place(TMP_Text text, Rect rect)
    {
        RectTransform r = text.rectTransform;
        r.anchoredPosition = rect.position;
        r.sizeDelta = rect.size;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (ammo != null)
            ammo.PickedUp -= ShowPickup;
    }

    private void ShowPickup(AmmoType type, int amount)
    {
        pickupNotice.text = $"+{amount} {AmmoInfo.Name(type)}";
        pickupNotice.color = AmmoInfo.Colour(type);
        pickupNoticeUntil = Time.unscaledTime + pickupNoticeTime;
    }

    private TMP_Text MakeText(string name, string text, float size, Rect rect, Color colour, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
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
        label.characterSpacing = 6f;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.richText = true;
        label.text = text;
        return label;
    }

    // ---------- Text ----------

    private void UpdateText()
    {
        // Only touch the text when something changed; rebuilding TMP every frame is wasteful.
        string all = PanelString(Gun.Slot.Main, weapons.Main) + "|" + PanelString(Gun.Slot.Sidearm, weapons.Sidearm);

        if (all == lastTexts)
            return;

        lastTexts = all;
        FillPanelText(mainText, Gun.Slot.Main, weapons.Main);
        FillPanelText(sidearmText, Gun.Slot.Sidearm, weapons.Sidearm);
    }

    private string PanelString(Gun.Slot slot, PlayerWeapon.Equipped equipped)
    {
        Gun gun = equipped.Gun;
        bool active = slot == weapons.ActiveSlot;
        string ammo = gun != null ? equipped.Name + gun.GetCurrentAmmo() + "/" + gun.GetMagazineSize() + "+" + gun.Reserve : "";
        return active + SlotLabel(slot) + ammo + StatusText(gun, active);
    }

    private void FillPanelText(PanelText text, Gun.Slot slot, PlayerWeapon.Equipped equipped)
    {
        Gun gun = equipped.Gun;
        bool active = slot == weapons.ActiveSlot;
        float brightness = active ? 1f : Unlit;

        text.Slot.text = active ? "<b>>> " + SlotLabel(slot) + "</b>" : SlotLabel(slot);
        text.Slot.color = Fade(UiColours.Phosphor, brightness);

        if (!active)
            text.Name.text = gun != null ? $"{equipped.Name}  {gun.GetCurrentAmmo()}/{gun.GetMagazineSize()}  <alpha=#88>+{gun.Reserve}" : "-- EMPTY --";
        else
            text.Name.text = gun != null ? equipped.Name : "-- EMPTY --";

        text.Name.color = Fade(UiColours.Phosphor, brightness);
        text.Ammo.text = gun != null ? $"{gun.GetCurrentAmmo()}<size=60%> / {gun.GetMagazineSize()}</size>" : "";
        text.Ammo.color = Fade(AmmoColour(gun), brightness);
        text.Status.text = StatusText(gun, active);
        Color status = gun != null && !gun.IsReloading && (gun.GetCurrentAmmo() == 0 || gun.Reserve == 0) ? UiColours.Hostile : UiColours.Phosphor;
        text.Status.color = Fade(status, brightness);
    }

    private static string SlotLabel(Gun.Slot slot)
    {
        string button = GameInput.Label(slot == Gun.Slot.Main ? GameInput.WeaponMain : GameInput.WeaponSidearm);
        return (slot == Gun.Slot.Main ? "MAIN" : "SIDEARM") + "  [" + button + "]";
    }

    // Warnings blink on the gun in hand and stay still on the other.
    private string StatusText(Gun gun, bool active)
    {
        if (gun == null)
            return "";

        bool show = !active || Blink();

        if (gun.IsReloading)
            return show ? "RELOADING" : "";

        if (gun.GetCurrentAmmo() == 0)
        {
            if (gun.Reserve == 0)
                return show ? "OUT OF AMMO" : "";

            if (!active)
                return "EMPTY";

            return show ? "EMPTY // [" + GameInput.Label(GameInput.Reload) + "] RELOAD" : "";
        }

        // Normally: which ammo it takes and how many spare rounds are left.
        return $"{AmmoInfo.Name(gun.AmmoType)}  +{gun.Reserve}";
    }

    private Color AmmoColour(Gun gun)
    {
        if (gun == null)
            return Fade(UiColours.Phosphor, 0.5f);

        return gun.GetCurrentAmmo() <= Mathf.FloorToInt(gun.GetMagazineSize() * lowAmmo) ? UiColours.Hostile : UiColours.Phosphor;
    }

    private static bool Blink()
    {
        return Mathf.Repeat(Time.unscaledTime * 2.5f, 1f) < 0.6f;
    }
}
