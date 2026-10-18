using UnityEngine;
using UnityEngine.UI;

// Wireframe gun drawings for the HUD and inventory, matching the 3D gun models: each one uses the
// same side-view outlines (in reference-image pixels, muzzle to the right) that the Blender
// scripts in Tools/Blender/ extrude into the models, so if a model is reshaped, copy its new
// numbers here (for guns built with gunkit.py, Tools/Blender/export_icons.py prints them).
// Drawn fitted to the area, centred, with a faint fill under bright outlines.
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

    // MP5 (Tools/Blender/MP5.py), generated by Tools/Blender/export_icons.py.
    private static readonly LineArt MP5 = new LineArt(52, 55, 1165, 473)
        .Shape(60, 125, 100, 125, 98, 150, 92, 190, 82, 300, 75, 315, 60, 312, 57, 290, 70, 180, 62, 140)  // ButtPad
        .Shape(98, 143.9f, 380, 143.9f, 380, 158.1f, 98, 158.1f)                                // StockRodLeft
        .Shape(373, 140, 455, 140, 458, 200, 390, 200, 375, 175)                                // EndCap
        .Shape(418, 110, 440, 96, 840, 99, 840, 190, 458, 195, 418, 165)                        // Receiver
        .Shape(470, 100, 476, 76, 490, 60, 524, 60, 540, 80, 540, 100)                          // RearSight
        .Shape(835, 112, 1078, 112, 1086, 120, 1086, 178, 1078, 186, 835, 186)                  // Handguard
        .Shape(838, 98, 1086, 101, 1086, 113, 838, 112)                                         // CockingTube
        .Shape(915, 62, 945, 62, 945, 100, 915, 100)                                            // CockingHandle
        .Shape(1072, 150, 1072, 90, 1080, 62, 1100, 62, 1106, 90, 1106, 150)                    // FrontSight
        .Hole(1080, 70, 1098, 70, 1098, 88, 1080, 88)                                           // FrontSight opening
        .Shape(1100, 141, 1148, 141, 1148, 163, 1100, 163)                                      // Barrel
        .Shape(1140, 138.6f, 1160, 138.6f, 1160, 165.4f, 1140, 165.4f)                          // Muzzle
        .Shape(450, 190, 762, 190, 766, 250, 700, 270, 560, 268, 540, 250, 450, 250)            // TriggerGroup
        .Shape(482, 250, 566, 255, 546, 300, 512, 386, 470, 386, 420, 356, 450, 300)            // PistolGrip
        .Shape(556, 262, 668, 262, 664, 292, 578, 293)                                          // TriggerGuard
        .Hole(566, 262, 658, 262, 655, 284, 584, 285)                                           // TriggerGuard opening
        .Shape(596, 262, 606, 262, 612, 278, 604, 284)                                          // Trigger
        .Shape(700, 192, 772, 192, 772, 255, 705, 255)                                          // MagWell
        .Shape(722, 212, 768, 210, 790, 300, 820, 400, 858, 450, 850, 466, 812, 468, 790, 410, 760, 320, 735, 250);  // Magazine

    // Uzi (Tools/Blender/Uzi.py), generated by Tools/Blender/export_icons.py.
    private static readonly LineArt Uzi = new LineArt(7, 95, 1197, 655)
        .Shape(80, 140, 230, 150, 870, 148, 955, 165, 955, 184, 645, 184, 645, 234, 80, 240)    // Receiver
        .Shape(345, 232, 642, 232, 642, 282, 345, 284)                                          // LowerReceiver
        .Shape(645, 180, 942, 180, 952, 192, 952, 222, 942, 232, 645, 232)                      // Handguard
        .Shape(90, 150, 95, 115, 120, 100, 175, 100, 200, 120, 200, 150)                        // RearSight
        .Shape(878, 165, 880, 110, 895, 100, 935, 100, 950, 115, 950, 165)                      // FrontSight
        .Shape(698, 150, 698, 110, 742, 110, 742, 150)                                          // CockingKnob
        .Shape(955, 184.3f, 1030, 184.3f, 1030, 259.7f, 955, 259.7f)                            // BarrelNut
        .Shape(1028, 205.7f, 1192, 205.7f, 1192, 238.3f, 1028, 238.3f)                          // Barrel
        .Shape(365, 280, 510, 280, 515, 465, 518, 535, 430, 535, 370, 500, 365, 400)            // Grip
        .Shape(425, 530, 500, 530, 500, 650, 425, 650)                                          // Magazine
        .Shape(512, 280, 645, 280, 645, 320, 630, 346, 560, 349, 512, 316)                      // TriggerGuard
        .Hole(522, 280, 635, 280, 635, 316, 622, 338, 565, 341, 522, 310)                       // TriggerGuard opening
        .Shape(548, 284, 560, 284, 582, 344, 571, 348)                                          // Trigger
        .Shape(30, 238, 300, 238, 300, 262, 40, 262)                                            // StockArm
        .Shape(55, 330, 160, 262, 205, 262, 72, 352)                                            // StockBrace
        .Shape(12, 240, 50, 240, 70, 320, 66, 520, 42, 536, 25, 522, 15, 330);                  // ButtPlate

    // Revolver (Tools/Blender/Revolver.py), generated by Tools/Blender/export_icons.py.
    private static readonly LineArt Revolver = new LineArt(13, 22, 505, 240)
        .Shape(300, 35.2f, 500, 35.2f, 500, 64.8f, 300, 64.8f)                                  // Barrel
        .Shape(446, 42, 448, 33, 458, 33, 460, 42)                                              // FrontSight
        .Shape(300, 56.7f, 448, 56.7f, 448, 75.3f, 300, 75.3f)                                  // EjectorHousing
        .Shape(448, 60, 470, 60, 470, 76, 452, 78)                                              // EjectorHead
        .Shape(186, 30.8f, 318, 30.8f, 318, 101.2f, 186, 101.2f)                                // Cylinder
        .Shape(172, 30, 304, 32, 304, 40, 178, 40)                                              // TopStrap
        .Shape(150, 85, 168, 70, 186, 70, 186, 100, 304, 100, 306, 110, 292, 126, 172, 128, 150, 120)  // Frame
        .Shape(300, 32, 318, 36, 318, 100, 300, 100)                                            // FrameFront
        .Shape(148, 30, 158, 27, 176, 44, 184, 70, 168, 74, 162, 50, 150, 36)                   // Hammer
        .Shape(180, 118, 262, 118, 260, 150, 245, 168, 215, 172, 192, 160, 180, 140)            // TriggerGuard
        .Hole(192, 120, 250, 120, 248, 148, 238, 160, 216, 163, 198, 153, 192, 138)             // TriggerGuard opening
        .Shape(200, 120, 208, 120, 216, 145, 206, 150, 201, 135)                                // Trigger
        .Shape(152, 92, 178, 125, 160, 140, 140, 165, 128, 200, 125, 226, 22, 228, 30, 205, 55, 160, 85, 120, 120, 98)  // Grip
        .Shape(20, 225, 127, 223, 127, 234, 18, 235);                                           // ButtCap

    // DoubleBarrel (Tools/Blender/DoubleBarrel.py), generated by Tools/Blender/export_icons.py.
    private static readonly LineArt DoubleBarrel = new LineArt(21, 91, 565, 219)
        .Shape(26, 137, 160, 127, 176, 131, 192, 142, 225, 125, 252, 114, 264, 113, 264, 130, 258, 133, 248, 137, 228, 146, 205, 170, 197, 182, 178, 179, 170, 172, 40, 214, 26, 212)  // Stock
        .Shape(300, 112, 330, 110, 455, 113, 462, 118, 455, 125, 305, 128)                      // Forend
        .Shape(262, 112, 282, 102, 306, 102, 306, 124, 298, 134, 275, 139, 262, 134)            // Action
        .Shape(242, 136, 277, 135, 277, 150, 267, 160, 250, 160, 242, 152)                      // TriggerGuard
        .Hole(248, 139, 271, 139, 271, 149, 262, 155, 252, 155, 248, 149)                       // TriggerGuard opening
        .Shape(255, 137, 259, 137, 263, 150, 257, 152)                                          // Trigger
        .Shape(266, 104, 282, 101, 283, 106, 268, 108)                                          // TopLever
        .Shape(300, 101.7f, 560, 101.7f, 560, 114.3f, 300, 114.3f)                              // BarrelLeft
        .Shape(300, 99, 560, 100, 560, 104, 300, 104)                                           // Rib
        .Shape(552, 100, 553, 96, 557, 96, 558, 100);                                           // FrontBead

    // HuntingRifle (Tools/Blender/HuntingRifle.py), generated by Tools/Blender/export_icons.py.
    private static readonly LineArt HuntingRifle = new LineArt(87, 163, 1540, 521)
        .Shape(104, 309, 377, 300, 465, 294, 1310, 292, 1325, 300, 1320, 325, 1300, 333, 700, 355, 690, 350, 600, 350, 560, 365, 530, 395, 515, 435, 495, 446, 465, 445, 445, 430, 420, 425, 277, 477, 146, 515, 104, 515)  // Stock
        .Shape(92, 309, 105, 309, 105, 516, 92, 514)                                            // ButtPad
        .Shape(430, 266, 870, 259, 870, 294, 430, 294)                                          // Receiver
        .Shape(410, 264.3f, 432, 264.3f, 432, 293.7f, 410, 293.7f)                              // BoltShroud
        .Shape(452, 272, 470, 272, 470, 300, 452, 300)                                          // BoltHandle
        .Shape(446, 292, 474, 292, 474, 318, 446, 318)                                          // BoltKnob
        .Shape(590, 350, 690, 350, 688, 385, 670, 405, 615, 407, 595, 390)                      // TriggerGuard
        .Hole(600, 352, 680, 352, 678, 382, 665, 397, 620, 399, 604, 386)                       // TriggerGuard opening
        .Shape(632, 352, 642, 352, 650, 385, 640, 388)                                          // Trigger
        .Shape(868, 263.6f, 1320, 263.6f, 1320, 290.4f, 868, 290.4f)                            // Barrel
        .Shape(1318, 265.6f, 1535, 265.6f, 1535, 288.4f, 1318, 288.4f)                          // BarrelTaper
        .Shape(732, 224, 760, 224, 760, 260, 732, 260)                                          // RingRear
        .Shape(935, 224, 963, 224, 963, 260, 935, 260)                                          // RingFront
        .Shape(563, 173.5f, 600, 173.5f, 600, 240.5f, 563, 240.5f)                              // ScopeEyepiece
        .Shape(600, 181.6f, 720, 181.6f, 720, 232.4f, 600, 232.4f)                              // ScopeOcular
        .Shape(718, 190.9f, 1012, 190.9f, 1012, 223.1f, 718, 223.1f)                            // ScopeTube
        .Shape(1010, 173.5f, 1105, 173.5f, 1105, 240.5f, 1010, 240.5f)                          // ScopeBell
        .Shape(800, 168, 836, 168, 836, 200, 800, 200)                                          // ScopeTurret
        .Circle(818, 207, 20.1f);                                                               // ScopeKnob

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
            case Gun.Icon.SMG: return MP5;
            case Gun.Icon.MachinePistol: return Uzi;
            case Gun.Icon.Revolver: return Revolver;
            case Gun.Icon.DoubleBarrel: return DoubleBarrel;
            case Gun.Icon.BoltAction: return HuntingRifle;
            default: return Rifle;
        }
    }
}
