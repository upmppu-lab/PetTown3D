using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Read-only queries against the current NavMesh. A candidate rectangle is a
// virtual obstacle; agents and the live NavMesh are never changed by this check.
public static class PetTownNavigationQueries
{
    public static bool SegmentIntersects(Rect rect,Vector3 a,Vector3 b)
    {
        float lo=0f,hi=1f;
        return Clip(a.x,b.x-a.x,rect.xMin,rect.xMax,ref lo,ref hi)&&Clip(a.z,b.z-a.z,rect.yMin,rect.yMax,ref lo,ref hi);
    }
    static bool Clip(float a,float delta,float min,float max,ref float lo,ref float hi)
    {
        if(Mathf.Abs(delta)<.00001f)return a>=min&&a<=max;
        float t0=(min-a)/delta,t1=(max-a)/delta;
        if(t0>t1){float t=t0;t0=t1;t1=t;}
        lo=Mathf.Max(lo,t0);hi=Mathf.Min(hi,t1);return lo<=hi;
    }
    static bool Touches(NavMeshPath path,Rect rect)
    {
        var corners=path.corners;
        for(int i=1;i<corners.Length;i++)if(SegmentIntersects(rect,corners[i-1],corners[i]))return true;
        return false;
    }
    static bool Path(Vector3 a,Vector3 b,NavMeshPath scratch)
    {
        return NavMesh.CalculatePath(a,b,NavMesh.AllAreas,scratch)&&scratch.status==NavMeshPathStatus.PathComplete;
    }
    static bool Sample(Vector3 p,out Vector3 sampled)
    {
        if(NavMesh.SamplePosition(p,out NavMeshHit hit,1.2f,NavMesh.AllAreas)){sampled=hit.position;return true;}
        sampled=p;return false;
    }
    static bool Detour(Vector3 start,Vector3 target,Rect blocked,NavMeshPath scratch)
    {
        var nodes=new Vector3[6];var valid=new bool[6];
        nodes[0]=start;nodes[1]=target;valid[0]=valid[1]=true;
        float y=(start.y+target.y)*.5f;
        var corners=new[]{new Vector3(blocked.xMin-.1f,y,blocked.yMin-.1f),new Vector3(blocked.xMin-.1f,y,blocked.yMax+.1f),new Vector3(blocked.xMax+.1f,y,blocked.yMin-.1f),new Vector3(blocked.xMax+.1f,y,blocked.yMax+.1f)};
        for(int i=0;i<4;i++)valid[i+2]=Sample(corners[i],out nodes[i+2])&&!blocked.Contains(new Vector2(nodes[i+2].x,nodes[i+2].z));
        var edges=new bool[6,6];
        for(int a=0;a<6;a++)for(int b=a+1;b<6;b++)
            if(valid[a]&&valid[b]&&Path(nodes[a],nodes[b],scratch)&&!Touches(scratch,blocked))edges[a,b]=edges[b,a]=true;
        var reached=new bool[6];reached[0]=true;
        for(int step=0;step<6;step++)for(int a=0;a<6;a++)if(reached[a])for(int b=0;b<6;b++)if(edges[a,b])reached[b]=true;
        return reached[1];
    }
    // null means verified; a message means blocked or conservatively unverified.
    public static string Check(Vector3 entrance,bool hasEntrance,Rect footprint,float clearance,List<Vector3> anchors)
    {
        Rect blocked=PetTownPlacementValidator.Expand(footprint,clearance);
        var sampled=new List<Vector3>();
        if(anchors!=null)foreach(var p in anchors)
        {
            if(!Sample(p,out Vector3 point))continue;
            if(blocked.Contains(new Vector2(point.x,point.z)))return "기존 통행 지점이나 이동 목표를 막습니다.";
            sampled.Add(point);
        }
        Vector3 entry=entrance;
        if(hasEntrance&&(!Sample(entrance,out entry)||blocked.Contains(new Vector2(entry.x,entry.z))))return "출입구에 이동 가능한 지면이 없습니다.";
        if(sampled.Count==0)return null;
        var scratch=new NavMeshPath();Vector3 reference=sampled[0];
        if(hasEntrance)
        {
            if(!Path(reference,entry,scratch))return "출입구까지 이동 경로가 없습니다.";
            if(Touches(scratch,blocked)&&!Detour(reference,entry,blocked,scratch))return "출입구로 통하는 경로를 확보할 수 없습니다.";
        }
        for(int i=1;i<sampled.Count;i++)
        {
            // Already disconnected anchors are pre-existing conditions, not a
            // reason to blame this candidate. Preserve paths that currently exist.
            if(!Path(reference,sampled[i],scratch))continue;
            if(Touches(scratch,blocked)&&!Detour(reference,sampled[i],blocked,scratch))return "기존 통행로를 막을 수 있습니다. 위치를 옮겨 주세요.";
        }
        return null;
    }
}