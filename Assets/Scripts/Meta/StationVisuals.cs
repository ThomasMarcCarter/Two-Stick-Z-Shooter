using System;
using UnityEngine;

// What the hideout stations look like: a model per station for its static body (the generator,
// workbench, med station, ammo press, trade post and intel desk). StationConsole still builds
// the moving and glowing parts itself (flywheel, press ram, screens, lamps) on top. Set on the
// FloorGenerator, which makes it current; any station without a model keeps its primitive kit.
[CreateAssetMenu(menuName = "Roguelike/Station Visuals", fileName = "StationVisuals")]
public class StationVisuals : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public HideoutStation station;
        [Tooltip("Visual only (no collider), pivot on the floor at the station's origin, front facing +Z.")]
        public GameObject model;
    }

    public Entry[] models = new Entry[0];

    public static StationVisuals Current { get; private set; }

    // Statics outlive Play sessions with domain reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Current = null;
    }

    public static void Use(StationVisuals visuals)
    {
        Current = visuals;
    }

    public GameObject ModelFor(HideoutStation station)
    {
        foreach (Entry entry in models)
        {
            if (entry != null && entry.station == station && entry.model != null)
                return entry.model;
        }

        return null;
    }
}
