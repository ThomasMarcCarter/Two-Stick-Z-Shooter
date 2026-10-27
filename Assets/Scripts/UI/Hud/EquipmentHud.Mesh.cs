using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static MapMesh;

// The EquipmentHud's mesh: each gun's panel frame, its ammo pips and the corner brackets on the
// one in hand.
public partial class EquipmentHud
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (weapons == null)
            return;

        Vector2 origin = rectTransform.rect.min;
        DrawPanel(vh, Gun.Slot.Main, weapons.Main.Gun, origin);
        DrawPanel(vh, Gun.Slot.Sidearm, weapons.Sidearm.Gun, origin);
    }

    private void DrawPanel(VertexHelper vh, Gun.Slot slot, Gun gun, Vector2 origin)
    {
        Rect panel = Offset(PanelRect(slot), origin);
        bool active = slot == weapons.ActiveSlot;

        if (active)
        {
            // In hand: lit up. A phosphor-tinted fill (brighter for a moment after switching), a
            // solid bright frame, the radar's corner brackets and a selection bar down the left.
            float flash = 1f - Mathf.Clamp01((Time.unscaledTime - switchedAt) / SwitchFlashTime);
            FillRect(vh, panel, new Color(0f, 0.07f, 0.03f, 0.85f));
            FillRect(vh, panel, Fade(UiColours.Phosphor, 0.05f + 0.25f * flash));
            OutlineRect(vh, panel, 2f, UiColours.Phosphor);
            Brackets(vh, panel, UiColours.Phosphor);
            FillRect(vh, new Rect(panel.xMin - 9f, panel.yMin, 4f, panel.height), UiColours.Phosphor);
        }
        else
        {
            // Stowed: unlit and faded.
            FillRect(vh, panel, new Color(0f, 0.04f, 0.02f, 0.5f));
            OutlineRect(vh, panel, 1f, Fade(UiColours.Phosphor, 0.18f));
        }

        if (gun == null)
            return;

        float brightness = active ? 1f : Unlit;
        WeaponSilhouette.Draw(vh, gun.HudIcon, IconArea(panel, active), Fade(UiColours.Phosphor, brightness));

        if (active)
            DrawPips(vh, gun, PipArea(panel));
    }

    // One pip per round in the magazine, filled for rounds loaded; a thin bar shows reload progress
    // on the gun in hand. Pips and gaps are whole screen pixels wide, so with a big magazine (a
    // drum or extended mag) every gap is the same, rather than some rounding to nothing and some
    // to two pixels; the row is centred in what's left over.
    private void DrawPips(VertexHelper vh, Gun gun, Rect area)
    {
        int size = Mathf.Max(1, gun.GetMagazineSize());
        int loaded = gun.GetCurrentAmmo();
        float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        int areaPixels = Mathf.FloorToInt(area.width * scale);
        int gapPixels = Mathf.Max(1, Mathf.RoundToInt((size > 20 ? 1f : 3f) * scale));
        int pipPixels = Mathf.Max(1, (areaPixels - gapPixels * (size - 1)) / size);
        int rowPixels = pipPixels * size + gapPixels * (size - 1);
        float start = area.xMin + Mathf.Floor((areaPixels - rowPixels) / 2f) / scale;
        float pipWidth = pipPixels / scale;
        float step = (pipPixels + gapPixels) / scale;
        Color full = AmmoColour(gun);
        Color empty = Fade(UiColours.Phosphor, 0.35f);

        for (int i = 0; i < size; i++)
        {
            Rect pip = new Rect(start + i * step, area.yMin, pipWidth, area.height);

            if (i < loaded)
                FillRect(vh, pip, full);
            else
                OutlineRect(vh, pip, Mathf.Min(1f, pipWidth / 2f), empty);
        }

        if (gun.IsReloading)
        {
            Rect bar = new Rect(area.xMin, area.yMin - 4f, area.width * gun.ReloadProgress, 2f);
            FillRect(vh, bar, UiColours.Phosphor);
        }
    }

    private static void Brackets(VertexHelper vh, Rect rect, Color colour)
    {
        const float length = 12f;
        const float thickness = 2f;

        FillRect(vh, new Rect(rect.xMin, rect.yMax - thickness, length, thickness), colour);
        FillRect(vh, new Rect(rect.xMin, rect.yMax - length, thickness, length), colour);
        FillRect(vh, new Rect(rect.xMax - length, rect.yMax - thickness, length, thickness), colour);
        FillRect(vh, new Rect(rect.xMax - thickness, rect.yMax - length, thickness, length), colour);
        FillRect(vh, new Rect(rect.xMin, rect.yMin, length, thickness), colour);
        FillRect(vh, new Rect(rect.xMin, rect.yMin, thickness, length), colour);
        FillRect(vh, new Rect(rect.xMax - length, rect.yMin, length, thickness), colour);
        FillRect(vh, new Rect(rect.xMax - thickness, rect.yMin, thickness, length), colour);
    }

    private static Rect Offset(Rect rect, Vector2 origin)
    {
        return new Rect(rect.position + origin, rect.size);
    }
}
