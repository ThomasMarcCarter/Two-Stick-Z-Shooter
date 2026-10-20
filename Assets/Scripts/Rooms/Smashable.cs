using UnityEngine;

// Room furniture (a theme's shelving, lockers, beds, crates...) that a boss can wreck: the
// Butcher's charge and a splash of the Broodmother's acid break it apart in a burst of debris,
// and the zombies' paths close up where it stood. Also the one way those attacks break whatever
// they meet (Break, BreakAround): cardboard boxes are crushed flat, trash props break, are thrown
// aside or (fixed cover) wrecked, and furniture is smashed. Bullets and melee don't touch furniture.
public class Smashable : MonoBehaviour
{
    [Tooltip("Colours of the debris it breaks into.")]
    [SerializeField] private Color colour = new Color(0.45f, 0.44f, 0.4f);
    [SerializeField] private Color colourDark = new Color(0.2f, 0.19f, 0.17f);

    private bool smashed;

    // Breaks whatever this collider belongs to, hit at point while moving in direction. True if
    // it was something breakable.
    public static bool Break(Collider collider, Vector3 point, Vector3 direction)
    {
        if (collider == null || !ImpactEffects.TryGetMaterials(out ImpactEffects.Materials materials))
            return false;

        CardboardBox box = collider.GetComponentInParent<CardboardBox>();

        if (box != null)
        {
            box.Smash(direction, materials);
            return true;
        }

        Prop prop = collider.GetComponentInParent<Prop>();

        if (prop != null)
        {
            prop.Smash(point, direction, materials);
            return true;
        }

        Smashable furniture = collider.GetComponentInParent<Smashable>();

        if (furniture != null)
        {
            furniture.Smash(direction, materials);
            return true;
        }

        return false;
    }

    // Everything breakable within radius of centre, pushed outward from it. Returns how many.
    public static int BreakAround(Vector3 centre, float radius)
    {
        int count = 0;

        foreach (Collider collider in Physics.OverlapSphere(centre, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 point = collider.ClosestPoint(centre);
            Vector3 outward = point - centre;
            outward.y = 0f;

            if (Break(collider, point, outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward))
                count++;
        }

        return count;
    }

    private void Smash(Vector3 direction, ImpactEffects.Materials materials)
    {
        if (smashed)
            return;

        smashed = true;
        Renderer[] parts = GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.zero);

        foreach (Renderer part in parts)
            bounds.Encapsulate(part.bounds);

        // A few bursts spread over its size, thrown the way it was hit.
        for (int i = 0; i < 4; i++)
        {
            Vector3 at = bounds.center + Vector3.Scale(bounds.extents, new Vector3(Random.Range(-0.7f, 0.7f), Random.Range(-0.5f, 0.5f), Random.Range(-0.7f, 0.7f)));
            ImpactEffects.Debris(at, (Vector3.up + direction.normalized * 0.8f).normalized, 10, colour, colourDark, materials);
        }

        ImpactEffects.Sparks(bounds.center, (Vector3.up - direction.normalized).normalized, materials);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("prop.metal", 3, ProceduralAudio.MetalClank)), bounds.center, 0.8f, 0.7f);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("cardboard.break", 2, ProceduralAudio.CardboardBreak)), bounds.center, 0.8f, 0.6f);
        Destroy(gameObject);
    }
}
