using UnityEngine;

// The audio listener, kept on the player rather than the camera, so positional sounds (enemy
// footsteps) are heard from where the player is standing: an enemy to the player's left sounds
// to the left, however the camera is framed. It doesn't turn with the player's aim; it stays
// lined up with the screen (right is screen right, forward is screen up), so directions match
// what's on screen.
// Put it on its own object, not a child of the player, and remove the camera's listener.
[RequireComponent(typeof(AudioListener))]
[DefaultExecutionOrder(150)]
public class PlayerListener : MonoBehaviour
{
    [SerializeField] private Transform player;
    [Tooltip("Height above the floor to hear from, about the player's head.")]
    [SerializeField] private float earHeight = 1.5f;

    private void LateUpdate()
    {
        if (player == null)
            return;

        Vector3 position = player.position;
        position.y = earHeight;
        transform.SetPositionAndRotation(position, Quaternion.identity);
    }
}
