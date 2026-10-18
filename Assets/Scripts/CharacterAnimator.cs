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
//   Zombies: swing in time with the Enemy's attack clock (the hit lands as the arms come down):
//   a two-handed swipe for walkers and
//   screamers, quick alternating claws for runners, an overhead double-fisted slam for brutes and
//   a frantic flailing for bloaters. They reel back when staggered.
//   Bosses: each has its own walk and poses for its moves (IBossMoves): the Butcher raises its
//   cleaver to wind up, leans into the charge with the blade out front, and reels about limp-armed
//   and lolling when dazed, chopping when it's on the player; the Broodmother's sac breathes and
//   her arms clutch, and she rears back with arms spread before lunging forward to spit; the
//   Howler lopes stooped with its arms dragging, rears up with its arms flung wide and head
//   thrown back to howl, and leans hard into a dodge.
//   Survivors: stand easy and breathe, turn their head to whoever they're looking at, gesture
//   while talking, and cower (crouched, arms over the head) while caged in a fight.
// Added by CharacterVisuals when a model is attached.
public class CharacterAnimator : MonoBehaviour
{
    public enum Style { Player, Walker, Runner, Brute, Bloater, Npc, Butcher, Broodmother, Howler }

    // Survivors (Style.Npc): what they're doing, set by HideoutNpc / CagedSurvivor.
    public bool Talking { get; set; }
    public bool Cowering { get; set; }
    public Transform LookAt { get; set; }

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
    private IBossMoves boss;
    private PlayerShooting shooting;
    private PlayerMovement movement;
    private PlayerStamina stamina;

    private Vector3 lastPosition;
    private Vector3 velocity;
    private float phase;
    private float walkBlend;
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
        boss = GetComponentInParent<IBossMoves>();
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
        else if (style == Style.Npc)
            AnimateNpcArms();
        else if (IsBoss)
            AnimateBossArms(dt);
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
                case Style.Npc: return 1.2f;
                case Style.Butcher: return 1.1f;
                case Style.Broodmother: return 0.5f;
                case Style.Howler: return 1.6f;
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
                case Style.Butcher: return Move == BossMove.Charge ? 40f : 24f;
                case Style.Broodmother: return 14f;
                case Style.Howler: return 34f;
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
            case Style.Npc:
                // Breathing, and a slow shift of weight.
                bob = bob * 0.035f + Mathf.Sin(Time.time * 1.6f + seed) * 0.006f;
                roll = (Mathf.PerlinNoise(seed, Time.time * 0.2f) - 0.5f) * 4f;
                cower = Mathf.MoveTowards(cower, Cowering ? 1f : 0f, dt * 3f);
                lean = 28f * cower + (Talking ? Mathf.Sin(Time.time * 3f) * 2f : 0f);
                drop = 0.3f * cower;
                break;
            case Style.Butcher:
            case Style.Broodmother:
            case Style.Howler:
                BossBody(dt, ref bob, ref lean, ref roll);
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
            // Zombies' heads loll; the player's stays steady; survivors look about.
            if (style == Style.Npc)
                AnimateNpcHead(dt);
            else if (IsBoss)
                Pose(head, BossHead());
            else
            {
                float loll = style == Style.Player ? 0f : (Mathf.PerlinNoise(seed, Time.time * 0.7f) - 0.5f) * 24f;
                Pose(head, Quaternion.Euler(0f, 0f, loll));
            }
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

    // ---------- Survivors ----------

    private float cower;
    private float headYaw;

    // Turned (within reach of the neck) towards whoever they're looking at, otherwise glancing
    // around now and then; a nod while talking.
    private void AnimateNpcHead(float dt)
    {
        float wanted = (Mathf.PerlinNoise(seed + 5f, Time.time * 0.15f) - 0.5f) * 70f;

        if (LookAt != null)
        {
            Vector3 local = transform.InverseTransformPoint(LookAt.position);
            wanted = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -65f, 65f);
        }

        headYaw = Mathf.LerpAngle(headYaw, wanted, 1f - Mathf.Exp(-6f * dt));
        float nod = Talking ? Mathf.Sin(Time.time * 7f) * 5f : 0f;
        Pose(head, Quaternion.Euler(nod - 12f * cower, headYaw, 0f));
    }

    // Arms hang and sway with the stride; one hand gestures while talking; both come up over
    // the head when cowering.
    private void AnimateNpcArms()
    {
        float sway = Mathf.Sin(phase) * walkBlend * 18f;
        float idle = (Mathf.PerlinNoise(seed + 10f, Time.time * 0.4f) - 0.5f) * 6f;
        float gesture = Talking ? (0.5f + 0.5f * Mathf.Sin(Time.time * 4.5f + seed)) : 0f;

        // Negative X brings an arm forward and up.
        Quaternion left = Quaternion.Euler(sway + idle, 0f, 0f);
        Quaternion right = Quaternion.Euler(-sway - idle - 55f * gesture, 0f, -15f * gesture);
        Quaternion shield = Quaternion.Euler(-150f, 0f, 0f);

        Pose(armL, Quaternion.Slerp(left, shield * Quaternion.Euler(0f, 0f, -20f), cower));
        Pose(armR, Quaternion.Slerp(right, shield * Quaternion.Euler(0f, 0f, 20f), cower));
    }

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
        float t = enemy != null ? enemy.AttackTime : -1f;

        if (t < 0f)
            return 0f;

        float windUp = Mathf.InverseLerp(0f, 0.4f, t);
        float strike = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.4f, 0.8f, t)) * Mathf.PI);
        float amount = style == Style.Brute ? 22f : style == Style.Bloater ? 6f : 14f;
        return -amount * 0.4f * windUp * (1f - Mathf.InverseLerp(0.4f, 0.5f, t)) + amount * strike;
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
                float cleaver = chop >= 0f ? Claw(chop, true) * 1.6f : 0f;
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

    // ---------- Helpers ----------

    private static void Pose(Limb limb, Quaternion offset)
    {
        if (limb.Bone != null)
            limb.Bone.localRotation = limb.Rest * offset;
    }
}
