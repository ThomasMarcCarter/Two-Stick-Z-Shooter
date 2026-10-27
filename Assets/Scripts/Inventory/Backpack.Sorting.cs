using System.Collections.Generic;
using System.Linq;

// Keeps the backpack sorted (user, 2026-10-27): ammo first, then consumables, then loot (crafting
// materials, then attachments), each kind in its enum order, with part-used stacks of the same
// thing merged so they take as few cells as possible, packed from the first cell with no gaps.
// It runs once at the end of any frame the contents changed, not in the middle of a change, so
// code that takes from a cell and puts something back in the same cell still works.
public partial class Backpack
{
    private bool unsorted;

    // Every change to the cells goes through here.
    private void Touched()
    {
        unsorted = true;
        Changed?.Invoke();
    }

    private void LateUpdate()
    {
        if (!unsorted)
            return;

        unsorted = false;

        if (Sort())
            Changed?.Invoke();
    }

    // Returns whether anything moved.
    private bool Sort()
    {
        List<Item> merged = new List<Item>();

        foreach (Item item in cells)
        {
            if (item == null)
                continue;

            // Into the part-used stacks of the same thing already seen.
            foreach (Item stack in merged)
            {
                if (item.Amount <= 0)
                    break;

                if (!stack.StacksWith(item) || stack.Amount >= stack.StackLimit)
                    continue;

                int add = System.Math.Min(item.Amount, stack.StackLimit - stack.Amount);
                stack.Amount += add;
                item.Amount -= add;
            }

            if (!item.IsStack || item.Amount > 0)
                merged.Add(item);
        }

        Item[] sorted = merged
            .OrderBy(SortGroup)
            .ThenBy(SortType)
            .ThenByDescending(item => item.Amount)
            .ToArray();

        bool moved = false;

        for (int i = 0; i < CellCount; i++)
        {
            Item item = i < sorted.Length ? sorted[i] : null;
            moved |= cells[i] != item;
            cells[i] = item;
        }

        return moved;
    }

    private static int SortGroup(Item item)
    {
        switch (item.Kind)
        {
            case ItemKind.Ammo: return 0;
            case ItemKind.Consumable: return 1;
            case ItemKind.Material: return 2;
            default: return 3;
        }
    }

    private static int SortType(Item item)
    {
        switch (item.Kind)
        {
            case ItemKind.Ammo: return (int)item.AmmoType;
            case ItemKind.Consumable: return (int)item.Consumable;
            case ItemKind.Material: return (int)item.Material;
            default: return (int)item.Attachment.slot;
        }
    }
}
