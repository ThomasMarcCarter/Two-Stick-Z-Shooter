using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's furniture helpers, shared by the hideout, room themes, the supply room
// and the roof: a piece of furniture from FurnitureVisuals (a sprite or a model, sized and
// turned to face a way), with a coloured block standing in when there's no art for it.
public partial class FloorGenerator
{
    // A size given in a facing's own axes, as a world-aligned box size (facings are always square
    // to the grid here).
    private static Vector3 RotateSize(Quaternion facing, Vector3 size)
    {
        Vector3 rotated = facing * size;
        return new Vector3(Mathf.Abs(rotated.x), Mathf.Abs(rotated.y), Mathf.Abs(rotated.z));
    }

    // A piece of furniture: its model if there is one (see PropModel), else a tinted block the
    // same size. `position` is the floor point under its centre, `size` is in its own axes
    // (width, height, depth) and `front` is the way it faces.
    private GameObject Furniture(string model, string name, Transform parent, Vector3 position, Vector3 front, Vector3 size,
        Color colour, bool solid = true, PrimitiveType shape = PrimitiveType.Cube)
    {
        GameObject prop = PropModel(model, name, parent, position, front, size, solid);

        if (prop != null)
            return prop;

        return HideoutProp(name, parent, position + Vector3.up * size.y / 2f, RotateSize(Quaternion.LookRotation(front), size),
            colour, Color.black, solid, shape);
    }

    // A model for it, or (with the sprite art) a sprite: sprite-only pieces (the Shopping Mall's)
    // cast their shadow from a plain box instead.
    private bool HasPropModel(string model)
    {
        return (furnitureVisuals != null && furnitureVisuals.ModelFor(model) != null) || FurnitureSprite.Has(model);
    }

    // A furniture model stood on the floor at `position`, facing `front`, stretched to fit `size`
    // (its own width, height, depth) so it takes the same space the block did; solid ones get a
    // box collider that size (the navmesh is built from colliders), and with the sprite art it's
    // drawn as its sprite. Null without a model.
    private GameObject PropModel(string model, string name, Transform parent, Vector3 position, Vector3 front, Vector3 size,
        bool solid = true)
    {
        GameObject prefab = furnitureVisuals != null ? furnitureVisuals.ModelFor(model) : null;

        if (prefab == null && !FurnitureSprite.Has(model))
            return null;

        // Built at the world origin first, so the model's bounds are in the root's own space. A
        // sprite-only piece gets a plain box the size of its footprint to cast its shadow.
        GameObject root = new GameObject(name);
        GameObject body;

        if (prefab != null)
        {
            body = Instantiate(prefab, root.transform);
        }
        else
        {
            body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up * 0.5f;
        }

        body.name = "Model";

        Renderer[] renderers = body.GetComponentsInChildren<Renderer>();

        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Vector3 fit = new Vector3(size.x / Mathf.Max(bounds.size.x, 0.001f), size.y / Mathf.Max(bounds.size.y, 0.001f),
                size.z / Mathf.Max(bounds.size.z, 0.001f));
            body.transform.localScale = Vector3.Scale(body.transform.localScale, fit);
            body.transform.localPosition = -Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), fit);
        }

        root.transform.SetParent(parent, true);
        root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(front, Vector3.up));

        if (solid)
        {
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = Vector3.up * size.y / 2f;
            box.size = size;
        }

        // With the sprite art, drawn as its pixel sprite (the model keeps casting the shadow).
        FurnitureSprite.Apply(root, model, front, size, position);
        return root;
    }

    // A solid (or not) tinted block of the hideout's furniture, from the pickups' material.
    private GameObject HideoutProp(string name, Transform parent, Vector3 position, Vector3 size, Color colour, Color emission,
        bool solid, PrimitiveType shape = PrimitiveType.Cube)
    {
        GameObject prop = GameObject.CreatePrimitive(shape);
        prop.name = name;
        prop.transform.SetParent(parent, true);
        prop.transform.position = position;
        prop.transform.localScale = shape == PrimitiveType.Cylinder ? new Vector3(size.x, size.y / 2f, size.z) : size;

        if (!solid)
            Destroy(prop.GetComponent<Collider>());

        Renderer renderer = prop.GetComponent<Renderer>();
        renderer.shadowCastingMode = solid ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

        Material material = ConsumablePickup.DefaultMaterial != null ? ConsumablePickup.DefaultMaterial : ammoMaterial;

        if (material != null)
        {
            renderer.sharedMaterial = material;
            ConsumableModel.Tint(renderer, colour, emission);
        }

        return prop;
    }
}
