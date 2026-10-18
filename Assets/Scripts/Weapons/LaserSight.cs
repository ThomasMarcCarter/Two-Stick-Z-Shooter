using UnityEngine;

// The beam of a laser sight attachment: a thin red line from the muzzle along the gun's aim to
// the first thing a shot would hit (found at Gun.ShotHeight, where shots travel; the beam itself
// is drawn level from the muzzle), with a brighter dot where it lands. Bright enough to bloom, so it
// shows the aim in the dark. A child of the muzzle, so it holsters with the gun.
public class LaserSight : MonoBehaviour
{
    private const float Range = 25f;
    private static readonly Color BeamColour = new Color(1f, 0.08f, 0.08f) * 2.5f;
    private static Material material;

    private Transform gun;
    private LineRenderer line;
    private Transform dot;

    public static LaserSight Create(Transform gun, Transform muzzle)
    {
        GameObject go = new GameObject("Laser Sight");
        go.transform.SetParent(muzzle, false);

        LaserSight laser = go.AddComponent<LaserSight>();
        laser.gun = gun;
        laser.Build();
        return laser;
    }

    private void Build()
    {
        if (material == null)
            material = new Material(Shader.Find("Sprites/Default"));

        line = gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = 0.018f;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        // Strong at the gun, fading towards the far end.
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(BeamColour, 0f), new GradientColorKey(BeamColour, 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.15f, 1f) });
        line.colorGradient = gradient;

        GameObject dotObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        dotObject.name = "Dot";
        Destroy(dotObject.GetComponent<Collider>());
        dotObject.transform.SetParent(transform, false);
        dotObject.transform.localScale = Vector3.one * 0.09f;

        Renderer dotRenderer = dotObject.GetComponent<Renderer>();
        dotRenderer.sharedMaterial = material;
        dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dotRenderer.receiveShadows = false;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_Color", BeamColour * 1.5f);
        dotRenderer.SetPropertyBlock(block);
        dot = dotObject.transform;
    }

    // After the player has turned this frame, so the beam doesn't trail behind the aim.
    private void LateUpdate()
    {
        Vector3 start = transform.position;
        Vector3 probe = new Vector3(start.x, Gun.ShotHeight, start.z);
        Vector3 direction = gun.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        direction.Normalize();
        Vector3 end = start + direction * Range;
        Vector3 normal = -direction;
        float nearest = Range;

        foreach (RaycastHit hit in Physics.RaycastAll(probe, direction, Range, ~0, QueryTriggerInteraction.Ignore))
        {
            // Not the shooter's own body.
            if (hit.collider.transform.root == gun.root || hit.distance >= nearest)
                continue;

            nearest = hit.distance;
            end = start + direction * hit.distance;
            normal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;

            if (normal.sqrMagnitude < 0.01f)
                normal = -direction;
        }

        line.SetPosition(0, start);
        line.SetPosition(1, end);

        bool landed = nearest < Range;
        dot.gameObject.SetActive(landed);

        if (landed)
        {
            dot.position = end + normal * 0.02f;
            dot.rotation = Quaternion.LookRotation(-normal);
        }
    }
}
