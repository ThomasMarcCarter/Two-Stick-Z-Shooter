using UnityEngine;

public enum MeleeUpgrade
{
    Sweep,      // hits more zombies per swing
    Edge,       // more damage
    Reach,      // longer and wider swing
    Weight,     // knocks back and staggers harder
    QuickHands, // swings more often
    Conditioning // costs less stamina
}

// The melee upgrades: what each does per level and how many levels it can go to. Picked up off
// the floor and kept for the rest of the run (PlayerMelee holds them).
public static class MeleeUpgrades
{
    public static readonly MeleeUpgrade[] All =
    {
        MeleeUpgrade.Sweep, MeleeUpgrade.Edge, MeleeUpgrade.Reach,
        MeleeUpgrade.Weight, MeleeUpgrade.QuickHands, MeleeUpgrade.Conditioning
    };

    public static string Name(MeleeUpgrade upgrade)
    {
        switch (upgrade)
        {
            case MeleeUpgrade.Sweep: return "SWEEP";
            case MeleeUpgrade.Edge: return "EDGE";
            case MeleeUpgrade.Reach: return "REACH";
            case MeleeUpgrade.Weight: return "WEIGHT";
            case MeleeUpgrade.QuickHands: return "QUICK HANDS";
            default: return "CONDITIONING";
        }
    }

    public static string Describe(MeleeUpgrade upgrade)
    {
        switch (upgrade)
        {
            case MeleeUpgrade.Sweep: return "+2 ZOMBIES HIT PER SWING";
            case MeleeUpgrade.Edge: return "+1 MELEE DAMAGE";
            case MeleeUpgrade.Reach: return "+0.35M REACH, WIDER SWING";
            case MeleeUpgrade.Weight: return "+50% KNOCKBACK AND STAGGER";
            case MeleeUpgrade.QuickHands: return "SWINGS 20% FASTER";
            default: return "SWINGS COST 35% LESS STAMINA";
        }
    }

    public static int MaxLevel(MeleeUpgrade upgrade)
    {
        switch (upgrade)
        {
            case MeleeUpgrade.Sweep: return 3;
            case MeleeUpgrade.Edge: return 3;
            case MeleeUpgrade.Reach: return 2;
            case MeleeUpgrade.Weight: return 2;
            case MeleeUpgrade.QuickHands: return 2;
            default: return 2;
        }
    }

    public static Color Colour(MeleeUpgrade upgrade)
    {
        switch (upgrade)
        {
            case MeleeUpgrade.Sweep: return new Color(1f, 0.85f, 0.3f);
            case MeleeUpgrade.Edge: return new Color(1f, 0.35f, 0.3f);
            case MeleeUpgrade.Reach: return new Color(0.4f, 0.8f, 1f);
            case MeleeUpgrade.Weight: return new Color(0.75f, 0.6f, 1f);
            case MeleeUpgrade.QuickHands: return new Color(0.45f, 1f, 0.55f);
            default: return new Color(1f, 0.6f, 0.25f);
        }
    }

    // Roman numerals for levels on labels.
    public static string Level(int level)
    {
        switch (level)
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            default: return level.ToString();
        }
    }
}
