using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run explicitly in the existing MainLevel. Does not rebuild the bunker/NavMesh.
public static class SpawnDirectorIntegrationSetup
{
    private const string MainScene = "Assets/Scenes/MainScenes/MainLevel.unity";
    private const string BasePrefab = "Assets/Prefabs/Agents/EnemyAgent.prefab";
    private const string LightPrefab = "Assets/Prefabs/Agents/EnemyAgentLight.prefab";
    private const string HeavyPrefab = "Assets/Prefabs/Agents/EnemyAgentHeavy.prefab";

    [MenuItem("Agent Controller/Set Up Spawn Director in MainLevel")]
    public static void SetUpMainLevel()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Stop Play mode before running integration setup.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != MainScene)
        {
            Debug.LogError("Open " + MainScene + " first. No scene was changed.");
            return;
        }

        PlayerStats[] players = FindInScene<PlayerStats>(scene);
        GameFlow[] flows = FindInScene<GameFlow>(scene);
        SpawnDirector[] directors = FindInScene<SpawnDirector>(scene);
        if (players.Length != 1 || flows.Length != 1 || directors.Length > 1)
        {
            Debug.LogError("Expected one PlayerStats, one GameFlow, and at most one SpawnDirector. No scene was changed.");
            return;
        }

        PlayerStats player = players[0];
        PlayerThrower thrower = player.GetComponent<PlayerThrower>();
        Camera camera = player.GetComponentInChildren<Camera>();
        Transform[] markers = FindInScene<BunkerAgentMarker>(scene)
            .Where(m => m.kind == BunkerAgentMarker.Kind.SpawnCandidate &&
                m.gameObject.activeInHierarchy)
            .OrderBy(m => m.name, StringComparer.Ordinal)
            .Select(m => m.transform).ToArray();

        int obstacleLayer = LayerMask.NameToLayer("SpawnObstacle");
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab);
        if (thrower == null || camera == null || markers.Length != 4 ||
            obstacleLayer < 0 || source == null ||
            source.GetComponent<AgentMovement>() == null)
        {
            Debug.LogError("Missing player thrower/camera, four active SpawnCandidate markers, SpawnObstacle layer, or source movement prefab. No scene was changed.");
            return;
        }

        // Fail early if the baked navigation data is not available.
        if (NavMesh.CalculateTriangulation().vertices.Length == 0)
        {
            Debug.LogError("No loaded NavMesh. Enable the existing NavMesh Surface and verify its baked data, then run setup again.");
            return;
        }

        GameObject light = GetOrCreateVariant(source, LightPrefab, 30f, 2f);
        GameObject heavy = GetOrCreateVariant(source, HeavyPrefab, 60f, 1.4f);
        if (light == null || heavy == null)
            return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set up Spawn Director integration");

        SpawnDirector director;
        if (directors.Length == 0)
        {
            var root = new GameObject("SpawnDirector");
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create SpawnDirector");
            director = Undo.AddComponent<SpawnDirector>(root);
        }
        else
        {
            director = directors[0];
        }

        Undo.RecordObject(director, "Assign Spawn Director references");
        director.player = player.transform;
        director.playerCamera = camera;
        director.spawnPoints = markers;
        director.enemyPrefab = light;
        director.enemyTypes = new[]
        {
            new SpawnDirector.EnemyTypeOption
            {
                typeName = "Light", prefab = light,
                preferredPressure = 0.3f, peakOnly = false
            },
            new SpawnDirector.EnemyTypeOption
            {
                typeName = "Heavy", prefab = heavy,
                preferredPressure = 0.8f, peakOnly = true
            }
        };
        director.alignSpawnRootToNavMesh = true;
        director.spawnRootHeight = 1.05f;
        // Existing bunker walls and props use Default. The floor stays on its
        // original layer; the raised capsule clears it by 0.05 units or more.
        director.spawnBlockingLayers = (1 << 0) | (1 << obstacleLayer);
        director.rejectVisiblePoints = true;
        director.automaticEnemyCount = true;

        SpawnDirectorPlayerBridge bridge =
            director.GetComponent<SpawnDirectorPlayerBridge>();
        bool newBridge = bridge == null;
        if (newBridge)
            bridge = Undo.AddComponent<SpawnDirectorPlayerBridge>(director.gameObject);
        Undo.RecordObject(bridge, "Assign real player state");
        bridge.playerStats = player;
        bridge.playerThrower = thrower;
        bridge.gameFlow = flows[0];
        if (newBridge)
            bridge.rockAmmoCapacity = Mathf.Max(1, thrower.ammo);

        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(bridge);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = director.gameObject;

        Debug.Log("[Integration Setup] Configured MainLevel with four original spawn markers, real player/camera, Light/Heavy variants, and Default + SpawnObstacle clearance. Save the scene with Ctrl+S, then run Validate and Play-mode tests.", director);
        ValidateMainLevel();
    }

    private static GameObject GetOrCreateVariant(GameObject source,
        string path, float health, float speed)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            if (existing.GetComponent<EnemyHealth>() == null ||
                existing.GetComponent<AgentNavMeshChase>() == null)
            {
                Debug.LogError("Existing prefab at " + path + " lacks integration components. It was left unchanged.");
                return null;
            }
            return existing;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            instance.name = System.IO.Path.GetFileNameWithoutExtension(path);
            instance.SetActive(true);
            AgentMovement movement = instance.GetComponent<AgentMovement>();
            movement.pathPoints = Array.Empty<Transform>();
            movement.moveSpeed = speed;
            movement.loopPath = false;
            movement.planarMovement = false;
            movement.isAlive = true;
            movement.movementEnabled = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(movement);

            EnemyHealth damage = instance.GetComponent<EnemyHealth>();
            if (damage == null)
                damage = instance.AddComponent<EnemyHealth>();
            damage.maxHealth = health;
            AgentNavMeshChase chase = instance.GetComponent<AgentNavMeshChase>();
            if (chase == null)
                chase = instance.AddComponent<AgentNavMeshChase>();
            chase.rootHeightOffset = 1.05f;

            GameObject result = PrefabUtility.SaveAsPrefabAsset(instance, path);
            if (result == null)
                Debug.LogError("Could not save integration variant: " + path);
            return result;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [MenuItem("Agent Controller/Validate MainLevel Spawn Setup")]
    public static void ValidateMainLevel()
    {
        Scene scene = SceneManager.GetActiveScene();
        SpawnDirector[] directors = FindInScene<SpawnDirector>(scene);
        if (scene.path != MainScene || directors.Length != 1)
        {
            Debug.LogError("Open MainLevel and run Spawn Director setup first.");
            return;
        }

        SpawnDirector director = directors[0];
        var bridge = director.GetComponent<SpawnDirectorPlayerBridge>();
        if (bridge == null || bridge.playerStats == null ||
            bridge.playerThrower == null || director.player == null ||
            director.playerCamera == null || director.spawnPoints == null ||
            director.spawnPoints.Length == 0)
        {
            Debug.LogError("[Integration Validation] Missing scene references.", director);
            return;
        }

        if (!NavMesh.SamplePosition(director.player.position, out NavMeshHit playerHit,
            2f, NavMesh.AllAreas) || HorizontalDistance(
                director.player.position, playerHit.position) > 0.5f)
        {
            Debug.LogError("[Integration Validation] Player is not near the baked NavMesh.", director.player);
            return;
        }

        Physics.SyncTransforms();
        int reachable = 0;
        int clear = 0;
        foreach (Transform point in director.spawnPoints)
        {
            if (point == null)
                continue;
            var path = new NavMeshPath();
            bool valid = NavMesh.SamplePosition(point.position, out NavMeshHit hit,
                2f, NavMesh.AllAreas) && HorizontalDistance(point.position, hit.position) <= 0.5f &&
                NavMesh.CalculatePath(hit.position, playerHit.position, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete;
            if (!valid)
            {
                Debug.LogWarning("[Integration Validation] No complete route from " + point.name + ". The director will reject this point.", point);
                continue;
            }

            reachable++;
            Vector3 center = director.alignSpawnRootToNavMesh
                ? hit.position + Vector3.up * director.spawnRootHeight
                : point.position;
            float radius = Mathf.Max(0.01f, director.spawnRadius);
            float halfSegment = Mathf.Max(radius * 2f, director.spawnHeight) * 0.5f - radius;
            bool blocked = Physics.CheckCapsule(center - Vector3.up * halfSegment,
                center + Vector3.up * halfSegment, radius,
                director.spawnBlockingLayers, QueryTriggerInteraction.Ignore);
            if (blocked)
                Debug.LogWarning("[Integration Validation] Capsule clearance blocked at " + point.name + ".", point);
            else
                clear++;
        }

        string message = $"[Integration Validation] Complete routes: {reachable}/{director.spawnPoints.Length}; reachable and clear: {clear}/{director.spawnPoints.Length}. Visibility and player distance are checked at spawn time. Play-mode tests are still required.";
        if (clear == 0)
            Debug.LogError(message, director);
        else
            Debug.Log(message, director);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector3 offset = a - b;
        offset.y = 0f;
        return offset.magnitude;
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component
    {
        var list = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            list.AddRange(root.GetComponentsInChildren<T>(true));
        return list.ToArray();
    }
}
