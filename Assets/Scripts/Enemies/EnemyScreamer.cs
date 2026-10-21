using UnityEngine;

// The screamer: when it spots the player it stops and shrieks, a noise loud enough that every
// zombie in earshot comes for where the player is standing. It can shriek again after a cooldown
// if it loses them and spots them again. Killing it before it sees you keeps the room quiet.
[RequireComponent(typeof(Enemy))]
public class EnemyScreamer : MonoBehaviour
{
    [SerializeField] private float cooldown = 10f;
    [Tooltip("How far the shriek carries, as a multiple of a gunshot.")]
    [SerializeField] private float loudness = 2.2f;
    [SerializeField] private float screamTime = 0.8f;

    private Enemy enemy;
    private float nextScream;
    private float screamingUntil;
    private Vector3 baseScale;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        enemy.SpottedPlayer += OnSpotted;
    }

    private void OnDisable()
    {
        enemy.SpottedPlayer -= OnSpotted;
    }

    private void OnSpotted(Enemy _)
    {
        if (Time.time < nextScream || enemy.Target == null)
            return;

        nextScream = Time.time + cooldown;
        screamingUntil = Time.time + screamTime;

        // Stands its ground to scream.
        enemy.Knock(Vector3.zero, screamTime);

        ScreamSound.PlayAt(transform.position, 1f);
        Noise.Emit(enemy.Target.position, Noise.Gunshot * loudness, Noise.Kind.Scream);
        CameraFollow.Shake(0.12f);
    }

    // A shudder while it screams, so it's clear which one did it.
    private void Update()
    {
        if (Time.time >= screamingUntil)
        {
            transform.localScale = baseScale;
            return;
        }

        float shake = Mathf.Sin(Time.time * 60f) * 0.06f;
        transform.localScale = new Vector3(baseScale.x * (1f + shake), baseScale.y * (1.12f - shake), baseScale.z * (1f + shake));
    }
}
