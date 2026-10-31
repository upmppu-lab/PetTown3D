using System;
using UnityEngine;

// Fixed world cells preserve discoveries across camera moves, saves and map expansion.
[Serializable]
public sealed class PetTownExplorationState
{
    public const float CellSize=2;
    public int version=1,minX,minZ,width,height;
    public string cells;
    [NonSerialized] byte[] pixels;
    [NonSerialized] bool saveDirty;
    [NonSerialized] int revision;
    public int Revision=>revision;
    public byte[] Pixels=>pixels;
    public Vector4 MapBounds=>new Vector4(minX*CellSize,minZ*CellSize,width*CellSize,height*CellSize);
    public void EnsureBounds(float halfExtent)
    {
        if(pixels==null)
        {
            if(Math.Abs((long)minX)<10000&&Math.Abs((long)minZ)<10000&&width>0&&height>0&&width<=1024&&height<=1024&&!string.IsNullOrEmpty(cells)&&cells.Length<=1400000)
            {try{var decoded=Convert.FromBase64String(cells);if(decoded.Length==width*height)pixels=decoded;}catch(FormatException){}}
            if(pixels==null){minX=minZ=width=height=0;pixels=new byte[0];}
        }
        int first=Mathf.FloorToInt(-halfExtent/CellSize),last=Mathf.CeilToInt(halfExtent/CellSize),size=last-first;
        size=Mathf.Clamp(size,1,1024);
        // Never shrink previously discovered world data.
        int x0=width>0?Mathf.Min(first,minX):first,z0=height>0?Mathf.Min(first,minZ):first;
        int x1=Mathf.Max(first+size,minX+width),z1=Mathf.Max(first+size,minZ+height);
        int w=x1-x0,h=z1-z0;if(w>1024||h>1024)return;
        if(minX==x0&&minZ==z0&&width==w&&height==h)return;
        var next=new byte[w*h];for(int z=0;z<height;z++)Array.Copy(pixels,z*width,next,(z+minZ-z0)*w+minX-x0,width);
        minX=x0;minZ=z0;width=w;height=h;pixels=next;saveDirty=true;revision++;
    }
    public bool Reveal(Vector3 center,float radius,float feather=10)
    {
        if(pixels==null||width==0||!float.IsFinite(center.x)||!float.IsFinite(center.z))return false;
        radius=Mathf.Max(0,radius);feather=Mathf.Max(.1f,feather);float outer=radius+feather;bool changed=false;
        int x0=Mathf.Max(0,Mathf.FloorToInt((center.x-outer)/CellSize)-minX),x1=Mathf.Min(width-1,Mathf.CeilToInt((center.x+outer)/CellSize)-minX);
        int z0=Mathf.Max(0,Mathf.FloorToInt((center.z-outer)/CellSize)-minZ),z1=Mathf.Min(height-1,Mathf.CeilToInt((center.z+outer)/CellSize)-minZ);
        for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
        {
            float distance=Vector2.Distance(new Vector2((minX+x+.5f)*CellSize,(minZ+z+.5f)*CellSize),new Vector2(center.x,center.z));
            byte value=(byte)Mathf.RoundToInt(255*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(radius,outer,distance))));int index=z*width+x;
            if(value<=pixels[index])continue;pixels[index]=value;changed=true;
        }
        if(changed){saveDirty=true;revision++;}return changed;
    }
    public float Sample(Vector3 point)
    {
        if(pixels==null||width==0||point.x<minX*CellSize||point.z<minZ*CellSize||point.x>=(minX+width)*CellSize||point.z>=(minZ+height)*CellSize)return 0;
        float x=point.x/CellSize-minX-.5f,z=point.z/CellSize-minZ-.5f;int a=Mathf.FloorToInt(x),b=Mathf.FloorToInt(z);
        int x0=Mathf.Clamp(a,0,width-1),x1=Mathf.Clamp(a+1,0,width-1),z0=Mathf.Clamp(b,0,height-1),z1=Mathf.Clamp(b+1,0,height-1);
        return Mathf.Lerp(Mathf.Lerp(pixels[z0*width+x0],pixels[z0*width+x1],x-a),Mathf.Lerp(pixels[z1*width+x0],pixels[z1*width+x1],x-a),z-b)/255f;
    }
    public bool HasExplored(Bounds bounds)
    {
        if(pixels==null)return false;
        int x0=Mathf.Max(0,Mathf.FloorToInt(bounds.min.x/CellSize)-minX-1),x1=Mathf.Min(width-1,Mathf.CeilToInt(bounds.max.x/CellSize)-minX+1);
        int z0=Mathf.Max(0,Mathf.FloorToInt(bounds.min.z/CellSize)-minZ-1),z1=Mathf.Min(height-1,Mathf.CeilToInt(bounds.max.z/CellSize)-minZ+1);
        for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)if(pixels[z*width+x]>0)return true;return false;
    }
    public void PrepareSave(){if(saveDirty&&pixels!=null){cells=Convert.ToBase64String(pixels);saveDirty=false;}}
}
