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

    [Header("Shopping Mall zombies")]
    [Tooltip("The same kinds dressed as the mall's shoppers and staff. Each falls back to the base sheet without one.")]
    public SpriteSheet mallWalker;
    public SpriteSheet mallRunner;
    public SpriteSheet mallBrute;
    public SpriteSheet mallBloater;
    public SpriteSheet mallScreamer;

    [Header("Hospital zombies")]
    [Tooltip("The same kinds dressed as patients and staff. Each falls back to the base sheet without one.")]
    public SpriteSheet hospWalker;
    public SpriteSheet hospRunner;
    public SpriteSheet hospBrute;
    public SpriteSheet hospBloater;
    public SpriteSheet hospScreamer;

    [Header("Airport zombies")]
    [Tooltip("The same kinds dressed as passengers and crew. Each falls back to the base sheet without one.")]
    public SpriteSheet airWalker;
    public SpriteSheet airRunner;
    public SpriteSheet airBrute;
    public SpriteSheet airBloater;
    public SpriteSheet airScreamer;

    [Header("Ranged zombies (the bullet-hell kinds), by zone")]
    public SpriteSheet spitter;
    public SpriteSheet retcher;
    public SpriteSheet gunner;
    public SpriteSheet lobber;
    public SpriteSheet pustule;
    public SpriteSheet leaper;
    public SpriteSheet mallSpitter;
    public SpriteSheet mallRetcher;
    public SpriteSheet mallGunner;
    public SpriteSheet mallLobber;
    public SpriteSheet mallPustule;
    public SpriteSheet mallLeaper;
    public SpriteSheet hospSpitter;
    public SpriteSheet hospRetcher;
    public SpriteSheet hospGunner;
    public SpriteSheet hospLobber;
    public SpriteSheet hospPustule;
    public SpriteSheet hospLeaper;
    public SpriteSheet airSpitter;
    public SpriteSheet airRetcher;
    public SpriteSheet airGunner;
    public SpriteSheet airLobber;
    public SpriteSheet airPustule;
    public SpriteSheet airLeaper;

    [Header("Bosses")]
    public SpriteSheet butcher;
    public SpriteSheet broodmother;
    public SpriteSheet howler;
    [Tooltip("The Shopping Mall's bosses. Each falls back to a Military Base boss's sheet without one.")]
    public SpriteSheet warden;
    public SpriteSheet mannequin;
    public SpriteSheet glutton;
    [Tooltip("The Hospital's bosses. Each falls back to a Military Base boss's sheet without one.")]
    public SpriteSheet surgeon;
    public SpriteSheet gurney;
    public SpriteSheet hazmat;
    [Tooltip("The Airport's bosses. Each falls back to a Military Base boss's sheet without one.")]
    public SpriteSheet handler;
    public SpriteSheet captain;
    public SpriteSheet marshal;

    [Header("Survivors")]
    public SpriteSheet mechanic;
    public SpriteSheet medic;
    public SpriteSheet quartermaster;
    public SpriteSheet trader;
    [Tooltip("The fifth survivor (chef's whites). Falls back to the trader's sheet without one.")]
    public SpriteSheet cook;

    [Header("Items")]
    [Tooltip("The items atlas: consumables, ammo, materials, melee upgrades, attachments (see ItemSprites).")]
    public SpriteSheet items;
    [Tooltip("Story items and the supply room's loot: row 0 the truck parts (StoryItem order), row 1 scrap piles small to big (SupplyShelfItem).")]
    public SpriteSheet storyItems;
    [Tooltip("The supply crate: closed, then open.")]
    public SpriteSheet supplyCrate;
    [Tooltip("The floor hatch from above: closed, then open.")]
    public SpriteSheet hatch;
    [Tooltip("The zone exits' flat floor pieces from above (Items/ExitFloors.png): row 0 the escalator's steps, row 1 the baggage belt; column 0 stopped, 1-4 running.")]
    public SpriteSheet exitFloors;
    [Tooltip("The hideout range's training dummy, standing, facing right (TrainingDummy).")]
    public SpriteSheet trainingDummy;

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

    [Header("Furniture (see FurnitureSprite)")]
    [Tooltip("Furniture, stations, the uplink and pillars by name (Tools/Sprites/furniture.py).")]
    public FurnitureEntry[] furniture = new FurnitureEntry[0];

    [System.Serializable]
    public class FurnitureEntry
    {
        public string name = "Crate";
        [Tooltip("Rows: facing the camera, right, away, left, then the same four dusty; columns: animation frames.")]
        public SpriteSheet sheet;
        [Tooltip("The glowing pixels (screens, lamps, fire) in the same layout, drawn unlit on top. Optional.")]
        public SpriteSheet glow;
        public int frames = 1;
        public float fps;
    }

    public FurnitureEntry ForFurniture(string name)
    {
        foreach (FurnitureEntry entry in furniture)
        {
            if (entry != null && entry.name == name && entry.sheet != null)
                return entry;
        }

        return null;
    }

    [Header("Guns (by Gun prefab name)")]
    public GunEntry[] guns = new GunEntry[0];
    [Tooltip("Attachments drawn on held gun sprites: frame 0 suppressor, frame 1 laser sight (see GunSprite).")]
    public SpriteSheet gunAttachments;

    [Header("Rooms")]
    public Material floor;
    public Material floorVariant;
    public Material corridorFloor;
    public Material wall;
    public Material door;
    [Tooltip("Floors for themed rooms (Tools/Sprites/themefloors.py), found by name: PixelTheme<Name> (FloorGenerator ThemeFloorName).")]
    public Material[] themeFloors = new Material[0];

    // A theme floor by its name (without the PixelTheme prefix), or null.
    public Material ThemeFloor(string name)
    {
        foreach (Material material in themeFloors)
        {
            if (material != null && material.name == "PixelTheme" + name)
                return material;
        }

        return null;
    }

    [Header("Effects")]
    [Tooltip("Hard-edged square for blood and spark particles.")]
    public Material particle;
    [Tooltip("Pixel flame for muzzle flashes.")]
    public Material muzzleFlame;
    [Tooltip("Unlit, alpha-cut base material for sprites that glow (acid); each sheet gets a copy with its texture.")]
    public Material glowSprite;

    [Header("Fire")]
    [Tooltip("A grenade going off: one row of frames, standing up from the floor.")]
    public SpriteSheet explosion;
    [Tooltip("One tongue of a molotov's fire: row 0 flickering loop, row 1 dying down.")]
    public SpriteSheet flame;
    [Tooltip("A molotov's burning fuel from above: row 0 burning loop, row 1 dying to char.")]
    public SpriteSheet firePool;

    [Header("Blood")]
    [Tooltip("Where a zombie fell, from above: one row of pools (Tools/Sprites/blood.py).")]
    public SpriteSheet bloodPool;
    [Tooltip("A shot's spatter on the floor, from above, thrown toward the right: one row of variants.")]
    public SpriteSheet bloodDrip;
    [Tooltip("A shot's spray in the air, standing, toward the right: a row of frames per variant.")]
    public SpriteSheet bloodSplash;

    [Header("Acid")]
    [Tooltip("A bloater bursting / a glob splashing: one row of frames, standing up from the floor.")]
    public SpriteSheet acidBurst;
    [Tooltip("The acid pool from above: row 0 bubbling loop, row 1 drying up.")]
    public SpriteSheet acidPool;
    [Tooltip("The Broodmother's glob: row 0 wobbling in flight (trail to the left), row 1 its shadow.")]
    public SpriteSheet acidGlob;
    [Tooltip("Zombie projectiles (EnemyBullet): row 0 bile glob, row 1 tracer round, row 2 thrown debris; pointing right.")]
    public SpriteSheet enemyShots;

    [Header("Small effects")]
    [Tooltip("Spent cases: 4 turns per row, row 0 brass, row 1 shotgun shell (ShellCasing).")]
    public SpriteSheet casing;
    [Tooltip("The dithered round shadow under every character, lying flat.")]
    public SpriteSheet shadow;
    [Tooltip("Bloody shoe prints, toes right: left/right columns, rows fresh to faded (BloodyFootprints).")]
    public SpriteSheet footprint;

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
            case EnemyKind.Spitter: return spitter;
            case EnemyKind.Retcher: return retcher;
            case EnemyKind.Gunner: return gunner;
            case EnemyKind.Lobber: return lobber;
            case EnemyKind.Pustule: return pustule;
            case EnemyKind.Leaper: return leaper;
            default: return walker;
        }
    }

    // A kind's look in a zone of the map (the mall's shoppers and staff, the hospital's patients
    // and staff), or its base look.
    public SpriteSheet For(EnemyKind kind, Zone zone)
    {
        SpriteSheet dressed = null;

        if (zone == Zone.ShoppingMall)
        {
            switch (kind)
            {
                case EnemyKind.Runner: dressed = mallRunner; break;
                case EnemyKind.Brute: dressed = mallBrute; break;
                case EnemyKind.Bloater: dressed = mallBloater; break;
                case EnemyKind.Screamer: dressed = mallScreamer; break;
                case EnemyKind.Spitter: dressed = mallSpitter; break;
                case EnemyKind.Retcher: dressed = mallRetcher; break;
                case EnemyKind.Gunner: dressed = mallGunner; break;
                case EnemyKind.Lobber: dressed = mallLobber; break;
                case EnemyKind.Pustule: dressed = mallPustule; break;
                case EnemyKind.Leaper: dressed = mallLeaper; break;
                default: dressed = mallWalker; break;
            }
        }
        else if (zone == Zone.Hospital)
        {
            switch (kind)
            {
                case EnemyKind.Runner: dressed = hospRunner; break;
                case EnemyKind.Brute: dressed = hospBrute; break;
                case EnemyKind.Bloater: dressed = hospBloater; break;
                case EnemyKind.Screamer: dressed = hospScreamer; break;
                case EnemyKind.Spitter: dressed = hospSpitter; break;
                case EnemyKind.Retcher: dressed = hospRetcher; break;
                case EnemyKind.Gunner: dressed = hospGunner; break;
                case EnemyKind.Lobber: dressed = hospLobber; break;
                case EnemyKind.Pustule: dressed = hospPustule; break;
                case EnemyKind.Leaper: dressed = hospLeaper; break;
                default: dressed = hospWalker; break;
            }
        }
        else if (zone == Zone.Airport)
        {
            switch (kind)
            {
                case EnemyKind.Runner: dressed = airRunner; break;
                case EnemyKind.Brute: dressed = airBrute; break;
                case EnemyKind.Bloater: dressed = airBloater; break;
                case EnemyKind.Screamer: dressed = airScreamer; break;
                case EnemyKind.Spitter: dressed = airSpitter; break;
                case EnemyKind.Retcher: dressed = airRetcher; break;
                case EnemyKind.Gunner: dressed = airGunner; break;
                case EnemyKind.Lobber: dressed = airLobber; break;
                case EnemyKind.Pustule: dressed = airPustule; break;
                case EnemyKind.Leaper: dressed = airLeaper; break;
                default: dressed = airWalker; break;
            }
        }

        return dressed != null ? dressed : For(kind);
    }

    public SpriteSheet For(BossKind kind)
    {
        switch (kind)
        {
            case BossKind.Broodmother: return broodmother;
            case BossKind.Howler: return howler;
            case BossKind.Warden: return warden != null ? warden : butcher;
            case BossKind.Mannequin: return mannequin != null ? mannequin : howler;
            case BossKind.Glutton: return glutton != null ? glutton : broodmother;
            case BossKind.Surgeon: return surgeon != null ? surgeon : howler;
            case BossKind.Gurney: return gurney != null ? gurney : broodmother;
            case BossKind.Hazmat: return hazmat != null ? hazmat : butcher;
            case BossKind.Handler: return handler != null ? handler : butcher;
            case BossKind.Captain: return captain != null ? captain : howler;
            case BossKind.Marshal: return marshal != null ? marshal : butcher;
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
            case NpcKind.Cook: return cook != null ? cook : trader;
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
