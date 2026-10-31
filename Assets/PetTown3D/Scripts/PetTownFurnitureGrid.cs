using System.Collections.Generic;
using UnityEngine;

// Small, floor-aligned mesh. No terrain raycasts, colliders or NavMesh sources.
public sealed class PetTownFurnitureGrid : MonoBehaviour
{
    Mesh mesh,markerMesh;
    MeshFilter marker;
    Material markerMaterial;
    Material material;
    PetTownInteriorState current;
    float currentCell;
    Rect currentBounds;
    public void Show(PetTownInteriorState owner,float cell)
    {
        if(owner==null){Hide();return;}
        gameObject.SetActive(true);if(current==owner&&Mathf.Approximately(currentCell,cell)&&currentBounds==owner.LocalBounds)return;
        currentCell=PetTownBuildGrid.ValidCellSize(cell);transform.SetParent(owner.transform,false);transform.localPosition=Vector3.up*(owner.FloorOffset+.025f);transform.localRotation=Quaternion.identity;transform.localScale=Vector3.one;
        var mf=GetComponent<MeshFilter>();if(mf==null)mf=gameObject.AddComponent<MeshFilter>();var renderer=GetComponent<MeshRenderer>();if(renderer==null)renderer=gameObject.AddComponent<MeshRenderer>();
        if(material==null){var shader=Resources.Load<Shader>("Materials/PetTownPlacementPreview");material=new Material(shader);material.SetColor("_BaseColor",new Color(.12f,.42f,.28f,.55f));}renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        var verts=new List<Vector3>();var triangles=new List<int>();var bounds=owner.LocalBounds;
        void Line(float x0,float z0,float x1,float z1)
        {
            var a=new Vector3(x0,0,z0);var b=new Vector3(x1,0,z1);var side=Vector3.Cross(Vector3.up,(b-a).normalized)*.015f;int i=verts.Count;verts.Add(a-side);verts.Add(a+side);verts.Add(b-side);verts.Add(b+side);triangles.AddRange(new[]{i,i+2,i+1,i+2,i+3,i+1});
        }
        for(float x=Mathf.Ceil(bounds.xMin/currentCell)*currentCell;x<=bounds.xMax+.001f;x+=currentCell)Line(x,bounds.yMin,x,bounds.yMax);
        for(float z=Mathf.Ceil(bounds.yMin/currentCell)*currentCell;z<=bounds.yMax+.001f;z+=currentCell)Line(bounds.xMin,z,bounds.xMax,z);
        if(mesh!=null)Release(mesh);mesh=new Mesh();mesh.SetVertices(verts);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mf.sharedMesh=mesh;
        var modifier=GetComponent<Unity.AI.Navigation.NavMeshModifier>();if(modifier==null)modifier=gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();modifier.ignoreFromBuild=true;current=owner;currentBounds=owner.LocalBounds;
    }
    public void ShowPlacement(Vector3 point,Vector2 size,int quarter,bool valid)
    {
        if(current==null)return;
        if(marker==null)
        {
            var go=new GameObject("Furniture_Footprint_Preview");go.transform.SetParent(transform,false);go.transform.localPosition=Vector3.up*.015f;marker=go.AddComponent<MeshFilter>();
            var renderer=go.AddComponent<MeshRenderer>();markerMaterial=new Material(Resources.Load<Shader>("Materials/PetTownPlacementPreview"));renderer.sharedMaterial=markerMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            markerMesh=new Mesh();marker.sharedMesh=markerMesh;
        }
        markerMaterial.SetColor("_BaseColor",valid?new Color(.08f,.85f,.25f,.85f):new Color(1,.12f,.08f,.85f));
        var area=PetTownFurniturePlanner.Area(point,current,size,quarter);var vertices=new List<Vector3>();var triangles=new List<int>();
        void Line(Vector3 a,Vector3 b)
        {
            var side=Vector3.Cross(Vector3.up,(b-a).normalized)*.025f;int i=vertices.Count;vertices.Add(a-side);vertices.Add(a+side);vertices.Add(b-side);vertices.Add(b+side);triangles.AddRange(new[]{i,i+2,i+1,i+2,i+3,i+1});
        }
        var a=new Vector3(area.xMin,0,area.yMin);var b=new Vector3(area.xMax,0,area.yMin);var c=new Vector3(area.xMax,0,area.yMax);var d=new Vector3(area.xMin,0,area.yMax);
        Line(a,b);Line(b,c);Line(c,d);Line(d,a);markerMesh.Clear();markerMesh.SetVertices(vertices);markerMesh.SetTriangles(triangles,0);markerMesh.RecalculateBounds();
    }
    public void Hide(){gameObject.SetActive(false);}
    static void Release(Object value){if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    void OnDestroy(){Release(mesh);Release(material);Release(markerMesh);Release(markerMaterial);}
}
