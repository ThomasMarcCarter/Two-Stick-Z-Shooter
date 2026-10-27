using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// A military supply crate holding a room's haul: the treasure room's (in its middle) and each
// boss's (dropped in the boss room once it dies, clear of the hatch and the walls). Instead of
// a pile of pickups on the floor, the player walks up, presses Interact, and picks what they
// want on the crate screen (CrateScreen); the rest stays inside for later. Anything a pick
// pushes out of the player's hands (the gun it swaps for, an attachment swapped off) goes back
// in the crate. Its amber lamp blinks until it's been opened, and the lid stays up once it
// has. Once it's been emptied it sinks into the floor and is gone. The body is built from primitives in the shared pickup material, olive with steel straps.
public class SupplyCrate : MonoBehaviour, FloorPickups.IPickup
{
    private static readonly Color BodyColour = new Color(0.25f, 0.29f, 0.17f);
    private static readonly Color StrapColour = new Color(0.32f, 0.33f, 0.34f);
    private static readonly Color LampColour = new Color(1f, 0.6f, 0.15f);
    private const float LidOpenAngle = 105f;

    private readonly List<CrateItem> items = new List<CrateItem>();
    private PickupLabel label;
    private PlayerWeapon playerWeapon;
    private Transform lid;
    private Renderer lamp;
    private Light glow;
    private bool opened;
    private ItemSprite sprite;
    private FurnitureSprite furnitureSprite;
    private bool vanishing;
    private float lidAngle;

    public string Title { get; private set; } = "SUPPLY CRATE";
    public IReadOnlyList<CrateItem> Items => items;
    public bool Opened => opened;
    public bool Holds(CrateItem item) => items.Contains(item);

    public Vector3 Position => transform.position;
    public float Reach => 1.9f;
    public bool CanTake => !vanishing && !CrateScreen.IsOpen && !StationScreen.IsOpen;
    public bool IsEmpty => items.Count == 0;

    // The contents changed (something taken or put back).
    public event Action Changed;

    public static SupplyCrate Create(string title, Vector3 position, Vector3 facing, Transform parent)
    {
        GameObject root = new GameObject("Supply Crate");
        root.transform.SetParent(parent, true);
        facing.y = 0f;
        root.transform.SetPositionAndRotation(new Vector3(position.x, 0f, position.z),
            Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing.normalized : Vector3.back, Vector3.up));

        SupplyCrate crate = root.AddComponent<SupplyCrate>();
        crate.Title = title;
        crate.Build();
        return crate;
    }

    private void Build()
    {
        Material material = ConsumablePickup.DefaultMaterial;
        const float width = 1.3f;
        const float depth = 0.8f;
        const float height = 0.62f;

        Part("Body", transform, new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth), material, BodyColour);
        Part("Skid", transform, new Vector3(0f, 0.04f, 0f), new Vector3(width + 0.06f, 0.08f, depth + 0.06f), material, BodyColour * 0.7f);

        // Steel straps round the body, and an amber stencil band along the front.
        foreach (float x in new[] { -0.42f, 0.42f })
            Part("Strap", transform, new Vector3(x, height / 2f, 0f), new Vector3(0.08f, height + 0.02f, depth + 0.02f), material, StrapColour);

        Part("Stencil", transform, new Vector3(0f, height * 0.55f, depth / 2f + 0.006f), new Vector3(0.6f, 0.08f, 0.01f), material, LampColour * 0.6f);

        // The lid, hinged along the back edge.
        lid = new GameObject("Lid").transform;
        lid.SetParent(transform, false);
        lid.localPosition = new Vector3(0f, height, -depth / 2f);
        Part("Lid", lid, new Vector3(0f, 0.06f, depth / 2f), new Vector3(width + 0.04f, 0.12f, depth + 0.04f), material, BodyColour * 1.1f);

        foreach (float x in new[] { -0.42f, 0.42f })
            Part("Lid Strap", lid, new Vector3(x, 0.07f, depth / 2f), new Vector3(0.08f, 0.13f, depth + 0.06f), material, StrapColour);

        lamp = Part("Lamp", lid, new Vector3(0.52f, 0.16f, depth * 0.8f), Vector3.one * 0.09f, material, LampColour, PrimitiveType.Sphere);

        glow = new GameObject("Light").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = new Vector3(0f, 1.2f, 0.4f);
        glow.type = LightType.Point;
        glow.color = LampColour;
        glow.range = 3.5f;
        glow.intensity = 1.2f;
        glow.shadows = LightShadows.None;

        // Solid, and zombies path round it.
        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, height / 2f + 0.05f, 0f);
        box.size = new Vector3(width + 0.06f, height + 0.1f, depth + 0.06f);

        NavMeshObstacle obstacle = gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = box.center;
        obstacle.size = box.size;
        obstacle.carving = true;

        label = PickupLabel.Create(transform, 1.6f);
        label.Compactable = true;

        // With the sprite art: seen from its side like the furniture (shut and lit, shut and dark,
        // open), the model casting the shadow; the older single-view sprite without that sheet.
        Vector3 middle = transform.TransformPoint(box.center);
        furnitureSprite = FurnitureSprite.Apply(gameObject, "SupplyCrate", transform.forward, box.size, new Vector3(middle.x, 0f, middle.z));

        if (furnitureSprite == null)
            sprite = ItemSprite.Apply(gameObject, SpriteVisuals.Current != null ? SpriteVisuals.Current.supplyCrate : null, Vector2Int.zero, castsShadow: true);
    }

    private static Renderer Part(string name, Transform parent, Vector3 position, Vector3 size, Material material, Color colour,
        PrimitiveType shape = PrimitiveType.Cube)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = name;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;

        Renderer renderer = part.GetComponent<Renderer>();

        if (material != null)
        {
            renderer.sharedMaterial = material;
            ConsumableModel.Tint(renderer, colour, Color.black);
        }

        return renderer;
    }

    // Puts something in (materials of a kind share one entry).
    public void Add(CrateItem item)
    {
        if (item == null || item.IsEmpty)
            return;

        foreach (CrateItem existing in items)
        {
            if (existing.Merges(item))
            {
                existing.Merge(item);
                Changed?.Invoke();
                return;
            }
        }

        items.Add(item);
        Changed?.Invoke();
    }

    // Gives an item to the player; whatever comes back out of their hands goes in its place.
    // Returns false if nothing could be taken.
    public bool Take(CrateItem item, PlayerWeapon player)
    {
        int index = items.IndexOf(item);

        if (index < 0 || !item.Take(player, out CrateItem returned))
            return false;

        if (item.IsEmpty || item.Kind == CrateItem.ItemKind.Gun || item.Kind == CrateItem.ItemKind.Attachment
            || item.Kind == CrateItem.ItemKind.MeleeUpgrade)
            items.RemoveAt(index);

        if (returned != null)
            items.Insert(Mathf.Min(index, items.Count), returned);

        Changed?.Invoke();

        if (items.Count == 0)
            StartCoroutine(Vanish());

        return true;
    }

    // Emptied: no longer solid or interactable, it sinks into the floor and is removed.
    private System.Collections.IEnumerator Vanish()
    {
        if (vanishing)
            yield break;

        vanishing = true;
        label.Hide();

        foreach (Collider body in GetComponents<Collider>())
            body.enabled = false;

        Vector3 start = transform.position;
        const float time = 0.6f;

        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            transform.position = start + Vector3.down * 0.8f * k * k;

            if (glow != null)
                glow.intensity = 0.5f * (1f - k);

            yield return null;
        }

        Destroy(gameObject);
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
        if (vanishing)
            return;

        Animate();

        if (playerWeapon == null)
        {
            playerWeapon = PlayerWeapon.Current;

            if (playerWeapon == null)
                return;
        }

        Vector3 offset = playerWeapon.transform.position - transform.position;
        offset.y = 0f;
        bool showLabel = offset.magnitude <= Reach + 2f && !CrateScreen.IsOpen && !StationScreen.IsOpen;

        if (showLabel && !label.IsShowing)
            label.Show(LabelText, LampColour);
        else if (!showLabel && label.IsShowing)
            label.Hide();

        if (FloorPickups.Pressed(this, playerWeapon))
            Open();
    }

    private void Open()
    {
        opened = true;

        if (sprite != null)
            sprite.SetFrame(new Vector2Int(1, 0));

        CrateScreen.Open(this, playerWeapon);
    }

    // The lid swings up once opened; until then the lamp blinks and the crate glows amber.
    private void Animate()
    {
        float target = opened ? LidOpenAngle : 0f;
        lidAngle = Mathf.MoveTowards(lidAngle, target, 240f * Time.deltaTime);
        lid.localRotation = Quaternion.Euler(-lidAngle, 0f, 0f);

        bool on = !opened && Mathf.Repeat(Time.time, 1f) < 0.5f;
        ConsumableModel.Tint(lamp, LampColour, on ? LampColour * 3f : LampColour * 0.15f);

        if (glow != null)
            glow.intensity = opened ? 0.5f : on ? 1.6f : 0.8f;

        if (furnitureSprite != null)
            furnitureSprite.Column = opened ? 2 : on ? 0 : 1;
    }

    private string LabelText()
    {
        string header = $"{Title}  <size=70%><alpha=#99>// {(items.Count == 0 ? "EMPTY" : items.Count + (items.Count == 1 ? " ITEM" : " ITEMS"))}</size>\n";
        return header + $"<size=80%><color=#FFFFFF>{InputHint.Format("[{Interact}]")} {(opened ? "SEARCH" : "OPEN")}</color></size>";
    }
}
