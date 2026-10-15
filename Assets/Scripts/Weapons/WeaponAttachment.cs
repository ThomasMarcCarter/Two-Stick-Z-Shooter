using System;
using System.Collections.Generic;
using UnityEngine;

// Which part of a gun an attachment goes on. A gun takes one attachment per slot; fitting a
// second to the same slot swaps the old one off.
public enum AttachmentSlot
{
    Muzzle,     // suppressor, choke
    Barrel,     // long barrel, match barrel
    Magazine,   // extended, drum, quick-release
    Trigger,    // hair trigger, auto sear
    Rail        // laser sight
}

// A weapon attachment: changes to a gun's stats while it's fitted. Multipliers of 1 (and
// bonuses of 0) leave that stat alone. The list of attachments and their numbers lives on the
// FloorGenerator (Attachments), filled with the defaults below; found in treasure rooms and
// dropped by bosses as AttachmentPickups. Stays on the gun when it's dropped and picked up again.
[Serializable]
public class WeaponAttachment
{
    // Which guns it can go on.
    public enum Fit
    {
        Any,
        SpreadGuns,     // fires several pellets (shotguns)
        SingleShot,     // fires one round a shot (not shotguns)
        SemiAuto,       // semi-automatic guns only
        Magazine        // guns that reload a whole magazine, not shell by shell
    }

    public string name = "Attachment";
    [Tooltip("Shown in the inventory's MODS line, where space is short.")]
    public string shortName = "ATT";
    public AttachmentSlot slot;
    public Fit fits = Fit.Any;
    [Tooltip("Colour of its pickup's glow and label stripe.")]
    public Color colour = Color.white;

    [Header("Stats")]
    public float magazineMultiplier = 1f;
    public float fireRateMultiplier = 1f;
    [Tooltip("Below 1 is a faster reload.")]
    public float reloadTimeMultiplier = 1f;
    [Tooltip("Below 1 is a tighter shotgun spread.")]
    public float spreadMultiplier = 1f;
    [Tooltip("Below 1 is more accurate: shots stray less from the aim (any gun).")]
    public float accuracyMultiplier = 1f;
    public float projectileSpeedMultiplier = 1f;
    [Min(0)] public int bonusDamage;
    [Tooltip("How far gunshots are heard (by enemies) and how loud they sound, as a multiple.")]
    public float loudnessMultiplier = 1f;
    [Tooltip("Makes a semi-automatic gun fire while the trigger is held.")]
    public bool fullAuto;
    public bool hidesMuzzleFlash;
    [Tooltip("Shows a laser line from the muzzle to whatever it's pointed at.")]
    public bool laser;

    [Header("Drops")]
    [Tooltip("Relative odds against the other attachments.")]
    [Min(0f)] public float weight = 1f;
    [Tooltip("Can't drop before this floor.")]
    [Min(1)] public int minFloor = 1;

    public static string SlotName(AttachmentSlot slot)
    {
        return slot.ToString().ToUpperInvariant();
    }

    public bool Fits(Gun gun)
    {
        if (gun == null)
            return false;

        switch (fits)
        {
            case Fit.SpreadGuns: return gun.PelletsPerShot > 1;
            case Fit.SingleShot: return gun.PelletsPerShot <= 1;
            case Fit.SemiAuto: return gun.BaseFireMode == Gun.FireMode.Semi;
            case Fit.Magazine: return gun.Reloading == Gun.ReloadStyle.Magazine;
            default: return true;
        }
    }

    public string FitDescription()
    {
        switch (fits)
        {
            case Fit.SpreadGuns: return "SHOTGUNS ONLY";
            case Fit.SingleShot: return "NOT FOR SHOTGUNS";
            case Fit.SemiAuto: return "SEMI-AUTO ONLY";
            case Fit.Magazine: return "MAGAZINE-FED ONLY";
            default: return "FITS ANY GUN";
        }
    }

    // What it does, worked out from its numbers: "MAG +50%  RELOAD +10%".
    public string Describe()
    {
        List<string> parts = new List<string>();
        Percent(parts, "MAG", magazineMultiplier);
        Percent(parts, "FIRE RATE", fireRateMultiplier);
        Percent(parts, "RELOAD TIME", reloadTimeMultiplier);
        Percent(parts, "AIM SPREAD", accuracyMultiplier);
        Percent(parts, "PELLET SPREAD", spreadMultiplier);
        Percent(parts, "BULLET SPEED", projectileSpeedMultiplier);

        if (bonusDamage > 0)
            parts.Add("DAMAGE +" + bonusDamage);

        Percent(parts, "NOISE", loudnessMultiplier);

        if (fullAuto)
            parts.Add("FULL AUTO");

        if (hidesMuzzleFlash)
            parts.Add("NO FLASH");

        if (laser)
            parts.Add("LASER");

        return string.Join("  ", parts);
    }

    private static void Percent(List<string> parts, string label, float multiplier)
    {
        int percent = Mathf.RoundToInt((multiplier - 1f) * 100f);

        if (percent != 0)
            parts.Add($"{label} {(percent > 0 ? "+" : "")}{percent}%");
    }

    // The starting list on the FloorGenerator, tweakable there.
    public static WeaponAttachment[] Defaults()
    {
        return new[]
        {
            new WeaponAttachment
            {
                name = "Suppressor", shortName = "SUP", slot = AttachmentSlot.Muzzle,
                colour = new Color(0.55f, 0.75f, 1f),
                loudnessMultiplier = 0.35f, hidesMuzzleFlash = true, projectileSpeedMultiplier = 0.9f
            },
            new WeaponAttachment
            {
                name = "Choke", shortName = "CHK", slot = AttachmentSlot.Muzzle, fits = Fit.SpreadGuns,
                colour = new Color(1f, 0.6f, 0.3f),
                spreadMultiplier = 0.55f
            },
            new WeaponAttachment
            {
                name = "Long Barrel", shortName = "LNG", slot = AttachmentSlot.Barrel,
                colour = new Color(0.9f, 0.9f, 0.5f),
                projectileSpeedMultiplier = 1.4f, spreadMultiplier = 0.75f, accuracyMultiplier = 0.85f
            },
            new WeaponAttachment
            {
                name = "Match Barrel", shortName = "MCH", slot = AttachmentSlot.Barrel,
                colour = new Color(1f, 0.35f, 0.35f),
                bonusDamage = 1, projectileSpeedMultiplier = 1.15f, fireRateMultiplier = 0.9f,
                weight = 0.6f, minFloor = 2
            },
            new WeaponAttachment
            {
                name = "Extended Mag", shortName = "EXT", slot = AttachmentSlot.Magazine,
                colour = new Color(0.4f, 1f, 0.5f),
                magazineMultiplier = 1.5f, reloadTimeMultiplier = 1.1f
            },
            new WeaponAttachment
            {
                name = "Drum Mag", shortName = "DRM", slot = AttachmentSlot.Magazine, fits = Fit.Magazine,
                colour = new Color(0.2f, 0.85f, 0.35f),
                magazineMultiplier = 2f, reloadTimeMultiplier = 1.4f, fireRateMultiplier = 0.95f,
                weight = 0.7f
            },
            new WeaponAttachment
            {
                name = "Quick Mag", shortName = "QCK", slot = AttachmentSlot.Magazine,
                colour = new Color(0.5f, 1f, 0.9f),
                reloadTimeMultiplier = 0.6f
            },
            new WeaponAttachment
            {
                name = "Hair Trigger", shortName = "HTR", slot = AttachmentSlot.Trigger,
                colour = new Color(1f, 0.85f, 0.3f),
                fireRateMultiplier = 1.3f
            },
            new WeaponAttachment
            {
                name = "Auto Sear", shortName = "AUT", slot = AttachmentSlot.Trigger, fits = Fit.SemiAuto,
                colour = new Color(1f, 0.5f, 0.15f),
                fullAuto = true, fireRateMultiplier = 1.1f,
                weight = 0.7f
            },
            new WeaponAttachment
            {
                name = "Laser Sight", shortName = "LSR", slot = AttachmentSlot.Rail,
                colour = new Color(1f, 0.2f, 0.25f),
                laser = true, spreadMultiplier = 0.85f, accuracyMultiplier = 0.45f
            },
        };
    }
}
