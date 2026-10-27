using System.Collections.Generic;
using UnityEngine;

// One frame of a sprite sheet drawn on a quad in the 3D world, lit like everything else
// (Roguelike/SpriteLit) and casting a shadow. Two ways of standing:
//   Upright (characters): a vertical quad on the floor under what it follows, turned to the
//   camera and stretched up by however much the camera's tilt squashes vertical things, so its
//   pixels come out square on screen. Being vertical, it sorts against walls and props as a
//   real body would (a quad facing the camera would lean back into the wall behind it).
//   Facing (guns, effects): a sprite at a given point, turned on screen by Angle and scaled by
//   Scale. Its corners are turned as they should look on screen and then mapped onto the quad,
//   which keeps its pixels square at any angle. The quad lies parallel to the screen, or with
//   Standing it stands upright like a character's (stretched up by the camera's squash), so a
//   held gun sits parallel to its holder's body instead of cutting through it.
//   Flat (floor litter): lying on the floor under what it follows, turned with it, like a decal.
// Upright and Flat sprites are drawn at Scale (a prop's sprite sized to the prop).
// Positions can be snapped to the art pixel grid (Snap, off: the camera moves smoothly).
// DepthOffset slides it along the line of sight (CameraFollow.ViewRay), which the camera doesn't
// see but the depth test does: a gun goes in front of or behind its holder that way. Frames are
// picked with SetFrame and set in the mesh's UVs (not a property block, which an enemy's hit flash
// uses). Upright and Standing quads are marked (second UV) to lean back and face the camera when
// CameraFollow has sprites face it (done in SpriteLit, which keeps the upright depth).
[DefaultExecutionOrder(300)]
public class PixelSprite : MonoBehaviour
{
    public const float PixelsPerUnit = 16f;

    public enum Mode { Upright, Facing, Flat }

    private static readonly Dictionary<Texture, Material> materials = new Dictionary<Texture, Material>();
    private static readonly Dictionary<Texture, Material> glowMaterials = new Dictionary<Texture, Material>();
    private static readonly int ShadowSquashId = Shader.PropertyToID("_SpriteShadowSquash");

    // Snap sprites to the art pixel grid. Off: the camera glides, so sprites do too.
    public static bool Snap { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        materials.Clear();
        glowMaterials.Clear();
        Snap = false;
    }

    private SpriteSheet sheet;
    private Mode mode;
    private MeshRenderer meshRenderer;
    private Mesh mesh;
    private readonly Vector3[] vertices = new Vector3[4];
    private readonly Vector2[] uvs = new Vector2[4];
    private static readonly Vector2[] Leans = { Vector2.right, Vector2.right, Vector2.right, Vector2.right };
    private static readonly Vector2[] NoLeans = { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero };
    private float builtStretch = -1f;
    private float builtAngle = float.NaN;
    private float builtScale;
    private bool builtMirrorY;
    private Rect frame = new Rect(-1f, -1f, 0f, 0f);
    private bool flipX;
    private bool flipY;

    // Upright: stands on the floor under this. Facing: drawn at Position.
    public Transform Follow { get; set; }
    public Vector3 Position { get; set; }
    public float Angle { get; set; }
    public float Scale { get; set; } = 1f;
    public float DepthOffset { get; set; }
    public bool MirrorY { get; set; }

    // Upright: metres above the floor it stands at (a floating pickup, a wall mount).
    public float Lift { get; set; }
    public bool Standing { get; set; }

    // Squashed or stretched across and up its own plane about its pivot (a hit's squash), 1 = as drawn.
    public Vector2 Stretch { get; set; } = Vector2.one;

    // The frame last set: its column and row, and whether it's mirrored.
    public int Column { get; private set; }
    public int Row { get; private set; }
    public bool Mirrored => flipX;
    public Renderer Renderer => meshRenderer;
    public SpriteSheet Sheet => sheet;

    public static PixelSprite Create(string name, Transform parent, SpriteSheet sheet, Mode mode, uint renderingLayers = 1u)
    {
        GameObject go = new GameObject(name);
        go.layer = parent != null ? parent.gameObject.layer : 0;
        go.transform.SetParent(parent, false);

        PixelSprite sprite = go.AddComponent<PixelSprite>();
        sprite.sheet = sheet;
        sprite.mode = mode;
        sprite.Follow = parent;
        sprite.Position = parent != null ? parent.position : Vector3.zero;

        sprite.mesh = new Mesh { name = name + " (sprite)" };
        sprite.mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = sprite.mesh;

        sprite.meshRenderer = go.AddComponent<MeshRenderer>();
        sprite.meshRenderer.sharedMaterial = MaterialFor(sheet);
        sprite.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        sprite.meshRenderer.receiveShadows = true;
        sprite.meshRenderer.renderingLayerMask = renderingLayers;

        sprite.SetFrame(0, 0, false);
        sprite.Place();
        return sprite;
    }

    // One material per sheet texture, copied from the visuals' base sprite material.
    private static Material MaterialFor(SpriteSheet sheet)
    {
        Texture texture = sheet != null ? sheet.texture : null;

        if (texture == null)
            return null;

        if (!materials.TryGetValue(texture, out Material material) || material == null)
        {
            Material source = SpriteVisuals.Current != null ? SpriteVisuals.Current.spriteMaterial : null;
            material = source != null ? new Material(source) : new Material(Shader.Find("Roguelike/SpriteLit"));
            material.name = texture.name + " (sprite)";
            material.SetTexture("_BaseMap", texture);
            materials[texture] = material;
        }

        return material;
    }

    // Draws it unlit at full brightness (glowing effects such as acid), casting no shadow.
    // Stays lit if the visuals have no glow material.
    public void UseGlow()
    {
        Texture texture = sheet != null ? sheet.texture : null;
        Material source = SpriteVisuals.Current != null ? SpriteVisuals.Current.glowSprite : null;

        if (texture == null || source == null)
            return;

        if (!glowMaterials.TryGetValue(texture, out Material material) || material == null)
        {
            material = new Material(source) { name = texture.name + " (glow sprite)" };
            material.SetTexture("_BaseMap", texture);
            glowMaterials[texture] = material;
        }

        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    // A frame by column and row (rows from the top of the sheet).
    public void SetFrame(int column, int row, bool mirror, bool upsideDown = false)
    {
        Rect uv = sheet != null ? sheet.FrameUV(column, row) : new Rect(0f, 0f, 1f, 1f);
        Column = column;
        Row = row;

        if (uv == frame && mirror == flipX && upsideDown == flipY)
            return;

        frame = uv;
        flipX = mirror;
        flipY = upsideDown;
        float u0 = mirror ? uv.xMax : uv.xMin;
        float u1 = mirror ? uv.xMin : uv.xMax;
        float v0 = upsideDown ? uv.yMax : uv.yMin;
        float v1 = upsideDown ? uv.yMin : uv.yMax;
        uvs[0] = new Vector2(u0, v0);
        uvs[1] = new Vector2(u1, v0);
        uvs[2] = new Vector2(u0, v1);
        uvs[3] = new Vector2(u1, v1);

        if (vertices[3] != Vector3.zero)
            mesh.uv = uvs;
    }

    // A clip's frame for a facing (see SpriteFacing).
    public void SetFrame(SpriteSheet.Clip clip, int index, SpriteFacing.Facing facing)
    {
        if (clip == null)
            return;

        int row = clip.row + (clip.directional ? facing.Row : 0);
        SetFrame(((index % clip.frames) + clip.frames) % clip.frames, row, clip.directional && facing.Mirror);
    }

    private void LateUpdate()
    {
        Place();
    }

    private void Place()
    {
        Camera camera = Camera.main;

        if (camera == null || sheet == null)
            return;

        Transform view = camera.transform;

        // How much the view squashes height: an upright quad is stretched by the inverse.
        float squash = Mathf.Max(0.25f, CameraFollow.VerticalScale);
        float stretch = mode == Mode.Upright ? 1f / squash : 1f;
        Shader.SetGlobalFloat(ShadowSquashId, squash);

        if (mode == Mode.Upright && (!Mathf.Approximately(stretch, builtStretch) || Scale != builtScale))
            BuildMesh(stretch);
        else if (mode == Mode.Flat && (builtStretch != 1f || Scale != builtScale))
            BuildMesh(1f);
        else if (mode == Mode.Facing)
            BuildFacingMesh();

        Vector3 position;
        Quaternion rotation;

        if (mode == Mode.Upright)
        {
            Vector3 feet = Follow != null ? Follow.position : transform.position;
            position = new Vector3(feet.x, Lift, feet.z);
            rotation = Quaternion.Euler(0f, view.eulerAngles.y, 0f);
        }
        else if (mode == Mode.Flat)
        {
            Vector3 at = Follow != null ? Follow.position : transform.position;
            position = new Vector3(at.x, Lift + 0.006f, at.z);
            rotation = Quaternion.Euler(90f, Follow != null ? Follow.eulerAngles.y : 0f, 0f);
        }
        else
        {
            position = Position;
            rotation = Standing ? Quaternion.Euler(0f, view.eulerAngles.y, 0f) : view.rotation;
        }

        if (Snap)
            position = SnapToPixels(position, view, sheet.pixelsPerUnit);

        transform.SetPositionAndRotation(position + CameraFollow.ViewRay(position) * DepthOffset, rotation);

        // World-sized whatever the parent's scale (enemy bodies are scaled blocks).
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(
            Stretch.x / Mathf.Max(0.0001f, parentScale.x),
            Stretch.y / Mathf.Max(0.0001f, parentScale.y),
            1f / Mathf.Max(0.0001f, parentScale.z));
    }

    // A screen-facing quad's corners in the camera's plane: mirrored, scaled and turned as they
    // should look on screen, then squashed by the screen stretch so they come out that way.
    private void BuildFacingMesh()
    {
        // Screen metres per metre of the quad, up it.
        float stretch = Standing ? Mathf.Max(0.25f, CameraFollow.VerticalScale) : CameraFollow.ScreenStretch;

        if (Angle == builtAngle && Scale == builtScale && MirrorY == builtMirrorY && Mathf.Approximately(stretch, builtStretch))
            return;

        builtAngle = Angle;
        builtScale = Scale;
        builtMirrorY = MirrorY;
        builtStretch = stretch;

        for (int i = 0; i < 4; i++)
            vertices[i] = FacingCorner(i, stretch);

        bool first = mesh.vertexCount == 0;
        mesh.vertices = vertices;

        if (first)
        {
            mesh.uv = uvs;
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        }

        mesh.uv2 = Standing ? Leans : NoLeans;

        mesh.RecalculateBounds();
    }

    // One corner of the facing quad, in its own space: mirrored, scaled and turned as it should
    // look on screen, then squashed by the screen stretch.
    private Vector3 FacingCorner(int i, float stretch)
    {
        float ppu = Mathf.Max(1f, sheet.pixelsPerUnit);
        Vector2 c = new Vector2(
            (i % 2 == 0 ? -sheet.pivot.x : sheet.frameWidth - sheet.pivot.x) / ppu,
            (i < 2 ? -sheet.pivot.y : sheet.frameHeight - sheet.pivot.y) / ppu) * Scale;

        if (MirrorY)
            c.y = -c.y;

        float radians = Angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
        return new Vector3(c.x * cos - c.y * sin, (c.x * sin + c.y * cos) / Mathf.Max(0.01f, stretch), 0f);
    }

    // A Standing facing sprite: how far its lowest corner reaches below Position, in metres of
    // height, at the current Angle/Scale/MirrorY (so it can be kept out of the floor).
    public float StandingDrop()
    {
        if (sheet == null)
            return 0f;

        float stretch = Mathf.Max(0.25f, CameraFollow.VerticalScale);
        float lowest = 0f;

        for (int i = 0; i < 4; i++)
            lowest = Mathf.Min(lowest, FacingCorner(i, stretch).y);

        return -lowest;
    }

    // To the nearest art pixel across and up the screen.
    public static Vector3 SnapToPixels(Vector3 position, Transform view, float pixelsPerUnit = PixelsPerUnit)
    {
        float across = Vector3.Dot(position, view.right);
        float up = Vector3.Dot(position, view.up);
        float step = 1f / pixelsPerUnit;
        return position + view.right * (Mathf.Round(across / step) * step - across) + view.up * (Mathf.Round(up / step) * step - up);
    }

    private void BuildMesh(float stretch)
    {
        builtStretch = stretch;
        builtScale = Scale;
        float ppu = Mathf.Max(1f, sheet.pixelsPerUnit) / Mathf.Max(0.01f, Scale);
        float x0 = -sheet.pivot.x / ppu;
        float x1 = (sheet.frameWidth - sheet.pivot.x) / ppu;
        float y0 = -sheet.pivot.y / ppu * stretch;
        float y1 = (sheet.frameHeight - sheet.pivot.y) / ppu * stretch;

        vertices[0] = new Vector3(x0, y0, 0f);
        vertices[1] = new Vector3(x1, y0, 0f);
        vertices[2] = new Vector3(x0, y1, 0f);
        vertices[3] = new Vector3(x1, y1, 0f);

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.uv2 = mode == Mode.Upright ? Leans : NoLeans;
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        mesh.RecalculateBounds();
    }

    private void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);
    }
}
