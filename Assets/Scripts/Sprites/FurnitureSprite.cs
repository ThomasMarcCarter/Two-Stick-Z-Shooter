using System.Collections.Generic;
using UnityEngine;

// A piece of furniture, a hideout station, the uplink terminal or a pillar drawn as a pixel sprite
// (the art pass) instead of its 3D model. Each sheet holds the piece seen four ways (facing the
// camera, right, away, left; Tools/Sprites/furniture.py) and a second set of each (dusty for
// furniture, open for the survivor's cage and its door), with any
// animation (a generator's flywheel, the fire barrel's flames) along the rows. The view nearest
// the way the piece faces is picked, scaled to the size it was placed at, and stood upright on the
// south edge of its footprint, so anything north of that edge is behind it and anything south in
// front. Glowing pixels (screens, lamps, fire) are a second, unlit sprite on top.
// The model stays as a shadow caster only (its shadows and the light it blocks are the real
// shape's), and its colliders, lights and labels stay as they were. Without the sprite art, or a
// sheet for the name, nothing changes.
[DefaultExecutionOrder(250)]
public class FurnitureSprite : MonoBehaviour
{
    private const float GlowGap = 0.02f;

    // Screen metres per metre of height the furniture art is drawn at (Tools/Sprites/furniture.py),
    // so a piece lifted off the floor (a rail, a shelf on a wall) sits at the height its art implies.
    private const float DrawnHeight = 0.7f;

    private PixelSprite sprite;
    private PixelSprite glow;
    private int view;
    private bool secondRows;       // rows 4-7: dusty, or open
    private int frames = 1;
    private float fps;
    private int column;
    private int shownColumn = -1;
    private float phase;
    private float lift;             // metres off the floor
    private string entryName;
    private Vector3 builtFront;
    private Vector3 builtSize;

    // Whether the sprite art has a sheet for this name.
    public static bool Has(string name)
    {
        return SpriteVisuals.Current != null && SpriteVisuals.Current.ForFurniture(name) != null;
    }

    // `front` is the way the piece faces, `size` its width, height and depth in its own axes, and
    // `floorCentre` the point under the middle of its footprint (its y lifts it, for a shelf on a
    // wall). Null without a sheet for the name.
    public static FurnitureSprite Apply(GameObject root, string name, Vector3 front, Vector3 size, Vector3 floorCentre)
    {
        SpriteVisuals visuals = SpriteVisuals.Current;
        SpriteVisuals.FurnitureEntry entry = visuals != null && root != null ? visuals.ForFurniture(name) : null;

        if (entry == null)
            return null;

        FurnitureSprite furniture = root.GetComponent<FurnitureSprite>();

        if (furniture == null)
            furniture = root.AddComponent<FurnitureSprite>();

        furniture.Build(entry, front, size, floorCentre);
        return furniture;
    }

    // Grey with years of dust (the hideout's abandoned areas).
    public bool Dusty
    {
        get => secondRows;
        set
        {
            secondRows = value;
            shownColumn = -1;
        }
    }

    // Smashed (a run's screens and glass, FurnitureDamage): the same second set of rows, drawn
    // broken for those pieces (Tools/Sprites/furniture.py ALTS) instead of dusty.
    public bool Broken
    {
        get => secondRows;
        set => Dusty = value;
    }

    // Whether this piece's second rows are drawn broken rather than dusty.
    public bool CanBreak => entryName != null && FurnitureDamage.HasBrokenArt(entryName);

    // Redrawn as another sheet where it stands now (a chair knocked over), keeping its facing,
    // size and lift. False without a sheet for the name.
    public bool Swap(string name)
    {
        SpriteVisuals.FurnitureEntry entry = SpriteVisuals.Current != null ? SpriteVisuals.Current.ForFurniture(name) : null;

        if (entry == null)
            return false;

        if (sprite != null)
            Destroy(sprite.gameObject);

        if (glow != null)
            Destroy(glow.gameObject);

        sprite = null;
        glow = null;
        column = 0;
        Build(entry, builtFront, builtSize, new Vector3(transform.position.x, lift, transform.position.z));
        entryName = name;
        return true;
    }

    // Open (the survivor's cage once freed): the same second set of rows.
    public bool Open
    {
        get => secondRows;
        set => Dusty = value;
    }

    // The glowing layer's renderer (screens, lamps), or null when the piece has none.
    public Renderer GlowRenderer => glow != null ? glow.Renderer : null;

    // How many frames (or, for a still piece, variants) its row holds.
    public int Frames => frames;

    // A still piece's frame along its row (when it isn't animated).
    public int Column
    {
        get => column;
        set
        {
            if (column == value)
                return;

            column = value;
            shownColumn = -1;
        }
    }

    private void Build(SpriteVisuals.FurnitureEntry entry, Vector3 front, Vector3 size, Vector3 floorCentre)
    {
        entryName = entry.name;
        builtFront = front;
        builtSize = size;
        frames = Mathf.Max(1, entry.frames);
        fps = entry.fps;
        phase = Random.value * 10f;

        // Which way it faces on screen: toward the camera, right, away or left.
        Camera camera = Camera.main;
        Vector3 toward = camera != null ? -camera.transform.forward : Vector3.back;
        Vector3 right = camera != null ? camera.transform.right : Vector3.right;
        toward.y = 0f;
        right.y = 0f;
        front.y = 0f;
        toward = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.back;
        right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
        front = front.sqrMagnitude > 0.0001f ? front.normalized : toward;

        float facing = Vector3.Dot(front, toward);
        float side = Vector3.Dot(front, right);
        view = Mathf.Abs(facing) >= Mathf.Abs(side) ? (facing >= 0f ? 0 : 2) : (side >= 0f ? 1 : 3);
        bool across = view == 0 || view == 2;

        // Sized to the piece across the screen; stood on the edge of its footprint nearest the camera.
        SpriteSheet sheet = entry.sheet;
        float drawn = across ? sheet.footprint.x : sheet.footprint.z;
        float shown = across ? size.x : size.z;
        float deep = across ? size.z : size.x;
        Vector3 edge = new Vector3(floorCentre.x, 0f, floorCentre.z) + toward * deep / 2f;

        Transform anchor = transform.Find("Sprite Anchor");

        if (anchor == null)
        {
            anchor = new GameObject("Sprite Anchor").transform;
            anchor.SetParent(transform, false);
        }

        anchor.position = edge;

        if (sprite == null)
            sprite = PixelSprite.Create("Furniture Sprite", transform, sheet, PixelSprite.Mode.Upright);

        sprite.Follow = anchor;
        lift = floorCentre.y;
        sprite.Scale = Mathf.Max(0.05f, shown / Mathf.Max(0.01f, drawn));
        sprite.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (entry.glow != null)
        {
            if (glow == null)
            {
                glow = PixelSprite.Create("Furniture Glow", transform, entry.glow, PixelSprite.Mode.Upright);
                glow.UseGlow();
            }

            glow.Follow = anchor;
            glow.Scale = sprite.Scale;
            glow.DepthOffset = -GlowGap;
        }

        HideModel();
        shownColumn = -1;
        ShowFrame();
    }

    // Every mesh under the piece but the sprites: shadow casters keep casting (the real shape's
    // shadow), the rest are switched off.
    private void HideModel()
    {
        List<MeshRenderer> renderers = new List<MeshRenderer>();
        GetComponentsInChildren(true, renderers);

        foreach (MeshRenderer part in renderers)
        {
            if (part.GetComponent<PixelSprite>() != null)
                continue;

            if (part.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off)
                part.enabled = false;
            else
                part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }
    }

    private void LateUpdate()
    {
        // Lifted by as much as its art draws height (the camera's squash is only known once it runs).
        if (sprite != null && lift != 0f)
        {
            sprite.Lift = lift * DrawnHeight / Mathf.Max(0.25f, CameraFollow.VerticalScale);

            if (glow != null)
                glow.Lift = sprite.Lift;
        }

        ShowFrame();
    }

    private void ShowFrame()
    {
        if (sprite == null)
            return;

        int frame = fps > 0f && frames > 1 ? (int)((Time.time + phase) * fps) % frames : Mathf.Clamp(column, 0, frames - 1);

        if (frame == shownColumn)
            return;

        shownColumn = frame;
        int row = view + (secondRows ? 4 : 0);
        sprite.SetFrame(frame, row, false);

        if (glow != null)
            glow.SetFrame(frame, row, false);
    }
}
