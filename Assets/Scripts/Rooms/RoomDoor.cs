using UnityEngine;

// A block that fills a doorway. Open = walk-through and invisible, closed = solid.
public class RoomDoor : MonoBehaviour
{
    [SerializeField] private bool isOpen = true;

    private Collider doorCollider;
    private Renderer doorRenderer;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        doorCollider = GetComponent<Collider>();
        doorRenderer = GetComponent<Renderer>();
        Apply();
    }

    public void SetOpen(bool open)
    {
        isOpen = open;
        Apply();
    }

    private void Apply()
    {
        if (doorCollider != null)
            doorCollider.enabled = !isOpen;

        if (doorRenderer != null)
            doorRenderer.enabled = !isOpen;
    }
}
