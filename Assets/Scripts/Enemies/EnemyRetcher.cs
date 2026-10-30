using UnityEngine;

// The retcher: slow and bloated, hanging about 3 m off. Every few seconds, with the player within
// 7 m, it swells up (its body bulging) and vomits a ring of bile globs all the way round: 8 on
// floor 1, 10 on floor 2, 12 below that, each ring turned half a gap from the last so standing
// still in a gap doesn't work twice.
public class EnemyRetcher : EnemyRangedAttack
{
    private const float Speed = 5f;

    private Vector3 baseScale;
    private bool turned;

    protected override float Range => 7f;
    protected override float WindUp => 0.8f;
    protected override Vector2 Interval => new Vector2(3.6f, 4.4f);

    protected override void Awake()
    {
        base.Awake();
        enemy.KeepAway = 3f;
    }

    protected override void Start()
    {
        base.Start();
        baseScale = transform.localScale;
    }

    protected override void Update()
    {
        base.Update();

        // Swelling up through the wind-up.
        float swell = WindingUp ? Mathf.Clamp01(enemy.PoseTime / 0.33f) : 0f;
        transform.localScale = new Vector3(baseScale.x * (1f + 0.25f * swell), baseScale.y * (1f + 0.08f * swell), baseScale.z * (1f + 0.25f * swell));
    }

    protected override void Release()
    {
        int count = Floor >= 3 ? 12 : Floor == 2 ? 10 : 8;
        float start = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg + (turned ? 180f / count : 0f);
        turned = !turned;
        EnemyBullet.Ring(transform.position, count, start, Speed, EnemyBullet.Look.Bile, RoomTransform);
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("bloater.swell", 2, ProceduralAudio.BloaterSwell)), transform.position, 0.8f, 0.8f);
    }
}
