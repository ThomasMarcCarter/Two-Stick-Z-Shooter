using UnityEngine;

// What a melee swing looks like: a pale arc sweeping across in front of the player, out at the
// swing's reach, then fading. Left where the swing happened rather than following the player,
// so a turn mid-swing doesn't drag it round.
public class MeleeSwing : MonoBehaviour
{
    private const float SweepTime = 0.1f;
    private const float FadeTime = 0.14f;
    private const int Points = 18;
    private static readonly Color ArcColour = new Color(0.85f, 1f, 0.9f) * 1.6f;
    private static Material material;

    private LineRenderer line;
    private float started;
    private float reach;
    private float arc;
    private Vector3 forward;

    public static void Show(Vector3 position, Vector3 forward, float reach, float arc)
    {
        if (material == null)
            material = new Material(Shader.Find("Sprites/Default"));

        GameObject go = new GameObject("Melee Swing");
        go.transform.position = new Vector3(position.x, 0.6f, position.z);

        MeleeSwing swing = go.AddComponent<MeleeSwing>();
        swing.started = Time.time;
        swing.reach = reach;
        swing.arc = arc;
        swing.forward = new Vector3(forward.x, 0f, forward.z).normalized;

        swing.line = go.AddComponent<LineRenderer>();
        swing.line.sharedMaterial = material;
        swing.line.useWorldSpace = true;
        swing.line.positionCount = Points;
        swing.line.numCapVertices = 2;
        swing.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        swing.line.receiveShadows = false;

        // Thick at the leading edge, trailing off behind it.
        swing.line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.02f), new Keyframe(1f, 0.16f));
        swing.Update();
    }

    private void Update()
    {
        float age = Time.time - started;

        if (age >= SweepTime + FadeTime)
        {
            Destroy(gameObject);
            return;
        }

        // Sweeps from one side to the other (right to left, like a right-handed swing).
        float sweep = Mathf.Clamp01(age / SweepTime);
        float from = arc / 2f;
        float to = Mathf.Lerp(arc / 2f, -arc / 2f, sweep);

        for (int i = 0; i < Points; i++)
        {
            float angle = Mathf.Lerp(from, to, i / (Points - 1f));
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * forward;
            line.SetPosition(i, transform.position + direction * reach * 0.9f);
        }

        float alpha = 1f - Mathf.Clamp01((age - SweepTime) / FadeTime);
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(ArcColour, 0f), new GradientColorKey(ArcColour, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f * alpha, 1f) });
        line.colorGradient = gradient;
    }
}
