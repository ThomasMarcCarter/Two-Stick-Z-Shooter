using UnityEngine;

// Generated placeholder sounds for fighting: bullet impacts, getting hurt, melee hits,
// explosions, fire, zombie screams and the bloater swelling.
public static partial class ProceduralAudio
{
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
}
