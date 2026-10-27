using UnityEngine;

// Each zone's hideout: the same hall and layout (FloorGenerator.Hideout.cs) on the zone's floor
// and walls (FloorGenerator.Zones.cs), dressed for where the survivors have holed up. The north
// wall's free stretch between the generator and the supply room gets the place's way in, shut:
//   Military Base  the hangar: a rolling shutter door, tyres and crates by it, sandbags on the
//                  south wall and benches either side of the walk to the hatch.
//   Shopping Mall  a boarded-up store: shop windows and the glass doors boarded from inside, a
//                  pallet against them, store shelves shoved against the south wall, display
//                  tables for seats.
//   Hospital       the emergency room, barred up: barred windows, the EMERGENCY doors chained
//                  behind a gate, beds against the south wall, a wheelchair and a trolley.
//   Airport        the baggage sorting room: a belt coming through the wall under its strip
//                  curtain, cases left on it and piled about, baggage carts.
public partial class FloorGenerator
{
    // The middle of the north wall's free stretch (between the generator's drums and the supply
    // room), from the hall's middle.
    private const float HideoutDoorX = 4.2f;

    private void DressHideout(Room hall)
    {
        switch (Zone)
        {
            case Zone.ShoppingMall: DressBoardedStore(hall); break;
            case Zone.Hospital: DressEmergencyRoom(hall); break;
            case Zone.Airport: DressBaggageRoom(hall); break;
            default: DressHangar(hall); break;
        }
    }

    private void DressHangar(Room hall)
    {
        Transform parent = hall.transform;
        Color wood = new Color(0.35f, 0.25f, 0.15f);
        Color sand = new Color(0.45f, 0.4f, 0.3f);

        WallPiece("HangarDoor", parent, HideoutDoorX, new Vector3(6f, 2.8f, 0.2f));
        DoorLamp(parent, new Color(1f, 0.85f, 0.6f));
        Furniture("Tyres", "Tyres", parent, NorthWall(HideoutDoorX + 3.9f, 0.8f), Vector3.back, new Vector3(0.9f, 0.55f, 0.9f),
            new Color(0.06f, 0.06f, 0.06f), true, PrimitiveType.Cylinder);
        Furniture("Crate", "Crate", parent, NorthWall(HideoutDoorX - 3.8f, 0.7f), Vector3.back, new Vector3(1f, 0.9f, 1f), new Color(0.3f, 0.33f, 0.2f));

        SideSeats(hall, "Bench", new Vector3(2.4f, 0.5f, 0.5f), wood);

        // Sandbags along the south wall, west of the truck bay (east of it is the truck's way out).
        SouthWallPieces(hall, "Sandbags", new Vector3(3f, 0.7f, 0.8f), sand, 0.9f);
    }

    private void DressBoardedStore(Room hall)
    {
        Transform parent = hall.transform;

        WallPiece("BoardedWindow", parent, HideoutDoorX - 3f, new Vector3(2.3f, 2.3f, 0.16f));
        WallPiece("BoardedDoor", parent, HideoutDoorX, new Vector3(1.9f, 2.45f, 0.16f));
        DoorLamp(parent, new Color(0.8f, 0.9f, 1f));
        WallPiece("BoardedWindow", parent, HideoutDoorX + 3f, new Vector3(2.3f, 2.3f, 0.16f));
        Furniture("Pallet", "Barricade", parent, NorthWall(HideoutDoorX + 1.6f, 0.7f), Vector3.back, new Vector3(1.2f, 1.2f, 1f), new Color(0.5f, 0.4f, 0.25f));

        SideSeats(hall, "DisplayTable", new Vector3(1.6f, 0.8f, 0.9f), new Color(0.6f, 0.55f, 0.45f));
        SouthWallPieces(hall, "StoreShelf", new Vector3(2.4f, 1.8f, 0.6f), new Color(0.5f, 0.5f, 0.52f), 0.5f);
    }

    private void DressEmergencyRoom(Room hall)
    {
        Transform parent = hall.transform;
        Vector3 centre = HallCentre;

        WallPiece("BarredWindow", parent, HideoutDoorX - 3.1f, new Vector3(2.1f, 2.25f, 0.16f));
        WallPiece("BarredDoor", parent, HideoutDoorX, new Vector3(2.4f, 2.6f, 0.2f));
        DoorLamp(parent, new Color(1f, 0.55f, 0.5f));
        WallPiece("BarredWindow", parent, HideoutDoorX + 3.1f, new Vector3(2.1f, 2.25f, 0.16f));

        // A wheelchair and a trolley either side of the walk to the hatch.
        Furniture("Wheelchair", "Wheelchair", parent, centre + new Vector3(-3.5f, 0f, -2f), Vector3.right, new Vector3(0.7f, 1.1f, 0.6f), new Color(0.3f, 0.3f, 0.32f));
        Furniture("Trolley", "Trolley", parent, centre + new Vector3(3.5f, 0f, -2f), Vector3.left, new Vector3(0.9f, 0.9f, 0.5f), new Color(0.7f, 0.72f, 0.74f));

        // Beds, heads against the south wall.
        SouthWallPieces(hall, "HospitalBed", new Vector3(0.9f, 0.88f, 2f), new Color(0.75f, 0.77f, 0.75f), 1.1f);
        Furniture("IVStand", "IV Stand", parent, new Vector3(centre.x - 6.75f, 0f, centre.z - HallHalf.y + 0.6f), Vector3.forward, new Vector3(0.5f, 1.9f, 0.5f), new Color(0.7f, 0.72f, 0.74f), false);
    }

    private void DressBaggageRoom(Room hall)
    {
        Transform parent = hall.transform;
        Color luggage = new Color(0.4f, 0.3f, 0.25f);
        Color cart = new Color(0.35f, 0.37f, 0.4f);

        // The belt comes through the wall and runs a few metres into the room.
        WallPiece("BeltHatch", parent, HideoutDoorX, new Vector3(2.4f, 2.65f, 0.2f));
        DoorLamp(parent, new Color(1f, 0.75f, 0.4f));
        Furniture("Conveyor", "Conveyor", parent, NorthWall(HideoutDoorX, 2.2f), Vector3.back, new Vector3(1f, 0.9f, 4f), new Color(0.6f, 0.62f, 0.64f));

        Furniture("Luggage", "Luggage", parent, NorthWall(HideoutDoorX - 2.4f, 0.5f), Vector3.back, new Vector3(1f, 0.9f, 0.6f), luggage);
        Furniture("Luggage", "Luggage", parent, NorthWall(HideoutDoorX + 2.3f, 0.5f), Vector3.back, new Vector3(1f, 0.9f, 0.6f), luggage);
        Furniture("Luggage", "Luggage", parent, NorthWall(HideoutDoorX + 1.5f, 4.4f), Vector3.back, new Vector3(1f, 0.9f, 0.6f), luggage);

        SideSeats(hall, "BaggageCart", new Vector3(2.3f, 1f, 1.1f), cart);

        Vector3 centre = HallCentre;
        float south = centre.z - HallHalf.y;
        Furniture("BaggageCart", "Baggage Cart", parent, new Vector3(centre.x - 8.5f, 0f, south + 0.8f), Vector3.forward, new Vector3(2.3f, 1f, 1.1f), cart);
        Furniture("Luggage", "Luggage", parent, new Vector3(centre.x - 5f, 0f, south + 0.5f), Vector3.forward, new Vector3(1f, 0.9f, 0.6f), luggage);
    }

    // ---------- Helpers ----------

    // A floor point `out` metres in from the north wall, `x` along it from the hall's middle.
    private Vector3 NorthWall(float x, float outFromWall)
    {
        return HallCentre + new Vector3(x, 0f, HallHalf.y - outFromWall);
    }

    // A door, window or hatch flat against the north wall, facing into the hall (not solid: the
    // wall is behind it).
    private void WallPiece(string model, Transform parent, float x, Vector3 size)
    {
        Furniture(model, model, parent, NorthWall(x, size.z / 2f + 0.02f), Vector3.back, size, new Color(0.3f, 0.3f, 0.3f), false);
    }

    // A lamp over the way in, lighting the north wall's doors and windows (the hall's own lights
    // leave that wall dark).
    private void DoorLamp(Transform parent, Color colour)
    {
        Light lamp = new GameObject("Door Lamp").AddComponent<Light>();
        lamp.transform.SetParent(parent, false);
        lamp.transform.position = NorthWall(HideoutDoorX, 2.5f) + Vector3.up * 4f;
        lamp.transform.rotation = Quaternion.LookRotation(NorthWall(HideoutDoorX, 0f) + Vector3.up * 0.8f - lamp.transform.position);
        lamp.type = LightType.Spot;
        lamp.spotAngle = 110f;
        lamp.range = 9f;
        lamp.intensity = 6f;
        lamp.color = colour;
        lamp.shadows = LightShadows.None;
    }

    // A pair either side of the walk from the spawn to the hatch, facing it.
    private void SideSeats(Room hall, string model, Vector3 size, Color colour)
    {
        Vector3 centre = HallCentre;
        Furniture(model, model, hall.transform, centre + new Vector3(-3.5f, 0f, -2f), Vector3.right, size, colour);
        Furniture(model, model, hall.transform, centre + new Vector3(3.5f, 0f, -2f), Vector3.left, size, colour);
    }

    // Two along the south wall, west of the truck bay, `out` metres from it.
    private void SouthWallPieces(Room hall, string model, Vector3 size, Color colour, float outFromWall)
    {
        Vector3 centre = HallCentre;
        float z = centre.z - HallHalf.y + outFromWall;

        foreach (float x in new[] { -8.5f, -5f })
            Furniture(model, model, hall.transform, new Vector3(centre.x + x, 0f, z), Vector3.forward, size, colour);
    }
}
