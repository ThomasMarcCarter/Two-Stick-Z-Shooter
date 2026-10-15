using UnityEngine;

// Ammo lying about: a small box that floats and spins (an enemy drop) or a bigger crate sitting
// still on the floor (found in a room), shown with the ammo type's model (see AmmoVisuals) over
// a soft glow in its colour so it can be spotted in the dark. Walking over it tops up the
// player's ammo stacks of that type, no button needed; anything that would need a new stack in
// the backpack waits for Interact (see FloorPickups), so ammo doesn't fill the backpack without
// the player choosing to. Shows a label (type, amount, and the button if needed) once the
// player is near, from further out than the pickup reaches.
public class AmmoPickup : MonoBehaviour, FloorPickups.IPickup
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private const float DropLabelRange = 2.5f;
    private const float CrateLabelRange = 3.5f;

    private static Transform player;
    private static AmmoInventory playerAmmo;
    private static PlayerWeapon playerWeapon;

    // The ammo pickups' material, for stacks dropped from the inventory.
    public static Material DefaultMaterial { get; private set; }

    // Statics outlive Play sessions with domain reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        player = null;
        playerAmmo = null;
        playerWeapon = null;
        DefaultMaterial = null;
    }

    public AmmoType Type { get; private set; }
    public int Amount { get; private set; }
    public bool IsCrate => crate;

    private Transform visual;
    private float spinSpeed;
    private float bobOffset;
    private float baseHeight = 0.35f;
    private bool crate;
    private PickupLabel label;
    private float reach;

    // Dropped from the inventory: not topped up from until the player has stepped away, or it
    // would go straight back in.
    private bool waitForPlayerToLeave;

    public Vector3 Position => transform.position;
    public float Reach => reach;
    // Interact is only needed for what won't go in the stacks already carried.
    public bool CanTake => Amount > 0 && playerAmmo != null && playerAmmo.TopUpRoom(Type) < Amount && !playerAmmo.IsFull(Type);

    // Builds a pickup out of primitives, so no prefab is needed.
    public static AmmoPickup Spawn(AmmoType type, int amount, Vector3 position, Transform parent, Material material, bool crate)
    {
        if (material != null)
            DefaultMaterial = material;

        GameObject root = new GameObject(crate ? $"Ammo Crate ({AmmoInfo.Name(type)})" : $"Ammo ({AmmoInfo.Name(type)})");
        root.transform.SetParent(parent, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);

        SphereCollider trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = crate ? 1f : 0.7f;
        trigger.center = Vector3.up * 0.5f;

        AmmoVisuals visuals = AmmoVisuals.Current;
        GameObject modelPrefab = visuals != null ? visuals.ModelFor(type) : null;
        Transform shown;
        float height;

        if (modelPrefab != null)
        {
            // The ammo's own box. Crates sit still on the floor at a random angle; drops float.
            GameObject model = Object.Instantiate(modelPrefab, root.transform);
            model.name = "Model";
            model.transform.localScale = Vector3.one * (crate ? visuals.crateSize : visuals.dropSize);
            model.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            height = crate ? 0f : visuals.dropHeight;
            model.transform.localPosition = Vector3.up * height;
            shown = model.transform;

            foreach (Collider part in model.GetComponentsInChildren<Collider>())
                Object.Destroy(part);

            AddGlow(root.transform, type, visuals, crate);
        }
        else
        {
            // No model for it: a plain block glowing in the ammo's colour.
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Box";
            Object.Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(root.transform, false);
            box.transform.localScale = crate ? new Vector3(0.9f, 0.5f, 0.6f) : new Vector3(0.4f, 0.22f, 0.28f);
            height = crate ? 0.25f : 0.35f;
            box.transform.localPosition = Vector3.up * height;
            shown = box.transform;

            Renderer renderer = box.GetComponent<Renderer>();

            if (material != null)
            {
                renderer.sharedMaterial = material;

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                Color colour = AmmoInfo.Colour(type);
                block.SetColor(BaseColorId, colour * 0.5f);
                // Bright enough to spot outside the flashlight beam.
                block.SetColor(EmissionColorId, colour * (crate ? 3f : 4f));
                renderer.SetPropertyBlock(block);
            }
        }

        AmmoPickup pickup = root.AddComponent<AmmoPickup>();
        pickup.Type = type;
        pickup.Amount = amount;
        pickup.visual = shown;
        pickup.baseHeight = height;
        pickup.spinSpeed = crate ? 0f : 90f;   // crates sit still; small drops spin to catch the eye
        pickup.bobOffset = Random.value * 10f;
        pickup.crate = crate;
        pickup.reach = trigger.radius + 0.6f;
        pickup.label = PickupLabel.Create(root.transform, 1.6f);
        return pickup;
    }

    // A stack dragged out of the inventory, landing in front of the player.
    public static AmmoPickup Drop(AmmoType type, int amount, Vector3 position, Transform parent)
    {
        AmmoPickup pickup = Spawn(type, amount, position, parent, DefaultMaterial, false);
        pickup.waitForPlayerToLeave = true;
        return pickup;
    }

    private void OnEnable()
    {
        FloorPickups.Register(this);
    }

    private void OnDisable()
    {
        FloorPickups.Unregister(this);
    }

    // A soft disc of the ammo's colour on the floor beneath it, so it shows up in the dark.
    private static void AddGlow(Transform root, AmmoType type, AmmoVisuals visuals, bool crate)
    {
        if (visuals.glowMaterial == null)
            return;

        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        glow.name = "Glow";
        Object.Destroy(glow.GetComponent<Collider>());
        glow.transform.SetParent(root, false);
        glow.transform.localPosition = Vector3.up * 0.015f;
        glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        glow.transform.localScale = Vector3.one * visuals.glowSize * (crate ? 1.4f : 1f);

        Renderer renderer = glow.GetComponent<Renderer>();
        renderer.sharedMaterial = visuals.glowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        Color colour = AmmoInfo.Colour(type) * visuals.glowStrength;
        colour.a = 0.8f;
        block.SetColor("_Color", colour);
        renderer.SetPropertyBlock(block);
    }

    private void Update()
    {
        UpdateLabel();

        if (FloorPickups.Pressed(this, playerWeapon))
            TakeAll();

        if (visual == null || spinSpeed <= 0f)
            return;

        visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        Vector3 local = visual.localPosition;
        local.y = baseHeight + Mathf.Sin(Time.time * 3f + bobOffset) * 0.06f;
        visual.localPosition = local;
    }

    // Shown within label range, which reaches further than the pickup's own trigger.
    private void UpdateLabel()
    {
        if (label == null)
            return;

        if (player == null)
        {
            playerAmmo = FindAnyObjectByType<AmmoInventory>();
            playerWeapon = playerAmmo != null ? playerAmmo.GetComponent<PlayerWeapon>() : null;
            player = playerAmmo != null ? playerAmmo.transform : null;
        }

        if (player == null)
            return;

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;

        if (waitForPlayerToLeave && offset.magnitude > reach)
            waitForPlayerToLeave = false;

        bool inRange = offset.magnitude <= (crate ? CrateLabelRange : DropLabelRange);

        if (inRange && !label.IsShowing)
            label.Show(LabelText, AmmoInfo.Colour(Type));
        else if (!inRange && label.IsShowing)
            label.Hide();
    }

    private string LabelText()
    {
        string name = AmmoInfo.Name(Type) + "  x" + Amount;
        string text = crate ? "<size=70%><alpha=#99>AMMO CRATE</size>\n" + name : name;

        if (playerAmmo == null)
            return text;

        // What's left once the stacks already carried are topped up needs a new stack.
        if (playerAmmo.IsFull(Type))
            return text + "\n<size=70%><alpha=#88>BACKPACK FULL</size>";

        if (playerAmmo.TopUpRoom(Type) < Amount)
        {
            bool focused = FloorPickups.Focus(player.position) == (FloorPickups.IPickup)this;
            return focused ? text + $"\n<size=80%><color=#FFFFFF>[{GameInput.Label(GameInput.Interact)}] TAKE</color></size>" : text;
        }

        return text;
    }

    // Interact: everything that fits, starting new stacks as needed.
    private void TakeAll()
    {
        Amount -= playerAmmo.Add(Type, Amount, true);

        if (Amount <= 0)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other);
    }

    // Stay too, so standing on a pickup collects it once there's room after using some ammo.
    private void OnTriggerStay(Collider other)
    {
        TryCollect(other);
    }

    // Walking over it only tops up the stacks already carried.
    private void TryCollect(Collider other)
    {
        if (waitForPlayerToLeave || Amount <= 0 || !other.CompareTag("Player") || !other.TryGetComponent(out AmmoInventory inventory))
            return;

        if (inventory.TopUpRoom(Type) <= 0)
            return;

        Amount -= inventory.Add(Type, Amount, false);

        if (Amount <= 0)
            Destroy(gameObject);
    }
}
