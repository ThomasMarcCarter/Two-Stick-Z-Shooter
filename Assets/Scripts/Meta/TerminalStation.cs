using UnityEngine;

// The upgrade terminal's desk in the hideout: a desk with a glowing green monitor and a soft
// light. Walking up to it shows a label; pressing Interact opens the HideoutTerminal screen.
// Built in code by the FloorGenerator when it lays out the hideout.
public class TerminalStation : MonoBehaviour, FloorPickups.IPickup
{
    private static readonly Color ScreenGlow = new Color(0.35f, 1f, 0.55f);

    private PickupLabel label;
    private PlayerWeapon playerWeapon;
    private Transform player;
    private Renderer screen;

    public Vector3 Position => transform.position;
    public float Reach => 2.2f;
    public bool CanTake => !HideoutTerminal.IsOpen;

    // `facing` is the way the screen looks (into the room).
    public static TerminalStation Create(Vector3 position, Vector3 facing, Transform parent)
    {
        GameObject root = new GameObject("Upgrade Terminal");
        root.transform.SetParent(parent, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);
        root.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);

        Material material = ConsumablePickup.DefaultMaterial;
        TerminalStation station = root.AddComponent<TerminalStation>();

        Part(root.transform, "Desk", new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.9f, 0.8f), material, new Color(0.18f, 0.19f, 0.2f), Color.black, true);
        Part(root.transform, "Keyboard", new Vector3(0f, 0.92f, 0.12f), new Vector3(0.8f, 0.04f, 0.25f), material, new Color(0.1f, 0.1f, 0.1f), ScreenGlow * 0.15f, false);
        Part(root.transform, "Monitor", new Vector3(0f, 1.25f, -0.15f), new Vector3(1.1f, 0.7f, 0.35f), material, new Color(0.12f, 0.13f, 0.13f), Color.black, true);
        station.screen = Part(root.transform, "Screen", new Vector3(0f, 1.25f, 0.03f), new Vector3(0.95f, 0.55f, 0.02f), material, ScreenGlow * 0.3f, ScreenGlow * 2.5f, false);

        GameObject light = new GameObject("Screen Light");
        light.transform.SetParent(root.transform, false);
        light.transform.localPosition = new Vector3(0f, 1.3f, 0.8f);
        Light glow = light.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = ScreenGlow;
        glow.range = 5f;
        glow.intensity = 2.5f;
        glow.shadows = LightShadows.None;

        // Solid, so the player walks up to it rather than through it.
        BoxCollider body = root.AddComponent<BoxCollider>();
        body.center = new Vector3(0f, 0.6f, -0.05f);
        body.size = new Vector3(1.8f, 1.2f, 0.9f);

        station.label = PickupLabel.Create(root.transform, 2.2f);
        return station;
    }

    private static Renderer Part(Transform parent, string name, Vector3 position, Vector3 size, Material material, Color colour, Color emission, bool shadows)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

        if (material != null)
        {
            renderer.sharedMaterial = material;
            ConsumableModel.Tint(renderer, colour, emission);
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
        if (player == null)
        {
            playerWeapon = FindAnyObjectByType<PlayerWeapon>();
            player = playerWeapon != null ? playerWeapon.transform : null;

            if (player == null)
                return;
        }

        // A slow flicker on the screen, like an old monitor.
        if (screen != null)
            ConsumableModel.Tint(screen, ScreenGlow * 0.3f, ScreenGlow * (2.2f + Mathf.PerlinNoise(Time.time * 3f, 0f) * 0.6f));

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        bool inRange = offset.magnitude <= Reach + 1.5f && !HideoutTerminal.IsOpen;

        if (inRange && !label.IsShowing)
            label.Show(LabelText, ScreenGlow);
        else if (!inRange && label.IsShowing)
            label.Hide();

        if (FloorPickups.Pressed(this, playerWeapon))
            HideoutTerminal.Open();
    }

    private string LabelText()
    {
        return $"UPGRADE TERMINAL  <size=70%><alpha=#99>// {MetaProgress.Scrap} SCRAP</size>\n"
            + $"<size=80%><color=#FFFFFF>{InputHint.Format("[{Interact}]")} ACCESS</color></size>";
    }
}
