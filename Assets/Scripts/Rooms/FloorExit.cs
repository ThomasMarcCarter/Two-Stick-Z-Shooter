using UnityEngine;

// Appears in the boss room once the boss is dead: a hatch down to the next floor, or the trophy
// on the last floor (the hideout's deploy hatch is one too). Nothing happens by walking onto
// it: the player walks up and presses Interact, with a label saying what it does. The hatch's
// two doors swing open over the arming delay (with a clank), and it can't be used until
// they're open; in the hideout it starts open.
public class FloorExit : MonoBehaviour, FloorPickups.IPickup
{
    public enum Kind
    {
        NextFloor,
        Trophy
    }

    private const float OpenAngle = 112f;

    [SerializeField] private float spinSpeed = 60f;
    [SerializeField] private float bobHeight = 0.15f;

    private FloorGenerator floor;
    private Kind kind;
    private float openedAt;
    private float armDelay;
    private bool used;
    private Transform visual;
    private Vector3 visualRest;
    private Transform[] doors;
    private Quaternion[] doorRest;
    private PickupLabel label;
    private PlayerWeapon playerWeapon;
    private bool clanked;

    public Vector3 Position => transform.position;
    public float Reach => kind == Kind.Trophy ? 1.6f : 2.1f;
    public bool CanTake => !used && Armed && !StationScreen.IsOpen;

    private bool Armed => Time.time >= openedAt + armDelay;

    // doorHinges: the hatch's door pivots (on the hinge line, door lying flat along their local
    // +x), or null; they swing open about their local z.
    public void Initialise(FloorGenerator owner, Kind exitKind, float delay, Transform spinningPart, Transform[] doorHinges)
    {
        floor = owner;
        kind = exitKind;
        openedAt = Time.time;
        armDelay = Mathf.Max(0f, delay);
        visual = spinningPart;
        doors = doorHinges;
        label = PickupLabel.Create(transform, kind == Kind.Trophy ? 2.2f : 1.4f);

        if (visual != null)
            visualRest = visual.localPosition;

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

    private void Update()
    {
        if (visual != null && kind == Kind.Trophy)
        {
            visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            visual.localPosition = visualRest + Vector3.up * (Mathf.Sin(Time.time * 2f) * bobHeight);
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
            label.Show(LabelText, kind == Kind.Trophy ? new Color(1f, 0.8f, 0.3f) : UiColours.Phosphor);
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

        if (kind == Kind.Trophy)
            return "TROPHY  <size=70%><alpha=#99>// THE LAST FLOOR IS CLEAR</size>\n" + action + "TAKE IT AND GET OUT</color></size>";

        if (floor != null && floor.IsHideout)
            return "DEPLOY HATCH  <size=70%><alpha=#99>// FLOOR 1</size>\n" + action + "DEPLOY</color></size>";

        string header = $"HATCH  <size=70%><alpha=#99>// DOWN TO FLOOR {(floor != null ? floor.FloorNumber + 1 : 0)}</size>\n";
        return Armed ? header + action + "CLIMB DOWN</color></size>" : header + "<size=70%><alpha=#88>OPENING...</size>";
    }

    private void Use()
    {
        used = true;
        label.Hide();
        floor.UseExit(kind);
    }
}
