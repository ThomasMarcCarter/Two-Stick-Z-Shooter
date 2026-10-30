using System;
using System.Collections.Generic;
using UnityEngine;

// The Warden (the Shopping Mall's floor 1 boss): the mall's security chief in riot gear. Its
// riot shield stops bullets and blows from in front (sparks and a clank: Enemy.HitBlocker), and
// it only turns the shield slowly toward the player, so the fight is getting round it; grenade
// blasts and fire go round a shield. Every so often it plants its feet and blows its whistle
// (glowing blue): security guards climb in at the room's edges. Below half health it turns
// faster, whistles more often and a runner comes too. Up close it clubs with its baton (Enemy's
// swing attack, set up in Bosses.Equip). Three melee blows within 10 s (on the shield or past it)
// stun it for 5 s: it drops the shield on the floor in front
// of it, reels on the spot (stars, the sheet's "stunned" clip) without swinging or whistling, and
// takes double damage from everything; then it picks the shield up again.
[RequireComponent(typeof(Enemy))]
public class BossWarden : MonoBehaviour, IBossMoves
{
    private const float ShieldArc = 130f;        // degrees the shield covers, centred ahead
    private const float TurnSpeed = 65f;         // degrees per second the shield swings round
    private const float EnragedTurnSpeed = 100f;
    private const float WhistleWindUp = 0.9f;
    private static readonly Color WhistleColour = new Color(0.35f, 0.6f, 1f);
    private const int StunHits = 3;              // melee blows landed within StunWindow
    private const float StunWindow = 10f;
    private const float StunTime = 5f;
    private const float StunnedDamage = 2f;      // damage multiplier while stunned
    private const float ShieldDropDistance = 0.9f;

    private Enemy enemy;
    private Func<Vector3, EnemyKind, Enemy> spawnMinion;
    private Vector3 shieldFacing = Vector3.back;
    private float nextWhistle;
    private float whistleAt = -1f;
    private Color restGlow;
    private readonly List<float> meleeHits = new List<float>();
    private float stunnedUntil = -1f;
    private GameObject droppedShield;

    private bool Enraged => enemy.Health <= enemy.MaxHealth / 2;

    public bool Stunned => stunnedUntil >= 0f;

    public BossMove Move => Stunned ? BossMove.Dazed : whistleAt >= 0f ? BossMove.Whistle : BossMove.None;
    public float MoveProgress => Stunned ? Mathf.Clamp01(1f - (stunnedUntil - Time.time) / StunTime)
        : whistleAt >= 0f ? Mathf.Clamp01(1f - (whistleAt - Time.time) / WhistleWindUp) : 0f;

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
        enemy.MeleeHit += OnMeleeHit;
    }

    private void Start()
    {
        restGlow = Bosses.Glow(BossKind.Warden);
    }

    private void OnDestroy()
    {
        if (enemy != null)
        {
            enemy.HitBlocker = null;
            enemy.MeleeHit -= OnMeleeHit;
        }

        if (droppedShield != null)
            Destroy(droppedShield);
    }

    // A hit from in front of the shield is stopped (none while it's on the floor).
    private bool Blocks(Vector3 origin)
    {
        if (Stunned)
            return false;

        Vector3 from = Bosses.Flat(origin - transform.position);
        return from.sqrMagnitude > 0.0001f && Vector3.Angle(shieldFacing, from) <= ShieldArc / 2f;
    }

    private void OnMeleeHit(Enemy hit)
    {
        if (Stunned)
            return;

        meleeHits.Add(Time.time);
        meleeHits.RemoveAll(t => Time.time - t > StunWindow);

        if (meleeHits.Count >= StunHits)
            Stun();
    }

    private void Update()
    {
        if (Stunned)
        {
            UpdateStun();
            return;
        }

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
        if (whistleAt >= 0f || Stunned)
            enemy.Body.linearVelocity = Vector3.zero;
    }

    // Knocked senseless: the shield clatters to the floor ahead of it and it takes double damage.
    private void Stun()
    {
        meleeHits.Clear();
        whistleAt = -1f;
        stunnedUntil = Time.time + StunTime;
        enemy.ExternalControl = true;
        enemy.DamageMultiplier = StunnedDamage;
        enemy.Body.linearVelocity = Vector3.zero;

        SoundFX.PlayAt(SoundFX.Pick(SoundFX.Clips("metal.clank", 3, ProceduralAudio.MetalClank)), transform.position, 0.9f, 0.6f);
        Noise.Emit(transform.position, Noise.Gunshot * 0.5f, Noise.Kind.Smash);
        DropShield();
    }

    private void UpdateStun()
    {
        // Stars: a flickering yellow glow while it reels (as the Butcher's daze).
        enemy.SetGlow(new Color(1f, 0.85f, 0.2f) * (0.4f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 9f))));

        if (Time.time >= stunnedUntil || !enemy.IsActive)
            Recover();
    }

    // Picks the shield back up and carries on; it doesn't whistle straight away.
    private void Recover()
    {
        stunnedUntil = -1f;
        enemy.ExternalControl = false;
        enemy.DamageMultiplier = 1f;
        enemy.SetGlow(restGlow);
        nextWhistle = Mathf.Max(nextWhistle, Time.time + 2f);

        if (droppedShield != null)
            Destroy(droppedShield);
    }

    // The shield lying flat on the floor in front of it (the dead row's second cell of its sheet).
    private void DropShield()
    {
        SpriteSheet sheet = SpriteVisuals.Current != null ? SpriteVisuals.Current.For(BossKind.Warden) : null;
        SpriteSheet.Clip dead = sheet != null ? sheet.Find("dead") : null;

        if (dead == null)
            return;

        droppedShield = new GameObject("Dropped Shield");
        Vector3 at = transform.position + shieldFacing * ShieldDropDistance;
        droppedShield.transform.SetPositionAndRotation(new Vector3(at.x, 0f, at.z), Quaternion.LookRotation(shieldFacing, Vector3.up));
        ItemSprite.ApplyFlat(droppedShield, sheet, new Vector2Int(1, dead.row), sheet.frameWidth / Mathf.Max(1f, sheet.pixelsPerUnit));
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
