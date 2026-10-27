using System.Collections.Generic;
using UnityEngine;

// A weapon attachment lying on the floor: a small dark case with a glowing stripe in the
// attachment's colour, floating and turning like an ammo drop. Close by, a label says what it
// is, what it does and where it'll go; Interact takes it. It's fitted straight to a gun if one
// it fits (in hand first) has that slot free; otherwise it goes in the backpack, to fit later
// from the inventory. With the backpack full it swaps onto the gun, dropping the old one here.
// Found in treasure rooms and dropped by bosses (see FloorGenerator).
public class AttachmentPickup : MonoBehaviour, FloorPickups.IPickup
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private const float UseRange = 1.4f;
    private const float LabelRange = 3f;

    private static readonly List<AttachmentPickup> all = new List<AttachmentPickup>();
    private static PlayerWeapon player;

    public WeaponAttachment Attachment { get; private set; }

    // The case material, for attachments dropped from the inventory.
    public static Material DefaultMaterial { get; private set; }

    private Transform visual;
    private float bobOffset;
    private PickupLabel label;
    private bool used;

    // Statics outlive Play sessions with domain reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        all.Clear();
        player = null;
        DefaultMaterial = null;
    }

    public static AttachmentPickup Spawn(WeaponAttachment attachment, Vector3 position, Transform parent, Material material)
    {
        if (material != null)
            DefaultMaterial = material;

        GameObject root = new GameObject("Attachment (" + attachment.name + ")");
        root.transform.SetParent(parent, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);

        GameObject kit = new GameObject("Case");
        kit.transform.SetParent(root.transform, false);
        kit.transform.localPosition = Vector3.up * 0.35f;

        Color colour = attachment.colour;
        Vector3 size = new Vector3(0.5f, 0.14f, 0.32f);
        Part(kit.transform, "Body", Vector3.zero, size, material, new Color(0.12f, 0.13f, 0.12f), Color.black);

        // A glowing band round the case and a bar on the lid, in the attachment's colour.
        Part(kit.transform, "Band", Vector3.zero, new Vector3(size.x * 0.25f, size.y + 0.02f, size.z + 0.02f), material, colour * 0.5f, colour * 3f);
        Part(kit.transform, "Lid", new Vector3(0f, size.y / 2f + 0.005f, 0f), new Vector3(size.x * 0.7f, 0.01f, 0.05f), material, colour * 0.5f, colour * 2f);

        AttachmentPickup pickup = root.AddComponent<AttachmentPickup>();
        pickup.Attachment = attachment;
        pickup.visual = kit.transform;
        pickup.bobOffset = Random.value * 10f;
        AddGlow(root.transform, colour);
        pickup.label = PickupLabel.Create(root.transform, 1.6f);
        pickup.label.Compactable = true;
        ItemSprite.Apply(root, SpriteVisuals.Current != null ? SpriteVisuals.Current.items : null, ItemSprites.Attachment(attachment.name), true);
        return pickup;
    }

    // A soft disc of its colour on the floor beneath it, like the other drops, so it shows up in
    // the dark.
    private static void AddGlow(Transform root, Color tint)
    {
        AmmoVisuals visuals = AmmoVisuals.Current;

        if (visuals == null || visuals.glowMaterial == null)
            return;

        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        glow.name = "Glow";
        Destroy(glow.GetComponent<Collider>());
        glow.transform.SetParent(root, false);
        glow.transform.localPosition = Vector3.up * FloorPickups.GlowHeight;
        glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        glow.transform.localScale = Vector3.one * visuals.glowSize;

        Renderer renderer = glow.GetComponent<Renderer>();
        renderer.sharedMaterial = visuals.glowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        Color colour = tint * visuals.glowStrength;
        colour.a = 0.8f;
        block.SetColor("_Color", colour);
        renderer.SetPropertyBlock(block);
    }

    private static void Part(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material, Color baseColour, Color emission)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        Object.Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = size;

        if (material == null)
            return;

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, baseColour);
        block.SetColor(EmissionColorId, emission);
        renderer.SetPropertyBlock(block);
    }

    public Vector3 Position => transform.position;
    public float Reach => UseRange;
    public bool CanTake => !used && player != null;

    private void OnEnable()
    {
        all.Add(this);
        FloorPickups.Register(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
        FloorPickups.Unregister(this);
    }

    private void Update()
    {
        if (visual != null)
        {
            visual.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);
            Vector3 local = visual.localPosition;
            local.y = 0.35f + Mathf.Sin(Time.time * 3f + bobOffset) * 0.06f;
            visual.localPosition = local;
        }

        if (player == null)
            player = PlayerWeapon.Current;

        if (player == null)
            return;

        // Only the nearest one shows its label, so pickups side by side don't overlap.
        float distance = Distance(player.transform.position);
        bool nearest = Nearest(player.transform.position) == this;
        bool inRange = distance <= LabelRange && nearest;

        if (inRange && !label.IsShowing)
            label.Show(LabelText, Attachment.colour);
        else if (!inRange && label.IsShowing)
            label.Hide();

        // Only the nearest pickup in reach takes the press, and a gun pickup in reach comes
        // first (its own label asks for the same button).
        if (FloorPickups.Pressed(this, player))
            TryFit();
    }

    private float Distance(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    private static AttachmentPickup Nearest(Vector3 position)
    {
        AttachmentPickup nearest = null;
        float best = float.MaxValue;

        foreach (AttachmentPickup pickup in all)
        {
            if (pickup.used)
                continue;

            float distance = pickup.Distance(position);

            if (distance < best)
            {
                best = distance;
                nearest = pickup;
            }
        }

        return nearest;
    }

    public enum Take
    {
        Fit,        // onto a gun with that slot free
        Store,      // into the backpack
        Swap,       // onto a gun, swapping the old one off (backpack full)
        None
    }

    // Every carried gun it fits, main first: "FITS AK47 + PISTOL", "FITS PISTOL", or
    // "FITS NO GUN YOU CARRY". Also used by supply crates.
    public static string FitsCarried(PlayerWeapon player, WeaponAttachment attachment)
    {
        List<string> names = new List<string>();

        foreach (PlayerWeapon.Equipped slot in new[] { player.Main, player.Sidearm })
        {
            if (slot.Gun != null && attachment.Fits(slot.Gun))
                names.Add(slot.Name);
        }

        return names.Count > 0 ? "FITS " + string.Join(" + ", names) : "FITS NO GUN YOU CARRY";
    }

    // What taking it would do, and the gun involved if any.
    private Take Decide(out PlayerWeapon.Equipped target)
    {
        return Decide(player, Attachment, out target);
    }

    // What taking an attachment would do for this player (also used by supply crates).
    public static Take Decide(PlayerWeapon player, WeaponAttachment attachment, out PlayerWeapon.Equipped target)
    {
        target = null;

        foreach (PlayerWeapon.Equipped slot in new[] { player.Active, player.Holstered })
        {
            if (slot.Gun != null && attachment.Fits(slot.Gun) && slot.Gun.AttachmentIn(attachment.slot) == null)
            {
                target = slot;
                return Take.Fit;
            }
        }

        if (player.Backpack != null && player.Backpack.HasRoomForAttachment)
            return Take.Store;

        target = player.TargetFor(attachment);

        if (target != null && target.Gun.AttachmentIn(attachment.slot) != attachment)
            return Take.Swap;

        target = null;
        return Take.None;
    }

    private void TryFit()
    {
        Take take = Decide(out PlayerWeapon.Equipped target);

        if (take == Take.None)
        {
            SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-deny", 1, i => ProceduralAudio.AttachmentDeny())), 0.4f);
            return;
        }

        used = true;

        if (take == Take.Store)
        {
            player.Backpack.AddAttachment(Attachment);
            SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("inventory-lift", 2, i => ProceduralAudio.BackpackRustle(300 + i))), 0.35f, 1.1f);
            Destroy(gameObject);
            return;
        }

        WeaponAttachment replaced = target.Gun.Attach(Attachment);
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-fit", 3, ProceduralAudio.AttachmentFit)), 0.6f, Random.Range(0.95f, 1.05f));

        // The old one goes down where this one was.
        if (replaced != null)
            Spawn(replaced, transform.position, transform.parent, MaterialOf(this));

        Destroy(gameObject);
    }

    // The case's material, so a swapped-off attachment looks the same.
    private static Material MaterialOf(AttachmentPickup pickup)
    {
        Renderer renderer = pickup.visual != null ? pickup.visual.GetComponentInChildren<Renderer>() : null;
        return renderer != null ? renderer.sharedMaterial : null;
    }

    private string LabelText()
    {
        WeaponAttachment attachment = Attachment;

        // Which carried guns it fits goes on the first line, so the name tag a far-off label
        // shrinks to (PickupLabel.Compact) still shows it.
        string fitsCarried = player != null ? $"  <alpha=#AA>{FitsCarried(player, attachment)}<alpha=#FF>" : "";
        string text = $"{attachment.name.ToUpperInvariant()}{fitsCarried}  <size=70%><alpha=#99>// {WeaponAttachment.SlotName(attachment.slot)}</size>\n"
            + $"<size=70%>{attachment.Describe()}</size>\n";

        if (player == null)
            return text;

        string button = $"[{GameInput.Label(GameInput.Interact)}]";

        switch (Decide(out PlayerWeapon.Equipped target))
        {
            case Take.Fit:
                return text + $"<size=80%><color=#FFFFFF>{button} FIT TO {target.Name}</color></size>";

            case Take.Store:
                string note = player.TargetFor(attachment) != null ? "SLOT TAKEN" : attachment.FitDescription();
                return text + $"<size=80%><color=#FFFFFF>{button} TAKE</color>  <alpha=#88><size=85%>{note}</size></size>";

            case Take.Swap:
                WeaponAttachment current = target.Gun.AttachmentIn(attachment.slot);
                return text + $"<size=80%><color=#FFFFFF>{button} FIT TO {target.Name}</color>  <alpha=#88>(BACKPACK FULL // SWAPS {current.name.ToUpperInvariant()})</size>";

            default:
                return text + "<size=70%><alpha=#88>BACKPACK FULL</size>";
        }
    }
}
