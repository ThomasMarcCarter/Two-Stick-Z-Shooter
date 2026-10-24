using System;
using UnityEngine;

// The ways on: the hatch that appears in the boss room once the boss is dead (down to the next
// floor, or on the last floor down to the supply room), the hideout's deploy hatch, and the
// supply room's ladder up to the extraction point, which ends the run as a win. Nothing happens
// by walking onto one: the player walks up and presses Interact, with a label saying what it
// does. The hatch's two doors swing open over the arming delay (with a clank), and it can't be
// used until they're open; in the hideout it starts open. The ladder can be held shut by a
// reason (`Blocked`, e.g. the truck part still on its shelf), which its label shows.
public class FloorExit : MonoBehaviour, FloorPickups.IPickup
{
    public enum Kind
    {
        NextFloor,
        Extraction
    }

    private const float OpenAngle = 112f;

    private FloorGenerator floor;
    private Kind kind;
    private float openedAt;
    private float armDelay;
    private bool used;
    private Transform[] doors;
    private Quaternion[] doorRest;
    private PickupLabel label;
    private PlayerWeapon playerWeapon;
    private bool clanked;

    // Why it can't be used yet, or null when it can (the supply room's ladder: the part first).
    public Func<string> Blocked { get; set; }

    public Vector3 Position => transform.position;
    public float Reach => kind == Kind.Extraction ? 1.8f : 2.1f;
    public bool CanTake => !used && Armed && !StationScreen.IsOpen && BlockedReason == null;

    private bool Armed => Time.time >= openedAt + armDelay;
    private string BlockedReason => Blocked?.Invoke();

    // doorHinges: the hatch's door pivots (on the hinge line, door lying flat along their local
    // +x), or null; they swing open about their local z.
    public void Initialise(FloorGenerator owner, Kind exitKind, float delay, Transform[] doorHinges)
    {
        floor = owner;
        kind = exitKind;
        openedAt = Time.time;
        armDelay = Mathf.Max(0f, delay);
        doors = doorHinges;
        label = PickupLabel.Create(transform, kind == Kind.Extraction ? 2.4f : 1.4f);

        if (doors != null)
        {
            doorRest = new Quaternion[doors.Length];

            for (int i = 0; i < doors.Length; i++)
                doorRest[i] = doors[i].localRotation;
        }

        // Already open (the hideout's): no clank.
        clanked = armDelay <= 0f;
        UpdateDoors();
    }

    private void OnEnable()
    {
        FloorPickups.Register(this);
    }

    private void OnDisable()
    {
        FloorPickups.Unregister(this);
    }

    private ItemSprite hatchSprite;
    private bool spriteOpen;

    private void Update()
    {
        // The hatch's sprite (sprite art) shows it open once it's ready to use.
        if (hatchSprite == null && kind == Kind.NextFloor)
            hatchSprite = GetComponent<ItemSprite>();

        if (hatchSprite != null && !spriteOpen && Armed)
        {
            spriteOpen = true;
            hatchSprite.SetFrame(new Vector2Int(1, 0));
        }

        UpdateDoors();

        if (playerWeapon == null)
        {
            playerWeapon = FindAnyObjectByType<PlayerWeapon>();

            if (playerWeapon == null)
                return;
        }

        Vector3 offset = playerWeapon.transform.position - transform.position;
        offset.y = 0f;
        bool showLabel = !used && offset.magnitude <= Reach + 2.5f && !StationScreen.IsOpen;

        if (showLabel && !label.IsShowing)
            label.Show(LabelText, UiColours.Phosphor);
        else if (!showLabel && label.IsShowing)
            label.Hide();

        if (FloorPickups.Pressed(this, playerWeapon))
            Use();
    }

    // The doors swing up and over, easing out, with a clank as they start.
    private void UpdateDoors()
    {
        if (doors == null)
            return;

        float t = armDelay <= 0f ? 1f : Mathf.Clamp01((Time.time - openedAt) / armDelay);

        if (!clanked && t > 0f)
        {
            clanked = true;
            SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("metal.clank", 3, ProceduralAudio.MetalClank)), transform.position, 0.7f, 0.7f);
        }

        float eased = 1f - (1f - t) * (1f - t) * (1f - t);

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null)
                doors[i].localRotation = doorRest[i] * Quaternion.Euler(0f, 0f, OpenAngle * eased);
        }
    }

    private string LabelText()
    {
        string action = $"<size=80%><color=#FFFFFF>{InputHint.Format("[{Interact}]")} ";

        if (kind == Kind.Extraction)
        {
            string header = "LADDER UP  <size=70%><alpha=#99>// EXTRACTION POINT</size>\n";
            string blocked = BlockedReason;

            return blocked != null
                ? header + $"<size=70%><color={UiColours.DangerHex}>{blocked}</color></size>"
                : header + action + "CLIMB OUT AND HEAD HOME</color></size>";
        }

        if (floor != null && floor.IsHideout)
            return $"DEPLOY HATCH  <size=70%><alpha=#99>// {Zones.Name(Zone.MilitaryBase)} // FLOOR 1</size>\n" + action + "DEPLOY</color></size>";

        string where = floor == null ? "" : floor.FloorNumber >= floor.TotalFloors ? "DOWN TO THE SUPPLY ROOM" : $"DOWN TO FLOOR {floor.FloorNumber + 1}";
        string hatch = $"HATCH  <size=70%><alpha=#99>// {where}</size>\n";
        return Armed ? hatch + action + "CLIMB DOWN</color></size>" : hatch + "<size=70%><alpha=#88>OPENING...</size>";
    }

    private void Use()
    {
        used = true;
        label.Hide();
        floor.UseExit(kind);
    }
}
