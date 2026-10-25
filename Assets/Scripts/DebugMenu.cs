using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// F5 debug menu for testing: teleport between key rooms, kill or spawn enemies, reveal the map,
// skip or rebuild the floor, god mode, refills and item spawns. Pauses the game while open. Only
// available in the editor and development builds. Options are grouped into fold-out sections in
// two columns: each section's header is a button that shows or hides its options, and opening one
// folds away the others in its column so the panel always fits on screen. The panel grows and
// shrinks from the top to fit what's showing. The buttons are built at runtime, so adding an
// option is one line in BuildOptions, under the Section it belongs to.
public class DebugMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [Header("References")]
    [SerializeField] private FloorGenerator floor;
    [SerializeField] private Transform player;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private TMP_FontAsset font;

    [Header("Weapons")]
    [SerializeField] private GameObject gunPickupPrefab;
    [Tooltip("Dropped by the SPAWN WEAPONS option.")]
    [SerializeField] private GameObject[] debugWeapons;

    [Header("Look")]
    [SerializeField] private Color amber = new Color(1f, 0.7f, 0.2f, 1f);
    [SerializeField] private Vector2 buttonSize = new Vector2(300f, 30f);

    private const int ColumnCount = 2;
    private const float ColumnGap = 30f;
    private const float ButtonGap = 6f;
    private const float SectionGap = 8f;
    private const float TitleSpace = 70f;
    private const float PanelTop = 30f;

    private class Option
    {
        public Func<string> Label;
        public Action Run;
        public bool ClosesMenu;
        public CrtButton Button;
    }

    private class FoldSection
    {
        public string Name;
        public int Column;
        public bool Open;
        public CrtButton Header;
        public readonly List<Option> Options = new List<Option>();
    }

    // In the order they were added (all of the first column, then the second).
    private readonly List<FoldSection> sections = new List<FoldSection>();
    private readonly List<Option> options = new List<Option>();
    // The buttons on show, in CrtMenu's numbering (column by column): a section header (Option
    // null) or one of its options.
    private readonly List<(FoldSection Section, Option Option)> visible = new List<(FoldSection, Option)>();
    private FoldSection currentSection;
    private GameObject panel;
    private RectTransform panelRect;
    private TMP_Text status;
    private CrtMenu menu;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        IsOpen = false;

        if (!Debug.isDebugBuild)
        {
            enabled = false;
            return;
        }

        BuildOptions();
        BuildPanel();
        panel.SetActive(false);
    }

    private void Update()
    {
        bool toggle = Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame;

        if (!IsOpen)
        {
            if (toggle && !GameFlow.InputBlocked)
                Open();

            return;
        }

        if (toggle || GameInput.Cancel.WasPressedThisFrame())
        {
            Close();
            return;
        }

        int chosen = menu.Update();

        if (chosen < 0)
            return;

        Option option = visible[chosen].Option;

        if (option == null)
        {
            ToggleSection(visible[chosen].Section);
            return;
        }

        // Some options need the game running (teleports, floor changes), so close first.
        if (option.ClosesMenu)
            Close();

        option.Run();

        if (!option.ClosesMenu)
            RefreshLabels();
    }

    private void Open()
    {
        IsOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        status.text = floor != null ? $"{floor.LocationName} // {(floor.CurrentRoom != null ? floor.CurrentRoom.name : "NO ROOM")}" : "";
        RefreshLabels();
        panel.SetActive(true);
        Layout(null);
    }

    private void Close()
    {
        IsOpen = false;
        Time.timeScale = previousTimeScale;
        panel.SetActive(false);
    }

    // ---------- Options ----------

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

        Section(1, "PLAYER");
        Add(() => "GOD MODE: " + (Health != null && Health.GodMode ? "ON" : "OFF"), ToggleGodMode, false);
        Add("REFILL HEALTH + AMMO", Refill, false);
        Add("HURT PLAYER (1)", HurtPlayer, false);
        Add("GIVE MAX AMMO (ALL TYPES)", GiveAllAmmo, false);
        Add("GIVE CONSUMABLES (1 STACK EACH)", GiveConsumables, false);
        Add(() => $"GIVE 100 SCRAP ({MetaProgress.Scrap})", () => MetaProgress.AddScrap(100), false);
        Add("RESET META PROGRESS (ALL HIDEOUT)", MetaProgress.ResetAll, false);

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

    // ---------- UI ----------

    private void BuildPanel()
    {
        float width = ColumnCount * buttonSize.x + (ColumnCount - 1) * ColumnGap + 60f;

        // Hangs from the top of the screen, so folding sections only moves its bottom edge.
        panel = new GameObject("Panel", typeof(RectTransform));
        panel.layer = gameObject.layer;
        panel.transform.SetParent(transform, false);
        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -PanelTop);
        panelRect.sizeDelta = new Vector2(width, TitleSpace);

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.06f, 0.03f, 0f, 0.95f);
        background.raycastTarget = false;

        MakeText("Title", "> DEBUG", 22, new Vector2(0f, -20f), amber, null, width - 40f);
        status = MakeText("Status", "", 11, new Vector2(0f, -46f), new Color(amber.r, amber.g, amber.b, 0.6f), null, width - 40f);

        foreach (FoldSection section in sections)
        {
            section.Header = MakeButton(section.Name, Vector2.zero, true);

            foreach (Option option in section.Options)
                option.Button = MakeButton(option.Label(), Vector2.zero, false);
        }
    }

    // Opening a section folds away the others in its column.
    private void ToggleSection(FoldSection section)
    {
        bool open = !section.Open;

        foreach (FoldSection other in sections)
        {
            if (other.Column == section.Column)
                other.Open = false;
        }

        section.Open = open;
        Layout(section);
    }

    // Places the headers and the options of open sections, hides the rest, fits the panel to
    // them and rebuilds the menu over what's showing. focus: the header the controller highlight
    // starts on (null for the top of the first column).
    private void Layout(FoldSection focus)
    {
        visible.Clear();
        List<CrtButton>[] buttons = new List<CrtButton>[ColumnCount];
        float[] y = new float[ColumnCount];
        int focusColumn = 0;
        int focusRow = 0;

        for (int c = 0; c < ColumnCount; c++)
        {
            buttons[c] = new List<CrtButton>();
            y[c] = -TitleSpace - buttonSize.y / 2f;
        }

        // Sections are in column order, so this fills visible in CrtMenu's numbering.
        foreach (FoldSection section in sections)
        {
            int c = section.Column;
            float x = (c - (ColumnCount - 1) / 2f) * (buttonSize.x + ColumnGap);

            if (section == focus)
            {
                focusColumn = c;
                focusRow = buttons[c].Count;
            }

            section.Header.Label.text = (section.Open ? "[-] " : "[+] ") + section.Name + (section.Open ? "" : $" ({section.Options.Count})");
            Place(section.Header, x, ref y[c], buttons[c]);
            visible.Add((section, null));

            foreach (Option option in section.Options)
            {
                option.Button.gameObject.SetActive(section.Open);

                if (!section.Open)
                    continue;

                Place(option.Button, x, ref y[c], buttons[c]);
                visible.Add((section, option));
            }

            y[c] -= SectionGap;
        }

        float lowest = 0f;

        for (int c = 0; c < ColumnCount; c++)
            lowest = Mathf.Min(lowest, y[c]);

        panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, -lowest + buttonSize.y / 2f + 10f);

        CrtButton[][] columns = new CrtButton[ColumnCount][];

        for (int c = 0; c < ColumnCount; c++)
            columns[c] = buttons[c].ToArray();

        menu = new CrtMenu(columns, focusColumn, focusRow);
        menu.Open();
    }

    private static void Place(CrtButton button, float x, ref float y, List<CrtButton> column)
    {
        button.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
        column.Add(button);
        y -= button.GetComponent<RectTransform>().sizeDelta.y + ButtonGap;
    }

    private void RefreshLabels()
    {
        foreach (Option option in options)
            option.Button.Label.text = option.Label();
    }

    // header: a section's fold-out button, with its label on the left and a dimmer fill.
    private CrtButton MakeButton(string text, Vector2 position, bool header)
    {
        GameObject go = new GameObject(text, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(panel.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = buttonSize;

        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;

        TMP_Text label = MakeText("Label", text, header ? 12 : 13, Vector2.zero, amber, rect);
        label.rectTransform.sizeDelta = buttonSize;

        if (header)
        {
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = new Vector4(10f, 0f, 10f, 0f);
        }

        CrtButton button = go.AddComponent<CrtButton>();
        button.Setup(image, label,
            new Color(amber.r, amber.g, amber.b, header ? 0.04f : 0.1f),
            new Color(amber.r, amber.g, amber.b, 0.4f),
            new Color(amber.r, amber.g, amber.b, header ? 0.6f : 0.85f),
            Color.white);
        return button;
    }

    private TMP_Text MakeText(string name, string text, float size, Vector2 position, Color colour, RectTransform parent = null, float width = 0f)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent != null ? parent : panel.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();

        // Straight on the panel, positions are measured down from its top edge.
        if (parent == null)
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);

        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width > 0f ? width : buttonSize.x + 40f, 26f);

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.color = colour;
        label.characterSpacing = 4;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }
}
