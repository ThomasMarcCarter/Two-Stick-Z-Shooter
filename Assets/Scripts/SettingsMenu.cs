using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The settings screen, opened from the pause and main menus, with two tabs along the top.
// CONTROLS: one row per action with a keyboard/mouse column and a controller column; pick a cell
// and press the new key or button (moving and aiming on the controller stay on the sticks).
// Bindings are saved by GameInput. AUDIO / VIDEO: volumes, display mode, resolution, frame rate
// and CRT strength (GameSettings); click or press to step a value on, right-click or left/right
// to step it either way. Rows are built at runtime, so adding one is a line in BuildControls or
// BuildOptions.
public class SettingsMenu : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform rowContainer;
    [SerializeField] private TMP_Text status;
    [SerializeField] private TMP_FontAsset font;

    [Header("Look")]
    [SerializeField] private Color phosphor = new Color(0.35f, 1f, 0.55f, 1f);
    [SerializeField] private float rowHeight = 30f;

    public bool IsOpen { get; private set; }

    // Waiting for the player to press the new key or button.
    public bool IsRebinding => rebinding != null || pendingRebind != null;

    // One rebindable, fixed, option, tab or plain button cell.
    private class Cell
    {
        public CrtButton Button;      // null for fixed cells
        public TMP_Text Text;
        public string FixedText;      // what a fixed cell shows, e.g. "L-STICK"
        public InputAction Action;
        public int BindingIndex = -1;
        public bool Gamepad;
        public int Tab = -1;          // a tab button: the page it opens
        public Func<string> Value;    // an option: its current value...
        public Action<int> Step;      // ...and how to move it on (+1) or back (-1)
    }

    private const int ControlsPage = 0;
    private const int OptionsPage = 1;

    private readonly List<Cell[]>[] pages = { new List<Cell[]>(), new List<Cell[]>() };
    private readonly RectTransform[] pageRoots = new RectTransform[2];
    private int page;
    private RectTransform building;

    private List<Cell[]> grid => pages[page];

    private int row = -1;
    private int column;
    private float nextMove;
    private bool held;

    private InputActionRebindingExtensions.RebindingOperation rebinding;
    private Cell pendingRebind;
    private int ignoreCancelUntilFrame;

    private void Awake()
    {
        BuildRows();
        panel.SetActive(false);
    }

    public void Open()
    {
        IsOpen = true;
        panel.SetActive(true);
        ShowPage(ControlsPage);
        held = true;
        nextMove = Time.unscaledTime + 0.35f;
    }

    public void Close()
    {
        if (rebinding != null)
            rebinding.Cancel();

        pendingRebind = null;
        IsOpen = false;
        panel.SetActive(false);
    }

    private void ShowPage(int index)
    {
        page = index;

        for (int i = 0; i < pageRoots.Length; i++)
            pageRoots[i].gameObject.SetActive(i == page);

        status.text = page == OptionsPage
            ? (GameInput.UsingGamepad ? "> LEFT / RIGHT TO CHANGE A SETTING" : "> CLICK TO CHANGE // RIGHT-CLICK OR LEFT / RIGHT TO GO BACK")
            : "";
        row = GameInput.UsingGamepad ? 0 : -1;
        column = page;
        RefreshLabels();
        Highlight();
    }

    // Call every frame while open. Returns true when the player backs out.
    public bool Tick()
    {
        if (rebinding != null)
            return false;

        if (pendingRebind != null)
        {
            // Start only once the button that picked the cell is let go, or it gets bound itself.
            if (!GameInput.Submit.IsPressed() && (Mouse.current == null || !Mouse.current.leftButton.isPressed))
                BeginRebind(pendingRebind);

            return false;
        }

        if (GameInput.Cancel.WasPressedThisFrame() && Time.frameCount > ignoreCancelUntilFrame)
            return true;

        Cell hovered = HandlePointer();
        HandleNavigation();
        Highlight();

        // Right-click steps an option back.
        if (hovered != null && hovered.Step != null && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            hovered.Step(-1);
            RefreshLabels();
            return false;
        }

        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && hovered != null;
        Cell chosen = clicked ? hovered : GameInput.Submit.WasPressedThisFrame() ? Selected() : null;

        if (chosen == null)
            return false;

        if (chosen.Tab >= 0)
        {
            ShowPage(chosen.Tab);
        }
        else if (chosen.Step != null)
        {
            chosen.Step(1);
            RefreshLabels();
        }
        else if (chosen == controlsResetCell)
        {
            GameInput.ResetBindings();
            RefreshLabels();
            status.text = "> BINDINGS RESET TO DEFAULTS";
        }
        else if (chosen == optionsResetCell)
        {
            GameSettings.ResetToDefaults();
            RefreshLabels();
            status.text = "> AUDIO / VIDEO RESET TO DEFAULTS";
        }
        else if (chosen.Tab < 0 && chosen.Action == null)
        {
            return true;   // a BACK button
        }
        else if (chosen.Action != null)
        {
            pendingRebind = chosen;
            chosen.Button.Label.text = chosen.Gamepad ? "PRESS BUTTON..." : "PRESS KEY...";
            status.text = chosen.Gamepad ? "> WAITING FOR CONTROLLER INPUT" : "> WAITING FOR KEY OR MOUSE BUTTON // [ESC] CANCEL";
        }

        return false;
    }

    // ---------- Rows ----------

    private Cell controlsResetCell;
    private Cell optionsResetCell;

    // Tabs + header + action rows + gap + two buttons, for fitting everything into the container.
    private const float LayoutRows = 1 + 1 + 23 + 0.5f + 2;

    private void BuildRows()
    {
        // Shrink the rows if they'd otherwise run past the bottom of the panel.
        rowHeight = Mathf.Min(rowHeight, rowContainer.rect.height / LayoutRows - 4f);

        for (int i = 0; i < pageRoots.Length; i++)
        {
            GameObject go = new GameObject(i == ControlsPage ? "Controls Page" : "Audio Video Page", typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(rowContainer, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            pageRoots[i] = rect;
        }

        page = ControlsPage;
        building = pageRoots[ControlsPage];
        BuildControls();

        page = OptionsPage;
        building = pageRoots[OptionsPage];
        BuildOptions();

        page = ControlsPage;
    }

    private void BuildControls()
    {
        float y = 0f;
        AddTabs(ref y);
        AddHeader(ref y);

        AddRow(ref y, "MOVE UP", Part(GameInput.Move, "Up"), Fixed("L-STICK"));
        AddRow(ref y, "MOVE DOWN", Part(GameInput.Move, "Down"), Fixed("L-STICK"));
        AddRow(ref y, "MOVE LEFT", Part(GameInput.Move, "Left"), Fixed("L-STICK"));
        AddRow(ref y, "MOVE RIGHT", Part(GameInput.Move, "Right"), Fixed("L-STICK"));
        AddRow(ref y, "SPRINT", Binding(GameInput.Sprint, false), Binding(GameInput.Sprint, true));
        AddRow(ref y, "SNEAK (TOGGLE)", Binding(GameInput.Sneak, false), Binding(GameInput.Sneak, true));
        AddRow(ref y, "AIM", Fixed("MOUSE"), Fixed("R-STICK"));
        AddRow(ref y, "FIRE", Binding(GameInput.Fire, false), Binding(GameInput.Fire, true));
        AddRow(ref y, "RELOAD", Binding(GameInput.Reload, false), Binding(GameInput.Reload, true));
        AddRow(ref y, "MELEE", Binding(GameInput.Melee, false), Binding(GameInput.Melee, true));
        AddRow(ref y, "SWAP / PICK UP", Binding(GameInput.Interact, false), Binding(GameInput.Interact, true));
        AddRow(ref y, "MAIN WEAPON", Binding(GameInput.WeaponMain, false), Binding(GameInput.WeaponMain, true));
        AddRow(ref y, "SIDEARM", Binding(GameInput.WeaponSidearm, false), Binding(GameInput.WeaponSidearm, true));
        AddRow(ref y, "INVENTORY", Binding(GameInput.Inventory, false), Binding(GameInput.Inventory, true));
        AddRow(ref y, "QUICK USE (SELECTED)", Binding(GameInput.QuickUse, false), Binding(GameInput.QuickUse, true));
        AddRow(ref y, "QUICK SLOT LEFT", Fixed("-"), Binding(GameInput.QuickPrevious, true));
        AddRow(ref y, "QUICK SLOT RIGHT", Fixed("-"), Binding(GameInput.QuickNext, true));

        for (int i = 0; i < GameInput.QuickSlots.Length; i++)
            AddRow(ref y, $"QUICK SLOT {i + 1}", Binding(GameInput.QuickSlots[i], false), Fixed("-"));
        AddRow(ref y, "MAP", Binding(GameInput.Map, false), Binding(GameInput.Map, true));
        AddRow(ref y, "PAUSE", Binding(GameInput.Pause, false), Binding(GameInput.Pause, true));

        y -= rowHeight * 0.5f;
        controlsResetCell = AddWideButton(ref y, "[ RESET TO DEFAULTS ]");
        AddWideButton(ref y, "[ BACK ]");
    }

    private void BuildOptions()
    {
        float y = 0f;
        AddTabs(ref y);
        y -= rowHeight * 0.5f;

        AddSection(ref y, "AUDIO");
        AddOption(ref y, "MASTER VOLUME", () => GameSettings.Percent(GameSettings.MasterVolume), GameSettings.StepMaster);
        AddOption(ref y, "EFFECTS VOLUME", () => GameSettings.Percent(GameSettings.EffectsVolume), GameSettings.StepEffects);
        AddOption(ref y, "MUSIC VOLUME", () => GameSettings.Percent(GameSettings.MusicVolume), GameSettings.StepMusic);

        y -= rowHeight * 0.5f;
        AddSection(ref y, "VIDEO");
        AddOption(ref y, "DISPLAY MODE", () => GameSettings.DisplayModeName(GameSettings.DisplayMode), GameSettings.StepDisplayMode);
        AddOption(ref y, "RESOLUTION", () => $"{GameSettings.Resolution.x} x {GameSettings.Resolution.y}", GameSettings.StepResolution);
        AddOption(ref y, "VSYNC", () => GameSettings.VSync ? "ON" : "OFF", _ => GameSettings.ToggleVSync());
        AddOption(ref y, "FRAME RATE CAP", () => GameSettings.FrameCapName, d => { if (!GameSettings.VSync) GameSettings.StepFrameCap(d); });
        AddOption(ref y, "CRT EFFECT", () => GameSettings.Crt.ToString().ToUpperInvariant(), GameSettings.StepCrt);

        y -= rowHeight * 0.5f;
        optionsResetCell = AddWideButton(ref y, "[ RESET TO DEFAULTS ]");
        AddWideButton(ref y, "[ BACK ]");
    }

    private void AddTabs(ref float y)
    {
        Cell controls = new Cell { Tab = ControlsPage };
        controls.Button = MakeButton("Tab Controls", "CONTROLS", new Vector2(0f, y), new Vector2(325f, rowHeight));
        Cell options = new Cell { Tab = OptionsPage };
        options.Button = MakeButton("Tab Audio Video", "AUDIO / VIDEO", new Vector2(335f, y), new Vector2(325f, rowHeight));
        grid.Add(new[] { controls, options });
        y -= rowHeight + 10f;
    }

    private void AddSection(ref float y, string title)
    {
        Color dim = new Color(phosphor.r, phosphor.g, phosphor.b, 0.55f);
        MakeText(title + " Section", "> " + title, new Vector2(0f, y), new Vector2(240f, rowHeight), dim, TextAlignmentOptions.Left);
        y -= rowHeight + 4f;
    }

    private void AddOption(ref float y, string name, Func<string> value, Action<int> step)
    {
        MakeText(name + " Label", name, new Vector2(0f, y), new Vector2(240f, rowHeight), phosphor, TextAlignmentOptions.Left);
        Cell cell = new Cell { Value = value, Step = step };
        cell.Button = MakeButton(name + " Value", "", new Vector2(250f, y), new Vector2(410f, rowHeight));
        grid.Add(new[] { cell });
        y -= rowHeight + 4f;
    }

    private Cell Part(InputAction action, string part)
    {
        return new Cell { Action = action, BindingIndex = GameInput.FindCompositePart(action, part), Gamepad = false };
    }

    private Cell Binding(InputAction action, bool gamepad)
    {
        string group = gamepad ? GameInput.GamepadGroup : GameInput.KeyboardMouseGroup;
        return new Cell { Action = action, BindingIndex = GameInput.FindBinding(action, group), Gamepad = gamepad };
    }

    private static Cell Fixed(string text)
    {
        return new Cell { FixedText = text };
    }

    private void AddHeader(ref float y)
    {
        Color dim = new Color(phosphor.r, phosphor.g, phosphor.b, 0.55f);
        MakeText("Header Action", "ACTION", new Vector2(0f, y), new Vector2(240f, rowHeight), dim, TextAlignmentOptions.Left);
        MakeText("Header Keyboard", "KEYBOARD / MOUSE", new Vector2(250f, y), new Vector2(200f, rowHeight), dim, TextAlignmentOptions.Center);
        MakeText("Header Controller", "CONTROLLER", new Vector2(460f, y), new Vector2(200f, rowHeight), dim, TextAlignmentOptions.Center);
        y -= rowHeight + 4f;
    }

    private void AddRow(ref float y, string name, Cell keyboard, Cell controller)
    {
        MakeText(name + " Label", name, new Vector2(0f, y), new Vector2(240f, rowHeight), phosphor, TextAlignmentOptions.Left);
        BuildCell(keyboard, name + " Keyboard", new Vector2(250f, y));
        BuildCell(controller, name + " Controller", new Vector2(460f, y));
        grid.Add(new[] { keyboard, controller });
        y -= rowHeight + 4f;
    }

    private Cell AddWideButton(ref float y, string text)
    {
        Cell cell = new Cell();
        cell.Button = MakeButton(text, text, new Vector2(250f, y), new Vector2(410f, rowHeight));
        grid.Add(new[] { cell });
        y -= rowHeight + 4f;
        return cell;
    }

    private void BuildCell(Cell cell, string name, Vector2 position)
    {
        Vector2 size = new Vector2(200f, rowHeight);

        if (cell.Action == null)
        {
            cell.Text = MakeText(name, cell.FixedText, position, size, new Color(phosphor.r, phosphor.g, phosphor.b, 0.35f), TextAlignmentOptions.Center);
            return;
        }

        cell.Button = MakeButton(name, "", position, size);
    }

    private CrtButton MakeButton(string name, string text, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(building, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;

        TMP_Text label = MakeText("Label", text, Vector2.zero, size, phosphor, TextAlignmentOptions.Center, rect);

        CrtButton button = go.AddComponent<CrtButton>();
        button.Setup(image, label,
            new Color(phosphor.r, phosphor.g, phosphor.b, 0.12f),
            new Color(phosphor.r, phosphor.g, phosphor.b, 0.4f),
            new Color(phosphor.r, phosphor.g, phosphor.b, 0.85f),
            Color.white);
        return button;
    }

    private TMP_Text MakeText(string name, string text, Vector2 position, Vector2 size, Color colour,
        TextAlignmentOptions alignment, RectTransform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent != null ? parent : building, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = 14;
        label.color = colour;
        label.characterSpacing = 4;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    private void RefreshLabels()
    {
        foreach (List<Cell[]> rows in pages)
        {
            foreach (Cell[] cells in rows)
            {
                foreach (Cell cell in cells)
                {
                    if (cell.Action != null && cell.BindingIndex >= 0)
                        cell.Button.Label.text = GameInput.BindingLabel(cell.Action, cell.BindingIndex);
                    else if (cell.Value != null)
                        cell.Button.Label.text = $"<    {cell.Value()}    >";
                }
            }
        }
    }

    // ---------- Rebinding ----------

    private void BeginRebind(Cell cell)
    {
        pendingRebind = null;

        rebinding = GameInput.StartRebind(cell.Action, cell.BindingIndex, () =>
        {
            rebinding = null;
            ignoreCancelUntilFrame = Time.frameCount + 1;   // the Esc that cancelled shouldn't also close the menu
            status.text = "";
            RefreshLabels();
        });
    }

    // ---------- Selection ----------

    private Cell HandlePointer()
    {
        if (GameInput.UsingGamepad)
            return null;

        Cell hovered = null;

        for (int r = 0; r < grid.Count; r++)
        {
            for (int c = 0; c < grid[r].Length; c++)
            {
                Cell cell = grid[r][c];

                if (cell.Button != null && cell.Button.IsPointerOver())
                {
                    hovered = cell;

                    if (GameInput.PointerMovedThisFrame)
                    {
                        row = r;
                        column = c;
                    }
                }
            }
        }

        if (hovered == null && GameInput.PointerMovedThisFrame)
            row = -1;

        return hovered;
    }

    private void HandleNavigation()
    {
        Vector2 input = GameInput.Navigate.ReadValue<Vector2>();

        if (input.magnitude < 0.5f)
        {
            held = false;

            if (GameInput.UsingGamepad && row < 0)
                row = 0;

            return;
        }

        float now = Time.unscaledTime;

        if (held && now < nextMove)
            return;

        nextMove = now + (held ? 0.15f : 0.35f);
        held = true;

        if (row < 0)
        {
            row = 0;
            return;
        }

        if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
        {
            row = (row + (input.y > 0f ? -1 : 1) + grid.Count) % grid.Count;
        }
        else
        {
            Cell current = grid[row][Mathf.Min(column, grid[row].Length - 1)];

            // Left / right on an option changes it rather than moving across.
            if (current.Step != null)
            {
                current.Step(input.x > 0f ? 1 : -1);
                RefreshLabels();
                return;
            }

            column = Mathf.Clamp(column + (input.x > 0f ? 1 : -1), 0, 1);
        }

        // Land on a cell that can be picked; fixed cells and single-button rows fall back.
        column = Mathf.Min(column, grid[row].Length - 1);

        if (grid[row][column].Button == null)
            column = grid[row].Length > 1 && grid[row][1 - column].Button != null ? 1 - column : column;
    }

    private Cell Selected()
    {
        if (row < 0 || row >= grid.Count)
            return null;

        Cell cell = grid[row][Mathf.Min(column, grid[row].Length - 1)];
        return cell.Button != null ? cell : null;
    }

    private void Highlight()
    {
        Cell selected = Selected();

        foreach (Cell[] cells in grid)
        {
            foreach (Cell cell in cells)
            {
                // The open page's tab stays lit, unless the cursor is on the tab row choosing another.
                bool openTab = cell.Tab == page && row != 0;

                if (cell.Button != null)
                    cell.Button.SetHighlighted(cell == selected || openTab);
            }
        }
    }
}
