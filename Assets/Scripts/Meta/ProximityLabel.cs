using System;
using UnityEngine;

// A floating label (PickupLabel) on something that isn't a pickup, shown while the player is
// within range: the hideout's deploy hatch, the stash, sealed wing doors. The text is re-read
// every frame, so it can show live numbers.
public class ProximityLabel : MonoBehaviour
{
    private PickupLabel label;
    private Func<string> text;
    private Color colour;
    private float range;
    private Transform player;

    // clearance > 0 puts the label above the owner on screen, that far clear of it, instead of
    // over it (see PickupLabel.Clearance).
    public static ProximityLabel Add(GameObject owner, float height, float showRange, Color stripe, Func<string> source,
        float clearance = 0f)
    {
        ProximityLabel proximity = owner.AddComponent<ProximityLabel>();
        proximity.label = PickupLabel.Create(owner.transform, height);
        proximity.label.Clearance = clearance;
        proximity.text = source;
        proximity.colour = stripe;
        proximity.range = showRange;
        return proximity;
    }

    private void Update()
    {
        if (player == null)
        {
            PlayerWeapon weapon = FindAnyObjectByType<PlayerWeapon>();
            player = weapon != null ? weapon.transform : null;

            if (player == null)
                return;
        }

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        bool show = offset.magnitude <= range && !StationScreen.IsOpen;

        if (show && !label.IsShowing)
            label.Show(text, colour);
        else if (!show && label.IsShowing)
            label.Hide();
    }
}
