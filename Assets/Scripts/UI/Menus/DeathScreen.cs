using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shown when the player dies. First the death plays out in the room (BeginDeath: the player
// falls, time slows for a moment), then the screen fades in over it: its backdrop is a
// translucent dark red, so the room the player died in stays visible behind the text. It pauses
// the game, ends the run (MetaProgress), loses the scrap and materials not sent home from an
// uplink (both listed on the notice) and offers a new run, the hideout or the main menu. Works with mouse or
// controller (CrtMenu).
// Run report (2026-11-12): beside the buttons, a framed recap of the run: what killed the
// player, how long they lasted, kills, floors cleared and the items they held. Everything is
// laid out as a column down the left of the screen over a shade that darkens that side, with
// the text and buttons solid (the scene's layout is moved there in Awake, LayOutLeft).
public class DeathScreen : MonoBehaviour
{
    public static bool IsShowing { get; private set; }

    // The death is playing out, before the screen comes up (controls are already locked).
    public static bool IsDying { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private CrtButton newRunButton;
    [SerializeField] private CrtButton hideoutButton;
    [SerializeField] private CrtButton mainMenuButton;
    [SerializeField] private TMP_Text notice;

    [Header("Death sequence")]
    [Tooltip("Seconds (real time) the death plays out in the room before the screen fades in.")]
    [SerializeField] private float delay = 1.4f;
    [Tooltip("Game speed while the death plays out.")]
    [SerializeField, Range(0.05f, 1f)] private float slowMotion = 0.4f;
    [Tooltip("Seconds the screen takes to fade in.")]
    [SerializeField] private float fadeTime = 0.6f;
    [Tooltip("How opaque the backdrop is, so the room shows through behind it.")]
    [SerializeField, Range(0f, 1f)] private float backdropAlpha = 0.6f;

    private CrtMenu menu;
    private CanvasGroup group;
    private TMP_Text report;

    private const float LeftMargin = 90f;
    private static readonly Color Under = new Color(0.04f, 0f, 0f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        IsShowing = false;
        IsDying = false;
    }

    private void Awake()
    {
        IsShowing = false;
        IsDying = false;
        panel.SetActive(false);
        menu = new CrtMenu(newRunButton, hideoutButton, mainMenuButton);

        if (panel.TryGetComponent(out Image backdrop))
        {
            Color colour = backdrop.color;
            colour.a = backdropAlpha;
            backdrop.color = colour;
        }

        group = panel.GetComponent<CanvasGroup>();

        if (group == null)
            group = panel.AddComponent<CanvasGroup>();

        BuildReport();
    }

    // The scene's title, subtitle, buttons and notice move into a column down the left, solid,
    // over a shade darkening that side of the screen.
    private void LayOutLeft()
    {
        CrtPanel.MakeSideShade(panel.transform, new Color(0f, 0f, 0f, 0.92f), 0.75f).transform.SetAsFirstSibling();

        foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
            if (text.GetComponentInParent<CrtButton>(true) == null)
            {
                Color colour = text.color;
                text.color = CrtPanel.Solid(Under, colour, colour.a);
            }

        PlaceLeft(TitleOf("Title"), new Vector2(LeftMargin, 150f), TextAlignmentOptions.Left);
        PlaceLeft(TitleOf("Subtitle"), new Vector2(LeftMargin, 100f), TextAlignmentOptions.Left);
        PlaceLeft(notice.rectTransform, new Vector2(LeftMargin, -150f), TextAlignmentOptions.TopLeft);
        notice.rectTransform.pivot = new Vector2(0f, 1f);

        CrtButton[] buttons = { newRunButton, hideoutButton, mainMenuButton };

        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].MakeOpaque(Under);
            buttons[i].Label.alignment = TextAlignmentOptions.Left;
            buttons[i].Label.margin = new Vector4(18f, 0f, 0f, 0f);

            if (buttons[i].transform is RectTransform rect)
                PlaceLeft(rect, new Vector2(LeftMargin, 30f - i * 60f), null);
        }
    }

    // Everything on the Canvas under this screen (the HUD) goes for good: the scene reloads
    // whichever button is pressed.
    private void HideHud()
    {
        for (int i = 0; i < transform.GetSiblingIndex(); i++)
        {
            Transform hud = transform.parent.GetChild(i);
            CanvasGroup hudGroup = hud.GetComponent<CanvasGroup>();

            if (hudGroup == null)
                hudGroup = hud.gameObject.AddComponent<CanvasGroup>();

            hudGroup.alpha = 0f;
        }
    }

    private RectTransform TitleOf(string name)
    {
        Transform found = panel.transform.Find(name);
        return found as RectTransform;
    }

    // Anchors a scene element to the left middle of the screen, its left edge at `position`.
    private static void PlaceLeft(RectTransform rect, Vector2 position, TextAlignmentOptions? alignment)
    {
        if (rect == null)
            return;

        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;

        if (alignment.HasValue && rect.TryGetComponent(out TMP_Text text))
            text.alignment = alignment.Value;
    }

    // The report frame sits right of the buttons, its top level with the first.
    private void BuildReport()
    {
        LayOutLeft();

        Vector2 centre = new Vector2(0.5f, 0.5f);
        Vector2 left = new Vector2(0f, 0.5f);
        RectTransform frame = CrtPanel.MakeRect(panel.transform, "Run Report", left, new Vector2(0f, 1f), new Vector2(LeftMargin + 370f, 52f), new Vector2(400f, 166f));
        Image fill = frame.gameObject.AddComponent<Image>();
        fill.color = CrtPanel.Solid(Under, UiColours.PanelBackground, 1f);
        fill.raycastTarget = false;

        // A phosphor rule down its left edge.
        RectTransform rule = CrtPanel.MakeRect(frame, "Rule", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(2f, 166f));
        Image line = rule.gameObject.AddComponent<Image>();
        line.color = CrtPanel.Solid(Under, UiColours.Danger, 0.8f);
        line.raycastTarget = false;

        RectTransform textRect = CrtPanel.MakeRect(frame, "Text", centre, centre, Vector2.zero, new Vector2(370f, 150f));
        report = CrtPanel.MakeLabel(textRect, notice.font, "", 15f, UiColours.Phosphor, 3f, TextAlignmentOptions.TopLeft);
        report.textWrappingMode = TextWrappingModes.Normal;
        report.lineSpacing = 6f;
    }

    private void FillReport(FloorGenerator floor)
    {
        PlayerHealth health = PlayerWeapon.Current != null ? PlayerWeapon.Current.GetComponent<PlayerHealth>() : null;
        string killer = health != null && !string.IsNullOrEmpty(health.KilledBy) ? health.KilledBy : "UNKNOWN";
        float seconds = floor != null ? Mathf.Max(0f, Time.time - floor.RunStartTime) : 0f;
        string items = "NONE";

        if (PlayerItems.Current != null && PlayerItems.Current.Held.Count > 0)
        {
            string[] names = new string[PlayerItems.Current.Held.Count];

            for (int i = 0; i < names.Length; i++)
                names[i] = RunItems.Name(PlayerItems.Current.Held[i]);

            items = string.Join(", ", names);
        }

        report.text = $"<color={UiColours.AmberHex}>> RUN REPORT</color>\n"
            + Row("KILLED BY", $"<color={UiColours.DangerHex}>{killer.ToUpperInvariant()}</color>")
            + Row("SURVIVED", $"{(int)seconds / 60}:{(int)seconds % 60:00}")
            + Row("KILLS", floor != null ? floor.EnemiesKilled.ToString() : "0")
            + Row("FLOORS CLEARED", floor != null ? floor.FloorsCleared.ToString() : "0")
            + Row("ITEMS", items.ToUpperInvariant());
    }

    // A label in dim phosphor, then its value indented so a long one wraps under itself.
    private static string Row(string label, string value)
    {
        string dim = ColorUtility.ToHtmlStringRGBA(CrtPanel.Solid(Under, UiColours.Phosphor, 0.55f));
        return $"<color=#{dim}>{label}</color><indent=40%>{value}</indent>\n";
    }

    // The player just died: let it play out in slow motion, then bring the screen up.
    public void BeginDeath()
    {
        if (IsDying || IsShowing)
            return;

        IsDying = true;
        StartCoroutine(PlayDeath());
    }

    private IEnumerator PlayDeath()
    {
        Time.timeScale = slowMotion;
        yield return new WaitForSecondsRealtime(delay);
        IsDying = false;
        Show();
    }

    public void Show()
    {
        IsShowing = true;
        Time.timeScale = 0f;

        // Scrap not sent home from an uplink dies with the player; what was sent home is safe.
        FloorGenerator floor = FindAnyObjectByType<FloorGenerator>();
        int lostScrap = floor != null ? MetaProgress.CarriedScrap(floor.FloorsCleared, floor.EnemiesKilled) : 0;
        int sentScrap = MetaProgress.ScrapSentHome;

        FillReport(floor);

        if (floor != null)
            MetaProgress.EndRun(false, floor.FloorsCleared, floor.FloorNumber, floor.EnemiesKilled);

        notice.text = (floor != null ? $"<size=80%>LAST KNOWN POSITION: {floor.LocationName}</size>\n" : "")
            + $"<color={UiColours.AmberHex}>{sentScrap} SCRAP SENT HOME THIS RUN // {MetaProgress.Scrap} BANKED FOR THE HIDEOUT</color>";

        if (lostScrap > 0)
            notice.text += $"\n<size=80%><color={UiColours.DangerHex}>LOST SCRAP NOT SENT HOME:</color> {lostScrap}</size>";

        // Materials never sent home from an uplink die with the player.
        PlayerWeapon player = PlayerWeapon.Current;
        string lost = player != null ? Materials.Describe(Backpack.For(player.gameObject).TakeAllMaterials()) : "";

        if (lost.Length > 0)
            notice.text += $"\n<size=80%><color={UiColours.DangerHex}>LOST MATERIALS NOT SENT HOME:</color> {lost}</size>";

        // So is a truck part that never made it out of the supply room.
        if (player != null && StorySlot.For(player.gameObject).Take() is StoryItem part)
            notice.text += $"\n<size=80%><color={UiColours.DangerHex}>LOST:</color> {StoryItems.Name(part)}</size>";
        panel.SetActive(true);
        HideHud();
        menu.Open();
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / fadeTime;
            yield return null;
        }

        group.alpha = 1f;
    }

    private void Update()
    {
        if (!IsShowing)
            return;

        switch (menu.Update())
        {
            case 0:
                RunFlow.RestartRun();
                break;
            case 1:
                RunFlow.ExitToHideout();
                break;
            case 2:
                RunFlow.ExitToMainMenu();
                break;
        }
    }
}
