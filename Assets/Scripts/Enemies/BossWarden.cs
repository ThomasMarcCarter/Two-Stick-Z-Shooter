using System;
using UnityEngine;

// The Warden (the Shopping Mall's floor 1 boss): the mall's security chief in riot gear. Its
// riot shield stops bullets and blows from in front (sparks and a clank: Enemy.HitBlocker), and
// it only turns the shield slowly toward the player, so the fight is getting round it; grenade
// blasts and fire go round a shield. Every so often it plants its feet and blows its whistle
// (glowing blue): security guards climb in at the room's edges. Below half health it turns
// faster, whistles more often and a runner comes too. Up close it clubs with its baton (Enemy's
// swing attack, set up in Bosses.Equip).
[RequireComponent(typeof(Enemy))]
public class BossWarden : MonoBehaviour, IBossMoves
{
    private const float ShieldArc = 130f;        // degrees the shield covers, centred ahead
    private const float TurnSpeed = 65f;         // degrees per second the shield swings round
    private const float EnragedTurnSpeed = 100f;
    private const float WhistleWindUp = 0.9f;
    private static readonly Color WhistleColour = new Color(0.35f, 0.6f, 1f);

    private Enemy enemy;
    private Func<Vector3, EnemyKind, Enemy> spawnMinion;
    private Vector3 shieldFacing = Vector3.back;
    private float nextWhistle;
    private float whistleAt = -1f;
    private Color restGlow;

    private bool Enraged => enemy.Health <= enemy.MaxHealth / 2;

    public BossMove Move => whistleAt >= 0f ? BossMove.Whistle : BossMove.None;
    public float MoveProgress => whistleAt >= 0f ? Mathf.Clamp01(1f - (whistleAt - Time.time) / WhistleWindUp) : 0f;

    // Which way the shield faces (flat).
    public Vector3 ShieldFacing => shieldFacing;

    public void Configure(Func<Vector3, EnemyKind, Enemy> spawn)
    {
        spawnMinion = spawn;
    }

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        nextWhistle = Time.time + 5f;
        enemy.HitBlocker = Blocks;
    }

    private void Start()
    {
        restGlow = Bosses.Glow(BossKind.Warden);
    }

    private void OnDestroy()
    {
        if (enemy != null)
            enemy.HitBlocker = null;
    }

    // A hit from in front of the shield is stopped.
    private bool Blocks(Vector3 origin)
    {
        Vector3 from = Bosses.Flat(origin - transform.position);
        return from.sqrMagnitude > 0.0001f && Vector3.Angle(shieldFacing, from) <= ShieldArc / 2f;
    }

    private void Update()
    {
        if (!enemy.IsActive || enemy.Target == null)
            return;

        // The shield swings round toward the player, slowly.
        Vector3 toTarget = Bosses.Flat(enemy.Target.position - transform.position);

        if (toTarget.sqrMagnitude > 0.01f)
        {
            float speed = (Enraged ? EnragedTurnSpeed : TurnSpeed) * Mathf.Deg2Rad;
            shieldFacing = Vector3.RotateTowards(shieldFacing, toTarget.normalized, speed * Time.deltaTime, 0f);
        }

        enemy.FacingOverride = shieldFacing;

        if (whistleAt >= 0f)
        {
            float t = 1f - (whistleAt - Time.time) / WhistleWindUp;
            enemy.SetGlow(Color.Lerp(restGlow, WhistleColour * 2.5f, t));

            if (Time.time >= whistleAt)
                Whistle();

            return;
        }

        if (Time.time >= nextWhistle && enemy.CanSeeTarget)
        {
            whistleAt = Time.time + WhistleWindUp;
            enemy.ExternalControl = true;
        }
    }

    private void FixedUpdate()
    {
        if (whistleAt >= 0f)
            enemy.Body.linearVelocity = Vector3.zero;
    }

    private void Whistle()
    {
        whistleAt = -1f;
        nextWhistle = Time.time + (Enraged ? 7f : 10f);
        enemy.ExternalControl = false;
        enemy.SetGlow(restGlow);

        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("whistle", 1, i => ProceduralAudio.Whistle())), transform.position, 0.9f);
        Noise.Emit(transform.position, Noise.Gunshot, Noise.Kind.Scream);

        if (spawnMinion == null || enemy.Target == null)
            return;

        int guards = 2;
        int runners = Enraged ? 1 : 0;

        for (int i = 0; i < guards + runners; i++)
            Bosses.CallMinion(enemy, Bosses.SpawnPointInRoom(enemy, enemy.Target.position, 5f), i < guards ? EnemyKind.Walker : EnemyKind.Runner,
                spawnMinion, WhistleColour);
    }
}
