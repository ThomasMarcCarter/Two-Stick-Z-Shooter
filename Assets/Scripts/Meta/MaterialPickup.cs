using UnityEngine;

// A crafting material lying on the floor (parts, wiring, chemicals, gunpowder, fuel), dropped by
// zombies, left in cleared rooms, the treasure room and by bosses. Walking over it puts it in the
// backpack, no button needed (as much as fits; the rest stays, labelled BACKPACK FULL). It isn't
// safe until sent home from an uplink terminal (see UplinkTerminal).
// Its model (MaterialVisuals, or a stand-in built from primitives in the material's colour)
// floats and turns over a soft glow, with a label once the player is near.
public class MaterialPickup : MonoBehaviour
{
    private const float LabelRange = 2.8f;
    private const float Reach = 1.1f;

    private static Transform player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        player = null;
    }

    public MaterialType Type { get; private set; }
    public int Amount { get; private set; }

    private Transform visual;
    private float bobOffset;
    private PickupLabel label;
    private bool taken;
    private bool full;          // the last try found no room in the backpack

    public static MaterialPickup Spawn(MaterialType type, int amount, Vector3 position, Transform parent)
    {
        Material material = ConsumablePickup.DefaultMaterial;

        GameObject root = new GameObject($"Material ({Materials.Name(type)})");
        root.transform.SetParent(parent, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);

        MaterialPickup pickup = root.AddComponent<MaterialPickup>();
        pickup.Type = type;
        pickup.Amount = Mathf.Max(1, amount);
        pickup.visual = BuildModel(type, root.transform, material);
        pickup.visual.localPosition = Vector3.up * 0.3f;
        pickup.visual.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        pickup.bobOffset = Random.value * 10f;
        AddGlow(root.transform, Materials.Colour(type));
        pickup.label = PickupLabel.Create(root.transform, 1.5f);
        return pickup;
    }

    // The material's model, centred on the float point; a little stand-in built from primitives,
    // about a hand's size, if there's no model for it.
    public static Transform BuildModel(MaterialType type, Transform parent, Material material)
    {
        Transform model = new GameObject("Model").transform;
        model.SetParent(parent, false);
        GameObject prefab = MaterialVisuals.Current != null ? MaterialVisuals.Current.ModelFor(type) : null;

        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab, model, false);
            instance.name = prefab.name;
            instance.transform.localScale = Vector3.one * 1.3f;   // a touch bigger, to read over its glow

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
                Destroy(collider);

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The model's pivot is at its base; centre it on the point it floats and turns about.
            Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(renderer.bounds);

            instance.transform.localPosition = -model.InverseTransformPoint(bounds.center);
            return model;
        }

        Color colour = Materials.Colour(type);
        Color dark = colour * 0.35f;
        dark.a = 1f;
        Color glow = colour * 0.6f;

        switch (type)
        {
            case MaterialType.Parts:
                // A gear (a flat disc with teeth) with a bolt through it.
                Part(model, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.32f, 0.04f, 0.32f), Quaternion.Euler(90f, 0f, 0f), material, colour, glow * 0.4f);
                for (int i = 0; i < 6; i++)
                {
                    Quaternion around = Quaternion.Euler(0f, 0f, i * 60f);
                    Part(model, PrimitiveType.Cube, around * Vector3.up * 0.17f, new Vector3(0.07f, 0.07f, 0.08f), around, material, colour, glow * 0.4f);
                }
                Part(model, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.08f, 0.09f, 0.08f), Quaternion.Euler(90f, 0f, 0f), material, dark, Color.black);
                break;

            case MaterialType.Wiring:
                // A spool of cable.
                Part(model, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.3f, 0.08f, 0.3f), Quaternion.Euler(90f, 0f, 0f), material, colour, glow * 0.5f);
                Part(model, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.09f), new Vector3(0.36f, 0.015f, 0.36f), Quaternion.Euler(90f, 0f, 0f), material, dark, Color.black);
                Part(model, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.09f), new Vector3(0.36f, 0.015f, 0.36f), Quaternion.Euler(90f, 0f, 0f), material, dark, Color.black);
                Part(model, PrimitiveType.Cube, new Vector3(0.12f, -0.14f, 0f), new Vector3(0.18f, 0.025f, 0.025f), Quaternion.Euler(0f, 0f, -20f), material, colour, glow * 0.5f);
                break;

            case MaterialType.Chemicals:
                // A stoppered flask.
                Part(model, PrimitiveType.Cylinder, new Vector3(0f, -0.04f, 0f), new Vector3(0.18f, 0.12f, 0.18f), Quaternion.identity, material, colour, glow);
                Part(model, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.07f, 0.06f, 0.07f), Quaternion.identity, material, colour, glow);
                Part(model, PrimitiveType.Cylinder, new Vector3(0f, 0.19f, 0f), new Vector3(0.08f, 0.025f, 0.08f), Quaternion.identity, material, dark, Color.black);
                break;

            case MaterialType.Gunpowder:
                // A small powder keg with bands.
                Part(model, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.22f, 0.13f, 0.22f), Quaternion.identity, material, new Color(0.45f, 0.3f, 0.15f), glow * 0.2f);
                Part(model, PrimitiveType.Cylinder, new Vector3(0f, 0.07f, 0f), new Vector3(0.235f, 0.015f, 0.235f), Quaternion.identity, material, colour, glow * 0.5f);
                Part(model, PrimitiveType.Cylinder, new Vector3(0f, -0.07f, 0f), new Vector3(0.235f, 0.015f, 0.235f), Quaternion.identity, material, colour, glow * 0.5f);
                break;

            default:
                // A jerry can.
                Part(model, PrimitiveType.Cube, Vector3.zero, new Vector3(0.24f, 0.28f, 0.1f), Quaternion.identity, material, colour, glow * 0.4f);
                Part(model, PrimitiveType.Cube, new Vector3(-0.03f, 0.16f, 0f), new Vector3(0.14f, 0.03f, 0.04f), Quaternion.identity, material, dark, Color.black);
                Part(model, PrimitiveType.Cylinder, new Vector3(0.08f, 0.17f, 0f), new Vector3(0.04f, 0.03f, 0.04f), Quaternion.identity, material, dark, Color.black);
                break;
        }

        return model;
    }

    private static void Part(Transform parent, PrimitiveType shape, Vector3 position, Vector3 size, Quaternion rotation,
        Material material, Color colour, Color emission)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation;
        part.transform.localScale = size;

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (material != null)
        {
            renderer.sharedMaterial = material;
            ConsumableModel.Tint(renderer, colour, emission);
        }
    }

    // A soft disc of its colour beneath it, like ammo, so it shows up in the dark.
    private static void AddGlow(Transform root, Color colour)
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
        Color tint = colour * visuals.glowStrength;
        tint.a = 0.8f;
        block.SetColor("_Color", tint);
        renderer.SetPropertyBlock(block);
    }

    private void Update()
    {
        if (taken)
            return;

        if (visual != null)
        {
            visual.Rotate(Vector3.up, 50f * Time.deltaTime, Space.World);
            Vector3 local = visual.localPosition;
            local.y = 0.3f + Mathf.Sin(Time.time * 2.5f + bobOffset) * 0.05f;
            visual.localPosition = local;
        }

        if (player == null)
        {
            PlayerWeapon weapon = FindAnyObjectByType<PlayerWeapon>();
            player = weapon != null ? weapon.transform : null;

            if (player == null)
                return;
        }

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;

        if (distance <= Reach && !GameFlow.InputBlocked && Take())
            return;

        if (distance > Reach)
            full = false;

        bool inRange = distance <= LabelRange;

        if (inRange && !label.IsShowing)
            label.Show(LabelText, Materials.Colour(Type));
        else if (!inRange && label.IsShowing)
            label.Hide();
    }

    private string LabelText()
    {
        return Materials.Name(Type) + (Amount > 1 ? "  x" + Amount : "")
            + (full ? $"\n<size=70%><color={UiColours.DangerHex}>BACKPACK FULL // MAKE ROOM</color></size>"
                : "\n<size=70%><alpha=#88>MATERIAL // SEND IT HOME FROM AN UPLINK</size>");
    }

    // Into the backpack, as much as fits. True once it's all gone.
    private bool Take()
    {
        int added = Backpack.For(player.gameObject).AddMaterial(Type, Amount);
        full = added < Amount;

        if (added == 0)
            return false;

        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("material-pickup", 3, ProceduralAudio.AmmoPickup)), 0.45f, 1.25f);
        Amount -= added;

        if (Amount > 0)
            return false;

        taken = true;
        Destroy(gameObject);
        return true;
    }
}
