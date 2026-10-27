using System;
using System.Collections.Generic;
using UnityEngine;

// Survivors found caged on runs. Freeing one sends them to the hideout for good, where they set
// up their station. The cook isn't found: they turn up at the hideout infected once the trader's
// goal is done, and only set up their kitchen once the medic has cured them.
// Appended only: the save stores rescues by this order.
public enum NpcKind
{
    Mechanic,
    Medic,
    Quartermaster,
    Trader,
    Cook
}

// Who each survivor is, where they're found, and what they say.
public static partial class Npcs
{
    public static readonly NpcKind[] All = (NpcKind[])Enum.GetValues(typeof(NpcKind));

    public static string Name(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic: return "RIGGS";
            case NpcKind.Medic: return "DR. OKAFOR";
            case NpcKind.Trader: return "MAGPIE";
            case NpcKind.Cook: return "ROSA";
            default: return "SGT. VOSS";
        }
    }

    public static string Role(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic: return "MECHANIC";
            case NpcKind.Medic: return "MEDIC";
            case NpcKind.Trader: return "TRADER";
            case NpcKind.Cook: return "COOK";
            default: return "QUARTERMASTER";
        }
    }

    // The mechanic isn't out on the floors: they're held in the supply room under the last floor,
    // found on the first run that gets that far, with the truck's first part.
    public static bool FoundInSupplyRoom(NpcKind kind) => kind == NpcKind.Mechanic;

    // The cook isn't found on a run: they stumble into the hideout, bitten, the visit the trader's
    // shelves are full (FloorGenerator.Hideout.cs).
    public static bool ArrivesInfected(NpcKind kind) => kind == NpcKind.Cook;

    // Ready to turn up: the trader's goal done, and not here yet.
    public static bool CookDue => !MetaProgress.IsRescued(NpcKind.Cook) && StoryItems.Complete(NpcKind.Trader);

    // Here, and still infected: until the medic's goal (the cure) is done.
    public static bool CookSick => MetaProgress.IsRescued(NpcKind.Cook) && !StoryItems.Complete(NpcKind.Medic);

    // How sick the cook looks and sounds, 1 to 3: worse a step every two runs since they came,
    // a step better for every two medical supplies brought home. Story only: they never die.
    public static int CookSickness
    {
        get
        {
            if (!CookSick)
                return 0;

            int runs = Mathf.Max(0, MetaProgress.Runs - MetaProgress.CookArrivedRun);
            return Mathf.Clamp(1 + runs / 2 - StoryItems.MedicalSuppliesRecovered / 2, 1, 3);
        }
    }

    // Truck parts recovered (one per completed run) before their distress signal can be picked
    // up on a run: the medic after the second, the quartermaster after the third, the trader after
    // the fourth (the mechanic comes with the first; see FoundInSupplyRoom).
    public static int RequiredParts(NpcKind kind) => FoundInSupplyRoom(kind) ? 0 : (int)kind + 1;

    // Their signal is out there: enough truck parts recovered, and not rescued yet.
    public static bool Findable(NpcKind kind)
    {
        return !MetaProgress.IsRescued(kind) && !FoundInSupplyRoom(kind) && !ArrivesInfected(kind)
            && StoryItems.TruckPartsRecovered >= RequiredParts(kind);
    }

    // How to find them, for their abandoned area's sign (short lines, upper case), or null once
    // their signal is out there.
    public static string FindHint(NpcKind kind)
    {
        if (FoundInSupplyRoom(kind))
            return $"HELD IN THE SUPPLY ROOM UNDER THE LAST FLOOR // FINISH A RUN TO REACH THE {Role(kind)}";

        if (ArrivesInfected(kind))
        {
            return CookSick
                ? $"THE {Role(kind)} IS SICK IN THE MED BAY // {Name(NpcKind.Medic)} NEEDS MEDICAL SUPPLIES FROM THE HOSPITAL"
                : $"NOBODY HERE YET // FILL {Name(NpcKind.Trader)}'S SHELVES WITH GOODS FROM THE MALL";
        }

        int needed = RequiredParts(kind) - StoryItems.TruckPartsRecovered;
        return needed > 0 ? $"NO SIGNAL // RECOVER {needed} MORE TRUCK PART{(needed == 1 ? "" : "S")} TO PICK UP THE {Role(kind)}" : null;
    }

    // Who a run can turn up: the earliest findable survivor, or null if there's none.
    public static NpcKind? NextToFind()
    {
        foreach (NpcKind kind in All)
        {
            if (Findable(kind))
                return kind;
        }

        return null;
    }

    public static HideoutStation Station(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic: return HideoutStation.Workbench;
            case NpcKind.Medic: return HideoutStation.MedStation;
            case NpcKind.Trader: return HideoutStation.TradePost;
            case NpcKind.Cook: return HideoutStation.Kitchen;
            default: return HideoutStation.AmmoPress;
        }
    }

    // The colour their speech is drawn in: the green of the UI, shifted a little per person.
    public static Color Voice(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic: return new Color(0.55f, 1f, 0.45f);
            case NpcKind.Medic: return new Color(0.4f, 1f, 0.75f);
            case NpcKind.Trader: return new Color(0.85f, 1f, 0.55f);
            case NpcKind.Cook: return new Color(0.95f, 1f, 0.6f);
            default: return new Color(0.75f, 1f, 0.4f);
        }
    }

    // One-time story scenes on the hideout radio, as goals finish and the next begin. Appended
    // only: the save stores which have played by this order.
    public enum Scene
    {
        TruckRunning,      // the mechanic's goal done: the truck can drive
        MallRequest,       // the trader asks for goods from the Mall
        AirportRequest     // the quartermaster asks for radio parts from the Airport
    }

    // Shouted from the cage while zombies are still in the room.
    public static readonly string[] CagedPleas =
    {
        "HEY! OVER HERE!",
        "GET ME OUT OF THIS THING!",
        "WATCH OUT, THEY'RE EVERYWHERE!",
        "DON'T LEAVE ME HERE!"
    };
}
