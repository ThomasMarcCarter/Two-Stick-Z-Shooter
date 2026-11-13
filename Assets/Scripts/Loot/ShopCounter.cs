using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// One thing for sale at a shop counter: a run item or a crate-style item (consumable, ammo,
// attachment, armour plate), with its price in carried scrap. Title, detail, colour and what
// buying would do are read off the run item or the CrateItem.
public class ShopEntry
{
    public CrateItem Item;
    public RunItem Run;
    public bool IsRun;
    public int Price;

    public static ShopEntry ForRun(RunItem run, int price) => new ShopEntry { IsRun = true, Run = run, Price = price };
    public static ShopEntry ForItem(CrateItem item, int price) => new ShopEntry { Item = item, Price = price };

    public string Title => IsRun ? RunItems.Name(Run) : Item.Title;
    public string Detail => IsRun ? RunItems.Describe(Run) : Item.Detail;
    public Color Colour => IsRun ? RunItems.Colour(Run) : Item.Colour;

    // What buying does right now, or why it can't be bought (`blocked` true then).
    public string Action(PlayerWeapon player, out bool blocked)
    {
        blocked = false;

        if (IsRun)
            return "BUY";

        string action = Item.Action(player, out blocked);

        // A shop never swaps a fitted attachment out: that one would be lost.
        if (!blocked && Item.Kind == CrateItem.ItemKind.Attachment && AttachmentPickup.Decide(player, Item.Attachment, out _) == AttachmentPickup.Take.Swap)
        {
            blocked = true;
            return "NO FREE SLOT // BACKPACK FULL";
        }

        return action;
    }
}

// A shop counter in a shop room (the checkout till sprite): Interact opens ShopScreen, which sells
// the room's fixed stock for carried scrap (MetaProgress.SpendCarriedScrap). Bought rows go; the
// stock is built once when the room is, by FloorGenerator.FillSpecialRoom.
public class ShopCounter : MonoBehaviour, FloorPickups.IPickup
{
    private static readonly Color LabelColour = new Color(0.45f, 0.9f, 0.55f);

    private readonly List<ShopEntry> stock = new List<ShopEntry>();
    private PickupLabel label;
    private PlayerWeapon playerWeapon;
    private FloorGenerator floor;

    public IReadOnlyList<ShopEntry> Stock => stock;
    public Vector3 Position => transform.position;
    public float Reach => 2f;
    public bool CanTake => !ShopScreen.IsOpen && !CrateScreen.IsOpen && !StationScreen.IsOpen;

    public static ShopCounter Create(Vector3 position, Vector3 facing, Transform parent)
    {
        GameObject root = new GameObject("Shop Counter");
        root.transform.SetParent(parent, true);
        facing.y = 0f;
        root.transform.SetPositionAndRotation(new Vector3(position.x, 0f, position.z),
            Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing.normalized : Vector3.back, Vector3.up));

        ShopCounter counter = root.AddComponent<ShopCounter>();

        // Solid, and zombies path round it.
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.55f, 0f);
        box.size = new Vector3(1.8f, 1.1f, 0.8f);

        NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = box.center;
        obstacle.size = box.size;
        obstacle.carving = true;

        counter.label = PickupLabel.Create(root.transform, 1.8f);

        // The checkout till's sprite; without the art a bare counter block, so it still reads.
        Vector3 middle = root.transform.TransformPoint(box.center);

        if (FurnitureSprite.Apply(root, "Checkout", root.transform.forward, box.size, new Vector3(middle.x, 0f, middle.z)) == null)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(block.GetComponent<Collider>());
            block.transform.SetParent(root.transform, false);
            block.transform.localPosition = box.center;
            block.transform.localScale = box.size;
        }

        return counter;
    }

    public void Add(ShopEntry entry)
    {
        stock.Add(entry);
    }

    // The run's scrap not sent home or spent yet.
    public int CarriedScrap()
    {
        if (floor == null)
            floor = FindAnyObjectByType<FloorGenerator>();

        return floor != null ? MetaProgress.CarriedScrap(floor.FloorsCleared, floor.EnemiesKilled) : 0;
    }

    // Buys one entry: gives it to the player and takes its price. On failure, `message` says why
    // and nothing is spent.
    public bool Buy(ShopEntry entry, PlayerWeapon player, out string message)
    {
        message = "";

        if (!stock.Contains(entry))
            return false;

        entry.Action(player, out bool blocked);

        if (blocked)
        {
            message = "CAN'T TAKE THAT // " + entry.Action(player, out _);
            return false;
        }

        int carried = CarriedScrap();

        if (carried < entry.Price)
        {
            message = $"NOT ENOUGH SCRAP // {entry.Price - carried} MORE NEEDED";
            return false;
        }

        // Give it first, so a pickup that fails costs nothing; then charge.
        bool given = entry.IsRun
            ? PlayerItems.For(player.gameObject).Add(entry.Run)
            : entry.Item.Take(player, out _);

        if (!given)
        {
            message = "CAN'T TAKE THAT";
            return false;
        }

        MetaProgress.SpendCarriedScrap(entry.Price, floor.FloorsCleared, floor.EnemiesKilled);
        MetaProgress.AddStat(Stat.ShopBuys);
        stock.Remove(entry);
        message = $"BOUGHT {entry.Title} FOR {entry.Price}";
        return true;
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
        if (playerWeapon == null)
        {
            playerWeapon = PlayerWeapon.Current;

            if (playerWeapon == null)
                return;
        }

        Vector3 offset = playerWeapon.transform.position - transform.position;
        offset.y = 0f;
        bool showLabel = offset.magnitude <= Reach + 2f && CanTake;

        if (showLabel && !label.IsShowing)
            label.Show(LabelText, LabelColour);
        else if (!showLabel && label.IsShowing)
            label.Hide();

        if (FloorPickups.Pressed(this, playerWeapon))
        {
            ShopScreen.Open(this, playerWeapon);
        }
    }

    private string LabelText()
    {
        string header = $"SHOP  <size=70%><alpha=#99>// {(stock.Count == 0 ? "SOLD OUT" : stock.Count + " FOR SALE")}</size>\n";
        return header + $"<size=80%><color=#FFFFFF>{InputHint.Format("[{Interact}]")} BROWSE</color></size>";
    }
}
