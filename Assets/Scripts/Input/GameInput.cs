using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum InputDeviceKind
{
    KeyboardMouse,
    Gamepad
}

// All game input in one place, on the Input System: keyboard + mouse and controller side by side,
// whichever was used last is "active". Bindings can be changed at runtime and are saved to
// PlayerPrefs, by action name and binding index (the bindings are made in code, so their IDs are
// new every launch and the Input System's own ID-based override JSON never matched them back up).
// InputDriver updates the shared aim point and active device each frame.
// The project has domain reload turned off for entering Play mode, so statics survive between
// sessions; everything here is rebuilt at the start of each one instead of in a static constructor.
public static partial class GameInput
{
    public const string KeyboardMouseGroup = "KeyboardMouse";
    public const string GamepadGroup = "Gamepad";

    private const string SaveKey = "input.bindings.v2";
    private const string OldSaveKey = "input.bindings";   // ID-based, never loadable; deleted

    [Serializable]
    private class SavedOverride
    {
        public string action;   // "Map/Action"
        public int index;
        public string path;
    }

    [Serializable]
    private class SavedOverrides
    {
        public List<SavedOverride> overrides = new List<SavedOverride>();
    }

    public static InputActionAsset Asset { get; private set; }

    // Gameplay
    public static InputAction Move { get; private set; }
    public static InputAction Aim { get; private set; }
    public static InputAction Fire { get; private set; }
    public static InputAction Reload { get; private set; }
    public static InputAction Sprint { get; private set; }
    public static InputAction Sneak { get; private set; }
    public static InputAction Interact { get; private set; }
    public static InputAction Map { get; private set; }
    public static InputAction Pause { get; private set; }
    public static InputAction WeaponMain { get; private set; }
    public static InputAction WeaponSidearm { get; private set; }
    public static InputAction QuickScroll { get; private set; }    // mouse wheel: selects a quick-use slot
    public static InputAction Inventory { get; private set; }
    public static InputAction Melee { get; private set; }
    public static InputAction QuickUse { get; private set; }
    public static InputAction QuickNext { get; private set; }
    public static InputAction QuickPrevious { get; private set; }
    public static InputAction[] QuickSlots { get; private set; }  // keyboard only: use a slot directly

    // Menus
    public static InputAction Navigate { get; private set; }
    public static InputAction Submit { get; private set; }
    public static InputAction Cancel { get; private set; }

    public static InputDeviceKind ActiveDevice { get; internal set; } = InputDeviceKind.KeyboardMouse;
    public static bool UsingGamepad => ActiveDevice == InputDeviceKind.Gamepad;

    // Where the player is aiming, in the undistorted screen image (after the CRT mapping), so it
    // can go straight into Camera.ScreenPointToRay or the UI. Follows the mouse, or the right
    // stick around the player on a controller.
    public static Vector2 Pointer { get; internal set; }
    public static bool PointerMovedThisFrame { get; internal set; }

    // Lets a screen (e.g. the map) park the controller's pointer somewhere while it's open.
    public static Func<Vector2?> GamepadPointerOverride { get; set; }

    public static event Action BindingsChanged;

    // Built on first use after a script reload, and rebuilt at the start of every Play session
    // after that. Both are needed: Play mode can start straight after a reload without the
    // session hook running, and with domain reload off the hook is what gives each session a
    // clean slate.
    static GameInput()
    {
        Build();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void StartSession()
    {
        if (Asset != null)
        {
            Asset.Disable();
            UnityEngine.Object.Destroy(Asset);
        }

        ActiveDevice = InputDeviceKind.KeyboardMouse;
        Pointer = default;
        PointerMovedThisFrame = false;
        GamepadPointerOverride = null;
        BindingsChanged = null;

        Build();
    }

    private static void Build()
    {
        Asset = ScriptableObject.CreateInstance<InputActionAsset>();
        Asset.name = "Game Input";

        InputActionMap gameplay = Asset.AddActionMap("Gameplay");

        Move = gameplay.AddAction("Move", InputActionType.Value);
        Move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w", KeyboardMouseGroup)
            .With("Down", "<Keyboard>/s", KeyboardMouseGroup)
            .With("Left", "<Keyboard>/a", KeyboardMouseGroup)
            .With("Right", "<Keyboard>/d", KeyboardMouseGroup);
        Move.AddBinding("<Gamepad>/leftStick", groups: GamepadGroup);

        Aim = gameplay.AddAction("Aim", InputActionType.Value);
        Aim.AddBinding("<Gamepad>/rightStick", groups: GamepadGroup);

        Fire = AddButton(gameplay, "Fire", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
        Reload = AddButton(gameplay, "Reload", "<Keyboard>/r", "<Gamepad>/buttonWest");
        Sprint = AddButton(gameplay, "Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftTrigger");
        Sneak = AddButton(gameplay, "Sneak", "<Keyboard>/leftCtrl", "<Gamepad>/leftStickPress");
        Interact = AddButton(gameplay, "Interact", "<Keyboard>/e", "<Gamepad>/buttonNorth");
        Map = AddButton(gameplay, "Map", "<Keyboard>/m", "<Gamepad>/select");
        Pause = AddButton(gameplay, "Pause", "<Keyboard>/escape", "<Gamepad>/start");
        WeaponMain = AddButton(gameplay, "WeaponMain", "<Keyboard>/1", "<Gamepad>/dpad/up");
        WeaponSidearm = AddButton(gameplay, "WeaponSidearm", "<Keyboard>/2", "<Gamepad>/dpad/down");

        Inventory = AddButton(gameplay, "Inventory", "<Keyboard>/tab", "<Gamepad>/leftShoulder");

        Melee = AddButton(gameplay, "Melee", "<Keyboard>/v", "<Gamepad>/buttonEast");
        QuickUse = AddButton(gameplay, "QuickUse", "<Mouse>/rightButton", "<Gamepad>/rightShoulder");
        QuickNext = AddButton(gameplay, "QuickNext", null, "<Gamepad>/dpad/right");
        QuickPrevious = AddButton(gameplay, "QuickPrevious", null, "<Gamepad>/dpad/left");
        QuickSlots = new InputAction[4];

        for (int i = 0; i < QuickSlots.Length; i++)
            QuickSlots[i] = AddButton(gameplay, "QuickSlot" + (i + 1), "<Keyboard>/" + (i + 3), null);

        QuickScroll = gameplay.AddAction("QuickScroll", InputActionType.Value);
        QuickScroll.AddBinding("<Mouse>/scroll/y", groups: KeyboardMouseGroup);

        InputActionMap menus = Asset.AddActionMap("Menus");

        Navigate = menus.AddAction("Navigate", InputActionType.Value);
        Navigate.AddBinding("<Gamepad>/leftStick", groups: GamepadGroup);
        Navigate.AddBinding("<Gamepad>/dpad", groups: GamepadGroup);
        Navigate.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow", KeyboardMouseGroup)
            .With("Down", "<Keyboard>/downArrow", KeyboardMouseGroup)
            .With("Left", "<Keyboard>/leftArrow", KeyboardMouseGroup)
            .With("Right", "<Keyboard>/rightArrow", KeyboardMouseGroup);

        Submit = AddButton(menus, "Submit", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
        Submit.AddBinding("<Keyboard>/space", groups: KeyboardMouseGroup);
        Cancel = AddButton(menus, "Cancel", "<Keyboard>/escape", "<Gamepad>/buttonEast");

        LoadBindings();
        Asset.Enable();
    }

    private static InputAction AddButton(InputActionMap map, string name, string keyboardMouse, string gamepad)
    {
        InputAction action = map.AddAction(name, InputActionType.Button);

        // Either can be left out, for an action only one device has.
        if (keyboardMouse != null)
            action.AddBinding(keyboardMouse, groups: KeyboardMouseGroup);

        if (gamepad != null)
            action.AddBinding(gamepad, groups: GamepadGroup);

        return action;
    }

    // ---------- Labels ----------

    // Short label for an action's button on the active device, e.g. "E" or "Y". Follows rebinds.
    public static string Label(InputAction action)
    {
        return Label(action, UsingGamepad ? GamepadGroup : KeyboardMouseGroup);
    }

    public static string Label(InputAction action, string group)
    {
        int index = FindBinding(action, group);
        return index < 0 ? "-" : BindingLabel(action, index);
    }

    public static string BindingLabel(InputAction action, int bindingIndex)
    {
        string path = action.bindings[bindingIndex].effectivePath;

        if (GamepadNames.TryGetValue(path, out string friendly))
            return friendly;

        if (path == "<Mouse>/leftButton") return "LMB";
        if (path == "<Mouse>/rightButton") return "RMB";
        if (path == "<Mouse>/middleButton") return "MMB";

        return action.GetBindingDisplayString(bindingIndex).ToUpperInvariant();
    }

    // Xbox-style names, since the generic gamepad layout names buttons by position.
    private static readonly Dictionary<string, string> GamepadNames = new Dictionary<string, string>
    {
        { "<Gamepad>/buttonSouth", "A" },
        { "<Gamepad>/buttonEast", "B" },
        { "<Gamepad>/buttonWest", "X" },
        { "<Gamepad>/buttonNorth", "Y" },
        { "<Gamepad>/rightTrigger", "RT" },
        { "<Gamepad>/leftTrigger", "LT" },
        { "<Gamepad>/rightShoulder", "RB" },
        { "<Gamepad>/leftShoulder", "LB" },
        { "<Gamepad>/start", "MENU" },
        { "<Gamepad>/select", "VIEW" },
        { "<Gamepad>/leftStickPress", "LS" },
        { "<Gamepad>/rightStickPress", "RS" },
        { "<Gamepad>/dpad/up", "D-UP" },
        { "<Gamepad>/dpad/down", "D-DOWN" },
        { "<Gamepad>/dpad/left", "D-LEFT" },
        { "<Gamepad>/dpad/right", "D-RIGHT" },
        { "<Gamepad>/leftStick", "L-STICK" },
        { "<Gamepad>/rightStick", "R-STICK" }
    };

    // First non-composite binding in a group, e.g. the controller binding for Fire.
    public static int FindBinding(InputAction action, string group)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            if (!binding.isComposite && binding.groups != null && binding.groups.Contains(group))
                return i;
        }

        return -1;
    }

    // A named part of a composite, e.g. Move's "Up" key.
    public static int FindCompositePart(InputAction action, string part)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            if (action.bindings[i].isPartOfComposite && string.Equals(action.bindings[i].name, part, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }
}
