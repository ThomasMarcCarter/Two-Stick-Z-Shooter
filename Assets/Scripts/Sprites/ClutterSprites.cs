using UnityEngine;

// Which clutter sprite stands in for each clutter prefab (by name: the art packs' boxes, trash
// and litter), and how to size it to the prop (Tools/Sprites/clutter.py draws each item filling
// its frame's width or height). Boxes and big trash stand upright and cast shadows; small trash
// stands upright without, and shows its knocked-over frame (row 1) when the prop is lying down;
// litter, sheets, planks and tyres lie flat on the floor. Each zone's own clutter ("Zone_...")
// is looked up in ZoneClutter.
public static class ClutterSprites
{
    // With the sprite art: the prop drawn as its clutter sprite, its colliders and physics as
    // they were. The floor's dressing, and a prop's broken remains, go through here.
    public static void Dress(GameObject prop)
    {
        DressAs(prop, prop != null ? prop.name : "");
    }

    // The same, choosing the sprite by another name (a shot box's remains are a flattened box).
    public static void DressAs(GameObject prop, string name)
    {
        SpriteVisuals visuals = SpriteVisuals.Current;

        if (visuals == null || prop == null || !TryFind(name, out Entry entry))
            return;

        ItemSprite.ApplyProp(prop, visuals.For(entry.Sheet), entry);
    }

    public enum Sheet { Boxes, Small, Flat }

    public struct Entry
    {
        public Sheet Sheet;
        public Vector2Int Frame;
        public bool FitHeight;      // sized by the prop's height, else by its width
        public float ContentPixels; // how many pixels of the frame the item fills that way
        public bool CastsShadow;
    }

    // How much smaller than the art pack makes it a prop is placed with the sprite art: the pack's
    // bottles and drinks cups are huge next to the (smaller) character sprites.
    public static float SizeFactor(string prefabName)
    {
        string name = prefabName.Replace("(Clone)", "").Trim();

        if (name.StartsWith("GlassBottle") || name.StartsWith("Shampoo") || name.StartsWith("Soap") || name.StartsWith("WaterBottle"))
            return 0.3f;

        if (name.StartsWith("Coffee_Cup") || name.StartsWith("Soda_Cup"))
            return 0.35f;

        // Trash bags: the pack's are 1.8 m across; about 0.8 m.
        if (name.StartsWith("TrashBag"))
            return 0.45f;

        // The gas can, drawn as an oil drum: about 0.9 m tall.
        if (name.StartsWith("GasCan"))
            return 0.7f;

        return 1f;
    }

    // The small trash frames' drawn heights (or widths, for the brick and fry box).
    private static readonly float[] SmallContent = { 14, 7, 14, 9, 12, 14, 14, 13, 10, 14, 14, 14, 15, 10 };

    public static bool TryFind(string prefabName, out Entry entry)
    {
        string name = prefabName.Replace("(Clone)", "").Trim();
        entry = default;

        // A zone's own clutter (ZoneClutter).
        if (name.StartsWith("Zone_"))
            return ZoneClutter.TryFind(name, out entry);

        // Lying flat.
        int flat = FlatFrame(name);

        if (flat >= 0)
        {
            entry = new Entry { Sheet = Sheet.Flat, Frame = new Vector2Int(flat, 0), ContentPixels = 30 };
            return true;
        }

        // Boxes and big trash.
        if (name.StartsWith("Box_") || name.StartsWith("CardboardBox"))
        {
            int column = name.Contains("Open_L") ? 5 : name.Contains("Open") ? 4 : name.Contains("Tall") ? 2
                : name.Contains("Long") ? 3 : name.Contains("Closed_L") || name == "CardboardBox_2" ? 1 : 0;
            entry = new Entry { Sheet = Sheet.Boxes, Frame = new Vector2Int(column, 0), FitHeight = column == 2, ContentPixels = 30, CastsShadow = true };
            return true;
        }

        int large = name.StartsWith("Barriar") ? 0 : name.StartsWith("Cinder") ? 1 : name.StartsWith("TrashBag") ? 2
            : name.StartsWith("News_Stack") ? 3 : name.StartsWith("GasCan") ? 4 : -1;

        if (large >= 0)
        {
            entry = new Entry { Sheet = Sheet.Boxes, Frame = new Vector2Int(large, 1), FitHeight = large == 1 || large == 4, ContentPixels = 30, CastsShadow = true };
            return true;
        }

        int small = SmallFrame(name);

        if (small < 0)
            return false;

        entry = new Entry { Sheet = Sheet.Small, Frame = new Vector2Int(small, 0), FitHeight = small != 9 && small != 13, ContentPixels = SmallContent[small] };
        return true;
    }

    private static int FlatFrame(string name)
    {
        if (name.StartsWith("Cardboard_Cutout")) return 0;
        if (name.StartsWith("Box_Flattened")) return 1;
        if (name.StartsWith("Concrete_Debris")) return 2;
        if (name.StartsWith("Flat_Can")) return 3;
        if (name.StartsWith("Glass_Shards")) return 4;
        if (name.StartsWith("Litter")) return 5;
        if (name.StartsWith("News_")) return name.StartsWith("News_Stack") ? -1 : 6;
        if (name.StartsWith("Paper_") && !name.StartsWith("Paper_Bag")) return 7;
        if (name.StartsWith("Pills")) return 8;
        if (name.StartsWith("Rebar")) return 9;
        if (name.StartsWith("Sheet_Metal")) return 10;
        if (name.StartsWith("Wooden_Plank")) return 11;
        if (name.StartsWith("Wheel")) return 12;
        if (name.StartsWith("Coffee_Cup_Lid") || name.StartsWith("Condom") || name.StartsWith("Mask")) return 13;
        return -1;
    }

    private static int SmallFrame(string name)
    {
        if (name.StartsWith("Can_")) return name.Contains("Crushed") ? 1 : 0;
        if (name.StartsWith("GlassBottle")) return name.Contains("Broken") ? 3 : 2;
        if (name.StartsWith("Coffee_Cup")) return name.Contains("WithLid") ? 5 : 4;
        if (name.StartsWith("Chip_Bag")) return 6;
        if (name.StartsWith("Paper_Bag")) return 7;
        if (name.StartsWith("Tin_")) return 8;
        if (name.StartsWith("Brick")) return 9;
        if (name.StartsWith("Shampoo") || name.StartsWith("Soap")) return 10;
        if (name.StartsWith("WaterBottle")) return 11;
        if (name.StartsWith("Soda_Cup")) return 12;
        if (name.StartsWith("Fry")) return 13;
        return -1;
    }
}
