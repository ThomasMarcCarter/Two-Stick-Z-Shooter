using System;
using UnityEngine;

// The hideout's stations. Each holds some of the permanent upgrades (the trade post sells
// supplies instead, and the truck bay holds the truck being repaired); all but the generator,
// the intel desk and the truck bay need their NPC rescued first.
public enum HideoutStation
{
    Generator,
    Workbench,
    MedStation,
    AmmoPress,
    TradePost,
    IntelDesk,
    TruckBay,
    Kitchen
}

// What each station is called, who runs it, and which upgrades it holds.
public static class Stations
{
    public static readonly HideoutStation[] All = (HideoutStation[])Enum.GetValues(typeof(HideoutStation));

    public static string Name(HideoutStation station)
    {
        switch (station)
        {
            case HideoutStation.Generator: return "GENERATOR";
            case HideoutStation.Workbench: return "WORKBENCH";
            case HideoutStation.MedStation: return "MED STATION";
            case HideoutStation.TradePost: return "TRADE POST";
            case HideoutStation.IntelDesk: return "INTEL DESK";
            case HideoutStation.TruckBay: return "TRUCK BAY";
            case HideoutStation.Kitchen: return "KITCHEN";
            default: return "AMMO PRESS";
        }
    }

    // The NPC who has to be rescued before it can be used; null for the generator, the intel desk
    // and the truck bay, which are there from the start.
    public static NpcKind? Owner(HideoutStation station)
    {
        switch (station)
        {
            case HideoutStation.Workbench: return NpcKind.Mechanic;
            case HideoutStation.MedStation: return NpcKind.Medic;
            case HideoutStation.AmmoPress: return NpcKind.Quartermaster;
            case HideoutStation.TradePost: return NpcKind.Trader;
            case HideoutStation.Kitchen: return NpcKind.Cook;
            default: return null;
        }
    }

    public static MetaUpgrade[] Upgrades(HideoutStation station)
    {
        switch (station)
        {
            case HideoutStation.Generator: return new[] { MetaUpgrade.Generator };
            case HideoutStation.Workbench: return new[] { MetaUpgrade.Armory, MetaUpgrade.Gunsmith };
            case HideoutStation.MedStation: return new[] { MetaUpgrade.Toughness, MetaUpgrade.Conditioning, MetaUpgrade.FieldKit };
            case HideoutStation.TradePost: return new MetaUpgrade[0];
            case HideoutStation.IntelDesk: return new[] { MetaUpgrade.Recon };
            case HideoutStation.TruckBay: return new MetaUpgrade[0];
            case HideoutStation.Kitchen: return new[] { MetaUpgrade.HeartyStew, MetaUpgrade.StrongCoffee, MetaUpgrade.BoneBroth };
            default: return new[] { MetaUpgrade.AmmoCache, MetaUpgrade.Scavenger };
        }
    }

    public static Color Colour(HideoutStation station)
    {
        switch (station)
        {
            case HideoutStation.Generator: return new Color(1f, 0.8f, 0.3f);
            case HideoutStation.Workbench: return new Color(1f, 0.6f, 0.25f);
            case HideoutStation.MedStation: return new Color(0.45f, 0.9f, 1f);
            case HideoutStation.TradePost: return new Color(1f, 0.85f, 0.35f);
            case HideoutStation.IntelDesk: return new Color(0.35f, 1f, 0.55f);
            case HideoutStation.TruckBay: return new Color(1f, 0.72f, 0.25f);
            case HideoutStation.Kitchen: return new Color(1f, 0.55f, 0.3f);
            default: return new Color(0.85f, 1f, 0.35f);
        }
    }

    // Its owner rescued (and for the kitchen, cured too).
    public static bool Unlocked(HideoutStation station)
    {
        NpcKind? owner = Owner(station);

        if (owner == NpcKind.Cook && Npcs.CookSick)
            return false;

        return owner == null || MetaProgress.IsRescued(owner.Value);
    }
}
