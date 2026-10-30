using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Item pictures for the UI drawn from their pixel-art sprites (the art pass), so the backpack,
// quick bar, crates and HUD show the same thing as the floor: each opaque pixel of the sprite's
// frame becomes a square in the caller's mesh, in the caller's ink (one-colour phosphor), lit or
// dimmed by the pixel's own brightness; the sprite's dark outline stays a faint rim. Runs of
// equal pixels along a row are merged into one quad. Fitted into the area (at a whole number of
// screen pixels per art pixel once that's 2 or more), centred on the frame's drawn part. The
// sheets' textures are imported Read/Write for this. Draw returns false without the art (no
// SpriteVisuals, or an unreadable texture), and the callers then draw their LineArt wireframe.
public static class PixelIcon
{
    private const float RimShade = 0.18f;   // the outline's brightness
    private const float DarkShade = 0.4f;   // the darkest coloured pixel's brightness

    private struct Run
    {
        public int x0, x1, y;
        public float shade;
    }

    private class Picture
    {
        public Run[] runs;
        public RectInt bounds;
    }

    private static readonly Dictionary<(Texture2D, int, int), Picture> pictures = new Dictionary<(Texture2D, int, int), Picture>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        pictures.Clear();
    }

    // fit: the art size (in pixels) the area stands for; by default the frame's drawn part, so it
    // fills the area. Guns pass one size for all, so a pistol draws smaller than a rifle.
    public static bool Draw(VertexHelper vh, SpriteSheet sheet, Vector2Int frame, Rect area, Color ink, Vector2 fit = default)
    {
        Picture picture = PictureOf(sheet, frame);

        if (picture == null || picture.runs.Length == 0)
            return false;

        RectInt bounds = picture.bounds;

        if (fit == Vector2.zero)
            fit = new Vector2(bounds.width, bounds.height);

        float size = Mathf.Min(area.width / Mathf.Max(1f, fit.x), area.height / Mathf.Max(1f, fit.y));

        if (size >= 2f)
            size = Mathf.Floor(size);

        Vector2 origin = area.center - new Vector2(bounds.center.x, bounds.center.y) * size;

        foreach (Run run in picture.runs)
        {
            Color colour = new Color(ink.r * run.shade, ink.g * run.shade, ink.b * run.shade, ink.a);
            Vector2 a = origin + new Vector2(run.x0, run.y) * size;
            Vector2 b = origin + new Vector2(run.x1 + 1, run.y + 1) * size;
            MapMesh.Quad(vh, a, new Vector2(a.x, b.y), b, new Vector2(b.x, a.y), colour);
        }

        return true;
    }

    // A gun's picture: row 1 of its sheet (the gun without the hand), all guns fitted to one size.
    public static bool DrawGun(VertexHelper vh, string gunName, Rect area, Color ink)
    {
        SpriteSheet sheet = SpriteVisuals.Current != null ? SpriteVisuals.Current.ForGun(gunName) : null;
        return Draw(vh, sheet, new Vector2Int(0, 1), area, ink, new Vector2(32f, 16f));
    }

    public static bool DrawItem(VertexHelper vh, Vector2Int frame, Rect area, Color ink)
    {
        return Draw(vh, SpriteVisuals.Current != null ? SpriteVisuals.Current.items : null, frame, area, ink);
    }

    public static bool DrawStory(VertexHelper vh, Vector2Int frame, Rect area, Color ink)
    {
        return Draw(vh, SpriteVisuals.Current != null ? SpriteVisuals.Current.storyItems : null, frame, area, ink);
    }

    private static Picture PictureOf(SpriteSheet sheet, Vector2Int frame)
    {
        Texture2D texture = sheet != null ? sheet.texture as Texture2D : null;

        if (texture == null || !texture.isReadable)
            return null;

        var key = (texture, frame.x, frame.y);

        if (pictures.TryGetValue(key, out Picture picture))
            return picture;

        picture = Read(texture, sheet, frame);
        pictures[key] = picture;
        return picture;
    }

    // The frame's pixels as runs, y up from the frame's bottom row, shaded by brightness.
    private static Picture Read(Texture2D texture, SpriteSheet sheet, Vector2Int frame)
    {
        int w = sheet.frameWidth, h = sheet.frameHeight;
        int left = frame.x * w, bottom = texture.height - (frame.y + 1) * h;

        if (left < 0 || bottom < 0 || left + w > texture.width)
            return new Picture { runs = new Run[0] };

        Color[] pixels = texture.GetPixels(left, bottom, w, h);
        float[] light = new float[pixels.Length];
        float darkest = 1f, brightest = 0f;

        for (int i = 0; i < pixels.Length; i++)
        {
            Color p = pixels[i];
            light[i] = -1f;

            if (p.a < 0.5f)
                continue;

            light[i] = p.grayscale;

            if (IsRim(p))
                continue;

            darkest = Mathf.Min(darkest, light[i]);
            brightest = Mathf.Max(brightest, light[i]);
        }

        List<Run> runs = new List<Run>();
        int minX = w, minY = h, maxX = -1, maxY = -1;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;

                if (light[i] < 0f)
                    continue;

                float shade = IsRim(pixels[i])
                    ? RimShade
                    : Mathf.Lerp(DarkShade, 1f, Mathf.InverseLerp(darkest, Mathf.Max(darkest + 0.01f, brightest), light[i]));
                shade = Mathf.Round(shade * 16f) / 16f;   // a few steps, so runs merge

                if (runs.Count > 0 && runs[runs.Count - 1].y == y && runs[runs.Count - 1].x1 == x - 1 && runs[runs.Count - 1].shade == shade)
                {
                    Run last = runs[runs.Count - 1];
                    last.x1 = x;
                    runs[runs.Count - 1] = last;
                }
                else
                {
                    runs.Add(new Run { x0 = x, x1 = x, y = y, shade = shade });
                }

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
        }

        RectInt bounds = maxX >= 0 ? new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1) : new RectInt(0, 0, w, h);
        return new Picture { runs = runs.ToArray(), bounds = bounds };
    }

    // The sprites' outline colour (make_placeholders.py OUTLINE) and anything as dark.
    private static bool IsRim(Color p)
    {
        return p.r < 0.08f && p.g < 0.08f && p.b < 0.08f;
    }
}
