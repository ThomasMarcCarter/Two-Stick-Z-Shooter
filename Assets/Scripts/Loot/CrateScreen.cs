using System.Collections.Generic;
using TMPro;
using UnityEngine;

// The supply crate screen: a CRT panel listing what's in the crate (SupplyCrate), one row per
// item with its name in its colour and what taking it would do (fit to a gun, swap for the gun
// in that slot, backpack full...). The highlighted item's details show under the list. Pick a
// row to take that item, TAKE ALL to take everything that goes straight in (no gun or attachment
// swaps), CLOSE or Cancel to shut it; what's left stays in the crate. Taking the last thing
// closes it, and the crate sinks away. The player can't move or
// shoot while it's up. Built in code the first time it opens (CrtPanel).
public class CrateScreen : MonoBehaviour
{
    private static readonly Color Phosphor = UiColours.Phosphor;
    private const float PanelWidth = 900f;
    private static readonly Vector2 RowSize = new Vector2(820f, 34f);
    private const float RowStep = 38f;
    private const float ListTop = 100f;

    public static bool IsOpen { get; private set; }

    private static CrateScreen instance;

    private SupplyCrate crate;
    private PlayerWeapon player;
    private GameObject panel;
    private TMP_Text title;
    private TMP_Text subtitle;
    private TMP_Text detail;
    private TMP_Text status;
    private readonly List<CrtButton> rows = new List<CrtButton>();
    private CrtButton takeAllButton;
    private CrtButton closeButton;
    private CrtMenu menu;
    private TMP_FontAsset font;
    private int openedFrame;
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
            instance = Create();

        if (instance == null)
            return;

        IsOpen = true;
        instance.openedFrame = Time.frameCount;
        instance.crate = crate;
        instance.player = player;
        instance.shownCount = -1;
        instance.panel.SetActive(true);
        instance.title.text = $"> {crate.Title}";
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
            instance.crate = null;
        }
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        // The crate went (floor changed) or the player died.
        if (crate == null || player == null || DeathScreen.IsShowing)
        {
            Close();
            return;
        }

        // Don't let the Interact press that opened it also take something.
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

        int count = crate.Items.Count;

        if (chosen == count + 1)
        {
            Close();
            return;
        }

        if (chosen == count)
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
            Deny();
            return;
        }

        status.text = $"> TOOK {StripTags(name)}";
        Taken();
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
            Deny();
            return;
        }

        status.text = left > 0 ? $"> TOOK {took} // {left} LEFT IN THE CRATE (BACKPACK FULL OR A SWAP)" : $"> TOOK {took} // THE CRATE IS EMPTY";
        Taken();
    }

    private static void Taken()
    {
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("inventory-lift", 2, i => ProceduralAudio.BackpackRustle(300 + i))), 0.45f, 1.1f);
    }

    private static void Deny()
    {
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-deny", 1, i => ProceduralAudio.AttachmentDeny())), 0.4f);
    }

    private static string StripTags(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "");
    }

    // ---------- Rows ----------

    // One row per item, then TAKE ALL and CLOSE; keeps the highlight near `focus`.
    private void Rebuild(int focus)
    {
        IReadOnlyList<CrateItem> items = crate.Items;
        int count = items.Count;

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
        takeAllButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -buttonsTop);
        closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -buttonsTop - 44f);
        detail.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -ListTop - Mathf.Max(1, count) * RowStep + 4f);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelWidth, buttonsTop + 44f + 36f + 36f);

        subtitle.text = count == 0
            ? "<alpha=#88>THE CRATE IS EMPTY"
            : "SELECT AN ITEM TO TAKE IT // THE REST STAYS IN THE CRATE";

        List<CrtButton> all = new List<CrtButton>();

        for (int i = 0; i < count; i++)
            all.Add(rows[i]);

        all.Add(takeAllButton);
        all.Add(closeButton);

        // Same row as before where there still is one, else the last item (or CLOSE when empty).
        int first = count == 0 ? 1 : Mathf.Clamp(focus, 0, count - 1);

        if (shownCount >= 0 && focus >= shownCount)
            first = focus - shownCount + count;

        menu = new CrtMenu(new[] { all.ToArray() }, 0, Mathf.Clamp(first, 0, all.Count - 1));
        menu.Open();
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

    private void ShowDetail(int index)
    {
        detail.text = crate != null && index >= 0 && index < crate.Items.Count
            ? crate.Items[index].Detail
            : "<alpha=#66>HIGHLIGHT AN ITEM FOR DETAILS";
    }

    // ---------- Building ----------

    private static CrateScreen Create()
    {
        GameObject root = CrtPanel.CreateRoot("Crate Screen", out Canvas canvas);

        if (root == null)
            return null;

        CrateScreen screen = root.AddComponent<CrateScreen>();
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

        takeAllButton = MakeButton("Take All", new Vector2(260f, 36f), TextAlignmentOptions.Center);
        takeAllButton.Label.text = "[ TAKE ALL ]";
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
