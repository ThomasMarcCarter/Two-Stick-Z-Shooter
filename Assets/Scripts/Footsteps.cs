using UnityEngine;

// Footstep sounds driven by how fast the Rigidbody is actually moving: one step per stride
// travelled, so steps come quicker the faster it goes, and they get louder towards sprinting
// speed. Used by the player (2D, always heard) and enemies (positional, lower and heavier).
// Each step is a random clip (never the same twice running) with a small random pitch change.
// Clips come from this component's own list, else the shared set (the FloorGenerator's
// Footstep Sounds), else generated steps.
[RequireComponent(typeof(Rigidbody))]
public class Footsteps : MonoBehaviour
{
    [Tooltip("Leave empty to use the FloorGenerator's footstep sounds (or generated ones if those are empty too).")]
    [SerializeField] private AudioClip[] clips;

    [Header("Timing")]
    [Tooltip("Distance travelled per step.")]
    [SerializeField] private float strideLength = 1.5f;
    [Tooltip("Below this speed there are no steps.")]
    [SerializeField] private float minSpeed = 0.6f;

    [Header("Loudness")]
    [Tooltip("Speeds at which steps are at their quietest and loudest (walk and sprint).")]
    [SerializeField] private Vector2 speedRange = new Vector2(2.5f, 8f);
    [SerializeField, Range(0f, 1f)] private float quietVolume = 0.13f;
    [SerializeField, Range(0f, 1f)] private float loudVolume = 0.26f;
    [SerializeField] private float pitch = 1f;
    [Tooltip("Random pitch change per step, either way, for variety.")]
    [SerializeField, Range(0f, 0.4f)] private float pitchVariation = 0.06f;

    [Header("Noise")]
    [Tooltip("Steps can be heard by enemies (the player's). Quiet when slow, loud when sprinting.")]
    [SerializeField] private bool alertsEnemies;

    [Header("Positional (enemies)")]
    [Tooltip("0 = heard the same everywhere (the player), 1 = fully positional.")]
    [SerializeField, Range(0f, 1f)] private float spatialBlend;
    [Tooltip("Positional only: full volume within this distance of the player (the listener is on them).")]
    [SerializeField] private float fullVolumeDistance = 3f;
    [SerializeField] private float silentDistance = 24f;

    private static AudioClip[] generated;
    private static AudioClip[] shared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        shared = null;
    }

    // The footstep clips for everything without its own (set by the FloorGenerator).
    public static void UseClips(AudioClip[] stepClips)
    {
        shared = stepClips != null && stepClips.Length > 0 ? stepClips : null;
    }

    private Rigidbody rb;
    private AudioSource source;
    private AudioClip[] steps;
    private float travelled;
    private int lastClip = -1;

    // For enemies made in code: sets the look of the steps before they start.
    public void Configure(float stridePerStep, float basePitch, float quiet, float loud, Vector2 speeds, float blend)
    {
        strideLength = stridePerStep;
        pitch = basePitch;
        quietVolume = quiet;
        loudVolume = loud;
        speedRange = speeds;
        spatialBlend = blend;
        ApplySourceSettings();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        source = GameAudio.Route(gameObject.AddComponent<AudioSource>());
        source.playOnAwake = false;
        ApplySourceSettings();

        // Start part-way through a stride, so a group of enemies setting off together don't
        // all step in time.
        travelled = Random.value * strideLength;
    }

    private void ApplySourceSettings()
    {
        if (source == null)
            return;

        source.spatialBlend = spatialBlend;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = fullVolumeDistance;
        source.maxDistance = silentDistance;
        source.dopplerLevel = 0f;
    }

    private void Update()
    {
        Vector3 velocity = rb.linearVelocity;
        float speed = new Vector2(velocity.x, velocity.z).magnitude;

        if (speed < minSpeed || Time.deltaTime <= 0f)
        {
            // Standing still: the next step lands soon after setting off again.
            travelled = Mathf.Max(travelled, strideLength * 0.6f);
            return;
        }

        travelled += speed * Time.deltaTime;

        if (travelled < strideLength)
            return;

        travelled -= strideLength;
        Step(speed);
    }

    // Picked on the first step, as the player's Awake can run before the FloorGenerator's.
    private AudioClip[] Clips()
    {
        if (clips != null && clips.Length > 0)
            return clips;

        if (shared != null)
            return shared;

        // Generated clips are shared by everything with footsteps. Rebuilt if a previous Play
        // session's clips were destroyed (statics outlive it with domain reload off).
        if (generated == null || generated[0] == null)
            generated = new[] { ProceduralAudio.Footstep(3), ProceduralAudio.Footstep(17), ProceduralAudio.Footstep(41), ProceduralAudio.Footstep(66) };

        return generated;
    }

    private void Step(float speed)
    {
        steps ??= Clips();

        if (steps.Length == 0)
            return;

        // Never the same clip twice in a row.
        int index = Random.Range(0, steps.Length);

        if (steps.Length > 1 && index == lastClip)
            index = (index + 1) % steps.Length;

        lastClip = index;

        float effort = Mathf.InverseLerp(speedRange.x, speedRange.y, speed);
        source.pitch = pitch * (1f + effort * 0.06f) * Random.Range(1f - pitchVariation, 1f + pitchVariation);
        source.PlayOneShot(steps[index], Mathf.Lerp(quietVolume, loudVolume, effort));

        if (alertsEnemies)
            Noise.Emit(transform.position, StepLoudness(speed), Noise.Kind.Footstep);
    }

    // Walking speed is a normal step; slower (inventory open, winded) is quieter, and a sprint
    // carries furthest.
    private float StepLoudness(float speed)
    {
        float walk = (speedRange.x + speedRange.y) / 2f;

        return speed <= walk
            ? Mathf.Lerp(Noise.SlowStep, Noise.WalkStep, Mathf.InverseLerp(speedRange.x, walk, speed))
            : Mathf.Lerp(Noise.WalkStep, Noise.SprintStep, Mathf.InverseLerp(walk, speedRange.y, speed));
    }
}
