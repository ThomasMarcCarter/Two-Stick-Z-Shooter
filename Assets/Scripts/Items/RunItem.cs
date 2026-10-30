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
    PlateCarrier
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
