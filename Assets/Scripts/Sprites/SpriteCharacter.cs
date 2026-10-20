using UnityEngine;

// Animates a character's sprite (PixelSprite, upright) from what its body is doing: it faces the
// way its facing transform points (the player's aim; an enemy's FaceMovement "Model" child), in
// eight directions, and plays "walk" while moving, stepping a frame per strideLength travelled so
// the feet keep pace with the ground, "idle" when still, "attack" on the enemy's swing clock (so
// the blow frame shows as the hit lands, like CharacterAnimator's swings), "special" through a
// boss's moves (wind-up, charge, spit, howl, dodge, dazed: IBossMoves) and "dead" once the
// player has died. Added by CharacterVisuals in place of a model when the sprite art is on.
// The tilted camera shows an upright sprite taller than the body it stands for, so shots test
// against the sprite as it looks (Footprint, used by Projectile): what's under the sprite on
// screen, down at the height the shot flies, is where it can be hit.
[DefaultExecutionOrder(250)]
public class SpriteCharacter : MonoBehaviour
{
    private const float StrideLength = 0.3f;   // metres travelled per walk frame
    private const float MovingSpeed = 0.4f;    // slower than this counts as standing still

    private PixelSprite sprite;
    private Transform facingSource;
    private Enemy enemy;
    private PlayerHealth player;
    private SpriteSheet.Clip idle;
    private SpriteSheet.Clip walk;
    private SpriteSheet.Clip attack;
    private SpriteSheet.Clip dead;
    private SpriteSheet.Clip special;
    private IBossMoves boss;
    private bool lookedForBoss;
    private Vector3 lastPosition;
    private float stride;
    private SpriteFacing.Facing facing;

    public PixelSprite Sprite => sprite;
    public SpriteFacing.Facing CurrentFacing => facing;
    public Enemy Enemy => enemy;

    // Enemies drawn as sprites, for Projectile's on-screen hit test.
    private static readonly System.Collections.Generic.List<SpriteCharacter> enemies = new System.Collections.Generic.List<SpriteCharacter>();
    public static System.Collections.Generic.IReadOnlyList<SpriteCharacter> Enemies => enemies;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        enemies.Clear();
    }

    private void OnEnable()
    {
        if (enemy != null && !enemies.Contains(this))
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
        Vector3 view = CameraFollow.ViewDirection;
        SpriteSheet sheet = sprite != null ? sprite.Sheet : null;
        Vector3 feet = new Vector3(transform.position.x, 0f, transform.position.z);
        from = to = new Vector3(feet.x, height, feet.z);
        radius = sheet != null ? sheet.HitRadius : 0.25f;

        if (sheet == null || view.y > -0.01f)
            return false;

        float top = sheet.bodyHeight / Mathf.Max(0.01f, CameraFollow.VerticalScale);
        from = OnPlane(feet, 0f, height, view);
        to = OnPlane(feet, top, height, view);
        return true;
    }

    // Where the line of sight through the point `up` metres above the feet crosses `height`.
    private static Vector3 OnPlane(Vector3 feet, float up, float height, Vector3 view)
    {
        Vector3 point = feet + Vector3.up * up;
        return point + view * ((up - height) / -view.y);
    }

    public static SpriteCharacter Attach(Transform body, SpriteSheet sheet, Transform facingSource, uint renderingLayers)
    {
        SpriteCharacter character = body.gameObject.AddComponent<SpriteCharacter>();
        character.sprite = PixelSprite.Create("Sprite", body, sheet, PixelSprite.Mode.Upright, renderingLayers);
        character.facingSource = facingSource != null ? facingSource : body;
        character.enemy = body.GetComponent<Enemy>();
        character.player = body.GetComponent<PlayerHealth>();
        character.idle = sheet.Find("idle");
        character.walk = sheet.Find("walk");
        character.attack = sheet.Find("attack");
        character.dead = sheet.Find("dead");
        character.special = sheet.Find("special");
        character.lastPosition = body.position;

        if (character.enemy != null && !enemies.Contains(character))
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

        if (player != null && player.IsDead && dead != null)
        {
            sprite.SetFrame(dead, 0, facing);
            return;
        }

        facing = SpriteFacing.For(facingSource.forward, camera.transform);

        // Boss behaviours are added after the body, so look for one the first time round.
        if (!lookedForBoss)
        {
            lookedForBoss = true;
            boss = GetComponent<IBossMoves>();
        }

        if (boss != null && special != null && boss.Move != BossMove.None)
        {
            sprite.SetFrame(special, Mathf.Min(special.frames - 1, Mathf.FloorToInt(boss.MoveProgress * special.frames)), facing);
            return;
        }

        float swing = enemy != null ? enemy.AttackTime : -1f;

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
}
