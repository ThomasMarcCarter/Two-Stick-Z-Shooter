using System;

// The areas of the map a run can take place in. Each is tied to one hideout survivor who needs
// something from it (StoryItems), and they open in order: a zone unlocks once the survivor tied
// to the one before it has everything they asked for (the Mechanic's truck parts open the
// Shopping Mall, and so on). Only the Military Base has its own content so far; the others reuse
// it until they're built (see Story.md).
// Appended only: the save stores the chosen zone and the unlocked flags by this order.
public enum Zone
{
    MilitaryBase,
    ShoppingMall,
    Hospital,
    Airport
}

// What each zone is called, whose story it belongs to, what unlocks it and which bosses guard its
// floors. The FloorGenerator reads the run's zone (FloorGenerator.Zone) for its bosses and room
// themes, and its Inspector's per-zone settings (FloorGenerator.Zones.cs) for floor count, enemy
// mix, loot and music; a zone with none set plays like the Military Base. Art is still shared.
public static class Zones
{
    public static readonly Zone[] All = (Zone[])Enum.GetValues(typeof(Zone));

    public static string Name(Zone zone)
    {
        switch (zone)
        {
            case Zone.ShoppingMall: return "SHOPPING MALL";
            case Zone.Hospital: return "HOSPITAL";
            case Zone.Airport: return "AIRPORT";
            default: return "MILITARY BASE";
        }
    }

    // The survivors' hideout in each zone, where the truck parks: a hangar at the base, a
    // boarded-up store at the mall, the barred-up emergency room at the hospital and the baggage
    // sorting room at the airport (FloorGenerator.HideoutZones.cs).
    public static string HideoutName(Zone zone)
    {
        switch (zone)
        {
            case Zone.ShoppingMall: return "BOARDED-UP STORE";
            case Zone.Hospital: return "EMERGENCY ROOM";
            case Zone.Airport: return "BAGGAGE ROOM";
            default: return "HANGAR";
        }
    }

    // The survivor whose goal is found in this zone.
    public static NpcKind Owner(Zone zone)
    {
        switch (zone)
        {
            case Zone.ShoppingMall: return NpcKind.Trader;
            case Zone.Hospital: return NpcKind.Medic;
            case Zone.Airport: return NpcKind.Quartermaster;
            default: return NpcKind.Mechanic;
        }
    }

    // The zone holding this survivor's goal (null for the cook, who has none).
    public static Zone? Of(NpcKind npc)
    {
        foreach (Zone zone in All)
            if (Owner(zone) == npc)
                return zone;

        return null;
    }

    // The zone before this one, whose survivor's goal opens it (null for the first zone).
    public static Zone? Previous(Zone zone)
    {
        return zone == Zone.MilitaryBase ? (Zone?)null : zone - 1;
    }

    // Open to runs: the first zone always, the others once the previous zone's goal is done (or
    // unlocked from the debug menu).
    public static bool IsUnlocked(Zone zone)
    {
        Zone? previous = Previous(zone);
        return previous == null || MetaProgress.IsZoneFlagged(zone) || StoryItems.Complete(Owner(previous.Value));
    }

    // Why a locked zone is locked, for the zone select screen and the debug menu.
    public static string UnlockHint(Zone zone)
    {
        Zone? previous = Previous(zone);

        if (previous == null)
            return "";

        NpcKind owner = Owner(previous.Value);
        string goal = StoryItems.GoalName(owner);
        return goal.Length > 0
            ? $"{Npcs.Name(owner)} NEEDS ALL {StoryItems.Goal(owner).Length} {goal}"
            : $"{Npcs.Name(owner)} NEEDS HELP AT THE {Name(previous.Value)}";
    }

    // The boss guarding each floor of a zone. Floors past the list cycle round it.
    public static BossKind Boss(Zone zone, int floor)
    {
        BossKind[] bosses;

        switch (zone)
        {
            case Zone.ShoppingMall: bosses = new[] { BossKind.Warden, BossKind.Mannequin, BossKind.Glutton }; break;
            case Zone.Hospital: bosses = new[] { BossKind.Surgeon, BossKind.Gurney, BossKind.Hazmat }; break;
            case Zone.Airport: bosses = new[] { BossKind.Handler, BossKind.Captain, BossKind.Marshal }; break;
            default: bosses = new[] { BossKind.Butcher, BossKind.Broodmother, BossKind.Howler }; break;
        }

        int index = Math.Max(1, floor) - 1;
        return bosses[index % bosses.Length];
    }

    // "MILITARY BASE // FLOOR 2".
    public static string Location(Zone zone, int floor) => $"{Name(zone)} // FLOOR {floor}";
}
