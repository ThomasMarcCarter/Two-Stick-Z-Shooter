using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The FloorGenerator's block helpers: plain boxes for walls and floors, with colliders raised
// to stop throws, wall and world-aligned UVs, room tints, and visual-only copies of models.
public partial class FloorGenerator
{
    // A model instance that's only for looking at: no colliders, named.
    private static void VisualOnly(GameObject instance, string name)
    {
        instance.name = name;

        foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
            Destroy(collider);
    }

    // A block with no collider, so only the exit's trigger affects the player.
    private void CreateVisual(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        GameObject block = CreateBlock(name, parent, localPosition, size, material);
        Destroy(block.GetComponent<Collider>());
    }

    private static Vector3 ToLocal(Transform parent, Vector3 world)
    {
        return parent.InverseTransformPoint(world);
    }

    private GameObject CreateWall(string name, Transform parent, Vector3 localPosition, Vector3 size)
    {
        GameObject wall = CreateBlock(name, parent, localPosition, size, wallMaterial);
        wall.tag = "Wall";
        wall.GetComponent<Collider>().sharedMaterial = slidePhysics;
        RaiseCollider(wall);
        ApplyWallUVs(wall);
        return wall;
    }

    // Stretches a floor-standing wall block's collider up to wallBlockHeight, leaving the block
    // drawn at wallHeight (see wallBlockHeight).
    private void RaiseCollider(GameObject block)
    {
        if (!block.TryGetComponent(out BoxCollider box) || wallBlockHeight <= wallHeight || wallHeight <= 0f)
            return;

        float scale = wallBlockHeight / wallHeight;
        box.size = new Vector3(1f, scale, 1f);
        box.center = new Vector3(0f, (scale - 1f) / 2f, 0f);
    }

    // Like ApplyWorldUVs, but for the painted wall texture: along each face it tiles by world
    // position (so spans line up), and up it runs from the floor to the top of the wall once, so
    // every wall shows the same baseboard, band and blockwork. Tops and bottoms are mapped onto
    // the cap strip at the top of the texture.
    private void ApplyWallUVs(GameObject wall)
    {
        MeshFilter filter = wall.GetComponent<MeshFilter>();

        if (filter == null || filter.sharedMesh == null || wallTextureLength <= 0f || wallHeight <= 0f)
            return;

        Mesh mesh = Instantiate(filter.sharedMesh);
        mesh.name = "Wall (world UVs)";
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uvs = new Vector2[vertices.Length];
        Transform t = wall.transform;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = t.TransformPoint(vertices[i]);
            Vector3 normal = normals[i];

            if (Mathf.Abs(normal.y) > 0.5f)
                uvs[i] = new Vector2((world.x + world.z) / wallTextureLength, pixelWalls ? PixelWallCap : 0.985f);
            else
            {
                float along = Mathf.Abs(normal.x) > 0.5f ? world.z : world.x;
                uvs[i] = new Vector2(along / wallTextureLength, Mathf.Clamp01(world.y / wallHeight) * (pixelWalls ? PixelWallFace : 0.95f));
            }
        }

        mesh.uv = uvs;
        mesh.RecalculateTangents();
        filter.sharedMesh = mesh;
        floorMeshes.Add(mesh);
    }

    private Vector3 WallSize(bool alongX, float length, float thickness)
    {
        return alongX
            ? new Vector3(length, wallHeight, thickness)
            : new Vector3(thickness, wallHeight, length);
    }

    private static GameObject CreateBlock(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = size;

        if (material != null)
        {
            block.GetComponent<Renderer>().sharedMaterial = material;
        }

        return block;
    }

    // A cube's texture covers each face once, so on blocks of different sizes it would stretch
    // differently and not line up. Instead map it by world position (top faces by X/Z, sides by
    // the wall they face), so it tiles evenly across every block and runs seamlessly from one
    // block to the next. Run once the block is in place.
    private void ApplyWorldUVs(GameObject block)
    {
        MeshFilter filter = block.GetComponent<MeshFilter>();

        if (filter == null || filter.sharedMesh == null || floorTextureSize <= 0f)
            return;

        Mesh mesh = Instantiate(filter.sharedMesh);
        mesh.name = "Floor (world UVs)";
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uvs = new Vector2[vertices.Length];
        Transform t = block.transform;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = t.TransformPoint(vertices[i]);
            Vector3 normal = normals[i];

            Vector2 uv = Mathf.Abs(normal.y) > 0.5f ? new Vector2(world.x, world.z)
                : Mathf.Abs(normal.x) > 0.5f ? new Vector2(world.z, world.y)
                : new Vector2(world.x, world.y);

            uvs[i] = uv / floorTextureSize;
        }

        mesh.uv = uvs;
        mesh.RecalculateTangents();   // for the normal map, now the UVs run a different way
        filter.sharedMesh = mesh;
        floorMeshes.Add(mesh);
    }

    private void ApplyTint(GameObject floor, RoomType type)
    {
        Color? tint = type switch
        {
            RoomType.Treasure => treasureTint,
            RoomType.Boss => bossTint,
            _ => null
        };

        if (tint == null)
            return;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", tint.Value);
        floor.GetComponent<Renderer>().SetPropertyBlock(block);
    }

    private static string DirectionName(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return "North";
        if (direction == Vector2Int.right) return "East";
        if (direction == Vector2Int.down) return "South";
        return "West";
    }
}
