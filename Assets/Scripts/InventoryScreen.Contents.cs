using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// The inventory screen's contents: refreshing the gun cards, mod bars, backpack cells, quick
// slots and melee upgrades from the player's gear, and the hover tooltips for each spot.
public partial class InventoryScreen
{
    // ---------- Contents ----------

    private void Refresh()
    {
        if (weapons == null || ammo == null)
            return;

        hint.text = InputHint.Format("[{Inventory}] CLOSE");

        PlayerWeapon.Equipped[] slots = { weapons.Main, weapons.Sidearm };
        string[] slotNames = { "MAIN", "SIDEARM" };
        Gun.Slot[] slotIds = { Gun.Slot.Main, Gun.Slot.Sidearm };

        for (int i = 0; i < 2; i++)
        {
            Gun gun = slots[i].Gun;
            bool inHand = weapons.ActiveSlot == slotIds[i] && gun != null;

            cardLabels[i].text = slotNames[i] + (inHand ? "  // IN HAND" : "");
            cardNames[i].text = gun != null ? slots[i].Name : "-- EMPTY --";
            cardDetails[i].text = gun != null
                ? $"MAG  {gun.GetCurrentAmmo()} / {gun.GetMagazineSize()}\nAMMO {AmmoInfo.Name(gun.AmmoType)}  ({gun.Reserve} SPARE)"
                : "";

            for (int s = 0; s < ModSlots.Length; s++)
            {
                WeaponAttachment fitted = gun != null ? gun.AttachmentIn(ModSlots[s]) : null;
                bool lifted = holding && heldFrom.Kind == SpotKind.ModSlot && heldFrom.Index == i && heldFrom.Slot == s;
                modLabels[i, s].text = fitted != null && !lifted ? AttachmentLabel(fitted) : WeaponAttachment.SlotName(ModSlots[s]);
                modLabels[i, s].alpha = gun != null ? 1f : 0.35f;
            }
        }

        for (int i = 0; i < Backpack.CellCount; i++)
        {
            Backpack.Item item = Bag != null ? Bag[i] : null;
            bool lifted = holding && heldFrom.Kind == SpotKind.Cell && heldFrom.Index == i;
            SetItemText(cellMain[i], cellSub[i], lifted ? null : item);
        }

        for (int i = 0; i < QuickBar.SlotCount; i++)
        {
            bool lifted = holding && heldFrom.Kind == SpotKind.QuickSlot && heldFrom.Index == i;
            Backpack.Item item = lifted ? null : QuickItem(i);
            SetItemText(quickMain[i], quickSub[i], item);

            if (item == null && Quick != null)
                quickSub[i].text = GameInput.UsingGamepad ? "" : GameInput.Label(GameInput.QuickSlots[i]);
        }

        RefreshMelee();

        if (Bag != null)
            capacity.text = $"{Bag.UsedCells} / {Backpack.CellCount} SLOTS";

        footer.text = "DRAG MODS ONTO A GUN, CONSUMABLES ONTO A QUICK SLOT\nDRAG ANYTHING OUT OF THE PANEL TO DROP IT";

        RefreshOverlay();
    }

    private void RefreshMelee()
    {
        PlayerMelee melee = weapons.Melee;

        if (melee == null)
            return;

        meleeStats.text = $"MELEE [{GameInput.Label(GameInput.Melee)}]  <alpha=#99><size=90%>"
            + $"DMG {melee.Damage} // HITS {melee.MaxTargets} // REACH {melee.Reach:0.0#}M // {melee.Cooldown:0.0#}S";

        string upgrades = "";

        foreach (MeleeUpgrade upgrade in MeleeUpgrades.All)
        {
            int level = melee.Level(upgrade);

            if (level <= 0)
                continue;

            string colour = ColorUtility.ToHtmlStringRGB(MeleeUpgrades.Colour(upgrade));
            upgrades += $"<color=#{colour}>{MeleeUpgrades.Name(upgrade)} {MeleeUpgrades.Level(level)}</color>  ";
        }

        meleeUpgrades.text = upgrades.Length > 0 ? upgrades : "NO UPGRADES YET // FIND THEM ON THE FLOOR";
    }

    private void SetItemText(TMP_Text main, TMP_Text sub, Backpack.Item item)
    {
        if (item == null)
        {
            main.text = "";
            sub.text = "";
            return;
        }

        if (item.Kind == Backpack.ItemKind.Attachment)
        {
            main.text = "";   // drawn as a picture instead (DrawItem)
            sub.text = AttachmentLabel(item.Attachment);
        }
        else if (item.Kind == Backpack.ItemKind.Consumable)
        {
            main.text = ConsumableInfo.ShortName(item.Consumable);
            main.color = Fade(Color.Lerp(ConsumableInfo.Colour(item.Consumable), Color.white, 0.35f), item.Amount > 0 ? 1f : 0.4f);
            sub.text = "x" + item.Amount;
        }
        else if (item.Kind == Backpack.ItemKind.Material)
        {
            main.text = Materials.Short(item.Material);
            main.color = Color.Lerp(Materials.Colour(item.Material), Color.white, 0.3f);
            sub.text = "x" + item.Amount;
        }
        else
        {
            main.text = item.Amount.ToString();
            main.color = Color.Lerp(AmmoInfo.Colour(item.AmmoType), Color.white, 0.3f);
            sub.text = AmmoInfo.Name(item.AmmoType);
        }
    }

    private static string AttachmentLabel(WeaponAttachment attachment)
    {
        return attachment.name.ToUpperInvariant();
    }

    // The colour an attachment's picture is drawn in: its own colour, lifted towards white.
    private static Color AttachmentInk(WeaponAttachment attachment, float alpha)
    {
        return Fade(Color.Lerp(attachment.colour, Color.white, 0.4f), alpha);
    }

    // The carried item under the cursor, or the tooltip for whatever is under it.
    private void RefreshOverlay()
    {
        Vector2 ghostOrigin = GhostRect.position;
        Backpack.Item held = holding ? HeldAsItem() : null;

        SetItemText(ghostMain, ghostSub, held);
        ghostMain.rectTransform.anchoredPosition = ghostOrigin + new Vector2(0f, 22f);
        ghostSub.rectTransform.anchoredPosition = ghostOrigin + new Vector2(0f, 9f);

        string tip = holding ? null : TooltipFor(hover);
        tooltipText.gameObject.SetActive(tip != null);

        if (tip == null)
            return;

        tooltipText.text = tip;
        Vector2 textSize = tooltipText.GetPreferredValues(tip, 320f, 0f);
        textSize.x = Mathf.Min(textSize.x, 320f);
        tooltipRect.sizeDelta = textSize;

        // Beside the cursor, kept inside the panel's sides.
        Vector2 position = pointer + new Vector2(18f, -18f - textSize.y);

        if (position.x + textSize.x + 12f > size.x / 2f)
            position.x = pointer.x - 18f - textSize.x;

        position.y = Mathf.Max(position.y, -size.y / 2f + 10f);
        tooltipRect.anchoredPosition = position;
    }

    // Where the carried item is drawn: hanging off the cursor's lower right, so neither covers
    // the other.
    private Rect GhostRect
    {
        get
        {
            float cell = CellSize * 0.9f;
            return new Rect(pointer.x + 12f, pointer.y - 12f - cell, cell, cell);
        }
    }

    // The carried thing as a backpack item, for drawing it the same way.
    private Backpack.Item HeldAsItem()
    {
        WeaponAttachment attachment = AttachmentAt(heldFrom);

        if (attachment != null)
            return new Backpack.Item { Kind = Backpack.ItemKind.Attachment, Attachment = attachment };

        if (heldFrom.Kind == SpotKind.QuickSlot)
            return QuickItem(heldFrom.Index);

        return ItemAt(heldFrom);
    }

    private string TooltipFor(Spot spot)
    {
        if (spot.Kind == SpotKind.QuickSlot)
        {
            Backpack.Item quick = QuickItem(spot.Index);
            string key = GameInput.UsingGamepad ? GameInput.Label(GameInput.QuickUse) + " (WHEN SELECTED)" : GameInput.Label(GameInput.QuickSlots[spot.Index]);

            if (quick == null)
            {
                tooltipAccent = Fade(phosphor, 0.5f);
                return $"QUICK SLOT {spot.Index + 1}\n<alpha=#88><size=85%>EMPTY // DRAG A CONSUMABLE HERE</size>";
            }

            return ConsumableTooltip(quick.Consumable, $"QUICK SLOT {spot.Index + 1} // USE WITH [{key}]");
        }

        if (spot.Kind == SpotKind.ModSlot)
        {
            Gun gun = GunAt(spot.Index);

            if (gun == null)
                return null;

            WeaponAttachment fitted = gun.AttachmentIn(ModSlots[spot.Slot]);

            if (fitted == null)
            {
                tooltipAccent = Fade(phosphor, 0.5f);
                return $"{WeaponAttachment.SlotName(ModSlots[spot.Slot])} SLOT\n<alpha=#88><size=85%>EMPTY // DRAG A MOD HERE TO FIT IT</size>";
            }

            return AttachmentTooltip(fitted, "ON " + EquippedAt(spot.Index).Name);
        }

        Backpack.Item item = ItemAt(spot);

        if (item == null)
            return null;

        if (item.Kind == Backpack.ItemKind.Attachment)
        {
            PlayerWeapon.Equipped target = weapons.TargetFor(item.Attachment);
            return AttachmentTooltip(item.Attachment, target != null ? "FITS " + target.Name : "FITS NO GUN YOU CARRY");
        }

        if (item.Kind == Backpack.ItemKind.Consumable)
        {
            int slot = -1;

            for (int i = 0; i < QuickBar.SlotCount && Quick != null; i++)
            {
                if (Quick[i] == item.Consumable)
                    slot = i;
            }

            string where = $"{item.Amount} / {ConsumableInfo.StackSize(item.Consumable)} IN STACK // "
                + (slot >= 0 ? $"ON QUICK SLOT {slot + 1}" : "NOT ON THE QUICK BAR");
            return ConsumableTooltip(item.Consumable, where);
        }

        if (item.Kind == Backpack.ItemKind.Material)
        {
            tooltipAccent = Materials.Colour(item.Material);
            return $"{Materials.Name(item.Material)}  <alpha=#99><size=85%>// {item.Amount} / {Materials.StackSize} IN STACK</size>\n"
                + $"<alpha=#CC><size=85%>CRAFTING MATERIAL // {MetaProgress.MaterialCount(item.Material)} AT THE HIDEOUT</size>\n"
                + $"<alpha=#88><size=80%>LOST IF YOU DIE // SEND IT HOME FROM AN UPLINK TERMINAL</size>";
        }

        tooltipAccent = AmmoInfo.Colour(item.AmmoType);
        string users = null;

        foreach (PlayerWeapon.Equipped equipped in new[] { weapons.Main, weapons.Sidearm })
        {
            if (equipped.Gun != null && equipped.Gun.AmmoType == item.AmmoType)
                users = users == null ? equipped.Name : users + ", " + equipped.Name;
        }

        return $"{AmmoInfo.Name(item.AmmoType)}  <alpha=#99><size=85%>// {item.Amount} / {Backpack.StackSize(item.AmmoType)} IN STACK</size>\n"
            + $"<alpha=#CC><size=85%>CARRYING {ammo.Count(item.AmmoType)} IN ALL</size>\n"
            + $"<alpha=#88><size=85%>{(users != null ? "IN USE // " + users : "NOT USED BY YOUR GUNS")}</size>";
    }

    private string ConsumableTooltip(ConsumableType type, string where)
    {
        tooltipAccent = ConsumableInfo.Colour(type);
        return $"{ConsumableInfo.Name(type)}  <alpha=#99><size=85%>// CARRYING {Bag.ConsumableCount(type)}</size>\n"
            + $"<alpha=#FF><size=85%>{ConsumableInfo.Describe(type)}</size>\n"
            + $"<alpha=#88><size=80%>{where}</size>";
    }

    private string AttachmentTooltip(WeaponAttachment attachment, string where)
    {
        tooltipAccent = attachment.colour;
        string effect = attachment.Describe();
        return $"{attachment.name.ToUpperInvariant()}  <alpha=#99><size=85%>// {WeaponAttachment.SlotName(attachment.slot)}</size>\n"
            + $"<alpha=#FF><size=85%>{(effect.Length > 0 ? effect : "NO EFFECT")}</size>\n"
            + $"<alpha=#88><size=80%>{attachment.FitDescription()} // {where}</size>";
    }
}
