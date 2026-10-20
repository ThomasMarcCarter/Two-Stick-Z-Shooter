using System;
using UnityEngine;

// One pixel-art sprite sheet: a grid of equal frames (top-left first) and the clips laid out in
// it. A directional clip takes five rows, one per drawn facing in SpriteFacing order (down,
// down-side, side, up-side, up; side faces right and is mirrored for the left), with its frames
// along each row; a plain clip takes one row. The pivot is the pixel that sits on the
// character's feet (or a gun's grip), and a sheet is shown at its pixelsPerUnit.
// Placeholder sheets come from Tools/Sprites/make_placeholders.py; real art replaces the PNGs.
[CreateAssetMenu(menuName = "Roguelike/Sprite Sheet", fileName = "SpriteSheet")]
public class SpriteSheet : ScriptableObject
{
    [Serializable]
    public class Clip
    {
        public string name = "idle";
        [Tooltip("First row of the clip, counting from the top of the sheet.")]
        public int row;
        public int frames = 1;
        [Tooltip("Frames per second for clips that play on time (idle). Walk clips follow the stride instead.")]
        public float fps = 4f;
        [Tooltip("Five rows, one per facing; otherwise one row whatever the facing.")]
        public bool directional = true;
    }

    public Texture2D texture;
    public int frameWidth = 32;
    public int frameHeight = 32;
    [Tooltip("The pixel, from the bottom-left of a frame, that sits on the feet (a gun: its grip).")]
    public Vector2 pivot = new Vector2(16f, 2f);
    [Tooltip("Art pixels per metre on screen. The world is drawn at 16; a higher number shows this sheet smaller (the placeholder characters are drawn big and shown at 20).")]
    public float pixelsPerUnit = PixelSprite.PixelsPerUnit;
    public Clip[] clips = new Clip[0];

    [Header("Body (characters)")]
    [Tooltip("Hitbox radius in metres (see CharacterVisuals.FitHitbox).")]
    public float bodyRadius = 0.25f;
    [Tooltip("Height of the drawn character in metres.")]
    public float bodyHeight = 1.75f;
    [Tooltip("Pixels above the pivot where a held gun's grip sits (the hands' height).")]
    public float holdHeight = 12f;
    [Tooltip("How far from the middle of the body a held gun's grip is, in metres.")]
    public float holdReach = 0.3f;

    [Header("Guns")]
    [Tooltip("The muzzle's pixel, from the bottom-left of the frame (guns only).")]
    public Vector2 muzzle = new Vector2(15f, 10f);

    public Clip Find(string name)
    {
        foreach (Clip clip in clips)
        {
            if (clip.name == name)
                return clip;
        }

        return null;
    }

    // UV rectangle of a frame: column along, row down from the top.
    public Rect FrameUV(int column, int row)
    {
        if (texture == null)
            return new Rect(0f, 0f, 1f, 1f);

        float w = (float)frameWidth / texture.width;
        float h = (float)frameHeight / texture.height;
        return new Rect(column * w, 1f - (row + 1) * h, w, h);
    }
}
