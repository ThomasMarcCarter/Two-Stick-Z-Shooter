using System;
using System.Collections.Generic;
using UnityEngine;

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
