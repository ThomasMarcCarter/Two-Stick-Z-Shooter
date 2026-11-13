using System;
using UnityEngine;

// The Backpack's stacks by kind: ammo, consumables and crafting materials, each counted, topped
// up and taken across every stack of its type.
public partial class Backpack
{
    // ---------- Shared ----------

    // Tops up the stacks `isStack` picks (in cell order, up to `size` each), then, if allowed,
    // starts new ones made by `newStack(amount)`. Returns how many went in.
    private int AddStacked(int amount, int size, bool newStacks, Func<Item, bool> isStack, Func<int, Item> newStack)
    {
        int left = Mathf.Max(0, amount);

        for (int i = 0; i < CellCount && left > 0; i++)
        {
            Item item = cells[i];

            if (!isStack(item) || item.Amount >= size)
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
            cells[free] = newStack(add);
            left -= add;
        }

        int added = Mathf.Max(0, amount) - left;

        if (added > 0)
            Touched();

        return added;
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
        return AddStacked(amount, StackSize(type), newStacks, item => IsStack(item, type),
            add => new Item { Kind = ItemKind.Ammo, AmmoType = type, Amount = add });
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
            Touched();

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
        return AddStacked(amount, ConsumableInfo.StackSize(type), newStacks, item => IsStack(item, type),
            add => new Item { Kind = ItemKind.Consumable, Consumable = type, Amount = add });
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

        Touched();
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
        return AddStacked(amount, Materials.StackSize, true, item => IsStack(item, type),
            add => new Item { Kind = ItemKind.Material, Material = type, Amount = add });
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
            Touched();

        return taken;
    }
}
