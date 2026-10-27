using System;
using System.IO;
using UnityEngine;

// Progress that carries over between runs (the only thing the game saves; there are no
// mid-run saves): scrap, banked materials, the survivors rescued, the station upgrades bought
// with scrap and materials, and a few lifetime stats. Kept in a small JSON file in the player's
// data folder, written whenever something changes. Scrap earned on a run (a scrap a kill, more
// for each boss beaten) is carried, like materials: it's banked only when sent home from a boss
// room's uplink terminal (UplinkTerminal) or when the run is won (with a bonus for winning), and
// whatever hasn't been sent home is lost if the player dies (abandoned runs pay nothing either).
// An upgrade is bought at its station: it needs the station's survivor rescued (except the
// generator), scrap and materials, and for levels above 1 the generator upgraded far enough.
// There are three save profiles, each its own file (profile 1 keeps the original file name); the
// one in use is remembered in PlayerPrefs and picked or wiped from the main menu.
public static partial class MetaProgress
{
    private const string FileName = "meta_progress.json";
    private const string ProfileKey = "MetaProfile";

    public const int ProfileCount = 3;

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
        public int[] story = new int[0];
        public int zone;                    // the zone runs start in (Zone)
        public int cookArrived = -1;        // Runs when the cook turned up (-1: not yet)
        public int cookCureSeen;            // 1 once the cured cook's moved into their kitchen
        public int evacs;                   // times the evac chopper has flown everyone out (the ending)
        public int[] scenes = new int[0];   // one-time story scenes already played, by Npcs.Scene
        public int[] zones = new int[0];    // zones unlocked from the debug menu, by Zone
        public int loop;                    // New Game+ loops started (0: the first playthrough)
        public int loopEvacs;               // evacs when the current loop began
    }

    // What the main menu shows for a profile without switching to it.
    public struct ProfileSummary
    {
        public bool Exists;
        public int Scrap;
        public int Runs;
        public int Victories;
        public int Rescued;
    }

    private static SaveData data;
    private static bool runActive;
    private static int profile;   // 0 until read from PlayerPrefs

    public static event Action Changed;

    public static int Scrap => Data.scrap;
    public static int Runs => Data.runs;
    public static int Victories => Data.victories;
    public static int BestFloor => Data.bestFloor;
    public static int Kills => Data.kills;

    // The profile in use, 1 to ProfileCount.
    public static int Profile
    {
        get
        {
            if (profile == 0)
                profile = Mathf.Clamp(PlayerPrefs.GetInt(ProfileKey, 1), 1, ProfileCount);

            return profile;
        }
    }

    private static string SavePath => PathFor(Profile);

    private static string PathFor(int slot)
    {
        string file = slot == 1 ? FileName : $"meta_progress_{slot}.json";
        return Path.Combine(Application.persistentDataPath, file);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        data = null;
        runActive = false;
        ScrapSentHome = 0;
        BonusScrap = 0;
        profile = 0;
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

    // A run has begun (floor 1 is being played); its scrap is carried until sent home or won.
    public static void BeginRun()
    {
        runActive = true;
        ScrapSentHome = 0;
        BonusScrap = 0;
    }

    // Scrap picked up as it is on this run (the supply room's shelves), on top of what kills and
    // floors earn. Carried like the rest: banked with the win, lost on a death.
    public static int BonusScrap { get; private set; }

    public static void AddBonusScrap(int amount)
    {
        if (!runActive || amount <= 0)
            return;

        BonusScrap += amount;
        Changed?.Invoke();
    }

    // Scrap this run has already sent home from uplinks (banked).
    public static int ScrapSentHome { get; private set; }

    // Scrap earned on this run so far that hasn't been sent home yet: lost if the player dies.
    public static int CarriedScrap(int floorsCleared, int kills)
    {
        return runActive ? Mathf.Max(0, ScrapFor(false, floorsCleared, kills) - ScrapSentHome) : 0;
    }

    // An uplink sends the carried scrap home: banked for good. Returns how much went.
    public static int SendScrapHome(int floorsCleared, int kills)
    {
        int amount = CarriedScrap(floorsCleared, kills);

        if (amount <= 0)
            return 0;

        ScrapSentHome += amount;
        Data.scrap += amount;
        Data.totalScrap += amount;
        Save();
        Changed?.Invoke();
        return amount;
    }

    // The run ended in death or victory: banks its stats, and on a victory the rest of its scrap
    // (with the bonus for winning). On a death the carried scrap is simply gone. Returns the
    // scrap banked now (0 for a death, or if this run has already ended or never began).
    public static int EndRun(bool victory, int floorsCleared, int deepestFloor, int kills)
    {
        if (!runActive)
            return 0;

        runActive = false;
        int earned = victory ? Mathf.Max(0, ScrapFor(true, floorsCleared, kills) - ScrapSentHome) : 0;
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

    // The generator adds 10% a level (not to scrap picked up as it is).
    public static int ScrapFor(bool victory, int floorsCleared, int kills)
    {
        int basic = kills * ScrapPerKill + floorsCleared * ScrapPerFloor + (victory ? ScrapForVictory : 0);
        return Mathf.RoundToInt(basic * (1f + 0.1f * Level(MetaUpgrade.Generator))) + BonusScrap;
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

}
