using System;
using UnityEngine;

// The materials picked up on the current run that haven't been banked yet. They're at risk:
// dying (or abandoning the run) loses them. They bank into MetaProgress at checkpoints: when a
// floor's boss dies, when the player takes the hatch down, and on victory. Carried in a pouch of
// their own, not the backpack, so they never compete with ammo for space. The Secure Pouch
// upgrade saves a few on death (the scarcest first), Tarkov secure-container style.
public static class RunMaterials
{
    private static int[] carried = new int[Materials.All.Length];

    // Fired whenever the carried amounts change (picked up, banked or lost).
    public static event Action Changed;

    // Fired when materials are banked, with what was banked (indexed by MaterialType).
    public static event Action<int[]> Banked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        carried = new int[Materials.All.Length];
        Changed = null;
        Banked = null;
    }

    public static int Carried(MaterialType type) => carried[(int)type];

    public static int Total
    {
        get
        {
            int total = 0;

            foreach (int amount in carried)
                total += amount;

            return total;
        }
    }

    public static void Add(MaterialType type, int amount)
    {
        carried[(int)type] += amount;
        Changed?.Invoke();
    }

    // A checkpoint: everything carried goes into the save.
    public static void Bank()
    {
        if (Total == 0)
            return;

        int[] banked = (int[])carried.Clone();
        MetaProgress.AddMaterials(banked);
        carried = new int[Materials.All.Length];
        Changed?.Invoke();
        Banked?.Invoke(banked);
    }

    // Death or an abandoned run: everything carried is gone. Returns what was lost.
    public static int[] Lose()
    {
        int[] lost = (int[])carried.Clone();
        carried = new int[Materials.All.Length];
        Changed?.Invoke();
        return lost;
    }

    // How many carried materials survive death (the Secure Pouch upgrade).
    public static int SecureSize => MetaUpgrades.SecurePouchSize(MetaProgress.Level(MetaUpgrade.SecurePouch));

    // Death: the secure pouch's worth are banked (fuel and wiring first, being the scarcest, then
    // chemicals, gunpowder, parts) and the rest are lost. Returns what was lost and what was kept.
    public static (int[] lost, int[] kept) Die()
    {
        int[] kept = new int[carried.Length];
        int room = SecureSize;
        MaterialType[] priority = { MaterialType.Fuel, MaterialType.Wiring, MaterialType.Chemicals, MaterialType.Gunpowder, MaterialType.Parts };

        foreach (MaterialType type in priority)
        {
            int take = Mathf.Min(room, carried[(int)type]);
            kept[(int)type] = take;
            carried[(int)type] -= take;
            room -= take;
        }

        if (room < SecureSize)
            MetaProgress.AddMaterials(kept);

        return (Lose(), kept);
    }

    // "3 PARTS, 1 FUEL" for a set of amounts, coloured per material; empty if none.
    public static string Describe(int[] amounts)
    {
        System.Text.StringBuilder text = new System.Text.StringBuilder();

        for (int i = 0; i < amounts.Length; i++)
        {
            if (amounts[i] <= 0)
                continue;

            if (text.Length > 0)
                text.Append(", ");

            MaterialType type = (MaterialType)i;
            text.Append($"<color={Materials.Hex(type)}>{amounts[i]} {Materials.Name(type)}</color>");
        }

        return text.ToString();
    }
}
