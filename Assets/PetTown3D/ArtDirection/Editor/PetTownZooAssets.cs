#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Adapters preserve the uploaded GLB; source parts are merged by native material.
public static class PetTownZooAssets
{
    const string Output="Assets/PetTown3D/ArtDirection/Prefabs/UserZoo/";
    static readonly Dictionary<Material,Material> palette=new Dictionary<Material,Material>();
    public static string Build()
    {
        Directory.CreateDirectory(Output);AssetDatabase.Refresh();palette.Clear();
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PetTown3D/ArtDirection/SourceModels/UserSavanna/SavannaPaddock.glb");
        if(!source)throw new System.InvalidOperationException("Uploaded savanna GLB not imported");
        var lib=PetTownCommunityLibrary.Current;System.Array.Resize(ref lib.animals,PetTownCommunityState.AnimalNames.Length);
        string[] names={"000-african-elephant-bull","001-reticulated-giraffe","002-plains-zebra","003-shade-shelter","004-hay-rack","005-enrichment-ball","006-timber-rail-straight"};
        string[] outputs={"AfricanElephant","ReticulatedGiraffe","PlainsZebra","ShadeShelter","HayRack","EnrichmentBall","ObservationRail"};
        float[] heights={2.35f,3.15f,1.35f,2.2f,.9f,.55f,1};
        for(int i=0;i<names.Length;i++)
        {
            Transform part=null;foreach(var child in source.GetComponentsInChildren<Transform>(true))if(child.name==names[i]){part=child;break;}
            if(!part)throw new System.InvalidOperationException("Missing uploaded part "+names[i]);
            var instance=Object.Instantiate(part.gameObject);var prefab=Flatten(instance,outputs[i],heights[i],i==6);Object.DestroyImmediate(instance);
            if(i<3)lib.animals[3+i]=new PetTownExhibitModel{prefab=prefab,height=heights[i]};
            if(i==3)lib.zooShelter=prefab;if(i==4)lib.zooHayRack=prefab;if(i==5)lib.zooEnrichment=prefab;if(i==6)lib.zooRail=prefab;
        }
        lib.zooCliff=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D set of stylized nature - GHIBLI style/Art/Prefabs/Rock_08.prefab");
        BuildGrass(lib);EditorUtility.SetDirty(lib);AssetDatabase.SaveAssets();return "Uploaded elephant, giraffe, zebra and four habitat props linked; original three species retained.";
    }
    static void BuildGrass(PetTownCommunityLibrary lib)
    {
        var sources=PetTownForestLibrary.Current.grasses;lib.meadowPatches=new GameObject[sources.Length];
        for(int i=0;i<sources.Length;i++)
        {
            var node=Object.Instantiate(sources[i]);node.name=sources[i].name+"_Readable";
            try
            {
                int index=0;foreach(var f in node.GetComponentsInChildren<MeshFilter>(true))
                {
                    var s=f.sharedMesh;if(!s)continue;var mesh=new Mesh{name=node.name+"_Mesh"};mesh.vertices=s.vertices;mesh.normals=s.normals;mesh.tangents=s.tangents;mesh.uv=s.uv;mesh.colors=s.colors;mesh.subMeshCount=s.subMeshCount;for(int sub=0;sub<s.subMeshCount;sub++)mesh.SetTriangles(s.GetTriangles(sub),sub);mesh.RecalculateBounds();
                    string path=Output+node.name+"_"+(index++)+".asset";var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(previous){EditorUtility.CopySerialized(mesh,previous);Object.DestroyImmediate(mesh);mesh=previous;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);f.sharedMesh=mesh;
                }
                lib.meadowPatches[i]=PrefabUtility.SaveAsPrefabAsset(node,Output+node.name+".prefab");
            }
            finally{Object.DestroyImmediate(node);}
        }
    }
    static Material Lit(Material source,bool rail)
    {
        if(!rail&&palette.TryGetValue(source,out var existing))return existing;
        string name=rail?"ZooObservationRail":source.name+"_UserSavanna";string path=Output+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        mat.shader=Shader.Find("Universal Render Pipeline/Lit");mat.name=name;
        Color color=source.HasProperty("baseColorFactor")?source.GetColor("baseColorFactor"):source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.HasProperty("_Color")?source.GetColor("_Color"):Color.white;
        mat.SetColor("_BaseColor",rail?new Color(.21f,.25f,.23f):color);mat.SetFloat("_Smoothness",rail?.22f:.12f);mat.SetFloat("_Metallic",rail?.35f:0);mat.enableInstancing=true;
        EditorUtility.SetDirty(mat);if(!rail)palette[source]=mat;return mat;
    }
    static GameObject Flatten(GameObject source,string name,float height,bool rail)
    {
        source.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var b=new Bounds();bool first=true;foreach(var r in source.GetComponentsInChildren<Renderer>(true)){if(first){b=r.bounds;first=false;}else b.Encapsulate(r.bounds);}
        float scale=height/Mathf.Max(.001f,b.size.y);var fit=Matrix4x4.Scale(Vector3.one*scale)*Matrix4x4.Translate(-new Vector3(b.center.x,b.min.y,b.center.z));
        var batches=new Dictionary<Material,List<CombineInstance>>();
        foreach(var f in source.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer=f.GetComponent<Renderer>();if(!f.sharedMesh||!renderer)continue;
            for(int sub=0;sub<f.sharedMesh.subMeshCount;sub++){var mat=Lit(renderer.sharedMaterials[sub],rail);if(!batches.TryGetValue(mat,out var list)){list=new List<CombineInstance>();batches.Add(mat,list);}list.Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=sub,transform=fit*f.transform.localToWorldMatrix});}
        }
        var node=new GameObject(name);int index=0;
        try
        {
            foreach(var batch in batches)
            {
                var mesh=new Mesh{name=name+"_Mesh"};mesh.CombineMeshes(batch.Value.ToArray(),true,true);mesh.RecalculateBounds();string path=Output+name+"_"+index+".asset";var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(previous){EditorUtility.CopySerialized(mesh,previous);Object.DestroyImmediate(mesh);mesh=previous;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
                var child=new GameObject("OriginalMesh_"+index++);child.transform.SetParent(node.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=batch.Key;
            }
            return PrefabUtility.SaveAsPrefabAsset(node,Output+name+".prefab");
        }
        finally{Object.DestroyImmediate(node);}
    }
}
#endif
