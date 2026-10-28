using UnityEngine;

// Low-wall dressings: the low cover walls the cut-outs stand in a room (FloorGenerator.Cutouts.cs)
// are drawn as something that belongs to the room's theme instead of a bare wall: gate seats in a
// lounge, specimen fridges in a lab, office supplies in an office, parts shelves in a workshop;
// unthemed rooms get their zone's own (sandbags and barriers at the base, planters in the mall...).
// Each theme has 3-4 variants per length (3 or 4 m, sheets `Low<Key><L>` from
// Tools/Sprites/lowwalls.py), one picked at random per wall. The wall keeps its collider (still 1 m
// cover that stops bullets and zombies) and casts its shadow; only its kit mesh stops being drawn.
// Without the sprite art the walls stay as they are.
public partial class FloorGenerator
{
    private void DressLowWalls(Room room, RoomTheme theme)
    {
        string key = LowWallKey(theme);

        foreach (Transform child in room.transform)
        {
            if (child.name != "Low Wall" || !child.TryGetComponent(out BoxCollider box))
                continue;

            float length = box.size.x;
            string sheet = $"Low{key}{Mathf.RoundToInt(length)}";
            FurnitureSprite sprite = FurnitureSprite.Apply(child.gameObject, sheet, Vector3.back,
                new Vector3(length, lowWallHeight, wallThickness), child.position);

            if (sprite != null)
                sprite.Column = rng.Next(sprite.Frames);
        }
    }

    // The sheet key for a theme's low walls: the zone's, plus the theme (labs are shared by the
    // base and the hospital).
    private string LowWallKey(RoomTheme theme)
    {
        if (theme == RoomTheme.Lab)
            return "Lab";

        if (theme == RoomTheme.PatientWing)
            theme = RoomTheme.Ward;

        string zone;

        switch (Zone)
        {
            case Zone.ShoppingMall: zone = "Mall"; break;
            case Zone.Hospital: zone = "Hosp"; break;
            case Zone.Airport: zone = "Air"; break;
            default: zone = "Base"; break;
        }

        return theme == RoomTheme.None ? zone : zone + theme;
    }
}
