using System.Collections.Generic;
using UnityEngine;

// One thing inside a supply crate (SupplyCrate): a gun (with its rounds and attachments), a box
// of ammo, consumables, an attachment, a melee upgrade or crafting materials. Knows how to show
// itself on the crate screen and how to go to the player when picked: ammo, consumables and
// materials go in the backpack (as much as fits, the rest stays in the crate); an attachment is
// fitted to a gun with that slot free, stored, or swapped onto a gun if the pack is full; a gun
// goes into its slot and the one it replaces goes back in the crate; a melee upgrade is applied.
public class CrateItem
{
    public enum ItemKind
    {
        Gun,
        Ammo,
        Consumable,
        Attachment,
        MeleeUpgrade,
        Material
    }

    public ItemKind Kind { get; private set; }
    public GameObject Weapon { get; private set; }
    public List<WeaponAttachment> Fitted { get; private set; }
    public AmmoType AmmoType { get; private set; }
    public ConsumableType Consumable { get; private set; }
    public WeaponAttachment Attachment { get; private set; }
    public MeleeUpgrade Upgrade { get; private set; }
    public MaterialType Material { get; private set; }

    // Rounds in a gun's magazine, or how many of a stack.
    public int Amount { get; private set; }

    public bool IsEmpty => Kind != ItemKind.Gun && Kind != ItemKind.Attachment && Kind != ItemKind.MeleeUpgrade && Amount <= 0;

    public static CrateItem ForGun(GameObject weapon, int ammo, IEnumerable<WeaponAttachment> fitted = null)
    {
        return new CrateItem
        {
            Kind = ItemKind.Gun,
            Weapon = weapon,
            Amount = ammo,
            Fitted = fitted != null ? new List<WeaponAttachment>(fitted) : new List<WeaponAttachment>()
        };
    }

    public static CrateItem ForAmmo(AmmoType type, int amount) => new CrateItem { Kind = ItemKind.Ammo, AmmoType = type, Amount = amount };
    public static CrateItem ForConsumable(ConsumableType type, int amount) => new CrateItem { Kind = ItemKind.Consumable, Consumable = type, Amount = amount };
    public static CrateItem ForAttachment(WeaponAttachment attachment) => new CrateItem { Kind = ItemKind.Attachment, Attachment = attachment, Amount = 1 };
    public static CrateItem ForMeleeUpgrade(MeleeUpgrade upgrade) => new CrateItem { Kind = ItemKind.MeleeUpgrade, Upgrade = upgrade, Amount = 1 };
    public static CrateItem ForMaterial(MaterialType type, int amount) => new CrateItem { Kind = ItemKind.Material, Material = type, Amount = amount };

    // Whether another item can simply be added onto this one (materials of a kind share a row).
    public bool Merges(CrateItem other)
    {
        return other.Kind == Kind && Kind == ItemKind.Material && other.Material == Material;
    }

    public void Merge(CrateItem other)
    {
        Amount += other.Amount;
    }

    public Color Colour
    {
        get
        {
            switch (Kind)
            {
                case ItemKind.Ammo: return AmmoInfo.Colour(AmmoType);
                case ItemKind.Consumable: return ConsumableInfo.Colour(Consumable);
                case ItemKind.Attachment: return Attachment.colour;
                case ItemKind.MeleeUpgrade: return MeleeUpgrades.Colour(Upgrade);
                case ItemKind.Material: return Materials.Colour(Material);
                default: return Color.white;
            }
        }
    }

    // The name and count, e.g. "AK-47  30 RDS" or "9MM ROUNDS x48".
    public string Title
    {
        get
        {
            switch (Kind)
            {
                case ItemKind.Gun:
                    Gun gun = Weapon.GetComponent<Gun>();
                    return $"{Gun.DisplayName(Weapon)}  <alpha=#99>{Amount}/{gun.GetMagazineSize()} RDS";
                case ItemKind.Ammo: return $"{AmmoInfo.Name(AmmoType)} x{Amount}";
                case ItemKind.Consumable: return ConsumableInfo.Name(Consumable) + (Amount > 1 ? $" x{Amount}" : "");
                case ItemKind.Attachment: return Attachment.name.ToUpperInvariant();
                case ItemKind.MeleeUpgrade: return MeleeUpgrades.Name(Upgrade);
                default: return $"{Materials.Name(Material)} x{Amount}";
            }
        }
    }

    // What it is or does, under the title.
    public string Detail
    {
        get
        {
            switch (Kind)
            {
                case ItemKind.Gun:
                    string slot = Weapon.GetComponent<Gun>().EquipSlot == Gun.Slot.Main ? "MAIN WEAPON" : "SIDEARM";
                    return Fitted.Count > 0 ? $"{slot} // {Fitted.Count} ATTACHMENT{(Fitted.Count > 1 ? "S" : "")} FITTED" : slot;
                case ItemKind.Ammo: return "AMMUNITION";
                case ItemKind.Consumable: return ConsumableInfo.Describe(Consumable);
                case ItemKind.Attachment: return $"{WeaponAttachment.SlotName(Attachment.slot)} // {Attachment.Describe()}";
                case ItemKind.MeleeUpgrade: return "MELEE UPGRADE // " + MeleeUpgrades.Describe(Upgrade);
                default: return "CRAFTING MATERIAL // LOST IF YOU DIE // SEND IT HOME FROM AN UPLINK";
            }
        }
    }

    // What picking it would do right now ("SWAPS FOR YOUR SHOTGUN"), or why it can't be taken
    // (`blocked` true then).
    public string Action(PlayerWeapon player, out bool blocked)
    {
        blocked = false;
        Backpack backpack = player.Backpack;

        switch (Kind)
        {
            case ItemKind.Gun:
                PlayerWeapon.Equipped slot = Weapon.GetComponent<Gun>().EquipSlot == Gun.Slot.Main ? player.Main : player.Sidearm;
                return slot.Gun != null ? "SWAPS FOR YOUR " + slot.Name + " (IT GOES IN THE CRATE)" : "TAKE";

            case ItemKind.Ammo:
                blocked = backpack.RoomForAmmo(AmmoType) <= 0;
                return blocked ? "BACKPACK FULL" : backpack.RoomForAmmo(AmmoType) < Amount ? "TAKE WHAT FITS" : "TAKE";

            case ItemKind.Consumable:
                blocked = backpack.RoomFor(Consumable) <= 0;
                return blocked ? "BACKPACK FULL" : backpack.RoomFor(Consumable) < Amount ? "TAKE WHAT FITS" : "TAKE";

            case ItemKind.Material:
                blocked = !HasRoomForMaterial(backpack);
                return blocked ? "BACKPACK FULL" : "TAKE";

            case ItemKind.MeleeUpgrade:
                blocked = player.Melee.IsMaxed(Upgrade);
                return blocked ? "ALREADY MAXED" : "APPLY";

            default:
                switch (AttachmentPickup.Decide(player, Attachment, out PlayerWeapon.Equipped target))
                {
                    case AttachmentPickup.Take.Fit: return "FIT TO " + target.Name;
                    case AttachmentPickup.Take.Store: return "TAKE";
                    case AttachmentPickup.Take.Swap: return $"FIT TO {target.Name} (BACKPACK FULL // {target.Gun.AttachmentIn(Attachment.slot).name.ToUpperInvariant()} GOES IN THE CRATE)";
                    default:
                        blocked = true;
                        return "BACKPACK FULL";
                }
        }
    }

    private bool HasRoomForMaterial(Backpack backpack)
    {
        for (int i = 0; i < Backpack.CellCount; i++)
        {
            Backpack.Item item = backpack[i];

            if (item == null || (item.Kind == Backpack.ItemKind.Material && item.Material == Material && item.Amount < Materials.StackSize))
                return true;
        }

        return false;
    }

    // Gives it (or as much as fits) to the player. Returns false if nothing could be taken.
    // Anything it pushes out of the player's hands (a gun, a swapped attachment) comes back as
    // `returned`, for the crate to keep.
    public bool Take(PlayerWeapon player, out CrateItem returned)
    {
        returned = null;
        Backpack backpack = player.Backpack;

        switch (Kind)
        {
            case ItemKind.Gun:
                player.TakeFromCrate(Weapon, Amount, Fitted, out PlayerWeapon.Stowed old);

                if (old != null)
                    returned = ForGun(old.Prefab, old.Ammo, old.Attachments);

                Amount = 0;
                return true;

            case ItemKind.Ammo:
                AmmoInventory ammo = player.GetComponent<AmmoInventory>();
                int rounds = ammo != null ? ammo.Add(AmmoType, Amount, true) : backpack.AddAmmo(AmmoType, Amount, true);
                Amount -= rounds;
                return rounds > 0;

            case ItemKind.Consumable:
                int added = backpack.AddConsumable(Consumable, Amount, true);
                Amount -= added;
                return added > 0;

            case ItemKind.Material:
                int materials = backpack.AddMaterial(Material, Amount);
                Amount -= materials;
                return materials > 0;

            case ItemKind.MeleeUpgrade:
                return player.Melee.AddUpgrade(Upgrade);

            default:
                AttachmentPickup.Take take = AttachmentPickup.Decide(player, Attachment, out PlayerWeapon.Equipped target);

                if (take == AttachmentPickup.Take.None)
                    return false;

                if (take == AttachmentPickup.Take.Store)
                    return backpack.AddAttachment(Attachment);

                WeaponAttachment replaced = target.Gun.Attach(Attachment);

                if (replaced != null)
                    returned = ForAttachment(replaced);

                return true;
        }
    }
}
