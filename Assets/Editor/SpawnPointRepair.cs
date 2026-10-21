using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Explicit scene configuration tool. Never weakens the runtime spawn filters.
public static class SpawnPointRepair
{
    private struct Candidate
    {
        public Vector3 floor;
        public float pathLength;
    }

    [MenuItem("Agent Controller/Find Reachable Spawn Points")]
    public static void FindReachablePoints()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Stop Play mode before finding spawn points.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/MainScenes/MainLevel.unity")
        {
            Debug.LogError("Open MainLevel first. Nothing was changed.");
            return;
        }

        var directors = new List<SpawnDirector>();
        foreach (GameObject root in scene.GetRootGameObjects())
            directors.AddRange(root.GetComponentsInChildren<SpawnDirector>(true));
        if (directors.Count != 1 || directors[0].player == null ||
            (directors[0].rejectVisiblePoints && directors[0].playerCamera == null))
        {
            Debug.LogError("Assign one SpawnDirector, its Player and Camera first.");
            return;
        }

        SpawnDirector director = directors[0];
        if (!director.alignSpawnRootToNavMesh)
        {
            Debug.LogError("This tool expects Align Spawn Root To NavMesh enabled by the MainLevel setup. Nothing changed.");
            return;
        }
        if (!TryFloor(director.player.position, out Vector3 playerFloor))
        {
            Debug.LogError("Player is not near the baked NavMesh. Inspect the floor under Player. Nothing changed.");
            return;
        }

        Physics.SyncTransforms();
        DiagnoseOriginalPoints(director, playerFloor);

        var candidates = new List<Candidate>();
        var path = new NavMeshPath();
        // A deterministic bounded search on the player's own connected region.
        // Sampling a point inside a wall cannot pass clearance or a full path.
        float firstRadius = Mathf.Max(4f, director.minimumPlayerDistance + 1f);
        for (int ring = 0; ring < 6; ring++)
        {
            float radius = firstRadius + ring * 3f;
            for (int angleIndex = 0; angleIndex < 32; angleIndex++)
            {
                float angle = angleIndex * Mathf.PI * 2f / 32f;
                Vector3 point = playerFloor + new Vector3(
                    Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                AddCandidate(point, playerFloor, director, path, candidates);
            }
        }

        // Triangle centers cover irregular corridors that the rings can miss.
        NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
        int triangles = mesh.indices.Length / 3;
        int stride = Mathf.Max(1, Mathf.CeilToInt(triangles / 1024f));
        for (int triangle = 0; triangle < triangles; triangle += stride)
        {
            int index = triangle * 3;
            Vector3 point = (mesh.vertices[mesh.indices[index]] +
                mesh.vertices[mesh.indices[index + 1]] +
                mesh.vertices[mesh.indices[index + 2]]) / 3f;
            AddCandidate(point, playerFloor, director, path, candidates);
        }

        candidates.Sort((a, b) => Mathf.Abs(a.pathLength - director.preferredPlayerDistance)
            .CompareTo(Mathf.Abs(b.pathLength - director.preferredPlayerDistance)));
        var selected = new List<Candidate>();
        float separation = Mathf.Max(0.1f, director.minimumEnemySpacing);
        foreach (Candidate candidate in candidates)
        {
            bool tooClose = false;
            foreach (Candidate chosen in selected)
                if (HorizontalDistance(candidate.floor, chosen.floor) < separation)
                    tooClose = true;
            if (tooClose)
                continue;
            selected.Add(candidate);
            if (selected.Count == 4)
                break;
        }

        if (selected.Count == 0)
        {
            Debug.LogError($"[Spawn Point Search] Found only {selected.Count}/4 distinct points passing complete-route, capsule, distance and viewport checks. No markers or settings were changed. Keep the NavMesh diagnostic lines above and send them for review.", director);
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configure reachable spawn points");

        Transform parent = director.transform.Find("ValidatedSpawnPoints");
        if (parent == null)
        {
            var go = new GameObject("ValidatedSpawnPoints");
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Create validated spawn group");
            parent = go.transform;
            Undo.SetTransformParent(parent, director.transform, "Parent validated spawn group");
        }

        Transform[] points = new Transform[selected.Count];
        for (int i = 0; i < points.Length; i++)
        {
            string name = "SpawnPoint" + (i + 1);
            Transform point = parent.Find(name);
            if (point == null)
            {
                var go = new GameObject(name);
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Create reachable spawn point");
                point = go.transform;
                Undo.SetTransformParent(point, parent, "Parent reachable spawn point");
            }
            Undo.RecordObject(point, "Position reachable spawn point");
            point.position = selected[i].floor;
            point.rotation = Quaternion.identity;
            points[i] = point;
        }

        Undo.RecordObject(director, "Assign reachable spawn points");
        director.spawnPoints = points;
        EditorUtility.SetDirty(director);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = director.gameObject;
        Debug.Log("[Spawn Point Search] Assigned reachable, clear, separated floor markers outside the player's current viewport and minimum distance. Original level markers and geometry were preserved. Save with Ctrl+S and run Validate MainLevel Spawn Setup. Do not rerun initial setup: it assigns the original markers again.", director);
        SpawnDirectorIntegrationSetup.ValidateMainLevel();
    }

    private static void AddCandidate(Vector3 position, Vector3 playerFloor,
        SpawnDirector director, NavMeshPath path, List<Candidate> candidates)
    {
        // Do not use roofs or another storey to "repair" a ground-floor spawn.
        if (Mathf.Abs(position.y - playerFloor.y) > 1f ||
            HorizontalDistance(position, playerFloor) > 24f ||
            !TryFloor(position, out Vector3 floor) ||
            Mathf.Abs(floor.y - playerFloor.y) > 1f)
            return;

        foreach (Candidate candidate in candidates)
            if (Vector3.SqrMagnitude(candidate.floor - floor) < 0.25f)
                return;

        Vector3 center = floor + Vector3.up * director.spawnRootHeight;
        if (HorizontalDistance(center, director.player.position) < director.minimumPlayerDistance)
            return;
        if (director.rejectVisiblePoints)
        {
            Vector3 view = director.playerCamera.WorldToViewportPoint(center);
            if (view.z >= director.playerCamera.nearClipPlane &&
                view.z <= director.playerCamera.farClipPlane &&
                view.x >= 0f && view.x <= 1f && view.y >= 0f && view.y <= 1f)
                return;
        }

        float radius = Mathf.Max(0.01f, director.spawnRadius);
        float segment = Mathf.Max(radius * 2f, director.spawnHeight) * 0.5f - radius;
        if (Physics.CheckCapsule(center - Vector3.up * segment,
            center + Vector3.up * segment, radius,
            director.spawnBlockingLayers, QueryTriggerInteraction.Ignore))
            return;

        if (!NavMesh.CalculatePath(floor, playerFloor, NavMesh.AllAreas, path) ||
            path.status != NavMeshPathStatus.PathComplete)
            return;

        float length = 0f;
        Vector3[] corners = path.corners;
        for (int i = 1; i < corners.Length; i++)
            length += Vector3.Distance(corners[i - 1], corners[i]);
        candidates.Add(new Candidate { floor = floor, pathLength = length });
    }

    private static bool TryFloor(Vector3 position, out Vector3 floor)
    {
        floor = Vector3.zero;
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas) ||
            HorizontalDistance(position, hit.position) > 0.5f)
            return false;
        floor = hit.position;
        return true;
    }

    private static void DiagnoseOriginalPoints(SpawnDirector director, Vector3 playerFloor)
    {
        if (director.spawnPoints == null)
            return;
        foreach (Transform point in director.spawnPoints)
        {
            if (point == null)
                continue;
            if (!NavMesh.SamplePosition(point.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                Debug.Log($"[NavMesh Diagnostic] {point.name}: no NavMesh within 2 units of marker {point.position}.", point);
                continue;
            }
            float gap = HorizontalDistance(point.position, hit.position);
            var path = new NavMeshPath();
            bool calculated = NavMesh.CalculatePath(hit.position, playerFloor,
                NavMesh.AllAreas, path);
            Debug.Log($"[NavMesh Diagnostic] {point.name}: horizontal sample gap {gap:F2} (limit 0.50); route computed {calculated}, status {path.status}; marker {point.position}, sampled floor {hit.position}, player floor {playerFloor}.", point);
        }
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector3 offset = a - b;
        offset.y = 0f;
        return offset.magnitude;
    }
}
