using UnityEngine;
using UnityEngine.Rendering.Universal;

// Turns the CRT fullscreen renderer feature on while this component is enabled
// (i.e. in Play mode) and back off afterwards, so the editor view stays clean.
// The settings below drive the CRT material and can be tweaked live in Play mode.
public class CRTEffect : MonoBehaviour
{
    private static readonly int CurvatureId = Shader.PropertyToID("_Curvature");
    private static readonly int ScanlineStrengthId = Shader.PropertyToID("_ScanlineStrength");
    private static readonly int ScanlineCountId = Shader.PropertyToID("_ScanlineCount");
    private static readonly int ScanlineSpeedId = Shader.PropertyToID("_ScanlineSpeed");
    private static readonly int ChromaticAberrationId = Shader.PropertyToID("_ChromaticAberration");
    private static readonly int VignetteStrengthId = Shader.PropertyToID("_VignetteStrength");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int NoiseId = Shader.PropertyToID("_Noise");

    [SerializeField] private FullScreenPassRendererFeature crtFeature;

    [Header("Screen")]
    [SerializeField, Range(0f, 0.5f)] private float curvature = 0.07f;
    [SerializeField, Range(0f, 2f)] private float vignetteStrength = 0.6f;
    [SerializeField, Range(0.5f, 2f)] private float brightness = 1.15f;

    [Header("Scanlines")]
    [SerializeField, Range(0f, 1f)] private float scanlineStrength = 0.25f;
    [SerializeField, Range(50f, 1000f)] private float scanlineCount = 360f;
    [SerializeField, Range(-5f, 5f)] private float scanlineSpeed = 0.5f;

    [Header("Signal")]
    [SerializeField, Range(0f, 0.01f)] private float chromaticAberration = 0.0006f;
    [SerializeField, Range(0f, 0.2f)] private float noise = 0.015f;

    [Tooltip("Keeps mouse-driven UI this far (0-1 of the screen) inside the curved edge.")]
    [SerializeField] private float edgeMargin = 0.01f;

    private static CRTEffect instance;

    // From the settings screen: Full uses the values above, Light softens the scanlines, noise,
    // fringing and curve, Off switches the pass off (and the pointer stops being remapped).
    private static GameSettings.CrtLevel level = GameSettings.CrtLevel.Full;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        instance = null;
        level = GameSettings.CrtLevel.Full;
    }

    public static void SetLevel(GameSettings.CrtLevel newLevel)
    {
        level = newLevel;

        if (instance != null)
            instance.Refresh();
    }

    private float Strength => level == GameSettings.CrtLevel.Light ? 0.4f : 1f;

    private void OnEnable()
    {
        instance = this;
        Refresh();
    }

    private void Refresh()
    {
        ApplySettings();

        if (crtFeature != null)
        {
            crtFeature.SetActive(level != GameSettings.CrtLevel.Off);
        }
    }

    private void OnDisable()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (crtFeature != null)
        {
            crtFeature.SetActive(false);
        }
    }

    // Called whenever a value changes in the Inspector, including during Play mode.
    private void OnValidate()
    {
        ApplySettings();
    }

    private void ApplySettings()
    {
        if (crtFeature == null || crtFeature.passMaterial == null)
            return;

        Material material = crtFeature.passMaterial;
        float strength = Strength;
        material.SetFloat(CurvatureId, curvature * strength);
        material.SetFloat(ScanlineStrengthId, scanlineStrength * strength);
        material.SetFloat(ScanlineCountId, scanlineCount);
        material.SetFloat(ScanlineSpeedId, scanlineSpeed);
        material.SetFloat(ChromaticAberrationId, chromaticAberration * strength);
        material.SetFloat(VignetteStrengthId, vignetteStrength * Mathf.Lerp(0.6f, 1f, strength));
        material.SetFloat(BrightnessId, brightness);
        material.SetFloat(NoiseId, noise * strength);
    }

    // Maps a point on the physical screen (e.g. Input.mousePosition) to the point in the
    // undistorted frame that the CRT shader displays there. Use it for anything that has
    // to line up with the cursor: UI that follows the mouse, and aiming raycasts.
    // Mirrors the barrel distortion in CRTShader.shader.
    public static Vector2 ScreenToSource(Vector2 screenPoint)
    {
        if (instance == null || instance.crtFeature == null || !instance.crtFeature.isActive)
        {
            return screenPoint;
        }

        Vector2 screenSize = new Vector2(Screen.width, Screen.height);

        Vector2 centered = screenPoint / screenSize - new Vector2(0.5f, 0.5f);
        Vector2 uv = centered * (1f + Vector2.Dot(centered, centered) * instance.curvature * instance.Strength * 4f)
            + new Vector2(0.5f, 0.5f);

        // Outside 0-1 is the black bezel; keep the point on the visible screen.
        float margin = instance.edgeMargin;
        uv.x = Mathf.Clamp(uv.x, margin, 1f - margin);
        uv.y = Mathf.Clamp(uv.y, margin, 1f - margin);

        return Vector2.Scale(uv, screenSize);
    }
}
