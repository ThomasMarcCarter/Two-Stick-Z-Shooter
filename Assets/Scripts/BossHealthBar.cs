using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// The boss's name and health across the top of the screen, in the HUD's phosphor style with a
// stripe of the boss's colour. Appears when the player is locked in with the boss, drains as it
// takes hits (with a trailing "damage" segment), and goes when it dies or the run ends. Built in
// code under the HUD canvas, below the menus and end screens (see GameFlow.md's Canvas order).
public class BossHealthBar : MaskableGraphic
{
    private static readonly Color Phosphor = new Color(0.35f, 1f, 0.55f, 1f);
    private static readonly Vector2 Size = new Vector2(560f, 46f);

    private static BossHealthBar instance;

    private Enemy boss;
    private Color accent;
    private TMP_Text label;
    private float shown;      // health fraction drawn
    private float trailing;   // the recent-damage segment, catching up
    private float visibility;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        instance = null;
    }

    // Starts showing a boss (when its room locks).
    public static void Track(Enemy enemy, string bossName, Color colour)
    {
        if (instance == null)
            instance = Create();

        if (instance == null)
            return;

        instance.boss = enemy;
        instance.accent = colour;
        instance.label.text = "> " + bossName;
        instance.shown = instance.trailing = 1f;
        instance.visibility = 0f;
    }

    private static BossHealthBar Create()
    {
        EquipmentHud hud = FindAnyObjectByType<EquipmentHud>();
        Canvas canvas = hud != null ? hud.canvas : null;

        if (canvas == null)
            return null;

        GameObject go = new GameObject("Boss Health Bar", typeof(RectTransform));
        go.layer = canvas.gameObject.layer;
        go.transform.SetParent(canvas.transform, false);

        // Under every menu and end screen, like the rest of the HUD.
        Transform screens = canvas.transform.Find("Fullscreen Map");

        if (screens != null)
            go.transform.SetSiblingIndex(screens.GetSiblingIndex());

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -40f);
        rect.sizeDelta = Size;

        go.AddComponent<CanvasRenderer>();
        BossHealthBar bar = go.AddComponent<BossHealthBar>();
        bar.raycastTarget = false;

        GameObject text = new GameObject("Name", typeof(RectTransform));
        text.layer = go.layer;
        text.transform.SetParent(go.transform, false);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(0f, 20f);

        TextMeshProUGUI label = text.AddComponent<TextMeshProUGUI>();
        TMP_Text sample = canvas.GetComponentInChildren<TMP_Text>(true);

        if (sample != null)
            label.font = sample.font;

        label.fontSize = 16f;
        label.characterSpacing = 8f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Phosphor;
        label.raycastTarget = false;
        bar.label = label;
        return bar;
    }

    private void Update()
    {
        bool show = boss != null && boss.IsActive && !DeathScreen.IsShowing && !VictoryScreen.IsShowing && !MainMenu.IsOpen;
        visibility = Mathf.MoveTowards(visibility, show ? 1f : 0f, Time.unscaledDeltaTime * 3f);

        if (boss != null && boss.MaxHealth > 0)
        {
            float fraction = Mathf.Clamp01(boss.Health / (float)boss.MaxHealth);
            shown = Mathf.MoveTowards(shown, fraction, Time.unscaledDeltaTime * 2f);
            trailing = trailing < shown ? shown : Mathf.MoveTowards(trailing, shown, Time.unscaledDeltaTime * 0.35f);
        }
        else
        {
            shown = Mathf.MoveTowards(shown, 0f, Time.unscaledDeltaTime * 2f);
            trailing = Mathf.MoveTowards(trailing, shown, Time.unscaledDeltaTime * 0.35f);
        }

        label.alpha = visibility;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (visibility <= 0.001f)
            return;

        Rect bar = new Rect(-Size.x / 2f, -Size.y + 4f, Size.x, 16f);
        FillRect(vh, bar, new Color(0f, 0.06f, 0.03f, 0.85f * visibility));

        Rect inner = new Rect(bar.xMin + 3f, bar.yMin + 3f, bar.width - 6f, bar.height - 6f);
        FillRect(vh, new Rect(inner.xMin, inner.yMin, inner.width * trailing, inner.height), Fade(Color.white, 0.55f * visibility));
        FillRect(vh, new Rect(inner.xMin, inner.yMin, inner.width * shown, inner.height), Fade(Color.Lerp(accent, Phosphor, 0.35f), visibility));

        // Notches every tenth, and the boss's colour as a stripe beneath.
        for (int i = 1; i < 10; i++)
        {
            float x = inner.xMin + inner.width * i / 10f;
            FillRect(vh, new Rect(x - 0.5f, inner.yMin, 1f, inner.height), new Color(0f, 0.06f, 0.03f, 0.6f * visibility));
        }

        OutlineRect(vh, bar, 1.5f, Fade(Phosphor, 0.8f * visibility));
        FillRect(vh, new Rect(bar.xMin, bar.yMin - 4f, bar.width, 2f), Fade(accent, visibility));
    }
}
