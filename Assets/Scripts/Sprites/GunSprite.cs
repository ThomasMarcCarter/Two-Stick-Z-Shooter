using UnityEngine;

// A held gun drawn as a sprite (Gungeon-style) instead of its 3D model. It's held out from the
// holder's body at their hands' height (the sheet's holdHeight) and reach (holdReach), on the
// side toward the cursor, with its barrel (the sheet's pivot sits on the barrel's line) pointing
// straight at the cursor on screen, flipped over when it points left so it's never upside down.
// The gun's Muzzle is moved to the drawn barrel's tip at Gun.AimHeight each frame, so shots,
// tracers and the flash come out of the drawn barrel and fly through the crosshair, and the
// holder's flashlight goes there too, shining the way the gun points (and still landing the same
// distance ahead). It stands upright just in front of its holder's sprite, or just behind when
// they face away up the screen (pulled toward the camera when it would dip into the floor), casts no shadow (it would block the flashlight), tips up
// while reloading and swings through an arc in a melee bash (PlayerMelee.SwingProgress). Added by CharacterVisuals.DressGun for guns with a sheet in SpriteVisuals.
[DefaultExecutionOrder(260)]
public class GunSprite : MonoBehaviour
{
    private const float DepthGap = 0.15f;  // metres in front of / behind the holder's body
    private const float ReloadTip = 40f;   // degrees the gun tips up mid-reload
    private const float MeleeArc = 150f;   // degrees the gun sweeps through in a melee swing
    private const float FloorClearance = 0.05f;  // lowest the gun's quad may reach, in metres

    private Gun gun;
    private SpriteSheet sheet;
    private PixelSprite sprite;
    private Transform muzzle;
    private Renderer[] modelRenderers = new Renderer[0];
    private float drawnAngle;
    private Vector2 aim = Vector2.right;
    private Light flashlight;
    private float flashlightThrow;   // how far ahead the flashlight's beam meets the floor

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

        return gunSprite;
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;

        if (camera == null || sprite == null)
            return;

        // The 3D model stays hidden (attachments and reloads can turn bits of it back on).
        foreach (Renderer part in modelRenderers)
        {
            if (part != null && part.enabled)
                part.enabled = false;
        }

        // Worked out on screen: from the hands' height on the body, toward the cursor.
        Transform holder = transform.root;
        SpriteCharacter character = holder.GetComponent<SpriteCharacter>();
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
        float barrel = (sheet.muzzle - sheet.pivot).magnitude / Mathf.Max(1f, sheet.pixelsPerUnit);
        Vector2 tip = grip + aim * barrel * pixelsPerMetre;

        float target = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        bool left = aim.x < 0f;

        if (gun.IsReloading)
            target += Mathf.Sin(gun.ReloadProgress * Mathf.PI) * ReloadTip * (left ? -1f : 1f);

        // A melee swing bashes with the gun: drawn back, then swept through an arc and back.
        PlayerMelee melee = holder.GetComponent<PlayerMelee>();
        float bash = melee != null ? melee.SwingProgress : -1f;

        if (bash >= 0f)
        {
            float sweep = bash < 0.25f ? Mathf.Lerp(0f, 1f, bash / 0.25f) : bash < 0.6f ? Mathf.Lerp(1f, -1f, (bash - 0.25f) / 0.35f) : Mathf.Lerp(-1f, 0f, (bash - 0.6f) / 0.4f);
            target += sweep * MeleeArc * 0.5f * (left ? -1f : 1f);
        }

        Vector3 gripWorld = OnAimPlane(camera, grip);
        sprite.MirrorY = left;
        sprite.Angle = left ? target + drawnAngle : target - drawnAngle;
        sprite.Position = gripWorld;

        if (muzzle != null)
        {
            Vector3 tipWorld = OnAimPlane(camera, tip);
            Vector3 barrelLine = tipWorld - gripWorld;
            barrelLine.y = 0f;
            muzzle.position = tipWorld;

            if (barrelLine.sqrMagnitude > 0.0001f)
            {
                muzzle.rotation = Quaternion.LookRotation(barrelLine.normalized, Vector3.up);

                // The flashlight on the end of the barrel, aimed to land as far ahead as before.
                if (flashlight != null)
                {
                    float pitch = Mathf.Atan2(tipWorld.y, flashlightThrow) * Mathf.Rad2Deg;
                    flashlight.transform.SetPositionAndRotation(tipWorld, muzzle.rotation * Quaternion.Euler(pitch, 0f, 0f));
                }
            }
        }

        // On the holder's body plane (the vertical plane through their feet facing the camera),
        // then a touch in front of it, or behind when they face away.
        Vector3 view = CameraFollow.ViewRay(gripWorld);
        Vector3 flatForward = Vector3.ProjectOnPlane(view, Vector3.up);
        float toPlane = 0f;

        if (flatForward.sqrMagnitude > 0.0001f)
        {
            flatForward.Normalize();
            float along = Vector3.Dot(view, flatForward);
            toPlane = along > 0.01f ? Vector3.Dot(feet - gripWorld, flatForward) / along : 0f;
        }

        bool behind = character != null && character.CurrentFacing.Away;
        sprite.DepthOffset = toPlane + (behind ? DepthGap : -DepthGap);

        // Pointing down the screen, a long gun's quad reaches below the floor, which hides the
        // barrel: slide it toward the camera (no change on screen) until it clears the floor.
        // (The drop is measured on its depth plane: stretched as the camera foreshortens height.)
        float drop = sprite.StandingDrop() * CameraFollow.VerticalScale / Mathf.Max(0.25f, CameraFollow.Foreshortening);
        float lowest = gripWorld.y + view.y * sprite.DepthOffset - drop;

        if (lowest < FloorClearance && view.y < -0.01f)
            sprite.DepthOffset -= (FloorClearance - lowest) / -view.y;
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
