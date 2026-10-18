using System.Collections.Generic;
using UnityEngine;

// A training dummy for the hideout's shooting range: a sandbag torso with a painted target on a
// post. Bullets and melee hit it like a zombie (it never dies); each hit knocks it into a wobble,
// flashes it, pops up the damage as a floating number, and its label keeps score: the last hit,
// damage per second over the last few seconds, and the total. Guns don't use ammo in the hideout
// (Gun.FreeAmmo), so trying them out costs nothing. Built in code by the FloorGenerator.
public class TrainingDummy : MonoBehaviour
{
    private const float DpsWindow = 3f;

    private readonly List<(float time, int damage)> hits = new List<(float, int)>();
    private Transform bag;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private Color[] colours;
    private Vector2 tilt;          // degrees around X and Z
    private Vector2 tiltSpeed;
    private float flash;
    private int lastHit;
    private int total;

    public static TrainingDummy Create(Vector3 position, Vector3 facing, Transform parent)
    {
        GameObject root = new GameObject("Training Dummy");
        root.transform.SetParent(parent, true);
        root.transform.SetPositionAndRotation(new Vector3(position.x, 0f, position.z), Quaternion.LookRotation(facing, Vector3.up));

        TrainingDummy dummy = root.AddComponent<TrainingDummy>();
        Material material = ConsumablePickup.DefaultMaterial;
        Color canvas = new Color(0.55f, 0.48f, 0.34f);
        Color wood = new Color(0.32f, 0.22f, 0.13f);
        Color red = new Color(0.8f, 0.12f, 0.1f);

        Part(root.transform, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.8f, 0.04f, 0.8f), material, new Color(0.2f, 0.2f, 0.21f));

        // Everything above the base pivots at the base, so it rocks when hit.
        dummy.bag = new GameObject("Bag").transform;
        dummy.bag.SetParent(root.transform, false);
        dummy.bag.localPosition = Vector3.up * 0.08f;
        List<Renderer> parts = new List<Renderer>
        {
            Part(dummy.bag, "Post", PrimitiveType.Cylinder, new Vector3(0f, 0.55f, 0f), new Vector3(0.1f, 0.55f, 0.1f), material, wood),
            Part(dummy.bag, "Torso", PrimitiveType.Cylinder, new Vector3(0f, 1.2f, 0f), new Vector3(0.55f, 0.35f, 0.45f), material, canvas),
            Part(dummy.bag, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.72f, 0f), new Vector3(0.32f, 0.32f, 0.32f), material, canvas),
            Part(dummy.bag, "Arms", PrimitiveType.Cube, new Vector3(0f, 1.35f, 0f), new Vector3(1.1f, 0.12f, 0.12f), material, wood),
            Part(dummy.bag, "Target", PrimitiveType.Cylinder, new Vector3(0f, 1.22f, 0.24f), new Vector3(0.36f, 0.01f, 0.36f), material, red, Quaternion.Euler(90f, 0f, 0f)),
            Part(dummy.bag, "Target Ring", PrimitiveType.Cylinder, new Vector3(0f, 1.22f, 0.245f), new Vector3(0.24f, 0.01f, 0.24f), material, canvas, Quaternion.Euler(90f, 0f, 0f)),
            Part(dummy.bag, "Bullseye", PrimitiveType.Cylinder, new Vector3(0f, 1.22f, 0.25f), new Vector3(0.1f, 0.01f, 0.1f), material, red, Quaternion.Euler(90f, 0f, 0f))
        };

        dummy.renderers = parts.ToArray();
        dummy.colours = new Color[dummy.renderers.Length];

        for (int i = 0; i < dummy.renderers.Length; i++)
            dummy.colours[i] = i == 0 ? wood : i == 3 ? wood : i == 4 || i == 6 ? red : canvas;

        // Solid, so bullets and melee find it and the player can't walk through it.
        CapsuleCollider body = root.AddComponent<CapsuleCollider>();
        body.center = Vector3.up * 1.05f;
        body.height = 2f;
        body.radius = 0.32f;

        ProximityLabel.Add(root, 2.3f, 6f, new Color(0.35f, 1f, 0.55f), dummy.LabelText);
        return dummy;
    }

    private static Renderer Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 size, Material material,
        Color colour, Quaternion? rotation = null)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = name;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation ?? Quaternion.identity;
        part.transform.localScale = size;

        Renderer renderer = part.GetComponent<Renderer>();

        if (material != null)
        {
            renderer.sharedMaterial = material;
            ConsumableModel.Tint(renderer, colour, Color.black);
        }

        return renderer;
    }

    // Struck for `damage`, from `direction` (the way the blow travels), at a point.
    public void Hit(int damage, Vector3 point, Vector3 direction)
    {
        lastHit = damage;
        total += damage;
        hits.Add((Time.time, damage));

        // Rock away from the blow, harder for bigger hits.
        Vector3 local = transform.InverseTransformDirection(direction.normalized);
        tiltSpeed += new Vector2(local.z, -local.x) * (90f + 40f * damage);
        flash = 1f;

        DamageNumber.Show(point + Vector3.up * 0.3f, damage);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("melee.hit", 3, ProceduralAudio.MeleeHit)), point, 0.35f, Random.Range(0.85f, 1f));

        if (ImpactEffects.TryGetMaterials(out ImpactEffects.Materials materials))
            ImpactEffects.Sparks(point, -direction.normalized, materials);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // A damped spring back to upright.
        tiltSpeed += (-tilt * 60f - tiltSpeed * 6f) * dt;
        tilt += tiltSpeed * dt;
        tilt = Vector2.ClampMagnitude(tilt, 25f);
        bag.localRotation = Quaternion.Euler(tilt.x, 0f, tilt.y);

        if (flash > 0f)
        {
            flash = Mathf.Max(0f, flash - dt * 6f);

            for (int i = 0; i < renderers.Length; i++)
                ConsumableModel.Tint(renderers[i], Color.Lerp(colours[i], Color.white, flash * 0.6f), Color.white * flash * 0.4f);
        }

        while (hits.Count > 0 && Time.time - hits[0].time > DpsWindow)
            hits.RemoveAt(0);
    }

    private string LabelText()
    {
        int recent = 0;

        foreach ((float _, int damage) in hits)
            recent += damage;

        return "TRAINING DUMMY  <size=70%><alpha=#99>// FREE AMMO HERE</size>\n"
            + $"<size=70%>LAST HIT {lastHit}   //   DPS {recent / DpsWindow:0.0}   //   TOTAL {total}</size>";
    }
}
