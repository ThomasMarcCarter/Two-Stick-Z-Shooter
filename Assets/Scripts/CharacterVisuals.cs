using UnityEngine;

// The character models (Tools/Blender/Characters.py): the player, the zombie kinds, the
// floor bosses and the hideout survivors. Set on the FloorGenerator, which makes it current. Attach swaps a body's
// placeholder block or capsule for a model: the model goes in as a child standing on the floor,
// the root's own renderer is hidden, and the body's tint/flash/blink switch to the model's
// renderers. Without an asset (or a model for that kind) the blocks stay as they were.
[CreateAssetMenu(menuName = "Roguelike/Character Visuals", fileName = "CharacterVisuals")]
public class CharacterVisuals : ScriptableObject
{
    [Header("Player (real size, 3 materials)")]
    public GameObject player;

    [Header("Zombies (unit footprint, one 'Body' material: the enemy material)")]
    public GameObject zombie;
    public GameObject brute;
    public GameObject bloater;

    [Header("Bosses")]
    public GameObject butcher;
    public GameObject broodmother;
    public GameObject howler;

    [Header("Hideout survivors (real size, 4 materials)")]
    public GameObject mechanic;
    public GameObject medic;
    public GameObject quartermaster;
    public GameObject trader;

    public static CharacterVisuals Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Current = null;
    }

    public static void Use(CharacterVisuals visuals)
    {
        Current = visuals;
    }

    public GameObject For(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Brute: return brute;
            case EnemyKind.Bloater: return bloater;
            default: return zombie;
        }
    }

    public GameObject For(BossKind kind)
    {
        switch (kind)
        {
            case BossKind.Broodmother: return broodmother;
            case BossKind.Howler: return howler;
            default: return butcher;
        }
    }

    public GameObject For(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Medic: return medic;
            case NpcKind.Quartermaster: return quartermaster;
            case NpcKind.Trader: return trader;
            default: return mechanic;
        }
    }

    // Puts a survivor's model on an empty root standing on the floor, animated as a survivor.
    // Returns the animator (null if there's no model for them).
    public static CharacterAnimator AttachToNpc(Transform root, NpcKind kind)
    {
        GameObject model = Current != null ? Current.For(kind) : null;

        if (model == null || root == null)
            return null;

        Attach(root, model, Vector3.zero, Vector3.one);
        return CharacterAnimator.Add(root.Find("Model"), CharacterAnimator.Style.Npc);
    }

    // Puts a model on an enemy's unit-cube body (scaled with it), feet on the floor, turning to
    // face where it goes and (for the jointed zombie models) animated in the given style. The
    // block's box collider is swapped for a capsule round the model (FitHitbox).
    public static void AttachToEnemy(Enemy enemy, GameObject model, CharacterAnimator.Style style)
    {
        if (enemy == null || model == null)
            return;

        Renderer[] renderers = Attach(enemy.transform, model, new Vector3(0f, -0.5f, 0f), Vector3.one);
        enemy.SetBodyRenderers(renderers);
        Transform instance = enemy.transform.Find("Model");
        FitHitbox(enemy, renderers);
        instance.gameObject.AddComponent<FaceMovement>();
        CharacterAnimator.Add(instance, style);
    }

    // An upright capsule as wide as the model's torso (reaching arms and weapons stick out of
    // it) and as tall as the whole model, in place of the unit-cube block's box, which was wider
    // than the body and only half as tall. It stops just short of the floor, as the box did, so
    // it doesn't catch on floor seams. Shots, blood and pushing all go by it.
    private static void FitHitbox(Enemy enemy, Renderer[] renderers)
    {
        if (renderers.Length == 0 || !enemy.TryGetComponent(out BoxCollider box))
            return;

        Bounds whole = renderers[0].bounds;
        Bounds torso = whole;
        bool foundTorso = false;

        foreach (Renderer part in renderers)
        {
            whole.Encapsulate(part.bounds);

            if (!foundTorso && part.name.StartsWith("Torso"))
            {
                torso = part.bounds;
                foundTorso = true;
            }
        }

        if (!foundTorso)
            torso = whole;

        Transform body = enemy.transform;
        float scale = Mathf.Max(0.01f, body.lossyScale.x);
        float radius = Mathf.Max(torso.extents.x, torso.extents.z) / scale;
        float bottom = -0.42f;
        float top = Mathf.Max(bottom + radius * 2f, body.InverseTransformPoint(new Vector3(body.position.x, whole.max.y, body.position.z)).y);

        PhysicsMaterial material = box.sharedMaterial;
        Object.DestroyImmediate(box);

        CapsuleCollider capsule = enemy.gameObject.AddComponent<CapsuleCollider>();
        capsule.sharedMaterial = material;
        capsule.direction = 1;
        capsule.radius = radius;
        capsule.height = top - bottom;
        capsule.center = new Vector3(0f, (top + bottom) / 2f, 0f);
        enemy.RefreshBodyRadius();
    }

    public static CharacterAnimator.Style StyleFor(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Runner: return CharacterAnimator.Style.Runner;
            case EnemyKind.Brute: return CharacterAnimator.Style.Brute;
            case EnemyKind.Bloater: return CharacterAnimator.Style.Bloater;
            default: return CharacterAnimator.Style.Walker;
        }
    }

    // Puts the player model on the player's capsule (its pivot is the capsule's middle).
    public static void AttachToPlayer(Transform player, float floorOffset)
    {
        if (Current == null || Current.player == null || player == null || player.Find("Model") != null)
            return;

        Vector3 scale = player.lossyScale;
        Vector3 inverse = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
        Renderer[] renderers = Attach(player, Current.player, new Vector3(0f, -floorOffset * inverse.y, 0f), inverse);

        if (player.TryGetComponent(out PlayerHealth health))
            health.SetBodyRenderers(renderers);

        CharacterAnimator.Add(player.Find("Model"), CharacterAnimator.Style.Player);
    }

    private static Renderer[] Attach(Transform body, GameObject model, Vector3 localPosition, Vector3 localScale)
    {
        if (body.TryGetComponent(out Renderer own))
            own.enabled = false;

        GameObject instance = Object.Instantiate(model, body, false);
        instance.name = "Model";
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = localScale;

        foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
            Object.Destroy(collider);

        return instance.GetComponentsInChildren<Renderer>();
    }
}
