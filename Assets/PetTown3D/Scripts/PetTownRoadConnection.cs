using System;
using System.Collections.Generic;
using UnityEngine;

public enum PetTownRoadGrade { Dirt=0, Paved=1 }
[Serializable]
public sealed class PetTownRoadSaveRecord
{
    public string id;
    public float width=2.2f;
    public int mode;
    public int type;
    public PetTownRoadGrade grade;
    public Vector3[] points;
    public bool Valid => points!=null&&points.Length>=2&&points.Length<=20000&&width>=.5f&&width<=12f&&Array.TrueForAll(points,p=>Finite(p.x)&&Finite(p.y)&&Finite(p.z)&&Mathf.Abs(p.x)<=120f&&Mathf.Abs(p.z)<=120f);
    static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
}
public struct PetTownRoadHit
{
    public Vector3 Point,Tangent,From,To;
    public float Width;
    public PetTownRoadGrade Grade;
    public string RoadId;
}
public struct PetTownRoadConnection
{
    public bool Connected;
    public Vector3 Entrance,RoadEdge;
    public float Gap;
    public PetTownRoadGrade Grade;
    public string RoadId;
    // Future bonuses may consume this data; no population/speed changes are applied here.
}
public static class PetTownRoadConnections
{
    public static PetTownRoadConnection Query(Vector3 center,PetTownBuildingSaveRecord record,float maxGap,float passageWidth,Func<float,float,float> height,Func<float,float,bool> water,Func<Vector3,Vector3,float,bool> clear,IList<Rect> obstacles=null,Rect? bodyOverride=null)
    {
        Vector3 entrance=center+record.Rotation*record.entranceLocal;
        var result=new PetTownRoadConnection{Entrance=entrance,Gap=float.PositiveInfinity};
        if(!record.hasEntrance)return result;
        Rect body=bodyOverride??PetTownPlacementValidator.Footprint(center,record.WorldFootprint);
        foreach(var hit in PetTownRoadPath.Candidates(entrance,maxGap+6f))
        {
            Vector3 direction=hit.Point-entrance;direction.y=0;
            float gap=Mathf.Max(0,direction.magnitude-hit.Width*.5f);
            if(gap>maxGap||gap>=result.Gap)continue;
            Vector3 edge=hit.Point-direction.normalized*Mathf.Min(direction.magnitude,hit.Width*.5f);
            Vector3 start=entrance+record.EntranceForward*.1f;
            if(Vector3.Dot(direction,record.EntranceForward)<-.05f||PetTownNavigationQueries.SegmentIntersects(body,start,edge))continue;
            bool blocked=false;
            if(obstacles!=null)foreach(var area in obstacles)if(PetTownNavigationQueries.SegmentIntersects(PetTownPlacementValidator.Expand(area,passageWidth*.5f),start,edge)){blocked=true;break;}
            int count=Mathf.Max(1,Mathf.CeilToInt(direction.magnitude/.5f));float lastY=height(start.x,start.z);
            for(int i=0;!blocked&&i<=count;i++)
            {
                var sample=Vector3.Lerp(start,edge,i/(float)count);float y=height(sample.x,sample.z);
                if((water!=null&&water(sample.x,sample.z))||(i>0&&Mathf.Abs(y-lastY)>Mathf.Tan(35f*Mathf.Deg2Rad)*Mathf.Max(.01f,Vector2.Distance(new Vector2(start.x,start.z),new Vector2(edge.x,edge.z))/count)))blocked=true;
                lastY=y;
            }
            edge.y=height(edge.x,edge.z);
            if(blocked||(clear!=null&&!clear(entrance,edge,passageWidth*.5f)))continue;
            result=new PetTownRoadConnection{Connected=true,Entrance=entrance,RoadEdge=edge,Gap=gap,Grade=hit.Grade,RoadId=hit.RoadId};
        }
        return result;
    }
}
// Adjusts only a NEW preview. Curved or diagonal roads retain free grid placement.
public static class PetTownRoadAlignment
{
    public static Vector3 Suggest(Vector3 point,PetTownBuildingSaveRecord record,float cell,Vector2 origin,float search,float gap,float maxShift,IList<PetTownPlacementObstacle> neighbors)
    {
        if(!record.hasEntrance||!PetTownRoadPath.TryNearestHit(point,search,out var road))return point;
        var forward=record.EntranceForward;
        if(Mathf.Abs(Vector3.Dot(road.Tangent,forward))>.15f)return point;
        Vector3 toward=road.Point-point;toward.y=0;
        if(Vector3.Dot(toward,forward)<=0)return point;
        float half=Vector3.Dot(new Vector3(record.WorldFootprint.x,0,record.WorldFootprint.y),new Vector3(Mathf.Abs(forward.x),0,Mathf.Abs(forward.z)))*.5f;
        float target=Vector3.Dot(road.Point,forward)-(road.Width*.5f+gap+half);
        float best=float.PositiveInfinity;
        if(neighbors!=null)foreach(var neighbor in neighbors)
        {
            if(!neighbor.HasEntrance||Vector3.Dot(neighbor.Forward,forward)<.99f)continue;
            Vector3 other=new Vector3(neighbor.Footprint.center.x,0,neighbor.Footprint.center.y);
            float frontage=Vector3.Dot(other,forward)+(Mathf.Abs(forward.x)*neighbor.Footprint.width+Mathf.Abs(forward.z)*neighbor.Footprint.height)*.5f;
            float candidate=frontage-half;
            float distance=Vector2.Distance(new Vector2(other.x,other.z),new Vector2(point.x,point.z));
            if(distance<search*2f&&Mathf.Abs(candidate-target)<cell*1.5f&&distance<best){best=distance;target=candidate;}
        }
        float shift=target-Vector3.Dot(point,forward);
        if(Mathf.Abs(shift)>maxShift)return point;
        return PetTownBuildGrid.SnapHorizontal(point+forward*shift,cell,origin);
    }
}
