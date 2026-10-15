using TMPro;
using UnityEngine;

// Shown when the player dies. Pauses the game and offers two buttons: start a new run, or exit
// to the hideout, or back to the main menu. Works with mouse or controller (CrtMenu).
public class DeathScreen : MonoBehaviour
{
    public static bool IsShowing { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private CrtButton newRunButton;
    [SerializeField] private CrtButton hideoutButton;
    [SerializeField] private CrtButton mainMenuButton;
    [SerializeField] private TMP_Text notice;

    private CrtMenu menu;

    private void Awake()
    {
        IsShowing = false;
        panel.SetActive(false);
        menu = new CrtMenu(newRunButton, hideoutButton, mainMenuButton);
    }

    public void Show()
    {
        IsShowing = true;
        Time.timeScale = 0f;
        notice.text = "";
        panel.SetActive(true);
        menu.Open();
    }

    private void Update()
    {
        if (!IsShowing)
            return;

        switch (menu.Update())
        {
            case 0:
                RunFlow.RestartRun();
                break;
            case 1:
                if (!RunFlow.TryExitToHideout())
                    notice.text = RunFlow.HideoutUnavailable;
                break;
            case 2:
                RunFlow.ExitToMainMenu();
                break;
        }
    }
}
