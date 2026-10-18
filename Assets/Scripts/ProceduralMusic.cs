using System;

// Placeholder music made in code (like ProceduralAudio's sound effects), as raw samples so it
// can be built off the main thread; MusicPlayer turns them into clips. Two 32-second layers that
// loop without a seam: an ambient bed (a slow D-minor pad progression over a drone, wind and the
// odd distant bell) and a tension layer at 105 BPM (thumping kick, pulsing bass, ticking hats and
// a dissonant tremolo swell) that fades in over it during fights.
public static class ProceduralMusic
{
    public const int SampleRate = 22050;
    public const float LoopSeconds = 32f;      // 56 beats at 105 BPM, 4 chords of 8 s
    private const float FadeSeconds = 1.5f;    // tail rendered past the loop and blended into the start
    private const double TwoPi = Math.PI * 2.0;

    public static float[] Ambient(int seed)
    {
        Random rng = new Random(seed);
        int loop = (int)(SampleRate * LoopSeconds);
        float[] data = new float[loop + (int)(SampleRate * FadeSeconds)];

        // Root (low) + three chord tones, one chord every 8 s: Dm, Bb, Gm, A.
        float[][] chords =
        {
            new[] { 36.71f, 146.83f, 174.61f, 220.00f },
            new[] { 29.14f, 116.54f, 146.83f, 174.61f },
            new[] { 49.00f, 116.54f, 146.83f, 196.00f },
            new[] { 27.50f, 110.00f, 138.59f, 164.81f },
        };
        const float chordLength = 8f;
        const float rise = 2.5f;

        for (int c = 0; c < chords.Length + 1; c++)   // +1: the first chord again, under the tail
        {
            float[] chord = chords[c % chords.Length];
            float start = c * chordLength;

            for (int n = 0; n < chord.Length; n++)
            {
                bool root = n == 0;
                float freq = chord[n];
                float level = root ? 0.32f : 0.11f;
                double phaseA = rng.NextDouble() * TwoPi;
                double phaseB = rng.NextDouble() * TwoPi;
                int from = Math.Max(0, (int)((start - rise) * SampleRate));
                int to = Math.Min(data.Length, (int)((start + chordLength + rise) * SampleRate));

                for (int i = from; i < to; i++)
                {
                    float t = i / (float)SampleRate;
                    float local = t - (start - rise);
                    float env = Smooth(local / (rise * 2f)) * (1f - Smooth((local - chordLength) / (rise * 2f)));
                    double w = TwoPi * freq * t;

                    // Two slightly detuned voices with a couple of soft harmonics; the root is a
                    // plain sine an octave down for weight.
                    double voice = root
                        ? Math.Sin(w + phaseA) + 0.25 * Math.Sin(2.0 * w + phaseB)
                        : Math.Sin(w + phaseA) + Math.Sin(TwoPi * (freq + 0.35) * t + phaseB)
                          + 0.3 * Math.Sin(2.0 * w) + 0.12 * Math.Sin(3.0 * w);
                    data[i] += (float)(voice * env * level);
                }
            }
        }

        // Breath: the whole pad swells gently, two cycles a loop.
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            data[i] *= 0.8f + 0.2f * (float)Math.Sin(TwoPi * 2.0 / LoopSeconds * t);
        }

        // Wind: low-passed noise whose brightness drifts.
        float low = 0f;
        float lower = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            float cutoff = 0.02f + 0.015f * (float)Math.Sin(TwoPi * 3.0 / LoopSeconds * t);
            float noise = (float)rng.NextDouble() * 2f - 1f;
            low += (noise - low) * cutoff;
            lower += (low - lower) * cutoff;
            data[i] += lower * 0.9f * (0.6f + 0.4f * (float)Math.Sin(TwoPi * 1.0 / LoopSeconds * t + 1.3));
        }

        // Distant bells: a few struck notes from the D-minor pentatonic, with a clangy partial.
        float[] bells = { 587.33f, 698.46f, 880.00f, 1046.50f, 1174.66f };
        float when = 2f + (float)rng.NextDouble() * 3f;

        while (when < LoopSeconds - 1f)
        {
            float freq = bells[rng.Next(bells.Length)];
            int start = (int)(when * SampleRate);
            int length = Math.Min(data.Length - start, SampleRate * 4);

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float env = (float)Math.Exp(-t * 1.4) * Math.Min(1f, t * 200f);
                data[start + i] += env * 0.05f * (float)(Math.Sin(TwoPi * freq * t) + 0.4 * Math.Sin(TwoPi * freq * 2.76 * t));
            }

            when += 5f + (float)rng.NextDouble() * 5f;
        }

        return Finish(data, loop, 0.7f);
    }

    public static float[] Tension(int seed)
    {
        Random rng = new Random(seed);
        int loop = (int)(SampleRate * LoopSeconds);
        float[] data = new float[loop + (int)(SampleRate * FadeSeconds)];
        const double bpm = 105.0;
        double beat = 60.0 / bpm;
        int beats = (int)Math.Round(LoopSeconds / beat) + 4;

        // Kick on every beat, harder on the first of the bar.
        for (int b = 0; b < beats; b++)
        {
            int start = (int)(b * beat * SampleRate);
            float accent = b % 4 == 0 ? 1f : 0.65f;
            double phase = 0.0;

            for (int i = 0; i < SampleRate / 3 && start + i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                double freq = 45.0 + 70.0 * Math.Exp(-t * 30.0);
                phase += TwoPi * freq / SampleRate;
                data[start + i] += (float)(Math.Sin(phase) * Math.Exp(-t * 14.0)) * 0.55f * accent;
            }
        }

        // Bass ostinato in eighths, moving D-D-Bb-C a bar at a time.
        float[] roots = { 73.42f, 73.42f, 58.27f, 65.41f };

        for (int e = 0; e < beats * 2; e++)
        {
            int start = (int)(e * beat * 0.5 * SampleRate);
            float freq = roots[(e / 8) % roots.Length] * (e % 8 == 6 ? 2f : 1f);
            int length = (int)(beat * 0.5 * SampleRate);

            for (int i = 0; i < length && start + i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                double w = TwoPi * freq * t;
                double square = Math.Sin(w) + Math.Sin(3 * w) / 3.0 + Math.Sin(5 * w) / 5.0;
                data[start + i] += (float)(square * Math.Exp(-t * 7.0)) * 0.16f;
            }
        }

        // Hats on the off-beats: high-passed noise ticks.
        for (int e = 1; e < beats * 2; e += 2)
        {
            int start = (int)(e * beat * 0.5 * SampleRate);
            float low = 0f;

            for (int i = 0; i < SampleRate / 12 && start + i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                low += (noise - low) * 0.3f;
                data[start + i] += (noise - low) * (float)Math.Exp(-t * 45.0) * 0.12f;
            }
        }

        // Strings: a minor second shivering at 8 Hz, swelling twice a loop.
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            double swell = 0.5 - 0.5 * Math.Cos(TwoPi * 2.0 / LoopSeconds * t);
            double tremolo = 0.6 + 0.4 * Math.Sin(TwoPi * 8.0 * t);
            double tone = Math.Sin(TwoPi * 440.0 * t) + Math.Sin(TwoPi * 466.16 * t) + 0.5 * Math.Sin(TwoPi * 220.0 * t);
            data[i] += (float)(tone * swell * swell * tremolo) * 0.05f;
        }

        return Finish(data, loop, 0.8f);
    }

    // Blends the tail past the loop point into the start, so the clip repeats without a click,
    // and scales it to the given peak.
    private static float[] Finish(float[] data, int loop, float peak)
    {
        float[] result = new float[loop];
        int fade = data.Length - loop;

        for (int i = 0; i < loop; i++)
        {
            float sample = data[i];

            if (i < fade)
            {
                float k = i / (float)fade;
                sample = data[i] * k + data[loop + i] * (1f - k);
            }

            result[i] = sample;
        }

        float max = 0.0001f;

        foreach (float s in result)
            max = Math.Max(max, Math.Abs(s));

        float scale = peak / max;

        for (int i = 0; i < loop; i++)
            result[i] *= scale;

        return result;
    }

    private static float Smooth(float x)
    {
        x = Math.Max(0f, Math.Min(1f, x));
        return x * x * (3f - 2f * x);
    }
}
