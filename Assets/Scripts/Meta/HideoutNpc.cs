using UnityEngine;

// A rescued survivor living in the hideout, standing by their station. Says hello (floating
// speech, see SpeechText) when the player comes over, mutters to themselves now and then while
// the player is about, turns to face the player when they're close, and comments when the player
// builds an upgrade at their station. Interact to talk: they run through their lines.
// Built in code by the FloorGenerator when it lays out the hideout.
public class HideoutNpc : MonoBehaviour, FloorPickups.IPickup
{
    private const float GreetRange = 4.5f;
    private const float IdleRange = 10f;
    private const float FaceRange = 4f;
    private const float SpeechHeight = 2.15f;

    private NpcKind kind;
    private CharacterAnimator animator;
    private SpeechText speech;
    private PickupLabel label;
    private PlayerWeapon playerWeapon;
    private Transform player;
    private Quaternion restRotation;
    private bool playerNear;
    private float nextIdle;
    private int lineIndex;
    private string pending;

    public NpcKind Kind => kind;
    public Vector3 Position => transform.position;
    public float Reach => 2f;
    public bool CanTake => !StationScreen.IsOpen;

    public static HideoutNpc Create(NpcKind kind, Vector3 position, Vector3 facing, Transform parent)
    {
        GameObject root = new GameObject(Npcs.Name(kind));
        root.transform.SetParent(parent, true);
        root.transform.SetPositionAndRotation(new Vector3(position.x, 0f, position.z), Quaternion.LookRotation(facing, Vector3.up));

        HideoutNpc npc = root.AddComponent<HideoutNpc>();
        npc.kind = kind;
        npc.restRotation = root.transform.rotation;
        npc.animator = BuildBody(kind, root.transform);
        npc.speech = SpeechText.Create(root.transform, SpeechHeight);
        npc.label = PickupLabel.Create(root.transform, 1.2f);
        npc.lineIndex = Random.Range(0, 3);
        npc.nextIdle = Time.time + Random.Range(6f, 14f);
        return npc;
    }

    // The survivor's model with a solid capsule round it; a plain capsule if there's no model.
    public static CharacterAnimator BuildBody(NpcKind kind, Transform root)
    {
        CapsuleCollider body = root.gameObject.AddComponent<CapsuleCollider>();
        body.center = Vector3.up * 0.9f;
        body.height = 1.8f;
        body.radius = 0.32f;

        CharacterAnimator animator = CharacterVisuals.AttachToNpc(root, kind);

        if (animator == null && root.Find("Model") == null && root.GetComponent<SpriteCharacter>() == null)
        {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Model";
            Destroy(capsule.GetComponent<Collider>());
            capsule.transform.SetParent(root, false);
            capsule.transform.localPosition = Vector3.up * 0.9f;
            capsule.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);

            if (ConsumablePickup.DefaultMaterial != null)
            {
                Renderer renderer = capsule.GetComponent<Renderer>();
                renderer.sharedMaterial = ConsumablePickup.DefaultMaterial;
                ConsumableModel.Tint(renderer, Npcs.Voice(kind) * 0.5f, Color.black);
            }
        }

        return animator;
    }

    private void OnEnable()
    {
        FloorPickups.Register(this);
        StationScreen.Purchased += OnPurchased;
    }

    private void OnDisable()
    {
        FloorPickups.Unregister(this);
        StationScreen.Purchased -= OnPurchased;
    }

    private void OnPurchased(HideoutStation station)
    {
        if (station == Npcs.Station(kind))
            pending = Pick(Npcs.Upgraded(kind));
    }

    public void Say(string line)
    {
        speech.Say(line, Npcs.Voice(kind));
        label.Hide();
    }

    private void Update()
    {
        if (player == null)
        {
            playerWeapon = FindAnyObjectByType<PlayerWeapon>();
            player = playerWeapon != null ? playerWeapon.transform : null;

            if (player == null)
                return;
        }

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;

        if (animator != null)
        {
            animator.Talking = speech.IsSpeaking;
            animator.LookAt = distance < IdleRange ? player : null;
        }

        // Turn to face the player when they're close, back to the station when they go.
        Quaternion wanted = distance < FaceRange && offset.sqrMagnitude > 0.01f ? Quaternion.LookRotation(offset) : restRotation;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, 220f * Time.deltaTime);

        // Something to say once the station screen is out of the way.
        if (pending != null && !StationScreen.IsOpen)
        {
            Say(pending);
            pending = null;
        }

        bool near = distance <= GreetRange;

        if (near && !playerNear && !speech.IsSpeaking)
            Say(Pick(Npcs.Greetings(kind)));

        playerNear = near;

        if (distance <= IdleRange && Time.time >= nextIdle)
        {
            if (!speech.IsSpeaking && !StationScreen.IsOpen)
                Say(Pick(Npcs.Idle(kind)));

            nextIdle = Time.time + Random.Range(14f, 26f);
        }

        bool showLabel = distance <= Reach + 1f && !speech.IsSpeaking && !StationScreen.IsOpen;

        if (showLabel && !label.IsShowing)
            label.Show(LabelText, Npcs.Voice(kind));
        else if (!showLabel && label.IsShowing)
            label.Hide();

        if (FloorPickups.Pressed(this, playerWeapon))
            Talk();
    }

    // Interact: the next of their lines, greetings and idle chatter in turn.
    private void Talk()
    {
        string[] greetings = Npcs.Greetings(kind);
        string[] idle = Npcs.Idle(kind);
        int count = greetings.Length + idle.Length;
        int i = lineIndex++ % count;
        Say(i < greetings.Length ? greetings[i] : idle[i - greetings.Length]);
        nextIdle = Time.time + Random.Range(14f, 26f);
    }

    private string LabelText()
    {
        return $"{Npcs.Name(kind)}  <size=70%><alpha=#99>// {Npcs.Role(kind)}</size>\n"
            + $"<size=80%><color=#FFFFFF>{InputHint.Format("[{Interact}]")} TALK</color></size>";
    }

    private static string Pick(string[] lines)
    {
        return lines[Random.Range(0, lines.Length)];
    }
}
