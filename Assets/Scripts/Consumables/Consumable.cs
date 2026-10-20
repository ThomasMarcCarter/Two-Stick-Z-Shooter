using UnityEngine;

public enum ConsumableType
{
    Medkit,
    LargeMedkit,
    Adrenaline,
    Grenade,
    Molotov,
    Bottle
}

// Per-type details for the things the player carries in the backpack and uses from the quick
// bar: names, how many fit in a stack, the colour their pickups and cells glow, what they do,
// and how they're used (held for a moment like a medkit, or thrown).
public static class ConsumableInfo
{
    public static readonly ConsumableType[] All =
    {
        ConsumableType.Medkit, ConsumableType.LargeMedkit, ConsumableType.Adrenaline,
        ConsumableType.Grenade, ConsumableType.Molotov, ConsumableType.Bottle
    };

    public const int SmallHeal = 1;
    public const int LargeHeal = 2;
    public const float AdrenalineTime = 10f;

    public static string Name(ConsumableType type)
    {
        switch (type)
        {
            case ConsumableType.Medkit: return "MEDKIT";
            case ConsumableType.LargeMedkit: return "LARGE MEDKIT";
            case ConsumableType.Adrenaline: return "ADRENALINE";
            case ConsumableType.Grenade: return "GRENADE";
            case ConsumableType.Molotov: return "MOLOTOV";
            default: return "BOTTLE";
        }
    }

    public static int StackSize(ConsumableType type)
    {
        switch (type)
        {
            case ConsumableType.Medkit: return 3;
            case ConsumableType.LargeMedkit: return 2;
            case ConsumableType.Adrenaline: return 3;
            case ConsumableType.Grenade: return 3;
            case ConsumableType.Molotov: return 3;
            default: return 5;
        }
    }

    public static Color Colour(ConsumableType type)
    {
        switch (type)
        {
            case ConsumableType.Medkit:
            case ConsumableType.LargeMedkit: return new Color(1f, 0.25f, 0.25f);
            case ConsumableType.Adrenaline: return new Color(0.75f, 1f, 0.2f);
            case ConsumableType.Grenade: return new Color(0.55f, 0.75f, 0.35f);
            case ConsumableType.Molotov: return new Color(1f, 0.5f, 0.1f);
            default: return new Color(0.45f, 0.85f, 0.75f);
        }
    }

    public static string Describe(ConsumableType type)
    {
        switch (type)
        {
            case ConsumableType.Medkit: return $"HEALS {SmallHeal} // {UseTime(type):0.#}S TO USE";
            case ConsumableType.LargeMedkit: return $"HEALS {LargeHeal} // {UseTime(type):0.#}S TO USE";
            case ConsumableType.Adrenaline: return $"{AdrenalineTime:0}S OF FREE SPRINTING, +15% SPEED";
            case ConsumableType.Grenade: return "THROWN // EXPLODES AFTER A SHORT FUSE";
            case ConsumableType.Molotov: return "THROWN // BURSTS INTO A POOL OF FIRE";
            default: return "THROWN // SMASHES LOUDLY, DRAWING ZOMBIES TO IT";
        }
    }

    // Seconds spent using it, during which a hit interrupts it; thrown things are instant.
    public static float UseTime(ConsumableType type)
    {
        switch (type)
        {
            case ConsumableType.Medkit: return 1f;
            case ConsumableType.LargeMedkit: return 1.6f;
            case ConsumableType.Adrenaline: return 0.4f;
            default: return 0f;
        }
    }

    public static bool IsThrown(ConsumableType type)
    {
        return type == ConsumableType.Grenade || type == ConsumableType.Molotov || type == ConsumableType.Bottle;
    }

    public static bool IsMedkit(ConsumableType type)
    {
        return type == ConsumableType.Medkit || type == ConsumableType.LargeMedkit;
    }

    public static int Heal(ConsumableType type)
    {
        return type == ConsumableType.LargeMedkit ? LargeHeal : type == ConsumableType.Medkit ? SmallHeal : 0;
    }
}
