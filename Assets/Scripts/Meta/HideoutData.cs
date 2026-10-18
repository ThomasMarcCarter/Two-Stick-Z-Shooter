using System;
using UnityEngine;

// Crafting materials found on runs and spent, with scrap, on hideout station upgrades. Carried
// in the backpack, and at risk until sent home from an uplink terminal (see UplinkTerminal).
public enum MaterialType
{
    Parts,
    Wiring,
    Chemicals,
    Gunpowder,
    Fuel
}

// Survivors found caged on runs. Freeing one sends them to the hideout for good, where they set
// up their station. Appended only: the save stores rescues by this order.
public enum NpcKind
{
    Mechanic,
    Medic,
    Quartermaster,
    Trader
}

// The hideout's stations. Each holds some of the permanent upgrades (the trade post sells
// supplies instead); all but the generator need their NPC rescued first.
public enum HideoutStation
{
    Generator,
    Workbench,
    MedStation,
    AmmoPress,
    TradePost,
    IntelDesk
}

// What each material is called and looks like.
public static class Materials
{
    public static readonly MaterialType[] All = (MaterialType[])Enum.GetValues(typeof(MaterialType));

    public static string Name(MaterialType type)
    {
        switch (type)
        {
            case MaterialType.Parts: return "PARTS";
            case MaterialType.Wiring: return "WIRING";
            case MaterialType.Chemicals: return "CHEMICALS";
            case MaterialType.Gunpowder: return "GUNPOWDER";
            default: return "FUEL";
        }
    }

    // Three letters, for the HUD and cost lists.
    public static string Short(MaterialType type)
    {
        switch (type)
        {
            case MaterialType.Parts: return "PRT";
            case MaterialType.Wiring: return "WIR";
            case MaterialType.Chemicals: return "CHM";
            case MaterialType.Gunpowder: return "GPW";
            default: return "FUL";
        }
    }

    public static Color Colour(MaterialType type)
    {
        switch (type)
        {
            case MaterialType.Parts: return new Color(0.75f, 0.78f, 0.82f);
            case MaterialType.Wiring: return new Color(1f, 0.6f, 0.2f);
            case MaterialType.Chemicals: return new Color(0.45f, 0.85f, 1f);
            case MaterialType.Gunpowder: return new Color(1f, 0.85f, 0.35f);
            default: return new Color(1f, 0.35f, 0.3f);
        }
    }

    public static string Hex(MaterialType type) => "#" + ColorUtility.ToHtmlStringRGB(Colour(type));

    // How many of one material fit in a backpack cell.
    public const int StackSize = 10;

    // "3 PARTS, 1 FUEL" for a set of amounts (indexed by MaterialType), coloured per material
    // unless asked for plain text; empty if none.
    public static string Describe(int[] amounts, bool coloured = true)
    {
        System.Text.StringBuilder text = new System.Text.StringBuilder();

        for (int i = 0; i < amounts.Length; i++)
        {
            if (amounts[i] <= 0)
                continue;

            if (text.Length > 0)
                text.Append(", ");

            MaterialType type = (MaterialType)i;
            text.Append(coloured ? $"<color={Hex(type)}>{amounts[i]} {Name(type)}</color>" : $"{amounts[i]} {Name(type)}");
        }

        return text.ToString();
    }
}

// A material and how many of it.
[Serializable]
public struct MaterialCost
{
    public MaterialType Type;
    public int Amount;

    public MaterialCost(MaterialType type, int amount)
    {
        Type = type;
        Amount = amount;
    }
}

// Who each survivor is, where they're found, and what they say.
public static class Npcs
{
    public static readonly NpcKind[] All = (NpcKind[])Enum.GetValues(typeof(NpcKind));

    public static string Name(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic: return "RIGGS";
            case NpcKind.Medic: return "DR. OKAFOR";
            case NpcKind.Trader: return "MAGPIE";
            default: return "SGT. VOSS";
        }
    }

    public static string Role(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic: return "MECHANIC";
            case NpcKind.Medic: return "MEDIC";
            case NpcKind.Trader: return "TRADER";
            default: return "QUARTERMASTER";
        }
    }

    // Completed runs (final boss beaten) before their distress signal can be picked up: the
    // mechanic after the first, the medic after the second, the quartermaster after the third,
    // the trader after the fourth.
    public static int RequiredVictories(NpcKind kind) => (int)kind + 1;

    // Their signal is out there: enough runs completed, and not rescued yet.
    public static bool Findable(NpcKind kind)
    {
        return !MetaProgress.IsRescued(kind) && MetaProgress.Victories >= RequiredVictories(kind);
    }

    // Who a run can turn up: the earliest findable survivor, or null if there's none.
    public static NpcKind? NextToFind()
    {
        foreach (NpcKind kind in All)
        {
            if (Findable(kind))
                return kind;
        }

        return null;
    }

    // The radio message when a floor has them caged somewhere on it.
    public static string DistressCall(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic: return "...ANYONE? THIS IS RIGGS... THEY'VE GOT ME LOCKED UP... SOMEWHERE ON THIS LEVEL...";
            case NpcKind.Medic: return "...MAYDAY... DR. OKAFOR... CAGED... DEEP IN... PLEASE HURRY...";
            case NpcKind.Trader: return "...MAGPIE HERE... GOT GOODS TO TRADE... IF SOMEONE GETS ME OUT OF THIS BOX...";
            default: return "...SERGEANT VOSS, PINNED DOWN... CAGED IN THE FAR ROOMS... REQUEST EXTRACTION...";
        }
    }

    public static HideoutStation Station(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic: return HideoutStation.Workbench;
            case NpcKind.Medic: return HideoutStation.MedStation;
            case NpcKind.Trader: return HideoutStation.TradePost;
            default: return HideoutStation.AmmoPress;
        }
    }

    // The colour their speech is drawn in: the green of the UI, shifted a little per person.
    public static Color Voice(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic: return new Color(0.55f, 1f, 0.45f);
            case NpcKind.Medic: return new Color(0.4f, 1f, 0.75f);
            case NpcKind.Trader: return new Color(0.85f, 1f, 0.55f);
            default: return new Color(0.75f, 1f, 0.4f);
        }
    }

    // Shouted from the cage while zombies are still in the room.
    public static readonly string[] CagedPleas =
    {
        "HEY! OVER HERE!",
        "GET ME OUT OF THIS THING!",
        "WATCH OUT, THEY'RE EVERYWHERE!",
        "DON'T LEAVE ME HERE!"
    };

    // Once the room is clear and the player is near.
    public static string CagedReady(NpcKind kind)
    {
        return "THE LATCH! QUICK, OPEN IT!";
    }

    public static string Freed(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic: return "OWE YOU ONE. I'LL GET YOUR WORKBENCH RUNNING.";
            case NpcKind.Medic: return "THANK GOD. FIND ME AT THE HIDEOUT, I CAN PATCH YOU UP.";
            case NpcKind.Trader: return "A PLEASURE DOING BUSINESS. I'LL SET UP SHOP IN YOUR BARRACKS.";
            default: return "GOOD TIMING, SOLDIER. I'LL SORT YOUR SUPPLY LINES.";
        }
    }

    // First thing they say each time the player comes over in the hideout.
    public static string[] Greetings(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic:
                return new[] { "BRING ME PARTS AND WIRE, I'LL BUILD YOU SOMETHING.", "BENCH IS WARM. WHAT ARE WE MAKING?", "STILL IN ONE PIECE? GOOD." };
            case NpcKind.Medic:
                return new[] { "LET ME SEE THOSE CUTS.", "CHEMICALS. I ALWAYS NEED MORE CHEMICALS.", "STAY STILL. THIS WON'T HURT. MUCH." };
            case NpcKind.Trader:
                return new[] { "BROWSING OR BUYING?", "FRESH STOCK. WELL, FRESH-ISH.", "SCRAP TALKS, FRIEND." };
            default:
                return new[] { "QUARTERMASTER'S OPEN. MAKE IT QUICK.", "POWDER WINS WARS, SOLDIER.", "COUNT YOUR ROUNDS. THEN COUNT THEM AGAIN." };
        }
    }

    // Said now and then while they're near but the player isn't talking to them.
    public static string[] Idle(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic:
                return new[] { "WHERE'D I PUT THAT WRENCH...", "THIS GENERATOR IS HELD TOGETHER WITH TAPE.", "*HUMMING*" };
            case NpcKind.Medic:
                return new[] { "STERILE. MOSTLY.", "I USED TO HAVE A CLINIC, YOU KNOW.", "SOMEONE KEEPS STEALING THE GAUZE." };
            case NpcKind.Trader:
                return new[] { "EVERYTHING HAS A PRICE.", "*COUNTING SCRAP*", "NO REFUNDS. NO EXCEPTIONS." };
            default:
                return new[] { "INVENTORY CHECK. AGAIN.", "BACK IN MY DAY WE HAD RATIONS.", "NOBODY TOUCHES MY CRATES." };
        }
    }

    // When the player buys an upgrade at their station.
    public static string[] Upgraded(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic: return new[] { "NOW WE'RE TALKING.", "SHE'LL HOLD. PROBABLY.", "NICE. GO BREAK IT IN." };
            case NpcKind.Medic: return new[] { "THAT SHOULD KEEP YOU BREATHING.", "DOCTOR'S ORDERS: DON'T DIE.", "BETTER. MUCH BETTER." };
            case NpcKind.Trader: return new[] { "PLEASURE DOING BUSINESS.", "SPEND IT WISELY. OR DON'T.", "COME BACK SOON. WITH MORE SCRAP." };
            default: return new[] { "LOGGED AND STOCKED.", "THAT'S MORE LIKE IT.", "DON'T WASTE IT." };
        }
    }
}

// What each station is called, who runs it, and which upgrades it holds.
public static class Stations
{
    public static readonly HideoutStation[] All = (HideoutStation[])Enum.GetValues(typeof(HideoutStation));

    public static string Name(HideoutStation station)
    {
        switch (station)
        {
            case HideoutStation.Generator: return "GENERATOR";
            case HideoutStation.Workbench: return "WORKBENCH";
            case HideoutStation.MedStation: return "MED STATION";
            case HideoutStation.TradePost: return "TRADE POST";
            case HideoutStation.IntelDesk: return "INTEL DESK";
            default: return "AMMO PRESS";
        }
    }

    // The NPC who has to be rescued before it can be used; null for the generator and the intel
    // desk, which are there from the start.
    public static NpcKind? Owner(HideoutStation station)
    {
        switch (station)
        {
            case HideoutStation.Workbench: return NpcKind.Mechanic;
            case HideoutStation.MedStation: return NpcKind.Medic;
            case HideoutStation.AmmoPress: return NpcKind.Quartermaster;
            case HideoutStation.TradePost: return NpcKind.Trader;
            default: return null;
        }
    }

    public static MetaUpgrade[] Upgrades(HideoutStation station)
    {
        switch (station)
        {
            case HideoutStation.Generator: return new[] { MetaUpgrade.Generator };
            case HideoutStation.Workbench: return new[] { MetaUpgrade.Armory, MetaUpgrade.Gunsmith };
            case HideoutStation.MedStation: return new[] { MetaUpgrade.Toughness, MetaUpgrade.Conditioning, MetaUpgrade.FieldKit };
            case HideoutStation.TradePost: return new MetaUpgrade[0];
            case HideoutStation.IntelDesk: return new[] { MetaUpgrade.Recon };
            default: return new[] { MetaUpgrade.AmmoCache, MetaUpgrade.Scavenger };
        }
    }

    public static Color Colour(HideoutStation station)
    {
        switch (station)
        {
            case HideoutStation.Generator: return new Color(1f, 0.8f, 0.3f);
            case HideoutStation.Workbench: return new Color(1f, 0.6f, 0.25f);
            case HideoutStation.MedStation: return new Color(0.45f, 0.9f, 1f);
            case HideoutStation.TradePost: return new Color(1f, 0.85f, 0.35f);
            case HideoutStation.IntelDesk: return new Color(0.35f, 1f, 0.55f);
            default: return new Color(0.85f, 1f, 0.35f);
        }
    }

    public static bool Unlocked(HideoutStation station)
    {
        NpcKind? owner = Owner(station);
        return owner == null || MetaProgress.IsRescued(owner.Value);
    }
}

// What the trader sells and for how much scrap. The stock is a few consumables with a few of
// each, re-rolled once per run (coming back to the hideout without having started a run keeps
// the same stock, sold-out items included); bought ones go straight into the player's pack for
// the next run. Until the player deploys they're unsettled: leaving the hideout any other way
// (menu, quitting) refunds them, scrap and stock, so nothing paid for is ever lost.
public static class TraderStock
{
    public struct Offer
    {
        public ConsumableType Type;
        public int Price;
        public int Left;
    }

    private const int OfferCount = 4;

    private static Offer[] offers = new Offer[0];
    private static int stockedAtRun = -1;
    private static int[] unsettled = new int[0];   // bought since the last deployment, per offer

    public static Offer[] Offers => offers;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        offers = new Offer[0];
        stockedAtRun = -1;
        unsettled = new int[0];
        Application.quitting -= RefundUnsettled;
        Application.quitting += RefundUnsettled;
    }

    public static int Price(ConsumableType type)
    {
        switch (type)
        {
            case ConsumableType.Medkit: return 20;
            case ConsumableType.LargeMedkit: return 40;
            case ConsumableType.Adrenaline: return 25;
            case ConsumableType.Grenade: return 30;
            case ConsumableType.Molotov: return 25;
            default: return 5;
        }
    }

    // A fresh stock: four different consumables, one to three of each (bottles come in more).
    // Does nothing if the stock is already this run's.
    public static void Restock(System.Random rng)
    {
        if (stockedAtRun == MetaProgress.Runs && offers.Length > 0)
            return;

        stockedAtRun = MetaProgress.Runs;
        ConsumableType[] all = (ConsumableType[])Enum.GetValues(typeof(ConsumableType));

        for (int i = all.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (all[i], all[j]) = (all[j], all[i]);
        }

        int count = Mathf.Min(OfferCount, all.Length);
        offers = new Offer[count];

        for (int i = 0; i < count; i++)
        {
            ConsumableType type = all[i];
            int amount = type == ConsumableType.Bottle ? 3 + rng.Next(3) : 1 + rng.Next(3);
            offers[i] = new Offer { Type = type, Price = Price(type), Left = amount };
        }

        Array.Sort(offers, (a, b) => a.Type.CompareTo(b.Type));
        unsettled = new int[offers.Length];
    }

    // Why the offer can't be bought, or null if it can.
    public static string BlockedReason(int index, Backpack backpack)
    {
        if (index < 0 || index >= offers.Length)
            return "NOTHING THERE";

        Offer offer = offers[index];

        if (offer.Left <= 0)
            return "SOLD OUT";

        if (MetaProgress.Scrap < offer.Price)
            return "NOT ENOUGH SCRAP";

        if (backpack == null || backpack.RoomFor(offer.Type) <= 0)
            return "NO ROOM IN YOUR PACK";

        return null;
    }

    // Pays and puts one in the pack. False if it couldn't.
    public static bool TryBuy(int index, Backpack backpack)
    {
        if (BlockedReason(index, backpack) != null || !MetaProgress.SpendScrap(offers[index].Price))
            return false;

        backpack.AddConsumable(offers[index].Type, 1, true);
        offers[index].Left--;

        if (index < unsettled.Length)
            unsettled[index]++;

        return true;
    }

    // The player deployed with what they bought: it's theirs now (used or lost with the run).
    public static void Settle()
    {
        unsettled = new int[offers.Length];
    }

    // Leaving the hideout without deploying: the scrap comes back and the goods go back on the
    // shelf (the pack is reset with the scene anyway).
    public static void RefundUnsettled()
    {
        int scrap = 0;

        for (int i = 0; i < unsettled.Length && i < offers.Length; i++)
        {
            scrap += unsettled[i] * offers[i].Price;
            offers[i].Left += unsettled[i];
        }

        unsettled = new int[offers.Length];

        if (scrap > 0)
            MetaProgress.AddScrap(scrap);
    }
}
