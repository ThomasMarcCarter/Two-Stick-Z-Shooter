using UnityEngine;

// A station in the hideout that upgrades are bought at: the generator, the mechanic's workbench,
// the medic's med station or the quartermaster's ammo press; or the trader's stall, where the
// StationScreen is a shop instead. Each is a piece of kit built from
// blocks with a small glowing control screen and a soft light in the station's colour (with the
// sprite art, drawn as its pixel sprite: FurnitureSprite, "Station<name>"). Walking
// up to it shows a label (its name and levels); pressing Interact opens the StationScreen for it.
// Built in code by the FloorGenerator when it lays out the hideout.
public partial class StationConsole : MonoBehaviour, FloorPickups.IPickup
{
    private static readonly Color ScreenGlow = UiColours.Phosphor;
    private static readonly Color Metal = new Color(0.2f, 0.21f, 0.22f);
    private static readonly Color DarkMetal = new Color(0.1f, 0.1f, 0.11f);

    private HideoutStation station;
    private PickupLabel label;
    private PlayerWeapon playerWeapon;
    private Transform player;
    private Renderer screen;
    private Transform moving;      // the press's ram, the generator's flywheel
    private Light glow;

    public HideoutStation Station => station;
    public Vector3 Position => transform.position;
    public float Reach => 2.4f;
    public bool CanTake => !StationScreen.IsOpen;

    // `facing` is the way the front of the station looks (into the room).
    public static StationConsole Create(HideoutStation station, Vector3 position, Vector3 facing, Transform parent)
    {
        GameObject root = new GameObject(Stations.Name(station));
        root.transform.SetParent(parent, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);
        root.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);

        StationConsole console = root.AddComponent<StationConsole>();
        console.station = station;
        StationMarker.Add(root, station, false);
        Material material = ConsumablePickup.DefaultMaterial;
        Color accent = Stations.Colour(station);
        Transform t = root.transform;
        BoxCollider body = root.AddComponent<BoxCollider>();

        switch (station)
        {
            case HideoutStation.Generator: BuildGenerator(console, t, body, material, accent); break;
            case HideoutStation.Workbench: BuildWorkbench(console, t, body, material, accent); break;
            case HideoutStation.MedStation: BuildMedStation(console, t, body, material, accent); break;
            case HideoutStation.IntelDesk: BuildIntelDesk(console, t, body, material, accent); break;
            case HideoutStation.TradePost: BuildTradePost(console, t, body, material, accent); break;
            case HideoutStation.Kitchen: BuildKitchen(console, t, body, material, accent); break;
            default: BuildAmmoPress(console, t, body, material, accent); break;
        }

        UseModel(console, station);

        GameObject light = new GameObject("Station Light");
        light.transform.SetParent(t, false);
        light.transform.localPosition = new Vector3(0f, 1.6f, 1f);
        console.glow = light.AddComponent<Light>();
        console.glow.type = LightType.Point;
        console.glow.color = Color.Lerp(accent, ScreenGlow, 0.4f);
        console.glow.range = 5.5f;
        console.glow.intensity = 2.5f;
        console.glow.shadows = LightShadows.None;

        console.label = PickupLabel.Create(t, 2.4f);

        // With the sprite art, drawn as its pixel sprite over the footprint of its body.
        Vector3 middle = t.TransformPoint(body.center);
        FurnitureSprite.Apply(root, "Station" + station, facing, body.size, new Vector3(middle.x, 0f, middle.z));
        return console;
    }

    // With a model for this station (StationVisuals), it replaces the static primitives; the
    // screen/lamp and the moving part stay, as the model has none.
    private static void UseModel(StationConsole console, HideoutStation station)
    {
        GameObject prefab = StationVisuals.Current != null ? StationVisuals.Current.ModelFor(station) : null;

        if (prefab == null)
            return;

        Transform root = console.transform;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);

            if ((console.screen == null || child != console.screen.transform) && child != console.moving)
                Destroy(child.gameObject);
        }

        GameObject model = Instantiate(prefab, root, false);
        model.name = "Model";

        foreach (Collider collider in model.GetComponentsInChildren<Collider>())
            Destroy(collider);
    }

    private static Renderer Part(Transform parent, string name, Vector3 position, Vector3 size, Material material, Color colour,
        Color emission, bool shadows, PrimitiveType shape = PrimitiveType.Cube)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = name;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = shape == PrimitiveType.Cylinder ? new Vector3(size.x, size.y / 2f, size.z) : size;

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
        Animate();

        if (player == null)
        {
            playerWeapon = PlayerWeapon.Current;
            player = playerWeapon != null ? playerWeapon.transform : null;

            if (player == null)
                return;
        }

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        bool inRange = offset.magnitude <= Reach + 1.5f && !StationScreen.IsOpen;

        if (inRange && !label.IsShowing)
            label.Show(LabelText, Stations.Colour(station));
        else if (!inRange && label.IsShowing)
            label.Hide();

        if (FloorPickups.Pressed(this, playerWeapon))
            StationScreen.Open(station);
    }

    // A slow flicker on the screen; the flywheel turns faster with each generator level, and the
    // press's ram pumps.
    private void Animate()
    {
        if (screen != null && station != HideoutStation.Workbench && station != HideoutStation.TradePost)
            ConsumableModel.Tint(screen, ScreenGlow * 0.3f, ScreenGlow * (2f + Mathf.PerlinNoise(Time.time * 3f, transform.position.x) * 0.6f));

        if (moving == null)
            return;

        if (station == HideoutStation.Generator)
        {
            float speed = 90f + 240f * MetaProgress.Level(MetaUpgrade.Generator);
            moving.Rotate(Vector3.up, speed * Time.deltaTime, Space.Self);

            if (glow != null)
                glow.intensity = 2.2f + Mathf.PerlinNoise(Time.time * 8f, 0f) * 0.6f;
        }
        else
        {
            Vector3 local = moving.localPosition;
            local.y = 1.45f + Mathf.Max(0f, Mathf.Sin(Time.time * 2.2f)) * -0.15f;
            moving.localPosition = local;
        }
    }

    private string LabelText()
    {
        if (station == HideoutStation.TradePost)
        {
            int stock = 0;

            foreach (TraderStock.Offer offer in TraderStock.Offers)
                stock += offer.Left;

            return $"{Stations.Name(station)}  <size=70%><alpha=#99>// {stock} ITEMS IN STOCK</size>\n"
                + $"<size=80%><color=#FFFFFF>{InputHint.Format("[{Interact}]")} TRADE</color></size>";
        }

        int levels = 0;
        int max = 0;

        foreach (MetaUpgrade upgrade in Stations.Upgrades(station))
        {
            levels += MetaProgress.Level(upgrade);
            max += MetaUpgrades.MaxLevel(upgrade);
        }

        return $"{Stations.Name(station)}  <size=70%><alpha=#99>// {levels}/{max} BUILT</size>\n"
            + $"<size=80%><color=#FFFFFF>{InputHint.Format("[{Interact}]")} UPGRADE</color></size>";
    }
}
