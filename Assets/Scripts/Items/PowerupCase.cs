using System.Collections.Generic;
using UnityEngine;

// A powerup case: its own drop beside the treasure room's crate and each boss's, floating and lit
// green so it can't be taken for a crate. Interact opens its PowerupScreen to choose one of the run
// items inside; the case goes once one's taken, and stays for later if the player walks off.
public class PowerupCase : MonoBehaviour, FloorPickups.IPickup
{
    private static readonly Color GlowColour = UiColours.Phosphor;

    private readonly List<RunItem> offers = new List<RunItem>();
    private PickupLabel label;
    private PlayerWeapon playerWeapon;
    private bool taken;

    public IReadOnlyList<RunItem> Offers => offers;
    public Vector3 Position => transform.position;
    public float Reach => 1.9f;
    public bool CanTake => !taken && !PowerupScreen.IsOpen;

    public static PowerupCase Spawn(IEnumerable<RunItem> items, Vector3 position, Transform parent)
    {
        GameObject root = new GameObject("Powerup Case");
        root.transform.SetParent(parent, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);

        PowerupCase powerup = root.AddComponent<PowerupCase>();
        powerup.offers.AddRange(items);
        powerup.label = PickupLabel.Create(root.transform, 1.6f);
        powerup.label.Compactable = true;

        if (ItemSprite.Apply(root, SpriteVisuals.Current != null ? SpriteVisuals.Current.items : null, ItemSprites.PowerupCase, true) == null)
            powerup.BuildModel();

        GameObject lamp = new GameObject("Light");
        lamp.transform.SetParent(root.transform, false);
        lamp.transform.localPosition = Vector3.up * 1.2f;
        Light light = lamp.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = GlowColour;
        light.range = 3.5f;
        light.intensity = 1.6f;
        light.shadows = LightShadows.None;
        return powerup;
    }

    // The stand-in without the sprite art.
    private void BuildModel()
    {
        GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(model.GetComponent<Collider>());
        model.transform.SetParent(transform, false);
        model.transform.localPosition = Vector3.up * 0.4f;
        model.transform.localScale = new Vector3(0.55f, 0.4f, 0.2f);

        if (ConsumablePickup.DefaultMaterial != null)
        {
            Renderer renderer = model.GetComponent<Renderer>();
            renderer.sharedMaterial = ConsumablePickup.DefaultMaterial;
            ConsumableModel.Tint(renderer, GlowColour * 0.5f, GlowColour);
        }
    }

    private void OnEnable()
    {
        FloorPickups.Register(this);
    }

    private void OnDisable()
    {
        FloorPickups.Unregister(this);
    }

    private void Update()
    {
        if (taken)
            return;

        if (playerWeapon == null)
        {
            playerWeapon = PlayerWeapon.Current;

            if (playerWeapon == null)
                return;
        }

        Vector3 offset = playerWeapon.transform.position - transform.position;
        offset.y = 0f;
        bool showLabel = offset.magnitude <= Reach + 2f && !PowerupScreen.IsOpen;

        if (showLabel && !label.IsShowing)
            label.Show(LabelText, GlowColour);
        else if (!showLabel && label.IsShowing)
            label.Hide();

        if (FloorPickups.Pressed(this, playerWeapon))
            PowerupScreen.Open(this, playerWeapon);
    }

    private static string LabelText()
    {
        return $"POWERUP CASE\n<size=80%><color=#FFFFFF>{InputHint.Format("[{Interact}]")} OPEN</color></size>";
    }

    // The player's chosen: it's theirs, and the case is gone.
    public void Choose(RunItem item, PlayerWeapon player)
    {
        taken = true;
        label.Hide();
        PlayerItems.For(player.gameObject).Add(item);
        Destroy(gameObject);
    }
}
