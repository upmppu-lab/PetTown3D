#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PetTownFarmCropAssets
{
    public static string Rebuild()
    {
        var lib=PetTownFarmLibrary.Current;if(!lib)throw new System.Exception("Existing farm library missing");
        string root="Assets/FREE_VERSION_30_MODELS/FREE_VERSION_30_MODELS/GLB/";
        var carrot=AssetDatabase.LoadAssetAtPath<GameObject>(root+"Carrot Plant.glb");var corn=AssetDatabase.LoadAssetAtPath<GameObject>(root+"Corn Stalk.glb");var wheat=AssetDatabase.LoadAssetAtPath<GameObject>(root+"Wheat Crop.glb");var pumpkin=AssetDatabase.LoadAssetAtPath<GameObject>(root+"Pumpkin Plant.glb");var potato=AssetDatabase.LoadAssetAtPath<GameObject>(root+"Potato Plant.glb");
        if(!carrot||!corn||!wheat||!pumpkin||!potato)throw new System.Exception("Imported crop sources missing");
        lib.carrot=carrot;lib.corn=corn;lib.wheat=wheat;lib.pumpkin=pumpkin;lib.potato=potato;
        var icons=new Texture2D[PetTownFarmState.CropCount];string folder="Assets/PetTown3D/Resources/Environment/Farm/CropIcons";Directory.CreateDirectory(folder);
        for(int i=0;i<icons.Length;i++)icons[i]=Capture(lib.Crop(i),folder+"/Crop_"+i+".png");
        lib.cropIcons=icons;EditorUtility.SetDirty(lib);AssetDatabase.SaveAssetIfDirty(lib);return "Crop references and seven transparent URP icons ready";
    }
    static Texture2D Capture(GameObject source,string path)
    {
        // Source packs mix legacy and glTF shaders. Use local URP copies, never change imported materials.
        var scene=EditorSceneManager.NewPreviewScene();var owned=new List<Material>();RenderTexture target=null;Texture2D image=null;var previous=RenderTexture.active;
        try
        {
            var clone=Object.Instantiate(source);SceneManager.MoveGameObjectToScene(clone,scene);clone.transform.position=Vector3.zero;
            bool first=true;var bounds=new Bounds();
            foreach(var renderer in clone.GetComponentsInChildren<Renderer>())
            {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    var original=slots[i];if(!original)continue;
                    var color=original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):original.HasProperty("baseColorFactor")?original.GetColor("baseColorFactor"):original.color;
                    var texture=original.HasProperty("_BaseMap")?original.GetTexture("_BaseMap"):original.HasProperty("baseColorTexture")?original.GetTexture("baseColorTexture"):original.mainTexture;
                    var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",color);material.SetTexture("_BaseMap",texture);material.SetFloat("_Smoothness",.1f);material.SetFloat("_Cull",0);owned.Add(material);slots[i]=material;
                }
                renderer.sharedMaterials=slots;if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
            }
            if(first)throw new System.Exception("Crop has no renderer: "+source.name);
            var camera=new GameObject("CropIconCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(camera,scene);var cam=camera.GetComponent<Camera>();cam.scene=scene;cam.orthographic=true;cam.aspect=1;cam.allowHDR=false;cam.backgroundColor=Color.clear;cam.clearFlags=CameraClearFlags.SolidColor;
            cam.transform.position=bounds.center+new Vector3(5,4,-7).normalized*Mathf.Max(5,bounds.size.magnitude*2);cam.transform.LookAt(bounds.center);cam.farClipPlane=Mathf.Max(100,bounds.size.magnitude*5);
            float extent=0;for(int i=0;i<8;i++){var p=cam.transform.InverseTransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));extent=Mathf.Max(extent,Mathf.Max(Mathf.Abs(p.x),Mathf.Abs(p.y)));}cam.orthographicSize=extent*1.12f;
            for(int i=0;i<2;i++){var lamp=new GameObject("CropIconLight",typeof(Light));SceneManager.MoveGameObjectToScene(lamp,scene);var light=lamp.GetComponent<Light>();light.type=LightType.Directional;light.intensity=i==0?1.6f:.65f;lamp.transform.rotation=Quaternion.Euler(i==0?45:25,i==0?-35:145,0);}
            target=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);target.Create();cam.targetTexture=target;cam.Render();RenderTexture.active=target;image=new Texture2D(256,256,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());cam.targetTexture=null;
        }
        finally
        {
            RenderTexture.active=previous;if(image)Object.DestroyImmediate(image);if(target){target.Release();Object.DestroyImmediate(target);}EditorSceneManager.ClosePreviewScene(scene);foreach(var material in owned)Object.DestroyImmediate(material);
        }
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=256;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
#endif
