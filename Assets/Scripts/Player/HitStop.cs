using UnityEngine;

// Hit-stop: the game all but freezes for a few hundredths of a second when a blow lands (a kill,
// a melee hit, the player being hurt), so hits feel heavy. Time runs at StopScale for the
// freeze, counted in real seconds, then goes back to normal speed.
// It only starts from normal speed, so it never fights a pause, the death slow-motion or any
// screen that owns the time scale. A menu opened mid-freeze saves and later restores StopScale;
// the freeze then simply finishes as normal. Several hits keep the longest freeze, not the sum.
// The runner object is made on demand and goes with the scene.
public class HitStop : MonoBehaviour
{
    private const float StopScale = 0.05f;

    private static HitStop runner;
    private static float remaining;

    // Statics outlive Play sessions with domain reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        runner = null;
        remaining = 0f;
    }

    public static bool Active => remaining > 0f && Time.timeScale == StopScale;

    public static void Freeze(float seconds)
    {
        if (seconds <= 0f || GameFlow.InputBlocked)
            return;

        if (Time.timeScale != 1f && !Active)
            return;

        if (runner == null)
            runner = new GameObject("Hit Stop").AddComponent<HitStop>();

        remaining = Mathf.Max(remaining, seconds);
        Time.timeScale = StopScale;
    }

    private void Update()
    {
        // Paused, or something else has the time scale: wait until it hands StopScale back.
        if (remaining <= 0f || Time.timeScale != StopScale)
            return;

        remaining -= Time.unscaledDeltaTime;

        if (remaining <= 0f)
        {
            remaining = 0f;
            Time.timeScale = 1f;
        }
    }
}
