#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PetTownProjectSetup
{
    private const string ScenePath = "Assets/PetTown3D/Scenes/PetTown3D.unity";

    [MenuItem("Pet Town 3D/Open Playable Scene")]
    public static void OpenPlayableScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Pet Town 3D: Stop Play mode before opening the scene.");
            return;
        }

        // Never rely on an automatically generated scene: the original generator
        // could save an empty scene because the wrong scene was active.
        // Prompt before replacing unsaved changes in the current editor scene.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Open existing scene if it contains the actual game root.
        if (System.IO.File.Exists(ScenePath))
        {
            var existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (FindGameRoot(existing) != null)
            {
                Debug.Log("Pet Town 3D: playable scene opened successfully.");
                return;
            }
            Debug.LogWarning("Pet Town 3D: existing scene had no game root; repairing it.");
        }

        // Single mode avoids 'untitled scene unsaved' additive-scene errors.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        var root = new GameObject("PetTown3D_GAME");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<PetTownGame>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = root;
        Debug.Log("Pet Town 3D: repaired playable scene saved to " + ScenePath);
    }

    private static GameObject FindGameRoot(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponent<PetTownGame>() != null) return root;
        return null;
    }
}
#endif
