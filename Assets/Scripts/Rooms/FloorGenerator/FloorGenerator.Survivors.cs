using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's caged survivors: now and then a floor holds the next survivor whose
// signal is findable, caged in the combat room furthest from the start, with a radio distress
// call once the floor starts. At most one a run. Rescued survivors move into the hideout.
public partial class FloorGenerator
{
    [Header("Survivors")]
    [Tooltip("Chance a floor holds a caged survivor, once one's signal is findable (see Npcs). At most one a run.")]
    [SerializeField, Range(0f, 1f)] private float survivorChance = 0.25f;
    [Tooltip("Seconds after a floor starts before the survivor's distress call comes over the radio.")]
    [SerializeField] private float distressCallDelay = 3f;

    // Debug: the next floor generated holds the next findable survivor, chance or not.
    private static bool forceSurvivor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        forceSurvivor = false;
    }

    // Whether this run has already turned up a survivor (at most one a run).
    private bool survivorThisRun;

    // Sometimes a floor holds a survivor: the earliest one whose signal is findable (enough runs
    // completed, not yet rescued), at survivorChance per floor and at most one a run. Caged in
    // the combat room furthest from the start (so it takes some getting to), in the cell its door
    // opens into, towards the back, with a radio distress call once the floor starts. Placed
    // before the navmesh is built so zombies path round the cage.
    private void PlaceCagedSurvivor(List<PlannedRoom> plan)
    {
        NpcKind? kind = Npcs.NextToFind();

        if (kind == null || survivorThisRun || (!forceSurvivor && rng.NextDouble() >= survivorChance))
            return;

        PlannedRoom best = null;

        foreach (PlannedRoom planned in plan)
        {
            if (planned.Type == RoomType.Normal && (best == null || planned.Depth > best.Depth))
                best = planned;
        }

        if (best == null || !roomsByCell.TryGetValue(best.Cells[0], out Room room))
            return;

        room.DisplayName = "SURVIVOR";
        Vector3 centre = CellToWorld(best.Cells[0]);
        List<Vector3> doors = DoorwayPositions(room);
        Vector3 exit = centre + Vector3.back * 3f;
        float nearest = float.MaxValue;

        // The doorway nearest this cell: the one they'll walk out of.
        foreach (Vector3 door in doors)
        {
            float distance = Vector3.Distance(door, centre);

            if (distance < nearest)
            {
                nearest = distance;
                exit = door;
            }
        }

        // Off the middle (that's where loot and the crate like to go), towards the back wall.
        Vector3 away = centre - exit;
        away.y = 0f;
        Vector3 spot = centre + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * 2.2f;

        // Somewhere else near the middle if that's in a cut-out or by a pillar.
        foreach (Vector3 offset in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.zero })
        {
            if (IsClear(spot, PillarsOf(room), 1.6f))
                break;

            spot = centre + offset * 2.2f;
        }

        CagedSurvivor.Create(kind.Value, room, spot, exit, ConsumablePickup.DefaultMaterial != null ? ConsumablePickup.DefaultMaterial : ammoMaterial);
        survivorThisRun = true;
        forceSurvivor = false;
        StartCoroutine(DistressCall(kind.Value));

        // Counted as a pillar, so zombies, boxes, trash and crates keep off it.
        if (!pillars.TryGetValue(room, out List<Vector3> list))
            pillars[room] = list = new List<Vector3>();

        list.Add(spot);
    }

    // The radio crackles with their call for help a few seconds into the floor (after the
    // floor transition has faded out).
    private System.Collections.IEnumerator DistressCall(NpcKind kind)
    {
        yield return new WaitForSeconds(distressCallDelay);
        RadioMessage.Show("DISTRESS SIGNAL", Npcs.DistressCall(kind));
    }

    // Debug: the next floor generated holds the next findable survivor, chance or not.
    public static void ForceSurvivorOnNextFloor()
    {
        forceSurvivor = true;
    }
}
