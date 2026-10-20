using System.Collections.Generic;
using UnityEngine;

// A pickup, crate, thrown item or bit of room clutter drawn as a sprite (the sprite art pass)
// instead of its 3D model.
// The model's mesh renderers are kept hidden (their colliders, lights, glow quads and labels
// stay), and the item's frame stands on the floor under the pickup like a character, floating
// gently if it's a drop that bobbed, or lifted to a wall mount. Flying (thrown) items are drawn
// screen-facing where the item actually is in the air instead, spinning while they move. Clutter
// (ApplyProp) is sized to the prop it stands in for and follows it as it's shoved or knocked
// over: upright on its bottom (so stacked boxes sit on each other), or lying flat for litter.
// Added by each pickup's Spawn (and Thrown.Launch, SupplyCrate, GunPickup, the floor's clutter)
// when SpriteVisuals has the art; without it nothing changes.
[DefaultExecutionOrder(250)]
public class ItemSprite : MonoBehaviour
{
    private const float FloatHeight = 0.18f;
    private const float FloatBob = 0.05f;
    private const float SpinSpeed = 540f;
    private const float RescanInterval = 0.5f;

    private PixelSprite sprite;
    private bool floating;
    private bool flying;
    private float lift;
    private bool frameOnFloor;      // stands the bottom of its frame on the floor, not its pivot
    private float phase;
    private float nextScan;
    private Vector3 lastPosition;
    private readonly List<Renderer> model = new List<Renderer>();
    private Transform anchor;       // props: the middle of the prop, which the sprite follows
    private float uprightHalf;      // props: half its height standing up
    private float lyingHalf;        // props: half its thickness lying down
    private bool tippable;          // small trash: shows its knocked-over frame when lying down
    private Vector2Int standingFrame;
    private int shownTip = -1;      // 0 standing, 1 lying to the right, 2 lying to the left

    // An item standing on the floor (or `lift` metres up, for a wall mount). Pickups cast no
    // shadow; a big solid thing like a supply crate does (castsShadow). A sheet pivoted above its
    // bottom (a gun's, on the barrel line) passes frameOnFloor, or its lower part sinks into the floor.
    public static ItemSprite Apply(GameObject root, SpriteSheet sheet, Vector2Int frame, bool floating = false, float lift = 0f, bool castsShadow = false, bool frameOnFloor = false)
    {
        ItemSprite item = Attach(root, sheet, PixelSprite.Mode.Upright);

        if (item == null)
            return null;

        item.sprite.Renderer.shadowCastingMode = castsShadow
            ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

        item.floating = floating;
        item.lift = lift;
        item.frameOnFloor = frameOnFloor;
        item.SetFrame(frame);
        return item;
    }

    // Something lying flat on the floor, `size` metres across (the hatch), `lift` metres up (above
    // the litter, so it's never drawn under it).
    public static ItemSprite ApplyFlat(GameObject root, SpriteSheet sheet, Vector2Int frame, float size, float lift = 0f)
    {
        ItemSprite item = Attach(root, sheet, PixelSprite.Mode.Flat);

        if (item == null)
            return null;

        item.sprite.Scale = size / (sheet.frameWidth / Mathf.Max(1f, sheet.pixelsPerUnit));
        item.sprite.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        item.lift = lift;
        item.SetFrame(frame);
        return item;
    }

    // A thrown item in the air.
    public static ItemSprite ApplyFlying(GameObject root, SpriteSheet sheet, Vector2Int frame)
    {
        ItemSprite item = Attach(root, sheet, PixelSprite.Mode.Facing);

        if (item == null)
            return null;

        item.flying = true;
        item.sprite.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        item.SetFrame(frame);
        return item;
    }

    // A bit of clutter, sized to the prop from its model's shape (before it's hidden), measured
    // in the prop's own frame so one placed lying down is sized as it would stand.
    public static ItemSprite ApplyProp(GameObject root, SpriteSheet sheet, ClutterSprites.Entry entry)
    {
        if (root == null || sheet == null)
            return null;

        Transform frame = root.transform;
        Bounds bounds = default;
        bool any = false;

        foreach (MeshFilter part in root.GetComponentsInChildren<MeshFilter>())
        {
            MeshRenderer renderer = part.GetComponent<MeshRenderer>();

            if (part.sharedMesh == null || renderer == null || !renderer.enabled)
                continue;

            Bounds mesh = part.sharedMesh.bounds;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3(i % 2 == 0 ? -1 : 1, (i / 2) % 2 == 0 ? -1 : 1, i / 4 == 0 ? -1 : 1));
                Vector3 local = frame.InverseTransformPoint(part.transform.TransformPoint(corner));

                if (!any)
                    bounds = new Bounds(local, Vector3.zero);
                else
                    bounds.Encapsulate(local);

                any = true;
            }
        }

        if (!any)
            return null;

        Vector3 size = Vector3.Scale(bounds.size, frame.lossyScale);
        Transform middle = new GameObject("Sprite Anchor").transform;
        middle.SetParent(frame, false);
        middle.localPosition = bounds.center;

        bool flat = entry.Sheet == ClutterSprites.Sheet.Flat;
        ItemSprite item = Attach(root, sheet, flat ? PixelSprite.Mode.Flat : PixelSprite.Mode.Upright);

        if (item == null)
            return null;

        float fit = entry.FitHeight ? size.y : Mathf.Max(size.x, size.z);
        item.sprite.Scale = Mathf.Max(0.05f, fit / (entry.ContentPixels / Mathf.Max(1f, sheet.pixelsPerUnit)));
        item.sprite.Follow = middle;
        item.sprite.Renderer.shadowCastingMode = entry.CastsShadow
            ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        item.anchor = middle;
        item.uprightHalf = size.y / 2f;
        item.lyingHalf = Mathf.Min(size.x, size.z) / 2f;
        item.tippable = entry.Sheet == ClutterSprites.Sheet.Small;
        item.standingFrame = entry.Frame;
        item.SetFrame(entry.Frame);
        return item;
    }

    private static ItemSprite Attach(GameObject root, SpriteSheet sheet, PixelSprite.Mode mode)
    {
        if (root == null || sheet == null)
            return null;

        ItemSprite item = root.GetComponent<ItemSprite>();

        if (item == null)
            item = root.AddComponent<ItemSprite>();

        // A pickup rebuilt for another item (a gun pickup given its gun) gets a fresh sprite.
        if (item.sprite != null && item.sprite.Sheet != sheet)
        {
            Destroy(item.sprite.gameObject);
            item.sprite = null;
        }

        if (item.sprite == null)
            item.sprite = PixelSprite.Create("Item Sprite", root.transform, sheet, mode);

        item.sprite.Renderer.enabled = true;

        item.phase = Random.value * 10f;
        item.lastPosition = root.transform.position;
        item.HideModel();
        return item;
    }

    public void SetFrame(Vector2Int frame)
    {
        if (sprite != null)
            sprite.SetFrame(frame.x, frame.y, false);
    }

    private void LateUpdate()
    {
        if (sprite == null)
            return;

        if (Time.time >= nextScan)
            HideModel();

        if (flying)
        {
            Vector3 moved = transform.position - lastPosition;
            sprite.Position = transform.position;

            if (moved.sqrMagnitude > 0.000001f)
                sprite.Angle += SpinSpeed * Time.deltaTime;
        }
        else if (anchor != null)
        {
            // Small trash lying down shows its knocked-over frame, its top the way it fell.
            int tip = 0;

            if (tippable && Vector3.Dot(transform.up, Vector3.up) < 0.5f)
            {
                Camera camera = Camera.main;
                tip = camera != null && Vector3.Dot(transform.up, camera.transform.right) < 0f ? 2 : 1;
            }

            if (tip != shownTip)
            {
                shownTip = tip;
                sprite.SetFrame(standingFrame.x, standingFrame.y + (tip > 0 ? 1 : 0), tip == 2);
            }

            sprite.Lift = Mathf.Max(0f, anchor.position.y - (tip > 0 ? lyingHalf : uprightHalf));
        }
        else
        {
            sprite.Lift = lift + (floating ? FloatHeight + Mathf.Sin(Time.time * 2f + phase) * FloatBob : 0f) + PivotRise();
        }

        lastPosition = transform.position;
    }

    // With frameOnFloor: how far up the drawn quad its pivot sits (stretched like the quad).
    private float PivotRise()
    {
        if (!frameOnFloor || sprite.Sheet == null)
            return 0f;

        return sprite.Sheet.pivot.y / Mathf.Max(1f, sprite.Sheet.pixelsPerUnit) * sprite.Scale / Mathf.Max(0.25f, CameraFollow.VerticalScale);
    }

    // Every mesh under the item but its glow and the sprite itself; again now and then, as some
    // pickups rebuild or add parts.
    private void HideModel()
    {
        nextScan = Time.time + RescanInterval;
        model.Clear();
        GetComponentsInChildren(true, model);

        foreach (Renderer part in model)
        {
            if (part is MeshRenderer && part.enabled && part.GetComponent<PixelSprite>() == null && !part.name.StartsWith("Glow"))
                part.enabled = false;
        }
    }
}
