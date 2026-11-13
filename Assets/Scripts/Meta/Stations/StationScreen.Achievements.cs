using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The intel desk's ACHIEVEMENTS page: a separate panel opened from the desk's ACHIEVEMENTS row
// (StationScreen.IsOpen stays true, so everything the station screen blocks stays blocked). A
// scrolling grid of cards, two columns, four rows in view: each card has a picture of the first
// thing it unlocks (a run item's or gun's pixel sprite, or the room's map symbol), its name, how
// to earn it, what it unlocks and a progress bar. Earned cards are amber, the rest dim phosphor.
// Scrolled with the mouse wheel, or the stick / d-pad / arrow keys; Cancel or BACK returns to the
// desk's upgrades.
public partial class StationScreen
{
    private const int CardColumns = 2;
    private const int CardRowsShown = 4;
    private static readonly Vector2 CardSize = new Vector2(405f, 104f);
    private const float CardGap = 10f;
    private const float CardsTop = 100f;
    private const float StickScrollSpeed = 700f;   // pixels a second with the stick held fully
    private const float IconSize = 84f;

    private GameObject achievementsPanel;
    private RectTransform cardContent;
    private TMP_Text achievementsSubtitle;
    private TMP_Text[] cardTexts;
    private TMP_Text[] cardProgress;
    private CrtMenu achievementsMenu;
    private float scroll;
    private float scrollTarget;
    private bool showingAchievements;

    private static float CardStep => CardSize.y + CardGap;
    private static float ViewportHeight => CardRowsShown * CardStep - CardGap;
    private static int CardRows => (Achievements.All.Length + CardColumns - 1) / CardColumns;
    private static float MaxScroll => Mathf.Max(0f, CardRows * CardStep - CardGap - ViewportHeight);

    // The intel desk's row that opens the page.
    private static string AchievementsRowText()
    {
        return $"<pos=1%>ACHIEVEMENTS<pos=30%><color={UiColours.AmberHex}>{EarnedCount()}/{Achievements.All.Length} EARNED</color>\n"
            + "<size=75%><pos=30%><alpha=#88>THEY UNLOCK NEW ITEMS, GUNS AND ROOMS</size>\n"
            + "<size=80%><alpha=#FF><pos=30%>> VIEW ALL</size>";
    }

    private static int EarnedCount()
    {
        int earned = 0;

        foreach (Achievement a in Achievements.All)
            if (MetaProgress.IsAchieved(a))
                earned++;

        return earned;
    }

    private void ShowAchievements()
    {
        showingAchievements = true;
        openedFrame = Time.frameCount;   // the press that opened it mustn't also press BACK
        panel.SetActive(false);
        achievementsPanel.SetActive(true);
        scroll = scrollTarget = 0f;
        achievementsSubtitle.text = $"{EarnedCount()}/{Achievements.All.Length} EARNED // EACH ONE UNLOCKS NEW ITEMS, GUNS OR ROOMS FOR FUTURE RUNS";

        for (int i = 0; i < Achievements.All.Length; i++)
            FillCard(i);

        achievementsMenu.Open();
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-fit", 3, ProceduralAudio.AttachmentFit)), 0.35f, 1.2f);
    }

    private void HideAchievements()
    {
        showingAchievements = false;

        if (achievementsPanel != null)
            achievementsPanel.SetActive(false);
    }

    // Back to the desk's upgrade rows.
    private void BackFromAchievements()
    {
        HideAchievements();
        openedFrame = Time.frameCount;
        panel.SetActive(true);
        Refresh();
        menu.Open();
    }

    private void UpdateAchievements()
    {
        if (GameInput.Cancel.WasPressedThisFrame() || GameInput.Pause.WasPressedThisFrame() || achievementsMenu.Update() >= 0)
        {
            BackFromAchievements();
            return;
        }

        // Stick / d-pad / arrows glide; each wheel notch moves half a card row.
        float stick = GameInput.Navigate.ReadValue<Vector2>().y;

        if (Mathf.Abs(stick) > 0.3f)
            scrollTarget -= stick * StickScrollSpeed * Time.unscaledDeltaTime;

        float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;

        if (Mathf.Abs(wheel) > 0.01f)
            scrollTarget -= Mathf.Sign(wheel) * CardStep * 0.5f;

        scrollTarget = Mathf.Clamp(scrollTarget, 0f, MaxScroll);
        scroll = Mathf.Lerp(scroll, scrollTarget, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        cardContent.anchoredPosition = new Vector2(0f, scroll);
    }

    // Name (amber once earned), how to earn it, and what it unlocks; the count beside the bar.
    private void FillCard(int index)
    {
        Achievement a = Achievements.All[index];
        bool done = MetaProgress.IsAchieved(a);
        int goal = Achievements.Goal(a);
        int have = Mathf.Min(goal, MetaProgress.StatValue(Achievements.StatFor(a)));
        string name = done ? $"<color={UiColours.AmberHex}>{Achievements.Name(a)}</color>" : Achievements.Name(a);

        cardTexts[index].text = $"<size=19>{name}</size>\n"
            + $"<alpha=#AA>{Achievements.HowToEarn(a).ToUpperInvariant()}<alpha=#FF>\n"
            + $"<alpha=#77>UNLOCKS:<alpha=#FF> {Achievements.UnlocksText(a)}";
        cardProgress[index].text = done ? $"<color={UiColours.AmberHex}>DONE</color>" : $"{have}/{goal}";
    }

    // The card's frame, its icon box and picture, and the progress bar (text is on top).
    private static void DrawCard(VertexHelper vh, int index, Rect rect)
    {
        vh.Clear();
        Achievement a = Achievements.All[index];
        bool done = MetaProgress.IsAchieved(a);
        Color ink = done ? UiColours.Amber : UiColours.WithAlpha(UiColours.Phosphor, 0.45f);

        MapMesh.FillRect(vh, rect, UiColours.WithAlpha(done ? UiColours.Amber : UiColours.Phosphor, done ? 0.08f : 0.04f));
        MapMesh.OutlineRect(vh, rect, 1.5f, ink);

        Rect box = new Rect(rect.xMin + 10f, rect.center.y - IconSize / 2f, IconSize, IconSize);
        MapMesh.OutlineRect(vh, box, 1f, UiColours.WithAlpha(ink, ink.a * 0.6f));
        Rect picture = new Rect(box.xMin + 10f, box.yMin + 10f, box.width - 20f, box.height - 20f);
        DrawUnlockIcon(vh, a, picture, done ? UiColours.Phosphor : UiColours.WithAlpha(UiColours.Phosphor, 0.35f));

        int goal = Achievements.Goal(a);
        float fraction = done ? 1f : Mathf.Clamp01(MetaProgress.StatValue(Achievements.StatFor(a)) / (float)Mathf.Max(1, goal));
        Rect bar = new Rect(box.xMax + 14f, rect.yMin + 12f, rect.xMax - box.xMax - 100f, 6f);
        MapMesh.FillRect(vh, bar, UiColours.WithAlpha(UiColours.Phosphor, 0.15f));
        MapMesh.FillRect(vh, new Rect(bar.xMin, bar.yMin, bar.width * fraction, bar.height), done ? UiColours.Amber : UiColours.Phosphor);
    }

    // The first thing it unlocks: a run item's or gun's sprite, else the room's map symbol.
    private static void DrawUnlockIcon(VertexHelper vh, Achievement a, Rect area, Color ink)
    {
        RunItem[] items = Achievements.ItemsUnlocked(a);

        if (items.Length > 0 && PixelIcon.DrawItem(vh, ItemSprites.RunItem(items[0]), area, ink))
            return;

        string[] guns = Achievements.GunsUnlocked(a);

        if (guns.Length > 0 && PixelIcon.DrawGun(vh, guns[0], area, ink))
            return;

        UnlockRoom[] rooms = Achievements.RoomsUnlocked(a);

        if (rooms.Length > 0)
        {
            RoomType room = (RoomType)Enum.Parse(typeof(RoomType), rooms[0].ToString());
            MapSymbols.DrawMarker(vh, room, area.center, area.width * 0.8f, ink, ink);
            return;
        }

        // Without the art: a plain plate outline, as on the HUD.
        MapMesh.OutlineRect(vh, new Rect(area.xMin + 8f, area.yMin + 8f, area.width - 16f, area.height - 16f), 2f, ink);
    }

    // ---------- Building ----------

    private void BuildAchievementsPage(Transform root)
    {
        float panelHeight = CardsTop + ViewportHeight + 130f;
        achievementsPanel = CrtPanel.CreatePanel(root, new Vector2(PanelWidth, panelHeight));
        Transform page = achievementsPanel.transform;
        Color dim = UiColours.WithAlpha(Phosphor, 0.55f);
        float inner = PanelWidth - 60f;

        CrtPanel.MakeText(page, font, "Title", "> HIDEOUT // INTEL DESK // ACHIEVEMENTS", 26, new Vector2(0f, -38f), 1f, new Vector2(inner, 36f), Phosphor, 10f, TextAlignmentOptions.Left);
        achievementsSubtitle = CrtPanel.MakeText(page, font, "Subtitle", "", 13, new Vector2(0f, -68f), 1f, new Vector2(inner, 20f), dim, 4f, TextAlignmentOptions.Left);
        CrtPanel.MakeText(page, font, "Hint", "> SCROLL WITH THE WHEEL OR STICK // CANCEL TO GO BACK", 12, new Vector2(0f, 28f), 0f, new Vector2(inner, 20f), dim, 4f, TextAlignmentOptions.Center);

        // The viewport clips the cards; the content inside it moves up as it scrolls.
        float width = CardColumns * CardSize.x + (CardColumns - 1) * CardGap;
        RectTransform viewport = CrtPanel.MakeRect(page, "Viewport", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -CardsTop), new Vector2(width, ViewportHeight));
        viewport.gameObject.AddComponent<RectMask2D>();
        cardContent = CrtPanel.MakeRect(viewport, "Cards", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(width, CardRows * CardStep));

        int count = Achievements.All.Length;
        cardTexts = new TMP_Text[count];
        cardProgress = new TMP_Text[count];

        for (int i = 0; i < count; i++)
            BuildCard(i);

        // Where the view is in the list: a thin track right of the cards with a lit thumb.
        RectTransform track = CrtPanel.MakeRect(page, "Scroll Bar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(width / 2f + 12f, -CardsTop), new Vector2(4f, ViewportHeight));
        track.gameObject.AddComponent<CanvasRenderer>();
        CallbackGraphic bar = track.gameObject.AddComponent<CallbackGraphic>();
        bar.Draw = DrawScrollBar;

        CrtButton back = CrtPanel.MakeButton(page, font, "Back", new Vector2(260f, 40f), TextAlignmentOptions.Center);
        back.Label.text = "[ BACK ]";
        back.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -(CardsTop + ViewportHeight + 45f));
        achievementsMenu = new CrtMenu(back);

        achievementsPanel.SetActive(false);
    }

    private void BuildCard(int index)
    {
        int column = index % CardColumns;
        int row = index / CardColumns;
        float x = (column - (CardColumns - 1) / 2f) * (CardSize.x + CardGap);
        RectTransform card = CrtPanel.MakeRect(cardContent, "Card " + index, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -row * CardStep), CardSize);
        card.gameObject.AddComponent<CanvasRenderer>();
        CallbackGraphic graphic = card.gameObject.AddComponent<CallbackGraphic>();
        graphic.Draw = (vh, rect) => DrawCard(vh, index, rect);

        // Text right of the icon box, wrapping; the count sits right of the progress bar.
        float textLeft = 10f + IconSize + 14f;
        RectTransform textRect = CrtPanel.MakeRect(card, "Text", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(textLeft, -8f), new Vector2(CardSize.x - textLeft - 10f, CardSize.y - 26f));
        TextMeshProUGUI text = CrtPanel.MakeLabel(textRect, font, "", 13f, Phosphor, 2f, TextAlignmentOptions.TopLeft);
        text.textWrappingMode = TextWrappingModes.Normal;
        cardTexts[index] = text;

        RectTransform progressRect = CrtPanel.MakeRect(card, "Progress", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 4f), new Vector2(76f, 22f));
        cardProgress[index] = CrtPanel.MakeLabel(progressRect, font, "", 15f, Phosphor, 2f, TextAlignmentOptions.Right);
    }

    private void DrawScrollBar(VertexHelper vh, Rect rect)
    {
        vh.Clear();

        if (MaxScroll <= 0f)
            return;

        float total = CardRows * CardStep - CardGap;
        float thumb = rect.height * ViewportHeight / total;
        float top = rect.yMax - (rect.height - thumb) * (scroll / MaxScroll);
        MapMesh.FillRect(vh, rect, UiColours.WithAlpha(Phosphor, 0.12f));
        MapMesh.FillRect(vh, new Rect(rect.xMin, top - thumb, rect.width, thumb), UiColours.WithAlpha(Phosphor, 0.8f));
    }
}
