#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PetTownEnvironmentComparisonBuilder
{
    [MenuItem("PetTown/Environment/Open Comparison Scene")]
    public static void Open(){if(EditorApplication.isPlaying){Debug.LogWarning("Stop Play before opening the environment comparison.");return;}if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EditorSceneManager.OpenScene("Assets/PetTown3D/EnvironmentTests/EnvironmentComparison.unity");}
    public static string Build()
    {
        var active=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PetTown3D/EnvironmentTests/EnvironmentComparisonSource.prefab");
            var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);root.AddComponent<PetTownEnvironmentComparison>();
            var camera=new GameObject("Environment_Comparison_Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(35,38,-12);camera.transform.LookAt(new Vector3(0,0,25));camera.orthographic=true;camera.orthographicSize=27;camera.nearClipPlane=.1f;camera.farClipPlane=500;camera.backgroundColor=new Color(.68f,.83f,.91f);camera.clearFlags=CameraClearFlags.SolidColor;
            var light=new GameObject("Environment_Comparison_Sun").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(50,-30,0);light.intensity=1.1f;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.72f,.76f);
            string path="Assets/PetTown3D/EnvironmentTests/EnvironmentComparison.unity";EditorSceneManager.SaveScene(scene,path);return path;
        }
        finally{SceneManager.SetActiveScene(active);EditorSceneManager.CloseScene(scene,true);}
    }
}
#endif
