using System;
using UnityEngine;

// How well enemies sense the player, and how far each of the player's noises carries. Set on the
// FloorGenerator (Enemy Senses), which makes its copy the current one; every enemy, every noise
// and the debug view of detection ranges read from it. Changes in Play mode apply straight away.
[Serializable]
public class EnemySenses
{
    [Header("Sight")]
    [Tooltip("How far enemies can see the player, given a clear line (walls, closed doors and pillars block it).")]
    [Min(0f)] public float sightRange = 8f;
    [Tooltip("Sight range is multiplied by this while the player is sneaking.")]
    [Range(0f, 1f)] public float sneakingSightMultiplier = 0.6f;

    [Header("Hearing")]
    [Tooltip("Base hearing distance, through walls. Each noise below is a multiple of it. Keep it above Sight Range.")]
    [Min(0f)] public float hearingRange = 12f;

    [Header("Noise loudness (x Hearing Range)")]
    [Tooltip("A footstep at sneaking speed, or slowed by the inventory or being winded.")]
    [Min(0f)] public float slowStep = 0.6f;
    [Tooltip("A footstep at walking speed.")]
    [Min(0f)] public float walkStep = 1f;
    [Tooltip("A footstep at sprinting speed.")]
    [Min(0f)] public float sprintStep = 1.5f;
    [Tooltip("Reloading (each shell, or the start and end of a magazine swap).")]
    [Min(0f)] public float reload = 1f;
    [Tooltip("Reloading while sneaking.")]
    [Min(0f)] public float quietReload = 0.6f;
    [Tooltip("Firing.")]
    [Min(0f)] public float gunshot = 2f;

    [Header("Memory")]
    [Tooltip("How long an enemy keeps hunting once it reaches where it last saw or heard the player.")]
    [Min(0f)] public float memoryTime = 3f;
    [Tooltip("Longest it will spend getting to where it last saw or heard the player, if it's far away.")]
    [Min(0f)] public float maxSearchTime = 8f;
    [Tooltip("After reaching where the player was last seen, how far it follows the way they were going.")]
    [Min(0f)] public float followDistance = 4f;

    // The settings in use. Defaults until a FloorGenerator supplies its own.
    public static EnemySenses Current { get; private set; } = new EnemySenses();

    // Statics outlive Play sessions with domain reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Current = new EnemySenses();
    }

    public static void Use(EnemySenses senses)
    {
        Current = senses ?? new EnemySenses();
    }

    public float SightRangeFor(bool sneaking)
    {
        return sneaking ? sightRange * sneakingSightMultiplier : sightRange;
    }
}
