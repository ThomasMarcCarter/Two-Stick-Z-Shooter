using UnityEngine;
using UnityEngine.UI;

// The aim cursor: a circle with four ticks and a dot. The circle shows how far shots can stray
// at the cursor's distance (the gun's inaccuracy, bloom from firing, moving or sneaking, and
// attachments like the laser sight), so it tightens as aim settles and widens in a firefight.
// Each shot also kicks it out a little. In menus it's only a pointer: a small, steady circle
// with no bloom.
public class Crosshair : MaskableGraphic
{
    [Header("Crosshair")]
    [SerializeField] private float circleRadius = 20f;
    [SerializeField] private float circleThickness = 2f;

    [SerializeField] private float lineLength = 10f;
    [SerializeField] private float lineThickness = 2f;
    [SerializeField] private float lineGap = 6f;

    [SerializeField] private float dotRadius = 2f;

    [Header("Spread")]
    [Tooltip("Smallest the circle gets, however accurate the gun.")]
    [SerializeField] private float minRadius = 8f;
    [Tooltip("Largest the circle gets.")]
    [SerializeField] private float maxRadius = 60f;
    [Tooltip("How quickly the circle follows changes in spread.")]
    [SerializeField] private float spreadSmoothing = 18f;

    [Header("Recoil")]
    [SerializeField] private float recoilExpansion = 8f;
    [SerializeField] private float recoilReturnSpeed = 15f;

    [SerializeField] private PlayerShooting playerShooting;

    private float currentExpansion;
    private int lastAmmo = -1;
    private float spreadRadius = -1f;
    private Camera mainCamera;

    private void Update()
    {
        if (playerShooting == null)
            return;

        canvasRenderer.cull = !GameFlow.ShowCrosshair;
        FollowMouse();

        Gun gun = playerShooting.EquippedGun;

        if (gun != null)
        {
            int currentAmmo = gun.GetCurrentAmmo();

            if (lastAmmo != -1 && currentAmmo < lastAmmo)
            {
                currentExpansion = recoilExpansion;
            }

            lastAmmo = currentAmmo;
        }
        else
        {
            lastAmmo = -1;
        }

        currentExpansion = Mathf.MoveTowards(
            currentExpansion,
            0f,
            recoilReturnSpeed * Time.deltaTime
        );

        if (GameFlow.InMenu)
        {
            currentExpansion = 0f;
            spreadRadius = minRadius;
        }
        else
        {
            float target = gun != null ? SpreadRadius(gun.CurrentInaccuracy) : circleRadius;
            spreadRadius = spreadRadius < 0f ? target : Mathf.Lerp(spreadRadius, target, 1f - Mathf.Exp(-spreadSmoothing * Time.unscaledDeltaTime));
        }

        SetVerticesDirty();
    }

    // The stray, in degrees either way, as a radius at the cursor: how far to the side of the
    // aim point a shot that strays that much lands, on screen, in this canvas's units.
    private float SpreadRadius(float degrees)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null || playerShooting == null)
            return circleRadius;

        if (!Gun.PointerAimPoint(out Vector3 aim))
            return circleRadius;
        Vector3 from = playerShooting.transform.position;
        Vector3 toAim = aim - from;
        toAim.y = 0f;

        float distance = toAim.magnitude;

        if (distance < 0.01f)
            return minRadius;

        Vector3 side = Vector3.Cross(Vector3.up, toAim / distance) * distance * Mathf.Tan(degrees * Mathf.Deg2Rad);
        Vector2 a = mainCamera.WorldToScreenPoint(aim);
        Vector2 b = mainCamera.WorldToScreenPoint(aim + side);
        float pixels = Vector2.Distance(a, b) / (canvas != null ? canvas.scaleFactor : 1f);
        return Mathf.Clamp(pixels, minRadius, maxRadius);
    }

    private void FollowMouse()
    {
        RectTransform parent = (RectTransform)rectTransform.parent;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screenPoint = GameInput.Pointer;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screenPoint, uiCamera, out Vector3 worldPoint))
        {
            rectTransform.position = worldPoint;
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float spread = spreadRadius < 0f ? circleRadius : spreadRadius;
        float radius = spread + currentExpansion;
        float gap = Mathf.Max(lineGap, radius - lineLength * 0.5f);

        DrawCircle(vh, radius, circleThickness);
        DrawLines(vh, radius, gap);
        DrawDot(vh, dotRadius);
    }

    private void DrawCircle(VertexHelper vh, float radius, float thickness)
    {
        int segments = 64;
        float innerRadius = radius - thickness;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;

            Vector2 outer = new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );

            Vector2 inner = new Vector2(
                Mathf.Cos(angle) * innerRadius,
                Mathf.Sin(angle) * innerRadius
            );

            vh.AddVert(outer, color, Vector2.zero);
            vh.AddVert(inner, color, Vector2.zero);
        }

        for (int i = 0; i < segments; i++)
        {
            int index = i * 2;

            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index + 1, index + 3, index + 2);
        }
    }

    private void DrawLines(VertexHelper vh, float radius, float gap)
    {
        DrawRectangle(
            vh,
            new Vector2(-lineThickness / 2f, gap),
            new Vector2(lineThickness / 2f, gap + lineLength)
        );

        DrawRectangle(
            vh,
            new Vector2(-lineThickness / 2f, -gap - lineLength),
            new Vector2(lineThickness / 2f, -gap)
        );

        DrawRectangle(
            vh,
            new Vector2(-gap - lineLength, -lineThickness / 2f),
            new Vector2(-gap, lineThickness / 2f)
        );

        DrawRectangle(
            vh,
            new Vector2(gap, -lineThickness / 2f),
            new Vector2(gap + lineLength, lineThickness / 2f)
        );
    }

    private void DrawDot(VertexHelper vh, float radius)
    {
        int segments = 16;
        int startIndex = vh.currentVertCount;

        vh.AddVert(Vector2.zero, color, Vector2.zero);

        for (int i = 0; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;

            Vector2 position = new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );

            vh.AddVert(position, color, Vector2.zero);
        }

        for (int i = 0; i < segments; i++)
        {
            vh.AddTriangle(
                startIndex,
                startIndex + i + 1,
                startIndex + i + 2
            );
        }
    }

    private void DrawRectangle(VertexHelper vh, Vector2 min, Vector2 max)
    {
        int index = vh.currentVertCount;

        vh.AddVert(new Vector2(min.x, min.y), color, Vector2.zero);
        vh.AddVert(new Vector2(min.x, max.y), color, Vector2.zero);
        vh.AddVert(new Vector2(max.x, max.y), color, Vector2.zero);
        vh.AddVert(new Vector2(max.x, min.y), color, Vector2.zero);

        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }
}