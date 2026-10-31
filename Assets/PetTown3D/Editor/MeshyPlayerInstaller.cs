#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class MeshyPlayerInstaller
{
    const string Root="Assets/PetTown3D/Resources/";
    const string Fbx=Root+"MeshyPlayer.fbx";
    const string Prefab=Root+"MeshyPlayer.prefab";
    const string Controller=Root+"MeshyRun.controller";
    const string MaterialPath=Root+"MeshyCharacter.mat";
    const string BaseName="Meshy_AI_Chibi_Schoolgirl_with_biped_texture_0";

    [MenuItem("Pet Town 3D/Install Meshy Player")]
    public static void Install()
    {
        var importer=AssetImporter.GetAtPath(Fbx) as ModelImporter;
        if(importer==null){EditorUtility.DisplayDialog("Pet Town", "MeshyPlayer.fbx not found. Copy all files first.","OK");return;}
        // Keep the source FBX intact. Import animation using Generic hierarchy.
        if(importer.animationType!=ModelImporterAnimationType.Generic || !importer.importAnimation)
        {
            importer.animationType=ModelImporterAnimationType.Generic;
            importer.importAnimation=true;
            importer.SaveAndReimport();
        }
        var albedo=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+BaseName+".png");
        var normalPath=Root+BaseName+"_normal.png";
        var normalImporter=AssetImporter.GetAtPath(normalPath) as TextureImporter;
        if(normalImporter!=null && normalImporter.textureType!=TextureImporterType.NormalMap)
        {
            normalImporter.textureType=TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
        }
        var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        var metallicSmooth=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Meshy_MetallicSmoothness.png");
        Shader shader=Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null)shader=Shader.Find("Standard");
        if(shader==null){EditorUtility.DisplayDialog("Pet Town", "No compatible Lit shader found.","OK");return;}
        var mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,MaterialPath);}
        else mat.shader=shader;
        mat.name="MeshyCharacter";
        mat.color=Color.white;
        if(mat.HasProperty("_BaseMap"))mat.SetTexture("_BaseMap",albedo);
        if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",Color.white);
        if(mat.HasProperty("_MainTex"))mat.SetTexture("_MainTex",albedo);
        if(mat.HasProperty("_BumpMap"))mat.SetTexture("_BumpMap",normal);
        if(normal!=null)mat.EnableKeyword("_NORMALMAP");
        if(metallicSmooth!=null)
        {
            if(mat.HasProperty("_MetallicGlossMap"))mat.SetTexture("_MetallicGlossMap",metallicSmooth);
            if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",1f);
            mat.EnableKeyword("_METALLICGLOSSMAP");
        }
        EditorUtility.SetDirty(mat);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if(source==null){EditorUtility.DisplayDialog("Pet Town", "FBX import failed.","OK");return;}
        var clips=AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>()
            .Where(c=>!c.name.StartsWith("__preview__") && !c.name.StartsWith("Take 001 (Read-Only)"))
            .ToArray();
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(Controller);
        if(!controller.parameters.Any(p=>p.name=="Moving"))controller.AddParameter("Moving",AnimatorControllerParameterType.Bool);
        var sm=controller.layers[0].stateMachine;
        foreach(var s in sm.states)sm.RemoveState(s.state);
        var idle=sm.AddState("Standing");
        var run=sm.AddState("Running");
        var runClip=clips.FirstOrDefault(c=>c.name.ToLowerInvariant().Contains("run")) ?? clips.FirstOrDefault();
        if(runClip!=null)run.motion=runClip;
        sm.defaultState=idle;
        var tr=idle.AddTransition(run);tr.hasExitTime=false;tr.duration=.12f;tr.AddCondition(AnimatorConditionMode.If,0,"Moving");
        var back=run.AddTransition(idle);back.hasExitTime=false;back.duration=.12f;back.AddCondition(AnimatorConditionMode.IfNot,0,"Moving");
        var obj=(GameObject)PrefabUtility.InstantiatePrefab(source);
        if(obj==null)obj=UnityEngine.Object.Instantiate(source);
        obj.name="MeshyPlayer";
        int count=0;
        foreach(var renderer in obj.GetComponentsInChildren<Renderer>(true))
        {
            if(!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))continue;
            var mats=renderer.sharedMaterials;
            for(int i=0;i<mats.Length;i++)mats[i]=mat;
            if(mats.Length==0)mats=new[]{mat};
            renderer.sharedMaterials=mats;
            count++;
        }
        var animator=obj.GetComponent<Animator>();
        if(animator==null)animator=obj.AddComponent<Animator>();
        animator.runtimeAnimatorController=controller;
        animator.applyRootMotion=false;
        PrefabUtility.SaveAsPrefabAsset(obj,Prefab);
        UnityEngine.Object.DestroyImmediate(obj);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Pet Town 3D", "Meshy player installed. Renderers: "+count+"; Animation clips: "+clips.Length+". Press Play to test. If mesh is distorted, use 'Inspect Meshy Without Animation' menu.","OK");
    }

    [MenuItem("Pet Town 3D/Inspect Meshy Without Animation")]
    public static void Inspect()
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if(source==null){EditorUtility.DisplayDialog("Pet Town","FBX not found.","OK");return;}
        var obj=(GameObject)PrefabUtility.InstantiatePrefab(source);
        if(obj==null)obj=UnityEngine.Object.Instantiate(source);
        obj.name="MESHY_DIAGNOSTIC_NO_ANIMATION";
        obj.transform.position=Vector3.zero;
        foreach(var animator in obj.GetComponentsInChildren<Animator>(true))animator.enabled=false;
        var mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(mat!=null)foreach(var r in obj.GetComponentsInChildren<Renderer>(true))
        {
            var mats=r.sharedMaterials;
            for(int i=0;i<mats.Length;i++)mats[i]=mat;
            r.sharedMaterials=mats;
        }
        Selection.activeGameObject=obj;
        SceneView.lastActiveSceneView?.FrameSelected();
        EditorUtility.DisplayDialog("Meshy Diagnostic", "A separate test model was placed in the Scene with Animator disabled. This does not modify the FBX or your player. Delete it after inspection.","OK");
    }
}
#endif
