using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's run start: the hideout's station upgrades applied when floor 1 begins
// (starting gear, health, Recon on the maps), and the run's clock and scrap tally started.
public partial class FloorGenerator
{
    // The Recon upgrade: the boss room (level 1), or every room (level 2), shows on the maps from
    // the start of each floor. Scouted rooms still show as unexplored until entered.
    private void ApplyRecon()
    {
        int recon = MetaProgress.Level(MetaUpgrade.Recon);

        if (recon <= 0)
            return;

        foreach (Room room in rooms)
        {
            if (recon >= 2 || room.Type == RoomType.Boss)
                room.Scouted = true;
        }
    }

    // The start of a run (floor 1): applies the station upgrades to the player and starts the
    // clock and the run's scrap tally. Waits a frame so the player's own start-up (starting gun,
    // starting ammo) has happened first.
    private System.Collections.IEnumerator BeginRun()
    {
        runPerksApplied = true;
        RunStartTime = Time.time;
        MetaProgress.BeginRun();
        TraderStock.Settle();
        yield return null;

        if (player == null)
            yield break;

        int toughness = MetaProgress.Level(MetaUpgrade.Toughness);
        int conditioning = MetaProgress.Level(MetaUpgrade.Conditioning);
        int cache = MetaProgress.Level(MetaUpgrade.AmmoCache);
        int kit = MetaProgress.Level(MetaUpgrade.FieldKit);
        int armory = MetaProgress.Level(MetaUpgrade.Armory);
        int gunsmith = MetaProgress.Level(MetaUpgrade.Gunsmith);

        if (toughness > 0 && player.TryGetComponent(out PlayerHealth health))
            health.AddMaxHealth(toughness);

        if (conditioning > 0 && player.TryGetComponent(out PlayerStamina stamina))
            stamina.ScaleMax(1f + 0.15f * conditioning);

        // The cook's meals before heading out.
        int stew = MetaProgress.Level(MetaUpgrade.HeartyStew);
        int coffee = MetaProgress.Level(MetaUpgrade.StrongCoffee);
        int broth = MetaProgress.Level(MetaUpgrade.BoneBroth);

        if (player.TryGetComponent(out PlayerHealth fed))
        {
            if (stew > 0)
                fed.AddMaxHealth(stew);

            fed.MedkitBonus = broth;
        }

        if (coffee > 0 && player.TryGetComponent(out PlayerStamina rested))
            rested.ScaleRegen(1f + 0.2f * coffee);

        AmmoInventory ammo = player.GetComponent<AmmoInventory>();

        if (cache > 0 && ammo != null)
            ammo.Add(AmmoType.NineMil, MetaUpgrades.AmmoCacheRounds(cache));

        Backpack backpack = Backpack.For(player.gameObject);

        if (kit >= 1)
            backpack.AddConsumable(ConsumableType.Medkit, 1, true);

        if (kit >= 2)
            backpack.AddConsumable(ConsumableType.Grenade, 1, true);

        if (armory > 0 && armoryWeapons != null && armoryWeapons.Length > 0 && player.TryGetComponent(out PlayerWeapon weapons))
        {
            Gun gun = weapons.Give(armoryWeapons[Mathf.Min(armory, armoryWeapons.Length) - 1]);

            // A spare magazine's worth for it.
            if (gun != null && ammo != null)
                ammo.Add(gun.AmmoType, gun.GetMagazineSize());
        }

        // Attachments in the backpack, usually ones that fit the guns carried.
        if (gunsmith > 0)
        {
            foreach (WeaponAttachment attachment in PickAttachments(gunsmith, () => UnityEngine.Random.value))
                backpack.AddAttachment(attachment);
        }

        // Until the mechanic's been found, every Military Base run starts with their call from the
        // supply room.
        if (!IsTutorial && Zone == Zone.MilitaryBase && !MetaProgress.IsRescued(NpcKind.Mechanic))
            StartCoroutine(DistressCall(NpcKind.Mechanic));
    }
}
