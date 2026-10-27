using UnityEngine;

// The way down out of each zone's boss room (ZoneExit): what it's called on its label, what
// pressing Interact does, how long it takes to open once the boss is dead, the opening it needs in
// the boss room's north wall and how far it reaches out behind the wall (it's never on the room's
// floor), whether it stands in a walled bay of its own beyond the opening (the escalator and the
// chute; the stairwell and the lift fill the opening themselves), and its light once it's open.
//   Military Base: a locked stairwell door      Shopping Mall: a down escalator
//   Hospital:      a lift                       Airport:       a baggage belt into the hold
public struct ZoneExitInfo
{
    public string Name;
    public string Action;
    public float OpenTime;          // seconds from the boss dying to usable
    public float Opening;           // the gap in the north wall
    public float Depth;             // how far it reaches out beyond the wall's face
    public bool Bay;                // stands in a walled bay with a floor, not filling the opening
    public Color Light;
}

public static class ZoneExits
{
    public static ZoneExitInfo For(Zone zone)
    {
        switch (zone)
        {
            case Zone.ShoppingMall:
                return new ZoneExitInfo
                {
                    Name = "ESCALATOR", Action = "RIDE IT DOWN", OpenTime = 0.9f,
                    Opening = 2.4f, Depth = 3f, Bay = true, Light = new Color(1f, 0.85f, 0.6f)
                };

            case Zone.Hospital:
                return new ZoneExitInfo
                {
                    Name = "LIFT", Action = "TAKE THE LIFT DOWN", OpenTime = 2.6f,
                    Opening = 3f, Depth = 2f, Light = new Color(0.85f, 0.95f, 1f)
                };

            case Zone.Airport:
                return new ZoneExitInfo
                {
                    Name = "BAGGAGE CHUTE", Action = "RIDE THE BELT DOWN", OpenTime = 0.7f,
                    Opening = 2.6f, Depth = 2.9f, Bay = true, Light = new Color(1f, 0.7f, 0.25f)
                };

            default:
                return new ZoneExitInfo
                {
                    Name = "STAIRWELL", Action = "TAKE THE STAIRS DOWN", OpenTime = 1.4f,
                    Opening = 3f, Depth = 2.4f, Light = new Color(0.35f, 1f, 0.55f)
                };
        }
    }
}
