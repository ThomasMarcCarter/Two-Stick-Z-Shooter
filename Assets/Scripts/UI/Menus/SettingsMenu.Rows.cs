using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The SettingsMenu's rows: the CONTROLS page (an action per row, keyboard and controller
// bindings side by side) and the options pages (audio sliders, video options), built in code.
public partial class SettingsMenu
{
    private Cell controlsResetCell;
    private Cell optionsResetCell;

    // Tabs + header + action rows + gap + two buttons, for fitting everything into the container.
    private const float LayoutRows = 1 + 1 + 24 + 0.5f + 2;

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
        AddRow(ref y, "DODGE ROLL", Binding(GameInput.Dodge, false), Binding(GameInput.Dodge, true));
        AddRow(ref y, "AIM", Fixed("MOUSE"), Fixed("R-STICK"));
        AddRow(ref y, "FIRE", Binding(GameInput.Fire, false), Binding(GameInput.Fire, true));
        AddRow(ref y, "RELOAD", Binding(GameInput.Reload, false), Binding(GameInput.Reload, true));
        AddRow(ref y, "MELEE", Binding(GameInput.Melee, false), Binding(GameInput.Melee, true));
        AddRow(ref y, "SWAP / PICK UP", Binding(GameInput.Interact, false), Binding(GameInput.Interact, true));
        AddRow(ref y, "MAIN WEAPON", Binding(GameInput.WeaponMain, false), Binding(GameInput.WeaponMain, true));
        AddRow(ref y, "SIDEARM", Binding(GameInput.WeaponSidearm, false), Binding(GameInput.WeaponSidearm, true));
        AddRow(ref y, "INVENTORY", Binding(GameInput.Inventory, false), Binding(GameInput.Inventory, true));
        AddRow(ref y, "QUICK USE (SELECTED)", Binding(GameInput.QuickUse, false), Binding(GameInput.QuickUse, true));
        AddRow(ref y, "QUICK SLOT LEFT", Fixed("WHEEL UP"), Binding(GameInput.QuickPrevious, true));
        AddRow(ref y, "QUICK SLOT RIGHT", Fixed("WHEEL DOWN"), Binding(GameInput.QuickNext, true));

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
        AddSlider(ref y, "MASTER VOLUME", () => GameSettings.MasterVolume, GameSettings.SetMasterVolume);
        AddSlider(ref y, "EFFECTS VOLUME", () => GameSettings.EffectsVolume, GameSettings.SetEffectsVolume);
        AddSlider(ref y, "MUSIC VOLUME", () => GameSettings.MusicVolume, GameSettings.SetMusicVolume);

        y -= rowHeight * 0.5f;
        AddSection(ref y, "VIDEO");
        // Window settings only where there's a desktop window (not on consoles).
        if (Platform.HasWindowSettings)
        {
            AddOption(ref y, "DISPLAY MODE", () => GameSettings.DisplayModeName(GameSettings.DisplayMode), GameSettings.StepDisplayMode);
            AddOption(ref y, "RESOLUTION", () => $"{GameSettings.Resolution.x} x {GameSettings.Resolution.y}", GameSettings.StepResolution);
            AddOption(ref y, "VSYNC", () => GameSettings.VSync ? "ON" : "OFF", _ => GameSettings.ToggleVSync());
            AddOption(ref y, "FRAME RATE CAP", () => GameSettings.FrameCapName, d => { if (!GameSettings.VSync) GameSettings.StepFrameCap(d); });
        }

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
        Color dim = UiColours.WithAlpha(UiColours.Phosphor, 0.55f);
        MakeText(title + " Section", "> " + title, new Vector2(0f, y), new Vector2(240f, rowHeight), dim, TextAlignmentOptions.Left);
        y -= rowHeight + 4f;
    }

    private void AddOption(ref float y, string name, Func<string> value, Action<int> step)
    {
        MakeText(name + " Label", name, new Vector2(0f, y), new Vector2(240f, rowHeight), UiColours.Phosphor, TextAlignmentOptions.Left);
        Cell cell = new Cell { Value = value, Step = step };
        cell.Left = MakeButton(name + " Left", "<", new Vector2(250f, y), new Vector2(40f, rowHeight));
        cell.Button = MakeButton(name + " Value", "", new Vector2(296f, y), new Vector2(318f, rowHeight));
        cell.Right = MakeButton(name + " Right", ">", new Vector2(620f, y), new Vector2(40f, rowHeight));
        grid.Add(new[] { cell });
        y -= rowHeight + 4f;
    }

    // A volume row: a track with a filled part and a handle, the percentage at the right end.
    private void AddSlider(ref float y, string name, Func<float> level, Action<float, bool> setLevel)
    {
        MakeText(name + " Label", name, new Vector2(0f, y), new Vector2(240f, rowHeight), UiColours.Phosphor, TextAlignmentOptions.Left);
        Cell cell = new Cell { Level = level, SetLevel = setLevel };
        cell.Step = d => setLevel(GameSettings.StepVolume(level(), d), true);
        cell.Button = MakeButton(name + " Slider", "", new Vector2(250f, y), new Vector2(410f, rowHeight));
        cell.Button.Label.alignment = TextAlignmentOptions.Right;

        RectTransform buttonRect = (RectTransform)cell.Button.transform;
        cell.Track = MakeImage(buttonRect, "Track", UiColours.WithAlpha(UiColours.Phosphor, 0.3f));
        cell.Track.anchorMin = new Vector2(0f, 0.5f);
        cell.Track.anchorMax = new Vector2(1f, 0.5f);
        cell.Track.offsetMin = new Vector2(16f, -2f);
        cell.Track.offsetMax = new Vector2(-76f, 2f);

        cell.Fill = MakeImage(cell.Track, "Fill", UiColours.WithAlpha(UiColours.Phosphor, 0.85f));
        cell.Fill.anchorMin = Vector2.zero;
        cell.Fill.offsetMin = cell.Fill.offsetMax = Vector2.zero;

        cell.Handle = MakeImage(cell.Track, "Handle", UiColours.Phosphor);
        cell.Handle.sizeDelta = new Vector2(8f, rowHeight - 10f);

        grid.Add(new[] { cell });
        y -= rowHeight + 4f;
    }

    private static RectTransform MakeImage(RectTransform parent, string name, Color colour)
    {
        RectTransform rect = CrtPanel.MakeRect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
        return rect;
    }

    // Sets a slider from where the pointer is along its track.
    private void DragTo(Cell cell, bool save)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(cell.Track, GameInput.Pointer, uiCamera, out Vector2 local))
            return;

        Rect track = cell.Track.rect;
        cell.SetLevel(Mathf.Round(Mathf.InverseLerp(track.xMin, track.xMax, local.x) * 100f) / 100f, save);
        RefreshLabels();
    }

    private void EndDrag()
    {
        if (dragging == null)
            return;

        dragging.SetLevel(dragging.Level(), true);
        dragging = null;
        RefreshLabels();
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
        Color dim = UiColours.WithAlpha(UiColours.Phosphor, 0.55f);
        MakeText("Header Action", "ACTION", new Vector2(0f, y), new Vector2(240f, rowHeight), dim, TextAlignmentOptions.Left);
        MakeText("Header Keyboard", "KEYBOARD / MOUSE", new Vector2(250f, y), new Vector2(200f, rowHeight), dim, TextAlignmentOptions.Center);
        MakeText("Header Controller", "CONTROLLER", new Vector2(460f, y), new Vector2(200f, rowHeight), dim, TextAlignmentOptions.Center);
        y -= rowHeight + 4f;
    }

    private void AddRow(ref float y, string name, Cell keyboard, Cell controller)
    {
        MakeText(name + " Label", name, new Vector2(0f, y), new Vector2(240f, rowHeight), UiColours.Phosphor, TextAlignmentOptions.Left);
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
            cell.Text = MakeText(name, cell.FixedText, position, size, UiColours.WithAlpha(UiColours.Phosphor, 0.35f), TextAlignmentOptions.Center);
            return;
        }

        cell.Button = MakeButton(name, "", position, size);
    }

    private CrtButton MakeButton(string name, string text, Vector2 position, Vector2 size)
    {
        RectTransform rect = CrtPanel.MakeRect(building, name, TopLeft, TopLeft, position, size);
        CrtButton button = CrtPanel.MakeButton(rect, font, 14f, 4f, TextAlignmentOptions.Center, 0f, 0.12f, 0.4f);
        button.Label.textWrappingMode = TextWrappingModes.Normal;
        button.Label.lineSpacing = 0f;
        button.Label.text = text;
        return button;
    }

    private TMP_Text MakeText(string name, string text, Vector2 position, Vector2 size, Color colour, TextAlignmentOptions alignment)
    {
        RectTransform rect = CrtPanel.MakeRect(building, name, TopLeft, TopLeft, position, size);
        TMP_Text label = CrtPanel.MakeLabel(rect, font, text, 14f, colour, 4f, alignment);
        label.textWrappingMode = TextWrappingModes.Normal;
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
                        cell.Button.Label.text = cell.Value();
                    else if (cell.Level != null)
                        ShowLevel(cell);
                }
            }
        }
    }

    private static void ShowLevel(Cell cell)
    {
        float level = cell.Level();
        cell.Button.Label.text = GameSettings.Percent(level);
        cell.Fill.anchorMax = new Vector2(level, 1f);
        cell.Handle.anchorMin = cell.Handle.anchorMax = new Vector2(level, 0.5f);
        cell.Handle.anchoredPosition = Vector2.zero;
    }
}
