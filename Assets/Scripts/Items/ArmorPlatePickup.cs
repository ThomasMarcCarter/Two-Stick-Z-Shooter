using UnityEngine;

// A spare armour plate on the floor, a rare zombie drop for a player wearing the Plate Carrier.
// Walking over it slots it in, no button needed; with every slot full it stays where it is,
// labelled so.
public class ArmorPlatePickup : MonoBehaviour
{
    private const float LabelRange = 2.8f;
    private const float Reach = 1.1f;

    private PickupLabel label;
    private bool taken;

    public static ArmorPlatePickup Spawn(Vector3 position, Transform parent)
    {
        GameObject root = new GameObject("Armor Plate");
        root.transform.SetParent(parent, true);
        root.transform.position = new Vector3(position.x, 0f, position.z);

        ArmorPlatePickup pickup = root.AddComponent<ArmorPlatePickup>();
        pickup.label = PickupLabel.Create(root.transform, 1.5f);
        pickup.label.Compactable = true;

        if (ItemSprite.Apply(root, SpriteVisuals.Current != null ? SpriteVisuals.Current.items : null, ItemSprites.ArmorPlate, true) == null)
            pickup.BuildModel();

        return pickup;
    }

    // The stand-in without the sprite art.
    private void BuildModel()
    {
        GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(model.GetComponent<Collider>());
        model.transform.SetParent(transform, false);
        model.transform.localPosition = Vector3.up * 0.3f;
        model.transform.localScale = new Vector3(0.35f, 0.45f, 0.06f);

        Renderer renderer = model.GetComponent<Renderer>();

        if (ConsumablePickup.DefaultMaterial != null)
        {
            renderer.sharedMaterial = ConsumablePickup.DefaultMaterial;
            ConsumableModel.Tint(renderer, RunItems.PlateColour, RunItems.PlateColour * 0.5f);
        }
    }

    private void Update()
    {
        PlayerItems items = PlayerItems.Current;

        if (taken || items == null)
            return;

        Vector3 offset = items.transform.position - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;

        if (distance <= Reach && !GameFlow.InputBlocked && items.AddPlate())
        {
            taken = true;
            Destroy(gameObject);
            return;
        }

        bool inRange = distance <= LabelRange;

        if (inRange && !label.IsShowing)
            label.Show(LabelText, RunItems.PlateColour);
        else if (!inRange && label.IsShowing)
            label.Hide();
    }

    private static string LabelText()
    {
        PlayerItems items = PlayerItems.Current;
        string state = items != null && items.PlatesFull ? "PLATES FULL" : "WALK OVER IT TO SLOT IT IN";
        return $"ARMOR PLATE\n<size=70%><alpha=#88>{state}</size>";
    }
}
