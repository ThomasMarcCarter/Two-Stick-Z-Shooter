using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float deceleration = 5f;
    [Tooltip("Speed multiplier while sprinting (needs a PlayerStamina on the player).")]
    [SerializeField] private float sprintMultiplier = 1.6f;
    [Tooltip("Acceleration multiplier while sprinting, so the burst kicks in quickly.")]
    [SerializeField] private float sprintAcceleration = 2f;
    [Tooltip("Speed multiplier while winded: after running stamina dry, until it recovers.")]
    [SerializeField] private float windedMultiplier = 0.5f;
    [Tooltip("Speed multiplier while the inventory is open (the game keeps running).")]
    [SerializeField] private float inventoryMultiplier = 0.5f;
    [Tooltip("Speed multiplier while sneaking (toggled with Sneak; a Sprint press breaks it). Slower steps are quieter to enemies.")]
    [SerializeField] private float sneakMultiplier = 0.5f;
    [Tooltip("Speed multiplier on adrenaline.")]
    [SerializeField] private float boostMultiplier = 1.15f;

    // Toggled on and off with Sneak; pressing Sprint breaks it.
    public bool IsSneaking { get; private set; }

    private Rigidbody rb;
    private PlayerStamina stamina;
    private QuickBar quickBar;
    private PlayerDodge dodge;
    private bool sprinting;
    private bool winded;
    private Vector3 movement;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        stamina = GetComponent<PlayerStamina>();
        quickBar = QuickBar.For(gameObject);
        dodge = PlayerDodge.For(gameObject);
    }

    private void Update()
    {
        // WASD comes in normalised; a stick can be part-way, for walking slower.
        Vector2 input = Vector2.ClampMagnitude(GameInput.Move.ReadValue<Vector2>(), 1f);

        movement = GameFlow.MovementBlocked ? Vector3.zero : new Vector3(input.x, 0f, input.y);
        UpdateSneak();

        Vector3 velocity = rb.linearVelocity;
        float speed = new Vector2(velocity.x, velocity.z).magnitude;
        sprinting = stamina != null && stamina.UpdateSprint(movement.sqrMagnitude > 0.01f, speed, !IsSneaking);
        winded = stamina != null && stamina.Exhausted;
    }

    // Sneak flips on each press; a Sprint press snaps out of it (holding Sprint from before
    // sneaking doesn't, so turning sneak on mid-sprint slows down until Sprint is pressed again).
    private void UpdateSneak()
    {
        if (GameFlow.InputBlocked)
            return;

        if (GameInput.Sneak.WasPressedThisFrame())
            IsSneaking = !IsSneaking;

        if (IsSneaking && (GameInput.Sprint.WasPressedThisFrame() || dodge.Rolling))
            IsSneaking = false;
    }

    private void FixedUpdate()
    {
        // PlayerDodge drives the body through a roll.
        if (dodge.Rolling)
            return;

        float multiplier = winded ? windedMultiplier : sprinting ? sprintMultiplier : 1f;

        if (IsSneaking)
            multiplier = Mathf.Min(multiplier, sneakMultiplier);

        // Slowed while rummaging in the inventory. Not stacked with being winded, so it's
        // never slower than the slower of the two.
        if (InventoryScreen.IsOpen)
            multiplier = Mathf.Min(multiplier, inventoryMultiplier);

        // Slowed while using a medkit; quicker on adrenaline.
        if (quickBar != null)
            multiplier *= quickBar.SpeedMultiplier;

        if (stamina != null && stamina.Boosted)
            multiplier *= boostMultiplier;
        Vector3 targetVelocity = movement * moveSpeed * multiplier;

        float currentAcceleration = movement.magnitude > 0
            ? acceleration * (sprinting ? sprintAcceleration : 1f)
            : deceleration;

        rb.linearVelocity = Vector3.MoveTowards(
            rb.linearVelocity,
            targetVelocity,
            currentAcceleration * Time.fixedDeltaTime
        );
    }
}