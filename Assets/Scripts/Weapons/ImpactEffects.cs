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
public static partial class ImpactEffects
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
