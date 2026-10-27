using System.Collections.Generic;
using UnityEngine;

// Modular room meshes: walls and floors are built as meshes from a kit of pieces instead of
// stretched cubes. A wall span is one mesh (with a box collider as tall as the old block's) cut
// into modules on the world's 1 m grid. The rooms' north walls on a run (the only faces the tilted
// camera sees) get the zone's pieces (RoomKits): pilasters where the wall runs on into the next
// cell and at the zone's rhythm along each cell, flat panel variants, 2 m alcoves set back into
// the wall (roller-shutter bays, shop windows, ward windows, glazing) and broken, jagged tops.
// Everything else is plain wall. Every module samples the zone's wall kit atlas (12 one-metre
// columns of the usual 36-row wall layout, Tools/Sprites/wallkit.py) on the walls' own pixel grid,
// so plain wall reads exactly as the old block walls did. A room's floor is one mesh of its blocks'
// top faces, with a collider-only box per block underneath.
// Features are kept clear of doorways (room for an EXIT sign beside them) and corners, and are
// remembered per room so wall decals don't hang over an alcove, a pilaster or a broken top.
// Uses its own random sequence, so a seed's layout is the same with or without the kit. Not used
// without the sprite art or a kit material (old block walls); the hideout, supply room and roof
// get plain kit walls.
public partial class FloorGenerator
{
    [Header("Room Kit")]
    [Tooltip("Build walls and floors as modular meshes (walls from the zone's wall kit). Off builds them from blocks.")]
    [SerializeField] private bool modularRooms = true;
    [Tooltip("The Military Base's wall kit: URP Lit with Tiles/WallKit.png. Zones with their own walls set their kit in Zone Settings. Empty = block walls.")]
    [SerializeField] private Material wallKitMaterial;

    private const int KitColumns = 12;
    private const int KitColumnPixels = 16;            // one column = 1 m of wall
    private const float KitRows = 36f;
    private const float KitFaceRows = 32f;
    private const int KitPanelA = 4, KitPanelB = 5, KitAlcove = 6, KitReveal = 8, KitPilaster = 9, KitBroken = 10;
    private const float PilasterWidth = 0.5f;
    private const float PilasterDepth = 0.1f;          // how far it stands out into the room
    private const float PilasterRise = 0.125f;         // above the top of the wall
    private const float KitDoorGuard = 2.75f;          // features keep this far from a doorway (an EXIT sign fits)
    private const float KitCornerGuard = 0.5f;

    private enum WallModule { Plain, PanelA, PanelB, Alcove, Broken }

    // One wall span to build. Room is set for walls that get the kit's features.
    private struct KitSpan
    {
        public Room Room;
        public bool DoorAtStart;
        public bool DoorAtEnd;
        public bool PilasterAtEnd;     // the wall carries straight on into the next block of the room
        public float CellMiddle;       // middle of the cell's wall, for the pilaster rhythm; NaN for none

        public static KitSpan Plain => new KitSpan { CellMiddle = float.NaN };
    }

    private Material wallKit;
    private System.Random kitRng;

    // Per room: (middle x, north face z, half width) of each alcove, pilaster and broken top.
    private readonly Dictionary<Room, List<Vector3>> wallFeatures = new Dictionary<Room, List<Vector3>>();

    private bool UseWallKit => modularRooms && pixelWalls && wallKit != null;
    private bool KitFeatures => !IsHideout && !IsSupplyRoom && !IsEvac;

    // ---------- Walls ----------

    // A wall from start to end along X (or Z) at `across`, its room face toward `inward`. A height
    // under the wall height makes a low wall (cover), which blocks only as high as it stands.
    private GameObject CreateKitWall(Transform parent, bool alongX, float across, float start, float end, Vector3 inward, KitSpan span, float height = 0f)
    {
        bool low = height > 0f && height < wallHeight;

        if (!low)
            height = wallHeight;

        float length = end - start;

        if (length <= 0.001f)
            return null;

        float middle = (start + end) / 2f;
        GameObject wall = new GameObject("Wall");
        wall.tag = "Wall";
        wall.transform.SetParent(parent, false);
        wall.transform.SetPositionAndRotation(alongX ? new Vector3(middle, 0f, across) : new Vector3(across, 0f, middle), Quaternion.identity);

        // Blocks as high as the old raised block collider (wallBlockHeight).
        float blockHeight = low ? height : Mathf.Max(wallHeight, wallBlockHeight);
        BoxCollider box = wall.AddComponent<BoxCollider>();
        Vector3 size = WallSize(alongX, length, wallThickness);
        box.size = new Vector3(size.x, blockHeight, size.z);
        box.center = Vector3.up * blockHeight / 2f;
        box.sharedMaterial = slidePhysics;

        KitWall builder = new KitWall(this, alongX ? Vector3.right : Vector3.forward, inward, middle, height);
        RoomKit kit = RoomKits.For(Zone);
        List<float> pilasters = new List<float>();

        foreach ((float from, float to, WallModule piece) in LayOutWall(start, end, span, kit, pilasters))
        {
            switch (piece)
            {
                case WallModule.Alcove: builder.Alcove(from, to, kit); break;
                case WallModule.Broken: builder.Broken(from, to, kitRng); break;
                case WallModule.PanelA: builder.Slab(from, to, KitPanelA); break;
                case WallModule.PanelB: builder.Slab(from, to, KitPanelB); break;
                default: builder.Plain(from, to); break;
            }

            if (span.Room != null && piece != WallModule.Plain && piece != WallModule.PanelA && piece != WallModule.PanelB)
                AddWallFeature(span.Room, (from + to) / 2f, across + inward.z * wallThickness / 2f, (to - from) / 2f);
        }

        foreach (float pilaster in pilasters)
        {
            builder.Pilaster(pilaster);
            AddWallFeature(span.Room, pilaster, across + inward.z * wallThickness / 2f, PilasterWidth / 2f);
        }

        builder.Ends(start, end);

        Mesh mesh = builder.Build("Wall (kit)");
        floorMeshes.Add(mesh);
        wall.AddComponent<MeshFilter>().sharedMesh = mesh;
        wall.AddComponent<MeshRenderer>().sharedMaterial = wallKit;
        return wall;
    }

    // Cuts a span into pieces: plain wall with, on feature walls, panels, alcoves and broken tops
    // on whole metres of the world grid, clear of doorways, corners and pilasters. Fills
    // `pilasters` with the pilasters' middles.
    private List<(float, float, WallModule)> LayOutWall(float start, float end, KitSpan span, RoomKit kit, List<float> pilasters)
    {
        List<(float, float, WallModule)> pieces = new List<(float, float, WallModule)>();

        if (span.Room == null)
        {
            pieces.Add((start, end, WallModule.Plain));
            return pieces;
        }

        float lo = start + (span.DoorAtStart ? KitDoorGuard : KitCornerGuard);
        float hi = end - (span.DoorAtEnd ? KitDoorGuard : KitCornerGuard);

        // Where the wall carries on into the next block, and the zone's rhythm along the cell.
        if (span.PilasterAtEnd)
            pilasters.Add(end);

        if (!float.IsNaN(span.CellMiddle) && kit.PilasterOffsets != null)
        {
            foreach (float offset in kit.PilasterOffsets)
            {
                float at = span.CellMiddle + offset;

                if (at - PilasterWidth / 2f >= start + (span.DoorAtStart ? 1.5f : 0.75f)
                    && at + PilasterWidth / 2f <= end - (span.DoorAtEnd ? 1.5f : 0.75f))
                {
                    pilasters.Add(at);
                }
            }
        }

        float bossDamage = span.Room.Type == RoomType.Boss ? 2f : 1f;
        int first = Mathf.CeilToInt(lo);
        int last = Mathf.FloorToInt(hi);
        float cursor = start;
        WallModule previous = WallModule.Plain;

        for (int k = first; k < last;)
        {
            if (UnderPilaster(k, k + 1, pilasters))
            {
                previous = WallModule.Plain;
                k++;
                continue;
            }

            float roll = (float)kitRng.NextDouble();
            WallModule piece = WallModule.Plain;
            int width = 1;

            if (roll < kit.AlcoveChance)
            {
                if (k + 2 <= last && previous != WallModule.Alcove && !UnderPilaster(k, k + 2, pilasters))
                {
                    piece = WallModule.Alcove;
                    width = 2;
                }
            }
            else if (roll < kit.AlcoveChance + kit.BrokenChance * bossDamage)
                piece = WallModule.Broken;
            else if (roll < kit.AlcoveChance + kit.BrokenChance * bossDamage + kit.PanelChance)
            {
                // Cladding comes in runs of a few metres.
                piece = kitRng.NextDouble() < 0.5 ? WallModule.PanelA : WallModule.PanelB;
                width = 1 + kitRng.Next(3);

                while (width > 1 && (k + width > last || UnderPilaster(k, k + width, pilasters)))
                    width--;
            }

            if (piece != WallModule.Plain)
            {
                if (k > cursor)
                    pieces.Add((cursor, k, WallModule.Plain));

                // Panels are built a metre at a time, so each samples its own column.
                for (int m = 0; m < (piece == WallModule.Alcove ? 1 : width); m++)
                    pieces.Add((k + m, piece == WallModule.Alcove ? k + 2 : k + m + 1, piece));

                cursor = k + width;
            }

            previous = piece;
            k += width;
        }

        if (end > cursor)
            pieces.Add((cursor, end, WallModule.Plain));

        return pieces;
    }

    private static bool UnderPilaster(float from, float to, List<float> pilasters)
    {
        foreach (float at in pilasters)
        {
            if (at + PilasterWidth / 2f > from && at - PilasterWidth / 2f < to)
                return true;
        }

        return false;
    }

    private void AddWallFeature(Room room, float x, float face, float halfWidth)
    {
        if (room == null)
            return;

        if (!wallFeatures.TryGetValue(room, out List<Vector3> list))
            wallFeatures[room] = list = new List<Vector3>();

        list.Add(new Vector3(x, face, halfWidth));
    }

    // Whether a 3 m decal at x on the north face would overlap one of the room's wall features.
    private bool WallFeatureAt(Room room, float x, float face, float halfWidth)
    {
        if (!wallFeatures.TryGetValue(room, out List<Vector3> list))
            return false;

        foreach (Vector3 feature in list)
        {
            if (Mathf.Abs(feature.y - face) < 0.6f && Mathf.Abs(feature.x - x) < halfWidth + feature.z)
                return true;
        }

        return false;
    }

    // Builds one wall's mesh. Positions are given as (along, depth, height): along the world axis
    // the wall runs on, depth back from its room face (negative stands out into the room), height
    // from the floor. Faces sample the kit atlas on the wall's pixel grid: 16 px per metre along,
    // the 32 face rows up the wall, the 4 cap rows across its top.
    private class KitWall
    {
        private readonly Vector3 along;
        private readonly Vector3 inward;
        private readonly float middle;
        private readonly float height;
        private readonly float thickness;
        private readonly KitMesh mesh = new KitMesh();

        // The texture's face rows always span the full wall height, so a low wall shows the
        // bottom of the wall (skirting, band) under its cap.
        private readonly float textureHeight;

        public KitWall(FloorGenerator generator, Vector3 along, Vector3 inward, float middle, float height)
        {
            this.along = along;
            this.inward = inward;
            this.middle = middle;
            this.height = height;
            textureHeight = generator.wallHeight;
            thickness = generator.wallThickness;
        }

        public Mesh Build(string name) => mesh.Build(name);

        private Vector3 P(float s, float depth, float y)
        {
            return along * (s - middle) + inward * (thickness / 2f - depth) + Vector3.up * y;
        }

        private static Vector2 UV(float px, float row)
        {
            return new Vector2(px / (KitColumns * KitColumnPixels), row / KitRows);
        }

        private float FaceRow(float y) => y / textureHeight * KitFaceRows;

        private static float CapRow(float t) => KitFaceRows + Mathf.Clamp01(t) * (KitRows - KitFaceRows);

        // The plain wall's pixel column at s: its 4 m texture repeats along the world.
        private static float PlainPx(float s) => Mathf.Repeat(s, 4f) * KitColumnPixels;

        // A face toward the room at depth d, from s0 to s1 and y0 to y1, sampling px0 to px1.
        private void Front(float s0, float s1, float d, float y0, float y1, float px0, float px1)
        {
            mesh.Quad(P(s0, d, y0), P(s1, d, y0), P(s1, d, y1), P(s0, d, y1),
                UV(px0, FaceRow(y0)), UV(px1, FaceRow(y0)), UV(px1, FaceRow(y1)), UV(px0, FaceRow(y1)), inward);
        }

        // A top at height y over depths d0 to d1, its front edge on the cap's edge row.
        private void Top(float s0, float s1, float d0, float d1, float y, float px0, float px1)
        {
            mesh.Quad(P(s0, d0, y), P(s1, d0, y), P(s1, d1, y), P(s0, d1, y),
                UV(px0, CapRow(0f)), UV(px1, CapRow(0f)), UV(px1, CapRow(1f)), UV(px0, CapRow(1f)), Vector3.up);
        }

        // The face away from the room (plain wall).
        private void Back(float s0, float s1, float y)
        {
            float px0 = PlainPx(s0);
            float px1 = px0 + (s1 - s0) * KitColumnPixels;
            mesh.Quad(P(s0, thickness, 0f), P(s1, thickness, 0f), P(s1, thickness, y), P(s0, thickness, y),
                UV(px0, 0f), UV(px1, 0f), UV(px1, FaceRow(y)), UV(px0, FaceRow(y)), -inward);
        }

        // A face across the wall at s (facing +along when sign > 0), sampling px0 to px1 across it.
        private void Side(float s, float sign, float d0, float d1, float y0, float y1, float px0, float px1, float row0, float row1)
        {
            mesh.Quad(P(s, d0, y0), P(s, d1, y0), P(s, d1, y1), P(s, d0, y1),
                UV(px0, row0), UV(px1, row0), UV(px1, row1), UV(px0, row1), along * sign);
        }

        // Plain wall, cut where its 4 m texture repeats.
        public void Plain(float s0, float s1)
        {
            float s = s0;

            while (s < s1 - 0.0001f)
            {
                float next = Mathf.Min(s1, (Mathf.Floor(s / 4f + 0.0001f) + 1f) * 4f);
                float px0 = PlainPx(s);
                float px1 = px0 + (next - s) * KitColumnPixels;
                Front(s, next, 0f, 0f, height, px0, px1);
                Top(s, next, 0f, thickness, height, px0, px1);
                Back(s, next, height);
                s = next;
            }
        }

        // A full-height metre of wall from one of the atlas's columns (the panels).
        public void Slab(float s0, float s1, int column)
        {
            float px0 = column * KitColumnPixels;
            float px1 = px0 + (s1 - s0) * KitColumnPixels;
            Front(s0, s1, 0f, 0f, height, px0, px1);
            Top(s0, s1, 0f, thickness, height, px0, px1);
            Back(s0, s1, height);
        }

        // A 2 m opening set back into the wall: sill and lintel flush with the face, the back of
        // the opening `depth` in, reveals either side and the sill's top.
        public void Alcove(float s0, float s1, RoomKit kit)
        {
            float sill = Mathf.Clamp(kit.AlcoveSill, 0f, height);
            float lintel = Mathf.Clamp(kit.AlcoveLintel, sill, height);
            float depth = Mathf.Clamp(kit.AlcoveDepth, 0.05f, thickness - 0.05f);
            float px0 = KitAlcove * KitColumnPixels;
            float px1 = px0 + (s1 - s0) * KitColumnPixels;
            float reveal = KitReveal * KitColumnPixels;

            if (sill > 0f)
                Front(s0, s1, 0f, 0f, sill, px0, px1);

            Front(s0, s1, 0f, lintel, height, px0, px1);
            Front(s0, s1, depth, sill, lintel, px0, px1);
            Top(s0, s1, 0f, thickness, height, px0, px1);
            Back(s0, s1, height);

            // The sill's top, then the reveals facing into the opening.
            mesh.Quad(P(s0, 0f, sill), P(s1, 0f, sill), P(s1, depth, sill), P(s0, depth, sill),
                UV(reveal, CapRow(0f)), UV(reveal + KitColumnPixels, CapRow(0f)),
                UV(reveal + KitColumnPixels, CapRow(1f)), UV(reveal, CapRow(1f)), Vector3.up);
            float revealPx = depth * KitColumnPixels;
            Side(s0, 1f, 0f, depth, sill, lintel, reveal, reveal + revealPx, FaceRow(sill), FaceRow(lintel));
            Side(s1, -1f, 0f, depth, sill, lintel, reveal, reveal + revealPx, FaceRow(sill), FaceRow(lintel));
        }

        // A metre whose top has broken away: four strips of jagged height, rubble on top, the
        // damaged column of the atlas below.
        public void Broken(float s0, float s1, System.Random random)
        {
            const int strips = 4;
            float px = KitBroken * KitColumnPixels;
            float width = (s1 - s0) / strips;
            float previous = height;

            for (int i = 0; i < strips; i++)
            {
                float a = s0 + i * width;
                float b = a + width;
                float top = height * Mathf.Lerp(0.7f, 0.95f, (float)random.NextDouble());
                float pa = px + i * width * KitColumnPixels;
                float pb = pa + width * KitColumnPixels;

                Front(a, b, 0f, 0f, top, pa, pb);
                Top(a, b, 0f, thickness, top, pa, pb);
                Back(a, b, top);

                // The step up to the strip (or wall) before this one.
                if (!Mathf.Approximately(previous, top))
                {
                    float low = Mathf.Min(previous, top);
                    float high = Mathf.Max(previous, top);
                    Side(a, previous > top ? 1f : -1f, 0f, thickness, low, high, pa, pa + thickness * 8f, CapRow(0f), CapRow(1f));
                }

                previous = top;
            }

            // The step back up to full height after it.
            Side(s1, -1f, 0f, thickness, previous, height, px + 8f, px + 8f + thickness * 8f, CapRow(0f), CapRow(1f));
        }

        // A column standing out of the face and a little above the wall, centred on s.
        public void Pilaster(float s)
        {
            float s0 = s - PilasterWidth / 2f;
            float s1 = s + PilasterWidth / 2f;
            float top = height + PilasterRise;
            float px0 = KitPilaster * KitColumnPixels;
            float px1 = px0 + PilasterWidth * KitColumnPixels;
            float side = px1 + 4f;

            Front(s0, s1, -PilasterDepth, 0f, top, px0, px1);
            mesh.Quad(P(s0, -PilasterDepth, top), P(s1, -PilasterDepth, top), P(s1, thickness, top), P(s0, thickness, top),
                UV(px0, CapRow(0f)), UV(px1, CapRow(0f)), UV(px1, CapRow(1f)), UV(px0, CapRow(1f)), Vector3.up);
            Side(s0, -1f, -PilasterDepth, thickness, 0f, top, px1, side, 0f, FaceRow(top));
            Side(s1, 1f, -PilasterDepth, thickness, 0f, top, px1, side, 0f, FaceRow(top));
        }

        // The wall's two ends (seen at corners and doorways).
        public void Ends(float s0, float s1)
        {
            float px = thickness * KitColumnPixels;
            Side(s0, -1f, 0f, thickness, 0f, height, 0f, px, 0f, FaceRow(height));
            Side(s1, 1f, 0f, thickness, 0f, height, 0f, px, 0f, FaceRow(height));
        }
    }

    // ---------- Floors ----------

    // The floor block's collider (no renderer) and its top face in the room's floor mesh.
    private void AddFloorBlock(Transform room, Rect block, KitMesh floor)
    {
        GameObject collider = new GameObject("Floor");
        collider.transform.SetParent(room, false);
        collider.transform.position = new Vector3(block.center.x, -0.1f, block.center.y);
        collider.AddComponent<BoxCollider>().size = new Vector3(block.width, 0.2f, block.height);

        Vector3 origin = room.position;
        Vector3 a = new Vector3(block.xMin, 0f, block.yMin);
        Vector3 b = new Vector3(block.xMax, 0f, block.yMin);
        Vector3 c = new Vector3(block.xMax, 0f, block.yMax);
        Vector3 d = new Vector3(block.xMin, 0f, block.yMax);
        float size = floorTextureSize > 0f ? floorTextureSize : 4f;

        floor.Quad(a - origin, b - origin, c - origin, d - origin,
            new Vector2(a.x, a.z) / size, new Vector2(b.x, b.z) / size, new Vector2(c.x, c.z) / size, new Vector2(d.x, d.z) / size,
            Vector3.up);
    }

    // The room's floor mesh, once all its blocks are in.
    private GameObject CreateFloorMesh(Transform room, KitMesh floor, Material material, RoomType type)
    {
        GameObject floorObject = new GameObject("Floor Mesh");
        floorObject.transform.SetParent(room, false);

        Mesh mesh = floor.Build("Floor (kit)");
        floorMeshes.Add(mesh);
        floorObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        floorObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        ApplyTint(floorObject, type);
        return floorObject;
    }

    // Collects quads into a mesh.
    private class KitMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        // Corners in order round the quad; the winding is turned to face `normal`.
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 normal)
        {
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);

            for (int k = 0; k < 4; k++)
                normals.Add(normal);

            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f)
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            else
                triangles.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }

        public Mesh Build(string name)
        {
            Mesh mesh = new Mesh { name = name };

            if (vertices.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
