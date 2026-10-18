using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The title screen shown when the game starts: New Run, Enter Hideout, Settings, Exit to Desktop,
// in the HUD's phosphor style. The game is paused and hidden behind it. New Run deploys through
// the floor loading screen onto a fresh floor 1. Restarting a run from the pause or death screen
// reloads the scene and skips straight past this (see RunFlow).
// Built in code when the game starts; it sits on the Canvas below the Pause Menu, so the shared
// settings screen draws on top of it.
public class MainMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    // Set by RunFlow before reloading the scene for a new run, so the reload goes straight in.
    public static bool SkipOnNextLoad { get; set; }

    [Header("References")]
    [Tooltip("Found automatically if left empty.")]
    [SerializeField] private SettingsMenu settings;
    [Tooltip("Found automatically if left empty.")]
    [SerializeField] private FloorGenerator floor;
    [Tooltip("Found automatically if left empty.")]
    [SerializeField] private FloorTransition transition;

    [Header("Text")]
    [Tooltip("Leave empty to use the product name from Player Settings.")]
    [SerializeField] private string title = "";
    [SerializeField] private string subtitle = "> TERMINAL READY // AWAITING OPERATOR";

    [Header("Look")]
    [SerializeField] private Color phosphor = new Color(0.35f, 1f, 0.55f, 1f);
    [SerializeField] private Color background = new Color(0f, 0.025f, 0.012f, 1f);
    [SerializeField] private Vector2 buttonSize = new Vector2(340f, 42f);

    private GameObject backdrop;
    private GameObject panel;
    private TMP_Text subtitleText;
    private TMP_Text notice;
    private CrtMenu menu;
    private float previousTimeScale = 1f;

    // Statics outlive Play sessions with domain reload off: a fresh session starts at the menu.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        IsOpen = false;
        SkipOnNextLoad = false;
    }

    private void Awake()
    {
        if (settings == null)
            settings = FindAnyObjectByType<SettingsMenu>(FindObjectsInactive.Include);

        if (floor == null)
            floor = FindAnyObjectByType<FloorGenerator>();

        if (transition == null)
            transition = FindAnyObjectByType<FloorTransition>(FindObjectsInactive.Include);

        Build();

        if (SkipOnNextLoad)
        {
            SkipOnNextLoad = false;
            backdrop.SetActive(false);
            return;
        }

        Open();
    }

    private void OnDisable()
    {
        if (IsOpen)
            Close();
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        // A blinking cursor on the subtitle.
        subtitleText.text = subtitle + (Mathf.Repeat(Time.unscaledTime * 1.6f, 1f) < 0.5f ? "_" : " ");

        if (settings != null && settings.IsOpen)
        {
            // Pause backs out of the settings too, except while waiting for a rebind.
            bool pauseBack = !settings.IsRebinding && GameInput.Pause.WasPressedThisFrame()
                && !GameInput.Cancel.WasPressedThisFrame();

            if (settings.Tick() || pauseBack)
                CloseSettings();

            return;
        }

        switch (menu.Update())
        {
            case 0:
                NewRun();
                break;
            case 1:
                RunFlow.ExitToHideout();
                break;
            case 2:
                OpenSettings();
                break;
            case 3:
                ExitToDesktop();
                break;
        }
    }

    private void Open()
    {
        IsOpen = true;
        previousTimeScale = Time.timeScale == 0f ? 1f : Time.timeScale;
        Time.timeScale = 0f;

        notice.text = "";
        backdrop.SetActive(true);
        panel.SetActive(true);
        menu.Open();
    }

    private void Close()
    {
        if (settings != null && settings.IsOpen)
            settings.Close();

        IsOpen = false;
        Time.timeScale = previousTimeScale;
        backdrop.SetActive(false);

        // In the editor, clicking frees the cursor; hide it again for the crosshair.
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    // Deploy: the loading screen covers building a fresh floor 1, then the run begins. Without
    // a loading screen, the floor built at startup is used as it is.
    private void NewRun()
    {
        Close();

        if (transition != null && floor != null)
            transition.Play(1, floor.TotalFloors, floor.Generate);
    }

    private void OpenSettings()
    {
        if (settings == null)
            return;

        panel.SetActive(false);
        settings.Open();
    }

    private void CloseSettings()
    {
        settings.Close();
        panel.SetActive(true);
        menu.Open();
    }

    private static void ExitToDesktop()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Building ----------

    private void Build()
    {
        // Full-screen, opaque: hides the HUD and the floor behind it.
        backdrop = new GameObject("Backdrop", typeof(RectTransform));
        backdrop.layer = gameObject.layer;
        backdrop.transform.SetParent(transform, false);
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = backdropRect.offsetMax = Vector2.zero;
        Image fill = backdrop.AddComponent<Image>();
        fill.color = background;
        fill.raycastTarget = false;

        panel = new GameObject("Panel", typeof(RectTransform));
        panel.layer = gameObject.layer;
        panel.transform.SetParent(backdrop.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(700f, 520f);

        Color dim = new Color(phosphor.r, phosphor.g, phosphor.b, 0.55f);
        string heading = string.IsNullOrEmpty(title) ? Application.productName : title;

        MakeText("Title", heading.ToUpperInvariant(), 54, new Vector2(0f, 175f), new Vector2(700f, 70f), phosphor, 14f);
        subtitleText = MakeText("Subtitle", subtitle, 15, new Vector2(0f, 122f), new Vector2(700f, 24f), dim, 6f);

        string[] labels = { "[ NEW RUN ]", "[ ENTER HIDEOUT ]", "[ SETTINGS ]", "[ EXIT TO DESKTOP ]" };
        CrtButton[] buttons = new CrtButton[labels.Length];

        for (int i = 0; i < labels.Length; i++)
            buttons[i] = MakeButton(labels[i], new Vector2(0f, 40f - i * (buttonSize.y + 14f)));

        menu = new CrtMenu(buttons);

        notice = MakeText("Notice", "", 13, new Vector2(0f, -195f), new Vector2(700f, 22f), dim, 4f);
        MakeText("Version", $"BUILD {Application.version}", 11, new Vector2(0f, -240f), new Vector2(700f, 18f),
            new Color(phosphor.r, phosphor.g, phosphor.b, 0.3f), 4f);
    }

    private TMP_Text MakeText(string name, string text, float size, Vector2 position, Vector2 boxSize, Color colour, float spacing)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(panel.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = boxSize;

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = size;
        label.color = colour;
        label.characterSpacing = spacing;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    private CrtButton MakeButton(string text, Vector2 position)
    {
        GameObject go = new GameObject(text, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(panel.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = buttonSize;

        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.layer = gameObject.layer;
        labelObject.transform.SetParent(go.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 17;
        label.characterSpacing = 8f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.text = text;

        CrtButton button = go.AddComponent<CrtButton>();
        button.Setup(image, label,
            new Color(phosphor.r, phosphor.g, phosphor.b, 0.08f),
            new Color(phosphor.r, phosphor.g, phosphor.b, 0.35f),
            new Color(phosphor.r, phosphor.g, phosphor.b, 0.85f),
            Color.white);
        return button;
    }
}
