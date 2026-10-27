using UnityEngine;
using UnityEngine.AI;

// A supply uplink: a field radio console that turns up in the boss room once the boss is dead,
// against a wall away from the door. Interact at it to send the scrap earned on the run so far
// and every crafting material in the backpack home to the hideout, where they're banked for good
// (MetaProgress); anything not sent home is lost if the player dies. It can be used again for
// anything earned or picked up afterwards.
// Its label lists what's waiting to go, its screen glows (brighter for a moment after sending),
// the beacon on its aerial blinks, and the radio confirms what the hideout received. The body is
// the Uplink model (FloorGenerator), or a plain block without one; with the sprite art it's drawn
// as its pixel sprite (FurnitureSprite), screen and beacon included.
public class UplinkTerminal : MonoBehaviour, FloorPickups.IPickup
{
    private static readonly Color ScreenColour = new Color(0.35f, 1f, 0.55f);
    private static readonly Color BeaconColour = new Color(1f, 0.55f, 0.15f);

    private PickupLabel label;
    private PlayerWeapon playerWeapon;
    private FloorGenerator floor;
    private Renderer screen;
    private Renderer beacon;
    private Light lamp;
    private float sentAt = -100f;
    private string lastSent;

    public Vector3 Position => transform.position;
    public float Reach => 2f;
    public bool CanTake => !StationScreen.IsOpen;

    public static UplinkTerminal Create(GameObject model, Vector3 position, Vector3 facing, Transform parent)
    {
        GameObject root = new GameObject("Uplink Terminal");
        root.transform.SetParent(parent, true);
        root.transform.SetPositionAndRotation(new Vector3(position.x, 0f, position.z), Quaternion.LookRotation(facing, Vector3.up));

        UplinkTerminal terminal = root.AddComponent<UplinkTerminal>();
        Material material = ConsumablePickup.DefaultMaterial;

        if (model != null)
        {
            GameObject body = Instantiate(model, root.transform, false);
            body.name = "Model";

            foreach (Collider collider in body.GetComponentsInChildren<Collider>())
                Destroy(collider);
        }
        else
        {
            terminal.Part("Body", PrimitiveType.Cube, new Vector3(0f, 0.68f, 0f), new Vector3(0.95f, 1.3f, 0.5f), material, new Color(0.27f, 0.31f, 0.19f));
        }

        // The screen in the console's bezel, and a beacon on top of the whip aerial.
        terminal.screen = terminal.Part("Screen", PrimitiveType.Quad, new Vector3(0f, 1.04f, 0.258f), new Vector3(0.6f, 0.36f, 1f), material,
            ScreenColour * 0.3f, Quaternion.Euler(0f, 180f, 0f));
        terminal.beacon = terminal.Part("Beacon", PrimitiveType.Sphere, new Vector3(-0.35f, 1.87f, -0.18f), Vector3.one * 0.07f, material, BeaconColour);

        terminal.lamp = new GameObject("Light").AddComponent<Light>();
        terminal.lamp.transform.SetParent(root.transform, false);
        terminal.lamp.transform.localPosition = new Vector3(0f, 1.3f, 0.8f);
        terminal.lamp.type = LightType.Point;
        terminal.lamp.color = ScreenColour;
        terminal.lamp.range = 4f;
        terminal.lamp.intensity = 1.5f;
        terminal.lamp.shadows = LightShadows.None;

        // Solid, and zombies path round it (the boss room's navmesh was baked long before).
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.68f, 0f);
        box.size = new Vector3(1f, 1.36f, 0.62f);

        NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = box.center;
        obstacle.size = box.size;
        obstacle.carving = true;

        terminal.label = PickupLabel.Create(root.transform, 2.5f);

        // With the sprite art, drawn as its pixel sprite (screen and blinking beacon included).
        Vector3 middle = root.transform.TransformPoint(box.center);
        FurnitureSprite.Apply(root, "Uplink", facing, box.size, new Vector3(middle.x, 0f, middle.z));
        return terminal;
    }

    private Renderer Part(string name, PrimitiveType shape, Vector3 position, Vector3 size, Material material, Color colour,
        Quaternion? rotation = null)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = name;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(transform, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation ?? Quaternion.identity;
        part.transform.localScale = size;

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (material != null)
        {
            renderer.sharedMaterial = material;
            ConsumableModel.Tint(renderer, colour, colour);
        }

        return renderer;
    }

    private void OnEnable()
    {
        FloorPickups.Register(this);
    }

    private void OnDisable()
    {
        FloorPickups.Unregister(this);
    }

    private void Update()
    {
        Animate();

        if (playerWeapon == null)
        {
            playerWeapon = PlayerWeapon.Current;

            if (playerWeapon == null)
                return;
        }

        Vector3 offset = playerWeapon.transform.position - transform.position;
        offset.y = 0f;
        bool showLabel = offset.magnitude <= Reach + 3f && !StationScreen.IsOpen;

        if (showLabel && !label.IsShowing)
            label.Show(LabelText, ScreenColour);
        else if (!showLabel && label.IsShowing)
            label.Hide();

        if (FloorPickups.Pressed(this, playerWeapon))
            Send();
    }

    // The screen flickers softly (bright for a moment after a send) and the beacon blinks.
    private void Animate()
    {
        float sinceSend = Time.time - sentAt;
        float flash = Mathf.Clamp01(1f - sinceSend / 1.5f);
        float flicker = 0.85f + Mathf.PerlinNoise(Time.time * 6f, 0.3f) * 0.3f;

        if (screen != null)
            ConsumableModel.Tint(screen, ScreenColour * 0.3f, ScreenColour * (0.6f * flicker + flash * 1.5f));

        if (beacon != null)
        {
            bool on = Mathf.Repeat(Time.time, sinceSend < 1.5f ? 0.25f : 1.2f) < 0.12f;
            ConsumableModel.Tint(beacon, BeaconColour, on ? BeaconColour * 3f : Color.black);
        }

        if (lamp != null)
            lamp.intensity = 1.2f * flicker + flash * 2f;
    }

    private int[] Carried()
    {
        return playerWeapon != null ? Backpack.For(playerWeapon.gameObject).MaterialCounts() : new int[Materials.All.Length];
    }

    // The run's scrap not sent home yet.
    private int CarriedScrap()
    {
        if (floor == null)
            floor = FindAnyObjectByType<FloorGenerator>();

        return floor != null ? MetaProgress.CarriedScrap(floor.FloorsCleared, floor.EnemiesKilled) : 0;
    }

    private string LabelText()
    {
        string header = "SUPPLY UPLINK  <size=70%><alpha=#99>// SENDS SCRAP AND MATERIALS TO THE HIDEOUT</size>\n";

        if (Time.time - sentAt < 3f && lastSent != null)
            return header + $"<size=70%><color={UiColours.AmberHex}>RECEIVED AT THE HIDEOUT // {lastSent}</color></size>";

        int scrap = CarriedScrap();
        string carried = Materials.Describe(Carried());

        if (carried.Length == 0 && scrap <= 0)
            return header + "<size=70%><alpha=#88>NOTHING TO SEND // NO SCRAP OR MATERIALS CARRIED</size>";

        string scrapText = scrap > 0 ? $"<color={UiColours.AmberHex}>{scrap} SCRAP</color>" : "";
        string both = scrapText.Length > 0 && carried.Length > 0 ? scrapText + "  " + carried : scrapText + carried;
        return header + $"<size=70%>{both}</size>\n"
            + $"<size=80%><color=#FFFFFF>{InputHint.Format("[{Interact}]")} SEND HOME</color></size>";
    }

    // The run's scrap so far and everything in the backpack goes home and is banked.
    private void Send()
    {
        if (floor == null)
            floor = FindAnyObjectByType<FloorGenerator>();

        int scrap = floor != null ? MetaProgress.SendScrapHome(floor.FloorsCleared, floor.EnemiesKilled) : 0;
        int[] sent = Backpack.For(playerWeapon.gameObject).TakeAllMaterials();
        string plain = Materials.Describe(sent, false);

        if (plain.Length == 0 && scrap <= 0)
        {
            SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-deny", 1, i => ProceduralAudio.AttachmentDeny())), 0.5f);
            return;
        }

        MetaProgress.AddMaterials(sent);
        sentAt = Time.time;
        string coloured = Materials.Describe(sent);
        lastSent = scrap > 0 ? $"{scrap} SCRAP" + (coloured.Length > 0 ? "  " + coloured : "") : coloured;
        plain = scrap > 0 ? $"{scrap} SCRAP" + (plain.Length > 0 ? ", " + plain : "") : plain;
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("room.cleared", 1, i => ProceduralAudio.RoomCleared())), 0.45f, 1.4f);
        RadioMessage.Show("SUPPLY UPLINK", "RECEIVED AT THE HIDEOUT: " + plain);
    }
}
