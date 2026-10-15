using System;
using UnityEngine;

// Player hit points. Each hit knocks the player back, plays a hurt sound and gives a short window
// of invulnerability, during which the player model blinks. At zero the controls lock and the
// death screen shows. Medkits heal through Heal.
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 6;
    [Tooltip("Seconds after a hit during which the player can't be hurt again.")]
    [SerializeField] private float invulnerableTime = 0.5f;
    [SerializeField] private float knockback = 7f;
    [SerializeField] private float blinkRate = 14f;

    [Header("Sound")]
    [Tooltip("Played when the player is hurt. Leave empty to use a generated sound.")]
    [SerializeField] private AudioClip hurtSound;
    [SerializeField, Range(0f, 1f)] private float hurtVolume = 0.7f;

    [Header("Death")]
    [SerializeField] private DeathScreen deathScreen;

    public int Health { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDead { get; private set; }
    public bool IsFull => Health >= maxHealth;
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    // Debug: ignore all damage.
    public bool GodMode { get; set; }

    public event Action<PlayerHealth> Damaged;
    public event Action<PlayerHealth> Healed;
    public event Action<PlayerHealth> Died;

    private Rigidbody rb;
    private Renderer body;
    private float invulnerableUntil;

    private void Awake()
    {
        Health = maxHealth;
        rb = GetComponent<Rigidbody>();
        body = GetComponent<Renderer>();
    }

    private void Update()
    {
        if (body == null || IsDead)
            return;

        body.enabled = !IsInvulnerable || Mathf.Repeat(Time.time * blinkRate, 1f) < 0.5f;
    }

    // Returns false if the hit didn't land (dead, or still invulnerable from the last one).
    public bool TakeDamage(int amount, Vector3 source)
    {
        if (IsDead || IsInvulnerable || GodMode || amount <= 0)
            return false;

        Health = Mathf.Max(0, Health - amount);
        invulnerableUntil = Time.time + invulnerableTime;

        Vector3 away = transform.position - source;
        away.y = 0f;

        if (away.sqrMagnitude > 0.0001f)
            rb.linearVelocity = away.normalized * knockback;

        AudioClip clip = hurtSound != null ? hurtSound : SoundFX.Pick(SoundFX.Clips("player.hurt", 3, ProceduralAudio.PlayerHurt));
        SoundFX.Play2D(clip, hurtVolume, UnityEngine.Random.Range(0.94f, 1.06f));

        Damaged?.Invoke(this);

        if (Health == 0)
            Die();

        return true;
    }

    // Debug: takes health off straight away, ignoring the invulnerability window, without
    // knockback. Plays the hurt sound like a real hit.
    public void ForceDamage(int amount)
    {
        invulnerableUntil = 0f;
        TakeDamage(amount, transform.position);
    }

    // Returns how much health was actually restored (none if dead or already full).
    public int Heal(int amount)
    {
        if (IsDead || amount <= 0)
            return 0;

        int healed = Mathf.Min(amount, maxHealth - Health);

        if (healed <= 0)
            return 0;

        Health += healed;
        Healed?.Invoke(this);
        return healed;
    }

    public void RestoreFullHealth()
    {
        if (!IsDead)
            Health = maxHealth;
    }

    private void Die()
    {
        IsDead = true;
        body.enabled = true;
        rb.linearVelocity = Vector3.zero;

        // Lock the controls.
        GetComponent<PlayerMovement>().enabled = false;
        GetComponent<PlayerShooting>().enabled = false;
        GetComponent<PlayerWeapon>().enabled = false;

        Died?.Invoke(this);

        if (deathScreen != null)
            deathScreen.Show();
    }
}
