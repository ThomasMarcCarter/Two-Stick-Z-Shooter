using UnityEngine;

// A gun's muzzle flash: a burst of warm light that lights up the room (and the player) for a
// moment, and a flat flame sprite out of the barrel, visible from the top-down camera. Lasts a
// frame or three per shot, each one rolled and sized a little differently. Gun creates one on
// its muzzle and calls Fire for every shot.
public class MuzzleFlash : MonoBehaviour
{
    private const float Duration = 0.05f;

    private static readonly Color LightColour = new Color(1f, 0.72f, 0.38f);

    private Light flashLight;
    private Transform flame;
    private float size = 1f;
    private float hideAt;

    // Builds the flash as a child of the muzzle, hidden until the first shot. size scales the
    // flame and the light's reach (bigger for a shotgun, smaller for a pistol).
    public static MuzzleFlash Create(Transform muzzle, Material material, float size)
    {
        GameObject root = new GameObject("Muzzle Flash");
        root.transform.SetParent(muzzle, false);

        MuzzleFlash flash = root.AddComponent<MuzzleFlash>();
        flash.size = size;

        flash.flashLight = root.AddComponent<Light>();
        flash.flashLight.type = LightType.Point;
        flash.flashLight.color = LightColour;
        flash.flashLight.range = 4.5f * size;
        flash.flashLight.intensity = 8f;
        flash.flashLight.shadows = LightShadows.None;

        // The world and the player (rendering layers 1 and 2): a shot lights up whoever fired it.
        UnityEngine.Rendering.Universal.UniversalAdditionalLightData data =
            root.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
        data.renderingLayers = 3u;

        // Flat quad lying in the floor plane, pointing out of the barrel.
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Flame";
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(root.transform, false);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        flash.flame = quad.transform;

        root.SetActive(false);
        return flash;
    }

    public void Fire()
    {
        gameObject.SetActive(true);
        hideAt = Time.time + Duration;

        // A different shape every shot: spun about the barrel, and stretched by a random amount.
        float length = size * Random.Range(0.45f, 0.7f);
        float width = size * Random.Range(0.28f, 0.4f);
        flame.localScale = new Vector3(width, length, 1f);
        flame.localPosition = new Vector3(0f, 0f, length * 0.42f);
        flame.localRotation = Quaternion.Euler(90f, 0f, Random.Range(-12f, 12f));
        flashLight.intensity = Random.Range(6f, 10f);
    }

    private void Update()
    {
        if (Time.time >= hideAt)
            gameObject.SetActive(false);
    }

    // Holstered or dropped mid-flash: don't leave it lit.
    private void OnDisable()
    {
        hideAt = 0f;
    }
}
