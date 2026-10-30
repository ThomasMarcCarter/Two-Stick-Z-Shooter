using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// The training prompts on a new profile's first floor (FloorGenerator.Tutorial.cs builds the floor):
// a small panel low on the HUD, one instruction at a time, each ticked off by the player actually
// doing it. Moving, aiming and firing, rolling and sneaking in the spawn room, then the supply crate
// next door, the combat room and the hatch home. Nothing is held back while it runs: going straight
// for the fight jumps the prompts ahead. Built in code on the HUD canvas by the FloorGenerator.
public class Tutorial : MonoBehaviour
{
    private const float MoveTime = 0.8f;      // seconds of walking before Move counts as learned

    private static class Step
    {
        public const int Move = 0;
        public const int Fire = 1;
        public const int Roll = 2;
        public const int Sneak = 3;
        public const int Crate = 4;
        public const int EnterFight = 5;
        public const int Fight = 6;
        public const int Hatch = 7;
    }

    private static Tutorial instance;

    private FloorGenerator floor;
    private Room combatRoom;
    private SupplyCrate crate;
    private CanvasGroup group;
    private TMP_Text body;
    private int step;
    private float moved;
    private bool sawSneak;
    private bool? shownForGamepad;
    private int shownStep = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        instance = null;
    }

    // Called once a floor is built. Reaching the hideout, by any way, means the profile's done with it.
    public static void Attach(FloorGenerator generator)
    {
        if (generator.IsHideout && !MetaProgress.TutorialDone)
            MetaProgress.FinishTutorial();

        if (!generator.IsTutorial)
        {
            if (instance != null)
                Destroy(instance.gameObject);

            return;
        }

        if (instance == null)
            instance = Create();

        if (instance != null)
            instance.Begin(generator);
    }

    private static Tutorial Create()
    {
        EquipmentHud hud = FindAnyObjectByType<EquipmentHud>();
        Canvas canvas = hud != null ? hud.canvas : null;

        if (canvas == null)
            return null;

        GameObject root = new GameObject("Tutorial", typeof(RectTransform));
        root.layer = canvas.gameObject.layer;
        root.transform.SetParent(canvas.transform, false);

        // With the HUD widgets, under the menus and the cursor.
        root.transform.SetSiblingIndex(hud.transform.GetSiblingIndex() + 1);

        Vector2 bottom = new Vector2(0.5f, 0f);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = bottom;
        rect.anchoredPosition = new Vector2(0f, 150f);
        rect.sizeDelta = new Vector2(620f, 58f);

        Tutorial tutorial = root.AddComponent<Tutorial>();
        tutorial.group = root.AddComponent<CanvasGroup>();
        tutorial.group.blocksRaycasts = false;
        tutorial.group.interactable = false;

        GameObject panel = CrtPanel.CreatePanel(root.transform, rect.sizeDelta);
        panel.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        TMP_Text sample = hud.GetComponentInChildren<TMP_Text>(true);
        TMP_FontAsset font = sample != null ? sample.font : TMP_Settings.defaultFontAsset;
        CrtPanel.MakeText(panel.transform, font, "Header", "> TRAINING", 13f, new Vector2(0f, -14f), 1f, new Vector2(600f, 18f),
            UiColours.Amber, 4f, TextAlignmentOptions.Center);
        tutorial.body = CrtPanel.MakeText(panel.transform, font, "Prompt", "", 17f, new Vector2(0f, -38f), 1f, new Vector2(600f, 24f),
            UiColours.Phosphor, 2f, TextAlignmentOptions.Center);
        return tutorial;
    }

    private void Begin(FloorGenerator generator)
    {
        floor = generator;
        combatRoom = generator.FirstRoom(RoomType.Normal);
        crate = FindAnyObjectByType<SupplyCrate>();
        step = Step.Move;
        moved = 0f;
        sawSneak = false;
        shownStep = -1;
    }

    private void Update()
    {
        group.alpha = !GameFlow.ScreenOverWorld ? 1f : 0f;

        if (GameInput.UsingGamepad != shownForGamepad || step != shownStep)
        {
            shownForGamepad = GameInput.UsingGamepad;
            shownStep = step;
            body.text = InputHint.Format(Prompt(step));
        }

        PlayerWeapon player = PlayerWeapon.Current;

        if (player == null || floor == null || GameFlow.InputBlocked)
            return;

        // The fight comes first, whatever step they'd got to.
        if (combatRoom != null && combatRoom.IsCleared)
            AdvanceTo(Step.Hatch);
        else if (combatRoom != null && combatRoom.IsLocked)
            AdvanceTo(Step.Fight);
        else if (StepDone(player))
            AdvanceTo(step + 1);
    }

    private bool StepDone(PlayerWeapon player)
    {
        switch (step)
        {
            case Step.Move:
                if (GameInput.Move.ReadValue<Vector2>().sqrMagnitude > 0.04f)
                    moved += Time.deltaTime;

                return moved >= MoveTime;

            case Step.Fire:
                return GameInput.Fire.WasPressedThisFrame();

            case Step.Roll:
                return PlayerDodge.For(player.gameObject).Rolling;

            case Step.Sneak:
                bool sneaking = player.TryGetComponent(out PlayerMovement movement) && movement.IsSneaking;
                bool stopped = sawSneak && !sneaking;
                sawSneak |= sneaking;
                return stopped;

            case Step.Crate:
                return crate == null || crate.Opened;

            default:
                return false;
        }
    }

    private void AdvanceTo(int next)
    {
        if (next <= step)
            return;

        step = next;
        SoundFX.Play2D(SpeechText.Blip(), 0.5f, 1.6f);
    }

    private static string Prompt(int step)
    {
        switch (step)
        {
            case Step.Move: return $"[{MoveLabel}] MOVE";
            case Step.Fire: return $"[{AimLabel}] AIM   [{{Fire}}] FIRE";
            case Step.Roll: return "[{Dodge}] DODGE ROLL: UNTOUCHABLE AT THE START OF THE ROLL";
            case Step.Sneak: return "[{Sneak}] SNEAK: SLOW BUT QUIET. PRESS AGAIN TO STOP";
            case Step.Crate: return "EAST DOOR: [{Interact}] OPEN THE SUPPLY CRATE";
            case Step.EnterFight: return "NORTH DOOR: DOORS LOCK UNTIL EVERY ZOMBIE IS DEAD";
            case Step.Fight: return "[{Fire}] KILL THE ZOMBIES   [{Reload}] RELOAD";
            default: return "[{Interact}] TAKE THE HATCH BACK TO THE HIDEOUT";
        }
    }

    private static string MoveLabel => GameInput.UsingGamepad
        ? GameInput.Label(GameInput.Move)
        : GameInput.Move.GetBindingDisplayString(InputBinding.MaskByGroup(GameInput.KeyboardMouseGroup)).ToUpperInvariant();

    private static string AimLabel => GameInput.UsingGamepad ? GameInput.Label(GameInput.Aim) : "MOUSE";
}
