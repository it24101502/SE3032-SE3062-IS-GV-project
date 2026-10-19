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

    [Tooltip("Ignore target height on a flat floor. Disable for paths with elevation.")]
    public bool planarMovement = false;
    [Min(0f)] public float animationDamping = 0.1f;

    public event System.Action PathCompleted;

    [SerializeField] private Animator agentAnimator;

    private int currentPoint;
    private Vector3[] positionPath = new Vector3[0];
    private bool usePositionPath;

    private static readonly int SpeedParameter =
        Animator.StringToHash("Speed");

    private int PathLength => usePositionPath
        ? positionPath.Length
        : (pathPoints == null ? 0 : pathPoints.Length);

    private void Awake()
    {
        if (agentAnimator == null)
            agentAnimator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        Vector3 previousPosition = transform.position;

        MoveAlongPath();

        float actualSpeed = Time.deltaTime > 0f
            ? Vector3.Distance(previousPosition, transform.position)
                / Time.deltaTime
            : 0f;

        if (agentAnimator != null)
        {
            if (actualSpeed <= 0.001f)
            {
                agentAnimator.SetFloat(SpeedParameter, 0f);
            }
            else
            {
                agentAnimator.SetFloat(
                    SpeedParameter,
                    actualSpeed,
                    Mathf.Max(0f, animationDamping),
                    Time.deltaTime
                );
            }
        }
    }

    private bool TryGetTarget(out Vector3 targetPosition)
    {
        targetPosition = Vector3.zero;

        if (currentPoint < 0 || currentPoint >= PathLength)
            return false;

        if (usePositionPath)
        {
            targetPosition = positionPath[currentPoint];
        }
        else
        {
            Transform target = pathPoints[currentPoint];

            if (target == null)
                return false;

            targetPosition = target.position;
        }

        if (planarMovement)
            targetPosition.y = transform.position.y;

        return true;
    }

    private void MoveAlongPath()
    {
        if (!isAlive || !movementEnabled)
            return;

        if (!TryGetTarget(out Vector3 targetPosition))
            return;

        if (Vector3.Distance(transform.position, targetPosition)
            <= arrivalDistance)
        {
            currentPoint++;

            if (currentPoint >= PathLength)
            {
                if (loopPath)
                {
                    currentPoint = 0;
                }
                else
                {
                    PathCompleted?.Invoke();
                    return;
                }
            }

            if (!TryGetTarget(out targetPosition))
                return;
        }

        Vector3 direction = targetPosition - transform.position;
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
            targetPosition,
            moveSpeed * Time.deltaTime
        );
    }

    // Existing scene-waypoint input.
    public void SetPath(Transform[] newPath)
    {
        if (newPath != null)
        {
            foreach (Transform point in newPath)
            {
                if (point == null)
                {
                    Debug.LogWarning(
                        "Path contains a missing waypoint.", this
                    );
                    return;
                }
            }
        }

        pathPoints = newPath == null
            ? new Transform[0]
            : (Transform[])newPath.Clone();

        usePositionPath = false;
        currentPoint = 0;
    }

    // World-space positions supplied by a path producer.
    public void SetPositionPath(Vector3[] newPath)
    {
        if (newPath != null)
        {
            foreach (Vector3 point in newPath)
            {
                if (!IsFinite(point.x) ||
                    !IsFinite(point.y) ||
                    !IsFinite(point.z))
                {
                    Debug.LogWarning(
                        "Position path contains an invalid coordinate.",
                        this
                    );
                    return;
                }
            }
        }

        positionPath = newPath == null
            ? new Vector3[0]
            : (Vector3[])newPath.Clone();

        usePositionPath = true;
        currentPoint = 0;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [ContextMenu("Apply Inspector Path")]
    private void ApplyInspectorPath()
    {
        SetPath(pathPoints);
    }

#if UNITY_EDITOR
    [ContextMenu("Tests/Apply Position Array Test Path")]
    private void ApplyPositionArrayTestPath()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Run this test in Play mode.", this);
            return;
        }

        Vector3 start = transform.position;

        SetPositionPath(new Vector3[]
        {
            start,
            start + new Vector3(-3f, 0f, 0f),
            start + new Vector3(-3f, 0f, 3f),
            start + new Vector3(0f, 0f, 3f),
            start
        });

        Debug.Log(
            "[AgentMovement] Applied world-space position array.",
            this
        );
    }

    private int completionTestCount;

    [ContextMenu("Tests/Start Completion Event Test")]
    private void StartCompletionEventTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Run this test in Play mode.", this);
            return;
        }

        if (!isAlive || !movementEnabled)
        {
            Debug.LogWarning(
                "Enable Is Alive and Movement Enabled first.", this
            );
            return;
        }

        completionTestCount = 0;

        PathCompleted -= RecordTestCompletion;
        PathCompleted += RecordTestCompletion;

        loopPath = false;
        ApplyPositionArrayTestPath();
    }

    private void RecordTestCompletion()
    {
        completionTestCount++;

        Debug.Log(
            $"[Completion Test] Event count: {completionTestCount}",
            this
        );
    }

    [ContextMenu("Tests/Check Completion Event Count")]
    private void CheckCompletionEventCount()
    {
        if (!Application.isPlaying)
            return;

        if (completionTestCount == 1)
            Debug.Log("[Completion Test] PASS: event fired once.", this);
        else
            Debug.LogError(
                $"[Completion Test] Expected 1; got {completionTestCount}.",
                this
            );
    }
#endif
}
