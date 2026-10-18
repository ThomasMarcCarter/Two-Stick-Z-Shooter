using TMPro;
using UnityEngine;

// Shown when the player dies. Pauses the game, banks the run's scrap (MetaProgress) and offers a
// new run, the hideout or the main menu. Works with mouse or controller (CrtMenu).
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

        // The run pays out its scrap for the hideout: kills, and each boss beaten.
        FloorGenerator floor = FindAnyObjectByType<FloorGenerator>();
        int scrap = floor != null ? MetaProgress.EndRun(false, floor.FloorNumber - 1, floor.FloorNumber, floor.EnemiesKilled) : 0;
        notice.text = $"<color=#FFD95A>+{scrap} SCRAP SALVAGED // {MetaProgress.Scrap} BANKED FOR THE HIDEOUT</color>";
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
                RunFlow.ExitToHideout();
                break;
            case 2:
                RunFlow.ExitToMainMenu();
                break;
        }
    }
}
