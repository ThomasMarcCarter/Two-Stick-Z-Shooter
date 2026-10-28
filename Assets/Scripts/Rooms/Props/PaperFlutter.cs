using System;
using UnityEngine;
using Random = UnityEngine.Random;

// Loose paper caught in a draught (room life, 2026-10-28): every so often a sheet of paper or a
// newspaper lifts a hand's breadth off the floor, slides a little way with the room's draught
// and turns as it settles. It never strays far from where it was dropped (it drifts back once
// it's gone a metre and a half). Moved by hand, not physics; Unity's Random drives it.
// FloorGenerator.RoomLife.cs adds it to the paper litter of rooms with a draught.
public class PaperFlutter : MonoBehaviour
{
    private const float Leash = 1.5f;
    private const float HopTime = 0.9f;

    private Vector3 home;
    private Vector3 draught;
    private float next;
    private float hopStart = -1f;
    private Vector3 from;
    private Vector3 to;
    private float fromYaw;
    private float toYaw;
    private Rigidbody body;
    private bool wasKinematic;
    private Func<Vector3, bool> onFloor;

    // `onFloor` says whether a point is clear floor to land on (it never hops into a wall).
    public static void Add(GameObject paper, Vector3 draught, Func<Vector3, bool> onFloor)
    {
        PaperFlutter flutter = paper.AddComponent<PaperFlutter>();
        flutter.home = paper.transform.position;
        flutter.draught = new Vector3(draught.x, 0f, draught.z).normalized;
        flutter.next = Time.time + Random.Range(1f, 10f);
        flutter.body = paper.GetComponent<Rigidbody>();
        flutter.wasKinematic = flutter.body != null && flutter.body.isKinematic;
        flutter.onFloor = onFloor;
    }

    private void Update()
    {
        if (hopStart >= 0f)
        {
            float t = Mathf.Clamp01((Time.time - hopStart) / HopTime);
            float eased = 1f - (1f - t) * (1f - t);
            Vector3 at = Vector3.Lerp(from, to, eased) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.15f;
            transform.position = at;
            Vector3 euler = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(euler.x, Mathf.LerpAngle(fromYaw, toYaw, eased), euler.z);

            if (t >= 1f)
            {
                hopStart = -1f;

                // Physics back on once it's down (a kick still sends it skidding).
                if (body != null)
                    body.isKinematic = wasKinematic;
            }

            return;
        }

        if (Time.time < next)
            return;

        // With the draught, or back home when it's wandered too far.
        Vector3 away = transform.position - home;
        away.y = 0f;
        Vector3 way = away.magnitude > Leash ? -away.normalized : Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * draught;

        from = transform.position;
        to = from + way * Random.Range(0.25f, 0.7f);
        to.y = from.y;
        next = Time.time + Random.Range(4f, 13f);

        if (onFloor != null && !onFloor(to))
            return;

        fromYaw = transform.eulerAngles.y;
        toYaw = fromYaw + Random.Range(-160f, 160f);
        hopStart = Time.time;

        if (body != null)
            body.isKinematic = true;
    }
}
