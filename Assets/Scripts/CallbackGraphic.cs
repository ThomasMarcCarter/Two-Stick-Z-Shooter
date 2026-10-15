using System;
using UnityEngine;
using UnityEngine.UI;

// A UI graphic whose mesh is drawn by whoever owns it, for screens built in code that need
// lines, frames and icons alongside their text (e.g. the inventory). Redraws every frame while
// enabled in Play mode.
public class CallbackGraphic : MaskableGraphic
{
    public Action<VertexHelper, Rect> Draw;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    private void Update()
    {
        if (Application.isPlaying)
            SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Draw?.Invoke(vh, rectTransform.rect);
    }
}
