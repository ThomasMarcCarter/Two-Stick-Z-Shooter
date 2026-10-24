using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's ending: the Airport terminal's roof, where the evac chopper the
// quartermaster called in comes down. Reached up from the supply room's ladder on the run that
// brings the last radio part home (Extract). One wide, quiet room (two cells): a painted landing
// pad in the middle with its H, floodlights, a red beacon, and the stairwell door at the west end
// the survivors come up through. The EvacSequence takes it from there.
public partial class FloorGenerator
{
    // On the roof for the evac; FloorNumber stays the last floor's.
    public bool IsEvac { get; private set; }

    private Vector3 EvacPad => (CellToWorld(Vector2Int.zero) + CellToWorld(Vector2Int.right)) / 2f;

    // Where the player arrives: by the stairwell door at the west end.
    private Vector3 EvacSpawn => CellToWorld(Vector2Int.zero) + new Vector3(-roomSize.x / 2f + 2.5f, 1f, 0f);

    // Debug: up to the roof, as if the last radio part had just come home.
    public void DebugEnterEvac()
    {
        if (IsHideout || IsEvac)
            return;

        FloorNumber = TotalFloors;
        transition.Play("> UP TO THE ROOF", $"{ZoneName} // ROOF // EVAC INBOUND", EnterEvac);
    }

    // Runs while the transition screen is fully black.
    private void EnterEvac()
    {
        IsSupplyRoom = false;
        IsEvac = true;
        ClearLooseObjects();
        Generate();

        if (cameraFollow != null)
            cameraFollow.SnapToTarget();
    }

    private void GenerateEvac()
    {
        floorRoot = new GameObject("Roof").transform;
        floorRoot.SetParent(transform, false);

        RoomShape wide = RoomShape.Single;

        foreach (RoomShape shape in RoomShape.All)
        {
            if (shape.Name == "2x1")
                wide = shape;
        }

        BuildRoom(new PlannedRoom { Shape = wide, Cells = new List<Vector2Int> { Vector2Int.zero, Vector2Int.right }, Type = RoomType.Start },
            new HashSet<(Vector2Int, Vector2Int)>());

        Room room = rooms[0];
        room.DisplayName = "ROOF // EVAC";
        clutter[room] = new List<Vector3>();
        Transform parent = room.transform;
        Vector3 pad = EvacPad;

        PaintPad(parent, pad);

        // Floodlights at the pad's corners and a red beacon on the stairwell.
        foreach (Vector2 corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
        {
            Light flood = new GameObject("Floodlight").AddComponent<Light>();
            flood.transform.SetParent(parent, false);
            flood.transform.position = pad + new Vector3(corner.x * 6f, 4f, corner.y * 3.5f);
            flood.transform.rotation = Quaternion.LookRotation(pad - flood.transform.position);
            flood.type = LightType.Spot;
            flood.spotAngle = 80f;
            flood.range = 14f;
            flood.intensity = 6f;
            flood.color = new Color(1f, 0.95f, 0.85f);
            flood.shadows = LightShadows.None;
        }

        Vector3 stairs = CellToWorld(Vector2Int.zero) + new Vector3(-roomSize.x / 2f + 1f, 0f, 1.5f);
        Light beacon = new GameObject("Beacon").AddComponent<Light>();
        beacon.transform.SetParent(parent, false);
        beacon.transform.position = stairs + new Vector3(0f, 2.6f, 0f);
        beacon.type = LightType.Point;
        beacon.range = 5f;
        beacon.intensity = 3f;
        beacon.color = new Color(1f, 0.2f, 0.15f);
        beacon.gameObject.AddComponent<FluorescentFlicker>();

        // The chopper, parked over the pad side on (nose east); the sequence brings it down.
        Vector3 size = new Vector3(2.6f, 2.6f, 8f);
        GameObject chopper = PropModel("Chopper", "Chopper", parent, pad, Vector3.right, size, false)
            ?? HideoutProp("Chopper", parent, pad + Vector3.up * size.y / 2f, RotateSize(Quaternion.LookRotation(Vector3.right), size),
                new Color(0.3f, 0.35f, 0.22f), Color.black, false);
        Vector3 door = pad + new Vector3(0.4f, 0f, -1.9f);

        SpawnTrash(room);
        PlacePlayer();
        EvacSequence.Create(parent, chopper.transform, door, stairs);
        Debug.Log("Generated the roof: the evac chopper's coming in");
    }

    // The landing pad: a painted ring with the big H in it.
    private void PaintPad(Transform parent, Vector3 pad)
    {
        if (bossArenaMaterial != null)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = "Landing Pad";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(parent, false);
            ring.transform.position = new Vector3(pad.x, 0.013f, pad.z);
            ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = new Vector3(9f, 9f, 1f);
            ring.GetComponent<Renderer>().sharedMaterial = bossArenaMaterial;
        }

        Material paint = ConsumablePickup.DefaultMaterial != null ? ConsumablePickup.DefaultMaterial : ammoMaterial;
        Color yellow = new Color(0.85f, 0.7f, 0.15f);

        foreach ((Vector3 offset, Vector3 scale) in new[]
        {
            (new Vector3(-1.1f, 0f, 0f), new Vector3(0.45f, 3.4f, 1f)),
            (new Vector3(1.1f, 0f, 0f), new Vector3(0.45f, 3.4f, 1f)),
            (Vector3.zero, new Vector3(2.2f, 0.45f, 1f))
        })
        {
            GameObject stroke = GameObject.CreatePrimitive(PrimitiveType.Quad);
            stroke.name = "Pad H";
            Destroy(stroke.GetComponent<Collider>());
            stroke.transform.SetParent(parent, false);
            stroke.transform.position = new Vector3(pad.x, 0.016f, pad.z) + offset;
            stroke.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            stroke.transform.localScale = scale;
            Renderer renderer = stroke.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (paint != null)
            {
                renderer.sharedMaterial = paint;
                ConsumableModel.Tint(renderer, yellow, yellow * 0.2f);
            }
        }
    }
}
