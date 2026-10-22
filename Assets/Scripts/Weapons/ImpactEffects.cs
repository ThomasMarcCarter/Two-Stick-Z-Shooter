using System.Collections.Generic;
using UnityEngine;

// What a bullet leaves where it hits: a scorch mark stuck to the surface, plus a shower of sparks
// off a wall or a small spray of blood off a zombie. Sparks and blood come from two shared
// particle systems (one burst per hit, so heavy fire stays cheap). Scorch marks are small decals
// parented to what was hit: a zombie's go with it, and wall marks are capped, oldest first.
// Built the first time a bullet hits anything; the materials come from that bullet.
// With the sprite art, blood is pixel art too: a spray out of the wound (a lit SpriteEffect, toward
// the way the shot went), a spatter on the floor beyond it, and a pool where a zombie fell, all
// flat sprites from SpriteVisuals' blood sheets (Tools/Sprites/blood.py), oldest first past the cap.
// The few square droplets left die on the floor instead of falling through it.
public static class ImpactEffects
{
    private const int MaxWallMarks = 80;
    private const int MaxFloorSplats = 60;
    private const int MaxDrips = 120;
    private const float SplashWidth = 1.2f;
    private const float SplashFps = 18f;

    private static ParticleSystem sparks;
    private static ParticleSystem blood;
    private static ParticleSystem debris;
    private static Material markMaterial;
    private static Mesh quad;
    private static readonly Queue<GameObject> wallMarks = new Queue<GameObject>();
    private static readonly Queue<GameObject> floorSplats = new Queue<GameObject>();
    private static readonly Queue<GameObject> drips = new Queue<GameObject>();
    private static MaterialPropertyBlock markBlock;
    private static Materials known;
    private static bool haveKnown;

    private static readonly Color WallScorch = new Color(0.04f, 0.035f, 0.03f, 0.85f);
    private static readonly Color FleshScorch = new Color(0.12f, 0.02f, 0.015f, 0.9f);

    // Statics outlive Play sessions with domain reload off; the objects they point to don't.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        sparks = null;
        blood = null;
        debris = null;
        markMaterial = null;
        haveKnown = false;
        wallMarks.Clear();
        floorSplats.Clear();
        drips.Clear();
    }

    // Walls are boxes scaled unevenly, which would skew a mark parented to them, so wall marks
    // go on the wall's parent (its room), and still go when the floor does.
    public static void WallHit(Vector3 point, Vector3 normal, Transform wall, Materials materials)
    {
        Ensure(materials);
        Burst(sparks, point, normal, 10, 3f, 8f, 0.1f, 0.25f, 0.08f, 0.14f, 70f,
            new Color(1f, 0.95f, 0.75f), new Color(1f, 0.55f, 0.15f));

        // A pillar drawn as a sprite has no surface to mark.
        if (wall != null && wall.GetComponentInParent<FurnitureSprite>() != null)
            return;

        GameObject mark = Mark(point, normal, wall != null ? wall.parent : null, WallScorch, Random.Range(0.2f, 0.3f));

        if (mark == null)
            return;

        wallMarks.Enqueue(mark);

        while (wallMarks.Count > MaxWallMarks)
        {
            GameObject oldest = wallMarks.Dequeue();

            if (oldest != null)
                Object.Destroy(oldest);
        }
    }

    // Just the shower of sparks, without a scorch mark (a bullet off a can or a gas can).
    public static void Sparks(Vector3 point, Vector3 normal, Materials materials)
    {
        Ensure(materials);
        Burst(sparks, point, normal, 8, 3f, 7f, 0.08f, 0.2f, 0.07f, 0.12f, 70f,
            new Color(1f, 0.95f, 0.75f), new Color(1f, 0.55f, 0.15f));
    }

    // Bits of material knocked off something (cardboard off a box): chips of the given colours
    // thrown out from the surface, falling and slowing. Uses the plain soft-dot material.
    public static void Debris(Vector3 point, Vector3 normal, int count, Color a, Color b, Materials materials)
    {
        Ensure(materials);

        if (debris == null)
            debris = MakeSystem("Impact Debris", materials.Blood, 3f, false);

        Burst(debris, point, normal, count, 1.5f, 4f, 0.35f, 0.7f, 0.05f, 0.11f, 65f, a, b);
    }

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

    // The materials an impact needs, carried by the bullet that made it. Sparks use their own,
    // tinted brighter than white so they bloom; blood is a plain soft dot.
    public struct Materials
    {
        public Material Sparks;
        public Material Blood;
        public Material Scorch;
    }

    // The materials from the last bullet that hit something, for effects that don't come from
    // a bullet (a grenade's sparks, a boss smashing a box). Before the first hit, a gun's bullet
    // lends them; false only if there's no gun about at all.
    public static bool TryGetMaterials(out Materials materials)
    {
        if (!haveKnown)
        {
            Gun gun = Object.FindAnyObjectByType<Gun>();
            Projectile bullet = gun != null && gun.ProjectilePrefab != null ? gun.ProjectilePrefab.GetComponent<Projectile>() : null;

            if (bullet != null)
            {
                Materials borrowed = bullet.EffectMaterials;

                if (borrowed.Sparks != null && borrowed.Blood != null)
                {
                    known = borrowed;
                    haveKnown = true;
                }
            }
        }

        materials = known;
        return haveKnown;
    }

    private static void Ensure(Materials materials)
    {
        if (materials.Sparks != null && materials.Blood != null)
        {
            known = materials;
            haveKnown = true;
        }

        if (markMaterial == null)
            markMaterial = materials.Scorch;

        EnsureQuad();

        // Hard-edged square particles with the sprite art.
        Material pixel = SpriteVisuals.Current != null ? SpriteVisuals.Current.particle : null;

        if (sparks == null)
            sparks = MakeSystem("Impact Sparks", pixel != null ? pixel : materials.Sparks, 1.6f, true);

        if (blood == null)
            blood = MakeSystem("Impact Blood", pixel != null ? pixel : materials.Blood, 4f, false);
    }

    // A world-space system that only ever emits on demand. Sparks are stretched along their
    // motion so they read as streaks; blood is round droplets that fall.
    private static ParticleSystem MakeSystem(string name, Material material, float gravity, bool stretched)
    {
        GameObject go = new GameObject(name);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        // Looping with emission off: it never stops (a stopped system ignores new bursts), and
        // only ever makes particles when a hit asks for them.
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;
        main.maxParticles = 600;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        // Fade and shrink out over each particle's life.
        ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
        colour.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(stretched ? 0.8f : 1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, stretched ? 0.3f : 0.6f)));

        // Blood soon loses its speed in the air, and dies on the floor rather than falling through
        // it; sparks keep going until they burn out.
        if (!stretched)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2f;

            Transform floor = new GameObject("Floor").transform;
            floor.SetParent(go.transform, false);
            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.SetPlane(0, floor);
            collision.bounce = 0f;
            collision.dampen = 1f;
            collision.lifetimeLoss = 1f;
        }

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (stretched)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 2f;
        }

        system.Play();
        return system;
    }

    // count particles thrown out from point, within spread degrees of direction.
    private static void Burst(ParticleSystem system, Vector3 point, Vector3 direction, int count,
        float minSpeed, float maxSpeed, float minLife, float maxLife, float minSize, float maxSize,
        float spread, Color a, Color b)
    {
        if (system == null)
            return;

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.up;

        direction.Normalize();
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();

        for (int i = 0; i < count; i++)
        {
            // A random direction within the cone, tipped up a little so it arcs.
            Vector3 dir = Quaternion.AngleAxis(Random.Range(0f, 360f), direction)
                * Quaternion.AngleAxis(Random.Range(0f, spread), Perpendicular(direction)) * direction;
            dir = (dir + Vector3.up * 0.25f).normalized;

            emit.position = point;
            emit.velocity = dir * Random.Range(minSpeed, maxSpeed);
            emit.startLifetime = Random.Range(minLife, maxLife);
            emit.startSize = Random.Range(minSize, maxSize);
            emit.startColor = Color.Lerp(a, b, Random.value);
            system.Emit(emit, 1);
        }
    }

    private static Vector3 Perpendicular(Vector3 v)
    {
        Vector3 other = Mathf.Abs(Vector3.Dot(v, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
        return Vector3.Cross(v, other).normalized;
    }

    // A small soot mark flat on the surface, just off it to avoid flicker, randomly turned.
    private static GameObject Mark(Vector3 point, Vector3 normal, Transform surface, Color colour, float size)
    {
        if (markMaterial == null || quad == null || normal.sqrMagnitude < 0.0001f)
            return null;

        GameObject mark = new GameObject("Scorch");
        mark.transform.SetPositionAndRotation(point + normal.normalized * 0.006f,
            Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
        mark.transform.localScale = Vector3.one * size;

        if (surface != null)
            mark.transform.SetParent(surface, true);

        mark.AddComponent<MeshFilter>().sharedMesh = quad;
        MeshRenderer renderer = mark.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = markMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        markBlock ??= new MaterialPropertyBlock();
        markBlock.SetColor("_Color", colour);
        renderer.SetPropertyBlock(markBlock);
        return mark;
    }
}
