using UnityEngine;

// Runs after CameraFollow so aim is worked out from where the camera ends up this frame,
// matching the crosshair instead of lagging a frame behind it.
[DefaultExecutionOrder(100)]
public class PlayerShooting : MonoBehaviour
{
    private Gun equippedGun;
    [SerializeField] private float rotationSpeed = 20f;
    [Tooltip("Ignore the cursor when it's this close to the player, where tiny movements swing the aim wildly.")]
    [SerializeField] private float aimDeadZone = 0.25f;

    private Camera mainCamera;
    private Rigidbody rb;
    private Quaternion targetRotation;
    private QuickBar quickBar;


    private void Awake()
    {
        mainCamera = Camera.main;
        rb = GetComponent<Rigidbody>();
        targetRotation = transform.rotation;
        quickBar = QuickBar.For(gameObject);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    private void Update()
    {
        if (GameFlow.InputBlocked)
            return;

        if (equippedGun == null)
            return;

        // Hands full with a medkit.
        if (quickBar != null && quickBar.IsUsing)
            return;

        if (GameInput.Reload.WasPressedThisFrame())
        {
            equippedGun.TryReload();
        }

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
        Ray ray = mainCamera.ScreenPointToRay(GameInput.Pointer);

        // At the height shots fly, so what's under the cursor is what gets aimed at, whatever
        // the camera's tilt.
        Plane groundPlane = new Plane(Vector3.up, Vector3.up * Gun.ShotHeight);

        if (!groundPlane.Raycast(ray, out float distance))
            return;

        Vector3 mouseWorldPosition = ray.GetPoint(distance);

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