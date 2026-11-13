using System;

// MetaProgress's lifetime counters and achievements: AddStat bumps a counter, earns any achievement
// whose goal it reaches, saves at once when one is earned (otherwise the counters are only marked dirty and Flush writes them
// at run end, floor change, hideout return and quit) and tells the UNLOCKED card and other listeners.
public static partial class MetaProgress
{
    public static event Action<Achievement> AchievementEarned;

    public static int StatValue(Stat stat)
    {
        int i = (int)stat;
        return i < Data.stats.Length ? Data.stats[i] : 0;
    }

    public static bool IsAchieved(Achievement a)
    {
        int i = (int)a;
        return i < Data.achieved.Length && Data.achieved[i] != 0;
    }

    // DeepestFloor keeps the highest value given; every other counter adds.
    public static void AddStat(Stat stat, int amount = 1)
    {
        int i = (int)stat;

        if (Data.stats.Length <= i)
            Array.Resize(ref Data.stats, Enum.GetValues(typeof(Stat)).Length);

        Data.stats[i] = stat == Stat.DeepestFloor ? Math.Max(Data.stats[i], amount) : Data.stats[i] + amount;

        bool earned = false;

        foreach (Achievement a in Achievements.All)
        {
            if (Achievements.StatFor(a) == stat && !IsAchieved(a) && Data.stats[i] >= Achievements.Goal(a))
            {
                SetAchieved(a, true);
                earned = true;
            }
        }

        if (earned)
            Save();
        else
            statsDirty = true;

        Changed?.Invoke();
    }

    private static bool statsDirty;

    // Writes counters that AddStat left unsaved.
    public static void Flush()
    {
        if (!statsDirty)
            return;

        Save();
    }

    private static void SetAchieved(Achievement a, bool value)
    {
        if (Data.achieved.Length <= (int)a)
            Array.Resize(ref Data.achieved, Achievements.All.Length);

        Data.achieved[(int)a] = value ? 1 : 0;

        if (value)
        {
            UnlockCard.Enqueue(a);
            AchievementEarned?.Invoke(a);
        }
    }

    // Debug: every achievement earned or none (locking also zeroes the counters).
    public static void SetAllAchieved(bool value)
    {
        foreach (Achievement a in Achievements.All)
        {
            if (IsAchieved(a) != value)
                SetAchieved(a, value);
        }

        if (!value)
            Array.Clear(Data.stats, 0, Data.stats.Length);   // LOCK ALL is a clean slate

        Save();
        Changed?.Invoke();
    }
}
