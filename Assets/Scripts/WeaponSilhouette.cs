using UnityEngine;
using UnityEngine.UI;

// Wireframe gun drawings for the HUD and inventory, matching the 3D gun models: each one uses the
// same side-view outlines (in reference-image pixels, muzzle to the right) that the Blender
// scripts in Tools/Blender/ extrude into the models, so if a model is reshaped, copy its new
// numbers here. Drawn fitted to the area, centred, with a faint fill under bright outlines.
public static class WeaponSilhouette
{
    // Glock-style pistol (Tools/Blender/Pistol.py).
    private static readonly LineArt Pistol = new LineArt(30, 45, 562, 395)
        .Shape(63, 109, 555, 109, 555, 148, 530, 150, 365, 150, 372, 225, 360, 229, 232, 228,           // frame
               225, 250, 210, 270, 195, 310, 185, 340, 175, 375, 35, 375, 38, 340, 55, 280, 80, 175, 88, 150, 63, 126)
        .Hole(250, 158, 352, 158, 352, 214, 262, 214, 248, 196)                                          // trigger guard opening
        .Shape(282, 172, 290, 168, 300, 186, 312, 210, 306, 212, 292, 192, 284, 186)                     // trigger
        .Shape(57, 375, 160, 375, 158, 390, 60, 390)                                                    // magazine base
        .Shape(67, 58, 70, 56, 550, 56, 557, 62, 557, 108, 67, 108)                                     // slide
        .Hole(262, 58, 336, 58, 336, 87, 262, 87)                                                       // ejection port
        .Shape(93, 56, 95, 49, 106, 49, 108, 56)                                                        // rear sight
        .Shape(529, 56, 531, 50, 537, 50, 539, 56);                                                     // front sight

    // Pump-action shotgun (Tools/Blender/Shotgun.py).
    private static readonly LineArt Shotgun = new LineArt(12, 82, 850, 254)
        .Box(440, 92, 845, 108)                                                                         // barrel
        .Shape(814, 92, 816, 86, 822, 86, 824, 92)                                                      // front bead
        .Box(600, 110, 803, 126)                                                                        // magazine tube
        .Box(803, 108, 838, 128)                                                                        // magazine cap
        .Box(795, 99, 803, 119)                                                                         // barrel band
        .Shape(450, 104, 600, 104, 608, 110, 608, 132, 600, 140, 450, 140)                              // forend
        .Shape(289, 143, 348, 143, 342, 168, 326, 178, 300, 178, 289, 166)                              // trigger guard
        .Hole(297, 145, 338, 145, 334, 164, 322, 171, 303, 171, 297, 162)
        .Shape(318, 146, 324, 146, 322, 166, 316, 162)                                                  // trigger
        .Shape(278, 110, 296, 96, 450, 96, 450, 144, 290, 144, 278, 140)                                // receiver
        .Hole(356, 99, 430, 99, 430, 110, 356, 110)                                                     // ejection port
        .Shape(24, 137, 120, 128, 205, 119, 232, 129, 282, 110, 282, 142, 262, 150, 242, 178,            // stock
               230, 204, 212, 212, 198, 200, 40, 250, 24, 246)
        .Shape(17, 137, 25, 137, 25, 247, 17, 245);                                                     // recoil pad

    // AK-47 (Tools/Blender/AK47.py).
    private static readonly LineArt Rifle = new LineArt(35, 240, 1183, 594)
        .Box(885, 266, 990, 288)                                                                        // gas tube
        .Box(885, 300, 1165, 322)                                                                       // barrel
        .Box(1150, 296, 1178, 326)                                                                      // muzzle
        .Shape(985, 264, 1002, 264, 1010, 300, 1010, 324, 985, 324)                                     // gas block
        .Shape(1124, 252, 1146, 252, 1150, 300, 1120, 300)                                              // front sight
        .Box(1118, 300, 1150, 326)
        .Shape(738, 260, 885, 262, 885, 294, 738, 294)                                                  // upper handguard
        .Shape(682, 298, 885, 298, 885, 345, 870, 352, 700, 356, 682, 350)                              // lower handguard
        .Shape(45, 345, 70, 340, 338, 300, 338, 372, 300, 385, 70, 452, 45, 448)                        // stock
        .Box(40, 343, 48, 450)                                                                          // butt plate
        .Wire(441, 362, 441, 398, 525, 397, 525, 358)                                                   // trigger guard
        .Shape(335, 292, 348, 266, 655, 262, 685, 272, 685, 350, 600, 352, 525, 358, 335, 372)          // receiver
        .Shape(652, 262, 738, 256, 738, 298, 652, 298)                                                  // rear sight block
        .Shape(528, 356, 612, 356, 640, 420, 672, 470, 715, 522, 690, 560, 656, 590, 612, 540,          // magazine
               575, 480, 545, 420)
        .Shape(362, 360, 438, 360, 420, 400, 395, 478, 385, 494, 345, 490, 338, 474, 365, 400);         // pistol grip

    // M4A1 carbine (Tools/Blender/M4A1.py).
    private static readonly LineArt Carbine = new LineArt(42, 62, 612, 267)
        .Box(182, 110, 232, 132)                                                                        // buffer tube
        .Box(522, 118, 568, 130)                                                                        // barrel
        .Box(566, 117, 607, 131)                                                                        // flash hider
        .Box(520, 116, 554, 132)                                                                        // front sight base
        .Shape(522, 142, 522, 117, 539, 73, 551, 73, 551, 142, 544, 142, 544, 84, 540, 84, 530, 117, 530, 142)  // front sight
        .Shape(388, 99, 522, 99, 522, 147, 388, 147)                                                    // quad rail
        .Wire(392, 116, 518, 116)
        .Wire(392, 130, 518, 130)
        .Box(380, 103, 390, 146)                                                                        // barrel nut
        .Shape(47, 109, 185, 109, 185, 135, 160, 150, 105, 182, 55, 205, 47, 205)                       // stock
        .Shape(318, 172, 368, 172, 372, 214, 381, 256, 333, 262, 325, 214)                              // magazine
        .Shape(232, 142, 372, 142, 372, 178, 318, 178, 300, 168, 280, 168, 262, 176, 240, 176, 232, 160) // lower receiver
        .Wire(280, 168, 280, 181, 319, 180, 319, 172)                                                   // trigger guard
        .Shape(242, 166, 282, 166, 264, 212, 252, 234, 214, 236, 210, 227, 232, 192)                    // pistol grip
        .Shape(230, 107, 385, 107, 385, 142, 230, 142)                                                  // upper receiver
        .Box(232, 99, 386, 107)                                                                         // top rail
        .Shape(244, 99, 276, 99, 274, 74, 266, 67, 252, 67, 246, 74);                                   // rear sight

    public static void Draw(VertexHelper vh, Gun.Icon icon, Rect area, Color colour)
    {
        // Keep to the old 2:1 box so every gun sits in the same space and the pistol stays smaller.
        float width = Mathf.Min(area.width, area.height * 2f);
        Rect box = new Rect(area.center - new Vector2(width, width * 0.5f) / 2f, new Vector2(width, width * 0.5f));
        Drawing(icon).Draw(vh, box, colour);
    }

    private static LineArt Drawing(Gun.Icon icon)
    {
        switch (icon)
        {
            case Gun.Icon.Pistol: return Pistol;
            case Gun.Icon.Shotgun: return Shotgun;
            case Gun.Icon.Carbine: return Carbine;
            default: return Rifle;
        }
    }
}
