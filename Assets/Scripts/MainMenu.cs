using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The title screen shown when the game starts: Start Game, Profiles, Settings, Exit to Desktop,
// in the HUD's phosphor style. The game is paused and hidden behind it. Start Game loads the
// hideout, and every run is deployed from there. Restarting a run from the pause or death screen
// reloads the scene and skips straight past this (see RunFlow).
// Profiles lists the three save profiles (scrap, completed runs, survivors freed) to pick one or
// wipe one; wiping asks to be pressed a second time to confirm. The profile in use is shown under
// the title.
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

    [Header("Text")]
    [Tooltip("Leave empty to use the product name from Player Settings.")]
    [SerializeField] private string title = "";
    [SerializeField] private string subtitle = "> TERMINAL READY // AWAITING OPERATOR";

    [Header("Look")]
    [SerializeField] private Color phosphor = UiColours.Phosphor;
    [SerializeField] private Color background = new Color(0f, 0.025f, 0.012f, 1f);
    [SerializeField] private Vector2 buttonSize = new Vector2(340f, 42f);

    private GameObject backdrop;
    private GameObject panel;
    private GameObject mainPage;
    private GameObject profilePage;
    private TMP_Text subtitleText;
    private TMP_Text profileLine;
    private TMP_Text notice;
    private CrtMenu menu;
    private CrtMenu profileMenu;
    private CrtButton[] profileButtons;
    private CrtButton[] wipeButtons;
    private int confirmingWipe;   // profile waiting for a second press of its wipe button, or 0
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

        if (profilePage.activeSelf)
        {
            UpdateProfiles();
            return;
        }

        switch (menu.Update())
        {
            case 0:
                RunFlow.ExitToHideout();
                break;
            case 1:
                OpenProfiles();
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
        ShowMain();
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

    private void ShowMain()
    {
        profilePage.SetActive(false);
        mainPage.SetActive(true);
        profileLine.text = $"PROFILE {MetaProgress.Profile} // {Describe(MetaProgress.Summary(MetaProgress.Profile))}";
        menu.Open();
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

    // ---------- Profiles ----------

    private void OpenProfiles()
    {
        confirmingWipe = 0;
        notice.text = "";
        mainPage.SetActive(false);
        profilePage.SetActive(true);
        RefreshProfiles();
        profileMenu.Open();
    }

    // Buttons are numbered: the profiles, then Back, then the wipe buttons.
    private void UpdateProfiles()
    {
        if (GameInput.Cancel.WasPressedThisFrame() || GameInput.Pause.WasPressedThisFrame())
        {
            notice.text = "";
            ShowMain();
            return;
        }

        int choice = profileMenu.Update();
        int count = MetaProgress.ProfileCount;

        if (choice < 0)
            return;

        if (choice < count)
        {
            MetaProgress.SelectProfile(choice + 1);
            notice.text = $"PROFILE {choice + 1} SELECTED";
            ShowMain();
        }
        else if (choice == count)
        {
            notice.text = "";
            ShowMain();
        }
        else
        {
            Wipe(choice - count);
        }
    }

    // The first press arms the wipe, the second press of the same button does it.
    private void Wipe(int slot)
    {
        if (!MetaProgress.Summary(slot).Exists)
        {
            confirmingWipe = 0;
            notice.text = $"PROFILE {slot} IS ALREADY EMPTY";
        }
        else if (confirmingWipe != slot)
        {
            confirmingWipe = slot;
            notice.text = $"<color={UiColours.DangerHex}>ERASE PROFILE {slot}? THIS CANNOT BE UNDONE. PRESS AGAIN TO CONFIRM</color>";
        }
        else
        {
            confirmingWipe = 0;
            MetaProgress.WipeProfile(slot);
            notice.text = $"PROFILE {slot} WIPED";
        }

        RefreshProfiles();
    }

    private void RefreshProfiles()
    {
        for (int i = 0; i < profileButtons.Length; i++)
        {
            int slot = i + 1;
            string text = $"PROFILE {slot} // {Describe(MetaProgress.Summary(slot))}";
            profileButtons[i].Label.text = slot == MetaProgress.Profile ? $"> {text} <" : text;
            wipeButtons[i].Label.text = confirmingWipe == slot ? "[ CONFIRM ]" : "[ WIPE ]";
        }
    }

    private static string Describe(MetaProgress.ProfileSummary summary)
    {
        if (!summary.Exists)
            return "EMPTY";

        return $"{summary.Scrap} SCRAP, {summary.Victories} WON, {summary.Rescued} FREED";
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

        panel = MakeGroup("Panel", backdrop.transform);
        mainPage = MakeGroup("Main", panel.transform);
        profilePage = MakeGroup("Profiles", panel.transform);

        Color dim = new Color(phosphor.r, phosphor.g, phosphor.b, 0.55f);
        string heading = string.IsNullOrEmpty(title) ? Application.productName : title;

        MakeText("Title", panel.transform, heading.ToUpperInvariant(), 54, new Vector2(0f, 175f), new Vector2(700f, 70f), phosphor, 14f);
        subtitleText = MakeText("Subtitle", panel.transform, subtitle, 15, new Vector2(0f, 122f), new Vector2(700f, 24f), dim, 6f);
        profileLine = MakeText("Profile", mainPage.transform, "", 13, new Vector2(0f, 90f), new Vector2(700f, 22f), dim, 4f);

        // No quit button where the platform doesn't allow one (consoles).
        string[] labels = Platform.CanQuit
            ? new[] { "[ START GAME ]", "[ PROFILES ]", "[ SETTINGS ]", "[ EXIT TO DESKTOP ]" }
            : new[] { "[ START GAME ]", "[ PROFILES ]", "[ SETTINGS ]" };
        CrtButton[] buttons = new CrtButton[labels.Length];

        for (int i = 0; i < labels.Length; i++)
            buttons[i] = MakeButton(labels[i], mainPage.transform, new Vector2(0f, 40f - i * (buttonSize.y + 14f)), buttonSize);

        menu = new CrtMenu(buttons);

        // The profile page: a row per profile (pick it) with its wipe button, and Back.
        MakeText("Profiles Heading", profilePage.transform, "SELECT PROFILE", 15, new Vector2(0f, 90f), new Vector2(700f, 22f), dim, 6f);
        int count = MetaProgress.ProfileCount;
        profileButtons = new CrtButton[count];
        wipeButtons = new CrtButton[count];
        CrtButton[] rows = new CrtButton[count + 1];

        for (int i = 0; i < count; i++)
        {
            float y = 40f - i * (buttonSize.y + 14f);
            profileButtons[i] = MakeButton("", profilePage.transform, new Vector2(-95f, y), new Vector2(500f, buttonSize.y));
            profileButtons[i].Label.fontSize = 14;
            profileButtons[i].Label.characterSpacing = 3f;
            wipeButtons[i] = MakeButton("[ WIPE ]", profilePage.transform, new Vector2(260f, y), new Vector2(170f, buttonSize.y));
            rows[i] = profileButtons[i];
        }

        rows[count] = MakeButton("[ BACK ]", profilePage.transform, new Vector2(0f, 40f - count * (buttonSize.y + 14f)), buttonSize);
        profileMenu = new CrtMenu(new[] { rows, wipeButtons }, 0, 0);
        profilePage.SetActive(false);

        notice = MakeText("Notice", panel.transform, "", 13, new Vector2(0f, -195f), new Vector2(700f, 22f), dim, 4f);
        MakeText("Version", panel.transform, $"BUILD {Application.version}", 11, new Vector2(0f, -240f), new Vector2(700f, 18f),
            new Color(phosphor.r, phosphor.g, phosphor.b, 0.3f), 4f);
    }

    // A centred 700 x 520 area to lay things out in.
    private GameObject MakeGroup(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(700f, 520f);
        return go;
    }

    private TMP_Text MakeText(string name, Transform parent, string text, float size, Vector2 position, Vector2 boxSize, Color colour, float spacing)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
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

    private CrtButton MakeButton(string text, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(string.IsNullOrEmpty(text) ? "Button" : text, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

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
