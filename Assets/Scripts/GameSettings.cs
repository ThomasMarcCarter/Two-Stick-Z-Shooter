using System;
using System.Collections.Generic;
using UnityEngine;

// Audio and video options, saved to PlayerPrefs and applied when the game starts and whenever
// one changes (from the settings screen's AUDIO / VIDEO page). Master x Effects sets the audio
// listener's volume, which every sound effect goes through; the music ignores the listener and
// uses Master x Music instead (MusicPlayer), so the two can be set apart without a mixer.
// Display mode and resolution only apply in a build; the editor's Game view ignores them.
public static class GameSettings
{
    public enum CrtLevel { Off, Light, Full }

    private const string Prefix = "settings.";

    public static float MasterVolume { get; private set; }
    public static float EffectsVolume { get; private set; }
    public static float MusicVolume { get; private set; }
    public static FullScreenMode DisplayMode { get; private set; }
    public static Vector2Int Resolution { get; private set; }
    public static bool VSync { get; private set; }
    public static int FrameCap { get; private set; }      // 0 = unlimited
    public static CrtLevel Crt { get; private set; }

    // Music volume after the master volume.
    public static float MusicLevel => MasterVolume * MusicVolume;

    public static event Action Changed;

    public static readonly int[] FrameCaps = { 30, 60, 120, 144, 0 };
    public static readonly FullScreenMode[] DisplayModes =
        { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };

    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        loaded = false;
        Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadOnStart()
    {
        Load();
        Apply();
    }

    public static void Load()
    {
        MasterVolume = PlayerPrefs.GetFloat(Prefix + "master", 1f);
        EffectsVolume = PlayerPrefs.GetFloat(Prefix + "effects", 1f);
        MusicVolume = PlayerPrefs.GetFloat(Prefix + "music", 0.6f);
        DisplayMode = (FullScreenMode)PlayerPrefs.GetInt(Prefix + "displayMode", (int)FullScreenMode.FullScreenWindow);
        Resolution = new Vector2Int(PlayerPrefs.GetInt(Prefix + "width", 0), PlayerPrefs.GetInt(Prefix + "height", 0));
        VSync = PlayerPrefs.GetInt(Prefix + "vsync", 1) == 1;
        FrameCap = PlayerPrefs.GetInt(Prefix + "frameCap", 0);
        Crt = (CrtLevel)PlayerPrefs.GetInt(Prefix + "crt", (int)CrtLevel.Full);

        if (Resolution.x <= 0 || Resolution.y <= 0)
            Resolution = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);

        loaded = true;
    }

    private static void Save()
    {
        PlayerPrefs.SetFloat(Prefix + "master", MasterVolume);
        PlayerPrefs.SetFloat(Prefix + "effects", EffectsVolume);
        PlayerPrefs.SetFloat(Prefix + "music", MusicVolume);
        PlayerPrefs.SetInt(Prefix + "displayMode", (int)DisplayMode);
        PlayerPrefs.SetInt(Prefix + "width", Resolution.x);
        PlayerPrefs.SetInt(Prefix + "height", Resolution.y);
        PlayerPrefs.SetInt(Prefix + "vsync", VSync ? 1 : 0);
        PlayerPrefs.SetInt(Prefix + "frameCap", FrameCap);
        PlayerPrefs.SetInt(Prefix + "crt", (int)Crt);
        PlayerPrefs.Save();
    }

    public static void Apply()
    {
        if (!loaded)
            Load();

        AudioListener.volume = MasterVolume * EffectsVolume;
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = VSync || FrameCap <= 0 ? -1 : FrameCap;

        if (!Application.isEditor)
            Screen.SetResolution(Resolution.x, Resolution.y, DisplayMode);

        CRTEffect.SetLevel(Crt);
        Changed?.Invoke();
    }

    // ---------- Changing values (each saves and applies) ----------

    public static void StepMaster(int direction) => SetVolume(direction, v => MasterVolume = v, MasterVolume);
    public static void StepEffects(int direction) => SetVolume(direction, v => EffectsVolume = v, EffectsVolume);
    public static void StepMusic(int direction) => SetVolume(direction, v => MusicVolume = v, MusicVolume);

    private static void SetVolume(int direction, Action<float> set, float current)
    {
        // Steps of 10%, wrapping from 100% back to 0% (and the other way).
        int step = Mathf.RoundToInt(current * 10f) + direction;
        step = (step % 11 + 11) % 11;
        set(step / 10f);
        Commit();
    }

    public static void StepDisplayMode(int direction)
    {
        int index = Array.IndexOf(DisplayModes, DisplayMode);
        DisplayMode = DisplayModes[Wrap(index + direction, DisplayModes.Length)];
        Commit();
    }

    public static void StepResolution(int direction)
    {
        List<Vector2Int> options = Resolutions();
        int index = options.IndexOf(Resolution);

        if (index < 0)
            index = options.Count - 1;

        Resolution = options[Wrap(index + direction, options.Count)];
        Commit();
    }

    public static void ToggleVSync()
    {
        VSync = !VSync;
        Commit();
    }

    public static void StepFrameCap(int direction)
    {
        int index = Array.IndexOf(FrameCaps, FrameCap);
        FrameCap = FrameCaps[Wrap(index + direction, FrameCaps.Length)];
        Commit();
    }

    public static void StepCrt(int direction)
    {
        Crt = (CrtLevel)Wrap((int)Crt + direction, 3);
        Commit();
    }

    public static void ResetToDefaults()
    {
        foreach (string key in new[] { "master", "effects", "music", "displayMode", "width", "height", "vsync", "frameCap", "crt" })
            PlayerPrefs.DeleteKey(Prefix + key);

        Load();
        Save();
        Apply();
    }

    private static void Commit()
    {
        Save();
        Apply();
    }

    private static int Wrap(int value, int count)
    {
        return (value % count + count) % count;
    }

    // The screen sizes the display offers, each once, smallest first; at least the current one.
    public static List<Vector2Int> Resolutions()
    {
        List<Vector2Int> sizes = new List<Vector2Int>();

        foreach (UnityEngine.Resolution r in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(r.width, r.height);

            if (!sizes.Contains(size) && size.x >= 800)
                sizes.Add(size);
        }

        if (!sizes.Contains(Resolution))
            sizes.Add(Resolution);

        sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return sizes;
    }

    // ---------- Labels for the settings screen ----------

    public static string Percent(float volume) => Mathf.RoundToInt(volume * 100f) + "%";

    public static string DisplayModeName(FullScreenMode mode)
    {
        switch (mode)
        {
            case FullScreenMode.ExclusiveFullScreen: return "FULLSCREEN";
            case FullScreenMode.Windowed: return "WINDOWED";
            default: return "BORDERLESS";
        }
    }

    public static string FrameCapName => VSync ? "VSYNC" : FrameCap <= 0 ? "UNLIMITED" : FrameCap + " FPS";
}
