using System.Collections.Generic;
using UnityEngine;

// Things on the floor taken with Interact (attachments, and ammo that needs a new backpack
// stack). When several are in reach, only the nearest one answers the press, so one press
// takes one thing. A gun pickup in reach comes before all of them (PlayerWeapon handles it).
// Also works out where something dropped from the inventory lands.
public static class FloorPickups
{
    public interface IPickup
    {
        Vector3 Position { get; }
        float Reach { get; }

        // Whether pressing Interact would do anything right now.
        bool CanTake { get; }
    }

    // Height of a drop's coloured glow disc above the floor: over the flat litter sprites (which
    // lie a centimetre or two up), so the clutter never hides it.
    public const float GlowHeight = 0.05f;

    // A soft disc of `colour` on the floor under a drop (child of `root`), so it shows up in the
    // dark. Shared by every floor drop; `scale` enlarges it (ammo crates).
    public static void AddGlow(Transform root, Color colour, float scale = 1f)
    {
        AmmoVisuals visuals = AmmoVisuals.Current;

        if (visuals == null || visuals.glowMaterial == null)
            return;

        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        glow.name = "Glow";
        Object.Destroy(glow.GetComponent<Collider>());
        glow.transform.SetParent(root, false);
        glow.transform.localPosition = Vector3.up * GlowHeight;
        glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        glow.transform.localScale = Vector3.one * visuals.glowSize * scale;

        Renderer renderer = glow.GetComponent<Renderer>();
        renderer.sharedMaterial = visuals.glowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        Color tint = colour * visuals.glowStrength;
        tint.a = 0.8f;
        block.SetColor("_Color", tint);
        renderer.SetPropertyBlock(block);
    }

    private static readonly List<IPickup> all = new List<IPickup>();

    // Statics outlive Play sessions with domain reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        all.Clear();
    }

    public static void Register(IPickup pickup)
    {
        if (!all.Contains(pickup))
            all.Add(pickup);
    }

    public static void Unregister(IPickup pickup)
    {
        all.Remove(pickup);
    }

    // The nearest pickup that's in its reach of a point and can be taken, or null.
    public static IPickup Focus(Vector3 point)
    {
        IPickup nearest = null;
        float best = float.MaxValue;

        foreach (IPickup pickup in all)
        {
            if (!pickup.CanTake)
                continue;

            float distance = Flat(pickup.Position - point).magnitude;

            if (distance <= pickup.Reach && distance < best)
            {
                best = distance;
                nearest = pickup;
            }
        }

        return nearest;
    }

    // Whether Interact this frame goes to this pickup.
    public static bool Pressed(IPickup pickup, PlayerWeapon player)
    {
        return player != null && !GameFlow.InputBlocked && !player.HasNearbyPickup
            && GameInput.Interact.WasPressedThisFrame() && Focus(player.transform.position) == pickup;
    }

    // In front of the player, short of any wall in the way, on the floor.
    public static Vector3 DropPoint(Transform player, float distance = 1.3f)
    {
        Vector3 from = new Vector3(player.position.x, 0.5f, player.position.z);
        Vector3 forward = Flat(player.forward).normalized;

        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;

        float reach = distance;

        foreach (RaycastHit hit in Physics.RaycastAll(from, forward, distance + 0.4f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.root == player.root)
                continue;

            reach = Mathf.Min(reach, hit.distance - 0.4f);
        }

        Vector3 point = from + forward * Mathf.Max(0f, reach);
        return new Vector3(point.x, 0f, point.z);
    }

    // The room a point is in, so something dropped or thrown there is cleared away with it.
    public static Transform RoomAt(Vector3 position)
    {
        FloorGenerator floor = Object.FindAnyObjectByType<FloorGenerator>();
        return floor != null && floor.TryGetRoom(position, out Room room) ? room.transform : null;
    }

    private static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }
}
