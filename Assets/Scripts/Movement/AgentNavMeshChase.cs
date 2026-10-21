using System;
using UnityEngine;
using UnityEngine.AI;

// Produces paths; AgentMovement remains the only component moving the root.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(AgentMovement))]
public sealed class AgentNavMeshChase : MonoBehaviour
{
    public Transform target;
    [Min(0.1f)] public float repathInterval = 0.5f;
    [Min(0.1f)] public float stoppingDistance = 1.5f;
    [Tooltip("NavMesh floor to capsule root; 1.05 for the supplied centered capsule.")]
    [Min(0f)] public float rootHeightOffset = 1.05f;
    [Min(0.1f)] public float sampleDistance = 2f;
    [Min(0f)] public float maximumHorizontalSampleOffset = 0.5f;
    [SerializeField] private bool hasCompletePath;

    private AgentMovement movement;
    private SpawnDirector owner;
    private PlayerStats targetStats;
    private NavMeshPath path;
    private float nextRepathTime;
    private bool suppliedPath;

    private void Awake()
    {
        movement = GetComponent<AgentMovement>();
        path = new NavMeshPath();
    }

    public void Initialize(Transform player, SpawnDirector director)
    {
        target = player;
        targetStats = target != null ? target.GetComponent<PlayerStats>() : null;
        owner = director;
        nextRepathTime = 0f;
        movement.loopPath = false;
        movement.planarMovement = false;
    }

    private void Update()
    {
        if (target == null || !target.gameObject.activeInHierarchy ||
            !movement.isAlive || !movement.movementEnabled ||
            (targetStats != null && targetStats.IsDead) ||
            (owner != null && (!owner.gameplayActive || !owner.playerAlive)))
        {
            ClearSuppliedPath();
            nextRepathTime = 0f;
            return;
        }

        Vector3 offset = target.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude <= stoppingDistance * stoppingDistance)
        {
            ClearSuppliedPath();
            nextRepathTime = 0f;
            return;
        }

        if (Time.time < nextRepathTime)
            return;
        nextRepathTime = Time.time + Mathf.Max(0.1f, repathInterval);

        Vector3 feet = transform.position - Vector3.up * rootHeightOffset;
        if (!TrySample(feet, out Vector3 start) ||
            !TrySample(target.position, out Vector3 destination) ||
            !NavMesh.CalculatePath(start, destination, NavMesh.AllAreas, path) ||
            path.status != NavMeshPathStatus.PathComplete)
        {
            ClearSuppliedPath();
            return;
        }

        Vector3[] corners = path.corners;
        if (corners.Length < 2)
        {
            ClearSuppliedPath();
            return;
        }

        // Skip the first corner (our present location), retaining floor height.
        Vector3[] positions = new Vector3[corners.Length - 1];
        for (int i = 1; i < corners.Length; i++)
            positions[i - 1] = corners[i] + Vector3.up * rootHeightOffset;

        movement.SetPositionPath(positions);
        suppliedPath = true;
        hasCompletePath = true;
    }

    private bool TrySample(Vector3 position, out Vector3 result)
    {
        result = Vector3.zero;
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit,
            Mathf.Max(0.1f, sampleDistance), NavMesh.AllAreas))
            return false;

        Vector3 offset = hit.position - position;
        offset.y = 0f;
        if (offset.magnitude > maximumHorizontalSampleOffset)
            return false;

        result = hit.position;
        return true;
    }

    private void ClearSuppliedPath()
    {
        hasCompletePath = false;
        if (!suppliedPath || movement == null)
            return;
        movement.SetPositionPath(Array.Empty<Vector3>());
        suppliedPath = false;
    }

    private void OnDisable()
    {
        ClearSuppliedPath();
    }
}
