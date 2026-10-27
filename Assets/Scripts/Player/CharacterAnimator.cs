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
//   home; shell by shell, the gun stays tipped and the hand feeds it in a steady rhythm. Dying
//   (Die), the knees buckle and the body slumps, then it topples over backwards, arms flung out.
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
public partial class CharacterAnimator : MonoBehaviour
{
    public enum Style { Player, Walker, Runner, Brute, Bloater, Npc, Butcher, Broodmother, Howler }

    // Survivors (Style.Npc): what they're doing, set by HideoutNpc / CagedSurvivor.
    public bool Talking { get; set; }
    public bool Cowering { get; set; }
    public Transform LookAt { get; set; }

    // Dead (the player): plays the fall once, then lies still.
    public bool Dead => diedAt >= 0f;
    private float diedAt = -1f;

    // On real time, so the fall plays out at full speed through the death's slow motion and
    // finishes even once the death screen has paused the game.
    public void Die()
    {
        if (diedAt < 0f)
            diedAt = Time.unscaledTime;
    }

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

        if (Dead && torso.Bone != null)
        {
            AnimateDeath(Time.unscaledTime - diedAt);
            return;
        }

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

    // Knees buckle and the body slumps forward (first 0.25 s), then the whole model topples over
    // backwards about its feet with a small bounce as it lands, arms flung out and head lolling.
    private void AnimateDeath(float t)
    {
        float buckle = Mathf.SmoothStep(0f, 1f, t / 0.25f);
        float fall = Mathf.Clamp01((t - 0.2f) / 0.55f);
        float landed = fall * fall;                                           // speeds up as it goes over
        float bounce = t > 0.75f ? Mathf.Sin(Mathf.Clamp01((t - 0.75f) / 0.25f) * Mathf.PI) * 6f : 0f;

        transform.localRotation = Quaternion.Euler(-86f * landed + bounce, 0f, 0f);

        torso.Bone.localPosition = torso.RestPosition + Vector3.down * 0.12f * buckle * (1f - landed);
        torso.Bone.localRotation = torso.Rest * Quaternion.Euler(Mathf.Lerp(22f * buckle, -8f, landed), 0f, 0f);

        if (head.Bone != null)
            Pose(head, Quaternion.Euler(Mathf.Lerp(30f * buckle, -25f, landed), 0f, 20f * landed));

        if (armL.Bone != null)
            Pose(armL, Quaternion.Euler(-60f * landed, 0f, -15f * buckle - 55f * landed));

        if (armR.Bone != null)
            Pose(armR, Quaternion.Euler(-40f * landed, 0f, 15f * buckle + 65f * landed));

        if (legL.Bone != null)
            Pose(legL, Quaternion.Euler(25f * buckle * (1f - landed) - 10f * landed, 0f, -8f * landed));

        if (legR.Bone != null)
            Pose(legR, Quaternion.Euler(-15f * buckle * (1f - landed) + 12f * landed, 0f, 6f * landed));
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

    // ---------- Helpers ----------

    private static void Pose(Limb limb, Quaternion offset)
    {
        if (limb.Bone != null)
            limb.Bone.localRotation = limb.Rest * offset;
    }
}
