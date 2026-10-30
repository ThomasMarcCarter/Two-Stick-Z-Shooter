using UnityEngine;

// Where each item's picture is on the items atlas (SpriteVisuals.items, Items.png from
// Tools/Sprites/more_sprites.py): column and row of its 16x16 frame. Row 0 consumables, 1 ammo
// drops, 2 ammo crates, 3 crafting materials, 4 melee upgrades, 5 weapon attachments, 6-7 run items,
// the armour plate and the powerup case.
public static class ItemSprites
{
    public static Vector2Int Consumable(ConsumableType type) => new Vector2Int((int)type, 0);

    public static Vector2Int Ammo(AmmoType type, bool crate) => new Vector2Int((int)type, crate ? 2 : 1);

    public static Vector2Int Material(MaterialType type) => new Vector2Int((int)type, 3);

    public static Vector2Int Melee(MeleeUpgrade upgrade) => new Vector2Int((int)upgrade, 4);

    public static Vector2Int RunItem(RunItem item) => new Vector2Int((int)item % 10, 6 + (int)item / 10);

    public static readonly Vector2Int ArmorPlate = new Vector2Int(3, 7);
    public static readonly Vector2Int PowerupCase = new Vector2Int(4, 7);

    // By name, as the attachments are a list rather than an enum (WeaponAttachment.All).
    public static Vector2Int Attachment(string name)
    {
        switch (name)
        {
            case "Suppressor": return new Vector2Int(0, 5);
            case "Choke": return new Vector2Int(1, 5);
            case "Long Barrel": return new Vector2Int(2, 5);
            case "Match Barrel": return new Vector2Int(3, 5);
            case "Extended Mag": return new Vector2Int(4, 5);
            case "Drum Mag": return new Vector2Int(5, 5);
            case "Quick Mag": return new Vector2Int(6, 5);
            case "Hair Trigger": return new Vector2Int(7, 5);
            case "Auto Sear": return new Vector2Int(8, 5);
            default: return new Vector2Int(9, 5);   // Laser Sight
        }
    }
}
