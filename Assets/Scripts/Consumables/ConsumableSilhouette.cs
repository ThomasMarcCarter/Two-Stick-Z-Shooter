using UnityEngine;
using UnityEngine.UI;

// Small wireframe pictures of the consumables for the inventory and quick bar, matching their
// pickup models: a first-aid case (medkit), a bigger strapped bag (large medkit), a syringe
// (adrenaline), a pineapple grenade with its pin, a bottle with a burning rag (molotov) and a
// plain bottle. Drawn in a 100x50 box (y down) and fitted into the cell, like the attachments'
// and materials' pictures (AttachmentSilhouette, MaterialSilhouette).
// With the sprite art the item's own sprite is drawn instead (PixelIcon); these are the fallback.
public static class ConsumableSilhouette
{
    private static readonly LineArt Medkit = new LineArt(0, 0, 100, 50)
        .Hole(42, 4, 58, 4, 58, 11, 42, 11)                     // handle
        .Box(28, 11, 72, 45)                                    // case
        .Shape(46, 17, 54, 17, 54, 24, 61, 24, 61, 32, 54, 32, 54, 39, 46, 39, 46, 32, 39, 32, 39, 24, 46, 24);   // cross

    private static readonly LineArt LargeMedkit = new LineArt(0, 0, 100, 50)
        .Hole(40, 3, 60, 3, 60, 10, 40, 10)                     // handle
        .Box(16, 10, 84, 46)                                    // bag
        .Wire(28, 10, 28, 46)                                   // straps
        .Wire(72, 10, 72, 46)
        .Shape(46, 17, 54, 17, 54, 24, 61, 24, 61, 32, 54, 32, 54, 39, 46, 39, 46, 32, 39, 32, 39, 24, 46, 24);   // cross

    private static readonly LineArt Adrenaline = new LineArt(0, 0, 100, 50)
        .Box(4, 17, 9, 33)                                      // plunger
        .Wire(9, 25, 20, 25)
        .Box(20, 18, 64, 32)                                    // barrel
        .Wire(30, 18, 30, 24)                                   // graduations
        .Wire(40, 18, 40, 24)
        .Wire(50, 18, 50, 24)
        .Box(64, 21, 71, 29)                                    // hub
        .Wire(71, 25, 96, 25);                                  // needle

    private static readonly LineArt Grenade = new LineArt(0, 0, 100, 50)
        .Box(40, 6, 52, 13)                                     // fuse head
        .Shape(52, 6, 66, 9, 64, 30, 60, 30, 61, 12, 52, 11)    // spoon
        .Circle(70, 9, 5, false)                                // pin ring
        .Circle(46, 30, 16)                                     // body
        .Wire(34, 25, 58, 25)                                   // segments
        .Wire(32, 33, 60, 33)
        .Wire(46, 14, 46, 46);

    private static readonly LineArt Molotov = new LineArt(0, 0, 100, 50)
        .Shape(46, 12, 54, 12, 54, 20, 60, 26, 60, 48, 40, 48, 40, 26, 46, 20)   // bottle
        .Shape(46, 12, 54, 12, 57, 6, 49, 8, 43, 5)             // rag
        .Wire(41, 34, 59, 34)                                   // fuel line
        .Wire(50, 4, 47, 0)                                     // flame licks
        .Wire(55, 5, 58, 1)
        .Wire(45, 5, 41, 2);

    private static readonly LineArt Bottle = new LineArt(0, 0, 100, 50)
        .Box(46, 3, 54, 7)                                      // cap
        .Shape(46, 7, 54, 7, 54, 18, 60, 25, 60, 48, 40, 48, 40, 25, 46, 18)     // bottle
        .Hole(42, 30, 58, 30, 58, 40, 42, 40);                  // label

    public static void Draw(VertexHelper vh, ConsumableType type, Rect area, Color colour)
    {
        if (!PixelIcon.DrawItem(vh, ItemSprites.Consumable(type), area, colour))
            Drawing(type).Draw(vh, area, colour);
    }

    private static LineArt Drawing(ConsumableType type)
    {
        switch (type)
        {
            case ConsumableType.Medkit: return Medkit;
            case ConsumableType.LargeMedkit: return LargeMedkit;
            case ConsumableType.Adrenaline: return Adrenaline;
            case ConsumableType.Grenade: return Grenade;
            case ConsumableType.Molotov: return Molotov;
            default: return Bottle;
        }
    }
}
