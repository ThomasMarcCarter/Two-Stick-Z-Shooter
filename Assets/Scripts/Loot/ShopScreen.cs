using System.Collections.Generic;
using TMPro;
using UnityEngine;

// The shop screen: a CRT panel in the crate screen's style listing a shop counter's stock (ShopCounter),
// one row per item with its name in its colour, what buying it does and its price in scrap. Carried
// scrap shows at the top and updates as it's spent. Pick a row to buy it (too little scrap or a full
// backpack is refused with a message), CLOSE or Cancel to shut it. Bought rows go. The player can't
// move or shoot while it's up. Built in code the first time it opens (CrtPanel).
public class ShopScreen : MonoBehaviour
{
    private static readonly Color Phosphor = UiColours.Phosphor;
    private const float PanelWidth = 900f;
    private static readonly Vector2 RowSize = new Vector2(820f, 34f);
    private const float RowStep = 38f;
    private const float ListTop = 100f;

    public static bool IsOpen { get; private set; }

    private static ShopScreen instance;

    private ShopCounter shop;
    private PlayerWeapon player;
    private GameObject panel;
    private TMP_Text title;
    private TMP_Text subtitle;
    private TMP_Text detail;
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
    }

    public static void Open(ShopCounter shop, PlayerWeapon player)
    {
        if (shop == null || player == null)
            return;

        if (instance == null)
            instance = Create();

        if (instance == null)
            return;

        IsOpen = true;
        instance.openedFrame = Time.frameCount;
        instance.shop = shop;
        instance.player = player;
        instance.panel.SetActive(true);
        instance.status.text = "";
        instance.Rebuild(0);
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("inventory-lift", 2, i => ProceduralAudio.BackpackRustle(300 + i))), 0.45f, 0.8f);
    }

    public static void Close()
    {
        IsOpen = false;

        if (instance != null)
        {
            instance.panel.SetActive(false);
            instance.shop = null;
        }
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        // The shop went (floor changed) or the player died.
        if (shop == null || player == null || DeathScreen.IsShowing)
        {
            Close();
            return;
        }

        // Don't let the Interact press that opened it also buy something.
        if (Time.frameCount == openedFrame)
            return;

        if (GameInput.Cancel.WasPressedThisFrame() || GameInput.Pause.WasPressedThisFrame())
        {
            Close();
            return;
        }

        int chosen = menu.Update();
        ShowDetail(menu.Highlighted);

        if (chosen < 0)
            return;

        int count = shop.Stock.Count;

        if (chosen >= count)
        {
            Close();
            return;
        }

        if (shop.Buy(shop.Stock[chosen], player, out string message))
        {
            status.text = "> " + StripTags(message);
            SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("inventory-lift", 2, i => ProceduralAudio.BackpackRustle(300 + i))), 0.45f, 1.1f);
            Rebuild(chosen);
        }
        else
        {
            status.text = "> " + StripTags(message);
            SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-deny", 1, i => ProceduralAudio.AttachmentDeny())), 0.4f);
        }
    }

    private static string StripTags(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "");
    }

    // ---------- Rows ----------

    // One row per item for sale, then CLOSE; keeps the highlight near `focus`.
    private void Rebuild(int focus)
    {
        IReadOnlyList<ShopEntry> stock = shop.Stock;
        int count = stock.Count;

        while (rows.Count < count)
            rows.Add(MakeButton("Item " + rows.Count, RowSize, TextAlignmentOptions.Left));

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < count;
            rows[i].gameObject.SetActive(used);

            if (used)
                rows[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -ListTop - i * RowStep);
        }

        float buttonsTop = ListTop + Mathf.Max(1, count) * RowStep + 70f;
        closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -buttonsTop);
        detail.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -ListTop - Mathf.Max(1, count) * RowStep + 4f);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelWidth, buttonsTop + 44f + 36f + 36f);

        title.text = "> SHOP";

        List<CrtButton> all = new List<CrtButton>();

        for (int i = 0; i < count; i++)
            all.Add(rows[i]);

        all.Add(closeButton);

        int first = count == 0 ? 0 : Mathf.Clamp(focus, 0, count - 1);
        menu = new CrtMenu(new[] { all.ToArray() }, 0, first);
        menu.Open();
        Refresh();
    }

    private void Refresh()
    {
        IReadOnlyList<ShopEntry> stock = shop.Stock;
        int carried = shop.CarriedScrap();

        subtitle.text = stock.Count == 0
            ? $"<alpha=#88>SOLD OUT // SCRAP CARRIED: {carried}"
            : $"SELECT AN ITEM TO BUY IT // SCRAP CARRIED: <color={UiColours.AmberHex}>{carried}</color>";

        for (int i = 0; i < stock.Count; i++)
        {
            ShopEntry entry = stock[i];
            string action = entry.Action(player, out bool blocked);
            string colour = "#" + ColorUtility.ToHtmlStringRGB(entry.Colour);
            string actionText = blocked ? $"<color={UiColours.DangerHex}>{action}</color>" : action;
            string price = entry.Price > carried ? $"<color={UiColours.DangerHex}>{entry.Price}</color>" : entry.Price.ToString();
            rows[i].Label.text = $"<pos=1%><color={colour}>{entry.Title}</color><pos=46%><size=75%>{actionText}</size><pos=92%>{price}";
        }

        ShowDetail(menu.Highlighted);
    }

    private void ShowDetail(int index)
    {
        detail.text = shop != null && index >= 0 && index < shop.Stock.Count
            ? shop.Stock[index].Detail
            : "<alpha=#66>HIGHLIGHT AN ITEM FOR DETAILS";
    }

    // ---------- Building ----------

    private static ShopScreen Create()
    {
        GameObject root = CrtPanel.CreateRoot("Shop Screen", out Canvas canvas);

        if (root == null)
            return null;

        ShopScreen screen = root.AddComponent<ShopScreen>();
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

        title = CrtPanel.MakeText(panel.transform, font, "Title", "", 26, new Vector2(0f, -38f), 1f, new Vector2(inner, 36f), Phosphor, 10f, TextAlignmentOptions.Left);
        subtitle = CrtPanel.MakeText(panel.transform, font, "Subtitle", "", 13, new Vector2(0f, -68f), 1f, new Vector2(inner, 20f), dim, 4f, TextAlignmentOptions.Left);
        detail = CrtPanel.MakeText(panel.transform, font, "Detail", "", 12, Vector2.zero, 1f, new Vector2(inner, 40f), dim, 3f, TextAlignmentOptions.Left);
        detail.textWrappingMode = TextWrappingModes.Normal;
        detail.rectTransform.pivot = new Vector2(0.5f, 1f);

        closeButton = MakeButton("Close", new Vector2(260f, 36f), TextAlignmentOptions.Center);
        closeButton.Label.text = "[ CLOSE ]";
        status = CrtPanel.MakeText(panel.transform, font, "Status", "", 12, new Vector2(0f, 28f), 0f, new Vector2(inner, 20f), dim, 4f, TextAlignmentOptions.Center);
    }

    private CrtButton MakeButton(string name, Vector2 size, TextAlignmentOptions alignment)
    {
        CrtButton button = CrtPanel.MakeButton(panel.transform, font, name, size, alignment);
        button.Label.fontSize = 15;
        return button;
    }
}
