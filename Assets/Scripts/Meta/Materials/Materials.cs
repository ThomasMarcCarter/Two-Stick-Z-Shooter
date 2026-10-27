using System;
using UnityEngine;

// Crafting materials found on runs and spent, with scrap, on hideout station upgrades. Carried
// in the backpack, and at risk until sent home from an uplink terminal (see UplinkTerminal).
public enum MaterialType
{
    Parts,
    Wiring,
    Chemicals,
    Gunpowder,
    Fuel
}

// What each material is called and looks like.
public static class Materials
{
    public static readonly MaterialType[] All = (MaterialType[])Enum.GetValues(typeof(MaterialType));

    public static string Name(MaterialType type)
    {
        switch (type)
        {
            case MaterialType.Parts: return "PARTS";
            case MaterialType.Wiring: return "WIRING";
            case MaterialType.Chemicals: return "CHEMICALS";
            case MaterialType.Gunpowder: return "GUNPOWDER";
            default: return "FUEL";
        }
    }

    public static Color Colour(MaterialType type)
    {
        switch (type)
        {
            case MaterialType.Parts: return new Color(0.75f, 0.78f, 0.82f);
            case MaterialType.Wiring: return new Color(1f, 0.6f, 0.2f);
            case MaterialType.Chemicals: return new Color(0.45f, 0.85f, 1f);
            case MaterialType.Gunpowder: return new Color(1f, 0.85f, 0.35f);
            default: return new Color(1f, 0.35f, 0.3f);
        }
    }

    public static string Hex(MaterialType type) => "#" + ColorUtility.ToHtmlStringRGB(Colour(type));

    // How many of one material fit in a backpack cell.
    public const int StackSize = 10;

    // "3 PARTS, 1 FUEL" for a set of amounts (indexed by MaterialType), coloured per material
    // unless asked for plain text; empty if none.
    public static string Describe(int[] amounts, bool coloured = true)
    {
        System.Text.StringBuilder text = new System.Text.StringBuilder();

        for (int i = 0; i < amounts.Length; i++)
        {
            if (amounts[i] <= 0)
                continue;

            if (text.Length > 0)
                text.Append(", ");

            MaterialType type = (MaterialType)i;
            text.Append(coloured ? $"<color={Hex(type)}>{amounts[i]} {Name(type)}</color>" : $"{amounts[i]} {Name(type)}");
        }

        return text.ToString();
    }
}

// A material and how many of it.
[Serializable]
public struct MaterialCost
{
    public MaterialType Type;
    public int Amount;

    public MaterialCost(MaterialType type, int amount)
    {
        Type = type;
        Amount = amount;
    }
}
