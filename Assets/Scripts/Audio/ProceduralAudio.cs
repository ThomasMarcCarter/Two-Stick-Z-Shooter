using UnityEngine;

// Sounds made in code, as placeholders until real recordings are added. The clips are in
// partials by kind (ProceduralAudio.Items.cs, .Combat.cs, .World.cs); this file holds the
// shared helpers they're built with.
public static partial class ProceduralAudio
{
    private static float Noise(System.Random rng)
    {
        return (float)rng.NextDouble() * 2f - 1f;
    }

    private static float Tanh(float x)
    {
        float e = Mathf.Exp(2f * x);
        return (e - 1f) / (e + 1f);
    }

    // Normalises so the loudest moment sits just under full volume, and makes the clip.
    private static AudioClip Build(string name, float[] data, int sampleRate)
    {
        float peak = 0.0001f;

        foreach (float sample in data)
            peak = Mathf.Max(peak, Mathf.Abs(sample));

        for (int i = 0; i < data.Length; i++)
            data[i] *= 0.8f / peak;

        AudioClip clip = AudioClip.Create(name, data.Length, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
