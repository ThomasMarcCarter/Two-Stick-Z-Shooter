using System;
using UnityEngine;

// What crafting materials look like on the floor: a model per type (a gear and bolts, a cable
// drum, a chemical bottle, a powder keg, a jerry can). Set on the FloorGenerator, which makes it
// current; any type without a model falls back to MaterialPickup's primitive shapes. The models'
// own materials carry the colour and the glow of their accent parts.
[CreateAssetMenu(menuName = "Roguelike/Material Visuals", fileName = "MaterialVisuals")]
public class MaterialVisuals : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public MaterialType type;
        [Tooltip("Visual only (no collider), pivot at the bottom centre, about 0.3 m across.")]
        public GameObject model;
    }

    public Entry[] models = new Entry[0];

    public static MaterialVisuals Current { get; private set; }

    // Statics outlive Play sessions with domain reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Current = null;
    }

    public static void Use(MaterialVisuals visuals)
    {
        Current = visuals;
    }

    public GameObject ModelFor(MaterialType type)
    {
        foreach (Entry entry in models)
        {
            if (entry != null && entry.type == type && entry.model != null)
                return entry.model;
        }

        return null;
    }
}
