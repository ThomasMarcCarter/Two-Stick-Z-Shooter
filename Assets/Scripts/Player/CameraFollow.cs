using UnityEngine;

// The game camera: follows the player at a fixed height, leaning a little towards the cursor,
// kept inside the current room so it never shows the void past the walls, and shaken by
// explosions and heavy hits. Orthographic (Gungeon-style: the camera's size sets how much floor
// is in view) or perspective (`perspective`, at `fieldOfView`, backed off so the same floor area
// is in view as the orthographic size gives, for more depth). It can lean back from straight down
// by `tilt` degrees to show the faces of the walls; all the following and clamping is done on the
// floor point under the middle of the screen, so any tilt works. With `spritesFaceCamera` the
// upright sprites are drawn leaning back to face the camera (SpriteLit, from _SpriteUp/_SpriteLean)
// instead of standing stretched; they still sort, cast shadows and are lit as if upright.
public class CameraFollow : MonoBehaviour
{
    private static readonly int SpriteUpId = Shader.PropertyToID("_SpriteUp");
    private static readonly int SpriteLeanId = Shader.PropertyToID("_SpriteLean");

    // How tall a metre of a sprite's height looks on screen against a metre across (the tilt
    // foreshortens upright sprites, which stand stretched by the inverse so their pixels come out
    // square); 1 when sprites face the camera.
    public static float VerticalScale { get; private set; } = 1f;

    // How tall a metre of real height looks on screen against a metre across, whatever the
    // sprites do: a sprite's depth is the upright plane through its feet, foreshortened by this.
    public static float Foreshortening { get; private set; } = 1f;

    // The way up a drawn sprite: straight up, or the camera's up when sprites face the camera. A
    // point h metres up a sprite's art is drawn at feet + SpriteUp * h / VerticalScale.
    public static Vector3 SpriteUp { get; private set; } = Vector3.up;

    // The way the view looks into the scene: moving something along it doesn't move it on screen
    // (orthographic; with perspective use ViewRay).
    public static Vector3 ViewDirection { get; private set; } = Vector3.down;

    private static Vector3 eye;
    private static bool orthographic = true;

    // The line of sight through a point, from the camera: moving the point along it doesn't move
    // it on screen, whatever the projection.
    public static Vector3 ViewRay(Vector3 at)
    {
        if (orthographic)
            return ViewDirection;

        Vector3 ray = at - eye;
        return ray.sqrMagnitude > 0.0001f ? ray.normalized : ViewDirection;
    }

    // How much the picture is stretched up the screen beyond the camera's own plane (always 1
    // for this camera; sprites allow for other projections).
    public static float ScreenStretch => 1f;

    [SerializeField] private Transform target;
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private float mouseLookAhead = 0.5f;

    [Tooltip("Degrees the camera leans back from looking straight down (0 = straight down). "
        + "More shows more of the walls' faces and less of the floor's depth.")]
    [SerializeField, Range(0f, 60f)] private float tilt = 0f;

    [Header("Projection")]
    [Tooltip("Perspective instead of orthographic: walls and things nearer the edges of the screen show their sides, for more depth.")]
    [SerializeField] private bool perspective;
    [Tooltip("Perspective only: vertical field of view. The camera backs off so the floor in view stays what the orthographic size gives.")]
    [SerializeField, Range(15f, 70f)] private float fieldOfView = 40f;
    [Tooltip("Upright sprites lean back to face the camera instead of standing up stretched.")]
    [SerializeField] private bool spritesFaceCamera;

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
        Foreshortening = 1f;
        SpriteUp = Vector3.up;
        ViewDirection = Vector3.down;
        eye = Vector3.zero;
        orthographic = true;
        Shader.SetGlobalFloat(SpriteLeanId, 0f);
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
        // Set every frame, so the tilt and projection can be tried out live in the Inspector.
        transform.rotation = Quaternion.Euler(90f - tilt, 0f, 0f);
        ApplyProjection();

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
        Foreshortening = Mathf.Max(0.01f, Vector3.Dot(transform.up, Vector3.up));
        VerticalScale = spritesFaceCamera ? 1f : Foreshortening;
        SpriteUp = spritesFaceCamera ? transform.up : Vector3.up;
        ViewDirection = transform.forward;
        eye = transform.position;
        orthographic = cam == null || cam.orthographic;
        Shader.SetGlobalVector(SpriteUpId, SpriteUp);
        Shader.SetGlobalFloat(SpriteLeanId, spritesFaceCamera ? 1f : 0f);
    }

    // Perspective keeps the orthographic size as the amount of floor in view: the camera sits as
    // far back as that takes at the field of view (Height).
    private void ApplyProjection()
    {
        if (cam == null)
            return;

        cam.orthographic = !perspective;

        if (perspective)
            cam.fieldOfView = fieldOfView;
    }

    // How high the camera sits above the floor point it looks at.
    private float Height()
    {
        if (cam == null || cam.orthographic)
            return transform.position.y;

        float distance = cam.orthographicSize / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return distance * Mathf.Cos(tilt * Mathf.Deg2Rad);
    }

    // Jump straight to the player instead of sliding there, e.g. after moving to a new floor.
    public void SnapToTarget()
    {
        shakeOffset = Vector3.zero;
        transform.rotation = Quaternion.Euler(90f - tilt, 0f, 0f);
        ApplyProjection();
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
        float height = Height();
        return focus + new Vector3(0f, height, -height * Mathf.Tan(tilt * Mathf.Deg2Rad));
    }

    private Vector3 ClampToRoom(Vector3 focus)
    {
        if (floor == null || !floor.TryGetCameraArea(target.position, out Rect area))
            return focus;

        ViewOnFloor(out Vector2 low, out Vector2 high);
        focus.x = ClampAxis(focus.x, area.xMin - low.x, area.xMax - high.x);
        focus.z = ClampAxis(focus.z, area.yMin - low.y, area.yMax - high.y);
        return focus;
    }

    // How far the view reaches across the floor from the point under the screen's middle: to the
    // left/bottom edges (low, negative) and the right/top edges (high), x across and y up the
    // screen. With perspective and a tilt the top reaches further than the bottom.
    private void ViewOnFloor(out Vector2 low, out Vector2 high)
    {
        Plane floorPlane = new Plane(Vector3.up, Vector3.zero);
        Vector3 centre = OnFloor(floorPlane, new Vector3(0.5f, 0.5f, 0f));
        low = new Vector2(OnFloor(floorPlane, new Vector3(0f, 0.5f, 0f)).x - centre.x, OnFloor(floorPlane, new Vector3(0.5f, 0f, 0f)).z - centre.z);
        high = new Vector2(OnFloor(floorPlane, new Vector3(1f, 0.5f, 0f)).x - centre.x, OnFloor(floorPlane, new Vector3(0.5f, 1f, 0f)).z - centre.z);
    }

    private Vector3 OnFloor(Plane floorPlane, Vector3 viewport)
    {
        Ray ray = cam.ViewportPointToRay(viewport);
        return floorPlane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : ray.GetPoint(cam.farClipPlane);
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
