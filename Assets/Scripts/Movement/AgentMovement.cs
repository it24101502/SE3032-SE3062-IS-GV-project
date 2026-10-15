using UnityEngine;

public class AgentMovement : MonoBehaviour
{
    public Transform[] pathPoints;

    [Min(0f)] public float moveSpeed = 2f;
    [Min(0f)] public float turnSpeed = 180f;
    [Min(0.001f)] public float arrivalDistance = 0.05f;

    public bool isAlive = true;
    public bool movementEnabled = true;
    public bool loopPath = false;

    private int currentPoint = 0;

    private void Update()
    {
        if (!isAlive || !movementEnabled)
            return;

        if (pathPoints == null || pathPoints.Length == 0)
            return;

        if (currentPoint >= pathPoints.Length)
            return;

        Transform target = pathPoints[currentPoint];

        if (target == null)
            return;

        if (Vector3.Distance(transform.position, target.position)
            <= arrivalDistance)
        {
            currentPoint++;

            if (currentPoint >= pathPoints.Length)
            {
                if (loopPath)
                    currentPoint = 0;
                else
                    return;
            }

            target = pathPoints[currentPoint];

            if (target == null)
                return;
        }

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
    }
}