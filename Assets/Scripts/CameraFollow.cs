using UnityEngine;

// The game camera: follows the player at a fixed height, leaning a little towards the cursor,
// kept inside the current room so it never shows the void past the walls, and shaken by
// explosions and heavy hits. Works with the orthographic camera the game uses (Gungeon-style:
// the camera's size sets how much floor is in view) and with a perspective one. It can lean back
// from straight down by `tilt` degrees to show the faces of the walls; all the following and
// clamping is done on the floor point under the middle of the screen, so any tilt works.
public class CameraFollow : MonoBehaviour
{
    // How tall a metre of height looks on screen against a metre across (the tilt foreshortens
    // it); sprites stand stretched by the inverse so their pixels come out square.
    public static float VerticalScale { get; private set; } = 1f;

    // The way the view looks into the scene: moving something along it doesn't move it on screen.
    public static Vector3 ViewDirection { get; private set; } = Vector3.down;

    // How much the picture is stretched up the screen beyond the camera's own plane (always 1
    // for this camera; sprites allow for other projections).
    public static float ScreenStretch => 1f;

    [SerializeField] private Transform target;
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private float mouseLookAhead = 0.5f;

    [Tooltip("Degrees the camera leans back from looking straight down (0 = straight down). "
        + "More shows more of the walls' faces and less of the floor's depth.")]
    [SerializeField, Range(0f, 60f)] private float tilt = 0f;

    [Tooltip("Optional. When set, the camera stays inside the current room's area.")]
    [SerializeField] private FloorGenerator floor;

    [Tooltip("How quickly a shake (from an explosion) dies away, per second.")]
    [SerializeField] private float shakeDecay = 2.5f;

    private Camera cam;
    private static float shake;
    private Vector3 shakeOffset;

    // Statics outlive Play sessions with domain reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        shake = 0f;
        VerticalScale = 1f;
        ViewDirection = Vector3.down;
    }

    // Jolts the camera about, in world units, dying away over a moment. Stronger shakes win.
    public static void Shake(float amount)
    {
        shake = Mathf.Max(shake, amount);
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        // Set every frame, so the tilt can be tried out live in the Inspector.
        transform.rotation = Quaternion.Euler(90f - tilt, 0f, 0f);

        // The shake is taken off before following, so it never drags the camera anywhere.
        transform.position -= shakeOffset;

        transform.position = Vector3.Lerp(
            transform.position,
            DesiredPosition(),
            smoothSpeed * Time.deltaTime
        );

        shake = Mathf.MoveTowards(shake, 0f, shakeDecay * Time.deltaTime * Mathf.Max(0.3f, shake));
        Vector2 jolt = Random.insideUnitCircle * shake;
        shakeOffset = new Vector3(jolt.x, 0f, jolt.y);
        transform.position += shakeOffset;
        UpdateViewInfo();
    }

    private void UpdateViewInfo()
    {
        VerticalScale = Mathf.Max(0.01f, Vector3.Dot(transform.up, Vector3.up));
        ViewDirection = transform.forward;
    }

    // Jump straight to the player instead of sliding there, e.g. after moving to a new floor.
    public void SnapToTarget()
    {
        shakeOffset = Vector3.zero;
        transform.rotation = Quaternion.Euler(90f - tilt, 0f, 0f);
        transform.position = DesiredPosition();
        UpdateViewInfo();
    }

    private Vector3 DesiredPosition()
    {
        // Look-ahead comes from where the cursor is relative to the camera, not to the player.
        // The world point under the cursor moves whenever the camera moves, so basing it on
        // that fed the camera's own motion back into itself and dragged the aim around.
        Vector3 lookAhead = GetCursorOffset();

        Vector3 focus = ClampToRoom(target.position + lookAhead * mouseLookAhead);
        focus.y = 0f;

        // The camera sits at its height, back from the floor point it looks at by the tilt.
        float height = transform.position.y;
        return focus + new Vector3(0f, height, -height * Mathf.Tan(tilt * Mathf.Deg2Rad));
    }

    private Vector3 ClampToRoom(Vector3 focus)
    {
        if (floor == null || !floor.TryGetCameraArea(target.position, out Rect area))
            return focus;

        Vector2 half = HalfViewOnFloor();
        focus.x = ClampAxis(focus.x, area.xMin + half.x, area.xMax - half.x);
        focus.z = ClampAxis(focus.z, area.yMin + half.y, area.yMax - half.y);
        return focus;
    }

    // How far the view reaches across the floor either side of the screen's middle (x across,
    // y up the screen). A tilt stretches the view up the screen across more floor.
    private Vector2 HalfViewOnFloor()
    {
        float lean = Mathf.Cos(tilt * Mathf.Deg2Rad);
        float halfHeight;

        if (cam.orthographic)
        {
            halfHeight = cam.orthographicSize / lean;
        }
        else
        {
            // Looking straight down (near enough for a small tilt).
            float distance = transform.position.y / lean;
            halfHeight = distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / lean;
        }

        float halfWidth = (cam.orthographic ? cam.orthographicSize : halfHeight * lean) * cam.aspect;
        return new Vector2(halfWidth, halfHeight);
    }

    // If the area is smaller than the view on this axis, centre on it instead.
    private static float ClampAxis(float value, float min, float max)
    {
        return min > max ? (min + max) / 2f : Mathf.Clamp(value, min, max);
    }

    // Ground-plane offset from the point under the screen centre to the point under the cursor.
    // This doesn't change as the camera moves, whatever its projection or tilt.
    private Vector3 GetCursorOffset()
    {
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        Ray cursorRay = cam.ScreenPointToRay(GameInput.Pointer);
        Ray centreRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!groundPlane.Raycast(cursorRay, out float cursorDistance)
            || !groundPlane.Raycast(centreRay, out float centreDistance))
        {
            return Vector3.zero;
        }

        Vector3 offset = cursorRay.GetPoint(cursorDistance) - centreRay.GetPoint(centreDistance);
        offset.y = 0f;
        return offset;
    }
}
