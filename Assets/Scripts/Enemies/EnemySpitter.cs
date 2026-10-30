using UnityEngine;

// The spitter: hangs back (backs off inside 5 m, holds its ground a little past that) and every
// couple of seconds rears back and spits a slow glob of bile at where the player's heading. From
// floor 2 it spits a fan of three.
public class EnemySpitter : EnemyRangedAttack
{
    private const float Speed = 6.5f;

    protected override float Range => 9f;
    protected override float WindUp => 0.6f;
    protected override Vector2 Interval => new Vector2(2.1f, 2.9f);
    protected override float AimLock => 0.15f;

    protected override void Awake()
    {
        base.Awake();
        enemy.KeepAway = 5f;
    }

    protected override void Release()
    {
        float distance = FlatDistance(transform.position, enemy.Target.position);
        Vector3 at = Lead(distance / Speed * 0.5f) - transform.position;
        at.y = 0f;

        if (Floor >= 2)
            EnemyBullet.Fan(transform.position, at, 3, 20f, Speed, EnemyBullet.Look.Bile, RoomTransform);
        else
            EnemyBullet.Fire(transform.position, at, Speed, EnemyBullet.Look.Bile, RoomTransform);

        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("throw", 2, ProceduralAudio.Throw)), transform.position, 0.7f, 1.4f);
    }
}
