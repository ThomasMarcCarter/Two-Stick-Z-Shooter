using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// Stamina as one continuous bar under the health bar, in the same phosphor style. Brightens
// while sprinting; after running dry it turns red and shows a dim marker at the point sprinting
// unlocks again, until it has recovered. A small SNEAK tag sits beside it while sneaking.
public class StaminaDisplay : MaskableGraphic
{
    [SerializeField] private PlayerStamina stamina;

    [Header("Look")]
    [SerializeField] private Color phosphor = new Color(0.35f, 1f, 0.55f, 1f);
    [SerializeField] private Color hostile = new Color(1f, 0.25f, 0.2f, 1f);
    [Tooltip("Fill colour while not sprinting, as a fraction of full brightness.")]
    [SerializeField, Range(0f, 1f)] private float idleBrightness = 0.7f;

    private PlayerMovement movement;
    private TMP_Text sneakTag;
    private float lastFraction = -1f;
    private bool lastSprinting;
    private bool lastExhausted;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    private void Update()
    {
        if (!Application.isPlaying || stamina == null)
            return;

        UpdateSneakTag();

        if (!Mathf.Approximately(stamina.Fraction, lastFraction) || stamina.IsSprinting != lastSprinting
            || stamina.Exhausted != lastExhausted)
        {
            lastFraction = stamina.Fraction;
            lastSprinting = stamina.IsSprinting;
            lastExhausted = stamina.Exhausted;
            SetVerticesDirty();
        }
    }

    // Built on first use rather than in Awake: graphics run in edit mode too, and with scene
    // reload off they don't get a fresh Awake each time Play starts.
    private void UpdateSneakTag()
    {
        if (movement == null)
            movement = stamina.GetComponent<PlayerMovement>();

        if (sneakTag == null)
        {
            GameObject go = new GameObject("Sneak Tag", typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(8f, 0f);
            rect.sizeDelta = new Vector2(80f, 16f);

            TextMeshProUGUI tag = go.AddComponent<TextMeshProUGUI>();
            tag.font = TMP_Settings.defaultFontAsset;
            tag.fontSize = 12;
            tag.characterSpacing = 6f;
            tag.color = phosphor;
            tag.alignment = TextAlignmentOptions.Left;
            tag.textWrappingMode = TextWrappingModes.NoWrap;
            tag.raycastTarget = false;
            tag.text = "SNEAK";
            sneakTag = tag;
        }

        bool sneaking = movement != null && movement.IsSneaking;

        if (sneakTag.gameObject.activeSelf != sneaking)
            sneakTag.gameObject.SetActive(sneaking);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect bounds = rectTransform.rect;
        bool playing = Application.isPlaying && stamina != null;
        float fraction = playing ? stamina.Fraction : 1f;
        bool exhausted = playing && stamina.Exhausted;
        bool sprinting = playing && stamina.IsSprinting;

        Color tone = exhausted ? hostile : phosphor;
        Color fill = sprinting || exhausted ? tone : tone * idleBrightness;
        fill.a = 1f;

        FillRect(vh, bounds, Fade(tone, 0.08f));
        OutlineRect(vh, bounds, 1f, Fade(tone, 0.45f));

        // Inset so the outline stays visible around the fill.
        Rect inner = new Rect(bounds.xMin + 2f, bounds.yMin + 2f, bounds.width - 4f, bounds.height - 4f);

        if (fraction > 0f)
            FillRect(vh, new Rect(inner.xMin, inner.yMin, inner.width * fraction, inner.height), fill);

        // Where sprinting unlocks again.
        if (exhausted)
        {
            float x = inner.xMin + inner.width * stamina.RecoverFraction;
            FillRect(vh, new Rect(x - 1f, bounds.yMin, 2f, bounds.height), Fade(tone, 0.6f));
        }
    }
}
