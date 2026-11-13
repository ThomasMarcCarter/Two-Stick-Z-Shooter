using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The plug in a secret room's doorway (FloorGenerator.Secret.cs): a doorway's width of ordinary
// wall with a crack painted on it. It blocks like any wall, and since it's left out of the navmesh
// bake it carves its own hole (NavMeshObstacle) so zombies can't path through it. While it stands,
// the secret room behind it is hidden from every map (Room.Hidden). A blast (Blast.Explode) going
// off within 2.5 m of it brings it down: chunks and dust, a crumbling thud, and the wall is gone,
// which opens the way, uncarves the navmesh and puts the room on the maps.
public class CrackedWall : MonoBehaviour
{
    private const float BlastReach = 2.5f;

    private static readonly List<CrackedWall> standing = new List<CrackedWall>();

    private Room secret;
    private Bounds bounds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        standing.Clear();
    }

    // Makes `root` (standing in the doorway, on the floor) the cracked wall hiding `room`. `size`
    // is the plug's width, height and thickness, along X.
    public static CrackedWall Attach(GameObject root, Room room, Vector3 size)
    {
        CrackedWall wall = root.AddComponent<CrackedWall>();
        wall.secret = room;
        wall.bounds = new Bounds(root.transform.position + Vector3.up * size.y / 2f, size);
        room.Hidden = true;

        NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.up * size.y / 2f;
        obstacle.size = size;
        obstacle.carving = true;
        return wall;
    }

    private void OnEnable()
    {
        standing.Add(this);
    }

    private void OnDisable()
    {
        standing.Remove(this);
    }

    // A blast at `centre`: opens every cracked wall within reach of it.
    public static void BlastAt(Vector3 centre)
    {
        for (int i = standing.Count - 1; i >= 0; i--)
        {
            if (i < standing.Count && standing[i].bounds.SqrDistance(centre) <= BlastReach * BlastReach)
                standing[i].Open();
        }
    }

    // Debug: opens them all (so a teleport into a secret room isn't a trap).
    public static void OpenAll()
    {
        for (int i = standing.Count - 1; i >= 0; i--)
            standing[i].Open();
    }

    private void Open()
    {
        if (secret != null)
            secret.Hidden = false;

        Vector3 centre = bounds.center;

        if (ImpactEffects.TryGetMaterials(out ImpactEffects.Materials materials))
        {
            Color stone = new Color(0.42f, 0.4f, 0.37f);
            Color dust = new Color(0.62f, 0.6f, 0.56f);
            ImpactEffects.Debris(centre, Vector3.back + Vector3.up, 16, stone, dust, materials);
            ImpactEffects.Debris(new Vector3(centre.x, 0.2f, centre.z), Vector3.up, 12, dust, Color.Lerp(dust, Color.white, 0.4f), materials);
        }

        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("explosion", 3, ProceduralAudio.Explosion)), centre, 0.7f, 0.55f);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("cardboard.break", 2, ProceduralAudio.CardboardBreak)), centre, 0.9f, 0.45f);

        standing.Remove(this);
        Destroy(gameObject);
    }
}
