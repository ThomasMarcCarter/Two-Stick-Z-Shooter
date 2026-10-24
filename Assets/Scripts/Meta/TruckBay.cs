using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The hideout's military truck, as something to use: walking up to it and pressing Interact
// opens its zone map (the StationScreen's truck mode), which lists the zones of the map, who
// needs what from each and which are open. Once the truck runs (all five parts fitted), picking
// an open zone drives there: the player climbs in, the truck pulls away east with its engine
// roaring, and the run starts on floor 1 of that zone behind a "DRIVING TO..." transition. Until
// then the deploy hatch is the only way out, to the Military Base. Built by the FloorGenerator
// with the truck bay (FloorGenerator.Hideout.cs).
public class TruckBay : MonoBehaviour, FloorPickups.IPickup
{
    // The truck is pulling away: controls are locked until the next floor is built.
    public static bool Driving { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Driving = false;
    }

    private Transform truck;
    private PlayerWeapon playerWeapon;

    public Vector3 Position => transform.position;
    public float Reach => 2.6f;
    public bool CanTake => !StationScreen.IsOpen && !Driving;

    // The truck can drive: every part the mechanic asked for is fitted.
    public static bool Running => StoryItems.Complete(NpcKind.Mechanic);

    // `truck` is the body (and sprite) that drives off.
    public static TruckBay Create(GameObject owner, Transform truck)
    {
        TruckBay bay = owner.AddComponent<TruckBay>();
        bay.truck = truck;
        return bay;
    }

    private void OnEnable()
    {
        FloorPickups.Register(this);
    }

    private void OnDisable()
    {
        FloorPickups.Unregister(this);
        Driving = false;
    }

    private void Update()
    {
        if (playerWeapon == null)
        {
            playerWeapon = FindAnyObjectByType<PlayerWeapon>();

            if (playerWeapon == null)
                return;
        }

        if (FloorPickups.Pressed(this, playerWeapon))
            StationScreen.Open(HideoutStation.TruckBay);
    }

    // Why the truck can't take the player to a zone right now, or null if it can.
    public static string DriveBlocked(Zone zone)
    {
        if (!Zones.IsUnlocked(zone))
            return $"LOCKED // {Zones.UnlockHint(zone)}";

        if (!Running)
            return $"THE TRUCK ISN'T RUNNING YET // {StoryItems.Progress(NpcKind.Mechanic)}";

        return null;
    }

    // Climbs in and drives off to the zone; `leave` builds its first floor once the screen is black.
    public void DriveTo(Zone zone, FloorTransition transition, System.Action leave)
    {
        if (!Driving && DriveBlocked(zone) == null)
            StartCoroutine(Drive(zone, transition, leave));
    }

    private IEnumerator Drive(Zone zone, FloorTransition transition, System.Action leave)
    {
        Driving = true;
        MetaProgress.ChooseZone(zone);

        // In the cab: the player (body, gun, flashlight) is hidden until the zone is reached.
        List<Renderer> hidden = new List<Renderer>();
        List<Light> darkened = new List<Light>();
        PlayerHealth body = playerWeapon != null ? playerWeapon.GetComponent<PlayerHealth>() : null;

        if (body != null)
            body.Hidden = true;

        if (playerWeapon != null)
        {
            foreach (Renderer part in playerWeapon.GetComponentsInChildren<Renderer>())
            {
                if (part.enabled)
                {
                    part.enabled = false;
                    hidden.Add(part);
                }
            }

            foreach (Light light in playerWeapon.GetComponentsInChildren<Light>())
            {
                if (light.enabled)
                {
                    light.enabled = false;
                    darkened.Add(light);
                }
            }
        }

        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("truck-engine", 1, i => ProceduralAudio.TruckEngine())), truck.position, 1f);

        // Turns over and catches, then pulls away east, picking up speed.
        yield return new WaitForSeconds(0.9f);

        Vector3 start = truck.position;

        for (float t = 0f; t < 1.4f; t += Time.deltaTime)
        {
            truck.position = start + Vector3.right * (1.6f * t * t);
            yield return null;
        }

        string name = Zones.Name(zone);
        transition.Play($"> DRIVING TO THE {name}", $"{name} // FLOOR 1 // LINK ESTABLISHED", () =>
        {
            foreach (Renderer part in hidden)
            {
                if (part != null)
                    part.enabled = true;
            }

            foreach (Light light in darkened)
            {
                if (light != null)
                    light.enabled = true;
            }

            if (body != null)
                body.Hidden = false;

            Driving = false;
            leave();
        }, true);
    }
}
