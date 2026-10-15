using System;
using UnityEngine;

// What ammo pickups look like: a model per ammo type (an ammo can, a shell box...) shown small
// and spinning for a drop, or bigger and still for a crate, over a soft glow in the ammo's colour
// so it can still be spotted in the dark. Set on the FloorGenerator, which makes it current;
// without one, pickups fall back to plain glowing blocks.
[CreateAssetMenu(menuName = "Roguelike/Ammo Visuals", fileName = "AmmoVisuals")]
public class AmmoVisuals : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public AmmoType type;
        [Tooltip("Visual only (no collider), pivot at the bottom centre, longest side 1 unit.")]
        public GameObject model;
    }

    public Entry[] models = new Entry[0];

    [Tooltip("Size of the model for a drop (an enemy's, or loose ammo).")]
    public float dropSize = 0.55f;
    [Tooltip("Size of the model for a crate.")]
    public float crateSize = 1.1f;
    [Tooltip("How high a drop floats above the floor (it bobs around this).")]
    public float dropHeight = 0.3f;

    [Header("Glow")]
    [Tooltip("Soft disc on the floor under a pickup, tinted the ammo's colour.")]
    public Material glowMaterial;
    public float glowSize = 1.3f;
    [Tooltip("Above 1 makes it bloom.")]
    public float glowStrength = 1.8f;

    public static AmmoVisuals Current { get; private set; }

    // Statics outlive Play sessions with domain reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Current = null;
    }

    public static void Use(AmmoVisuals visuals)
    {
        Current = visuals;
    }

    public GameObject ModelFor(AmmoType type)
    {
        foreach (Entry entry in models)
        {
            if (entry != null && entry.type == type && entry.model != null)
                return entry.model;
        }

        return null;
    }
}
