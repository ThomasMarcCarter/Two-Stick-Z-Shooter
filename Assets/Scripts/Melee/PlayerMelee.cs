using System;
using System.Collections.Generic;
using UnityEngine;

// Melee (V / B): a swing in an arc in front of the player that hits the nearest few zombies in
// it (3 to start with), knocking them back and staggering them for a moment. It costs a little
// stamina and can't be used while winded, but it's much quieter than a gunshot and needs no
// ammo. Walls and pillars shield what's behind them, and boxes and trash in the arc get hit too.
// Upgrades picked up during the run (MeleeUpgradePickup) add to it, up to each one's max level.
// Added to the player by PlayerWeapon. SwingProgress times the swing's animation (the player
// sprite's melee clip and the held gun's arc).
public class PlayerMelee : MonoBehaviour
{
    private const float SwingTime = 0.32f;   // seconds the swing's animation takes

    [Header("Base Swing")]
    [SerializeField] private int damage = 1;
    [SerializeField] private int maxTargets = 3;
    [SerializeField] private float reach = 1.7f;
    [Tooltip("Width of the swing, in degrees.")]
    [SerializeField] private float arc = 110f;
    [SerializeField] private float cooldown = 0.55f;
    [SerializeField] private float knockback = 7f;
    [SerializeField] private float stagger = 0.35f;
    [SerializeField] private float staminaCost = 15f;
    [Tooltip("How far the swing carries, as a multiple of hearing range.")]
    [SerializeField] private float swingLoudness = 0.35f;
    [Tooltip("A hit carries a little further than the swing.")]
    [SerializeField] private float hitLoudness = 0.6f;

    private readonly int[] levels = new int[MeleeUpgrades.All.Length];
    private readonly List<(Enemy enemy, float distance)> candidates = new List<(Enemy, float)>();

    private PlayerStamina stamina;
    private PlayerHealth health;
    private QuickBar quickBar;
    private float nextSwing;
    private float swungAt = -10f;

    // How far through the swing's animation it is (0 to 1), or -1 when not swinging.
    public float SwingProgress
    {
        get
        {
            float t = (Time.time - swungAt) / SwingTime;
            return t >= 0f && t < 1f ? t : -1f;
        }
    }

    public event Action Changed;

    public static PlayerMelee For(GameObject owner)
    {
        PlayerMelee melee = owner.GetComponent<PlayerMelee>();
        return melee != null ? melee : owner.AddComponent<PlayerMelee>();
    }

    // ---------- Stats, with upgrades ----------

    public int Level(MeleeUpgrade upgrade) => levels[(int)upgrade];
    public bool IsMaxed(MeleeUpgrade upgrade) => Level(upgrade) >= MeleeUpgrades.MaxLevel(upgrade);

    public int Damage => damage + Level(MeleeUpgrade.Edge);
    public int MaxTargets => maxTargets + 2 * Level(MeleeUpgrade.Sweep);
    public float Reach => reach + 0.35f * Level(MeleeUpgrade.Reach);
    public float Arc => arc + 20f * Level(MeleeUpgrade.Reach);
    public float Cooldown => cooldown * Mathf.Pow(0.8f, Level(MeleeUpgrade.QuickHands));
    public float Knockback => knockback * (1f + 0.5f * Level(MeleeUpgrade.Weight));
    public float Stagger => stagger * (1f + 0.5f * Level(MeleeUpgrade.Weight));
    public float StaminaCost => staminaCost * Mathf.Pow(0.65f, Level(MeleeUpgrade.Conditioning));

    public int UpgradeCount
    {
        get
        {
            int total = 0;

            foreach (int level in levels)
                total += level;

            return total;
        }
    }

    // False if it's already at its max level.
    public bool AddUpgrade(MeleeUpgrade upgrade)
    {
        if (IsMaxed(upgrade))
            return false;

        levels[(int)upgrade]++;
        Changed?.Invoke();
        return true;
    }

    // ---------- Swinging ----------

    private void Awake()
    {
        stamina = GetComponent<PlayerStamina>();
        health = GetComponent<PlayerHealth>();
        quickBar = QuickBar.For(gameObject);
    }

    private void Update()
    {
        if (GameFlow.InputBlocked || (health != null && health.IsDead))
            return;

        if (!GameInput.Melee.WasPressedThisFrame() || Time.time < nextSwing)
            return;

        // Hands full with a medkit, or too winded to swing.
        if ((quickBar != null && quickBar.IsUsing) || (stamina != null && stamina.Exhausted))
        {
            SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-deny", 1, i => ProceduralAudio.AttachmentDeny())), 0.3f);
            return;
        }

        Swing();
    }

    private void Swing()
    {
        nextSwing = Time.time + Cooldown;
        swungAt = Time.time;

        if (stamina != null && !stamina.Boosted)
            stamina.Spend(StaminaCost);

        Vector3 origin = Flat(transform.position);
        Vector3 forward = Flat(transform.forward).normalized;

        MeleeSwing.Show(origin, forward, Reach, Arc);
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("throw", 2, ProceduralAudio.Throw)), 0.4f, UnityEngine.Random.Range(0.7f, 0.8f));
        Noise.Emit(origin, swingLoudness, Noise.Kind.Melee);

        int hit = HitEnemies(origin, forward) + HitDummies(origin, forward);
        HitBoxes(origin, forward);

        if (hit > 0)
        {
            SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("melee.hit", 3, ProceduralAudio.MeleeHit)), 0.6f, UnityEngine.Random.Range(0.92f, 1.08f));
            Noise.Emit(origin, hitLoudness, Noise.Kind.Melee);
            CameraFollow.Shake(0.05f + 0.02f * Mathf.Min(hit, 4));
        }
    }

    // The nearest zombies in the arc, up to the most it can hit. Returns how many it hit.
    private int HitEnemies(Vector3 origin, Vector3 forward)
    {
        candidates.Clear();
        Vector3 eye = origin + Vector3.up * 0.5f;

        foreach (Collider collider in Physics.OverlapSphere(eye, Reach + 1f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!collider.TryGetComponent(out Enemy enemy) || !enemy.IsActive)
                continue;

            // Measured to the near side of its body, so big zombies aren't harder to reach.
            Vector3 offset = Flat(collider.bounds.center) - origin;
            float radius = Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z);
            float distance = Mathf.Max(0f, offset.magnitude - radius);

            if (distance > Reach)
                continue;

            // Something right on top of the player counts as in front of them.
            if (offset.magnitude > radius + 0.2f && Vector3.Angle(forward, offset) > Arc / 2f)
                continue;

            if (WallBetween(eye, Flat(collider.bounds.center) + Vector3.up * 0.5f))
                continue;

            candidates.Add((enemy, distance));
        }

        candidates.Sort((a, b) => a.distance.CompareTo(b.distance));
        bool haveMaterials = ImpactEffects.TryGetMaterials(out ImpactEffects.Materials materials);
        int count = Mathf.Min(MaxTargets, candidates.Count);

        for (int i = 0; i < count; i++)
        {
            Enemy enemy = candidates[i].enemy;
            Vector3 away = Flat(enemy.transform.position) - origin;
            Vector3 direction = away.sqrMagnitude > 0.01f ? away.normalized : forward;

            if (haveMaterials)
                ImpactEffects.FleshHit(enemy.transform.position - direction * 0.4f, -direction, direction * 5f, enemy.transform, materials);

            enemy.Knock(direction * Knockback, Stagger);
            enemy.TakeDamage(Damage);
        }

        return count;
    }

    // The hideout's training dummies in the arc take the blow too.
    private int HitDummies(Vector3 origin, Vector3 forward)
    {
        int count = 0;

        foreach (Collider collider in Physics.OverlapSphere(origin + Vector3.up * 0.5f, Reach + 0.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!collider.TryGetComponent(out TrainingDummy dummy))
                continue;

            Vector3 offset = Flat(collider.bounds.center) - origin;

            if (offset.magnitude > 0.5f && Vector3.Angle(forward, offset) > Arc / 2f)
                continue;

            Vector3 direction = offset.sqrMagnitude > 0.01f ? offset.normalized : forward;
            dummy.Hit(Damage, collider.ClosestPoint(origin + Vector3.up * 1.1f), direction);
            count++;
        }

        return count;
    }

    private void HitBoxes(Vector3 origin, Vector3 forward)
    {
        if (!ImpactEffects.TryGetMaterials(out ImpactEffects.Materials materials))
            return;

        HashSet<Component> done = new HashSet<Component>();

        foreach (Collider collider in Physics.OverlapSphere(origin + Vector3.up * 0.5f, Reach, ~0, QueryTriggerInteraction.Ignore))
        {
            // Boxes, and props like trash bags.
            Component target = collider.GetComponentInParent<CardboardBox>();

            if (target == null)
                target = collider.GetComponentInParent<Prop>();

            if (target == null || !done.Add(target))
                continue;

            Vector3 offset = Flat(collider.bounds.center) - origin;

            if (Vector3.Angle(forward, offset) > Arc / 2f)
                continue;

            Vector3 point = collider.ClosestPoint(origin + Vector3.up * 0.5f);

            if (target is CardboardBox box)
                box.Hit(point, -offset.normalized, offset.normalized, materials);
            else
                ((Prop)target).Hit(point, -offset.normalized, offset.normalized, materials);
        }
    }

    private static bool WallBetween(Vector3 from, Vector3 to)
    {
        foreach (RaycastHit hit in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.CompareTag("Wall"))
                return true;
        }

        return false;
    }

    private static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }
}
