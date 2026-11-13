using System.Collections.Generic;
using UnityEngine;

// A powerup case: its own drop beside the treasure room's crate and each boss's, floating and lit
// green so it can't be taken for a crate. Interact opens its PowerupScreen to choose one of the run
// items inside; the case goes once one's taken, and stays for later if the player walks off.
public class PowerupCase : MonoBehaviour, FloorPickups.IPickup
{
    // Normal: free pick of one, green. Cursed: the cursed room's case, red. Blood: the blood bank's case, red,
    // each item bought with max health, as many as the player will pay for.
    public enum Mode { Normal, Cursed, Blood }

    private Color glowColour = UiColours.Phosphor;

    private readonly List<RunItem> offers = new List<RunItem>();
    private PickupLabel label;
    private PlayerWeapon playerWeapon;
    private bool taken;

    public Mode CaseMode { get; private set; }

    public IReadOnlyList<RunItem> Offers => offers;
    public Vector3 Position => transform.position;
    public float Reach => 1.9f;
    public bool CanTake => !taken && !PowerupScreen.IsOpen;

    public static PowerupCase Spawn(IEnumerable<RunItem> items, Vector3 position, Transform parent, Mode mode = Mode.Normal)
    {
        GameObject root = new GameObject("Powerup Case");
        root.transform.SetParent(parent, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);

        PowerupCase powerup = root.AddComponent<PowerupCase>();
        powerup.CaseMode = mode;
        powerup.glowColour = mode == Mode.Normal ? UiColours.Phosphor : RunItems.CursedColour;
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
        light.color = powerup.glowColour;
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
            ModelParts.Tint(renderer, glowColour * 0.5f, glowColour);
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
            label.Show(LabelText, glowColour);
        else if (!showLabel && label.IsShowing)
            label.Hide();

        if (FloorPickups.Pressed(this, playerWeapon))
            PowerupScreen.Open(this, playerWeapon);
    }

    private string LabelText()
    {
        string title = CaseMode == Mode.Blood ? "BLOOD BANK" : CaseMode == Mode.Cursed ? "CURSED CASE" : "POWERUP CASE";
        return $"{title}\n<size=80%><color=#FFFFFF>{InputHint.Format("[{Interact}]")} OPEN</color></size>";
    }

    // Max health a blood bank item costs: 1 for a normal one, 2 for a cursed one.
    public static int BloodCost(RunItem item)
    {
        return RunItems.IsCursed(item) ? 2 : 1;
    }

    // Max health never drops below 2 (PlayerHealth.RemoveMaxHealth), so a purchase must leave that.
    public static bool CanAfford(RunItem item, PlayerHealth health)
    {
        return health != null && health.MaxHealth - BloodCost(item) >= 2;
    }

    // The player's chosen: it's theirs. A normal or cursed case is gone; the blood bank's takes its
    // price in max health and stays until it's empty.
    public void Choose(RunItem item, PlayerWeapon player)
    {
        if (CaseMode == Mode.Blood)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();

            if (!CanAfford(item, health) || !offers.Remove(item))
                return;

            health.RemoveMaxHealth(BloodCost(item));
            PlayerItems.For(player.gameObject).Add(item);

            if (offers.Count > 0)
                return;
        }
        else
            PlayerItems.For(player.gameObject).Add(item);

        taken = true;
        label.Hide();
        Destroy(gameObject);
    }
}
