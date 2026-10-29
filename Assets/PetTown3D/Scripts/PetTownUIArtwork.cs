using System.Collections.Generic;
using UnityEngine;

// Read-only display crop: source textures and the target's photo/export UV remain unchanged.
public static class PetTownUIArtwork
{
    static readonly Dictionary<Texture2D,Dictionary<Rect,Rect>> bounds=new Dictionary<Texture2D,Dictionary<Rect,Rect>>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache(){bounds.Clear();}
    public static Rect ContentUV(Texture2D source,Rect frame)
    {
        if(!source)return frame;
        Dictionary<Rect,Rect> entries;if(!bounds.TryGetValue(source,out entries)){entries=new Dictionary<Rect,Rect>();bounds[source]=entries;}
        Rect cached;if(entries.TryGetValue(frame,out cached))return cached;
        Rect result=frame;RenderTexture temporary=null;Texture2D copy=null;var previous=RenderTexture.active;
        try
        {
            Color32[] pixels;
            if(source.isReadable)pixels=source.GetPixels32();
            else
            {
                temporary=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
                Graphics.Blit(source,temporary);RenderTexture.active=temporary;
                copy=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
                copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0,false);pixels=copy.GetPixels32();
            }
            int left=Mathf.Clamp(Mathf.FloorToInt(frame.xMin*source.width),0,source.width-1),right=Mathf.Clamp(Mathf.CeilToInt(frame.xMax*source.width),left+1,source.width);
            int bottom=Mathf.Clamp(Mathf.FloorToInt(frame.yMin*source.height),0,source.height-1),top=Mathf.Clamp(Mathf.CeilToInt(frame.yMax*source.height),bottom+1,source.height);
            int minX=right,minY=top,maxX=left-1,maxY=bottom-1;
            for(int y=bottom;y<top;y++)for(int x=left;x<right;x++)if(pixels[y*source.width+x].a>24){minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
            if(maxX>=minX&&maxY>=minY)
            {
                float pad=Mathf.Max(maxX-minX+1,maxY-minY+1)*.06f;
                float x0=Mathf.Max(left,minX-pad),y0=Mathf.Max(bottom,minY-pad),x1=Mathf.Min(right,maxX+1+pad),y1=Mathf.Min(top,maxY+1+pad);
                result=new Rect(x0/source.width,y0/source.height,(x1-x0)/source.width,(y1-y0)/source.height);
            }
        }
        finally
        {
            RenderTexture.active=previous;if(temporary)RenderTexture.ReleaseTemporary(temporary);
            if(copy){if(Application.isPlaying)Object.Destroy(copy);else Object.DestroyImmediate(copy);}
        }
        entries[frame]=result;return result;
    }
}
