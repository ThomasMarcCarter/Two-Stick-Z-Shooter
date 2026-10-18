using UnityEngine;

// One place to ask whether gameplay input should be ignored right now: the main menu is up, the
// game is paused, the map is open, a floor transition is playing, the victory or death screen
// is up, or the hideout's upgrade terminal is open.
public static class GameFlow
{
    // Domain reload is off for entering Play mode, so time scale and audio pause can carry over
    // from a session that was stopped while paused.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void StartSession()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    public static bool InputBlocked =>
        MainMenu.IsOpen || PauseMenu.IsOpen || FullscreenMap.IsOpen || FloorTransition.IsActive
        || VictoryScreen.IsShowing || DeathScreen.IsShowing || DebugMenu.IsOpen || InventoryScreen.IsOpen
        || HideoutTerminal.IsOpen;

    // Like InputBlocked, but the inventory doesn't stop the player walking: the game keeps
    // running while it's open, so they can still get out of the way (just not shoot or sprint).
    public static bool MovementBlocked =>
        MainMenu.IsOpen || PauseMenu.IsOpen || FullscreenMap.IsOpen || FloorTransition.IsActive
        || VictoryScreen.IsShowing || DeathScreen.IsShowing || DebugMenu.IsOpen || HideoutTerminal.IsOpen;

    // A menu or screen is up that doesn't use a cursor on a controller.
    public static bool InMenu =>
        MainMenu.IsOpen || PauseMenu.IsOpen || VictoryScreen.IsShowing || DeathScreen.IsShowing || DebugMenu.IsOpen
        || InventoryScreen.IsOpen || HideoutTerminal.IsOpen;

    // The crosshair is the cursor everywhere except menus on a controller, where the highlight
    // does that job. The inventory is dragged about with a cursor on a controller too.
    public static bool ShowCrosshair => !(GameInput.UsingGamepad && InMenu) || OnlyInventoryOpen;

    private static bool OnlyInventoryOpen =>
        InventoryScreen.IsOpen && !(MainMenu.IsOpen || PauseMenu.IsOpen || VictoryScreen.IsShowing || DeathScreen.IsShowing || DebugMenu.IsOpen
        || HideoutTerminal.IsOpen);
}
