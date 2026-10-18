using UnityEngine;
using UnityEngine.SceneManagement;

// Leaving the current run: starting a fresh one, heading to the hideout, or back to the main
// menu. Shared by the main menu and the pause, death and victory screens. Each reloads the game
// scene; the hideout is built by the FloorGenerator as floor 0 of that same scene (the hub with
// the stations, rescued survivors and the hatch down to floor 1), so no other scene is needed.
// Any reload loses the run's unbanked materials.
public static class RunFlow
{
    // Set before reloading for the hideout; the FloorGenerator reads it once when it starts.
    private static bool enterHideoutOnLoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        enterHideoutOnLoad = false;
    }

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

    // Reloads the game scene into the hideout.
    public static void ExitToHideout()
    {
        MainMenu.SkipOnNextLoad = true;
        enterHideoutOnLoad = true;
        Load(SceneManager.GetActiveScene().name);
    }

    // True once if the scene was loaded for the hideout.
    public static bool ConsumeHideoutRequest()
    {
        bool requested = enterHideoutOnLoad;
        enterHideoutOnLoad = false;
        return requested;
    }

    private static void Load(string sceneName)
    {
        // Leaving a run loses whatever materials weren't banked (victory banks them first);
        // leaving the hideout without deploying refunds anything bought from the trader.
        RunMaterials.Lose();
        TraderStock.RefundUnsettled();

        // Time scale and audio pause survive scene loads, so reset them before leaving.
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(sceneName);
    }
}
