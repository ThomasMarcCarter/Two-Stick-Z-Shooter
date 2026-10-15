using System.Collections.Generic;
using UnityEngine;

// A gun lying on the floor or mounted on a wall, ready to swap for. Shows the same model the gun
// has in the player's hands (the "Model" child of the gun prefab), lying on its side so the
// top-down camera sees its full profile, in place of the placeholder box. A dropped gun keeps
// its attachments (and the rounds an extended magazine held) for when it's picked up again.
public class GunPickup : MonoBehaviour
{
    private const string ModelName = "Model";
    [Tooltip("How much bigger than in the hand the model is shown, so it reads from the camera.")]
    [SerializeField] private float modelScale = 1.6f;
    [Tooltip("Wall-mounted pickups: height of the model's centre above the pickup's origin. Floor pickups rest on the floor.")]
    [SerializeField] private float modelHeight = 0.25f;
    [Tooltip("Pickups whose origin is below this height are on the floor, and their model rests on it.")]
    [SerializeField] private float floorHeight = 0.5f;

    [SerializeField] private GameObject weaponPrefab;
    [SerializeField] private GunPickupUI pickupUI;
    [Tooltip("Start with a full magazine instead of a random amount.")]
    [SerializeField] private bool startWithFullAmmo;

    private int storedAmmo;
    private bool hasStoredAmmo;
    private readonly List<WeaponAttachment> attachments = new List<WeaponAttachment>();

    public IReadOnlyList<WeaponAttachment> Attachments => attachments;
    private Transform model;

    private void Awake()
    {
        BuildModel();

        if (weaponPrefab == null)
            return;

        Gun gun = weaponPrefab.GetComponent<Gun>();

        if (gun == null)
            return;

        storedAmmo = startWithFullAmmo ? gun.GetMagazineSize() : Random.Range(1, gun.GetMagazineSize() + 1);
        hasStoredAmmo = true;
    }

    public void SetWeaponPrefab(GameObject prefab, int ammo, IEnumerable<WeaponAttachment> fitted = null)
    {
        weaponPrefab = prefab;
        storedAmmo = ammo;
        hasStoredAmmo = true;
        attachments.Clear();

        if (fitted != null)
            attachments.AddRange(fitted);

        BuildModel();
    }

    // The magazine with the attachments it carries.
    public int MagazineSize
    {
        get
        {
            Gun gun = weaponPrefab != null ? weaponPrefab.GetComponent<Gun>() : null;

            if (gun == null)
                return 0;

            float multiplier = 1f;

            foreach (WeaponAttachment attachment in attachments)
                multiplier *= attachment.magazineMultiplier;

            return Mathf.Max(1, Mathf.RoundToInt(gun.GetMagazineSize() * multiplier));
        }
    }

    // A copy of the gun's own model, scaled up, lying on its side (barrel to the right, magazine
    // and grip towards the bottom of the screen, so the top-down camera sees its profile) and
    // turned to lie along a wall when mounted. On the floor it rests on the floor; on a wall it's
    // centred on the mount. Falls back to the placeholder box for a gun without one.
    private void BuildModel()
    {
        if (model != null)
            Destroy(model.gameObject);

        model = null;
        Transform source = weaponPrefab != null ? weaponPrefab.transform.Find(ModelName) : null;
        SetPlaceholderVisible(source == null);

        if (source == null)
            return;

        model = Instantiate(source, transform);
        model.name = ModelName;
        // Roll onto its side about the barrel, then turn the barrel to point along the pickup's right.
        model.localRotation = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(0f, 0f, 90f);
        model.localScale = Vector3.one * modelScale;
        model.localPosition = Vector3.zero;

        Bounds bounds = new Bounds(model.position, Vector3.zero);
        bool first = true;

        foreach (Renderer part in model.GetComponentsInChildren<Renderer>())
        {
            if (first)
                bounds = part.bounds;
            else
                bounds.Encapsulate(part.bounds);

            first = false;
        }

        bool onFloor = transform.position.y < floorHeight;
        Vector3 target = onFloor
            ? new Vector3(transform.position.x, transform.position.y + bounds.extents.y + 0.01f, transform.position.z)
            : transform.position + transform.up * modelHeight;

        model.position += target - bounds.center;
    }

    // The box the prefab shows when there's no model. Its collider stays either way.
    private void SetPlaceholderVisible(bool visible)
    {
        foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
        {
            if (model != null && part.transform.IsChildOf(model))
                continue;

            if (part is MeshRenderer && part.GetComponent<MeshFilter>() != null)
                part.enabled = visible;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerWeapon playerWeapon = other.GetComponent<PlayerWeapon>();

        if (playerWeapon == null)
            return;

        playerWeapon.SetNearbyPickup(this);

        if (pickupUI != null)
            pickupUI.Show();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerWeapon playerWeapon = other.GetComponent<PlayerWeapon>();

        if (playerWeapon == null)
            return;

        playerWeapon.ClearNearbyPickup(this);

        if (pickupUI != null)
            pickupUI.Hide();
    }

    public GameObject GetWeaponPrefab()
    {
        return weaponPrefab;
    }

    public int GetStoredAmmo()
    {
        return storedAmmo;
    }

    public bool HasStoredAmmo()
    {
        return hasStoredAmmo;
    }
}