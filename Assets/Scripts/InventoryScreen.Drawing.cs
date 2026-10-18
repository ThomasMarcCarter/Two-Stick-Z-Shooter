using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// The inventory screen's mesh drawing: the panel, card and cell frames, mod slots, item
// glyphs, and the overlay (the carried item following the cursor and the tooltip box).
public partial class InventoryScreen
{
    // ---------- Drawing ----------

    private void DrawPanel(VertexHelper vh, Rect bounds)
    {
        FillRect(vh, bounds, new Color(0f, 0.04f, 0.02f, 0.97f));
        OutlineRect(vh, bounds, 1.5f, Fade(phosphor, 0.6f));

        // Divider between the columns.
        FillRect(vh, new Rect(Middle - 0.5f, bounds.yMin + 24f, 1f, bounds.height - 100f), Fade(phosphor, 0.2f));

        if (weapons == null || ammo == null)
            return;

        WeaponAttachment carried = holding ? AttachmentAt(heldFrom) : null;
        PlayerWeapon.Equipped[] slots = { weapons.Main, weapons.Sidearm };
        Gun.Slot[] slotIds = { Gun.Slot.Main, Gun.Slot.Sidearm };

        for (int i = 0; i < 2; i++)
        {
            Rect card = CardRect(i);
            Gun gun = slots[i].Gun;
            bool inHand = weapons.ActiveSlot == slotIds[i] && gun != null;
            bool canTake = carried != null && carried.Fits(gun);

            FillRect(vh, card, Fade(phosphor, inHand ? 0.1f : 0.04f));
            OutlineRect(vh, card, inHand || canTake ? 2f : 1f, Fade(phosphor, inHand || canTake ? 0.9f : 0.4f));

            if (gun != null)
            {
                Rect icon = new Rect(card.xMin + 12f, card.yMin + 12f, 148f, card.height - 40f);
                WeaponSilhouette.Draw(vh, gun.HudIcon, icon, Fade(phosphor, inHand ? 1f : 0.6f));
            }

            for (int s = 0; s < ModSlots.Length; s++)
                DrawModSlot(vh, i, s, gun, carried);
        }

        for (int i = 0; i < Backpack.CellCount; i++)
        {
            Rect cell = CellRect(i);
            Backpack.Item item = Bag != null ? Bag[i] : null;
            bool lifted = holding && heldFrom.Kind == SpotKind.Cell && heldFrom.Index == i;
            bool hovered = hover.Kind == SpotKind.Cell && hover.Index == i;

            FillRect(vh, cell, Fade(phosphor, 0.03f));
            OutlineRect(vh, cell, hovered ? 2f : 1f, Fade(phosphor, hovered ? 0.9f : 0.25f));

            if (item != null && !lifted)
                DrawItem(vh, cell, item, 1f);
        }

        // The quick bar: lit up as a target while a consumable is carried.
        Backpack.Item carriedItem = holding ? HeldAsItem() : null;
        bool consumableCarried = carriedItem != null && carriedItem.Kind == Backpack.ItemKind.Consumable;

        for (int i = 0; i < QuickBar.SlotCount; i++)
        {
            Rect slot = QuickSlotRect(i);
            Backpack.Item item = QuickItem(i);
            bool lifted = holding && heldFrom.Kind == SpotKind.QuickSlot && heldFrom.Index == i;
            bool hovered = hover.Kind == SpotKind.QuickSlot && hover.Index == i;
            bool selected = Quick != null && Quick.Selected == i && GameInput.UsingGamepad;

            FillRect(vh, slot, Fade(phosphor, consumableCarried ? 0.1f : 0.03f));

            if (item != null && !lifted)
                DrawItem(vh, slot, item, item.Amount > 0 ? 1f : 0.4f);

            bool bright = hovered || consumableCarried || selected;
            OutlineRect(vh, slot, bright ? 2f : 1f, Fade(phosphor, bright ? 0.9f : 0.3f));
        }
    }

    private void DrawModSlot(VertexHelper vh, int gunIndex, int slotIndex, Gun gun, WeaponAttachment carried)
    {
        Rect rect = ModSlotRect(gunIndex, slotIndex);
        AttachmentSlot slot = ModSlots[slotIndex];
        WeaponAttachment fitted = gun != null ? gun.AttachmentIn(slot) : null;
        bool lifted = holding && heldFrom.Kind == SpotKind.ModSlot && heldFrom.Index == gunIndex && heldFrom.Slot == slotIndex;
        bool hovered = hover.Kind == SpotKind.ModSlot && hover.Index == gunIndex && hover.Slot == slotIndex;
        bool target = carried != null && carried.slot == slot && carried.Fits(gun);

        if (gun == null)
        {
            DashedOutlineRect(vh, rect, 1f, 4f, Fade(phosphor, 0.15f));
            return;
        }

        FillRect(vh, rect, Fade(phosphor, target ? 0.12f : 0.03f));

        if (fitted != null && !lifted)
        {
            FillRect(vh, rect, Fade(fitted.colour, 0.16f));
            FillRect(vh, new Rect(rect.xMin, rect.yMax - 3f, rect.width, 3f), fitted.colour);
            AttachmentSilhouette.Draw(vh, fitted, new Rect(rect.xMin + 5f, rect.yMin + 15f, rect.width - 10f, rect.height - 21f), AttachmentInk(fitted, 1f));
        }

        float thickness = hovered || target ? 2f : 1f;
        OutlineRect(vh, rect, thickness, Fade(phosphor, hovered || target ? 0.95f : 0.3f));
    }

    // An item in a cell (or the one being carried): attachments as a wireframe picture under a
    // bar in their colour, ammo with a swatch and a fill bar for how full the stack is.
    private void DrawItem(VertexHelper vh, Rect cell, Backpack.Item item, float alpha)
    {
        if (item.Kind == Backpack.ItemKind.Attachment)
        {
            Color colour = item.Attachment.colour;
            FillRect(vh, cell, Fade(colour, 0.15f * alpha));
            FillRect(vh, new Rect(cell.xMin, cell.yMax - 4f, cell.width, 4f), Fade(colour, alpha));
            Rect picture = new Rect(cell.xMin + 5f, cell.yMin + 22f, cell.width - 10f, cell.height - 30f);
            AttachmentSilhouette.Draw(vh, item.Attachment, picture, AttachmentInk(item.Attachment, alpha));
            return;
        }

        if (item.Kind == Backpack.ItemKind.Consumable)
        {
            Color colour = ConsumableInfo.Colour(item.Consumable);
            FillRect(vh, cell, Fade(colour, 0.12f * alpha));
            FillRect(vh, new Rect(cell.xMin, cell.yMax - 4f, cell.width, 4f), Fade(colour, alpha));

            // A pip per item in the stack, filled for the ones there.
            int size = ConsumableInfo.StackSize(item.Consumable);
            float pip = Mathf.Min(6f, (cell.width - 12f - (size - 1) * 2f) / size);
            float start = cell.center.x - (size * pip + (size - 1) * 2f) / 2f;

            for (int i = 0; i < size; i++)
            {
                Rect dot = new Rect(start + i * (pip + 2f), cell.yMin + 3f, pip, 3f);
                FillRect(vh, dot, i < item.Amount ? Fade(colour, 0.9f * alpha) : Fade(phosphor, 0.15f * alpha));
            }

            return;
        }

        // Materials: a tint and a bar in the material's colour, filled to how full the stack is.
        if (item.Kind == Backpack.ItemKind.Material)
        {
            Color colour = Materials.Colour(item.Material);
            FillRect(vh, cell, Fade(colour, 0.1f * alpha));
            FillRect(vh, new Rect(cell.xMin, cell.yMax - 4f, cell.width, 4f), Fade(colour, alpha));

            Rect stackBar = new Rect(cell.xMin + 6f, cell.yMin + 4f, cell.width - 12f, 4f);
            FillRect(vh, stackBar, Fade(phosphor, 0.15f * alpha));
            FillRect(vh, new Rect(stackBar.xMin, stackBar.yMin, stackBar.width * item.Amount / Materials.StackSize, stackBar.height), Fade(colour, 0.9f * alpha));
            return;
        }

        Color ammoColour = AmmoInfo.Colour(item.AmmoType);
        FillRect(vh, new Rect(cell.xMin + 6f, cell.yMax - 12f, 8f, 8f), Fade(ammoColour, alpha));

        Rect bar = new Rect(cell.xMin + 6f, cell.yMin + 4f, cell.width - 12f, 4f);
        float fill = item.Amount / (float)Backpack.StackSize(item.AmmoType);
        FillRect(vh, bar, Fade(phosphor, 0.15f * alpha));
        FillRect(vh, new Rect(bar.xMin, bar.yMin, bar.width * fill, bar.height), Fade(ammoColour, 0.9f * alpha));
    }

    private void DrawOverlay(VertexHelper vh, Rect bounds)
    {
        if (holding)
        {
            Backpack.Item held = HeldAsItem();

            if (held != null)
            {
                Rect ghost = GhostRect;
                FillRect(vh, ghost, new Color(0f, 0.06f, 0.03f, 0.92f));
                DrawItem(vh, ghost, held, 1f);

                // Outside the panel, whatever is carried will be dropped on the floor.
                bool dropping = !pointerInside;
                OutlineRect(vh, ghost, 2f, dropping ? new Color(1f, 0.4f, 0.3f, 1f) : phosphor);
            }

            return;
        }

        if (!tooltipText.gameObject.activeSelf)
            return;

        Vector2 position = tooltipRect.anchoredPosition;
        Vector2 textSize = tooltipRect.sizeDelta;
        Rect box = new Rect(position.x - 10f, position.y - 8f, textSize.x + 22f, textSize.y + 16f);
        FillRect(vh, box, new Color(0f, 0.05f, 0.03f, 0.96f));
        OutlineRect(vh, box, 1.5f, Fade(phosphor, 0.7f));
        FillRect(vh, new Rect(box.xMin + 3f, box.yMin + 5f, 3f, box.height - 10f), tooltipAccent);
    }
}
