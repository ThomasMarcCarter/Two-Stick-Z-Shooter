using UnityEngine;

// A glob of acid in flight: an arc from the Broodmother to where the player was heading,
// leaving an AcidPool where it lands.
public class AcidGlob : MonoBehaviour
{
    private const float FlightTime = 0.85f;
    private const float ArcHeight = 3f;

    private Vector3 from;
    private Vector3 to;
    private float launched;
    private Transform room;

    public static void Launch(Vector3 from, Vector3 to, Transform room)
    {
        GameObject glob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        glob.name = "Acid Glob";
        Destroy(glob.GetComponent<Collider>());
        glob.transform.localScale = Vector3.one * 0.35f;

        Renderer renderer = glob.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (ConsumablePickup.DefaultMaterial != null)
        {
            renderer.sharedMaterial = ConsumablePickup.DefaultMaterial;
            ConsumableModel.Tint(renderer, new Color(0.3f, 0.6f, 0.05f), new Color(0.55f, 1f, 0.15f) * 3f);
        }

        AcidGlob acid = glob.AddComponent<AcidGlob>();
        acid.from = from;
        acid.to = new Vector3(to.x, 0f, to.z);
        acid.launched = Time.time;
        acid.room = room;
        acid.Update();
    }

    private void Update()
    {
        float t = (Time.time - launched) / FlightTime;

        if (t >= 1f)
        {
            AcidPool.Create(to, room);
            Destroy(gameObject);
            return;
        }

        Vector3 position = Vector3.Lerp(from, to, t);
        position.y = Mathf.Lerp(from.y, 0.2f, t) + Mathf.Sin(t * Mathf.PI) * ArcHeight;
        transform.position = position;
    }
}
