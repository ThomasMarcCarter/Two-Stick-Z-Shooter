using UnityEngine;

// The lobber: one arm swollen huge. It hangs back (backs off inside 6 m) and every few seconds
// hefts a lump of debris and hurls it in a high arc at where the player's heading; a disc glows
// on the floor where it'll land, and it bursts there (1 damage) smashing what's around it
// (Lobbed). From floor 3 it throws a second lump beside the first.
public class EnemyLobber : EnemyRangedAttack
{
    private const float FlightTime = 0.9f;
    private const float Radius = 1.2f;
    private static readonly Color DiscColour = new Color(1f, 0.6f, 0.2f);

    protected override float Range => 10f;
    protected override float WindUp => 0.5f;
    protected override Vector2 Interval => new Vector2(2.6f, 3.4f);
    protected override Color WindUpGlow => DiscColour;

    protected override void Awake()
    {
        base.Awake();
        enemy.KeepAway = 6f;
    }

    protected override void Release()
    {
        Vector3 target = Lead(FlightTime * 0.6f);
        Vector3 from = transform.position + Vector3.up * 1.2f;
        Lobbed.Throw(from, target, FlightTime, Radius, DiscColour, RoomTransform, 1, 1, Lobbed.Style.Debris);

        if (Floor >= 3)
        {
            Vector3 side = Vector3.Cross(Vector3.up, aim) * 2f;
            Lobbed.Throw(from, target + (Random.value < 0.5f ? side : -side), FlightTime, Radius, DiscColour, RoomTransform, 1, 1, Lobbed.Style.Debris);
        }

        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("throw", 2, ProceduralAudio.Throw)), transform.position, 0.9f, 0.7f);
    }
}
