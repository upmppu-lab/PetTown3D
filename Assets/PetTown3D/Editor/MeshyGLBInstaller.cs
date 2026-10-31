#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class MeshyGLBInstaller
{
    const string Source = "Assets/PetTown3D/Models/MeshyPlayer_AllAnimations.glb";
    const string Prefab = "Assets/PetTown3D/Resources/MeshyPlayer.prefab";
    const string Controller = "Assets/PetTown3D/Resources/MeshyRun.controller";

    [MenuItem("Pet Town 3D/Install NEW GLB Player")]
    public static void Install()
    {
        CleanupPreviewObjects();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        if (source == null)
        {
            EditorUtility.DisplayDialog("GLB importer required",
                "GLB is not imported as a Unity GameObject yet. Install 'glTFast' in Window > Package Manager > Unity Registry (search glTFast), wait for import, then run this menu again. Do not use the old FBX installer.", "OK");
            return;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
        if (go == null) go = Object.Instantiate(source);
        go.name = "MeshyPlayer";
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        // GLB material and texture references are preserved; no FBX materials are reused.
        var clips = AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        var animator = go.GetComponentInChildren<Animator>(true);
        if (animator == null) animator = go.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.speed = 0f;
        if (clips.Length > 0)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(Controller);
            if (!controller.parameters.Any(p => p.name == "Moving"))
                controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            var sm = controller.layers[0].stateMachine;
            foreach (var state in sm.states) sm.RemoveState(state.state);
            var idle = sm.AddState("Idle");
            var run = sm.AddState("Running");
            run.motion = clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("running"))
                         ?? clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("walking"))
                         ?? clips[0];
            sm.defaultState = idle;
            var t = idle.AddTransition(run);
            t.hasExitTime = false; t.duration = 0.12f;
            t.AddCondition(AnimatorConditionMode.If, 0, "Moving");
            t = run.AddTransition(idle);
            t.hasExitTime = false; t.duration = 0.12f;
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(controller);
        }
        else
        {
            // Avoid reusing a controller from the old FBX, whose bone paths differ.
            animator.runtimeAnimatorController = null;
        }
        PrefabUtility.SaveAsPrefabAsset(go, Prefab);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Pet Town GLB",
            "GLB player prefab created. Imported animation clips: " + clips.Length +
            ". Press Play to see the new model. If animations = 0, the model can appear but animations require glTFast animation import support or a separate animation conversion.", "OK");
    }


    [MenuItem("Pet Town 3D/Clean Up Old Character Previews")]
    public static void CleanUpOldCharacterPreviews()
    {
        int removed=CleanupPreviewObjects();
        EditorUtility.DisplayDialog("Pet Town 3D", "Removed " + removed + " diagnostic preview object(s). The active game player and saved game were not deleted.", "OK");
    }

    static int CleanupPreviewObjects()
    {
        int removed=0;
        // Only remove known temporary preview objects created by our prior menus.
        string[] names={"GLB_DIAGNOSTIC_NO_ANIMATION","MESHY_DIAGNOSTIC_NO_ANIMATION","Meshy_DIAGNOSTIC_NO_ANIMATION","Meshy_Diagnostic_No_Animation"};
        var all=Resources.FindObjectsOfTypeAll<GameObject>();
        foreach(var obj in all)
        {
            if(obj==null || !obj.scene.IsValid() || !obj.scene.isLoaded)continue;
            if(obj.transform.parent!=null)continue;
            if(System.Array.IndexOf(names,obj.name)<0)continue;
            Undo.DestroyObjectImmediate(obj);
            removed++;
        }
        return removed;
    }

    [MenuItem("Pet Town 3D/Preview NEW GLB Without Animation")]
    public static void Preview()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        if (source == null) { EditorUtility.DisplayDialog("GLB", "Install glTFast first.", "OK"); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
        if (go == null) go = Object.Instantiate(source);
        go.name = "GLB_DIAGNOSTIC_NO_ANIMATION";
        go.transform.position = Vector3.zero;
        foreach (var a in go.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        Selection.activeGameObject = go;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        EditorUtility.DisplayDialog("GLB preview", "GLB preview object added. This does not modify your original model or player. Delete the diagnostic object after checking.", "OK");
    }
}
#endif
