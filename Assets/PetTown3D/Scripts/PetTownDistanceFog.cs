using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// A single camera veil uses horizontal world distance, so zooming does not expose a grass cutoff.
// Actual player travel and owned buildings reveal a persistent world mask; camera travel never does.
[DefaultExecutionOrder(100)]
public sealed class PetTownDistanceFog : MonoBehaviour
{
    static PetTownDistanceFog active;
    public static PetTownDistanceFog Current=>active&&active.isActiveAndEnabled&&active.fogEnabled?active:null;
    [SerializeField] bool fogEnabled=true;
    [SerializeField,Min(8)] float nearDistance=50;
    [SerializeField,Min(16)] float farDistance=120;
    [SerializeField] Color fogColor=new Color(.78f,.84f,.79f,1);
    public void SetAtmosphereColor(Color color){fogColor=color;}
    public float FarDistance=>farDistance;
    public Vector3 Focus=>focus;
    public int HiddenNatureRenderers {get;private set;}
    PetTownGame game;Camera camera;Mesh mesh;Material material;
    PetTownExplorationState exploration;Texture2D exploredMap;int uploadedRevision=-1;
    Vector3 focus,lastCullFocus;Vector4 river;bool ready;float nextNature,nextCull;
    readonly int[] originIds=new int[4],directionIds=new int[4];
    readonly Vector3[] vertices=new Vector3[4];
    sealed class Nature {public Renderer[] renderers;public bool[] original;public Bounds bounds;}
    readonly List<Nature> nature=new List<Nature>();
    public void Initialize(PetTownGame source)
    {
        game=source;active=this;
        for(int i=0;i<4;i++){originIds[i]=Shader.PropertyToID("_Origin"+i);directionIds[i]=Shader.PropertyToID("_Direction"+i);}
        var shader=Resources.Load<Shader>("Environment/PetTownDistanceFog");if(!shader){enabled=false;return;}
        material=new Material(shader){name="PetTown_WorldDistanceFog"};
        mesh=new Mesh{name="PetTown_FogViewQuad"};mesh.vertices=new Vector3[4];mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,1,2,0,2,3};
    }
    void Update()
    {
        if(game){game.UpdateWorldExploration();var current=game.Exploration;if(!ReferenceEquals(exploration,current)){exploration=current;uploadedRevision=-1;}UpdateExploredMap();}
        var view=Camera.main;if(view)UpdateFocus(view);
        if(Time.unscaledTime>=nextNature){nextNature=Time.unscaledTime+1;RefreshNature();nextCull=0;}
        if(Time.unscaledTime>=nextCull||(focus-lastCullFocus).sqrMagnitude>.25f){nextCull=Time.unscaledTime+.12f;lastCullFocus=focus;CullNature();}
    }
    void LateUpdate(){UpdateView();}
    void UpdateView()
    {
        if(!material||!game)return;var main=Camera.main;if(!main)return;
        camera=main;
        UpdateFocus(camera);
        farDistance=Mathf.Max(nearDistance+8,farDistance);
        material.SetVector("_FogCenter",focus);material.SetVector("_FogRange",new Vector4(nearDistance,farDistance,8,50));material.SetVector("_RiverLine",river);material.SetColor("_FogColor",fogColor);material.SetVector("_WorldExtent",new Vector4(game.WorldHalfExtent,24,0,0));
        for(int i=0;i<4;i++)
        {
            var uv=i==0?Vector2.zero:i==1?Vector2.right:i==2?Vector2.one:Vector2.up;
            var ray=camera.ViewportPointToRay(new Vector3(uv.x,uv.y,0));
            vertices[i]=camera.transform.InverseTransformPoint(ray.origin+ray.direction*.05f);
            material.SetVector(originIds[i],ray.origin);material.SetVector(directionIds[i],ray.direction);
        }
        mesh.vertices=vertices;mesh.RecalculateBounds();ready=true;
        if(fogEnabled)Graphics.DrawMesh(mesh,camera.transform.localToWorldMatrix,material,2,camera,0,null,ShadowCastingMode.Off,false,null,LightProbeUsage.Off);
    }
    void UpdateFocus(Camera view)
    {
        var centerRay=view.ViewportPointToRay(new Vector3(.5f,.5f,0));float t;
        focus=new Plane(Vector3.up,Vector3.zero).Raycast(centerRay,out t)?centerRay.GetPoint(t):view.transform.position;
        float riverX=Mathf.Sin(focus.z*.031f)*12+Mathf.Sin(focus.z*.09f)*3;
        float slope=.372f*Mathf.Cos(focus.z*.031f)+.27f*Mathf.Cos(focus.z*.09f);
        river=new Vector4(riverX,slope,focus.z,Mathf.Clamp((focus.x-riverX)/12,-1,1));
    }
    void UpdateExploredMap()
    {
        if(exploration==null||exploration.Pixels==null)return;
        if(!exploredMap||exploredMap.width!=exploration.width||exploredMap.height!=exploration.height)
        {
            if(exploredMap)Destroy(exploredMap);
            exploredMap=new Texture2D(exploration.width,exploration.height,TextureFormat.R8,false,true){name="PetTown_ExploredCells",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};uploadedRevision=-1;
        }
        if(uploadedRevision==exploration.Revision)return;
        exploredMap.LoadRawTextureData(exploration.Pixels);exploredMap.Apply(false,false);uploadedRevision=exploration.Revision;
        material.SetTexture("_ExploredMap",exploredMap);material.SetVector("_ExploredBounds",exploration.MapBounds);nextCull=0;
    }
    public bool HasExplored(Bounds bounds)=>exploration!=null&&exploration.HasExplored(bounds);
    public bool FullyCovers(Bounds bounds,float padding=0)
    {
        for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
            if(Amount(bounds.center+new Vector3(x*(bounds.extents.x+padding),0,z*(bounds.extents.z+padding)))<.999f)return false;
        return true;
    }
    public float Amount(Vector3 point)
    {
        if(!ready||!fogEnabled)return 0;
        float distance=Vector2.Distance(new Vector2(point.x,point.z),new Vector2(focus.x,focus.z));
        float radial=Mathf.SmoothStep(0,1,Mathf.InverseLerp(nearDistance,farDistance,distance));
        float across=-(point.x-(river.x+river.y*(point.z-river.z)))*river.w;
        float edge=game.WorldHalfExtent-Mathf.Max(Mathf.Abs(point.x),Mathf.Abs(point.z));
        float border=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,24,edge));
        float bank=.55f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,50,across));
        float discovered=exploration!=null?exploration.Sample(point):0;
        float unknown=Mathf.Max(.30f,Mathf.Max(radial,bank))*(1-discovered);
        float atmosphere=.10f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(100,200,distance));
        return Mathf.Max(border,Mathf.Max(unknown,atmosphere));
    }
    void RefreshNature()
    {
        RestoreNature();nature.Clear();
        foreach(var root in Object.FindObjectsByType<PetTownNatureObstacle>(FindObjectsSortMode.None))
        {
            var rr=root.GetComponentsInChildren<Renderer>();if(rr.Length==0)continue;
            var n=new Nature{renderers=rr,original=new bool[rr.Length],bounds=rr[0].bounds};
            for(int i=0;i<rr.Length;i++){n.original[i]=rr[i].forceRenderingOff;n.bounds.Encapsulate(rr[i].bounds);}nature.Add(n);
        }
    }
    void CullNature()
    {
        HiddenNatureRenderers=0;
        foreach(var n in nature)
        {
            bool hidden=fogEnabled&&ready&&FullyCovers(n.bounds,n.bounds.size.y+2);
            for(int i=0;i<n.renderers.Length;i++)if(n.renderers[i])
            {
                n.renderers[i].forceRenderingOff=n.original[i]||hidden;
                if(hidden&&!n.original[i]&&n.renderers[i].enabled&&n.renderers[i].gameObject.activeInHierarchy)HiddenNatureRenderers++;
            }
        }
    }
    void RestoreNature(){foreach(var n in nature)for(int i=0;i<n.renderers.Length;i++)if(n.renderers[i])n.renderers[i].forceRenderingOff=n.original[i];}
    void OnDisable(){RestoreNature();}
    void OnDestroy(){RestoreNature();if(active==this)active=null;if(material)Destroy(material);if(mesh)Destroy(mesh);if(exploredMap)Destroy(exploredMap);}
}
