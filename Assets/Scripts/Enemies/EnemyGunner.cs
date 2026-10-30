using System.Collections;
using UnityEngine;

// The gunner: an infected still clutching a weapon (a soldier's rifle at the base, a mall cop's
// pistol, an orderly's flare gun, an air marshal's sidearm). With the player within 10 m it stops
// and aims: a thin red line lights up on the floor where the shots will go (the aim is fixed the
// moment the line appears, so stepping off it is the dodge), then it fires a burst of three
// tracer rounds down it. The shots are as loud as a gun, so they bring the rest of the room. It
// sometimes drops a little ammo for the gun it was holding (FloorGenerator.DropAmmo).
public class EnemyGunner : EnemyRangedAttack
{
    private const float Speed = 8f;
    private const int Burst = 3;
    private const float BurstGap = 0.12f;
    private static readonly Color LineColour = new Color(1f, 0.15f, 0.1f);

    protected override float Range => 10f;
    protected override float WindUp => 0.7f;
    protected override Vector2 Interval => new Vector2(2.5f, 3.5f);
    protected override float AimLock => WindUp;
    protected override Color WindUpGlow => new Color(1f, 0.3f, 0.1f) * 1.2f;

    // The ammo its weapon takes, by zone.
    public static AmmoType AmmoFor(Zone zone)
    {
        switch (zone)
        {
            case Zone.Hospital: return AmmoType.ShotgunShell;
            case Zone.ShoppingMall:
            case Zone.Airport: return AmmoType.NineMil;
            default: return AmmoType.Rifle556;
        }
    }

    protected override void OnWindUp()
    {
        Vector3 from = transform.position;
        from.y = 0f;
        FloorMarker.Sector(from, aim, Range, 3f, LineColour, WindUp, RoomTransform, null);
    }

    protected override void Release()
    {
        StartCoroutine(Fire(aim));
    }

    private IEnumerator Fire(Vector3 direction)
    {
        for (int i = 0; i < Burst; i++)
        {
            if (enemy.Health <= 0)
                yield break;

            EnemyBullet.Fire(transform.position + direction * 0.4f, direction, Speed, EnemyBullet.Look.Round, RoomTransform);
            Noise.Emit(transform.position, Noise.Gunshot, Noise.Kind.Gunshot);

            Gun held = PlayerWeapon.Current != null ? PlayerWeapon.Current.GetComponentInChildren<Gun>() : null;

            if (held != null && held.ShotClip != null)
                SoundFX.PlayAt(held.ShotClip, transform.position, 0.6f, 0.85f);

            yield return new WaitForSeconds(BurstGap);
        }
    }
}
