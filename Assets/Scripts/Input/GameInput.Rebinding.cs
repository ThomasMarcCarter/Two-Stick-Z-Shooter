using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// GameInput's rebinding: listening for a new key or button for an action (swapping with any
// action that already had it), resetting to the defaults, and saving and loading the overrides
// in PlayerPrefs.
public static partial class GameInput
{
    // Waits for the next keyboard/mouse or controller input (by group) and binds it. If another
    // gameplay action in the same group already used that input, the two swap.
    public static InputActionRebindingExtensions.RebindingOperation StartRebind(
        InputAction action, int bindingIndex, Action onFinished)
    {
        string group = action.bindings[bindingIndex].groups;
        bool gamepad = group != null && group.Contains(GamepadGroup);
        string previousPath = action.bindings[bindingIndex].effectivePath;

        action.Disable();

        InputActionRebindingExtensions.RebindingOperation operation = action
            .PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithControlsExcluding("<Pointer>/position")
            .WithControlsExcluding("<Gamepad>/leftStick")
            .WithControlsExcluding("<Gamepad>/rightStick")
            .WithTimeout(6f)
            .OnMatchWaitForAnother(0.1f);

        if (gamepad)
        {
            operation.WithControlsHavingToMatchPath("<Gamepad>");
        }
        else
        {
            operation.WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsHavingToMatchPath("<Mouse>")
                .WithCancelingThrough("<Keyboard>/escape");
        }

        operation
            .OnComplete(op =>
            {
                op.Dispose();
                SwapIfTaken(action, bindingIndex, group, previousPath);
                action.Enable();
                SaveBindings();
                onFinished?.Invoke();
            })
            .OnCancel(op =>
            {
                op.Dispose();
                action.Enable();
                onFinished?.Invoke();
            });

        return operation.Start();
    }

    private static void SwapIfTaken(InputAction rebound, int reboundIndex, string group, string previousPath)
    {
        string newPath = rebound.bindings[reboundIndex].effectivePath;

        foreach (InputAction action in Asset.FindActionMap("Gameplay").actions)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action == rebound && i == reboundIndex)
                    continue;

                InputBinding binding = action.bindings[i];

                if (binding.isComposite || binding.groups != group || binding.effectivePath != newPath)
                    continue;

                action.ApplyBindingOverride(i, previousPath);
            }
        }
    }

    public static void ResetBindings()
    {
        Asset.RemoveAllBindingOverrides();
        SaveBindings();
    }

    private static void SaveBindings()
    {
        SavedOverrides saved = new SavedOverrides();

        foreach (InputActionMap map in Asset.actionMaps)
        {
            foreach (InputAction action in map.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    string path = action.bindings[i].overridePath;

                    if (path != null)
                        saved.overrides.Add(new SavedOverride { action = map.name + "/" + action.name, index = i, path = path });
                }
            }
        }

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saved));
        PlayerPrefs.Save();
        BindingsChanged?.Invoke();
    }

    private static void LoadBindings()
    {
        if (PlayerPrefs.HasKey(OldSaveKey))
            PlayerPrefs.DeleteKey(OldSaveKey);

        string json = PlayerPrefs.GetString(SaveKey, "");

        if (string.IsNullOrEmpty(json))
            return;

        SavedOverrides saved;

        try
        {
            saved = JsonUtility.FromJson<SavedOverrides>(json);
        }
        catch (ArgumentException)
        {
            return;
        }

        if (saved?.overrides == null)
            return;

        // Anything that no longer lines up (an action renamed or removed) is skipped.
        foreach (SavedOverride entry in saved.overrides)
        {
            InputAction action = Asset.FindAction(entry.action);

            if (action != null && entry.index >= 0 && entry.index < action.bindings.Count && !action.bindings[entry.index].isComposite)
                action.ApplyBindingOverride(entry.index, entry.path);
        }
    }
}
