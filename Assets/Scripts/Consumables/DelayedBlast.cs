using UnityEngine;

// A blast that goes off a moment from now (or at once, with no delay), outliving whatever set
// it off, e.g. a bloater that has already been destroyed.
public class DelayedBlast : MonoBehaviour
{
    private Blast.Settings settings;
    private Transform room;
    private float at;

    public static void Start(Vector3 position, Transform room, Blast.Settings settings, float delay)
    {
        if (delay <= 0f)
        {
            Blast.Explode(position, room, settings);
            return;
        }

        GameObject go = new GameObject("Pending Blast");
        go.transform.SetParent(room, true);
        go.transform.position = position;

        DelayedBlast pending = go.AddComponent<DelayedBlast>();
        pending.settings = settings;
        pending.room = room;
        pending.at = Time.time + delay;
    }

    private void Update()
    {
        if (Time.time < at)
            return;

        Blast.Explode(transform.position, room, settings);
        Destroy(gameObject);
    }
}
