using System;
using UnityEngine;

// The grid in the inventory: spare weapon attachments, the player's ammo, consumables
// (medkits, grenades and so on) and crafting materials, one item per cell, kept sorted (ammo, then
// consumables, then loot; see Backpack.Sorting). Materials are only safe once sent home from an uplink terminal;
// whatever's still in here when the player dies is lost with the run. Ammo is carried as stacks of up to StackSize rounds, and consumables as stacks of
// ConsumableInfo.StackSize; there's no limit per type, only the cells, so the player can fill
// the whole backpack with ammo if they like. AmmoInventory reads and changes the ammo through
// here, and the QuickBar uses consumables from it.
// Added to the player by whichever of AmmoInventory and PlayerWeapon wakes first (see For).
public partial class Backpack : MonoBehaviour
{
    public const int Columns = 6;
    public const int Rows = 4;
    public const int CellCount = Columns * Rows;

    public enum ItemKind
    {
        Attachment,
        Ammo,
        Consumable,
        Material
    }

    public class Item
    {
        public ItemKind Kind;
        public WeaponAttachment Attachment;
        public AmmoType AmmoType;
        public ConsumableType Consumable;
        public MaterialType Material;
        public int Amount;      // stacks: rounds (or consumables, or materials) in this stack

        public bool IsStack => Kind != ItemKind.Attachment;

        public int StackLimit
        {
            get
            {
                switch (Kind)
                {
                    case ItemKind.Ammo: return StackSize(AmmoType);
                    case ItemKind.Consumable: return ConsumableInfo.StackSize(Consumable);
                    case ItemKind.Material: return Materials.StackSize;
                    default: return 1;
                }
            }
        }

        // The same kind of stack: the same ammo, consumable or material.
        public bool StacksWith(Item other)
        {
            if (other == null || !IsStack || other.Kind != Kind)
                return false;

            switch (Kind)
            {
                case ItemKind.Ammo: return other.AmmoType == AmmoType;
                case ItemKind.Consumable: return other.Consumable == Consumable;
                default: return other.Material == Material;
            }
        }
    }

    private readonly Item[] cells = new Item[CellCount];

    public event Action Changed;

    // The player's backpack, added if it isn't there yet.
    public static Backpack For(GameObject owner)
    {
        Backpack backpack = owner.GetComponent<Backpack>();
        return backpack != null ? backpack : owner.AddComponent<Backpack>();
    }

    public static int StackSize(AmmoType type)
    {
        switch (type)
        {
            case AmmoType.NineMil: return 32;
            case AmmoType.ShotgunShell: return 10;
            case AmmoType.Rifle762: return 50;
            default: return 60;
        }
    }

    // Named, as the default name for an indexer ("Item") is taken by the class above.
    [System.Runtime.CompilerServices.IndexerName("Cell")]
    public Item this[int cell] => cell >= 0 && cell < CellCount ? cells[cell] : null;

    public int UsedCells
    {
        get
        {
            int used = 0;

            foreach (Item item in cells)
            {
                if (item != null)
                    used++;
            }

            return used;
        }
    }

    public int FreeCells => CellCount - UsedCells;

    public bool HasRoomForAttachment => FirstFree() >= 0;

    // ---------- Attachments ----------

    // Into the first free cell. False if there's no room.
    public bool AddAttachment(WeaponAttachment attachment)
    {
        int free = FirstFree();

        if (attachment == null || free < 0)
            return false;

        cells[free] = new Item { Kind = ItemKind.Attachment, Attachment = attachment };
        Touched();
        return true;
    }

    // Into a particular cell, if it's free. False if not.
    public bool PlaceAttachment(WeaponAttachment attachment, int cell)
    {
        if (attachment == null || cell < 0 || cell >= CellCount || cells[cell] != null)
            return false;

        cells[cell] = new Item { Kind = ItemKind.Attachment, Attachment = attachment };
        Touched();
        return true;
    }

    public WeaponAttachment TakeAttachment(int cell)
    {
        Item item = this[cell];

        if (item == null || item.Kind != ItemKind.Attachment)
            return null;

        cells[cell] = null;
        Touched();
        return item.Attachment;
    }

    // ---------- Stacks ----------

    // Takes a whole stack (ammo, consumables or materials) out, to drop it. Returns how many it held.
    public int TakeStack(int cell)
    {
        Item item = this[cell];

        if (item == null || !item.IsStack)
            return 0;

        cells[cell] = null;
        Touched();
        return item.Amount;
    }

    // ---------- Arranging ----------

    // Moves whatever is in one cell to another. Onto a stack of the same ammo, consumable or material,
    // it's merged in as far as it fits (what doesn't fit stays behind); onto anything else, the
    // two swap places.
    public void Move(int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= CellCount || to >= CellCount)
            return;

        Item moving = cells[from];
        Item there = cells[to];

        if (moving != null && moving.StacksWith(there))
        {
            int add = Mathf.Min(moving.Amount, there.StackLimit - there.Amount);
            there.Amount += add;
            moving.Amount -= add;

            if (moving.Amount <= 0)
                cells[from] = null;

            // Already full: just swap them.
            if (add == 0)
            {
                cells[from] = there;
                cells[to] = moving;
            }
        }
        else
        {
            cells[from] = there;
            cells[to] = moving;
        }

        Touched();
    }

    private static bool IsStack(Item item, AmmoType type)
    {
        return item != null && item.Kind == ItemKind.Ammo && item.AmmoType == type;
    }

    private static bool IsStack(Item item, ConsumableType type)
    {
        return item != null && item.Kind == ItemKind.Consumable && item.Consumable == type;
    }

    private static bool IsStack(Item item, MaterialType type)
    {
        return item != null && item.Kind == ItemKind.Material && item.Material == type;
    }

    private int FirstFree()
    {
        for (int i = 0; i < CellCount; i++)
        {
            if (cells[i] == null)
                return i;
        }

        return -1;
    }
}
