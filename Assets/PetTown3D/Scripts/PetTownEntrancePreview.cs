using UnityEngine;
using Unity.AI.Navigation;

[DisallowMultipleComponent]
public sealed class PetTownEntrancePreview : MonoBehaviour
{
    Mesh mesh;
    Material material;
    MeshRenderer rendererComponent;
    public void Show(Vector3 entrance,Vector3 outward)
    {
        if(rendererComponent==null)
        {
            var shader=Resources.Load<Shader>("Materials/PetTownBuildGrid");
            if(shader==null||!shader.isSupported)return;
            mesh=new Mesh{name="PetTown_EntranceDirection"};
            mesh.vertices=new[]{new Vector3(-.12f,0,.15f),new Vector3(.12f,0,.15f),new Vector3(-.12f,0,1f),new Vector3(.12f,0,1f),new Vector3(-.5f,0,1f),new Vector3(.5f,0,1f),new Vector3(0,0,1.8f)};
            mesh.triangles=new[]{0,2,1,1,2,3,4,6,5};
            var colors=new Color[7];for(int i=0;i<colors.Length;i++)colors[i]=new Color(1f,.75f,.2f,1f);mesh.colors=colors;mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            material=new Material(shader){name="PetTown_EntranceDirection_Runtime"};material.renderQueue=2995;
            rendererComponent=gameObject.AddComponent<MeshRenderer>();rendererComponent.sharedMaterial=material;
            rendererComponent.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;rendererComponent.receiveShadows=false;
            gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
        }
        transform.SetPositionAndRotation(entrance,Quaternion.LookRotation(outward,Vector3.up));rendererComponent.enabled=true;
    }
    public void Hide(){if(rendererComponent!=null)rendererComponent.enabled=false;}
    void OnDisable(){Hide();}
    void OnDestroy()
    {
        if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
        if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
    }
}