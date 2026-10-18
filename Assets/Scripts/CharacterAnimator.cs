using UnityEngine;

// Procedural animation for the jointed character models (Tools/Blender/Characters.py: Torso,
// Head, ArmL, ArmR, LegL, LegR, each pivoting at its joint). No clips or Animator: limbs are
// rotated from their rest pose each frame from what the character is doing.
//   Walking: legs swing in the direction of travel (strafing and backpedalling too), with a
//   stride cycle driven by distance covered, so feet don't skate whatever the speed; the body
//   bobs, and each style moves differently (the runner pumps its arms and leans in, the brute
//   lumbers and rolls its shoulders, the bloater waddles side to side).
//   Player: crouches and leans when sneaking, leans into a sprint, and reloads: for a magazine
//   the support hand drops to the belt for a fresh one while the gun tips over, then slaps it
//   home; shell by shell, the gun stays tipped and the hand feeds it in a steady rhythm.
//   Zombies: attack whenever they're on the player and close: a two-handed swipe for walkers and
//   screamers, quick alternating claws for runners, an overhead double-fisted slam for brutes and
//   a frantic flailing for bloaters. They reel back when staggered.
// Added by CharacterVisuals when a model is attached; bosses (one-piece models) don't get one.
public class CharacterAnimator : MonoBehaviour
{
    public enum Style { Player, Walker, Runner, Brute, Bloater }

    private struct Limb
    {
        public Transform Bone;
        public Quaternion Rest;
        public Vector3 RestPosition;
    }

    private Style style;
    private Limb torso, head, armL, armR, legL, legR;

    // In Unity the model's "R" arm is the one reaching forward (the support hand on the player).
    private Limb FrontArm => armR;
    private Limb RearArm => armL;

    private Enemy enemy;
    private PlayerShooting shooting;
    private PlayerMovement movement;
    private PlayerStamina stamina;

    private Vector3 lastPosition;
    private Vector3 velocity;
    private float phase;
    private float walkBlend;
    private float attackTime = -1f;
    private float seed;
    private Transform tiltedGun;   // the gun visual currently being tipped

    // Sets up the model for animation; null if it isn't a jointed model.
    public static CharacterAnimator Add(Transform model, Style style)
    {
        Transform torsoBone = model.Find("Torso");

        if (torsoBone == null || model.Find("LegL") == null)
            return null;

        // The head and arms ride on the torso, so leaning and bobbing carry them along.
        foreach (string name in new[] { "Head", "ArmL", "ArmR" })
        {
            Transform bone = model.Find(name);

            if (bone != null)
                bone.SetParent(torsoBone, true);
        }

        CharacterAnimator animator = model.gameObject.AddComponent<CharacterAnimator>();
        animator.style = style;
        return animator;
    }

    private void Start()
    {
        torso = Bind(transform.Find("Torso"));
        head = Bind(torso.Bone != null ? torso.Bone.Find("Head") : null);
        armL = Bind(torso.Bone != null ? torso.Bone.Find("ArmL") : null);
        armR = Bind(torso.Bone != null ? torso.Bone.Find("ArmR") : null);
        legL = Bind(transform.Find("LegL"));
        legR = Bind(transform.Find("LegR"));

        enemy = GetComponentInParent<Enemy>();
        shooting = GetComponentInParent<PlayerShooting>();
        movement = GetComponentInParent<PlayerMovement>();
        stamina = GetComponentInParent<PlayerStamina>();
        lastPosition = transform.position;
        seed = Random.value * 100f;
    }

    private static Limb Bind(Transform bone)
    {
        return bone == null
            ? new Limb()
            : new Limb { Bone = bone, Rest = bone.localRotation, RestPosition = bone.localPosition };
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;

        if (dt <= 0f || torso.Bone == null)
            return;

        // How it's moving, measured rather than read from physics so it works for both the
        // player and enemies (and ignores the model turning on the spot).
        Vector3 moved = transform.position - lastPosition;
        lastPosition = transform.position;
        moved.y = 0f;
        velocity = Vector3.Lerp(velocity, moved / dt, 1f - Mathf.Exp(-12f * dt));

        float size = Mathf.Max(0.1f, transform.lossyScale.y);
        float speed = velocity.magnitude;
        walkBlend = Mathf.MoveTowards(walkBlend, speed > 0.25f ? 1f : 0f, dt * 5f);

        // One stride cycle (two steps) per this many metres, in the model's own size.
        phase += speed * dt / (Stride * size) * Mathf.PI * 2f;
        phase = Mathf.Repeat(phase, Mathf.PI * 2f);

        Vector3 localMove = speed > 0.05f ? transform.InverseTransformDirection(velocity / speed) : Vector3.forward;
        localMove.y = 0f;

        AnimateLegs(localMove);
        AnimateBody(dt);

        if (style == Style.Player)
            AnimatePlayerArms();
        else
            AnimateZombieArms(dt);
    }

    private float Stride
    {
        get
        {
            switch (style)
            {
                case Style.Runner: return 1.4f;
                case Style.Brute: return 1.2f;
                case Style.Bloater: return 0.6f;
                case Style.Player: return 1.3f;
                default: return 0.9f;
            }
        }
    }

    private float LegSwing
    {
        get
        {
            switch (style)
            {
                case Style.Runner: return 42f;
                case Style.Brute: return 22f;
                case Style.Bloater: return 20f;
                case Style.Player: return Sneaking ? 22f : Sprinting ? 40f : 30f;
                default: return 26f;
            }
        }
    }

    private bool Sneaking => movement != null && movement.IsSneaking;
    private bool Sprinting => stamina != null && stamina.IsSprinting;

    // ---------- Legs and body ----------

    private void AnimateLegs(Vector3 localMove)
    {
        // Swing around the axis across the direction of travel, so strafing and backing up look
        // right too.
        Vector3 axis = localMove.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, localMove.normalized) : Vector3.right;
        float swing = Mathf.Sin(phase) * LegSwing * walkBlend;

        Pose(legL, Quaternion.AngleAxis(-swing, axis));
        Pose(legR, Quaternion.AngleAxis(swing, axis));
    }

    private void AnimateBody(float dt)
    {
        // Bob twice a cycle (once a step), and settle when standing.
        float bob = (Mathf.Abs(Mathf.Sin(phase)) - 0.5f) * walkBlend;
        float lean = 0f;
        float roll = 0f;
        float drop = 0f;

        switch (style)
        {
            case Style.Runner:
                lean = 18f * walkBlend;
                bob *= 0.05f;
                break;
            case Style.Brute:
                roll = Mathf.Sin(phase) * 7f * walkBlend;      // shoulders rolling with each step
                bob *= 0.04f;
                break;
            case Style.Bloater:
                roll = Mathf.Sin(phase) * 12f * walkBlend;     // waddle
                bob *= 0.03f;
                break;
            case Style.Player:
                lean = Sneaking ? 14f : Sprinting ? 9f * walkBlend : 0f;
                drop = Sneaking ? 0.1f : 0f;
                bob *= 0.035f;
                break;
            default:
                roll = Mathf.Sin(phase) * 4f * walkBlend;
                bob *= 0.035f;
                break;
        }

        // Reeling from a blow.
        if (enemy != null && enemy.IsStaggered)
            lean -= 18f;

        // The attack swing leans the body into it.
        lean += AttackLean();

        torso.Bone.localPosition = torso.RestPosition + Vector3.up * (bob - drop);
        torso.Bone.localRotation = torso.Rest * Quaternion.Euler(lean, 0f, roll);

        if (head.Bone != null)
        {
            // Zombies' heads loll; the player's stays steady.
            float loll = style == Style.Player ? 0f : (Mathf.PerlinNoise(seed, Time.time * 0.7f) - 0.5f) * 24f;
            Pose(head, Quaternion.Euler(0f, 0f, loll));
        }
    }

    // ---------- Player ----------

    // Where each hand is, from its shoulder, in the arm's rest pose (the model's gloves).
    // Elbows bent, holding the gun at mid-torso (Tools/Blender/Characters.py).
    private static readonly Vector3 RearHand = new Vector3(-0.24f, -0.38f, 0.45f);
    private static readonly Vector3 FrontHand = new Vector3(0.24f, -0.32f, 0.72f);

    private Quaternion currentTilt = Quaternion.identity;

    // Both hands go to the gun's grips (worked out from the gun: its muzzle height and length),
    // reaching for them each frame, so every gun sits in the hands. Reloading tips the gun over
    // around the rear hand, so it stays in that hand, while the support hand goes off to the
    // belt for a magazine (or feeds shells) and comes back.
    private void AnimatePlayerArms()
    {
        Gun gun = shooting != null ? shooting.EquippedGun : null;
        Transform gunModel = gun != null ? gun.transform.Find("Model") : null;

        if (gunModel == null)
        {
            Pose(FrontArm, Quaternion.identity);
            Pose(RearArm, Quaternion.identity);
            RestoreGun(null);
            return;
        }

        Transform gunRoot = gun.transform;
        Transform muzzle = gunRoot.Find("Muzzle");
        float bore = muzzle != null ? muzzle.localPosition.y : -0.49f;
        float muzzleZ = muzzle != null ? muzzle.localPosition.z : 0.9f;
        bool shortGun = muzzleZ < 0.95f;

        // Grip points, in the gun's own space.
        Vector3 grip = new Vector3(0f, bore - 0.1f, 0.45f);
        Vector3 foregrip = shortGun
            ? new Vector3(0f, bore - 0.12f, 0.47f)                                 // cupping the grip
            : new Vector3(0f, bore - 0.04f, Mathf.Min(0.82f, muzzleZ - 0.3f));     // under the handguard

        Quaternion tilt = Quaternion.identity;
        float away = 0f;                 // 0 = support hand on the gun, 1 = at the belt
        Vector3 support = foregrip;

        if (gun.IsReloading)
        {
            float t = gun.ReloadProgress;

            if (gun.Reloading == Gun.ReloadStyle.Magazine)
            {
                float tip = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.03f, 0.92f, t)) * Mathf.PI);
                tilt = Quaternion.Euler(-10f * tip, 0f, 28f * tip);
                away = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.32f, t))
                     * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.8f, t)));
                float slap = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.8f, 0.92f, t)) * Mathf.PI);
                support = Vector3.Lerp(foregrip, grip + new Vector3(0f, -0.12f, 0.05f), 0.5f) + Vector3.up * 0.06f * slap;
            }
            else
            {
                // Tipped over; the hand moves between the shell pouch and the loading port.
                tilt = Quaternion.Euler(-6f, 0f, -26f);
                float pump = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * 1.8f);
                support = Vector3.Lerp(grip + new Vector3(0f, -0.06f, 0.15f), foregrip, 0.5f) + new Vector3(0f, -0.06f * pump, 0f);
                away = pump * 0.35f;
            }
        }

        currentTilt = Quaternion.Slerp(currentTilt, tilt, 1f - Mathf.Exp(-20f * Time.deltaTime));
        TipGunAround(gunModel, grip, currentTilt);

        // Where the grip points are now, with the tip applied.
        Vector3 gripWorld = gunRoot.TransformPoint(grip);
        Vector3 supportWorld = gunRoot.TransformPoint(grip + currentTilt * (support - grip));

        // The belt: low on the support side, by the hip.
        Vector3 belt = torso.Bone.TransformPoint(new Vector3(-0.22f, 0.02f, 0.12f));
        supportWorld = Vector3.Lerp(supportWorld, belt, away);

        Reach(RearArm, RearHand, gripWorld);
        Reach(FrontArm, FrontHand, supportWorld);
    }

    // Rotates the gun's visual by `tilt` around a point (its grip) rather than its origin, so the
    // grip stays put in the hand. The muzzle isn't moved, so shots still go where they're aimed.
    private void TipGunAround(Transform gunModel, Vector3 pivot, Quaternion tilt)
    {
        RestoreGun(gunModel);
        gunModel.localRotation = tilt;
        gunModel.localPosition = pivot - tilt * pivot;
    }

    // Puts a previously tilted gun (one that's been swapped out) back as it was.
    private void RestoreGun(Transform current)
    {
        if (tiltedGun != null && tiltedGun != current)
        {
            tiltedGun.localRotation = Quaternion.identity;
            tiltedGun.localPosition = Vector3.zero;
        }

        tiltedGun = current;
    }

    // Swings a rigid arm from its shoulder so its hand lands on a point, stretching it a little
    // if it's short (blocky arms, so it isn't noticeable).
    private static void Reach(Limb arm, Vector3 hand, Vector3 target)
    {
        if (arm.Bone == null)
            return;

        Quaternion rest = arm.Bone.parent.rotation * arm.Rest;
        Vector3 current = rest * hand;
        Vector3 wanted = target - arm.Bone.position;

        if (wanted.sqrMagnitude < 0.0001f)
            return;

        arm.Bone.rotation = Quaternion.FromToRotation(current, wanted) * rest;
        float stretch = Mathf.Clamp(wanted.magnitude / (current.magnitude * arm.Bone.parent.lossyScale.y), 0.8f, 1.35f);
        arm.Bone.localScale = Vector3.one * stretch;
    }

    // ---------- Zombies ----------

    private float AttackPeriod
    {
        get
        {
            switch (style)
            {
                case Style.Runner: return 0.5f;
                case Style.Brute: return 1.4f;
                case Style.Bloater: return 0.35f;
                default: return 0.9f;
            }
        }
    }

    private bool InAttackRange()
    {
        if (enemy == null || !enemy.IsActive || !enemy.IsChasing || enemy.Target == null || enemy.IsStaggered)
            return false;

        Vector3 offset = enemy.Target.position - transform.position;
        offset.y = 0f;
        return offset.magnitude < transform.lossyScale.x * 0.5f + 1.3f;
    }

    private void AnimateZombieArms(float dt)
    {
        // Attacks run a whole swing once started, and keep coming while the player is close.
        if (attackTime >= 0f)
        {
            attackTime += dt / AttackPeriod;

            if (attackTime >= 1f)
                attackTime = InAttackRange() ? 0f : -1f;
        }
        else if (InAttackRange())
        {
            attackTime = 0f;
        }

        if (attackTime >= 0f)
        {
            AttackPose(attackTime);
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

    // Arm positions through one attack (t from 0 to 1). Negative X raises a reaching arm.
    private void AttackPose(float t)
    {
        switch (style)
        {
            case Style.Runner:
            {
                // Alternating claws: one arm rakes down, then the other.
                float left = Claw(Mathf.Repeat(t * 2f, 1f), t < 0.5f);
                float right = Claw(Mathf.Repeat(t * 2f, 1f), t >= 0.5f);
                Pose(armL, Quaternion.Euler(left, 0f, 0f));
                Pose(armR, Quaternion.Euler(right, 0f, 0f));
                break;
            }
            case Style.Brute:
            {
                // Both fists up over the head, then down together.
                float up = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.45f, t));
                float down = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.6f, t));
                float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, t));
                float angle = Mathf.Lerp(Mathf.Lerp(0f, -150f, up), -35f, down) * (1f - back);
                Pose(armL, Quaternion.Euler(angle, 0f, -10f * up * (1f - back)));
                Pose(armR, Quaternion.Euler(angle, 0f, 10f * up * (1f - back)));
                break;
            }
            case Style.Bloater:
            {
                // Frantic flailing.
                float flail = Mathf.Sin(t * Mathf.PI * 2f);
                Pose(armL, Quaternion.Euler(-40f * flail, 0f, 25f * flail));
                Pose(armR, Quaternion.Euler(40f * flail, 0f, -25f * flail));
                break;
            }
            default:
            {
                // Two-handed swipe: raise, then strike down and through.
                float swing = Claw(t, true);
                Pose(armL, Quaternion.Euler(swing, 8f, 0f));
                Pose(armR, Quaternion.Euler(swing, -8f, 0f));
                break;
            }
        }
    }

    // A single arm's swing: raise (wind-up), strike down past rest, recover. When not this arm's
    // turn, it stays near rest.
    private static float Claw(float t, bool active)
    {
        if (!active)
            return 10f;

        if (t < 0.4f)
            return Mathf.Lerp(0f, -60f, Mathf.SmoothStep(0f, 1f, t / 0.4f));

        if (t < 0.55f)
            return Mathf.Lerp(-60f, 45f, Mathf.SmoothStep(0f, 1f, (t - 0.4f) / 0.15f));

        return Mathf.Lerp(45f, 0f, Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.45f));
    }

    private float AttackLean()
    {
        if (attackTime < 0f)
            return 0f;

        float t = attackTime;
        float windUp = Mathf.InverseLerp(0f, 0.4f, t);
        float strike = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.4f, 0.8f, t)) * Mathf.PI);
        float amount = style == Style.Brute ? 22f : style == Style.Bloater ? 6f : 14f;
        return -amount * 0.4f * windUp * (1f - Mathf.InverseLerp(0.4f, 0.5f, t)) + amount * strike;
    }

    // ---------- Helpers ----------

    private static void Pose(Limb limb, Quaternion offset)
    {
        if (limb.Bone != null)
            limb.Bone.localRotation = limb.Rest * offset;
    }
}
