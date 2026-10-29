using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// New room architecture. Logical room/entrance coordinates drive the shell and its real collision.
public sealed class PetTownCozyInterior : MonoBehaviour
{
    PetTownInteriorState owner;Rect bounds;GameObject visual,roof;Mesh floor;
    Material window,roofMain,roofAccent,sign,floorMaterial,doorTimber;float next;
    static Mesh box;
    [SerializeField,Range(.05f,.6f)] float cutawayOpacity=.32f;
    const float DoorFrameOpacity=.95f;
    sealed class WallAppearance {public Renderer renderer;public Material[] solid,faded;public bool transparent;}
    readonly List<WallAppearance>[] wallRenderers={new List<WallAppearance>(),new List<WallAppearance>(),new List<WallAppearance>(),new List<WallAppearance>()};
    readonly Dictionary<Material,Material> fadedMaterials=new Dictionary<Material,Material>();
    float lastOpacity=-1;
    readonly List<Mesh> meshes=new List<Mesh>();
    readonly Dictionary<Renderer,bool> hidden=new Dictionary<Renderer,bool>();
    readonly Dictionary<Collider,bool> oldColliders=new Dictionary<Collider,bool>();
    readonly Dictionary<NavMeshObstacle,bool> oldObstacles=new Dictionary<NavMeshObstacle,bool>();
    readonly List<GameObject> sides=new List<GameObject>();
    void Start(){owner=GetComponent<PetTownInteriorState>();Refresh();}
    public void RebuildNow(){if(!owner)owner=GetComponent<PetTownInteriorState>();Refresh();}
    void Update()
    {
        if(!owner)return;if(Time.unscaledTime>=next){next=Time.unscaledTime+.25f;if(owner.LocalBounds!=bounds)Refresh();UpdateCutaway();}
    }
    bool IsArchitecture(Transform t)
    {
        string p="";for(;t&&t!=transform;t=t.parent)p=t.name+"/"+p;
        return !p.Contains("CozyInteriorVisual")&&(p.Contains("Wall")||p.Contains("Window")||p.Contains("Foundation_Trim")||p.Contains("Toggle_Roof"));
    }
    void Refresh()
    {
        var lib=PetTownCozyLibrary.Current;if(!owner||lib==null)return;bounds=owner.LocalBounds;
        if(visual){visual.SetActive(false);Destroy(visual);}foreach(var m in meshes)if(m)Destroy(m);meshes.Clear();sides.Clear();foreach(var list in wallRenderers)list.Clear();
        foreach(var r in GetComponentsInChildren<Renderer>(true))if(!r.GetComponentInParent<PetTownFurniturePlacement>()&&IsArchitecture(r.transform)){if(!hidden.ContainsKey(r))hidden[r]=r.forceRenderingOff;r.forceRenderingOff=true;}
        foreach(var c in GetComponentsInChildren<Collider>(true))if(!c.GetComponentInParent<PetTownFurniturePlacement>()&&IsArchitecture(c.transform)){if(!oldColliders.ContainsKey(c))oldColliders[c]=c.enabled;c.enabled=false;}
        foreach(var n in GetComponentsInChildren<NavMeshObstacle>(true))if(!n.GetComponentInParent<PetTownFurniturePlacement>()&&IsArchitecture(n.transform)){if(!oldObstacles.ContainsKey(n))oldObstacles[n]=n.enabled;n.enabled=false;}
        if(!window){window=new Material(lib.plaster);window.color=new Color(.42f,.70f,.79f);window.EnableKeyword("_EMISSION");window.SetColor("_EmissionColor",new Color(.04f,.08f,.10f));}
        visual=new GameObject("CozyInteriorVisual");visual.transform.SetParent(transform,false);visual.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild=true;
        if(!floorMaterial)floorMaterial=new Material(Resources.Load<Shader>("Environment/PetTownCozyOak"));
        floor=PetTownCozyFloor.Create(bounds,owner.FloorOffset+.016f);meshes.Add(floor);MeshObject("NaturalStaggeredOakFloor",visual.transform,floor,floorMaterial);
        for(int i=0;i<4;i++)BuildSide(i,lib);
        for(int i=0;i<sides.Count;i++)foreach(var renderer in sides[i].GetComponentsInChildren<Renderer>())
        {
            var solid=renderer.sharedMaterials;var faded=new Material[solid.Length];
            for(int m=0;m<solid.Length;m++)faded[m]=FadedMaterial(solid[m]);
            wallRenderers[i].Add(new WallAppearance{renderer=renderer,solid=solid,faded=faded});
        }
        BuildRoof(lib);UpdateCutaway();
    }
    void BuildSide(int side,PetTownCozyLibrary lib)
    {
        bool xAxis=side<2;float boundary=side==0?bounds.xMin:side==1?bounds.xMax:side==2?bounds.yMin:bounds.yMax;
        float min=xAxis?bounds.yMin:bounds.xMin,max=xAxis?bounds.yMax:bounds.xMax;
        var group=new GameObject("Wall_"+side);group.transform.SetParent(visual.transform,false);sides.Add(group);
        bool entrance=side==2;float door=owner.EntranceLocal.x,doorWidth=1.9f,h=3.2f;
        if(entrance)
        {
            if(!doorTimber){doorTimber=new Material(lib.timber){name="Cozy_DarkEntranceTimber"};doorTimber.color=new Color(.23f,.115f,.055f,1);}
            float left=Mathf.Max(.1f,door-doorWidth*.5f-min),right=Mathf.Max(.1f,max-door-doorWidth*.5f);
            WallBox(group.transform,"DoorLeft",new Vector3(min+left*.5f,owner.FloorOffset+h*.5f,boundary),new Vector3(left,h,.18f),lib.plaster,true);
            WallBox(group.transform,"DoorRight",new Vector3(max-right*.5f,owner.FloorOffset+h*.5f,boundary),new Vector3(right,h,.18f),lib.plaster,true);
            WallBox(group.transform,"DoorLintel",new Vector3(door,owner.FloorOffset+2.9f,boundary),new Vector3(doorWidth,.6f,.18f),lib.plaster,true);
            foreach(float s in new[]{-1f,1f})WallBox(group.transform,"DoorOakJamb",new Vector3(door+s*1.02f,owner.FloorOffset+1.32f,boundary-.035f),new Vector3(.14f,2.64f,.23f),doorTimber,false);
            WallBox(group.transform,"DoorOakLintel",new Vector3(door,owner.FloorOffset+2.65f,boundary-.035f),new Vector3(2.15f,.16f,.24f),doorTimber,false);
        }
        else WallBox(group.transform,"PlasterWall",xAxis?new Vector3(boundary,owner.FloorOffset+h*.5f,(min+max)*.5f):new Vector3((min+max)*.5f,owner.FloorOffset+h*.5f,boundary),xAxis?new Vector3(.18f,h,max-min):new Vector3(max-min,h,.18f),lib.plaster,true);
        float inward=(side==0||side==2)?1:-1;
        for(float u=min+1.5f;u<max-.9f;u+=4)
        {
            if(entrance&&Mathf.Abs(u-door)<1.9f)continue;
            var center=xAxis?new Vector3(boundary+inward*.115f,owner.FloorOffset+1.86f,u):new Vector3(u,owner.FloorOffset+1.86f,boundary+inward*.115f);
            var depth=xAxis?new Vector3(.035f,1.35f,1.30f):new Vector3(1.30f,1.35f,.035f);WallBox(group.transform,"BlueWindow",center,depth,window,false);
            for(int s=-1;s<=1;s+=2)
            {
                var offset=xAxis?new Vector3(0,0,s*.72f):new Vector3(s*.72f,0,0);
                WallBox(group.transform,"OakWindowUpright",center+offset,xAxis?new Vector3(.13f,1.57f,.09f):new Vector3(.09f,1.57f,.13f),lib.timber,false);
                WallBox(group.transform,"OakWindowRail",center+Vector3.up*(s*.75f),xAxis?new Vector3(.15f,.09f,1.52f):new Vector3(1.52f,.09f,.15f),lib.timber,false);
            }
            WallBox(group.transform,"WindowCrossbar",center,xAxis?new Vector3(.14f,1.48f,.05f):new Vector3(.05f,1.48f,.14f),lib.timber,false);
            WallBox(group.transform,"WindowSill",center-Vector3.up*.77f,xAxis?new Vector3(.32f,.10f,1.62f):new Vector3(1.62f,.10f,.32f),lib.timber,false);
        }
        for(float u=min+.35f;u<max-.1f;u+=1.2f)
        {
            if(entrance&&Mathf.Abs(u-door)<1.3f)continue;
            var p=xAxis?new Vector3(boundary+inward*.12f,owner.FloorOffset+.47f,u):new Vector3(u,owner.FloorOffset+.47f,boundary+inward*.12f);
            WallBox(group.transform,"OakWainscot",p,xAxis?new Vector3(.08f,.86f,1.16f):new Vector3(1.16f,.86f,.08f),lib.timber,false);
        }
        var lamp=new GameObject("WarmInteriorLight",typeof(Light));lamp.transform.SetParent(group.transform,false);lamp.transform.localPosition=xAxis?new Vector3(boundary+inward*.7f,2.4f,(min+max)*.5f):new Vector3((min+max)*.5f,2.4f,boundary+inward*.7f);
        var light=lamp.GetComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.76f,.49f);light.intensity=.65f;light.range=7;light.shadows=LightShadows.None;
        if(entrance)BuildPawSign(group.transform,door,boundary-.13f,lib);
    }
    void WallBox(Transform parent,string name,Vector3 p,Vector3 size,Material mat,bool physical)
    {
        if(!box)box=CreateBox();var g=MeshObject(name,parent,box,mat);g.transform.localPosition=p;g.transform.localScale=size;
        if(physical){g.AddComponent<BoxCollider>();var n=g.AddComponent<NavMeshObstacle>();n.shape=NavMeshObstacleShape.Box;n.size=Vector3.one;n.carving=true;n.carveOnlyStationary=false;}
    }
    void BuildPawSign(Transform parent,float x,float z,PetTownCozyLibrary lib)
    {
        if(!sign){sign=new Material(Shader.Find("Universal Render Pipeline/Lit"));sign.mainTexture=lib.style.iconAtlas;sign.SetFloat("_AlphaClip",1);sign.EnableKeyword("_ALPHATEST_ON");sign.SetFloat("_Cutoff",.1f);sign.SetFloat("_Cull",0);}
        var m=new Mesh();m.vertices=new[]{new Vector3(-.47f,-.37f,0),new Vector3(-.47f,.37f,0),new Vector3(.47f,.37f,0),new Vector3(.47f,-.37f,0)};m.triangles=new[]{0,1,2,0,2,3};m.uv=new[]{new Vector2(.25f,0),new Vector2(.25f,1f/3),new Vector2(.50f,1f/3),new Vector2(.50f,0)};m.RecalculateNormals();meshes.Add(m);var g=MeshObject("PetShopPawSign",parent,m,sign);g.transform.localPosition=new Vector3(x,2.92f,z);
    }
    void BuildRoof(PetTownCozyLibrary lib)
    {
        roof=new GameObject("CozyRoofDisplay");roof.transform.SetParent(visual.transform,false);
        if(!box)box=CreateBox();if(!roofMain){roofMain=new Material(lib.plaster);roofMain.color=owner.Kind==4?new Color(.16f,.47f,.46f):owner.Kind==1?new Color(.20f,.39f,.58f):new Color(.69f,.19f,.12f);roofAccent=new Material(roofMain);roofAccent.color=Color.Lerp(roofMain.color,new Color(1,.58f,.32f),.18f);}
        float half=bounds.width*.5f+.35f,rise=Mathf.Min(3.6f,bounds.width*.26f),length=bounds.height+.7f,slope=Mathf.Sqrt(half*half+rise*rise),angle=Mathf.Atan2(rise,half)*Mathf.Rad2Deg;
        int rows=Mathf.CeilToInt(slope/.95f),cols=Mathf.CeilToInt(length/1.15f);var main=new List<CombineInstance>();var accent=new List<CombineInstance>();
        for(int side=-1;side<=1;side+=2)for(int r=0;r<rows;r++)for(int c=0;c<cols;c++)
        {
            float u=(r+.5f)/rows;var p=new Vector3(bounds.center.x+side*u*half,owner.FloorOffset+3.2f+rise*(1-u),bounds.yMin-.35f+(c+.5f)*length/cols);var ci=new CombineInstance{mesh=box,transform=Matrix4x4.TRS(p,Quaternion.Euler(0,0,-side*angle),new Vector3(slope/rows*1.07f,.105f,length/cols*1.025f))};if((r*7+c*3)%6==0)accent.Add(ci);else main.Add(ci);
        }
        foreach(var pair in new[]{new KeyValuePair<List<CombineInstance>,Material>(main,roofMain),new KeyValuePair<List<CombineInstance>,Material>(accent,roofAccent)}){var m=new Mesh();m.CombineMeshes(pair.Key.ToArray(),true,true);meshes.Add(m);MeshObject("TerracottaRoofTiles",roof.transform,m,pair.Value);}
        foreach(float z in new[]{bounds.yMin-.12f,bounds.yMax+.12f})
        {
            var m=new Mesh();m.vertices=new[]{new Vector3(bounds.xMin-.35f,owner.FloorOffset+3.2f,z),new Vector3(bounds.center.x,owner.FloorOffset+3.2f+rise,z),new Vector3(bounds.xMax+.35f,owner.FloorOffset+3.2f,z)};m.triangles=z<bounds.center.y?new[]{0,1,2}:new[]{2,1,0};m.RecalculateNormals();meshes.Add(m);MeshObject("TimberRoofGable",roof.transform,m,lib.timber);
        }
        foreach(var r in roof.GetComponentsInChildren<Renderer>(true)){r.enabled=false;r.sharedMaterial=RoofGlassMaterial(r.sharedMaterial);r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;}
    }
    readonly Dictionary<Material,Material> roofGlass=new Dictionary<Material,Material>();
    const float RoofGlassOpacity=.16f;
    Material RoofGlassMaterial(Material source)
    {
        if(!source)return null;Material result;if(roofGlass.TryGetValue(source,out result))return result;
        result=new Material(source){name=source.name+"_RoofGlass",renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent};
        result.SetOverrideTag("RenderType","Transparent");result.SetFloat("_Surface",1);result.SetFloat("_Blend",0);
        result.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);result.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        result.SetInt("_SrcBlendAlpha",(int)UnityEngine.Rendering.BlendMode.One);result.SetInt("_DstBlendAlpha",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        result.SetFloat("_ZWrite",0);result.SetFloat("_AlphaClip",0);result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        result.DisableKeyword("_ALPHATEST_ON");result.DisableKeyword("_ALPHAPREMULTIPLY_ON");result.SetShaderPassEnabled("ShadowCaster",false);
        var color=source.color;color.a=RoofGlassOpacity;result.color=color;roofGlass[source]=result;return result;
    }
    Material FadedMaterial(Material source)
    {
        if(!source)return null;Material result;if(fadedMaterials.TryGetValue(source,out result))return result;
        result=new Material(source){name=source.name+"_CameraCutaway",renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent};
        result.SetOverrideTag("RenderType","Transparent");result.SetFloat("_Surface",1);result.SetFloat("_Blend",0);
        result.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);result.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        result.SetInt("_SrcBlendAlpha",(int)UnityEngine.Rendering.BlendMode.One);result.SetInt("_DstBlendAlpha",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        result.SetFloat("_ZWrite",0);result.SetFloat("_AlphaClip",0);result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        result.DisableKeyword("_ALPHATEST_ON");result.DisableKeyword("_ALPHAPREMULTIPLY_ON");result.DisableKeyword("_ALPHAMODULATE_ON");
        result.SetShaderPassEnabled("ShadowCaster",false);var color=source.color;color.a=source==doorTimber?DoorFrameOpacity:cutawayOpacity;result.color=color;
        fadedMaterials[source]=result;return result;
    }
    void UpdateCutaway()
    {
        if(sides.Count!=4)return;var original=transform.Find("Expansion_Shell/Toggle_Roof");if(!original)original=transform.Find("Toggle_Roof");bool closed=original&&original.gameObject.activeSelf;roof.SetActive(closed);
        var cam=Camera.main;if(!cam)return;var p=transform.InverseTransformPoint(cam.transform.position);int farX=p.x<bounds.center.x?1:0,farZ=p.z<bounds.center.y?3:2;
        if(Mathf.Abs(lastOpacity-cutawayOpacity)>.001f){lastOpacity=cutawayOpacity;foreach(var pair in fadedMaterials){var color=pair.Value.color;color.a=pair.Key==doorTimber?DoorFrameOpacity:cutawayOpacity;pair.Value.color=color;}}
        for(int i=0;i<4;i++)
        {
            bool transparent=!closed&&i!=farX&&i!=farZ;
            foreach(var wall in wallRenderers[i])if(wall.renderer)
            {
                wall.renderer.forceRenderingOff=false;
                if(wall.transparent==transparent)continue;
                wall.transparent=transparent;wall.renderer.sharedMaterials=transparent?wall.faded:wall.solid;
            }
        }
    }
    static GameObject MeshObject(string name,Transform parent,Mesh mesh,Material mat){var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=mat;return g;}
    static Mesh CreateBox()
    {
        var m=new Mesh();m.name="CozyJoinery";var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();foreach(var n in new[]{Vector3.forward,Vector3.back,Vector3.right,Vector3.left,Vector3.up,Vector3.down}){var a=Vector3.Cross(n,Mathf.Abs(n.y)>.5f?Vector3.forward:Vector3.up);var b=Vector3.Cross(n,a);int s=v.Count;v.Add((n-a-b)*.5f);v.Add((n+a-b)*.5f);v.Add((n+a+b)*.5f);v.Add((n-a+b)*.5f);uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});tr.AddRange(new[]{s,s+1,s+2,s,s+2,s+3});}m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.RecalculateNormals();return m;
    }
    void OnDestroy()
    {
        foreach(var p in hidden)if(p.Key)p.Key.forceRenderingOff=p.Value;foreach(var p in oldColliders)if(p.Key)p.Key.enabled=p.Value;foreach(var p in oldObstacles)if(p.Key)p.Key.enabled=p.Value;
        foreach(var material in fadedMaterials.Values)if(material)Destroy(material);fadedMaterials.Clear();
        foreach(var g in roofGlass.Values)if(g)Destroy(g);roofGlass.Clear();foreach(var m in meshes)if(m)Destroy(m);if(doorTimber)Destroy(doorTimber);if(window)Destroy(window);if(roofMain)Destroy(roofMain);if(roofAccent)Destroy(roofAccent);if(sign)Destroy(sign);if(floorMaterial)Destroy(floorMaterial);if(visual)Destroy(visual);
    }
}
