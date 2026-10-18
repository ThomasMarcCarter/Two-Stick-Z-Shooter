using UnityEngine;

// What the platform the game is running on allows, so PC-only features stay out of the way on
// consoles (planned): quitting to the desktop (console stores forbid a quit button) and window
// settings (display mode, resolution and a frame-rate cap only make sense with a desktop window).
// The editor counts as desktop.
public static class Platform
{
    public static bool IsDesktop
    {
        get
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return true;
                default:
                    return false;
            }
        }
    }

    // A "quit to desktop" option is allowed.
    public static bool CanQuit => IsDesktop;

    // Display mode, resolution and frame-rate cap can be changed.
    public static bool HasWindowSettings => IsDesktop;
}
