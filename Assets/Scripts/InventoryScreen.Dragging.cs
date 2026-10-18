using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// The inventory screen's drag and drop: picking things up (press-drag-release, or press and
// release to carry), and what dropping them on each kind of spot does: fitting attachments
// to a gun, moving and merging backpack stacks, setting quick slots, dropping on the floor.
public partial class InventoryScreen
{
    // ---------- Dragging ----------

    private static bool GrabPressed => GameInput.Fire.WasPressedThisFrame() || GameInput.Submit.WasPressedThisFrame();
    private static bool GrabReleased => GameInput.Fire.WasReleasedThisFrame() || GameInput.Submit.WasReleasedThisFrame();

    // Press on an item to pick it up. Release somewhere else to put it there; release where it
    // was picked up to carry it, and press again to put it down.
    private void HandleDrag()
    {
        if (Bag == null)
            return;

        if (GrabPressed)
        {
            if (holding)
            {
                Place(hover);
                return;
            }

            if (!HasItem(hover))
                return;

            holding = true;
            heldFrom = hover;
            pressPoint = pointer;
            dragged = false;
            clickHold = false;
            SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("inventory-lift", 2, i => ProceduralAudio.BackpackRustle(300 + i))), 0.2f, 1.3f);
            return;
        }

        if (!holding || clickHold)
            return;

        if ((pointer - pressPoint).magnitude > DragThreshold)
            dragged = true;

        if (GrabReleased)
        {
            // A click without moving: keep carrying it until the next press.
            if (!dragged && hover.Same(heldFrom))
                clickHold = true;
            else
                Place(hover);
        }
    }

    private void CancelHold()
    {
        holding = false;
        clickHold = false;
    }

    private void Place(Spot target)
    {
        Spot from = heldFrom;
        CancelHold();

        if (target.Same(from))
            return;

        WeaponAttachment attachment = AttachmentAt(from);

        if (attachment != null)
        {
            PlaceAttachment(attachment, from, target);
            return;
        }

        // A quick slot: onto another to swap them, anywhere else to clear it.
        if (from.Kind == SpotKind.QuickSlot)
        {
            if (target.Kind == SpotKind.QuickSlot)
                Quick.Swap(from.Index, target.Index);
            else
                Quick.Clear(from.Index);

            PlayMove();
            return;
        }

        // An ammo or consumable stack: moved within the backpack, put on the quick bar, or
        // dropped on the floor.
        if (from.Kind != SpotKind.Cell)
            return;

        Backpack.Item stack = Bag[from.Index];

        if (stack == null)
            return;

        if (target.Kind == SpotKind.Cell)
        {
            Bag.Move(from.Index, target.Index);
            PlayMove();
        }
        else if (target.Kind == SpotKind.QuickSlot)
        {
            if (stack.Kind != Backpack.ItemKind.Consumable)
            {
                PlayDeny();
                return;
            }

            Quick.Assign(target.Index, stack.Consumable);
            PlayFit();
        }
        else if (target.Kind == SpotKind.Outside)
        {
            int amount = Bag.TakeStack(from.Index);

            if (amount <= 0)
                return;

            Vector3 position = FloorPickups.DropPoint(weapons.transform);

            if (stack.Kind == Backpack.ItemKind.Ammo)
                AmmoPickup.Drop(stack.AmmoType, amount, position, RoomAt(position));
            else if (stack.Kind == Backpack.ItemKind.Material)
                MaterialPickup.Spawn(stack.Material, amount, position, RoomAt(position));
            else
                ConsumablePickup.Drop(stack.Consumable, amount, position, RoomAt(position));

            PlayMove();
        }
    }

    private void PlaceAttachment(WeaponAttachment attachment, Spot from, Spot target)
    {
        switch (target.Kind)
        {
            case SpotKind.ModSlot:
            case SpotKind.Card:
                FitToGun(attachment, from, target);
                return;

            case SpotKind.Cell:
                ToCell(attachment, from, target.Index);
                return;

            case SpotKind.Outside:
                DropOnFloor(attachment, from);
                return;
        }
    }

    // Onto a gun: dropped on the matching slot or anywhere on the card. Whatever was in that
    // slot goes back where this one came from.
    private void FitToGun(WeaponAttachment attachment, Spot from, Spot target)
    {
        Gun gun = GunAt(target.Index);

        if (gun == null || !attachment.Fits(gun) || (target.Kind == SpotKind.ModSlot && ModSlots[target.Slot] != attachment.slot))
        {
            PlayDeny();
            return;
        }

        // Already on this gun.
        if (from.Kind == SpotKind.ModSlot && from.Index == target.Index)
            return;

        RemoveFrom(attachment, from);
        WeaponAttachment replaced = gun.Attach(attachment);

        if (replaced != null)
            Return(replaced, from);

        PlayFit();
    }

    // Into a backpack cell. Onto another attachment: they swap, if the other one can go where
    // this one came from.
    private void ToCell(WeaponAttachment attachment, Spot from, int cell)
    {
        Backpack.Item there = Bag[cell];

        if (from.Kind == SpotKind.Cell)
        {
            Bag.Move(from.Index, cell);
            PlayMove();
            return;
        }

        // From a gun.
        Gun gun = GunAt(from.Index);

        if (there == null)
        {
            if (gun == null || !Bag.PlaceAttachment(attachment, cell))
            {
                PlayDeny();
                return;
            }

            gun.Detach(attachment);
            PlayMove();
            return;
        }

        if (there.Kind == Backpack.ItemKind.Attachment && there.Attachment.slot == attachment.slot && there.Attachment.Fits(gun))
        {
            WeaponAttachment other = Bag.TakeAttachment(cell);
            gun.Attach(other);
            Bag.PlaceAttachment(attachment, cell);
            PlayFit();
            return;
        }

        PlayDeny();
    }

    private void DropOnFloor(WeaponAttachment attachment, Spot from)
    {
        RemoveFrom(attachment, from);

        Vector3 position = FloorPickups.DropPoint(weapons.transform);
        AttachmentPickup.Spawn(attachment, position, RoomAt(position), AttachmentPickup.DefaultMaterial);
        PlayMove();
    }

    private static Transform RoomAt(Vector3 position)
    {
        return FloorPickups.RoomAt(position);
    }

    private void RemoveFrom(WeaponAttachment attachment, Spot from)
    {
        if (from.Kind == SpotKind.Cell)
            Bag.TakeAttachment(from.Index);
        else if (from.Kind == SpotKind.ModSlot && GunAt(from.Index) != null)
            GunAt(from.Index).Detach(attachment);
    }

    // A swapped-off attachment goes back to where the new one came from: the same backpack
    // cell, or the other gun if it fits there; otherwise any free cell, or the floor.
    private void Return(WeaponAttachment attachment, Spot from)
    {
        if (from.Kind == SpotKind.Cell && Bag.PlaceAttachment(attachment, from.Index))
            return;

        if (from.Kind == SpotKind.ModSlot)
        {
            Gun gun = GunAt(from.Index);

            if (gun != null && attachment.Fits(gun) && gun.AttachmentIn(attachment.slot) == null)
            {
                gun.Attach(attachment);
                return;
            }
        }

        if (Bag.AddAttachment(attachment))
            return;

        DropOnFloor(attachment, new Spot { Kind = SpotKind.None });
    }

    private static void PlayFit()
    {
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-fit", 3, ProceduralAudio.AttachmentFit)), 0.5f, Random.Range(0.95f, 1.05f));
    }

    private static void PlayMove()
    {
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("inventory-lift", 2, i => ProceduralAudio.BackpackRustle(300 + i))), 0.25f, 1.1f);
    }

    private static void PlayDeny()
    {
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-deny", 1, i => ProceduralAudio.AttachmentDeny())), 0.35f);
    }
}
