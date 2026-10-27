using UnityEngine;

// A block that fills a doorway. Open = walk-through and invisible, closed = solid. The door is a
// shutter: shown, it rises out of the floor when it shuts and sinks back into it when it opens
// (the floor hides the part below it), over SlideTime. The collider switches straight away, so a
// locked room is locked the moment it shuts. The look is a copy of the block's mesh on a child
// panel that slides, made on the first frame, so the block itself (and its collider) never moves.
public class RoomDoor : MonoBehaviour
{
    private const float SlideTime = 0.25f;

    [SerializeField] private bool isOpen = true;

    private Collider doorCollider;
    private Renderer doorRenderer;
    private Transform panel;
    private Renderer panelRenderer;
    private float shown;    // 0 = sunk in the floor, 1 = standing in the doorway

    public bool IsOpen => isOpen;

    private void Awake()
    {
        doorCollider = GetComponent<Collider>();
        doorRenderer = GetComponent<Renderer>();
        shown = isOpen ? 0f : 1f;
        Apply();
    }

    private void Start()
    {
        MeshFilter filter = GetComponent<MeshFilter>();

        if (doorRenderer == null || filter == null)
            return;

        GameObject child = new GameObject("Panel");
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        child.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
        panelRenderer = child.AddComponent<MeshRenderer>();
        panelRenderer.sharedMaterials = doorRenderer.sharedMaterials;
        panelRenderer.shadowCastingMode = doorRenderer.shadowCastingMode;
        panelRenderer.receiveShadows = doorRenderer.receiveShadows;
        panelRenderer.renderingLayerMask = doorRenderer.renderingLayerMask;
        panel = child.transform;
        doorRenderer.enabled = false;
        Slide();
    }

    public void SetOpen(bool open)
    {
        isOpen = open;
        Apply();
    }

    private void Update()
    {
        if (panel == null)
            return;

        float target = isOpen ? 0f : 1f;

        if (shown == target)
            return;

        shown = Mathf.MoveTowards(shown, target, Time.deltaTime / SlideTime);
        Slide();
    }

    // Sunk by however much isn't shown (a little further, so the top's flush with the floor).
    private void Slide()
    {
        panel.localPosition = Vector3.down * (1.02f * (1f - shown));
        panelRenderer.enabled = shown > 0f;
    }

    private void Apply()
    {
        if (doorCollider != null)
            doorCollider.enabled = !isOpen;

        if (doorRenderer != null && panel == null)
            doorRenderer.enabled = !isOpen;
    }
}
