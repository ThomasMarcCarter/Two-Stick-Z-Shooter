using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Full-screen "descending" screen between floors. Fades to black, builds the next floor while
// hidden, types out the heading over a few flavour lines (generation is instant, so this is just
// pacing), then the new floor fades in under an arrival card ("FLOOR 2 / 3", the zone): the
// black goes first while the camera eases in from wide and the player lands with a jolt, the
// card moves up out of the way and lingers a moment over the live game, then fades. The game is
// paused only while the screen is black; the controls come back as the floor appears.
// (2026-11-12: replaced the fake progress bar and the plain fade back in.)
public class FloorTransition : MonoBehaviour
{
    public static bool IsActive { get; private set; }

    // The card is still showing over the game, after the controls came back.
    private bool lingering;

    [SerializeField] private GameObject panel;
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text status;
    [SerializeField] private RectTransform barFill;

    [Header("Timing (real seconds)")]
    [SerializeField] private float fadeTime = 0.35f;
    [SerializeField] private float barTime = 1.2f;
    [SerializeField] private float holdTime = 0.25f;
    [Tooltip("Seconds the arrival card stays over the new floor before fading.")]
    [SerializeField] private float lingerTime = 1.4f;
    [Tooltip("How far the camera starts zoomed out on arrival (1 = not at all).")]
    [SerializeField] private float arrivalZoom = 1.3f;

    [Header("Flavour")]
    [Tooltip("Lines shown under the bar while descending. A few are picked at random each time.")]
    [SerializeField] private string[] flavourLines =
    {
        "CHECKING MAGS...",
        "COUNTING SHELLS...",
        "TIGHTENING BOOT LACES...",
        "TESTING FLASHLIGHT BATTERY...",
        "WIPING DOWN THE SCOPE...",
        "CALIBRATING RADAR...",
        "STEADYING BREATHING...",
        "CHAMBERING A ROUND...",
        "RECEIVING SECTOR MAP...",
        "SYNCING UPLINK...",
        "LISTENING AT THE HATCH...",
        "CHECKING SIX..."
    };
    [SerializeField] private int linesPerDescent = 3;
    [Tooltip("Lines shown instead while driving to another zone in the hideout's truck.")]
    [SerializeField] private string[] drivingLines =
    {
        "CHECKING THE FUEL GAUGE...",
        "WEAVING ROUND WRECKS...",
        "KEEPING THE HEADLIGHTS LOW...",
        "RADIO SILENCE ON THE ROAD...",
        "CLEARING A ROADBLOCK...",
        "WATCHING THE MIRRORS...",
        "GRINDING THROUGH THE GEARS...",
        "SCANNING THE SKYLINE..."
    };

    private Image background;
    private Vector2 titleHome;
    private Vector2 statusHome;

    // The card sits up here over the new floor, clear of the player in the middle.
    private const float CardLift = 210f;

    private void Awake()
    {
        IsActive = false;
        panel.SetActive(false);
        Transform backdrop = panel.transform.Find("Background");
        background = backdrop != null ? backdrop.GetComponent<Image>() : null;
        titleHome = title.rectTransform.anchoredPosition;
        statusHome = status.rectTransform.anchoredPosition;

        // No progress bar any more: the heading types out instead.
        if (barFill != null && barFill.parent != null)
            barFill.parent.gameObject.SetActive(false);
    }

    // Down a hatch to a numbered floor of a zone ("MILITARY BASE").
    public void Play(string zoneName, int floorNumber, int totalFloors, Action buildFloor)
    {
        Play($"> DESCENDING TO FLOOR {floorNumber}", $"{zoneName} // LINK ESTABLISHED", buildFloor, false,
            $"FLOOR {floorNumber} / {totalFloors}");
    }

    // Any other descent (the supply room under the last floor): the heading, and the line shown
    // on arrival (with `arrivedTitle` over it, if any). `driving` (in the truck to another zone)
    // uses the road's lines.
    public void Play(string heading, string arrived, Action buildFloor, bool driving = false, string arrivedTitle = null)
    {
        if (!IsActive && !lingering)
            StartCoroutine(Run(heading, arrived, arrivedTitle, buildFloor, driving ? drivingLines : flavourLines));
    }

    private IEnumerator Run(string heading, string arrived, string arrivedTitle, Action buildFloor, string[] pool)
    {
        IsActive = true;
        float previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        string[] lines = PickLines(pool);

        title.text = heading;
        title.maxVisibleCharacters = 0;
        status.text = "";
        title.rectTransform.anchoredPosition = titleHome;
        status.rectTransform.anchoredPosition = statusHome;
        SetBackground(1f);
        panel.SetActive(true);

        yield return Fade(0f, 1f);

        buildFloor();

        // The heading types out while the flavour lines tick over under it.
        for (float t = 0f; t < barTime; t += Time.unscaledDeltaTime)
        {
            float progress = t / barTime;
            title.maxVisibleCharacters = Mathf.CeilToInt(heading.Length * Mathf.Clamp01(progress * 2f));
            status.text = lines[Mathf.Min(lines.Length - 1, (int)(progress * lines.Length))];
            yield return null;
        }

        title.maxVisibleCharacters = int.MaxValue;
        yield return new WaitForSecondsRealtime(holdTime);
        yield return Arrive(arrived, arrivedTitle, previousTimeScale);
    }

    // The floor fades in under the card; the controls come back once it's showing, and the card
    // fades a little later.
    private IEnumerator Arrive(string arrived, string arrivedTitle, float previousTimeScale)
    {
        title.text = arrivedTitle ?? "";
        status.text = arrived;
        title.rectTransform.anchoredPosition = titleHome + Vector2.up * CardLift;
        status.rectTransform.anchoredPosition = statusHome + Vector2.up * (CardLift + 45f);

        Time.timeScale = previousTimeScale;
        CameraFollow.ZoomIn(arrivalZoom, 1.4f);
        CameraFollow.Shake(0.12f);
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("arrive.land", 3, ProceduralAudio.Footstep)), 0.8f, 0.75f);

        for (float t = 0f; t < fadeTime * 2f; t += Time.unscaledDeltaTime)
        {
            SetBackground(1f - t / (fadeTime * 2f));
            yield return null;
        }

        SetBackground(0f);
        IsActive = false;
        lingering = true;

        yield return new WaitForSecondsRealtime(lingerTime);
        yield return Fade(1f, 0f);

        panel.SetActive(false);
        SetBackground(1f);
        lingering = false;
    }

    private void SetBackground(float alpha)
    {
        if (background != null)
            background.color = new Color(background.color.r, background.color.g, background.color.b, alpha);
    }

    // A few different lines in a random order.
    private string[] PickLines(string[] from)
    {
        string[] pool = (string[])from.Clone();

        for (int i = pool.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        int count = Mathf.Clamp(linesPerDescent, 1, pool.Length);
        string[] picked = new string[count];
        Array.Copy(pool, picked, count);
        return picked;
    }

    private IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            fade.alpha = Mathf.Lerp(from, to, t / fadeTime);
            yield return null;
        }

        fade.alpha = to;
    }
}
