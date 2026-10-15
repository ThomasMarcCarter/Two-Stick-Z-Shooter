using UnityEngine;

// The little models for consumables, built from primitives: on the floor as pickups, and in
// flight when thrown. Each glows a bit in its colour so it reads in the dark. Pivot at the
// bottom middle; size 1 is pickup size.
public static class ConsumableModel
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public static Transform Build(ConsumableType type, Transform parent, Material material, float scale = 1f)
    {
        GameObject root = new GameObject("Model");
        root.transform.SetParent(parent, false);
        root.transform.localScale = Vector3.one * scale;
        Transform t = root.transform;
        Color colour = ConsumableInfo.Colour(type);

        switch (type)
        {
            case ConsumableType.Medkit:
            case ConsumableType.LargeMedkit:
            {
                bool large = type == ConsumableType.LargeMedkit;
                Vector3 size = large ? new Vector3(0.7f, 0.3f, 0.55f) : new Vector3(0.45f, 0.22f, 0.36f);
                Part(t, PrimitiveType.Cube, new Vector3(0f, size.y / 2f, 0f), size, material, new Color(0.75f, 0.75f, 0.72f), new Color(0.35f, 0.35f, 0.33f));

                // Cross on the lid.
                float top = size.y + 0.01f;
                float arm = Mathf.Min(size.x, size.z) * 0.7f;
                float width = arm * 0.3f;
                Part(t, PrimitiveType.Cube, new Vector3(0f, top, 0f), new Vector3(arm, 0.02f, width), material, colour, colour * 4f);
                Part(t, PrimitiveType.Cube, new Vector3(0f, top, 0f), new Vector3(width, 0.02f, arm), material, colour, colour * 4f);
                break;
            }

            case ConsumableType.Adrenaline:
            {
                // A syringe lying down: a clear barrel of glowing liquid, plunger and needle.
                Quaternion lying = Quaternion.Euler(0f, 0f, 90f);
                Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.08f, 0.16f, 0.08f), material, colour * 0.6f, colour * 4f, lying);
                Part(t, PrimitiveType.Cylinder, new Vector3(-0.2f, 0.06f, 0f), new Vector3(0.03f, 0.06f, 0.03f), material, new Color(0.8f, 0.8f, 0.8f), Color.black, lying);
                Part(t, PrimitiveType.Cube, new Vector3(-0.26f, 0.06f, 0f), new Vector3(0.02f, 0.1f, 0.1f), material, new Color(0.8f, 0.8f, 0.8f), Color.black);
                Part(t, PrimitiveType.Cylinder, new Vector3(0.22f, 0.06f, 0f), new Vector3(0.01f, 0.07f, 0.01f), material, new Color(0.9f, 0.9f, 0.9f), new Color(0.3f, 0.3f, 0.3f), lying);
                break;
            }

            case ConsumableType.Grenade:
            {
                Part(t, PrimitiveType.Sphere, new Vector3(0f, 0.11f, 0f), new Vector3(0.18f, 0.22f, 0.18f), material, new Color(0.22f, 0.28f, 0.16f), colour * 0.6f);
                Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.24f, 0f), new Vector3(0.07f, 0.03f, 0.07f), material, new Color(0.5f, 0.5f, 0.45f), Color.black);
                Part(t, PrimitiveType.Cube, new Vector3(0.05f, 0.2f, 0f), new Vector3(0.03f, 0.1f, 0.05f), material, new Color(0.5f, 0.5f, 0.45f), Color.black, Quaternion.Euler(0f, 0f, -15f));
                // The pin's ring, glowing, so it reads in the dark.
                Part(t, PrimitiveType.Cylinder, new Vector3(-0.06f, 0.25f, 0f), new Vector3(0.05f, 0.005f, 0.05f), material, colour, colour * 4f, Quaternion.Euler(90f, 0f, 0f));
                break;
            }

            case ConsumableType.Molotov:
            {
                Color glass = new Color(0.45f, 0.3f, 0.1f);
                Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.14f, 0.12f, 0.14f), material, glass, colour * 1.2f);
                Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.06f, 0.06f, 0.06f), material, glass, colour * 0.8f);
                // The rag.
                Part(t, PrimitiveType.Cube, new Vector3(0f, 0.38f, 0f), new Vector3(0.05f, 0.08f, 0.05f), material, new Color(0.8f, 0.75f, 0.6f), colour * 3f, Quaternion.Euler(0f, 0f, 20f));
                break;
            }

            default:
            {
                Color glass = colour * 0.35f;
                Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.13f, 0.12f, 0.13f), material, glass, colour * 1.5f);
                Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.31f, 0f), new Vector3(0.05f, 0.07f, 0.05f), material, glass, colour * 1.2f);
                break;
            }
        }

        return t;
    }

    private static void Part(Transform parent, PrimitiveType shape, Vector3 localPosition, Vector3 size, Material material,
        Color baseColour, Color emission, Quaternion? rotation = null)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = shape.ToString();
        Object.Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = rotation ?? Quaternion.identity;
        part.transform.localScale = size;

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (material == null)
            return;

        renderer.sharedMaterial = material;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, baseColour);
        block.SetColor(EmissionColorId, emission);
        renderer.SetPropertyBlock(block);
    }

    // Lit and emissive in a colour, for the effects (blasts, fire) built from the same material.
    public static void Tint(Renderer renderer, Color baseColour, Color emission)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, baseColour);
        block.SetColor(EmissionColorId, emission);
        renderer.SetPropertyBlock(block);
    }
}
