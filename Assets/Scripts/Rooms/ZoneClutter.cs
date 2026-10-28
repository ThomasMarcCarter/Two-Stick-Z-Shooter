using UnityEngine;

// What each zone leaves lying about its rooms (2026-10-27), so the clutter fits the place: ammo
// crates, footlockers and sandbags on the base; trolleys, mannequins, clothes rails and shoe boxes
// in the mall; wheelchairs, IV stands, waste bins, linen carts and gurneys in the hospital;
// suitcases, luggage trolleys and queue posts in the airport, with each zone's own small things
// and floor litter. Cardboard boxes stay the base's main clutter and thin out elsewhere, and only
// the art pack's trash that belongs in the zone is used (no bricks in a gate lounge). A zone item
// is one of the art pack's props (its stand-in, for the physics: a breakable box, a pushable tin,
// fixed cover) stretched to the item's size and drawn as the item's sprite (Clutter/Boxes.png rows
// 2-5, Small.png columns 14+, Flat.png row 1; Tools/Sprites/clutter.py). Shot cases and crates
// break into their own remains: clothes, or splinters. Placed by FloorGenerator.Trash.cs and
// FloorGenerator.Dressing.cs.
public static class ZoneClutter
{
    public readonly struct Item
    {
        public readonly string Name;        // the sprite's name, "Zone_..."
        public readonly string StandIn;     // the art pack prop it's built on (name prefix)
        public readonly Vector3 Size;       // width, height, depth in metres
        public readonly ClutterSprites.Sheet Sheet;
        public readonly Vector2Int Frame;
        public readonly bool FitHeight;
        public readonly float Content;      // drawn pixels along the fitted side
        public readonly Prop.Surface Surface;
        public readonly string Remains;     // what a breakable box stand-in leaves when shot

        public Item(string name, string standIn, Vector3 size, ClutterSprites.Sheet sheet, Vector2Int frame, bool fitHeight, float content,
            Prop.Surface surface = Prop.Surface.Plastic, string remains = "Box_Flattened")
        {
            Name = name;
            StandIn = standIn;
            Size = size;
            Sheet = sheet;
            Frame = frame;
            FitHeight = fitHeight;
            Content = content;
            Surface = surface;
            Remains = remains;
        }
    }

    public sealed class Kit
    {
        public float BoxShare;          // chance a box cluster is still cardboard boxes
        public Item[] Clusters;         // what stands in its place, against the walls and out in the room
        public float LargeShare;        // chance a big piece against a wall is the zone's (else a trash bag or tyre)
        public Item[] Large;
        public float SmallShare;        // chance a small thing is the zone's (else from SmallPrefabs)
        public Item[] Small;
        public string[] SmallPrefabs;   // the art pack's small trash that belongs here (name prefixes)
        public float FlatShare;         // chance a bit of litter is the zone's (else from LitterPrefabs)
        public Item[] Flat;
        public string[] LitterPrefabs;  // the art pack's flat litter that belongs here (name prefixes)
        public float SheetShare;        // times the flattened cardboard sheets
    }

    private const ClutterSprites.Sheet Upright = ClutterSprites.Sheet.Boxes;
    private const ClutterSprites.Sheet Small = ClutterSprites.Sheet.Small;
    private const ClutterSprites.Sheet Flat = ClutterSprites.Sheet.Flat;

    // Stand-ins: "Wheel_1" is fixed cover, "Box_" a breakable cardboard box, the rest pushable.
    private static readonly Item[] Items =
    {
        // the Military Base
        new Item("Zone_AmmoCrate", "Box_Closed_Taped", new Vector3(0.9f, 0.45f, 0.5f), Upright, new Vector2Int(0, 2), false, 30, remains: "Zone_Splinters"),
        new Item("Zone_Footlocker", "Box_Long_Closed", new Vector3(1f, 0.4f, 0.5f), Upright, new Vector2Int(1, 2), false, 30, remains: "Zone_Splinters"),
        new Item("Zone_Sandbags", "Wheel_1", new Vector3(1.2f, 0.6f, 0.6f), Upright, new Vector2Int(2, 2), false, 30, Prop.Surface.Rubber),
        new Item("Zone_JerryCan", "GasCan", new Vector3(0.35f, 0.45f, 0.2f), Small, new Vector2Int(14, 0), true, 14, Prop.Surface.Metal),
        new Item("Zone_AmmoTin", "Tin_1", new Vector3(0.3f, 0.2f, 0.15f), Small, new Vector2Int(15, 0), true, 9, Prop.Surface.Metal),
        new Item("Zone_Helmet", "Tin_1", new Vector3(0.3f, 0.18f, 0.3f), Small, new Vector2Int(16, 0), true, 9, Prop.Surface.Metal),
        new Item("Zone_RationPack", "Chip_Bag_1", new Vector3(0.2f, 0.25f, 0.06f), Small, new Vector2Int(17, 0), true, 12),
        new Item("Zone_Splinters", "Cardboard_Cutout_1", new Vector3(0.9f, 0.02f, 0.9f), Flat, new Vector2Int(0, 1), false, 30),
        new Item("Zone_Brass", "Cardboard_Cutout_1", new Vector3(0.5f, 0.02f, 0.5f), Flat, new Vector2Int(1, 1), false, 30),
        new Item("Zone_TornSandbag", "Cardboard_Cutout_1", new Vector3(0.9f, 0.02f, 0.9f), Flat, new Vector2Int(2, 1), false, 30),
        new Item("Zone_FieldMap", "Cardboard_Cutout_1", new Vector3(0.6f, 0.02f, 0.6f), Flat, new Vector2Int(3, 1), false, 30),
        new Item("Zone_RationWrappers", "Cardboard_Cutout_1", new Vector3(0.4f, 0.02f, 0.4f), Flat, new Vector2Int(4, 1), false, 30),

        // the Shopping Mall
        new Item("Zone_Trolley", "Wheel_1", new Vector3(0.9f, 1f, 0.6f), Upright, new Vector2Int(0, 3), true, 30, Prop.Surface.Metal),
        new Item("Zone_Mannequin", "Wheel_1", new Vector3(0.45f, 1.7f, 0.35f), Upright, new Vector2Int(1, 3), true, 30),
        new Item("Zone_ClothesRail", "Wheel_1", new Vector3(1.2f, 1.3f, 0.5f), Upright, new Vector2Int(2, 3), true, 30, Prop.Surface.Metal),
        new Item("Zone_ShoeBoxes", "Box_Closed_M", new Vector3(0.5f, 0.35f, 0.35f), Upright, new Vector2Int(3, 3), false, 30),
        new Item("Zone_ShoppingBag", "Paper_Bag_1", new Vector3(0.35f, 0.4f, 0.15f), Small, new Vector2Int(18, 0), true, 14, Prop.Surface.Paper),
        new Item("Zone_Shoe", "Tin_3", new Vector3(0.28f, 0.12f, 0.1f), Small, new Vector2Int(19, 0), true, 6, Prop.Surface.Rubber),
        new Item("Zone_Perfume", "GlassBottle_1", new Vector3(0.1f, 0.18f, 0.06f), Small, new Vector2Int(20, 0), true, 12, Prop.Surface.Glass),
        new Item("Zone_ClothesHeap", "Cardboard_Cutout_1", new Vector3(0.8f, 0.02f, 0.8f), Flat, new Vector2Int(5, 1), false, 30),
        new Item("Zone_Receipts", "Cardboard_Cutout_1", new Vector3(0.5f, 0.02f, 0.5f), Flat, new Vector2Int(6, 1), false, 30),
        new Item("Zone_Hangers", "Cardboard_Cutout_1", new Vector3(0.5f, 0.02f, 0.5f), Flat, new Vector2Int(7, 1), false, 30),
        new Item("Zone_SaleFlyers", "Cardboard_Cutout_1", new Vector3(0.45f, 0.02f, 0.45f), Flat, new Vector2Int(8, 1), false, 30),

        // the Hospital
        new Item("Zone_Wheelchair", "Wheel_1", new Vector3(0.8f, 0.9f, 0.65f), Upright, new Vector2Int(0, 4), true, 30, Prop.Surface.Metal),
        new Item("Zone_IVStand", "Wheel_1", new Vector3(0.4f, 1.8f, 0.4f), Upright, new Vector2Int(1, 4), true, 30, Prop.Surface.Metal),
        new Item("Zone_WasteBin", "Wheel_1", new Vector3(0.45f, 0.7f, 0.45f), Upright, new Vector2Int(2, 4), true, 30),
        new Item("Zone_LinenCart", "Wheel_1", new Vector3(1f, 1f, 0.55f), Upright, new Vector2Int(3, 4), true, 30, Prop.Surface.Metal),
        new Item("Zone_Oxygen", "Wheel_1", new Vector3(0.5f, 1.2f, 0.3f), Upright, new Vector2Int(4, 4), true, 30, Prop.Surface.Metal),
        new Item("Zone_MedicalBoxes", "Box_Closed_L", new Vector3(0.6f, 0.45f, 0.45f), Upright, new Vector2Int(5, 4), false, 30),
        new Item("Zone_Gurney", "Wheel_1", new Vector3(2f, 0.9f, 0.7f), Upright, new Vector2Int(6, 4), false, 30, Prop.Surface.Metal),
        new Item("Zone_PillBottle", "Tin_1", new Vector3(0.08f, 0.14f, 0.08f), Small, new Vector2Int(21, 0), true, 12),
        new Item("Zone_SalineBag", "Chip_Bag_1", new Vector3(0.2f, 0.3f, 0.05f), Small, new Vector2Int(22, 0), true, 12),
        new Item("Zone_SharpsBox", "Tin_2", new Vector3(0.2f, 0.25f, 0.15f), Small, new Vector2Int(23, 0), true, 13),
        new Item("Zone_Gauze", "Cardboard_Cutout_1", new Vector3(0.5f, 0.02f, 0.5f), Flat, new Vector2Int(9, 1), false, 30),
        new Item("Zone_XRay", "Cardboard_Cutout_1", new Vector3(0.45f, 0.02f, 0.45f), Flat, new Vector2Int(10, 1), false, 30),
        new Item("Zone_Syringes", "Cardboard_Cutout_1", new Vector3(0.35f, 0.02f, 0.35f), Flat, new Vector2Int(11, 1), false, 30),
        new Item("Zone_Gloves", "Cardboard_Cutout_1", new Vector3(0.35f, 0.02f, 0.35f), Flat, new Vector2Int(12, 1), false, 30),
        new Item("Zone_Chart", "Cardboard_Cutout_1", new Vector3(0.4f, 0.02f, 0.4f), Flat, new Vector2Int(13, 1), false, 30),

        // the Airport
        new Item("Zone_Suitcase", "Box_Closed_Taped", new Vector3(0.5f, 0.7f, 0.28f), Upright, new Vector2Int(0, 5), true, 30, remains: "Zone_ClothesSpill"),
        new Item("Zone_HardCase", "Box_Closed_L", new Vector3(0.55f, 0.75f, 0.3f), Upright, new Vector2Int(1, 5), true, 30, remains: "Zone_ClothesSpill"),
        new Item("Zone_Duffel", "Box_Long_Closed", new Vector3(0.8f, 0.35f, 0.35f), Upright, new Vector2Int(2, 5), false, 30, remains: "Zone_ClothesSpill"),
        new Item("Zone_LuggageTrolley", "Wheel_1", new Vector3(1.1f, 1f, 0.6f), Upright, new Vector2Int(3, 5), false, 30, Prop.Surface.Metal),
        new Item("Zone_Stanchion", "Wheel_1", new Vector3(0.3f, 1f, 0.3f), Upright, new Vector2Int(4, 5), true, 30, Prop.Surface.Metal),
        new Item("Zone_Bin", "Wheel_1", new Vector3(0.45f, 0.9f, 0.45f), Upright, new Vector2Int(5, 5), true, 30, Prop.Surface.Metal),
        new Item("Zone_Backpack", "Paper_Bag_1", new Vector3(0.35f, 0.45f, 0.2f), Small, new Vector2Int(24, 0), true, 14),
        new Item("Zone_NeckPillow", "Tin_3", new Vector3(0.3f, 0.12f, 0.25f), Small, new Vector2Int(25, 0), true, 7, Prop.Surface.Rubber),
        new Item("Zone_BoardingPasses", "Cardboard_Cutout_1", new Vector3(0.45f, 0.02f, 0.45f), Flat, new Vector2Int(14, 1), false, 30),
        new Item("Zone_ClothesSpill", "Cardboard_Cutout_1", new Vector3(1f, 0.02f, 1f), Flat, new Vector2Int(15, 1), false, 30),
        new Item("Zone_Magazine", "Cardboard_Cutout_1", new Vector3(0.35f, 0.02f, 0.35f), Flat, new Vector2Int(16, 1), false, 30),
        new Item("Zone_Passport", "Cardboard_Cutout_1", new Vector3(0.2f, 0.02f, 0.2f), Flat, new Vector2Int(17, 1), false, 30),
    };

    private static readonly Kit Base = new Kit
    {
        BoxShare = 0.6f,
        Clusters = Pick("Zone_AmmoCrate", "Zone_AmmoCrate", "Zone_Footlocker", "Zone_Sandbags"),
        LargeShare = 0.5f,
        Large = Pick("Zone_Sandbags", "Zone_AmmoCrate"),
        SmallShare = 0.35f,
        Small = Pick("Zone_JerryCan", "Zone_AmmoTin", "Zone_AmmoTin", "Zone_Helmet", "Zone_RationPack"),
        SmallPrefabs = new[] { "Can_", "Tin_", "Brick", "GasCan", "WaterBottle", "GlassBottle", "Coffee_Cup" },
        FlatShare = 0.35f,
        Flat = Pick("Zone_Splinters", "Zone_Brass", "Zone_Brass", "Zone_TornSandbag", "Zone_FieldMap", "Zone_RationWrappers"),
        LitterPrefabs = new[] { "Concrete_Debris", "Rebar", "Sheet_Metal", "Wooden_Plank", "Cardboard_Cutout", "Flat_Can", "News_", "Paper_", "Litter" },
        SheetShare = 1f
    };

    private static readonly Kit Mall = new Kit
    {
        BoxShare = 0.35f,
        Clusters = Pick("Zone_Trolley", "Zone_Mannequin", "Zone_ClothesRail", "Zone_ShoeBoxes", "Zone_ShoeBoxes"),
        LargeShare = 0.6f,
        Large = Pick("Zone_Trolley", "Zone_ClothesRail", "Zone_Mannequin"),
        SmallShare = 0.4f,
        Small = Pick("Zone_ShoppingBag", "Zone_ShoppingBag", "Zone_Shoe", "Zone_Perfume"),
        SmallPrefabs = new[] { "Can_", "Coffee_Cup", "Soda_Cup", "Chip_Bag", "Paper_Bag", "Fry", "WaterBottle", "GlassBottle", "Shampoo", "Soap" },
        FlatShare = 0.4f,
        Flat = Pick("Zone_ClothesHeap", "Zone_Receipts", "Zone_Hangers", "Zone_SaleFlyers"),
        LitterPrefabs = new[] { "Glass_Shards", "Litter", "Paper_", "News_", "Coffee_Cup_Lid", "Flat_Can", "Cardboard_Cutout", "Mask" },
        SheetShare = 0.3f
    };

    private static readonly Kit Hospital = new Kit
    {
        BoxShare = 0.2f,
        Clusters = Pick("Zone_Wheelchair", "Zone_IVStand", "Zone_WasteBin", "Zone_LinenCart", "Zone_Oxygen", "Zone_MedicalBoxes", "Zone_MedicalBoxes"),
        LargeShare = 0.7f,
        Large = Pick("Zone_Gurney", "Zone_Gurney", "Zone_LinenCart", "Zone_Wheelchair"),
        SmallShare = 0.45f,
        Small = Pick("Zone_PillBottle", "Zone_PillBottle", "Zone_SalineBag", "Zone_SharpsBox"),
        SmallPrefabs = new[] { "WaterBottle", "Coffee_Cup", "Soda_Cup", "Can_", "Shampoo", "Soap", "Paper_Bag" },
        FlatShare = 0.45f,
        Flat = Pick("Zone_Gauze", "Zone_Gauze", "Zone_XRay", "Zone_Syringes", "Zone_Gloves", "Zone_Chart"),
        LitterPrefabs = new[] { "Pills", "Mask", "Paper_", "Litter", "Glass_Shards", "News_", "Coffee_Cup_Lid" },
        SheetShare = 0.15f
    };

    private static readonly Kit Airport = new Kit
    {
        BoxShare = 0.25f,
        Clusters = Pick("Zone_Suitcase", "Zone_Suitcase", "Zone_HardCase", "Zone_Duffel", "Zone_LuggageTrolley", "Zone_Stanchion", "Zone_Bin"),
        LargeShare = 0.6f,
        Large = Pick("Zone_LuggageTrolley", "Zone_HardCase", "Zone_Suitcase", "Zone_Bin"),
        SmallShare = 0.35f,
        Small = Pick("Zone_Backpack", "Zone_NeckPillow"),
        SmallPrefabs = new[] { "WaterBottle", "Coffee_Cup", "Soda_Cup", "Chip_Bag", "Can_", "Paper_Bag", "Fry", "GlassBottle" },
        FlatShare = 0.4f,
        Flat = Pick("Zone_BoardingPasses", "Zone_BoardingPasses", "Zone_ClothesSpill", "Zone_Magazine", "Zone_Passport"),
        LitterPrefabs = new[] { "Paper_", "News_", "Litter", "Coffee_Cup_Lid", "Flat_Can", "Glass_Shards", "Mask" },
        SheetShare = 0.25f
    };

    public static Kit For(Zone zone)
    {
        switch (zone)
        {
            case Zone.ShoppingMall: return Mall;
            case Zone.Hospital: return Hospital;
            case Zone.Airport: return Airport;
            default: return Base;
        }
    }

    // A zone item's sprite (ClutterSprites), by its name.
    public static bool TryFind(string name, out ClutterSprites.Entry entry)
    {
        foreach (Item item in Items)
        {
            if (item.Name != name)
                continue;

            entry = new ClutterSprites.Entry
            {
                Sheet = item.Sheet,
                Frame = item.Frame,
                FitHeight = item.FitHeight,
                ContentPixels = item.Content,
                CastsShadow = item.Sheet == Upright
            };
            return true;
        }

        entry = default;
        return false;
    }

    public static bool TryItem(string name, out Item found)
    {
        foreach (Item item in Items)
        {
            if (item.Name == name)
            {
                found = item;
                return true;
            }
        }

        found = default;
        return false;
    }

    // Names may repeat, to weight the pick.
    private static Item[] Pick(params string[] names)
    {
        Item[] picked = new Item[names.Length];

        for (int i = 0; i < names.Length; i++)
        {
            if (!TryItem(names[i], out picked[i]))
                Debug.LogError("ZoneClutter: no item " + names[i]);
        }

        return picked;
    }
}
