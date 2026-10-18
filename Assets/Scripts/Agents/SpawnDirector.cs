using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class SpawnDirector : MonoBehaviour
{
    public enum DirectorPhase
    {
        BuildUp,
        Peak,
        Relax
    }

    [Header("Prototype Inputs - Set Manually")]
    public bool gameplayActive = true;
    public bool playerAlive = true;

    [Range(0f, 1f)] public float health01 = 1f;
    [Range(0f, 1f)] public float ammo01 = 1f;
    [Min(0)] public int nearbyEnemies = 0;
    [Min(1)] public int enemyCountForMaxStress = 5;

    [Header("Stress Settings")]
    [Min(0.01f)] public float smoothingSpeed = 2f;
    [Range(0f, 1f)] public float highStressThreshold = 0.75f;
    [Range(0f, 1f)] public float recoveryThreshold = 0.35f;

    [Header("Phase Durations - Seconds")]
    [Min(1f)] public float buildUpDuration = 20f;
    [Min(1f)] public float peakDuration = 10f;
    [Min(1f)] public float minimumRelaxDuration = 8f;

    [Header("Runtime Status - Observe During Play")]
    [SerializeField] private float targetStress;
    [SerializeField] private float smoothedStress;
    [SerializeField] private float phaseTimer;
    [SerializeField] private DirectorPhase currentPhase;

    public DirectorPhase CurrentPhase => currentPhase;
    public float Stress => smoothedStress;

    private void Start()
    {
        currentPhase = DirectorPhase.BuildUp;
        phaseTimer = 0f;
        targetStress = CalculateStress();
        smoothedStress = targetStress;
    }

    private void EvaluateDirector()
    {
        if (!gameplayActive || !playerAlive)
            return;

        targetStress = CalculateStress();

        float blend = 1f - Mathf.Exp(
            -Mathf.Max(0.01f, smoothingSpeed) * Time.deltaTime
        );

        smoothedStress = Mathf.Lerp(
            smoothedStress,
            targetStress,
            blend
        );

        phaseTimer += Time.deltaTime;

        if (currentPhase != DirectorPhase.Relax &&
            smoothedStress >= highStressThreshold)
        {
            ChangePhase(DirectorPhase.Relax);
            return;
        }

        switch (currentPhase)
        {
            case DirectorPhase.BuildUp:
                if (phaseTimer >= buildUpDuration)
                    ChangePhase(DirectorPhase.Peak);
                break;

            case DirectorPhase.Peak:
                if (phaseTimer >= peakDuration)
                    ChangePhase(DirectorPhase.Relax);
                break;

            case DirectorPhase.Relax:
                if (phaseTimer >= minimumRelaxDuration &&
                    smoothedStress <= recoveryThreshold)
                {
                    ChangePhase(DirectorPhase.BuildUp);
                }
                break;
        }
    }

    [Header("Spawning")]
    public GameObject enemyPrefab;
    public Transform[] spawnPoints;
    public Transform player;
    public Camera playerCamera;
    public bool rejectVisiblePoints = true;
    [Min(0f)] public float minimumPlayerDistance = 3f;
    [Min(0.1f)] public float preferredPlayerDistance = 6f;

    [Min(1)] public int maxActiveEnemies = 3;
    [Min(0.1f)] public float buildUpSpawnInterval = 4f;
    [Min(0.1f)] public float peakSpawnInterval = 2f;
    [Min(0.1f)] public float minimumEnemySpacing = 1.5f;

    [Header("Spawn Runtime Status")]
    [SerializeField] private int activeEnemyCount;
    [SerializeField] private float spawnTimer;
    [SerializeField] private float currentSpawnInterval;

    private readonly List<GameObject> spawnedEnemies =
        new List<GameObject>();

    private void Update()
    {
        spawnedEnemies.RemoveAll(
            enemy => enemy == null || !enemy.activeInHierarchy
        );

        activeEnemyCount = spawnedEnemies.Count;

        if (!gameplayActive || !playerAlive)
        {
            spawnTimer = 0f;
            return;
        }

        EvaluateDirector();

        float baseInterval = currentPhase == DirectorPhase.Peak
            ? peakSpawnInterval
            : buildUpSpawnInterval;

        currentSpawnInterval = Mathf.Max(
            0.1f,
            baseInterval * (1f + smoothedStress)
        );

        if (currentPhase == DirectorPhase.Relax ||
            activeEnemyCount >= maxActiveEnemies)
        {
            spawnTimer = 0f;
            return;
        }

        if (enemyPrefab == null ||
            spawnPoints == null ||
            spawnPoints.Length == 0)
        {
            spawnTimer = 0f;
            return;
        }

        spawnTimer += Time.deltaTime;

        if (spawnTimer < currentSpawnInterval)
            return;

        spawnTimer = 0f;
        TrySpawnEnemy();
    }

    private void TrySpawnEnemy()
    {
        if (player == null)
            return;

        if (rejectVisiblePoints && playerCamera == null)
            return;

        Transform bestPoint = null;
        float bestScore = float.NegativeInfinity;

        foreach (Transform point in spawnPoints)
        {
            if (point == null)
                continue;

            Vector3 playerOffset = point.position - player.position;
            playerOffset.y = 0f;

            float playerDistance = playerOffset.magnitude;

            if (playerDistance < minimumPlayerDistance)
                continue;

            if (rejectVisiblePoints && IsPointInCameraView(point.position))
                continue;
            if (!TryGetPathDistance(point.position, out float pathDistance))
                continue;

            bool occupied = false;
            float nearestEnemyDistance = float.PositiveInfinity;

            foreach (GameObject enemy in spawnedEnemies)
            {
                if (enemy == null)
                    continue;

                Vector3 enemyOffset =
                    point.position - enemy.transform.position;
                enemyOffset.y = 0f;

                float distance = enemyOffset.magnitude;

                if (distance < minimumEnemySpacing)
                {
                    occupied = true;
                    break;
                }

                nearestEnemyDistance = Mathf.Min(
                    nearestEnemyDistance,
                    distance
                );
            }

            if (occupied)
                continue;

            float preferredDistance = Mathf.Max(
                minimumPlayerDistance,
                preferredPlayerDistance
            );

            float distanceScore = 1f / (
                 1f + Mathf.Abs(pathDistance - preferredDistance)
            );

            float spacingScore = float.IsPositiveInfinity(
                nearestEnemyDistance
            )
                ? 1f
                : Mathf.Clamp01(
                    nearestEnemyDistance /
                    Mathf.Max(0.1f, preferredDistance)
                );

            float score = distanceScore * 0.7f + spacingScore * 0.3f;

            if (score > bestScore)
            {
                bestScore = score;
                bestPoint = point;
            }
        }

        if (bestPoint == null)
            return;

        GameObject newEnemy = Instantiate(
            enemyPrefab,
            bestPoint.position,
            bestPoint.rotation
        );

        newEnemy.SetActive(true);
        spawnedEnemies.Add(newEnemy);
        activeEnemyCount = spawnedEnemies.Count;

        Debug.Log(
            $"[SpawnDirector] Selected {bestPoint.name}, " +
            $"Score: {bestScore:F2}, Active: {activeEnemyCount}",
            this
        );
    }

    private bool IsPointInCameraView(Vector3 position)
    {
        Vector3 viewport = playerCamera.WorldToViewportPoint(position);

        return viewport.z >= playerCamera.nearClipPlane &&
               viewport.z <= playerCamera.farClipPlane &&
               viewport.x >= 0f && viewport.x <= 1f &&
               viewport.y >= 0f && viewport.y <= 1f;
    }

    private bool TryGetGroundPoint(Vector3 position, out Vector3 result)
    {
        result = Vector3.zero;

        if (!NavMesh.SamplePosition(
            position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            return false;
        }

        Vector3 offset = hit.position - position;
        offset.y = 0f;

        if (offset.magnitude > 0.5f)
            return false;

        result = hit.position;
        return true;
    }

    private bool TryGetPathDistance(Vector3 spawnPosition, out float distance)
    {
        distance = 0f;

        if (player == null)
            return false;

        if (!TryGetGroundPoint(spawnPosition, out Vector3 start))
            return false;

        if (!TryGetGroundPoint(player.position, out Vector3 destination))
            return false;

        NavMeshPath path = new NavMeshPath();

        if (!NavMesh.CalculatePath(
            start, destination, NavMesh.AllAreas, path))
        {
            return false;
        }

        if (path.status != NavMeshPathStatus.PathComplete)
            return false;

        Vector3[] corners = path.corners;

        for (int i = 1; i < corners.Length; i++)
            distance += Vector3.Distance(corners[i - 1], corners[i]);

        return true;
    }

    private float CalculateStress()
    {
        float healthStress = 1f - Mathf.Clamp01(health01);
        float ammoStress = 1f - Mathf.Clamp01(ammo01);

        float enemyStress = Mathf.Clamp01(
            nearbyEnemies / (float)Mathf.Max(1, enemyCountForMaxStress)
        );

        return Mathf.Clamp01(
            healthStress * 0.5f +
            ammoStress * 0.2f +
            enemyStress * 0.3f
        );
    }

    private void ChangePhase(DirectorPhase nextPhase)
    {
        currentPhase = nextPhase;
        phaseTimer = 0f;

        Debug.Log(
            $"[SpawnDirector] Phase: {currentPhase}, Stress: {smoothedStress:F2}",
            this
        );
    }
}