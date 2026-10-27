using UnityEngine;

// The CharacterAnimator's player and survivor poses: the player's arms on the gun's grips, the
// gun tipping over for reloads, and survivors' heads following who they look at, gesturing and
// cowering in a cage.
public partial class CharacterAnimator
{
    // ---------- Player ----------

    // Where each hand is, from its shoulder, in the arm's rest pose (the model's gloves).
    // Elbows bent, holding the gun at mid-torso (Tools/Blender/Characters.py).
    private static readonly Vector3 RearHand = new Vector3(-0.24f, -0.38f, 0.45f);
    private static readonly Vector3 FrontHand = new Vector3(0.24f, -0.32f, 0.72f);

    private Quaternion currentTilt = Quaternion.identity;

    // Both hands go to the gun's grips (worked out from the gun: its muzzle height and length),
    // reaching for them each frame, so every gun sits in the hands. Reloading tips the gun over
    // around the rear hand, so it stays in that hand, while the support hand goes off to the
    // belt for a magazine (or feeds shells) and comes back.
    private void AnimatePlayerArms()
    {
        Gun gun = shooting != null ? shooting.EquippedGun : null;
        Transform gunModel = gun != null ? gun.transform.Find("Model") : null;

        if (gunModel == null)
        {
            Pose(FrontArm, Quaternion.identity);
            Pose(RearArm, Quaternion.identity);
            RestoreGun(null);
            return;
        }

        Transform gunRoot = gun.transform;
        Transform muzzle = gunRoot.Find("Muzzle");
        float bore = muzzle != null ? muzzle.localPosition.y : -0.49f;
        float muzzleZ = muzzle != null ? muzzle.localPosition.z : 0.9f;
        bool shortGun = muzzleZ < 0.95f;

        // Grip points, in the gun's own space.
        Vector3 grip = new Vector3(0f, bore - 0.1f, 0.45f);
        Vector3 foregrip = shortGun
            ? new Vector3(0f, bore - 0.12f, 0.47f)                                 // cupping the grip
            : new Vector3(0f, bore - 0.04f, Mathf.Min(0.82f, muzzleZ - 0.3f));     // under the handguard

        Quaternion tilt = Quaternion.identity;
        float away = 0f;                 // 0 = support hand on the gun, 1 = at the belt
        Vector3 support = foregrip;

        if (gun.IsReloading)
        {
            float t = gun.ReloadProgress;

            if (gun.Reloading == Gun.ReloadStyle.Magazine)
            {
                float tip = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.03f, 0.92f, t)) * Mathf.PI);
                tilt = Quaternion.Euler(-10f * tip, 0f, 28f * tip);
                away = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.32f, t))
                     * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.8f, t)));
                float slap = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.8f, 0.92f, t)) * Mathf.PI);
                support = Vector3.Lerp(foregrip, grip + new Vector3(0f, -0.12f, 0.05f), 0.5f) + Vector3.up * 0.06f * slap;
            }
            else
            {
                // Tipped over; the hand moves between the shell pouch and the loading port.
                tilt = Quaternion.Euler(-6f, 0f, -26f);
                float pump = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * 1.8f);
                support = Vector3.Lerp(grip + new Vector3(0f, -0.06f, 0.15f), foregrip, 0.5f) + new Vector3(0f, -0.06f * pump, 0f);
                away = pump * 0.35f;
            }
        }

        currentTilt = Quaternion.Slerp(currentTilt, tilt, 1f - Mathf.Exp(-20f * Time.deltaTime));
        TipGunAround(gunModel, grip, currentTilt);

        // Where the grip points are now, with the tip applied.
        Vector3 gripWorld = gunRoot.TransformPoint(grip);
        Vector3 supportWorld = gunRoot.TransformPoint(grip + currentTilt * (support - grip));

        // The belt: low on the support side, by the hip.
        Vector3 belt = torso.Bone.TransformPoint(new Vector3(-0.22f, 0.02f, 0.12f));
        supportWorld = Vector3.Lerp(supportWorld, belt, away);

        Reach(RearArm, RearHand, gripWorld);
        Reach(FrontArm, FrontHand, supportWorld);
    }

    // Rotates the gun's visual by `tilt` around a point (its grip) rather than its origin, so the
    // grip stays put in the hand. The muzzle isn't moved, so shots still go where they're aimed.
    private void TipGunAround(Transform gunModel, Vector3 pivot, Quaternion tilt)
    {
        RestoreGun(gunModel);
        gunModel.localRotation = tilt;
        gunModel.localPosition = pivot - tilt * pivot;
    }

    // Puts a previously tilted gun (one that's been swapped out) back as it was.
    private void RestoreGun(Transform current)
    {
        if (tiltedGun != null && tiltedGun != current)
        {
            tiltedGun.localRotation = Quaternion.identity;
            tiltedGun.localPosition = Vector3.zero;
        }

        tiltedGun = current;
    }

    // Swings a rigid arm from its shoulder so its hand lands on a point, stretching it a little
    // if it's short (blocky arms, so it isn't noticeable).
    private static void Reach(Limb arm, Vector3 hand, Vector3 target)
    {
        if (arm.Bone == null)
            return;

        Quaternion rest = arm.Bone.parent.rotation * arm.Rest;
        Vector3 current = rest * hand;
        Vector3 wanted = target - arm.Bone.position;

        if (wanted.sqrMagnitude < 0.0001f)
            return;

        arm.Bone.rotation = Quaternion.FromToRotation(current, wanted) * rest;
        float stretch = Mathf.Clamp(wanted.magnitude / (current.magnitude * arm.Bone.parent.lossyScale.y), 0.8f, 1.35f);
        arm.Bone.localScale = Vector3.one * stretch;
    }

    // ---------- Survivors ----------

    private float cower;
    private float headYaw;

    // Turned (within reach of the neck) towards whoever they're looking at, otherwise glancing
    // around now and then; a nod while talking.
    private void AnimateNpcHead(float dt)
    {
        float wanted = (Mathf.PerlinNoise(seed + 5f, Time.time * 0.15f) - 0.5f) * 70f;

        if (LookAt != null)
        {
            Vector3 local = transform.InverseTransformPoint(LookAt.position);
            wanted = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -65f, 65f);
        }

        headYaw = Mathf.LerpAngle(headYaw, wanted, 1f - Mathf.Exp(-6f * dt));
        float nod = Talking ? Mathf.Sin(Time.time * 7f) * 5f : 0f;
        Pose(head, Quaternion.Euler(nod - 12f * cower, headYaw, 0f));
    }

    // Arms hang and sway with the stride; one hand gestures while talking; both come up over
    // the head when cowering.
    private void AnimateNpcArms()
    {
        float sway = Mathf.Sin(phase) * walkBlend * 18f;
        float idle = (Mathf.PerlinNoise(seed + 10f, Time.time * 0.4f) - 0.5f) * 6f;
        float gesture = Talking ? (0.5f + 0.5f * Mathf.Sin(Time.time * 4.5f + seed)) : 0f;

        // Negative X brings an arm forward and up.
        Quaternion left = Quaternion.Euler(sway + idle, 0f, 0f);
        Quaternion right = Quaternion.Euler(-sway - idle - 55f * gesture, 0f, -15f * gesture);
        Quaternion shield = Quaternion.Euler(-150f, 0f, 0f);

        Pose(armL, Quaternion.Slerp(left, shield * Quaternion.Euler(0f, 0f, -20f), cower));
        Pose(armR, Quaternion.Slerp(right, shield * Quaternion.Euler(0f, 0f, 20f), cower));
    }
}
