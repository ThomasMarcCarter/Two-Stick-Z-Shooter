using UnityEngine;

// The CharacterAnimator's zombie and boss poses: swings timed to the Enemy's attack clock,
// staggers, and each boss's walk and poses for its moves (IBossMoves).
public partial class CharacterAnimator
{
    // ---------- Zombies ----------

    private void AnimateZombieArms(float dt)
    {
        // The swing runs on the enemy's attack clock, so the blow lands as the arms come down.
        float attack = enemy != null ? enemy.AttackTime : -1f;

        if (attack >= 0f)
        {
            AttackPose(attack);
            return;
        }
        // Walking: arms sway with the stride (pumping, for a runner); a little drift when still.
        float sway = Mathf.Sin(phase) * walkBlend;
        float drift = (Mathf.PerlinNoise(seed + 10f, Time.time * 0.5f) - 0.5f) * 10f;

        switch (style)
        {
            case Style.Runner:
                Pose(armL, Quaternion.Euler(sway * 35f + 15f * walkBlend, 0f, 0f));
                Pose(armR, Quaternion.Euler(-sway * 35f + 15f * walkBlend, 0f, 0f));
                break;
            case Style.Brute:
                Pose(armL, Quaternion.Euler(-sway * 20f, 0f, 0f));
                Pose(armR, Quaternion.Euler(sway * 20f, 0f, 0f));
                break;
            case Style.Bloater:
                Pose(armL, Quaternion.Euler(0f, 0f, sway * 15f + drift));
                Pose(armR, Quaternion.Euler(0f, 0f, -sway * 15f - drift));
                break;
            default:
                Pose(armL, Quaternion.Euler(sway * 8f + drift, 0f, 0f));
                Pose(armR, Quaternion.Euler(-sway * 8f - drift, 0f, 0f));
                break;
        }
    }

    // Arm positions through one attack (t from 0 to 1). Negative X raises a reaching arm. Each
    // swing is built around the enemy's strike times (EnemyKinds.StrikeTimes), so the arms are
    // coming down on the player at the moment the damage lands.
    private void AttackPose(float t)
    {
        float[] strikes = EnemyKinds.StrikeTimes(enemy.Kind);

        switch (style)
        {
            case Style.Runner:
            {
                // Alternating claws: one arm rakes down, then the other, each half of the swing
                // around its own strike.
                bool first = t < 0.5f;
                float local = Mathf.Repeat(t * 2f, 1f);
                float hit = strikes.Length > 1 ? Mathf.Repeat((first ? strikes[0] : strikes[1]) * 2f, 1f) : 0.47f;
                Pose(armL, Quaternion.Euler(Swipe(local, hit, first), 0f, 0f));
                Pose(armR, Quaternion.Euler(Swipe(local, hit, !first), 0f, 0f));
                break;
            }
            case Style.Brute:
            {
                // Both fists up over the head, then down together onto the player.
                float hit = strikes[0];
                float up = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, hit - 0.1f, t));
                float down = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hit - 0.1f, hit + 0.05f, t));
                float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, t));
                float angle = Mathf.Lerp(Mathf.Lerp(0f, -150f, up), -35f, down) * (1f - back);
                Pose(armL, Quaternion.Euler(angle, 0f, -10f * up * (1f - back)));
                Pose(armR, Quaternion.Euler(angle, 0f, 10f * up * (1f - back)));
                break;
            }
            case Style.Bloater:
            {
                // Frantic flailing, arms flung furthest forward as the blow lands.
                float flail = Mathf.Sin((t - strikes[0] + 0.25f) * Mathf.PI * 2f) * Mathf.Sin(t * Mathf.PI);
                Pose(armL, Quaternion.Euler(-40f * flail, 0f, 25f * flail));
                Pose(armR, Quaternion.Euler(40f * flail, 0f, -25f * flail));
                break;
            }
            default:
            {
                // Two-handed swipe: raise, then strike down at the player.
                float swing = Swipe(t, strikes[0], true);
                Pose(armL, Quaternion.Euler(swing, 8f, 0f));
                Pose(armR, Quaternion.Euler(swing, -8f, 0f));
                break;
            }
        }
    }

    // A single arm's swing around its hit (both 0 to 1): raised high in front for the wind-up,
    // then swept down so it's reaching out at the player (about 70 degrees forward) as the blow
    // lands, finishing low in front, then back to rest. When not this arm's turn, it stays near
    // rest.
    private static float Swipe(float t, float hit, bool active)
    {
        if (!active)
            return 10f;

        float raised = Mathf.Max(0.05f, hit - SwipeLead);
        float struck = Mathf.Min(0.95f, hit + SwipeFollow);

        if (t < raised)
            return Mathf.Lerp(0f, -140f, Mathf.SmoothStep(0f, 1f, t / raised));

        if (t < struck)
            return Mathf.Lerp(-140f, -40f, Mathf.SmoothStep(0f, 1f, (t - raised) / (struck - raised)));

        return Mathf.Lerp(-40f, 0f, Mathf.SmoothStep(0f, 1f, (t - struck) / (1f - struck)));
    }

    // How long before and after the hit (in swing time) the downstroke starts and ends.
    private const float SwipeLead = 0.1f;
    private const float SwipeFollow = 0.06f;

    private float AttackLean()
    {
        float t = enemy != null ? enemy.AttackTime : -1f;

        if (t < 0f)
            return 0f;

        // Rocks back for the wind-up, furthest forward as the (first) blow lands.
        float hit = EnemyKinds.StrikeTimes(enemy.Kind)[0];
        float windUp = Mathf.InverseLerp(0f, hit - SwipeLead, t);
        float strike = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(hit - 0.2f, hit + 0.2f, t)) * Mathf.PI);
        float amount = style == Style.Brute ? 22f : style == Style.Bloater ? 6f : 14f;
        return -amount * 0.4f * windUp * (1f - Mathf.InverseLerp(hit - SwipeLead, hit, t)) + amount * strike;
    }

    // ---------- Bosses ----------

    private bool IsBoss => style == Style.Butcher || style == Style.Broodmother || style == Style.Howler;
    private BossMove Move => boss != null ? boss.Move : BossMove.None;
    private float MoveTime => boss != null ? boss.MoveProgress : 0f;

    // Smoothed, so a pose eases out when a move ends rather than snapping back.
    private float bossLean;
    private float bossRoll;
    private float armLift;      // cleaver raised / arms flung up (0 to 1)
    private float armSpread;

    private void BossBody(float dt, ref float bob, ref float lean, ref float roll)
    {
        float t = MoveTime;
        float targetLean = 0f;
        float targetRoll = 0f;

        switch (style)
        {
            case Style.Butcher:
                bob *= 0.04f;
                targetRoll = Mathf.Sin(phase) * 6f * walkBlend;

                if (Move == BossMove.WindUp)
                    targetLean = -14f * Mathf.SmoothStep(0f, 1f, t);
                else if (Move == BossMove.Charge)
                    targetLean = 26f;
                else if (Move == BossMove.Dazed)
                {
                    // Reeling in a slow wobbly circle.
                    targetLean = Mathf.Sin(Time.time * 4.3f) * 9f - 6f;
                    targetRoll = Mathf.Sin(Time.time * 6.1f) * 12f;
                }
                break;

            case Style.Broodmother:
            {
                bob *= 0.02f;
                targetRoll = Mathf.Sin(phase) * 5f * walkBlend;

                // The sac breathes.
                float breath = 1f + Mathf.Sin(Time.time * 2.2f + seed) * 0.03f;
                torso.Bone.localScale = new Vector3(breath, 1f / breath, breath);

                if (Move == BossMove.Spit)
                    targetLean = t < 0.75f ? -16f * Mathf.SmoothStep(0f, 1f, t / 0.75f) : Mathf.Lerp(-16f, 20f, (t - 0.75f) / 0.25f);
                break;
            }

            case Style.Howler:
                bob *= 0.05f;
                targetLean = 16f * walkBlend;
                targetRoll = Mathf.Sin(phase) * 5f * walkBlend;

                if (Move == BossMove.Howl)
                    targetLean = -34f * Mathf.SmoothStep(0f, 1f, t);
                else if (Move == BossMove.Dodge)
                {
                    // Leans into the dart, whichever way it's going.
                    Vector3 local = transform.InverseTransformDirection(velocity);
                    targetRoll = -Mathf.Clamp(local.x * 4f, -28f, 28f);
                    targetLean = 10f;
                }
                break;
        }

        float rate = 1f - Mathf.Exp(-14f * dt);
        bossLean = Mathf.Lerp(bossLean, targetLean, rate);
        bossRoll = Mathf.Lerp(bossRoll, targetRoll, rate);
        lean += bossLean;
        roll += bossRoll;
    }

    private Quaternion BossHead()
    {
        float t = MoveTime;
        float loll = (Mathf.PerlinNoise(seed, Time.time * 0.7f) - 0.5f) * 20f;

        switch (Move)
        {
            case BossMove.Dazed:
                return Quaternion.Euler(Mathf.Sin(Time.time * 5f) * 15f, Mathf.Sin(Time.time * 3.7f) * 25f, Mathf.Sin(Time.time * 4.4f) * 30f);
            case BossMove.Howl:
                return Quaternion.Euler(-40f * Mathf.SmoothStep(0f, 1f, t), 0f, loll * 0.3f);
            case BossMove.Spit:
                return Quaternion.Euler(t < 0.75f ? -20f * t / 0.75f : Mathf.Lerp(-20f, 25f, (t - 0.75f) / 0.25f), 0f, 0f);
            case BossMove.WindUp:
                return Quaternion.Euler(-10f * t, 0f, Mathf.Sin(Time.time * 40f) * 4f * t);
            default:
                return Quaternion.Euler(0f, 0f, loll);
        }
    }

    private void AnimateBossArms(float dt)
    {
        float t = MoveTime;
        float sway = Mathf.Sin(phase) * walkBlend;
        float rate = 1f - Mathf.Exp(-12f * dt);

        switch (style)
        {
            case Style.Butcher:
            {
                // The cleaver arm (ArmR) goes up overhead for the wind-up and leads the charge.
                float lift = Move == BossMove.WindUp ? Mathf.SmoothStep(0f, 1f, t) : 0f;
                armLift = Mathf.Lerp(armLift, lift, rate);

                if (Move == BossMove.Dazed)
                {
                    float limp = Mathf.Sin(Time.time * 5f);
                    Pose(armL, Quaternion.Euler(limp * 18f, 0f, 18f));
                    Pose(armR, Quaternion.Euler(-limp * 18f, 0f, -18f));
                    return;
                }

                if (Move == BossMove.Charge)
                {
                    Pose(armL, Quaternion.Euler(35f, 0f, 15f));
                    Pose(armR, Quaternion.Euler(-75f, 0f, 0f));
                    return;
                }

                // Up on the player: hacks at them with the cleaver, on the enemy's attack clock
                // (the hit lands on the downstroke).
                float chop = enemy != null ? enemy.AttackTime : -1f;
                float cleaver = chop >= 0f ? Swipe(chop, EnemyKinds.StrikeTimes(enemy.Kind)[0], true) * 1.15f : 0f;
                Pose(armL, Quaternion.Euler(-sway * 18f - 50f * armLift, 0f, 0f));
                Pose(armR, Quaternion.Euler(sway * 14f - 160f * armLift + cleaver, 0f, 12f * armLift));
                break;
            }

            case Style.Broodmother:
            {
                // Arms clutching at the air, spreading wide as she swells to spit.
                float spread = Move == BossMove.Spit ? Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t / 0.75f)) : 0f;
                armSpread = Mathf.Lerp(armSpread, spread, rate);
                float clutchL = Mathf.Sin(Time.time * 3.1f + seed) * 25f;
                float clutchR = Mathf.Sin(Time.time * 3.1f + seed + 2f) * 25f;
                Pose(armL, Quaternion.Euler(clutchL * (1f - armSpread) - 40f * armSpread, 0f, 50f * armSpread));
                Pose(armR, Quaternion.Euler(clutchR * (1f - armSpread) - 40f * armSpread, 0f, -50f * armSpread));
                break;
            }

            case Style.Howler:
            {
                // Long arms swinging loose as it lopes; flung up and wide for the howl.
                float lift = Move == BossMove.Howl ? Mathf.SmoothStep(0f, 1f, t) : 0f;
                armLift = Mathf.Lerp(armLift, lift, rate);
                float trail = Move == BossMove.Dodge ? 25f : 0f;
                float drift = (Mathf.PerlinNoise(seed + 10f, Time.time * 0.6f) - 0.5f) * 12f;
                Pose(armL, Quaternion.Euler((sway * 28f + drift + trail) * (1f - armLift) - 110f * armLift, 0f, 65f * armLift));
                Pose(armR, Quaternion.Euler((-sway * 28f - drift + trail) * (1f - armLift) - 110f * armLift, 0f, -65f * armLift));
                break;
            }
        }
    }
}
