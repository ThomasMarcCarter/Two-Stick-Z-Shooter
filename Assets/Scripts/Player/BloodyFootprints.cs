using System.Collections.Generic;
using UnityEngine;

// Walk through a pool of blood and the player tracks it about: a shoe print every step, left and
// right either side of the path and turned the way they walked, fading out over the next ten
// steps. Prints are flat sprites (SpriteVisuals.footprint) on the floor of the room they're
// left in, so a new floor clears them; the oldest go first past the cap. Added to the player by
// SpriteCharacter when the sprite art is on.
public class BloodyFootprints : MonoBehaviour
{
    private const float StepLength = 0.45f;
    private const int StepsPerPool = 10;
    private const float Gait = 0.09f;      // each foot this far off the middle of the path
    private const int MaxPrints = 80;

    private static readonly Queue<GameObject> prints = new Queue<GameObject>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        prints.Clear();
    }

    private Vector3 lastPosition;
    private float travelled;
    private int bloodySteps;
    private bool rightFoot;

    private void Start()
    {
        lastPosition = transform.position;
    }

    private void Update()
    {
        Vector3 moved = transform.position - lastPosition;
        moved.y = 0f;
        lastPosition = transform.position;

        // A teleport or a floor change isn't a step.
        if (moved.sqrMagnitude > 1f)
            return;

        travelled += moved.magnitude;

        if (travelled < StepLength)
            return;

        travelled -= StepLength;

        if (ImpactEffects.InBlood(transform.position))
            bloodySteps = StepsPerPool;
        else if (bloodySteps > 0)
            Print(moved.normalized, --bloodySteps);
    }

    private void Print(Vector3 along, int left)
    {
        SpriteSheet sheet = SpriteVisuals.Current != null ? SpriteVisuals.Current.footprint : null;

        if (sheet == null || sheet.texture == null || along.sqrMagnitude < 0.0001f)
            return;

        rightFoot = !rightFoot;
        Vector3 side = Vector3.Cross(Vector3.up, along) * (rightFoot ? Gait : -Gait);
        Vector3 at = new Vector3(transform.position.x, 0f, transform.position.z) + side;

        // Toes along the way they walked (the art's toes point right).
        GameObject holder = new GameObject("Footprint");
        holder.transform.SetPositionAndRotation(at, Quaternion.LookRotation(along) * Quaternion.Euler(0f, -90f, 0f));
        holder.transform.SetParent(FloorPickups.RoomAt(at), true);

        PixelSprite sprite = PixelSprite.Create("Sprite", holder.transform, sheet, PixelSprite.Mode.Flat);
        sprite.Lift = Random.Range(0.003f, 0.007f);
        sprite.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        int fade = left > StepsPerPool * 2 / 3 ? 0 : left > StepsPerPool / 3 ? 1 : 2;
        sprite.SetFrame(rightFoot ? 1 : 0, fade, false);

        prints.Enqueue(holder);

        while (prints.Count > MaxPrints)
        {
            GameObject oldest = prints.Dequeue();

            if (oldest != null)
                Destroy(oldest);
        }
    }
}
