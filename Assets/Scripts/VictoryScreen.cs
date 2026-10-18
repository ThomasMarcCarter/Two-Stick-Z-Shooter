using TMPro;
using UnityEngine;

// Shown when the player takes the trophy after the last boss. Pauses the game, shows the run's
// stats and the scrap it earned for the hideout, and offers a new run, the hideout or the main menu. Works with mouse or controller (CrtMenu).
public class VictoryScreen : MonoBehaviour
{
    public static bool IsShowing { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text stats;
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

    public void Show(int floorsCleared, int enemiesKilled, float runSeconds, int health, int maxHealth, int scrapEarned)
    {
        IsShowing = true;
        Time.timeScale = 0f;

        int minutes = Mathf.FloorToInt(runSeconds / 60f);
        int seconds = Mathf.FloorToInt(runSeconds % 60f);

        stats.text =
            $"<mspace=0.62em>FLOORS CLEARED     {floorsCleared}\n" +
            $"HOSTILES DOWN      {enemiesKilled}\n" +
            $"VITALS REMAINING   {health}/{maxHealth}\n" +
            $"MISSION TIME       {minutes:00}:{seconds:00}\n" +
            $"<color={UiColours.AmberHex}>SCRAP SALVAGED     +{scrapEarned}  ({MetaProgress.Scrap} BANKED)</color>";

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
                RunFlow.ExitToHideout();
                break;
            case 2:
                RunFlow.ExitToMainMenu();
                break;
        }
    }
}
