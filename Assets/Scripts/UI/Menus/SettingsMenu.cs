using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The settings screen, opened from the pause and main menus, with two tabs along the top.
// CONTROLS: one row per action with a keyboard/mouse column and a controller column; pick a cell
// and press the new key or button (moving and aiming on the controller stay on the sticks).
// Bindings are saved by GameInput. AUDIO / VIDEO: volume sliders (click or drag, or left/right in
// 5% steps), then display mode, resolution, frame rate and CRT strength (GameSettings), each with
// clickable arrows either side to cycle it (clicking the value steps on, right-click steps back,
// left/right on the keyboard or controller too). Rows are built at runtime, so adding one is a
// line in BuildControls or BuildOptions.
public partial class SettingsMenu : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform rowContainer;
    [SerializeField] private TMP_Text status;
    [SerializeField] private TMP_FontAsset font;

    [Header("Look")]
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
        public CrtButton Left;        // an option's arrows
        public CrtButton Right;
        public Func<float> Level;     // a slider: its 0-1 value...
        public Action<float, bool> SetLevel;   // ...and how to set it (bool: save now)
        public RectTransform Track;
        public RectTransform Fill;
        public RectTransform Handle;
    }

    private const int ControlsPage = 0;
    private const int OptionsPage = 1;

    private readonly List<Cell[]>[] pages = { new List<Cell[]>(), new List<Cell[]>() };
    private readonly RectTransform[] pageRoots = new RectTransform[2];
    private int page;
    private RectTransform building;

    // Cells are laid out down and across from the page's top-left corner.
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

    private List<Cell[]> grid => pages[page];

    private int row = -1;
    private int column;
    private float nextMove;
    private bool held;

    private Cell dragging;            // the slider held down with the mouse
    private int hoverArrow;           // -1 / +1 while the pointer is over an option's arrow
    private Camera uiCamera;

    private InputActionRebindingExtensions.RebindingOperation rebinding;
    private Cell pendingRebind;
    private int ignoreCancelUntilFrame;

    private void Awake()
    {
        Canvas canvas = rowContainer.GetComponentInParent<Canvas>(true);
        uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
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

        EndDrag();
        pendingRebind = null;
        IsOpen = false;
        panel.SetActive(false);
    }

    private void ShowPage(int index)
    {
        page = index;

        for (int i = 0; i < pageRoots.Length; i++)
            pageRoots[i].gameObject.SetActive(i == page);

        status.text = "";
        row = GameInput.UsingGamepad ? 0 : -1;
        column = page;
        RefreshLabels();
        Highlight();
    }

    // Call every frame while open. Returns true when the player backs out.
    public bool Tick()
    {
        if (Busy())
            return false;

        if (GameInput.Cancel.WasPressedThisFrame() && Time.frameCount > ignoreCancelUntilFrame)
            return true;

        Cell hovered = HandlePointer();
        HandleNavigation();
        Highlight();

        // Right-click steps an option back.
        if (hovered != null && hovered.Step != null && hovered.SetLevel == null && Mouse.current != null
            && Mouse.current.rightButton.wasPressedThisFrame)
        {
            hovered.Step(-1);
            RefreshLabels();
            return false;
        }

        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && hovered != null;
        Cell chosen = clicked ? hovered : GameInput.Submit.WasPressedThisFrame() ? Selected() : null;

        return chosen != null && Choose(chosen, clicked);
    }

    // A rebind waiting for or listening to input, or a slider being dragged: the menu does nothing
    // else until it's over.
    private bool Busy()
    {
        if (rebinding != null)
            return true;

        if (pendingRebind != null)
        {
            // Start only once the button that picked the cell is let go, or it gets bound itself.
            if (!GameInput.Submit.IsPressed() && (Mouse.current == null || !Mouse.current.leftButton.isPressed))
                BeginRebind(pendingRebind);

            return true;
        }

        if (dragging != null)
        {
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                DragTo(dragging, false);
            else
                EndDrag();

            return true;
        }

        return false;
    }

    // Does what a picked cell does (`clicked` if by mouse rather than Submit). True for BACK.
    private bool Choose(Cell chosen, bool clicked)
    {
        if (chosen.SetLevel != null)
        {
            // Sliders: the mouse grabs the handle; Submit does nothing (left/right moves them).
            if (clicked)
            {
                dragging = chosen;
                DragTo(chosen, false);
            }
        }
        else if (chosen.Tab >= 0)
        {
            ShowPage(chosen.Tab);
        }
        else if (chosen.Step != null)
        {
            chosen.Step(clicked && hoverArrow != 0 ? hoverArrow : 1);
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
}
