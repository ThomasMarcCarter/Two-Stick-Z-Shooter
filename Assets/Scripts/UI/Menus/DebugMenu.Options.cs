using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The DebugMenu's options: what each button does (teleports, spawners, kills, god mode, floor
// skips, hideout and save cheats) and the sections they're listed under.
public partial class DebugMenu
{
    private void BuildOptions()
    {
        Section(0, "FLOOR");
        Add("TELEPORT: BOSS ROOM", () => TeleportTo(RoomType.Boss), true);
        Add("TELEPORT: TREASURE ROOM", () => TeleportTo(RoomType.Treasure), true);
        Add("TELEPORT: SPAWN", () => TeleportTo(RoomType.Start), true);
        Add("REVEAL MAP", RevealMap, false);
        Add("SKIP TO NEXT FLOOR", SkipFloor, true);
        Add("GO TO THE SUPPLY ROOM", () => floor.DebugEnterSupplyRoom(), true);
        Add("GO TO THE ROOF (EVAC ENDING)", () => floor.DebugEnterEvac(), true);
        Add("REGENERATE FLOOR", RegenerateFloor, true);

        Section(0, "ENEMIES");
        Add("KILL ENEMIES IN ROOM", KillInRoom, false);
        Add("KILL ALL ENEMIES ON FLOOR", KillOnFloor, false);
        Add("SPAWN ONE OF EACH ZOMBIE", SpawnEnemyKinds, true);
        Add(() => "SHOW DETECTION RADII: " + (NoiseDebug.Enabled ? "ON" : "OFF"), () => NoiseDebug.SetEnabled(!NoiseDebug.Enabled), false);

        Section(0, "HIDEOUT");
        Add(() => $"GIVE 10 OF EACH MATERIAL (PARTS {MetaProgress.MaterialCount(MaterialType.Parts)})", () => MetaProgress.AddEveryMaterial(10), false);
        Add(() => $"RESCUE ALL SURVIVORS ({MetaProgress.RescuedCount()}/{Npcs.All.Length})", MetaProgress.RescueAll, false);
        Add("SPAWN ALL MATERIALS", SpawnMaterials, true);
        Add(() => $"+1 TRUCK PART ({StoryItems.Progress(NpcKind.Mechanic)})", () => MetaProgress.GrantNextStoryItem(), false);
        Add(() => $"+1 TRADE GOOD ({StoryItems.Progress(NpcKind.Trader)})", () => MetaProgress.GrantNextStoryItem(NpcKind.Trader), false);
        Add(() => $"+1 MEDICAL SUPPLY ({StoryItems.Progress(NpcKind.Medic)})", () => MetaProgress.GrantNextStoryItem(NpcKind.Medic), false);
        Add(() => $"+1 RADIO PART ({StoryItems.Progress(NpcKind.Quartermaster)})", () => MetaProgress.GrantNextStoryItem(NpcKind.Quartermaster), false);
        Add("SURVIVOR ON NEXT FLOOR", FloorGenerator.ForceSurvivorOnNextFloor, false);
        Add(() => $"NEXT RUN'S ZONE: {Zones.Name(MetaProgress.ChosenZone)}", MetaProgress.ChooseNextZone, false);
        Add("UNLOCK ALL ZONES", MetaProgress.UnlockAllZones, false);
        Add(() => $"START NEW GAME+ (LOOP {MetaProgress.Loop}, ENEMIES x{MetaProgress.Toughness:0.##})", MetaProgress.DebugStartNewGamePlus, true);

        Section(1, "PLAYER");
        Add(() => "GOD MODE: " + (Health != null && Health.GodMode ? "ON" : "OFF"), ToggleGodMode, false);
        Add("REFILL HEALTH + AMMO", Refill, false);
        Add("HURT PLAYER (1)", HurtPlayer, false);
        Add("GIVE MAX AMMO (ALL TYPES)", GiveAllAmmo, false);
        Add("GIVE CONSUMABLES (1 STACK EACH)", GiveConsumables, false);
        Add(() => $"GIVE 100 SCRAP ({MetaProgress.Scrap})", () => MetaProgress.AddScrap(100), false);
        Add("RESET META PROGRESS (ALL HIDEOUT)", MetaProgress.ResetAll, false);
        Add("REPLAY TUTORIAL", () => { MetaProgress.ResetTutorial(); RunFlow.RestartRun(); }, true);

        Section(1, "SPAWN ITEMS");
        Add("SPAWN WEAPONS", SpawnWeapons, true);
        Add("SPAWN ALL ATTACHMENTS", SpawnAttachments, true);
        Add("SPAWN ALL CONSUMABLES", SpawnConsumables, true);
        Add("SPAWN ALL MELEE UPGRADES", SpawnMeleeUpgrades, true);
        Add("FILL LOOT POINTS IN ROOM", FillLootPoints, false);
    }

    // Starts a fold-out group of options under a header. Sections must be added column by column.
    private void Section(int column, string header)
    {
        currentSection = new FoldSection { Name = header, Column = Mathf.Clamp(column, 0, ColumnCount - 1) };
        sections.Add(currentSection);
    }

    private void Add(string label, Action run, bool closesMenu)
    {
        Add(() => label, run, closesMenu);
    }

    private void Add(Func<string> label, Action run, bool closesMenu)
    {
        Option option = new Option { Label = label, Run = run, ClosesMenu = closesMenu };
        options.Add(option);
        currentSection.Options.Add(option);
    }

    private PlayerHealth Health => player != null ? player.GetComponent<PlayerHealth>() : null;

    private void TeleportTo(RoomType type)
    {
        foreach (Room room in floor.Rooms)
        {
            if (room.Type != type)
                continue;

            // Off to the side of the centre, where the boss and the treasure pickup sit.
            Rect anchor = room.AnchorRect;
            Vector3 position = new Vector3(anchor.center.x, 1f, anchor.center.y - anchor.height * 0.3f);
            MovePlayer(position);
            return;
        }
    }

    private void KillInRoom()
    {
        if (floor.CurrentRoom != null)
            KillAll(floor.CurrentRoom);
    }

    private void KillOnFloor()
    {
        foreach (Room room in floor.Rooms)
            KillAll(room);
    }

    private static void KillAll(Room room)
    {
        foreach (Enemy enemy in new List<Enemy>(room.Enemies))
        {
            if (enemy != null)
                enemy.Kill();
        }
    }

    // Spawns an item at every empty loot spawn point in the current room.
    private void FillLootPoints()
    {
        if (floor.CurrentRoom == null)
            return;

        int spawned = floor.ForceLoot(floor.CurrentRoom);
        status.text = $"LOOT // {spawned} ITEM{(spawned == 1 ? "" : "S")} SPAWNED IN ROOM";
    }

    // One of each kind in a row in front of the player.
    private void SpawnConsumables()
    {
        Vector3 ahead = player.position + player.forward * 2f;
        ahead.y = 0f;
        ConsumableType[] all = ConsumableInfo.All;

        for (int i = 0; i < all.Length; i++)
            floor.SpawnConsumable(all[i], 1, ahead + player.right * ((i - (all.Length - 1) / 2f) * 1.1f));
    }

    private void SpawnMeleeUpgrades()
    {
        Vector3 ahead = player.position + player.forward * 2f;
        ahead.y = 0f;
        floor.SpawnAllMeleeUpgrades(ahead, player.right);
    }

    private void SpawnMaterials()
    {
        Vector3 ahead = player.position + player.forward * 2.5f;
        ahead.y = 0f;
        floor.SpawnAllMaterials(ahead, player.right);
    }

    // Further ahead, so they don't spawn on top of the player.
    private void SpawnEnemyKinds()
    {
        Vector3 ahead = player.position + player.forward * 4f;
        ahead.y = 0f;
        floor.SpawnEnemyKinds(ahead, player.right);
    }

    private void GiveConsumables()
    {
        Backpack backpack = Backpack.For(player.gameObject);

        foreach (ConsumableType type in ConsumableInfo.All)
            backpack.AddConsumable(type, ConsumableInfo.StackSize(type) - backpack.ConsumableCount(type), true);
    }

    private void SpawnAttachments()
    {
        Vector3 ahead = player.position + player.forward * 2f;
        ahead.y = 0f;
        floor.SpawnAllAttachments(ahead, player.right);
    }

    // Ignores god mode and the invulnerability window, so healing can be tested. Never takes
    // the last segment.
    private void HurtPlayer()
    {
        PlayerHealth health = Health;

        if (health == null || health.Health <= 1)
            return;

        bool god = health.GodMode;
        health.GodMode = false;
        health.ForceDamage(1);
        health.GodMode = god;
    }

    private void RevealMap()
    {
        foreach (Room room in floor.Rooms)
            room.MarkVisited();
    }

    private void SkipFloor()
    {
        floor.UseExit(floor.IsSupplyRoom ? FloorExit.Kind.Extraction : FloorExit.Kind.NextFloor);
    }

    private void RegenerateFloor()
    {
        floor.Generate();

        if (cameraFollow != null)
            cameraFollow.SnapToTarget();
    }

    private void ToggleGodMode()
    {
        if (Health != null)
            Health.GodMode = !Health.GodMode;
    }

    private void Refill()
    {
        if (Health != null)
            Health.RestoreFullHealth();

        Gun gun = player.GetComponent<PlayerShooting>().EquippedGun;

        if (gun != null)
        {
            gun.SetCurrentAmmo(gun.GetMagazineSize());
            AmmoInventory ammo = player.GetComponent<AmmoInventory>();

            if (ammo != null)
                ammo.Add(gun.AmmoType, Backpack.StackSize(gun.AmmoType) * 3);
        }
    }

    private void GiveAllAmmo()
    {
        AmmoInventory ammo = player.GetComponent<AmmoInventory>();

        if (ammo != null)
            ammo.FillAll();
    }

    // Drops each debug weapon in front of the player, in rows of three, with a full magazine.
    private void SpawnWeapons()
    {
        if (gunPickupPrefab == null || debugWeapons == null)
            return;

        const int perRow = 3;

        for (int i = 0; i < debugWeapons.Length; i++)
        {
            Gun gun = debugWeapons[i] != null ? debugWeapons[i].GetComponent<Gun>() : null;

            if (gun == null)
                continue;

            int row = i / perRow;
            int column = i % perRow;
            Vector3 position = player.position + player.forward * (1.8f + row * 1.4f) + player.right * ((column - (perRow - 1) / 2f) * 1.8f);
            position.y = 0f;

            GameObject pickup = Instantiate(gunPickupPrefab, position, Quaternion.identity);
            pickup.GetComponent<GunPickup>().SetWeaponPrefab(debugWeapons[i], gun.GetMagazineSize());
        }
    }

    private void MovePlayer(Vector3 position)
    {
        Rigidbody body = player.GetComponent<Rigidbody>();

        if (body != null)
        {
            body.position = position;
            body.linearVelocity = Vector3.zero;
        }

        player.position = position;

        if (cameraFollow != null)
            cameraFollow.SnapToTarget();
    }
}
