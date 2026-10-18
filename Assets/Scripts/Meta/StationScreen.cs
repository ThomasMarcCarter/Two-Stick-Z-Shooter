using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A hideout station's upgrade screen: a CRT panel listing the upgrades that station holds, each
// with its level, what the next level gives, and what it costs in scrap and materials (anything
// the player is short of in red, and a note if the generator isn't up to it yet). Pick one to
// buy its next level. Opened by walking up to a station (StationConsole) and pressing Interact;
// Cancel (or the CLOSE button) shuts it. The game keeps running behind it, but the player can't
// move or shoot while it's up. Built in code the first time it opens, on the HUD canvas just
// below the Crosshair (the menu cursor), like the other screens, and rebuilt per station.
// The trade post uses the same screen as a shop: a row per item in the trader's stock (price,
// how many are left, how many the player already carries), bought with scrap straight into the
// pack.
public class StationScreen : MonoBehaviour
{
    private static readonly Color Phosphor = UiColours.Phosphor;
    private static readonly Color Background = UiColours.PanelBackground;
    private const float PanelWidth = 900f;
    private static readonly Vector2 RowSize = new Vector2(820f, 62f);

    public static bool IsOpen { get; private set; }

    // Something was bought (an upgrade, or the trader's goods) at this station.
    public static event Action<HideoutStation> Purchased;

    private static StationScreen instance;

    private HideoutStation station;
    private MetaUpgrade[] upgrades = new MetaUpgrade[0];
    private bool trading;
    private Backpack backpack;
    private GameObject panel;
    private TMP_Text title;
    private TMP_Text subtitle;
    private TMP_Text scrapText;
    private TMP_Text materialsText;
    private TMP_Text statsText;
    private TMP_Text status;
    private readonly List<CrtButton> rows = new List<CrtButton>();
    private CrtButton closeButton;
    private CrtMenu menu;
    private TMP_FontAsset font;
    private int openedFrame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        IsOpen = false;
        instance = null;
        Purchased = null;
    }

    public static void Open(HideoutStation station)
    {
        if (instance == null)
            instance = Create();

        if (instance == null)
            return;

        IsOpen = true;
        instance.openedFrame = Time.frameCount;
        instance.Show(station);
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-fit", 3, ProceduralAudio.AttachmentFit)), 0.4f, 0.8f);
    }

    public static void Close()
    {
        IsOpen = false;

        if (instance != null)
            instance.panel.SetActive(false);
    }

    private void Show(HideoutStation shown)
    {
        station = shown;
        trading = station == HideoutStation.TradePost;
        upgrades = Stations.Upgrades(station);
        PlayerHealth player = FindAnyObjectByType<PlayerHealth>();
        backpack = player != null ? Backpack.For(player.gameObject) : null;
        BuildRows();
        panel.SetActive(true);

        NpcKind? owner = Stations.Owner(station);
        title.text = $"> HIDEOUT // {Stations.Name(station)}";
        subtitle.text = owner == null
            ? station == HideoutStation.Generator
                ? "POWERS THE HIDEOUT // HIGHER UPGRADE LEVELS NEED MORE POWER"
                : "INTEL ON THE FLOORS AHEAD // HIGHER LEVELS NEED MORE POWER"
            : trading
                ? $"RUN BY {Npcs.Name(owner.Value)}, {Npcs.Role(owner.Value)} // GOODS GO STRAIGHT IN YOUR PACK // NEW STOCK EVERY VISIT"
                : $"RUN BY {Npcs.Name(owner.Value)}, {Npcs.Role(owner.Value)}";
        status.text = trading ? "> SELECT AN ITEM TO BUY ONE" : "> SELECT AN UPGRADE TO BUILD ITS NEXT LEVEL";
        Refresh();
        menu.Open();
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

        if (chosen >= RowCount)
        {
            Close();
            return;
        }

        if (trading)
        {
            Trade(chosen);
            Refresh();
            return;
        }

        MetaUpgrade upgrade = upgrades[chosen];
        string blocked = MetaProgress.BlockedReason(upgrade);

        if (blocked != null || !MetaProgress.TryBuy(upgrade))
        {
            status.text = $"> {blocked ?? "CAN'T BUILD THAT NOW"}";
            Deny();
        }
        else
        {
            status.text = $"> {MetaUpgrades.Name(upgrade)} UPGRADED TO LEVEL {MetaProgress.Level(upgrade)} // APPLIES FROM THE NEXT DEPLOYMENT";
            SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-fit", 3, ProceduralAudio.AttachmentFit)), 0.6f, 1.1f);
            Purchased?.Invoke(station);
        }

        Refresh();
    }

    private int RowCount => trading ? TraderStock.Offers.Length : upgrades.Length;

    private void Trade(int index)
    {
        string blocked = TraderStock.BlockedReason(index, backpack);

        if (blocked != null || !TraderStock.TryBuy(index, backpack))
        {
            status.text = $"> {blocked ?? "CAN'T BUY THAT NOW"}";
            Deny();
            return;
        }

        ConsumableType type = TraderStock.Offers[index].Type;
        status.text = $"> BOUGHT A {ConsumableInfo.Name(type)} // IT'S IN YOUR PACK";
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-fit", 3, ProceduralAudio.AttachmentFit)), 0.6f, 1.3f);
        Purchased?.Invoke(station);
    }

    private static void Deny()
    {
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-deny", 1, i => ProceduralAudio.AttachmentDeny())), 0.4f);
    }

    private void Refresh()
    {
        scrapText.text = $"SCRAP  {MetaProgress.Scrap}";
        materialsText.text = trading ? "" : BankedMaterials();
        statsText.text = $"RUNS {MetaProgress.Runs}   //   WINS {MetaProgress.Victories}   //   DEEPEST FLOOR {MetaProgress.BestFloor}   //   KILLS {MetaProgress.Kills}";

        if (trading)
        {
            for (int i = 0; i < TraderStock.Offers.Length; i++)
                rows[i].Label.text = OfferText(TraderStock.Offers[i]);

            return;
        }

        for (int i = 0; i < upgrades.Length; i++)
        {
            MetaUpgrade upgrade = upgrades[i];
            int level = MetaProgress.Level(upgrade);
            int max = MetaUpgrades.MaxLevel(upgrade);
            int cost = MetaProgress.NextCost(upgrade);
            string pips = "[" + new string('#', level) + new string('-', max - level) + "]";
            string now = $"<alpha=#88>NOW: {MetaUpgrades.Describe(upgrade, level)}";
            string next = cost < 0 ? "<alpha=#66>MAXED" : $"NEXT: {MetaUpgrades.Describe(upgrade, level + 1)}";

            rows[i].Label.text =
                $"<pos=1%>{MetaUpgrades.Name(upgrade)}<pos=20%>{pips}<pos=30%><size=80%>{next}</size>\n"
                + $"<size=70%><pos=1%>{now}<alpha=#FF><pos=30%>{CostLine(upgrade, level + 1, cost)}</size>";
        }
    }

    // "40 SCRAP + 3 PARTS + 2 WIRING", short items in red, then any generator requirement.
    private static string CostLine(MetaUpgrade upgrade, int level, int scrap)
    {
        if (scrap < 0)
            return "";

        string Item(bool enough, string text) => enough ? text : $"<color={UiColours.DangerHex}>{text}</color>";
        string line = Item(MetaProgress.Scrap >= scrap, $"{scrap} SCRAP");

        foreach (MaterialCost material in MetaUpgrades.MaterialCosts(upgrade, level))
        {
            bool enough = MetaProgress.MaterialCount(material.Type) >= material.Amount;
            string text = $"{material.Amount} {Materials.Name(material.Type)}";
            line += " + " + (enough ? $"<color={Materials.Hex(material.Type)}>{text}</color>" : Item(false, text));
        }

        int generator = MetaUpgrades.GeneratorNeeded(upgrade, level);

        if (MetaProgress.Level(MetaUpgrade.Generator) < generator)
            line += $"   <color={UiColours.DangerHex}>// NEEDS POWER LV{generator}</color>";

        return line;
    }

    // "MEDKIT    20 SCRAP    2 LEFT" over what it does and how many are in the pack.
    private string OfferText(TraderStock.Offer offer)
    {
        string price = MetaProgress.Scrap >= offer.Price ? $"{offer.Price} SCRAP" : $"<color={UiColours.DangerHex}>{offer.Price} SCRAP</color>";
        string left = offer.Left > 0 ? $"{offer.Left} LEFT" : "<alpha=#66>SOLD OUT";
        int carried = backpack != null ? backpack.ConsumableCount(offer.Type) : 0;
        string colour = "#" + ColorUtility.ToHtmlStringRGB(ConsumableInfo.Colour(offer.Type));
        return $"<pos=1%><color={colour}>{ConsumableInfo.Name(offer.Type)}</color><pos=30%><color={UiColours.AmberHex}>{price}</color><pos=55%>{left}\n"
            + $"<size=70%><pos=1%><alpha=#88>{ConsumableInfo.Describe(offer.Type)}<pos=55%>IN PACK: {carried}</size>";
    }

    private static string BankedMaterials()
    {
        string text = "<alpha=#99>BANKED:<alpha=#FF>";

        foreach (MaterialType type in Materials.All)
            text += $"   <color={Materials.Hex(type)}>{Materials.Name(type)} {MetaProgress.MaterialCount(type)}</color>";

        return text;
    }

    // ---------- Building ----------

    private static StationScreen Create()
    {
        EquipmentHud hud = FindAnyObjectByType<EquipmentHud>();
        Canvas canvas = hud != null ? hud.canvas : null;

        if (canvas == null)
            return null;

        GameObject root = new GameObject("Station Screen", typeof(RectTransform));
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

        StationScreen screen = root.AddComponent<StationScreen>();
        screen.Build(root.transform, canvas);
        screen.panel.SetActive(false);
        return screen;
    }

    private void Build(Transform root, Canvas canvas)
    {
        font = TMP_Settings.defaultFontAsset;
        TMP_Text sample = canvas.GetComponentInChildren<TMP_Text>(true);

        if (sample != null)
            font = sample.font;

        panel = new GameObject("Panel", typeof(RectTransform));
        panel.layer = gameObject.layer;
        panel.transform.SetParent(root, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelWidth, 400f);

        Image fill = panel.AddComponent<Image>();
        fill.color = Background;
        fill.raycastTarget = false;
        // A thin phosphor frame (an Outline effect would copy the whole filled panel in green).
        Color frame = new Color(Phosphor.r, Phosphor.g, Phosphor.b, 0.6f);
        FrameEdge(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1.5f), frame);
        FrameEdge(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1.5f), frame);
        FrameEdge(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1.5f, 0f), frame);
        FrameEdge(new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1.5f, 0f), frame);

        Color dim = new Color(Phosphor.r, Phosphor.g, Phosphor.b, 0.55f);
        float inner = PanelWidth - 60f;

        // Text at the top is placed down from the panel's top edge, and at the bottom up from
        // its bottom edge, so the panel can grow and shrink with the number of rows.
        title = MakeText("Title", "", 26, new Vector2(0f, -38f), 1f, new Vector2(inner, 36f), Phosphor, 10f, TextAlignmentOptions.Left);
        scrapText = MakeText("Scrap", "", 22, new Vector2(0f, -38f), 1f, new Vector2(inner, 36f), UiColours.Amber, 6f, TextAlignmentOptions.Right);
        subtitle = MakeText("Subtitle", "", 13, new Vector2(0f, -68f), 1f, new Vector2(inner, 20f), dim, 4f, TextAlignmentOptions.Left);
        materialsText = MakeText("Materials", "", 13, new Vector2(0f, -94f), 1f, new Vector2(inner, 20f), Phosphor, 3f, TextAlignmentOptions.Left);

        closeButton = MakeButton("Close", new Vector2(260f, 40f), TextAlignmentOptions.Center);
        closeButton.Label.text = "[ CLOSE ]";
        statsText = MakeText("Stats", "", 11, new Vector2(0f, 52f), 0f, new Vector2(inner, 18f), dim, 4f, TextAlignmentOptions.Center);
        status = MakeText("Status", "", 12, new Vector2(0f, 28f), 0f, new Vector2(inner, 20f), dim, 4f, TextAlignmentOptions.Center);
    }

    // One edge of the panel's frame, stretched along the panel between two anchors.
    private void FrameEdge(Vector2 anchorMin, Vector2 anchorMax, Vector2 thickness, Color colour)
    {
        GameObject edge = new GameObject("Frame", typeof(RectTransform));
        edge.layer = gameObject.layer;
        edge.transform.SetParent(panel.transform, false);
        RectTransform rect = edge.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = thickness;
        rect.anchoredPosition = Vector2.zero;

        Image image = edge.AddComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
    }

    // One row per upgrade of the station shown, then the close button under them.
    private void BuildRows()
    {
        int count = RowCount;

        while (rows.Count < count)
            rows.Add(MakeButton("Upgrade " + rows.Count, RowSize, TextAlignmentOptions.Left));

        const float top = 140f;
        float step = RowSize.y + 10f;

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < count;
            rows[i].gameObject.SetActive(used);

            if (used)
                rows[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -top - i * step);
        }

        float closeY = top + count * step + 4f;
        closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -closeY);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelWidth, closeY + 40f + 90f);

        List<CrtButton> all = new List<CrtButton>();

        for (int i = 0; i < count; i++)
            all.Add(rows[i]);

        all.Add(closeButton);
        menu = new CrtMenu(all.ToArray());
    }

    // `edge` is 1 to place it from the panel's top, 0 from its bottom.
    private TMP_Text MakeText(string name, string text, float size, Vector2 position, float edge, Vector2 box, Color colour,
        float spacing, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(panel.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, edge);
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

    // Placed down from the panel's top edge (BuildRows positions it).
    private CrtButton MakeButton(string name, Vector2 size, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(panel.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
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
        label.lineSpacing = 6f;
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
