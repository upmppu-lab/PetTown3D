using System.Collections.Generic;
using UnityEngine;

// Read-only source geometry for direction recommendations; no road mesh changes.
[DisallowMultipleComponent]
public sealed class PetTownRoadPath : MonoBehaviour
{
    [SerializeField] Vector3[] points;
    [SerializeField] float width;
    [SerializeField] string roadId;
    [SerializeField] int constructionMode;
    [SerializeField] PetTownRoadGrade grade;
    [SerializeField] int roadType;
    public int RoadType=>roadType;
    public void SetType(int type){roadType=Mathf.Max(0,type);grade=PetTownRoadTypes.Paved(roadType)?PetTownRoadGrade.Paved:PetTownRoadGrade.Dirt;}
    public PetTownRoadSaveRecord Capture()=>new PetTownRoadSaveRecord{id=roadId,width=width,mode=constructionMode,type=roadType,grade=grade,points=System.Array.ConvertAll(points,p=>transform.TransformPoint(p))};
    public void RestoreIdentity(PetTownRoadSaveRecord record){roadId=record.id;constructionMode=record.mode;roadType=Mathf.Max(0,record.type);grade=record.grade;Revision++;}
    public static List<PetTownRoadHit> Candidates(Vector3 point,float maxDistance)
    {
        var result=new List<PetTownRoadHit>();float limit=maxDistance*maxDistance;
        for(int k=paths.Count-1;k>=0;k--)
        {
            var path=paths[k];if(path==null||!path.isActiveAndEnabled||path.points==null)continue;
            for(int i=1;i<path.points.Length;i++)
            {
                Vector3 a=path.transform.TransformPoint(path.points[i-1]),b=path.transform.TransformPoint(path.points[i]);
                Vector3 delta=b-a;delta.y=0;if(delta.sqrMagnitude<.000001f)continue;
                Vector3 offset=point-a;offset.y=0;float t=Mathf.Clamp01(Vector3.Dot(offset,delta)/delta.sqrMagnitude);
                Vector3 nearest=Vector3.Lerp(a,b,t);offset=nearest-point;offset.y=0;
                if(offset.sqrMagnitude<=limit)result.Add(new PetTownRoadHit{Point=nearest,Tangent=delta.normalized,From=a,To=b,Width=path.width,Grade=path.grade,RoadId=path.roadId});
            }
        }
        result.Sort((a,b)=>HorizontalDistance(a.Point,point).CompareTo(HorizontalDistance(b.Point,point)));return result;
    }
    static float HorizontalDistance(Vector3 a,Vector3 b)=>(new Vector2(a.x-b.x,a.z-b.z)).sqrMagnitude;
    public static bool TryNearestHit(Vector3 point,float distance,out PetTownRoadHit hit)
    {
        hit=default;float best=distance*distance;bool found=false;
        foreach(var path in paths)
        {
            if(path==null||!path.isActiveAndEnabled||path.points==null)continue;
            for(int i=1;i<path.points.Length;i++)
            {
                Vector3 a=path.transform.TransformPoint(path.points[i-1]),b=path.transform.TransformPoint(path.points[i]);
                Vector3 delta=b-a;delta.y=0;if(delta.sqrMagnitude<.000001f)continue;
                Vector3 offset=point-a;offset.y=0;float t=Mathf.Clamp01(Vector3.Dot(offset,delta)/delta.sqrMagnitude);
                Vector3 nearest=Vector3.Lerp(a,b,t);float sqr=HorizontalDistance(nearest,point);
                if(sqr<best){best=sqr;found=true;hit=new PetTownRoadHit{Point=nearest,Tangent=delta.normalized,From=a,To=b,Width=path.width,Grade=path.grade,RoadId=path.roadId};}
            }
        }
        return found;
    }
    public static int Revision { get; private set; }
    static readonly List<PetTownRoadPath> paths=new List<PetTownRoadPath>();
    public void Initialize(List<Vector3> centerline,float roadWidth){points=centerline.ToArray();width=roadWidth;roadId=System.Guid.NewGuid().ToString("N");if(!paths.Contains(this))paths.Add(this);Revision++;}
    void OnEnable(){if(!paths.Contains(this)){paths.Add(this);Revision++;}}
    void OnDisable(){paths.Remove(this);Revision++;}
    public static bool TryNearest(Vector3 point,float maxDistance,out Vector3 nearest)
    {
        return TryNearest(point,maxDistance,out nearest,out float ignoredWidth);
    }
    public static bool TryNearest(Vector3 point,float maxDistance,out Vector3 nearest,out float roadWidth)
    {
        nearest=point;roadWidth=0f;float best=maxDistance*maxDistance;bool found=false;
        for(int k=paths.Count-1;k>=0;k--)
        {
            var path=paths[k];
            if(path==null){paths.RemoveAt(k);continue;}
            if(path.points==null)continue;
            for(int i=1;i<path.points.Length;i++)
            {
                Vector3 a=path.transform.TransformPoint(path.points[i-1]),b=path.transform.TransformPoint(path.points[i]);
                Vector2 delta=new Vector2(b.x-a.x,b.z-a.z);
                float t=Mathf.Clamp01(Vector2.Dot(new Vector2(point.x-a.x,point.z-a.z),delta)/Mathf.Max(delta.sqrMagnitude,.000001f));
                Vector3 candidate=Vector3.Lerp(a,b,t);
                float distance=new Vector2(point.x-candidate.x,point.z-candidate.z).sqrMagnitude;
                if(distance<best){best=distance;nearest=candidate;roadWidth=path.width;found=true;}
            }
        }
        return found;
    }
    public static bool Overlaps(Rect footprint)
    {
        for(int k=paths.Count-1;k>=0;k--)
        {
            var path=paths[k];if(path==null||path.points==null)continue;
            var area=PetTownPlacementValidator.Expand(footprint,path.width*.5f);
            for(int i=1;i<path.points.Length;i++)
                if(PetTownNavigationQueries.SegmentIntersects(area,path.transform.TransformPoint(path.points[i-1]),path.transform.TransformPoint(path.points[i])))return true;
        }
        return false;
    }
}