using System.Collections.Generic;
using UnityEngine;

// Fixed native mesh grass, merged by material. No camera-centred patch or per-frame scatter.
public sealed class PetTownCommunityMeadow : MonoBehaviour
{
    readonly List<Mesh> owned=new List<Mesh>();readonly List<Material> ownedMaterials=new List<Material>();
    public int PatchCount{get;private set;}
    public void Initialize(PetTownCommunityView view,PetTownStoneWalkway paths)
    {
        var forest=PetTownForestLibrary.Current;if(!forest||forest.grasses==null||forest.grasses.Length==0)return;
        var groups=new Dictionary<Material,List<CombineInstance>>();
        float half=view.Kind==7?10.6f:7.1f,step=view.Kind==7?.67f:.57f;int cap=view.Kind==7?640:440;
        var random=new System.Random(572+view.Kind);var blockers=view.GetComponentsInChildren<BoxCollider>();
        for(float x=-half;x<half;x+=step)for(float z=-half;z<half;z+=step)
        {
            if(PatchCount>=cap)break;
            var p=new Vector3(x+((float)random.NextDouble()-.5f)*step,.05f,z+((float)random.NextDouble()-.5f)*step);
            float edge=paths!=null?paths.Field(new Vector2(p.x,p.z)):-100;
            if(edge>-.045f)continue;
            if(view.Kind==6&&Mathf.Abs(p.x)<4.9f&&Mathf.Abs(p.z-1)<3.7f)continue;
            if(view.Kind==7&&PetTownZooLayout.Habitats[2].Outside(new Vector2(p.x,p.z))<.15f)continue;
            bool blocked=false;foreach(var c in blockers){if(c.name.StartsWith("ZooHabitatSafety"))continue;var q=c.transform.InverseTransformPoint(view.transform.TransformPoint(p))-c.center;if(Mathf.Abs(q.x)<c.size.x*.5f+.1f&&Mathf.Abs(q.z)<c.size.z*.5f+.1f){blocked=true;break;}}if(blocked)continue;
            float patch=Mathf.PerlinNoise(p.x*.27f+17,p.z*.27f+31);if(edge<-.5f&&patch<.31f)continue;
            // Edge plants are short and loosely overlap the soil seam, never the walking lane.
            float height=edge>-.55f?.10f+(float)random.NextDouble()*.08f:.14f+(float)random.NextDouble()*.10f;
            var patches=PetTownCommunityLibrary.Current.meadowPatches;var source=patches!=null&&patches.Length>0?patches:forest.grasses;var prefab=source[PatchCount%4==0?0:Mathf.Min(1,source.Length-1)];if(!prefab)continue;
            var filters=prefab.GetComponentsInChildren<MeshFilter>(true);var bounds=new Bounds();bool first=true;
            foreach(var f in filters){if(!f.sharedMesh)continue;var b=f.sharedMesh.bounds;for(int a=-1;a<=1;a+=2)for(int b1=-1;b1<=1;b1+=2)for(int c=-1;c<=1;c+=2){var q=prefab.transform.worldToLocalMatrix.MultiplyPoint3x4(f.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(a,b1,c))));if(first){bounds=new Bounds(q,Vector3.zero);first=false;}else bounds.Encapsulate(q);}}
            float scale=height/Mathf.Max(.001f,bounds.size.y);var placement=Matrix4x4.TRS(p,Quaternion.Euler(0,(float)random.NextDouble()*360,0),new Vector3(scale*1.8f,scale,scale*1.8f))*Matrix4x4.Translate(-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z));
            foreach(var f in filters)
            {
                var r=f.GetComponent<Renderer>();if(!f.sharedMesh||!r||!f.sharedMesh.isReadable)continue;
                for(int sub=0;sub<f.sharedMesh.subMeshCount&&sub<r.sharedMaterials.Length;sub++)
                {
                    var mat=r.sharedMaterials[sub];if(!mat)continue;List<CombineInstance> batch;if(!groups.TryGetValue(mat,out batch)){batch=new List<CombineInstance>();groups.Add(mat,batch);}
                    batch.Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=sub,transform=placement*prefab.transform.worldToLocalMatrix*f.transform.localToWorldMatrix});
                }
            }
            PatchCount++;
        }
        var parent=new GameObject("NativeMeadowGrass");parent.transform.SetParent(transform,false);var modifier=parent.AddComponent<Unity.AI.Navigation.NavMeshModifier>();modifier.ignoreFromBuild=true;modifier.applyToChildren=true;
        foreach(var pair in groups){var mesh=new Mesh{name="NativeMeadowBatch",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();owned.Add(mesh);var node=new GameObject("Grass_"+pair.Key.name);node.transform.SetParent(parent.transform,false);node.AddComponent<MeshFilter>().sharedMesh=mesh;var r=node.AddComponent<MeshRenderer>();var mat=new Material(pair.Key){name=pair.Key.name+"_CommunityMeadow"};mat.SetFloat("_FadeStart",0);mat.SetFloat("_FadeEnd",0);mat.SetFloat("_Height",.30f);mat.SetFloat("_Wind",.025f);mat.SetColor("_RootColor",new Color(.28f,.38f,.14f));mat.SetColor("_TipColor",new Color(.46f,.55f,.25f));ownedMaterials.Add(mat);r.sharedMaterial=mat;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=true;}
    }
    void OnDestroy(){foreach(var m in ownedMaterials)if(m){if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);}foreach(var mesh in owned)if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}}
}
