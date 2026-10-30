using UnityEngine;

// A held gun drawn as a sprite (Gungeon-style) instead of its 3D model. It's held out from the
// holder's body at their hands' height (the sheet's holdHeight) and reach (holdReach), on the
// side toward the cursor, with its barrel (the sheet's pivot sits on the barrel's line) pointing
// straight at the cursor on screen, flipped over when it points left so it's never upside down.
// The gun's Muzzle is moved to the drawn barrel's tip at Gun.AimHeight each frame, so shots,
// tracers and the flash come out of the drawn barrel and fly through the crosshair, and the
// holder's flashlight goes there too, shining the way the gun points (and still landing the same
// distance ahead). It stands upright just in front of its holder's sprite, or just behind when
// they face away up the screen (pulled toward the camera when it would dip into the floor), casts no shadow (it would block the flashlight), moves
// through a reload (ReloadAngle: mag out, slapped in, racked; or a nod per shell) and swings through an arc in a melee bash (PlayerMelee.SwingProgress). Added by CharacterVisuals.DressGun for guns with a sheet in SpriteVisuals.
// Attachments are drawn on it as sprites of their own (SpriteVisuals.gunAttachments) that turn,
// flip and tip with the drawn barrel: a long or match barrel on the muzzle, then a suppressor or
// choke on the end of that (shots then come out of the last part's end), a mag attachment hung
// from the bottom of the gun's own mag (the sheet's magazine point; none on guns without one), and
// the laser under the barrel's front with the beam starting at its lens. Trigger parts aren't drawn.
[DefaultExecutionOrder(260)]
public class GunSprite : MonoBehaviour
{
    private const float DepthGap = 0.15f;  // metres in front of / behind the holder's body
    private const float ReloadTip = 40f;   // degrees the gun tips up mid-reload
    private const float MeleeArc = 150f;   // degrees the gun sweeps through in a melee swing
    private const float FloorClearance = 0.05f;  // lowest the gun's quad may reach, in metres
    private const float AttachmentGap = 0.01f;   // metres an attachment sits in front of the gun
    private const float LaserSetBack = 2f;       // gun pixels the laser's front sits behind the muzzle
    private const int LaserFrame = 1;
    // Mag frames hang from this point of their frame (its top middle).
    private static readonly Vector2 MagTop = new Vector2(8.5f, 8f);
    // The drawn slots, in the order parts sit along the barrel (a muzzle part on a barrel part).
    private static readonly AttachmentSlot[] DrawnSlots = { AttachmentSlot.Barrel, AttachmentSlot.Muzzle, AttachmentSlot.Magazine };
    // The laser's front on the barrel line is 5 px right of and 2 px above its sheet's pivot (the
    // sheet's one pivot suits the can), and its lens is at (-0.5, -2.5) from that front.
    private static readonly Vector2 LaserFront = new Vector2(5f, 2f);
    private static readonly Vector2 LensOffset = new Vector2(-0.5f, -2.5f);

    private Gun gun;
    private SpriteSheet sheet;
    private PixelSprite sprite;
    private Transform muzzle;
    private Renderer[] modelRenderers = new Renderer[0];
    private float drawnAngle;
    private Vector2 aim = Vector2.right;
    private Light flashlight;
    private float flashlightThrow;   // how far ahead the flashlight's beam meets the floor
    private readonly PixelSprite[] slotParts = new PixelSprite[3];   // per DrawnSlots
    private PixelSprite laserModule;
    private bool putAway;
    private Vector2 screenGrip;      // this frame's grip on screen and screen pixels per metre
    private float screenScale;

    public static GunSprite Attach(Gun gun, SpriteSheet sheet)
    {
        if (gun == null || sheet == null || gun.GetComponent<GunSprite>() != null)
            return null;

        GunSprite gunSprite = gun.gameObject.AddComponent<GunSprite>();
        gunSprite.gun = gun;
        gunSprite.sheet = sheet;
        gunSprite.muzzle = gun.Muzzle;

        Transform model = gun.transform.Find("Model");
        gunSprite.modelRenderers = model != null ? model.GetComponentsInChildren<Renderer>(true) : new Renderer[0];

        uint layers = 1u;
        Renderer holder = gun.transform.root.GetComponent<Renderer>();

        if (holder != null)
            layers = holder.renderingLayerMask | 1u;

        gunSprite.sprite = PixelSprite.Create("Gun Sprite", gun.transform, sheet, PixelSprite.Mode.Facing, layers);
        gunSprite.sprite.Standing = true;
        gunSprite.sprite.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Vector2 barrel = sheet.muzzle - sheet.pivot;
        gunSprite.drawnAngle = Mathf.Atan2(barrel.y, barrel.x) * Mathf.Rad2Deg;

        foreach (Light light in gun.transform.root.GetComponentsInChildren<Light>(true))
        {
            if (light.name != "Flashlight")
                continue;

            gunSprite.flashlight = light;
            Vector3 forward = light.transform.forward;
            float drop = Mathf.Max(0.05f, -forward.y / Mathf.Max(0.01f, new Vector2(forward.x, forward.z).magnitude));
            gunSprite.flashlightThrow = Mathf.Max(1f, light.transform.position.y / drop);
        }

        // A 3D suppressor fitted before the sprite came goes (the sprite draws its own).
        gun.UpdateAttachmentVisuals();
        return gunSprite;
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;

        if (camera == null || sprite == null)
            return;

        HideModel();

        // Worked out on screen: from the hands' height on the body, toward the cursor.
        Transform holder = transform.root;
        SpriteCharacter character = holder.GetComponent<SpriteCharacter>();

        // Put away while the hands are busy (a medkit, adrenaline, a ladder).
        // Only switched when that changes, so the player being hidden (ZoneExit) still hides it.
        bool away = character != null && character.HandsBusy;

        if (away != putAway)
        {
            putAway = away;
            sprite.Renderer.enabled = !away;
        }

        if (away)
        {
            foreach (PixelSprite part in slotParts)
            {
                if (part != null)
                    part.Renderer.enabled = false;
            }

            if (laserModule != null)
                laserModule.Renderer.enabled = false;

            return;
        }

        SpriteSheet body = character != null ? character.Sprite.Sheet : null;
        float holdUp = body != null ? body.holdHeight / Mathf.Max(1f, body.pixelsPerUnit) : 0.6f;
        float reach = body != null ? body.holdReach : 0.3f;
        Vector3 feet = new Vector3(holder.position.x, 0f, holder.position.z);
        Vector3 handsWorld = feet + CameraFollow.SpriteUp * (holdUp / Mathf.Max(0.01f, CameraFollow.VerticalScale));
        Vector2 hands = camera.WorldToScreenPoint(handsWorld);
        Vector2 toCursor = GameInput.Pointer - hands;

        if (toCursor.sqrMagnitude > 4f)
            aim = toCursor.normalized;

        // Screen pixels per metre across, at the holder (with perspective it depends on the distance).
        float pixelsPerMetre = Vector2.Distance(hands, camera.WorldToScreenPoint(handsWorld + camera.transform.right));
        Vector2 grip = hands + aim * reach * pixelsPerMetre;
        float barrel = ((sheet.muzzle - sheet.pivot).magnitude + Extension(AttachmentSlot.Barrel) + Extension(AttachmentSlot.Muzzle))
                       / Mathf.Max(1f, sheet.pixelsPerUnit);
        Vector2 tip = grip + aim * barrel * pixelsPerMetre;

        bool left = aim.x < 0f;
        PlayerMelee melee = holder.GetComponent<PlayerMelee>();
        float bash = melee != null ? melee.SwingProgress : -1f;
        float target = DrawnAngle(left, bash);

        Vector3 gripWorld = OnAimPlane(camera, grip);
        sprite.MirrorY = left;
        sprite.Angle = left ? target + drawnAngle : target - drawnAngle;
        sprite.Position = gripWorld;

        if (muzzle != null)
            PlaceMuzzle(OnAimPlane(camera, tip), gripWorld);

        Vector3 view = CameraFollow.ViewRay(gripWorld);
        SetDepth(view, gripWorld, feet, character != null && character.CurrentFacing.Away);
        screenGrip = grip;
        screenScale = pixelsPerMetre;
        PlaceAttachments(camera, target, left, gun.IsReloading || bash >= 0f, view);
    }

    // The 3D model stays hidden (attachments and reloads can turn bits of it back on).
    private void HideModel()
    {
        foreach (Renderer part in modelRenderers)
        {
            if (part != null && part.enabled)
                part.enabled = false;
        }
    }

    // The gun's angle on screen: along the aim, tipped over while reloading, and swung by a melee
    // bash (`bash` is the swing's progress, or below 0 when not swinging): drawn back, then swept
    // through an arc and back.
    private float DrawnAngle(bool left, float bash)
    {
        float target = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

        if (gun.IsReloading)
            target += ReloadAngle(gun.ReloadProgress) * (left ? -1f : 1f);

        if (bash >= 0f)
        {
            float sweep = bash < 0.25f ? Mathf.Lerp(0f, 1f, bash / 0.25f) : bash < 0.6f ? Mathf.Lerp(1f, -1f, (bash - 0.25f) / 0.35f) : Mathf.Lerp(-1f, 0f, (bash - 0.6f) / 0.4f);
            target += sweep * MeleeArc * 0.5f * (left ? -1f : 1f);
        }

        return target;
    }

    // How far the gun's turned (up, in degrees) through a reload. A magazine: the nose dips as the
    // old mag drops out, holds while the new one's found, snaps up as it's slapped in, then a
    // jolt back as the action's racked. Shells: tipped up and nodding once per shell pushed in.
    private float ReloadAngle(float t)
    {
        if (gun.ReloadsPerShell)
        {
            float shell = Mathf.Repeat(t * gun.ReloadShellCount, 1f);
            float raise = Mathf.Clamp01(t * gun.ReloadShellCount * 4f) * Mathf.Clamp01((1f - t) * gun.ReloadShellCount * 4f);
            return raise * (ReloadTip * 0.75f - Mathf.Sin(shell * Mathf.PI) * 10f);
        }

        if (t < 0.25f)
            return Mathf.Lerp(0f, -22f, Smooth(t / 0.25f));

        if (t < 0.6f)
            return -22f + Mathf.Sin((t - 0.25f) / 0.35f * Mathf.PI * 3f) * 2.5f;

        if (t < 0.7f)
            return Mathf.Lerp(-22f, ReloadTip, Smooth((t - 0.6f) / 0.1f));

        if (t < 0.85f)
            return Mathf.Lerp(ReloadTip, -6f, Smooth((t - 0.7f) / 0.15f));

        return Mathf.Lerp(-6f, 0f, Smooth((t - 0.85f) / 0.15f));
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    // The muzzle point (where shots and the flash come from) at the drawn barrel's tip, facing
    // along it, with the flashlight on the end aimed to land as far ahead as before.
    private void PlaceMuzzle(Vector3 tipWorld, Vector3 gripWorld)
    {
        Vector3 barrelLine = tipWorld - gripWorld;
        barrelLine.y = 0f;
        muzzle.position = tipWorld;

        if (barrelLine.sqrMagnitude <= 0.0001f)
            return;

        muzzle.rotation = Quaternion.LookRotation(barrelLine.normalized, Vector3.up);

        if (flashlight != null)
        {
            float pitch = Mathf.Atan2(tipWorld.y, flashlightThrow) * Mathf.Rad2Deg;
            flashlight.transform.SetPositionAndRotation(tipWorld, muzzle.rotation * Quaternion.Euler(pitch, 0f, 0f));
        }
    }

    // On the holder's body plane (the vertical plane through their feet facing the camera), then
    // a touch in front of it, or behind when they face away.
    private void SetDepth(Vector3 view, Vector3 gripWorld, Vector3 feet, bool behind)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(view, Vector3.up);
        float toPlane = 0f;

        if (flatForward.sqrMagnitude > 0.0001f)
        {
            flatForward.Normalize();
            float along = Vector3.Dot(view, flatForward);
            toPlane = along > 0.01f ? Vector3.Dot(feet - gripWorld, flatForward) / along : 0f;
        }

        sprite.DepthOffset = toPlane + (behind ? DepthGap : -DepthGap);

        // Pointing down the screen, a long gun's quad reaches below the floor, which hides the
        // barrel: slide it toward the camera (no change on screen) until it clears the floor.
        // (The drop is measured on its depth plane: stretched as the camera foreshortens height.)
        float drop = sprite.StandingDrop() * CameraFollow.VerticalScale / Mathf.Max(0.25f, CameraFollow.Foreshortening);
        float lowest = gripWorld.y + view.y * sprite.DepthOffset - drop;

        if (lowest < FloorClearance && view.y < -0.01f)
            sprite.DepthOffset -= (FloorClearance - lowest) / -view.y;
    }

    public static SpriteSheet AttachmentArt => SpriteVisuals.Current != null ? SpriteVisuals.Current.gunAttachments : null;

    // Where a pixel of the gun's sheet is on screen as the gun is drawn this frame.
    private Vector2 ScreenOf(Vector2 pixel)
    {
        Vector2 offset = (pixel - sheet.pivot) / Mathf.Max(1f, sheet.pixelsPerUnit);

        if (sprite.MirrorY)
            offset.y = -offset.y;

        float radians = sprite.Angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
        return screenGrip + new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos) * screenScale;
    }

    // The attachments' frames in the art, and how many gun pixels a barrel or muzzle part carries
    // the muzzle on by. -1: not drawn.
    private static int FrameOf(WeaponAttachment attachment, out float length)
    {
        length = 0f;

        switch (attachment != null ? attachment.name : null)
        {
            case "Suppressor": length = 8f; return 0;
            case "Laser Sight": return LaserFrame;
            case "Choke": length = 3f; return 2;
            case "Long Barrel": length = 7f; return 3;
            case "Match Barrel": length = 5f; return 4;
            case "Extended Mag": return 5;
            case "Drum Mag": return 6;
            case "Quick Mag": return 7;
            default: return -1;
        }
    }

    // The frame of the attachment art showing a fitted part (-1: not drawn), and the gun pixel
    // the art's pivot goes on: barrel parts on the muzzle, muzzle parts on the end of the barrel
    // part (barrelPart, if any), mag parts hung under the gun's mag, the laser under the barrel's
    // front. Shared with gun pickups (PickupAttachments).
    public static int Mount(SpriteSheet sheet, SpriteSheet art, WeaponAttachment part, WeaponAttachment barrelPart, out Vector2 mount)
    {
        mount = sheet.muzzle;
        int frame = art != null ? FrameOf(part, out _) : -1;

        switch (frame < 0 ? AttachmentSlot.Trigger : part.slot)
        {
            case AttachmentSlot.Muzzle:
                FrameOf(barrelPart, out float barrel);
                mount.x += barrel;
                break;
            case AttachmentSlot.Magazine:
                if (sheet.magazine == Vector2.zero)
                    return -1;

                mount = sheet.magazine - (MagTop - art.pivot);
                break;
            case AttachmentSlot.Rail:
                mount = new Vector2(sheet.muzzle.x - LaserSetBack, sheet.pivot.y) - LaserFront;
                break;
        }

        return frame;
    }

    // How far the part in a slot carries the drawn muzzle on, in gun pixels.
    private float Extension(AttachmentSlot slot)
    {
        if (AttachmentArt == null)
            return 0f;

        FrameOf(gun.AttachmentIn(slot), out float length);
        return length;
    }

    // The attachment sprites on the drawn gun (barrelAngle is the barrel's angle on screen),
    // and the laser's beam from the drawn lens.
    private void PlaceAttachments(Camera camera, float barrelAngle, bool left, bool tipped, Vector3 view)
    {
        SpriteSheet art = AttachmentArt;
        LaserSight laser = gun.Laser;
        Vector2 laserMount = new Vector2(sheet.muzzle.x - LaserSetBack, sheet.pivot.y);
        WeaponAttachment barrelPart = gun.AttachmentIn(AttachmentSlot.Barrel);

        for (int i = 0; i < DrawnSlots.Length; i++)
        {
            int frame = Mount(sheet, art, gun.AttachmentIn(DrawnSlots[i]), barrelPart, out Vector2 mount);
            slotParts[i] = Show(slotParts[i], art, frame >= 0, Mathf.Max(0, frame), DrawnSlots[i] + " Sprite");

            if (slotParts[i] != null && frame >= 0)
                slotParts[i].SetFrame(frame, 0, false);

            PlaceAttachment(slotParts[i], camera, mount, barrelAngle, left, view);
        }

        laserModule = Show(laserModule, art, laser != null, LaserFrame, "Laser Sprite");
        PlaceAttachment(laserModule, camera, laserMount - LaserFront, barrelAngle, left, view);

        if (laser == null)
            return;

        Vector2 lens = laserModule != null ? ScreenOf(laserMount + LensOffset) : ScreenOf(sheet.muzzle);
        Vector3 way = OnAimPlane(camera, ScreenOf(sheet.muzzle)) - OnAimPlane(camera, ScreenOf(sheet.pivot));
        way.y = 0f;
        laser.transform.position = OnAimPlane(camera, lens);

        if (way.sqrMagnitude > 0.0001f)
            laser.transform.rotation = Quaternion.LookRotation(way.normalized, Vector3.up);

        laser.FollowBarrel = tipped;
    }

    // Makes the attachment's sprite the first time it's fitted; shown while fitted and the gun shows.
    private PixelSprite Show(PixelSprite part, SpriteSheet art, bool fitted, int frame, string name)
    {
        if (part == null && fitted && art != null)
        {
            part = PixelSprite.Create(name, gun.transform, art, PixelSprite.Mode.Facing, sprite.Renderer.renderingLayerMask);
            part.Standing = true;
            part.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            part.SetFrame(frame, 0, false);
            part.Scale = art.pixelsPerUnit / Mathf.Max(1f, sheet.pixelsPerUnit);
        }

        if (part != null)
            part.Renderer.enabled = fitted && sprite.Renderer.enabled;

        return part;
    }

    // Its pivot on the given pixel of the gun, along the barrel, just in front of the gun and
    // clear of the floor (as the gun is kept clear above).
    private void PlaceAttachment(PixelSprite part, Camera camera, Vector2 mount, float barrelAngle, bool left, Vector3 view)
    {
        if (part == null || !part.Renderer.enabled)
            return;

        part.Angle = barrelAngle;
        part.MirrorY = left;
        part.Position = OnAimPlane(camera, ScreenOf(mount));
        part.DepthOffset = sprite.DepthOffset - AttachmentGap;

        float drop = part.StandingDrop() * CameraFollow.VerticalScale / Mathf.Max(0.25f, CameraFollow.Foreshortening);
        float lowest = part.Position.y + view.y * part.DepthOffset - drop;

        if (lowest < FloorClearance && view.y < -0.01f)
            part.DepthOffset -= (FloorClearance - lowest) / -view.y;
    }

    // The point at Gun.AimHeight under a screen point.
    private static Vector3 OnAimPlane(Camera camera, Vector2 screen)
    {
        Ray ray = camera.ScreenPointToRay(screen);
        Plane plane = new Plane(Vector3.up, Vector3.up * Gun.AimHeight);
        return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : ray.origin;
    }

    private void OnDestroy()
    {
        foreach (Renderer part in modelRenderers)
        {
            if (part != null)
                part.enabled = true;
        }
    }
}
