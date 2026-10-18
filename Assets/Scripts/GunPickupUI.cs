using UnityEngine;

// The label over a gun pickup: the gun, its ammo, and the button to equip it. Shown while the
// player is in the pickup's range (GunPickup calls Show/Hide). Uses the HUD-styled PickupLabel;
// this object's own canvas from the prefab stays hidden.
public class GunPickupUI : MonoBehaviour
{
    private static readonly Color Phosphor = new Color(0.35f, 1f, 0.55f, 1f);

    private GunPickup gunPickup;
    private PickupLabel label;

    private void Awake()
    {
        gunPickup = GetComponentInParent<GunPickup>();
        label = PickupLabel.Create(gunPickup.transform, transform.localPosition.y);

        gameObject.SetActive(false);
    }

    public void Show()
    {
        if (gunPickup == null || gunPickup.GetWeaponPrefab() == null)
            return;

        Gun gun = gunPickup.GetWeaponPrefab().GetComponent<Gun>();

        if (gun == null)
            return;

        label.Show(Text, AmmoInfo.Colour(gun.AmmoType));
    }

    public void Hide()
    {
        if (label != null)
            label.Hide();
    }

    // Re-read every frame by the label, so the button follows the device in use and any rebind.
    private string Text()
    {
        GameObject weapon = gunPickup.GetWeaponPrefab();
        Gun gun = weapon.GetComponent<Gun>();

        string text = $"{Gun.DisplayName(weapon)}  <size=70%><alpha=#99>// {AmmoInfo.Name(gun.AmmoType)}</size>\n";

        // Attachments it still has on from when it was dropped.
        if (gunPickup.Attachments.Count > 0)
        {
            string[] names = new string[gunPickup.Attachments.Count];

            for (int i = 0; i < names.Length; i++)
                names[i] = gunPickup.Attachments[i].name.ToUpperInvariant();

            text += $"<size=70%><alpha=#99>+ {string.Join(", ", names)}</size>\n";
        }

        return text + $"<size=80%><alpha=#FF>MAG {gunPickup.GetStoredAmmo()}/{gunPickup.MagazineSize}    "
            + $"<color=#FFFFFF>[{GameInput.Label(GameInput.Interact)}] EQUIP</color></size>";
    }
}
