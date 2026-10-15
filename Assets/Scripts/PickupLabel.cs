using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// A floating label over a pickup, styled like the HUD: dark panel, phosphor frame, corner
// brackets and a stripe in the item's colour. Faces the top-down camera, and is kept over the
// floor of the room the pickup is in (allowing for perspective, since it floats above the walls),
// so labels on wall-mounted guns don't hang over the wall. Built entirely in code.
// After CameraFollow, so it's placed for where the camera ends up this frame.
[DefaultExecutionOrder(200)]
public class PickupLabel : MonoBehaviour
{
    private const float Scale = 0.01f;          // world units per canvas pixel
    private const float Padding = 12f;
    private const float WallMargin = 0.25f;

    private static readonly Color Phosphor = new Color(0.35f, 1f, 0.55f, 1f);
    private static FloorGenerator floor;

    private Transform pickup;
    private float height;
    private RectTransform panel;
    private TMP_Text text;
    private Func<string> textSource;
    private Color accent = Phosphor;
    private Canvas canvas;

    // Makes a hidden label that floats `labelHeight` above the pickup.
    public static PickupLabel Create(Transform owner, float labelHeight)
    {
        GameObject root = new GameObject("Label", typeof(RectTransform));
        root.transform.SetParent(owner, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        root.transform.localScale = Vector3.one * Scale;

        PickupLabel label = root.AddComponent<PickupLabel>();
        label.pickup = owner;
        label.height = labelHeight;
        label.Build();
        root.SetActive(false);
        return label;
    }

    private void Build()
    {
        GameObject panelObject = new GameObject("Panel", typeof(RectTransform));
        panelObject.transform.SetParent(transform, false);
        panel = panelObject.GetComponent<RectTransform>();
        panelObject.AddComponent<CanvasRenderer>();
        CallbackGraphic background = panelObject.AddComponent<CallbackGraphic>();
        background.Draw = DrawPanel;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(panel, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(Padding + 6f, Padding);
        textRect.offsetMax = new Vector2(-Padding, -Padding);

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 24;
        label.color = Phosphor;
        label.characterSpacing = 6f;
        label.lineSpacing = 10f;
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.richText = true;
        text = label;
    }

    // Shows the label. The text comes from `source`, re-read every frame so it can change
    // (ammo left in a crate, the button for the device in use).
    public void Show(Func<string> source, Color stripe)
    {
        textSource = source;
        accent = stripe;
        gameObject.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public bool IsShowing => gameObject.activeSelf;

    private void LateUpdate()
    {
        // Out of the way while the inventory is up over the room.
        if (canvas == null)
            canvas = GetComponent<Canvas>();

        canvas.enabled = !InventoryScreen.IsOpen;
        Refresh();
    }

    private void Refresh()
    {
        if (textSource != null)
        {
            string value = textSource();

            if (text.text != value)
                text.text = value;
        }

        // Fit the panel to the text.
        Vector2 size = text.GetPreferredValues(text.text) + new Vector2(Padding * 2f + 6f, Padding * 2f);
        panel.sizeDelta = size;

        Camera camera = Camera.main;

        if (camera != null)
            transform.rotation = camera.transform.rotation;

        transform.position = Place(camera, size * Scale / 2f);
    }

    // Above the pickup, then nudged inwards so the whole label appears over the room's floor.
    private Vector3 Place(Camera camera, Vector2 halfSize)
    {
        Vector3 position = pickup.position + Vector3.up * height;

        if (floor == null)
            floor = FindAnyObjectByType<FloorGenerator>();

        if (camera == null || floor == null || !floor.TryGetRoom(pickup.position, out Room room))
            return position;

        Vector2 point = new Vector2(pickup.position.x, pickup.position.z);
        Rect area = default;
        bool found = false;

        foreach (Rect block in room.Blocks)
        {
            if (block.Contains(point))
            {
                area = block;
                found = true;
                break;
            }
        }

        float cameraHeight = camera.transform.position.y;

        if (!found || position.y >= cameraHeight)
            return position;

        // Seen on the floor, the label looks bigger and further out than it is.
        float spread = cameraHeight / (cameraHeight - position.y);
        Vector2 eye = new Vector2(camera.transform.position.x, camera.transform.position.z);
        Vector2 apparent = eye + (new Vector2(position.x, position.z) - eye) * spread;
        Vector2 half = halfSize * spread;

        apparent.x = ClampAxis(apparent.x, area.xMin + half.x + WallMargin, area.xMax - half.x - WallMargin);
        apparent.y = ClampAxis(apparent.y, area.yMin + half.y + WallMargin, area.yMax - half.y - WallMargin);

        Vector2 actual = eye + (apparent - eye) / spread;
        return new Vector3(actual.x, position.y, actual.y);
    }

    private static float ClampAxis(float value, float min, float max)
    {
        return min > max ? (min + max) / 2f : Mathf.Clamp(value, min, max);
    }

    private void DrawPanel(VertexHelper vh, Rect rect)
    {
        FillRect(vh, rect, new Color(0f, 0.06f, 0.03f, 0.88f));
        OutlineRect(vh, rect, 1.5f, Fade(Phosphor, 0.55f));

        // Stripe in the item's colour down the left edge.
        FillRect(vh, new Rect(rect.xMin + 4f, rect.yMin + 6f, 4f, rect.height - 12f), accent);

        // Corner brackets, like the radar and equipment panel.
        const float length = 10f;
        const float thickness = 2.5f;
        FillRect(vh, new Rect(rect.xMin, rect.yMax - thickness, length, thickness), Phosphor);
        FillRect(vh, new Rect(rect.xMin, rect.yMax - length, thickness, length), Phosphor);
        FillRect(vh, new Rect(rect.xMax - length, rect.yMax - thickness, length, thickness), Phosphor);
        FillRect(vh, new Rect(rect.xMax - thickness, rect.yMax - length, thickness, length), Phosphor);
        FillRect(vh, new Rect(rect.xMin, rect.yMin, length, thickness), Phosphor);
        FillRect(vh, new Rect(rect.xMin, rect.yMin, thickness, length), Phosphor);
        FillRect(vh, new Rect(rect.xMax - length, rect.yMin, length, thickness), Phosphor);
        FillRect(vh, new Rect(rect.xMax - thickness, rect.yMin, thickness, length), Phosphor);
    }
}
