using UnityEngine;
using UnityEngine.UI;

// Small wireframe pictures of the story items for the inventory's story slot and the HUD,
// matching their shelf sprites: a truck battery with its terminals, a fuel pump with its pipes,
// a finned alternator and its pulley, a stack of two tyres, and an engine block with its valve
// cover. Drawn in a 100x50 box (y down) and fitted into the slot, like the other items'.
public static class StoryItemSilhouette
{
    private static readonly LineArt Battery = new LineArt(0, 0, 100, 50)
        .Box(22, 14, 78, 46)                                    // case
        .Wire(22, 22, 78, 22)                                   // lid line
        .Box(30, 7, 38, 14)                                     // + terminal
        .Box(62, 7, 70, 14)                                     // - terminal
        .Wire(31, 32, 37, 32)                                   // +
        .Wire(34, 29, 34, 35)
        .Wire(63, 32, 69, 32)                                   // -
        .Box(44, 28, 56, 38);                                   // label

    private static readonly LineArt FuelPump = new LineArt(0, 0, 100, 50)
        .Box(32, 40, 68, 46)                                    // flange
        .Shape(36, 40, 36, 14, 42, 8, 58, 8, 64, 14, 64, 40)    // body
        .Wire(36, 24, 64, 24)
        .Box(64, 26, 86, 32)                                    // outlet pipe
        .Box(14, 16, 36, 22)                                    // inlet pipe
        .Box(45, 3, 55, 8);                                     // cap

    private static readonly LineArt Alternator = new LineArt(0, 0, 100, 50)
        .Circle(44, 25, 20, false)                              // drum
        .Wire(32, 12, 32, 38)                                   // cooling fins
        .Wire(39, 7, 39, 43)
        .Wire(46, 6, 46, 44)
        .Wire(53, 8, 53, 42)
        .Box(64, 21, 72, 29)                                    // shaft
        .Circle(80, 25, 9, false)                               // pulley
        .Circle(80, 25, 3, false);

    private static readonly LineArt Tyres = new LineArt(0, 0, 100, 50)
        .Box(14, 28, 86, 46)                                    // bottom tyre
        .Box(14, 12, 86, 28)                                    // top tyre
        .Wire(14, 37, 86, 37)                                   // treads
        .Wire(14, 20, 86, 20)
        .Shape(30, 12, 38, 5, 62, 5, 70, 12)                    // the top tyre's face
        .Wire(42, 9, 58, 9);                                    // hub

    private static readonly LineArt EngineBlock = new LineArt(0, 0, 100, 50)
        .Box(12, 18, 80, 46)                                    // block
        .Box(18, 8, 74, 18)                                     // valve cover
        .Wire(24, 13, 30, 13)                                   // bolts
        .Wire(43, 13, 49, 13)
        .Wire(62, 13, 68, 13)
        .Wire(26, 22, 26, 42)                                   // ribs
        .Wire(42, 22, 42, 42)
        .Wire(58, 22, 58, 42)
        .Circle(86, 32, 8, false)                               // pulley
        .Circle(86, 32, 3, false);

    public static void Draw(VertexHelper vh, StoryItem item, Rect area, Color colour)
    {
        Drawing(item).Draw(vh, area, colour);
    }

    private static LineArt Drawing(StoryItem item)
    {
        switch (item)
        {
            case StoryItem.Battery: return Battery;
            case StoryItem.FuelPump: return FuelPump;
            case StoryItem.Alternator: return Alternator;
            case StoryItem.Tyres: return Tyres;
            default: return EngineBlock;
        }
    }
}
