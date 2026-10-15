using UnityEngine;

// Fake bounce lighting for the flashlight. URP has no realtime global illumination for moving
// lights, so when the beam hits something close (a wall, an enemy) a soft light is placed just
// off that surface, tinted by its colour, and lights the player model. The closer the surface,
// the brighter the spill.
[RequireComponent(typeof(Light))]
public class FlashlightBounce : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Light bounceLight;

    [Header("Probe")]
    [SerializeField] private float maxDistance = 4f;
    [Tooltip("World height of the probe. Low enough to hit enemies, high enough to miss the floor.")]
    [SerializeField] private float probeHeight = 0.5f;
    [SerializeField] private float probeRadius = 0.25f;
    [SerializeField] private LayerMask surfaces = ~0;

    [Header("Light")]
    [SerializeField] private float maxIntensity = 1.8f;
    [Tooltip("How far off the surface the bounce light sits, towards the player.")]
    [SerializeField] private float surfaceOffset = 0.4f;
    [Tooltip("World height of the bounce light. Above the player so it reaches the faces the top-down camera sees.")]
    [SerializeField] private float lightHeight = 2.4f;
    [Tooltip("0 = white bounce, 1 = fully the surface colour.")]
    [SerializeField, Range(0f, 1f)] private float surfaceTint = 0.7f;
    [SerializeField] private float smoothing = 12f;

    private Light flashlight;
    private float currentIntensity;
    private Color currentColour = Color.white;

    private void Awake()
    {
        flashlight = GetComponent<Light>();
    }

    private void LateUpdate()
    {
        if (bounceLight == null)
            return;

        float targetIntensity = 0f;
        Color targetColour = currentColour;

        if (flashlight.enabled && TryProbe(out RaycastHit hit))
        {
            float closeness = 1f - hit.distance / maxDistance;

            targetIntensity = maxIntensity * closeness * closeness;
            targetColour = flashlight.color * SurfaceColour(hit.collider);

            Vector3 position = hit.point + hit.normal * surfaceOffset;
            position.y = lightHeight;
            bounceLight.transform.position = position;
            bounceLight.range = hit.distance + 3f;
        }

        // Ease in and out so it doesn't pop as walls come in and out of range.
        float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, blend);
        currentColour = Color.Lerp(currentColour, targetColour, blend);

        bounceLight.intensity = currentIntensity;
        bounceLight.color = currentColour;
        bounceLight.enabled = currentIntensity > 0.01f;
    }

    private bool TryProbe(out RaycastHit hit)
    {
        Vector3 direction = transform.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            hit = default;
            return false;
        }

        Vector3 origin = new Vector3(transform.position.x, probeHeight, transform.position.z);

        return Physics.SphereCast(origin, probeRadius, direction.normalized, out hit, maxDistance,
            surfaces, QueryTriggerInteraction.Ignore);
    }

    private Color SurfaceColour(Collider surface)
    {
        Renderer surfaceRenderer = surface.GetComponent<Renderer>();

        if (surfaceRenderer == null || surfaceRenderer.sharedMaterial == null
            || !surfaceRenderer.sharedMaterial.HasProperty(BaseColorId))
        {
            return Color.white;
        }

        Color colour = Color.Lerp(Color.white, surfaceRenderer.sharedMaterial.GetColor(BaseColorId), surfaceTint);
        colour.a = 1f;
        return colour;
    }
}
