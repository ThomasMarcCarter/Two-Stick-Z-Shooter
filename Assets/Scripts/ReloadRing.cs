using UnityEngine;
using UnityEngine.UI;

public class ReloadRing : MaskableGraphic
{
    [SerializeField] private PlayerShooting playerShooting;

    [SerializeField] private float radius = 30f;
    [SerializeField] private float thickness = 6f;
    [SerializeField] private int segments = 64;

    private void Update()
    {
        if (playerShooting == null)
            return;

        canvasRenderer.cull = !GameFlow.ShowCrosshair;
        FollowMouse();

        SetVerticesDirty();
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

        if (playerShooting == null)
            return;

        Gun gun = playerShooting.EquippedGun;

        if (gun == null || !gun.IsReloading)
            return;

        float fill = gun.ReloadProgress;

        if (fill <= 0f)
            return;

        float startAngle = 90f;
        float endAngle = startAngle - (360f * fill);

        float innerRadius = radius - thickness;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(startAngle, endAngle, t);
            float radians = angle * Mathf.Deg2Rad;

            Vector2 outer = new Vector2(
                Mathf.Cos(radians) * radius,
                Mathf.Sin(radians) * radius
            );

            Vector2 inner = new Vector2(
                Mathf.Cos(radians) * innerRadius,
                Mathf.Sin(radians) * innerRadius
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
}