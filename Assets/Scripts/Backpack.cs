using System;
using UnityEngine;

// The grid in the inventory: spare weapon attachments, the player's ammo, consumables
// (medkits, grenades and so on) and crafting materials, one item per cell, in whatever order the
// player has dragged them into. Materials are only safe once sent home from an uplink terminal;
// whatever's still in here when the player dies is lost with the run. Ammo is carried as stacks of up to StackSize rounds, and consumables as stacks of
// ConsumableInfo.StackSize; there's no limit per type, only the cells, so the player can fill
// the whole backpack with ammo if they like. AmmoInventory reads and changes the ammo through
// here, and the QuickBar uses consumables from it.
// Added to the player by whichever of AmmoInventory and PlayerWeapon wakes first (see For).
public class Backpack : MonoBehaviour
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
        Changed?.Invoke();
        return true;
    }

    // Into a particular cell, if it's free. False if not.
    public bool PlaceAttachment(WeaponAttachment attachment, int cell)
    {
        if (attachment == null || cell < 0 || cell >= CellCount || cells[cell] != null)
            return false;

        cells[cell] = new Item { Kind = ItemKind.Attachment, Attachment = attachment };
        Changed?.Invoke();
        return true;
    }

    public WeaponAttachment TakeAttachment(int cell)
    {
        Item item = this[cell];

        if (item == null || item.Kind != ItemKind.Attachment)
            return null;

        cells[cell] = null;
        Changed?.Invoke();
        return item.Attachment;
    }

    // ---------- Ammo ----------

    public int AmmoCount(AmmoType type)
    {
        int count = 0;

        foreach (Item item in cells)
        {
            if (IsStack(item, type))
                count += item.Amount;
        }

        return count;
    }

    // Rounds that would fit in the stacks already there, without starting a new one.
    public int TopUpRoom(AmmoType type)
    {
        int room = 0;

        foreach (Item item in cells)
        {
            if (IsStack(item, type))
                room += StackSize(type) - item.Amount;
        }

        return room;
    }

    // Rounds that would fit, starting new stacks in the free cells as needed.
    public int RoomForAmmo(AmmoType type)
    {
        return TopUpRoom(type) + FreeCells * StackSize(type);
    }

    // Tops up the stacks already there (in cell order), then, if allowed, starts new ones.
    // Returns how many rounds went in.
    public int AddAmmo(AmmoType type, int amount, bool newStacks)
    {
        int size = StackSize(type);
        int left = Mathf.Max(0, amount);

        for (int i = 0; i < CellCount && left > 0; i++)
        {
            Item item = cells[i];

            if (!IsStack(item, type) || item.Amount >= size)
                continue;

            int add = Mathf.Min(left, size - item.Amount);
            item.Amount += add;
            left -= add;
        }

        while (newStacks && left > 0)
        {
            int free = FirstFree();

            if (free < 0)
                break;

            int add = Mathf.Min(left, size);
            cells[free] = new Item { Kind = ItemKind.Ammo, AmmoType = type, Amount = add };
            left -= add;
        }

        int added = Mathf.Max(0, amount) - left;

        if (added > 0)
            Changed?.Invoke();

        return added;
    }

    // Takes from the emptiest stacks first, so part-used stacks are used up before full ones
    // are broken into. Empty stacks are removed. Returns how many rounds were taken.
    public int TakeAmmo(AmmoType type, int amount)
    {
        int left = Mathf.Max(0, amount);

        while (left > 0)
        {
            int emptiest = -1;

            for (int i = 0; i < CellCount; i++)
            {
                if (IsStack(cells[i], type) && (emptiest < 0 || cells[i].Amount < cells[emptiest].Amount))
                    emptiest = i;
            }

            if (emptiest < 0)
                break;

            int take = Mathf.Min(left, cells[emptiest].Amount);
            cells[emptiest].Amount -= take;
            left -= take;

            if (cells[emptiest].Amount <= 0)
                cells[emptiest] = null;
        }

        int taken = Mathf.Max(0, amount) - left;

        if (taken > 0)
            Changed?.Invoke();

        return taken;
    }

    // ---------- Consumables ----------

    public int ConsumableCount(ConsumableType type)
    {
        int count = 0;

        foreach (Item item in cells)
        {
            if (IsStack(item, type))
                count += item.Amount;
        }

        return count;
    }

    // How many would fit in the stacks already there, without starting a new one.
    public int TopUpRoom(ConsumableType type)
    {
        int room = 0;

        foreach (Item item in cells)
        {
            if (IsStack(item, type))
                room += ConsumableInfo.StackSize(type) - item.Amount;
        }

        return room;
    }

    public int RoomFor(ConsumableType type)
    {
        return TopUpRoom(type) + FreeCells * ConsumableInfo.StackSize(type);
    }

    // Like AddAmmo: tops up the stacks already there, then, if allowed, starts new ones.
    // Returns how many went in.
    public int AddConsumable(ConsumableType type, int amount, bool newStacks)
    {
        int size = ConsumableInfo.StackSize(type);
        int left = Mathf.Max(0, amount);

        for (int i = 0; i < CellCount && left > 0; i++)
        {
            Item item = cells[i];

            if (!IsStack(item, type) || item.Amount >= size)
                continue;

            int add = Mathf.Min(left, size - item.Amount);
            item.Amount += add;
            left -= add;
        }

        while (newStacks && left > 0)
        {
            int free = FirstFree();

            if (free < 0)
                break;

            int add = Mathf.Min(left, size);
            cells[free] = new Item { Kind = ItemKind.Consumable, Consumable = type, Amount = add };
            left -= add;
        }

        int added = Mathf.Max(0, amount) - left;

        if (added > 0)
            Changed?.Invoke();

        return added;
    }

    // Uses one up, from the emptiest stack, so a part-used stack goes first. False if there
    // are none.
    public bool TakeConsumable(ConsumableType type)
    {
        int emptiest = -1;

        for (int i = 0; i < CellCount; i++)
        {
            if (IsStack(cells[i], type) && (emptiest < 0 || cells[i].Amount < cells[emptiest].Amount))
                emptiest = i;
        }

        if (emptiest < 0)
            return false;

        if (--cells[emptiest].Amount <= 0)
            cells[emptiest] = null;

        Changed?.Invoke();
        return true;
    }

    // ---------- Materials ----------

    public int MaterialCount(MaterialType type)
    {
        int count = 0;

        foreach (Item item in cells)
        {
            if (IsStack(item, type))
                count += item.Amount;
        }

        return count;
    }

    // Every material carried, indexed by MaterialType.
    public int[] MaterialCounts()
    {
        int[] counts = new int[Materials.All.Length];

        foreach (Item item in cells)
        {
            if (item != null && item.Kind == ItemKind.Material)
                counts[(int)item.Material] += item.Amount;
        }

        return counts;
    }

    // Tops up the stacks already there, then starts new ones in free cells. Returns how many went in.
    public int AddMaterial(MaterialType type, int amount)
    {
        int left = Mathf.Max(0, amount);

        for (int i = 0; i < CellCount && left > 0; i++)
        {
            Item item = cells[i];

            if (!IsStack(item, type) || item.Amount >= Materials.StackSize)
                continue;

            int add = Mathf.Min(left, Materials.StackSize - item.Amount);
            item.Amount += add;
            left -= add;
        }

        while (left > 0)
        {
            int free = FirstFree();

            if (free < 0)
                break;

            int add = Mathf.Min(left, Materials.StackSize);
            cells[free] = new Item { Kind = ItemKind.Material, Material = type, Amount = add };
            left -= add;
        }

        int added = Mathf.Max(0, amount) - left;

        if (added > 0)
            Changed?.Invoke();

        return added;
    }

    // Empties out every material stack (sent home, or lost). Returns what was taken, indexed by
    // MaterialType.
    public int[] TakeAllMaterials()
    {
        int[] taken = MaterialCounts();
        bool any = false;

        for (int i = 0; i < CellCount; i++)
        {
            if (cells[i] != null && cells[i].Kind == ItemKind.Material)
            {
                cells[i] = null;
                any = true;
            }
        }

        if (any)
            Changed?.Invoke();

        return taken;
    }

    // ---------- Stacks ----------

    // Takes a whole stack (ammo, consumables or materials) out, to drop it. Returns how many it held.
    public int TakeStack(int cell)
    {
        Item item = this[cell];

        if (item == null || !item.IsStack)
            return 0;

        cells[cell] = null;
        Changed?.Invoke();
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

        Changed?.Invoke();
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
