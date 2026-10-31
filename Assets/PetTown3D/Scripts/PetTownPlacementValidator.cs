using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PetTownPlacementSafetySettings
{
    [Range(3f,35f)] public float maxSlope=18f;
    [Min(.25f)] public float maxHeightRange=2f;
    [Min(.8f)] public float minimumPassageWidth=1.2f;
    [Min(1f)] public float entranceDepth=2f;
    [Min(0f)] public float roadGap=2.5f;
    public bool checkNavigation=true;
    public int Signature => maxSlope.GetHashCode()^maxHeightRange.GetHashCode()^minimumPassageWidth.GetHashCode()^entranceDepth.GetHashCode()^roadGap.GetHashCode()^(checkNavigation?1:0);
}
public struct PetTownPlacementValidation
{
    public bool CanPlace,RoadConnected,NeedsGrading;
    public string Code,Message;
    public static PetTownPlacementValidation Block(string code,string message)=>new PetTownPlacementValidation{Code=code,Message=message};
}
public struct PetTownPlacementObstacle
{
    public Rect Footprint;
    public float GroundY;
    public bool HasEntrance,IsLake;
    public Vector3 Entrance,Forward;
}
public sealed class PetTownPlacementSafetyContext
{
    public float WorldHalfSize=110;
    public readonly List<PetTownPlacementObstacle> Buildings=new List<PetTownPlacementObstacle>();
    public readonly List<Vector3> Actors=new List<Vector3>();
    public readonly List<Vector3> NavigationAnchors=new List<Vector3>();
    public readonly List<Vector3> ActorAnchors=new List<Vector3>();
    public Func<float,float,float> Height;
    public Func<float,float,bool> Water;
    public Func<Rect,float,bool> AreaClear;
    public Func<Vector3,Vector3,float,bool> PassageClear;
    public Func<Vector3,bool,Rect,float,List<Vector3>,string> Navigation;
}
public sealed class PetTownPlacementValidator
{
    bool cached;
    Vector3 lastCenter,lastEntrance;
    Vector2 lastFootprint;
    int lastFacing,lastKind,lastWorld,lastRoad,lastSettings;
    float lastTime;
    bool lastHasEntrance;
    PetTownPlacementValidation last;
    readonly Dictionary<Vector2Int,float> heights=new Dictionary<Vector2Int,float>();
    public void Invalidate(){cached=false;heights.Clear();}
    public PetTownPlacementValidation Evaluate(Vector3 center,PetTownBuildingSaveRecord record,PetTownPlacementSafetySettings settings,Func<PetTownPlacementSafetyContext> contextFactory,int worldRevision,int roadRevision,float time,bool force=false)
    {
        bool same=cached&&lastCenter==center&&lastEntrance==record.entranceLocal&&lastFootprint==record.WorldFootprint&&lastFacing==record.facingQuarter&&lastKind==record.kind&&lastWorld==worldRevision&&lastRoad==roadRevision&&lastSettings==settings.Signature&&lastHasEntrance==record.hasEntrance;
        if(!force&&same&&time-lastTime<.3f)return last;
        if(force||lastWorld!=worldRevision)heights.Clear();
        var context=contextFactory();var source=context.Height;
        context.Height=(x,z)=>
        {
            var key=new Vector2Int(Mathf.RoundToInt(x*1000),Mathf.RoundToInt(z*1000));
            if(!heights.TryGetValue(key,out float y)){y=source(x,z);heights[key]=y;}
            return y;
        };
        if(heights.Count>100000)heights.Clear();
        last=Validate(center,record,settings,context);
        if(!last.CanPlace&&(last.Code=="Slope"||last.Code=="HeightRange"||last.Code=="GradingSlope"))
        {
            var graded=Validate(center,record,settings,context,true);
            if(graded.CanPlace){last.NeedsGrading=true;last.Message=last.Message+" 땅을 평탄화하면 지을 수 있어요.";}
        }
        cached=true;lastCenter=center;lastEntrance=record.entranceLocal;lastFootprint=record.WorldFootprint;lastFacing=record.facingQuarter;lastKind=record.kind;lastWorld=worldRevision;lastRoad=roadRevision;lastSettings=settings.Signature;lastHasEntrance=record.hasEntrance;lastTime=time;
        return last;
    }
    public static Rect Footprint(Vector3 center,Vector2 size)=>new Rect(center.x-size.x*.5f,center.z-size.y*.5f,size.x,size.y);
    public static Rect Expand(Rect area,float amount)=>Rect.MinMaxRect(area.xMin-amount,area.yMin-amount,area.xMax+amount,area.yMax+amount);
    public static Rect EntranceArea(Vector3 point,Vector3 forward,float width,float depth)
    {
        Vector3 side=Vector3.Cross(Vector3.up,forward).normalized*width*.5f;
        Vector3 a=point+forward*.1f,b=point+forward*depth;
        return Rect.MinMaxRect(Mathf.Min(a.x-side.x,a.x+side.x,b.x-side.x,b.x+side.x),Mathf.Min(a.z-side.z,a.z+side.z,b.z-side.z,b.z+side.z),Mathf.Max(a.x-side.x,a.x+side.x,b.x-side.x,b.x+side.x),Mathf.Max(a.z-side.z,a.z+side.z,b.z-side.z,b.z+side.z));
    }
    public static Vector2 EarthworkSize(PetTownBuildingSaveRecord record)
    {
        Vector2 size=PetTownBuildingDefinition.Default(record.kind).footprintMeters;
        if(record.kind==2)size=new Vector2(13.6f,13.6f);
        else size+=Vector2.one*((record.kind==0||record.kind==1||record.kind==3||record.kind==4)?9.4f:4.4f);
        if(Mathf.RoundToInt(record.BuildingYaw/90f)%2!=0)size=new Vector2(size.y,size.x);
        return Vector2.Max(size,record.WorldFootprint);
    }
    static float PredictedHeight(Vector3 center,Vector2 core,float x,float z,float original)
    {
        float outside=Mathf.Max(Mathf.Abs(x-center.x)-core.x*.5f,Mathf.Abs(z-center.z)-core.y*.5f,0f);
        float weight=1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01(outside/2.2f));
        return Mathf.Lerp(original,center.y,weight);
    }
    static bool GradingWalkable(Vector3 center,PetTownBuildingSaveRecord record,Rect body,Rect area,PetTownPlacementSafetyContext context)
    {
        Vector2 core=PetTownBuildingDefinition.Default(record.kind).footprintMeters;
        if(record.kind==2)core=new Vector2(9.2f,9.2f);
        else if(record.kind==0||record.kind==1||record.kind==3||record.kind==4)core+=Vector2.one*5f;
        if(Mathf.RoundToInt(record.BuildingYaw/90f)%2!=0)core=new Vector2(core.y,core.x);
        int nx=Mathf.CeilToInt(area.width),nz=Mathf.CeilToInt(area.height);float dx=area.width/nx,dz=area.height/nz;
        var oldRow=new float[nx+1];var newRow=new float[nx+1];
        for(int z=0;z<=nz;z++)
        {
            float leftOld=0,leftNew=0;
            for(int x=0;x<=nx;x++)
            {
                float px=area.xMin+x*dx,pz=area.yMin+z*dz;
                float old=context.Height(px,pz),predicted=PredictedHeight(center,core,px,pz,old);
                if(!body.Contains(new Vector2(px,pz)))
                {
                    if(x>0&&!body.Contains(new Vector2(px-dx,pz))&&Mathf.Atan2(Mathf.Abs(predicted-leftNew),dx)*Mathf.Rad2Deg>35f&&Mathf.Abs(predicted-leftNew)>Mathf.Abs(old-leftOld)+.01f)return false;
                    if(z>0&&!body.Contains(new Vector2(px,pz-dz))&&Mathf.Atan2(Mathf.Abs(predicted-newRow[x]),dz)*Mathf.Rad2Deg>35f&&Mathf.Abs(predicted-newRow[x])>Mathf.Abs(old-oldRow[x])+.01f)return false;
                }
                oldRow[x]=old;newRow[x]=predicted;leftOld=old;leftNew=predicted;
            }
        }
        return true;
    }
    // People and animals standing on the spot are ignored (they get pushed out when the building is built).
    static List<Vector3> AnchorsOutside(PetTownPlacementSafetyContext context,Rect blocked)
    {
        var list=new List<Vector3>(context.NavigationAnchors);
        foreach(var a in context.ActorAnchors)if(!blocked.Contains(new Vector2(a.x,a.z)))list.Add(a);
        return list;
    }
    static bool WaterAt(float x,float z,PetTownPlacementSafetyContext context)
    {
        if(context.Water!=null&&context.Water(x,z))return true;
        foreach(var building in context.Buildings)if(building.IsLake&&Expand(building.Footprint,.35f).Contains(new Vector2(x,z)))return true;
        return false;
    }
    public static PetTownPlacementValidation Validate(Vector3 center,PetTownBuildingSaveRecord record,PetTownPlacementSafetySettings settings,PetTownPlacementSafetyContext context,bool assumeGraded=false)
    {
        if(record==null||!record.ValidFor(record.kind)||context==null||context.Height==null)return PetTownPlacementValidation.Block("Invalid","배치 정보를 확인할 수 없습니다.");
        Rect body=Footprint(center,record.WorldFootprint),earthwork=Footprint(center,EarthworkSize(record));
        if(earthwork.xMin<-context.WorldHalfSize||earthwork.xMax>context.WorldHalfSize||earthwork.yMin<-context.WorldHalfSize||earthwork.yMax>context.WorldHalfSize)return PetTownPlacementValidation.Block("Bounds","지형 밖에는 설치할 수 없습니다.");
        int wx=Mathf.CeilToInt(earthwork.width),wz=Mathf.CeilToInt(earthwork.height);
        for(int z=0;z<=wz;z++)for(int x=0;x<=wx;x++)
            if(WaterAt(Mathf.Lerp(earthwork.xMin,earthwork.xMax,x/(float)wx),Mathf.Lerp(earthwork.yMin,earthwork.yMax,z/(float)wz),context))return PetTownPlacementValidation.Block("Water","물이나 강둑의 안전 범위에는 설치할 수 없습니다.");
        foreach(var obstacle in context.Buildings)if(body.Overlaps(obstacle.Footprint))return PetTownPlacementValidation.Block("Building","다른 건물과 겹칩니다.");
        if(PetTownRoadPath.Overlaps(body))return PetTownPlacementValidation.Block("Road","도로 위에는 건물을 설치할 수 없습니다.");
        Vector3 entrance=center+record.Rotation*record.entranceLocal;
        Vector3 forward=record.EntranceForward;
        Rect entry=EntranceArea(entrance,forward,Mathf.Max(1.6f,settings.minimumPassageWidth),settings.entranceDepth);
        foreach(var obstacle in context.Buildings)
        {
            if(record.hasEntrance&&entry.Overlaps(obstacle.Footprint))return PetTownPlacementValidation.Block("Entrance","출입구 공간이 부족합니다.");
            Rect oldEntry=EntranceArea(obstacle.Entrance,obstacle.Forward,Mathf.Max(1.6f,settings.minimumPassageWidth),settings.entranceDepth);
            if(obstacle.HasEntrance&&body.Overlaps(oldEntry))return PetTownPlacementValidation.Block("ExistingEntrance","기존 건물의 출입구를 막습니다.");
            if(Expand(body,settings.minimumPassageWidth).Overlaps(obstacle.Footprint))return PetTownPlacementValidation.Block("Passage","NPC 통로 너비가 부족합니다.");
            if(earthwork.Overlaps(obstacle.Footprint)&&Mathf.Abs(center.y-obstacle.GroundY)>.08f)return PetTownPlacementValidation.Block("Earthwork","지형 평탄화 범위가 기존 건물에 영향을 줍니다.");
            if(obstacle.HasEntrance&&earthwork.Overlaps(oldEntry)&&Mathf.Abs(center.y-obstacle.GroundY)>.25f)return PetTownPlacementValidation.Block("EarthworkEntrance","지형 평탄화로 기존 출입구에 단차가 생깁니다.");
        }
        int nx=Mathf.CeilToInt(body.width),nz=Mathf.CeilToInt(body.height);
        float dx=body.width/nx,dz=body.height/nz,min=float.MaxValue,max=float.MinValue;
        var previous=new float[nx+1];
        for(int z=0;z<=nz;z++)
        {
            float left=0;
            for(int x=0;x<=nx;x++)
            {
                float y=context.Height(body.xMin+x*dx,body.yMin+z*dz);
                if(float.IsNaN(y)||float.IsInfinity(y))return PetTownPlacementValidation.Block("Terrain","지형 높이를 확인할 수 없습니다.");
                min=Mathf.Min(min,y);max=Mathf.Max(max,y);
                if(!assumeGraded&&((x>0&&Mathf.Atan2(Mathf.Abs(y-left),dx)*Mathf.Rad2Deg>settings.maxSlope)||(z>0&&Mathf.Atan2(Mathf.Abs(y-previous[x]),dz)*Mathf.Rad2Deg>settings.maxSlope)))return PetTownPlacementValidation.Block("Slope","지형 경사가 너무 가파릅니다.");
                previous[x]=y;left=y;
            }
        }
        if(!assumeGraded&&max-min>settings.maxHeightRange)return PetTownPlacementValidation.Block("HeightRange","지형 높이 차이가 너무 큽니다.");
        if(!assumeGraded&&!GradingWalkable(center,record,body,earthwork,context))return PetTownPlacementValidation.Block("GradingSlope","평탄화 후 주변 통행 지형이 너무 가파릅니다.");
        Vector2 gradingCore=PetTownBuildingDefinition.Default(record.kind).footprintMeters;
        if(record.kind==2)gradingCore=new Vector2(9.2f,9.2f);
        else if(record.kind==0||record.kind==1||record.kind==3||record.kind==4)gradingCore+=Vector2.one*5f;
        if(Mathf.RoundToInt(record.BuildingYaw/90f)%2!=0)gradingCore=new Vector2(gradingCore.y,gradingCore.x);
        foreach(var road in PetTownRoadPath.Candidates(center,EarthworkSize(record).magnitude*.5f+6f))
        {
            if(!Expand(earthwork,road.Width*.5f).Contains(new Vector2(road.Point.x,road.Point.z)))continue;
            Vector3 side=Vector3.Cross(Vector3.up,road.Tangent)*road.Width*.5f;
            foreach(var point in new[]{road.Point,road.Point+side,road.Point-side})
            {
                float old=context.Height(point.x,point.z);
                if(Mathf.Abs(PredictedHeight(center,gradingCore,point.x,point.z,old)-old)>.05f)return PetTownPlacementValidation.Block("RoadEarthwork","평탄화가 기존 도로 높이에 영향을 줍니다. 위치를 조정해 주세요.");
            }
        }
        if(context.AreaClear!=null&&!context.AreaClear(body,center.y))return PetTownPlacementValidation.Block("Obstacle","건설 영역에 장애물이 있습니다.");
        float approachDistance=settings.entranceDepth;
        var navigationBody=Expand(body,settings.minimumPassageWidth*.5f+.15f);
        for(int i=0;i<256&&navigationBody.Contains(new Vector2(entrance.x+forward.x*approachDistance,entrance.z+forward.z*approachDistance));i++)approachDistance+=.5f;
        Vector3 approach=entrance+forward*approachDistance;approach.y=context.Height(approach.x,approach.z);
        if(record.hasEntrance)
        {
            for(int i=0;i<=4;i++)
            {
                Vector3 p=Vector3.Lerp(entrance,approach,i/4f);
                if(WaterAt(p.x,p.z,context))return PetTownPlacementValidation.Block("EntranceWater","출입구 앞에 물이 있습니다.");
            }
            if(context.PassageClear!=null&&!context.PassageClear(entrance,approach,settings.minimumPassageWidth*.5f))return PetTownPlacementValidation.Block("EntranceObstacle","출입구 앞 통행 공간이 부족합니다.");
        }
        if(settings.checkNavigation&&context.Navigation!=null)
        {
            string message=context.Navigation(approach,record.hasEntrance,body,settings.minimumPassageWidth*.5f,AnchorsOutside(context,Expand(body,settings.minimumPassageWidth*.5f+1.4f)));
            if(message!=null)return PetTownPlacementValidation.Block("Navigation",message);
        }
        var obstacles=new List<Rect>();foreach(var obstacle in context.Buildings)obstacles.Add(obstacle.Footprint);
        var connection=PetTownRoadConnections.Query(center,record,settings.roadGap,settings.minimumPassageWidth,context.Height,(x,z)=>WaterAt(x,z,context),context.PassageClear,obstacles);
        bool connected=connection.Connected;
        return new PetTownPlacementValidation{CanPlace=true,RoadConnected=connected,Code=connected?"Connected":"NoRoad",Message=connected?"설치 가능합니다.":"도로와 연결되지 않았습니다. 설치는 가능합니다."};
    }
}