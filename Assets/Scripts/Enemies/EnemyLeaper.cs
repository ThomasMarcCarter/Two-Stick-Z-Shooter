using UnityEngine;

// The leaper: thin and quick. With the player 3-7 m away and in sight it crouches behind an amber
// line on the floor (the aim is fixed when the line appears), then pounces along it at 10 m/s for
// up to 6 m, stopping at walls. Landing on the player does 1 damage and throws them aside (a roll
// through it dodges it). After a pounce it's left sprawled for a moment, an opening to punish it.
// Up close it claws like any zombie.
public class EnemyLeaper : EnemyRangedAttack
{
    private const float PounceSpeed = 10f;
    private const float PounceLength = 6f;
    private const float Sprawl = 0.8f;
    private static readonly Color LineColour = new Color(1f, 0.7f, 0.15f);

    private float pounceUntil = -1f;
    private bool hitThisPounce;
    private Vector3 direction;

    protected override float Range => 7f;
    protected override float MinRange => 3f;
    protected override float WindUp => 0.6f;
    protected override Vector2 Interval => new Vector2(2.6f, 3.4f);
    protected override float AimLock => WindUp;
    protected override Color WindUpGlow => LineColour;

    public bool Pouncing => pounceUntil >= 0f;

    protected override bool Ready() => !Pouncing && base.Ready();

    protected override void OnWindUp()
    {
        Vector3 from = transform.position;
        from.y = 0f;
        FloorMarker.Sector(from, aim, PounceLength, 6f, LineColour, WindUp, RoomTransform, null);
    }

    protected override void Release()
    {
        direction = aim;
        pounceUntil = Time.time + PounceLength / PounceSpeed;
        hitThisPounce = false;
        enemy.ExternalControl = true;
        enemy.FacingOverride = direction;
        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("throw", 2, ProceduralAudio.Throw)), transform.position, 0.8f, 1.2f);
    }

    protected override void Update()
    {
        base.Update();

        if (Pouncing && (Time.time >= pounceUntil || !enemy.IsActive))
            Land();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        if (!Pouncing)
            return;

        Vector3 eye = transform.position;
        eye.y = 0.5f;
        float probe = 0.3f + PounceSpeed * Time.fixedDeltaTime;

        RaycastHit[] hits = Physics.SphereCastAll(eye, 0.25f, direction, probe, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Rigidbody body = hit.collider.attachedRigidbody;

            if (body != null && body.TryGetComponent(out PlayerHealth player))
            {
                if (!hitThisPounce && player.TakeDamage(1, transform.position))
                {
                    hitThisPounce = true;
                    body.linearVelocity = Vector3.Cross(Vector3.up, direction).normalized * 6f;
                    CameraFollow.Shake(0.15f);
                }

                continue;
            }

            if (hit.collider.CompareTag("Wall"))
            {
                Land();
                return;
            }
        }

        enemy.Body.linearVelocity = direction * PounceSpeed;
    }

    private void Land()
    {
        pounceUntil = -1f;
        enemy.Body.linearVelocity = Vector3.zero;
        enemy.ExternalControl = false;
        enemy.FacingOverride = null;
        enemy.Knock(Vector3.zero, Sprawl);
    }
}
