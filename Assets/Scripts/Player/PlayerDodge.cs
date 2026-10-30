using System;
using UnityEngine;

// The dodge roll: a press of Dodge throws the player into a quick roll (about 4 m in under half a
// second) the way they're moving, or the way they're facing if they're standing still. The first
// part of the roll is untouchable: bullets, globs, swings and blasts pass through (acid and fire on
// the floor still burn: PlayerHealth.TakeDamage's fromFloor). Walls stop it. There's a short wait before the next
// roll, and in a fight each roll costs some stamina (free out of combat, like sprinting); it still
// works while winded, as a last resort. No shooting, swinging or item use mid-roll, but a reload
// carries on. Dust puffs up at take-off and landing, and landing makes about as much noise as a footstep, and a roll breaks sneaking.
[RequireComponent(typeof(Rigidbody))]
public class PlayerDodge : MonoBehaviour
{
    [Tooltip("How long the roll lasts, in seconds.")]
    [SerializeField] private float rollTime = 0.45f;
    [Tooltip("How far the roll carries, in metres.")]
    [SerializeField] private float rollDistance = 4f;
    [Tooltip("How long from the start of the roll the player can't be hurt.")]
    [SerializeField] private float invulnerableTime = 0.3f;
    [Tooltip("Wait after a roll ends before the next one.")]
    [SerializeField] private float cooldown = 0.35f;
    [Tooltip("Stamina a roll costs in a fight.")]
    [SerializeField] private float staminaCost = 20f;

    private const float TumbleLoaderCost = 10f;

    private Rigidbody rb;
    private PlayerStamina stamina;
    private PlayerHealth health;
    private float startTime = -100f;
    private float readyAt;
    private CollisionDetectionMode restoreMode;
    private bool holdDodge;
    private bool refunded;

    public bool Rolling { get; private set; }
    public bool Invulnerable => Rolling && Time.time < startTime + invulnerableTime;
    public float Progress => Rolling ? Mathf.Clamp01((Time.time - startTime) / rollTime) : 0f;
    public Vector3 Direction { get; private set; } = Vector3.forward;

    public event Action<PlayerDodge> Started;
    public event Action<PlayerDodge> Landed;

    private float StaminaCost => staminaCost + (PlayerItems.Has(RunItem.TumbleLoader) ? TumbleLoaderCost : 0f);

    public static PlayerDodge For(GameObject owner)
    {
        PlayerDodge dodge = owner.GetComponent<PlayerDodge>();
        return dodge != null ? dodge : owner.AddComponent<PlayerDodge>();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        stamina = GetComponent<PlayerStamina>();
        health = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        if (Rolling && (GameFlow.MovementBlocked || (health != null && health.IsDead)))
        {
            End(false);
            return;
        }

        if (Rolling && Time.time >= startTime + rollTime)
            End(true);

        // Space / A also confirm menus: the press that closes one is still down when control
        // comes back, so wait for it to be let go (as PlayerShooting does with Fire).
        if (GameFlow.InputBlocked)
        {
            holdDodge = true;
            return;
        }

        if (holdDodge && !GameInput.Dodge.IsPressed())
            holdDodge = false;

        if (holdDodge || !GameInput.Dodge.WasPressedThisFrame())
            return;

        Vector2 input = GameInput.Move.ReadValue<Vector2>();
        TryRoll(input.sqrMagnitude > 0.04f ? new Vector3(input.x, 0f, input.y) : transform.forward);
    }

    // Starts a roll that way, if one can start now. Also used by tests.
    public bool TryRoll(Vector3 direction)
    {
        direction.y = 0f;

        if (Rolling || Time.time < readyAt || GameFlow.MovementBlocked || (health != null && health.IsDead) || direction.sqrMagnitude < 0.0001f)
            return false;

        if (stamina != null && stamina.InCombat && !stamina.Boosted)
            stamina.Spend(StaminaCost);

        Direction = direction.normalized;
        Rolling = true;
        refunded = false;
        startTime = Time.time;
        restoreMode = rb.collisionDetectionMode;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        Dust();
        Started?.Invoke(this);
        return true;
    }

    private void FixedUpdate()
    {
        if (Rolling)
            rb.linearVelocity = Direction * (rollDistance / rollTime);
    }

    private void End(bool landed)
    {
        Rolling = false;
        readyAt = refunded ? Time.time : Time.time + cooldown;
        rb.collisionDetectionMode = restoreMode;
        rb.linearVelocity = landed ? Direction * 1.5f : Vector3.zero;

        if (!landed)
            return;

        Noise.Emit(transform.position, Noise.WalkStep, Noise.Kind.Footstep);
        Dust();
        Landed?.Invoke(this);
    }

    // Close Call: this roll's stamina back and no wait after it, once a roll.
    public void Refund()
    {
        if (!Rolling || refunded)
            return;

        refunded = true;

        if (stamina != null)
            stamina.Refund(StaminaCost);

        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-fit", 3, ProceduralAudio.AttachmentFit)), 0.5f, 1.5f);
    }

    // A puff of grey grit kicked up off the floor at take-off and landing.
    private void Dust()
    {
        if (!ImpactEffects.TryGetMaterials(out ImpactEffects.Materials materials))
            return;

        Vector3 feet = transform.position;
        feet.y = 0.05f;
        ImpactEffects.Debris(feet, Vector3.up - Direction * 0.5f, 7, new Color(0.42f, 0.4f, 0.37f), new Color(0.6f, 0.58f, 0.54f), materials);
    }
}
