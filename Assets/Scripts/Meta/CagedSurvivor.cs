using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// A survivor locked in a cage in one of a floor's combat rooms (at most one a run, and only once
// enough runs have been completed; see Npcs.NextToFind and FloorGenerator.PlaceCagedSurvivor). While the room's zombies are alive they cower and shout for
// help; once it's clear the player can open the cage with Interact. Freeing them saves the
// rescue straight away (MetaProgress.Rescue): they thank the player, step out of the cage and
// walk to the door along the floor's navmesh (round boxes and pillars), and head off to the
// hideout, where they set up their station from then on.
// The cage is solid, built before the floor's navmesh so zombies path round it, and stops
// bullets. Built in code by the FloorGenerator. With the sprite art it's drawn as pixel sprites in
// two layers, the bars behind the survivor and the bars (and roof) in front of them, on a flat base
// plate (FurnitureSprite "CageBack"/"CageFront", "CageBase"); the open door is their second rows.
public class CagedSurvivor : MonoBehaviour, FloorPickups.IPickup
{
    private const float CageSize = 1.7f;
    private const float CageHeight = 2.3f;
    private const float PleaRange = 14f;

    private NpcKind kind;
    private Room room;
    private Vector3 exit;
    private Transform npc;
    private CharacterAnimator animator;
    private SpeechText speech;
    private PickupLabel label;
    private Transform door;
    private PlayerWeapon playerWeapon;
    private Transform player;
    private float nextPlea;
    private bool readySaid;
    private bool freed;
    private float freedAt;
    private List<Vector3> route;
    private FurnitureSprite[] layers = new FurnitureSprite[0];

    public NpcKind Kind => kind;
    public bool Freed => freed;
    public Vector3 Position => transform.position;
    public float Reach => CageSize / 2f + 1.2f;
    public bool CanTake => !freed && room != null && room.IsCleared;

    // `exit` is where they walk to once freed (a doorway of the room).
    public static CagedSurvivor Create(NpcKind kind, Room room, Vector3 position, Vector3 exit, Material barMaterial)
    {
        GameObject root = new GameObject($"Caged {Npcs.Name(kind)}");
        root.transform.SetParent(room.transform, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);

        Vector3 toExit = exit - root.transform.position;
        toExit.y = 0f;
        root.transform.rotation = Quaternion.LookRotation(toExit.sqrMagnitude > 0.01f ? toExit.normalized : Vector3.back);

        CagedSurvivor cage = root.AddComponent<CagedSurvivor>();
        cage.kind = kind;
        cage.room = room;
        cage.exit = exit;
        cage.BuildCage(barMaterial);
        cage.UseSprites();

        GameObject body = new GameObject(Npcs.Name(kind));
        body.transform.SetParent(root.transform, false);
        cage.npc = body.transform;
        cage.animator = HideoutNpc.BuildBody(kind, body.transform);

        // Inside the cage, the body's own collider would fight the cage's.
        Destroy(body.GetComponent<Collider>());

        if (cage.animator != null)
            cage.animator.Cowering = true;

        // Both above the cage's roof; the speech higher, clear of the label.
        cage.speech = SpeechText.Create(body.transform, 3.3f);
        cage.label = PickupLabel.Create(root.transform, 2.55f);
        cage.nextPlea = Time.time + Random.Range(1f, 3f);
        return cage;
    }

    // Bars all round on a base plate, a roof, and a door of bars on the side facing the exit,
    // hinged at one edge. One solid box collider (tagged Wall) for the whole thing.
    private void BuildCage(Material material)
    {
        float half = CageSize / 2f;
        Color iron = new Color(0.22f, 0.2f, 0.18f);
        Color rust = new Color(0.35f, 0.2f, 0.1f);

        Block("Base", new Vector3(0f, 0.04f, 0f), new Vector3(CageSize + 0.1f, 0.08f, CageSize + 0.1f), material, iron, transform);

        // A barred roof, so the survivor can be seen from above.
        Block("Roof Rail", new Vector3(0f, CageHeight, -half), new Vector3(CageSize + 0.1f, 0.07f, 0.07f), material, rust, transform);
        Block("Roof Rail", new Vector3(0f, CageHeight, half), new Vector3(CageSize + 0.1f, 0.07f, 0.07f), material, rust, transform);
        Block("Roof Rail", new Vector3(-half, CageHeight, 0f), new Vector3(0.07f, 0.07f, CageSize + 0.1f), material, rust, transform);
        Block("Roof Rail", new Vector3(half, CageHeight, 0f), new Vector3(0.07f, 0.07f, CageSize + 0.1f), material, rust, transform);

        for (int i = 1; i < 5; i++)
            Block("Roof Bar", new Vector3(-half + CageSize * i / 5f, CageHeight, 0f), new Vector3(0.04f, 0.04f, CageSize), material, iron, transform);

        // Corner posts.
        foreach (Vector2 corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            Block("Post", new Vector3(corner.x * half, CageHeight / 2f, corner.y * half), new Vector3(0.09f, CageHeight, 0.09f), material, rust, transform);

        const int bars = 5;

        // Back and sides.
        for (int i = 1; i < bars; i++)
        {
            float along = -half + CageSize * i / bars;
            Block("Bar", new Vector3(along, CageHeight / 2f, -half), new Vector3(0.04f, CageHeight, 0.04f), material, iron, transform);
            Block("Bar", new Vector3(-half, CageHeight / 2f, along), new Vector3(0.04f, CageHeight, 0.04f), material, iron, transform);
            Block("Bar", new Vector3(half, CageHeight / 2f, along), new Vector3(0.04f, CageHeight, 0.04f), material, iron, transform);
        }

        // The door, at the front (towards the exit), hinged at the left post.
        door = new GameObject("Door").transform;
        door.SetParent(transform, false);
        door.localPosition = new Vector3(-half, 0f, half);

        for (int i = 1; i < bars; i++)
            Block("Bar", new Vector3(CageSize * i / bars, CageHeight / 2f, 0f), new Vector3(0.04f, CageHeight, 0.04f), material, iron, door);

        Block("Rail", new Vector3(half, CageHeight * 0.5f, 0f), new Vector3(CageSize, 0.06f, 0.06f), material, rust, door);
        Block("Lock", new Vector3(CageSize - 0.12f, CageHeight * 0.5f, 0.06f), new Vector3(0.14f, 0.18f, 0.08f), material,
            new Color(0.6f, 0.5f, 0.15f), door);

        BoxCollider solid = gameObject.AddComponent<BoxCollider>();
        solid.center = new Vector3(0f, CageHeight / 2f, 0f);
        solid.size = new Vector3(CageSize + 0.1f, CageHeight, CageSize + 0.1f);
        gameObject.tag = "Wall";
    }

    // With the sprite art: the cage's model only casts the shadow, and it's drawn as two layers of
    // bars, the back one just behind the middle (where the survivor stands) and the front one on
    // its south edge, over a flat base plate.
    private void UseSprites()
    {
        if (!FurnitureSprite.Has("CageFront") || !FurnitureSprite.Has("CageBack"))
            return;

        foreach (Renderer part in GetComponentsInChildren<Renderer>())
            part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

        Camera camera = Camera.main;
        Vector3 away = camera != null ? camera.transform.forward : Vector3.forward;
        away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
        float full = CageSize + 0.1f;
        const float back = 0.88f;

        layers = new[]
        {
            FurnitureSprite.Apply(Layer("Cage Back"), "CageBack", transform.forward, new Vector3(back, CageHeight, back),
                transform.position + away * (back / 2f + 0.02f)),
            FurnitureSprite.Apply(Layer("Cage Front"), "CageFront", transform.forward, new Vector3(full, CageHeight, full), transform.position)
        };

        SpriteVisuals.FurnitureEntry plate = SpriteVisuals.Current.ForFurniture("CageBase");

        if (plate != null)
            ItemSprite.ApplyFlat(Layer("Cage Base"), plate.sheet, Vector2Int.zero, full, 0.01f);
    }

    private GameObject Layer(string name)
    {
        GameObject layer = new GameObject(name);
        layer.transform.SetParent(transform, false);
        return layer;
    }

    private static void Block(string name, Vector3 position, Vector3 size, Material material, Color colour, Transform parent)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;

        if (material != null)
        {
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            ConsumableModel.Tint(renderer, colour, Color.black);
        }
    }

    private void OnEnable()
    {
        FloorPickups.Register(this);
    }

    private void OnDisable()
    {
        FloorPickups.Unregister(this);
    }

    private void Update()
    {
        if (player == null)
        {
            playerWeapon = FindAnyObjectByType<PlayerWeapon>();
            player = playerWeapon != null ? playerWeapon.transform : null;

            if (player == null)
                return;
        }

        if (freed)
        {
            WalkOut();
            return;
        }

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;
        bool clear = room != null && room.IsCleared;

        if (animator != null)
        {
            animator.Cowering = !clear;
            animator.Talking = speech.IsSpeaking;
            animator.LookAt = distance < PleaRange ? player : null;
        }

        if (!clear)
        {
            // Shouting for help while the player is in earshot.
            if (distance < PleaRange && Time.time >= nextPlea)
            {
                speech.Say(Npcs.CagedPleas[Random.Range(0, Npcs.CagedPleas.Length)], Npcs.Voice(kind), 1.6f);
                nextPlea = Time.time + Random.Range(5f, 9f);
            }
        }
        else if (!readySaid && distance < 6f)
        {
            readySaid = true;
            speech.Say(Npcs.CagedReady(kind), Npcs.Voice(kind), 2f);
        }

        bool showLabel = distance <= Reach + 2f;

        if (showLabel && !label.IsShowing)
            label.Show(LabelText, Npcs.Voice(kind));
        else if (!showLabel && label.IsShowing)
            label.Hide();

        if (FloorPickups.Pressed(this, playerWeapon))
            Free();
    }

    private string LabelText()
    {
        string name = $"{Npcs.Name(kind)}  <size=70%><alpha=#99>// {Npcs.Role(kind)}</size>";

        if (room == null || !room.IsCleared)
            return name + "\n<size=70%><alpha=#88>CAGED // CLEAR THE ROOM TO FREE THEM</size>";

        return name + $"\n<size=80%><color=#FFFFFF>{InputHint.Format("[{Interact}]")} OPEN CAGE</color></size>";
    }

    private void Free()
    {
        freed = true;
        freedAt = Time.time;
        MetaProgress.Rescue(kind);
        label.Hide();

        // The door swings open; the cage stays solid but the survivor steps out through it.
        door.localRotation = Quaternion.Euler(0f, -110f, 0f);

        foreach (FurnitureSprite layer in layers)
        {
            if (layer != null)
                layer.Open = true;
        }

        if (animator != null)
        {
            animator.Cowering = false;
            animator.LookAt = player;
        }

        speech.Say(Npcs.Freed(kind) + $"\n<size=70%><alpha=#AA>{Npcs.Role(kind)} RESCUED // NOW AT THE HIDEOUT</size>", Npcs.Voice(kind), 3.5f);
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("room.cleared", 1, i => ProceduralAudio.RoomCleared())), 0.4f, 1.25f);
    }

    // A moment to say thanks, then out of the cage and off towards the door; gone once there.
    private void WalkOut()
    {
        float since = Time.time - freedAt;

        if (animator != null)
            animator.Talking = speech.IsSpeaking;

        if (since < 2.2f || npc == null)
            return;

        route ??= PlanRoute();

        // Waypoint by waypoint; gone at the last one (or if it somehow takes too long).
        Vector3 offset = Vector3.zero;

        while (route.Count > 0)
        {
            offset = route[0] - npc.position;
            offset.y = 0f;

            if (offset.magnitude > 0.3f)
                break;

            route.RemoveAt(0);
        }

        if (route.Count == 0 || since > 15f)
        {
            Destroy(npc.gameObject);
            npc = null;
            return;
        }

        npc.rotation = Quaternion.RotateTowards(npc.rotation, Quaternion.LookRotation(offset), 360f * Time.deltaTime);
        npc.position += offset.normalized * Mathf.Min(offset.magnitude, 2.4f * Time.deltaTime);
    }

    // Out through the cage door first (the cage itself is cut out of the navmesh), then the
    // navmesh's route from there to the doorway; straight to the doorway if there's no route.
    private List<Vector3> PlanRoute()
    {
        Vector3 doorstep = transform.position + transform.forward * (CageSize / 2f + 0.6f);
        List<Vector3> points = new List<Vector3> { Flat(doorstep) };
        NavMeshPath path = new NavMeshPath();

        if (NavMesh.SamplePosition(doorstep, out NavMeshHit from, 1.5f, NavMesh.AllAreas)
            && NavMesh.SamplePosition(exit, out NavMeshHit to, 2f, NavMesh.AllAreas)
            && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path)
            && path.status == NavMeshPathStatus.PathComplete)
        {
            foreach (Vector3 corner in path.corners)
                points.Add(Flat(corner));
        }

        points.Add(Flat(exit));
        return points;
    }

    private static Vector3 Flat(Vector3 point) => new Vector3(point.x, 0f, point.z);
}
