using System;
using System.IO;
using UnityEngine;

// MetaProgress's story state: story items brought home, scenes seen, the cook's arrival and
// cure, the evac, and which zone is chosen and unlocked.
public static partial class MetaProgress
{
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

    // Debug: the next item of a survivor's goal (the truck parts by default), as if a run had
    // brought it home.
    public static void GrantNextStoryItem(NpcKind owner = NpcKind.Mechanic)
    {
        StoryItem? next = StoryItems.Next(owner);

        if (next != null)
            AddStoryItem(next.Value);
    }

    // The run count when the infected cook turned up, for how sick they look (Npcs.CookSickness).
    public static int CookArrivedRun => Data.cookArrived < 0 ? Data.runs : Data.cookArrived;

    // The cook stumbles in: on the books for good, and the clock on their sickness starts.
    public static void CookArrives()
    {
        if (IsRescued(NpcKind.Cook))
            return;

        Data.cookArrived = Data.runs;
        Rescue(NpcKind.Cook);
    }

    // A one-time story scene (a goal starting or finished) has played.
    public static bool SceneSeen(Npcs.Scene scene)
    {
        int index = (int)scene;
        return index < Data.scenes.Length && Data.scenes[index] != 0;
    }

    public static void SeeScene(Npcs.Scene scene)
    {
        int index = (int)scene;

        if (Data.scenes.Length <= index)
            Array.Resize(ref Data.scenes, index + 1);

        Data.scenes[index] = 1;
        Save();
    }

    // Times the game's been finished: everyone flown out in the evac chopper (EvacSequence).
    public static int Evacs => Data.evacs;

    public static void RecordEvac()
    {
        Data.evacs++;
        Save();
        Changed?.Invoke();
    }

    // Whether the cook's cure has been seen in the hideout (their thank-you plays once).
    public static bool CookCureSeen => Data.cookCureSeen != 0;

    public static void SeeCookCure()
    {
        Data.cookCureSeen = 1;
        Save();
    }

    // ---------- Zones ----------

    // The zone the survivors are parked in (its hideout, and where the next run starts): the one
    // last driven to, or the Military Base if that's locked.
    public static Zone ChosenZone
    {
        get
        {
            Zone zone = (Zone)Mathf.Clamp(Data.zone, 0, Zones.All.Length - 1);
            return Zones.IsUnlocked(zone) ? zone : Zone.MilitaryBase;
        }
    }

    public static void ChooseZone(Zone zone)
    {
        if (!Zones.IsUnlocked(zone) || (int)zone == Data.zone)
            return;

        Data.zone = (int)zone;
        Save();
        Changed?.Invoke();
    }

    // Unlocked by hand (the debug menu), apart from the story goals that normally open it.
    public static bool IsZoneFlagged(Zone zone)
    {
        int index = (int)zone;
        return index < Data.zones.Length && Data.zones[index] != 0;
    }

    // Debug: every zone open.
    public static void UnlockAllZones()
    {
        if (Data.zones.Length < Zones.All.Length)
            Array.Resize(ref Data.zones, Zones.All.Length);

        for (int i = 0; i < Data.zones.Length; i++)
            Data.zones[i] = 1;

        Save();
        Changed?.Invoke();
    }

    // Debug: the next unlocked zone after the chosen one, round to the first.
    public static void ChooseNextZone()
    {
        Zone chosen = ChosenZone;

        for (int step = 1; step <= Zones.All.Length; step++)
        {
            Zone zone = (Zone)(((int)chosen + step) % Zones.All.Length);

            if (Zones.IsUnlocked(zone))
            {
                ChooseZone(zone);
                return;
            }
        }
    }
}
