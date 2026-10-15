using TMPro;
using UnityEngine;

// On-screen control hints that match the device in use and any rebinds. Write the text for each
// device with {Action} where a button goes, e.g. "[{Map}] CLOSE" becomes "[M] CLOSE" or "[VIEW] CLOSE".
[RequireComponent(typeof(TMP_Text))]
public class InputHint : MonoBehaviour
{
    [SerializeField, TextArea] private string keyboardMouseText = "[{Map}] CLOSE";
    [SerializeField, TextArea] private string gamepadText = "[{Map}] CLOSE";

    private TMP_Text label;
    private bool? shownForGamepad;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        GameInput.BindingsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        GameInput.BindingsChanged -= Refresh;
    }

    private void Update()
    {
        if (shownForGamepad != GameInput.UsingGamepad)
            Refresh();
    }

    private void Refresh()
    {
        if (label == null)
            return;

        shownForGamepad = GameInput.UsingGamepad;
        label.text = Format(GameInput.UsingGamepad ? gamepadText : keyboardMouseText);
    }

    // Replaces {Action} with that action's button label.
    public static string Format(string template)
    {
        foreach (var action in GameInput.Asset.FindActionMap("Gameplay").actions)
            template = template.Replace("{" + action.name + "}", GameInput.Label(action));

        foreach (var action in GameInput.Asset.FindActionMap("Menus").actions)
            template = template.Replace("{" + action.name + "}", GameInput.Label(action));

        return template;
    }
}
