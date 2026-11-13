using System.Collections.Generic;
using UnityEngine;

// The FloorGenerator's side of the special rooms (RoomType Shop, Cursed, Blood, Secret): what goes
// in each once it is built. They hold no enemies, so their doors never lock. The shop gets a
// counter with its stock; the cursed room is a case of cursed items behind red doorways (the
// neighbouring side of each doorway goes red too) and the blood bank a case bought with max health;
// the secret room's cracked wall and contents are in FloorGenerator.Secret.cs.
public partial class FloorGenerator
{
    private const int RunItemPrice = 25;
    private const int MedkitPrice = 8;
    private const int LargeMedkitPrice = 14;
    private const int ThrowablePrice = 6;
    private const int AmmoPrice = 5;
    private const int AttachmentPrice = 12;
    private const int PlatePrice = 6;

    private void FillSpecialRoom(Room room)
    {
        switch (room.Type)
        {
            case RoomType.Shop: FillShop(room); break;
            case RoomType.Cursed: FillCursed(room); break;
            case RoomType.Blood: FillBloodBank(room); break;
            case RoomType.Secret: FillSecret(room); break;
            default: return;
        }

        room.DisplayName = Room.TypeName(room.Type);
    }

    // Red doorways, 1 health taken on the first entry (never the last, plates don't help), and a
    // case of 2 cursed items in the middle.
    private void FillCursed(Room room)
    {
        // The doorway's other side (in the room or corridor beyond) sits at the same spot, so warn there too.
        List<DoorBeacon> all = new List<DoorBeacon>();

        foreach (Room other in rooms)
            all.AddRange(other.GetComponentsInChildren<DoorBeacon>());

        foreach (DoorBeacon beacon in room.GetComponentsInChildren<DoorBeacon>())
        {
            foreach (DoorBeacon twin in all)
            {
                Vector3 gap = twin.transform.position - beacon.transform.position;
                gap.y = 0f;

                if (gap.sqrMagnitude < 1.5f * 1.5f)
                    twin.Tint(RunItems.CursedColour);
            }
        }

        bool paid = false;
        room.PlayerEntered += _ =>
        {
            if (paid || player == null)
                return;

            paid = true;
            PlayerHealth health = player.GetComponent<PlayerHealth>();

            if (health != null)
                health.Drain(1, 1);
        };

        SpawnSpecialCase(room, PickOffers(RunItems.Cursed, 2, false), PowerupCase.Mode.Cursed);
    }

    // A dim red machine (the hospital bed monitor's sprite) with a red light and its case beside it:
    // 2 normal unlocked items and 1 cursed, each bought with max health.
    private void FillBloodBank(Room room)
    {
        Vector3 middle = room.transform.position;
        SpawnFurniture(room, BedMonitorPiece, middle, Vector3.back, false);

        GameObject lamp = new GameObject("Blood Bank Light");
        lamp.transform.SetParent(room.transform, true);
        lamp.transform.position = middle + Vector3.up * 1.4f;
        Light light = lamp.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = RunItems.CursedColour;
        light.range = 5f;
        light.intensity = 1.8f;
        light.shadows = LightShadows.None;

        List<RunItem> offers = PickOffers(RunItems.Normal, 2, true);
        offers.AddRange(PickOffers(RunItems.Cursed, 1, false));
        SpawnSpecialCase(room, offers, PowerupCase.Mode.Blood);
    }

    // Up to `count` run items from `from` that the player doesn't hold and no other case this floor
    // has offered (only unlocked ones if `gated`; cursed items aren't gated). Fewer if the pool runs dry.
    private List<RunItem> PickOffers(RunItem[] from, int count, bool gated)
    {
        ResetFloorOffers();
        List<RunItem> pool = new List<RunItem>();

        foreach (RunItem item in from)
        {
            if (!PlayerItems.Has(item) && !floorOffers.Contains(item) && (!gated || Unlocks.IsUnlocked(item)))
                pool.Add(item);
        }

        List<RunItem> picked = new List<RunItem>();

        while (picked.Count < count && pool.Count > 0)
        {
            int pick = rng.Next(pool.Count);
            picked.Add(pool[pick]);
            floorOffers.Add(pool[pick]);
            pool.RemoveAt(pick);
        }

        return picked;
    }

    // Never an empty case.
    private void SpawnSpecialCase(Room room, List<RunItem> offers, PowerupCase.Mode mode)
    {
        if (offers.Count == 0)
            return;

        Vector3 middle = room.transform.position;
        PowerupCase.Spawn(offers, PowerupCaseSpot(room, middle + Vector3.back * 1.5f, middle), room.transform, mode);
    }

    // A counter in the middle of the room, facing the door end, with the shop's fixed stock.
    private void FillShop(Room room)
    {
        ShopCounter counter = ShopCounter.Create(room.transform.position, Vector3.back, room.transform);

        // One run item from the unlocked pool (never one held, never cursed).
        List<RunItem> pool = new List<RunItem>();

        foreach (RunItem item in RunItems.Normal)
        {
            if (!PlayerItems.Has(item) && Unlocks.IsUnlocked(item))
                pool.Add(item);
        }

        if (pool.Count > 0)
            counter.Add(ShopEntry.ForRun(pool[rng.Next(pool.Count)], RunItemPrice));

        counter.Add(ShopEntry.ForItem(CrateItem.ForConsumable(ConsumableType.Medkit, 1), MedkitPrice));
        counter.Add(ShopEntry.ForItem(CrateItem.ForConsumable(ConsumableType.LargeMedkit, 1), LargeMedkitPrice));
        counter.Add(ShopEntry.ForItem(CrateItem.ForConsumable(rng.NextDouble() < 0.5 ? ConsumableType.Grenade : ConsumableType.Molotov, 1), ThrowablePrice));

        List<AmmoType> held = HeldAmmoTypes();

        if (held.Count > 0)
        {
            AmmoType type = held[rng.Next(held.Count)];
            counter.Add(ShopEntry.ForItem(CrateItem.ForAmmo(type, DropAmount(type, 1f, 0.5f)), AmmoPrice));
        }

        foreach (WeaponAttachment attachment in PickAttachments(1, () => (float)rng.NextDouble()))
            counter.Add(ShopEntry.ForItem(CrateItem.ForAttachment(attachment), AttachmentPrice));

        if (PlayerItems.Has(RunItem.PlateCarrier))
            counter.Add(ShopEntry.ForItem(CrateItem.ForArmorPlate(), PlatePrice));
    }
}
