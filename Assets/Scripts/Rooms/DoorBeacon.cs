using UnityEngine;

// Amber warning lights on a doorway, so doors stand out in a dark room: a lamp on top of each
// steel frame post and a lit strip down each post's room-side face, flashing left and right in
// turn like a level-crossing signal, with one small amber light spilling onto the floor in front
// of the door. Doorways of the same room flash together. Built by the FloorGenerator with each
// doorway's frame (just for looks: no colliders).
public class DoorBeacon : MonoBehaviour
{
    private static readonly Color Amber = new Color(1f, 0.55f, 0.08f);
    private const float Rate = 0.9f;   // flashes per second, each side

    private Renderer[] left;
    private Renderer[] right;
    private Light glow;
    private float offset;

    // `posts` are the two frame posts' centres (world), at the top of the wall; `inward` points
    // into the room.
    public static DoorBeacon Create(Transform parent, Vector3 postA, Vector3 postB, float topHeight, Vector3 inward,
        float postWidth, float wallThickness, Material material)
    {
        GameObject root = new GameObject("Door Beacon");
        root.transform.SetParent(parent, false);
        root.transform.position = (postA + postB) / 2f;

        DoorBeacon beacon = root.AddComponent<DoorBeacon>();
        beacon.left = beacon.Lamps(postA, topHeight, inward, postWidth, wallThickness, material);
        beacon.right = beacon.Lamps(postB, topHeight, inward, postWidth, wallThickness, material);

        beacon.glow = new GameObject("Light").AddComponent<Light>();
        beacon.glow.transform.SetParent(root.transform, false);
        beacon.glow.transform.position = new Vector3(root.transform.position.x, topHeight * 0.8f, root.transform.position.z) + inward * 0.6f;
        beacon.glow.type = LightType.Point;
        beacon.glow.color = Amber;
        beacon.glow.range = 3.2f;
        beacon.glow.shadows = LightShadows.None;

        // The same flash for every doorway in the room.
        beacon.offset = Mathf.Repeat(parent.position.x * 0.137f + parent.position.z * 0.071f, 1f);
        return beacon;
    }

    // A lamp on top of a post and a strip down its room-side face.
    private Renderer[] Lamps(Vector3 post, float topHeight, Vector3 inward, float postWidth, float wallThickness, Material material)
    {
        Vector3 faceOut = inward * (wallThickness / 2f + 0.06f);
        Renderer lamp = Part("Lamp", new Vector3(post.x, topHeight + 0.06f, post.z) + inward * 0.05f,
            new Vector3(postWidth + 0.1f, 0.12f, postWidth + 0.1f), material);
        Renderer strip = Part("Strip", new Vector3(post.x, topHeight * 0.55f, post.z) + faceOut,
            Abs(Vector3.Cross(inward, Vector3.up)) * (postWidth * 0.5f) + Vector3.up * topHeight * 0.75f + Abs(inward) * 0.02f, material);
        return new[] { lamp, strip };
    }

    private static Vector3 Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    private Renderer Part(string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(transform, true);
        part.transform.position = position;
        part.transform.localScale = size;

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (material != null)
            renderer.sharedMaterial = material;

        return renderer;
    }

    private void Update()
    {
        // Each side lit for a little under half the cycle, the other side in the other half.
        float cycle = Mathf.Repeat(Time.time * Rate + offset, 1f);
        float leftOn = Flash(cycle);
        float rightOn = Flash(Mathf.Repeat(cycle + 0.5f, 1f));

        SetLit(left, leftOn);
        SetLit(right, rightOn);

        if (glow != null)
            glow.intensity = 0.3f + 1.8f * Mathf.Max(leftOn, rightOn);
    }

    // Bright for the first 40% of the cycle with soft edges, dim the rest.
    private static float Flash(float t)
    {
        return Mathf.Clamp01(Mathf.Min(t / 0.05f, (0.42f - t) / 0.05f));
    }

    private static void SetLit(Renderer[] parts, float on)
    {
        foreach (Renderer part in parts)
            ConsumableModel.Tint(part, Color.Lerp(Amber * 0.25f, Amber, on), Amber * Mathf.Lerp(0.15f, 5f, on));
    }
}
