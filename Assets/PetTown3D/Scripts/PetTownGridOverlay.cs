using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

// One local, cached mesh. No physics colliders, and explicitly excluded from NavMesh.
[DisallowMultipleComponent]
public sealed class PetTownGridOverlay : MonoBehaviour
{
    Mesh mesh;
    Material material;
    MeshRenderer meshRenderer;
    Vector2Int lastCell;
    Vector2 lastOrigin;
    float lastSize,lastLimit;
    int lastRadius;
    bool dirty=true;
    readonly List<Vector3> vertices=new List<Vector3>();
    readonly List<Color> colors=new List<Color>();
    readonly List<int> triangles=new List<int>();
    readonly Dictionary<Vector2Int,float> heights=new Dictionary<Vector2Int,float>();
    public int RebuildCount { get; private set; }
    public int HeightSampleCount { get; private set; }
    public bool IsVisible => meshRenderer!=null && meshRenderer.enabled;

    bool Initialize()
    {
        if(meshRenderer!=null)return material!=null;
        var shader=Resources.Load<Shader>("Materials/PetTownBuildGrid");
        if(shader==null || !shader.isSupported)
        {
            Debug.LogError("PetTown: construction grid shader unavailable. Placement is still enabled.");
            return false;
        }
        material=new Material(shader){name="PetTown_ConstructionGrid_Runtime"};
        mesh=new Mesh(){name="PetTown_LocalConstructionGrid"};
        mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        meshRenderer=gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial=material;
        meshRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows=false;
        var modifier=gameObject.GetComponent<NavMeshModifier>();
        if(modifier==null)modifier=gameObject.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild=true;
        modifier.applyToChildren=true;
        return true;
    }
    public void Hide()
    {
        if(meshRenderer!=null)meshRenderer.enabled=false;
    }
    public void Invalidate()
    {
        dirty=true;
        heights.Clear();
    }
    public void Show(Vector3 point,float cellSize,Vector2 origin,int radiusCells,float worldHalfSize,Func<float,float,float> height)
    {
        if(height==null || !Initialize())return;
        cellSize=PetTownBuildGrid.ValidCellSize(cellSize);
        radiusCells=Mathf.Clamp(radiusCells,4,24);
        Vector2Int cell=PetTownBuildGrid.WorldToCell(point,cellSize,origin);
        meshRenderer.enabled=true;
        bool settingsChanged=lastSize!=cellSize || lastOrigin!=origin || lastRadius!=radiusCells || lastLimit!=worldHalfSize;
        if(!dirty && !settingsChanged && cell==lastCell)return;
        if(settingsChanged || heights.Count>60000)heights.Clear();
        lastSize=cellSize;lastOrigin=origin;lastRadius=radiusCells;lastLimit=worldHalfSize;lastCell=cell;
        dirty=false;
        vertices.Clear();colors.Clear();triangles.Clear();
        float cx=origin.x+cell.x*cellSize,cz=origin.y+cell.y*cellSize;
        float extent=radiusCells*cellSize;
        int parts=Mathf.CeilToInt(extent*2f/Mathf.Min(cellSize,.5f));
        for(int line=-radiusCells;line<=radiusCells;line++)
        {
            float offset=line*cellSize;
            for(int i=0;i<parts;i++)
            {
                float a=Mathf.Lerp(-extent,extent,i/(float)parts),b=Mathf.Lerp(-extent,extent,(i+1)/(float)parts);
                AddSegment(new Vector2(cx+offset,cz+a),new Vector2(cx+offset,cz+b),(cell.x+line)%5==0,cx,cz,extent,worldHalfSize,height);
                AddSegment(new Vector2(cx+a,cz+offset),new Vector2(cx+b,cz+offset),(cell.y+line)%5==0,cx,cz,extent,worldHalfSize,height);
            }
        }
        mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);
        mesh.RecalculateBounds();
        RebuildCount++;
    }
    float Height(Vector2 p,Func<float,float,float> sample)
    {
        var key=new Vector2Int(Mathf.RoundToInt(p.x*1000f),Mathf.RoundToInt(p.y*1000f));
        if(!heights.TryGetValue(key,out float y))
        {
            y=sample(p.x,p.y);heights[key]=y;HeightSampleCount++;
        }
        return y;
    }
    void AddSegment(Vector2 a,Vector2 b,bool major,float cx,float cz,float extent,float limit,Func<float,float,float> sample)
    {
        float halfWidth=major?.022f:.014f;
        if(Mathf.Max(Mathf.Max(Mathf.Abs(a.x),Mathf.Abs(a.y)),Mathf.Max(Mathf.Abs(b.x),Mathf.Abs(b.y)))>limit-halfWidth)return;
        Vector2 side=new Vector2(-(b-a).y,(b-a).x).normalized*halfWidth;
        float ya=Height(a,sample)+.045f,yb=Height(b,sample)+.045f;
        int start=vertices.Count;
        vertices.Add(new Vector3(a.x-side.x,ya,a.y-side.y));
        vertices.Add(new Vector3(a.x+side.x,ya,a.y+side.y));
        vertices.Add(new Vector3(b.x-side.x,yb,b.y-side.y));
        vertices.Add(new Vector3(b.x+side.x,yb,b.y+side.y));
        Color ca=major?new Color(1f,.96f,.66f,.85f):new Color(.70f,1f,.95f,.62f);
        Color cb=ca;
        ca.a*=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.78f,1f,Mathf.Max(Mathf.Abs(a.x-cx),Mathf.Abs(a.y-cz))/extent));
        cb.a*=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.78f,1f,Mathf.Max(Mathf.Abs(b.x-cx),Mathf.Abs(b.y-cz))/extent));
        colors.Add(ca);colors.Add(ca);colors.Add(cb);colors.Add(cb);
        triangles.Add(start);triangles.Add(start+2);triangles.Add(start+1);
        triangles.Add(start+1);triangles.Add(start+2);triangles.Add(start+3);
    }
    void OnDisable(){Hide();}
    void OnDestroy()
    {
        if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
        if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
    }
}