using UnityEngine;

// Background music: three looping tracks set on the FloorGenerator's Music fields, crossfading
// into one another. The hideout track plays on the main menu and in the hideout; the background
// track on the floors (and on the death/victory screens); the boss track while a boss fight is on
// (the player is in a locked boss room). A track that fades out pauses and picks up where it left
// off when it comes back, except the boss track, which starts from the top for each fight. Created
// in code when the game starts and kept across scene reloads, so a new run doesn't restart the
// music. Its sources play through the mixer's Music group, so the music volume is that group's
// fader (without the mixer they set GameSettings.MusicLevel themselves).
public class MusicPlayer : MonoBehaviour
{
    private const float FadeSpeed = 0.5f;     // mix per second

    private class Track
    {
        public AudioSource Source;
        public float Level;                   // volume at full mix, before the music setting
        public bool Restart;                  // start from the top each time it comes in
        public float Mix;
    }

    private static MusicPlayer instance;

    private Track hideout;
    private Track background;
    private Track boss;
    private Track[] tracks;
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
        hideout = MakeTrack("Hideout", 0.275f, false);
        background = MakeTrack("Background", 0.275f, false);
        boss = MakeTrack("Boss", 0.4f, true);
        tracks = new[] { hideout, background, boss };
    }

    private void Start()
    {
        GameAudio.Reapply();
    }

    private Track MakeTrack(string label, float level, bool restart)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerVolume = true;   // music follows its own volume setting
        source.ignoreListenerPause = true;    // keeps playing (quietly) behind the pause menu
        source.volume = 0f;
        source.name = label;
        return new Track { Source = GameAudio.Route(source, true), Level = level, Restart = restart };
    }

    private void Update()
    {
        if (floor == null)
            floor = FindAnyObjectByType<FloorGenerator>();

        if (floor != null)
        {
            SetClip(hideout, floor.HideoutMusic);
            SetClip(background, floor.BackgroundMusic);
            SetClip(boss, floor.BossMusic);
        }

        Track wanted = Wanted();
        float paused = Time.timeScale == 0f && !MainMenu.IsOpen ? 0.5f : 1f;
        float level = (GameAudio.Music != null ? 1f : GameSettings.MusicLevel) * paused;

        foreach (Track track in tracks)
        {
            AudioSource source = track.Source;
            float target = track == wanted ? 1f : 0f;

            if (target > 0f && !source.isPlaying)
            {
                if (track.Restart || source.time <= 0f)
                {
                    source.time = 0f;
                    source.Play();
                }
                else
                {
                    source.UnPause();
                }
            }

            track.Mix = Mathf.MoveTowards(track.Mix, target, FadeSpeed * Time.unscaledDeltaTime);

            if (track.Mix <= 0f && source.isPlaying)
            {
                if (track.Restart)
                    source.Stop();
                else
                    source.Pause();
            }

            source.volume = level * track.Level * track.Mix;
        }
    }

    // Which track should be playing now (null if it has no clip).
    private Track Wanted()
    {
        Track wanted = background;

        if (MainMenu.IsOpen || (floor != null && floor.IsHideout))
        {
            wanted = hideout;
        }
        else if (floor != null && !VictoryScreen.IsShowing && !DeathScreen.IsShowing)
        {
            Room room = floor.CurrentRoom;

            if (room != null && room.IsLocked && room.Type == RoomType.Boss)
                wanted = boss;
        }

        return wanted.Source.clip != null ? wanted : background;
    }

    private static void SetClip(Track track, AudioClip clip)
    {
        if (track.Source.clip == null)
            track.Source.clip = clip;
    }
}
