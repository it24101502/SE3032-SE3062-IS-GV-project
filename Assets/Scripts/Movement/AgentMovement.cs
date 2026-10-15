using UnityEngine;

public class AgentMovement : MonoBehaviour
{
    public Transform[] pathPoints;
    public float moveSpeed = 2f;
    public float turnSpeed = 180f;

    private int currentPoint = 0;

    private void Update()
    {
        if (pathPoints == null || pathPoints.Length == 0)
            return;

        Transform target = pathPoints[currentPoint];

        if (target == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            moveSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, target.position) < 0.05f)
        {
            currentPoint = (currentPoint + 1) % pathPoints.Length;
        }
    }
}