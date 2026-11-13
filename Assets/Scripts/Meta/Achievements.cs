using System;
using System.Collections.Generic;

// The lifetime counters (Stat) and the achievements (Achievement) built on them. Each achievement
// watches one counter, has a goal, and unlocks things: run items, guns and room kinds that start
// locked (Unlocks reads these tables). MetaProgress.AddStat does the counting and earning.
public enum Stat
{
    Kills, BossKills, DeepestFloor, FloorTwoBossKills, Wins, MeleeKills,
    BlastKills, UnawareKills, CloseCalls, EliteKills, ShopBuys, CursedWins
}

public enum Achievement
{
    BodyCount, BossDown, GoingDeeper, FloorTwoClear, WayOut, UpClose,
    Demolition, Ghost, Untouchable, EliteHunter, Regular, Damned
}

// The special rooms that start locked; Unlocks.IsUnlocked(RoomType) maps room types onto these.
public enum UnlockRoom { Shop, Cursed, Blood, Secret }

public static class Achievements
{
    public static readonly Achievement[] All = (Achievement[])Enum.GetValues(typeof(Achievement));

    private struct Info
    {
        public string Name, HowToEarn;
        public Stat Stat;
        public int Goal;
        public RunItem[] Items;
        public string[] Guns;            // prefab names
        public UnlockRoom[] Rooms;
    }

    private static readonly RunItem[] NoItems = new RunItem[0];
    private static readonly string[] NoGuns = new string[0];
    private static readonly UnlockRoom[] NoRooms = new UnlockRoom[0];

    private static Info Make(string name, string how, Stat stat, int goal,
        RunItem[] items = null, string[] guns = null, UnlockRoom[] rooms = null)
    {
        return new Info
        {
            Name = name, HowToEarn = how, Stat = stat, Goal = goal,
            Items = items ?? NoItems, Guns = guns ?? NoGuns, Rooms = rooms ?? NoRooms
        };
    }

    private static readonly Info[] infos =
    {
        Make("BODY COUNT", "Kill 100 zombies", Stat.Kills, 100, new[] { RunItem.CorpseBloat, RunItem.LastWord }),
        Make("BOSS DOWN", "Kill a boss", Stat.BossKills, 1, rooms: new[] { UnlockRoom.Shop }),
        Make("GOING DEEPER", "Reach floor 3", Stat.DeepestFloor, 3, new[] { RunItem.FuelLeak, RunItem.HardLanding }),
        Make("FLOOR TWO CLEAR", "Kill a floor 2 boss", Stat.FloorTwoBossKills, 1, guns: new[] { "AK47" }),
        Make("WAY OUT", "Win a run", Stat.Wins, 1, new[] { RunItem.TwinFeed }, new[] { "M4A1" }),
        Make("UP CLOSE", "Get 50 melee kills", Stat.MeleeKills, 50, new[] { RunItem.Whirlwind, RunItem.Bayonet, RunItem.BatterUp }),
        Make("DEMOLITION", "Get 25 blast kills", Stat.BlastKills, 25, new[] { RunItem.ShortFuse, RunItem.ClusterPin }, rooms: new[] { UnlockRoom.Secret }),
        Make("GHOST", "Kill 25 zombies that have not noticed you", Stat.UnawareKills, 25, new[] { RunItem.ColdOpen }),
        Make("UNTOUCHABLE", "Roll through 30 shots", Stat.CloseCalls, 30, new[] { RunItem.RicochetRounds, RunItem.SeekerRounds }),
        Make("ELITE HUNTER", "Kill 10 elite zombies", Stat.EliteKills, 10, rooms: new[] { UnlockRoom.Cursed }),
        Make("REGULAR", "Buy from a shop 3 times", Stat.ShopBuys, 3, new[] { RunItem.JunkDrawer }, rooms: new[] { UnlockRoom.Blood }),
        Make("DAMNED", "Win a run holding a cursed item", Stat.CursedWins, 1, guns: new[] { "HuntingRifle" }),
    };

    public static string Name(Achievement a) => infos[(int)a].Name;
    public static string HowToEarn(Achievement a) => infos[(int)a].HowToEarn;
    public static int Goal(Achievement a) => infos[(int)a].Goal;
    public static Stat StatFor(Achievement a) => infos[(int)a].Stat;
    public static RunItem[] ItemsUnlocked(Achievement a) => infos[(int)a].Items;
    public static string[] GunsUnlocked(Achievement a) => infos[(int)a].Guns;
    public static UnlockRoom[] RoomsUnlocked(Achievement a) => infos[(int)a].Rooms;

    // What earning it unlocks, for display.
    public static string UnlocksText(Achievement a)
    {
        Info info = infos[(int)a];
        List<string> parts = new List<string>();

        foreach (RunItem item in info.Items)
            parts.Add(RunItems.Name(item));

        foreach (string gun in info.Guns)
            parts.Add(gun == "AK47" ? "AK-47" : Gun.DisplayName(gun));

        foreach (UnlockRoom room in info.Rooms)
            parts.Add(room.ToString().ToUpperInvariant() + " ROOMS");

        return string.Join(", ", parts);
    }
}
