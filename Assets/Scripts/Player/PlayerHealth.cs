using System;
using UnityEngine;
using Random = UnityEngine.Random;

// Player hit points. Each hit knocks the player back, plays a hurt sound and gives a short window
// of invulnerability (from everything, not just whatever landed the hit, so a crowd of zombies
// can't take several points off at once), during which the player model blinks. At zero the controls lock and the
// death screen shows. Medkits heal through Heal.
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 6;
    [Tooltip("Seconds after a hit during which the player can't be hurt again, by anything (not per enemy), so a crowd can't land its hits all at once.")]
    [SerializeField] private float invulnerableTime = 0.9f;
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

    // Debug: ignore all damage, and the held gun's magazine never runs dry (Gun).
    public bool GodMode { get; set; }

    // Out of sight (riding in the hideout's truck, TruckBay): the body isn't drawn.
    public bool Hidden { get; set; }

    // Extra vitals every medkit gives this run (the cook's broth).
    public int MedkitBonus { get; set; }

    public event Action<PlayerHealth> Damaged;
    public event Action<PlayerHealth> Healed;
    public event Action<PlayerHealth> Died;

    private Rigidbody rb;
    private Renderer[] bodies = new Renderer[0];
    private float invulnerableUntil;

    private void Awake()
    {
        Health = maxHealth;
        rb = GetComponent<Rigidbody>();
        Renderer own = GetComponent<Renderer>();
        bodies = own != null ? new[] { own } : new Renderer[0];
    }

    private void Update()
    {
        if (IsDead)
            return;

        bool visible = !Hidden && (!IsInvulnerable || Mathf.Repeat(Time.time * blinkRate, 1f) < 0.5f);

        foreach (Renderer renderer in bodies)
            renderer.enabled = visible;
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

    // The renderers that blink while invulnerable: the capsule, or the model that replaced it.
    public void SetBodyRenderers(Renderer[] renderers)
    {
        bodies = renderers ?? new Renderer[0];
    }

    // Raises max health (and current health with it), e.g. from the hideout's Toughness upgrade.
    public void AddMaxHealth(int amount)
    {
        if (amount <= 0)
            return;

        maxHealth += amount;
        Health += amount;
        Healed?.Invoke(this);
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

    // The controls lock, the model falls (CharacterAnimator.Die), the gun clatters to the floor,
    // and the death screen fades in over the room a moment later (DeathScreen.BeginDeath).
    private void Die()
    {
        IsDead = true;
        foreach (Renderer renderer in bodies)
            renderer.enabled = true;
        rb.linearVelocity = Vector3.zero;

        // Lock the controls.
        GetComponent<PlayerMovement>().enabled = false;
        GetComponent<PlayerShooting>().enabled = false;
        PlayerWeapon weapon = GetComponent<PlayerWeapon>();
        weapon.enabled = false;

        CharacterAnimator animator = GetComponentInChildren<CharacterAnimator>();

        if (animator != null)
            animator.Die();

        DropGun(weapon);
        CameraFollow.Shake(0.35f);
        Died?.Invoke(this);

        if (deathScreen != null)
            deathScreen.BeginDeath();
    }

    // The gun in hand falls out of it: a loose copy of its model tumbles to the floor (just for
    // show; the real one is hidden).
    private static void DropGun(PlayerWeapon weapon)
    {
        Gun gun = weapon != null ? weapon.Active.Gun : null;
        Transform model = gun != null ? gun.transform.Find("Model") : null;

        if (model == null)
            return;

        GameObject dropped = Instantiate(model.gameObject, model.position, model.rotation, FloorPickups.RoomAt(model.position));
        dropped.name = "Dropped Gun";
        dropped.transform.localScale = model.lossyScale;
        gun.gameObject.SetActive(false);

        Bounds bounds = new Bounds(dropped.transform.position, Vector3.zero);

        foreach (Renderer part in dropped.GetComponentsInChildren<Renderer>())
            bounds.Encapsulate(part.bounds);

        BoxCollider box = dropped.AddComponent<BoxCollider>();
        box.center = dropped.transform.InverseTransformPoint(bounds.center);
        Vector3 size = dropped.transform.InverseTransformVector(bounds.size);
        box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

        Rigidbody body = dropped.AddComponent<Rigidbody>();
        body.mass = 2f;
        body.linearVelocity = weapon.transform.forward * 1.5f + Vector3.up * 1.5f;
        body.angularVelocity = Random.onUnitSphere * 6f;
    }
}
