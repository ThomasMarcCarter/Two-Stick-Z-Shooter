using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Full-screen "descending" screen between floors. Fades to black, builds the next floor while
// hidden, runs a short progress bar (generation is instant, so this is just pacing), then fades
// back in. The game is paused for the whole thing.
public class FloorTransition : MonoBehaviour
{
    public static bool IsActive { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text status;
    [SerializeField] private RectTransform barFill;

    [Header("Timing (real seconds)")]
    [SerializeField] private float fadeTime = 0.35f;
    [SerializeField] private float barTime = 1.2f;
    [SerializeField] private float holdTime = 0.25f;

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

    private void Awake()
    {
        IsActive = false;
        panel.SetActive(false);
    }

    // Down a hatch to a numbered floor of a zone ("MILITARY BASE").
    public void Play(string zoneName, int floorNumber, int totalFloors, Action buildFloor)
    {
        Play($"> DESCENDING TO FLOOR {floorNumber}", $"{zoneName} // FLOOR {floorNumber} / {totalFloors} // LINK ESTABLISHED", buildFloor);
    }

    // Any other descent (the supply room under the last floor): the heading, and the line shown
    // once the bar is full. `driving` (in the truck to another zone) uses the road's lines.
    public void Play(string heading, string arrived, Action buildFloor, bool driving = false)
    {
        if (!IsActive)
            StartCoroutine(Run(heading, arrived, buildFloor, driving ? drivingLines : flavourLines));
    }

    private IEnumerator Run(string heading, string arrived, Action buildFloor, string[] pool)
    {
        IsActive = true;
        float previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        string[] lines = PickLines(pool);

        title.text = heading;
        status.text = lines[0];
        SetBar(0f);
        panel.SetActive(true);

        yield return Fade(0f, 1f);

        buildFloor();

        for (float t = 0f; t < barTime; t += Time.unscaledDeltaTime)
        {
            float progress = t / barTime;
            SetBar(progress);
            status.text = lines[Mathf.Min(lines.Length - 1, (int)(progress * lines.Length))];
            yield return null;
        }

        SetBar(1f);
        status.text = arrived;

        yield return new WaitForSecondsRealtime(holdTime);
        yield return Fade(1f, 0f);

        panel.SetActive(false);
        Time.timeScale = previousTimeScale;
        IsActive = false;
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

    private void SetBar(float progress)
    {
        barFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
    }
}
