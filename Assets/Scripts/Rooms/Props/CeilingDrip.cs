using UnityEngine;

// Water dripping from the ceiling (room life, 2026-10-28): every second or two a drop falls the
// ceiling's height onto the puddle below, splashes and plinks (ProceduralAudio.Drip). Pixel
// particles only; placed by FloorGenerator.RoomLife.cs over a puddle floor decal.
public class CeilingDrip : MonoBehaviour
{
    private const float Height = 2.6f;
    private static readonly Color Water = new Color(0.6f, 0.75f, 0.85f, 0.85f);

    private ParticleSystem drops;
    private ParticleSystem splash;
    private float next;
    private float landAt = -1f;

    // `floor` is the point on the floor it drips onto.
    public static CeilingDrip Create(Transform parent, Vector3 floor)
    {
        Material pixel = SpriteVisuals.Current != null ? SpriteVisuals.Current.particle : null;

        if (pixel == null)
            return null;

        GameObject root = new GameObject("Ceiling Drip");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(floor.x, 0f, floor.z);

        CeilingDrip drip = root.AddComponent<CeilingDrip>();
        drip.drops = NewSystem("Drops", root.transform, pixel, Vector3.up * Height, 0.05f);
        ParticleSystem.MainModule main = drip.drops.main;
        main.startLifetime = Mathf.Sqrt(2f * Height / 9.81f);
        main.startSpeed = 0f;
        main.gravityModifier = 1f;

        drip.splash = NewSystem("Splash", root.transform, pixel, Vector3.up * 0.02f, 0.03f);
        main = drip.splash.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.gravityModifier = 0.8f;
        ParticleSystem.ShapeModule shape = drip.splash.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 50f;
        shape.radius = 0.02f;
        drip.splash.transform.rotation = Quaternion.LookRotation(Vector3.up);

        drip.next = Time.time + Random.Range(0.2f, 2f);
        return drip;
    }

    private static ParticleSystem NewSystem(string name, Transform parent, Material pixel, Vector3 local, float size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSize = size;
        main.startColor = Water;
        main.maxParticles = 20;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = pixel;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        system.Play();
        return system;
    }

    private void Update()
    {
        if (landAt > 0f && Time.time >= landAt)
        {
            landAt = -1f;
            splash.Emit(Random.Range(3, 6));
            SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("water.drip", 3, ProceduralAudio.Drip)), transform.position, 0.12f, Random.Range(0.9f, 1.1f));
        }

        if (Time.time < next)
            return;

        drops.Emit(1);
        landAt = Time.time + drops.main.startLifetime.constant;
        next = Time.time + Random.Range(0.9f, 2.8f);
    }
}
