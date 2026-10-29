using System.Collections.Generic;
using UnityEngine;

// Curved, merged walking surface using the selected asset's actual height data.
// Local metre UVs keep stone size stable through bends and across intersections.
public sealed class PetTownStoneWalkway
{
    public const float TextureMeters=4f;
    sealed class Stroke {public readonly List<Vector2> points=new List<Vector2>();public float width;}
    readonly List<Stroke> strokes=new List<Stroke>();readonly int kind;
    public PetTownStoneWalkway(int kind,Vector3 entrance)
    {
        this.kind=kind;
        if(kind==7)
        {
            Curve(2.1f,new Vector2(entrance.x,-11),new Vector2(entrance.x*.5f,-9),new Vector2(0,-6.5f),new Vector2(-.1f,.4f),new Vector2(-.2f,6.2f),new Vector2(0,9.9f));
            Curve(1.65f,new Vector2(0,-6.5f),new Vector2(-3.4f,-7.8f),new Vector2(-8.8f,-7.7f),new Vector2(-9.8f,-3),new Vector2(-9.7f,4.4f),new Vector2(-8.7f,9.2f),new Vector2(0,9.9f),new Vector2(8.4f,9.8f),new Vector2(9.7f,6.2f),new Vector2(9.6f,2.6f),new Vector2(9.7f,-3.1f),new Vector2(8.7f,-6.3f),new Vector2(4.2f,-6.6f),new Vector2(0,-6.5f));
            Curve(1.6f,new Vector2(-9.8f,-.3f),new Vector2(-5.4f,-.3f),new Vector2(-.1f,.4f));
            Curve(1.5f,new Vector2(-.1f,3.1f),new Vector2(4.1f,3.15f),new Vector2(8,3.35f),new Vector2(9.6f,2.6f));
        }
        else if(kind==5)
        {
            Curve(2.5f,new Vector2(0,-7.25f),new Vector2(-.38f,-4.8f),new Vector2(.22f,-2.6f),Vector2.zero,new Vector2(.4f,3.4f),new Vector2(0,7.25f));
            Curve(2.5f,new Vector2(-7.25f,0),new Vector2(-4.8f,-.28f),Vector2.zero,new Vector2(4.7f,.32f),new Vector2(7.25f,0));
            for(int side=-1;side<=1;side+=2)for(int z=-1;z<=1;z+=2)
                Curve(1.05f,new Vector2(side*3.25f,.1f*z),new Vector2(side*3.5f,z*1.05f),new Vector2(side*3.7f,z*2));
            Curve(2.5f,new Vector2(entrance.x,-7.6f),new Vector2(entrance.x*.6f,-6.5f),new Vector2(-.3f,-5.3f));
        }
        else
        {
            var ring=new Stroke{width=1.3f};
            // Rounded rectangle stays outside the unchanged rectangular water exclusion.
            for(int corner=0;corner<4;corner++)
            {
                float sx=corner==0||corner==3?1:-1,sz=corner<2?1:-1;
                var center=new Vector2(sx*4.65f,1+sz*3.5f);
                for(int i=0;i<=12;i++)
                {
                    float angle=(corner*90+i*7.5f)*Mathf.Deg2Rad;
                    ring.points.Add(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.75f);
                }
            }
            ring.points.Add(ring.points[0]);strokes.Add(ring);
            Curve(2.25f,new Vector2(entrance.x,-7.6f),new Vector2(entrance.x*.5f+.22f,-6.2f),new Vector2(-.2f,-4.8f),new Vector2(0,-3.25f));
        }
    }
    void Curve(float width,params Vector2[] controls)
    {
        var stroke=new Stroke{width=width};
        for(int i=0;i<controls.Length-1;i++)for(int step=0;step<14;step++)
        {
            float t=step/14f,t2=t*t,t3=t2*t;
            var a=controls[Mathf.Max(0,i-1)];var b=controls[i];var c=controls[i+1];var d=controls[Mathf.Min(controls.Length-1,i+2)];
            stroke.points.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3));
        }
        stroke.points.Add(controls[controls.Length-1]);strokes.Add(stroke);
    }
    public float Field(Vector2 p)
    {
        float field=kind==5?1.95f-p.magnitude:-100f;
        foreach(var stroke in strokes)
        {
            float nearest=float.MaxValue;
            for(int i=1;i<stroke.points.Count;i++)
            {
                var a=stroke.points[i-1];var line=stroke.points[i]-a;
                float t=Mathf.Clamp01(Vector2.Dot(p-a,line)/Mathf.Max(.000001f,line.sqrMagnitude));
                nearest=Mathf.Min(nearest,(p-a-line*t).sqrMagnitude);
            }
            float width=stroke.width*.5f*(1+.025f*Mathf.Sin(p.x*1.35f+p.y*.83f));
            field=Mathf.Max(field,width-Mathf.Sqrt(nearest));
        }
        field=Mathf.Min(field,(kind==7?10.95f:7.45f)-Mathf.Abs(p.x),(kind==7?10.95f:7.55f)-Mathf.Abs(p.y));
        if(kind==7)field=Mathf.Min(field,PetTownZooLayout.Outside(p)-.20f);
        if(kind==6)field=Mathf.Min(field,Mathf.Max(Mathf.Abs(p.x)-4.65f,Mathf.Abs(p.y-1)-3.5f)-.025f);
        return field;
    }
    public bool Contains(Vector3 point,float margin)=>Field(new Vector2(point.x,point.z))>=-margin;
    sealed class Builder
    {
        public readonly List<Vector3> vertices=new List<Vector3>();public readonly List<Vector2> uv=new List<Vector2>();public readonly List<int> indices=new List<int>();
        readonly Dictionary<long,int> lookup=new Dictionary<long,int>();readonly PetTownStoneWalkway layout;readonly Texture2D height;
        public Builder(PetTownStoneWalkway layout,Texture2D height){this.layout=layout;this.height=height;}
        int Vertex(Vector2 p)
        {
            long key=((long)Mathf.RoundToInt((p.x+8)*100000)<<32)|(uint)Mathf.RoundToInt((p.y+8)*100000);
            if(lookup.TryGetValue(key,out var existing))return existing;
            int index=vertices.Count;lookup.Add(key,index);
            var tex=p/TextureMeters;
            float h=height&&height.isReadable?height.GetPixelBilinear(Mathf.Repeat(tex.x,1),Mathf.Repeat(tex.y,1)).r:.7f;
            float y=Mathf.Lerp(.048f,.058f+h*.022f,Mathf.Clamp01(layout.Field(p)/.14f));
            vertices.Add(new Vector3(p.x,y,p.y));uv.Add(tex);return index;
        }
        public void Triangle(Vector2 a,Vector2 b,Vector2 c,float fa,float fb,float fc)
        {
            if(fa<0&&fb<0&&fc<0)return;
            var p=new[]{a,b,c};var f=new[]{fa,fb,fc};var clipped=new List<Vector2>(4);
            for(int i=0;i<3;i++)
            {
                int j=(i+1)%3;if(f[i]>=0)clipped.Add(p[i]);
                if((f[i]>=0)!=(f[j]>=0))clipped.Add(Vector2.Lerp(p[i],p[j],f[i]/(f[i]-f[j])));
            }
            if(clipped.Count<3)return;
            int first=Vertex(clipped[0]);
            for(int i=1;i<clipped.Count-1;i++){int second=Vertex(clipped[i]),third=Vertex(clipped[i+1]);if(first==second||first==third||second==third||Vector3.Cross(vertices[second]-vertices[first],vertices[third]-vertices[first]).sqrMagnitude<1e-14f)continue;indices.Add(first);indices.Add(second);indices.Add(third);}
        }
    }
    public Mesh CreateMesh(Texture2D height,float expansion=0)
    {
        float start=kind==7?-11.04f:-7.6f;const float step=.16f;int cells=kind==7?138:95;
        var values=new float[cells+1,cells+1];for(int x=0;x<=cells;x++)for(int z=0;z<=cells;z++)values[x,z]=Field(new Vector2(start+x*step,start+z*step))+expansion;
        var builder=new Builder(this,height);
        for(int x=0;x<cells;x++)for(int z=0;z<cells;z++)
        {
            var a=new Vector2(start+x*step,start+z*step);var b=a+Vector2.up*step;var c=a+Vector2.one*step;var d=a+Vector2.right*step;
            builder.Triangle(a,b,c,values[x,z],values[x,z+1],values[x+1,z+1]);
            builder.Triangle(a,c,d,values[x,z],values[x+1,z+1],values[x+1,z]);
        }
        var mesh=new Mesh{name="SelectedStoneCurvedWalkway"};if(builder.vertices.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(builder.vertices);mesh.SetUVs(0,builder.uv);mesh.SetTriangles(builder.indices,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }
}
