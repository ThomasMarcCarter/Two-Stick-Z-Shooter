using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The hideout's upgrade terminal: a CRT screen listing the permanent upgrades, their level, what
// they give now and next, and the scrap price. Pick one to buy its next level. Opened by walking
// up to the terminal desk in the hideout and pressing Interact; Cancel (or the CLOSE button)
// shuts it. The game keeps running behind it, but the player can't move or shoot while it's up.
// Built in code the first time it opens, on the HUD canvas just below the Crosshair (the menu
// cursor), like the other screens.
public class HideoutTerminal : MonoBehaviour
{
    private static readonly Color Phosphor = new Color(0.35f, 1f, 0.55f, 1f);
    private static readonly Color Background = new Color(0f, 0.035f, 0.018f, 0.96f);
    private static readonly Vector2 PanelSize = new Vector2(860f, 560f);
    private static readonly Vector2 RowSize = new Vector2(780f, 46f);

    public static bool IsOpen { get; private set; }

    private static HideoutTerminal instance;

    private GameObject panel;
    private TMP_Text scrapText;
    private TMP_Text statsText;
    private TMP_Text status;
    private readonly List<CrtButton> rows = new List<CrtButton>();
    private CrtButton closeButton;
    private CrtMenu menu;
    private int openedFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        IsOpen = false;
        instance = null;
    }

    public static void Open()
    {
        if (instance == null)
            instance = Create();

        if (instance == null)
            return;

        IsOpen = true;
        instance.openedFrame = Time.frameCount;
        instance.panel.SetActive(true);
        instance.status.text = "> SELECT AN UPGRADE TO PURCHASE ITS NEXT LEVEL";
        instance.Refresh();
        instance.menu.Open();
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-fit", 3, ProceduralAudio.AttachmentFit)), 0.4f, 0.8f);
    }

    public static void Close()
    {
        IsOpen = false;

        if (instance != null)
            instance.panel.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        // Don't let the Interact press that opened it also buy something.
        if (Time.frameCount == openedFrame)
            return;

        if (GameInput.Cancel.WasPressedThisFrame() || GameInput.Pause.WasPressedThisFrame())
        {
            Close();
            return;
        }

        int chosen = menu.Update();

        if (chosen < 0)
            return;

        if (chosen >= MetaUpgrades.All.Length)
        {
            Close();
            return;
        }

        MetaUpgrade upgrade = MetaUpgrades.All[chosen];
        int cost = MetaProgress.NextCost(upgrade);

        if (cost < 0)
        {
            status.text = $"> {MetaUpgrades.Name(upgrade)} IS FULLY UPGRADED";
            Deny();
        }
        else if (!MetaProgress.TryBuy(upgrade))
        {
            status.text = $"> NOT ENOUGH SCRAP // NEED {cost - MetaProgress.Scrap} MORE";
            Deny();
        }
        else
        {
            status.text = $"> {MetaUpgrades.Name(upgrade)} UPGRADED TO LEVEL {MetaProgress.Level(upgrade)} // APPLIES FROM THE NEXT DEPLOYMENT";
            SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-fit", 3, ProceduralAudio.AttachmentFit)), 0.6f, 1.1f);
        }

        Refresh();
    }

    private static void Deny()
    {
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-deny", 1, i => ProceduralAudio.AttachmentDeny())), 0.4f);
    }

    private void Refresh()
    {
        scrapText.text = $"SCRAP  {MetaProgress.Scrap}";
        statsText.text = $"RUNS {MetaProgress.Runs}   //   WINS {MetaProgress.Victories}   //   DEEPEST FLOOR {MetaProgress.BestFloor}   //   KILLS {MetaProgress.Kills}";

        for (int i = 0; i < MetaUpgrades.All.Length; i++)
        {
            MetaUpgrade upgrade = MetaUpgrades.All[i];
            int level = MetaProgress.Level(upgrade);
            int max = MetaUpgrades.MaxLevel(upgrade);
            int cost = MetaProgress.NextCost(upgrade);
            string pips = "[" + new string('#', level) + new string('-', max - level) + "]";
            string next = cost < 0 ? "<alpha=#66>MAXED" : $"NEXT: {MetaUpgrades.Describe(upgrade, level + 1)}";
            string price = cost < 0 ? "" : cost <= MetaProgress.Scrap ? $"{cost} SCRAP" : $"<color=#FF6655>{cost} SCRAP</color>";

            rows[i].Label.text =
                $"<pos=2%>{MetaUpgrades.Name(upgrade)}<pos=22%>{pips}<pos=31%><size=80%>{next}</size><pos=84%>{price}";
        }
    }

    // ---------- Building ----------

    private static HideoutTerminal Create()
    {
        EquipmentHud hud = FindAnyObjectByType<EquipmentHud>();
        Canvas canvas = hud != null ? hud.canvas : null;

        if (canvas == null)
            return null;

        GameObject root = new GameObject("Hideout Terminal", typeof(RectTransform));
        root.layer = canvas.gameObject.layer;
        root.transform.SetParent(canvas.transform, false);

        // Below the Crosshair, which is the menu cursor and has to draw on top.
        Transform crosshair = canvas.transform.Find("Crosshair");

        if (crosshair != null)
            root.transform.SetSiblingIndex(crosshair.GetSiblingIndex());

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

        HideoutTerminal terminal = root.AddComponent<HideoutTerminal>();
        terminal.Build(root.transform, canvas);
        terminal.panel.SetActive(false);
        return terminal;
    }

    private void Build(Transform root, Canvas canvas)
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        TMP_Text sample = canvas.GetComponentInChildren<TMP_Text>(true);

        if (sample != null)
            font = sample.font;

        panel = new GameObject("Panel", typeof(RectTransform));
        panel.layer = gameObject.layer;
        panel.transform.SetParent(root, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = PanelSize;

        Image fill = panel.AddComponent<Image>();
        fill.color = Background;
        fill.raycastTarget = false;
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(Phosphor.r, Phosphor.g, Phosphor.b, 0.6f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        Color dim = new Color(Phosphor.r, Phosphor.g, Phosphor.b, 0.55f);
        float top = PanelSize.y / 2f;

        MakeText("Title", "> HIDEOUT // UPGRADE TERMINAL", 26, new Vector2(0f, top - 40f), new Vector2(800f, 36f), Phosphor, 10f, font, TextAlignmentOptions.Left);
        scrapText = MakeText("Scrap", "", 22, new Vector2(0f, top - 40f), new Vector2(800f, 36f), new Color(1f, 0.85f, 0.35f), 6f, font, TextAlignmentOptions.Right);
        statsText = MakeText("Stats", "", 12, new Vector2(0f, top - 76f), new Vector2(800f, 20f), dim, 4f, font, TextAlignmentOptions.Left);

        for (int i = 0; i < MetaUpgrades.All.Length; i++)
            rows.Add(MakeButton("Upgrade " + i, new Vector2(0f, top - 130f - i * (RowSize.y + 8f)), RowSize, font, TextAlignmentOptions.Left));

        float bottom = top - 130f - MetaUpgrades.All.Length * (RowSize.y + 8f);
        closeButton = MakeButton("Close", new Vector2(0f, bottom - 6f), new Vector2(260f, 40f), font, TextAlignmentOptions.Center);
        closeButton.Label.text = "[ CLOSE ]";
        status = MakeText("Status", "", 12, new Vector2(0f, -top + 30f), new Vector2(800f, 20f), dim, 4f, font, TextAlignmentOptions.Center);

        List<CrtButton> all = new List<CrtButton>(rows) { closeButton };
        menu = new CrtMenu(all.ToArray());
    }

    private TMP_Text MakeText(string name, string text, float size, Vector2 position, Vector2 box, Color colour, float spacing,
        TMP_FontAsset font, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(panel.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = box;

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.color = colour;
        label.characterSpacing = spacing;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.richText = true;
        label.text = text;
        return label;
    }

    private CrtButton MakeButton(string name, Vector2 position, Vector2 size, TMP_FontAsset font, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(panel.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.layer = gameObject.layer;
        labelObject.transform.SetParent(go.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 0f);
        labelRect.offsetMax = new Vector2(-10f, 0f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = 16;
        label.characterSpacing = 4f;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.richText = true;
        label.raycastTarget = false;

        CrtButton button = go.AddComponent<CrtButton>();
        button.Setup(image, label,
            new Color(Phosphor.r, Phosphor.g, Phosphor.b, 0.08f),
            new Color(Phosphor.r, Phosphor.g, Phosphor.b, 0.35f),
            new Color(Phosphor.r, Phosphor.g, Phosphor.b, 0.85f),
            Color.white);
        return button;
    }
}
