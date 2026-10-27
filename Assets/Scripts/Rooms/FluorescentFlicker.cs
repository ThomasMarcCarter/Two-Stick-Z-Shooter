using UnityEngine;

// An overhead fluorescent tube that's mostly steady but now and then stutters: a quick burst of
// cutting out, catching and dimming before it settles again. A few tubes are worn out and do it
// far more often, and spits sparks as it does if it has some (LightAtmosphere). Driven by the
// light's own intensity, so it works on any Light.
[RequireComponent(typeof(Light))]
public class FluorescentFlicker : MonoBehaviour
{
    [Tooltip("Seconds between flicker bursts on a healthy tube.")]
    [SerializeField] private Vector2 interval = new Vector2(8f, 30f);
    [Tooltip("Chance a tube is worn out and flickers much more often.")]
    [SerializeField, Range(0f, 1f)] private float wornChance = 0.15f;
    [SerializeField] private Vector2 wornInterval = new Vector2(1.5f, 6f);
    [Tooltip("How long a burst lasts.")]
    [SerializeField] private Vector2 burstLength = new Vector2(0.12f, 0.7f);

    private Light tube;
    private float baseIntensity;
    private bool worn;
    private float nextBurst;
    private float burstEnds;
    private float nextChange;

    // Shown when a worn tube's burst starts (LightAtmosphere.Dress sets it).
    public ParticleSystem Sparks { get; set; }

    private void Awake()
    {
        tube = GetComponent<Light>();
        baseIntensity = tube.intensity;
        worn = Random.value < wornChance;
        ScheduleNext();

        // Stagger the first bursts, so a floor's lights don't all go at once.
        nextBurst = Time.time + Random.Range(0f, (worn ? wornInterval : interval).y);
    }

    private void Update()
    {
        float now = Time.time;

        if (now < burstEnds)
        {
            // During a burst, jump between off, a weak glow and full every few hundredths of a
            // second, like the tube struggling to strike.
            if (now >= nextChange)
            {
                nextChange = now + Random.Range(0.02f, 0.08f);
                float roll = Random.value;
                tube.intensity = baseIntensity * (roll < 0.4f ? 0f : roll < 0.7f ? Random.Range(0.15f, 0.4f) : 1f);
            }

            return;
        }

        if (tube.intensity != baseIntensity)
            tube.intensity = baseIntensity;

        if (now >= nextBurst)
        {
            burstEnds = now + Random.Range(burstLength.x, burstLength.y);
            nextChange = now;

            if (worn && Sparks != null && Random.value < 0.5f)
                Sparks.Emit(Random.Range(6, 14));
            ScheduleNext();
        }
    }

    private void ScheduleNext()
    {
        Vector2 range = worn ? wornInterval : interval;
        nextBurst = Mathf.Max(burstEnds, Time.time) + Random.Range(range.x, range.y);
    }
}
