using System;
using System.IO;
using UnityEngine;

// Permanent upgrades bought in the hideout with scrap earned on runs.
public enum MetaUpgrade
{
    Toughness,      // +1 max vitals a level
    Conditioning,   // +15% stamina a level
    AmmoCache,      // start with extra 9mm
    FieldKit,       // start with a medkit, then a grenade too
    Armory,         // start with a main weapon (better each level)
    Scavenger,      // enemies drop ammo more often
    Generator,      // powers higher upgrade levels, and more scrap from runs
    Gunsmith,       // start with attachments in the backpack
    Recon,          // the boss room, then the whole floor, on the map from the start
    HeartyStew,     // the cook's: +1 max vitals a level
    StrongCoffee,   // the cook's: stamina comes back faster
    BoneBroth       // the cook's: medkits heal more
}

// What each hideout upgrade is called, does and costs.
public static class MetaUpgrades
{
    public static readonly MetaUpgrade[] All = (MetaUpgrade[])Enum.GetValues(typeof(MetaUpgrade));

    public static string Name(MetaUpgrade upgrade)
    {
        switch (upgrade)
        {
            case MetaUpgrade.Toughness: return "TOUGHNESS";
            case MetaUpgrade.Conditioning: return "CONDITIONING";
            case MetaUpgrade.AmmoCache: return "AMMO CACHE";
            case MetaUpgrade.FieldKit: return "FIELD KIT";
            case MetaUpgrade.Armory: return "ARMORY";
            case MetaUpgrade.Generator: return "POWER";
            case MetaUpgrade.Gunsmith: return "GUNSMITH";
            case MetaUpgrade.Recon: return "RECON";
            case MetaUpgrade.HeartyStew: return "HEARTY STEW";
            case MetaUpgrade.StrongCoffee: return "STRONG COFFEE";
            case MetaUpgrade.BoneBroth: return "BONE BROTH";
            default: return "SCAVENGER";
        }
    }

    public static int MaxLevel(MetaUpgrade upgrade)
    {
        return upgrade == MetaUpgrade.FieldKit || upgrade == MetaUpgrade.Generator || upgrade == MetaUpgrade.Gunsmith
            || upgrade == MetaUpgrade.Recon || upgrade == MetaUpgrade.HeartyStew || upgrade == MetaUpgrade.StrongCoffee
            || upgrade == MetaUpgrade.BoneBroth ? 2 : 3;
    }

    public static HideoutStation Station(MetaUpgrade upgrade)
    {
        foreach (HideoutStation station in Stations.All)
        {
            if (Array.IndexOf(Stations.Upgrades(station), upgrade) >= 0)
                return station;
        }

        return HideoutStation.Generator;
    }

    // The generator level a level of an upgrade needs: level 2 needs the generator at 1, level 3
    // at 2. The generator itself needs nothing.
    public static int GeneratorNeeded(MetaUpgrade upgrade, int level)
    {
        return upgrade == MetaUpgrade.Generator ? 0 : Mathf.Max(0, level - 1);
    }

    // The materials reaching `level` (1-based) takes, besides the scrap.
    public static MaterialCost[] MaterialCosts(MetaUpgrade upgrade, int level)
    {
        const MaterialType P = MaterialType.Parts, W = MaterialType.Wiring, C = MaterialType.Chemicals,
            G = MaterialType.Gunpowder, F = MaterialType.Fuel;
        MaterialCost[][] costs;

        switch (upgrade)
        {
            case MetaUpgrade.Toughness: costs = new[] { Of((C, 2)), Of((C, 4), (P, 2)), Of((C, 6), (P, 3), (W, 2)) }; break;
            case MetaUpgrade.Conditioning: costs = new[] { Of((C, 2)), Of((C, 3), (F, 2)), Of((C, 5), (F, 3)) }; break;
            case MetaUpgrade.FieldKit: costs = new[] { Of((C, 3)), Of((C, 3), (G, 3)) }; break;
            case MetaUpgrade.Armory: costs = new[] { Of((P, 3)), Of((P, 5), (W, 2)), Of((P, 8), (W, 4), (G, 3)) }; break;
            case MetaUpgrade.Gunsmith: costs = new[] { Of((P, 3), (W, 2)), Of((P, 5), (W, 4)) }; break;
            case MetaUpgrade.Recon: costs = new[] { Of((W, 2)), Of((W, 3), (P, 2)) }; break;
            case MetaUpgrade.AmmoCache: costs = new[] { Of((G, 2)), Of((G, 4), (P, 2)), Of((G, 6), (P, 3)) }; break;
            case MetaUpgrade.Generator: costs = new[] { Of((F, 3), (W, 2)), Of((F, 6), (W, 4), (P, 3)) }; break;
            case MetaUpgrade.HeartyStew: costs = new[] { Of((C, 2), (F, 2)), Of((C, 4), (F, 3)) }; break;
            case MetaUpgrade.StrongCoffee: costs = new[] { Of((C, 2), (W, 1)), Of((C, 3), (W, 3)) }; break;
            case MetaUpgrade.BoneBroth: costs = new[] { Of((C, 3)), Of((C, 5), (P, 2)) }; break;
            default: costs = new[] { Of((G, 2), (W, 1)), Of((G, 3), (W, 3)), Of((G, 5), (W, 4)) }; break;
        }

        return costs[Mathf.Clamp(level - 1, 0, costs.Length - 1)];
    }

    private static MaterialCost[] Of(params (MaterialType type, int amount)[] items)
    {
        MaterialCost[] list = new MaterialCost[items.Length];

        for (int i = 0; i < items.Length; i++)
            list[i] = new MaterialCost(items[i].type, items[i].amount);

        return list;
    }

    // Price of reaching `level` (1-based).
    public static int Cost(MetaUpgrade upgrade, int level)
    {
        int[] costs;

        switch (upgrade)
        {
            case MetaUpgrade.Toughness: costs = new[] { 25, 50, 90 }; break;
            case MetaUpgrade.Conditioning: costs = new[] { 20, 40, 70 }; break;
            case MetaUpgrade.AmmoCache: costs = new[] { 15, 30, 50 }; break;
            case MetaUpgrade.FieldKit: costs = new[] { 30, 60 }; break;
            case MetaUpgrade.Armory: costs = new[] { 40, 80, 140 }; break;
            case MetaUpgrade.Generator: costs = new[] { 30, 70 }; break;
            case MetaUpgrade.Gunsmith: costs = new[] { 35, 75 }; break;
            case MetaUpgrade.Recon: costs = new[] { 25, 55 }; break;
            case MetaUpgrade.HeartyStew: costs = new[] { 30, 65 }; break;
            case MetaUpgrade.StrongCoffee: costs = new[] { 25, 55 }; break;
            case MetaUpgrade.BoneBroth: costs = new[] { 30, 60 }; break;
            default: costs = new[] { 25, 50, 80 }; break;
        }

        return costs[Mathf.Clamp(level - 1, 0, costs.Length - 1)];
    }

    // What it gives at a level (0 = not bought).
    public static string Describe(MetaUpgrade upgrade, int level)
    {
        if (level <= 0)
            return "NONE";

        switch (upgrade)
        {
            case MetaUpgrade.Toughness: return $"+{level} MAX VITALS";
            case MetaUpgrade.Conditioning: return $"+{level * 15}% STAMINA";
            case MetaUpgrade.AmmoCache: return $"+{AmmoCacheRounds(level)} 9MM AT START";
            case MetaUpgrade.FieldKit: return level >= 2 ? "START WITH MEDKIT + GRENADE" : "START WITH A MEDKIT";
            case MetaUpgrade.Armory: return "START WITH " + ArmoryWeaponName(level);
            case MetaUpgrade.Generator: return $"POWERS LV{level + 1} UPGRADES // +{level * 10}% SCRAP";
            case MetaUpgrade.Gunsmith: return level >= 2 ? "START WITH 2 ATTACHMENTS" : "START WITH AN ATTACHMENT";
            case MetaUpgrade.Recon: return level >= 2 ? "EVERY FLOOR FULLY MAPPED" : "BOSS ROOM MAPPED ON EVERY FLOOR";
            case MetaUpgrade.HeartyStew: return $"+{level} MAX VITALS EACH RUN";
            case MetaUpgrade.StrongCoffee: return $"+{level * 20}% STAMINA RECOVERY";
            case MetaUpgrade.BoneBroth: return $"MEDKITS HEAL +{level}";
            default: return $"+{level * 10}% AMMO DROPS";
        }
    }

    public static int AmmoCacheRounds(int level) => level * 12;

    public static string ArmoryWeaponName(int level)
    {
        switch (level)
        {
            case 1: return "DOUBLE BARREL";
            case 2: return "MP5";
            default: return "AK47";
        }
    }
}
