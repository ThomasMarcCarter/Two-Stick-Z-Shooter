using System;
using System.Collections.Generic;
using UnityEngine;

// Four quick-use slots, each set to a kind of consumable, used straight from the backpack's
// stacks of it without opening the inventory. Keyboard: 3-6 use a slot, Q uses the selected one.
// Controller: D-pad left / right selects, RB uses. Slots are set by dragging consumables onto
// them in the inventory; a kind picked up for the first time goes in the first empty slot.
// Medkits and adrenaline take a moment to use, slowing the player and stopping them shooting,
// and a hit interrupts them (nothing is used up). Thrown things fly at the cursor, out to a
// maximum range. Added to the player by PlayerWeapon.
public class QuickBar : MonoBehaviour
{
    public const int SlotCount = 4;

    [Header("Throwing")]
    [SerializeField] private float maxThrow = 10f;
    [SerializeField] private float minThrow = 1.5f;
    [SerializeField] private float throwCooldown = 0.6f;

    [Header("Using")]
    [Tooltip("Speed multiplier while using a medkit or adrenaline.")]
    [SerializeField, Range(0f, 1f)] private float useSpeedMultiplier = 0.55f;

    private readonly ConsumableType?[] slots = new ConsumableType?[SlotCount];
    private readonly Dictionary<ConsumableType, int> lastCounts = new Dictionary<ConsumableType, int>();

    private Backpack backpack;
    private PlayerHealth health;
    private PlayerStamina stamina;
    private Camera mainCamera;
    private float nextThrow;

    private bool usingItem;
    private ConsumableType usingType;
    private float useStart;
    private float useEnd;

    public event Action Changed;

    public int Selected { get; private set; }
    public bool IsUsing => usingItem;
    public ConsumableType UsingType => usingType;
    public float UseProgress => usingItem ? Mathf.InverseLerp(useStart, useEnd, Time.time) : 0f;
    public float SpeedMultiplier => usingItem ? useSpeedMultiplier : 1f;

    // Slot in use, for the HUD to fill.
    public int UsingSlot { get; private set; } = -1;

    public static QuickBar For(GameObject owner)
    {
        QuickBar bar = owner.GetComponent<QuickBar>();
        return bar != null ? bar : owner.AddComponent<QuickBar>();
    }

    public ConsumableType? this[int slot] => slot >= 0 && slot < SlotCount ? slots[slot] : null;

    public int Count(int slot)
    {
        return this[slot] is ConsumableType type && backpack != null ? backpack.ConsumableCount(type) : 0;
    }

    private void Awake()
    {
        backpack = Backpack.For(gameObject);
        health = GetComponent<PlayerHealth>();
        stamina = GetComponent<PlayerStamina>();
        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        backpack.Changed += OnBackpackChanged;

        if (health != null)
            health.Damaged += Interrupt;
    }

    private void OnDisable()
    {
        backpack.Changed -= OnBackpackChanged;

        if (health != null)
            health.Damaged -= Interrupt;

        usingItem = false;
        UsingSlot = -1;
    }

    // ---------- Slots ----------

    // Sets a slot to a kind. If another slot already had that kind, the two swap.
    public void Assign(int slot, ConsumableType type)
    {
        if (slot < 0 || slot >= SlotCount)
            return;

        for (int i = 0; i < SlotCount; i++)
        {
            if (i != slot && slots[i] == type)
                slots[i] = slots[slot];
        }

        slots[slot] = type;
        Changed?.Invoke();
    }

    public void Clear(int slot)
    {
        if (slot < 0 || slot >= SlotCount || slots[slot] == null)
            return;

        slots[slot] = null;
        Changed?.Invoke();
    }

    public void Swap(int a, int b)
    {
        if (a < 0 || b < 0 || a >= SlotCount || b >= SlotCount || a == b)
            return;

        (slots[a], slots[b]) = (slots[b], slots[a]);
        Changed?.Invoke();
    }

    // A kind the player didn't have before goes in the first empty slot (if it isn't on the
    // bar already), so new finds are ready to use without a trip to the inventory.
    private void OnBackpackChanged()
    {
        foreach (ConsumableType type in ConsumableInfo.All)
        {
            int count = backpack.ConsumableCount(type);
            lastCounts.TryGetValue(type, out int before);
            lastCounts[type] = count;

            if (before > 0 || count <= 0 || Array.IndexOf(slots, (ConsumableType?)type) >= 0)
                continue;

            int empty = Array.IndexOf(slots, null);

            if (empty >= 0)
            {
                slots[empty] = type;
                Changed?.Invoke();
            }
        }
    }

    // ---------- Using ----------

    private void Update()
    {
        if (usingItem && Time.time >= useEnd)
            Finish();

        if (GameFlow.InputBlocked || (health != null && health.IsDead))
            return;

        for (int i = 0; i < SlotCount; i++)
        {
            if (GameInput.QuickSlots[i].WasPressedThisFrame())
            {
                Select(i);
                Use(i);
            }
        }

        if (GameInput.QuickNext.WasPressedThisFrame())
            Select((Selected + 1) % SlotCount);

        if (GameInput.QuickPrevious.WasPressedThisFrame())
            Select((Selected + SlotCount - 1) % SlotCount);

        if (GameInput.QuickUse.WasPressedThisFrame())
            Use(Selected);
    }

    private void Select(int slot)
    {
        if (Selected == slot)
            return;

        Selected = slot;
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("inventory-lift", 2, i => ProceduralAudio.BackpackRustle(300 + i))), 0.12f, 1.5f);
        Changed?.Invoke();
    }

    public void Use(int slot)
    {
        if (usingItem)
            return;

        if (!(this[slot] is ConsumableType type) || backpack.ConsumableCount(type) <= 0)
        {
            Deny();
            return;
        }

        if (ConsumableInfo.IsMedkit(type) && health != null && health.IsFull)
        {
            Deny();
            return;
        }

        if (ConsumableInfo.IsThrown(type))
        {
            if (Time.time >= nextThrow)
                Throw(type);

            return;
        }

        usingItem = true;
        usingType = type;
        UsingSlot = slot;
        useStart = Time.time;
        useEnd = Time.time + ConsumableInfo.UseTime(type);
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("inventory-lift", 2, i => ProceduralAudio.BackpackRustle(300 + i))), 0.35f, 0.9f);
    }

    private void Finish()
    {
        usingItem = false;
        UsingSlot = -1;

        // Healed up some other way in the meantime: keep the medkit.
        if (ConsumableInfo.IsMedkit(usingType) && health != null && health.IsFull)
            return;

        if (!backpack.TakeConsumable(usingType))
            return;

        if (ConsumableInfo.IsMedkit(usingType))
        {
            health.Heal(ConsumableInfo.Heal(usingType));
            SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("medkit", 1, i => ProceduralAudio.MedkitPickup())), 0.6f);
        }
        else if (usingType == ConsumableType.Adrenaline && stamina != null)
        {
            stamina.Boost(ConsumableInfo.AdrenalineTime);
            SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("adrenaline", 1, i => ProceduralAudio.Inject())), 0.6f);
        }
    }

    // A hit spoils whatever was being used; nothing is used up.
    private void Interrupt(PlayerHealth _)
    {
        if (!usingItem)
            return;

        usingItem = false;
        UsingSlot = -1;
        Deny();
    }

    private void Throw(ConsumableType type)
    {
        Vector3 from = transform.position + Vector3.up * 1.2f + transform.forward * 0.4f;
        Vector3 target = AimPoint();
        Vector3 flat = target - transform.position;
        flat.y = 0f;

        if (flat.sqrMagnitude < 0.01f)
            flat = transform.forward;

        float distance = Mathf.Clamp(flat.magnitude, minThrow, maxThrow);
        target = transform.position + flat.normalized * distance;
        target.y = 0f;

        if (!backpack.TakeConsumable(type))
            return;

        nextThrow = Time.time + throwCooldown;
        Thrown.Launch(type, from, target, gameObject);
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("throw", 2, ProceduralAudio.Throw)), 0.45f, UnityEngine.Random.Range(0.92f, 1.08f));
    }

    // The point on the floor under the cursor, like the aim.
    private Vector3 AimPoint()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        Ray ray = mainCamera.ScreenPointToRay(GameInput.Pointer);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        return ground.Raycast(ray, out float distance) ? ray.GetPoint(distance) : transform.position + transform.forward * maxThrow;
    }

    private static void Deny()
    {
        SoundFX.Play2D(SoundFX.Pick(SoundFX.Clips("attachment-deny", 1, i => ProceduralAudio.AttachmentDeny())), 0.35f);
    }
}
