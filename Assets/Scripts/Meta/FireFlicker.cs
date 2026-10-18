using UnityEngine;

// Makes a light flicker like a fire: the hideout's burning barrel. Intensity and colour wander
// with noise around their starting values, and the flame block (if any) pulses with it.
public class FireFlicker : MonoBehaviour
{
    private Light fire;
    private Renderer flame;
    private float baseIntensity;
    private float seed;

    public static FireFlicker Add(Light light, Renderer flameRenderer)
    {
        FireFlicker flicker = light.gameObject.AddComponent<FireFlicker>();
        flicker.fire = light;
        flicker.flame = flameRenderer;
        flicker.baseIntensity = light.intensity;
        flicker.seed = Random.value * 100f;
        return flicker;
    }

    private void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * 6f);
        float m = Mathf.PerlinNoise(seed + 3f, Time.time * 13f);
        fire.intensity = baseIntensity * (0.7f + 0.45f * n + 0.15f * m);
        fire.color = Color.Lerp(new Color(1f, 0.45f, 0.15f), new Color(1f, 0.7f, 0.3f), m);

        if (flame != null)
        {
            ConsumableModel.Tint(flame, new Color(1f, 0.5f, 0.15f), new Color(1f, 0.45f, 0.1f) * (1.5f + 2f * n));
            flame.transform.localScale = new Vector3(0.5f, 0.12f + 0.08f * m, 0.5f);
        }
    }
}
