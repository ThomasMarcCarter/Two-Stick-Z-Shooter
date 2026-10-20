using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// A hideout station's upgrade screen: a CRT panel listing the upgrades that station holds, each
// with its level, what the next level gives, and what it costs in scrap and materials (anything
// the player is short of in red, and a note if the generator isn't up to it yet). Pick one to
// buy its next level. Opened by walking up to a station (StationConsole) and pressing Interact;
// Cancel (or the CLOSE button) shuts it. The game keeps running behind it, but the player can't
// move or shoot while it's up. Built in code the first time it opens (CrtPanel), and rebuilt
// per station.
// The trade post uses the same screen as a shop: a row per item in the trader's stock (price,
// how many are left, how many the player already carries), bought with scrap straight into the
// pack.
public class StationScreen : MonoBehaviour
{
    private static readonly Color Phosphor = UiColours.Phosphor;
    private static readonly Color Background = UiColours.PanelBackground;
    private const float PanelWidth = 900f;
    private const float RowWidth = 820f;

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

    // Upgrade rows have three lines (next level, current level, cost); the trader's have two.
    private Vector2 RowSize => new Vector2(RowWidth, trading ? 62f : 80f);

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

            // The descriptions all start in the right-hand column, so a long one can't run into
            // the next (the name column is too narrow for them).
            rows[i].Label.text =
                $"<pos=1%>{MetaUpgrades.Name(upgrade)}<pos=20%>{pips}<pos=30%><size=80%>{next}</size>\n"
                + $"<size=70%><pos=30%>{now}</size>\n"
                + $"<size=70%><alpha=#FF><pos=30%>{CostLine(upgrade, level + 1, cost)}</size>";
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
        GameObject root = CrtPanel.CreateRoot("Station Screen", out Canvas canvas);

        if (root == null)
            return null;

        StationScreen screen = root.AddComponent<StationScreen>();
        screen.Build(root.transform, canvas);
        screen.panel.SetActive(false);
        return screen;
    }

    private void Build(Transform root, Canvas canvas)
    {
        font = CrtPanel.Font(canvas);
        panel = CrtPanel.CreatePanel(root, new Vector2(PanelWidth, 400f));

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

    // One row per upgrade of the station shown, then the close button under them.
    private void BuildRows()
    {
        int count = RowCount;

        while (rows.Count < count)
            rows.Add(MakeButton("Upgrade " + rows.Count, RowSize, TextAlignmentOptions.Left));

        // From the panel's top edge to the first row's top edge, under the banked materials.
        const float top = 109f;
        float step = RowSize.y + 10f;

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < count;
            rows[i].gameObject.SetActive(used);

            if (used)
            {
                RectTransform rect = rows[i].GetComponent<RectTransform>();
                rect.sizeDelta = RowSize;
                rect.anchoredPosition = new Vector2(0f, -top - RowSize.y / 2f - i * step);
            }
        }

        float closeY = top + count * step + 35f;
        closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -closeY);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelWidth, closeY + 40f + 90f);

        List<CrtButton> all = new List<CrtButton>();

        for (int i = 0; i < count; i++)
            all.Add(rows[i]);

        all.Add(closeButton);
        menu = new CrtMenu(all.ToArray());
    }

    private TMP_Text MakeText(string name, string text, float size, Vector2 position, float edge, Vector2 box, Color colour,
        float spacing, TextAlignmentOptions alignment)
    {
        return CrtPanel.MakeText(panel.transform, font, name, text, size, position, edge, box, colour, spacing, alignment);
    }

    private CrtButton MakeButton(string name, Vector2 size, TextAlignmentOptions alignment)
    {
        return CrtPanel.MakeButton(panel.transform, font, name, size, alignment);
    }
}
