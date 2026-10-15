using UnityEngine;

public enum AmmoType
{
    NineMil,
    ShotgunShell,
    Rifle762,
    Rifle556
}

// Per-type details: what it's called, how much you can carry, how much a drop gives, and the
// colour its pickups glow.
public static class AmmoInfo
{
    public static readonly AmmoType[] All =
    {
        AmmoType.NineMil, AmmoType.ShotgunShell, AmmoType.Rifle762, AmmoType.Rifle556
    };

    public static string Name(AmmoType type)
    {
        switch (type)
        {
            case AmmoType.NineMil: return "9MM";
            case AmmoType.ShotgunShell: return "12G SHELLS";
            case AmmoType.Rifle762: return "7.62MM";
            default: return "5.56MM";
        }
    }

    // Rounds in a normal drop (min inclusive, max inclusive). Crates and bosses give more.
    public static Vector2Int DropAmount(AmmoType type)
    {
        switch (type)
        {
            case AmmoType.NineMil: return new Vector2Int(6, 12);
            case AmmoType.ShotgunShell: return new Vector2Int(3, 6);
            case AmmoType.Rifle762: return new Vector2Int(10, 20);
            default: return new Vector2Int(12, 24);
        }
    }

    public static Color Colour(AmmoType type)
    {
        switch (type)
        {
            case AmmoType.NineMil: return new Color(1f, 0.85f, 0.35f);
            case AmmoType.ShotgunShell: return new Color(1f, 0.3f, 0.25f);
            case AmmoType.Rifle762: return new Color(1f, 0.55f, 0.15f);
            default: return new Color(0.4f, 0.9f, 1f);
        }
    }
}
