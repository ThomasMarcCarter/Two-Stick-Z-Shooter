using System;
using UnityEngine;

// A warning glowing on the floor for a moment before something happens there: a disc where a
// boss's zombie is about to climb in, or a wedge in front of the Butcher before its sweep. A
// faint outline shows the whole area from the start and a brighter fill grows out from the
// middle until it's full, so the timing reads at a glance; then the action runs and the marker
// goes. It sits just over the floor litter (FloorPickups.GlowHeight) and lights the floor around
// it a little. Built in code from the drops' glow material (soft, unlit, see-through).
public class FloorMarker : MonoBehaviour
{
    private const int Segments = 40;
    private const float OutlineAlpha = 0.28f;
    private const float FillAlpha = 0.75f;

    // How many markers are waiting to spawn a zombie (so a boss's cap counts them too).
    public static int PendingSpawns { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        PendingSpawns = 0;
    }

    private Mesh outline;
    private Mesh fill;
    private MeshRenderer fillRenderer;
    private MaterialPropertyBlock block;
    private Light glow;
    private Color colour;
    private float radius;
    private float arc;
    private float started;
    private float duration;
    private Action done;
    private bool countsAsSpawn;
    private bool finished;

    // A round warning, radius across, at a point on the floor.
    public static FloorMarker Disc(Vector3 centre, float radius, Color colour, float duration, Transform parent, Action done)
    {
        return Create(centre, Vector3.forward, radius, 360f, colour, duration, parent, done);
    }

    // A wedge of `arc` degrees out to radius, pointing along facing.
    public static FloorMarker Sector(Vector3 centre, Vector3 facing, float radius, float arc, Color colour, float duration, Transform parent, Action done)
    {
        return Create(centre, facing, radius, arc, colour, duration, parent, done);
    }

    // A disc where a zombie will climb in after the delay (spawn returns it, or null if the
    // room's full by then).
    public static FloorMarker Spawn(Vector3 position, Color colour, float delay, Transform parent, Func<Enemy> spawn)
    {
        FloorMarker marker = Disc(position, 0.7f, colour, delay, parent, () => spawn());
        marker.countsAsSpawn = true;
        PendingSpawns++;
        return marker;
    }

    private static FloorMarker Create(Vector3 centre, Vector3 facing, float radius, float arc, Color colour, float duration, Transform parent, Action done)
    {
        GameObject root = new GameObject("Floor Marker");
        root.transform.SetParent(parent, true);
        facing.y = 0f;
        root.transform.SetPositionAndRotation(new Vector3(centre.x, FloorPickups.GlowHeight, centre.z),
            Quaternion.LookRotation(facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector3.forward, Vector3.up));

        FloorMarker marker = root.AddComponent<FloorMarker>();
        marker.colour = colour;
        marker.radius = radius;
        marker.arc = Mathf.Clamp(arc, 1f, 360f);
        marker.started = Time.time;
        marker.duration = Mathf.Max(0.05f, duration);
        marker.done = done;
        marker.block = new MaterialPropertyBlock();

        Material material = AmmoVisuals.Current != null ? AmmoVisuals.Current.glowMaterial : null;
        marker.outline = marker.Part("Outline", material, OutlineAlpha, out _);
        marker.fill = marker.Part("Fill", material, 0f, out marker.fillRenderer);
        marker.BuildShape(marker.outline, 1f);
        marker.BuildShape(marker.fill, 0f);

        marker.glow = new GameObject("Glow Light").AddComponent<Light>();
        marker.glow.transform.SetParent(root.transform, false);
        marker.glow.transform.localPosition = Vector3.up * 0.4f + (marker.arc < 360f ? Vector3.forward * radius * 0.5f : Vector3.zero);
        marker.glow.type = LightType.Point;
        marker.glow.color = colour;
        marker.glow.range = radius * 1.6f + 0.5f;
        marker.glow.intensity = 0f;
        marker.glow.shadows = LightShadows.None;
        return marker;
    }

    private Mesh Part(string name, Material material, float alpha, out MeshRenderer renderer)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(transform, false);
        Mesh mesh = new Mesh { name = "Floor Marker " + name };
        mesh.MarkDynamic();
        part.AddComponent<MeshFilter>().sharedMesh = mesh;

        renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        SetAlpha(renderer, alpha);
        return mesh;
    }

    private void SetAlpha(Renderer renderer, float alpha)
    {
        Color tint = colour;
        tint.a = alpha;
        block.SetColor("_Color", tint);
        renderer.SetPropertyBlock(block);
    }

    // A fan out to `reach` (0 to 1) of the radius, over the marker's arc, lying on the floor.
    private void BuildShape(Mesh mesh, float reach)
    {
        Vector3[] vertices = new Vector3[Segments + 2];
        Vector2[] uvs = new Vector2[Segments + 2];
        Color[] colours = new Color[Segments + 2];
        int[] triangles = new int[Segments * 3];
        float r = radius * Mathf.Max(0.001f, reach);
        vertices[0] = Vector3.zero;

        // The glow texture is a soft dot: the middle maps to its centre and the rim most of the
        // way to its edge, so the marker is bright inside and soft round the rim.
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i <= Segments; i++)
        {
            float angle = (-arc / 2f + arc * i / Segments) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Sin(angle) * r, 0f, Mathf.Cos(angle) * r);
            uvs[i + 1] = new Vector2(0.5f + Mathf.Sin(angle) * 0.3f, 0.5f + Mathf.Cos(angle) * 0.3f);
        }

        for (int i = 0; i < colours.Length; i++)
            colours[i] = Color.white;

        for (int i = 0; i < Segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colours;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private void Update()
    {
        if (finished)
            return;

        float t = Mathf.Clamp01((Time.time - started) / duration);
        BuildShape(fill, t);

        // Pulses faster and brighter as it fills.
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 22f, t));
        SetAlpha(fillRenderer, FillAlpha * Mathf.Lerp(0.4f, 1f, t) * pulse);
        glow.intensity = Mathf.Lerp(0.3f, 2.2f, t) * pulse;

        if (t < 1f)
            return;

        Finish();
        done?.Invoke();
        Destroy(gameObject);
    }

    private void Finish()
    {
        if (finished)
            return;

        finished = true;

        if (countsAsSpawn)
            PendingSpawns = Mathf.Max(0, PendingSpawns - 1);
    }

    private void OnDestroy()
    {
        Finish();

        if (outline != null)
            Destroy(outline);

        if (fill != null)
            Destroy(fill);
    }
}
