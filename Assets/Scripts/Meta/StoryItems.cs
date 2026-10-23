using System;
using UnityEngine;

// The story's items: what each hideout survivor needs brought back from one zone of the map. The
// mechanic needs five parts from the Military Base to get the military truck in the hideout
// running again (then the survivors can drive to other zones). Each completed run recovers the
// next missing part from the supply room under the last floor, in a fixed order; it's carried in
// the story slot (StorySlot) and fitted when the run is won. Later zones add their own goals
// (the trader's goods, the medic's medical supplies, the quartermaster's radio parts).
// Appended only: the save stores what's been recovered by this order.
public enum StoryItem
{
    Battery,
    FuelPump,
    Alternator,
    Tyres,
    EngineBlock
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

    public static string Name(StoryItem item)
    {
        switch (item)
        {
            case StoryItem.Battery: return "TRUCK BATTERY";
            case StoryItem.FuelPump: return "FUEL PUMP";
            case StoryItem.Alternator: return "ALTERNATOR";
            case StoryItem.Tyres: return "TRUCK TYRES";
            default: return "ENGINE BLOCK";
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
            default: return "A REBUILT DIESEL ENGINE BLOCK. THE LAST PIECE.";
        }
    }

    // The survivor who wants it.
    public static NpcKind Owner(StoryItem item) => NpcKind.Mechanic;

    public static Color Colour(StoryItem item) => new Color(1f, 0.72f, 0.25f);

    public static string Hex(StoryItem item) => "#" + ColorUtility.ToHtmlStringRGB(Colour(item));

    // A survivor's goal: the items they need, in order (empty if they have none yet).
    public static StoryItem[] Goal(NpcKind owner)
    {
        return owner == NpcKind.Mechanic ? TruckParts : new StoryItem[0];
    }

    // What the goal is called on screens ("TRUCK PARTS 2/5").
    public static string GoalName(NpcKind owner) => owner == NpcKind.Mechanic ? "TRUCK PARTS" : "";

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
}
