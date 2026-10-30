using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Small wireframe pictures of the crafting materials for the inventory, matching their pickup
// models: a gear and bolt (parts), a cable drum (wiring), a flask and vial (chemicals), a powder
// keg and pouch (gunpowder) and a jerry can (fuel). Drawn in a 100x50 box (y down) and fitted
// into the cell, like the attachments' pictures (AttachmentSilhouette).
// With the sprite art the item's own sprite is drawn instead (PixelIcon); these are the fallback.
public static class MaterialSilhouette
{
    private static readonly LineArt Parts = new LineArt(0, 0, 100, 50)
        .Shape(Gear(30f, 25f, 19f, 14f, 8))                     // gear
        .Circle(30, 25, 5, false)                               // axle hole
        .Box(58, 15, 67, 35)                                    // bolt head
        .Box(67, 21, 94, 29)                                    // shank
        .Wire(74, 21, 72, 29)                                   // thread
        .Wire(80, 21, 78, 29)
        .Wire(86, 21, 84, 29);

    private static readonly LineArt Wiring = new LineArt(0, 0, 100, 50)
        .Box(24, 3, 31, 47)                                     // drum flanges
        .Box(67, 3, 74, 47)
        .Box(31, 9, 67, 41)                                     // wound cable
        .Wire(31, 15, 67, 15)
        .Wire(31, 21, 67, 21)
        .Wire(31, 27, 67, 27)
        .Wire(31, 33, 67, 33)
        .Wire(67, 37, 80, 40, 88, 34, 94, 38);                  // loose end

    private static readonly LineArt Chemicals = new LineArt(0, 0, 100, 50)
        .Box(37, 3, 49, 8)                                      // stopper
        .Shape(39, 8, 47, 8, 47, 18, 62, 45, 24, 45, 39, 18)    // flask
        .Wire(32, 32, 54, 32)                                   // liquid line
        .Box(67, 7, 77, 12)                                     // vial cap
        .Box(68, 12, 76, 45)                                    // vial
        .Wire(68, 30, 76, 30);

    private static readonly LineArt Gunpowder = new LineArt(0, 0, 100, 50)
        .Shape(26, 5, 56, 5, 61, 25, 56, 45, 26, 45, 21, 25)    // keg
        .Wire(24, 13, 58, 13)                                   // hoops
        .Wire(24, 37, 58, 37)
        .Hole(37, 20, 45, 20, 45, 30, 37, 30)                   // bung
        .Shape(70, 22, 82, 22, 88, 45, 64, 45)                  // pouch
        .Wire(68, 27, 84, 27)                                   // drawstring
        .Wire(76, 22, 74, 15, 79, 13);

    private static readonly LineArt Fuel = new LineArt(0, 0, 100, 50)
        .Shape(28, 12, 68, 12, 72, 16, 72, 46, 24, 46, 24, 16)  // can
        .Hole(30, 4, 44, 4, 44, 12, 30, 12)                     // handle
        .Box(58, 5, 66, 12)                                     // spout
        .Wire(30, 20, 66, 40)                                   // pressed X
        .Wire(66, 20, 30, 40);

    public static void Draw(VertexHelper vh, MaterialType type, Rect area, Color colour)
    {
        if (!PixelIcon.DrawItem(vh, ItemSprites.Material(type), area, colour))
            Drawing(type).Draw(vh, area, colour);
    }

    private static LineArt Drawing(MaterialType type)
    {
        switch (type)
        {
            case MaterialType.Parts: return Parts;
            case MaterialType.Wiring: return Wiring;
            case MaterialType.Chemicals: return Chemicals;
            case MaterialType.Gunpowder: return Gunpowder;
            default: return Fuel;
        }
    }

    // A toothed wheel's outline: `teeth` square-ish teeth between the inner and outer radius.
    private static float[] Gear(float cx, float cy, float outer, float inner, int teeth)
    {
        List<float> xy = new List<float>();

        for (int i = 0; i < teeth; i++)
        {
            float step = Mathf.PI * 2f / teeth;
            float start = i * step;

            foreach ((float at, float radius) in new[] { (0f, inner), (0.15f, outer), (0.5f, outer), (0.65f, inner) })
            {
                float angle = start + at * step;
                xy.Add(cx + Mathf.Cos(angle) * radius);
                xy.Add(cy + Mathf.Sin(angle) * radius);
            }
        }

        return xy.ToArray();
    }
}
