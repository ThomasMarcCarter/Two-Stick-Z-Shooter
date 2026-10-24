using UnityEngine;
using UnityEngine.UI;

// Small wireframe pictures of the story items for the inventory's story slot and the HUD,
// matching their shelf sprites: a truck battery with its terminals, a fuel pump with its pipes,
// a finned alternator and its pulley, a stack of two tyres, and an engine block with its valve
// cover; the trade goods: a crate of tins, a wrapped pallet of water bottles, a tent and a
// lantern, a toolbox with a wrench, and a safe with its dial; the medical supplies: a cool box
// with a cross, a tray of vials, a drip stand and bag, a centrifuge, and a folder of notes; the
// radio parts: a power cell, a circuit board, a folded mast, a crystal and a logbook. Drawn in a
// 100x50 box (y down) and fitted into the slot, like the other items'.
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

    private static readonly LineArt TinnedFood = new LineArt(0, 0, 100, 50)
        .Box(14, 20, 86, 46)                                    // crate
        .Wire(14, 29, 86, 29)                                   // slats
        .Wire(14, 38, 86, 38)
        .Circle(30, 13, 8)                                      // tins on top
        .Circle(50, 13, 8)
        .Circle(70, 13, 8)
        .Wire(24, 13, 36, 13)                                   // ring pulls
        .Wire(44, 13, 56, 13)
        .Wire(64, 13, 76, 13);

    private static readonly LineArt BottledWater = new LineArt(0, 0, 100, 50)
        .Box(10, 41, 90, 47)                                    // pallet
        .Wire(24, 41, 24, 47)
        .Wire(50, 41, 50, 47)
        .Wire(76, 41, 76, 47)
        .Shape(16, 41, 16, 18, 21, 12, 21, 6, 27, 6, 27, 12, 32, 18, 32, 41)    // bottles
        .Shape(36, 41, 36, 18, 41, 12, 41, 6, 47, 6, 47, 12, 52, 18, 52, 41)
        .Shape(56, 41, 56, 18, 61, 12, 61, 6, 67, 6, 67, 12, 72, 18, 72, 41)
        .Wire(12, 28, 88, 28)                                   // shrink-wrap
        .Wire(76, 18, 86, 18, 86, 41);

    private static readonly LineArt CampingGear = new LineArt(0, 0, 100, 50)
        .Shape(8, 46, 34, 8, 60, 46)                            // tent
        .Hole(28, 46, 34, 30, 40, 46)                           // its door
        .Wire(34, 8, 34, 2)                                     // pole
        .Box(70, 20, 86, 40)                                    // lantern glass
        .Shape(67, 20, 78, 12, 89, 20)                          // its cap
        .Circle(78, 8, 4, false)                                // handle
        .Box(67, 40, 89, 46)                                    // base
        .Wire(78, 24, 78, 36);                                  // mantle

    private static readonly LineArt HardwareTools = new LineArt(0, 0, 100, 50)
        .Box(12, 22, 88, 46)                                    // toolbox
        .Wire(12, 31, 88, 31)                                   // lid line
        .Box(44, 28, 56, 34)                                    // catch
        .Wire(36, 22, 36, 13, 64, 13, 64, 22)                   // handle
        .Wire(70, 22, 80, 7)                                    // wrench sticking out
        .Circle(82, 5, 4, false)
        .Wire(20, 22, 26, 10, 30, 10);                          // saw blade edge

    private static readonly LineArt StoreSafe = new LineArt(0, 0, 100, 50)
        .Box(24, 3, 76, 45)                                     // body
        .Hole(30, 9, 70, 39)                                    // door
        .Circle(48, 24, 8, false)                               // dial
        .Circle(48, 24, 2)
        .Wire(62, 19, 62, 29)                                   // handle
        .Wire(30, 13, 27, 13)                                   // hinges
        .Wire(30, 35, 27, 35)
        .Box(26, 45, 32, 48)                                    // feet
        .Box(68, 45, 74, 48);

    private static readonly LineArt BloodSamples = new LineArt(0, 0, 100, 50)
        .Box(14, 18, 86, 46)                                    // cool box
        .Wire(14, 26, 86, 26)                                   // lid line
        .Wire(38, 18, 38, 11, 62, 11, 62, 18)                   // handle
        .Hole(46, 30, 54, 44)                                   // cross on the side
        .Hole(41, 34, 59, 40);

    private static readonly LineArt AntiviralVials = new LineArt(0, 0, 100, 50)
        .Box(8, 38, 92, 46)                                     // tray
        .Box(16, 14, 24, 38)                                    // vials
        .Box(32, 14, 40, 38)
        .Box(48, 14, 56, 38)
        .Box(64, 14, 72, 38)
        .Box(80, 14, 88, 38)
        .Box(16, 9, 24, 14)                                     // caps
        .Box(32, 9, 40, 14)
        .Box(48, 9, 56, 14)
        .Box(64, 9, 72, 14)
        .Box(80, 9, 88, 14);

    private static readonly LineArt IVDripKit = new LineArt(0, 0, 100, 50)
        .Wire(30, 47, 30, 3, 46, 3)                             // stand and hook
        .Wire(16, 47, 44, 47)                                   // its feet
        .Shape(38, 6, 56, 6, 58, 24, 36, 24)                    // bag
        .Box(44, 26, 50, 32)                                    // drip chamber
        .Wire(47, 32, 47, 38, 64, 42, 86, 42);                  // line

    private static readonly LineArt Centrifuge = new LineArt(0, 0, 100, 50)
        .Box(18, 22, 82, 46)                                    // body
        .Shape(24, 22, 32, 9, 68, 9, 76, 22)                    // lid
        .Hole(26, 30, 50, 38)                                   // display
        .Circle(68, 34, 6, false)                               // dial
        .Wire(68, 34, 71, 30);

    private static readonly LineArt ResearchNotes = new LineArt(0, 0, 100, 50)
        .Hole(26, 4, 84, 40)                                    // papers behind
        .Box(16, 10, 74, 46)                                    // folder
        .Wire(24, 20, 60, 20)                                   // lines of notes
        .Wire(24, 27, 66, 27)
        .Wire(24, 34, 54, 34)
        .Box(56, 36, 68, 42);                                   // a stamp

    private static readonly LineArt PowerCell = new LineArt(0, 0, 100, 50)
        .Box(20, 12, 80, 46)                                    // case
        .Box(30, 6, 42, 12)                                     // terminals
        .Box(58, 6, 70, 12)
        .Shape(50, 18, 42, 31, 49, 31, 46, 40, 58, 26, 51, 26, 54, 18);   // lightning bolt

    private static readonly LineArt TransmitterBoard = new LineArt(0, 0, 100, 50)
        .Box(10, 8, 90, 44)                                     // board
        .Box(18, 14, 36, 26)                                    // chips
        .Box(44, 14, 58, 22)
        .Hole(66, 14, 82, 30)
        .Wire(18, 34, 40, 34, 40, 26)                           // traces
        .Wire(46, 22, 46, 38, 82, 38)
        .Circle(26, 38, 2)
        .Circle(58, 30, 2);

    private static readonly LineArt AntennaMast = new LineArt(0, 0, 100, 50)
        .Box(10, 36, 90, 44)                                    // folded sections
        .Box(14, 28, 86, 36)
        .Box(18, 20, 82, 28)
        .Wire(82, 24, 92, 6)                                    // the whip, sprung out
        .Circle(92, 5, 2);

    private static readonly LineArt FrequencyCrystal = new LineArt(0, 0, 100, 50)
        .Box(30, 34, 70, 44)                                    // holder
        .Shape(40, 34, 40, 14, 50, 4, 60, 14, 60, 34)           // the crystal
        .Wire(50, 4, 50, 34)
        .Wire(36, 44, 36, 48)                                   // pins
        .Wire(64, 44, 64, 48);

    private static readonly LineArt FlightLog = new LineArt(0, 0, 100, 50)
        .Box(18, 6, 82, 46)                                     // logbook
        .Wire(26, 6, 26, 46)                                    // spine
        .Wire(34, 16, 74, 16)                                   // entries
        .Wire(34, 24, 74, 24)
        .Wire(34, 32, 62, 32)
        .Circle(70, 36, 6, false);                              // the circled frequency

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
            case StoryItem.EngineBlock: return EngineBlock;
            case StoryItem.TinnedFood: return TinnedFood;
            case StoryItem.BottledWater: return BottledWater;
            case StoryItem.CampingGear: return CampingGear;
            case StoryItem.HardwareTools: return HardwareTools;
            case StoryItem.StoreSafe: return StoreSafe;
            case StoryItem.BloodSamples: return BloodSamples;
            case StoryItem.AntiviralVials: return AntiviralVials;
            case StoryItem.IVDripKit: return IVDripKit;
            case StoryItem.Centrifuge: return Centrifuge;
            case StoryItem.ResearchNotes: return ResearchNotes;
            case StoryItem.PowerCell: return PowerCell;
            case StoryItem.TransmitterBoard: return TransmitterBoard;
            case StoryItem.AntennaMast: return AntennaMast;
            case StoryItem.FrequencyCrystal: return FrequencyCrystal;
            default: return FlightLog;
        }
    }
}
