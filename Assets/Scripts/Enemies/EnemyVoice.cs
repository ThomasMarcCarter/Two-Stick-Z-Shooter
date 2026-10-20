using UnityEngine;

// An enemy's voice from its ZombieSoundPack: a groan every few seconds while it's alive, a grunt
// when it's hurt, and a death sound when it dies. Heard from where the enemy is, like its
// footsteps, so groans give away zombies around corners and behind doors. Each enemy is pitched
// slightly differently so a room of them doesn't sound like copies.
[RequireComponent(typeof(Enemy))]
public class EnemyVoice : MonoBehaviour
{
    [Tooltip("Seconds between groans while alive.")]
    [SerializeField] private Vector2 aliveInterval = new Vector2(3f, 9f);
    [Tooltip("Hits closer together than this make one grunt, so a shotgun blast isn't eight.")]
    [SerializeField] private float hitInterval = 0.25f;

    private ZombieSoundPack pack;
    private Enemy enemy;
    private AudioSource source;
    private float pitch = 1f;
    private float nextGroan;
    private float lastHit = -10f;

    // Set by FloorGenerator straight after adding the component. basePitch: lower for bigger
    // enemies (the boss).
    public void Configure(ZombieSoundPack soundPack, float basePitch)
    {
        pack = soundPack;
        pitch = basePitch * Random.Range(0.92f, 1.08f);

        // Not all at once when a floor starts.
        nextGroan = Time.time + Random.Range(0.5f, aliveInterval.y);
    }

    public ZombieSoundPack Pack => pack;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();

        source = GameAudio.Route(gameObject.AddComponent<AudioSource>());
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = SoundFX.NearDistance;
        source.maxDistance = SoundFX.FarDistance;
        source.dopplerLevel = 0f;
    }

    private void OnEnable()
    {
        enemy.Damaged += OnDamaged;
        enemy.Died += OnDied;
    }

    private void OnDisable()
    {
        enemy.Damaged -= OnDamaged;
        enemy.Died -= OnDied;
    }

    private void Update()
    {
        if (pack == null || Time.time < nextGroan)
            return;

        nextGroan = Time.time + Random.Range(aliveInterval.x, aliveInterval.y);

        // Don't talk over a grunt, and don't bother if it's too far away to be heard.
        if (source.isPlaying || !InEarshot())
            return;

        Say(ZombieSoundPack.Pick(pack.alive), pack.aliveVolume);
    }

    private void OnDamaged(Enemy hurt)
    {
        if (pack == null || Time.time - lastHit < hitInterval)
            return;

        lastHit = Time.time;

        // A grunt cuts off a groan.
        source.Stop();
        Say(ZombieSoundPack.Pick(pack.hit), pack.hitVolume);
        nextGroan = Mathf.Max(nextGroan, Time.time + 1.5f);
    }

    // The enemy is destroyed straight after this, so the death sound plays from a short-lived
    // object of its own where the enemy fell.
    private void OnDied(Enemy dead)
    {
        if (pack == null || !InEarshot())
            return;

        SoundFX.PlayAt(ZombieSoundPack.Pick(pack.death), transform.position, pack.deathVolume, pitch);
    }

    private void Say(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        source.pitch = pitch;
        source.clip = clip;
        source.volume = volume;
        source.Play();
    }

    private bool InEarshot()
    {
        AudioListener listener = FindListener();
        return listener == null || Vector3.Distance(listener.transform.position, transform.position) < SoundFX.FarDistance;
    }

    private static AudioListener cachedListener;

    private static AudioListener FindListener()
    {
        if (cachedListener == null)
            cachedListener = FindAnyObjectByType<AudioListener>();

        return cachedListener;
    }
}
