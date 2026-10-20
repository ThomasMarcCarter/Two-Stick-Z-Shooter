using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// Bottom-left equipment panel in the radar's phosphor style. The gun in hand gets the big panel:
// its silhouette, name, ammo count, spare rounds of its ammo type, a pip per round, and a reload
// bar. It's highlighted so there's no mistaking it: a bright solid frame over a phosphor-tinted
// fill, a selection bar down its left side and its slot name in bold (">> MAIN [1]"); it flashes
// for a moment when the player switches. The holstered slot sits underneath, smaller,
// unlit and faded right down. Picking up ammo flashes a short
// "+12 9MM" notice above the panel. Each slot shows the button that switches to it, following the
// device in use and any rebinds. Frames, icons and pips are one mesh; the text is created at
// runtime, so the component only needs adding to the Canvas. Also builds the quick-use bar
// beside it (QuickBarHud).
public class EquipmentHud : MaskableGraphic
{
    [Header("References")]
    [Tooltip("Found automatically if left empty.")]
    [SerializeField] private PlayerWeapon weapons;
    [Tooltip("Defaults to the TextMeshPro default font if left empty.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Layout")]
    [SerializeField] private Vector2 margin = new Vector2(40f, 36f);
    [SerializeField] private float width = 300f;
    [SerializeField] private float activeHeight = 92f;
    [SerializeField] private float holsteredHeight = 36f;
    [SerializeField] private float gap = 6f;

    [Header("Look")]
    [Tooltip("At or below this fraction of a magazine the ammo turns red.")]
    [SerializeField, Range(0f, 1f)] private float lowAmmo = 0.25f;
    [Tooltip("How long the \"+12 9MM\" pickup notice stays up, in seconds.")]
    [SerializeField] private float pickupNoticeTime = 2.5f;

    private TMP_Text header;
    private TMP_Text activeSlotLabel;
    private TMP_Text activeName;
    private TMP_Text activeAmmo;
    private TMP_Text activeStatus;
    private TMP_Text holsteredSlotLabel;
    private TMP_Text holsteredInfo;
    private TMP_Text pickupNotice;

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
            weapons = FindAnyObjectByType<PlayerWeapon>();

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
        rectTransform.sizeDelta = new Vector2(width, activeHeight + gap + holsteredHeight);

        BuildText();

        // The quick-use bar sits just to the right, along the bottom.
        // Drawn straight after this panel, so the menus and screens that cover the HUD (main
        // menu, transitions, the full map) cover it too, rather than it landing on top of them.
        if (weapons != null)
        {
            QuickBarHud bar = QuickBarHud.Create(transform.parent, weapons, font, UiColours.Phosphor, margin + new Vector2(width + 20f, 0f));
            bar.transform.SetSiblingIndex(transform.GetSiblingIndex() + 1);
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

    private Rect ActiveRect => new Rect(0f, holsteredHeight + gap, width, activeHeight);
    private Rect HolsteredRect => new Rect(0f, 0f, width * 0.8f, holsteredHeight);
    private Rect ActiveIconArea => new Rect(10f, ActiveRect.yMin + 24f, 104f, ActiveRect.height - 38f);
    private Rect PipArea => new Rect(10f, ActiveRect.yMin + 8f, width - 20f, 7f);
    private Rect HolsteredIconArea => new Rect(8f, 5f, 52f, holsteredHeight - 10f);

    private void BuildText()
    {
        Color dim = Fade(UiColours.Phosphor, 0.6f);
        Rect active = ActiveRect;
        Rect holstered = HolsteredRect;

        header = MakeText("Header", "> EQUIPMENT", 13, new Rect(0f, active.yMax + 3f, width, 18f), UiColours.Phosphor, TextAlignmentOptions.BottomLeft);
        activeSlotLabel = MakeText("Active Slot", "", 11, new Rect(8f, active.yMax - 18f, width - 16f, 16f), UiColours.Phosphor, TextAlignmentOptions.TopLeft);
        activeName = MakeText("Active Name", "", 16, new Rect(124f, active.yMax - 40f, width - 132f, 20f), UiColours.Phosphor, TextAlignmentOptions.Left);
        activeAmmo = MakeText("Active Ammo", "", 26, new Rect(124f, active.yMin + 22f, width - 132f, 32f), UiColours.Phosphor, TextAlignmentOptions.Left);
        activeStatus = MakeText("Active Status", "", 11, new Rect(124f, active.yMin + 16f, width - 132f, 14f), UiColours.Phosphor, TextAlignmentOptions.Right);
        holsteredSlotLabel = MakeText("Holstered Slot", "", 10, new Rect(66f, holstered.yMax - 16f, holstered.width - 72f, 14f), Fade(UiColours.Phosphor, 0.25f), TextAlignmentOptions.TopLeft);
        holsteredInfo = MakeText("Holstered Info", "", 13, new Rect(66f, 4f, holstered.width - 72f, 18f), Fade(UiColours.Phosphor, 0.3f), TextAlignmentOptions.BottomLeft);

        pickupNotice = MakeText("Pickup Notice", "", 13, new Rect(0f, active.yMax + 22f, width, 18f), UiColours.Phosphor, TextAlignmentOptions.BottomLeft);
        activeAmmo.characterSpacing = 2f;
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
        PlayerWeapon.Equipped active = weapons.Active;
        PlayerWeapon.Equipped holstered = weapons.Holstered;
        Gun gun = active.Gun;

        string activeLabel = SlotLabel(weapons.ActiveSlot);
        string name = gun != null ? active.Name : "UNARMED";
        string ammo = gun != null ? $"{gun.GetCurrentAmmo()}<size=60%> / {gun.GetMagazineSize()}</size>" : "--";
        string status = StatusText(gun);
        Gun.Slot otherSlot = weapons.ActiveSlot == Gun.Slot.Main ? Gun.Slot.Sidearm : Gun.Slot.Main;
        string holsteredLabel = SlotLabel(otherSlot);
        string holsteredText = holstered.Gun != null
            ? $"{holstered.Name}  {holstered.Gun.GetCurrentAmmo()}/{holstered.Gun.GetMagazineSize()}  <alpha=#88>+{holstered.Gun.Reserve}"
            : "-- EMPTY --";

        // Only touch the text when something changed; rebuilding TMP every frame is wasteful.
        string all = activeLabel + name + ammo + status + holsteredLabel + holsteredText;

        if (all == lastTexts)
            return;

        lastTexts = all;
        activeSlotLabel.text = "<b>>> " + activeLabel + "</b>";
        activeName.text = name;
        activeAmmo.text = ammo;
        activeAmmo.color = AmmoColour(gun);
        activeStatus.text = status;
        activeStatus.color = gun != null && !gun.IsReloading && (gun.GetCurrentAmmo() == 0 || gun.Reserve == 0) ? UiColours.Hostile : UiColours.Phosphor;
        holsteredSlotLabel.text = holsteredLabel;
        holsteredInfo.text = holsteredText;
    }

    private static string SlotLabel(Gun.Slot slot)
    {
        string button = GameInput.Label(slot == Gun.Slot.Main ? GameInput.WeaponMain : GameInput.WeaponSidearm);
        return (slot == Gun.Slot.Main ? "MAIN" : "SIDEARM") + "  [" + button + "]";
    }

    private string StatusText(Gun gun)
    {
        if (gun == null)
            return "";

        if (gun.IsReloading)
            return Blink() ? "RELOADING" : "";

        if (gun.GetCurrentAmmo() == 0)
        {
            if (gun.Reserve == 0)
                return Blink() ? "OUT OF AMMO" : "";

            return Blink() ? "EMPTY // [" + GameInput.Label(GameInput.Reload) + "] RELOAD" : "";
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

    // ---------- Mesh ----------

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (weapons == null)
            return;

        Vector2 origin = rectTransform.rect.min;
        Gun gun = weapons.Active.Gun;
        Gun holstered = weapons.Holstered.Gun;

        // Active panel: lit up. A phosphor-tinted fill (brighter for a moment after switching), a
        // solid bright frame, the radar's corner brackets and a selection bar down the left.
        Rect active = Offset(ActiveRect, origin);
        float flash = 1f - Mathf.Clamp01((Time.unscaledTime - switchedAt) / SwitchFlashTime);
        FillRect(vh, active, new Color(0f, 0.07f, 0.03f, 0.85f));
        FillRect(vh, active, Fade(UiColours.Phosphor, 0.05f + 0.25f * flash));
        OutlineRect(vh, active, 2f, UiColours.Phosphor);
        Brackets(vh, active, UiColours.Phosphor);
        FillRect(vh, new Rect(active.xMin - 9f, active.yMin, 4f, active.height), UiColours.Phosphor);

        if (gun != null)
        {
            WeaponSilhouette.Draw(vh, gun.HudIcon, Offset(ActiveIconArea, origin), UiColours.Phosphor);
            DrawPips(vh, gun, Offset(PipArea, origin));
        }

        // Holstered panel: smaller, unlit and faded.
        Rect slot = Offset(HolsteredRect, origin);
        FillRect(vh, slot, new Color(0f, 0.04f, 0.02f, 0.5f));
        OutlineRect(vh, slot, 1f, Fade(UiColours.Phosphor, 0.18f));

        if (holstered != null)
            WeaponSilhouette.Draw(vh, holstered.HudIcon, Offset(HolsteredIconArea, origin), Fade(UiColours.Phosphor, 0.22f));
    }

    // One pip per round in the magazine, filled for rounds loaded; a thin bar shows reload progress.
    private void DrawPips(VertexHelper vh, Gun gun, Rect area)
    {
        int size = Mathf.Max(1, gun.GetMagazineSize());
        int loaded = gun.GetCurrentAmmo();
        float pipGap = size > 20 ? 1f : 3f;
        float pipWidth = (area.width - pipGap * (size - 1)) / size;
        Color full = AmmoColour(gun);

        for (int i = 0; i < size; i++)
        {
            Rect pip = new Rect(area.xMin + i * (pipWidth + pipGap), area.yMin, pipWidth, area.height);

            if (i < loaded)
                FillRect(vh, pip, full);
            else
                OutlineRect(vh, pip, 1f, Fade(UiColours.Phosphor, 0.35f));
        }

        if (gun.IsReloading)
        {
            Rect bar = new Rect(area.xMin, area.yMin - 4f, area.width * gun.ReloadProgress, 2f);
            FillRect(vh, bar, UiColours.Phosphor);
        }
    }

    private static void Brackets(VertexHelper vh, Rect rect, Color colour)
    {
        const float length = 12f;
        const float thickness = 2f;

        FillRect(vh, new Rect(rect.xMin, rect.yMax - thickness, length, thickness), colour);
        FillRect(vh, new Rect(rect.xMin, rect.yMax - length, thickness, length), colour);
        FillRect(vh, new Rect(rect.xMax - length, rect.yMax - thickness, length, thickness), colour);
        FillRect(vh, new Rect(rect.xMax - thickness, rect.yMax - length, thickness, length), colour);
        FillRect(vh, new Rect(rect.xMin, rect.yMin, length, thickness), colour);
        FillRect(vh, new Rect(rect.xMin, rect.yMin, thickness, length), colour);
        FillRect(vh, new Rect(rect.xMax - length, rect.yMin, length, thickness), colour);
        FillRect(vh, new Rect(rect.xMax - thickness, rect.yMin, thickness, length), colour);
    }

    private static Rect Offset(Rect rect, Vector2 origin)
    {
        return new Rect(rect.position + origin, rect.size);
    }
}
