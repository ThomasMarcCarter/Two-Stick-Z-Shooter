using TMPro;
using UnityEngine;

// Keeps a header like "> TACTICAL MAP // FLOOR 2" in step with the current floor.
// {0} is the floor number, {1} the total.
[RequireComponent(typeof(TMP_Text))]
public class FloorLabel : MonoBehaviour
{
    [SerializeField] private FloorGenerator floor;
    [SerializeField] private string format = "> TACTICAL MAP // FLOOR {0}";

    private TMP_Text label;
    private string shownName;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        if (floor == null || floor.FloorName == shownName)
            return;

        shownName = floor.FloorName;
        label.text = floor.IsHideout || floor.IsSupplyRoom
            ? string.Format(format.Replace("FLOOR {0} OF {1}", "{2}").Replace("FLOOR {0}", "{2}"), floor.FloorNumber, floor.TotalFloors, floor.FloorName)
            : string.Format(format, floor.FloorNumber, floor.TotalFloors);
    }
}
