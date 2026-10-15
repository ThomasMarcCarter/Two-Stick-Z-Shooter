using System.Collections.Generic;
using UnityEngine;

// Sounds enemies can hear: the player's footsteps, gunshots and reloads, and thrown things
// smashing or going off. A noise has a loudness, as a
// multiple of the enemies' hearing range: 1 is heard exactly that far away, 2 twice as far.
// Hearing goes through walls; an enemy that hears one comes to look (see Enemy.Hear). The range
// and each sound's loudness are set in EnemySenses, on the FloorGenerator.
public static class Noise
{
    public enum Kind
    {
        Footstep,
        Reload,
        Gunshot,
        Smash,
        Explosion,
        Melee,
        Scream
    }

    // Loudness of each sound that makes noise, from the current EnemySenses.
    public static float Gunshot => EnemySenses.Current.gunshot;
    public static float SprintStep => EnemySenses.Current.sprintStep;
    public static float WalkStep => EnemySenses.Current.walkStep;
    public static float SlowStep => EnemySenses.Current.slowStep;
    public static float Reload => EnemySenses.Current.reload;
    public static float QuietReload => EnemySenses.Current.quietReload;

    private static readonly List<Enemy> listeners = new List<Enemy>();

    // Statics outlive Play sessions with domain reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        listeners.Clear();
    }

    public static void Register(Enemy enemy)
    {
        if (!listeners.Contains(enemy))
            listeners.Add(enemy);
    }

    public static void Unregister(Enemy enemy)
    {
        listeners.Remove(enemy);
    }

    public static void Emit(Vector3 position, float loudness, Kind kind)
    {
        if (loudness <= 0f)
            return;

        if (NoiseDebug.Enabled)
        {
            float radius = EnemySenses.Current.hearingRange * loudness;
            NoiseDebug.Show(position, radius, kind, $"{Describe(kind, loudness)} {radius:0.#}");
        }

        // Backwards, in case hearing it makes an enemy do something that unregisters it.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (listeners[i] != null)
                listeners[i].Hear(position, loudness);
        }
    }

    // For the debug view's labels.
    private static string Describe(Kind kind, float loudness)
    {
        switch (kind)
        {
            case Kind.Gunshot:
                return "GUNSHOT";
            case Kind.Smash:
                return "SMASH";
            case Kind.Explosion:
                return "EXPLOSION";
            case Kind.Melee:
                return "MELEE";
            case Kind.Scream:
                return "SCREAM";
            case Kind.Reload:
                return loudness < Reload ? "RELOAD (QUIET)" : "RELOAD";
            default:
                return loudness > (WalkStep + SprintStep) / 2f ? "SPRINT"
                    : loudness < (SlowStep + WalkStep) / 2f ? "STEP (QUIET)"
                    : "WALK";
        }
    }
}
