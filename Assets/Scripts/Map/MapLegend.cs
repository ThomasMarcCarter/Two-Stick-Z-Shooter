using UnityEngine;
using UnityEngine.UI;

// Swatches for the map legend, one per row from the top. Labels are separate TMP texts
// lined up with the rows.
public class MapLegend : MaskableGraphic
{
    public const float RowHeight = 22f;

    [SerializeField] private TacticalMapView view;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (view == null)
            return;

        Color phosphor = view.Phosphor;
        Color hostile = view.Hostile;
        Rect bounds = rectTransform.rect;

        Rect Swatch(int row) => new Rect(bounds.xMin, bounds.yMax - (row + 1) * RowHeight + 3f, 30f, RowHeight - 6f);
        Vector2 Icon(int row) => Swatch(row).center;

        MapSymbols.DrawRoom(vh, Swatch(0), MapRoomState.Cleared, phosphor, hostile);
        MapSymbols.DrawRoom(vh, Swatch(1), MapRoomState.Hostile, phosphor, hostile);
        MapSymbols.DrawRoom(vh, Swatch(2), MapRoomState.Unexplored, phosphor, hostile);
        MapSymbols.DrawPlayer(vh, Icon(3), Vector2.up, 8f, phosphor);
        MapSymbols.DrawSpawn(vh, Icon(4), 11f, phosphor);
        MapSymbols.DrawTreasure(vh, Icon(5), 11f, phosphor);
        MapSymbols.DrawBoss(vh, Icon(6), 10f, hostile);
        MapSymbols.DrawItems(vh, Icon(7), 9f, phosphor);
        MapSymbols.DrawAmmo(vh, Icon(8), 12f, phosphor);
        MapSymbols.DrawMedkit(vh, Icon(9), 11f, phosphor);
    }
}
