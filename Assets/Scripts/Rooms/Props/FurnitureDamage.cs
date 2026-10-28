using System.Collections.Generic;
using UnityEngine;

// Room furniture that reacts to gunfire (2026-10-28). Rounds still fly on through furniture (it
// isn't cover against bullets), but what they pass leaves its mark:
//   - light pieces on wheels or legs (desk chairs, trolleys, wheelchairs, drip stands, gurneys,
//     bins) are shoved along the shot and roll to a stop against whatever's in the way; a desk
//     chair sometimes goes over (the DeskChairTipped sheet);
//   - screens and glass (CRTs, kiosks, the vending machine, specimen tanks, perfume counters, lab
//     glassware...) smash on the first hit: the sprite's broken rows (Tools/Sprites/furniture.py
//     ALTS), its glow and any flicker gone, a burst of glass, sparks off electronics, the specimen
//     tank's green slop, and the sound of it.
// Rooms can start with some of the glass already smashed (PreBreak). Added by FloorGenerator to
// the theme furniture it places (SpawnFurniture); hit by Projectile as rounds pass.
public class FurnitureDamage : MonoBehaviour
{
    private const float RollDrag = 7f;           // m/s lost per second while sliding
    private const float WheelDrag = 3.5f;        // ...for things on castors
    private const float TipChance = 0.3f;

    private static readonly HashSet<string> Movable = new HashSet<string>
    {
        "DeskChair", "DeskChairTipped", "Trolley", "Wheelchair", "IVStand", "Gurney", "TrashBin"
    };

    private static readonly HashSet<string> Rolling = new HashSet<string> { "Trolley", "Wheelchair", "Gurney" };

    // Pieces drawn with broken rows (furniture.py ALTS).
    private static readonly HashSet<string> Glass = new HashSet<string>
    {
        "SpecimenTank", "PerfumeCounter", "VendingMachine", "FumeHood", "MallKiosk", "InfoKiosk", "LabBench", "LabTable",
        "DepartureBoard", "XRayScanner", "Desk", "CubiclePod", "NurseStation", "CheckInDesk", "Checkout", "BedMonitor",
        "ServerRack", "FoodCounter"
    };

    // Of those, the ones that spark when they go.
    private static readonly HashSet<string> Electronic = new HashSet<string>
    {
        "Desk", "CubiclePod", "NurseStation", "BedMonitor", "ServerRack", "InfoKiosk", "MallKiosk", "VendingMachine",
        "DepartureBoard", "XRayScanner", "CheckInDesk", "Checkout", "FoodCounter"
    };

    private static readonly Color GlassColour = new Color(0.75f, 0.88f, 0.95f, 1f);
    private static readonly Color SlopColour = new Color(0.35f, 0.8f, 0.45f, 1f);

    private string kind;
    private FurnitureSprite sprite;
    private BoxCollider box;
    private Vector3 velocity;
    private bool broken;

    public bool IsBroken => broken;

    public static bool HasBrokenArt(string name)
    {
        return Glass.Contains(name);
    }

    // Adds the reactions to a furniture piece named for its sheet, if it has any.
    public static FurnitureDamage Add(GameObject piece)
    {
        if (piece == null || (!Movable.Contains(piece.name) && !Glass.Contains(piece.name)))
            return null;

        FurnitureDamage damage = piece.AddComponent<FurnitureDamage>();
        damage.kind = piece.name;
        damage.sprite = piece.GetComponent<FurnitureSprite>();
        damage.box = piece.GetComponent<BoxCollider>();
        return damage;
    }

    // Already smashed when the room's found (no effects, no sound).
    public void PreBreak()
    {
        if (!Glass.Contains(kind) || sprite == null)
            return;

        broken = true;
        sprite.Broken = true;
    }

    // A round passing through it at `point`, travelling at `velocity`.
    public void Hit(Vector3 point, Vector3 shot)
    {
        Vector3 along = new Vector3(shot.x, 0f, shot.z);
        along = along.sqrMagnitude > 0.0001f ? along.normalized : Vector3.forward;

        if (Glass.Contains(kind) && !broken)
            Break(point, along);

        if (Movable.Contains(kind))
            Shove(along);
    }

    private void Break(Vector3 point, Vector3 along)
    {
        broken = true;

        if (sprite != null)
            sprite.Broken = true;

        if (TryGetComponent(out ScreenFlicker flicker))
            Destroy(flicker);

        Burst(point, along, GlassColour, 18, 1.5f, 3.5f);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("impact.glass", 3, ProceduralAudio.GlassBreak)), point, 0.55f, Random.Range(0.9f, 1.1f));

        if (Electronic.Contains(kind))
        {
            Burst(point, along, new Color(1f, 0.8f, 0.35f, 1f), 10, 2f, 4f);
            SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("electric.crackle", 3, ProceduralAudio.ElectricCrackle)), point, 0.35f);
        }

        if (kind == "SpecimenTank")
            Burst(transform.position + Vector3.up * 0.9f, along, SlopColour, 30, 0.8f, 2.2f);
    }

    private void Shove(Vector3 along)
    {
        velocity = along * Random.Range(2.5f, 4f);

        // A desk chair takes a hit and goes over (it stays over).
        if (kind == "DeskChair" && sprite != null && Random.value < TipChance && sprite.Swap("DeskChairTipped"))
            kind = "DeskChairTipped";
    }

    private void Update()
    {
        if (velocity.sqrMagnitude < 0.0025f)
            return;

        Vector3 step = velocity * Time.deltaTime;

        if (Blocked(step))
            velocity = -velocity * 0.2f;
        else
            transform.position += step;

        velocity = Vector3.MoveTowards(velocity, Vector3.zero, (Rolling.Contains(kind) ? WheelDrag : RollDrag) * Time.deltaTime);
    }

    // Whether moving by `step` would push it into a wall, another piece, a body.
    private bool Blocked(Vector3 step)
    {
        if (box == null)
            return false;

        Vector3 half = Vector3.Scale(box.size, transform.lossyScale) * 0.45f;
        Vector3 centre = transform.TransformPoint(box.center) + Vector3.up * 0.05f;
        half.y *= 0.8f;

        foreach (RaycastHit hit in Physics.BoxCastAll(centre, half, step.normalized, transform.rotation, step.magnitude + 0.02f, ~0,
                     QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(transform))
                return true;
        }

        return false;
    }

    // A one-off spray of pixel particles out of `point`, mostly along the shot, falling to the floor.
    private static void Burst(Vector3 point, Vector3 along, Color colour, int count, float minSpeed, float maxSpeed)
    {
        Material pixel = SpriteVisuals.Current != null ? SpriteVisuals.Current.particle : null;

        if (pixel == null)
            return;

        GameObject go = new GameObject("Furniture Burst");
        go.transform.SetPositionAndRotation(point, Quaternion.LookRotation(along + Vector3.up * 0.5f));
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.startColor = colour;
        main.gravityModifier = 1.4f;
        main.maxParticles = count;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = 0.1f;

        Transform floor = new GameObject("Floor").transform;
        floor.SetParent(go.transform, false);
        floor.SetPositionAndRotation(new Vector3(point.x, 0f, point.z), Quaternion.identity);
        ParticleSystem.CollisionModule collision = system.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.Planes;
        collision.SetPlane(0, floor);
        collision.bounce = 0.2f;
        collision.dampen = 0.6f;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = pixel;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        system.Play();
        system.Emit(count);
    }
}
