using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's zone clutter (ZoneClutter): which of the art pack's small trash and floor
// litter the zone uses (picked once a floor; trash bags burst into the same litter), and building
// one of the zone's own items: its stand-in prop stretched to the item's size, given the item's
// surface and remains, then settled and drawn like any other clutter.
public partial class FloorGenerator
{
    private GameObject[] zoneSmallTrash = new GameObject[0];
    private GameObject[] zoneLitter = new GameObject[0];

    private void UseZoneClutter()
    {
        ZoneClutter.Kit kit = ZoneClutter.For(Zone);
        zoneSmallTrash = Matching(smallTrash, kit.SmallPrefabs);
        zoneLitter = Matching(floorLitter, kit.LitterPrefabs);
        Prop.LitterPrefabs = zoneLitter;
    }

    // The prefabs whose names start with one of the prefixes (all of them if none match).
    private static GameObject[] Matching(GameObject[] pool, string[] prefixes)
    {
        if (pool == null)
            return new GameObject[0];

        List<GameObject> kept = new List<GameObject>();

        foreach (GameObject prefab in pool)
        {
            if (prefab == null)
                continue;

            foreach (string prefix in prefixes)
            {
                if (prefab.name.StartsWith(prefix))
                {
                    kept.Add(prefab);
                    break;
                }
            }
        }

        return kept.Count > 0 ? kept.ToArray() : pool;
    }

    private GameObject StandIn(string prefix)
    {
        foreach (GameObject[] pool in new[] { new[] { coverStandIn }, clutterBoxes, largeTrash, smallTrash, floorLitter })
        {
            if (pool == null)
                continue;

            foreach (GameObject prefab in pool)
            {
                if (prefab != null && prefab.name.StartsWith(prefix))
                    return prefab;
            }
        }

        return null;
    }

    // One of the zone's own items, standing (or lying) at position, turned by rotation.
    private GameObject PlaceZoneItem(Transform parent, ZoneClutter.Item item, Vector3 position, Quaternion rotation)
    {
        GameObject prefab = StandIn(item.StandIn);

        if (prefab == null)
            return null;

        GameObject placed = Instantiate(prefab, position, rotation, parent);
        placed.name = item.Name;

        // Stretched from the stand-in's size (its collider, or its model) to the item's.
        Vector3 from = LocalSize(placed);
        float vary = RandomRange(0.9f, 1.1f);
        Vector3 scale = new Vector3(item.Size.x / from.x, item.Size.y / from.y, item.Size.z / from.z) * vary;

        // Litter keeps its own thickness.
        if (item.Sheet == ClutterSprites.Sheet.Flat)
            scale.y = (scale.x + scale.z) / 2f;

        placed.transform.localScale = scale;

        if (placed.TryGetComponent(out Prop prop))
            prop.Material = item.Surface;

        if (placed.TryGetComponent(out CardboardBox box))
            box.RemainsName = item.Remains;

        SettleProp(placed, parent);
        return placed;
    }

    private static Vector3 LocalSize(GameObject placed)
    {
        Vector3 size = Vector3.zero;

        if (placed.TryGetComponent(out BoxCollider collider))
            size = collider.size;
        else
        {
            bool any = false;
            Bounds bounds = default;

            foreach (MeshFilter part in placed.GetComponentsInChildren<MeshFilter>())
            {
                if (part.sharedMesh == null)
                    continue;

                Bounds mesh = part.sharedMesh.bounds;

                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3(i % 2 == 0 ? -1 : 1, (i / 2) % 2 == 0 ? -1 : 1, i / 4 == 0 ? -1 : 1));
                    Vector3 local = placed.transform.InverseTransformPoint(part.transform.TransformPoint(corner));

                    if (!any)
                        bounds = new Bounds(local, Vector3.zero);
                    else
                        bounds.Encapsulate(local);

                    any = true;
                }
            }

            size = any ? bounds.size : Vector3.one;
        }

        return new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), Mathf.Max(0.01f, size.z));
    }
}
