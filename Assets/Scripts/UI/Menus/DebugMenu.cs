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
public partial class DebugMenu : MonoBehaviour
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
