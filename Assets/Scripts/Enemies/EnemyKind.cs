using UnityEngine;

public enum EnemyKind
{
    Walker,     // the standard zombie
    Runner,     // fast and fragile
    Brute,      // big, slow, tough, hits hard, hard to knock about
    Bloater,    // swells up and bursts when it reaches the player, or when killed
    Screamer    // shrieks when it spots the player, bringing the whole room
}

// Per-kind numbers, as multiples of the floor's normal enemy (FloorGenerator's enemy health,
// speed and damage), plus each kind's look. The kinds' odds per floor are set on the
// FloorGenerator.
public static class EnemyKinds
{
    public static string Name(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Runner: return "RUNNER";
            case EnemyKind.Brute: return "BRUTE";
            case EnemyKind.Bloater: return "BLOATER";
            case EnemyKind.Screamer: return "SCREAMER";
            default: return "WALKER";
        }
    }

    public static float HealthMultiplier(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Runner: return 0.67f;
            case EnemyKind.Brute: return 3.3f;
            case EnemyKind.Bloater: return 1.3f;
            default: return 1f;
        }
    }

    public static float SpeedMultiplier(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Runner: return 1.7f;
            case EnemyKind.Brute: return 0.7f;
            case EnemyKind.Bloater: return 0.75f;
            case EnemyKind.Screamer: return 1.1f;
            default: return 1f;
        }
    }

    public static int ExtraDamage(EnemyKind kind)
    {
        return kind == EnemyKind.Brute ? 1 : 0;
    }

    // How long one attack swing takes, in seconds (matches its CharacterAnimator swing).
    public static float AttackPeriod(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Runner: return 0.5f;
            case EnemyKind.Brute: return 1.4f;
            case EnemyKind.Bloater: return 0.35f;
            default: return 0.9f;
        }
    }

    // Where in a swing (0 to 1) the blows land: the moment the arms come down. A runner rakes
    // with each claw in turn, so it has two.
    public static float[] StrikeTimes(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Runner: return RunnerStrikes;
            case EnemyKind.Brute: return BruteStrikes;
            default: return SwipeStrikes;
        }
    }

    private static readonly float[] RunnerStrikes = { 0.23f, 0.73f };
    private static readonly float[] BruteStrikes = { 0.55f };
    private static readonly float[] SwipeStrikes = { 0.47f };

    // Body size, against the walker's.
    public static float SizeMultiplier(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Runner: return 0.78f;
            case EnemyKind.Brute: return 1.35f;
            case EnemyKind.Bloater: return 1.15f;
            case EnemyKind.Screamer: return 0.9f;
            default: return 1f;
        }
    }

    // How far a blow moves it, against a walker's.
    public static float KnockbackScale(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Runner: return 1.3f;
            case EnemyKind.Brute: return 0.25f;
            case EnemyKind.Bloater: return 0.6f;
            default: return 1f;
        }
    }

    // Body colour; null keeps the enemy material's own (the walker).
    public static Color? Tint(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Runner: return new Color(0.75f, 0.68f, 0.45f);
            case EnemyKind.Brute: return new Color(0.28f, 0.3f, 0.34f);
            case EnemyKind.Bloater: return new Color(0.45f, 0.6f, 0.2f);
            case EnemyKind.Screamer: return new Color(0.62f, 0.5f, 0.72f);
            default: return null;
        }
    }

    // A faint glow so the dangerous kinds can be picked out in the dark.
    public static Color Glow(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Bloater: return new Color(0.5f, 0.9f, 0.1f) * 0.5f;
            case EnemyKind.Screamer: return new Color(0.7f, 0.4f, 1f) * 0.35f;
            default: return Color.black;
        }
    }
}
