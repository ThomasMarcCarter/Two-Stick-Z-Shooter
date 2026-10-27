using System;
using UnityEngine;

// The story's items: what each hideout survivor needs brought back from one zone of the map. The
// mechanic needs five parts from the Military Base to get the military truck in the hideout
// running again (then the survivors can drive to other zones); the trader needs five lots of
// goods from the Shopping Mall to restock the trade post; the medic needs five medical supplies
// from the Hospital to cure the infected cook; the quartermaster needs five radio parts from the
// Airport to call in the evac chopper (the way out). Each completed run recovers the next
// missing item of its zone's goal from the supply room under the last floor, in a fixed order;
// it's carried in the story slot (StorySlot) and handed in when the run is won.
// Appended only: the save stores what's been recovered by this order.
public enum StoryItem
{
    Battery,
    FuelPump,
    Alternator,
    Tyres,
    EngineBlock,
    TinnedFood,
    BottledWater,
    CampingGear,
    HardwareTools,
    StoreSafe,
    BloodSamples,
    AntiviralVials,
    IVDripKit,
    Centrifuge,
    ResearchNotes,
    PowerCell,
    TransmitterBoard,
    AntennaMast,
    FrequencyCrystal,
    FlightLog
}

// What each story item is called and whose goal it belongs to.
public static class StoryItems
{
    public static readonly StoryItem[] All = (StoryItem[])Enum.GetValues(typeof(StoryItem));

    // The mechanic's goal: the truck's parts, in the order runs recover them.
    private static readonly StoryItem[] TruckParts =
    {
        StoryItem.Battery, StoryItem.FuelPump, StoryItem.Alternator, StoryItem.Tyres, StoryItem.EngineBlock
    };

    // The trader's goal: goods from the Shopping Mall, each one more stock for the trade post.
    private static readonly StoryItem[] TradeGoods =
    {
        StoryItem.TinnedFood, StoryItem.BottledWater, StoryItem.CampingGear, StoryItem.HardwareTools, StoryItem.StoreSafe
    };

    // The quartermaster's goal: a radio strong enough to reach the evac chopper, from the Airport.
    private static readonly StoryItem[] RadioParts =
    {
        StoryItem.PowerCell, StoryItem.TransmitterBoard, StoryItem.AntennaMast, StoryItem.FrequencyCrystal, StoryItem.FlightLog
    };

    // The medic's goal: what the Hospital has for a cure, step by step.
    private static readonly StoryItem[] MedicalSupplies =
    {
        StoryItem.BloodSamples, StoryItem.AntiviralVials, StoryItem.IVDripKit, StoryItem.Centrifuge, StoryItem.ResearchNotes
    };

    public static string Name(StoryItem item)
    {
        switch (item)
        {
            case StoryItem.Battery: return "TRUCK BATTERY";
            case StoryItem.FuelPump: return "FUEL PUMP";
            case StoryItem.Alternator: return "ALTERNATOR";
            case StoryItem.Tyres: return "TRUCK TYRES";
            case StoryItem.EngineBlock: return "ENGINE BLOCK";
            case StoryItem.TinnedFood: return "TINNED FOOD CRATE";
            case StoryItem.BottledWater: return "BOTTLED WATER";
            case StoryItem.CampingGear: return "CAMPING GEAR";
            case StoryItem.HardwareTools: return "HARDWARE TOOLS";
            case StoryItem.StoreSafe: return "STORE SAFE";
            case StoryItem.BloodSamples: return "BLOOD SAMPLES";
            case StoryItem.AntiviralVials: return "ANTIVIRAL VIALS";
            case StoryItem.IVDripKit: return "IV DRIP KIT";
            case StoryItem.Centrifuge: return "CENTRIFUGE";
            case StoryItem.ResearchNotes: return "RESEARCH NOTES";
            case StoryItem.PowerCell: return "POWER CELL";
            case StoryItem.TransmitterBoard: return "TRANSMITTER BOARD";
            case StoryItem.AntennaMast: return "ANTENNA MAST";
            case StoryItem.FrequencyCrystal: return "FREQUENCY CRYSTAL";
            default: return "FLIGHT LOG";
        }
    }

    public static string Description(StoryItem item)
    {
        switch (item)
        {
            case StoryItem.Battery: return "HEAVY-DUTY 24V BATTERY. STILL HOLDS A CHARGE.";
            case StoryItem.FuelPump: return "MECHANICAL FUEL PUMP OFF A SUPPLY TRUCK.";
            case StoryItem.Alternator: return "MILITARY ALTERNATOR. KEEPS THE BATTERY ALIVE.";
            case StoryItem.Tyres: return "A PAIR OF RUN-FLAT TRUCK TYRES.";
            case StoryItem.EngineBlock: return "A REBUILT DIESEL ENGINE BLOCK. THE LAST PIECE.";
            case StoryItem.TinnedFood: return "A CRATE OF TINS. BEANS, MOSTLY. STILL IN DATE, NEARLY.";
            case StoryItem.BottledWater: return "A SHRINK-WRAPPED PALLET OF BOTTLED WATER.";
            case StoryItem.CampingGear: return "A STOVE, LANTERNS AND A TENT FROM THE OUTDOOR STORE.";
            case StoryItem.HardwareTools: return "A TOOLBOX FULL OF HARDWARE-STORE TOOLS.";
            case StoryItem.StoreSafe: return "THE MALL MANAGER'S SAFE. HEAVY. SOMETHING RATTLES INSIDE.";
            case StoryItem.BloodSamples: return "A COOL BOX OF LABELLED BLOOD SAMPLES. SOME FROM THE INFECTED.";
            case StoryItem.AntiviralVials: return "A TRAY OF EXPERIMENTAL ANTIVIRAL VIALS.";
            case StoryItem.IVDripKit: return "A DRIP STAND, BAGS AND LINES. FOR GETTING IT INTO THEM.";
            case StoryItem.Centrifuge: return "A LAB CENTRIFUGE. FOR SPLITTING WHAT'S IN THE BLOOD.";
            case StoryItem.ResearchNotes: return "THE LEAD RESEARCHER'S NOTES. THE LAST PIECE OF THE CURE.";
            case StoryItem.PowerCell: return "A TOWER-GRADE POWER CELL. ENOUGH JUICE FOR A LONG-RANGE SET.";
            case StoryItem.TransmitterBoard: return "THE CONTROL TOWER'S TRANSMITTER BOARD. SCORCHED, BUT WHOLE.";
            case StoryItem.AntennaMast: return "A FOLDING ANTENNA MAST OFF A GROUND-CREW TRUCK.";
            case StoryItem.FrequencyCrystal: return "A QUARTZ CRYSTAL CUT FOR THE MILITARY BAND.";
            default: return "THE LAST FLIGHT LOG. THE EVAC FREQUENCY IS WRITTEN IN THE MARGIN.";
        }
    }

    // The survivor who wants it.
    public static NpcKind Owner(StoryItem item)
    {
        if (Array.IndexOf(TradeGoods, item) >= 0)
            return NpcKind.Trader;

        if (Array.IndexOf(RadioParts, item) >= 0)
            return NpcKind.Quartermaster;

        return Array.IndexOf(MedicalSupplies, item) >= 0 ? NpcKind.Medic : NpcKind.Mechanic;
    }

    public static Color Colour(StoryItem item)
    {
        switch (Owner(item))
        {
            case NpcKind.Trader: return new Color(1f, 0.85f, 0.35f);
            case NpcKind.Medic: return new Color(1f, 0.5f, 0.5f);
            case NpcKind.Quartermaster: return new Color(0.55f, 0.9f, 1f);
            default: return new Color(1f, 0.72f, 0.25f);
        }
    }

    public static string Hex(StoryItem item) => "#" + ColorUtility.ToHtmlStringRGB(Colour(item));

    // A survivor's goal: the items they need, in order (empty if they have none yet).
    public static StoryItem[] Goal(NpcKind owner)
    {
        switch (owner)
        {
            case NpcKind.Mechanic: return TruckParts;
            case NpcKind.Trader: return TradeGoods;
            case NpcKind.Medic: return MedicalSupplies;
            case NpcKind.Quartermaster: return RadioParts;
            default: return new StoryItem[0];
        }
    }

    // What the goal is called on screens ("TRUCK PARTS 2/5").
    public static string GoalName(NpcKind owner)
    {
        switch (owner)
        {
            case NpcKind.Mechanic: return "TRUCK PARTS";
            case NpcKind.Trader: return "TRADE GOODS";
            case NpcKind.Medic: return "MEDICAL SUPPLIES";
            case NpcKind.Quartermaster: return "RADIO PARTS";
            default: return "";
        }
    }

    // Its frame on the story sheet (Story.png): a row per goal (the truck parts on row 0, the
    // trade goods on row 3, the medical supplies on row 4, the radio parts on row 5), in goal
    // order along it.
    public static Vector2Int SheetFrame(StoryItem item)
    {
        NpcKind owner = Owner(item);
        int row = owner == NpcKind.Trader ? 3 : owner == NpcKind.Medic ? 4 : owner == NpcKind.Quartermaster ? 5 : 0;
        return new Vector2Int(Array.IndexOf(Goal(owner), item), row);
    }

    // The next item of a goal still to be recovered, or null once it's complete.
    public static StoryItem? Next(NpcKind owner)
    {
        foreach (StoryItem item in Goal(owner))
        {
            if (!MetaProgress.HasStoryItem(item))
                return item;
        }

        return null;
    }

    public static int Recovered(NpcKind owner)
    {
        int count = 0;

        foreach (StoryItem item in Goal(owner))
        {
            if (MetaProgress.HasStoryItem(item))
                count++;
        }

        return count;
    }

    public static bool Complete(NpcKind owner) => Goal(owner).Length > 0 && Next(owner) == null;

    // "TRUCK PARTS 2/5".
    public static string Progress(NpcKind owner) => $"{GoalName(owner)} {Recovered(owner)}/{Goal(owner).Length}";

    // The truck's parts recovered so far, which also bring the survivors' signals in range.
    public static int TruckPartsRecovered => Recovered(NpcKind.Mechanic);

    // The trade goods brought home so far: each one widens the trader's stock (TraderStock).
    public static int TradeGoodsRecovered => Recovered(NpcKind.Trader);

    // The medical supplies brought home so far: the cook's cure (Npcs, CookSickness).
    public static int MedicalSuppliesRecovered => Recovered(NpcKind.Medic);
}
