using System.Collections.Generic;
using UnityEngine;

public enum PetTownRoadMode { FreeCurve=0, GridStraight=1 }
// Input geometry only. The established renderer, terrain sampling and cost pipeline are reused.
public static class PetTownRoadDrawing
{
    public static List<Vector3> Straight(Vector3 start,Vector3 end,float cell,Vector2 origin,float spacing=.25f)
    {
        start=PetTownBuildGrid.SnapHorizontal(start,cell,origin);
        end=PetTownBuildGrid.SnapHorizontal(end,cell,origin);
        Vector3 delta=end-start;
        if(Mathf.Abs(delta.x)>=Mathf.Abs(delta.z))end.z=start.z;else end.x=start.x;
        float length=Vector2.Distance(new Vector2(start.x,start.z),new Vector2(end.x,end.z));
        var result=new List<Vector3>();if(length<.01f)return result;
        int count=Mathf.CeilToInt(length/Mathf.Max(.1f,spacing));
        for(int i=0;i<=count;i++)result.Add(Vector3.Lerp(start,end,i/(float)count));
        return result;
    }
    public static List<Vector3> Junctions(IList<Vector3> points,float width)
    {
        var found=new List<Vector3>();if(points==null)return found;
        for(int i=1;i<points.Count;i++)
        {
            Vector3 a=points[i-1],b=points[i];float length=Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
            foreach(var hit in PetTownRoadPath.Candidates((a+b)*.5f,length*.5f+width))
            {
                Vector2 r=new Vector2(b.x-a.x,b.z-a.z),q=new Vector2(hit.To.x-hit.From.x,hit.To.z-hit.From.z),c=new Vector2(hit.From.x-a.x,hit.From.z-a.z);
                float denominator=Cross(r,q);if(Mathf.Abs(denominator)<.00001f)continue;
                float along=Cross(c,q)/denominator,other=Cross(c,r)/denominator;
                if(along>=0&&along<=1&&other>=0&&other<=1)AddJunction(found,Vector3.Lerp(a,b,along));
            }
        }
        if(points.Count>0)foreach(var end in new[]{points[0],points[points.Count-1]})
            foreach(var hit in PetTownRoadPath.Candidates(end,width+6f))
                if(Vector2.Distance(new Vector2(end.x,end.z),new Vector2(hit.Point.x,hit.Point.z))<=(width+hit.Width)*.5f)AddJunction(found,hit.Point);
        return found;
    }
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    static void AddJunction(List<Vector3> found,Vector3 point)
    {
        foreach(var old in found)if(Vector2.Distance(new Vector2(old.x,old.z),new Vector2(point.x,point.z))<1f)return;
        found.Add(point);
    }
}
