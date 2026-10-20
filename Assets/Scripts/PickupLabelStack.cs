using System.Collections.Generic;
using UnityEngine;

// Keeps the floating labels (PickupLabel) from piling on top of each other. Once every label has
// placed itself over its item this frame, labels that overlap on screen are gathered into groups
// and each group is laid out as a vertical stack: left edges lined up, in the same top-to-bottom
// order as their items, centred on where the labels wanted to be, and kept on screen. Groups
// that then overlap merge and are laid out again. A big pile (a boss's loot) shows only the
// MaxPerStack labels nearest the player, with "+N MORE NEARBY" under them; walking over to the
// others brings theirs in. A label moved off its item gets a leader line back to it. Moves ease,
// so labels slide rather than jump as the player walks about; a newly shown label goes straight
// to its place. Created by the first label shown; one per scene.
[DefaultExecutionOrder(210)]
public class PickupLabelStack : MonoBehaviour
{
    private const int MaxPerStack = 3;
    private const float Gap = 6f;            // pixels between stacked labels
    private const float ScreenMargin = 12f;
    private const float Ease = 16f;          // per second
    private const float LeaderAfter = 10f;   // pixels moved before a leader line is drawn

    private static PickupLabelStack instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        instance = null;
    }

    public static void Ensure()
    {
        if (instance == null)
            instance = new GameObject("Pickup Label Stack").AddComponent<PickupLabelStack>();
    }

    // One label as laid out on screen this frame.
    private class Entry
    {
        public PickupLabel Label;
        public Vector2 Wanted;       // where it put itself (screen centre, pixels)
        public Vector2 Half;         // half its size on screen
        public float Depth;
        public Vector2 Target;       // where the layout puts it
        public bool Summary;         // a "+N MORE" line: always at the bottom of its stack
    }

    private class Group
    {
        public readonly List<Entry> Members = new List<Entry>();
        public Rect Bounds;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<Group> groups = new List<Group>();
    private readonly List<PickupLabel> summaries = new List<PickupLabel>();
    private readonly List<int> summaryCounts = new List<int>();
    private Transform player;

    private void LateUpdate()
    {
        Camera camera = Camera.main;

        if (camera == null)
            return;

        if (player == null)
        {
            PlayerWeapon weapon = FindAnyObjectByType<PlayerWeapon>();
            player = weapon != null ? weapon.transform : null;
        }

        Measure(camera);
        Arrange();

        // Too many in one stack: keep the nearest, hide the rest behind a "+N MORE" line, and
        // lay everything out again.
        int summariesUsed = 0;

        if (Trim(camera, ref summariesUsed))
            Merge();

        for (int i = summariesUsed; i < summaries.Count; i++)
        {
            if (summaries[i].IsShowing)
                summaries[i].Hide();
        }

        Apply(camera);
    }

    // Each visible label's spot and size in screen pixels. The labels face the camera, so their
    // own right and up are the camera's.
    private void Measure(Camera camera)
    {
        entries.Clear();

        foreach (PickupLabel label in PickupLabel.Showing)
        {
            if (label == null || label.Managed || !label.Visible)
                continue;

            label.SetSuppressed(false);
            Entry entry = MeasureOne(camera, label, label.Placed);

            if (entry != null)
                entries.Add(entry);
        }
    }

    private static Entry MeasureOne(Camera camera, PickupLabel label, Vector3 at)
    {
        Vector3 centre = camera.WorldToScreenPoint(at);

        if (centre.z <= 0f)
            return null;

        Vector3 right = camera.WorldToScreenPoint(at + camera.transform.right * label.WorldSize.x / 2f);
        Vector3 up = camera.WorldToScreenPoint(at + camera.transform.up * label.WorldSize.y / 2f);

        return new Entry
        {
            Label = label,
            Wanted = centre,
            Half = new Vector2(Mathf.Abs(right.x - centre.x), Mathf.Abs(up.y - centre.y)),
            Depth = centre.z
        };
    }

    // Start with every label alone where it wants to be, then merge.
    private void Arrange()
    {
        groups.Clear();

        foreach (Entry entry in entries)
        {
            Group group = new Group();
            group.Members.Add(entry);
            Stack(group);
            groups.Add(group);
        }

        Merge();
    }

    // While any two groups overlap, merge them and stack the result.
    private void Merge()
    {
        for (int pass = 0; pass < 64; pass++)
        {
            bool merged = false;

            for (int a = 0; a < groups.Count && !merged; a++)
            {
                for (int b = a + 1; b < groups.Count; b++)
                {
                    if (!Overlaps(groups[a].Bounds, groups[b].Bounds))
                        continue;

                    groups[a].Members.AddRange(groups[b].Members);
                    groups.RemoveAt(b);
                    Stack(groups[a]);
                    merged = true;
                    break;
                }
            }

            if (!merged)
                break;
        }
    }

    // Groups over the limit keep the labels nearest the player; the rest are hidden and a
    // summary line joins the stack at its foot. True if anything was trimmed.
    private bool Trim(Camera camera, ref int summariesUsed)
    {
        bool trimmed = false;

        foreach (Group group in groups)
        {
            if (group.Members.Count <= MaxPerStack)
                continue;

            Vector3 from = player != null ? player.position : camera.transform.position;
            group.Members.Sort((a, b) => Distance(a.Label, from).CompareTo(Distance(b.Label, from)));

            int hidden = group.Members.Count - MaxPerStack;

            for (int i = MaxPerStack; i < group.Members.Count; i++)
            {
                group.Members[i].Label.SetSuppressed(true);
                entries.Remove(group.Members[i]);
            }

            group.Members.RemoveRange(MaxPerStack, hidden);

            // Sized at the depth of the nearest label; it wants to be under the lowest one,
            // lined up with the rest, so it doesn't pull the stack about.
            PickupLabel summary = Summary(summariesUsed, hidden);
            summariesUsed++;

            float left = 0f;
            float lowest = float.MaxValue;

            foreach (Entry member in group.Members)
            {
                left += member.Wanted.x - member.Half.x;
                lowest = Mathf.Min(lowest, member.Wanted.y);
            }

            left /= group.Members.Count;
            Vector3 world = camera.ScreenToWorldPoint(new Vector3(left, lowest, group.Members[0].Depth));
            Entry entry = MeasureOne(camera, summary, world);

            if (entry != null)
            {
                entry.Wanted = new Vector2(left + entry.Half.x, lowest);
                entry.Summary = true;
                entries.Add(entry);
                group.Members.Add(entry);
            }

            Stack(group);
            trimmed = true;
        }

        return trimmed;
    }

    private static float Distance(PickupLabel label, Vector3 from)
    {
        Vector3 offset = label.Anchor - from;
        offset.y = 0f;
        return offset.sqrMagnitude;
    }

    // The n-th summary label, made as needed, showing "+hidden MORE NEARBY".
    private PickupLabel Summary(int index, int hidden)
    {
        while (summaries.Count <= index)
        {
            PickupLabel made = PickupLabel.Create(transform, 0f);
            made.Managed = true;
            summaries.Add(made);
            summaryCounts.Add(0);
        }

        PickupLabel summary = summaries[index];
        summaryCounts[index] = hidden;

        if (!summary.IsShowing)
            summary.Show(() => $"<size=80%><alpha=#BB>+{summaryCounts[index]} MORE NEARBY</size>", UiColours.Phosphor);

        return summary;
    }

    // A column: highest item at the top (any summary line last), left edges lined up on the
    // group's average wanted left edge, centred on its average wanted height, then nudged to
    // stay on screen.
    private static void Stack(Group group)
    {
        List<Entry> members = group.Members;
        members.Sort((a, b) => a.Summary != b.Summary ? a.Summary.CompareTo(b.Summary) : b.Wanted.y.CompareTo(a.Wanted.y));

        float height = -Gap;
        float left = 0f;
        float middle = 0f;
        float width = 0f;

        foreach (Entry entry in members)
        {
            height += entry.Half.y * 2f + Gap;
            left += entry.Wanted.x - entry.Half.x;
            middle += entry.Wanted.y;
            width = Mathf.Max(width, entry.Half.x * 2f);
        }

        left /= members.Count;
        middle /= members.Count;

        float top = middle + height / 2f;
        top = Mathf.Min(top, Screen.height - ScreenMargin);
        top = Mathf.Max(top, height + ScreenMargin);
        left = Mathf.Clamp(left, ScreenMargin, Mathf.Max(ScreenMargin, Screen.width - ScreenMargin - width));

        float y = top;

        foreach (Entry entry in members)
        {
            entry.Target = new Vector2(left + entry.Half.x, y - entry.Half.y);
            y -= entry.Half.y * 2f + Gap;
        }

        group.Bounds = new Rect(left, top - height, width, height);
    }

    private static bool Overlaps(Rect a, Rect b)
    {
        return a.xMin < b.xMax + Gap && b.xMin < a.xMax + Gap && a.yMin < b.yMax + Gap && b.yMin < a.yMax + Gap;
    }

    // Eases each label towards its place and moves it there (at its own depth, so perspective
    // still sizes it as before). Summary lines just go there, without a leader.
    private void Apply(Camera camera)
    {
        float blend = 1f - Mathf.Exp(-Ease * Time.unscaledDeltaTime);

        foreach (Entry entry in entries)
        {
            PickupLabel label = entry.Label;
            Vector2 wantedOffset = entry.Target - entry.Wanted;
            label.StackOffset = label.JustShown ? wantedOffset : Vector2.Lerp(label.StackOffset, wantedOffset, blend);
            label.JustShown = false;

            Vector2 screen = entry.Wanted + label.StackOffset;
            Vector3 position = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, entry.Depth));
            label.MoveTo(position, !entry.Summary && label.StackOffset.magnitude > LeaderAfter, camera);
        }
    }
}
