using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Original CC0 Quaternius meshes/textures, normalized and assigned dedicated materials.
public static class PetTownForestBuilder
{
    const string Source="Assets/Stylized Nature MegaKit[Standard]/FBX (Unity)/";
    const string Root="Assets/PetTown3D/Resources/Environment/Forest";
    [MenuItem("PetTown/Art Direction/Build Asset Forest Library")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before authoring.");Directory.CreateDirectory(Root+"/Materials");AssetDatabase.Refresh();
        var library=AssetDatabase.LoadAssetAtPath<PetTownForestLibrary>(Root+"/Library.asset");if(!library){library=ScriptableObject.CreateInstance<PetTownForestLibrary>();AssetDatabase.CreateAsset(library,Root+"/Library.asset");}
        var trees=new List<GameObject>();var covers=new List<GameObject>();var materials=new Dictionary<string,Material>();
        foreach(string name in new[]{"Pine_1","Pine_3","Pine_5","CommonTree_1","CommonTree_3","Grass_Common_Short","Grass_Wispy_Short","Flower_3_Group","Flower_4_Group","Clover_1","Fern_1","Bush_Common","Bush_Common_Flowers","Rock_Medium_1"})
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Source+name+".fbx");if(!asset)throw new InvalidOperationException("Missing source "+name);var wrapper=new GameObject(name);var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,wrapper.transform);
            Bounds b=Bounds(wrapper);model.transform.localScale/=Mathf.Max(.001f,b.size.y);b=Bounds(wrapper);model.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);
            foreach(var r in wrapper.GetComponentsInChildren<Renderer>())
            {
                var values=r.sharedMaterials;for(int i=0;i<values.Length;i++)
                {
                    var old=values[i];bool grass=old.name.Contains("Grass"),bark=old.name.Contains("Bark");string key=old.name;
                    if(!materials.TryGetValue(key,out var material))
                    {
                        string path=Root+"/Materials/"+key+".mat";material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!material){material=new Material(Shader.Find("PetTown/Environment/AssetFoliage"));AssetDatabase.CreateAsset(material,path);}material.shader=Shader.Find("PetTown/Environment/AssetFoliage");material.mainTexture=old.mainTexture;material.color=grass?new Color(.82f,.94f,.80f):Color.white;material.SetFloat("_Cutoff",.25f);material.SetFloat("_AlphaClip",1);material.EnableKeyword("_ALPHATEST_ON");material.SetFloat("_WindStrength",bark ? 0 : grass ? .14f : .05f);material.SetFloat("_VertexAO",bark ? .35f : .6f);material.SetFloat("_MeadowGrass",grass?1:0);material.SetFloat("_UpNormals",grass ? .75f : 0);material.SetFloat("_FadeStart",0);material.SetFloat("_FadeEnd",0);material.enableInstancing=true;materials[key]=material;EditorUtility.SetDirty(material);
                    }
                    values[i]=material;
                }
                r.sharedMaterials=values;r.receiveShadows=true;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            }
            foreach(var c in wrapper.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
            var prefab=PrefabUtility.SaveAsPrefabAsset(wrapper,Root+"/"+name+".prefab");if(name.Contains("Tree")||name.StartsWith("Pine"))trees.Add(prefab);else covers.Add(prefab);UnityEngine.Object.DestroyImmediate(wrapper);
        }
        library.trees=trees.ToArray();library.groundCover=covers.ToArray();EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
    }
    static Bounds Bounds(GameObject root)
    {
        Bounds b=default;bool first=true;foreach(var mf in root.GetComponentsInChildren<MeshFilter>()){var mesh=mf.sharedMesh;if(!mesh)continue;var local=mesh.bounds;for(int i=0;i<8;i++){var p=mf.transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}}return b;
    }
}
