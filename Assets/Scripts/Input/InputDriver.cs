using UnityEngine;
using UnityEngine.InputSystem;

// Runs before everything else each frame: works out which device the player is using, and
// where GameInput.Pointer is. With a mouse that's the mouse through the CRT curve; with a
// controller it's a point around the player in the right stick's direction (twin-stick aim),
// which stays put when the stick is released.
[DefaultExecutionOrder(-100)]
public class InputDriver : MonoBehaviour
{
    [SerializeField] private Transform player;
    [Tooltip("How far from the player the controller aim point sits, as a fraction of screen height.")]
    [SerializeField] private float aimRadius = 0.22f;
    [Tooltip("Right stick movement below this is ignored.")]
    [SerializeField] private float aimDeadZone = 0.3f;
    [Tooltip("Stick or mouse movement below this doesn't count as switching device.")]
    [SerializeField] private float switchThreshold = 0.4f;

    private Camera cam;
    private Vector2 aimDirection = Vector2.up;
    private Vector2 lastPointer;

    private void Awake()
    {
        cam = Camera.main;

        // Make sure the actions exist and are enabled from the first frame.
        GameInput.Asset.Enable();
    }

    private void Update()
    {
        DetectDevice();

        Vector2 pointer = GameInput.UsingGamepad ? GamepadPointer() : CRTEffect.ScreenToSource(MousePosition());

        GameInput.PointerMovedThisFrame = (pointer - lastPointer).sqrMagnitude > 0.25f;
        GameInput.Pointer = pointer;
        lastPointer = pointer;
    }

    private void DetectDevice()
    {
        Gamepad gamepad = Gamepad.current;

        if (gamepad != null && GamepadUsed(gamepad))
        {
            if (!GameInput.UsingGamepad && player != null)
            {
                // Start aiming the way the player already faces.
                aimDirection = new Vector2(player.forward.x, player.forward.z).normalized;
            }

            GameInput.ActiveDevice = InputDeviceKind.Gamepad;
            return;
        }

        bool keyboardUsed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool mouseUsed = Mouse.current != null
            && (Mouse.current.delta.ReadValue().magnitude > switchThreshold * 10f
                || Mouse.current.scroll.ReadValue().sqrMagnitude > 0f
                || Mouse.current.leftButton.wasPressedThisFrame
                || Mouse.current.rightButton.wasPressedThisFrame);

        if (keyboardUsed || mouseUsed)
            GameInput.ActiveDevice = InputDeviceKind.KeyboardMouse;
    }

    private bool GamepadUsed(Gamepad gamepad)
    {
        if (gamepad.leftStick.ReadValue().magnitude > switchThreshold) return true;
        if (gamepad.rightStick.ReadValue().magnitude > switchThreshold) return true;
        if (gamepad.leftTrigger.ReadValue() > switchThreshold) return true;
        if (gamepad.rightTrigger.ReadValue() > switchThreshold) return true;

        foreach (var control in gamepad.allControls)
        {
            if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    private Vector2 GamepadPointer()
    {
        Vector2? parked = GameInput.GamepadPointerOverride?.Invoke();

        if (parked.HasValue)
            return parked.Value;

        Vector2 stick = GameInput.Aim.ReadValue<Vector2>();

        if (stick.magnitude > aimDeadZone)
            aimDirection = stick.normalized;

        if (player == null || cam == null)
            return new Vector2(Screen.width, Screen.height) / 2f;

        // The ground point under the player, since aiming is worked out on the ground plane.
        Vector3 ground = new Vector3(player.position.x, 0f, player.position.z);
        Vector2 playerOnScreen = cam.WorldToScreenPoint(ground);

        return playerOnScreen + aimDirection * (Screen.height * aimRadius);
    }

    private static Vector2 MousePosition()
    {
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
    }
}
