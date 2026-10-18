using UnityEngine;

// Turns an enemy's model to face where it's going: the way it's moving, or the player while it's
// chasing and close. The enemy's body itself never rotates (its rotation is frozen for the
// physics), so only the model child turns. Added by CharacterVisuals.AttachToEnemy.
public class FaceMovement : MonoBehaviour
{
    private const float TurnSpeed = 540f;   // degrees per second

    private Enemy enemy;

    private void Awake()
    {
        enemy = GetComponentInParent<Enemy>();
    }

    private void LateUpdate()
    {
        if (enemy == null || enemy.Body == null)
            return;

        Vector3 look = enemy.Body.linearVelocity;
        look.y = 0f;

        if (enemy.IsChasing && enemy.Target != null)
        {
            Vector3 toTarget = enemy.Target.position - enemy.transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 9f || look.sqrMagnitude < 0.04f)
                look = toTarget;
        }

        if (look.sqrMagnitude < 0.04f)
            return;

        Quaternion target = Quaternion.LookRotation(look.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, TurnSpeed * Time.deltaTime);
    }
}
