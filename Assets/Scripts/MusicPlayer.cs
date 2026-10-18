using System.Threading.Tasks;
using UnityEngine;

// Background music: the two ProceduralMusic layers looping together. The ambient bed always
// plays; the tension layer fades in while the player is in a locked combat room (fully for the
// boss) and out once it's cleared or a menu/end screen is up. Created in code when the game
// starts and kept across scene reloads, so a new run doesn't restart the track. Its sources
// ignore the listener's volume and use GameSettings.MusicLevel (master x music), so the
// effects volume doesn't touch it. The clips are built on a worker thread to avoid a hitch.
public class MusicPlayer : MonoBehaviour
{
    private const float AmbientLevel = 0.55f;
    private const float FadeSpeed = 0.35f;     // volume per second

    private static MusicPlayer instance;

    private AudioSource ambient;
    private AudioSource tension;
    private Task<float[]> ambientData;
    private Task<float[]> tensionData;
    private float tensionMix;
    private FloorGenerator floor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("Music");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MusicPlayer>();
    }

    private void Awake()
    {
        ambient = MakeSource("Ambient");
        tension = MakeSource("Tension");

        int seed = Random.Range(0, int.MaxValue);
        ambientData = Task.Run(() => ProceduralMusic.Ambient(seed));
        tensionData = Task.Run(() => ProceduralMusic.Tension(seed + 1));
    }

    private AudioSource MakeSource(string label)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerVolume = true;   // music follows its own volume setting
        source.ignoreListenerPause = true;    // keeps playing (quietly) behind the pause menu
        source.volume = 0f;
        source.name = label;
        return source;
    }

    private void Update()
    {
        StartWhenReady(ambient, ref ambientData, "Ambient Music");
        StartWhenReady(tension, ref tensionData, "Tension Music");

        // Tension: up in a fight, all the way for the boss; down in menus and on end screens.
        float target = 0f;

        if (!MainMenu.IsOpen && !VictoryScreen.IsShowing && !DeathScreen.IsShowing)
        {
            if (floor == null)
                floor = FindAnyObjectByType<FloorGenerator>();

            Room room = floor != null ? floor.CurrentRoom : null;

            if (room != null && room.IsLocked)
                target = room.Type == RoomType.Boss ? 1f : 0.75f;
        }

        tensionMix = Mathf.MoveTowards(tensionMix, target, FadeSpeed * Time.unscaledDeltaTime);

        float paused = Time.timeScale == 0f && !MainMenu.IsOpen ? 0.5f : 1f;
        float level = GameSettings.MusicLevel * paused;
        ambient.volume = level * AmbientLevel * (1f - tensionMix * 0.35f);
        tension.volume = level * tensionMix;

        // Start the tension layer in step with the ambient one, so the two stay in time.
        if (tension.clip != null && ambient.isPlaying && !tension.isPlaying)
        {
            tension.timeSamples = ambient.timeSamples % tension.clip.samples;
            tension.Play();
        }
    }

    private static void StartWhenReady(AudioSource source, ref Task<float[]> data, string clipName)
    {
        if (data == null || !data.IsCompleted)
            return;

        if (data.Status == TaskStatus.RanToCompletion)
        {
            float[] samples = data.Result;
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, ProceduralMusic.SampleRate, false);
            clip.SetData(samples, 0);
            source.clip = clip;

            if (clipName == "Ambient Music")
                source.Play();
        }
        else
        {
            Debug.LogWarning($"MusicPlayer: couldn't build {clipName}: {data.Exception?.GetBaseException().Message}");
        }

        data = null;
    }
}
