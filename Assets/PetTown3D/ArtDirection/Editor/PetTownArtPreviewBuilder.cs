using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Builds review assets only, never changes the existing game scene or save.
public static class PetTownArtPreviewBuilder
{
    const string Root="Assets/PetTown3D/ArtDirection";
    static Material kit;
    static readonly Dictionary<string,Material> mats=new Dictionary<string,Material>();
    [Serializable] class SavedHud { public int coins,day,population,rep,level,dogs,cats,bunnies; }

    [MenuItem("PetTown/Art Direction/Build First UI Review")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before building the isolated review scene.");
        foreach(var dir in new[]{"Art","Materials","Models","Thumbnails","Scenes","Prefabs"})Directory.CreateDirectory(Root+"/"+dir);
        AssetDatabase.Refresh();mats.Clear();
        var data=AssetDatabase.LoadAssetAtPath<PetTownArtPreviewData>(Root+"/ReviewData.asset");
        if(data==null){data=ScriptableObject.CreateInstance<PetTownArtPreviewData>();AssetDatabase.CreateAsset(data,Root+"/ReviewData.asset");}
        var snapshot=File.ReadAllText("C:/UnityProjects/PetTown3D_ArtDirectionBackup_20261025/Save_before.txt");var stats=JsonUtility.FromJson<SavedHud>(snapshot);
        var sourceGame=UnityEngine.Object.FindFirstObjectByType<PetTownGame>();if(!sourceGame)throw new InvalidOperationException("Open the original game scene to read its catalog before building review assets.");
        var sourceCatalog=(List<PetTownCatalogEntry>)typeof(PetTownGame).GetMethod("ConstructionCatalogEntries",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sourceGame,null);
        data.gold=stats.coins;data.day=stats.day;data.residents=stats.population;data.reputation=stats.rep;data.level=stats.level;data.pets=stats.dogs+stats.cats+stats.bunnies;
        data.iconAtlas=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/CozyIconAtlas.png");
        var atlasImporter=(TextureImporter)AssetImporter.GetAtPath(Root+"/Art/CozyIconAtlas.png");atlasImporter.textureType=TextureImporterType.Default;atlasImporter.npotScale=TextureImporterNPOTScale.None;atlasImporter.alphaIsTransparency=true;atlasImporter.mipmapEnabled=false;atlasImporter.textureCompression=TextureImporterCompression.Uncompressed;atlasImporter.maxTextureSize=2048;atlasImporter.SaveAndReimport();
        data.font=MakeFont();
        data.paper=Chrome("CreamPaper",new Color(.99f,.96f,.86f),new Color(.96f,.91f,.77f),new Color(.74f,.62f,.44f),false);
        data.header=Chrome("WalnutHeader",new Color(.39f,.31f,.24f),new Color(.25f,.20f,.17f),new Color(.52f,.41f,.30f),false);
        data.card=Chrome("IvoryCard",new Color(1,.99f,.95f),new Color(.98f,.95f,.85f),new Color(.83f,.75f,.59f),false);
        data.selectedCard=Chrome("MintSelection",new Color(.79f,1,.86f),new Color(.54f,.94f,.72f),new Color(.02f,.66f,.39f),false);
        data.greenButton=Chrome("EmeraldButton",new Color(.06f,.82f,.53f),new Color(.01f,.62f,.36f),new Color(.01f,.45f,.27f),false);
        data.lightButton=Chrome("PearlButton",new Color(1,.99f,.94f),new Color(.94f,.91f,.82f),new Color(.78f,.69f,.52f),false);
        data.coralButton=Chrome("CoralClose",new Color(1,.53f,.38f),new Color(.97f,.28f,.19f),new Color(.73f,.20f,.12f),false);
        data.shadow=Chrome("SoftShadow",new Color(.12f,.075f,.035f,.25f),new Color(.12f,.075f,.035f,.25f),Color.clear,true);
        data.circle=Chrome("PortraitFrame",new Color(1,.98f,.91f),new Color(.98f,.89f,.68f),new Color(.72f,.51f,.28f),false,120);
        kit=Material("KenneyPalette",Color.white,Root+"/SourceModels/FantasyTown/Textures/variation-a.png");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        try
        {
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.50f,.54f,.43f);
            var desc=new[]{"나만의 가구로 따뜻하게\n꾸밀 수 있는 집입니다.","새로운 주민이 머무는\n아늑한 주택입니다.","반려동물과 함께 쉬어 가는\n마을의 카페입니다.","마을의 반려동물을 돌보는\n펫 병원입니다.","꽃과 나무, 분수가 어우러진\n마을의 휴식 공간입니다.","산책길을 따라 맑은 물을\n즐기는 작은 호수입니다.","울타리와 쉼터가 있는\n동물들의 야외 공간입니다.","자유 곡선 또는 격자 직선으로\n이어 만드는 흙길입니다."};
            data.buildings=new PetTownArtBuilding[8];
            for(int i=0;i<8;i++)
            {
                var model=CreateModel(i+1);var path=Root+"/Models/Building_"+(i+1)+".prefab";var prefab=PrefabUtility.SaveAsPrefabAsset(model,path);var thumb=RenderThumbnail(model,i+1);UnityEngine.Object.DestroyImmediate(model);
                var actual=sourceCatalog.Find(e=>e.Key==i+1);if(actual==null)throw new InvalidOperationException("Missing actual catalog data: "+(i+1));
                data.buildings[i]=new PetTownArtBuilding{key=actual.Key,title=actual.Name,price=actual.Price,category=actual.Category,size=actual.SizeText,description=desc[i],model=prefab,thumbnail=thumb};
            }
            CreateVillage(data);
            var camera=new GameObject("DesignReviewCamera",typeof(Camera));camera.tag="MainCamera";var cam=camera.GetComponent<Camera>();cam.orthographic=true;cam.orthographicSize=15;cam.nearClipPlane=.1f;cam.farClipPlane=180;cam.backgroundColor=new Color(.61f,.82f,.78f);cam.transform.position=new Vector3(26,31,-35);cam.transform.LookAt(new Vector3(0,0,2));
            var sun=new GameObject("WarmSun",typeof(Light)).GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=1f;sun.color=new Color(1,.97f,.91f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-35,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.50f,.54f,.43f);RenderSettings.fog=false;
            var postPath=Root+"/Art/CozyPost.asset";var profile=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(postPath);if(!profile){profile=ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();AssetDatabase.CreateAsset(profile,postPath);var tone=profile.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);tone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);AssetDatabase.AddObjectToAsset(tone,profile);var color=profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);color.saturation.Override(-6);AssetDatabase.AddObjectToAsset(color,profile);}
            var volume=new GameObject("CozyColorGrade",typeof(UnityEngine.Rendering.Volume)).GetComponent<UnityEngine.Rendering.Volume>();volume.isGlobal=true;volume.sharedProfile=profile;cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing=true;
            var ui=new GameObject("PetTownNewDesignUI").AddComponent<PetTownArtPreviewUI>();ui.data=data;ui.placementCamera=cam;ui.Build();
            PrefabUtility.SaveAsPrefabAsset(ui.gameObject,Root+"/Prefabs/CozyHudAndBuildingCatalog.prefab");
            EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene,Root+"/Scenes/FirstUIReview.unity");
        }
        finally { EditorSceneManager.CloseScene(scene,true); }
    }
    static TMP_FontAsset MakeFont()
    {
        var path=Root+"/Art/CozyKoreanFont.asset";var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(font)return font;
        font=TMP_FontAsset.CreateFontAsset(Resources.Load<Font>("Fonts/NanumSquareB"),64,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
        font.name="CozyKoreanFont";AssetDatabase.CreateAsset(font,path);foreach(var tex in font.atlasTextures)AssetDatabase.AddObjectToAsset(tex,font);AssetDatabase.AddObjectToAsset(font.material,font);font.TryAddCharacters("PET TOWN 3D0123456789,./×+− ▾전체주거생활야외도로집주민택펫카페병원공호수동물흙길건설탐험꾸미기가구방관리캐릭터명성골드재고매장크기배치보기선택검색이름가격순기본항목나만의따뜻한새로운아늑반려함께쉬어는마을돌보꽃과무분어우러진휴식간산책따라맑은즐작울타쉼있들의자유곡또격직으이어만드일디자인검토용별씬입니다원래저변경되지않습니닫취소화면확인후제합니다우리하루",out _);EditorUtility.SetDirty(font);return font;
    }
    static Sprite Chrome(string name,Color top,Color bottom,Color edge,bool shadow,float radius=25)
    {
        var path=Root+"/Art/"+name+".png";int n=256;var tex=new Texture2D(n,n,TextureFormat.RGBA32,false);
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float px=Mathf.Abs(x-127.5f)-(119-radius),py=Mathf.Abs(y-127.5f)-(119-radius);float dist=new Vector2(Mathf.Max(px,0),Mathf.Max(py,0)).magnitude+Mathf.Min(Mathf.Max(px,py),0)-radius;
            Color c=Color.Lerp(bottom,top,y/255f);float alpha=Mathf.Clamp01(.5f-dist);
            if(shadow){c=top;alpha=Mathf.Clamp01((9-dist)/14f);}
            else if(dist>-3)c=edge;else if(dist>-5)c=new Color(1,.99f,.9f);else if(dist>-8)c=Color.Lerp(edge,c,.6f);
            else if(y>217)c=Color.Lerp(c,Color.white,(y-217)/38f*.19f);
            c.a*=alpha;tex.SetPixel(x,y,c);
        }
        tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(path);
        var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.spriteBorder=new Vector4(42,42,42,42);imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Material Material(string name,Color color,string texture=null)
    {
        if(mats.TryGetValue(name,out var m))return m;var path=Root+"/Materials/"+name+".mat";m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Smoothness",.18f);if(texture!=null)m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texture);mats[name]=m;EditorUtility.SetDirty(m);return m;
    }
    static GameObject Module(string name,Transform parent,Vector3 p,float yaw=0,Vector3? scale=null)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/SourceModels/FantasyTown/Models/"+name+".fbx");if(!prefab)throw new InvalidOperationException("Missing module: "+name);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.name=name;go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=scale??Vector3.one;
        foreach(var r in go.GetComponentsInChildren<Renderer>()){var arr=r.sharedMaterials;for(int i=0;i<arr.Length;i++)arr[i]=kit;r.sharedMaterials=arr;}return go;
    }
    static GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 p,Vector3 scale,Material mat)
    {
        var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    static GameObject Nature(string name,Transform parent,Vector3 p,float height)
    {
        if(name.StartsWith("CommonTree"))
        {
            var sourceTree=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ithappy/Fantasy_FREE/Prefabs/tree_001.prefab");var tree=(GameObject)PrefabUtility.InstantiatePrefab(sourceTree,parent);var renderers=tree.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);float treeFactor=height/bounds.size.y;tree.transform.localScale*=treeFactor;tree.transform.position=p-new Vector3(bounds.center.x*treeFactor,bounds.min.y*treeFactor,bounds.center.z*treeFactor);
            tree.transform.localScale=Vector3.Scale(tree.transform.localScale,new Vector3(1.4f,1,1.4f));
            foreach(var r in renderers){var a=r.sharedMaterials;for(int i=0;i<a.Length;i++){var key="CozyTree_"+a[i].name;var original=a[i];var m=Material(key,new Color(.90f,.96f,.73f));m.mainTexture=original.mainTexture;m.SetFloat("_Smoothness",0);a[i]=m;}r.sharedMaterials=a;}return tree;
        }
        var source=Resources.Load<GameObject>("Environment/"+name);var go=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);var rr=go.GetComponentsInChildren<Renderer>();var b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);float factor=height/Mathf.Max(.001f,b.size.y);go.transform.localScale*=factor;go.transform.position=p-new Vector3(b.center.x*factor,b.min.y*factor,b.center.z*factor);return go;
    }
    static GameObject CreateModel(int kind)
    {
        var root=new GameObject("NewArt_Building_"+kind);var t=root.transform;
        var grass=Material("SoftGrass",new Color(.55f,.72f,.30f));var soil=Material("WarmPath",new Color(.72f,.55f,.32f));
        if(kind<=4)
        {
            int levels=kind==3?1:2;
            Primitive(PrimitiveType.Cube,"GardenBase",t,new Vector3(0,-.12f,0),new Vector3(5.7f,.24f,5.2f),grass);
            // Actual imported Kenney modules, assembled as the proposed new art prefab.
            for(int y=0;y<levels;y++)for(int i=0;i<4;i++)
            {
                Module(y==0&&i==2?"wall-door":i==1?"wall-window-shutters":"wall",t,new Vector3(i-1.5f,y,-1.95f),90);
                Module(i==1?"wall-window-shutters":"wall",t,new Vector3(i-1.5f,y,1.95f),270);
            }
            for(int y=0;y<levels;y++)for(int j=0;j<3;j++){Module(j==0?"wall-window-shutters":"wall",t,new Vector3(-1.55f,y,j-1));Module(j==0?"wall-window-shutters":"wall",t,new Vector3(1.55f,y,j-1),180);}
            for(int z=0;z<4;z++){Module("roof-high",t,new Vector3(-1.5f,levels,z-1.5f),180);Module("roof-high",t,new Vector3(-.5f,levels+1,z-1.5f),180);Module("roof-high",t,new Vector3(.5f,levels+1,z-1.5f));Module("roof-high",t,new Vector3(1.5f,levels,z-1.5f));}
            var roofMaterial=Material("Roof_"+kind,kind==2?new Color(.18f,.42f,.61f):kind==4?new Color(.14f,.51f,.49f):new Color(.76f,.22f,.12f));
            foreach(Transform child in t)if(child.name.StartsWith("roof"))foreach(var r in child.GetComponentsInChildren<Renderer>())r.sharedMaterial=roofMaterial;
            Gable(t,-2.02f,false,levels);Gable(t,2.02f,true,levels);
            Module("chimney",t,new Vector3(1,levels+.1f,.8f));
            if(kind==3){Module("stall-red",t,new Vector3(.3f,0,-2.1f),180,new Vector3(.9f,.9f,.9f));FurnitureProp("FurnTable",t,new Vector3(-1.5f,0,-2),.55f);FurnitureProp("FurnChair",t,new Vector3(-2.3f,0,-2.3f),.58f);FurnitureProp("FurnChair",t,new Vector3(-.8f,0,-2.3f),.58f);}
            if(kind==4)
            {
                var cross=Material("ClinicTeal",new Color(.18f,.68f,.66f));Primitive(PrimitiveType.Cube,"ClinicSignVertical",t,new Vector3(0,2.7f,-1.7f),new Vector3(.28f,.9f,.14f),cross);Primitive(PrimitiveType.Cube,"ClinicSignHorizontal",t,new Vector3(0,2.7f,-1.7f),new Vector3(.82f,.28f,.14f),cross);
            }
            Nature("Bush_Common_Flowers",t,new Vector3(-2.2f,0,-1.9f),.7f);Nature("Flower_3_Group",t,new Vector3(2,0,-2),.55f);Nature("CommonTree_3",t,new Vector3(-2,0,1.2f),3.4f);
            for(int j=0;j<4;j++)Module("fence",t,new Vector3(-2.5f+j*1.3f,0,2.3f));
            if(kind==2){root.transform.localScale=Vector3.one*.8f;}
        }
        else if(kind==5||kind==7)
        {
            Primitive(PrimitiveType.Cube,"GardenBase",t,new Vector3(0,-.12f,0),new Vector3(6.5f,.24f,5.8f),grass);Primitive(PrimitiveType.Cube,"GardenWalk",t,new Vector3(0,.012f,0),new Vector3(1.4f,.06f,5.7f),soil);
            Module("fountain-round",t,new Vector3(.1f,.07f,.3f),0,new Vector3(1.3f,1.3f,1.3f));
            Primitive(PrimitiveType.Cylinder,"FountainWater",t,new Vector3(.1f,.24f,.3f),new Vector3(2.1f,.045f,2.1f),Material("FountainWater",new Color(.14f,.60f,.72f)));
            for(int i=0;i<5;i++){Module("fence",t,new Vector3(-2.8f+i*1.3f,0,2.7f));if(i<4)Module("fence",t,new Vector3(-3,0,-2.1f+i*1.2f),90);}
            Nature("CommonTree_3",t,new Vector3(-2,0,1.7f),3);Nature("CommonTree_1",t,new Vector3(2,0,1.6f),3.4f);Nature("Bush_Common_Flowers",t,new Vector3(-2,0,-1.9f),.8f);Nature("Flower_3_Group",t,new Vector3(2,0,-1.9f),.6f);
            if(kind==7){Module("hedge-gate",t,new Vector3(.5f,0,-2.5f));Module("stall-green",t,new Vector3(-1.5f,0,.2f));}
        }
        else if(kind==6)
        {
            Primitive(PrimitiveType.Cube,"GardenBase",t,new Vector3(0,-.13f,0),new Vector3(6,.25f,5.2f),grass);Primitive(PrimitiveType.Cylinder,"ClearPond",t,new Vector3(0,.04f,0),new Vector3(4.8f,.08f,3.4f),Material("ClearPond",new Color(.17f,.70f,.79f)));
            for(int i=0;i<13;i++){float a=i*6.283f/13;Nature("Rock_Medium_1",t,new Vector3(Mathf.Cos(a)*2.3f,0,Mathf.Sin(a)*1.6f),.25f+(i%3)*.08f);}Nature("CommonTree_3",t,new Vector3(-2,0,1),3.1f);Nature("Flower_3_Group",t,new Vector3(2,0,-1.5f),.6f);
        }
        else
        {
            Primitive(PrimitiveType.Cube,"GardenBase",t,new Vector3(0,-.12f,0),new Vector3(5.8f,.24f,4.5f),grass);for(int i=0;i<5;i++)Module("road",t,new Vector3(i-2,.04f,0));Nature("Flower_3_Group",t,new Vector3(-1.4f,0,1.2f),.6f);Nature("Bush_Common_Flowers",t,new Vector3(1.8f,0,-1.2f),.7f);
        }
        return root;
    }
    static void FurnitureProp(string resource,Transform parent,Vector3 p,float height)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("Furniture/"+resource),parent);go.transform.localPosition=Vector3.zero;var rr=go.GetComponentsInChildren<Renderer>();var b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);float factor=height/b.size.y;go.transform.localScale*=factor;go.transform.localPosition=p-new Vector3(b.center.x*factor,b.min.y*factor,b.center.z*factor);
    }
    static void Gable(Transform parent,float z,bool reverse,int levels)
    {
        string path=Root+"/Models/Gable_"+(reverse?"Back":"Front")+levels+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(!mesh){mesh=new Mesh();mesh.name="WarmTimberGable";AssetDatabase.CreateAsset(mesh,path);}mesh.vertices=new[]{new Vector3(-2,levels,z),new Vector3(0,levels+2,z),new Vector3(2,levels,z)};mesh.triangles=reverse?new[]{2,1,0}:new[]{0,1,2};mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
        var g=new GameObject("TimberGable",typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=Material("WarmPlaster",new Color(.83f,.68f,.44f));
    }
    static Texture2D RenderThumbnail(GameObject model,int key)
    {
        var scene=EditorSceneManager.NewPreviewScene();var clone=UnityEngine.Object.Instantiate(model);SceneManager.MoveGameObjectToScene(clone,scene);
        var camera=new GameObject("ThumbnailCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(camera,scene);var cam=camera.GetComponent<Camera>();cam.scene=scene;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;cam.orthographic=true;
        var b=new Bounds(clone.transform.position,Vector3.zero);bool first=true;foreach(var r in clone.GetComponentsInChildren<Renderer>()){if(first){b=r.bounds;first=false;}else b.Encapsulate(r.bounds);}
        cam.transform.position=b.center+new Vector3(8,7,-11);cam.transform.LookAt(b.center);cam.aspect=1.15f;
        float maxX=0,maxY=0;for(int i=0;i<8;i++){var c=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var p=cam.transform.InverseTransformPoint(c);maxX=Mathf.Max(maxX,Mathf.Abs(p.x));maxY=Mathf.Max(maxY,Mathf.Abs(p.y));}cam.orthographicSize=Mathf.Max(maxY,maxX/cam.aspect)*1.08f;
        var light=new GameObject("ThumbnailSoftbox",typeof(Light));SceneManager.MoveGameObjectToScene(light,scene);var sun=light.GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=.95f;sun.color=new Color(1,.97f,.90f);sun.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(48,-35,0);
        var rt=new RenderTexture(640,560,24,RenderTextureFormat.ARGB32);rt.Create();cam.targetTexture=rt;cam.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(640,560,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,640,560),0,0);image.Apply();RenderTexture.active=previous;
        string path=Root+"/Thumbnails/Building_"+key+".png";File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);EditorSceneManager.ClosePreviewScene(scene);
        AssetDatabase.ImportAsset(path);var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static void CreateVillage(PetTownArtPreviewData data)
    {
        var root=new GameObject("NewVillage_Diorama_ReviewOnly");var grass=Material("SoftGrass",new Color(.55f,.72f,.30f));var path=Material("WarmPath",new Color(.72f,.55f,.32f));
        var meadow=Material("MeadowDisplay",Color.white);meadow.shader=Shader.Find("PetTown/ArtDirection/Meadow");
        Primitive(PrimitiveType.Cube,"VillageGround",root.transform,new Vector3(0,-.23f,0),new Vector3(180,.4f,180),meadow);
        for(int i=0;i<36;i++)Primitive(PrimitiveType.Cylinder,"RoundWalkStone",root.transform,new Vector3(-18+i*1.1f,.015f,Mathf.Sin(i*.2f)*2),new Vector3(1.65f,.045f,2.2f),path);
        foreach(var spec in new[]{new Vector3(-9,0,6),new Vector3(-2,0,7),new Vector3(10,0,8),new Vector3(13,0,-4)}){int i=spec.x<0?0:2;var house=(GameObject)PrefabUtility.InstantiatePrefab(data.buildings[i].model,root.transform);house.transform.position=spec;}
        // Separate display river, not the saved town's gameplay river.
        var water=Material("ClearRiver",new Color(.12f,.66f,.77f));
        Primitive(PrimitiveType.Cube,"DisplayRiver",root.transform,new Vector3(20,-.005f,3),new Vector3(4.5f,.08f,56),water);
        var wood=Material("BridgeHoneyWood",new Color(.60f,.35f,.16f));
        for(int i=0;i<12;i++)Primitive(PrimitiveType.Cube,"BridgePlank",root.transform,new Vector3(17.4f+i*.47f,.45f,2),new Vector3(.43f,.20f,3.2f),wood);
        for(int side=0;side<2;side++)for(int i=0;i<6;i++){var p=new Vector3(17.2f+i*1.1f,.9f,.5f+side*3);Primitive(PrimitiveType.Cube,"BridgePost",root.transform,p,new Vector3(.16f,1.4f,.16f),wood);}for(int side=0;side<2;side++)Primitive(PrimitiveType.Cube,"BridgeRail",root.transform,new Vector3(20,1.38f,.5f+side*3),new Vector3(5.7f,.14f,.16f),wood);
        var rand=new System.Random(1307);
        for(int i=0;i<80;i++)
        {
            float x=(float)rand.NextDouble()*52-26,z=(float)rand.NextDouble()*40-15;if(Mathf.Abs(z-Mathf.Sin((x+18)*.18f)*2)<3||x>16&&x<24)continue;
            if((x>-13&&x<3||x>6&&x<16)&&z>2&&z<13)continue;
            Nature(i%3==0?"CommonTree_1":"CommonTree_3",root.transform,new Vector3(x,0,z),3.5f+(float)rand.NextDouble()*2.8f);
        }
        for(int i=0;i<85;i++)
        {
            float x=(float)rand.NextDouble()*50-25,z=(float)rand.NextDouble()*33-12;if(x>17&&x<23||Mathf.Abs(z)<2.5f)continue;
            Nature(i%4==0?"Bush_Common_Flowers":"Flower_3_Group",root.transform,new Vector3(x,0,z),i%4==0?.9f:.45f);
        }
        for(int i=0;i<40;i++){float z=-20+i*1.2f;Nature("Rock_Medium_1",root.transform,new Vector3(i%2==0?17.5f:22.5f,0,z),.5f+(i%3)*.15f);}
    }
}
