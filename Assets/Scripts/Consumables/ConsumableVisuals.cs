using System;
using UnityEngine;

// What consumables look like: a model per type (medkits, syringe, grenade, molotov, bottle),
// used for pickups on the floor and for thrown ones in flight. Set on the FloorGenerator, which
// makes it current; any type without a model falls back to ConsumableModel's primitive shapes.
// The models' own materials carry the glow (the medkit's cross, the syringe's liquid...).
[CreateAssetMenu(menuName = "Roguelike/Consumable Visuals", fileName = "ConsumableVisuals")]
public class ConsumableVisuals : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public ConsumableType type;
        [Tooltip("Visual only (no collider), pivot at the bottom centre, at pickup size.")]
        public GameObject model;
    }

    public Entry[] models = new Entry[0];

    public static ConsumableVisuals Current { get; private set; }

    // Statics outlive Play sessions with domain reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Current = null;
    }

    public static void Use(ConsumableVisuals visuals)
    {
        Current = visuals;
    }

    public GameObject ModelFor(ConsumableType type)
    {
        foreach (Entry entry in models)
        {
            if (entry != null && entry.type == type && entry.model != null)
                return entry.model;
        }

        return null;
    }
}
