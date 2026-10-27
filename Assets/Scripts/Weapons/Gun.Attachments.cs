using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Gun's attachments: fitting and removing them, recomputing the stats they change, and
// showing them on the held gun (models, or sprites on a GunSprite).
public partial class Gun
{
    public WeaponAttachment AttachmentIn(AttachmentSlot slot)
    {
        return attachments.Find(a => a.slot == slot);
    }

    // Fits an attachment, returning the one it replaced in that slot (or null). Rounds that no
    // longer fit the magazine go back in the player's inventory.
    public WeaponAttachment Attach(WeaponAttachment attachment)
    {
        if (attachment == null)
            return null;

        WeaponAttachment replaced = AttachmentIn(attachment.slot);

        if (replaced != null)
            attachments.Remove(replaced);

        attachments.Add(attachment);
        AttachmentsUpdated();
        return replaced;
    }

    // Takes an attachment off. False if it wasn't fitted.
    public bool Detach(WeaponAttachment attachment)
    {
        if (!attachments.Remove(attachment))
            return false;

        AttachmentsUpdated();
        return true;
    }

    // Replaces everything fitted (a gun picked up with its attachments still on).
    public void SetAttachments(IEnumerable<WeaponAttachment> fitted)
    {
        attachments.Clear();

        if (fitted != null)
        {
            foreach (WeaponAttachment attachment in fitted)
            {
                if (attachment != null && AttachmentIn(attachment.slot) == null)
                    attachments.Add(attachment);
            }
        }

        AttachmentsUpdated();
    }

    private void AttachmentsUpdated()
    {
        // A reload in progress was timed for the old magazine.
        if (isReloading)
        {
            CancelReload();

            if (reloadAudio != null)
                reloadAudio.Stop();
        }

        int size = GetMagazineSize();

        if (currentAmmo > size)
        {
            if (Inventory != null)
                Inventory.Add(ammoType, currentAmmo - size);

            currentAmmo = size;
        }

        UpdateAttachmentVisuals();
        AttachmentsChanged?.Invoke();
    }

    // A suppressor can on the end of the barrel, and the laser line for a laser sight. A gun
    // drawn as a sprite gets no 3D can (GunSprite draws one on the sprite and calls this when added).
    public void UpdateAttachmentVisuals()
    {
        bool suppressed = HidesFlash && GetComponent<GunSprite>() == null;

        if (suppressed && suppressorModel == null && muzzle != null)
        {
            // Grabbed before the can is added, so it's one of the gun's own parts.
            Renderer gunPart = GetComponentInChildren<Renderer>();

            suppressorModel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            suppressorModel.name = "Suppressor";
            Destroy(suppressorModel.GetComponent<Collider>());
            suppressorModel.transform.SetParent(muzzle, false);

            // Along the barrel (the gun's forward): the cylinder's length is its Y axis. Sized in
            // world units, whatever the muzzle's own scale.
            suppressorModel.transform.rotation = Quaternion.LookRotation(transform.forward) * Quaternion.Euler(90f, 0f, 0f);
            suppressorModel.transform.position = muzzle.position + transform.forward * 0.1f;
            Vector3 lossy = muzzle.lossyScale;
            suppressorModel.transform.localScale = new Vector3(0.06f / lossy.x, 0.1f / lossy.y, 0.06f / lossy.z);

            Renderer renderer = suppressorModel.GetComponent<Renderer>();

            if (gunPart != null)
                renderer.sharedMaterial = gunPart.sharedMaterial;

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        else if (!suppressed && suppressorModel != null)
        {
            Destroy(suppressorModel);
            suppressorModel = null;
        }

        bool laser = attachments.Exists(a => a.laser);

        if (laser && laserSight == null && muzzle != null)
        {
            laserSight = LaserSight.Create(transform, muzzle);
        }
        else if (!laser && laserSight != null)
        {
            Destroy(laserSight.gameObject);
            laserSight = null;
        }
    }
}
