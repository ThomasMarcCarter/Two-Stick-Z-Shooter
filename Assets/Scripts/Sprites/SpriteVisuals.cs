using UnityEngine;

// The sprite art (the pixel-art pass): which sheet each character and gun uses, the lit sprite
// material, and the pixel-art room and effect materials. Set on the FloorGenerator, which makes
// it current when its "Sprite Art" switch is on (Current is null otherwise, and everything keeps
// its 3D model). Anything without a sheet here (for now everything but the player, the walker
// and the pistol) also keeps its model, so the two styles can be mixed while art is made.
[CreateAssetMenu(menuName = "Roguelike/Sprite Visuals", fileName = "SpriteVisuals")]
public class SpriteVisuals : ScriptableObject
{
    [Tooltip("Base material for sprites (Roguelike/SpriteLit); each sheet gets a copy with its texture.")]
    public Material spriteMaterial;

    [Header("Characters")]
    public SpriteSheet player;
    public SpriteSheet walker;

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
        return kind == EnemyKind.Walker ? walker : null;
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
