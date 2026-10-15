using TMPro;
using UnityEngine;

// Shown when the player takes the trophy after the last boss. Pauses the game, shows the run's
// stats, and offers a new run, the hideout or the main menu. Works with mouse or controller (CrtMenu).
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

    public void Show(int floorsCleared, int enemiesKilled, float runSeconds, int health, int maxHealth)
    {
        IsShowing = true;
        Time.timeScale = 0f;

        int minutes = Mathf.FloorToInt(runSeconds / 60f);
        int seconds = Mathf.FloorToInt(runSeconds % 60f);

        stats.text =
            $"<mspace=0.62em>FLOORS CLEARED     {floorsCleared}\n" +
            $"HOSTILES DOWN      {enemiesKilled}\n" +
            $"VITALS REMAINING   {health}/{maxHealth}\n" +
            $"MISSION TIME       {minutes:00}:{seconds:00}";

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
