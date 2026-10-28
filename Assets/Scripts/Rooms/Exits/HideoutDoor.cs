using UnityEngine;

// The way out of the Mall's, the Hospital's and the Airport's hideouts, in place of the deploy
// hatch, set in the north wall (Tools/Sprites/furniture.py): the Hospital's and the Airport's are
// automatic glass doors under a lit sign (AutoDoorHosp / AutoDoorAir) that slide apart as the
// player walks up and close again when they walk away; the Mall's is the boarded-up store's
// roll-up shop shutter (ShutterDoor), which only goes up when it's used. Interact (the FloorExit
// in front) opens it if it isn't, walks the player out through it, it shuts behind and the run
// starts. Only drawn: the wall behind is solid.
public class HideoutDoor : MonoBehaviour
{
    private const float OpenRange = 3f;
    private const float SlideTime = 0.6f;

    private FurnitureSprite sprite;
    private bool automatic;
    private float slideTime = SlideTime;
    private float open;
    private bool opening;
    private bool shut;

    // The point just past the doors the player walks to before they're hidden.
    public Vector3 Inside => transform.position + Vector3.forward * 0.3f;

    // How long it takes to open fully (the player waits this long before walking through).
    public float OpenTime => automatic ? 0f : slideTime;

    // Adds the doors' behaviour to a wall piece already drawn with one of the door sheets:
    // `automatic` doors open as the player comes near, others (the shutter) only when used.
    public static HideoutDoor Add(GameObject wallPiece, bool automatic)
    {
        HideoutDoor door = wallPiece.AddComponent<HideoutDoor>();
        door.sprite = wallPiece.GetComponent<FurnitureSprite>();
        door.automatic = automatic;
        door.slideTime = automatic ? SlideTime : 1.1f;
        return door;
    }

    // Up it goes (the shutter, rattling), to be walked through.
    public void Open()
    {
        if (opening)
            return;

        opening = true;

        if (!automatic)
            SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("metal.clank", 3, ProceduralAudio.MetalClank)), transform.position, 0.6f, 0.6f);
    }

    // The player's through: close and stay closed.
    public void Shut()
    {
        shut = true;
    }

    private void Update()
    {
        if (sprite == null)
            return;

        PlayerWeapon player = PlayerWeapon.Current;
        bool near = false;

        if (player != null)
        {
            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            near = offset.magnitude < OpenRange;
        }

        float target = !shut && (opening || (automatic && near)) ? 1f : 0f;

        if (target > open && open <= 0f)
            SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("motor.hum", 1, i => ProceduralAudio.MotorHum())), transform.position, 0.25f, automatic ? 2.2f : 1.1f);

        open = Mathf.MoveTowards(open, target, Time.deltaTime / slideTime);
        sprite.Column = Mathf.RoundToInt(open * (sprite.Frames - 1));
    }
}
