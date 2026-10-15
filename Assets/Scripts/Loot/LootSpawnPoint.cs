using UnityEngine;

// A place in a room where loot may appear: on the floor in the middle of the room, or mounted on
// a wall. FloorGenerator places these when it builds a room and rolls each one against its
// LootTable once; whatever spawned (if anything) is kept in Item. Shown as gizmos in the Scene
// view: green if something spawned, grey if the roll came up empty.
public class LootSpawnPoint : MonoBehaviour
{
    public enum Placement
    {
        Floor,
        Wall
    }

    [SerializeField] private Placement placement;

    public Placement Kind => placement;

    // Wall points: the direction from the room into the wall they're on.
    public Vector3 Outward { get; private set; }

    public GameObject Item { get; set; }

    public static LootSpawnPoint Create(Transform room, Vector3 position, Placement placement, Vector3 outward)
    {
        GameObject go = new GameObject(placement == Placement.Wall ? "Loot Point (Wall)" : "Loot Point (Floor)");
        go.transform.SetParent(room, true);
        go.transform.position = position;

        LootSpawnPoint point = go.AddComponent<LootSpawnPoint>();
        point.placement = placement;
        point.Outward = outward;

        if (outward != Vector3.zero)
            go.transform.rotation = Quaternion.LookRotation(-outward);

        return point;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Item != null ? new Color(0.35f, 1f, 0.55f) : new Color(0.5f, 0.5f, 0.5f, 0.6f);

        if (placement == Placement.Wall)
        {
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1.1f, new Vector3(0.8f, 0.4f, 0.8f));
            Gizmos.DrawLine(transform.position, transform.position + Outward * 0.6f);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.3f, 0.4f);
        }
    }
}
