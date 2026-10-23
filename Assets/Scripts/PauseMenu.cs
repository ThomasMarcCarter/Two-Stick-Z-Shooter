using TMPro;
using UnityEngine;

// Pause (Esc / Menu) stops the game. Two columns: on the left a live preview of the tactical map,
// which opens the full map (and comes back here when it closes); on the right resume, settings,
// restart the run, exit to the hideout (not shown in the hideout itself; Main Menu moves up into
// its place), or exit to the main menu. Pause or Back (Esc / B) closes it again, or steps back
// out of the settings. Runs before FullscreenMap, so the Esc that closes the map doesn't also open this.
[DefaultExecutionOrder(-50)]
public class PauseMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [SerializeField] private GameObject panel;

    [Header("Left column")]
    [SerializeField] private CrtButton mapButton;
    [SerializeField] private TacticalMapView mapPreview;
    [SerializeField] private FullscreenMap fullscreenMap;
    [SerializeField] private Transform player;

    [Header("Right column")]
    [SerializeField] private CrtButton resumeButton;
    [SerializeField] private CrtButton settingsButton;
    [SerializeField] private CrtButton restartButton;
    [SerializeField] private CrtButton hideoutButton;
    [SerializeField] private CrtButton mainMenuButton;
    [SerializeField] private SettingsMenu settings;
    [SerializeField] private TMP_Text subtitle;
    [SerializeField] private TMP_Text notice;
    [SerializeField] private FloorGenerator floor;

    private CrtMenu menu;
    private CrtMenu runMenu;
    private CrtMenu hideoutMenu;
    private Vector2 hideoutButtonSpot;
    private Vector2 mainMenuButtonSpot;
    private float previousTimeScale = 1f;
    private int openedFrame;
    private bool returnFromMap;

    private void Awake()
    {
        IsOpen = false;
        panel.SetActive(false);

        // Left column: the map. Right column: everything else. The controller starts on Resume.
        runMenu = new CrtMenu(new[]
        {
            new[] { mapButton },
            new[] { resumeButton, settingsButton, restartButton, hideoutButton, mainMenuButton }
        }, 1, 0);

        // In the hideout there's no going back to it, so that button is left out.
        hideoutMenu = new CrtMenu(new[]
        {
            new[] { mapButton },
            new[] { resumeButton, settingsButton, restartButton, mainMenuButton }
        }, 1, 0);

        menu = runMenu;
        hideoutButtonSpot = ((RectTransform)hideoutButton.transform).anchoredPosition;
        mainMenuButtonSpot = ((RectTransform)mainMenuButton.transform).anchoredPosition;
    }

    private void OnEnable()
    {
        if (fullscreenMap != null)
            fullscreenMap.Closed += OnMapClosed;
    }

    private void OnDisable()
    {
        if (fullscreenMap != null)
            fullscreenMap.Closed -= OnMapClosed;

        if (IsOpen)
            Close();
    }

    private void Update()
    {
        if (!IsOpen)
        {
            if (GameInput.Pause.WasPressedThisFrame() && !GameFlow.InputBlocked)
                Open();

            return;
        }

        // Esc is both Pause and Back, so ignore the press that opened the menu.
        if (Time.frameCount == openedFrame)
            return;

        if (settings.IsOpen)
        {
            // Pause also backs out of the settings, but not while waiting for a rebind, where
            // the button pressed might be the one being bound.
            bool pauseBack = !settings.IsRebinding && GameInput.Pause.WasPressedThisFrame()
                && !GameInput.Cancel.WasPressedThisFrame();

            if (settings.Tick() || pauseBack)
                CloseSettings();

            return;
        }

        if (GameInput.Pause.WasPressedThisFrame() || GameInput.Cancel.WasPressedThisFrame())
        {
            Close();
            return;
        }

        int choice = menu.Update();

        // The hideout menu has no hideout button, so Main Menu is one number earlier there.
        if (menu == hideoutMenu && choice == 4)
            choice = 5;

        switch (choice)
        {
            case 0:
                OpenMap();
                break;
            case 1:
                Close();
                break;
            case 2:
                OpenSettings();
                break;
            case 3:
                RunFlow.RestartRun();
                break;
            case 4:
                RunFlow.ExitToHideout();
                break;
            case 5:
                RunFlow.ExitToMainMenu();
                break;
        }
    }

    // Swap the pause menu for the full map; OnMapClosed brings this back afterwards.
    private void OpenMap()
    {
        Close();
        returnFromMap = true;
        fullscreenMap.Open();
    }

    private void OnMapClosed()
    {
        if (!returnFromMap)
            return;

        returnFromMap = false;
        Open();
    }

    private void Open()
    {
        IsOpen = true;
        openedFrame = Time.frameCount;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        subtitle.text = floor != null
            ? (floor.IsHideout ? "OPERATION STANDBY // HIDEOUT"
                : floor.IsSupplyRoom ? "OPERATION SUSPENDED // SUPPLY ROOM"
                : $"OPERATION SUSPENDED // FLOOR {floor.FloorNumber} OF {floor.TotalFloors}")
            : "OPERATION SUSPENDED";
        notice.text = "";

        bool inHideout = floor != null && floor.IsHideout;
        menu = inHideout ? hideoutMenu : runMenu;
        hideoutButton.gameObject.SetActive(!inHideout);
        ((RectTransform)mainMenuButton.transform).anchoredPosition = inHideout ? hideoutButtonSpot : mainMenuButtonSpot;

        panel.SetActive(true);
        menu.Open();

        if (mapPreview != null && player != null)
            mapPreview.CentreOn(player.position);
    }

    private void Close()
    {
        if (settings.IsOpen)
            settings.Close();

        IsOpen = false;
        Time.timeScale = previousTimeScale;
        AudioListener.pause = false;
        panel.SetActive(false);

        // In the editor, Esc frees the cursor; hide it again for the crosshair.
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    private void OpenSettings()
    {
        panel.SetActive(false);
        settings.Open();
    }

    private void CloseSettings()
    {
        settings.Close();
        panel.SetActive(true);
        menu.Open();
    }
}
