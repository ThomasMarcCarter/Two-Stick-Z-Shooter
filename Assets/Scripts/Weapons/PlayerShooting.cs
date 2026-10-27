using UnityEngine;

// Runs after CameraFollow so aim is worked out from where the camera ends up this frame,
// matching the crosshair instead of lagging a frame behind it.
[DefaultExecutionOrder(100)]
public class PlayerShooting : MonoBehaviour
{
    private Gun equippedGun;
    [SerializeField] private float rotationSpeed = 20f;
    [Tooltip("Ignore the cursor when it's this close to the player's centre (metres). Keep it tiny: inside it the player doesn't turn, and shots go the way they face (Gun.Fire).")]
    [SerializeField] private float aimDeadZone = 0.05f;

    private Rigidbody rb;
    private Quaternion targetRotation;
    private QuickBar quickBar;
    private bool holdFire;


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        targetRotation = transform.rotation;
        quickBar = QuickBar.For(gameObject);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    private void Update()
    {
        // A click that closes a menu or screen (taking the last item from a crate, a menu
        // button) is still held when control comes back: wait for it to be let go first.
        if (GameFlow.InputBlocked)
        {
            holdFire = true;
            return;
        }

        if (holdFire && !GameInput.Fire.IsPressed())
            holdFire = false;

        if (equippedGun == null)
            return;

        // Hands full with a medkit.
        if (quickBar != null && quickBar.IsUsing)
            return;

        if (GameInput.Reload.WasPressedThisFrame())
        {
            equippedGun.TryReload();
        }

        if (holdFire)
            return;

        if (equippedGun.GetFireMode() == Gun.FireMode.Semi)
        {
            if (GameInput.Fire.WasPressedThisFrame())
            {
                equippedGun.TryFire();
            }
        }
        else if (equippedGun.GetFireMode() == Gun.FireMode.Auto)
        {
            if (GameInput.Fire.IsPressed())
            {
                equippedGun.TryFire();
            }
        }
    }

    private void LateUpdate()
    {
        if (GameFlow.InputBlocked)
            return;

        CalculateAim();
    }

    public void SetEquippedGun(Gun gun)
    {
        equippedGun = gun;
    }

    public Gun EquippedGun => equippedGun;

    private void CalculateAim()
    {
        // The same point shots go to, so the player faces where they land, whatever the
        // camera's tilt.
        if (!Gun.PointerAimPoint(out Vector3 mouseWorldPosition))
            return;

        Vector3 direction = mouseWorldPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < aimDeadZone * aimDeadZone)
            return;

        targetRotation = Quaternion.LookRotation(direction);
    }

    private void FixedUpdate()
    {
        // Bumping into enemies and walls adds spin; aim alone decides which way the player faces.
        rb.angularVelocity = Vector3.zero;

        rb.MoveRotation(
            Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            )
        );
    }
}