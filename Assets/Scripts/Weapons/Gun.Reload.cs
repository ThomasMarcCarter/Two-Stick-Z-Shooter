using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Gun's reloading: a magazine swapped in one go, or shells fed in one at a time (which a
// shot or a swap can cut short), taking rounds from the holder's ammo and making noise.
public partial class Gun
{
    // Fed a shell at a time (the reload animation nods once per shell, GunSprite).
    public bool ReloadsPerShell => reloadStyle == ReloadStyle.PerShell;

    // Shells this reload is feeding in (per-shell guns; 1 for a magazine).
    public int ReloadShellCount { get; private set; } = 1;

    public void TryReload()
    {
        if (isReloading)
            return;

        if (currentAmmo >= GetMagazineSize())
            return;

        // Nothing to reload with: just the dry click.
        if (Reserve <= 0)
        {
            DryClick();
            return;
        }

        reloadRoutine = StartCoroutine(reloadStyle == ReloadStyle.PerShell ? ReloadShells() : Reload());
    }

    // The racket of reloading, for enemies within earshot.
    private void ReloadNoise(bool quiet)
    {
        Noise.Emit(transform.position, quiet ? Noise.QuietReload : Noise.Reload, Noise.Kind.Reload);
    }

    private void CancelReload()
    {
        if (reloadRoutine != null)
            StopCoroutine(reloadRoutine);

        reloadRoutine = null;
        isReloading = false;
        ReloadProgress = 0f;
    }

    // Loads one round at a time until full or out of spare rounds, taking each from the
    // inventory as it goes in. Progress covers the whole reload, so the reload ring fills once
    // from empty to full rather than once per shell.
    private IEnumerator ReloadShells()
    {
        isReloading = true;
        ReloadProgress = 0f;

        int needed = Mathf.Min(GetMagazineSize() - currentAmmo, Reserve);
        ReloadShellCount = Mathf.Max(1, needed);

        for (int loaded = 0; loaded < needed; loaded++)
        {
            // Checked shell by shell, so starting or stopping sneaking mid-reload takes effect
            // from the next one.
            bool quiet = Sneaking;
            float shellTime = ShellReloadTime * (quiet ? sneakReloadTimeMultiplier : 1f);
            float elapsed = 0f;

            while (elapsed < shellTime)
            {
                elapsed += Time.deltaTime;
                ReloadProgress = Mathf.Clamp01((loaded + elapsed / shellTime) / needed);

                yield return null;
            }

            if (Inventory.Take(ammoType, 1) == 0)
                break;

            currentAmmo++;

            if (shellInsertClip != null)
                reloadAudio.PlayOneShot(shellInsertClip, quiet ? sneakReloadVolume : 1f);

            ReloadNoise(quiet);
        }

        ReloadProgress = 1f;
        isReloading = false;
        reloadRoutine = null;
    }

    private IEnumerator Reload()
    {
        isReloading = true;
        ReloadProgress = 0f;

        // Decided at the start: a sneaking reload is slower and quieter throughout.
        bool quiet = Sneaking;
        float duration = ReloadTime * (quiet ? sneakReloadTimeMultiplier : 1f);

        // The sound is stretched to fit, so a slow reload sounds slower too.
        reloadAudio.pitch = reloadAudio.clip.length / duration;
        reloadAudio.volume = reloadVolume * (quiet ? sneakReloadVolume : 1f);
        reloadAudio.Play();
        ReloadNoise(quiet);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ReloadProgress = Mathf.Clamp01(elapsed / duration);

            yield return null;
        }

        // Only what's carried goes in; taken now, so a cancelled reload costs nothing.
        currentAmmo += Inventory.Take(ammoType, GetMagazineSize() - currentAmmo);

        // The magazine seating.
        ReloadNoise(quiet);
        ReloadProgress = 1f;
        isReloading = false;
        reloadRoutine = null;
    }
}
