using System;
using UnityEngine;

// What furniture looks like: a model per prop name (drums, crates, sandbags, bunks, lockers,
// beds, shelving...), used in the hideout and in the themed rooms of each floor. The
// FloorGenerator fits each model to the space the prop takes up; in the hideout a prop without
// a model is built as a tinted block instead.
[CreateAssetMenu(menuName = "Roguelike/Furniture Visuals", fileName = "FurnitureVisuals")]
public class FurnitureVisuals : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("The name the FloorGenerator asks for, e.g. \"Crate\" or \"BunkBed\".")]
        public string name;
        [Tooltip("Visual only (no collider), pivot on the floor at the centre, long side along X, front facing +Z.")]
        public GameObject model;
    }

    public Entry[] models = new Entry[0];

    public GameObject ModelFor(string name)
    {
        foreach (Entry entry in models)
        {
            if (entry != null && entry.name == name && entry.model != null)
                return entry.model;
        }

        return null;
    }
}
