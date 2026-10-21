using UnityEngine;

// The bloater: slow and swollen. When it gets close to the player it stops, swells and bursts
// a moment later, hurting everything around it, zombies included. Killing it first bursts it
// too (a beat after it drops, so shooting one in a crowd sets off the crowd), so the safe way
// to deal with one is from range. It bursts in a spray of acid (Blast.Settings.Acid: the pixel
// acid burst) and leaves an AcidPool that burns the player and other zombies for a few seconds.
[RequireComponent(typeof(Enemy))]
public class EnemyBloater : MonoBehaviour
{
    [Tooltip("Starts to swell when the player is this close.")]
    [SerializeField] private float triggerDistance = 1.6f;
    [SerializeField] private float fuseTime = 0.75f;
    [Tooltip("When killed, it bursts this long after.")]
    [SerializeField] private float deathDelay = 0.15f;

    public static Blast.Settings Burst => new Blast.Settings
    {
        Radius = 2.8f,
        MaxDamage = 4,
        PlayerRadius = 2.4f,
        PlayerDamage = 2,
        PlayerDamageOuter = 2,
        Push = 8f,
        Loudness = 1f,
        Shake = 0.25f,
        BreaksProps = true,
        HotColour = new Color(0.8f, 1f, 0.4f),
        CoolColour = new Color(0.25f, 0.45f, 0.05f),
        LightColour = new Color(0.6f, 1f, 0.3f),
        Acid = true
    };

    private Enemy enemy;
    private Vector3 baseScale;
    private float burstAt = -1f;
    private bool burst;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        enemy.Died += OnDied;
    }

    private void OnDisable()
    {
        enemy.Died -= OnDied;
    }

    private void Update()
    {
        if (burstAt < 0f)
        {
            if (enemy.IsActive && enemy.Target != null && FlatDistance(transform.position, enemy.Target.position) <= triggerDistance)
            {
                burstAt = Time.time + fuseTime;
                enemy.SpeedMultiplier = 0.2f;
                SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("bloater.swell", 2, ProceduralAudio.BloaterSwell)), transform.position, 0.8f);
            }

            return;
        }

        // Swelling, faster and faster, until it goes.
        float t = 1f - (burstAt - Time.time) / fuseTime;
        float wobble = Mathf.Sin(Time.time * Mathf.Lerp(20f, 60f, t)) * 0.05f;
        transform.localScale = baseScale * (1f + t * 0.35f + wobble);

        if (Time.time >= burstAt)
        {
            burst = true;
            Pop(transform.position, 0f);
            enemy.Kill();
        }
    }

    private void OnDied(Enemy dead)
    {
        if (burst)
            return;

        burst = true;
        Pop(dead.transform.position, deathDelay);
    }

    private void Pop(Vector3 position, float delay)
    {
        Transform room = enemy.Room != null ? enemy.Room.transform : null;
        DelayedBlast.Start(position, room, Burst, delay);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
