using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The powerup case's screen: "CHOOSE ONE:" over the case's items as large cards in a row, each with
// its picture, name and what it does. Picking a card takes it and the others go with the case;
// Cancel leaves the case as it is for later. The highlighted card stands a little larger. The
// player can't move or shoot while it's up. Built in code the first time it opens (CrtPanel).
public class PowerupScreen : MonoBehaviour
{
    private const int CardCount = 3;
    private const float CardGap = 24f;
    private const float HighlightScale = 1.06f;
    private static readonly Vector2 CardSize = new Vector2(300f, 350f);
    private static readonly Vector2 PanelSize = new Vector2(1040f, 520f);

    public static bool IsOpen { get; private set; }

    private static PowerupScreen instance;

    private readonly CrtButton[] cards = new CrtButton[CardCount];
    private readonly TMP_Text[] details = new TMP_Text[CardCount];
    private readonly RunItem[] shown = new RunItem[CardCount];
    private PowerupCase powerup;
    private PlayerWeapon player;
    private GameObject panel;
    private TMP_Text hint;
    private CrtMenu menu;
    private int openedFrame;
    private int count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        IsOpen = false;
        instance = null;
    }

    public static void Open(PowerupCase powerup, PlayerWeapon player)
    {
        if (powerup == null || player == null)
            return;

        if (instance == null)
            instance = Create();

        if (instance == null)
            return;

        IsOpen = true;
        instance.openedFrame = Time.frameCount;
        instance.powerup = powerup;
        instance.player = player;
        instance.panel.SetActive(true);
        instance.Fill();
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("inventory-lift", 2, i => ProceduralAudio.BackpackRustle(300 + i))), 0.45f, 0.8f);
    }

    public static void Close()
    {
        IsOpen = false;

        if (instance != null)
        {
            instance.panel.SetActive(false);
            instance.powerup = null;
        }
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (powerup == null || player == null || DeathScreen.IsShowing)
        {
            Close();
            return;
        }

        // Don't let the Interact press that opened it also pick something.
        if (Time.frameCount == openedFrame)
            return;

        if (GameInput.Cancel.WasPressedThisFrame() || GameInput.Pause.WasPressedThisFrame())
        {
            Close();
            return;
        }

        int chosen = menu.Update();

        for (int i = 0; i < count; i++)
            cards[i].transform.localScale = Vector3.one * (menu.Highlighted == i ? HighlightScale : 1f);

        if (chosen < 0)
            return;

        powerup.Choose(shown[chosen], player);
        Close();
    }

    // Lays out the case's items, centred however many it holds.
    private void Fill()
    {
        IReadOnlyList<RunItem> offers = powerup.Offers;
        count = Mathf.Min(CardCount, offers.Count);
        CrtButton[][] columns = new CrtButton[count][];

        for (int i = 0; i < CardCount; i++)
        {
            bool used = i < count;
            cards[i].gameObject.SetActive(used);

            if (!used)
                continue;

            shown[i] = offers[i];
            RectTransform rect = cards[i].GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2((i - (count - 1) / 2f) * (CardSize.x + CardGap), -20f);
            rect.localScale = Vector3.one;
            cards[i].Label.text = RunItems.Name(shown[i]);
            details[i].text = RunItems.Describe(shown[i]);
            columns[i] = new[] { cards[i] };
        }

        hint.text = $"<alpha=#88>[{GameInput.Label(GameInput.Cancel)}] LEAVE IT FOR NOW";
        menu = new CrtMenu(columns, 0, 0);
        menu.Open();
    }

    // ---------- Building ----------

    private static PowerupScreen Create()
    {
        GameObject root = CrtPanel.CreateRoot("Powerup Screen", out Canvas canvas);

        if (root == null)
            return null;

        PowerupScreen screen = root.AddComponent<PowerupScreen>();
        screen.Build(root.transform, canvas);
        screen.panel.SetActive(false);
        return screen;
    }

    private void Build(Transform root, Canvas canvas)
    {
        TMP_FontAsset font = CrtPanel.Font(canvas);
        panel = CrtPanel.CreatePanel(root, PanelSize);
        float inner = PanelSize.x - 60f;

        CrtPanel.MakeText(panel.transform, font, "Title", "CHOOSE ONE:", 40, new Vector2(0f, -48f), 1f, new Vector2(inner, 50f), UiColours.Phosphor, 10f, TextAlignmentOptions.Center);
        hint = CrtPanel.MakeText(panel.transform, font, "Hint", "", 13, new Vector2(0f, 24f), 0f, new Vector2(inner, 20f), UiColours.Phosphor, 4f, TextAlignmentOptions.Center);

        for (int i = 0; i < CardCount; i++)
            BuildCard(i, font);
    }

    // A big button: the picture up top, the name under it (the button's own label), then what it does.
    private void BuildCard(int index, TMP_FontAsset font)
    {
        RectTransform rect = CrtPanel.MakeRect(panel.transform, "Card " + index, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, CardSize);
        CrtButton card = CrtPanel.MakeButton(rect, font, 22f, 3f, TextAlignmentOptions.Center, 12f, 0.05f, 0.16f);
        cards[index] = card;

        RectTransform name = card.Label.rectTransform;
        name.anchorMin = name.anchorMax = new Vector2(0.5f, 1f);
        name.pivot = new Vector2(0.5f, 1f);
        name.anchoredPosition = new Vector2(0f, -176f);
        name.sizeDelta = new Vector2(CardSize.x - 20f, 34f);
        card.Label.enableAutoSizing = true;
        card.Label.fontSizeMin = 12f;
        card.Label.fontSizeMax = 22f;

        RectTransform icon = CrtPanel.MakeRect(rect, "Picture", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(144f, 144f));
        icon.gameObject.AddComponent<CanvasRenderer>();
        CallbackGraphic picture = icon.gameObject.AddComponent<CallbackGraphic>();
        picture.raycastTarget = false;
        picture.Draw = (vh, area) => DrawPicture(vh, index, area);

        RectTransform detail = CrtPanel.MakeRect(rect, "Detail", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -216f), new Vector2(CardSize.x - 32f, 120f));
        TextMeshProUGUI text = CrtPanel.MakeLabel(detail, font, "", 15f, UiColours.WithAlpha(UiColours.Phosphor, 0.75f), 2f, TextAlignmentOptions.Top);
        text.textWrappingMode = TextWrappingModes.Normal;
        details[index] = text;
    }

    private void DrawPicture(VertexHelper vh, int index, Rect area)
    {
        vh.Clear();

        if (PixelIcon.DrawItem(vh, ItemSprites.RunItem(shown[index]), area, UiColours.Phosphor))
            return;

        // Without the art: a plain plate outline, as on the HUD.
        MapMesh.OutlineRect(vh, new Rect(area.xMin + 40f, area.yMin + 20f, area.width - 80f, area.height - 40f), 3f, UiColours.Phosphor);
    }
}
