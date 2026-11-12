using UnityEngine;

// Which clutter sprite stands in for each clutter prefab (by name: the art packs' boxes, trash
// and litter), and how big it's drawn (Tools/Sprites/clutter.py: at 16 px/m, so many pixels wide
// or tall; the prop is sized to match, FitToArt). Boxes and big trash stand upright and cast shadows; small trash
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

        SpriteSheet sheet = visuals.For(entry.Sheet);
        FitToArt(prop, sheet, entry);
        ItemSprite.ApplyProp(prop, sheet, entry);
    }

    // The art is drawn at the world's pixel size (16 px/m), each item as big as it should be, so
    // the prop is sized to its art (not the art to the prop): the sprite then shows unscaled, and
    // the collider matches what's drawn. Its lowest point stays where it was.
    private static void FitToArt(GameObject prop, SpriteSheet sheet, Entry entry)
    {
        if (sheet == null || !ItemSprite.LocalBounds(prop, out Bounds bounds))
            return;

        Vector3 size = Vector3.Scale(bounds.size, prop.transform.lossyScale);
        float fit = entry.FitHeight ? size.y : Mathf.Max(size.x, size.z);

        if (fit < 0.001f)
            return;

        float bottom = Lowest(prop);
        prop.transform.localScale *= entry.ContentPixels / Mathf.Max(1f, sheet.pixelsPerUnit) / fit;
        prop.transform.position += Vector3.up * (bottom - Lowest(prop));
    }

    private static float Lowest(GameObject prop)
    {
        float lowest = prop.transform.position.y;

        foreach (Renderer part in prop.GetComponentsInChildren<Renderer>())
        {
            if (part is MeshRenderer)
                lowest = Mathf.Min(lowest, part.bounds.min.y);
        }

        return lowest;
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

    // The small trash frames' drawn heights (or widths, for the brick and fry box).
    private static readonly float[] SmallContent = { 4, 3, 5, 3, 3, 4, 5, 6, 4, 7, 4, 4, 4, 4 };

    // The flat litter frames' drawn widths, and the boxes' and big trash's (width, or height).
    private static readonly float[] FlatContent = { 20, 14, 18, 5, 10, 12, 12, 8, 4, 14, 20, 24, 11, 4 };
    private static readonly float[] BoxContent = { 13, 14, 14, 16, 14, 16 };
    private static readonly float[] LargeContent = { 22, 5, 12, 10, 14 };

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
            entry = new Entry { Sheet = Sheet.Flat, Frame = new Vector2Int(flat, 0), ContentPixels = FlatContent[flat] };
            return true;
        }

        // Boxes and big trash.
        if (name.StartsWith("Box_") || name.StartsWith("CardboardBox"))
        {
            int column = name.Contains("Open_L") ? 5 : name.Contains("Open") ? 4 : name.Contains("Tall") ? 2
                : name.Contains("Long") ? 3 : name.Contains("Closed_L") || name == "CardboardBox_2" ? 1 : 0;
            entry = new Entry { Sheet = Sheet.Boxes, Frame = new Vector2Int(column, 0), FitHeight = column == 2, ContentPixels = BoxContent[column], CastsShadow = true };
            return true;
        }

        int large = name.StartsWith("Barriar") ? 0 : name.StartsWith("Cinder") ? 1 : name.StartsWith("TrashBag") ? 2
            : name.StartsWith("News_Stack") ? 3 : name.StartsWith("GasCan") ? 4 : -1;

        if (large >= 0)
        {
            entry = new Entry { Sheet = Sheet.Boxes, Frame = new Vector2Int(large, 1), FitHeight = large == 1 || large == 4, ContentPixels = LargeContent[large], CastsShadow = true };
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
