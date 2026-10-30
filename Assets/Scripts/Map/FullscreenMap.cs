using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Fullscreen tactical map. Toggle with Map (M / View); Back (Esc / B) also closes. Pauses the
// game while open. Mouse: drag to pan. Controller: left stick pans, right stick moves the cursor.
// Either way, the room under the cursor is the one shown in the side panel; moving off the rooms
// keeps the last one. In the hideout, a station's icon under the cursor shows that station's
// upgrades there instead.
public class FullscreenMap : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    public event System.Action Closed;

    [Header("References")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TacticalMapView view;
    [SerializeField] private FloorGenerator floor;
    [SerializeField] private Transform player;
    [SerializeField] private TMP_Text roomInfoText;

    [Header("Input")]
    [Tooltip("Stick / WASD panning speed, in pixels per second.")]
    [SerializeField] private float panSpeed = 700f;
    [Tooltip("Right stick cursor speed, in pixels per second on a 720-pixel-tall screen (scaled for others).")]
    [SerializeField] private float cursorSpeed = 900f;

    private readonly Dictionary<Room, List<GunPickup>> items = new Dictionary<Room, List<GunPickup>>();
    private readonly Dictionary<Room, List<AmmoPickup>> ammoItems = new Dictionary<Room, List<AmmoPickup>>();
    private readonly Dictionary<Room, List<ConsumablePickup>> medkits = new Dictionary<Room, List<ConsumablePickup>>();
    private readonly Dictionary<Room, List<SupplyCrate>> crates = new Dictionary<Room, List<SupplyCrate>>();
    private readonly Dictionary<Room, List<PowerupCase>> powerups = new Dictionary<Room, List<PowerupCase>>();
    private float previousTimeScale = 1f;
    private bool dragging;
    private Vector2 lastPointer;
    private Vector2 padCursor;
    private Room shownRoom;
    private StationMarker shownStation;
    private Vector2 lastCursor;
    private bool hasLastCursor;

    private void Awake()
    {
        // Statics survive between Play sessions (domain reload is off), so reset it here.
        IsOpen = false;
        panel.SetActive(false);
    }

    private void OnDisable()
    {
        if (IsOpen)
            Close();
    }

    private void Update()
    {
        if (GameInput.Map.WasPressedThisFrame())
        {
            // No opening the map over a floor transition, the pause menu or the end screens.
            if (IsOpen)
                Close();
            else if (!GameFlow.InputBlocked)
                Open();
        }
        else if (IsOpen && (GameInput.Cancel.WasPressedThisFrame() || GameInput.Pause.WasPressedThisFrame()))
        {
            Close();
        }

        if (!IsOpen)
            return;

        HandleStick();

        if (GameInput.UsingGamepad)
            MovePadCursor();
        else
            HandleDrag();

        SelectRoomUnderCursor();
    }

    // The room under the cursor goes in the side panel. Off the rooms, the last one stays.
    // The whole path the cursor moved along since last frame is checked, not just where it ended
    // up, so a quick flick (or a slow frame) can't skip over a room: the last room crossed wins.
    private void SelectRoomUnderCursor()
    {
        bool inside = view.PointerToLocal(GameInput.Pointer, out Vector2 pointer);

        // Over a station's icon: its readout, until the cursor moves off it.
        StationMarker station = inside ? view.StationAt(pointer) : null;
        view.HoveredStation = station;

        if (station != null)
        {
            if (station != shownStation)
            {
                shownStation = station;
                roomInfoText.text = "<mspace=0.62em>" + station.Describe();
            }

            lastCursor = pointer;
            hasLastCursor = true;
            return;
        }

        if (shownStation != null)
        {
            shownStation = null;
            Select(shownRoom);
        }

        Vector2 from = hasLastCursor ? lastCursor : pointer;
        lastCursor = pointer;
        hasLastCursor = true;

        const float step = 4f;
        int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, pointer) / step));
        Room crossed = null;

        for (int i = 1; i <= samples; i++)
        {
            Vector2 point = Vector2.Lerp(from, pointer, i / (float)samples);

            if (!view.rectTransform.rect.Contains(point))
                continue;

            Room room = view.RoomAt(point);

            if (room != null)
                crossed = room;
        }

        if (crossed != null && crossed != shownRoom)
            Select(crossed);
    }

    // Right stick moves the controller cursor around the view, kept inside it.
    private void MovePadCursor()
    {
        Vector2 input = GameInput.Aim.ReadValue<Vector2>();

        // The cursor moves in screen pixels; scaled to the screen so it crosses the map in the
        // same time at any resolution (Cursor Speed is for a 720-pixel-tall screen).
        if (input.sqrMagnitude > 0.01f)
            padCursor += Vector2.ClampMagnitude(input, 1f) * cursorSpeed * (Screen.height / 720f) * Time.unscaledDeltaTime;

        Rect bounds = ViewOnScreen();
        padCursor.x = Mathf.Clamp(padCursor.x, bounds.xMin, bounds.xMax);
        padCursor.y = Mathf.Clamp(padCursor.y, bounds.yMin, bounds.yMax);
    }

    // Left stick or WASD pans the map; pushing right moves the view right, so the map slides left.
    private void HandleStick()
    {
        Vector2 input = GameInput.Move.ReadValue<Vector2>();

        if (input.sqrMagnitude > 0.01f)
            view.Pan -= Vector2.ClampMagnitude(input, 1f) * panSpeed * Time.unscaledDeltaTime;
    }

    // Where the controller cursor is while the map is open (fed to GameInput.Pointer).
    private Vector2? PadCursor()
    {
        return padCursor;
    }

    // The map view's area in screen space (the undistorted image, like GameInput.Pointer).
    private Rect ViewOnScreen()
    {
        Canvas canvas = view.canvas;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Rect rect = view.rectTransform.rect;

        Vector2 min = RectTransformUtility.WorldToScreenPoint(uiCamera, view.rectTransform.TransformPoint(rect.min));
        Vector2 max = RectTransformUtility.WorldToScreenPoint(uiCamera, view.rectTransform.TransformPoint(rect.max));
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    public void Open()
    {
        IsOpen = true;
        panel.SetActive(true);

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        RefreshItems();
        shownStation = null;
        view.HoveredStation = null;
        view.CentreOn(player.position);
        Select(floor.CurrentRoom);

        // The controller cursor starts in the middle of the view, over the player.
        padCursor = ViewOnScreen().center;
        hasLastCursor = false;
        GameInput.GamepadPointerOverride = PadCursor;
    }

    private void Close()
    {
        IsOpen = false;
        dragging = false;
        panel.SetActive(false);
        Time.timeScale = previousTimeScale;

        if (GameInput.GamepadPointerOverride == PadCursor)
            GameInput.GamepadPointerOverride = null;

        Closed?.Invoke();
    }

    // Mouse: drag anywhere in the view to pan.
    private void HandleDrag()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        bool inside = view.PointerToLocal(GameInput.Pointer, out Vector2 pointer);

        if (mouse.leftButton.wasPressedThisFrame && inside)
        {
            dragging = true;
            lastPointer = pointer;
        }

        if (!dragging)
            return;

        if (mouse.leftButton.isPressed)
        {
            view.Pan += pointer - lastPointer;
            lastPointer = pointer;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
            dragging = false;
    }

    // Explored rooms get highlighted; unexplored ones just report "no data".
    private void Select(Room room)
    {
        shownRoom = room;
        view.Selected = room != null && room.Visited ? room : null;
        // Monospaced so the columns line up like a terminal readout.
        roomInfoText.text = "<mspace=0.62em>" + DescribeRoom(room);
    }

    // Weapon, ammo and medkit pickups and supply crates, each assigned to the room it's in, so
    // dropped weapons and enemy ammo drops show up too.
    private void RefreshItems()
    {
        items.Clear();
        ammoItems.Clear();
        medkits.Clear();
        crates.Clear();
        powerups.Clear();

        foreach (GunPickup pickup in FindObjectsByType<GunPickup>())
            AddItem(items, pickup);

        foreach (AmmoPickup pickup in FindObjectsByType<AmmoPickup>())
            AddItem(ammoItems, pickup);

        foreach (ConsumablePickup pickup in FindObjectsByType<ConsumablePickup>())
            AddItem(medkits, pickup);

        foreach (SupplyCrate crate in FindObjectsByType<SupplyCrate>())
            AddItem(crates, crate);

        foreach (PowerupCase powerup in FindObjectsByType<PowerupCase>())
            AddItem(powerups, powerup);

        view.SetItemCounts(Counts(items), Counts(ammoItems), Counts(medkits));
    }

    private void AddItem<T>(Dictionary<Room, List<T>> byRoom, T pickup) where T : Component
    {
        if (!floor.TryGetRoom(pickup.transform.position, out Room room))
            return;

        if (!byRoom.TryGetValue(room, out List<T> list))
        {
            list = new List<T>();
            byRoom[room] = list;
        }

        list.Add(pickup);
    }

    private static Dictionary<Room, int> Counts<T>(Dictionary<Room, List<T>> byRoom)
    {
        Dictionary<Room, int> counts = new Dictionary<Room, int>();

        foreach (KeyValuePair<Room, List<T>> entry in byRoom)
            counts[entry.Key] = entry.Value.Count;

        return counts;
    }

    private string DescribeRoom(Room room)
    {
        string dim = "<alpha=#77>";

        if (room == null)
            return $"ROOM DATA\n\n{dim}SELECT AN EXPLORED\nSECTOR ON THE MAP";

        string sector = $"SECTOR {room.GridPosition.x},{room.GridPosition.y}";

        if (!room.Visited)
            return $"ROOM DATA\n\n{sector}\n\n{dim}NO DATA\nSECTOR NOT EXPLORED";

        string hostileColour = ColorUtility.ToHtmlStringRGB(view.Hostile);
        string status = MapSymbols.StateOf(room) == MapRoomState.Cleared ? "CLEARED"
            : room.IsLocked ? $"<color=#{hostileColour}>ENGAGED</color>"
            : $"<color=#{hostileColour}>HOSTILES REMAIN</color>";

        StringBuilder text = new StringBuilder();
        text.AppendLine("ROOM DATA");
        text.AppendLine();
        text.AppendLine(sector);
        text.AppendLine($"TYPE      {room.DisplayName ?? TypeName(room.Type)}");
        text.AppendLine($"STATUS    {status}");
        text.AppendLine($"HOSTILES  {room.Enemies.Count}");
        text.AppendLine();
        text.AppendLine("ITEMS");

        bool any = false;

        if (items.TryGetValue(room, out List<GunPickup> pickups))
        {
            foreach (GunPickup pickup in pickups)
            {
                text.AppendLine("> " + DescribePickup(pickup));
                any = true;
            }
        }

        if (ammoItems.TryGetValue(room, out List<AmmoPickup> ammo))
        {
            foreach (AmmoPickup pickup in ammo)
            {
                // Swatch in the ammo's own colour, like the inventory screen.
                string colour = ColorUtility.ToHtmlStringRGB(AmmoInfo.Colour(pickup.Type));
                text.AppendLine($"<color=#{colour}>|</color> {AmmoInfo.Name(pickup.Type)} x{pickup.Amount}{(pickup.IsCrate ? " (CRATE)" : "")}");
                any = true;
            }
        }

        if (medkits.TryGetValue(room, out List<ConsumablePickup> kits))
        {
            foreach (ConsumablePickup kit in kits)
            {
                string colour = ColorUtility.ToHtmlStringRGB(ConsumableInfo.Colour(kit.Type));
                text.AppendLine($"<color=#{colour}>+</color> {ConsumableInfo.Name(kit.Type)}{(kit.Amount > 1 ? " x" + kit.Amount : "")}");
                any = true;
            }
        }

        if (powerups.TryGetValue(room, out List<PowerupCase> roomPowerups) && roomPowerups.Count > 0)
        {
            text.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(UiColours.Phosphor)}>*</color> POWERUP CASE");
            any = true;
        }

        if (crates.TryGetValue(room, out List<SupplyCrate> roomCrates))
        {
            foreach (SupplyCrate crate in roomCrates)
            {
                text.AppendLine($"<color={UiColours.AmberHex}>#</color> SUPPLY CRATE ({(crate.Items.Count == 0 ? "EMPTY" : crate.Items.Count + " ITEMS")})");
                any = true;
            }
        }

        if (!any)
            text.AppendLine(dim + "NONE DETECTED");

        return text.ToString();
    }

    private static string DescribePickup(GunPickup pickup)
    {
        GameObject weapon = pickup.GetWeaponPrefab();

        if (weapon == null)
            return "UNKNOWN";

        Gun gun = weapon.GetComponent<Gun>();
        string ammo = gun != null ? $"  {pickup.GetStoredAmmo()}/{gun.GetMagazineSize()} RDS" : "";

        return Gun.DisplayName(weapon) + ammo;
    }

    private static string TypeName(RoomType type)
    {
        switch (type)
        {
            case RoomType.Start: return "SPAWN";
            case RoomType.Treasure: return "TREASURE";
            case RoomType.Boss: return "BOSS";
            default: return "COMBAT";
        }
    }
}
