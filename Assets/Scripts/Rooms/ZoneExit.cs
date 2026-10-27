using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The way down out of a boss room, built to fit the zone (ZoneExits): set into the room's north
// wall from the start, reaching out behind it (never on the room's floor), shut while the boss is
// alive:
//   Military Base: a concrete stair head with a steel door, barred and padlocked under a blinking
//                  red lamp. The boss dies: the bar drops, the lamp goes green and the door swings
//                  in on the dark stairwell. The player walks in and down; the door slams behind.
//   Shopping Mall: a down escalator, stopped, an OUT OF ORDER barrier across the top. The power
//                  comes back: the barrier's knocked flat, the arrows light and the steps run. The
//                  player steps on and rides down into the floor.
//   Hospital:      a lift with no power (red dashes). The car's called: the indicator counts it
//                  down, it chimes and the doors slide open. The player steps in, the doors close.
//   Airport:       a baggage belt running through rubber strips into the hold, stopped under a red
//                  lamp. The belt starts and the beacon turns; the player climbs on and rides it
//                  through the strips.
// Drawn as furniture sprites (Tools/Sprites/furniture.py), the escalator's steps and the belt as
// flat floor sprites (Tools/Sprites/exits.py); solid where it should be (walls to bullets and the
// navmesh). The FloorGenerator cuts the opening for it and, for the escalator and the chute, builds
// the bay round it (BuildExitBay). The FloorExit on its Entry child (in front of it, in the room)
// does the label and Interact; while
// the player goes in, input is blocked (Descending) and the player's body is moved by hand, then
// hidden until the next floor is built (FinishDescent, from FloorGenerator.PlacePlayer).
public class ZoneExit : MonoBehaviour
{
    private const float WalkSpeed = 2.4f;

    // The player is walking or riding into an exit: input and movement are blocked.
    public static bool Descending { get; private set; }

    private static Action restore;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Descending = false;
        restore = null;
        hiddenParts.Clear();
        hiddenLights.Clear();
    }

    public Zone Zone { get; private set; }
    public ZoneExitInfo Info => ZoneExits.For(Zone);
    public bool Unlocked { get; private set; }
    public bool IsOpen => Unlocked && Time.time >= unlockedAt + Info.OpenTime;

    // Just inside the room in front of it, where the player stands to use it (the FloorExit's spot).
    public Vector3 Entry => transform.TransformPoint(new Vector3(0f, 0f, -0.7f));

    private float unlockedAt;
    private FurnitureSprite main;          // the stair head, the lift, the chute's portal
    private FurnitureSprite[] rails;       // the escalator's balustrades
    private FurnitureSprite barrier;
    private ItemSprite floorSprite;        // the escalator's steps, the belt
    private AudioSource motor;
    private int heldColumn = -1;           // the use animation's frame, over the open one

    // Builds the zone's exit in an opening in a north wall: `wall` is the middle of the opening on
    // the wall's room face; it reaches out north from there. Null without the art.
    public static ZoneExit Create(Zone zone, Transform parent, Vector3 wall, PhysicsMaterial physics)
    {
        if (!HasArt(zone))
            return null;

        GameObject root = new GameObject("Zone Exit");
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(wall, Quaternion.identity);
        ZoneExit exit = root.AddComponent<ZoneExit>();
        exit.Zone = zone;
        exit.Build(physics);
        return exit;
    }

    public static bool HasArt(Zone zone)
    {
        switch (zone)
        {
            case Zone.ShoppingMall:
                return FurnitureSprite.Has("EscalatorRail") && FurnitureSprite.Has("EscalatorBarrier") && SpriteVisuals.Current.exitFloors != null;
            case Zone.Hospital:
                return FurnitureSprite.Has("ExitElevator");
            case Zone.Airport:
                return FurnitureSprite.Has("ChutePortal") && SpriteVisuals.Current.exitFloors != null;
            default:
                return FurnitureSprite.Has("ExitStairwell");
        }
    }

    // ---------- Building ----------

    // Positions are local: x along the wall, z out of it (north, behind the wall; 0 is the wall's
    // room face), y up.
    private void Build(PhysicsMaterial physics)
    {
        switch (Zone)
        {
            case Zone.ShoppingMall:
                // The steps' sprite holds 3 m from its bottom (comb plate, then the steps); the
                // barrier stands across the opening, in the room, and is knocked aside onto its floor.
                floorSprite = Flat("Escalator Steps", new Vector3(0f, 0f, 2f), 0);
                rails = new[]
                {
                    Upright("Escalator Rail L", "EscalatorRail", new Vector3(-0.95f, 0f, 1.5f), new Vector3(0.26f, 1.2f, 3f)),
                    Upright("Escalator Rail R", "EscalatorRail", new Vector3(0.95f, 0f, 1.5f), new Vector3(0.26f, 1.2f, 3f))
                };
                barrier = Upright("Out Of Order", "EscalatorBarrier", new Vector3(0f, 0f, -0.3f), new Vector3(2f, 1f, 0.3f));
                Solid(new Vector3(-0.95f, 0f, 1.5f), new Vector3(0.3f, 1.2f, 3f), physics, false);
                Solid(new Vector3(0.95f, 0f, 1.5f), new Vector3(0.3f, 1.2f, 3f), physics, false);
                Solid(new Vector3(0f, 0f, 1.8f), new Vector3(1.6f, 1.1f, 2.4f), physics, false);
                break;

            case Zone.Hospital:
                // The core fills the opening, its doors in the wall's face.
                main = Upright("Lift", "ExitElevator", new Vector3(0f, 0f, 0.9f), new Vector3(3f, 2.6f, 2f));
                Solid(new Vector3(0f, 0f, 1f), new Vector3(3f, 2.6f, 2f), physics, true);
                break;

            case Zone.Airport:
                // The belt runs 2.75 m out to the portal at the back of the bay (the top of its
                // sprite is empty, under the portal).
                floorSprite = Flat("Baggage Belt", new Vector3(0f, 0f, 2f), 1);
                main = Upright("Chute", "ChutePortal", new Vector3(0f, 0f, 2.6f), new Vector3(2.6f, 2.7f, 0.4f));
                Solid(new Vector3(0f, 0f, 2.6f), new Vector3(2.6f, 2.7f, 0.4f), physics, true);
                Solid(new Vector3(0f, 0f, 1.4f), new Vector3(1.6f, 0.9f, 2f), physics, false);
                break;

            default:
                // The stair head fills the opening, its door in the wall's face.
                main = Upright("Stairwell", "ExitStairwell", new Vector3(0f, 0f, 1.1f), new Vector3(3f, 2.5f, 2.4f));
                Solid(new Vector3(0f, 0f, 1.2f), new Vector3(3f, 2.5f, 2.4f), physics, true);
                break;
        }

        ShowLocked();
    }

    private FurnitureSprite Upright(string name, string sprite, Vector3 local, Vector3 size)
    {
        GameObject holder = new GameObject(name);
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = local;
        return FurnitureSprite.Apply(holder, sprite, -transform.forward, size, holder.transform.position);
    }

    private ItemSprite Flat(string name, Vector3 local, int row)
    {
        GameObject holder = new GameObject(name);
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = local;
        SpriteSheet sheet = SpriteVisuals.Current.exitFloors;
        return ItemSprite.ApplyFlat(holder, sheet, new Vector2Int(0, row), sheet.frameWidth / Mathf.Max(1f, sheet.pixelsPerUnit), 0.03f);
    }

    // A solid block standing on the floor: a wall to bullets, bodies and the navmesh. The tall
    // ones also cast the piece's shadow (the sprites cast none).
    private void Solid(Vector3 local, Vector3 size, PhysicsMaterial physics, bool shadow)
    {
        GameObject block = shadow ? GameObject.CreatePrimitive(PrimitiveType.Cube) : new GameObject("Solid");
        block.name = "Solid";
        block.tag = "Wall";
        block.transform.SetParent(transform, false);
        block.transform.localPosition = local + Vector3.up * size.y / 2f;

        if (shadow)
        {
            block.transform.localScale = size;
            block.GetComponent<Collider>().sharedMaterial = physics;
            block.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }
        else
        {
            BoxCollider box = block.AddComponent<BoxCollider>();
            box.size = size;
            box.sharedMaterial = physics;
        }
    }

    // ---------- Opening ----------

    // The boss is dead: it starts opening.
    public void Unlock()
    {
        if (Unlocked)
            return;

        Unlocked = true;
        unlockedAt = Time.time;

        switch (Zone)
        {
            case Zone.ShoppingMall:
            case Zone.Airport:
                Clank(0.8f);
                StartMotor();
                break;
            default:
                Clank(0.6f);
                break;
        }

        // A light to find it by once it's open.
        Light glow = new GameObject("Exit Light").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.transform.position = Entry + Vector3.up * 1.6f;
        glow.type = LightType.Point;
        glow.color = Info.Light;
        glow.range = 5.5f;
        glow.intensity = 2.5f;
        glow.shadows = LightShadows.None;
    }

    private void StartMotor()
    {
        motor = GameAudio.Route(gameObject.AddComponent<AudioSource>());
        motor.clip = SoundFX.Pick(SoundFX.Clips("motor.hum", 1, i => ProceduralAudio.MotorHum()));
        motor.loop = true;
        motor.volume = 0.35f;
        motor.spatialBlend = 1f;
        motor.minDistance = 2f;
        motor.maxDistance = 16f;
        motor.rolloffMode = AudioRolloffMode.Linear;
        motor.Play();
    }

    private void Clank(float pitch)
    {
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("metal.clank", 3, ProceduralAudio.MetalClank)), Entry, 0.75f, pitch);
    }

    private void Chime()
    {
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("lift.chime", 1, i => ProceduralAudio.LiftChime())), Entry, 0.8f);
    }

    private void Update()
    {
        if (!Unlocked)
        {
            ShowLocked();
            return;
        }

        float t = Time.time - unlockedAt;
        int running = 1 + (int)(Time.time * 8f) % 4;

        switch (Zone)
        {
            case Zone.ShoppingMall:
                SetFloor(running, 0);
                SetRails(1);
                SetColumn(barrier, 1);
                break;

            case Zone.Airport:
                SetFloor(1 + (int)(Time.time * 10f) % 4, 1);
                SetColumn(main, 1 + (int)(Time.time * 3f) % 2);
                break;

            case Zone.Hospital:
            {
                // The car counting down (0.6 s a floor), the chime, the doors sliding.
                int column = t < 1.8f ? 1 + (int)(t / 0.6f) : t < 2.2f ? 4 : t < 2.4f ? 5 : t < 2.6f ? 6 : 7;

                if (column >= 4 && !chimed)
                {
                    chimed = true;
                    Chime();
                }

                SetColumn(main, heldColumn >= 0 ? heldColumn : column);
                break;
            }

            default:
            {
                // The bar drops and the lamp goes green, then the door swings in (another clank).
                int column = t < 0.5f ? 2 : t < 0.8f ? 3 : t < 1.1f ? 4 : 5;

                if (column >= 3 && !swung)
                {
                    swung = true;
                    Clank(0.45f);
                }

                SetColumn(main, heldColumn >= 0 ? heldColumn : column);
                break;
            }
        }
    }

    private bool chimed;
    private bool swung;

    private void ShowLocked()
    {
        switch (Zone)
        {
            case Zone.ShoppingMall:
                SetFloor(0, 0);
                SetRails(0);
                SetColumn(barrier, 0);
                break;
            case Zone.Airport:
                SetFloor(0, 1);
                SetColumn(main, 0);
                break;
            case Zone.Hospital:
                SetColumn(main, 0);
                break;
            default:
                SetColumn(main, Mathf.Repeat(Time.time, 1f) < 0.5f ? 0 : 1);
                break;
        }
    }

    private static void SetColumn(FurnitureSprite sprite, int column)
    {
        if (sprite != null)
            sprite.Column = column;
    }

    private void SetRails(int column)
    {
        if (rails == null)
            return;

        foreach (FurnitureSprite rail in rails)
            SetColumn(rail, column);
    }

    private int floorFrame = -1;

    private void SetFloor(int column, int row)
    {
        if (floorSprite == null || floorFrame == column)
            return;

        floorFrame = column;
        floorSprite.SetFrame(new Vector2Int(column, row));
    }

    // ---------- Going in ----------

    // Walks the player in (or onto it and down), hides them, then `done` (the floor transition).
    public void Use(Transform player, Action done)
    {
        StartCoroutine(GoIn(player, done));
    }

    private IEnumerator GoIn(Transform player, Action done)
    {
        Descending = true;
        Rigidbody body = player.GetComponent<Rigidbody>();
        bool gravity = body != null && body.useGravity;

        if (body != null)
        {
            body.useGravity = false;
            body.detectCollisions = false;
            body.linearVelocity = Vector3.zero;
        }

        float rest = player.position.y;
        Vector3 Local(float x, float y, float z) => transform.TransformPoint(new Vector3(x, 0f, z)) + Vector3.up * (rest + y);

        restore = () =>
        {
            if (player == null)
                return;

            if (body != null)
            {
                body.useGravity = gravity;
                body.detectCollisions = true;
            }

            Show(player, true);
        };

        yield return Move(player, body, new Vector3(Entry.x, rest, Entry.z), WalkSpeed);

        switch (Zone)
        {
            case Zone.ShoppingMall:
                // Onto the top step, then down with the steps into the floor.
                yield return Move(player, body, Local(0f, 0f, 0.25f), WalkSpeed);
                yield return Move(player, body, Local(0f, -1.9f, 2.7f), 3.1f / 2.2f);
                Show(player, false);
                break;

            case Zone.Airport:
                // Up onto the belt, along it and through the strips.
                yield return Move(player, body, Local(0f, 0.6f, 0.3f), 2.5f);
                yield return Move(player, body, Local(0f, 0.6f, 2.3f), 1.2f);
                Show(player, false);
                yield return new WaitForSeconds(0.3f);
                break;

            case Zone.Hospital:
                // Into the car; the doors close (the open frames backwards) with a thud.
                yield return Move(player, body, Local(0f, 0f, 0.3f), WalkSpeed);
                Show(player, false);
                yield return Frames(new[] { 6, 5, 4 }, 0.2f);
                Clank(0.5f);
                yield return new WaitForSeconds(0.4f);
                break;

            default:
                // Through the door and a step down into the dark; the door slams behind.
                yield return Move(player, body, Local(0f, -0.35f, 0.3f), WalkSpeed * 0.8f);
                Show(player, false);
                yield return Frames(new[] { 4, 3, 2 }, 0.15f);
                Clank(0.4f);
                yield return new WaitForSeconds(0.35f);
                break;
        }

        done?.Invoke();
    }

    // The player's body moved by hand to a point at a speed (metres a second).
    private static IEnumerator Move(Transform player, Rigidbody body, Vector3 target, float speed)
    {
        while (player != null && (player.position - target).sqrMagnitude > 0.0004f)
        {
            Vector3 next = Vector3.MoveTowards(player.position, target, speed * Time.deltaTime);
            player.position = next;

            if (body != null)
            {
                body.position = next;
                body.linearVelocity = Vector3.zero;
            }

            yield return null;
        }
    }

    private IEnumerator Frames(int[] columns, float each)
    {
        foreach (int column in columns)
        {
            heldColumn = column;
            yield return new WaitForSeconds(each);
        }
    }

    private static readonly List<Renderer> hiddenParts = new List<Renderer>();
    private static readonly List<Light> hiddenLights = new List<Light>();

    // The player (body, gun, flashlight) hidden in the exit, or shown again.
    private static void Show(Transform player, bool visible)
    {
        if (player.TryGetComponent(out PlayerHealth health))
            health.Hidden = !visible;

        if (!visible)
        {
            hiddenParts.Clear();
            hiddenLights.Clear();

            foreach (Renderer part in player.GetComponentsInChildren<Renderer>())
            {
                if (part.enabled)
                {
                    part.enabled = false;
                    hiddenParts.Add(part);
                }
            }

            foreach (Light light in player.GetComponentsInChildren<Light>())
            {
                if (light.enabled)
                {
                    light.enabled = false;
                    hiddenLights.Add(light);
                }
            }

            return;
        }

        foreach (Renderer part in hiddenParts)
        {
            if (part != null)
                part.enabled = true;
        }

        foreach (Light light in hiddenLights)
        {
            if (light != null)
                light.enabled = true;
        }

        hiddenParts.Clear();
        hiddenLights.Clear();
    }

    // The next floor is built and the player's about to be put in it: back to normal.
    public static void FinishDescent()
    {
        restore?.Invoke();
        restore = null;
        Descending = false;
    }
}
