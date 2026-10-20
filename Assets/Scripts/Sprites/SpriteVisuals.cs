using UnityEngine;

// The sprite art (the pixel-art pass): which sheet each character and gun uses, the items atlas
// and crate (see ItemSprites), the lit sprite material, and the pixel-art room and effect
// materials. Set on the FloorGenerator, which makes it current when its "Sprite Art" switch is
// on (Current is null otherwise, and everything keeps its 3D model). Anything without a sheet
// here keeps its model, so the two styles can be mixed while art is made.
[CreateAssetMenu(menuName = "Roguelike/Sprite Visuals", fileName = "SpriteVisuals")]
public class SpriteVisuals : ScriptableObject
{
    [Tooltip("Base material for sprites (Roguelike/SpriteLit); each sheet gets a copy with its texture.")]
    public Material spriteMaterial;

    [Header("Characters")]
    public SpriteSheet player;
    public SpriteSheet walker;
    public SpriteSheet runner;
    public SpriteSheet brute;
    public SpriteSheet bloater;
    public SpriteSheet screamer;

    [Header("Bosses")]
    public SpriteSheet butcher;
    public SpriteSheet broodmother;
    public SpriteSheet howler;

    [Header("Survivors")]
    public SpriteSheet mechanic;
    public SpriteSheet medic;
    public SpriteSheet quartermaster;
    public SpriteSheet trader;

    [Header("Items")]
    [Tooltip("The items atlas: consumables, ammo, materials, melee upgrades, attachments (see ItemSprites).")]
    public SpriteSheet items;
    [Tooltip("The supply crate: closed, then open.")]
    public SpriteSheet supplyCrate;
    [Tooltip("The floor hatch from above: closed, then open.")]
    public SpriteSheet hatch;

    [Header("Clutter (see ClutterSprites)")]
    public SpriteSheet clutterBoxes;
    public SpriteSheet clutterSmall;
    public SpriteSheet clutterFlat;

    public SpriteSheet For(ClutterSprites.Sheet sheet)
    {
        switch (sheet)
        {
            case ClutterSprites.Sheet.Boxes: return clutterBoxes;
            case ClutterSprites.Sheet.Small: return clutterSmall;
            default: return clutterFlat;
        }
    }

    [Header("Guns (by Gun prefab name)")]
    public GunEntry[] guns = new GunEntry[0];

    [Header("Rooms")]
    public Material floor;
    public Material floorVariant;
    public Material corridorFloor;
    public Material wall;
    public Material door;

    [Header("Effects")]
    [Tooltip("Hard-edged square for blood and spark particles.")]
    public Material particle;
    [Tooltip("Pixel flame for muzzle flashes.")]
    public Material muzzleFlame;

    [System.Serializable]
    public class GunEntry
    {
        public string gun = "Pistol";
        public SpriteSheet sheet;
    }

    public static SpriteVisuals Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Current = null;
    }

    public static void Use(SpriteVisuals visuals)
    {
        Current = visuals;
    }

    public SpriteSheet For(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Runner: return runner;
            case EnemyKind.Brute: return brute;
            case EnemyKind.Bloater: return bloater;
            case EnemyKind.Screamer: return screamer;
            default: return walker;
        }
    }

    public SpriteSheet For(BossKind kind)
    {
        switch (kind)
        {
            case BossKind.Broodmother: return broodmother;
            case BossKind.Howler: return howler;
            default: return butcher;
        }
    }

    public SpriteSheet For(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Medic: return medic;
            case NpcKind.Quartermaster: return quartermaster;
            case NpcKind.Trader: return trader;
            default: return mechanic;
        }
    }

    // A gun's sheet by its prefab's name ("Pistol(Clone)" counts as "Pistol").
    public SpriteSheet ForGun(string gunName)
    {
        string name = gunName.Replace("(Clone)", "").Trim();

        foreach (GunEntry entry in guns)
        {
            if (entry.gun == name)
                return entry.sheet;
        }

        return null;
    }
}
