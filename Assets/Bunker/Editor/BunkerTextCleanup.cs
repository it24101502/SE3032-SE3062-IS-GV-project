using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Optional updater for a previously opened or edited bunker scene.
public static class BunkerTextCleanup
{
    [MenuItem("Bunker/Remove text from current scene")]
    public static void RemoveText()
    {
        Scene scene = SceneManager.GetActiveScene();
        int removed = 0;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Remove bunker text");
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item == null) continue;
                bool label = item.GetComponent<TextMesh>() != null;
                Renderer renderer = item.GetComponent<Renderer>();
                if (renderer != null)
                    foreach (Material material in renderer.sharedMaterials)
                        if (material != null && material.name.StartsWith("Sign ")) label = true;
                if (!label) continue;
                Undo.DestroyObjectImmediate(item.gameObject);
                removed++;
            }
        }
        Undo.CollapseUndoOperations(group);
        if (removed > 0) EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Removed " + removed + " bunker text objects. Save the scene to keep this change.");
    }
}
