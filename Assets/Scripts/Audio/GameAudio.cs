using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

// The game's audio mixer (Assets/Resources/GameAudio.mixer): Master with an Effects and a Music
// group under it. Every AudioSource is routed into one of the two, and the settings screen's
// master / effects / music volumes are the three groups' faders (exposed as MasterVolume,
// EffectsVolume and MusicVolume). Sources made in code call Route; ones in prefabs or the scene
// get the Effects group when a scene loads if they have none (the gun prefabs have it set).
// Without the mixer asset, sound still plays and the listener volume stands in for master x
// effects.
public static class GameAudio
{
    private const string MixerPath = "GameAudio";
    private const float SilentDb = -80f;

    private static AudioMixer mixer;
    private static AudioMixerGroup effects;
    private static AudioMixerGroup music;
    private static bool loaded;
    private static Vector3 volumes = Vector3.one;    // master, effects, music

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        mixer = null;
        effects = null;
        music = null;
        loaded = false;
        volumes = Vector3.one;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static AudioMixerGroup Effects
    {
        get
        {
            Load();
            return effects;
        }
    }

    public static AudioMixerGroup Music
    {
        get
        {
            Load();
            return music;
        }
    }

    // Sends a source made in code through the mixer: the music group, or effects otherwise.
    public static AudioSource Route(AudioSource source, bool isMusic = false)
    {
        if (source != null)
            source.outputAudioMixerGroup = isMusic ? Music : Effects;

        return source;
    }

    // Sets the three faders from the settings (0..1 each, as on the settings screen).
    public static void SetVolumes(float master, float effectsVolume, float musicVolume)
    {
        volumes = new Vector3(master, effectsVolume, musicVolume);
        Reapply();
    }

    // Sets the faders to the last volumes again. The mixer ignores values set before its first
    // frame (settings load before the scene does), so MusicPlayer calls this from Start.
    public static void Reapply()
    {
        Load();

        if (mixer == null)
        {
            AudioListener.volume = volumes.x * volumes.y;
            return;
        }

        AudioListener.volume = 1f;
        mixer.SetFloat("MasterVolume", ToDecibels(volumes.x));
        mixer.SetFloat("EffectsVolume", ToDecibels(volumes.y));
        mixer.SetFloat("MusicVolume", ToDecibels(volumes.z));
    }

    // Sources placed in the scene that weren't given a group go to effects.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Effects == null)
            return;

        foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
        {
            if (source.outputAudioMixerGroup == null)
                source.outputAudioMixerGroup = effects;
        }
    }

    private static void Load()
    {
        if (loaded)
            return;

        loaded = true;
        mixer = Resources.Load<AudioMixer>(MixerPath);

        if (mixer == null)
        {
            Debug.LogWarning("GameAudio: no mixer at Resources/" + MixerPath + "; volumes use the listener instead.");
            return;
        }

        effects = Group("Effects");
        music = Group("Music");
    }

    private static AudioMixerGroup Group(string name)
    {
        foreach (AudioMixerGroup group in mixer.FindMatchingGroups(name))
        {
            if (group.name == name)
                return group;
        }

        return null;
    }

    // Fader position for a 0..1 slider: logarithmic, so each step sounds even; 0 is silent.
    private static float ToDecibels(float volume)
    {
        return volume <= 0.0001f ? SilentDb : Mathf.Max(SilentDb, 20f * Mathf.Log10(volume));
    }
}
