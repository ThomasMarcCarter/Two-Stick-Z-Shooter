using TMPro;
using UnityEngine;

// Keeps a header like "> TACTICAL MAP // MILITARY BASE // FLOOR 2" in step with the current floor.
// {0} is the floor number, {1} the total, {3} the zone's name. In the hideout and the supply room
// the "FLOOR {0}" part becomes their name (and the hideout drops the zone, being in none).
[RequireComponent(typeof(TMP_Text))]
public class FloorLabel : MonoBehaviour
{
    [SerializeField] private FloorGenerator floor;
    [SerializeField] private string format = "> TACTICAL MAP // {3} // FLOOR {0}";

    private TMP_Text label;
    private string shownName;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        if (floor == null || floor.LocationName == shownName)
            return;

        shownName = floor.LocationName;
        string shown = format;

        if (floor.IsHideout || floor.IsSupplyRoom || floor.IsEvac)
            shown = shown.Replace("FLOOR {0} OF {1}", "{2}").Replace("FLOOR {0}", "{2}");

        if (floor.IsHideout)
            shown = shown.Replace("{3} // ", "");

        label.text = string.Format(shown, floor.FloorNumber, floor.TotalFloors, floor.FloorName, floor.ZoneName);
    }
}
