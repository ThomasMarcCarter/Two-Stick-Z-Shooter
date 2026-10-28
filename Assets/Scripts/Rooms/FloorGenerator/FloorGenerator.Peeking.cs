using System.Collections;
using UnityEngine;

// No peeking (2026-10-28): step through a door into a room that's still full of zombies (the
// camera has moved over to it: it's the CurrentRoom) and fire before the doors have locked, and
// the player is shunted on past the point where they lock and the doors slam shut behind them
// (the room's usual lock, Room.UpdatePlayer). Firing from the room you're leaving, however close
// to its door, doesn't count (user, 2026-10-28); nor does a cleared or already locked room, the
// spawn room or the hideout.
public partial class FloorGenerator
{
    private const float PeekReach = 2.5f;        // how close to a door counts as peeking
    private const float ShuntDepth = 2.3f;       // how far past the door it puts the player (the lock is 1 m past it)
    private const float ShuntTime = 0.22f;

    private bool shunting;

    private void OnGunFired(Gun gun)
    {
        if (shunting || IsHideout || player == null || PlayerWeapon.Current == null || gun.transform.root != PlayerWeapon.Current.transform.root)
            return;

        if (!PeekedRoom(player.position, out Vector3 door, out Vector3 inward))
            return;

        Vector3 target = door + inward * ShuntDepth;
        target.y = player.position.y;

        if (UnityEngine.AI.NavMesh.SamplePosition(target, out UnityEngine.AI.NavMeshHit hit, 1f, UnityEngine.AI.NavMesh.AllAreas))
            target = new Vector3(hit.position.x, player.position.y, hit.position.z);

        StartCoroutine(Shunt(target));
    }

    // The room the player has just stepped into (the current room) while it's uncleared and not
    // yet locked, with the door they came through and the way on into it.
    private bool PeekedRoom(Vector3 position, out Vector3 door, out Vector3 inward)
    {
        door = inward = Vector3.zero;
        Room room = CurrentRoom;

        if (room == null || room.IsCleared || room.IsLocked || room.Enemies.Count == 0 || room.Type == RoomType.Start)
            return false;

        foreach (RoomConnection connection in room.Connections)
        {
            if (connection.Door == null)
                continue;

            // The door stands halfway along the corridor, where the room's cells end.
            Vector3 outward = new Vector3(connection.Direction.x, 0f, connection.Direction.y);
            Vector3 point = Flat(connection.Door.transform.position);

            if (Vector3.Distance(point, Flat(position)) > PeekReach)
                continue;

            door = point;
            inward = -outward;
            return true;
        }

        return false;
    }

    // A quick lurch forward, easing out; the room locks itself once the player's past its margin.
    private IEnumerator Shunt(Vector3 target)
    {
        shunting = true;
        Rigidbody body = player.GetComponent<Rigidbody>();
        Vector3 start = player.position;

        for (float t = 0f; t < ShuntTime && player != null; t += Time.deltaTime)
        {
            float eased = 1f - (1f - t / ShuntTime) * (1f - t / ShuntTime);
            Vector3 at = Vector3.Lerp(start, target, eased);
            player.position = at;

            if (body != null)
            {
                body.position = at;
                body.linearVelocity = Vector3.zero;
            }

            yield return null;
        }

        if (player != null)
        {
            player.position = target;

            if (body != null)
                body.position = target;
        }

        shunting = false;
    }
}
