using UnityEngine;

// A station in the hideout that upgrades are bought at: the generator, the mechanic's workbench,
// the medic's med station or the quartermaster's ammo press; or the trader's stall, where the
// StationScreen is a shop instead. Each is a piece of kit built from
// blocks with a small glowing control screen and a soft light in the station's colour. Walking
// up to it shows a label (its name and levels); pressing Interact opens the StationScreen for it.
// Built in code by the FloorGenerator when it lays out the hideout.
public class StationConsole : MonoBehaviour, FloorPickups.IPickup
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
        Material material = ConsumablePickup.DefaultMaterial;
        Color accent = Stations.Colour(station);
        Transform t = root.transform;
        BoxCollider body = root.AddComponent<BoxCollider>();

        switch (station)
        {
            case HideoutStation.Generator:
                // A diesel generator on skids: a long engine block, a radiator grille, exhaust stack
                // and a flywheel turning at the side, with a control panel on the front.
                Part(t, "Skid", new Vector3(0f, 0.08f, 0f), new Vector3(2.4f, 0.16f, 1.2f), material, DarkMetal, Color.black, true);
                Part(t, "Engine", new Vector3(0f, 0.65f, -0.05f), new Vector3(2.1f, 1f, 1f), material, new Color(0.35f, 0.3f, 0.12f), Color.black, true);
                Part(t, "Radiator", new Vector3(-1.1f, 0.7f, -0.05f), new Vector3(0.2f, 1.1f, 1.05f), material, DarkMetal, accent * 0.15f, true);
                Part(t, "Exhaust", new Vector3(0.7f, 1.45f, -0.25f), new Vector3(0.14f, 0.7f, 0.14f), material, DarkMetal, Color.black, true, PrimitiveType.Cylinder);
                Part(t, "Panel", new Vector3(0.45f, 0.85f, 0.47f), new Vector3(0.7f, 0.5f, 0.08f), material, Metal, Color.black, false);
                console.screen = Part(t, "Screen", new Vector3(0.45f, 0.88f, 0.52f), new Vector3(0.55f, 0.3f, 0.02f), material, ScreenGlow * 0.3f, ScreenGlow * 2.2f, false);
                console.moving = Part(t, "Flywheel", new Vector3(1.12f, 0.6f, -0.05f), new Vector3(0.7f, 0.06f, 0.7f), material, Metal, Color.black, true, PrimitiveType.Cylinder).transform;
                console.moving.localRotation = Quaternion.Euler(0f, 0f, 90f);
                body.center = new Vector3(0f, 0.6f, -0.05f);
                body.size = new Vector3(2.6f, 1.2f, 1.2f);
                break;

            case HideoutStation.Workbench:
                // A heavy bench with a vice and tools, and a pegboard behind it.
                Part(t, "Bench", new Vector3(0f, 0.85f, 0f), new Vector3(2.2f, 0.1f, 0.9f), material, new Color(0.4f, 0.28f, 0.16f), Color.black, true);
                Part(t, "Leg", new Vector3(-1f, 0.4f, -0.35f), new Vector3(0.1f, 0.8f, 0.1f), material, DarkMetal, Color.black, true);
                Part(t, "Leg", new Vector3(1f, 0.4f, -0.35f), new Vector3(0.1f, 0.8f, 0.1f), material, DarkMetal, Color.black, true);
                Part(t, "Leg", new Vector3(-1f, 0.4f, 0.35f), new Vector3(0.1f, 0.8f, 0.1f), material, DarkMetal, Color.black, true);
                Part(t, "Leg", new Vector3(1f, 0.4f, 0.35f), new Vector3(0.1f, 0.8f, 0.1f), material, DarkMetal, Color.black, true);
                Part(t, "Shelf", new Vector3(0f, 0.25f, 0f), new Vector3(2f, 0.05f, 0.8f), material, new Color(0.3f, 0.22f, 0.13f), Color.black, false);
                Part(t, "Pegboard", new Vector3(0f, 1.5f, -0.42f), new Vector3(2.2f, 1.1f, 0.05f), material, new Color(0.45f, 0.38f, 0.28f), Color.black, true);
                Part(t, "Vice", new Vector3(0.8f, 1f, 0.25f), new Vector3(0.2f, 0.2f, 0.25f), material, accent * 0.6f, Color.black, true);
                Part(t, "Toolbox", new Vector3(-0.6f, 1.02f, -0.05f), new Vector3(0.5f, 0.25f, 0.25f), material, new Color(0.7f, 0.15f, 0.1f), Color.black, true);
                Part(t, "Wrench", new Vector3(-0.5f, 1.6f, -0.38f), new Vector3(0.06f, 0.45f, 0.02f), material, Metal, Color.black, false);
                Part(t, "Saw", new Vector3(0.1f, 1.55f, -0.38f), new Vector3(0.5f, 0.15f, 0.02f), material, Metal, Color.black, false);
                Part(t, "Hammer", new Vector3(0.55f, 1.6f, -0.38f), new Vector3(0.05f, 0.35f, 0.02f), material, new Color(0.4f, 0.28f, 0.16f), Color.black, false);
                console.screen = Part(t, "Lamp", new Vector3(0f, 2.1f, -0.3f), new Vector3(0.6f, 0.06f, 0.15f), material, accent * 0.3f, accent * 2f, false);
                body.center = new Vector3(0f, 0.6f, -0.05f);
                body.size = new Vector3(2.3f, 1.2f, 1f);
                break;

            case HideoutStation.MedStation:
                // A medical cabinet with a red cross, a cot beside it and an IV stand.
                Part(t, "Cabinet", new Vector3(-0.6f, 0.9f, -0.15f), new Vector3(1f, 1.8f, 0.55f), material, new Color(0.75f, 0.78f, 0.78f), Color.black, true);
                Part(t, "Cross H", new Vector3(-0.6f, 1.35f, 0.14f), new Vector3(0.4f, 0.12f, 0.02f), material, new Color(0.85f, 0.1f, 0.1f), new Color(0.4f, 0f, 0f), false);
                Part(t, "Cross V", new Vector3(-0.6f, 1.35f, 0.14f), new Vector3(0.12f, 0.4f, 0.02f), material, new Color(0.85f, 0.1f, 0.1f), new Color(0.4f, 0f, 0f), false);
                Part(t, "Cot", new Vector3(0.75f, 0.45f, 0.25f), new Vector3(0.8f, 0.12f, 1.9f), material, new Color(0.45f, 0.5f, 0.42f), Color.black, true);
                Part(t, "Cot Frame", new Vector3(0.75f, 0.2f, 0.25f), new Vector3(0.75f, 0.4f, 1.8f), material, DarkMetal, Color.black, true);
                Part(t, "Pillow", new Vector3(0.75f, 0.55f, -0.5f), new Vector3(0.6f, 0.1f, 0.3f), material, new Color(0.8f, 0.8f, 0.78f), Color.black, false);
                Part(t, "IV Pole", new Vector3(0.15f, 0.9f, -0.5f), new Vector3(0.04f, 0.9f, 0.04f), material, Metal, Color.black, false, PrimitiveType.Cylinder);
                Part(t, "IV Bag", new Vector3(0.15f, 1.65f, -0.45f), new Vector3(0.16f, 0.24f, 0.06f), material, accent * 0.5f, accent * 0.8f, false);
                console.screen = Part(t, "Screen", new Vector3(-0.6f, 0.75f, 0.14f), new Vector3(0.6f, 0.35f, 0.02f), material, ScreenGlow * 0.3f, ScreenGlow * 2.2f, false);
                body.center = new Vector3(0.05f, 0.6f, 0.1f);
                body.size = new Vector3(2.3f, 1.2f, 2f);
                break;

            case HideoutStation.IntelDesk:
                // A planning table with a glowing map of the floors, pins, a field radio and a
                // desk lamp.
                Part(t, "Table", new Vector3(0f, 0.8f, 0f), new Vector3(2.6f, 0.1f, 1.4f), material, new Color(0.35f, 0.25f, 0.15f), Color.black, true);
                Part(t, "Table Base", new Vector3(0f, 0.4f, 0f), new Vector3(2.2f, 0.8f, 1f), material, new Color(0.2f, 0.15f, 0.1f), Color.black, true);
                console.screen = Part(t, "Map", new Vector3(-0.2f, 0.86f, 0f), new Vector3(1.8f, 0.02f, 1f), material, ScreenGlow * 0.25f, ScreenGlow * 1.2f, false);
                Part(t, "Pin", new Vector3(-0.6f, 0.92f, 0.2f), new Vector3(0.05f, 0.1f, 0.05f), material, new Color(0.9f, 0.15f, 0.1f), new Color(0.6f, 0f, 0f), false);
                Part(t, "Pin", new Vector3(0.3f, 0.92f, -0.25f), new Vector3(0.05f, 0.1f, 0.05f), material, new Color(0.9f, 0.15f, 0.1f), new Color(0.6f, 0f, 0f), false);
                Part(t, "Pin", new Vector3(0.1f, 0.92f, 0.3f), new Vector3(0.05f, 0.1f, 0.05f), material, accent, accent, false);
                Part(t, "Radio", new Vector3(1f, 1.0f, -0.35f), new Vector3(0.4f, 0.3f, 0.25f), material, new Color(0.2f, 0.22f, 0.18f), Color.black, false);
                Part(t, "Aerial", new Vector3(1.12f, 1.35f, -0.4f), new Vector3(0.02f, 0.5f, 0.02f), material, DarkMetal, Color.black, false, PrimitiveType.Cylinder);
                Part(t, "Lamp", new Vector3(-1.1f, 1.05f, -0.5f), new Vector3(0.12f, 0.4f, 0.12f), material, DarkMetal, Color.black, false, PrimitiveType.Cylinder);
                body.center = new Vector3(0f, 0.6f, 0f);
                body.size = new Vector3(2.6f, 1.2f, 1.4f);
                break;

            case HideoutStation.TradePost:
                // A market stall: a counter of goods under a striped awning on poles, a cash box
                // and a lamp hanging over it.
                Part(t, "Counter", new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 1f, 0.7f), material, new Color(0.35f, 0.25f, 0.15f), Color.black, true);
                Part(t, "Counter Top", new Vector3(0f, 1.03f, 0.02f), new Vector3(2.5f, 0.06f, 0.8f), material, new Color(0.45f, 0.33f, 0.2f), Color.black, true);
                Part(t, "Cash Box", new Vector3(0.85f, 1.13f, 0.05f), new Vector3(0.35f, 0.15f, 0.25f), material, DarkMetal, Color.black, false);
                Part(t, "Medkit", new Vector3(-0.8f, 1.15f, 0.1f), new Vector3(0.3f, 0.18f, 0.22f), material, new Color(0.8f, 0.15f, 0.12f), new Color(0.2f, 0f, 0f), false);
                Part(t, "Grenade", new Vector3(-0.35f, 1.12f, 0.15f), new Vector3(0.12f, 0.14f, 0.12f), material, new Color(0.25f, 0.32f, 0.18f), Color.black, false, PrimitiveType.Sphere);
                Part(t, "Bottle", new Vector3(0.05f, 1.2f, 0.05f), new Vector3(0.08f, 0.28f, 0.08f), material, new Color(0.2f, 0.45f, 0.25f), new Color(0.02f, 0.08f, 0.03f), false, PrimitiveType.Cylinder);
                Part(t, "Bottle", new Vector3(0.2f, 1.2f, 0.12f), new Vector3(0.08f, 0.28f, 0.08f), material, new Color(0.45f, 0.3f, 0.12f), new Color(0.08f, 0.04f, 0f), false, PrimitiveType.Cylinder);
                Part(t, "Pole", new Vector3(-1.2f, 1.2f, -0.45f), new Vector3(0.07f, 2.4f, 0.07f), material, DarkMetal, Color.black, true, PrimitiveType.Cylinder);
                Part(t, "Pole", new Vector3(1.2f, 1.2f, -0.45f), new Vector3(0.07f, 2.4f, 0.07f), material, DarkMetal, Color.black, true, PrimitiveType.Cylinder);
                for (int i = 0; i < 5; i++)
                    Part(t, "Awning", new Vector3(-1f + i * 0.5f, 2.35f, 0f), new Vector3(0.5f, 0.05f, 1.1f), material,
                        i % 2 == 0 ? accent * 0.7f : new Color(0.75f, 0.72f, 0.65f), Color.black, false);
                Part(t, "Shelf", new Vector3(0f, 1.6f, -0.5f), new Vector3(2.2f, 0.06f, 0.3f), material, new Color(0.3f, 0.22f, 0.13f), Color.black, false);
                Part(t, "Crate", new Vector3(-0.6f, 1.8f, -0.5f), new Vector3(0.45f, 0.35f, 0.28f), material, new Color(0.3f, 0.35f, 0.2f), Color.black, false);
                Part(t, "Tins", new Vector3(0.4f, 1.72f, -0.5f), new Vector3(0.5f, 0.18f, 0.2f), material, new Color(0.6f, 0.6f, 0.62f), Color.black, false);
                console.screen = Part(t, "Lamp", new Vector3(0f, 2.15f, 0.1f), new Vector3(0.25f, 0.15f, 0.25f), material, accent * 0.4f, accent * 2.2f, false, PrimitiveType.Sphere);
                body.center = new Vector3(0f, 0.6f, -0.1f);
                body.size = new Vector3(2.6f, 1.2f, 1f);
                break;

            default:
                // A reloading press: a frame with a ram that pumps up and down, ammo crates beside it.
                Part(t, "Base", new Vector3(0f, 0.45f, 0f), new Vector3(1.2f, 0.9f, 0.8f), material, new Color(0.25f, 0.3f, 0.2f), Color.black, true);
                Part(t, "Column", new Vector3(0f, 1.45f, -0.25f), new Vector3(0.2f, 1.1f, 0.2f), material, DarkMetal, Color.black, true);
                Part(t, "Head", new Vector3(0f, 2f, -0.05f), new Vector3(0.5f, 0.25f, 0.6f), material, DarkMetal, Color.black, true);
                console.moving = Part(t, "Ram", new Vector3(0f, 1.5f, 0.1f), new Vector3(0.14f, 0.6f, 0.14f), material, Metal, Color.black, false, PrimitiveType.Cylinder).transform;
                Part(t, "Die", new Vector3(0f, 0.95f, 0.1f), new Vector3(0.3f, 0.1f, 0.3f), material, accent * 0.5f, accent * 0.3f, false);
                Part(t, "Crate", new Vector3(1.05f, 0.25f, 0.1f), new Vector3(0.7f, 0.5f, 0.5f), material, new Color(0.3f, 0.35f, 0.2f), Color.black, true);
                Part(t, "Crate", new Vector3(1.05f, 0.65f, 0.1f), new Vector3(0.6f, 0.3f, 0.45f), material, new Color(0.32f, 0.36f, 0.22f), Color.black, true);
                Part(t, "Shells", new Vector3(-0.85f, 0.95f, 0.1f), new Vector3(0.35f, 0.1f, 0.35f), material, new Color(0.7f, 0.55f, 0.2f), new Color(0.2f, 0.15f, 0f), false);
                console.screen = Part(t, "Screen", new Vector3(-0.3f, 0.6f, 0.41f), new Vector3(0.45f, 0.28f, 0.02f), material, ScreenGlow * 0.3f, ScreenGlow * 2.2f, false);
                body.center = new Vector3(0.25f, 0.6f, 0.05f);
                body.size = new Vector3(1.8f, 1.2f, 0.9f);
                break;
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
            playerWeapon = FindAnyObjectByType<PlayerWeapon>();
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
