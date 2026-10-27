using System;
using System.IO;
using UnityEngine;

// MetaProgress's save profiles and the save file: picking, summarising and wiping profiles,
// and reading and writing the JSON (written to a temp file first, then swapped in).
public static partial class MetaProgress
{
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
        saved.zones ??= new int[0];
        saved.scenes ??= new int[0];
        saved.version = 4;
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
