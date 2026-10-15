using UnityEngine;
using UnityEngine.UI;

// Small wireframe pictures of the weapon attachments for the inventory: a suppressor can, a
// flared choke, barrels, magazines, trigger parts and a laser module, drawn side-on in a 100x50
// box (y down) and fitted into the cell. Picked by the attachment's name, since the list is
// editable on the FloorGenerator; anything unrecognised gets its slot's general picture.
public static class AttachmentSilhouette
{
    private static readonly LineArt Suppressor = new LineArt(0, 0, 100, 50)
        .Box(4, 20, 12, 30)                                     // thread
        .Box(12, 13, 94, 37)                                    // can
        .Wire(22, 13, 22, 37)
        .Wire(84, 13, 84, 37);

    private static readonly LineArt Choke = new LineArt(0, 0, 100, 50)
        .Box(16, 19, 58, 31)                                    // threaded body
        .Wire(26, 19, 26, 31)
        .Wire(34, 19, 34, 31)
        .Wire(42, 19, 42, 31)
        .Shape(58, 17, 72, 17, 86, 11, 86, 39, 72, 33, 58, 33)  // flared front
        .Hole(64, 22, 72, 22, 72, 28, 64, 28);                  // port

    private static readonly LineArt LongBarrel = new LineArt(0, 0, 100, 50)
        .Box(2, 16, 20, 34)                                     // chamber
        .Box(20, 21, 92, 29)                                    // barrel
        .Box(92, 19, 98, 31);                                   // crown

    private static readonly LineArt MatchBarrel = new LineArt(0, 0, 100, 50)
        .Box(4, 15, 26, 35)                                     // chamber
        .Box(26, 19, 90, 31)                                    // heavy barrel
        .Wire(32, 23, 84, 23)                                   // flutes
        .Wire(32, 27, 84, 27)
        .Box(90, 17, 97, 33);                                   // crown

    private static readonly LineArt ExtendedMag = new LineArt(0, 0, 100, 50)
        .Shape(42, 2, 58, 2, 61, 40, 39, 40)                    // body
        .Wire(40, 28, 60, 28)                                   // extension seam
        .Hole(47, 7, 53, 7, 53, 22, 47, 22)                     // witness window
        .Box(36, 40, 64, 47);                                   // base pad

    private static readonly LineArt DrumMag = new LineArt(0, 0, 100, 50)
        .Box(43, 1, 57, 12)                                     // feed tower
        .Circle(50, 29, 19)                                     // drum
        .Circle(50, 29, 8, false)                               // winding key
        .Wire(50, 21, 50, 25);

    private static readonly LineArt QuickMag = new LineArt(0, 0, 100, 50)
        .Shape(29, 3, 43, 3, 46, 41, 32, 41)                    // two mags taped together
        .Shape(54, 3, 68, 3, 71, 41, 57, 41)
        .Box(25, 18, 75, 26)                                    // tape
        .Wire(44, 41, 46, 48, 55, 48, 57, 41);                  // pull loop

    private static readonly LineArt HairTrigger = new LineArt(0, 0, 100, 50)
        .Shape(44, 4, 54, 4, 54, 15, 52, 26, 46, 36, 37, 43, 35, 39, 42, 28, 46, 15)  // trigger blade
        .Circle(49, 9, 2.5f, false)                             // pivot pin
        .Wire(60, 8, 67, 12, 60, 16, 67, 20, 60, 24, 67, 28);   // light spring

    private static readonly LineArt AutoSear = new LineArt(0, 0, 100, 50)
        .Shape(26, 10, 70, 10, 70, 22, 46, 22, 46, 42, 26, 42)  // sear body
        .Circle(36, 16, 3.5f, false)                            // pins
        .Circle(36, 34, 3.5f, false)
        .Shape(70, 13, 82, 6, 87, 11, 74, 21);                  // trip lever

    private static readonly LineArt Laser = new LineArt(0, 0, 100, 50)
        .Box(16, 7, 50, 14)                                     // rail clamp
        .Box(10, 14, 58, 35)                                    // housing
        .Hole(16, 20, 30, 20, 30, 28, 16, 28)                   // switch
        .Shape(58, 18, 64, 16, 64, 33, 58, 31)                  // lens
        .Wire(68, 24.5f, 98, 24.5f);                            // beam

    public static void Draw(VertexHelper vh, WeaponAttachment attachment, Rect area, Color colour)
    {
        Drawing(attachment).Draw(vh, area, colour);
    }

    private static LineArt Drawing(WeaponAttachment attachment)
    {
        string name = attachment.name.ToLowerInvariant();

        if (name.Contains("suppress") || name.Contains("silenc")) return Suppressor;
        if (name.Contains("choke")) return Choke;
        if (name.Contains("match")) return MatchBarrel;
        if (name.Contains("barrel")) return LongBarrel;
        if (name.Contains("drum")) return DrumMag;
        if (name.Contains("quick")) return QuickMag;
        if (name.Contains("mag")) return ExtendedMag;
        if (name.Contains("sear") || name.Contains("auto")) return AutoSear;
        if (name.Contains("trigger")) return HairTrigger;
        if (name.Contains("laser")) return Laser;

        switch (attachment.slot)
        {
            case AttachmentSlot.Muzzle: return Suppressor;
            case AttachmentSlot.Barrel: return LongBarrel;
            case AttachmentSlot.Magazine: return ExtendedMag;
            case AttachmentSlot.Trigger: return HairTrigger;
            default: return Laser;
        }
    }
}
