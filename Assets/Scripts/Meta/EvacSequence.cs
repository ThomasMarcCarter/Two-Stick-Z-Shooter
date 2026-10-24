using System.Collections.Generic;
using UnityEngine;

// The ending: the evac chopper the quartermaster's radio called in. Built by the FloorGenerator on
// the Airport's roof (FloorGenerator.Evac.cs) the run the last radio part comes home. The chopper
// comes down onto the pad out of the dark, rotors thumping louder; once it's down the survivors
// come up the stairs one by one, each with a word on the way, and climb aboard; then the player
// walks to its door and presses Interact to board. It lifts off with everyone and the credits
// roll (CreditsScreen).
public class EvacSequence : MonoBehaviour, FloorPickups.IPickup
{
    private enum Phase { Descending, Boarding, Ready, LiftOff }

    private const float DescentTime = 5f;
    private const float StartHeight = 14f;
    private const float SurvivorEvery = 1.3f;
    private const float WalkSpeed = 2.6f;
    private const float LiftOffTime = 3.5f;

    // The chopper lifting off with the player aboard: controls are locked.
    public static bool Cutscene { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Cutscene = false;
    }

    private Transform chopper;
    private Vector3 pad;
    private Vector3 door;
    private Vector3 stairs;
    private Phase phase;
    private float phaseStarted;
    private readonly Queue<NpcKind> waiting = new Queue<NpcKind>();
    private readonly List<(Transform body, NpcKind kind, bool spoke)> walking = new List<(Transform, NpcKind, bool)>();
    private float nextSurvivor;
    private AudioSource rotor;
    private PickupLabel label;
    private PlayerWeapon playerWeapon;

    public Vector3 Position => door;
    public float Reach => 2.4f;
    public bool CanTake => phase == Phase.Ready;

    // `chopper` is its body (and sprite), parked on the pad; `door` where people climb in;
    // `stairs` where the survivors come up onto the roof.
    public static EvacSequence Create(Transform parent, Transform chopper, Vector3 door, Vector3 stairs)
    {
        EvacSequence evac = new GameObject("Evac").AddComponent<EvacSequence>();
        evac.transform.SetParent(parent, false);
        evac.transform.position = door;
        evac.chopper = chopper;
        evac.pad = chopper.position;
        evac.door = door;
        evac.stairs = stairs;
        evac.label = PickupLabel.Create(evac.transform, 1.6f);

        foreach (NpcKind kind in Npcs.All)
        {
            if (MetaProgress.IsRescued(kind))
                evac.waiting.Enqueue(kind);
        }

        evac.rotor = evac.gameObject.AddComponent<AudioSource>();
        evac.rotor.clip = SoundFX.Pick(SoundFX.Clips("rotor", 1, i => ProceduralAudio.RotorLoop()));
        evac.rotor.loop = true;
        evac.rotor.spatialBlend = 0f;
        evac.rotor.volume = 0f;
        GameAudio.Route(evac.rotor, false);
        evac.rotor.Play();

        chopper.position = evac.pad + Vector3.up * StartHeight;
        evac.phaseStarted = Time.time;
        return evac;
    }

    private void OnEnable()
    {
        FloorPickups.Register(this);
    }

    private void OnDisable()
    {
        FloorPickups.Unregister(this);
        Cutscene = false;
    }

    private void Update()
    {
        if (playerWeapon == null)
            playerWeapon = FindAnyObjectByType<PlayerWeapon>();

        float t = Time.time - phaseStarted;

        switch (phase)
        {
            case Phase.Descending:
            {
                float k = Mathf.Clamp01(t / DescentTime);
                chopper.position = pad + Vector3.up * (StartHeight * (1f - Mathf.SmoothStep(0f, 1f, k)));
                rotor.volume = Mathf.Lerp(0.15f, 0.7f, k);

                if (k >= 1f)
                {
                    CameraFollow.Shake(0.3f);
                    RadioMessage.Show("RADIO // " + Npcs.Name(NpcKind.Quartermaster), "WHEELS DOWN. EVERYBODY ON, NOW. NOBODY GETS LEFT BEHIND.");
                    SetPhase(Phase.Boarding);
                }
                break;
            }

            case Phase.Boarding:
                if (waiting.Count > 0 && Time.time >= nextSurvivor)
                {
                    nextSurvivor = Time.time + SurvivorEvery;
                    SendUp(waiting.Dequeue());
                }

                Walk();

                if (waiting.Count == 0 && walking.Count == 0)
                {
                    SetPhase(Phase.Ready);
                    label.Show(() => "EVAC CHOPPER  <size=70%><alpha=#99>// EVERYONE'S ABOARD</size>\n"
                        + $"<size=80%><color=#FFFFFF>{InputHint.Format("[{Interact}]")} BOARD</color></size>", UiColours.Amber);
                }
                break;

            case Phase.Ready:
                if (playerWeapon != null && FloorPickups.Pressed(this, playerWeapon))
                    Board();
                break;

            case Phase.LiftOff:
            {
                float k = Mathf.Clamp01(t / LiftOffTime);
                chopper.position = pad + Vector3.up * (StartHeight * k * k);
                rotor.volume = Mathf.Lerp(0.8f, 0.2f, k);

                if (k >= 1f && !CreditsScreen.IsShowing)
                {
                    rotor.Stop();
                    CreditsScreen.Show();
                }
                break;
            }
        }
    }

    private void SetPhase(Phase next)
    {
        phase = next;
        phaseStarted = Time.time;
    }

    // A survivor up the stairs, off to the chopper.
    private void SendUp(NpcKind kind)
    {
        HideoutNpc npc = HideoutNpc.Create(kind, stairs, door - stairs, transform.parent);

        // Walking, not standing about to be talked to.
        npc.enabled = false;
        walking.Add((npc.transform, kind, false));
    }

    private void Walk()
    {
        for (int i = walking.Count - 1; i >= 0; i--)
        {
            (Transform body, NpcKind kind, bool spoke) = walking[i];

            if (body == null)
            {
                walking.RemoveAt(i);
                continue;
            }

            Vector3 offset = door - body.position;
            offset.y = 0f;

            // A word for the player halfway there.
            if (!spoke && offset.magnitude < Vector3.Distance(Flat(stairs), Flat(door)) * 0.55f)
            {
                SpeechText speech = SpeechText.Create(body, 2.1f);
                speech.Say(Npcs.Boarding(kind), Npcs.Voice(kind), 2.4f);
                walking[i] = (body, kind, true);
            }

            if (offset.magnitude < 0.3f)
            {
                Destroy(body.gameObject);
                walking.RemoveAt(i);
                continue;
            }

            body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(offset), 360f * Time.deltaTime);
            body.position += offset.normalized * Mathf.Min(offset.magnitude, WalkSpeed * Time.deltaTime);
        }
    }

    // In with the rest: the player's hidden, and up it goes.
    private void Board()
    {
        label.Hide();
        Cutscene = true;
        MetaProgress.RecordEvac();

        if (playerWeapon != null)
        {
            if (playerWeapon.TryGetComponent(out PlayerHealth body))
                body.Hidden = true;

            foreach (Renderer part in playerWeapon.GetComponentsInChildren<Renderer>())
                part.enabled = false;

            foreach (Light light in playerWeapon.GetComponentsInChildren<Light>())
                light.enabled = false;
        }

        SetPhase(Phase.LiftOff);
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
