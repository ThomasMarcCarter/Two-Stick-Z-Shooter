using UnityEngine;

// Sprint stamina. Holding Sprint (Shift / LT) while actually moving (not pushing into a wall)
// drains it and lets PlayerMovement go faster. It refills after a short pause once sprinting
// stops. Running it dry means a longer pause, and the player is winded (slowed, no sprinting)
// until it has refilled to the recover fraction.
public class PlayerStamina : MonoBehaviour
{
    [SerializeField] private float maxStamina = 100f;
    [Tooltip("Stamina used per second of sprinting. Max / drain = how long a full bar lasts.")]
    [SerializeField] private float drainPerSecond = 40f;
    [SerializeField] private float regenPerSecond = 30f;
    [Tooltip("Pause before refilling after a sprint ends normally.")]
    [SerializeField] private float regenDelay = 0.4f;
    [Tooltip("Longer pause before refilling after running completely dry.")]
    [SerializeField] private float exhaustedRegenDelay = 1.2f;
    [Tooltip("After running dry, the player is winded until stamina is back to this fraction.")]
    [SerializeField, Range(0f, 1f)] private float recoverFraction = 0.25f;
    [Tooltip("Sprinting only counts (and drains) above this speed, so pushing into a wall costs nothing.")]
    [SerializeField] private float minSprintSpeed = 1.5f;

    public float Max => maxStamina;
    public float Current { get; private set; }
    public float Fraction => maxStamina > 0f ? Current / maxStamina : 0f;
    public bool IsSprinting { get; private set; }
    public float RecoverFraction => recoverFraction;

    // Ran dry and hasn't recovered enough to sprint again; PlayerMovement slows the player.
    public bool Exhausted { get; private set; }

    // Adrenaline: sprinting costs nothing and the player is a little faster, for a while.
    public bool Boosted => Time.time < boostUntil;
    public float BoostLeft => Mathf.Max(0f, boostUntil - Time.time);

    // Seconds until refilling starts (0 while refilling or full).
    public float RegenWait => Mathf.Max(0f, regenStartTime - Time.time);

    private float regenStartTime;
    private float boostUntil;

    private void Awake()
    {
        Current = maxStamina;
    }

    // Called by PlayerMovement each frame with whether the player is trying to move, how fast
    // they're actually going, and whether sprinting is allowed (not while sneaking).
    public bool UpdateSprint(bool moving, float speed, bool allowed = true)
    {
        bool wantsSprint = allowed && moving && !GameFlow.InputBlocked && GameInput.Sprint.IsPressed();

        IsSprinting = wantsSprint && !Exhausted && Current > 0f;

        // The boost applies straight away, but stamina is only spent while actually moving
        // quickly, so pushing into a wall or an enemy costs nothing (and doesn't hold up regen).
        bool draining = IsSprinting && speed >= minSprintSpeed && !Boosted;

        if (draining)
        {
            Current = Mathf.Max(0f, Current - drainPerSecond * Time.deltaTime);
            regenStartTime = Time.time + regenDelay;

            if (Current <= 0f)
            {
                Exhausted = true;
                IsSprinting = false;
                regenStartTime = Time.time + exhaustedRegenDelay;
            }
        }
        else if (Time.time >= regenStartTime && Current < maxStamina)
        {
            Current = Mathf.Min(maxStamina, Current + regenPerSecond * Time.deltaTime);
        }

        if (Exhausted && Current >= maxStamina * recoverFraction)
            Exhausted = false;

        return IsSprinting;
    }

    // Uses some up in one go (a melee swing), holding off the refill like sprinting does.
    // Running it dry this way winds the player too.
    // Scales the stamina pool (and refills it), e.g. from the hideout's Conditioning upgrade.
    public void ScaleMax(float multiplier)
    {
        maxStamina *= Mathf.Max(0.1f, multiplier);
        Current = maxStamina;
    }

    public void Spend(float amount)
    {
        if (amount <= 0f)
            return;

        Current = Mathf.Max(0f, Current - amount);
        regenStartTime = Time.time + regenDelay;

        if (Current <= 0f)
        {
            Exhausted = true;
            regenStartTime = Time.time + exhaustedRegenDelay;
        }
    }

    // Fills the bar and stops it draining for some seconds.
    public void Boost(float seconds)
    {
        RestoreFull();
        boostUntil = Mathf.Max(boostUntil, Time.time + seconds);
    }

    public void RestoreFull()
    {
        Current = maxStamina;
        Exhausted = false;
        IsSprinting = false;
    }
}
