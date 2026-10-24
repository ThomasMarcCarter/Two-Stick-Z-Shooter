using System;
using System.Collections.Generic;
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
// up their station. The cook isn't found: they turn up at the hideout infected once the trader's
// goal is done, and only set up their kitchen once the medic has cured them.
// Appended only: the save stores rescues by this order.
public enum NpcKind
{
    Mechanic,
    Medic,
    Quartermaster,
    Trader,
    Cook
}

// The hideout's stations. Each holds some of the permanent upgrades (the trade post sells
// supplies instead, and the truck bay holds the truck being repaired); all but the generator,
// the intel desk and the truck bay need their NPC rescued first.
public enum HideoutStation
{
    Generator,
    Workbench,
    MedStation,
    AmmoPress,
    TradePost,
    IntelDesk,
    TruckBay,
    Kitchen
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
            case NpcKind.Cook: return "ROSA";
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
            case NpcKind.Cook: return "COOK";
            default: return "QUARTERMASTER";
        }
    }

    // The mechanic isn't out on the floors: they're held in the supply room under the last floor,
    // found on the first run that gets that far, with the truck's first part.
    public static bool FoundInSupplyRoom(NpcKind kind) => kind == NpcKind.Mechanic;

    // The cook isn't found on a run: they stumble into the hideout, bitten, the visit the trader's
    // shelves are full (FloorGenerator.Hideout.cs).
    public static bool ArrivesInfected(NpcKind kind) => kind == NpcKind.Cook;

    // Ready to turn up: the trader's goal done, and not here yet.
    public static bool CookDue => !MetaProgress.IsRescued(NpcKind.Cook) && StoryItems.Complete(NpcKind.Trader);

    // Here, and still infected: until the medic's goal (the cure) is done.
    public static bool CookSick => MetaProgress.IsRescued(NpcKind.Cook) && !StoryItems.Complete(NpcKind.Medic);

    // How sick the cook looks and sounds, 1 to 3: worse a step every two runs since they came,
    // a step better for every two medical supplies brought home. Story only: they never die.
    public static int CookSickness
    {
        get
        {
            if (!CookSick)
                return 0;

            int runs = Mathf.Max(0, MetaProgress.Runs - MetaProgress.CookArrivedRun);
            return Mathf.Clamp(1 + runs / 2 - StoryItems.MedicalSuppliesRecovered / 2, 1, 3);
        }
    }

    // Truck parts recovered (one per completed run) before their distress signal can be picked
    // up on a run: the medic after the second, the quartermaster after the third, the trader after
    // the fourth (the mechanic comes with the first; see FoundInSupplyRoom).
    public static int RequiredParts(NpcKind kind) => FoundInSupplyRoom(kind) ? 0 : (int)kind + 1;

    // Their signal is out there: enough truck parts recovered, and not rescued yet.
    public static bool Findable(NpcKind kind)
    {
        return !MetaProgress.IsRescued(kind) && !FoundInSupplyRoom(kind) && !ArrivesInfected(kind)
            && StoryItems.TruckPartsRecovered >= RequiredParts(kind);
    }

    // How to find them, for their abandoned area's sign (short lines, upper case), or null once
    // their signal is out there.
    public static string FindHint(NpcKind kind)
    {
        if (FoundInSupplyRoom(kind))
            return $"HELD IN THE SUPPLY ROOM UNDER THE LAST FLOOR // FINISH A RUN TO REACH THE {Role(kind)}";

        if (ArrivesInfected(kind))
        {
            return CookSick
                ? $"THE {Role(kind)} IS SICK IN THE MED BAY // {Name(NpcKind.Medic)} NEEDS MEDICAL SUPPLIES FROM THE HOSPITAL"
                : $"NOBODY HERE YET // FILL {Name(NpcKind.Trader)}'S SHELVES WITH GOODS FROM THE MALL";
        }

        int needed = RequiredParts(kind) - StoryItems.TruckPartsRecovered;
        return needed > 0 ? $"NO SIGNAL // RECOVER {needed} MORE TRUCK PART{(needed == 1 ? "" : "S")} TO PICK UP THE {Role(kind)}" : null;
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
            case NpcKind.Mechanic: return "...ANYONE? THIS IS RIGGS... LOCKED IN THE SUPPLY ROOM UNDER THIS BASE... THERE'S A TRUCK PART DOWN HERE... GET ME OUT AND I'LL GET YOUR TRUCK RUNNING...";
            case NpcKind.Medic: return "...MAYDAY... DR. OKAFOR... CAGED... DEEP IN... I HEARD YOUR TRUCK'S COMING TOGETHER... TAKE ME WITH YOU...";
            case NpcKind.Trader: return "...MAGPIE HERE... GOT GOODS TO TRADE... IF SOMEONE GETS ME OUT OF THIS BOX...";
            case NpcKind.Cook: return "...HELLO? IT'S ROSA... FROM THE FOOD COURT... I'M HURT... PLEASE...";
            default: return "...SERGEANT VOSS, PINNED DOWN... CAGED IN THE FAR ROOMS... HEARD YOU'VE GOT A TRUCK... REQUEST EXTRACTION...";
        }
    }

    public static HideoutStation Station(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic: return HideoutStation.Workbench;
            case NpcKind.Medic: return HideoutStation.MedStation;
            case NpcKind.Trader: return HideoutStation.TradePost;
            case NpcKind.Cook: return HideoutStation.Kitchen;
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
            case NpcKind.Cook: return new Color(0.95f, 1f, 0.6f);
            default: return new Color(0.75f, 1f, 0.4f);
        }
    }

    // Shouted when the player shoots them, angrier the more it happens (level 0 first, then 1,
    // then 2 and on).
    public static string[] ShotAt(NpcKind kind, int level)
    {
        if (level <= 0)
        {
            switch (kind)
            {
                case NpcKind.Mechanic: return new[] { "WHAT THE HELL ARE YOU DOING?!", "WATCH IT! I'M WORKING HERE!", "HEY! POINT THAT SOMEWHERE ELSE!" };
                case NpcKind.Medic: return new[] { "WHAT THE HELL ARE YOU DOING?!", "I'M THE ONE WHO PATCHES YOU UP!", "CAREFUL! I'M NOT ONE OF THEM!" };
                case NpcKind.Trader: return new[] { "WHAT THE HELL ARE YOU DOING?!", "HEY! THAT'S NO WAY TO HAGGLE!", "WATCH THE MERCHANDISE!" };
                case NpcKind.Cook: return new[] { "WHAT THE HELL ARE YOU DOING?!", "I'M SICK, NOT ONE OF THEM!", "HEY! NOT IN MY KITCHEN!" };
                default: return new[] { "WHAT THE HELL ARE YOU DOING?!", "CEASE FIRE! FRIENDLY!", "CHECK YOUR TARGET, SOLDIER!" };
            }
        }

        if (level == 1)
            return new[] { "STOP SHOOTING AT ME!", "ARE YOU OUT OF YOUR MIND?!", "I SAID KNOCK IT OFF!", "ENOUGH!" };

        switch (kind)
        {
            case NpcKind.Mechanic: return new[] { "KEEP IT UP AND YOU CAN FIX YOUR OWN GEAR.", "I'VE HAD ROBOTS WITH BETTER MANNERS." };
            case NpcKind.Medic: return new[] { "NEXT TIME YOU'RE BLEEDING OUT, REMEMBER THIS.", "I'M WRITING THIS IN YOUR FILE." };
            case NpcKind.Trader: return new[] { "PRICES JUST WENT UP. FOR YOU.", "YOU KNOW I DON'T DO REFUNDS, RIGHT?" };
            case NpcKind.Cook: return new[] { "YOU'RE ON DISH DUTY FOR A MONTH.", "NO DESSERT. EVER." };
            default: return new[] { "ONE MORE AND YOU'RE ON LATRINE DUTY.", "THAT'S INSUBORDINATION, SOLDIER." };
        }
    }

    // The mechanic on the truck: what it still needs, or that it's ready to go.
    public static string TruckProgress()
    {
        StoryItem? next = StoryItems.Next(NpcKind.Mechanic);

        if (next == null)
            return "TRUCK'S READY. TANK'S FULL. SAY THE WORD AND WE'RE OUT OF HERE.";

        int have = StoryItems.Recovered(NpcKind.Mechanic);

        if (have == 0)
            return $"THAT TRUCK NEEDS FIVE PARTS. FIRST ON THE LIST: {StoryItems.Name(next.Value)}. CHECK THE SUPPLY ROOM UNDER THE BASE.";

        return $"{have} OF 5 PARTS IN. NEXT ON THE LIST: {StoryItems.Name(next.Value)}. THE SUPPLY ROOM SHOULD HAVE ONE.";
    }

    // The trader on their goods: what the trade post still needs, or that it's fully stocked.
    public static string GoodsProgress()
    {
        StoryItem? next = StoryItems.Next(NpcKind.Trader);

        if (next == null)
            return "SHELVES ARE FULL, FRIEND. GUNS, PARTS, THE LOT. BEST SHOP THIS SIDE OF THE APOCALYPSE.";

        int have = StoryItems.Recovered(NpcKind.Trader);

        if (have == 0)
            return $"WANT A REAL SHOP? THE MALL'S STILL STOCKED. START WITH A {StoryItems.Name(next.Value)}. SUPPLY ROOM, BOTTOM FLOOR.";

        return $"{have} OF 5 LOTS IN. MORE ON THE SHELVES EVERY TIME. NEXT: {StoryItems.Name(next.Value)}.";
    }

    // The medic on the cure: what's still needed for the cook, or that it's done.
    public static string MedicalProgress()
    {
        StoryItem? next = StoryItems.Next(NpcKind.Medic);

        if (next == null)
            return "THE COOK'S CLEAR. NO FEVER, NO SIGNS. WE DID IT.";

        int have = StoryItems.Recovered(NpcKind.Medic);

        if (have == 0)
            return $"THE COOK'S GETTING WORSE. I NEED SUPPLIES FROM THE HOSPITAL. START WITH {StoryItems.Name(next.Value)}.";

        return $"{have} OF 5 FOR THE CURE. THE COOK'S HOLDING ON. NEXT: {StoryItems.Name(next.Value)}.";
    }

    // The quartermaster on the radio set: what it still needs, or that the chopper can be called.
    public static string RadioProgress()
    {
        StoryItem? next = StoryItems.Next(NpcKind.Quartermaster);

        if (next == null)
            return "THE SET'S LIVE AND THE CHOPPER'S ON ITS WAY. WE'RE GETTING OUT, SOLDIER.";

        int have = StoryItems.Recovered(NpcKind.Quartermaster);

        if (have == 0)
            return $"THERE'S AN EVAC CHOPPER OUT THERE IF WE CAN REACH IT. THE AIRPORT HAS WHAT WE NEED. FIRST: {StoryItems.Name(next.Value)}.";

        return $"{have} OF 5 PARTS FOR THE RADIO. NEXT: {StoryItems.Name(next.Value)}. THE TOWER'S CRAWLING, STAY SHARP.";
    }

    // The item's owner on the radio when the player takes a story item off the supply room's shelf.
    public static string ItemTaken(StoryItem item)
    {
        switch (item)
        {
            case StoryItem.Battery: return "THAT'S A TRUCK BATTERY! BRING IT HOME AND SHE'LL AT LEAST TURN OVER.";
            case StoryItem.FuelPump: return "FUEL PUMP. GOOD. NOW SHE CAN DRINK. GET IT BACK HERE.";
            case StoryItem.Alternator: return "AN ALTERNATOR? YOU BEAUTY. CLIMB OUT AND BRING IT HOME.";
            case StoryItem.Tyres: return "TYRES! NO MORE RIMS ON CONCRETE. GET OUT OF THERE IN ONE PIECE.";
            case StoryItem.EngineBlock: return "THE ENGINE BLOCK... THAT'S THE LAST ONE. GET IT HOME AND WE'RE DRIVING OUT OF HERE.";
            case StoryItem.TinnedFood: return "TINNED FOOD! PEOPLE PAY ANYTHING FOR BEANS NOW. BRING IT HOME.";
            case StoryItem.BottledWater: return "CLEAN WATER. THAT'S BETTER THAN SCRAP. DON'T DROP IT.";
            case StoryItem.CampingGear: return "CAMPING GEAR? LANTERNS SELL THEMSELVES. GET IT BACK HERE.";
            case StoryItem.HardwareTools: return "TOOLS! NOW WE'RE TALKING. GET THEM HOME AND I'LL OPEN THE BACK ROOM.";
            case StoryItem.StoreSafe: return "THE SAFE... THAT'S THE MANAGER'S STASH. GET IT HOME AND WE'RE IN BUSINESS PROPER.";
            case StoryItem.BloodSamples: return "BLOOD SAMPLES. KEEP THAT COOL BOX SHUT AND GET IT HOME.";
            case StoryItem.AntiviralVials: return "ANTIVIRALS! IF ANYTHING CAN SLOW IT DOWN, IT'S THOSE. CAREFUL WITH THEM.";
            case StoryItem.IVDripKit: return "A DRIP KIT. NOW I CAN ACTUALLY GET MEDICINE INTO THEM. COME HOME.";
            case StoryItem.Centrifuge: return "A CENTRIFUGE? THAT'S A LAB IN A BOX. BRING IT BACK IN ONE PIECE.";
            case StoryItem.ResearchNotes: return "THE RESEARCH NOTES... THAT'S THE CURE. GET OUT OF THERE ALIVE.";
            case StoryItem.PowerCell: return "A POWER CELL. GOOD. THAT'LL RUN A REAL SET. BRING IT IN.";
            case StoryItem.TransmitterBoard: return "THE TRANSMITTER BOARD. NOW WE CAN TALK, NOT JUST LISTEN. MOVE OUT.";
            case StoryItem.AntennaMast: return "AN ANTENNA MAST. THAT'S OUR RANGE SORTED. GET IT HOME.";
            case StoryItem.FrequencyCrystal: return "THE CRYSTAL. ONE MORE PIECE, SOLDIER. DON'T DROP IT.";
            default: return "THE FLIGHT LOG... THE EVAC FREQUENCY. THAT'S OUR WAY OUT. GET HOME AND WE CALL IT IN.";
        }
    }

    // One-time story scenes on the hideout radio, as goals finish and the next begin. Appended
    // only: the save stores which have played by this order.
    public enum Scene
    {
        TruckRunning,      // the mechanic's goal done: the truck can drive
        MallRequest,       // the trader asks for goods from the Mall
        AirportRequest     // the quartermaster asks for radio parts from the Airport
    }

    // The scenes due on this hideout visit, in order (who speaks, what they say), each marked
    // played. The hospital's request comes with the cook's arrival (FloorGenerator.Hideout.cs).
    public static List<(NpcKind who, string line)> DueScenes()
    {
        List<(NpcKind, string)> due = new List<(NpcKind, string)>();

        void Add(Scene scene, bool ready, NpcKind who, string line)
        {
            if (!ready || MetaProgress.SceneSeen(scene) || !MetaProgress.IsRescued(who))
                return;

            MetaProgress.SeeScene(scene);
            due.Add((who, line));
        }

        Add(Scene.TruckRunning, StoryItems.Complete(NpcKind.Mechanic), NpcKind.Mechanic,
            "SHE RUNS! ALL FIVE PARTS IN AND THE ENGINE'S TICKING OVER. THE TRUCK CAN TAKE US ANYWHERE NOW.");
        Add(Scene.MallRequest, Zones.IsUnlocked(Zone.ShoppingMall), NpcKind.Trader,
            "A TRUCK, EH? THEN TAKE IT TO THE MALL. MY SHELVES ARE BARE AND THAT PLACE IS STILL STOCKED. BRING ME GOODS.");
        Add(Scene.AirportRequest, Zones.IsUnlocked(Zone.Airport), NpcKind.Quartermaster,
            "COOK'S BACK ON THEIR FEET. GOOD. NOW LISTEN UP: THERE'S AN EVAC CHOPPER OUT THERE. THE AIRPORT TOWER HAS WHAT WE NEED TO CALL IT.");
        return due;
    }

    // On the way up the chopper's ramp, the ending (EvacSequence).
    public static string Boarding(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic: return "SHE'S NO TRUCK, BUT SHE'LL DO.";
            case NpcKind.Medic: return "EVERYONE ACCOUNTED FOR? GOOD. GO.";
            case NpcKind.Trader: return "FIRST CLASS, I HOPE. I'M PAYING IN SCRAP.";
            case NpcKind.Cook: return "I'LL COOK FOR EVERYONE WHEN WE LAND. PROMISE.";
            default: return "MOVE, MOVE! WHEELS UP IN THIRTY!";
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
            case NpcKind.Mechanic: return "OWE YOU ONE. GRAB THAT PART - THERE'S A TRUCK AT YOUR HIDEOUT I CAN GET RUNNING.";
            case NpcKind.Medic: return "THANK GOD. I'LL CLEAN UP A MED BAY AT THE HIDEOUT AND PATCH YOU UP.";
            case NpcKind.Trader: return "A PLEASURE DOING BUSINESS. I'LL CLEAR A CORNER OF YOUR HIDEOUT AND SET UP SHOP.";
            case NpcKind.Cook: return "I FEEL... ALIVE. THANK YOU. LET ME COOK FOR YOU ALL. IT'S WHAT I DO.";
            default: return "GOOD TIMING, SOLDIER. I'LL TIDY UP YOUR SUPPLY ROOM AND SORT YOUR SUPPLY LINES.";
        }
    }

    // First thing they say each time the player comes over in the hideout.
    public static string[] Greetings(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic:
                return new[] { TruckProgress(), TruckProgress(), "BRING ME PARTS AND WIRE, I'LL BUILD YOU SOMETHING.", "STILL IN ONE PIECE? GOOD." };
            case NpcKind.Medic:
                return Zones.IsUnlocked(Zone.Hospital)
                    ? new[] { MedicalProgress(), "LET ME SEE THOSE CUTS.", MedicalProgress(), "STAY STILL. THIS WON'T HURT. MUCH." }
                    : new[] { "LET ME SEE THOSE CUTS.", "CHEMICALS. I ALWAYS NEED MORE CHEMICALS.", "STAY STILL. THIS WON'T HURT. MUCH." };
            case NpcKind.Trader:
                return Zones.IsUnlocked(Zone.ShoppingMall)
                    ? new[] { GoodsProgress(), "BROWSING OR BUYING?", GoodsProgress(), "SCRAP TALKS, FRIEND." }
                    : new[] { "BROWSING OR BUYING?", "FRESH STOCK. WELL, FRESH-ISH.", "SCRAP TALKS, FRIEND." };
            case NpcKind.Cook:
                return CookSick ? SickLines() : new[] { "HUNGRY? OF COURSE YOU ARE.", "EAT BEFORE YOU GO. DOCTOR'S ORDERS. WELL, MINE.", "I SAVED YOU A PLATE." };
            default:
                return Zones.IsUnlocked(Zone.Airport)
                    ? new[] { RadioProgress(), "QUARTERMASTER'S OPEN. MAKE IT QUICK.", RadioProgress(), "COUNT YOUR ROUNDS. THEN COUNT THEM AGAIN." }
                    : new[] { "QUARTERMASTER'S OPEN. MAKE IT QUICK.", "POWDER WINS WARS, SOLDIER.", "COUNT YOUR ROUNDS. THEN COUNT THEM AGAIN." };
        }
    }

    // The infected cook, worse the sicker they are.
    private static string[] SickLines()
    {
        switch (CookSickness)
        {
            case 1: return new[] { "*COUGH* I'M FINE. IT'S JUST A SCRATCH.", "THE DOC SAYS I'LL BE ALRIGHT. *COUGH*", "I USED TO RUN THE NOODLE BAR AT THE MALL." };
            case 2: return new[] { "*COUGH* *COUGH* IT'S... GETTING HARDER.", "I CAN'T STOP SHAKING.", "TELL THE DOC TO HURRY. PLEASE." };
            default: return new[] { "*WHEEZE*... STILL HERE...", "...COLD... SO COLD...", "DON'T... LET ME TURN..." };
        }
    }

    // Said now and then while they're near but the player isn't talking to them.
    public static string[] Idle(NpcKind kind)
    {
        switch (kind)
        {
            case NpcKind.Mechanic:
                return new[] { "WHERE'D I PUT THAT WRENCH...", "THIS GENERATOR IS HELD TOGETHER WITH TAPE.", "*HUMMING*",
                    StoryItems.Complete(NpcKind.Mechanic) ? "SHE PURRS. WELL, SHE GROWLS." : "COME ON, OLD GIRL. NOT LONG NOW." };
            case NpcKind.Medic:
                return new[] { "STERILE. MOSTLY.", "I USED TO HAVE A CLINIC, YOU KNOW.", "SOMEONE KEEPS STEALING THE GAUZE." };
            case NpcKind.Trader:
                return new[] { "EVERYTHING HAS A PRICE.", "*COUNTING SCRAP*", "NO REFUNDS. NO EXCEPTIONS." };
            case NpcKind.Cook:
                return CookSick ? new[] { "*COUGH*", "*COUGH* *COUGH*", "*SHIVERING*" } : new[] { "*HUMMING*", "NEEDS MORE SALT.", "WHO ATE MY LAST ONION?" };
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
            case NpcKind.Cook: return new[] { "ON THE MENU FROM NOW ON.", "EAT UP. YOU'LL NEED IT.", "COMPLIMENTS TO THE CHEF? YOU'RE WELCOME." };
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
            case HideoutStation.TruckBay: return "TRUCK BAY";
            case HideoutStation.Kitchen: return "KITCHEN";
            default: return "AMMO PRESS";
        }
    }

    // The NPC who has to be rescued before it can be used; null for the generator, the intel desk
    // and the truck bay, which are there from the start.
    public static NpcKind? Owner(HideoutStation station)
    {
        switch (station)
        {
            case HideoutStation.Workbench: return NpcKind.Mechanic;
            case HideoutStation.MedStation: return NpcKind.Medic;
            case HideoutStation.AmmoPress: return NpcKind.Quartermaster;
            case HideoutStation.TradePost: return NpcKind.Trader;
            case HideoutStation.Kitchen: return NpcKind.Cook;
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
            case HideoutStation.TruckBay: return new MetaUpgrade[0];
            case HideoutStation.Kitchen: return new[] { MetaUpgrade.HeartyStew, MetaUpgrade.StrongCoffee, MetaUpgrade.BoneBroth };
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
            case HideoutStation.TruckBay: return new Color(1f, 0.72f, 0.25f);
            case HideoutStation.Kitchen: return new Color(1f, 0.55f, 0.3f);
            default: return new Color(0.85f, 1f, 0.35f);
        }
    }

    // Its owner rescued (and for the kitchen, cured too).
    public static bool Unlocked(HideoutStation station)
    {
        NpcKind? owner = Owner(station);

        if (owner == NpcKind.Cook && Npcs.CookSick)
            return false;

        return owner == null || MetaProgress.IsRescued(owner.Value);
    }
}

// What the trader sells and for how much scrap. The stock is a few consumables with a few of
// each, re-rolled once per run (coming back to the hideout without having started a run keeps
// the same stock, sold-out items included); bought ones go straight into the player's pack (or
// hands, for a gun) for the next run. Until the player deploys they're unsettled: leaving the
// hideout any other way (menu, quitting) refunds them, scrap and stock, so nothing paid for is
// ever lost.
// The trade goods brought home from the Shopping Mall (StoryItems) widen it, one step each:
// one more of everything, a fifth kind of consumable, boxes of ammo, one more of everything
// again, and with all five, attachments and a gun.
public static class TraderStock
{
    public enum Kind
    {
        Consumable,
        Ammo,
        Attachment,
        Gun
    }

    public struct Offer
    {
        public Kind Kind;
        public ConsumableType Type;
        public AmmoType Ammo;
        public int Rounds;                      // per box, for ammo
        public WeaponAttachment Attachment;
        public GameObject Gun;                  // the gun's prefab
        public int Price;
        public int Left;
    }

    private const int AttachmentPrice = 45;
    private const int GunPrice = 90;

    private static Offer[] offers = new Offer[0];
    private static int stockedAtRun = -1;
    private static int stockedWithGoods = -1;
    private static int[] unsettled = new int[0];   // bought since the last deployment, per offer

    public static Offer[] Offers => offers;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        offers = new Offer[0];
        stockedAtRun = -1;
        stockedWithGoods = -1;
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

    public static int Price(AmmoType type) => type == AmmoType.NineMil ? 12 : 18;

    // How many kinds of consumable are on sale: four, five once the bottled water is home.
    private static int ConsumableKinds(int goods) => goods >= 2 ? 5 : 4;

    // Extra of each consumable: one more with the tinned food, another with the tools.
    private static int ExtraAmount(int goods) => (goods >= 1 ? 1 : 0) + (goods >= 4 ? 1 : 0);

    // A fresh stock, scaled by the trade goods brought home. Does nothing if the stock is already
    // this run's (and nothing's been brought home since). `attachments` and `guns` are what the
    // trader can get hold of once the safe's home.
    public static void Restock(System.Random rng, WeaponAttachment[] attachments, GameObject[] guns)
    {
        int goods = StoryItems.TradeGoodsRecovered;

        if (stockedAtRun == MetaProgress.Runs && stockedWithGoods == goods && offers.Length > 0)
            return;

        stockedAtRun = MetaProgress.Runs;
        stockedWithGoods = goods;
        List<Offer> stock = new List<Offer>();

        ConsumableType[] all = (ConsumableType[])Enum.GetValues(typeof(ConsumableType));
        Shuffle(all, rng);
        int kinds = Mathf.Min(ConsumableKinds(goods), all.Length);
        ConsumableType[] picked = new ConsumableType[kinds];
        Array.Copy(all, picked, kinds);
        Array.Sort(picked);

        foreach (ConsumableType type in picked)
        {
            int amount = (type == ConsumableType.Bottle ? 3 + rng.Next(3) : 1 + rng.Next(3)) + ExtraAmount(goods);
            stock.Add(new Offer { Kind = Kind.Consumable, Type = type, Price = Price(type), Left = amount });
        }

        // Camping gear: two kinds of ammo by the box.
        if (goods >= 3)
        {
            AmmoType[] ammo = (AmmoType[])AmmoInfo.All.Clone();
            Shuffle(ammo, rng);

            for (int i = 0; i < Mathf.Min(2, ammo.Length); i++)
            {
                AmmoType type = ammo[i];
                stock.Add(new Offer { Kind = Kind.Ammo, Ammo = type, Rounds = AmmoInfo.DropAmount(type).y * 2, Price = Price(type), Left = 2 + rng.Next(2) });
            }
        }

        // The safe: two attachments and a gun.
        if (goods >= 5)
        {
            List<WeaponAttachment> pool = new List<WeaponAttachment>();

            if (attachments != null)
            {
                foreach (WeaponAttachment attachment in attachments)
                {
                    if (attachment != null && attachment.weight > 0f)
                        pool.Add(attachment);
                }
            }

            for (int i = 0; i < 2 && pool.Count > 0; i++)
            {
                int index = rng.Next(pool.Count);
                stock.Add(new Offer { Kind = Kind.Attachment, Attachment = pool[index], Price = AttachmentPrice, Left = 1 });
                pool.RemoveAt(index);
            }

            List<GameObject> armoury = new List<GameObject>();

            if (guns != null)
            {
                foreach (GameObject gun in guns)
                {
                    if (gun != null && gun.GetComponent<Gun>() != null)
                        armoury.Add(gun);
                }
            }

            if (armoury.Count > 0)
                stock.Add(new Offer { Kind = Kind.Gun, Gun = armoury[rng.Next(armoury.Count)], Price = GunPrice, Left = 1 });
        }

        offers = stock.ToArray();
        unsettled = new int[offers.Length];
    }

    private static void Shuffle<T>(T[] items, System.Random rng)
    {
        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    public static string Name(Offer offer)
    {
        switch (offer.Kind)
        {
            case Kind.Ammo: return $"{AmmoInfo.Name(offer.Ammo)} x{offer.Rounds}";
            case Kind.Attachment: return offer.Attachment.name.ToUpperInvariant();
            case Kind.Gun: return Gun.DisplayName(offer.Gun);
            default: return ConsumableInfo.Name(offer.Type);
        }
    }

    public static Color Colour(Offer offer)
    {
        switch (offer.Kind)
        {
            case Kind.Ammo: return AmmoInfo.Colour(offer.Ammo);
            case Kind.Attachment: return offer.Attachment.colour;
            case Kind.Gun: return UiColours.Amber;
            default: return ConsumableInfo.Colour(offer.Type);
        }
    }

    public static string Describe(Offer offer)
    {
        switch (offer.Kind)
        {
            case Kind.Ammo: return "A BOX OF ROUNDS, STRAIGHT INTO YOUR PACK.";
            case Kind.Attachment: return offer.Attachment.Describe();
            case Kind.Gun: return "STRAIGHT INTO YOUR HANDS, WITH A SPARE MAG. WHATEVER WAS IN ITS SLOT GOES ON THE FLOOR.";
            default: return ConsumableInfo.Describe(offer.Type);
        }
    }

    // How many the player already carries, for the shop's IN PACK column (-1: not counted).
    public static int Carried(Offer offer, Backpack backpack)
    {
        if (backpack == null)
            return 0;

        switch (offer.Kind)
        {
            case Kind.Ammo: return backpack.AmmoCount(offer.Ammo);
            case Kind.Consumable: return backpack.ConsumableCount(offer.Type);
            default: return -1;
        }
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

        if (backpack == null)
            return "NO ROOM IN YOUR PACK";

        switch (offer.Kind)
        {
            case Kind.Consumable when backpack.RoomFor(offer.Type) <= 0:
            case Kind.Ammo when backpack.RoomForAmmo(offer.Ammo) < offer.Rounds:
            case Kind.Attachment when !backpack.HasRoomForAttachment:
                return "NO ROOM IN YOUR PACK";
            case Kind.Gun when backpack.GetComponent<PlayerWeapon>() == null:
                return "NO HANDS FREE";
        }

        return null;
    }

    // Pays and hands it over. False if it couldn't.
    public static bool TryBuy(int index, Backpack backpack)
    {
        if (BlockedReason(index, backpack) != null || !MetaProgress.SpendScrap(offers[index].Price))
            return false;

        Offer offer = offers[index];

        switch (offer.Kind)
        {
            case Kind.Ammo:
                backpack.AddAmmo(offer.Ammo, offer.Rounds, true);
                break;
            case Kind.Attachment:
                backpack.AddAttachment(offer.Attachment);
                break;
            case Kind.Gun:
                Gun gun = backpack.GetComponent<PlayerWeapon>().Give(offer.Gun);

                // With a spare magazine's worth, as the armory gives.
                if (gun != null)
                    backpack.AddAmmo(gun.AmmoType, gun.GetMagazineSize(), true);
                break;
            default:
                backpack.AddConsumable(offer.Type, 1, true);
                break;
        }

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
