using System;
using UnityEngine;

// What is still locked away from the pools a run rolls from: run items, guns and special rooms.
// Anything an achievement lists (the Achievements tables) is locked until that achievement is
// earned; everything else is always available.
public static class Unlocks
{
    public static bool IsUnlocked(RunItem item)
    {
        foreach (Achievement a in Achievements.All)
        {
            if (Array.IndexOf(Achievements.ItemsUnlocked(a), item) >= 0 && !MetaProgress.IsAchieved(a))
                return false;
        }

        return true;
    }

    public static bool IsRoomUnlocked(UnlockRoom room)
    {
        foreach (Achievement a in Achievements.All)
        {
            if (Array.IndexOf(Achievements.RoomsUnlocked(a), room) >= 0 && !MetaProgress.IsAchieved(a))
                return false;
        }

        return true;
    }

    // The special room types map onto UnlockRoom (IsRoomUnlocked); every other type is always open.
    public static bool IsUnlocked(RoomType type)
    {
        switch (type)
        {
            case RoomType.Shop: return IsRoomUnlocked(UnlockRoom.Shop);
            case RoomType.Cursed: return IsRoomUnlocked(UnlockRoom.Cursed);
            case RoomType.Blood: return IsRoomUnlocked(UnlockRoom.Blood);
            case RoomType.Secret: return IsRoomUnlocked(UnlockRoom.Secret);
            default: return true;
        }
    }

    // By prefab name; a clone's "(Clone)" suffix is ignored.
    public static bool IsWeaponUnlocked(GameObject weaponPrefab)
    {
        if (weaponPrefab == null)
            return true;

        string name = weaponPrefab.name.Replace("(Clone)", "").Trim();

        foreach (Achievement a in Achievements.All)
        {
            if (Array.IndexOf(Achievements.GunsUnlocked(a), name) >= 0 && !MetaProgress.IsAchieved(a))
                return false;
        }

        return true;
    }
}
