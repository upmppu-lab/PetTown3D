#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Rebuildable adapters for downloaded CC0 models; no original pack is overwritten.
public static class PetTownParkDetailAssets
{
    const string Sources="Assets/PetTown3D/ArtDirection/SourceModels/";
    const string Output="Assets/PetTown3D/ArtDirection/Prefabs/ParkDetail/";
    static Material plants,props;
    public static string Build()
    {
        Directory.CreateDirectory(Output);AssetDatabase.Refresh();
        BuildPlantMaterials();
        var flowerNames=new[]{"Cornflowers_Big_Cluster_Blue","Cosmea_Cluster_Small_1","Flowering_Garlic_1","Gerbera_1_Red","Larkspur_1_Purple","Poppy_Single_Red"};
        var flowers=new List<GameObject>();
        foreach(var name in flowerNames)
        {
            var node=new GameObject(name);
            try{Source(name,node.transform);flowers.Add(PrefabUtility.SaveAsPrefabAsset(node,Output+name+".prefab"));}
            finally{Object.DestroyImmediate(node);}
        }
        var bed=new GameObject("TerracottaFlowerGarden");
        GameObject flowerBed;
        try
        {
            for(int i=0;i<3;i++)
            {
                var pot=Fit(Source("Planter_1_Terracotta",bed.transform),.42f);pot.transform.localPosition+=new Vector3((i-1)*.64f,0,0);
                var plant=Fit(Object.Instantiate(flowers[i*2],bed.transform),.65f);
                plant.transform.localPosition+=new Vector3((i-1)*.64f,.30f,0);
                var accent=Fit(Object.Instantiate(flowers[i*2+1],bed.transform),.35f);
                accent.transform.localPosition+=new Vector3((i-1)*.64f+.12f,.31f,-.14f);
            }
            flowerBed=PrefabUtility.SaveAsPrefabAsset(bed,Output+"TerracottaFlowerGarden.prefab");
        }
        finally{Object.DestroyImmediate(bed);}
        var gazebo=BuildGazebo();
        var fountain=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Sources+"PixorFountain/fountain.glb"));GameObject fountainAsset;
        try
        {
            fountain.name="CC0_Fountain";
            var palette=new Dictionary<Material,Material>();
            foreach(var renderer in fountain.GetComponentsInChildren<Renderer>(true))
            {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    if(!palette.TryGetValue(slots[i],out var converted))
                    {
                        converted=ConvertMaterial(slots[i],"Fountain_"+slots[i].name);
                        palette[slots[i]]=converted;
                    }
                    slots[i]=converted;
                }
                renderer.sharedMaterials=slots;
            }
            foreach(var animator in fountain.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            fountainAsset=PrefabUtility.SaveAsPrefabAsset(fountain,Output+"CC0_Fountain.prefab");
        }
        finally{Object.DestroyImmediate(fountain);}
        var art=PetTownSceneryLibrary.Current;art.kiosk=gazebo;art.parkFlowerBed=flowerBed;art.detailedFlowers=flowers.ToArray();
        art.fountainFlow=AssetDatabase.LoadAllAssetsAtPath(Sources+"PixorFountain/fountain.glb").OfType<AnimationClip>().First(x=>x.name=="Flow");
        var community=PetTownCommunityLibrary.Current;community.fountain=fountainAsset;
        EditorUtility.SetDirty(art);EditorUtility.SetDirty(community);AssetDatabase.SaveAssets();
        return "CC0 flower garden, fountain with original Flow clip, and gazebo with 2 renderers linked.";
    }
    static void BuildPlantMaterials()
    {
        string tex=Sources+"AssetQuestGardenDemo/Textures/";
        var color=new Texture2D(2,2,TextureFormat.RGBA32,false);var opacity=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            color.LoadImage(File.ReadAllBytes(tex+"Plants_Atlas_1_Basecolor.png"));opacity.LoadImage(File.ReadAllBytes(tex+"Plants_Atlas_1_Opacity.png"));
            if(color.width!=opacity.width||color.height!=opacity.height)throw new InvalidOperationException("Plant atlas channel sizes differ");
            var pixels=color.GetPixels32();var alpha=opacity.GetPixels32();
            for(int i=0;i<pixels.Length;i++)pixels[i].a=alpha[i].r;
            color.SetPixels32(pixels);color.Apply();File.WriteAllBytes(Output+"GardenPlants_RGBA.png",color.EncodeToPNG());
        }
        finally{Object.DestroyImmediate(color);Object.DestroyImmediate(opacity);}
        AssetDatabase.ImportAsset(Output+"GardenPlants_RGBA.png");
        var importer=(TextureImporter)AssetImporter.GetAtPath(Output+"GardenPlants_RGBA.png");importer.alphaIsTransparency=true;importer.maxTextureSize=1024;importer.mipmapEnabled=true;importer.SaveAndReimport();
        plants=NewLit("GardenPlants");plants.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Output+"GardenPlants_RGBA.png"));
        plants.SetFloat("_AlphaClip",1);plants.SetFloat("_Cutoff",.45f);plants.SetFloat("_Cull",0);plants.SetFloat("_Smoothness",.05f);
        plants.EnableKeyword("_ALPHATEST_ON");plants.renderQueue=2450;plants.doubleSidedGI=true;
        props=NewLit("GardenProps");props.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(tex+"Props_Basecolor.png"));props.SetFloat("_Smoothness",.12f);
        EditorUtility.SetDirty(plants);EditorUtility.SetDirty(props);
    }
    static Material NewLit(string name)
    {
        string path=Output+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(material,path);}
        material.enableInstancing=true;return material;
    }
    static Material ConvertMaterial(Material source,string name)
    {
        var result=NewLit(name);
        result.SetTexture("_BaseMap",source.HasProperty("baseColorTexture")?source.GetTexture("baseColorTexture"):source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):null);
        result.SetColor("_BaseColor",source.HasProperty("baseColorFactor")?source.GetColor("baseColorFactor"):source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):Color.white);
        result.SetFloat("_Smoothness",source.HasProperty("roughnessFactor")?1-source.GetFloat("roughnessFactor"):.15f);
        result.SetFloat("_Metallic",source.HasProperty("metallicFactor")?source.GetFloat("metallicFactor"):0);
        EditorUtility.SetDirty(result);return result;
    }
    static GameObject Source(string name,Transform parent)
    {
        var node=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Sources+"AssetQuestGardenDemo/FBX/"+name+".fbx"),parent);
        foreach(var renderer in node.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m&&m.name.Contains("Plants")?plants:props).ToArray();
        return node;
    }
    static Bounds BoundsOf(Transform root)
    {
        var result=new Bounds();bool first=true;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var b=filter.sharedMesh.bounds;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {var p=root.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
                if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}
        }
        return result;
    }
    static GameObject Fit(GameObject node,float height)
    {
        var parent=node.transform.parent;var wrapper=new GameObject("AlignedFit").transform;wrapper.SetParent(parent,false);node.transform.SetParent(wrapper,false);
        var b=BoundsOf(wrapper);float scale=height/Mathf.Max(.001f,b.size.y);wrapper.localScale=Vector3.one*scale;
        wrapper.localPosition=-new Vector3(b.center.x,b.min.y,b.center.z)*scale;
        return wrapper.gameObject;
    }
    static GameObject BuildGazebo()
    {
        var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Sources+"InnersceneGazebo/gazebo.glb"));
        var result=new GameObject("CC0_TimberGazebo");
        try
        {
            var filters=source.GetComponentsInChildren<MeshFilter>(true);var materials=filters.Select(x=>x.GetComponent<Renderer>().sharedMaterial).Distinct().ToArray();
            for(int i=0;i<materials.Length;i++)
            {
                var material=materials[i];var combine=filters.Where(x=>x.GetComponent<Renderer>().sharedMaterial==material).Select(x=>new CombineInstance{mesh=x.sharedMesh,transform=source.transform.worldToLocalMatrix*x.transform.localToWorldMatrix}).ToArray();
                var mesh=new Mesh{name="Gazebo_"+material.name};mesh.CombineMeshes(combine,true,true);mesh.RecalculateBounds();
                string path=Output+"Gazebo_"+material.name+".asset";var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(previous){EditorUtility.CopySerialized(mesh,previous);Object.DestroyImmediate(mesh);mesh=previous;}else AssetDatabase.CreateAsset(mesh,path);
                var part=new GameObject(material.name);part.transform.SetParent(result.transform,false);part.AddComponent<MeshFilter>().sharedMesh=mesh;
                part.AddComponent<MeshRenderer>().sharedMaterial=ConvertMaterial(material,"Gazebo_"+material.name);
            }
            return PrefabUtility.SaveAsPrefabAsset(result,Output+"CC0_TimberGazebo.prefab");
        }
        finally{Object.DestroyImmediate(source);Object.DestroyImmediate(result);}
    }
}
#endif
