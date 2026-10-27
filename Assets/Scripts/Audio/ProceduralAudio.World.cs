using UnityEngine;

// Generated placeholder sounds for the world: footsteps, the room-cleared chime, boxes, glass
// and metal, the truck, the chopper, the lift, the escalator motor and the guard's whistle.
public static partial class ProceduralAudio
{
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

    // The hideout's truck starting and pulling away: the starter cranking, the diesel catching
    // with a cough, a lumpy idle, then revving up as it drives off and fading into the distance.
    public static AudioClip TruckEngine()
    {
        const int sampleRate = 44100;
        const float length = 3.4f;
        float[] data = new float[Mathf.CeilToInt(sampleRate * length)];
        System.Random rng = new System.Random(1300);
        float low = 0f;
        float phase = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float noise = Noise(rng);
            low += (noise - low) * 0.06f;

            // Firing rate: the starter's slow chugs, then idle, then the rev as it pulls away.
            float rate = t < 0.75f ? 7f : t < 1.3f ? 24f : Mathf.Lerp(24f, 58f, Mathf.SmoothStep(0f, 1f, (t - 1.3f) / 1.5f));
            phase += rate / sampleRate;
            float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * phase)), 6f);

            float sound;

            if (t < 0.75f)
            {
                float whine = Mathf.Sin(2f * Mathf.PI * (180f + 20f * Mathf.Sin(2f * Mathf.PI * 7f * t)) * t) * 0.25f;
                sound = whine + low * 3f * pulse;
            }
            else
            {
                float catchUp = Mathf.Clamp01((t - 0.75f) / 0.08f);
                float cough = t < 0.95f ? noise * 0.6f * Mathf.Exp(-(t - 0.75f) * 25f) : 0f;
                float body = Mathf.Sin(2f * Mathf.PI * rate * 2f * t) * 0.35f + low * 5f;
                sound = (body * (0.4f + pulse) + cough) * catchUp;
            }

            float fade = t > 2.4f ? Mathf.Clamp01(1f - (t - 2.4f) / (length - 2.4f)) : 1f;
            data[i] = Tanh(sound * 1.4f) * fade * Mathf.Min(1f, t * 400f);
        }

        return Build("Truck Engine", data, sampleRate);
    }

    // A helicopter's rotors: a deep thump six times a second over a turbine's whine. One second,
    // looping cleanly (a whole number of blade passes and whine cycles).
    public static AudioClip RotorLoop()
    {
        const int sampleRate = 44100;
        float[] data = new float[sampleRate];
        System.Random rng = new System.Random(1500);
        float low = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float noise = Noise(rng);
            low += (noise - low) * 0.05f;
            float beat = Mathf.Repeat(t * 6f, 1f);
            float thump = Mathf.Exp(-beat * 9f) * (low * 6f + Mathf.Sin(2f * Mathf.PI * 48f * t) * 0.6f);
            float whine = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.05f + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.03f;
            data[i] = Tanh((thump + whine + low * 0.6f) * 1.3f) * 0.8f;
        }

        return Build("Rotor Loop", data, sampleRate);
    }

    // A lift arriving: the two-note chime, a soft bell on each.
    public static AudioClip LiftChime()
    {
        const int sampleRate = 44100;
        const float length = 1.4f;
        float[] data = new float[Mathf.CeilToInt(sampleRate * length)];
        float[] notes = { 1046.5f, 830.6f };

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            float sound = 0f;

            for (int n = 0; n < notes.Length; n++)
            {
                float start = n * 0.42f;

                if (t < start)
                    continue;

                float u = t - start;
                float bell = Mathf.Sin(2f * Mathf.PI * notes[n] * u) + 0.3f * Mathf.Sin(2f * Mathf.PI * notes[n] * 2.76f * u) * Mathf.Exp(-u * 6f);
                sound += bell * Mathf.Exp(-u * 3.2f) * Mathf.Min(1f, u * 300f);
            }

            data[i] = sound * 0.35f;
        }

        return Build("Lift Chime", data, sampleRate);
    }

    // An electric motor running (an escalator, a baggage belt): a low hum with its harmonics and
    // a rattle of rollers. One second, looping cleanly (whole cycles of everything).
    public static AudioClip MotorHum()
    {
        const int sampleRate = 44100;
        float[] data = new float[sampleRate];
        System.Random rng = new System.Random(1600);
        float low = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;
            low += (Noise(rng) - low) * 0.08f;
            float hum = Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 200f * t) * 0.12f + Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.25f;
            float rattle = low * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 12f * t));
            data[i] = Tanh((hum + rattle * 1.5f) * 1.2f) * 0.5f;
        }

        return Build("Motor Hum", data, sampleRate);
    }

    // A security guard's pea whistle: two shrill trilling blasts.
    public static AudioClip Whistle()
    {
        const int sampleRate = 44100;
        const float length = 0.9f;
        float[] data = new float[Mathf.CeilToInt(sampleRate * length)];
        System.Random rng = new System.Random(1400);
        float phase = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sampleRate;

            // Two blasts with a gap, the pea rattling the pitch.
            bool blowing = t < 0.32f || (t > 0.42f && t < 0.86f);

            if (!blowing)
                continue;

            float trill = Mathf.Sin(2f * Mathf.PI * 34f * t);
            phase += (2900f + 160f * trill) / sampleRate;
            float edge = Mathf.Min(1f, Mathf.Min(t, Mathf.Abs(t - 0.42f)) * 200f);
            data[i] = (Mathf.Sin(2f * Mathf.PI * phase) * 0.5f + Noise(rng) * 0.08f) * (0.75f + 0.25f * trill) * edge;
        }

        return Build("Whistle", data, sampleRate);
    }
}
