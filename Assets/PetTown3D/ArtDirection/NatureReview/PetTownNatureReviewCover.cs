using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

// Review-only renderer. References imported meshes; stores placements once instead of duplicating vertices.
[ExecuteAlways]
public sealed class PetTownNatureReviewCover : MonoBehaviour
{
    public Mesh[] meshes;
    public Material[] materials;
    public TextAsset placementData;
    public int InstanceCount {get;private set;}
    public int VisibleBatches {get;private set;}
    sealed class Patch {public Mesh mesh;public Material material;public Matrix4x4[] matrices;public Bounds bounds;}
    readonly List<Patch> patches=new List<Patch>();
    readonly Plane[] planes=new Plane[6];
    TextAsset loaded;
    public void Reload()
    {
        patches.Clear();InstanceCount=0;loaded=placementData;
        if(!placementData || meshes==null || materials==null)return;
        using(var stream=new MemoryStream(placementData.bytes,false))using(var r=new BinaryReader(stream)){
            if(r.ReadInt32()!=0x50544e31)throw new InvalidDataException("Unknown nature review placement format.");
            int count=r.ReadInt32();
            for(int b=0;b<count;b++){
                int source=r.ReadInt32(),amount=r.ReadInt32();
                if(source<0||source>=meshes.Length||source>=materials.Length||amount<0||amount>1023)throw new InvalidDataException("Invalid nature review patch.");
                var patch=new Patch{mesh=meshes[source],material=materials[source],matrices=new Matrix4x4[amount]};
                for(int i=0;i<amount;i++){
                    var matrix=new Matrix4x4();for(int k=0;k<16;k++)matrix[k]=r.ReadSingle();patch.matrices[i]=matrix;
                    var p=matrix.MultiplyPoint3x4(patch.mesh.bounds.center);float radius=patch.mesh.bounds.extents.magnitude*matrix.lossyScale.magnitude;
                    var bounds=new Bounds(p,Vector3.one*Mathf.Max(.5f,radius*2));if(i==0)patch.bounds=bounds;else patch.bounds.Encapsulate(bounds);
                }
                patch.bounds.Expand(1);patches.Add(patch);InstanceCount+=amount;
            }
        }
    }
    void OnEnable(){Reload();}
    void OnDisable(){patches.Clear();loaded=null;}
    void LateUpdate()
    {
        if(loaded!=placementData)Reload();var camera=Camera.main;if(!camera)return;
        GeometryUtility.CalculateFrustumPlanes(camera,planes);VisibleBatches=0;
        foreach(var patch in patches){if(!GeometryUtility.TestPlanesAABB(planes,patch.bounds))continue;
            Graphics.DrawMeshInstanced(patch.mesh,0,patch.material,patch.matrices,patch.matrices.Length,null,ShadowCastingMode.Off,true,gameObject.layer,camera,LightProbeUsage.Off);VisibleBatches++;
        }
    }
}