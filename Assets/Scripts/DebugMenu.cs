using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// F5 debug menu for testing: teleport between key rooms, kill or spawn enemies, reveal the map,
// skip or rebuild the floor, god mode, refills and item spawns. Pauses the game while open. Only
// available in the editor and development builds. Options are grouped under headers in two
// columns; the buttons are built at runtime, so adding an option is one line in BuildOptions,
// under the Section it belongs to.
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
    private const float HeaderHeight = 30f;

    private class Option
    {
        public Func<string> Label;
        public Action Run;
        public bool ClosesMenu;
        public CrtButton Button;
        public int Column;
        public string Section;
    }

    // In column order (all of the first column, then the second), which is how CrtMenu numbers
    // its buttons.
    private readonly List<Option> options = new List<Option>();
    private int currentColumn;
    private string currentSection;
    private GameObject panel;
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

        Option option = options[chosen];

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
        status.text = floor != null ? $"FLOOR {floor.FloorNumber} // {(floor.CurrentRoom != null ? floor.CurrentRoom.name : "NO ROOM")}" : "";
        RefreshLabels();
        panel.SetActive(true);
        menu.Open();
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
        Add("REGENERATE FLOOR", RegenerateFloor, true);

        Section(0, "ENEMIES");
        Add("KILL ENEMIES IN ROOM", KillInRoom, false);
        Add("KILL ALL ENEMIES ON FLOOR", KillOnFloor, false);
        Add("SPAWN ONE OF EACH ZOMBIE", SpawnEnemyKinds, true);
        Add(() => "SHOW DETECTION RADII: " + (NoiseDebug.Enabled ? "ON" : "OFF"), () => NoiseDebug.SetEnabled(!NoiseDebug.Enabled), false);

        Section(1, "PLAYER");
        Add(() => "GOD MODE: " + (Health != null && Health.GodMode ? "ON" : "OFF"), ToggleGodMode, false);
        Add("REFILL HEALTH + AMMO", Refill, false);
        Add("HURT PLAYER (1)", HurtPlayer, false);
        Add("GIVE MAX AMMO (ALL TYPES)", GiveAllAmmo, false);
        Add("GIVE CONSUMABLES (1 STACK EACH)", GiveConsumables, false);

        Section(1, "SPAWN ITEMS");
        Add("SPAWN WEAPONS", SpawnWeapons, true);
        Add("SPAWN ALL ATTACHMENTS", SpawnAttachments, true);
        Add("SPAWN ALL CONSUMABLES", SpawnConsumables, true);
        Add("SPAWN ALL MELEE UPGRADES", SpawnMeleeUpgrades, true);
        Add("FILL LOOT POINTS IN ROOM", FillLootPoints, false);
    }

    // Starts a group of options under a header. Sections must be added column by column.
    private void Section(int column, string header)
    {
        currentColumn = Mathf.Clamp(column, 0, ColumnCount - 1);
        currentSection = header;
    }

    private void Add(string label, Action run, bool closesMenu)
    {
        Add(() => label, run, closesMenu);
    }

    private void Add(Func<string> label, Action run, bool closesMenu)
    {
        options.Add(new Option
        {
            Label = label,
            Run = run,
            ClosesMenu = closesMenu,
            Column = currentColumn,
            Section = currentSection
        });
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
        floor.UseExit(floor.FloorNumber < floor.TotalFloors ? FloorExit.Kind.NextFloor : FloorExit.Kind.Trophy);
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

    // Drops each debug weapon in a row in front of the player, with a full magazine.
    private void SpawnWeapons()
    {
        if (gunPickupPrefab == null || debugWeapons == null)
            return;

        for (int i = 0; i < debugWeapons.Length; i++)
        {
            Gun gun = debugWeapons[i] != null ? debugWeapons[i].GetComponent<Gun>() : null;

            if (gun == null)
                continue;

            Vector3 position = player.position + player.forward * 2f + player.right * ((i - (debugWeapons.Length - 1) / 2f) * 2.5f);
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
        // As tall as the taller column: its headers and buttons.
        float[] columnHeights = new float[ColumnCount];
        string[] lastSection = new string[ColumnCount];

        foreach (Option option in options)
        {
            if (option.Section != lastSection[option.Column])
            {
                columnHeights[option.Column] += HeaderHeight;
                lastSection[option.Column] = option.Section;
            }

            columnHeights[option.Column] += buttonSize.y + 6f;
        }

        float height = 90f + Mathf.Max(columnHeights);
        float width = ColumnCount * buttonSize.x + (ColumnCount - 1) * ColumnGap + 60f;

        panel = new GameObject("Panel", typeof(RectTransform));
        panel.layer = gameObject.layer;
        panel.transform.SetParent(transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(width, height);

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.06f, 0.03f, 0f, 0.95f);
        background.raycastTarget = false;

        float top = height / 2f - 20f;
        MakeText("Title", "> DEBUG", 22, new Vector2(0f, top), amber, null, width - 40f);
        status = MakeText("Status", "", 11, new Vector2(0f, top - 26f), new Color(amber.r, amber.g, amber.b, 0.6f), null, width - 40f);
        top -= 56f;

        Color headerColour = new Color(amber.r, amber.g, amber.b, 0.55f);
        List<CrtButton>[] buttons = new List<CrtButton>[ColumnCount];
        float[] y = new float[ColumnCount];
        string[] section = new string[ColumnCount];

        for (int c = 0; c < ColumnCount; c++)
        {
            buttons[c] = new List<CrtButton>();
            y[c] = top;
        }

        foreach (Option option in options)
        {
            int c = option.Column;
            float x = (c - (ColumnCount - 1) / 2f) * (buttonSize.x + ColumnGap);

            // A header over the first option of each group, lined up with the buttons.
            if (option.Section != section[c])
            {
                section[c] = option.Section;
                TMP_Text header = MakeText("Header " + option.Section, "// " + option.Section, 11, new Vector2(x, y[c] - 4f), headerColour, null, buttonSize.x);
                header.alignment = TextAlignmentOptions.MidlineLeft;
                y[c] -= HeaderHeight;
            }

            option.Button = MakeButton(option.Label(), new Vector2(x, y[c]));
            buttons[c].Add(option.Button);
            y[c] -= buttonSize.y + 6f;
        }

        CrtButton[][] columns = new CrtButton[ColumnCount][];

        for (int c = 0; c < ColumnCount; c++)
            columns[c] = buttons[c].ToArray();

        menu = new CrtMenu(columns, 0, 0);
    }

    private void RefreshLabels()
    {
        foreach (Option option in options)
            option.Button.Label.text = option.Label();
    }

    private CrtButton MakeButton(string text, Vector2 position)
    {
        GameObject go = new GameObject(text, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(panel.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = buttonSize;

        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;

        TMP_Text label = MakeText("Label", text, 13, Vector2.zero, amber, rect);
        label.rectTransform.sizeDelta = buttonSize;

        CrtButton button = go.AddComponent<CrtButton>();
        button.Setup(image, label,
            new Color(amber.r, amber.g, amber.b, 0.1f),
            new Color(amber.r, amber.g, amber.b, 0.4f),
            new Color(amber.r, amber.g, amber.b, 0.85f),
            Color.white);
        return button;
    }

    private TMP_Text MakeText(string name, string text, float size, Vector2 position, Color colour, RectTransform parent = null, float width = 0f)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent != null ? parent : panel.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
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
