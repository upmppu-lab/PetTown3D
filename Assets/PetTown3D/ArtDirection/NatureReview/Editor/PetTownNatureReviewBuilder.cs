using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Separate art review. Never touches the production scene, save data or source packages.
public static class PetTownNatureReviewBuilder
{
    public const string Root = "Assets/PetTown3D/ArtDirection/NatureReview";
    const string G = "Assets/3D set of stylized nature - GHIBLI style/Art/";
    const string A = "Assets/Anime Trees/";
    static System.Random rng;
    static readonly Dictionary<Material,Material> materials = new Dictionary<Material,Material>();
    static Transform trees, details, cover;
    static int plants;
    class Batch { public Mesh mesh; public Material material; public List<CombineInstance> instances=new List<CombineInstance>(); }
    static readonly Dictionary<string,Batch> batches = new Dictionary<string,Batch>();
    static float Range(float a,float b){return a+(float)rng.NextDouble()*(b-a);}
    static float PathX(float z){return -3.5f+5.5f*Mathf.Sin((z+8)*.061f)+2*Mathf.Sin(z*.13f);}
    static float Width(float z){return 2.0f+.20f*Mathf.Sin(z*.15f);}
    static float Height(float x,float z){float d=Mathf.Abs(x-PathX(z));return .28f*Mathf.Sin(z*.083f)+.19f*Mathf.Cos(x*.15f+z*.04f)+Mathf.SmoothStep(0,1,Mathf.Clamp01((d-5)/30))*(1.6f+.8f*Mathf.Sin(z*.047f));}
    static Material Copy(Material source)
    {
        if(materials.TryGetValue(source,out var found))return found;
        string name=source.name+"_"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)).Substring(0,8);
        string path=Root+"/Materials/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(source);AssetDatabase.CreateAsset(m,path);}else m.CopyPropertiesFromMaterial(source);
        m.enableInstancing=true;
        if(m.shader.name=="Shader Graphs/Leaves"){m.SetColor("_Color",new Color(.28f,.43f,.18f,.87f));m.SetColor("_EmissionColor",new Color(.32f,.47f,.21f));m.SetFloat("_EmissionPower",.2f);m.SetFloat("_ReceiveShadows",1);m.DisableKeyword("_RECEIVE_SHADOWS_OFF");}
        if(m.shader.name=="Shader Graphs/Grass"){
            m.SetColor("_Top_Color",new Color(.29f,.52f,.16f));m.SetColor("_Bottom_Color",new Color(.12f,.34f,.16f));
            m.SetColor("_Color_Variation",new Color(.33f,.58f,.24f));m.SetFloat("_Wind_Strength",.24f);m.SetFloat("_Wind_Speed",.7f);
        }
        if(m.shader.name.Contains("Grass_LOD")){
            m.SetColor("_TopColor",new Color(.39f,.58f,.20f));m.SetColor("_BottomColor",new Color(.14f,.36f,.15f));
            if(m.HasProperty("_WindStrength"))m.SetFloat("_WindStrength",.09f);
        }
        if(source.shader.name=="Shader Graphs/Grass" || source.shader.name.Contains("Grass_LOD")){
            m.shader=Shader.Find("PetTown/Review/LivingGrass");m.SetColor("_RootColor",new Color(.075f,.22f,.08f));m.SetColor("_TipColor",new Color(.28f,.50f,.14f));m.SetFloat("_Height",source.shader.name.Contains("Grass_LOD") ? .68f:.33f);m.SetFloat("_Wind",.08f);
        }
        materials[source]=m;EditorUtility.SetDirty(m);return m;
    }
    static GameObject Place(string path,Transform parent,Vector3 pos,float scale,float yaw)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!asset)throw new InvalidOperationException(path);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);
        go.transform.position=pos;go.transform.rotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*scale;
        foreach(var r in go.GetComponentsInChildren<Renderer>(true)){var ms=r.sharedMaterials;for(int i=0;i<ms.Length;i++)if(ms[i])ms[i]=Copy(ms[i]);r.sharedMaterials=ms;r.receiveShadows=true;r.shadowCastingMode=ShadowCastingMode.TwoSided;}
        foreach(var c in go.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
        return go;
    }
    static void Plant(string name,Vector3 pos,float scale,float yaw,bool anime=false)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>((anime?A+"Prefabs/":G+"Prefabs/")+name+".prefab");if(!asset)throw new InvalidOperationException(name);
        // Merge small actual source meshes in spatial chunks; no GameObject per grass blade.
        var filter=asset.GetComponentInChildren<MeshFilter>();var renderer=filter.GetComponent<Renderer>();
        var mat=Copy(renderer.sharedMaterial);var mesh=filter.sharedMesh;
        int cx=Mathf.FloorToInt(pos.x/8),cz=Mathf.FloorToInt(pos.z/8);
        string key=name+"_"+cx+"_"+cz;
        if(!batches.TryGetValue(key,out var batch)){batch=new Batch{mesh=mesh,material=mat};batches.Add(key,batch);}
        var trs=Matrix4x4.TRS(pos,Quaternion.Euler(0,yaw,0),Vector3.one*scale)*asset.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
        batch.instances.Add(new CombineInstance{mesh=mesh,subMeshIndex=0,transform=trs});plants++;
    }
    static void MeshObject(string name,Mesh mesh,Material mat,Transform parent)
    {
        string path=Root+"/Meshes/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(mesh,path);
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;
    }
    static Material Material(string name,string shader)
    {var m=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+name+".mat");if(!m){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,Root+"/Materials/"+name+".mat");}return m;}
    static void Ground(Transform root)
    {
        const int nx=193,nz=273;var v=new Vector3[nx*nz];var colors=new Color[v.Length];var tris=new int[(nx-1)*(nz-1)*6];int ti=0;
        for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){
            float wx=-48+x*.5f,wz=-24+z*.5f;int i=z*nx+x;v[i]=new Vector3(wx,Height(wx,wz),wz);
            float edge=Mathf.Abs(wx-PathX(wz))-Width(wz);colors[i]=new Color(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.35f,.65f,edge)),0,0,1);
            if(x==nx-1||z==nz-1)continue;tris[ti++]=i;tris[ti++]=i+nx;tris[ti++]=i+1;tris[ti++]=i+1;tris[ti++]=i+nx;tris[ti++]=i+nx+1;
        }
        var m=new Mesh{name="Contoured meadow and winding soil"};m.vertices=v;m.colors=colors;m.triangles=tris;m.RecalculateNormals();m.RecalculateBounds();
        var mat=Material("MeadowAndPath","PetTown/Review/ForestGround");mat.SetTexture("_Grass",AssetDatabase.LoadAssetAtPath<Texture2D>(G+"Textures/Grass_ground_Base_Color.png"));mat.SetTexture("_Soil",AssetDatabase.LoadAssetAtPath<Texture2D>(G+"Textures/Road_Base_Color.png"));EditorUtility.SetDirty(mat);
        MeshObject("MeadowAndPath",m,mat,root);
    }
    static void Mountains(Transform root)
    {
        for(int layer=0;layer<3;layer++){
            var v=new List<Vector3>();var tri=new List<int>();int rows=5,cols=41;
            for(int z=0;z<rows;z++)for(int x=0;x<cols;x++){
                float wx=-190+x*9.5f,wz=95+layer*34+z*17;
                float ridge=16+9*Mathf.Sin(x*.43f+layer)+7*Mathf.Sin(x*.91f+layer*2)+Range(-3,3);
                float wy=z==0?-3:ridge*Mathf.Sin(z*Mathf.PI/(rows-1));v.Add(new Vector3(wx,wy,wz));
                if(z==rows-1||x==cols-1)continue;int i=z*cols+x;tri.AddRange(new[]{i,i+cols,i+1,i+1,i+cols,i+cols+1});
            }
            var mesh=new Mesh{name="Distant ridge"};mesh.SetVertices(v);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();
            var mat=Material("Mountain_"+layer,"Universal Render Pipeline/Lit");mat.color=Color.Lerp(new Color(.23f,.40f,.33f),new Color(.49f,.67f,.67f),layer/2f);mat.SetFloat("_Smoothness",0);EditorUtility.SetDirty(mat);
            MeshObject("Mountain_"+layer,mesh,mat,root);
        }
    }
    [MenuItem("PetTown/Art Direction/Nature Review/Low View")]
    public static void LowView(){SetView(false);}
    [MenuItem("PetTown/Art Direction/Nature Review/High View")]
    public static void HighView(){SetView(true);}
    static void SetView(bool high)
    {
        if(SceneManager.GetActiveScene().path!=Root+"/NatureReview.unity")throw new InvalidOperationException("Open the separate NatureReview scene first.");
        var c=GameObject.Find("Nature Review Camera").GetComponent<Camera>();
        if(!EditorApplication.isPlaying)Undo.RecordObject(c.transform,"Nature review camera preset");
        c.orthographic=high;c.orthographicSize=20;c.fieldOfView=62;
        c.transform.position=high?new Vector3(25,46,8):new Vector3(4,3.6f,-15);c.transform.LookAt(high?new Vector3(0,0,40):new Vector3(-1,4.7f,40));
        if(!EditorApplication.isPlaying)EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
    }
    [MenuItem("PetTown/Art Direction/Build New Nature Review")]
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before authoring review.");
        Directory.CreateDirectory(Root+"/Meshes");Directory.CreateDirectory(Root+"/Materials");
        rng=new System.Random(61025);materials.Clear();batches.Clear();plants=0;
        var active=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try{
            SceneManager.SetActiveScene(scene);
            var root=new GameObject("Nature Review — imported Ghibli + Anime assets").transform;
            trees=new GameObject("Tree groves").transform;trees.SetParent(root);
            details=new GameObject("Flowers, shrubs and stone").transform;details.SetParent(root);
            cover=new GameObject("Grass carpet — spatial mesh batches").transform;cover.SetParent(root);
            Ground(root);Mountains(root);
            // Irregular groves around a clear, gently curved walking corridor.
            for(int iz=0;iz<17;iz++)for(int ix=0;ix<15;ix++){
                float z=-4+iz*6.3f+Range(-2.2f,2.2f),x=-44+ix*6.3f+Range(-2.2f,2.2f);
                float d=Mathf.Abs(x-PathX(z));if(d<(z<45?14f:11f) || (d<19&&z<28&&rng.NextDouble()<.45))continue;
                float keepTree=Range(0,1);if(keepTree>.5f)continue;bool anime=rng.NextDouble()<.62;string n=anime?"Tree_0"+rng.Next(2,9):"Tree_0"+new[]{1,3,4,4,5}[rng.Next(5)];
                float scale=anime?Range(.60f,.86f):Range(1.05f,1.65f);
                Place((anime?A+"Prefabs/":G+"Prefabs/")+n+".prefab",trees,new Vector3(x,Height(x,z)-.04f,z),scale,Range(0,360));
            }
            // A few closer silhouettes frame the view, without covering the path.
            Place(G+"Prefabs/Tree_04.prefab",trees,new Vector3(-13,Height(-13,7),7),1.1f,24);
            Place(A+"Prefabs/Tree_05.prefab",trees,new Vector3(19,Height(19,21),21),.85f,130);
            // About half of the first review's 185 trees, distributed across the same grove area.
            var originalTrees=new List<Transform>();foreach(Transform t in trees)originalTrees.Add(t);
            int removeCount=Mathf.Max(0,originalTrees.Count-93);
            for(int i=0;i<removeCount;i++){int index=Mathf.FloorToInt((i+.5f)*originalTrees.Count/removeCount);UnityEngine.Object.DestroyImmediate(originalTrees[index].gameObject);}
            for(int i=0;i<90;i++){
                float z=Range(4,100),x=PathX(z)+(rng.Next(2)==0?-1:1)*Range(6,35);
                Place((i%4==0?A+"Prefabs/Bush_0"+rng.Next(1,5):G+"Prefabs/Shrubs_0"+rng.Next(1,4))+".prefab",details,new Vector3(x,Height(x,z),z),i%4==0?Range(.22f,.42f):Range(.75f,1.35f),Range(0,360));
            }
            for(int i=0;i<85;i++){
                float z=Range(-14,88),x=PathX(z)+(rng.Next(2)==0?-1:1)*Range(2.8f,17);
                string rockPath=G+"Prefabs/Rock_"+rng.Next(1,12).ToString("00")+".prefab";
                var rock=AssetDatabase.LoadAssetAtPath<GameObject>(rockPath);var bounds=rock.GetComponentInChildren<Renderer>().bounds;
                float diameter=Range(.45f,1.45f),factor=diameter/Mathf.Max(bounds.size.x,bounds.size.z);
                float sign=x<PathX(z)?-1:1;x=PathX(z)+sign*Mathf.Max(Mathf.Abs(x-PathX(z)),Width(z)+diameter*.6f+.75f);
                Place(rockPath,details,new Vector3(x,Height(x,z)-.02f,z),factor,Range(0,360));
            }
            for(float z=-19;z<97;z+=.29f)for(float x=-38;x<38;x+=.29f){
                float wx=x+Range(-.12f,.12f),wz=z+Range(-.12f,.12f),d=Mathf.Abs(wx-PathX(wz));
                if(d<Width(wz)+Range(.05f,.6f))continue;
                float density=.80f+.18f*Mathf.PerlinNoise(wx*.07f,wz*.07f);if(rng.NextDouble()>density)continue;
                Plant("Gras_0"+rng.Next(1,4),new Vector3(wx,Height(wx,wz)-.02f,wz),Range(.95f,1.7f),Range(0,360));
            }
            // Flower drifts form colored groups rather than uniform random confetti.
            for(int bed=0;bed<25;bed++){
                float z=Range(-10,62),x=PathX(z)+(bed%2==0?-1:1)*Range(3.2f,11.5f);int species=new[]{1,2,3,4,5,8,9}[rng.Next(7)];
                if(bed<5){var centers=new[]{new Vector2(-9,-6),new Vector2(5,-7),new Vector2(10,4),new Vector2(-7,14),new Vector2(15,18)};x=centers[bed].x;z=centers[bed].y;species=new[]{5,5,3,1,4}[bed];}
                for(int i=0;i<Range(55,115);i++){
                    float a=Range(0,Mathf.PI*2),r=Mathf.Sqrt(Range(0,1))*Range(1.0f,2.8f),wx=x+Mathf.Cos(a)*r,wz=z+Mathf.Sin(a)*r;
                    if(Mathf.Abs(wx-PathX(wz))<Width(wz)+.45f)continue;
                    Plant("Flower_"+species.ToString("00")+"_"+(species>=8?2:3),new Vector3(wx,Height(wx,wz)+.01f,wz),species>=8?Range(.65f,.95f):Range(1.7f,2.5f),Range(0,360));
                }
            }
            // Taller Anime grass sits behind the small blades in sheltered banks.
            for(int i=0;i<650;i++){
                float z=Range(-8,80),x=PathX(z)+(i%2==0?-1:1)*Range(3.6f,14);
                Plant("GrassMesh",new Vector3(x,Height(x,z)-.04f,z),Range(.55f,.9f),Range(0,360),true);
            }
            var sourceMeshes=new List<Mesh>();var sourceMats=new List<Material>();
            using(var stream=File.Create(Root+"/PlantPlacements.bytes"))using(var writer=new BinaryWriter(stream)){
                writer.Write(0x50544e31);writer.Write(batches.Count);
                foreach(var pair in batches){var b=pair.Value;int index=-1;for(int i=0;i<sourceMeshes.Count;i++)if(sourceMeshes[i]==b.mesh&&sourceMats[i]==b.material){index=i;break;}
                    if(index<0){index=sourceMeshes.Count;sourceMeshes.Add(b.mesh);sourceMats.Add(b.material);}
                    if(b.instances.Count>1023)throw new InvalidOperationException("Review patch too large.");writer.Write(index);writer.Write(b.instances.Count);
                    foreach(var instance in b.instances)for(int k=0;k<16;k++)writer.Write(instance.transform[k]);
                }
            }
            AssetDatabase.ImportAsset(Root+"/PlantPlacements.bytes",ImportAssetOptions.ForceSynchronousImport);
            var field=cover.gameObject.AddComponent<PetTownNatureReviewCover>();field.meshes=sourceMeshes.ToArray();field.materials=sourceMats.ToArray();field.placementData=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/PlantPlacements.bytes");field.Reload();
            var camera=new GameObject("Nature Review Camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.transform.position=new Vector3(4,3.6f,-15);camera.transform.LookAt(new Vector3(-1,4.7f,40));camera.fieldOfView=62;camera.nearClipPlane=.1f;camera.farClipPlane=400;
            camera.clearFlags=CameraClearFlags.Skybox;camera.allowHDR=true;camera.allowMSAA=true;
            var sun=new GameObject("Warm afternoon sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(48,-32,0);sun.color=new Color(1,.95f,.84f);sun.intensity=1.15f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.72f;sun.shadowBias=.035f;sun.shadowNormalBias=.16f;
            RenderSettings.sun=sun;RenderSettings.skybox=Material("Painted sky","PetTown/Review/PaintedSky");RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.58f,.70f,.78f);RenderSettings.ambientEquatorColor=new Color(.42f,.51f,.43f);RenderSettings.ambientGroundColor=new Color(.27f,.35f,.25f);RenderSettings.ambientIntensity=1;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.64f,.78f,.79f);RenderSettings.fogStartDistance=45;RenderSettings.fogEndDistance=190;
            AssetDatabase.SaveAssets();string path=Root+"/NatureReview.unity";EditorSceneManager.SaveScene(scene,path);
            return path+"; trees="+trees.childCount+"; small plants="+plants+"; spatial batches="+batches.Count;
        }finally{SceneManager.SetActiveScene(active);EditorSceneManager.CloseScene(scene,true);materials.Clear();batches.Clear();}
    }
}