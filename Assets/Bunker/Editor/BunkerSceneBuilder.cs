using UnityEditor;
using UnityEditor.SceneManagement;

public static class BunkerSceneBuilder
{
    [MenuItem("Bunker/Open expanded bunker scene")]
    public static void OpenScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene("Assets/Bunker/Scenes/AbandonedBunker.unity");
    }
}
