using UnityEngine;

// A consumable lying on the floor (medkits, adrenaline, grenades, molotovs, bottles), one or a
// few of the same kind. Like ammo, walking over it tops up the stacks of that kind already in
// the backpack, no button needed; anything that would need a new stack waits for Interact (see
// FloorPickups). Floats and turns slowly, with a label once the player is near (only the
// nearest one's, when several lie together).
public class ConsumablePickup : MonoBehaviour, FloorPickups.IPickup
{
    private const float LabelRange = 3f;

    private static Transform player;
    private static Backpack playerBackpack;
    private static PlayerWeapon playerWeapon;
    private static readonly System.Collections.Generic.List<ConsumablePickup> all = new System.Collections.Generic.List<ConsumablePickup>();

    // The pickups' material, for things dropped from the inventory and thrown ones.
    public static Material DefaultMaterial { get; set; }

    // Statics outlive Play sessions with domain reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        player = null;
        playerBackpack = null;
        playerWeapon = null;
        DefaultMaterial = null;
        all.Clear();
    }

    public ConsumableType Type { get; private set; }
    public int Amount { get; private set; }

    private Transform visual;
    private float bobOffset;
    private PickupLabel label;
    private float reach;

    // Dropped from the inventory: not topped up from until the player has stepped away.
    private bool waitForPlayerToLeave;

    public Vector3 Position => transform.position;
    public float Reach => reach;
    public bool CanTake => Amount > 0 && playerBackpack != null && playerBackpack.TopUpRoom(Type) < Amount && playerBackpack.FreeCells > 0;

    public static ConsumablePickup Spawn(ConsumableType type, int amount, Vector3 position, Transform parent, Material material)
    {
        if (material != null)
            DefaultMaterial = material;

        material = material != null ? material : DefaultMaterial;

        GameObject root = new GameObject($"Consumable ({ConsumableInfo.Name(type)})");
        root.transform.SetParent(parent, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);

        SphereCollider trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.7f;
        trigger.center = Vector3.up * 0.5f;

        ConsumablePickup pickup = root.AddComponent<ConsumablePickup>();
        pickup.Type = type;
        pickup.Amount = Mathf.Max(1, amount);
        pickup.visual = ConsumableModel.Build(type, root.transform, material);
        pickup.visual.localPosition = Vector3.up * 0.25f;
        pickup.visual.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        pickup.bobOffset = Random.value * 10f;
        pickup.reach = trigger.radius + 0.6f;
        AddGlow(root.transform, type);
        pickup.label = PickupLabel.Create(root.transform, 1.6f);
        return pickup;
    }

    // A stack dragged out of the inventory, landing in front of the player.
    public static ConsumablePickup Drop(ConsumableType type, int amount, Vector3 position, Transform parent)
    {
        ConsumablePickup pickup = Spawn(type, amount, position, parent, DefaultMaterial);
        pickup.waitForPlayerToLeave = true;
        return pickup;
    }

    // A soft disc of its colour on the floor beneath it, like ammo, so it shows up in the dark.
    private static void AddGlow(Transform root, ConsumableType type)
    {
        AmmoVisuals visuals = AmmoVisuals.Current;

        if (visuals == null || visuals.glowMaterial == null)
            return;

        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        glow.name = "Glow";
        Destroy(glow.GetComponent<Collider>());
        glow.transform.SetParent(root, false);
        glow.transform.localPosition = Vector3.up * 0.015f;
        glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        glow.transform.localScale = Vector3.one * visuals.glowSize;

        Renderer renderer = glow.GetComponent<Renderer>();
        renderer.sharedMaterial = visuals.glowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        Color colour = ConsumableInfo.Colour(type) * visuals.glowStrength;
        colour.a = 0.8f;
        block.SetColor("_Color", colour);
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

    // Only the nearest consumable in range shows its label, so a few lying together don't
    // stack their labels on top of each other.
    private bool IsNearest(Vector3 point, float distance)
    {
        foreach (ConsumablePickup other in all)
        {
            if (other == this || other == null)
                continue;

            Vector3 offset = point - other.transform.position;
            offset.y = 0f;

            // A tie goes to whichever appeared first.
            float d = offset.magnitude;

            if (d < distance || (Mathf.Approximately(d, distance) && all.IndexOf(other) < all.IndexOf(this)))
                return false;
        }

        return true;
    }

    private void Update()
    {
        UpdateLabel();

        if (FloorPickups.Pressed(this, playerWeapon))
            Take(true);

        if (visual == null)
            return;

        visual.Rotate(Vector3.up, 40f * Time.deltaTime, Space.World);
        Vector3 local = visual.localPosition;
        local.y = 0.25f + Mathf.Sin(Time.time * 2.5f + bobOffset) * 0.05f;
        visual.localPosition = local;
    }

    private void UpdateLabel()
    {
        if (label == null)
            return;

        if (player == null)
        {
            playerWeapon = FindAnyObjectByType<PlayerWeapon>();
            playerBackpack = playerWeapon != null ? playerWeapon.Backpack : null;
            player = playerWeapon != null ? playerWeapon.transform : null;
        }

        if (player == null)
            return;

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;

        if (waitForPlayerToLeave && offset.magnitude > reach)
            waitForPlayerToLeave = false;

        bool inRange = offset.magnitude <= LabelRange && IsNearest(player.position, offset.magnitude);

        if (inRange && !label.IsShowing)
            label.Show(LabelText, ConsumableInfo.Colour(Type));
        else if (!inRange && label.IsShowing)
            label.Hide();
    }

    private string LabelText()
    {
        string text = ConsumableInfo.Name(Type) + (Amount > 1 ? "  x" + Amount : "");

        if (playerBackpack == null)
            return text;

        if (playerBackpack.RoomFor(Type) <= 0)
            return text + "\n<size=70%><alpha=#88>BACKPACK FULL</size>";

        if (playerBackpack.TopUpRoom(Type) < Amount)
        {
            bool focused = FloorPickups.Focus(player.position) == (FloorPickups.IPickup)this;
            return focused ? text + $"\n<size=80%><color=#FFFFFF>[{GameInput.Label(GameInput.Interact)}] TAKE</color></size>" : text;
        }

        return text;
    }

    // Into the backpack: only onto stacks already there, or (with Interact) new ones too.
    private void Take(bool newStacks)
    {
        int added = playerBackpack.AddConsumable(Type, Amount, newStacks);

        if (added <= 0)
            return;

        Amount -= added;
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("consumable-pickup", 2, i => ProceduralAudio.BackpackRustle(700 + i))), 0.45f, 1.2f);

        if (Amount <= 0)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other);
    }

    // Stay too, so standing on one collects it once a stack has room again.
    private void OnTriggerStay(Collider other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider other)
    {
        if (waitForPlayerToLeave || Amount <= 0 || playerBackpack == null || !other.CompareTag("Player"))
            return;

        if (playerBackpack.TopUpRoom(Type) > 0)
            Take(false);
    }
}
