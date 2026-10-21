using System;
using System.Collections.Generic;
using UnityEngine;

// The player's two weapon slots, main and sidearm. A picked-up gun goes into its own slot (see
// Gun.EquipSlot) and becomes the one in hand, dropping whatever was in that slot before. Switch
// with Weapon Main / Weapon Sidearm (D-pad up/down, 1/2); the mouse wheel is the quick bar's. Each slot keeps its
// own gun, so ammo carries over while it's holstered. A gun leaving the player's hands (swapped
// for one on the floor or in a supply crate) is stripped of its attachments first: each goes onto
// the new gun if it fits a free slot there, otherwise into the backpack, or onto the floor beside
// the player if that's full. A gun found with attachments on keeps them.
public class PlayerWeapon : MonoBehaviour
{
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private GameObject gunPickupPrefab;
    [SerializeField] private GameObject startingWeaponPrefab;

    // A gun in a slot, and the prefab it came from (for dropping it and for its name).
    public class Equipped
    {
        public Gun Gun;
        public GameObject Prefab;

        public string Name => Gun.DisplayName(Prefab);
    }

    public Equipped Main { get; } = new Equipped();
    public Equipped Sidearm { get; } = new Equipped();
    public Gun.Slot ActiveSlot { get; private set; } = Gun.Slot.Sidearm;

    public Equipped Active => SlotFor(ActiveSlot);
    public Equipped Holstered => SlotFor(ActiveSlot == Gun.Slot.Main ? Gun.Slot.Sidearm : Gun.Slot.Main);

    public event Action Changed;

    private PlayerShooting shooting;
    private GunPickup nearbyPickup;
    private int interactFrame = -1;

    // A gun pickup in reach takes the Interact press, so an attachment next to it waits.
    public bool HasNearbyPickup => nearbyPickup != null || interactFrame == Time.frameCount;

    // Spare attachments and ammo stacks, shown in the inventory.
    public Backpack Backpack { get; private set; }
    public QuickBar QuickBar { get; private set; }
    public PlayerMelee Melee { get; private set; }

    private void Awake()
    {
        shooting = GetComponent<PlayerShooting>();
        Backpack = Backpack.For(gameObject);
        QuickBar = QuickBar.For(gameObject);
        Melee = PlayerMelee.For(gameObject);
    }

    private void Start()
    {
        Gun gun = Equip(startingWeaponPrefab);
        gun.SetCurrentAmmo(gun.GetMagazineSize());
    }

    private void Update()
    {
        if (GameFlow.InputBlocked)
            return;

        if (nearbyPickup != null && GameInput.Interact.WasPressedThisFrame())
        {
            interactFrame = Time.frameCount;
            PickUp(nearbyPickup);
            return;
        }

        if (GameInput.WeaponMain.WasPressedThisFrame())
            Switch(Gun.Slot.Main);
        else if (GameInput.WeaponSidearm.WasPressedThisFrame())
            Switch(Gun.Slot.Sidearm);
    }

    public void SetNearbyPickup(GunPickup pickup)
    {
        nearbyPickup = pickup;
    }

    public void ClearNearbyPickup(GunPickup pickup)
    {
        if (nearbyPickup == pickup)
        {
            nearbyPickup = null;
        }
    }

    // Take a slot in hand. Does nothing if that slot is empty or already in hand.
    public void Switch(Gun.Slot slot)
    {
        if (slot == ActiveSlot || SlotFor(slot).Gun == null)
            return;

        if (Active.Gun != null)
            Active.Gun.gameObject.SetActive(false);

        ActiveSlot = slot;
        Active.Gun.gameObject.SetActive(true);
        shooting.SetEquippedGun(Active.Gun);
        Changed?.Invoke();
    }

    // Which gun an attachment would go on: the one in hand if it fits, otherwise the holstered
    // one, or null if it fits neither. A gun that already has this very attachment is passed
    // over for one that doesn't.
    public Equipped TargetFor(WeaponAttachment attachment)
    {
        if (attachment == null)
            return null;

        Equipped fallback = null;

        foreach (Equipped slot in new[] { Active, Holstered })
        {
            if (slot.Gun == null || !attachment.Fits(slot.Gun))
                continue;

            if (slot.Gun.AttachmentIn(attachment.slot) != attachment)
                return slot;

            if (fallback == null)
                fallback = slot;
        }

        return fallback;
    }

    private void PickUp(GunPickup pickup)
    {
        Gun gun = Equip(pickup.GetWeaponPrefab(), true, out List<WeaponAttachment> carried);

        // Before the ammo, so an extended magazine's extra rounds are kept.
        gun.SetAttachments(pickup.Attachments);

        if (pickup.HasStoredAmmo())
            gun.SetCurrentAmmo(pickup.GetStoredAmmo());

        Destroy(pickup.gameObject);
        nearbyPickup = null;
        Rehome(carried, gun);
    }

    // Hands the player a gun with a full magazine, e.g. the hideout's Armory upgrade at the start
    // of a run.
    public Gun Give(GameObject weaponPrefab)
    {
        if (weaponPrefab == null || weaponPrefab.GetComponent<Gun>() == null)
            return null;

        Gun gun = Equip(weaponPrefab, true, out List<WeaponAttachment> carried);
        gun.SetCurrentAmmo(gun.GetMagazineSize());
        Rehome(carried, gun);
        return gun;
    }

    // A gun put away somewhere other than the floor (a supply crate), with what it carried.
    public class Stowed
    {
        public GameObject Prefab;
        public int Ammo;
        public List<WeaponAttachment> Attachments;
    }

    // Takes a gun out of a supply crate: like picking one up, but whatever was in its slot comes
    // back out as `replaced` (for the crate to keep) instead of being dropped on the floor.
    public Gun TakeFromCrate(GameObject weaponPrefab, int ammo, IEnumerable<WeaponAttachment> fitted, out Stowed replaced)
    {
        replaced = null;

        if (weaponPrefab == null || weaponPrefab.GetComponent<Gun>() == null)
            return null;

        Equipped old = SlotFor(weaponPrefab.GetComponent<Gun>().EquipSlot);
        List<WeaponAttachment> carried = Strip(old);

        if (old.Gun != null)
        {
            replaced = new Stowed
            {
                Prefab = old.Prefab,
                Ammo = old.Gun.GetCurrentAmmo(),
                Attachments = new List<WeaponAttachment>()
            };
        }

        Gun gun = Equip(weaponPrefab, false, out _);

        if (fitted != null)
            gun.SetAttachments(fitted);

        gun.SetCurrentAmmo(ammo);
        Rehome(carried, gun);
        return gun;
    }

    private Gun Equip(GameObject weaponPrefab)
    {
        return Equip(weaponPrefab, true, out _);
    }

    // Puts a new gun from a prefab into its slot (dropping what was there, or just removing it
    // when it's been stowed elsewhere) and takes it in hand. The old gun goes without its
    // attachments: they come back as `carried`, for Rehome once the new gun is set up.
    private Gun Equip(GameObject weaponPrefab, bool dropOld, out List<WeaponAttachment> carried)
    {
        Gun.Slot slot = weaponPrefab.GetComponent<Gun>().EquipSlot;
        Equipped equipped = SlotFor(slot);
        carried = Strip(equipped);

        if (equipped.Gun != null && dropOld)
            Drop(equipped);
        else if (equipped.Gun != null)
            Remove(equipped);

        GameObject weapon = Instantiate(weaponPrefab, weaponHolder.position, weaponHolder.rotation, weaponHolder);
        equipped.Gun = weapon.GetComponent<Gun>();
        equipped.Prefab = weaponPrefab;
        CharacterVisuals.DressGun(equipped.Gun);

        // Hide the one in hand (if it's the other slot) and bring the new one out.
        if (slot != ActiveSlot && Active.Gun != null)
            Active.Gun.gameObject.SetActive(false);

        ActiveSlot = slot;
        shooting.SetEquippedGun(equipped.Gun);
        Changed?.Invoke();

        return equipped.Gun;
    }

    // Takes every attachment off the gun in a slot (rounds an extended magazine held that no longer
    // fit go back in the backpack) and returns them.
    private static List<WeaponAttachment> Strip(Equipped equipped)
    {
        List<WeaponAttachment> taken = new List<WeaponAttachment>();

        if (equipped.Gun == null || equipped.Gun.Attachments.Count == 0)
            return taken;

        taken.AddRange(equipped.Gun.Attachments);
        equipped.Gun.SetAttachments(null);
        return taken;
    }

    // Attachments off a gun that's gone: onto the new gun where they fit a free slot, otherwise
    // into the backpack, otherwise onto the floor beside the player.
    private void Rehome(List<WeaponAttachment> carried, Gun gun)
    {
        if (carried == null)
            return;

        foreach (WeaponAttachment attachment in carried)
        {
            if (gun != null && attachment.Fits(gun) && gun.AttachmentIn(attachment.slot) == null)
            {
                gun.Attach(attachment);
                continue;
            }

            if (Backpack.AddAttachment(attachment))
                continue;

            Vector3 position = FloorPickups.DropPoint(transform);
            AttachmentPickup.Spawn(attachment, position, FloorPickups.RoomAt(position), AttachmentPickup.DefaultMaterial);
        }

        if (carried.Count > 0)
            Changed?.Invoke();
    }

    private void Drop(Equipped equipped)
    {
        GameObject pickup = Instantiate(gunPickupPrefab, transform.position, Quaternion.identity);
        pickup.GetComponent<GunPickup>().SetWeaponPrefab(equipped.Prefab, equipped.Gun.GetCurrentAmmo(), equipped.Gun.Attachments);
        Remove(equipped);
    }

    private static void Remove(Equipped equipped)
    {
        Destroy(equipped.Gun.gameObject);
        equipped.Gun = null;
        equipped.Prefab = null;
    }

    private Equipped SlotFor(Gun.Slot slot)
    {
        return slot == Gun.Slot.Main ? Main : Sidearm;
    }
}
