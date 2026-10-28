using UnityEngine;

// A few flies circling something rotten (room life, 2026-10-28): a bloodied or stripped bed, a
// body bag on a gurney. Tiny dark pixel particles dart about in a small cloud just under a
// metre above it. Only an effect. Added by FloorGenerator.RoomLife.cs.
public static class FlySwarm
{
    private static readonly Color Fly = new Color(0.08f, 0.08f, 0.08f, 1f);

    // Null without the pixel particle material.
    public static ParticleSystem Create(Transform parent, Vector3 over)
    {
        Material pixel = SpriteVisuals.Current != null ? SpriteVisuals.Current.particle : null;

        if (pixel == null)
            return null;

        GameObject go = new GameObject("Flies");
        go.transform.SetParent(parent, false);
        go.transform.position = over + Vector3.up * 0.9f;
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
        main.startColor = Fly;
        main.maxParticles = 10;
        main.prewarm = true;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 1.6f;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;

        // Darting about: strong, fast noise in a small space.
        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = 1.4f;
        noise.frequency = 2.5f;
        noise.scrollSpeed = 1.5f;
        noise.damping = true;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = pixel;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        system.Play();
        return system;
    }
}
