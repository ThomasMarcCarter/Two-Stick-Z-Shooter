using UnityEngine;

// Sounds made in code, as placeholders until real recordings are added.
public static class ProceduralAudio
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

    // A boot on a hard floor: a low, dull thump of the heel landing, with a short gritty scuff
    // of filtered noise on top. Seeds give slightly different steps so a walk doesn't loop.
    public static AudioClip Footstep(int seed)
    {
        const int sampleRate = 44100;
        const float length = 0.16f;
        int samples = Mathf.CeilToInt(sampleRate * length);
        float[] data = new float[samples];
        System.Random rng = new System.Random(seed);

        float thumpPitch = 70f + (float)rng.NextDouble() * 30f;
        float scuffBrightness = 0.25f + (float)rng.NextDouble() * 0.2f;
        float scuffLevel = 0.35f + (float)rng.NextDouble() * 0.25f;
        int scuffDelay = (int)(sampleRate * (0.004f + (float)rng.NextDouble() * 0.01f));

        float low = 0f;
        float lower = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;

            // Heel: a pitch that drops as it decays, which reads as weight.
            float pitch = thumpPitch * (1f + 0.6f * Mathf.Exp(-t * 60f));
            float thump = Mathf.Sin(2f * Mathf.PI * pitch * t) * Mathf.Exp(-t * 38f);

            // Scuff: band-passed noise, a fraction later than the heel.
            float noise = (float)rng.NextDouble() * 2f - 1f;
            low += (noise - low) * scuffBrightness;
            lower += (low - lower) * 0.08f;
            float scuff = 0f;

            if (i >= scuffDelay)
            {
                float st = (i - scuffDelay) / (float)sampleRate;
                scuff = (low - lower) * Mathf.Min(1f, st * 900f) * Mathf.Exp(-st * 55f) * scuffLevel * 3f;
            }

            data[i] = thump + scuff;
        }

        // Normalise so the loudest moment sits just under full volume.
        float peak = 0.0001f;

        foreach (float sample in data)
            peak = Mathf.Max(peak, Mathf.Abs(sample));

        for (int i = 0; i < samples; i++)
            data[i] *= 0.8f / peak;

        AudioClip clip = AudioClip.Create("Footstep", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ---------- Combat and feedback ----------

    // A bullet hitting a wall: a bright crack and a little grit, and on some variations a thin
    // ricochet whine falling away.
    public static AudioClip ImpactWall(int seed, bool ricochet)
    {
        const int sampleRate = 44100;
        float[] data = new float[Mathf.CeilToInt(sampleRate * (ricochet ? 0.32f : 0.1f))];
        System.Random rng = new System.Random(seed);
        float low = 0f;
        float ping = 1600f + (float)rng.NextDouble() * 900f;
        float whineStart = 3000f + (float)rng.NextDouble() * 1200f;
        float phase = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float noise = Noise(rng);

            // High-passed noise: the crack.
            low += (noise - low) * 0.2f;
            float crack = (noise - low) * Mathf.Exp(-t * 90f) * 1.4f;
            float grit = low * Mathf.Exp(-t * 40f) * 0.6f;
            float tone = Mathf.Sin(2f * Mathf.PI * ping * t) * Mathf.Exp(-t * 70f) * 0.35f;
            float sample = crack + grit + tone;

            if (ricochet && t > 0.015f)
            {
                float rt = t - 0.015f;
                float frequency = Mathf.Lerp(whineStart, whineStart * 0.45f, rt / 0.3f);
                phase += 2f * Mathf.PI * frequency / sampleRate;
                sample += Mathf.Sin(phase) * Mathf.Min(1f, rt * 60f) * Mathf.Exp(-rt * 9f) * 0.22f;
            }

            data[i] = sample;
        }

        return Build("Impact Wall", data, sampleRate);
    }

    // The player getting hurt: a heavy body hit, a distorted downward buzz like the suit's
    // warning circuit tripping, and a crackle of static.
    public static AudioClip PlayerHurt(int seed)
    {
        const int sampleRate = 44100;
        float[] data = new float[Mathf.CeilToInt(sampleRate * 0.4f)];
        System.Random rng = new System.Random(seed);
        float phase = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;

            float hit = Mathf.Sin(2f * Mathf.PI * 80f * (1f + Mathf.Exp(-t * 40f)) * t) * Mathf.Exp(-t * 18f);

            float frequency = Mathf.Lerp(240f, 90f, Mathf.Clamp01(t / 0.3f));
            phase += 2f * Mathf.PI * frequency / sampleRate;
            float buzz = Tanh(Mathf.Sin(phase) * 4f) * Mathf.Min(1f, t * 300f) * Mathf.Exp(-t * 7f) * 0.35f;

            float staticBurst = Noise(rng) * Mathf.Exp(-t * 25f) * 0.25f;

            data[i] = hit + buzz + staticBurst;
        }

        return Build("Player Hurt", data, sampleRate);
    }

    // Room cleared: a rising three-note terminal chime (C, E, G), soft square tones.
    public static AudioClip RoomCleared()
    {
        const int sampleRate = 44100;
        float[] data = new float[Mathf.CeilToInt(sampleRate * 0.75f)];
        float[] notes = { 523.25f, 659.25f, 783.99f };

        for (int n = 0; n < notes.Length; n++)
        {
            int start = (int)(sampleRate * n * 0.1f);
            float hold = n == notes.Length - 1 ? 5f : 12f;   // the last note rings on

            for (int i = start; i < data.Length; i++)
            {
                float t = (i - start) / (float)sampleRate;
                float envelope = Mathf.Min(1f, t * 400f) * Mathf.Exp(-t * hold);

                // Stop once the note has died away (not during its attack, which starts at 0).
                if (t > 0.01f && envelope < 0.001f)
                    break;

                // Odd harmonics, rolled off: a square wave with the edges taken off.
                float w = 2f * Mathf.PI * notes[n] * t;
                float tone = Mathf.Sin(w) + Mathf.Sin(w * 3f) / 6f + Mathf.Sin(w * 5f) / 15f;
                data[i] += tone * envelope * 0.5f;
            }
        }

        return Build("Room Cleared", data, sampleRate);
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

    private static float Noise(System.Random rng)
    {
        return (float)rng.NextDouble() * 2f - 1f;
    }

    private static float Tanh(float x)
    {
        float e = Mathf.Exp(2f * x);
        return (e - 1f) / (e + 1f);
    }

    // A bullet punching into cardboard: a hollow papery thump with a short crackle.
    public static AudioClip CardboardHit(int seed)
    {
        const int sampleRate = 44100;
        float[] data = new float[Mathf.CeilToInt(sampleRate * 0.16f)];
        System.Random rng = new System.Random(seed + 300);
        float low = 0f;
        float pitch = 150f + (float)rng.NextDouble() * 60f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float thump = Mathf.Sin(2f * Mathf.PI * pitch * t) * Mathf.Exp(-t * 35f) * 0.7f;
            float noise = Noise(rng);
            low += (noise - low) * 0.35f;
            // Crackle: noise switched on and off in tiny bursts, like fibres tearing.
            float crackle = (rng.NextDouble() < 0.3 ? low : 0f) * Mathf.Exp(-t * 28f);
            data[i] = thump + crackle;
        }

        return Build("Cardboard Hit", data, sampleRate);
    }

    // A box crumpling: a dull collapse and a longer, rustling crunch of folding cardboard.
    public static AudioClip CardboardBreak(int seed)
    {
        const int sampleRate = 44100;
        float[] data = new float[Mathf.CeilToInt(sampleRate * 0.5f)];
        System.Random rng = new System.Random(seed + 400);
        float low = 0f;
        float lower = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float collapse = Mathf.Sin(2f * Mathf.PI * 95f * (1f + Mathf.Exp(-t * 20f)) * t) * Mathf.Exp(-t * 12f) * 0.6f;
            float noise = Noise(rng);
            low += (noise - low) * 0.25f;
            lower += (low - lower) * 0.05f;
            float band = low - lower;
            // Crunching in uneven waves as different panels give.
            float waves = 0.5f + 0.5f * Mathf.Sin(t * 70f + Mathf.Sin(t * 23f) * 3f);
            float crunch = (rng.NextDouble() < 0.45 ? band : band * 0.3f) * waves * Mathf.Exp(-t * 6f) * 1.6f;
            data[i] = collapse + crunch;
        }

        return Build("Cardboard Break", data, sampleRate);
    }

    // A bottle shattering: a sharp crack and a spray of bright, glassy tinkles falling away.
    public static AudioClip GlassBreak(int seed)
    {
        const int sampleRate = 44100;
        float[] data = new float[Mathf.CeilToInt(sampleRate * 0.45f)];
        System.Random rng = new System.Random(seed + 500);
        float low = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float noise = Noise(rng);
            low += (noise - low) * 0.5f;
            data[i] = (noise - low) * Mathf.Exp(-t * 60f) * 1.2f;
        }

        // Shards: short high pings, scattered over the first part of the sound.
        int shards = 9 + rng.Next(5);

        for (int s = 0; s < shards; s++)
        {
            int start = (int)(sampleRate * (float)rng.NextDouble() * 0.28f);
            float pitch = 2500f + (float)rng.NextDouble() * 4500f;
            float amplitude = 0.2f + (float)rng.NextDouble() * 0.35f;

            for (int i = start; i < data.Length; i++)
            {
                float t = (i - start) / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 40f);

                if (envelope < 0.002f)
                    break;

                data[i] += Mathf.Sin(2f * Mathf.PI * pitch * t) * envelope * amplitude;
            }
        }

        return Build("Glass Break", data, sampleRate);
    }

    // A bullet off a tin can or a gas can: a hollow metallic ring.
    public static AudioClip MetalClank(int seed)
    {
        const int sampleRate = 44100;
        float[] data = new float[Mathf.CeilToInt(sampleRate * 0.3f)];
        System.Random rng = new System.Random(seed + 600);
        float pitch = 700f + (float)rng.NextDouble() * 500f;
        float[] ratios = { 1f, 1.47f, 2.09f, 2.76f };

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float ring = 0f;

            for (int r = 0; r < ratios.Length; r++)
                ring += Mathf.Sin(2f * Mathf.PI * pitch * ratios[r] * t) * Mathf.Exp(-t * (14f + r * 6f)) / (r + 1f);

            float tick = t < 0.004f ? Noise(rng) * (1f - t / 0.004f) : 0f;
            data[i] = ring + tick;
        }

        return Build("Metal Clank", data, sampleRate);
    }

    // A grenade: a hard crack, then a deep rumbling boom that rolls away.
    public static AudioClip Explosion(int seed)
    {
        const int sampleRate = 44100;
        float[] data = new float[Mathf.CeilToInt(sampleRate * 1.6f)];
        System.Random rng = new System.Random(seed + 900);
        float low = 0f;
        float lower = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float noise = Noise(rng);
            low += (noise - low) * 0.08f;
            lower += (low - lower) * 0.05f;

            float crack = noise * Mathf.Exp(-t * 45f);
            float boom = low * 3f * Mathf.Exp(-t * 4f);
            float rumble = lower * 9f * Mathf.Exp(-t * 2.2f);
            float thump = Mathf.Sin(2f * Mathf.PI * (55f - t * 20f) * t) * Mathf.Exp(-t * 7f);
            data[i] = Tanh((crack + boom + rumble + thump) * 1.6f) * Mathf.Min(1f, t * 2000f);
        }

        return Build("Explosion", data, sampleRate);
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

    // A fire burning: soft roaring noise with random crackles. Loops cleanly (the ends fade
    // into each other).
    public static AudioClip FireLoop()
    {
        const int sampleRate = 44100;
        const float length = 2f;
        float[] data = new float[Mathf.CeilToInt(sampleRate * length)];
        System.Random rng = new System.Random(77);
        float low = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            low += (Noise(rng) - low) * 0.04f;
            data[i] = low * 2.5f;
        }

        for (int c = 0; c < 70; c++)
        {
            int start = rng.Next(data.Length);
            float amplitude = 0.2f + (float)rng.NextDouble() * 0.6f;

            for (int i = 0; i < 300; i++)
                data[(start + i) % data.Length] += Noise(rng) * amplitude * Mathf.Exp(-i / 40f);
        }

        // Crossfade the last tenth into the start so the loop point doesn't click.
        int fade = data.Length / 10;
        float[] looped = new float[data.Length - fade];

        for (int i = 0; i < looped.Length; i++)
        {
            looped[i] = data[i];

            if (i < fade)
            {
                float w = i / (float)fade;
                looped[i] = data[i] * w + data[looped.Length + i] * (1f - w);
            }
        }

        return Build("Fire Loop", looped, sampleRate);
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

    // A screamer's shriek: a harsh, wavering high cry over rough breath, rising then cracking.
    public static AudioClip Scream(int seed)
    {
        const int sampleRate = 44100;
        const float length = 1.1f;
        float[] data = new float[Mathf.CeilToInt(sampleRate * length)];
        System.Random rng = new System.Random(seed + 1200);
        float basePitch = 620f + (float)rng.NextDouble() * 160f;
        float phase = 0f;
        float low = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float rise = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / length)) * 0.35f;
            float wobble = Mathf.Sin(2f * Mathf.PI * 9f * t) * 0.06f + Noise(rng) * 0.02f;
            float pitch = basePitch * (1f + rise + wobble);
            phase += 2f * Mathf.PI * pitch / sampleRate;

            float voice = Mathf.Sin(phase) + 0.5f * Mathf.Sin(phase * 2.01f) + 0.3f * Mathf.Sin(phase * 3.03f);
            low += (Noise(rng) - low) * 0.3f;
            float envelope = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((length - t) / 0.35f);
            data[i] = Tanh((voice * 0.8f + low * 0.9f) * 2.2f) * envelope;
        }

        return Build("Scream", data, sampleRate);
    }

    // A bloater about to go: a wet, rising gurgle.
    public static AudioClip BloaterSwell(int seed)
    {
        const int sampleRate = 44100;
        const float length = 0.75f;
        float[] data = new float[Mathf.CeilToInt(sampleRate * length)];
        System.Random rng = new System.Random(seed + 1300);
        float phase = 0f;
        float low = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float pitch = Mathf.Lerp(70f, 190f, t / length) * (1f + Mathf.Sin(2f * Mathf.PI * 14f * t) * 0.25f);
            phase += 2f * Mathf.PI * pitch / sampleRate;
            low += (Noise(rng) - low) * 0.05f;
            float bubbles = Mathf.Sin(phase) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 23f * t));
            data[i] = (bubbles + low * 4f) * Mathf.Clamp01(t / 0.05f);
        }

        return Build("Bloater Swell", data, sampleRate);
    }

    // A melee blow landing: a dull, heavy thud with a short wet crunch.
    public static AudioClip MeleeHit(int seed)
    {
        const int sampleRate = 44100;
        float[] data = new float[Mathf.CeilToInt(sampleRate * 0.25f)];
        System.Random rng = new System.Random(seed + 1400);
        float low = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float noise = Noise(rng);
            low += (noise - low) * 0.15f;
            float thud = Mathf.Sin(2f * Mathf.PI * (110f - t * 200f) * t) * Mathf.Exp(-t * 22f);
            float crunch = low * 2.5f * Mathf.Exp(-t * 35f);
            float click = t < 0.003f ? noise * (1f - t / 0.003f) : 0f;
            data[i] = Tanh((thud + crunch + click) * 1.8f);
        }

        return Build("Melee Hit", data, sampleRate);
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
