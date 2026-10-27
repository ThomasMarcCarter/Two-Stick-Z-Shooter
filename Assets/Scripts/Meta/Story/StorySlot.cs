using System;
using UnityEngine;

// The story slot: holds the run's story item (a truck part taken off the supply room's shelf)
// apart from the backpack, so it never competes with loot for space and can't be dropped. It's
// only safe once the run is won (the supply room's exit brings it home and MetaProgress keeps
// it); a death or an abandoned run loses it, and the next won run finds the same part again.
// Shown on the HUD and in the inventory screen. Added to the player when first asked for (For).
public class StorySlot : MonoBehaviour
{
    public StoryItem? Item { get; private set; }

    public bool IsEmpty => Item == null;

    public event Action Changed;

    public static StorySlot For(GameObject owner)
    {
        StorySlot slot = owner.GetComponent<StorySlot>();
        return slot != null ? slot : owner.AddComponent<StorySlot>();
    }

    // The player's, or null if there's no player.
    public static StorySlot Player()
    {
        PlayerWeapon player = PlayerWeapon.Current;
        return player != null ? For(player.gameObject) : null;
    }

    public void Put(StoryItem item)
    {
        Item = item;
        Changed?.Invoke();
    }

    // Empties the slot, returning what was in it (the run's over: banked or lost).
    public StoryItem? Take()
    {
        StoryItem? item = Item;
        Item = null;

        if (item != null)
            Changed?.Invoke();

        return item;
    }
}
