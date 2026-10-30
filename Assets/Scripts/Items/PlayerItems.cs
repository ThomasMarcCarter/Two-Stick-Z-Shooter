using System;
using System.Collections.Generic;
using UnityEngine;

// The run items the player holds (RunItems), and the ones whose effects don't belong to anything
// else: stuns and shoves, fire trails, kill effects. Others are checked where they apply with
// PlayerItems.Has (Gun, Projectile, PlayerDodge, PlayerHealth, PlayerMelee, FloorGenerator). It also
// holds the armour plates from the Plate Carrier: each stops 1 point of damage before health does.
// Lives on the player, which the scene reload at the end of a run replaces, so it all goes then.
public class PlayerItems : MonoBehaviour
{
    public const int PlateSlots = 3;

    private const float RattleRadius = 5f;
    private const float RattleStun = 1.5f;
    private const float RattleLoudness = 1.5f;
    private const float LandingRadius = 3f;
    private const float LandingPush = 9f;
    private const float LandingStun = 0.5f;
    private const float CorpseDelay = 1f;

    // Where a roll's fires go: each lights once the player is this far along, this far behind them.
    private const int FuelFires = 3;
    private const float FuelStart = 0.4f;
    private const float FuelSpacing = 1.1f;
    private const float FuelClearance = 1.1f;

    private static PlayerItems current;

    private readonly List<RunItem> held = new List<RunItem>();
    private PlayerDodge dodge;
    private PlayerWeapon weapon;
    private Vector3 rollStart;
    private int rollFires;

    public static PlayerItems Current => current;
    public IReadOnlyList<RunItem> Held => held;
    public int Plates { get; private set; }
    public int MaxPlates => Holds(RunItem.PlateCarrier) ? PlateSlots : 0;
    public bool PlatesFull => Plates >= MaxPlates;

    public event Action Changed;

    public static bool Has(RunItem item) => current != null && current.Holds(item);

    public static PlayerItems For(GameObject owner)
    {
        PlayerItems items = owner.GetComponent<PlayerItems>();
        return items != null ? items : owner.AddComponent<PlayerItems>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        current = null;
    }

    private void Awake()
    {
        current = this;
        dodge = PlayerDodge.For(gameObject);
        weapon = GetComponent<PlayerWeapon>();
        dodge.Started += OnRollStarted;
        dodge.Landed += OnLanded;
        Gun.Reloaded += OnReloaded;
    }

    private void OnDestroy()
    {
        Gun.Reloaded -= OnReloaded;
    }

    public bool Holds(RunItem item) => held.Contains(item);

    // False if it's already held. The carrier comes with its plates in.
    public bool Add(RunItem item)
    {
        if (Holds(item))
            return false;

        held.Add(item);

        if (item == RunItem.PlateCarrier)
            Plates = PlateSlots;

        if (item == RunItem.DinnerBell)
        {
            FloorGenerator floor = FindAnyObjectByType<FloorGenerator>();

            if (floor != null)
                floor.RingDinnerBell();
        }

        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-fit", 3, ProceduralAudio.AttachmentFit)), 0.6f, 0.7f);
        Changed?.Invoke();
        return true;
    }

    // ---------- Armour plates ----------

    public bool AddPlate()
    {
        if (PlatesFull)
            return false;

        Plates++;
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("metal.clank", 3, ProceduralAudio.MetalClank)), 0.45f, 1.2f);
        Changed?.Invoke();
        return true;
    }

    // Takes a hit on the plates first. Returns the damage left for health.
    public static int AbsorbHit(int amount)
    {
        if (current == null || current.Plates <= 0)
            return amount;

        int stopped = Mathf.Min(amount, current.Plates);
        current.Plates -= stopped;
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("metal.clank", 3, ProceduralAudio.MetalClank)), 0.7f, 0.85f);
        current.Changed?.Invoke();
        return amount - stopped;
    }

    // ---------- What the items do ----------

    // An enemy died (FloorGenerator hears of every one).
    public static void EnemyKilled(Enemy dead, bool boss, Transform room)
    {
        if (current != null)
            current.OnEnemyKilled(dead, boss, room);
    }

    // A zombie's shot passed through the player.
    public static void ShotPassed(PlayerHealth player)
    {
        if (Has(RunItem.CloseCall) && current.dodge.Invulnerable)
            current.dodge.Refund();
    }

    // The blast from a Last Word round or a Corpse Bloat: smaller than a grenade's.
    public static Blast.Settings SmallBlast(int maxDamage)
    {
        Blast.Settings blast = Blast.Grenade;
        blast.Radius = 2.5f;
        blast.MaxDamage = maxDamage;
        blast.PlayerRadius = 2f;
        blast.PlayerDamage = 1;
        blast.PlayerDamageOuter = 1;
        blast.Push = 6f;
        blast.Loudness = 1f;
        blast.Shake = 0.2f;
        blast.BreaksProps = false;
        return blast;
    }

    private void OnEnemyKilled(Enemy dead, bool boss, Transform room)
    {
        if (Holds(RunItem.HungryMag) && weapon != null && weapon.Active.Gun != null)
        {
            Gun gun = weapon.Active.Gun;
            gun.SetCurrentAmmo(Mathf.Min(gun.GetMagazineSize(), gun.GetCurrentAmmo() + 1));
        }

        if (Holds(RunItem.CorpseBloat) && !boss)
            DelayedBlast.Start(dead.transform.position, room, SmallBlast(3), CorpseDelay);
    }

    private void OnReloaded(Gun gun)
    {
        if (!Holds(RunItem.RattleMag))
            return;

        Shock(RattleRadius, 0f, RattleStun, RattleLoudness);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("metal.clank", 3, ProceduralAudio.MetalClank)), transform.position, 1f, 0.6f);
        CameraFollow.Shake(0.12f);
    }

    private void OnLanded(PlayerDodge roll)
    {
        LightFuelTrail();

        if (Holds(RunItem.TumbleLoader) && weapon != null && weapon.Active.Gun != null)
            weapon.Active.Gun.InstantReload();

        if (Holds(RunItem.HardLanding))
        {
            Shock(LandingRadius, LandingPush, LandingStun, 1f);
            SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("melee.hit", 3, ProceduralAudio.MeleeHit)), transform.position, 0.9f, 0.6f);
            CameraFollow.Shake(0.18f);
        }
    }

    // Staggers and shoves nearby zombies, with a noise as a multiple of a gunshot.
    private void Shock(float radius, float push, float stun, float loudness)
    {
        Noise.Emit(transform.position, Noise.Gunshot * loudness, Noise.Kind.Smash);
        HashSet<Enemy> done = new HashSet<Enemy>();

        foreach (Collider collider in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!collider.TryGetComponent(out Enemy enemy) || !enemy.IsActive || !done.Add(enemy))
                continue;

            Vector3 away = enemy.transform.position - transform.position;
            away.y = 0f;
            enemy.Knock(away.normalized * push, stun);
        }
    }

    // Fuel Leak: fires light along the roll once the player's clear of them, as floor fire would
    // otherwise burn straight through the roll.
    private void Update()
    {
        if (dodge.Rolling)
            LightFuelTrail();
    }

    private void OnRollStarted(PlayerDodge roll)
    {
        rollStart = transform.position;
        rollFires = 0;
    }

    private void LightFuelTrail()
    {
        if (!Holds(RunItem.FuelLeak))
            return;

        Vector3 travelled = transform.position - rollStart;
        travelled.y = 0f;

        while (rollFires < FuelFires && travelled.magnitude >= FuelStart + FuelSpacing * rollFires + FuelClearance)
        {
            Vector3 spot = rollStart + dodge.Direction * (FuelStart + FuelSpacing * rollFires);
            FirePatch.Create(spot, FloorPickups.RoomAt(spot), 0.5f, 3f);
            rollFires++;
        }
    }
}
