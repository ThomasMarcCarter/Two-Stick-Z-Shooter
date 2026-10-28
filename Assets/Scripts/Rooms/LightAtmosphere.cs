using UnityEngine;

// The air under a room's ceiling lights (2026-10-27): dust motes drifting slowly through each
// tube's pool of light, and a shower of sparks from a worn-out tube when it stutters
// (FluorescentFlicker). Both are pixel particles (SpriteVisuals.particle); without that material
// nothing is added. FloorGenerator dresses each ceiling light as it places it.
public static class LightAtmosphere
{
    private static readonly Color DustColour = new Color(1f, 0.95f, 0.8f, 0.14f);
    private static readonly Color SparkHot = new Color(1f, 0.95f, 0.7f, 1f);
    private static readonly Color SparkCool = new Color(1f, 0.55f, 0.15f, 1f);

    public static void Dress(Light tube, float height, float poolWidth)
    {
        Material pixel = SpriteVisuals.Current != null ? SpriteVisuals.Current.particle : null;

        if (pixel == null)
            return;

        AddDust(tube.transform, pixel, height, poolWidth);

        if (tube.TryGetComponent(out FluorescentFlicker flicker))
            flicker.Sparks = AddSparks(tube.transform, pixel);
    }

    // A slow trickle of motes in a box under the light, wandering and fading in and out.
    private static void AddDust(Transform light, Material pixel, float height, float width)
    {
        ParticleSystem dust = NewSystem("Dust", light, pixel);
        // Halfway down the light's height, in the world's axes (the light points down).
        dust.transform.position = light.position + Vector3.down * height * 0.5f;
        dust.transform.rotation = Quaternion.identity;

        ParticleSystem.MainModule main = dust.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
        main.startColor = DustColour;
        main.maxParticles = 40;
        main.prewarm = true;

        ParticleSystem.EmissionModule emission = dust.emission;
        emission.rateOverTime = 2f;

        ParticleSystem.ShapeModule shape = dust.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width * 0.7f, height * 0.8f, width * 0.7f);

        ParticleSystem.NoiseModule noise = dust.noise;
        noise.enabled = true;
        noise.strength = 0.08f;
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.1f;

        ParticleSystem.ColorOverLifetimeModule colour = dust.colorOverLifetime;
        colour.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;

        dust.Play();
    }

    // Emitted on demand: a burst falling out of the tube, bouncing once off the floor.
    private static ParticleSystem AddSparks(Transform light, Material pixel)
    {
        return Sparks(light, pixel, new Vector3(light.position.x, 0f, light.position.z));
    }

    // A shower of sparks to Emit from `parent` (out along its forward, falling), bouncing on the
    // floor; null without the pixel particle material. Also the sparking cables (SparkingCable).
    public static ParticleSystem Sparks(Transform parent)
    {
        Material pixel = SpriteVisuals.Current != null ? SpriteVisuals.Current.particle : null;
        return pixel != null ? Sparks(parent, pixel, new Vector3(parent.position.x, 0f, parent.position.z)) : null;
    }

    private static ParticleSystem Sparks(Transform light, Material pixel, Vector3 floorPoint)
    {
        ParticleSystem sparks = NewSystem("Sparks", light, pixel);

        ParticleSystem.MainModule main = sparks.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(SparkHot, SparkCool);
        main.gravityModifier = 1.2f;
        main.maxParticles = 60;

        ParticleSystem.EmissionModule emission = sparks.emission;
        emission.enabled = false;

        // Out of the tube's face, spreading downward (the light's forward is down).
        ParticleSystem.ShapeModule shape = sparks.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 55f;
        shape.radius = 0.3f;

        Transform floor = new GameObject("Floor").transform;
        floor.SetParent(sparks.transform, false);
        floor.position = floorPoint;
        floor.rotation = Quaternion.identity;
        ParticleSystem.CollisionModule collision = sparks.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.Planes;
        collision.SetPlane(0, floor);
        collision.bounce = 0.3f;
        collision.dampen = 0.5f;

        ParticleSystem.ColorOverLifetimeModule colour = sparks.colorOverLifetime;
        colour.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.2f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;

        sparks.Play();
        return sparks;
    }

    private static ParticleSystem NewSystem(string name, Transform parent, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }
}
