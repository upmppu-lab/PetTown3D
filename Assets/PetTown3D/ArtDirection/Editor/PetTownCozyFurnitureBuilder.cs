using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Authoring tools: saved mesh assets with bevels, swept wire, upholstery and open cages.
// No runtime primitive stand-ins. Original imported assets/materials are never overwritten.
public static class PetTownCozyFurnitureBuilder
{
    const string Root="Assets/PetTown3D/ArtDirection/CozyFurniture";
    const string Kay="Assets/KayKit_Furniture_Bits_1.0_FREE/KayKit_Furniture_Bits_1.0_FREE/Assets/fbx (unity)/";
    static int meshIndex;
    static string modelName;
    static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
    static Material oak,cream,teal,gold,fabric,coral,glass;

    [MenuItem("PetTown/Art Direction/Build Furniture Collection")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before authoring furniture.");
        foreach(var d in new[]{"Models","Meshes","Materials","Thumbnails"})Directory.CreateDirectory(Root+"/"+d);
        Directory.CreateDirectory("Assets/PetTown3D/Resources/UI/Cozy");AssetDatabase.Refresh();materials.Clear();
        oak=Mat("HoneyOak",new Color(.68f,.40f,.18f));cream=Mat("IvoryEnamel",new Color(.94f,.85f,.65f));teal=Mat("DustyTeal",new Color(.17f,.40f,.44f));gold=Mat("BrushedBrass",new Color(.79f,.56f,.23f));fabric=Mat("SoftCreamFabric",new Color(.83f,.72f,.51f));coral=Mat("CoralUpholstery",new Color(.81f,.35f,.29f));glass=Mat("ClearAquariumGlass",new Color(.40f,.78f,.82f,.16f));
        string lp="Assets/PetTown3D/Resources/UI/Cozy/Library.asset";var lib=AssetDatabase.LoadAssetAtPath<PetTownCozyLibrary>(lp);if(!lib){lib=ScriptableObject.CreateInstance<PetTownCozyLibrary>();AssetDatabase.CreateAsset(lib,lp);}lib.style=AssetDatabase.LoadAssetAtPath<PetTownArtPreviewData>("Assets/PetTown3D/ArtDirection/ReviewData.asset");lib.furniture.Clear();lib.woodFloor=WoodMaterial();lib.plaster=Mat("WarmLinenWalls",new Color(.80f,.67f,.48f));lib.timber=oak;lib.glass=glass;oak.color=Color.white;oak.mainTexture=lib.woodFloor.mainTexture;
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            Add(lib,scene,0,false,"계산대","업무",1.05f,Counter);
            Add(lib,scene,1,false,"펫 쿠션","휴식",.22f,()=>Bed(false));
            Add(lib,scene,2,false,"목재 진열대","진열",1.1f,()=>Imported(Kay+"cabinet_medium_decorated.fbx"));
            Add(lib,scene,3,false,"쿠션 의자","좌석",.9f,()=>Imported(Kay+"chair_A.fbx"));
            Add(lib,scene,4,false,"원목 테이블","좌석",.95f,()=>Imported(Kay+"table_medium.fbx"));
            Add(lib,scene,5,false,"초록 화분","장식",1.1f,Plant);
            Add(lib,scene,0,true,"사료 선반","진열",1.9f,Products);
            Add(lib,scene,1,true,"장난감 진열대","진열",2.05f,Toys);
            Add(lib,scene,2,true,"RAT 케이지","펫 시설",1.58f,RatCage);
            Add(lib,scene,3,true,"수족관","펫 시설",1.6f,Aquarium);
            Add(lib,scene,4,true,"강아지 쿠션","휴식",.36f,()=>Bed(false));
            Add(lib,scene,5,true,"고양이 쿠션","휴식",.3f,()=>Bed(true));
            Add(lib,scene,6,true,"새장","펫 시설",2.2f,BirdCage);
            Add(lib,scene,7,true,"고양이 펫타워","펫 시설",1.61f,CatTower);
            Add(lib,scene,8,true,"컴퓨터","업무",.48f,()=>Imported("Assets/PetTown3D/Resources/Furniture/Generated/DesktopComputer.prefab"));
            Add(lib,scene,9,true,"사료 진열대","진열",1.6f,Products);
            string[] models={"armchair_pillows","couch_pillows","table_small","table_low","cabinet_small_decorated","cabinet_medium_decorated","shelf_A_big","shelf_B_large_decorated","lamp_standing","lamp_table","cactus_medium_A","cactus_medium_B","rug_oval_A","rug_rectangle_stripes_A","pictureframe_standing_A","chair_B","chair_C","chair_stool","book_set","pillow_A"};
            string[] names={"푹신한 안락의자","쿠션 소파","작은 테이블","낮은 테이블","수납장","장식 서랍장","높은 원목 선반","상품 장식 선반","스탠드 조명","테이블 조명","둥근 선인장","긴 선인장","타원 러그","줄무늬 러그","탁상 액자","모던 의자","라운지 의자","스툴","책 묶음","작은 쿠션"};
            string[] cats={"좌석","좌석","좌석","좌석","진열","진열","진열","진열","조명","조명","장식","장식","장식","장식","장식","좌석","좌석","좌석","장식","휴식"};
            float[] heights={1.05f,1.05f,.7f,.5f,1.2f,1.3f,2,2,1.9f,.65f,.8f,.9f,.025f,.025f,.45f,.95f,.95f,.55f,.35f,.18f};
            for(int i=0;i<models.Length;i++){int idx=i;Add(lib,scene,10+i,true,names[i],cats[i],heights[i],()=>idx==6?DisplayRack(false):idx==7?Products():Imported(Kay+models[idx]+".fbx"));}
            EditorUtility.SetDirty(lib);AssetDatabase.SaveAssets();
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    public static void BuildPetPortraits()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before authoring portraits.");
        string[] paths={"pets/dog_1","pets/dog_2","pets/ferrets_black"};
        for(int i=0;i<paths.Length;i++){var prefab=Resources.Load<GameObject>(paths[i]);var root=UnityEngine.Object.Instantiate(prefab);root.transform.rotation=Quaternion.Euler(0,180,0);foreach(var animation in root.GetComponentsInChildren<Animation>())animation.Stop();Capture(root,"Assets/PetTown3D/Resources/UI/Cozy/Pet_"+i+".png");UnityEngine.Object.DestroyImmediate(root);}
        var lib=AssetDatabase.LoadAssetAtPath<PetTownCozyLibrary>("Assets/PetTown3D/Resources/UI/Cozy/Library.asset");lib.careIcons=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/PetTown3D/ArtDirection/MobileStyle/PetCareIcons.png");EditorUtility.SetDirty(lib);AssetDatabase.SaveAssets();
    }
    static void Add(PetTownCozyLibrary lib,Scene scene,int kind,bool detail,string title,string category,float height,Func<GameObject> factory)
    {
        modelName=(detail?"Detail_":"Furniture_")+kind;meshIndex=0;var root=factory();root.name=modelName;SceneManager.MoveGameObjectToScene(root,scene);
        foreach(var c in root.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
        var b=Bounds(root);float scale=height/Mathf.Max(.001f,b.size.y);root.transform.localScale*=scale;b=Bounds(root);root.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);
        // Bake normalization into a wrapper so local zero is the floor contact.
        var wrapper=new GameObject(modelName);SceneManager.MoveGameObjectToScene(wrapper,scene);root.transform.SetParent(wrapper.transform,true);b=Bounds(wrapper);
        var prefab=PrefabUtility.SaveAsPrefabAsset(wrapper,Root+"/Models/"+modelName+".prefab");var thumb=Capture(wrapper,Root+"/Thumbnails/"+modelName+".png");
        lib.furniture.Add(new PetTownCozyFurniture{kind=kind,detail=detail,onSurface=detail&&(kind==8||kind==19||kind==24||kind==28),title=title,category=category,height=height,footprint=new Vector2(Mathf.Max(.2f,b.size.x),Mathf.Max(.2f,b.size.z)),prefab=prefab,thumbnail=thumb});UnityEngine.Object.DestroyImmediate(wrapper);
    }
    static Bounds Bounds(GameObject root){var rr=root.GetComponentsInChildren<Renderer>();if(rr.Length==0)throw new InvalidOperationException("No model renderers: "+root.name);var result=new Bounds();bool first=true;foreach(var r in rr){var mf=r.GetComponent<MeshFilter>();var sk=r as SkinnedMeshRenderer;var local=mf&&mf.sharedMesh?mf.sharedMesh.bounds:sk!=null?sk.localBounds:r.localBounds;for(int i=0;i<8;i++){var p=r.transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}}return result;}
    static GameObject Imported(string path)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!source)throw new InvalidOperationException("Missing model: "+path);var root=(GameObject)PrefabUtility.InstantiatePrefab(source);root.transform.position=Vector3.zero;
        foreach(var r in root.GetComponentsInChildren<Renderer>()){var arr=r.sharedMaterials;for(int i=0;i<arr.Length;i++){var old=arr[i];if(!old)continue;string key="Imported_"+Hash128.Compute(AssetDatabase.GetAssetPath(old)+"|"+old.name);var m=Mat(key,old.HasProperty("_BaseColor")?old.GetColor("_BaseColor"):old.color);m.mainTexture=old.mainTexture;arr[i]=m;}r.sharedMaterials=arr;}return root;
    }
    static Material Mat(string name,Color color)
    {
        if(materials.TryGetValue(name,out var found))return found;string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Smoothness",.22f);
        if(color.a<.99f){m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;}materials[name]=m;EditorUtility.SetDirty(m);return m;
    }
    static Material WoodMaterial()
    {
        string path=Root+"/Materials/HoneyPlanks.png";int n=512;var t=new Texture2D(n,n,TextureFormat.RGB24,false);for(int y=0;y<n;y++)for(int x=0;x<n;x++){int row=y/64;int shift=row%2*128;int end=(x+shift)%256;float grain=Mathf.Sin(x*.09f+Mathf.Sin(y*.21f)*.7f)*.018f+Mathf.Sin(y*1.2f+x*.01f)*.013f;float shade=(row%3-1)*.035f;Color c=new Color(.66f+shade+grain,.40f+shade+grain,.20f+shade+grain);if(y%64<2||end<2)c*=.65f;else if(y%64<4)c*=1.09f;t.SetPixel(x,y,c);}t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);var mat=Mat("HoneyPlankFloor",Color.white);mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);return mat;
    }
    static GameObject Empty(string n)=>new GameObject(n);
    static void Part(Transform parent,string name,Mesh mesh,Material mat,Vector3 p,Quaternion? rotation=null)
    {
        var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localRotation=rotation??Quaternion.identity;string path=Root+"/Meshes/"+modelName+"_"+(meshIndex++)+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){old.Clear();old.vertices=mesh.vertices;old.triangles=mesh.triangles;old.uv=mesh.uv;old.normals=mesh.normals;old.RecalculateBounds();old.UploadMeshData(false);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=mat;
    }
    static Mesh Box(float w,float h,float d,float radius=.035f)
    {
        radius=Mathf.Min(radius,Mathf.Min(w,Mathf.Min(h,d))*.45f);var v=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();int n=32;
        for(int layer=0;layer<4;layer++){float y=layer==0?0:layer==1?radius:layer==2?h-radius:h;float inset=layer==0||layer==3?radius*.55f:0;float rr=Mathf.Max(.001f,radius-inset);for(int i=0;i<n;i++){int corner=i/8;float a=(corner*90+(i%8)/7f*90)*Mathf.Deg2Rad;float cx=corner==0||corner==3?w*.5f-radius:-w*.5f+radius;float cz=corner<2?d*.5f-radius:-d*.5f+radius;v.Add(new Vector3(cx+Mathf.Cos(a)*rr,y,cz+Mathf.Sin(a)*rr));uv.Add(new Vector2(i/(float)n,y/h));}}
        for(int l=0;l<3;l++)for(int i=0;i<n;i++){int a=l*n+i,b=l*n+(i+1)%n,c=a+n,e=b+n;tris.AddRange(new[]{a,c,b,b,c,e});}
        for(int cap=0;cap<2;cap++){int center=v.Count;v.Add(new Vector3(0,cap==0?0:h,0));uv.Add(new Vector2(.5f,.5f));int start=v.Count;for(int i=0;i<n;i++){var p=v[(cap==0?0:3*n)+i];v.Add(p);uv.Add(new Vector2(p.x/w+.5f,p.z/d+.5f));}for(int i=0;i<n;i++){int a=start+i,b=start+(i+1)%n;tris.AddRange(cap==0?new[]{center,a,b}:new[]{center,b,a});}}return Mesh(v,tris,uv);
    }
    static Mesh Lathe(Vector2[] profile,int segments=48)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();for(int j=0;j<profile.Length;j++)for(int i=0;i<=segments;i++){float a=i*2*Mathf.PI/segments;v.Add(new Vector3(Mathf.Cos(a)*profile[j].x,profile[j].y,Mathf.Sin(a)*profile[j].x));uv.Add(new Vector2(i/(float)segments,j/(float)(profile.Length-1)));}for(int j=0;j<profile.Length-1;j++)for(int i=0;i<segments;i++){int a=j*(segments+1)+i,b=a+segments+1;tr.AddRange(new[]{a,b,a+1,a+1,b,b+1});}return Mesh(v,tr,uv);
    }
    static Mesh Tube(Vector3[] path,float radius,int sides=8)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();for(int j=0;j<path.Length;j++){Vector3 dir=(path[Mathf.Min(j+1,path.Length-1)]-path[Mathf.Max(0,j-1)]).normalized;var side=Vector3.Cross(dir,Mathf.Abs(Vector3.Dot(dir,Vector3.up))>.95f?Vector3.right:Vector3.up).normalized;var up=Vector3.Cross(dir,side);for(int i=0;i<sides;i++){float a=i*2*Mathf.PI/sides;v.Add(path[j]+radius*(side*Mathf.Cos(a)+up*Mathf.Sin(a)));uv.Add(new Vector2(i/(float)sides,j/(float)path.Length));}}for(int j=0;j<path.Length-1;j++)for(int i=0;i<sides;i++){int a=j*sides+i,b=j*sides+(i+1)%sides;tr.AddRange(new[]{a,b,a+sides,b,b+sides,a+sides});}return Mesh(v,tr,uv);
    }
    static Mesh Mesh(List<Vector3> v,List<int> tr,List<Vector2> uv){foreach(var p in v)if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z))throw new InvalidOperationException("Invalid vertex in "+modelName);var m=new Mesh();m.SetVertices(v);m.SetTriangles(tr,0);m.SetUVs(0,uv);m.RecalculateNormals();m.RecalculateBounds();return m;}
    static void BoxPart(Transform p,string n,Vector3 pos,Vector3 size,Material mat,float bevel=.035f)=>Part(p,n,Box(size.x,size.y,size.z,bevel),mat,pos);
    static void Wire(Transform p,string n,Vector3 a,Vector3 b,Material mat,float radius=.009f)=>Part(p,n,Tube(new[]{a,b},radius),mat,Vector3.zero);
    static void Ring(Transform p,string n,float radius,float y,Material mat,float tube=.012f){var points=new Vector3[49];for(int i=0;i<49;i++)points[i]=new Vector3(Mathf.Cos(i*2*Mathf.PI/48)*radius,y,Mathf.Sin(i*2*Mathf.PI/48)*radius);Part(p,n,Tube(points,tube),mat,Vector3.zero);}
    static GameObject Counter()
    {
        var g=Empty("WelcomeCounter");var t=g.transform;BoxPart(t,"RoundedCabinet",Vector3.zero,new Vector3(2.45f,.88f,.84f),teal,.09f);BoxPart(t,"OakWorktop",new Vector3(0,.88f,0),new Vector3(2.6f,.12f,1),oak,.08f);
        for(int i=0;i<3;i++){BoxPart(t,"InsetFrontPanel",new Vector3(-.78f+i*.78f,.12f,-.43f),new Vector3(.69f,.63f,.035f),cream,.03f);Wire(t,"BrassHandle",new Vector3(-.9f+i*.78f,.7f,-.47f),new Vector3(-.67f+i*.78f,.7f,-.47f),gold,.018f);}BoxPart(t,"Plinth",new Vector3(0,0,0),new Vector3(2.5f,.09f,.9f),oak);
        var emblem=Quaternion.Euler(-90,0,0);Part(t,"PawMedallion",Lathe(new[]{new Vector2(0,0),new Vector2(.18f,0),new Vector2(.18f,.015f),new Vector2(0,.015f)},32),gold,new Vector3(0,.44f,-.48f),emblem);Part(t,"PawPad",Lathe(new[]{new Vector2(0,0),new Vector2(.065f,0),new Vector2(.065f,.012f),new Vector2(0,.012f)},24),teal,new Vector3(0,.42f,-.50f),emblem);for(int i=0;i<4;i++)Part(t,"PawToe",Lathe(new[]{new Vector2(0,0),new Vector2(.028f,0),new Vector2(.028f,.012f),new Vector2(0,.012f)},16),teal,new Vector3(-.085f+i*.055f,.51f+(i==1||i==2?.035f:0),-.50f),emblem);return g;
    }
    static GameObject Bed(bool cat)
    {
        var g=Empty("QuiltedPetBed");Part(g.transform,"RolledUpholsteredRim",Lathe(new[]{new Vector2(.28f,.01f),new Vector2(.40f,.03f),new Vector2(.44f,.09f),new Vector2(.42f,.19f),new Vector2(.35f,.22f),new Vector2(.29f,.17f),new Vector2(.28f,.01f)}),cat?teal:coral,Vector3.zero);
        Part(g.transform,"SoftInsetCushion",Lathe(new[]{new Vector2(0,.03f),new Vector2(.25f,.035f),new Vector2(.30f,.09f),new Vector2(.25f,.13f),new Vector2(0,.14f)}),fabric,Vector3.zero);Ring(g.transform,"StitchedPiping",.39f,.18f,cream,.007f);return g;
    }
    static GameObject CatTower()
    {
        var g=Empty("CatPlayTower");var t=g.transform;BoxPart(t,"BeveledOakBase",Vector3.zero,new Vector3(1.05f,.10f,.95f),oak,.08f);
        for(int j=0;j<2;j++){Vector3 p=new Vector3(j==0?-.28f:.29f,.1f,j==0?.17f:-.12f);Part(t,"SisalColumn",Lathe(new[]{new Vector2(.085f,0),new Vector2(.085f,j==0?1.22f:.66f)}),fabric,p);int turns=j==0?28:15;var helix=new Vector3[turns*12+1];for(int i=0;i<helix.Length;i++){float a=i*2*Mathf.PI/12;helix[i]=p+new Vector3(Mathf.Cos(a)*.086f,i/(float)(helix.Length-1)*(j==0?1.22f:.66f),Mathf.Sin(a)*.086f);}Part(t,"WrappedRope",Tube(helix,.006f,5),cream,Vector3.zero);}
        BoxPart(t,"MiddlePerch",new Vector3(.17f,.73f,0),new Vector3(.73f,.09f,.7f),oak,.08f);BoxPart(t,"PaddedTopPerch",new Vector3(-.22f,1.34f,.14f),new Vector3(.66f,.15f,.62f),fabric,.07f);
        // Cubby made from open-faced mesh panels, not a solid box with a painted hole.
        BoxPart(t,"CubbyFloor",new Vector3(.10f,.12f,-.08f),new Vector3(.64f,.07f,.6f),oak);
        BoxPart(t,"CubbyLeft",new Vector3(-.19f,.19f,-.08f),new Vector3(.065f,.43f,.6f),oak);BoxPart(t,"CubbyRight",new Vector3(.39f,.19f,-.08f),new Vector3(.065f,.43f,.6f),oak);BoxPart(t,"CubbyRoof",new Vector3(.10f,.60f,-.08f),new Vector3(.64f,.07f,.6f),oak);BoxPart(t,"CubbyBack",new Vector3(.10f,.19f,.20f),new Vector3(.64f,.43f,.055f),oak);BoxPart(t,"CubbyCushion",new Vector3(.1f,.2f,-.08f),new Vector3(.50f,.07f,.47f),fabric,.03f);
        var arch=new Vector3[21];for(int i=0;i<21;i++){float a=i*Mathf.PI/20;arch[i]=new Vector3(.1f+Mathf.Cos(a)*.25f,.35f+Mathf.Sin(a)*.25f,-.40f);}Part(t,"ArchedCubbyEntrance",Tube(arch,.035f,10),oak,Vector3.zero);
        Wire(t,"HangingToyCord",new Vector3(-.42f,1.32f,-.04f),new Vector3(-.42f,1.02f,-.04f),gold,.004f);Part(t,"HangingToy",Lathe(new[]{new Vector2(0,0),new Vector2(.06f,.04f),new Vector2(.065f,.09f),new Vector2(0,.14f)},24),coral,new Vector3(-.42f,.89f,-.04f));return g;
    }
    static GameObject BirdCage()
    {
        var g=Empty("BrassAviary");var t=g.transform;Part(t,"TurnedWoodStand",Lathe(new[]{new Vector2(0,0),new Vector2(.28f,.03f),new Vector2(.27f,.09f),new Vector2(.08f,.13f),new Vector2(.04f,.85f),new Vector2(.23f,.93f),new Vector2(.24f,1),new Vector2(0,1)}),oak,Vector3.zero);
        Ring(t,"AviaryBase",.30f,1,gold,.025f);Ring(t,"AviaryBelt",.30f,1.39f,gold);Ring(t,"AviaryShoulder",.30f,1.65f,gold);
        for(int i=0;i<22;i++){float a=i*2*Mathf.PI/22;var pts=new Vector3[12];for(int j=0;j<12;j++){float f=j/11f;float rad=f<.70f?.30f:Mathf.Cos((f-.70f)/.30f*Mathf.PI*.5f)*.30f;pts[j]=new Vector3(Mathf.Cos(a)*rad,1+f*.95f,Mathf.Sin(a)*rad);}Part(t,"SweptAviaryWire",Tube(pts,.007f,6),gold,Vector3.zero);}
        Wire(t,"WoodenPerch",new Vector3(-.24f,1.3f,0),new Vector3(.24f,1.3f,0),oak,.018f);Part(t,"TopFinial",Lathe(new[]{new Vector2(0,0),new Vector2(.045f,.05f),new Vector2(0,.11f)},24),gold,new Vector3(0,1.95f,0));return g;
    }
    static GameObject RatCage()
    {
        var g=Empty("SmallPetHabitat");var t=g.transform;BoxPart(t,"CabinetStand",Vector3.zero,new Vector3(.85f,.69f,.65f),oak,.055f);BoxPart(t,"HabitatTray",new Vector3(0,.69f,0),new Vector3(.91f,.12f,.71f),teal,.045f);BoxPart(t,"Bedding",new Vector3(0,.80f,0),new Vector3(.82f,.025f,.63f),fabric);
        for(int i=0;i<15;i++){float x=-.41f+i*.82f/14;Wire(t,"FrontWire",new Vector3(x,.82f,-.31f),new Vector3(x,1.42f,-.31f),gold,.005f);Wire(t,"BackWire",new Vector3(x,.82f,.31f),new Vector3(x,1.42f,.31f),gold,.005f);}
        for(int i=0;i<10;i++){float z=-.31f+i*.62f/9;Wire(t,"SideWire",new Vector3(-.41f,.82f,z),new Vector3(-.41f,1.42f,z),gold,.005f);Wire(t,"SideWire",new Vector3(.41f,.82f,z),new Vector3(.41f,1.42f,z),gold,.005f);Wire(t,"RoofWire",new Vector3(-.41f,1.42f,z),new Vector3(.41f,1.42f,z),gold,.005f);}
        for(int i=0;i<2;i++){BoxPart(t,"TopFrameRail",new Vector3(0,1.42f,i==0?-.33f:.33f),new Vector3(.90f,.055f,.04f),teal);BoxPart(t,"TopFrameRail",new Vector3(i==0?-.43f:.43f,1.42f,0),new Vector3(.04f,.055f,.70f),teal);}var wheel=Empty("ExerciseWheel");wheel.transform.SetParent(t,false);wheel.transform.localPosition=new Vector3(.18f,1.04f,.12f);wheel.transform.localRotation=Quaternion.Euler(90,0,0);Ring(wheel.transform,"ExerciseRim",.19f,0,teal,.025f);for(int i=0;i<7;i++){float a=i*Mathf.PI/7;Wire(wheel.transform,"WheelSpoke",Vector3.zero,new Vector3(Mathf.Cos(a)*.18f,0,Mathf.Sin(a)*.18f),cream,.008f);}BoxPart(t,"HideawayFloor",new Vector3(-.20f,.82f,.06f),new Vector3(.25f,.06f,.27f),oak);BoxPart(t,"HideawayRoof",new Vector3(-.20f,1.02f,.06f),new Vector3(.30f,.07f,.29f),oak);return g;
    }
    static GameObject Aquarium()
    {
        var g=Empty("LivingAquarium");var t=g.transform;BoxPart(t,"BlueCabinet",Vector3.zero,new Vector3(1.65f,.73f,.76f),teal,.05f);for(int i=0;i<2;i++)BoxPart(t,"CabinetDoor",new Vector3(-.4f+i*.8f,.11f,-.39f),new Vector3(.72f,.52f,.035f),oak,.025f);
        BoxPart(t,"TankBottomRim",new Vector3(0,.73f,0),new Vector3(1.72f,.075f,.83f),gold);for(int i=0;i<2;i++){BoxPart(t,"TankTopRail",new Vector3(0,1.5f,i==0?-.39f:.39f),new Vector3(1.72f,.065f,.05f),gold);BoxPart(t,"TankTopRail",new Vector3(i==0?-.83f:.83f,1.5f,0),new Vector3(.05f,.065f,.83f),gold);}
        BoxPart(t,"GlassFront",new Vector3(0,.80f,-.39f),new Vector3(1.6f,.70f,.012f),glass,.003f);BoxPart(t,"GlassBack",new Vector3(0,.80f,.39f),new Vector3(1.6f,.70f,.012f),glass,.003f);for(int i=0;i<2;i++)BoxPart(t,"GlassSide",new Vector3(i==0?-.80f:.80f,.80f,0),new Vector3(.012f,.70f,.78f),glass,.003f);
        BoxPart(t,"SandBed",new Vector3(0,.80f,0),new Vector3(1.59f,.045f,.76f),fabric);var kelp=Mat("AquaticLeaves",new Color(.16f,.48f,.26f));for(int i=0;i<8;i++){float x=-.7f+i*.2f;var points=new Vector3[8];for(int j=0;j<8;j++)points[j]=new Vector3(x+Mathf.Sin(j*.8f+i)*.045f,.84f+j*.055f,.22f);Part(t,"AquaticPlant",Tube(points,.012f,5),kelp,Vector3.zero);}
        var source=Resources.Load<GameObject>("pets/fish_1");if(source)for(int i=0;i<3;i++){var fish=new GameObject("Swimmer_"+i);fish.transform.SetParent(t,false);fish.transform.localPosition=new Vector3(-.45f+i*.45f,1.12f+i%2*.1f,0);var fi=(GameObject)PrefabUtility.InstantiatePrefab(source,fish.transform);fi.name="Model";var b=Bounds(fi);float scale=.19f/b.size.y;fi.transform.localScale*=scale;fi.transform.localPosition-=b.center*scale;}
        return g;
    }
    static GameObject Toys()
    {
        return DisplayRack(true);
    }
    static GameObject Plant()
    {
        var g=Empty("BroadLeafHouseplant");var clay=Mat("TerracottaPlanter",new Color(.65f,.32f,.15f));var green=Mat("HouseplantLeaves",new Color(.18f,.48f,.23f));green.SetFloat("_Cull",0);var vein=Mat("LeafVeins",new Color(.29f,.59f,.27f));
        Part(g.transform,"TurnedTerracottaPot",Lathe(new[]{new Vector2(.13f,0),new Vector2(.16f,.02f),new Vector2(.22f,.29f),new Vector2(.24f,.30f),new Vector2(.24f,.35f),new Vector2(.205f,.35f),new Vector2(.19f,.31f),new Vector2(.17f,.06f),new Vector2(.13f,0)}),clay,Vector3.zero);Part(g.transform,"PottingSoil",Lathe(new[]{new Vector2(0,0),new Vector2(.19f,0),new Vector2(.19f,.015f),new Vector2(0,.015f)},32),Mat("PottingSoil",new Color(.20f,.12f,.07f)),new Vector3(0,.31f,0));
        for(int i=0;i<11;i++)
        {
            float length=.43f+(i%4)*.075f;var q=Quaternion.Euler(15+i%4*11,i*137.5f,0);var vertices=new List<Vector3>();var uvs=new List<Vector2>();var triangles=new List<int>();
            for(int j=0;j<=12;j++){float t=j/12f;float width=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.7f)*.12f;for(int k=0;k<5;k++){float u=(k-2)*.5f;vertices.Add(new Vector3(width*u,length*t,Mathf.Sin(t*Mathf.PI*.85f)*.27f-Mathf.Abs(u)*.035f));uvs.Add(new Vector2(u*.5f+.5f,t));}}
            for(int j=0;j<12;j++)for(int k=0;k<4;k++){int a=j*5+k;triangles.AddRange(new[]{a,a+5,a+1,a+1,a+5,a+6});}Part(g.transform,"CurvedBroadLeaf",Mesh(vertices,triangles,uvs),green,new Vector3(0,.32f,0),q);
            var center=new Vector3[13];for(int j=0;j<13;j++){float t=j/12f;center[j]=q*new Vector3(0,length*t,Mathf.Sin(t*Mathf.PI*.85f)*.27f)+new Vector3(0,.322f,0);}Part(g.transform,"LeafMidrib",Tube(center,.003f,5),vein,Vector3.zero);
        }
        return g;
    }
    static GameObject DisplayRack(bool stocked)
    {
        var wrapper=Empty("OakDisplayRack");var t=wrapper.transform;BoxPart(t,"CreamBackPanel",new Vector3(0,.08f,.23f),new Vector3(1.45f,1.82f,.055f),cream);for(int i=0;i<2;i++)BoxPart(t,"OakSidePanel",new Vector3(i==0?-.74f:.74f,.08f,0),new Vector3(.085f,1.9f,.55f),oak);for(int i=0;i<4;i++)BoxPart(t,"RoundedOakShelf",new Vector3(0,.12f+i*.52f,0),new Vector3(1.52f,.065f,.58f),oak);BoxPart(t,"OakCrown",new Vector3(0,1.94f,0),new Vector3(1.60f,.10f,.62f),oak);for(int i=0;i<4;i++)BoxPart(t,"DisplayFoot",new Vector3(i<2?-.63f:.63f,0,i%2==0?-.18f:.18f),new Vector3(.13f,.15f,.13f),teal);
        if(stocked)for(int i=0;i<9;i++)
        {
            Vector3 p=new Vector3(-.47f+i%3*.47f,.20f+i/3*.52f,-.035f);
            if(i%3==1)Part(t,"DogChewBone",Lathe(new[]{new Vector2(0,0),new Vector2(.065f,.015f),new Vector2(.075f,.06f),new Vector2(.040f,.11f),new Vector2(.035f,.22f),new Vector2(.075f,.27f),new Vector2(.065f,.32f),new Vector2(0,.335f)},32),cream,p+new Vector3(.165f,.08f,0),Quaternion.Euler(0,0,90));
            else
            {
                var toyMat=Mat(i%3==0?"SkyBlueToy":"CoralToy",i%3==0?new Color(.19f,.52f,.72f):new Color(.86f,.32f,.24f));Part(t,"SoftPlayBall",Lathe(new[]{new Vector2(0,0),new Vector2(.08f,.025f),new Vector2(.125f,.09f),new Vector2(.13f,.14f),new Vector2(.10f,.23f),new Vector2(0,.265f)},32),toyMat,p);
                var seam=new Vector3[25];for(int j=0;j<25;j++){float a=j*Mathf.PI*2/24;seam[j]=p+new Vector3(Mathf.Cos(a)*.131f,.14f,Mathf.Sin(a)*.131f);}Part(t,"StitchedToySeam",Tube(seam,.005f,5),cream,Vector3.zero);
            }
        }return wrapper;
    }
    static GameObject Products()
    {
        var g=DisplayRack(false);g.name="PetFoodBoutiqueShelf";var t=g.transform;
        for(int i=0;i<9;i++)
        {
            var p=new Vector3(-.48f+i%3*.48f,.20f+i/3*.52f,-.04f);
            var color=i%3==0?new Color(.20f,.52f,.52f):i%3==1?new Color(.87f,.44f,.29f):new Color(.67f,.57f,.23f);
            var bag=Mat("FoodPacket_"+(i%3),color);
            BoxPart(t,"PaddedFoodBag",p,new Vector3(.31f,.37f,.22f),bag,.06f);
            BoxPart(t,"PacketCrimpedSeal",p+new Vector3(0,.36f,0),new Vector3(.32f,.035f,.16f),cream,.012f);
            BoxPart(t,"IvoryPacketLabel",p+new Vector3(0,.095f,-.115f),new Vector3(.22f,.19f,.008f),cream,.015f);
            Part(t,"PawLabelPad",Lathe(new[]{new Vector2(0,0),new Vector2(.04f,0),new Vector2(.04f,.008f),new Vector2(0,.008f)},16),bag,p+new Vector3(0,.17f,-.125f),Quaternion.Euler(-90,0,0));
            for(int j=0;j<3;j++)Part(t,"PawLabelToe",Lathe(new[]{new Vector2(0,0),new Vector2(.016f,0),new Vector2(.016f,.008f),new Vector2(0,.008f)},12),bag,p+new Vector3((j-1)*.038f,.225f+(j==1?.015f:0),-.125f),Quaternion.Euler(-90,0,0));
        }
        return g;
    }
    static Texture2D Capture(GameObject root,string path)
    {
        var scene=EditorSceneManager.NewPreviewScene();var go=UnityEngine.Object.Instantiate(root);SceneManager.MoveGameObjectToScene(go,scene);var camera=new GameObject("FurniturePortrait",typeof(Camera));SceneManager.MoveGameObjectToScene(camera,scene);var cam=camera.GetComponent<Camera>();cam.scene=scene;cam.orthographic=true;cam.backgroundColor=Color.clear;cam.clearFlags=CameraClearFlags.SolidColor;var b=Bounds(go);cam.transform.position=b.center+new Vector3(5,4,-7);cam.transform.LookAt(b.center);cam.aspect=1;float extent=0;for(int i=0;i<8;i++){var p=cam.transform.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));extent=Mathf.Max(extent,Mathf.Max(Mathf.Abs(p.x),Mathf.Abs(p.y)));}cam.orthographicSize=extent*1.13f;
        var lamp=new GameObject("Softbox",typeof(Light));SceneManager.MoveGameObjectToScene(lamp,scene);var light=lamp.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(45,-35,0);var rt=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);rt.Create();cam.targetTexture=rt;cam.Render();var prev=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(512,512,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();RenderTexture.active=prev;File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);EditorSceneManager.ClosePreviewScene(scene);AssetDatabase.ImportAsset(path);var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
