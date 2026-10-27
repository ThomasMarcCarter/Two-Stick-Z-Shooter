using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// Where a PickupLabel goes on screen: above its item, nudged inwards so the whole label shows
// over the room's floor (never over a wall), and moved by PickupLabelStack to keep stacked
// labels apart, with a leader line back to the item when it's moved far from it.
public partial class PickupLabel
{
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
}
