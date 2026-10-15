using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// Debug view (F5 menu) of how far enemies can sense the player. Each noise shows a ring at the
// distance it can be heard, coloured by kind and fading out over a moment: footsteps green,
// reloads cyan, gunshots red. A faint white ring follows the player at the current sight range
// (smaller while sneaking). Rings are drawn on the floor and on the minimap, which shows far
// more of the room than the camera does, and a readout under the minimap lists each noise and
// its range. Off by default; built the first time it's turned on.
public class NoiseDebug : MonoBehaviour
{
    public static bool Enabled { get; private set; }

    // A ring as the minimap draws it.
    public struct RingInfo
    {
        public Vector3 Centre;
        public float Radius;
        public Color Colour;
    }

    private const int Segments = 64;
    private const float RingHeight = 0.06f;
    private const float RingWidth = 0.08f;
    private const float FadeTime = 1.2f;
    private const int PoolSize = 24;
    private const int ReadoutLines = 5;

    private static readonly Color StepColour = new Color(0.35f, 1f, 0.55f);
    private static readonly Color ReloadColour = new Color(0.3f, 0.85f, 1f);
    private static readonly Color GunshotColour = new Color(1f, 0.3f, 0.25f);
    private static readonly Color SightColour = new Color(1f, 1f, 1f, 0.5f);

    private static NoiseDebug instance;

    private class Ring
    {
        public LineRenderer Line;
        public Vector3 Centre;
        public float Radius;
        public Color Colour;
        public string Label;
        public float Born = -100f;
        public bool Active;
    }

    private readonly List<Ring> rings = new List<Ring>();
    private readonly List<RingInfo> visible = new List<RingInfo>();
    private readonly StringBuilder readoutText = new StringBuilder();
    private Material material;
    private Ring sightRing;
    private TMP_Text readout;
    private PlayerMovement player;
    private int next;

    // Statics outlive Play sessions with domain reload off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Enabled = false;
        instance = null;
    }

    public static void SetEnabled(bool on)
    {
        Enabled = on;

        if (on && instance == null)
            instance = new GameObject("Noise Debug").AddComponent<NoiseDebug>();

        if (instance != null)
        {
            instance.gameObject.SetActive(on);

            if (instance.readout != null)
                instance.readout.gameObject.SetActive(on);
        }
    }

    // Called by Noise for every noise, while the view is on.
    public static void Show(Vector3 position, float radius, Noise.Kind kind, string label)
    {
        if (!Enabled || instance == null)
            return;

        Color colour = kind == Noise.Kind.Gunshot ? GunshotColour : kind == Noise.Kind.Reload ? ReloadColour : StepColour;
        instance.Spawn(position, radius, colour, label);
    }

    // The rings to draw right now (noises still fading, then the sight ring), for the minimap.
    public static IReadOnlyList<RingInfo> VisibleRings
    {
        get
        {
            if (!Enabled || instance == null)
                return System.Array.Empty<RingInfo>();

            return instance.visible;
        }
    }

    private void Awake()
    {
        material = new Material(Shader.Find("Sprites/Default"));

        for (int i = 0; i < PoolSize; i++)
            rings.Add(MakeRing());

        sightRing = MakeRing();
        sightRing.Colour = SightColour;
        sightRing.Active = true;

        BuildReadout();
    }

    private void OnDestroy()
    {
        if (readout != null)
            Destroy(readout.gameObject);
    }

    private Ring MakeRing()
    {
        GameObject go = new GameObject("Ring");
        go.transform.SetParent(transform, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = Segments;
        line.widthMultiplier = RingWidth;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;

        return new Ring { Line = line };
    }

    // A small list of recent noises under the minimap, on the HUD canvas.
    private void BuildReadout()
    {
        Minimap minimap = FindAnyObjectByType<Minimap>();

        if (minimap == null)
            return;

        GameObject go = new GameObject("Noise Readout", typeof(RectTransform));
        go.layer = minimap.gameObject.layer;
        go.transform.SetParent(minimap.transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(0f, -24f);
        rect.sizeDelta = new Vector2(260f, 110f);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 11;
        text.characterSpacing = 4f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.richText = true;
        readout = text;
    }

    private void Spawn(Vector3 position, float radius, Color colour, string label)
    {
        Ring ring = rings[next];
        next = (next + 1) % rings.Count;

        ring.Centre = position;
        ring.Radius = radius;
        ring.Colour = colour;
        ring.Label = label;
        ring.Born = Time.time;
        ring.Active = true;
        Place(ring);
    }

    private static void Place(Ring ring)
    {
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            ring.Line.SetPosition(i, new Vector3(
                ring.Centre.x + Mathf.Cos(angle) * ring.Radius,
                RingHeight,
                ring.Centre.z + Mathf.Sin(angle) * ring.Radius));
        }

        ring.Line.enabled = true;
    }

    private void LateUpdate()
    {
        visible.Clear();

        // Newest first, for the readout.
        List<Ring> recent = new List<Ring>();

        foreach (Ring ring in rings)
        {
            if (!ring.Active)
                continue;

            float age = (Time.time - ring.Born) / FadeTime;

            if (age >= 1f)
            {
                ring.Active = false;
                ring.Line.enabled = false;
                continue;
            }

            Color colour = ring.Colour;
            colour.a = 1f - age;
            ring.Line.startColor = ring.Line.endColor = colour;
            visible.Add(new RingInfo { Centre = ring.Centre, Radius = ring.Radius, Colour = colour });
            recent.Add(ring);
        }

        recent.Sort((a, b) => b.Born.CompareTo(a.Born));

        if (player == null)
            player = FindAnyObjectByType<PlayerMovement>();

        float sight = EnemySenses.Current.SightRangeFor(player != null && player.IsSneaking);

        if (player != null)
        {
            sightRing.Centre = player.transform.position;
            sightRing.Radius = sight;
            Place(sightRing);
            sightRing.Line.startColor = sightRing.Line.endColor = SightColour;
            visible.Add(new RingInfo { Centre = sightRing.Centre, Radius = sight, Colour = SightColour });
        }

        UpdateReadout(recent, sight);
    }

    private void UpdateReadout(List<Ring> recent, float sight)
    {
        if (readout == null)
            return;

        readoutText.Clear();
        readoutText.Append("<alpha=#88>> DETECTION<alpha=#FF>\n");
        readoutText.Append($"<color=#FFFFFF88>SIGHT {sight:0.#}{(player != null && player.IsSneaking ? "  (SNEAK)" : "")}</color>\n");

        for (int i = 0; i < Mathf.Min(ReadoutLines, recent.Count); i++)
        {
            Color colour = recent[i].Colour;
            colour.a = 1f - (Time.time - recent[i].Born) / FadeTime;
            readoutText.Append($"<color=#{ColorUtility.ToHtmlStringRGBA(colour)}>{recent[i].Label}</color>\n");
        }

        readout.text = readoutText.ToString();
    }
}
