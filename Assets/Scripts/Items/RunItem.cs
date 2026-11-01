using UnityEngine;

public enum RunItem
{
    RattleMag,
    IncendiaryRounds,
    RicochetRounds,
    LastWord,
    HungryMag,
    TumbleLoader,
    FuelLeak,
    HardLanding,
    CloseCall,
    GlassJaw,
    CorpseBloat,
    DinnerBell,
    PlateCarrier,
    HairTrigger,
    TwinFeed,
    PointBlank,
    SeekerRounds,
    ColdOpen,
    WideBeam,
    OiledBolt,
    Bloodletter,
    Bayonet,
    Whirlwind,
    BatterUp,
    ShortFuse,
    ClusterPin,
    FieldMedic,
    JunkDrawer
}

// The run items: passive finds that change the rules for the rest of a run, most with a catch.
// A powerup case beside the treasure room's and each boss's crate holds a choice of three
// (FloorGenerator.Items, PowerupCase); the player holds as many as they find and never sees one
// they hold again that run. They're lost when the run ends. PlayerItems holds them and carries out what they do.
public static class RunItems
{
    public static readonly RunItem[] All = (RunItem[])System.Enum.GetValues(typeof(RunItem));

    public static string Name(RunItem item)
    {
        switch (item)
        {
            case RunItem.RattleMag: return "RATTLE MAG";
            case RunItem.IncendiaryRounds: return "INCENDIARY ROUNDS";
            case RunItem.RicochetRounds: return "RICOCHET ROUNDS";
            case RunItem.LastWord: return "LAST WORD";
            case RunItem.HungryMag: return "HUNGRY MAG";
            case RunItem.TumbleLoader: return "TUMBLE LOADER";
            case RunItem.FuelLeak: return "FUEL LEAK";
            case RunItem.HardLanding: return "HARD LANDING";
            case RunItem.CloseCall: return "CLOSE CALL";
            case RunItem.GlassJaw: return "GLASS JAW";
            case RunItem.CorpseBloat: return "CORPSE BLOAT";
            case RunItem.DinnerBell: return "DINNER BELL";
            case RunItem.HairTrigger: return "HAIR TRIGGER";
            case RunItem.TwinFeed: return "TWIN FEED";
            case RunItem.PointBlank: return "POINT BLANK";
            case RunItem.SeekerRounds: return "SEEKER ROUNDS";
            case RunItem.ColdOpen: return "COLD OPEN";
            case RunItem.WideBeam: return "WIDE BEAM";
            case RunItem.OiledBolt: return "OILED BOLT";
            case RunItem.Bloodletter: return "BLOODLETTER";
            case RunItem.Bayonet: return "BAYONET";
            case RunItem.Whirlwind: return "WHIRLWIND";
            case RunItem.BatterUp: return "BATTER UP";
            case RunItem.ShortFuse: return "SHORT FUSE";
            case RunItem.ClusterPin: return "CLUSTER PIN";
            case RunItem.FieldMedic: return "FIELD MEDIC";
            case RunItem.JunkDrawer: return "JUNK DRAWER";
            default: return "ARMOR PLATE CARRIER";
        }
    }

    public static string Describe(RunItem item)
    {
        switch (item)
        {
            case RunItem.RattleMag: return "FINISHING A RELOAD STUNS ZOMBIES WITHIN 5M, BUT THE WHOLE ROOM HEARS IT";
            case RunItem.IncendiaryRounds: return "A BULLET KILL LEAVES A SMALL FIRE, WHICH BURNS YOU TOO";
            case RunItem.RicochetRounds: return "BULLETS BOUNCE ONCE OFF WALLS, AND A BOUNCED BULLET CAN HIT YOU";
            case RunItem.LastWord: return "A MAGAZINE'S LAST SHOT DOES TRIPLE DAMAGE AND EXPLODES, WHICH CAN HURT YOU";
            case RunItem.HungryMag: return "EVERY KILL LOADS A FREE ROUND, BUT RELOADS TAKE TWICE AS LONG";
            case RunItem.TumbleLoader: return "LANDING A ROLL RELOADS YOUR GUN, BUT ROLLS COST MORE STAMINA";
            case RunItem.FuelLeak: return "ROLLING LEAVES A TRAIL OF FIRE, WHICH BURNS YOU IF YOU TURN BACK";
            case RunItem.HardLanding: return "LANDING A ROLL KNOCKS BACK ZOMBIES WITHIN 3M, WITH A LOUD THUD";
            case RunItem.CloseCall: return "ROLLING THROUGH A ZOMBIE'S SHOT REFUNDS THE ROLL'S STAMINA AND COOLDOWN";
            case RunItem.GlassJaw: return "GUNS AND MELEE DO DOUBLE DAMAGE, BUT YOU TAKE DOUBLE DAMAGE";
            case RunItem.CorpseBloat: return "KILLED ZOMBIES EXPLODE A SECOND LATER, HURTING ZOMBIES AND YOU";
            case RunItem.DinnerBell: return "COMBAT ROOMS HOLD 2 MORE ZOMBIES, AND CLEARING ONE ALWAYS DROPS EXTRA LOOT";
            case RunItem.HairTrigger: return "GUNS FIRE 40% FASTER, BUT WITH MUCH WIDER SPREAD";
            case RunItem.TwinFeed: return "EACH SHOT FIRES TWO BULLETS, BUT USES TWO ROUNDS";
            case RunItem.PointBlank: return "BULLETS DO DOUBLE DAMAGE WITHIN 4M, BUT HALF BEYOND 8M";
            case RunItem.SeekerRounds: return "BULLETS CURVE TOWARD THE NEAREST ZOMBIE, BUT DO 25% LESS DAMAGE";
            case RunItem.ColdOpen: return "HITTING AN UNAWARE ZOMBIE DOES 5X DAMAGE, BUT ZOMBIES HEAR YOU FROM FARTHER AWAY";
            case RunItem.WideBeam: return "YOUR FLASHLIGHT BEAM IS TWICE AS WIDE, BUT ZOMBIES SEE YOU FROM FARTHER AWAY";
            case RunItem.OiledBolt: return "ZOMBIES CAN'T HEAR YOUR GUNSHOTS, BUT GUNS DO 15% LESS DAMAGE";
            case RunItem.Bloodletter: return "EVERY 5TH MELEE KILL HEALS 1, BUT GUNS DO 25% LESS DAMAGE";
            case RunItem.Bayonet: return "EACH MELEE HIT LOADS A FREE ROUND, BUT SWINGS ARE SLOWER";
            case RunItem.Whirlwind: return "SWINGS HIT ALL THE WAY ROUND, BUT COST MORE STAMINA";
            case RunItem.BatterUp: return "SWINGS KNOCK ZOMBIE SHOTS BACK AT ZOMBIES, BUT THE SWING COOLDOWN IS LONGER";
            case RunItem.ShortFuse: return "GRENADES EXPLODE ON IMPACT WITH A 50% BIGGER BLAST";
            case RunItem.ClusterPin: return "GRENADES SPLIT INTO 3 SMALL BLASTS, EACH MUCH WEAKER THAN A NORMAL ONE";
            case RunItem.FieldMedic: return "MEDKITS HEAL TWICE AS MUCH, BUT TAKE TWICE AS LONG";
            case RunItem.JunkDrawer: return "CLEARED COMBAT ROOMS DROP A RANDOM THROWABLE, BUT SUPPLY CRATES HOLD ONE LESS ITEM";
            default: return "3 ARMOR PLATES OVER YOUR HEALTH, EACH STOPS 1 DAMAGE // REFILL FROM CRATES, RARE DROPS AND BOSSES";
        }
    }

    public static Color Colour(RunItem item)
    {
        switch (item)
        {
            case RunItem.IncendiaryRounds:
            case RunItem.FuelLeak: return new Color(1f, 0.55f, 0.2f);
            case RunItem.RicochetRounds:
            case RunItem.CloseCall: return new Color(0.45f, 1f, 0.55f);
            case RunItem.GlassJaw: return new Color(0.6f, 0.9f, 1f);
            case RunItem.LastWord:
            case RunItem.CorpseBloat: return new Color(1f, 0.35f, 0.3f);
            case RunItem.PlateCarrier: return UiColours.Amber;
            default: return new Color(1f, 0.85f, 0.3f);
        }
    }

    // A spare armour plate, as shown in crates and on the floor.
    public static readonly Color PlateColour = UiColours.Amber;
}
