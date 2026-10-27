using UnityEngine;

// Generated placeholder sounds for items and the player's hands: the backpack, pickups,
// fitting attachments, medkits, adrenaline and throws.
public static partial class ProceduralAudio
{
    // A backpack being opened: a soft thump of weight shifting, then a handful of fabric
    // rustles and zip-like scrapes made from filtered noise bursts.
    public static AudioClip BackpackRustle(int seed)
    {
        const int sampleRate = 44100;
        const float length = 0.6f;
        int samples = Mathf.CeilToInt(sampleRate * length);
        float[] data = new float[samples];
        System.Random rng = new System.Random(seed);

        // Thump: a low, quickly decaying tone.
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            data[i] += Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 40f) * 0.5f;
        }

        // Rustles: short noise grains, band-passed so they sound like cloth rather than hiss.
        int grains = 8;

        for (int g = 0; g < grains; g++)
        {
            int start = (int)(sampleRate * (0.02f + (float)rng.NextDouble() * 0.42f));
            int duration = (int)(sampleRate * (0.03f + (float)rng.NextDouble() * 0.09f));
            float amplitude = 0.25f + (float)rng.NextDouble() * 0.45f;
            float brightness = 0.15f + (float)rng.NextDouble() * 0.25f;   // low-pass amount: higher = brighter

            float low = 0f;
            float lower = 0f;

            for (int i = 0; i < duration && start + i < samples; i++)
            {
                float noise = (float)rng.NextDouble() * 2f - 1f;

                // Two one-pole low-passes; their difference is a rough band-pass.
                low += (noise - low) * brightness;
                lower += (low - lower) * 0.05f;
                float band = low - lower;

                float progress = i / (float)duration;
                float envelope = Mathf.Min(1f, progress * 12f) * Mathf.Pow(1f - progress, 2f);

                data[start + i] += band * amplitude * envelope * 2.5f;
            }
        }

        // Normalise so the loudest moment sits just under full volume.
        float peak = 0.0001f;

        foreach (float sample in data)
            peak = Mathf.Max(peak, Mathf.Abs(sample));

        for (int i = 0; i < samples; i++)
            data[i] *= 0.8f / peak;

        AudioClip clip = AudioClip.Create("Backpack Rustle", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Picking up ammo: a few brass cartridges clinking together (short, inharmonic metallic
    // pings with a tick of noise on each), then a soft thud as they go into the pack.
    public static AudioClip AmmoPickup(int seed)
    {
        const int sampleRate = 44100;
        const float length = 0.35f;
        int samples = Mathf.CeilToInt(sampleRate * length);
        float[] data = new float[samples];
        System.Random rng = new System.Random(seed);

        // Partials of a small brass case, as ratios of the base pitch. Not whole-number
        // multiples, which is what makes it sound like metal rather than a tone.
        float[] ratios = { 1f, 1.59f, 2.14f, 2.83f };
        int clinks = 3 + rng.Next(2);

        for (int c = 0; c < clinks; c++)
        {
            int start = (int)(sampleRate * (c * 0.035f + (float)rng.NextDouble() * 0.025f));
            float pitch = 2300f + (float)rng.NextDouble() * 1400f;
            float amplitude = (0.5f + (float)rng.NextDouble() * 0.5f) * (c == 0 ? 1f : 0.7f);
            float decay = 45f + (float)rng.NextDouble() * 30f;

            for (int i = start; i < samples; i++)
            {
                float t = (i - start) / (float)sampleRate;
                float envelope = Mathf.Exp(-t * decay);

                if (envelope < 0.001f)
                    break;

                float ring = 0f;

                for (int r = 0; r < ratios.Length; r++)
                    ring += Mathf.Sin(2f * Mathf.PI * pitch * ratios[r] * t) / (r + 1f);

                // The tick of the cases touching, gone within a couple of milliseconds.
                float tick = t < 0.003f ? ((float)rng.NextDouble() * 2f - 1f) * (1f - t / 0.003f) : 0f;

                data[i] += (ring * 0.5f + tick) * amplitude * envelope;
            }
        }

        // Thud: rounds landing in the pack, after the clinks.
        int thudStart = (int)(sampleRate * (clinks * 0.035f + 0.02f));
        float low = 0f;

        for (int i = thudStart; i < samples; i++)
        {
            float t = (i - thudStart) / (float)sampleRate;
            float envelope = Mathf.Min(1f, t * 400f) * Mathf.Exp(-t * 30f);
            low += (((float)rng.NextDouble() * 2f - 1f) - low) * 0.08f;
            data[i] += (Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.6f + low * 1.5f) * envelope * 0.6f;
        }

        // Normalise so the loudest moment sits just under full volume.
        float peak = 0.0001f;

        foreach (float sample in data)
            peak = Mathf.Max(peak, Mathf.Abs(sample));

        for (int i = 0; i < samples; i++)
            data[i] *= 0.8f / peak;

        AudioClip clip = AudioClip.Create("Ammo Pickup", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Medkit used: a soft hiss of a seal opening, then two rising blips.
    public static AudioClip MedkitPickup()
    {
        const int sampleRate = 44100;
        float[] data = new float[Mathf.CeilToInt(sampleRate * 0.35f)];
        System.Random rng = new System.Random(5);
        float low = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            low += (Noise(rng) - low) * 0.3f;
            float hiss = low * Mathf.Min(1f, t * 300f) * Mathf.Exp(-t * 30f) * 0.4f;

            float blips = 0f;
            float[] starts = { 0.04f, 0.12f };
            float[] pitches = { 880f, 1318.5f };

            for (int b = 0; b < 2; b++)
            {
                float bt = t - starts[b];

                if (bt > 0f)
                    blips += Mathf.Sin(2f * Mathf.PI * pitches[b] * bt) * Mathf.Min(1f, bt * 500f) * Mathf.Exp(-bt * 14f) * 0.5f;
            }

            data[i] = hiss + blips;
        }

        return Build("Medkit Pickup", data, sampleRate);
    }

    // An attachment going on a gun: a metallic slide, then a firm click-clack as it locks.
    public static AudioClip AttachmentFit(int seed)
    {
        const int sampleRate = 44100;
        float[] data = new float[Mathf.CeilToInt(sampleRate * 0.4f)];
        System.Random rng = new System.Random(200 + seed);
        float high = 0f;
        float previous = 0f;
        float[] clicks = { 0.16f, 0.22f + seed * 0.01f };

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float noise = Noise(rng);

            // High-passed noise for the scrape of metal on metal.
            high = 0.85f * (high + noise - previous);
            previous = noise;
            float slide = t < 0.15f ? high * Mathf.Sin(Mathf.PI * t / 0.15f) * 0.18f : 0f;

            float click = 0f;

            for (int c = 0; c < clicks.Length; c++)
            {
                float ct = t - clicks[c];

                if (ct > 0f)
                {
                    float ring = Mathf.Sin(2f * Mathf.PI * (c == 0 ? 2400f : 1700f) * ct) * 0.5f + noise * 0.5f;
                    click += ring * Mathf.Exp(-ct * (c == 0 ? 90f : 60f)) * 0.7f;
                }
            }

            data[i] = slide + click;
        }

        return Build("Attachment Fit", data, sampleRate);
    }

    // Can't fit it: a short low buzz.
    public static AudioClip AttachmentDeny()
    {
        const int sampleRate = 44100;
        float[] data = new float[Mathf.CeilToInt(sampleRate * 0.18f)];

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 140f * t));
            data[i] = square * Mathf.Min(1f, t * 400f) * Mathf.Min(1f, (0.18f - t) * 60f) * 0.25f;
        }

        return Build("Attachment Deny", data, sampleRate);
    }

    // Something thrown: a short airy whoosh.
    public static AudioClip Throw(int seed)
    {
        const int sampleRate = 44100;
        float[] data = new float[Mathf.CeilToInt(sampleRate * 0.25f)];
        System.Random rng = new System.Random(seed + 950);
        float low = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float noise = Noise(rng);
            float smoothing = Mathf.Lerp(0.05f, 0.4f, Mathf.Sin(Mathf.PI * t / 0.25f));
            low += (noise - low) * smoothing;
            data[i] = low * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.25f));
        }

        return Build("Throw", data, sampleRate);
    }

    // Adrenaline going in: a click of the plunger and a rising hiss.
    public static AudioClip Inject()
    {
        const int sampleRate = 44100;
        float[] data = new float[Mathf.CeilToInt(sampleRate * 0.5f)];
        System.Random rng = new System.Random(31);
        float low = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float noise = Noise(rng);
            low += (noise - low) * 0.6f;
            float hiss = (noise - low) * Mathf.Clamp01((t - 0.03f) * 6f) * Mathf.Exp(-Mathf.Max(0f, t - 0.25f) * 14f) * 0.5f;
            float click = t < 0.006f ? noise * (1f - t / 0.006f) : 0f;
            float tone = Mathf.Sin(2f * Mathf.PI * (600f + t * 900f) * t) * Mathf.Exp(-t * 6f) * 0.15f;
            data[i] = hiss + click + tone;
        }

        return Build("Inject", data, sampleRate);
    }
}
