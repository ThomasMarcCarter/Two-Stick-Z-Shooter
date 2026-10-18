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
    Scavenger       // enemies drop ammo more often
}

// Progress that carries over between runs (the only thing the game saves; there are no
// mid-run saves): scrap, the hideout upgrades bought with it, and a few lifetime stats. Kept in
// a small JSON file in the player's data folder, written whenever something changes. A run pays
// out once, when it ends in death or victory: a scrap a kill, more for each boss beaten, and a
// bonus for winning. Abandoned runs (restart, quit to menu) pay nothing.
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

    // The scrap price of the next level, or -1 if it's maxed.
    public static int NextCost(MetaUpgrade upgrade)
    {
        int level = Level(upgrade);
        return level >= MetaUpgrades.MaxLevel(upgrade) ? -1 : MetaUpgrades.Cost(upgrade, level + 1);
    }

    public static bool TryBuy(MetaUpgrade upgrade)
    {
        int cost = NextCost(upgrade);

        if (cost < 0 || Data.scrap < cost)
            return false;

        Data.scrap -= cost;
        SetLevel(upgrade, Level(upgrade) + 1);
        Save();
        Changed?.Invoke();
        return true;
    }

    private static void SetLevel(MetaUpgrade upgrade, int level)
    {
        int index = (int)upgrade;

        if (Data.levels.Length <= index)
            Array.Resize(ref Data.levels, Enum.GetValues(typeof(MetaUpgrade)).Length);

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

    public static int ScrapFor(bool victory, int floorsCleared, int kills)
    {
        return kills * ScrapPerKill + floorsCleared * ScrapPerFloor + (victory ? ScrapForVictory : 0);
    }

    // Debug: some scrap to spend.
    public static void AddScrap(int amount)
    {
        Data.scrap += amount;
        Save();
        Changed?.Invoke();
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
            default: return "SCAVENGER";
        }
    }

    public static int MaxLevel(MetaUpgrade upgrade)
    {
        return upgrade == MetaUpgrade.FieldKit ? 2 : 3;
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
