using System;
using UnityEngine;
using UnityEngine.AI;

// The Enemy's movement: choosing where to go (chase, last sighting, follow-through, wander),
// planning the route over the navmesh, and steering along walls and out of tight spots.
public partial class Enemy
{
    // ---------- Decisions ----------

    // Where to head for now, and how fast. False means stand still.
    private bool ChooseDestination(out Vector3 destination, out float speed)
    {
        speed = moveSpeed;

        switch (state)
        {
            case State.Chase:
                // Ranged kinds back off when the player's too close, and hold their ground
                // a little way past that, while they can see them.
                if (KeepAway > 0f && canSee)
                {
                    float distance = FlatDistance(rb.position, target.position);

                    if (distance < KeepAway)
                    {
                        Vector3 away = Flat(rb.position - target.position).normalized;
                        destination = FurthestClearPoint(rb.position, away, 2.5f);
                        return true;
                    }

                    if (distance < KeepAway + 2f)
                    {
                        destination = rb.position;
                        return false;
                    }
                }

                destination = Flat(target.position);
                return true;

            case State.Search:
                if (!reachedLastSeen)
                {
                    if (FlatDistance(rb.position, lastSeenPosition) > ArriveDistance)
                    {
                        destination = lastSeenPosition;
                        return true;
                    }

                    // At the corner they went round: carry on the way they were going, as far
                    // as the room allows, for a short while.
                    reachedLastSeen = true;
                    forgetAt = Mathf.Min(forgetAt, Time.time + Senses.memoryTime);
                    searchPoint = FurthestClearPoint(lastSeenPosition, lastSeenHeading, Senses.followDistance);
                }

                if (FlatDistance(rb.position, searchPoint) > ArriveDistance)
                {
                    destination = searchPoint;
                    return true;
                }

                // Followed the trail to its end without spotting them: give up early.
                state = State.Wander;
                break;
        }

        return Wander(out destination, out speed);
    }

    private bool Wander(out Vector3 destination, out float speed)
    {
        speed = moveSpeed * wanderSpeedFraction;
        destination = rb.position;

        if (Time.time < wanderWaitUntil)
            return false;

        bool arrived = hasWanderPoint && FlatDistance(rb.position, wanderPoint) < 0.5f;

        if (!hasWanderPoint || arrived || Time.time > wanderGiveUpAt)
        {
            if (arrived)
            {
                // A pause before setting off again.
                hasWanderPoint = false;
                wanderWaitUntil = Time.time + UnityEngine.Random.Range(wanderPause.x, wanderPause.y);
                return false;
            }

            hasWanderPoint = TryPickWanderPoint(out wanderPoint);
            wanderGiveUpAt = Time.time + 5f;

            if (!hasWanderPoint)
            {
                wanderWaitUntil = Time.time + 0.5f;
                return false;
            }
        }

        destination = wanderPoint;
        return true;
    }

    // A random spot on the room's floor, away from the walls, that's on the navmesh (so not
    // inside a pillar). The route there is worked out when walking it.
    private bool TryPickWanderPoint(out Vector3 point)
    {
        point = rb.position;

        if (room == null || room.Blocks.Count == 0)
            return false;

        float margin = bodyRadius + 0.8f;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Rect block = room.Blocks[UnityEngine.Random.Range(0, room.Blocks.Count)];

            if (block.width <= margin * 2f || block.height <= margin * 2f)
                continue;

            Vector3 candidate = new Vector3(
                UnityEngine.Random.Range(block.xMin + margin, block.xMax - margin),
                0f,
                UnityEngine.Random.Range(block.yMin + margin, block.yMax - margin));

            if (FlatDistance(candidate, rb.position) < 2f)
                continue;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            {
                point = Flat(hit.position);
                return true;
            }
        }

        return false;
    }

    // From `from`, up to `distance` along `heading`, stopping where the navmesh ends (at a
    // wall or pillar).
    private Vector3 FurthestClearPoint(Vector3 from, Vector3 heading, float distance)
    {
        if (heading.sqrMagnitude < 0.01f)
            return from;

        Vector3 end = Flat(from) + heading * distance;

        if (!NavMesh.SamplePosition(from, out NavMeshHit start, 1.5f, NavMesh.AllAreas))
            return end;

        return NavMesh.Raycast(start.position, end, out NavMeshHit hit, NavMesh.AllAreas) ? Flat(hit.position) : end;
    }

    // ---------- Navigation ----------

    // The next point to head for on the way to `destination`: the next corner of a navmesh
    // route there, or the destination itself if there's no route (no navmesh, or off it).
    private Vector3 NextWaypoint(Vector3 destination)
    {
        destination = Flat(destination);

        if (Time.time >= nextRepath || (destination - pathGoal).sqrMagnitude > 1f)
            Repath(destination);

        // Skip corners already reached.
        while (corner < corners.Length && FlatDistance(rb.position, corners[corner]) < 0.4f)
            corner++;

        return corner < corners.Length ? Flat(corners[corner]) : destination;
    }

    private void Repath(Vector3 destination)
    {
        nextRepath = Time.time + RepathInterval;
        pathGoal = destination;
        corners = new Vector3[0];
        corner = 0;

        if (!NavMesh.SamplePosition(Flat(rb.position), out NavMeshHit from, 1.5f, NavMesh.AllAreas)
            || !NavMesh.SamplePosition(destination, out NavMeshHit to, 1.5f, NavMesh.AllAreas))
        {
            return;
        }

        path ??= new NavMeshPath();

        if (NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) && path.status != NavMeshPathStatus.PathInvalid)
        {
            corners = path.corners;
            corner = corners.Length > 1 ? 1 : 0;   // the first corner is where it's standing
        }
    }

    // ---------- Movement ----------

    // If a wall is right ahead, turn to run along it instead of into it, so the enemy slides
    // around corners rather than grinding against them.
    private Vector3 Steer(Vector3 direction)
    {
        Vector3 eye = Flat(rb.position) + Vector3.up * EyeHeight;
        float probe = bodyRadius + 0.35f;

        if (!Physics.SphereCast(eye, bodyRadius * 0.8f, direction, out RaycastHit hit, probe, ~0, QueryTriggerInteraction.Ignore)
            || !hit.collider.CompareTag("Wall"))
        {
            return direction;
        }

        Vector3 normal = Flat(hit.normal).normalized;
        Vector3 along = direction - normal * Vector3.Dot(direction, normal);

        // Heading straight into it: pick a side.
        if (along.sqrMagnitude < 0.01f)
            along = Vector3.Cross(Vector3.up, normal);

        return along.normalized;
    }

    // Wedged (trying to move but barely going anywhere): side-step for a moment.
    private Vector3 Unstick(Vector3 direction)
    {
        if (Time.time < sidestepUntil)
            return sidestep;

        float speed = Flat(rb.linearVelocity).magnitude;
        stuckFor = speed < StuckSpeed ? stuckFor + Time.fixedDeltaTime : 0f;

        // Up against the player counts as arrived, not stuck.
        bool touchingPlayer = state == State.Chase && FlatDistance(rb.position, target.position) < bodyRadius + 0.8f;

        if (stuckFor < StuckTime || touchingPlayer)
            return direction;

        stuckFor = 0f;
        float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        sidestep = (Vector3.Cross(Vector3.up, direction) * side - direction * 0.3f).normalized;
        sidestepUntil = Time.time + 0.35f;
        hasWanderPoint = false;
        return sidestep;
    }

    private static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
