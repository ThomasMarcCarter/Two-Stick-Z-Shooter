using TMPro;
using UnityEngine;

// Floating speech over a character's head: no box or panel, just glowing phosphor text in the
// UI's green, typed out a letter at a time with a soft terminal blip, held long enough to read,
// then faded out. Faces the top-down camera. Saying something new replaces what's showing.
// Built in code; one per speaker.
// After CameraFollow, so it's placed for where the camera ends up this frame.
[DefaultExecutionOrder(200)]
public class SpeechText : MonoBehaviour
{
    private const float Scale = 0.01f;          // world units per canvas pixel
    private const float CharactersPerSecond = 38f;
    private const float FadeTime = 0.5f;

    private Transform speaker;
    private float height;
    private TextMeshProUGUI text;
    private Canvas canvas;
    private Color colour;
    private float startedAt;
    private float holdUntil;
    private int lastShown;
    private bool speaking;

    public bool IsSpeaking => speaking;

    // `lineHeight` is how far above the speaker's feet the text floats.
    public static SpeechText Create(Transform speaker, float lineHeight)
    {
        GameObject root = new GameObject("Speech", typeof(RectTransform));
        root.transform.SetParent(speaker, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        root.transform.localScale = Vector3.one * Scale;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(520f, 160f);

        SpeechText speech = root.AddComponent<SpeechText>();
        speech.canvas = canvas;
        speech.speaker = speaker;
        speech.height = lineHeight;
        speech.Build();
        canvas.enabled = false;
        return speech;
    }

    private void Build()
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 26;
        text.characterSpacing = 5f;
        text.lineSpacing = 4f;
        text.alignment = TextAlignmentOptions.Bottom;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        text.richText = true;

        // A glow and a thin dark edge, like phosphor on a screen, readable on any floor.
        Material glowing = new Material(text.fontSharedMaterial);
        glowing.EnableKeyword(ShaderUtilities.Keyword_Glow);
        glowing.SetFloat(ShaderUtilities.ID_GlowOuter, 0.45f);
        glowing.SetFloat(ShaderUtilities.ID_GlowPower, 0.6f);
        glowing.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
        glowing.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0.05f, 0.02f, 0.9f));
        text.fontMaterial = glowing;
    }

    public void Say(string line, Color voice, float hold = 2.5f)
    {
        colour = voice;
        text.text = line;
        text.color = voice;
        text.fontMaterial.SetColor(ShaderUtilities.ID_GlowColor, new Color(voice.r, voice.g, voice.b, 0.55f));
        text.maxVisibleCharacters = 0;
        text.alpha = 1f;
        startedAt = Time.time;
        holdUntil = Time.time + line.Length / CharactersPerSecond + hold + line.Length * 0.02f;
        lastShown = 0;
        speaking = true;
        canvas.enabled = true;
    }

    public void Stop()
    {
        speaking = false;
        canvas.enabled = false;
    }

    private void LateUpdate()
    {
        if (!speaking)
            return;

        // Out of the way while the inventory or a station screen is up.
        canvas.enabled = !InventoryScreen.IsOpen && !StationScreen.IsOpen;

        int shown = Mathf.Min(text.text.Length, Mathf.FloorToInt((Time.time - startedAt) * CharactersPerSecond));

        if (shown != lastShown)
        {
            // A blip every few letters, not every one.
            if (shown / 3 != lastShown / 3 && shown < text.text.Length && text.text[shown] != ' ')
                SoundFX.PlayAt(Blip(), speaker.position, 0.12f, Random.Range(0.92f, 1.08f));

            lastShown = shown;
            text.maxVisibleCharacters = shown;
        }

        float fade = Time.time - holdUntil;

        if (fade > 0f)
        {
            text.alpha = Mathf.Clamp01(1f - fade / FadeTime);

            if (fade >= FadeTime)
            {
                Stop();
                return;
            }
        }

        Camera camera = Camera.main;

        if (camera != null)
            transform.rotation = camera.transform.rotation;

        transform.position = speaker.position + Vector3.up * height;
    }

    // A short soft square blip, like a terminal printing.
    private static AudioClip Blip()
    {
        return SoundFX.Pick(SoundFX.Clips("speech-blip", 1, seed =>
        {
            const int sampleRate = 44100;
            float[] data = new float[Mathf.CeilToInt(sampleRate * 0.04f)];

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)sampleRate;
                float w = 2f * Mathf.PI * 620f * t;
                float tone = Mathf.Sin(w) + Mathf.Sin(w * 3f) / 5f;
                data[i] = tone * Mathf.Min(1f, t * 800f) * Mathf.Exp(-t * 90f) * 0.4f;
            }

            AudioClip clip = AudioClip.Create("Speech Blip", data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }));
    }
}
