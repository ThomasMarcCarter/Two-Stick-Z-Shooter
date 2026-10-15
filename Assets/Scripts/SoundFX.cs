using System;
using System.Collections.Generic;
using UnityEngine;

// One-shot sound effects: 2D (heard the same everywhere) or positional (heard from a point,
// relative to the listener on the player). Each plays on its own short-lived AudioSource, so
// pitch can vary per sound without affecting others. Generated clips are made once and shared.
public static class SoundFX
{
    // Positional sounds: full volume within this distance of the player, silent at the far one.
    public const float NearDistance = 3f;
    public const float FarDistance = 24f;

    private static readonly Dictionary<string, AudioClip[]> cache = new Dictionary<string, AudioClip[]>();
    private static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

    // Statics outlive Play sessions with domain reload off, and the clips from the last session
    // may be gone, so start each session clean.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        cache.Clear();
        lastPlayed.Clear();
    }

    // A set of generated variations, made the first time they're asked for.
    public static AudioClip[] Clips(string key, int variations, Func<int, AudioClip> make)
    {
        if (cache.TryGetValue(key, out AudioClip[] clips) && clips.Length > 0 && clips[0] != null)
            return clips;

        clips = new AudioClip[Mathf.Max(1, variations)];

        for (int i = 0; i < clips.Length; i++)
            clips[i] = make(i);

        cache[key] = clips;
        return clips;
    }

    public static AudioClip Pick(AudioClip[] clips)
    {
        return clips == null || clips.Length == 0 ? null : clips[UnityEngine.Random.Range(0, clips.Length)];
    }

    // True if a sound with this key played less than `interval` seconds ago. Stops a shotgun
    // blast's pellets all landing on the same frame from stacking into one very loud hit.
    public static bool Throttled(string key, float interval)
    {
        float now = Time.unscaledTime;

        if (lastPlayed.TryGetValue(key, out float last) && now - last < interval)
            return true;

        lastPlayed[key] = now;
        return false;
    }

    public static void Play2D(AudioClip clip, float volume, float pitch = 1f)
    {
        Play(clip, Vector3.zero, volume, pitch, false);
    }

    public static void PlayAt(AudioClip clip, Vector3 position, float volume, float pitch = 1f)
    {
        Play(clip, position, volume, pitch, true);
    }

    private static void Play(AudioClip clip, Vector3 position, float volume, float pitch, bool positional)
    {
        if (clip == null)
            return;

        GameObject go = new GameObject("SFX " + clip.name);
        go.transform.position = position;

        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.spatialBlend = positional ? 1f : 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = NearDistance;
        source.maxDistance = FarDistance;
        source.dopplerLevel = 0f;
        source.Play();

        // On game time, so a sound playing when the game pauses is cleaned up after it resumes.
        UnityEngine.Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
    }
}
