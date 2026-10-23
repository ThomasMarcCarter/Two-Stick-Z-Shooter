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
// data folder, written whenever something changes. Scrap earned on a run (a scrap a kill, more
// for each boss beaten) is carried, like materials: it's banked only when sent home from a boss
// room's uplink terminal (UplinkTerminal) or when the run is won (with a bonus for winning), and
// whatever hasn't been sent home is lost if the player dies (abandoned runs pay nothing either).
// An upgrade is bought at its station: it needs the station's survivor rescued (except the
// generator), scrap and materials, and for levels above 1 the generator upgraded far enough.
// There are three save profiles, each its own file (profile 1 keeps the original file name); the
// one in use is remembered in PlayerPrefs and picked or wiped from the main menu.
public static class MetaProgress
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

    // ---------- Story items ----------

    public static bool HasStoryItem(StoryItem item)
    {
        int index = (int)item;
        return index < Data.story.Length && Data.story[index] != 0;
    }

    // A story item brought home (a won run's truck part): kept for good.
    public static void AddStoryItem(StoryItem item)
    {
        if (HasStoryItem(item))
            return;

        int index = (int)item;

        if (Data.story.Length <= index)
            Array.Resize(ref Data.story, StoryItems.All.Length);

        Data.story[index] = 1;
        Save();
        Changed?.Invoke();
    }

    // Debug: the next truck part, as if a run had brought it home.
    public static void GrantNextStoryItem()
    {
        StoryItem? next = StoryItems.Next(NpcKind.Mechanic);

        if (next != null)
            AddStoryItem(next.Value);
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

    // ---------- Profiles ----------

    // Switches to another profile; its save is read the next time anything asks.
    public static void SelectProfile(int slot)
    {
        slot = Mathf.Clamp(slot, 1, ProfileCount);

        if (slot == Profile)
            return;

        profile = slot;
        runActive = false;
        data = null;
        PlayerPrefs.SetInt(ProfileKey, slot);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static ProfileSummary Summary(int slot)
    {
        SaveData saved = slot == Profile ? Data : Read(PathFor(slot));
        int rescued = 0;

        foreach (int flag in saved.rescued)
        {
            if (flag != 0)
                rescued++;
        }

        return new ProfileSummary
        {
            Exists = File.Exists(PathFor(slot)),
            Scrap = saved.scrap,
            Runs = saved.runs,
            Victories = saved.victories,
            Rescued = rescued
        };
    }

    // Deletes a profile's save file for good. Wiping the profile in use starts it afresh.
    public static void WipeProfile(int slot)
    {
        string path = PathFor(slot);

        try
        {
            if (File.Exists(path))
                File.Delete(path);

            if (File.Exists(path + ".tmp"))
                File.Delete(path + ".tmp");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"MetaProgress: couldn't delete {path}. {e.Message}");
        }

        if (slot == Profile)
        {
            runActive = false;
            data = null;
            Changed?.Invoke();
        }
    }

    // ---------- File ----------

    private static void Load()
    {
        data = Read(SavePath);
    }

    private static SaveData Read(string path)
    {
        SaveData saved = new SaveData();

        try
        {
            if (File.Exists(path))
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), saved);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"MetaProgress: couldn't read {path}, starting fresh. {e.Message}");
            saved = new SaveData();
        }

        saved.levels ??= new int[0];
        saved.materials ??= new int[0];
        saved.rescued ??= new int[0];
        saved.story ??= new int[0];
        saved.version = 3;
        return saved;
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
