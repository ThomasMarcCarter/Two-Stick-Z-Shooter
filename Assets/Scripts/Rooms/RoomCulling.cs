using System;
using System.Collections.Generic;
using UnityEngine;

// Room culling (2026-11-12): only the rooms the camera can see are drawn and animated. Every
// frame the floor area in view (the screen's corners cast onto the floor, grown by a margin for
// tall sprites and light spill) is checked against each room's Bounds; a room outside it has its
// renderers, lights and particles switched off and its per-frame visual scripts (sprite placing,
// light flicker, flies, drips, fluttering paper...) disabled. Coming back into view switches back
// on exactly what was switched off, so anything that was meant to be off stays off.
// Colliders, audio (zombie groans are how the player hears them) and gameplay scripts (enemies,
// pickups, doors, the room itself) are never touched, so nothing plays differently. Corridors
// aren't rooms and are always drawn. Made by the FloorGenerator when it starts.
public class RoomCulling : MonoBehaviour
{
    // Off from the debug menu, to compare frame rates.
    public static bool Enabled { get; set; } = true;

    // Metres the view is grown by on every side, and further to the south: an upright sprite or
    // a wall standing south of the screen's bottom edge still reaches up into the picture.
    private const float Margin = 3f;
    private const float SouthMargin = 3f;

    // The visual-only scripts that run every frame; anything else is left running.
    private static readonly HashSet<Type> VisualScripts = new HashSet<Type>
    {
        typeof(PixelSprite), typeof(ItemSprite), typeof(FurnitureSprite), typeof(SpriteCharacter),
        typeof(FaceMovement), typeof(FluorescentFlicker), typeof(PaperFlutter), typeof(CeilingDrip),
        typeof(DoorBeacon), typeof(ScreenFlicker), typeof(SparkingCable), typeof(FlySwarm),
        typeof(FireFlicker),
    };

    private class Hidden
    {
        public readonly List<Behaviour> Behaviours = new List<Behaviour>();
        public readonly List<Renderer> Renderers = new List<Renderer>();
        public readonly List<ParticleSystem> Particles = new List<ParticleSystem>();
    }

    private static readonly Plane Floor = new Plane(Vector3.up, Vector3.zero);
    private static readonly Vector2[] Corners = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };

    private readonly Dictionary<Room, Hidden> hidden = new Dictionary<Room, Hidden>();
    private readonly List<Room> gone = new List<Room>();
    private FloorGenerator floor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Enabled = true;
    }

    public static void Attach(FloorGenerator floor)
    {
        RoomCulling culling = floor.gameObject.AddComponent<RoomCulling>();
        culling.floor = floor;
    }

    private void LateUpdate()
    {
        ForgetDestroyedRooms();
        Camera camera = Camera.main;

        if (floor == null || camera == null)
            return;

        bool haveView = ViewOnFloor(camera, out Rect view);

        foreach (Room room in floor.Rooms)
        {
            if (room == null)
                continue;

            bool show = !Enabled || !haveView || room.Bounds.Overlaps(view);

            if (show)
                Show(room);
            else
                Hide(room);
        }
    }

    // The floor area the camera sees, grown by the margins. False if a corner misses the floor.
    private static bool ViewOnFloor(Camera camera, out Rect view)
    {
        view = default;
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;

        foreach (Vector2 corner in Corners)
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(corner.x, corner.y, 0f));

            if (!Floor.Raycast(ray, out float distance))
                return false;

            Vector3 point = ray.GetPoint(distance);
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minZ = Mathf.Min(minZ, point.z);
            maxZ = Mathf.Max(maxZ, point.z);
        }

        view = Rect.MinMaxRect(minX - Margin, minZ - Margin - SouthMargin, maxX + Margin, maxZ + Margin);
        return true;
    }

    private void Hide(Room room)
    {
        if (hidden.ContainsKey(room))
            return;

        Hidden off = new Hidden();

        foreach (Renderer renderer in room.GetComponentsInChildren<Renderer>())
            if (renderer.enabled)
            {
                renderer.enabled = false;
                off.Renderers.Add(renderer);
            }

        foreach (Behaviour behaviour in room.GetComponentsInChildren<Behaviour>())
            if (behaviour.enabled && (behaviour is Light || VisualScripts.Contains(behaviour.GetType())))
            {
                behaviour.enabled = false;
                off.Behaviours.Add(behaviour);
            }

        foreach (ParticleSystem particles in room.GetComponentsInChildren<ParticleSystem>())
            if (particles.isPlaying)
            {
                particles.Pause(false);
                off.Particles.Add(particles);
            }

        hidden.Add(room, off);
    }

    private void Show(Room room)
    {
        if (!hidden.TryGetValue(room, out Hidden off))
            return;

        hidden.Remove(room);

        foreach (Renderer renderer in off.Renderers)
            if (renderer != null)
                renderer.enabled = true;

        foreach (Behaviour behaviour in off.Behaviours)
            if (behaviour != null)
                behaviour.enabled = true;

        foreach (ParticleSystem particles in off.Particles)
            if (particles != null)
                particles.Play(false);
    }

    // A new floor destroys the old rooms.
    private void ForgetDestroyedRooms()
    {
        gone.Clear();

        foreach (Room room in hidden.Keys)
            if (room == null)
                gone.Add(room);

        foreach (Room room in gone)
            hidden.Remove(room);
    }
}
