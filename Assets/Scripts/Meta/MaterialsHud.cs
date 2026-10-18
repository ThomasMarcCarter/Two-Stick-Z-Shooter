using TMPro;
using UnityEngine;

// The HUD's material pouch, under the stamina bar: what's been picked up this run and not yet
// banked ("> POUCH // UNBANKED" and a coloured count of each), so the player knows what a death
// would cost. When a checkpoint banks them it flashes "MATERIALS BANKED" with what went in, then
// clears. Hidden when the pouch is empty, in the hideout, and while menus cover the HUD.
// Built in code on the HUD canvas by FloorGenerator.Start (Ensure).
public class MaterialsHud : MonoBehaviour
{
    private static readonly Color Phosphor = UiColours.Phosphor;
    private const float BankedTime = 3.5f;

    private TMP_Text header;
    private TMP_Text counts;
    private float bankedUntil;
    private string bankedText;
    private FloorGenerator floor;

    public static void Ensure()
    {
        if (FindAnyObjectByType<MaterialsHud>() != null)
            return;

        EquipmentHud hud = FindAnyObjectByType<EquipmentHud>();
        Canvas canvas = hud != null ? hud.canvas : null;

        if (canvas == null)
            return;

        GameObject root = new GameObject("Materials Hud", typeof(RectTransform));
        root.layer = canvas.gameObject.layer;
        root.transform.SetParent(canvas.transform, false);

        // With the other HUD widgets, under the menus and the cursor.
        root.transform.SetSiblingIndex(hud.transform.GetSiblingIndex() + 1);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(40f, -124f);
        rect.sizeDelta = new Vector2(520f, 40f);

        MaterialsHud materials = root.AddComponent<MaterialsHud>();
        TMP_FontAsset font = hud.GetComponentInChildren<TMP_Text>(true) != null ? hud.GetComponentInChildren<TMP_Text>(true).font : TMP_Settings.defaultFontAsset;
        materials.header = materials.MakeText("Header", font, 11f, 0f, new Color(Phosphor.r, Phosphor.g, Phosphor.b, 0.75f));
        materials.counts = materials.MakeText("Counts", font, 13f, -16f, Phosphor);
    }

    private TMP_Text MakeText(string name, TMP_FontAsset font, float size, float y, Color colour)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(520f, 18f);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = colour;
        text.characterSpacing = 4f;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    private void OnEnable()
    {
        RunMaterials.Banked += OnBanked;
    }

    private void OnDisable()
    {
        RunMaterials.Banked -= OnBanked;
    }

    private void OnBanked(int[] banked)
    {
        bankedText = RunMaterials.Describe(banked);
        bankedUntil = Time.unscaledTime + BankedTime;
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-fit", 3, ProceduralAudio.AttachmentFit)), 0.35f, 1.3f);
    }

    private void Update()
    {
        if (floor == null)
            floor = FindAnyObjectByType<FloorGenerator>();

        bool banking = Time.unscaledTime < bankedUntil;
        bool show = (RunMaterials.Total > 0 || banking) && (floor == null || !floor.IsHideout)
            && !InventoryScreen.IsOpen && !PauseMenu.IsOpen && !MainMenu.IsOpen;

        header.enabled = counts.enabled = show;

        if (!show)
            return;

        if (banking)
        {
            header.text = $"<color={UiColours.AmberHex}>> MATERIALS BANKED // SAFE IN THE HIDEOUT</color>";
            counts.text = bankedText;
            return;
        }

        int secure = RunMaterials.SecureSize;
        header.text = secure > 0
            ? $"> POUCH <alpha=#88>// UNBANKED: LOST ON DEATH, {secure} SECURE"
            : "> POUCH <alpha=#88>// UNBANKED: LOST ON DEATH";
        string text = "";

        foreach (MaterialType type in Materials.All)
        {
            int amount = RunMaterials.Carried(type);

            if (amount > 0)
                text += $"<color={Materials.Hex(type)}>{Materials.Name(type)} {amount}</color>   ";
        }

        counts.text = text;
    }
}
