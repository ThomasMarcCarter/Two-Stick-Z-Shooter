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
    private int shownFloor = -1;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        if (floor == null || floor.FloorNumber == shownFloor)
            return;

        shownFloor = floor.FloorNumber;
        label.text = floor.IsHideout
            ? string.Format(format.Replace("FLOOR {0}", "{2}"), shownFloor, floor.TotalFloors, floor.FloorName)
            : string.Format(format, shownFloor, floor.TotalFloors);
    }
}
