using UnityEngine;

// Something on a shelf in the supply room under the last floor: a pile of scrap, a stack of a
// crafting material, or the run's truck part. The player walks up and takes it with Interact (a
// label says what it is). Scrap joins the run's carried scrap (MetaProgress.AddBonusScrap), a
// material goes in the backpack (as much as fits; the rest stays, labelled BACKPACK FULL), and
// the truck part goes in the story slot (StorySlot), which unlocks the way out. Everything's
// only safe once the run is won by climbing out.
// Drawn from the story sheet (SpriteVisuals.storyItems; materials from the items sheet) at the
// height of the shelf it sits on, or as a small block stand-in without the sprite art.
public class SupplyShelfItem : MonoBehaviour, FloorPickups.IPickup
{
    public enum Kind
    {
        Scrap,
        Material,
        Story
    }

    private const float LabelRange = 2.8f;

    private Kind kind;
    private int amount;
    private MaterialType material;
    private StoryItem story;
    private Vector3 reachPoint;
    private PickupLabel label;
    private PlayerWeapon player;
    private bool full;
    private bool taken;

    // Where the player stands to reach it: in front of the shelf, on the floor.
    public Vector3 Position => reachPoint;
    public float Reach => 1.4f;
    public bool CanTake => !taken && !StationScreen.IsOpen;

    public Kind ItemKind => kind;
    public StoryItem Story => story;

    // `shelfFront` is the floor point in front of the shelf it's taken from; `height` how far up
    // the shelf it sits.
    public static SupplyShelfItem SpawnScrap(int amount, Vector3 position, float height, Vector3 shelfFront, Transform parent)
    {
        SupplyShelfItem item = Create("Scrap", position, height, shelfFront, parent);
        item.kind = Kind.Scrap;
        item.amount = Mathf.Max(1, amount);
        item.Dress(new Vector2Int(Mathf.Min(2, item.amount / 8), 1), new Color(0.6f, 0.58f, 0.52f), height);
        return item;
    }

    public static SupplyShelfItem SpawnMaterial(MaterialType type, int amount, Vector3 position, float height, Vector3 shelfFront, Transform parent)
    {
        SupplyShelfItem item = Create($"Material ({Materials.Name(type)})", position, height, shelfFront, parent);
        item.kind = Kind.Material;
        item.material = type;
        item.amount = Mathf.Max(1, amount);

        SpriteVisuals visuals = SpriteVisuals.Current;

        if (visuals != null && visuals.items != null)
            ItemSprite.Apply(item.gameObject, visuals.items, ItemSprites.Material(type), false, ShelfLift(height));
        else
            item.Block(Materials.Colour(type), new Vector3(0.3f, 0.25f, 0.3f), height);

        return item;
    }

    public static SupplyShelfItem SpawnStory(StoryItem story, Vector3 position, float height, Vector3 shelfFront, Transform parent)
    {
        SupplyShelfItem item = Create(StoryItems.Name(story), position, height, shelfFront, parent);
        item.kind = Kind.Story;
        item.story = story;
        item.Dress(StoryItems.SheetFrame(story), StoryItems.Colour(story), height);
        return item;
    }

    private static SupplyShelfItem Create(string name, Vector3 position, float height, Vector3 shelfFront, Transform parent)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);

        SupplyShelfItem item = root.AddComponent<SupplyShelfItem>();
        item.reachPoint = new Vector3(shelfFront.x, 0f, shelfFront.z);
        item.label = PickupLabel.Create(root.transform, 1.3f + height);
        item.label.Compactable = true;
        return item;
    }

    // Its frame on the story sheet, or a block in its colour without one.
    private void Dress(Vector2Int frame, Color colour, float height)
    {
        SpriteVisuals visuals = SpriteVisuals.Current;

        if (visuals != null && visuals.storyItems != null)
            ItemSprite.Apply(gameObject, visuals.storyItems, frame, false, ShelfLift(height));
        else
            Block(colour, new Vector3(0.45f, 0.3f, 0.35f), height);
    }

    private void Block(Color colour, Vector3 size, float height)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "Model";
        Destroy(block.GetComponent<Collider>());
        block.transform.SetParent(transform, false);
        block.transform.localPosition = Vector3.up * (height + size.y / 2f);
        block.transform.localScale = size;

        Renderer renderer = block.GetComponent<Renderer>();

        if (ConsumablePickup.DefaultMaterial != null)
        {
            renderer.sharedMaterial = ConsumablePickup.DefaultMaterial;
            ConsumableModel.Tint(renderer, colour, colour * 0.3f);
        }
    }

    // A sprite on a shelf is lifted as far as the furniture art draws that height (see
    // FurnitureSprite), so it sits on the drawn shelf.
    private static float ShelfLift(float height)
    {
        return height * 0.7f / Mathf.Max(0.25f, CameraFollow.VerticalScale);
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
        if (taken)
            return;

        if (player == null)
        {
            player = PlayerWeapon.Current;

            if (player == null)
                return;
        }

        Vector3 offset = player.transform.position - reachPoint;
        offset.y = 0f;
        float distance = offset.magnitude;

        if (distance > Reach)
            full = false;

        bool inRange = distance <= LabelRange && !StationScreen.IsOpen;

        if (inRange && !label.IsShowing)
            label.Show(LabelText, LabelColour);
        else if (!inRange && label.IsShowing)
            label.Hide();

        if (FloorPickups.Pressed(this, player))
            Take();
    }

    private Color LabelColour
    {
        get
        {
            switch (kind)
            {
                case Kind.Material: return Materials.Colour(material);
                case Kind.Story: return StoryItems.Colour(story);
                default: return UiColours.Amber;
            }
        }
    }

    private string LabelText()
    {
        string action = $"<size=80%><color=#FFFFFF>{InputHint.Format("[{Interact}]")} TAKE</color></size>";

        switch (kind)
        {
            case Kind.Material:
                return Materials.Name(material) + (amount > 1 ? "  x" + amount : "")
                    + (full ? $"\n<size=70%><color={UiColours.DangerHex}>BACKPACK FULL // MAKE ROOM</color></size>" : "\n" + action);

            case Kind.Story:
                return StoryItems.Name(story) + $"  <size=70%><alpha=#99>// {StoryItems.GoalName(StoryItems.Owner(story))}</size>\n"
                    + $"<size=65%><alpha=#BB>{StoryItems.Description(story)}</size>\n" + action;

            default:
                return $"SCRAP  x{amount}\n" + action;
        }
    }

    private void Take()
    {
        switch (kind)
        {
            case Kind.Material:
                int added = Backpack.For(player.gameObject).AddMaterial(material, amount);
                full = added < amount;

                if (added == 0)
                    return;

                amount -= added;
                SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("material-pickup", 3, ProceduralAudio.AmmoPickup)), 0.45f, 1.25f);

                if (amount > 0)
                    return;

                break;

            case Kind.Story:
                StorySlot.For(player.gameObject).Put(story);
                SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("metal.clank", 3, ProceduralAudio.MetalClank)), 0.6f, 0.9f);
                // Whoever wants it calls in from the hideout (the mechanic's first time, they're
                // right here, caged).
                NpcKind owner = StoryItems.Owner(story);

                if (MetaProgress.IsRescued(owner))
                    RadioMessage.Show("HIDEOUT // " + Npcs.Name(owner), Npcs.ItemTaken(story));
                break;

            default:
                MetaProgress.AddBonusScrap(amount);
                SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("material-pickup", 3, ProceduralAudio.AmmoPickup)), 0.5f, 0.9f);
                break;
        }

        taken = true;
        label.Hide();
        Destroy(gameObject);
    }
}
