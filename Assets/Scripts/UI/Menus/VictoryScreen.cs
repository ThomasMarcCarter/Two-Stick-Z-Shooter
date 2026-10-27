using TMPro;
using UnityEngine;

// Shown when the player climbs out of the supply room under the last floor. Pauses the game, shows the run's
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

    // zone: where the run was ("MILITARY BASE"); recovered: the materials still in the backpack,
    // sent home with the win ("" for none); story: the story item brought home and the goal's
    // progress ("" for none).
    public void Show(string zone, int floorsCleared, int enemiesKilled, float runSeconds, int health, int maxHealth, int scrapEarned, string recovered = "", string story = "")
    {
        IsShowing = true;
        Time.timeScale = 0f;

        int minutes = Mathf.FloorToInt(runSeconds / 60f);
        int seconds = Mathf.FloorToInt(runSeconds % 60f);

        stats.text =
            $"<mspace=0.62em>OPERATION AREA     {zone}\n" +
            $"FLOORS CLEARED     {floorsCleared}\n" +
            $"HOSTILES DOWN      {enemiesKilled}\n" +
            $"VITALS REMAINING   {health}/{maxHealth}\n" +
            $"MISSION TIME       {minutes:00}:{seconds:00}\n" +
            $"<color={UiColours.AmberHex}>SCRAP SALVAGED     +{scrapEarned}  ({MetaProgress.Scrap} BANKED)</color>";

        notice.text = recovered.Length > 0 ? $"<size=80%>MATERIALS BROUGHT HOME: {recovered}</size>" : "";

        if (story.Length > 0)
            notice.text = $"<color={UiColours.AmberHex}>{story}</color>" + (notice.text.Length > 0 ? "\n" + notice.text : "");

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
