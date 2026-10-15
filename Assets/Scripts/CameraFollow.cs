using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private float mouseLookAhead = 0.5f;

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
    }

    // Jump straight to the player instead of sliding there, e.g. after moving to a new floor.
    public void SnapToTarget()
    {
        shakeOffset = Vector3.zero;
        transform.position = DesiredPosition();
    }

    private Vector3 DesiredPosition()
    {
        // Look-ahead comes from where the cursor is relative to the camera, not to the player.
        // The world point under the cursor moves whenever the camera moves, so basing it on
        // that fed the camera's own motion back into itself and dragged the aim around.
        Vector3 lookAhead = GetCursorOffset();

        Vector3 desiredPosition = target.position + lookAhead * mouseLookAhead;

        desiredPosition.y = transform.position.y;
        return ClampToRoom(desiredPosition);
    }

    private Vector3 ClampToRoom(Vector3 position)
    {
        if (floor == null || !floor.TryGetCameraArea(target.position, out Rect area))
            return position;

        // Ground-plane half extents the camera can see, assuming it looks straight down.
        float halfHeight = transform.position.y * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfWidth = halfHeight * cam.aspect;

        position.x = ClampAxis(position.x, area.xMin + halfWidth, area.xMax - halfWidth);
        position.z = ClampAxis(position.z, area.yMin + halfHeight, area.yMax - halfHeight);

        return position;
    }

    // If the area is smaller than the view on this axis, centre on it instead.
    private static float ClampAxis(float value, float min, float max)
    {
        return min > max ? (min + max) / 2f : Mathf.Clamp(value, min, max);
    }

    // Ground-plane offset from the point under the screen centre to the point under the cursor.
    // For a camera looking straight down this doesn't change as the camera moves.
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
