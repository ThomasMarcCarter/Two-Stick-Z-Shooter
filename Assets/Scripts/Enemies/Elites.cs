using UnityEngine;

// Elite zombies: rare tougher versions of a normal zombie, shown by a colour tint and a small
// chevron above the head. Fast ones run at 1.5x speed, Armoured ones have 2.5x health and are
// barely knocked back, Explosive ones burst on death (the Corpse Bloat blast). They always
// drop ammo, half the time a material, and 3 bonus scrap. The numbers and look live here; the
// Enemy side is Enemy.Elite.cs, the chance and drops are in the FloorGenerator (Spawning).
public enum EliteKind
{
    None,
    Fast,
    Armoured,
    Explosive
}

public static class Elites
{
    public const float FastSpeed = 1.5f;
    public const float ArmouredHealth = 2.5f;
    public const float ArmouredKnockback = 0.3f;
    public const int BonusScrap = 3;
    public const float MaterialChance = 0.5f;

    // Chance a normal spawn is an elite: none on floor 1, then 6% and 10%.
    public static float Chance(int floor) => floor <= 1 ? 0f : floor == 2 ? 0.06f : 0.10f;

    public static string Name(EliteKind kind)
    {
        switch (kind)
        {
            case EliteKind.Fast: return "Fast Elite";
            case EliteKind.Armoured: return "Armoured Elite";
            case EliteKind.Explosive: return "Explosive Elite";
            default: return "";
        }
    }

    public static Color Tint(EliteKind kind)
    {
        switch (kind)
        {
            case EliteKind.Fast: return new Color(1f, 0.85f, 0.2f);
            case EliteKind.Armoured: return new Color(0.6f, 0.75f, 0.9f);
            case EliteKind.Explosive: return new Color(1f, 0.5f, 0.1f);
            default: return Color.white;
        }
    }

    // A random elite kind.
    public static EliteKind Random() => (EliteKind)UnityEngine.Random.Range(1, 4);
}

// The chevron over an elite's head: a small glowing "v" that always faces the camera and
// ignores the parent's scale. Parented to the enemy, so room culling hides it too.
public class EliteMarker : MonoBehaviour
{
    private const float Height = 2.1f;
    private const float Size = 0.3f;

    private static Material material;
    private static Mesh chevron;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        material = null;

        if (chevron != null)
            Object.Destroy(chevron);

        chevron = null;
    }

    // Shared unlit material (the soft glow material doesn't draw on a plain mesh).
    private static Material Material()
    {
        if (material == null)
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        return material;
    }

    // One shared mesh for every marker, built on first use.
    private static Mesh Chevron()
    {
        if (chevron != null)
            return chevron;

        // Two bars meeting at a point below: a "v".
        Mesh mesh = new Mesh { name = "Elite Chevron" };
        mesh.vertices = new[]
        {
            new Vector3(-1f, 1f, 0f), new Vector3(-0.6f, 1f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0.6f, 0f),
            new Vector3(1f, 1f, 0f), new Vector3(0.6f, 1f, 0f)
        };
        mesh.triangles = new[] { 0, 3, 2, 0, 1, 3, 2, 3, 4, 3, 5, 4 };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white, Color.white, Color.white };
        mesh.RecalculateBounds();
        chevron = mesh;
        return chevron;
    }

    public static void Create(Enemy enemy, Color colour)
    {
        GameObject go = new GameObject("Elite Marker");
        go.transform.SetParent(enemy.transform, false);
        go.AddComponent<EliteMarker>();

        go.AddComponent<MeshFilter>().sharedMesh = Chevron();

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = Material();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", colour);
        renderer.SetPropertyBlock(block);
    }

    private void LateUpdate()
    {
        Transform parent = transform.parent;
        Vector3 s = parent.lossyScale;
        transform.position = parent.position + Vector3.up * Height;
        transform.localScale = new Vector3(Size / Mathf.Max(0.01f, s.x), Size / Mathf.Max(0.01f, s.y), Size / Mathf.Max(0.01f, s.z));

        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
    }
}
