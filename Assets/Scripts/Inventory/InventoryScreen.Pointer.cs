using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// The InventoryScreen's pointer: which spot (card, mod slot, cell, quick slot) is under the
// cursor, and what's in it.
public partial class InventoryScreen
{
    private void UpdatePointer()
    {
        Camera uiCamera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, GameInput.Pointer, uiCamera, out pointer);
        pointerInside = PanelBounds.Contains(pointer);
        hover = SpotAt(pointer);
    }

    private Spot SpotAt(Vector2 point)
    {
        if (!PanelBounds.Contains(point))
            return new Spot { Kind = SpotKind.Outside };

        for (int i = 0; i < Backpack.CellCount; i++)
        {
            if (CellRect(i).Contains(point))
                return new Spot { Kind = SpotKind.Cell, Index = i };
        }

        for (int i = 0; i < QuickBar.SlotCount; i++)
        {
            if (QuickSlotRect(i).Contains(point))
                return new Spot { Kind = SpotKind.QuickSlot, Index = i };
        }

        if (StorySlotRect.Contains(point))
            return new Spot { Kind = SpotKind.Story };

        for (int g = 0; g < 2; g++)
        {
            for (int s = 0; s < ModSlots.Length; s++)
            {
                if (ModSlotRect(g, s).Contains(point))
                    return new Spot { Kind = SpotKind.ModSlot, Index = g, Slot = s };
            }

            if (CardRect(g).Contains(point))
                return new Spot { Kind = SpotKind.Card, Index = g };
        }

        return new Spot { Kind = SpotKind.None };
    }

    private Gun GunAt(int index)
    {
        if (weapons == null)
            return null;

        return index == 0 ? weapons.Main.Gun : weapons.Sidearm.Gun;
    }

    private PlayerWeapon.Equipped EquippedAt(int index)
    {
        return index == 0 ? weapons.Main : weapons.Sidearm;
    }

    // What's at a spot: an attachment (on a gun or in the backpack) or a backpack item.
    private WeaponAttachment AttachmentAt(Spot spot)
    {
        if (spot.Kind == SpotKind.ModSlot)
        {
            Gun gun = GunAt(spot.Index);
            return gun != null ? gun.AttachmentIn(ModSlots[spot.Slot]) : null;
        }

        if (spot.Kind == SpotKind.Cell && Bag != null)
        {
            Backpack.Item item = Bag[spot.Index];
            return item != null && item.Kind == Backpack.ItemKind.Attachment ? item.Attachment : null;
        }

        return null;
    }

    private Backpack.Item ItemAt(Spot spot)
    {
        return spot.Kind == SpotKind.Cell && Bag != null ? Bag[spot.Index] : null;
    }

    private bool HasItem(Spot spot)
    {
        if (spot.Kind == SpotKind.QuickSlot)
            return Quick != null && Quick[spot.Index] != null;

        return AttachmentAt(spot) != null || ItemAt(spot) != null;
    }

    // A quick slot as an item, for drawing it like a stack: its kind and how many are carried.
    private Backpack.Item QuickItem(int slot)
    {
        if (Quick == null || !(Quick[slot] is ConsumableType type))
            return null;

        return new Backpack.Item { Kind = Backpack.ItemKind.Consumable, Consumable = type, Amount = Quick.Count(slot) };
    }
}
