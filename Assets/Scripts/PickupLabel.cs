using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// A floating label over a pickup, styled like the HUD: dark panel, phosphor frame, corner
// brackets and a stripe in the item's colour. Faces the top-down camera, and is kept over the
// floor of the room the pickup is in (allowing for perspective, since it floats above the walls),
// so labels on wall-mounted guns don't hang over the wall. Built entirely in code.
// Labels that would overlap on screen are stacked into a tidy column by PickupLabelStack, which
// moves them after they've placed themselves; one moved off its item gets a thin leader line
// back to it, so it's clear which label is whose.
// Loot labels (Compactable) don't all show in full: only the one nearest the player does (its
// details and the button prompt); the rest shrink to a dim one-line name tag, so a room full of
// drops reads as a few short tags rather than a wall of panels.
// After CameraFollow, so it's placed for where the camera ends up this frame.
[DefaultExecutionOrder(200)]
public class PickupLabel : MonoBehaviour
{
    private const float Scale = 0.01f;          // world units per canvas pixel
    private const float Padding = 12f;
    private const float WallMargin = 0.25f;

    private static readonly Color Phosphor = UiColours.Phosphor;
    private static FloorGenerator floor;
    private static Transform player;
    private static PickupLabel nearest;
    private static int nearestFrame = -1;

    // Every label showing, for PickupLabelStack.
    private static readonly List<PickupLabel> showing = new List<PickupLabel>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        showing.Clear();
        floor = null;
        player = null;
        nearest = null;
        nearestFrame = -1;
    }

    public static IReadOnlyList<PickupLabel> Showing => showing;

    private Transform pickup;
    private float height;
    private RectTransform panel;
    private TMP_Text text;
    private Func<string> textSource;
    private Color accent = Phosphor;
    private Canvas canvas;
    private CanvasGroup group;

    // Above the item on screen instead of over it, with this much room between (world units):
    // for things the player needs to see while reading the label (the training dummy). 0 keeps
    // it centred over the item.
    public float Clearance { get; set; }

    // A loot label: shown in full only while it's the nearest loot label to the player, as a
    // dim one-line name tag otherwise.
    public bool Compactable { get; set; }

    // Showing as a name tag this frame.
    public bool Compact { get; private set; }

    // Where it put itself this frame, before any stacking, and its size in world units.
    public Vector3 Placed { get; private set; }
    public Vector2 WorldSize { get; private set; }

    // Shown on screen this frame, before the stack has its say (the inventory hides them all).
    public bool Visible => canvas != null && !ScreenOpen;

    // A full screen is up over the world (or the player is dying), so labels keep out of the way.
    private static bool ScreenOpen => GameFlow.ScreenOverWorld;

    // Hidden by the stack for this frame (too many in one pile; see PickupLabelStack).
    public bool Suppressed { get; private set; }

    // The stack's own labels ("+3 MORE NEARBY"), which it places itself.
    public bool Managed { get; set; }

    // Set by the stack: how far it's been moved (screen pixels, eased), and whether it's newly
    // shown (so it jumps straight into place rather than sliding).
    public Vector2 StackOffset { get; set; }
    public bool JustShown { get; set; }

    // The point it marks: just above the pickup.
    public Vector3 Anchor => pickup.position + Vector3.up * 0.4f;

    // The leader line's far end in the panel's own space, or null for none.
    private Vector2? leaderEnd;

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
        label.canvas = canvas;
        label.group = root.AddComponent<CanvasGroup>();
        label.group.interactable = false;
        label.group.blocksRaycasts = false;
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

        // Always over the level: never hidden behind walls, furniture or awnings.
        if (WorldOverlay.UI != null)
            background.material = WorldOverlay.UI;

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
        label.fontSharedMaterial = WorldOverlay.Text(label.fontSharedMaterial);
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

    private void OnEnable()
    {
        if (!showing.Contains(this))
            showing.Add(this);

        JustShown = true;
        StackOffset = Vector2.zero;
        leaderEnd = null;
        PickupLabelStack.Ensure();
    }

    private void OnDisable()
    {
        showing.Remove(this);
    }

    private void LateUpdate()
    {
        // Out of the way while a menu or screen is up over the room.
        if (canvas == null)
            canvas = GetComponent<Canvas>();

        canvas.enabled = !ScreenOpen && !Suppressed;
        Refresh();
    }

    private void Refresh()
    {
        Compact = Compactable && Nearest() != this;
        group.alpha = Compact ? 0.75f : 1f;
        text.fontSize = Compact ? 20f : 24f;

        if (textSource != null)
        {
            string value = textSource();

            if (Compact)
                value = NameOnly(value);

            if (text.text != value)
                text.text = value;
        }

        // Fit the panel to the text.
        Vector2 size = text.GetPreferredValues(text.text) + new Vector2(Padding * 2f + 6f, Padding * 2f);
        panel.sizeDelta = size;
        WorldSize = size * Scale;

        Camera camera = Camera.main;

        if (camera != null)
            transform.rotation = camera.transform.rotation;

        Placed = Place(camera, WorldSize / 2f);
        transform.position = Placed;
    }

    // The loot label nearest the player this frame, worked out once a frame.
    private static PickupLabel Nearest()
    {
        if (nearestFrame == Time.frameCount)
            return nearest;

        nearestFrame = Time.frameCount;
        nearest = null;

        if (player == null)
        {
            PlayerWeapon weapon = FindAnyObjectByType<PlayerWeapon>();
            player = weapon != null ? weapon.transform : null;

            if (player == null)
                return null;
        }

        float best = float.MaxValue;

        foreach (PickupLabel label in showing)
        {
            if (!label.Compactable || label.pickup == null)
                continue;

            Vector3 offset = label.pickup.position - player.position;
            float distance = offset.x * offset.x + offset.z * offset.z;

            if (distance < best)
            {
                best = distance;
                nearest = label;
            }
        }

        return nearest;
    }

    // The first line that isn't small print (a "MELEE UPGRADE" header), up to any smaller-print
    // detail after the name.
    private static string NameOnly(string value)
    {
        string[] lines = value.Split('\n');
        value = lines[0];

        foreach (string line in lines)
        {
            if (!line.StartsWith("<size", StringComparison.Ordinal))
            {
                value = line;
                break;
            }
        }

        int detail = value.IndexOf("<size", StringComparison.Ordinal);

        if (detail > 0)
            value = value.Substring(0, detail);

        return value.TrimEnd();
    }

    public void SetSuppressed(bool suppressed)
    {
        // Back from hiding: straight into place, not sliding from wherever it was.
        if (Suppressed && !suppressed)
            JustShown = true;

        Suppressed = suppressed;

        if (canvas != null)
            canvas.enabled = !ScreenOpen && !suppressed;
    }

    // Moved by the stack: to `position`, with a leader line back to the item if it's been moved
    // far enough to lose track of it.
    public void MoveTo(Vector3 position, bool leader, Camera camera)
    {
        transform.position = position;
        leaderEnd = null;

        if (!leader || camera == null)
            return;

        // Where the item appears, brought onto the label's plane (at the label's depth), in the
        // panel's own space.
        Vector3 anchorScreen = camera.WorldToScreenPoint(Anchor);
        float depth = camera.WorldToScreenPoint(position).z;
        Vector3 onPlane = camera.ScreenToWorldPoint(new Vector3(anchorScreen.x, anchorScreen.y, depth));
        leaderEnd = panel.InverseTransformPoint(onPlane);
    }

    // Above the pickup, then nudged inwards so the whole label appears over the room's floor.
    private Vector3 Place(Camera camera, Vector2 halfSize)
    {
        Vector3 position = pickup.position + Vector3.up * height;

        // Lifted up the screen so its bottom edge clears the item.
        if (Clearance > 0f && camera != null)
        {
            Vector3 screenUp = Vector3.ProjectOnPlane(camera.transform.up, Vector3.up).normalized;
            position += screenUp * (halfSize.y + Clearance);
        }

        if (floor == null)
            floor = FindAnyObjectByType<FloorGenerator>();

        if (camera == null || floor == null || !floor.TryGetRoom(pickup.position, out Room room))
            return position;

        Vector2 point = new Vector2(pickup.position.x, pickup.position.z);
        bool found = FloorAround(room, point, out Rect area);

        if (!found)
            return position;

        if (camera.orthographic)
        {
            // Seen on the floor, the label sits where the view straight through it lands: the
            // same size, shifted by the camera's tilt (not at all looking straight down).
            Vector3 view = camera.transform.forward;
            Vector2 shift = view.y < -0.01f ? new Vector2(view.x, view.z) * (position.y / -view.y) : Vector2.zero;
            Vector2 landed = new Vector2(position.x, position.z) + shift;

            landed.x = ClampAxis(landed.x, area.xMin + halfSize.x + WallMargin, area.xMax - halfSize.x - WallMargin);
            landed.y = ClampAxis(landed.y, area.yMin + halfSize.y + WallMargin, area.yMax - halfSize.y - WallMargin);
            return new Vector3(landed.x - shift.x, position.y, landed.y - shift.y);
        }

        float cameraHeight = camera.transform.position.y;

        if (position.y >= cameraHeight)
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

    // The floor block under a point, grown a side at a time while the blocks beyond that side
    // cover the whole of it: a plain rectangle of floor round the item, so a label can use the
    // whole of a big room but never hangs over the wall in the corner of an L-shaped one.
    private static bool FloorAround(Room room, Vector2 point, out Rect area)
    {
        area = default;
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

        if (!found)
            return false;

        bool grew = true;

        for (int pass = 0; pass < 64 && grew; pass++)
        {
            grew = false;

            for (int side = 0; side < 4; side++)
            {
                if (TryGrow(room, ref area, side))
                    grew = true;
            }
        }

        return true;
    }

    // Pushes one side of the area (0 up, 1 down, 2 left, 3 right, in world z/x) out to the far
    // edge of the blocks just beyond it, if they span that whole side and line up.
    private static bool TryGrow(Room room, ref Rect area, int side)
    {
        const float epsilon = 0.01f;
        bool vertical = side < 2;
        float edge = side == 0 ? area.yMax : side == 1 ? area.yMin : side == 2 ? area.xMin : area.xMax;
        float spanMin = vertical ? area.xMin : area.yMin;
        float spanMax = vertical ? area.xMax : area.yMax;
        float covered = 0f;
        float far = float.NaN;

        foreach (Rect block in room.Blocks)
        {
            float near = side == 0 ? block.yMin : side == 1 ? block.yMax : side == 2 ? block.xMax : block.xMin;
            float blockMin = vertical ? block.xMin : block.yMin;
            float blockMax = vertical ? block.xMax : block.yMax;

            if (Mathf.Abs(near - edge) > epsilon || blockMin < spanMin - epsilon || blockMax > spanMax + epsilon)
                continue;

            float blockFar = side == 0 ? block.yMax : side == 1 ? block.yMin : side == 2 ? block.xMin : block.xMax;

            if (!float.IsNaN(far) && Mathf.Abs(blockFar - far) > epsilon)
                return false;   // ragged: the blocks beyond don't line up

            far = blockFar;
            covered += blockMax - blockMin;
        }

        if (float.IsNaN(far) || covered < spanMax - spanMin - epsilon)
            return false;

        switch (side)
        {
            case 0: area.yMax = far; break;
            case 1: area.yMin = far; break;
            case 2: area.xMin = far; break;
            default: area.xMax = far; break;
        }

        return true;
    }

    private static float ClampAxis(float value, float min, float max)
    {
        return min > max ? (min + max) / 2f : Mathf.Clamp(value, min, max);
    }

    private void DrawPanel(VertexHelper vh, Rect rect)
    {
        // The leader first, so the panel covers its near end: from the panel's nearest edge
        // to a small dot on the item.
        if (leaderEnd is Vector2 end && !rect.Contains(end))
        {
            Vector2 start = new Vector2(Mathf.Clamp(end.x, rect.xMin, rect.xMax), Mathf.Clamp(end.y, rect.yMin, rect.yMax));
            Line(vh, start, end, 2f, Fade(accent, 0.7f));
            FillRect(vh, new Rect(end.x - 4f, end.y - 4f, 8f, 8f), Fade(accent, 0.9f));
        }

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
