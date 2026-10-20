using UnityEngine;

// A melee upgrade on the floor: a small blade over a glow in the upgrade's colour, floating and
// turning. Taken with Interact (see FloorPickups) and applied straight away for the rest of the
// run. One already at its max level can't be taken. Only the nearest one shows its label.
public class MeleeUpgradePickup : MonoBehaviour, FloorPickups.IPickup
{
    private const float LabelRange = 3f;
    private const float UseRange = 1.4f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private static Transform player;
    private static PlayerMelee playerMelee;
    private static PlayerWeapon playerWeapon;
    private static readonly System.Collections.Generic.List<MeleeUpgradePickup> all = new System.Collections.Generic.List<MeleeUpgradePickup>();

    // Statics outlive Play sessions with domain reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        player = null;
        playerMelee = null;
        playerWeapon = null;
        all.Clear();
    }

    public MeleeUpgrade Upgrade { get; private set; }

    private Transform visual;
    private float bobOffset;
    private PickupLabel label;
    private bool used;

    public Vector3 Position => transform.position;
    public float Reach => UseRange;
    public bool CanTake => !used && playerMelee != null && !playerMelee.IsMaxed(Upgrade);

    public static MeleeUpgradePickup Spawn(MeleeUpgrade upgrade, Vector3 position, Transform parent, Material material)
    {
        material = material != null ? material : ConsumablePickup.DefaultMaterial;

        GameObject root = new GameObject($"Melee Upgrade ({MeleeUpgrades.Name(upgrade)})");
        root.transform.SetParent(parent, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);

        MeleeUpgradePickup pickup = root.AddComponent<MeleeUpgradePickup>();
        pickup.Upgrade = upgrade;
        pickup.bobOffset = Random.value * 10f;

        GameObject model = new GameObject("Model");
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = Vector3.up * 0.45f;
        model.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
        pickup.visual = model.transform;

        Color colour = MeleeUpgrades.Colour(upgrade);
        Part(model.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.07f, 0.42f, 0.02f), material, new Color(0.75f, 0.78f, 0.8f), colour * 1.5f);
        Part(model.transform, new Vector3(0f, -0.1f, 0f), new Vector3(0.16f, 0.03f, 0.05f), material, new Color(0.3f, 0.3f, 0.3f), Color.black);
        Part(model.transform, new Vector3(0f, -0.2f, 0f), new Vector3(0.045f, 0.17f, 0.045f), material, colour * 0.5f, colour * 3f);

        AddGlow(root.transform, colour);
        pickup.label = PickupLabel.Create(root.transform, 1.6f);
        pickup.label.Compactable = true;
        ItemSprite.Apply(root, SpriteVisuals.Current != null ? SpriteVisuals.Current.items : null, ItemSprites.Melee(upgrade), true);
        return pickup;
    }

    private static void Part(Transform parent, Vector3 localPosition, Vector3 size, Material material, Color baseColour, Color emission)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = size;

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (material == null)
            return;

        renderer.sharedMaterial = material;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, baseColour);
        block.SetColor(EmissionColorId, emission);
        renderer.SetPropertyBlock(block);
    }

    private static void AddGlow(Transform root, Color colour)
    {
        AmmoVisuals visuals = AmmoVisuals.Current;

        if (visuals == null || visuals.glowMaterial == null)
            return;

        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        glow.name = "Glow";
        Destroy(glow.GetComponent<Collider>());
        glow.transform.SetParent(root, false);
        glow.transform.localPosition = Vector3.up * FloorPickups.GlowHeight;
        glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        glow.transform.localScale = Vector3.one * visuals.glowSize;

        Renderer renderer = glow.GetComponent<Renderer>();
        renderer.sharedMaterial = visuals.glowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        Color tint = colour * visuals.glowStrength;
        tint.a = 0.8f;
        block.SetColor("_Color", tint);
        renderer.SetPropertyBlock(block);
    }

    private void OnEnable()
    {
        FloorPickups.Register(this);
        all.Add(this);
    }

    private void OnDisable()
    {
        FloorPickups.Unregister(this);
        all.Remove(this);
    }

    private void Update()
    {
        UpdateLabel();

        if (FloorPickups.Pressed(this, playerWeapon))
            Take();

        if (visual == null)
            return;

        visual.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);
        Vector3 local = visual.localPosition;
        local.y = 0.45f + Mathf.Sin(Time.time * 2.5f + bobOffset) * 0.05f;
        visual.localPosition = local;
    }

    private void UpdateLabel()
    {
        if (label == null)
            return;

        if (player == null)
        {
            playerWeapon = FindAnyObjectByType<PlayerWeapon>();
            playerMelee = playerWeapon != null ? playerWeapon.Melee : null;
            player = playerWeapon != null ? playerWeapon.transform : null;
        }

        if (player == null)
            return;

        float distance = Flat(player.position - transform.position).magnitude;
        bool inRange = distance <= LabelRange && IsNearest(player.position, distance);

        if (inRange && !label.IsShowing)
            label.Show(LabelText, MeleeUpgrades.Colour(Upgrade));
        else if (!inRange && label.IsShowing)
            label.Hide();
    }

    private bool IsNearest(Vector3 point, float distance)
    {
        foreach (MeleeUpgradePickup other in all)
        {
            if (other == this || other == null)
                continue;

            float d = Flat(point - other.transform.position).magnitude;

            if (d < distance || (Mathf.Approximately(d, distance) && all.IndexOf(other) < all.IndexOf(this)))
                return false;
        }

        return true;
    }

    private string LabelText()
    {
        int level = playerMelee != null ? playerMelee.Level(Upgrade) : 0;
        int max = MeleeUpgrades.MaxLevel(Upgrade);
        string name = $"<size=70%><alpha=#99>MELEE UPGRADE</size>\n{MeleeUpgrades.Name(Upgrade)}  <size=80%><alpha=#AA>{level} / {max}</size>";
        string effect = $"\n<size=70%><alpha=#CC>{MeleeUpgrades.Describe(Upgrade)}</size>";

        if (playerMelee != null && playerMelee.IsMaxed(Upgrade))
            return name + effect + "\n<size=70%><alpha=#88>ALREADY MAXED</size>";

        bool focused = player != null && FloorPickups.Focus(player.position) == (FloorPickups.IPickup)this;
        return name + effect + (focused ? $"\n<size=80%><color=#FFFFFF>[{GameInput.Label(GameInput.Interact)}] TAKE</color></size>" : "");
    }

    private void Take()
    {
        if (used || playerMelee == null || !playerMelee.AddUpgrade(Upgrade))
            return;

        used = true;
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-fit", 3, ProceduralAudio.AttachmentFit)), 0.55f, 0.85f);
        Destroy(gameObject);
    }

    private static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }
}
