using UnityEngine;
using UnityEngine.SceneManagement;

// Leaving the current run: starting a fresh one, or heading to the hideout. Shared by the main
// menu and the pause, death and victory screens.
public static class RunFlow
{
    // The hideout scene. Until a scene with this name exists and is in the build list, exiting
    // to the hideout just reports that it isn't available.
    public const string HideoutScene = "Hideout";

    public const string HideoutUnavailable = "> HIDEOUT UPLINK OFFLINE // UNDER CONSTRUCTION";

    // Reloads the game scene for a fresh run, going straight in rather than to the main menu.
    public static void RestartRun()
    {
        MainMenu.SkipOnNextLoad = true;
        Load(SceneManager.GetActiveScene().name);
    }

    // Abandons the run: reloads the game scene to the main menu.
    public static void ExitToMainMenu()
    {
        MainMenu.SkipOnNextLoad = false;
        Load(SceneManager.GetActiveScene().name);
    }

    // Returns false if there's no hideout to go to yet.
    public static bool TryExitToHideout()
    {
        if (!Application.CanStreamedLevelBeLoaded(HideoutScene))
            return false;

        Load(HideoutScene);
        return true;
    }

    private static void Load(string sceneName)
    {
        // Time scale and audio pause survive scene loads, so reset them before leaving.
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(sceneName);
    }
}
