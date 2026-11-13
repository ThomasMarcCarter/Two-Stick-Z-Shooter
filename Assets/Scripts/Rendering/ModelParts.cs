using UnityEngine;

// Shared builders for the little models made in code from Unity primitives (pickups, crates,
// stations, the training dummy...): one collider-free primitive part tinted through a property
// block, and the tint itself (base colour + emission on the URP Lit material) so many parts can
// share one material.
public static class ModelParts
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // A `shape` child of `parent` at a local position/size/rotation, without a collider. With a
    // material it is tinted `colour` glowing `emission`. `name` null keeps the primitive's name.
    public static Renderer Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 size,
        Material material, Color colour, Color emission, Quaternion? rotation = null, bool shadows = false)
    {
        GameObject part = GameObject.CreatePrimitive(shape);

        if (name != null)
            part.name = name;

        Object.Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation ?? Quaternion.identity;
        part.transform.localScale = size;

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

        if (material != null)
        {
            renderer.sharedMaterial = material;
            Tint(renderer, colour, emission);
        }

        return renderer;
    }

    // Sets a renderer's base colour and emission without making a new material.
    public static void Tint(Renderer renderer, Color baseColour, Color emission)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, baseColour);
        block.SetColor(EmissionColorId, emission);
        renderer.SetPropertyBlock(block);
    }
}
