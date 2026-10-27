using System;
using UnityEngine;
using UnityEngine.AI;

// The Enemy's senses: what it can see (sight range, line of sight past walls and pillars) and
// what it remembers of where the player was last seen and heading.
public partial class Enemy
{
    private void UpdateSenses()
    {
        if (Time.time >= nextSightCheck)
        {
            nextSightCheck = Time.time + sightInterval;
            canSee = HasLineOfSight();

            // Only a successful check counts as a sighting, so what's remembered is where the
            // player actually was when last seen, not wherever they got to before the next check.
            if (canSee)
                RecordSighting();
        }

        if (canSee)
        {
            bool spotted = state != State.Chase;
            state = State.Chase;

            if (spotted)
                SpottedPlayer?.Invoke(this);
        }
        else if (state == State.Chase)
        {
            state = State.Search;
            forgetAt = lastSeenTime + Senses.maxSearchTime;
        }

        if (state == State.Search && Time.time > forgetAt)
            state = State.Wander;
    }

    private void RecordSighting()
    {
        lastSeenPosition = Flat(target.position);
        lastSeenTime = Time.time;
        reachedLastSeen = false;
        hasWanderPoint = false;

        // Which way they were heading, if they were moving at all; standing still leaves no
        // trail to follow.
        Vector3 velocity = targetBody != null ? Flat(targetBody.linearVelocity) : Vector3.zero;
        lastSeenHeading = velocity.sqrMagnitude > 0.25f ? velocity.normalized : Vector3.zero;
    }

    // Within sight range, with a clear line from eye height to the player through nothing
    // tagged as a wall (walls, closed doors and pillars). Other enemies and pickups don't block it.
    private bool HasLineOfSight()
    {
        // A sneaking player is harder to pick out, so has to be closer to be seen.
        float range = Senses.SightRangeFor(targetMovement != null && targetMovement.IsSneaking) * SightRangeMultiplier;

        if (FlatDistance(rb.position, target.position) > range)
            return false;

        Vector3 from = Flat(rb.position) + Vector3.up * EyeHeight;
        Vector3 to = Flat(target.position) + Vector3.up * EyeHeight;
        return !WallBetween(from, to);
    }

    private static bool WallBetween(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;

        if (distance < 0.001f)
            return false;

        int count = Physics.RaycastNonAlloc(from, delta / distance, hits, distance, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.CompareTag("Wall"))
                return true;
        }

        return false;
    }
}
