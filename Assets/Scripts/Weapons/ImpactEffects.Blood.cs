using System.Collections.Generic;
using UnityEngine;

// ImpactEffects' blood: pools and splats on the floor, spray and spatter where flesh is hit,
// and finding the point on a body's model the shot struck.
public static partial class ImpactEffects
{
    // With the sprite art: a pixel pool of blood where a zombie fell, size across, randomly turned
    // (parented to its room, oldest first past the cap). False without the sheet.
    public static bool FloorPool(Vector3 position, float size, Transform room)
    {
        SpriteSheet sheet = SpriteVisuals.Current != null ? SpriteVisuals.Current.bloodPool : null;

        if (sheet == null || sheet.texture == null)
            return false;

        Vector2 turn = Random.insideUnitCircle.normalized;
        Decal(sheet, position, new Vector3(turn.x, 0f, turn.y), size, room, floorSplats, MaxFloorSplats);
        return true;
    }

    // A pool of blood on the floor where a zombie fell, size across, randomly turned. Parented to
    // its room so it goes with the floor; the oldest go first past the cap.
    public static void FloorSplat(Vector3 position, float size, Transform room, Material material)
    {
        if (material == null)
            return;

        EnsureQuad();

        GameObject splat = new GameObject("Blood Splat");
        // Slightly different heights, so overlapping splats don't flicker against each other.
        splat.transform.SetPositionAndRotation(
            new Vector3(position.x, Random.Range(0.006f, 0.014f), position.z),
            Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
        // Up to half as big again, so the same variant comes out at different sizes.
        splat.transform.localScale = Vector3.one * size * Random.Range(1f, 1.5f);

        if (room != null)
            splat.transform.SetParent(room, true);

        splat.AddComponent<MeshFilter>().sharedMesh = quad;
        MeshRenderer renderer = splat.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        floorSplats.Enqueue(splat);

        while (floorSplats.Count > MaxFloorSplats)
        {
            GameObject oldest = floorSplats.Dequeue();

            if (oldest != null)
                Object.Destroy(oldest);
        }
    }

    private static void EnsureQuad()
    {
        if (quad != null)
            return;

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(temp);
    }

    // incoming: the bullet's direction of travel; some of the spray carries on through.
    public static void FleshHit(Vector3 point, Vector3 normal, Vector3 incoming, Transform body, Materials materials)
    {
        Ensure(materials);

        // On the model rather than the hitbox: the body part the shot went into, which the mark
        // then sticks to as it moves.
        Transform surface = body;

        if (ModelSurface(body, point, incoming, out Vector3 onModel, out Vector3 modelNormal, out Transform part))
        {
            point = onModel;
            normal = modelNormal;
            surface = part;
        }

        Color dark = new Color(0.35f, 0.02f, 0.02f);
        Color bright = new Color(0.6f, 0.04f, 0.03f);
        bool sprites = SpriteVisuals.Current != null && SpriteVisuals.Current.bloodSplash != null;
        Burst(blood, point, normal, sprites ? 3 : 9, 1.5f, 4f, 0.3f, 0.5f, 0.12f, 0.22f, 60f, bright, dark);
        Burst(blood, point, incoming.normalized, sprites ? 3 : 6, 1.5f, 3.5f, 0.25f, 0.45f, 0.1f, 0.18f, 35f, dark, bright);

        if (sprites)
            SprayAndSpatter(point, incoming, body);

        // Goes with the zombie (and is destroyed with it). Sprites have no surface to mark.
        if (body == null || body.GetComponent<SpriteCharacter>() == null)
            Mark(point, normal, surface, FleshScorch, Random.Range(0.16f, 0.24f));
    }

    // The sprite art's blood for a hit: a spray in the air out of the wound, toward the way the shot
    // went (mirrored when that's to the left on screen), and a spatter on the floor beyond the body.
    private static void SprayAndSpatter(Vector3 point, Vector3 incoming, Transform body)
    {
        SpriteVisuals visuals = SpriteVisuals.Current;
        Enemy enemy = body != null ? body.GetComponent<Enemy>() : null;
        Transform room = enemy != null && enemy.Room != null ? enemy.Room.transform : null;
        Vector3 along = new Vector3(incoming.x, 0f, incoming.z);

        if (along.sqrMagnitude < 0.0001f)
        {
            Vector2 any = Random.insideUnitCircle.normalized;
            along = new Vector3(any.x, 0f, any.y);
        }

        along.Normalize();
        Camera camera = Camera.main;
        bool left = camera != null && Vector3.Dot(along, camera.transform.right) < 0f;
        SpriteSheet splash = visuals.bloodSplash;
        int variants = Mathf.Max(1, splash.texture.height / splash.frameHeight);
        SpriteEffect.Play(splash, Random.Range(0, variants), SplashFps, point, SplashWidth, room, false, point.y, left);

        if (visuals.bloodDrip != null && visuals.bloodDrip.texture != null)
        {
            Vector3 origin = body != null ? body.position : point;
            Decal(visuals.bloodDrip, origin + along * Random.Range(0.35f, 1.1f), along, Random.Range(0.5f, 0.8f), room, drips, MaxDrips);
        }
    }

    // A flat pixel sprite on the floor (blood), size across, its art's right turned along `along`,
    // a random variant from the sheet's first row, sometimes flipped. Capped per queue.
    private static void Decal(SpriteSheet sheet, Vector3 position, Vector3 along, float size, Transform room, Queue<GameObject> queue, int cap)
    {
        GameObject holder = new GameObject(sheet.name + " Decal");
        holder.transform.SetPositionAndRotation(new Vector3(position.x, 0f, position.z),
            Quaternion.LookRotation(along) * Quaternion.Euler(0f, -90f, 0f));

        if (room != null)
            holder.transform.SetParent(room, true);

        PixelSprite sprite = PixelSprite.Create("Sprite", holder.transform, sheet, PixelSprite.Mode.Flat);
        sprite.Scale = size / (sheet.frameWidth / Mathf.Max(1f, sheet.pixelsPerUnit));
        // Slightly different heights, so overlapping ones don't flicker; above the litter.
        sprite.Lift = Random.Range(0.002f, 0.008f);
        sprite.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sprite.SetFrame(Random.Range(0, Mathf.Max(1, sheet.texture.width / sheet.frameWidth)), 0, false, Random.value < 0.5f);

        queue.Enqueue(holder);

        while (queue.Count > cap)
        {
            GameObject oldest = queue.Dequeue();

            if (oldest != null)
                Object.Destroy(oldest);
        }
    }

    // Where a shot along `incoming` through `point` meets the body's model (its "Model" child):
    // the nearest body part along the path, using each part's mesh box, which is close for these
    // blocky models. Missing every part (a shot past a thin limb), the part nearest the point,
    // facing back along the shot. False if the body has no model.
    private static bool ModelSurface(Transform body, Vector3 point, Vector3 incoming, out Vector3 hitPoint, out Vector3 hitNormal, out Transform part)
    {
        hitPoint = point;
        hitNormal = Vector3.zero;
        part = null;
        Transform model = body != null ? body.Find("Model") : null;
        Vector3 direction = new Vector3(incoming.x, 0f, incoming.z);

        if (model == null || direction.sqrMagnitude < 0.0001f)
            return false;

        direction.Normalize();
        Vector3 origin = point - direction * 1.5f;
        float nearestAlong = float.MaxValue;
        float nearestAside = float.MaxValue;
        Transform aside = null;
        Vector3 asidePoint = point;

        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null)
                continue;

            Transform t = filter.transform;
            Bounds box = filter.sharedMesh.bounds;
            Ray ray = new Ray(t.InverseTransformPoint(origin), t.InverseTransformVector(direction));

            if (box.IntersectRay(ray, out float distance))
            {
                Vector3 local = ray.GetPoint(distance);
                Vector3 world = t.TransformPoint(local);
                float along = Vector3.Distance(origin, world);

                if (along < nearestAlong)
                {
                    nearestAlong = along;
                    hitPoint = world;
                    hitNormal = t.TransformDirection(FaceNormal(box, local)).normalized;
                    part = t;
                }
            }
            else if (part == null)
            {
                Vector3 closest = t.TransformPoint(box.ClosestPoint(t.InverseTransformPoint(point)));
                float off = Vector3.Distance(closest, point);

                if (off < nearestAside)
                {
                    nearestAside = off;
                    aside = t;
                    asidePoint = closest;
                }
            }
        }

        if (part == null && aside != null)
        {
            part = aside;
            hitPoint = asidePoint;
            hitNormal = -direction;
        }

        return part != null;
    }

    // The outward direction of the face of the box a point on its surface is on.
    private static Vector3 FaceNormal(Bounds box, Vector3 point)
    {
        Vector3 offset = point - box.center;
        Vector3 extents = box.extents;
        float x = extents.x > 0f ? Mathf.Abs(offset.x) / extents.x : 0f;
        float y = extents.y > 0f ? Mathf.Abs(offset.y) / extents.y : 0f;
        float z = extents.z > 0f ? Mathf.Abs(offset.z) / extents.z : 0f;

        if (x >= y && x >= z)
            return new Vector3(Mathf.Sign(offset.x), 0f, 0f);

        return y >= z ? new Vector3(0f, Mathf.Sign(offset.y), 0f) : new Vector3(0f, 0f, Mathf.Sign(offset.z));
    }
}
