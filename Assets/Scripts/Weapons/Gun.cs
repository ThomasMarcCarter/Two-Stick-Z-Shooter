using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class Gun : MonoBehaviour
{
    public enum FireMode
    {
        Semi,
        Auto
    }

    public enum ReloadStyle
    {
        Magazine,   // the whole magazine at once, after Reload Time
        PerShell    // one round at a time, Shell Reload Time each; firing interrupts it
    }

    // Which equipment slot the gun goes in when picked up.
    public enum Slot
    {
        Main,
        Sidearm
    }

    // Silhouette drawn for the gun on the equipment HUD and inventory (see WeaponSilhouette).
    // Append new ones at the end: prefabs store the number.
    public enum Icon
    {
        Pistol,
        Shotgun,
        Rifle,          // AK-47
        Carbine,        // M4A1
        SMG,            // MP5
        MachinePistol,  // Uzi
        Revolver,
        DoubleBarrel,
        BoltAction      // hunting rifle
    }

    [Header("Equipment")]
    [SerializeField] private Slot slot = Slot.Sidearm;
    [SerializeField] private Icon icon = Icon.Pistol;
    [Tooltip("Reloads take this ammo from the player's inventory.")]
    [SerializeField] private AmmoType ammoType = AmmoType.NineMil;

    [Header("Gun Settings")]
    [SerializeField] private FireMode fireMode = FireMode.Semi;
    [SerializeField] private int magazineSize = 12;
    [SerializeField] private float fireRate = 5f;
    [SerializeField] private float reloadTime = 1.5f;
    [SerializeField] private ReloadStyle reloadStyle = ReloadStyle.Magazine;
    [Tooltip("Per Shell only: time to load each round.")]
    [SerializeField] private float shellReloadTime = 0.5f;
    [Tooltip("Reloading while sneaking takes this much longer...")]
    [SerializeField] private float sneakReloadTimeMultiplier = 1.5f;
    [Tooltip("...and is this much quieter, both to hear and for enemies (whose hearing it reaches less far).")]
    [SerializeField, Range(0f, 1f)] private float sneakReloadVolume = 0.5f;

    [Header("Spread")]
    [Tooltip("Projectiles fired per shot. More than 1 makes a shotgun-style spread.")]
    [SerializeField] private int pelletsPerShot = 1;
    [Tooltip("Total width of the spread cone, in degrees.")]
    [SerializeField] private float spreadAngle = 0f;
    [Tooltip("Random speed variation per pellet (0.1 = up to 10% slower or faster), so pellets don't fly in a neat line.")]
    [SerializeField, Range(0f, 0.5f)] private float pelletSpeedVariation = 0f;

    [Header("Accuracy")]
    [Tooltip("How far a shot can stray from the aim, either way, in degrees, when settled and standing still. For a shotgun the whole pattern strays.")]
    [SerializeField, Min(0f)] private float inaccuracy = 2.5f;
    [Tooltip("Degrees added by each shot (recoil), so sustained fire gets wilder.")]
    [SerializeField, Min(0f)] private float bloomPerShot = 1f;
    [Tooltip("Most the bloom can add.")]
    [SerializeField, Min(0f)] private float maxBloom = 5f;
    [Tooltip("Degrees per second the bloom settles back by.")]
    [SerializeField, Min(0f)] private float bloomRecovery = 10f;
    [Tooltip("Inaccuracy multipliers while walking, sprinting and sneaking.")]
    [SerializeField] private float movingMultiplier = 1.3f;
    [SerializeField] private float sprintingMultiplier = 1.8f;
    [SerializeField] private float sneakingMultiplier = 0.8f;

    [Header("References")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform muzzle;

    [Header("Muzzle Flash")]
    [Tooltip("The flame sprite's material. No flash if empty.")]
    [SerializeField] private Material muzzleFlashMaterial;
    [Tooltip("Size of the flame and reach of its light: bigger for a shotgun, smaller for a pistol.")]
    [SerializeField] private float muzzleFlashSize = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource shotAudio;
    [Tooltip("Pitch of the shot sound: below 1 is deeper (a bigger gun), above 1 sharper. Lets guns share a clip.")]
    [SerializeField] private float shotPitch = 1f;
    [SerializeField] private AudioSource reloadAudio;
    [SerializeField] private AudioSource emptyAudio;
    [Tooltip("Per Shell only: played as each round goes in, through the reload audio source.")]
    [SerializeField] private AudioClip shellInsertClip;

    private int currentAmmo;
    private float nextFireTime;
    private bool isReloading;
    private MuzzleFlash muzzleFlash;
    private float reloadVolume = 1f;
    private PlayerMovement owner;
    private PlayerStamina ownerStamina;
    private Rigidbody ownerBody;
    private Coroutine reloadRoutine;
    private int lastTryFireFrame = -10;
    private bool dryClickedThisPull;

    // Bloom as of the last shot; it settles back from there over time (see Bloom).
    private float bloomAtShot;
    private float lastShotTime = -100f;

    // Fitted attachments, one per slot (see WeaponAttachment). The stats below include them.
    private readonly List<WeaponAttachment> attachments = new List<WeaponAttachment>();
    private GameObject suppressorModel;
    private LaserSight laserSight;

    public IReadOnlyList<WeaponAttachment> Attachments => attachments;
    public event System.Action AttachmentsChanged;
    public bool Suppressed => HidesFlash;
    public LaserSight Laser => laserSight;

    public int PelletsPerShot => Mathf.Max(1, pelletsPerShot);
    public Transform Muzzle => muzzle;
    public GameObject ProjectilePrefab => projectilePrefab;
    public FireMode BaseFireMode => fireMode;
    public ReloadStyle Reloading => reloadStyle;

    private float FireRate => fireRate * Product(a => a.fireRateMultiplier);
    private float ReloadTime => reloadTime * Product(a => a.reloadTimeMultiplier);
    private float ShellReloadTime => shellReloadTime * Product(a => a.reloadTimeMultiplier);
    private float Spread => spreadAngle * Product(a => a.spreadMultiplier);
    private float ProjectileSpeed => Product(a => a.projectileSpeedMultiplier);
    private float AccuracyMultiplier => Product(a => a.accuracyMultiplier);

    private float Bloom => Mathf.Max(0f, bloomAtShot - bloomRecovery * (Time.time - lastShotTime));

    // How far the next shot can stray either way, in degrees: the gun's own inaccuracy plus
    // recoil bloom, worse on the move and better sneaking, scaled by attachments (a laser
    // sight roughly halves it). The crosshair draws this.
    public float CurrentInaccuracy => (inaccuracy + Bloom) * MovementMultiplier * AccuracyMultiplier;

    private float MovementMultiplier
    {
        get
        {
            if (owner == null)
            {
                owner = GetComponentInParent<PlayerMovement>();
                ownerStamina = owner != null ? owner.GetComponent<PlayerStamina>() : null;
                ownerBody = owner != null ? owner.GetComponent<Rigidbody>() : null;
            }

            if (owner == null)
                return 1f;

            if (owner.IsSneaking)
                return sneakingMultiplier;

            if (ownerStamina != null && ownerStamina.IsSprinting)
                return sprintingMultiplier;

            Vector3 velocity = ownerBody != null ? ownerBody.linearVelocity : Vector3.zero;
            velocity.y = 0f;
            return velocity.magnitude > 1f ? movingMultiplier : 1f;
        }
    }
    private float Loudness => Product(a => a.loudnessMultiplier);
    private bool HidesFlash => attachments.Exists(a => a.hidesMuzzleFlash);

    private int BonusDamage
    {
        get
        {
            int bonus = 0;

            foreach (WeaponAttachment attachment in attachments)
                bonus += attachment.bonusDamage;

            return bonus;
        }
    }

    private float Product(System.Func<WeaponAttachment, float> stat)
    {
        float product = 1f;

        foreach (WeaponAttachment attachment in attachments)
            product *= stat(attachment);

        return product;
    }

    public bool IsReloading => isReloading;

    public float ReloadProgress { get; private set; }

    public Slot EquipSlot => slot;
    public Icon HudIcon => icon;

    // The height (world Y) shots travel at, whatever height the gun is held at. Zombie bodies,
    // low cover (boxes, props) and blasts are sized for shots at this height; the guns are held
    // higher, at mid-torso, so each bullet's tracer is drawn lifted up to the muzzle (Projectile)
    // and the laser sight finds its target at this height but is drawn from the muzzle.
    public const float ShotHeight = 0.51f;

    // The height the cursor aims at: about where guns are held (the muzzle), not ShotHeight. The
    // tracer is drawn level at the muzzle, so aiming at this height makes it run straight through
    // the crosshair; it's also roughly the middle of a zombie on screen. Sprites are drawn smaller
    // and hold their guns lower (GunSprite puts the muzzle at this height).
    public static float AimHeight => SpriteVisuals.Current != null ? SpriteAimHeight : ModelAimHeight;
    public const float ModelAimHeight = 1.13f;
    public const float SpriteAimHeight = 0.75f;
    private const float SuppressedVolume = 0.8f;

    // The name shown for a gun prefab: its asset name upper-cased, with words split where a
    // lower-case letter meets a capital ("DoubleBarrel" -> "DOUBLE BARREL", "AK47" unchanged).
    // Shots don't use ammo (the hideout's shooting range). Set by the FloorGenerator per floor.
    public static bool FreeAmmo { get; set; }

    // The shot every suppressed gun plays instead of its own (the FloorGenerator's Suppressed
    // Shot Sound). Without one, a suppressed gun plays its own shot quieter and higher.
    public static AudioClip SuppressedShot { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetFreeAmmo()
    {
        FreeAmmo = false;
        SuppressedShot = null;
    }

    public static string DisplayName(GameObject weapon)
    {
        if (weapon == null)
            return "";

        string name = weapon.name;
        System.Text.StringBuilder text = new System.Text.StringBuilder(name.Length + 4);

        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1]))
                text.Append(' ');

            text.Append(name[i]);
        }

        return text.ToString().ToUpperInvariant();
    }
    public AmmoType AmmoType => ammoType;

    // Spare rounds of this gun's ammo the player is carrying.
    public int Reserve => Inventory != null ? Inventory.Count(ammoType) : 0;

    private AmmoInventory inventory;
    private AmmoInventory Inventory => inventory != null ? inventory : inventory = GetComponentInParent<AmmoInventory>();

    private void Awake()
    {
        currentAmmo = Random.Range(1, magazineSize + 1);

        if (reloadAudio != null)
            reloadVolume = reloadAudio.volume;

        Material flame = SpriteVisuals.Current != null && SpriteVisuals.Current.muzzleFlame != null
            ? SpriteVisuals.Current.muzzleFlame : muzzleFlashMaterial;

        if (muzzle != null && flame != null)
            muzzleFlash = MuzzleFlash.Create(muzzle, flame, muzzleFlashSize);
    }

    // Holstered (switched to the other slot) mid-reload: the coroutine stops with the object,
    // so clear the reload too or it would stay "reloading" forever.
    private void OnDisable()
    {
        if (isReloading)
        {
            CancelReload();

            if (reloadAudio != null)
                reloadAudio.Stop();
        }
    }

    public void TryFire()
    {
        // Full auto calls this every frame the trigger is held, so a call straight after one on
        // the last frame is the same pull; only a fresh pull can dry-click again.
        bool newPull = Time.frameCount - lastTryFireFrame > 1;
        lastTryFireFrame = Time.frameCount;

        if (newPull)
            dryClickedThisPull = false;

        if (isReloading)
        {
            // A shell-by-shell reload can be cut short to fire whatever is already loaded.
            if (reloadStyle == ReloadStyle.PerShell && currentAmmo > 0)
                CancelReload();
            else
                return;
        }

        if (Time.time < nextFireTime)
            return;

        // Debug god mode: the magazine never runs dry.
        if (HolderInGodMode)
            currentAmmo = GetMagazineSize();

        if (currentAmmo <= 0)
        {
            if (!dryClickedThisPull)
            {
                dryClickedThisPull = true;
                DryClick();
            }

            return;
        }

        Fire();
    }

    // The empty click: never restarted or stacked while one is still playing (a one-shot a
    // frame from a held trigger used up the audio voices and cut the music out).
    private void DryClick()
    {
        if (emptyAudio == null || emptyAudio.clip == null || emptyAudio.isPlaying)
            return;

        emptyAudio.Play();
    }

    // The point under the cursor at AimHeight, whatever the camera's tilt.
    public static bool PointerAimPoint(out Vector3 point)
    {
        point = default;
        Camera camera = Camera.main;

        if (camera == null)
            return false;

        Ray ray = camera.ScreenPointToRay(GameInput.Pointer);
        Plane aimPlane = new Plane(Vector3.up, Vector3.up * AimHeight);

        if (!aimPlane.Raycast(ray, out float distance))
            return false;

        point = ray.GetPoint(distance);
        return true;
    }

    private void Fire()
    {
        if (!PointerAimPoint(out Vector3 mouseWorldPosition))
            return;

        Vector3 spawn = SpawnPoint();
        Vector3 direction = ShotDirection(mouseWorldPosition, spawn);

        // The whole shot strays, more often a little than a lot (the average of two rolls).
        float stray = CurrentInaccuracy;
        float angle = (Random.Range(-stray, stray) + Random.Range(-stray, stray)) / 2f;
        direction = Quaternion.AngleAxis(angle, Vector3.up) * direction;

        bloomAtShot = Mathf.Min(maxBloom, Bloom + bloomPerShot);
        lastShotTime = Time.time;
        LaunchPellets(spawn, direction);

        // One shell per shot, however many pellets it holds (none in the hideout, or in god mode).
        if (!FreeAmmo && !HolderInGodMode)
            currentAmmo--;

        if (muzzleFlash != null && !HidesFlash)
            muzzleFlash.Fire();

        // Out of the ejection port, a little behind the muzzle.
        Vector3 port = muzzle != null ? Vector3.Lerp(transform.root.position, muzzle.position, 0.65f) : transform.position;
        port.y = muzzle != null ? muzzle.position.y : AimHeight;
        ShellCasing.Eject(port, direction, ammoType == AmmoType.ShotgunShell);

        PlayShot();
        nextFireTime = Time.time + (1f / FireRate);
    }

    // From where the round starts, so it flies through the point under the crosshair (the gun is
    // held off to one side of the player). With the cursor between the player and the muzzle, or
    // behind it, straight ahead the way the player faces, so a shot never leaves in a direction
    // the player isn't pointing.
    private Vector3 ShotDirection(Vector3 mouseWorldPosition, Vector3 spawn)
    {
        Vector3 fromBody = mouseWorldPosition - transform.root.position;
        fromBody.y = 0f;
        Vector3 direction = mouseWorldPosition - spawn;
        direction.y = 0f;
        // The way the gun points: out of the body to the muzzle (a gun sprite turns to the
        // cursor straight away, ahead of the body turning).
        Vector3 facing = muzzle != null ? muzzle.position - transform.root.position : transform.root.forward;
        facing.y = 0f;

        if (facing.sqrMagnitude < 0.0001f)
            facing = transform.root.forward;

        if (direction.sqrMagnitude < 0.01f || Vector3.Dot(direction, fromBody) <= 0f || Vector3.Dot(direction, facing) <= 0f)
            direction = facing;

        return direction.normalized;
    }

    // A projectile per pellet, spread across the cone (PelletAngle), each at a slightly random
    // speed.
    private void LaunchPellets(Vector3 spawn, Vector3 direction)
    {
        int pellets = Mathf.Max(1, pelletsPerShot);

        for (int i = 0; i < pellets; i++)
        {
            Vector3 pelletDirection = Quaternion.AngleAxis(PelletAngle(i, pellets), Vector3.up) * direction;

            GameObject projectile = Instantiate(
                projectilePrefab,
                spawn,
                Quaternion.LookRotation(pelletDirection)
            );

            projectile.GetComponent<Projectile>().Initialize(
                pelletDirection,
                transform.root.gameObject,
                ProjectileSpeed * (1f + Random.Range(-pelletSpeedVariation, pelletSpeedVariation)),
                BonusDamage,
                muzzle.position.y - spawn.y
            );
        }
    }

    // A suppressed shot plays the suppressed sound (pitched by the gun, so each still sounds its
    // own); without one, the gun's own shot quieter and a little higher. Either way it's heard.
    private void PlayShot()
    {
        float loudness = Loudness;

        if (HidesFlash && SuppressedShot != null)
        {
            shotAudio.pitch = shotPitch * Random.Range(0.92f, 1.08f);
            shotAudio.PlayOneShot(SuppressedShot, SuppressedVolume);
        }
        else
        {
            shotAudio.pitch = shotPitch * Random.Range(0.95f, 1.05f) * (loudness < 1f ? 1.15f : 1f);
            shotAudio.PlayOneShot(shotAudio.clip, Mathf.Clamp01(Mathf.Lerp(0.25f, 1f, loudness)));
        }

        Noise.Emit(transform.position, Noise.Gunshot * loudness, Noise.Kind.Gunshot);
    }

    // Where shots start: the muzzle, unless the barrel is poking through a wall or into an
    // enemy, in which case just short of whatever it's pushed into, so point-blank shots still
    // hit and shots can't start on the far side of a wall.
    // (At ShotHeight, under the muzzle: see ShotHeight.)
    private Vector3 SpawnPoint()
    {
        Vector3 from = new Vector3(transform.root.position.x, ShotHeight, transform.root.position.z);
        Vector3 muzzlePoint = new Vector3(muzzle.position.x, ShotHeight, muzzle.position.z);
        Vector3 toMuzzle = muzzlePoint - from;
        float length = toMuzzle.magnitude;

        if (length < 0.001f)
            return muzzlePoint;

        Vector3 direction = toMuzzle / length;
        RaycastHit[] hits = Physics.RaycastAll(from, direction, length, ~0, QueryTriggerInteraction.Ignore);
        float nearest = length;

        foreach (RaycastHit hit in hits)
        {
            // The shooter's own body doesn't count.
            if (hit.collider.transform.root == transform.root)
                continue;

            nearest = Mathf.Min(nearest, hit.distance);
        }

        return nearest < length ? from + direction * Mathf.Max(0f, nearest - 0.05f) : muzzlePoint;
    }

    // Pellets are spread evenly across the cone with a little jitter, so every shot covers the
    // whole cone but no two look the same.
    private float PelletAngle(int index, int count)
    {
        float spread = Spread;

        if (count <= 1 || spread <= 0f)
            return 0f;

        float slot = spread / count;
        float evenly = -spread / 2f + slot * (index + 0.5f);
        return evenly + Random.Range(-slot / 2f, slot / 2f);
    }

    // Reloading carefully while sneaking: slower and quieter.
    private bool Sneaking
    {
        get
        {
            if (owner == null)
                owner = GetComponentInParent<PlayerMovement>();

            return owner != null && owner.IsSneaking;
        }
    }

    public FireMode GetFireMode()
    {
        return attachments.Exists(a => a.fullAuto) ? FireMode.Auto : fireMode;
    }

    public int GetCurrentAmmo()
    {
        return currentAmmo;
    }

    // Including any magazine attachment.
    public int GetMagazineSize()
    {
        return Mathf.Max(1, Mathf.RoundToInt(magazineSize * Product(a => a.magazineMultiplier)));
    }

    // Held by the player with the debug menu's god mode on.
    private bool HolderInGodMode => transform.root.TryGetComponent(out PlayerHealth holder) && holder.GodMode;

    public void SetCurrentAmmo(int ammo)
    {
        currentAmmo = Mathf.Clamp(ammo, 0, GetMagazineSize());
    }
}