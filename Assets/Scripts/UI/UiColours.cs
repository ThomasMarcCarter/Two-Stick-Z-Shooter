using UnityEngine;

// The shared colours of the green-phosphor "military terminal" UI, so every screen, HUD widget
// and label matches: the phosphor green most text and lines are drawn in, the near-black green
// panels sit on, amber for scrap and highlights, and red for anything the player is short of or
// about to lose. The hex strings are for TextMesh Pro rich text (<color=...>).
// The HUD, maps and menus use these directly; only CrtButton keeps per-button Inspector colours
// (green, amber and red buttons), which default to these.
public static class UiColours
{
    public static readonly Color Phosphor = new Color(0.35f, 1f, 0.55f, 1f);
    public static readonly Color PanelBackground = new Color(0f, 0.035f, 0.018f, 0.96f);
    public static readonly Color Amber = new Color(1f, 0.85f, 0.35f, 1f);
    public static readonly Color Danger = new Color(1f, 0.4f, 0.333f, 1f);
    // The HUD's and maps' red: rooms with hostiles, radar blips, low health, low ammo, exhaustion.
    public static readonly Color Hostile = new Color(1f, 0.25f, 0.2f, 1f);
    // The minimap's see-through scope behind its grid and rings.
    public static readonly Color RadarBackground = new Color(0f, 0.07f, 0.03f, 0.85f);
    // The main menu's solid backdrop, hiding the paused game.
    public static readonly Color MenuBackground = new Color(0f, 0.025f, 0.012f, 1f);

    public const string AmberHex = "#FFD95A";
    public const string DangerHex = "#FF6655";

    // The same colour at another opacity (dim text, button fills).
    public static Color WithAlpha(Color colour, float alpha)
    {
        colour.a = alpha;
        return colour;
    }
}
