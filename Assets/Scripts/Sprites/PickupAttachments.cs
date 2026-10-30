using System.Collections.Generic;
using UnityEngine;

// The attachments fitted to a gun lying on the floor (or on a wall mount), drawn on its sprite
// the way a held gun shows them (GunSprite.Mount): a long or match barrel past the muzzle, a
// suppressor or choke on the end, a mag part under the mag, the laser under the barrel. They stand
// just in front of the gun's sprite and bob with it. Added by GunPickup with the sprite art.
[DefaultExecutionOrder(260)]
public class PickupAttachments : MonoBehaviour
{
    private const float Gap = 0.01f;   // metres in front of the gun's sprite

    private ItemSprite item;
    private SpriteSheet sheet;
    private readonly List<PixelSprite> parts = new List<PixelSprite>();
    private readonly List<Vector2> mounts = new List<Vector2>();

    public static void Apply(ItemSprite item, SpriteSheet sheet, IReadOnlyList<WeaponAttachment> fitted)
    {
        if (item == null || item.Sprite == null)
            return;

        PickupAttachments shown = item.GetComponent<PickupAttachments>();

        if (shown == null)
            shown = item.gameObject.AddComponent<PickupAttachments>();

        shown.Build(item, sheet, fitted);
    }

    private void Build(ItemSprite itemSprite, SpriteSheet gunSheet, IReadOnlyList<WeaponAttachment> fitted)
    {
        foreach (PixelSprite part in parts)
        {
            if (part != null)
                Destroy(part.gameObject);
        }

        parts.Clear();
        mounts.Clear();
        item = itemSprite;
        sheet = gunSheet;
        SpriteSheet art = GunSprite.AttachmentArt;

        if (art == null || fitted == null)
            return;

        WeaponAttachment barrelPart = null;

        foreach (WeaponAttachment attachment in fitted)
        {
            if (attachment != null && attachment.slot == AttachmentSlot.Barrel)
                barrelPart = attachment;
        }

        foreach (WeaponAttachment attachment in fitted)
        {
            int frame = GunSprite.Mount(sheet, art, attachment, barrelPart, out Vector2 mount);

            if (frame < 0)
                continue;

            PixelSprite part = PixelSprite.Create(attachment.name + " Sprite", transform, art, PixelSprite.Mode.Facing,
                                                  item.Sprite.Renderer.renderingLayerMask);
            part.Standing = true;
            part.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            part.SetFrame(frame, 0, false);
            part.Scale = art.pixelsPerUnit / Mathf.Max(1f, sheet.pixelsPerUnit) * item.Sprite.Scale;
            part.DepthOffset = item.Sprite.DepthOffset - Gap;
            parts.Add(part);
            mounts.Add(mount);
        }

        LateUpdate();
    }

    // Set from the gun sprite's Lift as ItemSprite left it this frame, so both are placed from the
    // same numbers next frame (sprites place themselves before this runs).
    private void LateUpdate()
    {
        Camera camera = Camera.main;

        if (camera == null || item == null || item.Sprite == null)
            return;

        PixelSprite gun = item.Sprite;
        Vector3 pivot = new Vector3(transform.position.x, gun.Lift, transform.position.z);
        Quaternion facing = Quaternion.Euler(0f, camera.transform.eulerAngles.y, 0f);
        float metres = gun.Scale / Mathf.Max(1f, sheet.pixelsPerUnit);
        float squash = Mathf.Max(0.25f, CameraFollow.VerticalScale);

        for (int i = 0; i < parts.Count; i++)
        {
            Vector2 offset = (mounts[i] - sheet.pivot) * metres;
            parts[i].Position = pivot + facing * new Vector3(offset.x, offset.y / squash, 0f);
            parts[i].Renderer.enabled = gun.Renderer.enabled;
        }
    }
}
