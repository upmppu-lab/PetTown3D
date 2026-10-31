using System.Collections.Generic;
using UnityEngine;

// One visual mesh: boards of varying lengths, staggered joints, narrow seams and bevels.
// The existing room collider remains the standing surface.
public static class PetTownCozyFloor
{
    public static Mesh Create(Rect area,float y)
    {
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();
        var random=new System.Random(731);
        Quad(vertices,uv,colors,triangles,new Vector3(area.xMin,y-.013f,area.yMin),new Vector3(area.xMin,y-.013f,area.yMax),new Vector3(area.xMax,y-.013f,area.yMax),new Vector3(area.xMax,y-.013f,area.yMin),new Color(.21f,.115f,.045f,0));
        for(float z=area.yMin;z<area.yMax-.001f;)
        {
            float width=Mathf.Min(.30f+(float)random.NextDouble()*.11f,area.yMax-z),x=area.xMin;
            bool first=true;
            while(x<area.xMax-.001f)
            {
                float length=first ? .45f+(float)random.NextDouble()*1.6f : 1.2f+(float)random.NextDouble()*1.5f;first=false;length=Mathf.Min(length,area.xMax-x);
                float inset=.0015f,bevel=.0025f,x0=x+inset,x1=x+length-inset,z0=z+inset,z1=z+width-inset;
                float shade=(float)random.NextDouble();var color=Color.Lerp(new Color(.63f,.49f,.33f),new Color(.78f,.65f,.47f),shade);color.a=(float)random.NextDouble();
                var a=new Vector3(x0+bevel,y,z0+bevel);var b=new Vector3(x0+bevel,y,z1-bevel);var c=new Vector3(x1-bevel,y,z1-bevel);var d=new Vector3(x1-bevel,y,z0+bevel);
                Quad(vertices,uv,colors,triangles,a,b,c,d,color);
                var aa=new Vector3(x0,y-.004f,z0);var bb=new Vector3(x0,y-.004f,z1);var cc=new Vector3(x1,y-.004f,z1);var dd=new Vector3(x1,y-.004f,z0);
                Quad(vertices,uv,colors,triangles,aa,bb,b,a,color);Quad(vertices,uv,colors,triangles,b,bb,cc,c,color);Quad(vertices,uv,colors,triangles,c,cc,dd,d,color);Quad(vertices,uv,colors,triangles,d,dd,aa,a,color);
                x+=length;
            }
            z+=width;
        }
        var mesh=new Mesh();mesh.name="StaggeredOakBoards";if(vertices.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    static void Quad(List<Vector3> vertices,List<Vector2> uv,List<Color> colors,List<int> triangles,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
    {
        int n=vertices.Count;foreach(var p in new[]{a,b,c,d}){vertices.Add(p);uv.Add(new Vector2(p.x,p.z));colors.Add(color);}triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
    }
}
