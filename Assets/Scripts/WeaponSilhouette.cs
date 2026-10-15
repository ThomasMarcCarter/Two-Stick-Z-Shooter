using UnityEngine;
using UnityEngine.UI;
using static MapMesh;

// Flat gun silhouettes for the HUD, drawn from simple shapes so they match the line-art style of
// the radar and map. Each gun is laid out in a 2:1 box facing right (0-1 across, 0-0.5 up) and
// scaled to fit the area it's drawn in.
public static class WeaponSilhouette
{
    public static void Draw(VertexHelper vh, Gun.Icon icon, Rect area, Color colour)
    {
        // Fit the 2:1 box inside the area, centred.
        float width = Mathf.Min(area.width, area.height * 2f);
        Vector2 origin = area.center - new Vector2(width, width * 0.5f) / 2f;

        Vector2 P(float x, float y) => origin + new Vector2(x, y) * width;
        void Box(float x0, float y0, float x1, float y1) => FillRect(vh, Rect.MinMaxRect(P(x0, y0).x, P(x0, y0).y, P(x1, y1).x, P(x1, y1).y), colour);
        void Shape(Vector2 a, Vector2 b, Vector2 c, Vector2 d) => Quad(vh, a, b, c, d, colour);

        switch (icon)
        {
            case Gun.Icon.Pistol:
                Box(0.18f, 0.30f, 0.82f, 0.42f);                                           // slide
                Box(0.82f, 0.32f, 0.88f, 0.40f);                                           // muzzle
                Box(0.24f, 0.25f, 0.70f, 0.31f);                                           // frame
                Shape(P(0.26f, 0.26f), P(0.20f, 0.04f), P(0.34f, 0.04f), P(0.40f, 0.26f));  // grip
                Box(0.42f, 0.17f, 0.54f, 0.19f);                                           // trigger guard
                Box(0.52f, 0.17f, 0.54f, 0.25f);
                break;

            case Gun.Icon.Shotgun:
                Box(0.36f, 0.30f, 0.98f, 0.36f);                                           // barrel
                Box(0.40f, 0.24f, 0.86f, 0.29f);                                           // magazine tube
                Box(0.56f, 0.21f, 0.74f, 0.31f);                                           // pump
                Box(0.24f, 0.22f, 0.40f, 0.37f);                                           // receiver
                Shape(P(0.26f, 0.23f), P(0.22f, 0.10f), P(0.30f, 0.10f), P(0.34f, 0.23f));  // grip
                Shape(P(0.02f, 0.14f), P(0.02f, 0.30f), P(0.25f, 0.36f), P(0.25f, 0.23f));  // stock
                break;

            case Gun.Icon.Rifle:   // AK-style: long handguard, curved magazine, solid stock
                Box(0.66f, 0.31f, 0.98f, 0.34f);                                           // barrel
                Box(0.52f, 0.28f, 0.70f, 0.35f);                                           // handguard
                Box(0.22f, 0.25f, 0.54f, 0.37f);                                           // receiver
                Box(0.84f, 0.34f, 0.86f, 0.38f);                                           // front sight
                Shape(P(0.42f, 0.25f), P(0.42f, 0.14f), P(0.49f, 0.14f), P(0.50f, 0.25f));  // magazine, upper
                Shape(P(0.42f, 0.14f), P(0.46f, 0.04f), P(0.53f, 0.06f), P(0.49f, 0.14f));  // magazine, curving forward
                Shape(P(0.26f, 0.25f), P(0.22f, 0.11f), P(0.29f, 0.11f), P(0.33f, 0.25f));  // grip
                Shape(P(0.02f, 0.18f), P(0.02f, 0.31f), P(0.23f, 0.36f), P(0.23f, 0.26f));  // stock
                break;

            case Gun.Icon.Carbine: // M4-style: carry handle, straight magazine, skeleton stock
                Box(0.62f, 0.31f, 0.96f, 0.34f);                                           // barrel
                Box(0.50f, 0.27f, 0.66f, 0.36f);                                           // handguard
                Box(0.24f, 0.25f, 0.52f, 0.36f);                                           // receiver
                Box(0.28f, 0.38f, 0.46f, 0.41f);                                           // carry handle
                Box(0.30f, 0.36f, 0.32f, 0.38f);
                Box(0.43f, 0.36f, 0.45f, 0.38f);
                Box(0.62f, 0.34f, 0.64f, 0.40f);                                           // front sight
                Box(0.40f, 0.08f, 0.47f, 0.25f);                                           // magazine
                Shape(P(0.27f, 0.25f), P(0.23f, 0.11f), P(0.30f, 0.11f), P(0.34f, 0.25f));  // grip
                Box(0.04f, 0.24f, 0.24f, 0.27f);                                           // stock tube
                Shape(P(0.02f, 0.18f), P(0.02f, 0.33f), P(0.07f, 0.33f), P(0.07f, 0.18f));  // butt
                break;
        }
    }
}
