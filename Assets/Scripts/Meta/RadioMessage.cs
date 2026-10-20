using TMPro;
using UnityEngine;

// A radio transmission across the top of the HUD: "> RADIO // DISTRESS SIGNAL" in amber over a
// line typed out in phosphor green, broken up with static, with a crackle of noise when it comes
// in. Holds long enough to read, then fades. Used for a survivor's distress call when a floor
// has one caged on it. Built in code on the HUD canvas the first time it's needed.
public class RadioMessage : MonoBehaviour
{
    private static readonly Color Phosphor = UiColours.Phosphor;
    private static readonly Color Amber = UiColours.Amber;
    private const float CharactersPerSecond = 32f;
    private const float Hold = 5f;
    private const float FadeTime = 1f;

    private static RadioMessage instance;

    private CanvasGroup group;
    private TMP_Text header;
    private TMP_Text body;
    private float startedAt;
    private float holdUntil;
    private int lastShown;
    private bool showing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        instance = null;
    }

    public static void Show(string channel, string line)
    {
        if (instance == null)
            instance = Create();

        if (instance == null)
            return;

        instance.header.text = $"> RADIO // {channel}";
        instance.body.text = line;
        instance.body.maxVisibleCharacters = 0;
        instance.group.alpha = 1f;
        instance.startedAt = Time.unscaledTime;
        instance.holdUntil = Time.unscaledTime + line.Length / CharactersPerSecond + Hold;
        instance.lastShown = 0;
        instance.showing = true;
        instance.gameObject.SetActive(true);
        SoundFX.Play2D(Crackle(), 0.35f);
    }

    private static RadioMessage Create()
    {
        EquipmentHud hud = FindAnyObjectByType<EquipmentHud>();
        Canvas canvas = hud != null ? hud.canvas : null;

        if (canvas == null)
            return null;

        GameObject root = new GameObject("Radio Message", typeof(RectTransform));
        root.layer = canvas.gameObject.layer;
        root.transform.SetParent(canvas.transform, false);

        // With the HUD widgets, under the menus and the cursor.
        root.transform.SetSiblingIndex(hud.transform.GetSiblingIndex() + 1);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -96f);
        rect.sizeDelta = new Vector2(640f, 70f);

        RadioMessage radio = root.AddComponent<RadioMessage>();
        radio.group = root.AddComponent<CanvasGroup>();
        radio.group.blocksRaycasts = false;
        radio.group.interactable = false;

        TMP_Text sample = hud.GetComponentInChildren<TMP_Text>(true);
        TMP_FontAsset font = sample != null ? sample.font : TMP_Settings.defaultFontAsset;
        radio.header = radio.MakeText("Header", font, 12f, 0f, 18f, Amber);
        radio.body = radio.MakeText("Line", font, 15f, -20f, 48f, Phosphor);
        radio.body.textWrappingMode = TextWrappingModes.Normal;
        root.SetActive(false);
        return radio;
    }

    private TMP_Text MakeText(string name, TMP_FontAsset font, float size, float y, float height, Color colour)
    {
        Vector2 top = new Vector2(0.5f, 1f);
        RectTransform rect = CrtPanel.MakeRect(transform, name, top, top, new Vector2(0f, y), new Vector2(640f, height));
        TMP_Text text = CrtPanel.MakeLabel(rect, font, "", size, colour, 4f, TextAlignmentOptions.Top);
        text.richText = false;    // typed out a character at a time
        return text;
    }

    private void Update()
    {
        if (!showing)
            return;

        // Out of the way under full-screen menus; the clock keeps running.
        bool covered = GameFlow.ScreenOverWorld;
        float now = Time.unscaledTime;
        int shown = Mathf.Min(body.text.Length, Mathf.FloorToInt((now - startedAt) * CharactersPerSecond));

        if (shown != lastShown)
        {
            // A burst of static now and then while it's coming through.
            if (shown / 12 != lastShown / 12 && Random.value < 0.5f)
                SoundFX.Play2D(Crackle(), 0.12f, Random.Range(0.9f, 1.2f));

            lastShown = shown;
            body.maxVisibleCharacters = shown;
        }

        // The signal flickers a little, like a weak transmission.
        float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(now * 9f, 0f);
        float fade = now > holdUntil ? Mathf.Clamp01(1f - (now - holdUntil) / FadeTime) : 1f;
        group.alpha = covered ? 0f : flicker * fade;

        if (now >= holdUntil + FadeTime)
        {
            showing = false;
            gameObject.SetActive(false);
        }
    }

    // A short crackle of filtered static.
    private static AudioClip Crackle()
    {
        return SoundFX.Pick(SoundFX.Clips("radio-crackle", 2, seed =>
        {
            const int sampleRate = 44100;
            float[] data = new float[Mathf.CeilToInt(sampleRate * 0.35f)];
            System.Random rng = new System.Random(900 + seed);
            float low = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)sampleRate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                low += (noise - low) * 0.35f;

                // Pops and dropouts break the hiss up.
                float gate = Mathf.PerlinNoise(t * 60f, seed * 3.1f) > 0.42f ? 1f : 0.15f;
                float pop = rng.NextDouble() < 0.002 ? 1.5f : 0f;
                data[i] = (low * gate + pop * noise) * Mathf.Min(1f, t * 200f) * Mathf.Clamp01((0.35f - t) * 12f) * 0.5f;
            }

            AudioClip clip = AudioClip.Create("Radio Crackle", data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }));
    }
}
