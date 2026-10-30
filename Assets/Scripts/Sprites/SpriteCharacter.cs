using UnityEngine;

// Animates a character's sprite (PixelSprite, upright) from what its body is doing: it faces the
// way its facing transform points (the player's aim; an enemy's FaceMovement "Model" child), in
// eight directions, and plays "walk" while moving, stepping a frame per strideLength travelled so
// the feet keep pace with the ground, "idle" when still, "attack" on the enemy's swing clock (so
// the blow frame shows as the hit lands, like CharacterAnimator's swings), "special" through a
// boss's moves (wind-up, charge, spit, howl, dodge, dazed: IBossMoves; a sheet with a "stunned"
// clip loops that while dazed instead, e.g. the Warden without its shield), the player's "heal" and
// "inject" through a medkit or adrenaline (QuickBar), "climb" down a hatch's ladder, "roll" through a dodge roll and "dead"
// once the player has died. Added by CharacterVisuals in place of a model when the sprite art is on.
// A soft round shadow lies on the floor under every character (SpriteVisuals.shadow), and a hit
// squashes the sprite wide and short for a moment (an enemy's or the player's Damaged).
// The tilted camera shows an upright sprite taller than the body it stands for, so shots test
// against the sprite as it looks (Footprint, used by Projectile): what's under the sprite on
// screen, down at the height the shot flies, is where it can be hit.
[DefaultExecutionOrder(250)]
public class SpriteCharacter : MonoBehaviour
{
    private const float StrideLength = 0.3f;   // metres travelled per walk frame
    private const float MovingSpeed = 0.4f;    // slower than this counts as standing still
    private const float SquashTime = 0.14f;    // a hit's squash, back to normal over this
    private const float ShadowWidth = 2.6f;    // the shadow across, in body radii

    private PixelSprite sprite;
    private Transform facingSource;
    private Enemy enemy;
    private PlayerHealth player;
    private HideoutNpc npc;
    private SpriteSheet.Clip idle;
    private SpriteSheet.Clip walk;
    private SpriteSheet.Clip attack;
    private SpriteSheet.Clip dead;
    private SpriteSheet.Clip special;
    private SpriteSheet.Clip stunned;
    private SpriteSheet.Clip melee;
    private SpriteSheet.Clip heal;
    private SpriteSheet.Clip inject;
    private SpriteSheet.Clip climb;
    private SpriteSheet.Clip roll;
    private PlayerDodge dodge;
    private QuickBar quickBar;
    private PlayerMelee playerMelee;
    private IBossMoves boss;
    private bool lookedForBoss;
    private Vector3 lastPosition;
    private float stride;
    private SpriteFacing.Facing facing;
    private float hitAt = -1f;

    public PixelSprite Sprite => sprite;
    public SpriteFacing.Facing CurrentFacing => facing;
    public Enemy Enemy => enemy;
    public HideoutNpc Npc => npc;

    // Enemies (and hideout survivors, who can be shot but not hurt) drawn as sprites, for
    // Projectile's on-screen hit test.
    private static readonly System.Collections.Generic.List<SpriteCharacter> enemies = new System.Collections.Generic.List<SpriteCharacter>();
    public static System.Collections.Generic.IReadOnlyList<SpriteCharacter> Enemies => enemies;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        enemies.Clear();
    }

    private void OnEnable()
    {
        if ((enemy != null || npc != null) && !enemies.Contains(this))
            enemies.Add(this);
    }

    private void OnDisable()
    {
        enemies.Remove(this);
    }

    // The stretch of floor-parallel plane at `height` that lies under the sprite on screen: from
    // under its feet to under the top of the drawn body, along the camera's line of sight, and the
    // body's half-width either side.
    public bool Footprint(float height, out Vector3 from, out Vector3 to, out float radius)
    {
        SpriteSheet sheet = sprite != null ? sprite.Sheet : null;
        Vector3 feet = new Vector3(transform.position.x, 0f, transform.position.z);
        from = to = new Vector3(feet.x, height, feet.z);
        radius = sheet != null ? sheet.HitRadius : 0.25f;

        if (sheet == null)
            return false;

        // The top of the drawn body: straight up and stretched, or leaning back to the camera.
        Vector3 top = feet + CameraFollow.SpriteUp * (sheet.bodyHeight / Mathf.Max(0.01f, CameraFollow.VerticalScale));
        return OnPlane(feet, height, out from) & OnPlane(top, height, out to);
    }

    // Where the line of sight through a point crosses `height`.
    private static bool OnPlane(Vector3 point, float height, out Vector3 crossing)
    {
        Vector3 view = CameraFollow.ViewRay(point);
        crossing = point;

        if (view.y > -0.01f)
            return false;

        crossing = point + view * ((point.y - height) / -view.y);
        return true;
    }

    public static SpriteCharacter Attach(Transform body, SpriteSheet sheet, Transform facingSource, uint renderingLayers)
    {
        SpriteCharacter character = body.gameObject.AddComponent<SpriteCharacter>();
        character.sprite = PixelSprite.Create("Sprite", body, sheet, PixelSprite.Mode.Upright, renderingLayers);
        character.facingSource = facingSource != null ? facingSource : body;
        character.enemy = body.GetComponent<Enemy>();
        character.player = body.GetComponent<PlayerHealth>();
        character.npc = body.GetComponent<HideoutNpc>();
        character.idle = sheet.Find("idle");
        character.walk = sheet.Find("walk");
        character.attack = sheet.Find("attack");
        character.dead = sheet.Find("dead");
        character.special = sheet.Find("special");
        character.stunned = sheet.Find("stunned");
        character.melee = sheet.Find("melee");
        character.heal = sheet.Find("heal");
        character.inject = sheet.Find("inject");
        character.climb = sheet.Find("climb");
        character.roll = sheet.Find("roll");
        character.dodge = body.GetComponent<PlayerDodge>();
        character.quickBar = body.GetComponent<QuickBar>();
        character.playerMelee = body.GetComponent<PlayerMelee>();
        character.lastPosition = body.position;
        character.AddShadow(sheet);

        if (character.enemy != null)
            character.enemy.Damaged += _ => character.hitAt = Time.time;

        if (character.player != null)
        {
            character.player.Damaged += _ => character.hitAt = Time.time;
            body.gameObject.AddComponent<BloodyFootprints>();
        }

        if ((character.enemy != null || character.npc != null) && !enemies.Contains(character))
            enemies.Add(character);

        return character;
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;

        if (camera == null || sprite == null)
            return;

        Vector3 moved = transform.position - lastPosition;
        moved.y = 0f;
        lastPosition = transform.position;
        float dt = Time.deltaTime;
        float speed = dt > 0f ? moved.magnitude / dt : 0f;
        Squash();

        if (player != null && player.IsDead && dead != null)
        {
            sprite.SetFrame(dead, 0, facing);
            return;
        }

        facing = SpriteFacing.For(facingSource.forward, camera.transform);

        // Rolling: faces the way the roll goes, not the aim.
        if (dodge != null && dodge.Rolling)
            facing = SpriteFacing.For(dodge.Direction, camera.transform);

        // Boss behaviours are added after the body, so look for one the first time round.
        if (!lookedForBoss)
        {
            lookedForBoss = true;
            boss = GetComponent<IBossMoves>();
        }

        if (boss != null && stunned != null && boss.Move == BossMove.Dazed)
        {
            sprite.SetFrame(stunned, Mathf.FloorToInt(Time.time * stunned.fps), facing);
            return;
        }

        if (boss != null && special != null && boss.Move != BossMove.None)
        {
            sprite.SetFrame(special, Mathf.Min(special.frames - 1, Mathf.FloorToInt(boss.MoveProgress * special.frames)), facing);
            return;
        }

        if (player != null && UpdatePlayerActions(facing))
            return;

        // The player's melee swing.
        float bash = playerMelee != null ? playerMelee.SwingProgress : -1f;

        if (bash >= 0f && melee != null)
        {
            sprite.SetFrame(melee, Mathf.Min(melee.frames - 1, Mathf.FloorToInt(bash * melee.frames)), facing);
            return;
        }

        float swing = enemy == null ? -1f : enemy.AttackTime >= 0f ? enemy.AttackTime : enemy.PoseTime;

        if (swing >= 0f && attack != null)
        {
            sprite.SetFrame(attack, Mathf.Min(attack.frames - 1, Mathf.FloorToInt(swing * attack.frames)), facing);
            return;
        }

        if (speed > MovingSpeed && walk != null)
        {
            stride += moved.magnitude;
            sprite.SetFrame(walk, Mathf.FloorToInt(stride / StrideLength), facing);
            return;
        }

        stride = 0f;

        if (idle != null)
            sprite.SetFrame(idle, Mathf.FloorToInt(Time.time * idle.fps), facing);
    }

    // A dark disc on the floor under the feet, sized to the body, under the character but over the
    // blood and litter. Casts and takes no shadow of its own.
    private void AddShadow(SpriteSheet body)
    {
        SpriteSheet sheet = SpriteVisuals.Current != null ? SpriteVisuals.Current.shadow : null;

        if (sheet == null || sheet.texture == null)
            return;

        PixelSprite shadow = PixelSprite.Create("Shadow", transform, sheet, PixelSprite.Mode.Flat);
        shadow.Scale = body.bodyRadius * ShadowWidth / (sheet.frameWidth / Mathf.Max(1f, sheet.pixelsPerUnit));
        shadow.Lift = 0.012f;
        shadow.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shadow.Renderer.receiveShadows = false;
    }

    // Wide and short just after a hit, easing back to as drawn.
    private void Squash()
    {
        float t = hitAt < 0f ? 1f : (Time.time - hitAt) / SquashTime;
        float s = 1f - Mathf.Clamp01(t);
        sprite.Stretch = new Vector2(1f + 0.22f * s, 1f - 0.16f * s);
    }

    // The player busy with something other than fighting: down a hatch's ladder, or a medkit or
    // adrenaline through its use time. False when they're not (or the sheet has no clip for it).
    private bool UpdatePlayerActions(SpriteFacing.Facing facing)
    {
        if (dodge != null && dodge.Rolling && roll != null)
        {
            sprite.SetFrame(roll, Mathf.Min(roll.frames - 1, Mathf.FloorToInt(dodge.Progress * roll.frames)), facing);
            return true;
        }

        if (ZoneExit.Climbing && climb != null)
        {
            sprite.SetFrame(climb, Mathf.FloorToInt(Time.time * climb.fps), facing);
            return true;
        }

        // The QuickBar is added lazily (QuickBar.For), after the sprite.
        if (quickBar == null)
            quickBar = GetComponent<QuickBar>();

        if (quickBar == null || !quickBar.IsUsing)
            return false;

        SpriteSheet.Clip use = ConsumableInfo.IsMedkit(quickBar.UsingType) ? heal
            : quickBar.UsingType == ConsumableType.Adrenaline ? inject : null;

        if (use == null)
            return false;

        sprite.SetFrame(use, Mathf.Min(use.frames - 1, Mathf.FloorToInt(quickBar.UseProgress * use.frames)), facing);
        return true;
    }

    // Busy with both hands (a medkit, adrenaline, a ladder, a roll): GunSprite puts the gun away.
    public bool HandsBusy => player != null && (ZoneExit.Climbing || (dodge != null && dodge.Rolling) || (quickBar != null && quickBar.IsUsing
        && (ConsumableInfo.IsMedkit(quickBar.UsingType) || quickBar.UsingType == ConsumableType.Adrenaline)));
}
