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
    Recon           // the boss room, then the whole floor, on the map from the start
}

// Progress that carries over between runs (the only thing the game saves; there are no
// mid-run saves): scrap, banked materials, the survivors rescued, the station upgrades bought
// with scrap and materials, and a few lifetime stats. Kept in a small JSON file in the player's
// data folder, written whenever something changes. A run pays out its scrap once, when it ends
// in death or victory: a scrap a kill, more for each boss beaten, and a bonus for winning
// (abandoned runs pay nothing). Materials are banked separately, when sent home from an uplink terminal (UplinkTerminal).
// An upgrade is bought at its station: it needs the station's survivor rescued (except the
// generator), scrap and materials, and for levels above 1 the generator upgraded far enough.
public static class MetaProgress
{
    private const string FileName = "meta_progress.json";

    public const int ScrapPerKill = 1;
    public const int ScrapPerFloor = 15;
    public const int ScrapForVictory = 40;

    [Serializable]
    private class SaveData
    {
        public int version = 1;
        public int scrap;
        public int totalScrap;
        public int runs;
        public int victories;
        public int bestFloor;
        public int kills;
        public int[] levels = new int[0];
        public int[] materials = new int[0];
        public int[] rescued = new int[0];
    }

    private static SaveData data;
    private static bool runActive;

    public static event Action Changed;

    public static int Scrap => Data.scrap;
    public static int Runs => Data.runs;
    public static int Victories => Data.victories;
    public static int BestFloor => Data.bestFloor;
    public static int Kills => Data.kills;

    private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        data = null;
        runActive = false;
        Changed = null;
    }

    private static SaveData Data
    {
        get
        {
            if (data == null)
                Load();

            return data;
        }
    }

    public static int Level(MetaUpgrade upgrade)
    {
        int index = (int)upgrade;
        return index < Data.levels.Length ? Data.levels[index] : 0;
    }

    // ---------- Materials ----------

    public static int MaterialCount(MaterialType type)
    {
        int index = (int)type;
        return index < Data.materials.Length ? Data.materials[index] : 0;
    }

    // Banks materials (amounts indexed by MaterialType).
    public static void AddMaterials(int[] amounts)
    {
        bool any = false;

        for (int i = 0; i < amounts.Length; i++)
        {
            if (amounts[i] == 0)
                continue;

            AddMaterial((MaterialType)i, amounts[i]);
            any = true;
        }

        if (!any)
            return;

        Save();
        Changed?.Invoke();
    }

    private static void AddMaterial(MaterialType type, int amount)
    {
        int index = (int)type;

        if (Data.materials.Length <= index)
            Array.Resize(ref Data.materials, Materials.All.Length);

        Data.materials[index] = Mathf.Max(0, Data.materials[index] + amount);
    }

    // ---------- Survivors ----------

    public static bool IsRescued(NpcKind kind)
    {
        int index = (int)kind;
        return index < Data.rescued.Length && Data.rescued[index] != 0;
    }

    public static int RescuedCount()
    {
        int count = 0;

        foreach (NpcKind kind in Npcs.All)
        {
            if (IsRescued(kind))
                count++;
        }

        return count;
    }

    // Saved straight away: a freed survivor stays freed even if the run ends badly.
    public static void Rescue(NpcKind kind)
    {
        if (IsRescued(kind))
            return;

        int index = (int)kind;

        if (Data.rescued.Length <= index)
            Array.Resize(ref Data.rescued, Npcs.All.Length);

        Data.rescued[index] = 1;
        Save();
        Changed?.Invoke();
    }

    // ---------- Upgrades ----------

    // Why the next level can't be bought right now (station locked, generator too low, short of
    // scrap or materials), or null if it can.
    public static string BlockedReason(MetaUpgrade upgrade)
    {
        int cost = NextCost(upgrade);

        if (cost < 0)
            return "FULLY UPGRADED";

        HideoutStation station = MetaUpgrades.Station(upgrade);

        if (!Stations.Unlocked(station))
            return $"RESCUE THE {Npcs.Role(Stations.Owner(station).Value)} FIRST";

        int level = Level(upgrade) + 1;
        int generator = MetaUpgrades.GeneratorNeeded(upgrade, level);

        if (Level(MetaUpgrade.Generator) < generator)
            return $"NEEDS GENERATOR POWER LEVEL {generator}";

        if (Data.scrap < cost)
            return $"NOT ENOUGH SCRAP // NEED {cost - Data.scrap} MORE";

        foreach (MaterialCost material in MetaUpgrades.MaterialCosts(upgrade, level))
        {
            int have = MaterialCount(material.Type);

            if (have < material.Amount)
                return $"NOT ENOUGH {Materials.Name(material.Type)} // NEED {material.Amount - have} MORE";
        }

        return null;
    }

    // The scrap price of the next level, or -1 if it's maxed.
    public static int NextCost(MetaUpgrade upgrade)
    {
        int level = Level(upgrade);
        return level >= MetaUpgrades.MaxLevel(upgrade) ? -1 : MetaUpgrades.Cost(upgrade, level + 1);
    }

    public static bool TryBuy(MetaUpgrade upgrade)
    {
        if (BlockedReason(upgrade) != null)
            return false;

        int level = Level(upgrade) + 1;
        Data.scrap -= NextCost(upgrade);

        foreach (MaterialCost material in MetaUpgrades.MaterialCosts(upgrade, level))
            AddMaterial(material.Type, -material.Amount);

        SetLevel(upgrade, Level(upgrade) + 1);
        Save();
        Changed?.Invoke();
        return true;
    }

    private static void SetLevel(MetaUpgrade upgrade, int level)
    {
        int index = (int)upgrade;

        if (Data.levels.Length <= index)
            Array.Resize(ref Data.levels, MetaUpgrades.All.Length);

        Data.levels[index] = level;
    }

    // ---------- Runs ----------

    // A run has begun (floor 1 is being played); it pays out when it ends.
    public static void BeginRun()
    {
        runActive = true;
    }

    // The run ended in death or victory: banks its scrap and stats. Returns the scrap earned, or
    // 0 if this run has already been paid (or never began).
    public static int EndRun(bool victory, int floorsCleared, int deepestFloor, int kills)
    {
        if (!runActive)
            return 0;

        runActive = false;
        int earned = ScrapFor(victory, floorsCleared, kills);
        Data.scrap += earned;
        Data.totalScrap += earned;
        Data.runs++;
        Data.kills += kills;
        Data.bestFloor = Mathf.Max(Data.bestFloor, deepestFloor);

        if (victory)
            Data.victories++;

        Save();
        Changed?.Invoke();
        return earned;
    }

    // The generator adds 10% a level.
    public static int ScrapFor(bool victory, int floorsCleared, int kills)
    {
        int basic = kills * ScrapPerKill + floorsCleared * ScrapPerFloor + (victory ? ScrapForVictory : 0);
        return Mathf.RoundToInt(basic * (1f + 0.1f * Level(MetaUpgrade.Generator)));
    }

    // Debug: some scrap to spend.
    // Pays scrap for something (the trader's goods). False, and nothing spent, if short.
    public static bool SpendScrap(int amount)
    {
        if (amount < 0 || Data.scrap < amount)
            return false;

        Data.scrap -= amount;
        Save();
        Changed?.Invoke();
        return true;
    }

    public static void AddScrap(int amount)
    {
        Data.scrap += amount;
        Save();
        Changed?.Invoke();
    }

    // Debug: some of every material.
    public static void AddEveryMaterial(int amount)
    {
        int[] amounts = new int[Materials.All.Length];

        for (int i = 0; i < amounts.Length; i++)
            amounts[i] = amount;

        AddMaterials(amounts);
    }

    // Debug: a completed run on the record (unlocks survivors' signals).
    public static void AddVictory()
    {
        Data.victories++;
        Save();
        Changed?.Invoke();
    }

    // Debug: every survivor freed.
    public static void RescueAll()
    {
        foreach (NpcKind kind in Npcs.All)
            Rescue(kind);
    }

    // Debug: wipes everything back to a fresh save.
    public static void ResetAll()
    {
        data = new SaveData();
        Save();
        Changed?.Invoke();
    }

    // ---------- File ----------

    private static void Load()
    {
        data = new SaveData();

        try
        {
            if (File.Exists(SavePath))
                JsonUtility.FromJsonOverwrite(File.ReadAllText(SavePath), data);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"MetaProgress: couldn't read {SavePath}, starting fresh. {e.Message}");
            data = new SaveData();
        }

        data.levels ??= new int[0];
        data.materials ??= new int[0];
        data.rescued ??= new int[0];
        data.version = 2;
    }

    private static void Save()
    {
        try
        {
            // Write to a temporary file first, so a crash mid-write can't corrupt the save.
            string temp = SavePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));

            if (File.Exists(SavePath))
                File.Delete(SavePath);

            File.Move(temp, SavePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"MetaProgress: couldn't save to {SavePath}. {e.Message}");
        }
    }
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
            default: return "SCAVENGER";
        }
    }

    public static int MaxLevel(MetaUpgrade upgrade)
    {
        return upgrade == MetaUpgrade.FieldKit || upgrade == MetaUpgrade.Generator || upgrade == MetaUpgrade.Gunsmith
            || upgrade == MetaUpgrade.Recon ? 2 : 3;
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
