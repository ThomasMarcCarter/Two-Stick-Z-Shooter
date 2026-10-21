using System.Collections.Generic;
using UnityEngine;

// The one scream used by everything that shrieks: the screamer zombie, the Butcher (pitched far
// down) and the Howler (a warbled copy). The clip is the FloorGenerator's Scream Sound; without
// one it falls back to the generated scream. Every scream gets a small random pitch change. The
// warbled copy is built once from the clip's samples: a fast vibrato (the read speed swings up
// and down) plus a matching tremolo, so the howl wavers like a voice breaking.
public static class ScreamSound
{
    private const float PitchVariation = 0.06f;
    private const float WarbleRate = 7f;        // wobbles per second
    private const float WarbleDepth = 0.14f;    // how far the read speed swings either way
    private const float TremoloDepth = 0.35f;   // how far the volume dips on each wobble

    private static AudioClip clip;
    private static AudioClip warbled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        clip = null;
        warbled = null;
    }

    // Set by the FloorGenerator.
    public static void Use(AudioClip scream)
    {
        if (scream != clip)
            warbled = null;

        clip = scream;
    }

    // A scream at `position`; `pitch` is the base pitch before the random change.
    public static void PlayAt(Vector3 position, float volume, float pitch = 1f)
    {
        SoundFX.PlayAt(Clip(), position, volume, Vary(pitch));
    }

    // The Howler's warbled scream.
    public static void PlayWarbledAt(Vector3 position, float volume, float pitch = 1f)
    {
        if (warbled == null)
            warbled = Warble(Clip());

        SoundFX.PlayAt(warbled, position, volume, Vary(pitch));
    }

    private static AudioClip Clip()
    {
        return clip != null ? clip : SoundFX.Pick(SoundFX.Clips("screamer", 3, ProceduralAudio.Scream));
    }

    private static float Vary(float pitch)
    {
        return pitch * Random.Range(1f - PitchVariation, 1f + PitchVariation);
    }

    private static AudioClip Warble(AudioClip source)
    {
        int channels = source.channels;
        float[] data = new float[source.samples * channels];

        // Streamed or compressed-in-memory clips can't be read; play those unwarbled.
        if (!source.GetData(data, 0))
            return source;

        List<float> output = new List<float>(data.Length * 11 / 10);
        double position = 0.0;
        float step = 2f * Mathf.PI * WarbleRate / source.frequency;
        float phase = 0f;
        int last = source.samples - 1;

        while (position < last)
        {
            int index = (int)position;
            float t = (float)(position - index);
            float wobble = Mathf.Sin(phase);
            float gain = 1f - TremoloDepth * 0.5f * (1f + wobble);

            for (int c = 0; c < channels; c++)
                output.Add(Mathf.Lerp(data[index * channels + c], data[(index + 1) * channels + c], t) * gain);

            position += 1.0 + WarbleDepth * wobble;
            phase += step;
        }

        AudioClip result = AudioClip.Create(source.name + " Warbled", output.Count / channels, channels, source.frequency, false);
        result.SetData(output.ToArray(), 0);
        return result;
    }
}
