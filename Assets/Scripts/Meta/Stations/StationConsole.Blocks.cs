using UnityEngine;

// The StationConsole's placeholder bodies, built from blocks when there's no model or sprite for
// the station: one builder per station. Each sets the console's screen (the part that glows) and
// moving part where it has one, and sizes the body's collider to fit.
public partial class StationConsole
{
    // A diesel generator on skids: a long engine block, a radiator grille, exhaust stack
    // and a flywheel turning at the side, with a control panel on the front.
    private static void BuildGenerator(StationConsole console, Transform t, BoxCollider body, Material material, Color accent)
    {
        Part(t, "Skid", new Vector3(0f, 0.08f, 0f), new Vector3(2.4f, 0.16f, 1.2f), material, DarkMetal, Color.black, true);
        Part(t, "Engine", new Vector3(0f, 0.65f, -0.05f), new Vector3(2.1f, 1f, 1f), material, new Color(0.35f, 0.3f, 0.12f), Color.black, true);
        Part(t, "Radiator", new Vector3(-1.1f, 0.7f, -0.05f), new Vector3(0.2f, 1.1f, 1.05f), material, DarkMetal, accent * 0.15f, true);
        Part(t, "Exhaust", new Vector3(0.7f, 1.45f, -0.25f), new Vector3(0.14f, 0.7f, 0.14f), material, DarkMetal, Color.black, true, PrimitiveType.Cylinder);
        Part(t, "Panel", new Vector3(0.45f, 0.85f, 0.47f), new Vector3(0.7f, 0.5f, 0.08f), material, Metal, Color.black, false);
        console.screen = Part(t, "Screen", new Vector3(0.45f, 0.88f, 0.52f), new Vector3(0.55f, 0.3f, 0.02f), material, ScreenGlow * 0.3f, ScreenGlow * 2.2f, false);
        console.moving = Part(t, "Flywheel", new Vector3(1.12f, 0.6f, -0.05f), new Vector3(0.7f, 0.06f, 0.7f), material, Metal, Color.black, true, PrimitiveType.Cylinder).transform;
        console.moving.localRotation = Quaternion.Euler(0f, 0f, 90f);
        body.center = new Vector3(0f, 0.6f, -0.05f);
        body.size = new Vector3(2.6f, 1.2f, 1.2f);
    }

    // A heavy bench with a vice and tools, and a pegboard behind it.
    private static void BuildWorkbench(StationConsole console, Transform t, BoxCollider body, Material material, Color accent)
    {
        Part(t, "Bench", new Vector3(0f, 0.85f, 0f), new Vector3(2.2f, 0.1f, 0.9f), material, new Color(0.4f, 0.28f, 0.16f), Color.black, true);
        Part(t, "Leg", new Vector3(-1f, 0.4f, -0.35f), new Vector3(0.1f, 0.8f, 0.1f), material, DarkMetal, Color.black, true);
        Part(t, "Leg", new Vector3(1f, 0.4f, -0.35f), new Vector3(0.1f, 0.8f, 0.1f), material, DarkMetal, Color.black, true);
        Part(t, "Leg", new Vector3(-1f, 0.4f, 0.35f), new Vector3(0.1f, 0.8f, 0.1f), material, DarkMetal, Color.black, true);
        Part(t, "Leg", new Vector3(1f, 0.4f, 0.35f), new Vector3(0.1f, 0.8f, 0.1f), material, DarkMetal, Color.black, true);
        Part(t, "Shelf", new Vector3(0f, 0.25f, 0f), new Vector3(2f, 0.05f, 0.8f), material, new Color(0.3f, 0.22f, 0.13f), Color.black, false);
        Part(t, "Pegboard", new Vector3(0f, 1.5f, -0.42f), new Vector3(2.2f, 1.1f, 0.05f), material, new Color(0.45f, 0.38f, 0.28f), Color.black, true);
        Part(t, "Vice", new Vector3(0.8f, 1f, 0.25f), new Vector3(0.2f, 0.2f, 0.25f), material, accent * 0.6f, Color.black, true);
        Part(t, "Toolbox", new Vector3(-0.6f, 1.02f, -0.05f), new Vector3(0.5f, 0.25f, 0.25f), material, new Color(0.7f, 0.15f, 0.1f), Color.black, true);
        Part(t, "Wrench", new Vector3(-0.5f, 1.6f, -0.38f), new Vector3(0.06f, 0.45f, 0.02f), material, Metal, Color.black, false);
        Part(t, "Saw", new Vector3(0.1f, 1.55f, -0.38f), new Vector3(0.5f, 0.15f, 0.02f), material, Metal, Color.black, false);
        Part(t, "Hammer", new Vector3(0.55f, 1.6f, -0.38f), new Vector3(0.05f, 0.35f, 0.02f), material, new Color(0.4f, 0.28f, 0.16f), Color.black, false);
        console.screen = Part(t, "Lamp", new Vector3(0f, 2.1f, -0.3f), new Vector3(0.6f, 0.06f, 0.15f), material, accent * 0.3f, accent * 2f, false);
        body.center = new Vector3(0f, 0.6f, -0.05f);
        body.size = new Vector3(2.3f, 1.2f, 1f);
    }

    // A medical cabinet with a red cross, a cot beside it and an IV stand.
    private static void BuildMedStation(StationConsole console, Transform t, BoxCollider body, Material material, Color accent)
    {
        Part(t, "Cabinet", new Vector3(-0.6f, 0.9f, -0.15f), new Vector3(1f, 1.8f, 0.55f), material, new Color(0.75f, 0.78f, 0.78f), Color.black, true);
        Part(t, "Cross H", new Vector3(-0.6f, 1.35f, 0.14f), new Vector3(0.4f, 0.12f, 0.02f), material, new Color(0.85f, 0.1f, 0.1f), new Color(0.4f, 0f, 0f), false);
        Part(t, "Cross V", new Vector3(-0.6f, 1.35f, 0.14f), new Vector3(0.12f, 0.4f, 0.02f), material, new Color(0.85f, 0.1f, 0.1f), new Color(0.4f, 0f, 0f), false);
        Part(t, "Cot", new Vector3(0.75f, 0.45f, 0.25f), new Vector3(0.8f, 0.12f, 1.9f), material, new Color(0.45f, 0.5f, 0.42f), Color.black, true);
        Part(t, "Cot Frame", new Vector3(0.75f, 0.2f, 0.25f), new Vector3(0.75f, 0.4f, 1.8f), material, DarkMetal, Color.black, true);
        Part(t, "Pillow", new Vector3(0.75f, 0.55f, -0.5f), new Vector3(0.6f, 0.1f, 0.3f), material, new Color(0.8f, 0.8f, 0.78f), Color.black, false);
        Part(t, "IV Pole", new Vector3(0.15f, 0.9f, -0.5f), new Vector3(0.04f, 0.9f, 0.04f), material, Metal, Color.black, false, PrimitiveType.Cylinder);
        Part(t, "IV Bag", new Vector3(0.15f, 1.65f, -0.45f), new Vector3(0.16f, 0.24f, 0.06f), material, accent * 0.5f, accent * 0.8f, false);
        console.screen = Part(t, "Screen", new Vector3(-0.6f, 0.75f, 0.14f), new Vector3(0.6f, 0.35f, 0.02f), material, ScreenGlow * 0.3f, ScreenGlow * 2.2f, false);
        body.center = new Vector3(0.05f, 0.6f, 0.1f);
        body.size = new Vector3(2.3f, 1.2f, 2f);
    }

    // A planning table with a glowing map of the floors, pins, a field radio and a
    // desk lamp.
    private static void BuildIntelDesk(StationConsole console, Transform t, BoxCollider body, Material material, Color accent)
    {
        Part(t, "Table", new Vector3(0f, 0.8f, 0f), new Vector3(2.6f, 0.1f, 1.4f), material, new Color(0.35f, 0.25f, 0.15f), Color.black, true);
        Part(t, "Table Base", new Vector3(0f, 0.4f, 0f), new Vector3(2.2f, 0.8f, 1f), material, new Color(0.2f, 0.15f, 0.1f), Color.black, true);
        console.screen = Part(t, "Map", new Vector3(-0.2f, 0.86f, 0f), new Vector3(1.8f, 0.02f, 1f), material, ScreenGlow * 0.25f, ScreenGlow * 1.2f, false);
        Part(t, "Pin", new Vector3(-0.6f, 0.92f, 0.2f), new Vector3(0.05f, 0.1f, 0.05f), material, new Color(0.9f, 0.15f, 0.1f), new Color(0.6f, 0f, 0f), false);
        Part(t, "Pin", new Vector3(0.3f, 0.92f, -0.25f), new Vector3(0.05f, 0.1f, 0.05f), material, new Color(0.9f, 0.15f, 0.1f), new Color(0.6f, 0f, 0f), false);
        Part(t, "Pin", new Vector3(0.1f, 0.92f, 0.3f), new Vector3(0.05f, 0.1f, 0.05f), material, accent, accent, false);
        Part(t, "Radio", new Vector3(1f, 1.0f, -0.35f), new Vector3(0.4f, 0.3f, 0.25f), material, new Color(0.2f, 0.22f, 0.18f), Color.black, false);
        Part(t, "Aerial", new Vector3(1.12f, 1.35f, -0.4f), new Vector3(0.02f, 0.5f, 0.02f), material, DarkMetal, Color.black, false, PrimitiveType.Cylinder);
        Part(t, "Lamp", new Vector3(-1.1f, 1.05f, -0.5f), new Vector3(0.12f, 0.4f, 0.12f), material, DarkMetal, Color.black, false, PrimitiveType.Cylinder);
        body.center = new Vector3(0f, 0.6f, 0f);
        body.size = new Vector3(2.6f, 1.2f, 1.4f);
    }

    // A market stall: a counter of goods under a striped awning on poles, a cash box
    // and a lamp hanging over it.
    private static void BuildTradePost(StationConsole console, Transform t, BoxCollider body, Material material, Color accent)
    {
        Part(t, "Counter", new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 1f, 0.7f), material, new Color(0.35f, 0.25f, 0.15f), Color.black, true);
        Part(t, "Counter Top", new Vector3(0f, 1.03f, 0.02f), new Vector3(2.5f, 0.06f, 0.8f), material, new Color(0.45f, 0.33f, 0.2f), Color.black, true);
        Part(t, "Cash Box", new Vector3(0.85f, 1.13f, 0.05f), new Vector3(0.35f, 0.15f, 0.25f), material, DarkMetal, Color.black, false);
        Part(t, "Medkit", new Vector3(-0.8f, 1.15f, 0.1f), new Vector3(0.3f, 0.18f, 0.22f), material, new Color(0.8f, 0.15f, 0.12f), new Color(0.2f, 0f, 0f), false);
        Part(t, "Grenade", new Vector3(-0.35f, 1.12f, 0.15f), new Vector3(0.12f, 0.14f, 0.12f), material, new Color(0.25f, 0.32f, 0.18f), Color.black, false, PrimitiveType.Sphere);
        Part(t, "Bottle", new Vector3(0.05f, 1.2f, 0.05f), new Vector3(0.08f, 0.28f, 0.08f), material, new Color(0.2f, 0.45f, 0.25f), new Color(0.02f, 0.08f, 0.03f), false, PrimitiveType.Cylinder);
        Part(t, "Bottle", new Vector3(0.2f, 1.2f, 0.12f), new Vector3(0.08f, 0.28f, 0.08f), material, new Color(0.45f, 0.3f, 0.12f), new Color(0.08f, 0.04f, 0f), false, PrimitiveType.Cylinder);
        Part(t, "Pole", new Vector3(-1.2f, 1.2f, -0.45f), new Vector3(0.07f, 2.4f, 0.07f), material, DarkMetal, Color.black, true, PrimitiveType.Cylinder);
        Part(t, "Pole", new Vector3(1.2f, 1.2f, -0.45f), new Vector3(0.07f, 2.4f, 0.07f), material, DarkMetal, Color.black, true, PrimitiveType.Cylinder);
        for (int i = 0; i < 5; i++)
            Part(t, "Awning", new Vector3(-1f + i * 0.5f, 2.35f, 0f), new Vector3(0.5f, 0.05f, 1.1f), material,
                i % 2 == 0 ? accent * 0.7f : new Color(0.75f, 0.72f, 0.65f), Color.black, false);
        Part(t, "Shelf", new Vector3(0f, 1.6f, -0.5f), new Vector3(2.2f, 0.06f, 0.3f), material, new Color(0.3f, 0.22f, 0.13f), Color.black, false);
        Part(t, "Crate", new Vector3(-0.6f, 1.8f, -0.5f), new Vector3(0.45f, 0.35f, 0.28f), material, new Color(0.3f, 0.35f, 0.2f), Color.black, false);
        Part(t, "Tins", new Vector3(0.4f, 1.72f, -0.5f), new Vector3(0.5f, 0.18f, 0.2f), material, new Color(0.6f, 0.6f, 0.62f), Color.black, false);
        console.screen = Part(t, "Lamp", new Vector3(0f, 2.15f, 0.1f), new Vector3(0.25f, 0.15f, 0.25f), material, accent * 0.4f, accent * 2.2f, false, PrimitiveType.Sphere);
        body.center = new Vector3(0f, 0.6f, -0.1f);
        body.size = new Vector3(2.6f, 1.2f, 1f);
    }

    // A field kitchen: a steel range with an oven door, pots on the hob over a burner's
    // flame, a prep shelf and an extractor hood on the wall above.
    private static void BuildKitchen(StationConsole console, Transform t, BoxCollider body, Material material, Color accent)
    {
        Part(t, "Range", new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.9f, 0.8f), material, Metal, Color.black, true);
        Part(t, "Oven Door", new Vector3(0f, 0.4f, 0.41f), new Vector3(1.1f, 0.5f, 0.02f), material, DarkMetal, Color.black, false);
        Part(t, "Hob", new Vector3(0f, 0.92f, 0f), new Vector3(1.62f, 0.04f, 0.82f), material, DarkMetal, Color.black, true);
        Part(t, "Pot", new Vector3(-0.4f, 1.1f, 0.05f), new Vector3(0.45f, 0.32f, 0.45f), material, new Color(0.55f, 0.56f, 0.58f), Color.black, false, PrimitiveType.Cylinder);
        Part(t, "Pan", new Vector3(0.4f, 0.98f, 0.1f), new Vector3(0.5f, 0.06f, 0.5f), material, new Color(0.15f, 0.15f, 0.16f), Color.black, false, PrimitiveType.Cylinder);
        Part(t, "Shelf", new Vector3(1.15f, 0.5f, 0f), new Vector3(0.6f, 1f, 0.7f), material, new Color(0.4f, 0.28f, 0.16f), Color.black, true);
        Part(t, "Hood", new Vector3(0f, 2.05f, -0.2f), new Vector3(1.4f, 0.3f, 0.5f), material, Metal, Color.black, false);
        console.screen = Part(t, "Flame", new Vector3(0.4f, 0.95f, 0.1f), new Vector3(0.3f, 0.03f, 0.3f), material, accent * 0.4f, accent * 2.2f, false, PrimitiveType.Cylinder);
        body.center = new Vector3(0.15f, 0.6f, 0f);
        body.size = new Vector3(2.2f, 1.2f, 0.9f);
    }

    // A reloading press: a frame with a ram that pumps up and down, ammo crates beside it.
    private static void BuildAmmoPress(StationConsole console, Transform t, BoxCollider body, Material material, Color accent)
    {
        Part(t, "Base", new Vector3(0f, 0.45f, 0f), new Vector3(1.2f, 0.9f, 0.8f), material, new Color(0.25f, 0.3f, 0.2f), Color.black, true);
        Part(t, "Column", new Vector3(0f, 1.45f, -0.25f), new Vector3(0.2f, 1.1f, 0.2f), material, DarkMetal, Color.black, true);
        Part(t, "Head", new Vector3(0f, 2f, -0.05f), new Vector3(0.5f, 0.25f, 0.6f), material, DarkMetal, Color.black, true);
        console.moving = Part(t, "Ram", new Vector3(0f, 1.5f, 0.1f), new Vector3(0.14f, 0.6f, 0.14f), material, Metal, Color.black, false, PrimitiveType.Cylinder).transform;
        Part(t, "Die", new Vector3(0f, 0.95f, 0.1f), new Vector3(0.3f, 0.1f, 0.3f), material, accent * 0.5f, accent * 0.3f, false);
        Part(t, "Crate", new Vector3(1.05f, 0.25f, 0.1f), new Vector3(0.7f, 0.5f, 0.5f), material, new Color(0.3f, 0.35f, 0.2f), Color.black, true);
        Part(t, "Crate", new Vector3(1.05f, 0.65f, 0.1f), new Vector3(0.6f, 0.3f, 0.45f), material, new Color(0.32f, 0.36f, 0.22f), Color.black, true);
        Part(t, "Shells", new Vector3(-0.85f, 0.95f, 0.1f), new Vector3(0.35f, 0.1f, 0.35f), material, new Color(0.7f, 0.55f, 0.2f), new Color(0.2f, 0.15f, 0f), false);
        console.screen = Part(t, "Screen", new Vector3(-0.3f, 0.6f, 0.41f), new Vector3(0.45f, 0.28f, 0.02f), material, ScreenGlow * 0.3f, ScreenGlow * 2.2f, false);
        body.center = new Vector3(0.25f, 0.6f, 0.05f);
        body.size = new Vector3(1.8f, 1.2f, 0.9f);
    }
}
