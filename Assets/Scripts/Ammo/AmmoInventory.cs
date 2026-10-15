using System;
using UnityEngine;

// The player's spare ammo, per type. Guns reload from here and pickups add to it. The rounds
// themselves are carried as stacks in the Backpack, so how much fits depends on the backpack's
// free cells rather than a limit per type. Plays a pickup sound whenever ammo is added.
public class AmmoInventory : MonoBehaviour
{
    [Serializable]
    private struct StartingAmmo
    {
        public AmmoType Type;
        public int Amount;
    }

    [SerializeField] private StartingAmmo[] startingAmmo =
    {
        new StartingAmmo { Type = AmmoType.NineMil, Amount = 24 }
    };

    [Header("Pickup Sound")]
    [Tooltip("Played when ammo is picked up. Leave empty to use generated cartridge clinks.")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.5f;
    [Tooltip("Shortest gap between pickup sounds, so standing on a crate while firing doesn't rattle every frame.")]
    [SerializeField] private float pickupSoundCooldown = 0.12f;

    // Fired with the type and how many rounds were picked up (for the "+12 9MM" notice).
    public event Action<AmmoType, int> PickedUp;
    public event Action Changed;

    private Backpack backpack;
    private AudioSource audioSource;
    private AudioClip[] pickupClips;
    private float lastPickupSound = -1f;

    public Backpack Backpack => backpack != null ? backpack : backpack = Backpack.For(gameObject);

    private void Awake()
    {
        foreach (StartingAmmo start in startingAmmo)
            Backpack.AddAmmo(start.Type, start.Amount, true);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        // A few generated variations, so repeated pickups don't sound identical.
        pickupClips = pickupSound != null
            ? new[] { pickupSound }
            : new[] { ProceduralAudio.AmmoPickup(11), ProceduralAudio.AmmoPickup(27), ProceduralAudio.AmmoPickup(53) };
    }

    private void OnEnable()
    {
        Backpack.Changed += OnBackpackChanged;
    }

    private void OnDisable()
    {
        if (backpack != null)
            backpack.Changed -= OnBackpackChanged;
    }

    // Stacks moved, merged or dropped from the inventory change the counts too.
    private void OnBackpackChanged()
    {
        Changed?.Invoke();
    }

    public int Count(AmmoType type)
    {
        return Backpack.AmmoCount(type);
    }

    // No room for another round of this type, even in a new stack.
    public bool IsFull(AmmoType type)
    {
        return Backpack.RoomForAmmo(type) <= 0;
    }

    // Rounds that would go into stacks already carried, without starting a new one.
    public int TopUpRoom(AmmoType type)
    {
        return Backpack.TopUpRoom(type);
    }

    // Adds what fits, starting new stacks in the backpack as needed (or, with newStacks off,
    // only topping up the stacks already there). Returns how many were actually added.
    public int Add(AmmoType type, int amount, bool newStacks = true)
    {
        int added = Backpack.AddAmmo(type, amount, newStacks);

        if (added <= 0)
            return 0;

        PlayPickupSound(added);
        PickedUp?.Invoke(type, added);
        return added;
    }

    // Bigger pickups sound a little lower and heavier.
    private void PlayPickupSound(int added)
    {
        if (audioSource == null || Time.time - lastPickupSound < pickupSoundCooldown)
            return;

        lastPickupSound = Time.time;
        float weight = Mathf.InverseLerp(5f, 60f, added);
        audioSource.pitch = Mathf.Lerp(1.08f, 0.9f, weight) * UnityEngine.Random.Range(0.96f, 1.04f);
        audioSource.PlayOneShot(pickupClips[UnityEngine.Random.Range(0, pickupClips.Length)], pickupVolume);
    }

    // Removes up to the amount asked for. Returns how many were actually taken.
    public int Take(AmmoType type, int amount)
    {
        return Backpack.TakeAmmo(type, amount);
    }

    // Debug: tops up every type to a few stacks, as far as the backpack has room.
    public void FillAll()
    {
        foreach (AmmoType type in AmmoInfo.All)
        {
            int target = Backpack.StackSize(type) * 3;
            Backpack.AddAmmo(type, Mathf.Max(0, target - Count(type)), true);
        }
    }
}
