using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// What the survivors say: distress calls, lines when shot at, progress on their goals, scenes
// due in the hideout, greetings, idle chatter, and thanks when an upgrade is bought.
public static partial class Npcs
{
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
        Add(Scene.ShopStocked, StoryItems.Complete(NpcKind.Trader), NpcKind.Trader,
            "THAT'S THE SAFE IN. SHELVES ARE FULL, BACK ROOM'S OPEN. GUNS, ATTACHMENTS, THE LOT. COME AND SPEND SOMETHING.");
        Add(Scene.CureFound, StoryItems.Complete(NpcKind.Medic), NpcKind.Medic,
            "THE NOTES WORKED. ROSA'S FEVER BROKE THIS MORNING. NO MORE SIGNS. WE BEAT IT, FOR ONE OF US AT LEAST.");
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

    // Said now and then while they're near but the player isn't talking to them, plus a line
    // about the places the truck can reach (Places).
    public static string[] Idle(NpcKind kind)
    {
        string[] lines;

        switch (kind)
        {
            case NpcKind.Mechanic:
                lines = new[] { "WHERE'D I PUT THAT WRENCH...", "THIS GENERATOR IS HELD TOGETHER WITH TAPE.", "*HUMMING*",
                    StoryItems.Complete(NpcKind.Mechanic) ? "SHE PURRS. WELL, SHE GROWLS." : "COME ON, OLD GIRL. NOT LONG NOW." };
                break;
            case NpcKind.Medic:
                lines = new[] { "STERILE. MOSTLY.", "I USED TO HAVE A CLINIC, YOU KNOW.", "SOMEONE KEEPS STEALING THE GAUZE." };
                break;
            case NpcKind.Trader:
                lines = new[] { "EVERYTHING HAS A PRICE.", "*COUNTING SCRAP*", "NO REFUNDS. NO EXCEPTIONS." };
                break;
            case NpcKind.Cook:
                return CookSick ? new[] { "*COUGH*", "*COUGH* *COUGH*", "*SHIVERING*" } : new[] { "*HUMMING*", "NEEDS MORE SALT.", "WHO ATE MY LAST ONION?" };
            default:
                lines = new[] { "INVENTORY CHECK. AGAIN.", "BACK IN MY DAY WE HAD RATIONS.", "NOBODY TOUCHES MY CRATES." };
                break;
        }

        string place = Places(kind);
        return place == null ? lines : lines.Concat(new[] { place }).ToArray();
    }

    // What they think of the places the truck can reach: their own zone before it opens, while
    // their goal is under way there, and once it's done. Null when they've nothing to say yet.
    private static string Places(NpcKind kind)
    {
        bool running = StoryItems.Complete(NpcKind.Mechanic);

        switch (kind)
        {
            case NpcKind.Mechanic:
                if (!running)
                    return "ONCE SHE RUNS, THE MALL'S AN HOUR DOWN THE COAST ROAD. THE HOSPITAL AND AIRPORT BEYOND THAT.";
                return Zones.IsUnlocked(Zone.Airport) ? "THE AIRPORT ROAD'S THE WORST OF IT. CRATERS. I'LL KEEP HER TOGETHER."
                    : "THE MALL'S A STRAIGHT RUN. AFTER THAT THE ROADS GET UGLY.";
            case NpcKind.Trader:
                if (!running)
                    return "THERE'S A WHOLE MALL OUT THERE. PITY WE CAN'T DRIVE TO IT. YET.";
                return StoryItems.Complete(NpcKind.Trader) ? "I'LL MISS THAT MALL. NOT THE ZOMBIES. THE MALL."
                    : "THE MALL'S STOREROOMS ARE UNDER THE FOOD COURT. BRING ME WHAT'S DOWN THERE.";
            case NpcKind.Medic:
                if (!Zones.IsUnlocked(Zone.Hospital))
                    return running ? "I TRAINED AT THE HOSPITAL PAST THE MALL. IF IT'S STILL STANDING, IT HAS WHAT WE NEED." : null;
                return StoryItems.Complete(NpcKind.Medic) ? "I NEVER WANT TO SEE THAT HOSPITAL AGAIN."
                    : "THE HOSPITAL'S LAB IS IN THE BASEMENT. THAT'S WHERE THE GOOD STUFF WILL BE.";
            case NpcKind.Quartermaster:
                if (!Zones.IsUnlocked(Zone.Airport))
                    return running ? "THE AIRPORT'S THE END OF THE LINE. IF ANYONE'S STILL FLYING, THEY'RE LISTENING THERE." : null;
                return StoryItems.Complete(NpcKind.Quartermaster) ? "THE CHOPPER'S COMING. PACK LIGHT."
                    : "THAT TOWER HAD A FULL RADIO SUITE. STRIP IT FOR PARTS, SOLDIER.";
            default:
                return null;
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
