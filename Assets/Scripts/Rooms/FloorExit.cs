using UnityEngine;

// Appears in the boss room once the boss is dead: a hatch down to the next floor, or the trophy
// on the last floor. It arms after a short delay so the player doesn't drop through just for
// standing where the boss died.
public class FloorExit : MonoBehaviour
{
    public enum Kind
    {
        NextFloor,
        Trophy
    }

    [SerializeField] private float spinSpeed = 60f;
    [SerializeField] private float bobHeight = 0.15f;

    private FloorGenerator floor;
    private Kind kind;
    private float armedAt;
    private bool used;
    private Transform visual;
    private Vector3 visualRest;

    public void Initialise(FloorGenerator owner, Kind exitKind, float armDelay, Transform spinningPart)
    {
        floor = owner;
        kind = exitKind;
        armedAt = Time.time + armDelay;
        visual = spinningPart;

        if (visual != null)
            visualRest = visual.localPosition;
    }

    private void Update()
    {
        if (visual == null || kind != Kind.Trophy)
            return;

        visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        visual.localPosition = visualRest + Vector3.up * (Mathf.Sin(Time.time * 2f) * bobHeight);
    }

    // Stay rather than Enter, so a player already standing on it when it arms still uses it.
    private void OnTriggerStay(Collider other)
    {
        if (used || Time.time < armedAt || !other.CompareTag("Player"))
            return;

        used = true;
        floor.UseExit(kind);
    }
}
