using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The ZoneExit's going in: the player walks or rides in, hides, the doors close behind them,
// and the floor transition plays; FinishDescent brings them back on the next floor.
public partial class ZoneExit
{
    // Walks the player in (or onto it and down), hides them, then `done` (the floor transition).
    public void Use(Transform player, Action done)
    {
        StartCoroutine(GoIn(player, done));
    }

    private IEnumerator GoIn(Transform player, Action done)
    {
        Descending = true;
        Rigidbody body = player.GetComponent<Rigidbody>();
        bool gravity = body != null && body.useGravity;

        if (body != null)
        {
            body.useGravity = false;
            body.detectCollisions = false;
            body.linearVelocity = Vector3.zero;
        }

        float rest = player.position.y;
        Vector3 Local(float x, float y, float z) => transform.TransformPoint(new Vector3(x, 0f, z)) + Vector3.up * (rest + y);

        restore = () =>
        {
            if (player == null)
                return;

            if (body != null)
            {
                body.useGravity = gravity;
                body.detectCollisions = true;
            }

            Show(player, true);
        };

        yield return Move(player, body, new Vector3(Entry.x, rest, Entry.z), WalkSpeed);

        switch (Zone)
        {
            case Zone.ShoppingMall:
                // Onto the top step, then down with the steps into the floor.
                yield return Move(player, body, Local(0f, 0f, 0.25f), WalkSpeed);
                yield return Move(player, body, Local(0f, -1.9f, 2.7f), 3.1f / 2.2f);
                Show(player, false);
                break;

            case Zone.Airport:
                // Up onto the belt, along it and through the strips.
                yield return Move(player, body, Local(0f, 0.6f, 0.3f), 2.5f);
                yield return Move(player, body, Local(0f, 0.6f, 2.3f), 1.2f);
                Show(player, false);
                yield return new WaitForSeconds(0.3f);
                break;

            case Zone.Hospital:
                // Into the car; the doors close (the open frames backwards) with a thud.
                yield return Move(player, body, Local(0f, 0f, 0.3f), WalkSpeed);
                Show(player, false);
                yield return Frames(new[] { 6, 5, 4 }, 0.2f);
                Clank(0.5f);
                yield return new WaitForSeconds(0.4f);
                break;

            default:
                // Through the door and a step down into the dark; the door slams behind.
                yield return Move(player, body, Local(0f, -0.35f, 0.3f), WalkSpeed * 0.8f);
                Show(player, false);
                yield return Frames(new[] { 4, 3, 2 }, 0.15f);
                Clank(0.4f);
                yield return new WaitForSeconds(0.35f);
                break;
        }

        done?.Invoke();
    }

    // The player's body moved by hand to a point at a speed (metres a second).
    private static IEnumerator Move(Transform player, Rigidbody body, Vector3 target, float speed)
    {
        while (player != null && (player.position - target).sqrMagnitude > 0.0004f)
        {
            Vector3 next = Vector3.MoveTowards(player.position, target, speed * Time.deltaTime);
            player.position = next;

            if (body != null)
            {
                body.position = next;
                body.linearVelocity = Vector3.zero;
            }

            yield return null;
        }
    }

    private IEnumerator Frames(int[] columns, float each)
    {
        foreach (int column in columns)
        {
            heldColumn = column;
            yield return new WaitForSeconds(each);
        }
    }

    private static readonly List<Renderer> hiddenParts = new List<Renderer>();
    private static readonly List<Light> hiddenLights = new List<Light>();

    // The player (body, gun, flashlight) hidden in the exit, or shown again.
    private static void Show(Transform player, bool visible)
    {
        if (player.TryGetComponent(out PlayerHealth health))
            health.Hidden = !visible;

        if (!visible)
        {
            hiddenParts.Clear();
            hiddenLights.Clear();

            foreach (Renderer part in player.GetComponentsInChildren<Renderer>())
            {
                if (part.enabled)
                {
                    part.enabled = false;
                    hiddenParts.Add(part);
                }
            }

            foreach (Light light in player.GetComponentsInChildren<Light>())
            {
                if (light.enabled)
                {
                    light.enabled = false;
                    hiddenLights.Add(light);
                }
            }

            return;
        }

        foreach (Renderer part in hiddenParts)
        {
            if (part != null)
                part.enabled = true;
        }

        foreach (Light light in hiddenLights)
        {
            if (light != null)
                light.enabled = true;
        }

        hiddenParts.Clear();
        hiddenLights.Clear();
    }

    // The next floor is built and the player's about to be put in it: back to normal.
    public static void FinishDescent()
    {
        restore?.Invoke();
        restore = null;
        Descending = false;
    }
}
