using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gun : MonoBehaviour
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

    // Bloom as of the last shot; it settles back from there over time (see Bloom).
    private float bloomAtShot;
    private float lastShotTime = -100f;

    // Fitted attachments, one per slot (see WeaponAttachment). The stats below include them.
    private readonly List<WeaponAttachment> attachments = new List<WeaponAttachment>();
    private GameObject suppressorModel;
    private LaserSight laserSight;

    public IReadOnlyList<WeaponAttachment> Attachments => attachments;
    public event System.Action AttachmentsChanged;

    public int PelletsPerShot => Mathf.Max(1, pelletsPerShot);
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
    // the crosshair with the tilted camera; it's also roughly the middle of a zombie on screen.
    public const float AimHeight = 1.13f;

    // The name shown for a gun prefab: its asset name upper-cased, with words split where a
    // lower-case letter meets a capital ("DoubleBarrel" -> "DOUBLE BARREL", "AK47" unchanged).
    // Shots don't use ammo (the hideout's shooting range). Set by the FloorGenerator per floor.
    public static bool FreeAmmo { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetFreeAmmo()
    {
        FreeAmmo = false;
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

        if (muzzle != null && muzzleFlashMaterial != null)
            muzzleFlash = MuzzleFlash.Create(muzzle, muzzleFlashMaterial, muzzleFlashSize);
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

        if (currentAmmo <= 0)
        {
            emptyAudio.PlayOneShot(emptyAudio.clip);
            return;
        }

        Fire();
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

        // From where the round starts, so it flies through the point under the crosshair (the
        // gun is held off to one side of the player). With the cursor between the player and
        // the muzzle, or behind it, straight ahead the way the player faces, so a shot never
        // leaves in a direction the player isn't pointing.
        Vector3 fromBody = mouseWorldPosition - transform.root.position;
        fromBody.y = 0f;
        Vector3 direction = mouseWorldPosition - spawn;
        direction.y = 0f;
        Vector3 facing = transform.root.forward;
        facing.y = 0f;

        if (direction.sqrMagnitude < 0.01f || Vector3.Dot(direction, fromBody) <= 0f || Vector3.Dot(direction, facing) <= 0f)
            direction = facing;

        direction.Normalize();

        // The whole shot strays, more often a little than a lot (the average of two rolls).
        float stray = CurrentInaccuracy;
        float angle = (Random.Range(-stray, stray) + Random.Range(-stray, stray)) / 2f;
        direction = Quaternion.AngleAxis(angle, Vector3.up) * direction;

        bloomAtShot = Mathf.Min(maxBloom, Bloom + bloomPerShot);
        lastShotTime = Time.time;

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

        // One shell per shot, however many pellets it holds (none in the hideout).
        if (!FreeAmmo)
            currentAmmo--;

        if (muzzleFlash != null && !HidesFlash)
            muzzleFlash.Fire();

        // A suppressed shot is quieter and a little higher: a crack rather than a bang.
        float loudness = Loudness;
        shotAudio.pitch = shotPitch * Random.Range(0.95f, 1.05f) * (loudness < 1f ? 1.15f : 1f);
        shotAudio.PlayOneShot(shotAudio.clip, Mathf.Clamp01(Mathf.Lerp(0.25f, 1f, loudness)));
        Noise.Emit(transform.position, Noise.Gunshot * loudness, Noise.Kind.Gunshot);

        nextFireTime = Time.time + (1f / FireRate);
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

    public void TryReload()
    {
        if (isReloading)
            return;

        if (currentAmmo >= GetMagazineSize())
            return;

        // Nothing to reload with: just the dry click.
        if (Reserve <= 0)
        {
            emptyAudio.PlayOneShot(emptyAudio.clip);
            return;
        }

        reloadRoutine = StartCoroutine(reloadStyle == ReloadStyle.PerShell ? ReloadShells() : Reload());
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

    // The racket of reloading, for enemies within earshot.
    private void ReloadNoise(bool quiet)
    {
        Noise.Emit(transform.position, quiet ? Noise.QuietReload : Noise.Reload, Noise.Kind.Reload);
    }

    private void CancelReload()
    {
        if (reloadRoutine != null)
            StopCoroutine(reloadRoutine);

        reloadRoutine = null;
        isReloading = false;
        ReloadProgress = 0f;
    }

    // Loads one round at a time until full or out of spare rounds, taking each from the
    // inventory as it goes in. Progress covers the whole reload, so the reload ring fills once
    // from empty to full rather than once per shell.
    private IEnumerator ReloadShells()
    {
        isReloading = true;
        ReloadProgress = 0f;

        int needed = Mathf.Min(GetMagazineSize() - currentAmmo, Reserve);

        for (int loaded = 0; loaded < needed; loaded++)
        {
            // Checked shell by shell, so starting or stopping sneaking mid-reload takes effect
            // from the next one.
            bool quiet = Sneaking;
            float shellTime = ShellReloadTime * (quiet ? sneakReloadTimeMultiplier : 1f);
            float elapsed = 0f;

            while (elapsed < shellTime)
            {
                elapsed += Time.deltaTime;
                ReloadProgress = Mathf.Clamp01((loaded + elapsed / shellTime) / needed);

                yield return null;
            }

            if (Inventory.Take(ammoType, 1) == 0)
                break;

            currentAmmo++;

            if (shellInsertClip != null)
                reloadAudio.PlayOneShot(shellInsertClip, quiet ? sneakReloadVolume : 1f);

            ReloadNoise(quiet);
        }

        ReloadProgress = 1f;
        isReloading = false;
        reloadRoutine = null;
    }

    private IEnumerator Reload()
    {
        isReloading = true;
        ReloadProgress = 0f;

        // Decided at the start: a sneaking reload is slower and quieter throughout.
        bool quiet = Sneaking;
        float duration = ReloadTime * (quiet ? sneakReloadTimeMultiplier : 1f);

        // The sound is stretched to fit, so a slow reload sounds slower too.
        reloadAudio.pitch = reloadAudio.clip.length / duration;
        reloadAudio.volume = reloadVolume * (quiet ? sneakReloadVolume : 1f);
        reloadAudio.Play();
        ReloadNoise(quiet);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ReloadProgress = Mathf.Clamp01(elapsed / duration);

            yield return null;
        }

        // Only what's carried goes in; taken now, so a cancelled reload costs nothing.
        currentAmmo += Inventory.Take(ammoType, GetMagazineSize() - currentAmmo);

        // The magazine seating.
        ReloadNoise(quiet);
        ReloadProgress = 1f;
        isReloading = false;
        reloadRoutine = null;
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

    public void SetCurrentAmmo(int ammo)
    {
        currentAmmo = Mathf.Clamp(ammo, 0, GetMagazineSize());
    }

    // ---------- Attachments ----------

    public WeaponAttachment AttachmentIn(AttachmentSlot slot)
    {
        return attachments.Find(a => a.slot == slot);
    }

    // Fits an attachment, returning the one it replaced in that slot (or null). Rounds that no
    // longer fit the magazine go back in the player's inventory.
    public WeaponAttachment Attach(WeaponAttachment attachment)
    {
        if (attachment == null)
            return null;

        WeaponAttachment replaced = AttachmentIn(attachment.slot);

        if (replaced != null)
            attachments.Remove(replaced);

        attachments.Add(attachment);
        AttachmentsUpdated();
        return replaced;
    }

    // Takes an attachment off. False if it wasn't fitted.
    public bool Detach(WeaponAttachment attachment)
    {
        if (!attachments.Remove(attachment))
            return false;

        AttachmentsUpdated();
        return true;
    }

    // Replaces everything fitted (a gun picked up with its attachments still on).
    public void SetAttachments(IEnumerable<WeaponAttachment> fitted)
    {
        attachments.Clear();

        if (fitted != null)
        {
            foreach (WeaponAttachment attachment in fitted)
            {
                if (attachment != null && AttachmentIn(attachment.slot) == null)
                    attachments.Add(attachment);
            }
        }

        AttachmentsUpdated();
    }

    private void AttachmentsUpdated()
    {
        // A reload in progress was timed for the old magazine.
        if (isReloading)
        {
            CancelReload();

            if (reloadAudio != null)
                reloadAudio.Stop();
        }

        int size = GetMagazineSize();

        if (currentAmmo > size)
        {
            if (Inventory != null)
                Inventory.Add(ammoType, currentAmmo - size);

            currentAmmo = size;
        }

        UpdateAttachmentVisuals();
        AttachmentsChanged?.Invoke();
    }

    // A suppressor can on the end of the barrel, and the laser line for a laser sight.
    private void UpdateAttachmentVisuals()
    {
        bool suppressed = HidesFlash;

        if (suppressed && suppressorModel == null && muzzle != null)
        {
            // Grabbed before the can is added, so it's one of the gun's own parts.
            Renderer gunPart = GetComponentInChildren<Renderer>();

            suppressorModel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            suppressorModel.name = "Suppressor";
            Destroy(suppressorModel.GetComponent<Collider>());
            suppressorModel.transform.SetParent(muzzle, false);

            // Along the barrel (the gun's forward): the cylinder's length is its Y axis. Sized in
            // world units, whatever the muzzle's own scale.
            suppressorModel.transform.rotation = Quaternion.LookRotation(transform.forward) * Quaternion.Euler(90f, 0f, 0f);
            suppressorModel.transform.position = muzzle.position + transform.forward * 0.1f;
            Vector3 lossy = muzzle.lossyScale;
            suppressorModel.transform.localScale = new Vector3(0.06f / lossy.x, 0.1f / lossy.y, 0.06f / lossy.z);

            Renderer renderer = suppressorModel.GetComponent<Renderer>();

            if (gunPart != null)
                renderer.sharedMaterial = gunPart.sharedMaterial;

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        else if (!suppressed && suppressorModel != null)
        {
            Destroy(suppressorModel);
            suppressorModel = null;
        }

        bool laser = attachments.Exists(a => a.laser);

        if (laser && laserSight == null && muzzle != null)
        {
            laserSight = LaserSight.Create(transform, muzzle);
        }
        else if (!laser && laserSight != null)
        {
            Destroy(laserSight.gameObject);
            laserSight = null;
        }
    }
}