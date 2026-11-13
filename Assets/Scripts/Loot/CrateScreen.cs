using System.Collections.Generic;
using UnityEngine;

// The supply crate screen: a CRT list screen (CrtListScreen) listing what's in the crate
// (SupplyCrate), one row per item with its name in its colour and what taking it would do (fit to
// a gun, swap for the gun in that slot, backpack full...). The highlighted item's details show
// under the list. Pick a row to take that item, TAKE ALL to take everything that goes straight in
// (no gun or attachment swaps), CLOSE or Cancel to shut it; what's left stays in the crate. Taking
// the last thing closes it, and the crate sinks away. The player can't move or shoot while it's up.
public class CrateScreen : CrtListScreen
{
    public static bool IsOpen { get; private set; }

    private static CrateScreen instance;

    private SupplyCrate crate;
    private int shownCount = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        IsOpen = false;
        instance = null;
    }

    public static void Open(SupplyCrate crate, PlayerWeapon player)
    {
        if (crate == null || player == null)
            return;

        if (instance == null)
            instance = Create<CrateScreen>("Crate Screen", "TAKE ALL");

        if (instance == null)
            return;

        IsOpen = true;
        instance.crate = crate;
        instance.shownCount = -1;
        instance.Show(player);
        instance.title.text = $"> {crate.Title}";
        instance.Rebuild(0);
    }

    public static void Close()
    {
        IsOpen = false;

        if (instance != null)
        {
            instance.Hide();
            instance.crate = null;
        }
    }

    protected override bool SourceGone => crate == null;
    protected override int EntryCount => crate != null ? crate.Items.Count : 0;
    protected override string DetailOf(int index) => crate.Items[index].Detail;
    protected override void Shut() => Close();

    protected override void Choose(int chosen)
    {
        if (chosen == crate.Items.Count)
            TakeAll();
        else
            TakeOne(crate.Items[chosen]);

        // Emptied: the crate goes (SupplyCrate.Vanish), and the screen with it.
        if (crate.IsEmpty)
        {
            Close();
            return;
        }

        Rebuild(chosen);
    }

    private void TakeOne(CrateItem item)
    {
        string name = item.Title;
        item.Action(player, out bool blocked);

        if (blocked || !crate.Take(item, player))
        {
            status.text = $"> CAN'T TAKE THAT // {item.Action(player, out _)}";
            PlayDeny();
            return;
        }

        status.text = $"> TOOK {StripTags(name)}";
        PlayTaken();
    }

    // Everything that goes straight in: no swapping guns or attachments out of the player's hands.
    private void TakeAll()
    {
        int took = 0;
        int left = 0;

        foreach (CrateItem item in new List<CrateItem>(crate.Items))
        {
            item.Action(player, out bool blocked);
            bool swap = item.Kind == CrateItem.ItemKind.Gun
                ? (item.Weapon.GetComponent<Gun>().EquipSlot == Gun.Slot.Main ? player.Main : player.Sidearm).Gun != null
                : item.Kind == CrateItem.ItemKind.Attachment && AttachmentPickup.Decide(player, item.Attachment, out _) == AttachmentPickup.Take.Swap;

            if (!blocked && !swap && crate.Take(item, player))
                took++;

            if (crate.Holds(item))
                left++;
        }

        if (took == 0)
        {
            status.text = crate.Items.Count == 0 ? "> THE CRATE IS EMPTY" : "> NOTHING ELSE GOES STRAIGHT IN // PICK AN ITEM TO SWAP IT";
            PlayDeny();
            return;
        }

        status.text = left > 0 ? $"> TOOK {took} // {left} LEFT IN THE CRATE (BACKPACK FULL OR A SWAP)" : $"> TOOK {took} // THE CRATE IS EMPTY";
        PlayTaken();
    }

    // One row per item, then TAKE ALL and CLOSE; keeps the highlight near `focus`.
    private void Rebuild(int focus)
    {
        int count = crate.Items.Count;

        // Same row as before where there still is one, else the last item (or CLOSE when empty).
        int first = count == 0 ? 1 : Mathf.Clamp(focus, 0, count - 1);

        if (shownCount >= 0 && focus >= shownCount)
            first = focus - shownCount + count;

        subtitle.text = count == 0
            ? "<alpha=#88>THE CRATE IS EMPTY"
            : "SELECT AN ITEM TO TAKE IT // THE REST STAYS IN THE CRATE";

        LayoutRows(count, first);
        shownCount = count;
        Refresh();
    }

    private void Refresh()
    {
        IReadOnlyList<CrateItem> items = crate.Items;

        for (int i = 0; i < items.Count; i++)
        {
            CrateItem item = items[i];
            string action = item.Action(player, out bool blocked);
            string colour = "#" + ColorUtility.ToHtmlStringRGB(item.Colour);
            string actionText = blocked ? $"<color={UiColours.DangerHex}>{action}</color>" : action;
            string fits = item.Kind == CrateItem.ItemKind.Attachment
                ? $"  <size=70%><alpha=#99>{AttachmentPickup.FitsCarried(player, item.Attachment)}</size><alpha=#FF>"
                : "";
            rows[i].Label.text = $"<pos=1%><color={colour}>{item.Title}</color>{fits}<pos=46%><size=75%>{actionText}</size>";
        }

        ShowDetail(menu.Highlighted);
    }
}
