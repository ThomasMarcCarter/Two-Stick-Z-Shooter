using System.Collections.Generic;
using UnityEngine;

// The shop screen: a CRT list screen (CrtListScreen) listing a shop counter's stock (ShopCounter),
// one row per item with its name in its colour, what buying it does and its price in scrap. Carried
// scrap shows at the top and updates as it's spent. Pick a row to buy it (too little scrap or a full
// backpack is refused with a message), CLOSE or Cancel to shut it. Bought rows go. The player can't
// move or shoot while it's up.
public class ShopScreen : CrtListScreen
{
    public static bool IsOpen { get; private set; }

    private static ShopScreen instance;

    private ShopCounter shop;

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
            instance = Create<ShopScreen>("Shop Screen");

        if (instance == null)
            return;

        IsOpen = true;
        instance.shop = shop;
        instance.Show(player);
        instance.title.text = "> SHOP";
        instance.Rebuild(0);
    }

    public static void Close()
    {
        IsOpen = false;

        if (instance != null)
        {
            instance.Hide();
            instance.shop = null;
        }
    }

    protected override bool SourceGone => shop == null;
    protected override int EntryCount => shop != null ? shop.Stock.Count : 0;
    protected override string DetailOf(int index) => shop.Stock[index].Detail;
    protected override void Shut() => Close();

    protected override void Choose(int chosen)
    {
        bool bought = shop.Buy(shop.Stock[chosen], player, out string message);
        status.text = "> " + StripTags(message);

        if (bought)
        {
            PlayTaken();
            Rebuild(chosen);
        }
        else
        {
            PlayDeny();
        }
    }

    // One row per item for sale, then CLOSE; keeps the highlight near `focus`.
    private void Rebuild(int focus)
    {
        int count = shop.Stock.Count;
        LayoutRows(count, count == 0 ? 0 : Mathf.Clamp(focus, 0, count - 1));
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
}
