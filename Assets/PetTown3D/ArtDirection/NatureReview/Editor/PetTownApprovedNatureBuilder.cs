using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Production visual wrappers; source packages and the existing world remain authoritative.
public static class PetTownApprovedNatureBuilder
{
    const string Root="Assets/PetTown3D/Resources/Environment/ApprovedNature";
    const string Review="Assets/PetTown3D/ArtDirection/NatureReview";
    const string G="Assets/3D set of stylized nature - GHIBLI style/Art/Prefabs/";
    const string A="Assets/Anime Trees/Prefabs/";
    static readonly Dictionary<Material,Material> materials=new Dictionary<Material,Material>();
    [MenuItem("PetTown/Art Direction/Build Approved Nature Library")]
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before authoring nature assets.");
        Directory.CreateDirectory(Root+"/Materials");AssetDatabase.Refresh();materials.Clear();
        BuildCoverage();var coverage=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/MeadowCoverage.png");if(!coverage)throw new InvalidOperationException("Coverage import has not completed.");
        var lib=AssetDatabase.LoadAssetAtPath<PetTownForestLibrary>(Root+"/Library.asset");
        if(!lib){lib=ScriptableObject.CreateInstance<PetTownForestLibrary>();AssetDatabase.CreateAsset(lib,Root+"/Library.asset");}
        lib.approvedMeadow=true;
        lib.trees=new[]{Model(G+"Tree_01.prefab","Ghibli_Tree_01"),Model(A+"Tree_02.prefab","Anime_Tree_02"),Model(G+"Tree_03.prefab","Ghibli_Tree_03"),Model(A+"Tree_04.prefab","Anime_Tree_04"),Model(G+"Tree_04.prefab","Ghibli_Tree_04"),Model(A+"Tree_05.prefab","Anime_Tree_05"),Model(A+"Tree_07.prefab","Anime_Tree_07"),Model(G+"Tree_05.prefab","Ghibli_Tree_05")};
        lib.grasses=new[]{Model(G+"Gras_03.prefab","Ghibli_Grass_03"),Model(G+"Gras_02.prefab","Ghibli_Grass_02")};
        lib.flowers=new[]{Model(G+"Flower_05_3.prefab","Blue_Flowers"),Model(G+"Flower_01_3.prefab","White_Flowers"),Model(G+"Flower_03_3.prefab","Red_Flowers"),Model(G+"Flower_04_3.prefab","Yellow_Flowers")};
        lib.shrubs=new[]{Model(G+"Shrubs_01.prefab","Ghibli_Shrub"),Model(A+"Bush_01.prefab","Anime_Shrub")};
        lib.rocks=new[]{Model(G+"Rock_05.prefab","Ghibli_Shore_Stone")};
        var covers=new List<GameObject>();covers.AddRange(lib.grasses);covers.AddRange(lib.flowers);covers.AddRange(lib.shrubs);covers.AddRange(lib.rocks);lib.groundCover=covers.ToArray();
        string groundPath=Root+"/Materials/ApprovedGround.mat";var ground=AssetDatabase.LoadAssetAtPath<Material>(groundPath);
        var source=AssetDatabase.LoadAssetAtPath<Material>(Review+"/Materials/MeadowAndPath.mat");if(!source)throw new InvalidOperationException("Approved review ground is missing.");
        if(!ground){ground=new Material(source);AssetDatabase.CreateAsset(ground,groundPath);}else{ground.shader=source.shader;ground.CopyPropertiesFromMaterial(source);}
        lib.meadowMask=coverage;ground.SetTexture("_Coverage",coverage);ground.SetVector("_CoverageBounds",lib.meadowBounds);ground.SetFloat("_UseCoverage",1);
        ground.SetFloat("_PaintStrength",.30f);EditorUtility.SetDirty(ground);lib.groundMaterial=ground;
        EditorUtility.SetDirty(lib);AssetDatabase.SaveAssets();
        var savedGround=AssetDatabase.LoadAssetAtPath<Material>(groundPath);savedGround.SetTexture("_Coverage",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/MeadowCoverage.png"));EditorUtility.SetDirty(savedGround);AssetDatabase.SaveAssets();
        materials.Clear();return "Approved library: "+lib.trees.Length+" tree variants, "+covers.Count+" ground-cover variants. Existing tree count unchanged.";
    }
    static Texture2D BuildCoverage()
    {
        const int size=512;var centers=new List<Vector4>();var rng=new System.Random(81026);
        for(int x=0;x<10;x++)for(int z=0;z<10;z++){if(rng.NextDouble()>.45)continue;centers.Add(new Vector4(-99+x*22+(float)rng.NextDouble()*10-5,-99+z*22+(float)rng.NextDouble()*10-5,3+(float)rng.NextDouble()*3.5f,2.5f+(float)rng.NextDouble()*2.5f));}
        centers.Add(new Vector4(-52,0,5.8f,3.8f));centers.Add(new Vector4(-18,6,4.2f,5.2f));centers.Add(new Vector4(-40,-30,5,3.5f));
        int originals=centers.Count;for(int i=0;i<originals;i++){var c=centers[i];float sign=i%2==0?-1:1;centers.Add(new Vector4(c.x+c.z*.50f*sign,c.y+c.w*.35f,c.z*.64f,c.w*.58f));}
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true);var pixels=new Color[size*size];
        for(int z=0;z<size;z++)for(int x=0;x<size;x++){
            float wx=-110+x/(float)(size-1)*220,wz=-110+z/(float)(size-1)*220;
            float field=Mathf.PerlinNoise(wx*.037f+81,wz*.037f+19),edge=Mathf.PerlinNoise(wx*.33f+26,wz*.33f+43);
            float clearing=0;foreach(var c in centers){float dx=(wx-c.x)/c.z,dz=(wz-c.y)/c.w,d=Mathf.Sqrt(dx*dx+dz*dz)+(edge-.5f)*.60f;clearing=Mathf.Max(clearing,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1.12f,d)));}
            float sparse=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.27f,.49f,field));
            float density=Mathf.Lerp(.55f,.94f,sparse)*Mathf.Pow(1-clearing,2);
            pixels[z*size+x]=new Color(clearing,density,sparse*(1-clearing*.75f),field);
        }
        texture.SetPixels(pixels);texture.Apply();string path=Root+"/MeadowCoverage.png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;importer.sRGBTexture=false;importer.mipmapEnabled=false;importer.isReadable=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Material Copy(Material source)
    {
        if(materials.TryGetValue(source,out var m))return m;
        string name=source.name+"_"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)).Substring(0,8);
        var approved=AssetDatabase.LoadAssetAtPath<Material>(Review+"/Materials/"+name+".mat");if(!approved)throw new InvalidOperationException("Missing approved material "+name);
        string path=Root+"/Materials/"+name+".mat";m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(approved);AssetDatabase.CreateAsset(m,path);}else{m.shader=approved.shader;m.CopyPropertiesFromMaterial(approved);}
        if(m.shader.name=="PetTown/Review/LivingGrass"){m.SetColor("_RootColor",new Color(.14f,.27f,.12f));m.SetColor("_TipColor",new Color(.39f,.55f,.25f));}
        m.enableInstancing=true;EditorUtility.SetDirty(m);materials[source]=m;return m;
    }
    static GameObject Model(string sourcePath,string name)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);if(!source)throw new InvalidOperationException(sourcePath);
        var root=new GameObject(name);try{
            var child=(GameObject)PrefabUtility.InstantiatePrefab(source,root.transform);
            var bounds=Bounds(root);float factor=1/Mathf.Max(.001f,bounds.size.y);child.transform.localScale*=factor;bounds=Bounds(root);
            child.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            foreach(var r in root.GetComponentsInChildren<Renderer>(true)){var ms=r.sharedMaterials;for(int i=0;i<ms.Length;i++)if(ms[i])ms[i]=Copy(ms[i]);r.sharedMaterials=ms;r.receiveShadows=true;r.shadowCastingMode=ShadowCastingMode.TwoSided;}
            foreach(var c in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
            return PrefabUtility.SaveAsPrefabAsset(root,Root+"/"+name+".prefab");
        }finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    static Bounds Bounds(GameObject root)
    {
        Bounds bounds=default;bool first=true;
        foreach(var f in root.GetComponentsInChildren<MeshFilter>(true)){if(!f.sharedMesh)continue;var b=f.sharedMesh.bounds;for(int i=0;i<8;i++){var p=f.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}}
        return bounds;
    }
}