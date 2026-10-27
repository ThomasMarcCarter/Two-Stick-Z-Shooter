using TMPro;
using UnityEngine;

// The credits, after the evac chopper lifts off (EvacSequence): a full-screen CRT panel that fades
// in over the world, rolls the credits up the screen (the survivors who made it out, then thanks),
// and ends on THANKS FOR PLAYING with a way back to the hideout or the main menu. The game's paused
// underneath. Built in code the first time it's shown (CrtPanel). Works with mouse or controller
// (CrtMenu); Cancel skips to the end of the roll.
public class CreditsScreen : MonoBehaviour
{
    private const float RollTime = 22f;
    private const float PanelWidth = 1000f;
    private const float PanelHeight = 640f;

    public static bool IsShowing { get; private set; }

    private static CreditsScreen instance;

    private const float RollBox = 1400f;

    private GameObject backdrop;
    private GameObject panel;
    private TMP_Text roll;
    private TMP_Text thanks;
    private CrtButton hideoutButton;
    private CrtButton menuButton;
    private CrtMenu menu;
    private float started;
    private bool rolled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        IsShowing = false;
        instance = null;
    }

    public static void Show()
    {
        if (instance == null)
            instance = Create();

        if (instance == null)
            return;

        IsShowing = true;
        Time.timeScale = 0f;
        instance.Begin();
    }

    private static CreditsScreen Create()
    {
        GameObject root = CrtPanel.CreateRoot("Credits", out Canvas canvas);

        if (root == null)
            return null;

        CreditsScreen screen = root.AddComponent<CreditsScreen>();
        screen.Build(canvas);
        return screen;
    }

    private void Build(Canvas canvas)
    {
        TMP_FontAsset font = CrtPanel.Font(canvas);

        // Black behind it all: the world's gone.
        GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform));
        backdrop.layer = gameObject.layer;
        backdrop.transform.SetParent(transform, false);
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = backdropRect.offsetMax = Vector2.zero;
        backdrop.AddComponent<UnityEngine.UI.Image>().color = Color.black;
        this.backdrop = backdrop;

        panel = CrtPanel.CreatePanel(transform, new Vector2(PanelWidth, PanelHeight));

        // The roll is cut off at the panel's edges.
        panel.AddComponent<UnityEngine.UI.RectMask2D>();
        roll = CrtPanel.MakeText(panel.transform, font, "Roll", "", 20, new Vector2(0f, -PanelHeight), 1f, new Vector2(PanelWidth - 80f, RollBox),
            UiColours.Phosphor, 6f, TextAlignmentOptions.Top);
        thanks = CrtPanel.MakeText(panel.transform, font, "Thanks", "> THANKS FOR PLAYING", 34, new Vector2(0f, -PanelHeight / 2f + 40f), 1f,
            new Vector2(PanelWidth - 80f, 60f), UiColours.Amber, 8f, TextAlignmentOptions.Center);
        hideoutButton = CrtPanel.MakeButton(panel.transform, font, "Hideout", new Vector2(320f, 46f), TextAlignmentOptions.Center);
        hideoutButton.Label.text = "[ RETURN TO HIDEOUT ]";
        hideoutButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -PanelHeight / 2f - 50f);
        menuButton = CrtPanel.MakeButton(panel.transform, font, "Menu", new Vector2(320f, 46f), TextAlignmentOptions.Center);
        menuButton.Label.text = "[ MAIN MENU ]";
        menuButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -PanelHeight / 2f - 110f);
        menu = new CrtMenu(hideoutButton, menuButton);
    }

    private void Begin()
    {
        roll.text = RollText();
        roll.gameObject.SetActive(true);
        started = Time.unscaledTime;
        rolled = false;
        thanks.gameObject.SetActive(false);
        hideoutButton.gameObject.SetActive(false);
        menuButton.gameObject.SetActive(false);
        panel.SetActive(true);
        backdrop.SetActive(true);
    }

    // Who made it out, then the thanks.
    private static string RollText()
    {
        System.Text.StringBuilder text = new System.Text.StringBuilder();
        text.AppendLine($"<size=150%>{Application.productName.ToUpperInvariant()}</size>\n");
        text.AppendLine($"<color={UiColours.AmberHex}>// EVAC COMPLETE //</color>\n");
        text.AppendLine("THE CHOPPER CLEARED THE CITY AT DAWN.\nBEHIND IT, THE DEAD KEPT WALKING.\nAHEAD, FOR THE FIRST TIME IN A LONG TIME, SOMEWHERE SAFE.\n\n");
        text.AppendLine($"<color={UiColours.AmberHex}>// THE SURVIVORS //</color>\n");

        foreach (NpcKind kind in Npcs.All)
        {
            if (MetaProgress.IsRescued(kind))
                text.AppendLine($"{Npcs.Name(kind)}  <alpha=#99>// {Npcs.Role(kind)}<alpha=#FF>");
        }

        text.AppendLine("AND YOU.\n\n");
        text.AppendLine($"<color={UiColours.AmberHex}>// THE RECORD //</color>\n");
        text.AppendLine($"RUNS  {MetaProgress.Runs}\nWINS  {MetaProgress.Victories}\nZOMBIES PUT DOWN  {MetaProgress.Kills}\n\n");
        text.AppendLine($"<color={UiColours.AmberHex}>// MADE WITH //</color>\n");
        text.AppendLine("UNITY\nVT323 FONT (SIL OPEN FONT LICENSE)\n");
        return text.ToString();
    }

    private void Update()
    {
        if (!IsShowing)
            return;

        float t = (Time.unscaledTime - started) / RollTime;

        if (!rolled && (t >= 1f || GameInput.Cancel.WasPressedThisFrame()))
            EndRoll();

        if (!rolled)
        {
            // The box is placed by its middle and the text sits at its top: in from the panel's
            // bottom edge, out past its top.
            float from = -PanelHeight - RollBox / 2f;
            float to = roll.preferredHeight - RollBox / 2f + 20f;
            roll.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(from, to, t));
            return;
        }

        switch (menu.Update())
        {
            case 0:
                Close();
                RunFlow.ExitToHideout();
                break;
            case 1:
                Close();
                RunFlow.ExitToMainMenu();
                break;
        }
    }

    private void EndRoll()
    {
        rolled = true;
        roll.gameObject.SetActive(false);
        thanks.gameObject.SetActive(true);
        hideoutButton.gameObject.SetActive(true);
        menuButton.gameObject.SetActive(true);
        menu.Open();
    }

    private void Close()
    {
        IsShowing = false;
        panel.SetActive(false);
        backdrop.SetActive(false);
    }
}
