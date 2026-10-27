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
