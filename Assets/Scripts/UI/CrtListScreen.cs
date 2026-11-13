using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Base for the CRT list screens (CrateScreen, ShopScreen): a panel with a title, a subtitle, one
// button row per entry, the highlighted entry's details under the list, footer buttons (the last
// is always CLOSE) and a status line at the bottom. It handles the shared running: shutting when
// its source goes or the player dies, ignoring the Interact press that opened it, Cancel/Pause and
// CLOSE, the row layout and the open/take/deny sounds. A subclass says how many entries there are,
// what each one's detail is, and what picking one (or a footer button before CLOSE) does.
// Built in code the first time it opens (CrtPanel).
public abstract class CrtListScreen : MonoBehaviour
{
    protected static readonly Color Phosphor = UiColours.Phosphor;
    protected const float PanelWidth = 900f;
    private static readonly Vector2 RowSize = new Vector2(820f, 34f);
    private static readonly Vector2 FooterSize = new Vector2(260f, 36f);
    private const float RowStep = 38f;
    private const float ListTop = 100f;
    private const float FooterStep = 44f;

    protected PlayerWeapon player;
    protected TMP_Text title;
    protected TMP_Text subtitle;
    protected TMP_Text status;
    protected readonly List<CrtButton> rows = new List<CrtButton>();
    protected CrtMenu menu;

    private GameObject panel;
    private TMP_Text detail;
    private CrtButton[] footer;
    private TMP_FontAsset font;
    private int openedFrame;

    // ---------- What a subclass fills in ----------

    // The crate or shop went (floor changed), so the screen shuts.
    protected abstract bool SourceGone { get; }

    // Entries listed as rows (before the footer buttons).
    protected abstract int EntryCount { get; }

    protected abstract string DetailOf(int index);

    // A row (0..EntryCount-1) or a footer button other than CLOSE (EntryCount + n) was picked.
    protected abstract void Choose(int chosen);

    // The subclass's static Close (clears its IsOpen).
    protected abstract void Shut();

    // ---------- Shared running ----------

    // Builds a screen of type T on the HUD canvas with these footer labels ("CLOSE" is added last).
    protected static T Create<T>(string name, params string[] footerLabels) where T : CrtListScreen
    {
        GameObject root = CrtPanel.CreateRoot(name, out Canvas canvas);

        if (root == null)
            return null;

        T screen = root.AddComponent<T>();
        screen.Build(root.transform, canvas, footerLabels);
        screen.panel.SetActive(false);
        return screen;
    }

    // Shows the panel for `player`; the subclass sets its source first, then calls Rebuild.
    protected void Show(PlayerWeapon who)
    {
        openedFrame = Time.frameCount;
        player = who;
        panel.SetActive(true);
        status.text = "";
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("inventory-lift", 2, i => ProceduralAudio.BackpackRustle(300 + i))), 0.45f, 0.8f);
    }

    protected void Hide()
    {
        panel.SetActive(false);
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf)
            return;

        if (SourceGone || player == null || DeathScreen.IsShowing)
        {
            Shut();
            return;
        }

        // Don't let the Interact press that opened it also pick something.
        if (Time.frameCount == openedFrame)
            return;

        if (GameInput.Cancel.WasPressedThisFrame() || GameInput.Pause.WasPressedThisFrame())
        {
            Shut();
            return;
        }

        int chosen = menu.Update();
        ShowDetail(menu.Highlighted);

        if (chosen < 0)
            return;

        if (chosen >= EntryCount + footer.Length - 1)
            Shut();
        else
            Choose(chosen);
    }

    // Lays out `count` rows then the footer buttons, resizes the panel and opens the menu on
    // `first` (clamped). Row labels are the subclass's to fill.
    protected void LayoutRows(int count, int first)
    {
        while (rows.Count < count)
            rows.Add(MakeButton("Item " + rows.Count, RowSize, TextAlignmentOptions.Left));

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < count;
            rows[i].gameObject.SetActive(used);

            if (used)
                rows[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -ListTop - i * RowStep);
        }

        float listBottom = ListTop + Mathf.Max(1, count) * RowStep;
        float buttonsTop = listBottom + 70f;

        for (int i = 0; i < footer.Length; i++)
            footer[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -buttonsTop - i * FooterStep);

        detail.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -listBottom + 4f);
        panel.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelWidth, buttonsTop + FooterStep + 72f);

        List<CrtButton> all = new List<CrtButton>(rows.GetRange(0, count));
        all.AddRange(footer);
        menu = new CrtMenu(new[] { all.ToArray() }, 0, Mathf.Clamp(first, 0, all.Count - 1));
        menu.Open();
    }

    protected void ShowDetail(int index)
    {
        detail.text = !SourceGone && index >= 0 && index < EntryCount
            ? DetailOf(index)
            : "<alpha=#66>HIGHLIGHT AN ITEM FOR DETAILS";
    }

    // "<color=#..>NAME</color>" in plain text, for status lines.
    protected static string StripTags(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "");
    }

    protected static void PlayTaken()
    {
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("inventory-lift", 2, i => ProceduralAudio.BackpackRustle(300 + i))), 0.45f, 1.1f);
    }

    protected static void PlayDeny()
    {
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-deny", 1, i => ProceduralAudio.AttachmentDeny())), 0.4f);
    }

    // ---------- Building ----------

    private void Build(Transform root, Canvas canvas, string[] footerLabels)
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

        footer = new CrtButton[footerLabels.Length + 1];

        for (int i = 0; i < footer.Length; i++)
        {
            string label = i < footerLabels.Length ? footerLabels[i] : "CLOSE";
            footer[i] = MakeButton(label, FooterSize, TextAlignmentOptions.Center);
            footer[i].Label.text = $"[ {label} ]";
        }

        status = CrtPanel.MakeText(panel.transform, font, "Status", "", 12, new Vector2(0f, 28f), 0f, new Vector2(inner, 20f), dim, 4f, TextAlignmentOptions.Center);
    }

    private CrtButton MakeButton(string name, Vector2 size, TextAlignmentOptions alignment)
    {
        CrtButton button = CrtPanel.MakeButton(panel.transform, font, name, size, alignment);
        button.Label.fontSize = 15;
        return button;
    }
}
