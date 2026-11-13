using System.Collections.Generic;
using TMPro;
using UnityEngine;

// The "UNLOCKED: <NAME>" card: an achievement was earned (MetaProgress calls Enqueue), so an amber
// heading and a phosphor line of what it unlocks show near the top of the HUD for a few seconds,
// then fade, like the floor arrival card. Several earned together queue up and show one after
// another. Hidden (and its clock held) under full-screen menus. Built in code on the HUD canvas
// the first time one is earned. The queue is static and outlives the scene, so one earned under a
// full-screen screen or just before a reload waits, and the next HUD recreates the card (Resume).
public class UnlockCard : MonoBehaviour
{
    private const float Hold = 3.5f;
    private const float FadeTime = 0.6f;

    private static readonly Queue<Achievement> queue = new Queue<Achievement>();
    private static UnlockCard instance;

    private CanvasGroup group;
    private TMP_Text header;
    private TMP_Text body;
    private float shownFor = -1f;   // seconds the current card has been up; -1 = none

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        queue.Clear();
        instance = null;
    }

    public static void Enqueue(Achievement earned)
    {
        queue.Enqueue(earned);
        Resume();
    }

    // Makes the card if something is waiting and there is a HUD to put it on (called when the HUD builds).
    public static void Resume()
    {
        if (instance == null && queue.Count > 0)
            instance = Create();
    }

    private static UnlockCard Create()
    {
        EquipmentHud hud = FindAnyObjectByType<EquipmentHud>();
        Canvas canvas = hud != null ? hud.canvas : null;

        if (canvas == null)
            return null;

        GameObject root = new GameObject("Unlock Card", typeof(RectTransform));
        root.layer = canvas.gameObject.layer;
        root.transform.SetParent(canvas.transform, false);
        root.transform.SetSiblingIndex(hud.transform.GetSiblingIndex() + 1);   // under the menus and the cursor

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -190f);
        rect.sizeDelta = new Vector2(640f, 70f);

        UnlockCard card = root.AddComponent<UnlockCard>();
        card.group = root.AddComponent<CanvasGroup>();
        card.group.blocksRaycasts = false;
        card.group.interactable = false;
        card.group.alpha = 0f;

        TMP_Text sample = hud.GetComponentInChildren<TMP_Text>(true);
        TMP_FontAsset font = sample != null ? sample.font : TMP_Settings.defaultFontAsset;
        card.header = CrtPanel.MakeTypedLine(card.transform, "Header", font, 22f, 0f, 30f, UiColours.Amber);
        card.body = CrtPanel.MakeTypedLine(card.transform, "Unlocks", font, 15f, -32f, 36f, UiColours.Phosphor);
        return card;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (GameFlow.ScreenOverWorld)
        {
            group.alpha = 0f;
            return;
        }

        if (shownFor < 0f)
        {
            if (queue.Count == 0)
                return;

            Achievement next = queue.Dequeue();
            header.text = "UNLOCKED: " + Achievements.Name(next);
            string unlocks = Achievements.UnlocksText(next);
            body.text = unlocks.Length > 0 ? unlocks : Achievements.HowToEarn(next);
            shownFor = 0f;
            SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("metal.clank", 3, ProceduralAudio.MetalClank)), 0.5f, 1.3f);
        }

        shownFor += Time.unscaledDeltaTime;
        float fade = shownFor > Hold ? Mathf.Clamp01(1f - (shownFor - Hold) / FadeTime) : Mathf.Clamp01(shownFor / 0.2f);
        group.alpha = fade;

        if (shownFor >= Hold + FadeTime)
        {
            shownFor = -1f;
            group.alpha = 0f;
        }
    }
}
