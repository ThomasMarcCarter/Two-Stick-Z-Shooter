using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A menu button that works under the CRT curve. Hover is tested against GameInput.Pointer (the
// point shown under the crosshair) rather than the raw mouse position the EventSystem would use,
// which drifts away from the button the further it is from the centre of the screen.
// Selection and clicking are handled by CrtMenu.
public class CrtButton : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;

    [Header("Look")]
    [SerializeField] private Color colour = new Color(0.35f, 1f, 0.55f, 0.12f);
    [SerializeField] private Color hoverColour = new Color(0.35f, 1f, 0.55f, 0.4f);
    [SerializeField] private Color labelColour = new Color(0.35f, 1f, 0.55f, 0.75f);
    [SerializeField] private Color labelHoverColour = Color.white;

    public TMP_Text Label => label;

    private Camera uiCamera;

    private void Awake()
    {
        Canvas canvas = GetComponentInParent<Canvas>(true);
        uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    // For buttons made at runtime.
    public void Setup(Image buttonBackground, TMP_Text buttonLabel, Color normal, Color hover, Color text, Color textHover)
    {
        background = buttonBackground;
        label = buttonLabel;
        colour = normal;
        hoverColour = hover;
        labelColour = text;
        labelHoverColour = textHover;
        SetHighlighted(false);
    }

    public bool IsPointerOver()
    {
        return RectTransformUtility.RectangleContainsScreenPoint(background.rectTransform, GameInput.Pointer, uiCamera);
    }

    public void SetHighlighted(bool highlighted)
    {
        background.color = highlighted ? hoverColour : colour;
        label.color = highlighted ? labelHoverColour : labelColour;
    }
}
