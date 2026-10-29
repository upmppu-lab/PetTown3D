using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public sealed class PetTownCoopView : MonoBehaviour
{
    public static Vector3 Center=>new Vector3(6.2f,.045f,7.4f);
    PetTownGame game;string id;Transform structure,models,eggModels;PetTownPoultryLibrary library;
    readonly Dictionary<Material,Material> materials=new Dictionary<Material,Material>();
    readonly Dictionary<string,PetTownPoultryBirdVisual> birds=new Dictionary<string,PetTownPoultryBirdVisual>();int eggCount=-1;float next;
    public void Initialize(PetTownGame source,string farmId){game=source;id=farmId;library=PetTownPoultryLibrary.Current;RefreshNow();}
    public bool CanBuild(out string reason)
    {
        reason="";if(!library||!library.Ready){reason="닭장 에셋을 준비해 주세요.";return false;}
        foreach(var c in Physics.OverlapBox(transform.TransformPoint(Center+Vector3.up*.9f),new Vector3(2.1f,.8f,2),transform.rotation,~(1<<9),QueryTriggerInteraction.Ignore))
        {
            if(structure&&c.transform.IsChildOf(structure))continue;reason="농장 뒤쪽 닭장 자리의 통로와 공간을 비워 주세요.";return false;
        }
        return true;
    }
    public void RefreshNow()
    {
        if(!game||!library||!library.Ready)return;var farm=game.Progression.Farm;var coop=farm.poultry?.Find(id);
        if(coop==null){if(structure){structure.gameObject.SetActive(false);Destroy(structure.gameObject);}structure=null;birds.Clear();eggCount=-1;return;}
        if(!structure)BuildStructure();var removed=new List<string>();foreach(var p in birds)if(!p.Value||!coop.birds.Exists(x=>x.id==p.Key)){if(p.Value){p.Value.gameObject.SetActive(false);Destroy(p.Value.gameObject);}removed.Add(p.Key);}foreach(var key in removed)birds.Remove(key);
        for(int i=0;i<coop.birds.Count;i++){var record=coop.birds[i];PetTownPoultryBirdVisual bird;if(!birds.TryGetValue(record.id,out bird)){var node=new GameObject("FARM_CHICKEN_"+record.id);node.transform.SetParent(models,false);node.transform.localPosition=new Vector3(Center.x+(i%3-1)*.75f,.05f,6.1f+i/3*.6f);bird=node.AddComponent<PetTownPoultryBirdVisual>();birds[record.id]=bird;}bird.Initialize(game,record,library);}
        if(eggCount!=coop.nestEggs){eggCount=coop.nestEggs;if(eggModels){eggModels.gameObject.SetActive(false);Destroy(eggModels.gameObject);}eggModels=new GameObject("UncollectedEggs").transform;eggModels.SetParent(models,false);for(int i=0;i<Mathf.Min(6,eggCount);i++)Spawn(library.egg,"Egg",new Vector3(7.95f+(i%2-.5f)*.13f,.53f,8+i/2*.12f),new Vector3(.09f,.13f,.09f),0,eggModels);}
    }
    void BuildStructure()
    {
        structure=new GameObject("ChickenCoopUpgrade").transform;structure.SetParent(transform,false);models=new GameObject("ImportedPoultryModels").transform;models.SetParent(structure,false);var mod=models.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();mod.ignoreFromBuild=true;mod.applyToChildren=true;
        Spawn(library.house,"ChickenHouse",new Vector3(6.2f,.05f,8.5f),new Vector3(2.7f,1.7f,1.65f),180);Spawn(library.hay,"BeddingHay",new Vector3(4.8f,.05f,8.7f),new Vector3(.6f,.45f,.8f));Spawn(library.sack,"ChickenFeed",new Vector3(4.65f,.05f,5.75f),new Vector3(.45f,.6f,.45f));Spawn(library.bucket,"FreshWaterBucket",new Vector3(7.75f,.05f,5.7f),new Vector3(.42f,.4f,.42f));Spawn(PetTownFarmLibrary.Current.crate,"EggNest",new Vector3(7.95f,.05f,8.1f),new Vector3(.65f,.48f,.7f));
        var fence=PetTownFarmLibrary.Current.fence;for(int side=0;side<4;side++){bool x=side<2;int count=x?3:3;for(int i=0;i<count;i++){float u=(i-1)*1.4f;if(side==0&&i==1)continue;var pos=Center+(x?new Vector3(u,0,side==0?-1.95f:1.95f):new Vector3(side==2?-2.1f:2.1f,0,u));Spawn(fence,"CoopFence",pos,new Vector3(1.4f,.75f,.12f),x?0:90);}}
        Spawn(library.gate,"ClosedCoopGate",Center+Vector3.back*1.95f,new Vector3(1.4f,.75f,.15f));
        var safety=new GameObject("CoopAnimalBoundary");safety.transform.SetParent(structure,false);safety.transform.localPosition=Center+Vector3.up*.6f;var box=safety.AddComponent<BoxCollider>();box.size=new Vector3(4.2f,1.2f,3.9f);var obstacle=safety.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=box.size;obstacle.carving=true;obstacle.carveOnlyStationary=false;
    }
    GameObject Spawn(GameObject prefab,string label,Vector3 pos,Vector3 size,float yaw=0,Transform parent=null)
    {
        var holder=new GameObject(label);holder.transform.SetParent(parent?parent:models,false);var fit=new GameObject("AlignedFit").transform;fit.SetParent(holder.transform,false);var model=Instantiate(prefab,fit);model.transform.localPosition=Vector3.zero;foreach(var c in model.GetComponentsInChildren<Collider>(true))c.enabled=false;foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))if(script)script.enabled=false;
        foreach(var renderer in model.GetComponentsInChildren<Renderer>()){var slots=renderer.sharedMaterials;for(int i=0;i<slots.Length;i++)slots[i]=Styled(slots[i]);renderer.sharedMaterials=slots;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;renderer.receiveShadows=true;}
        var b=LocalBounds(holder.transform);if(size.x>size.z*2&&b.size.z>b.size.x*1.5f){model.transform.localRotation=Quaternion.Euler(0,90,0)*model.transform.localRotation;b=LocalBounds(holder.transform);}fit.localScale=new Vector3(size.x/Mathf.Max(.001f,b.size.x),size.y/Mathf.Max(.001f,b.size.y),size.z/Mathf.Max(.001f,b.size.z));b=LocalBounds(holder.transform);fit.localPosition-=new Vector3(b.center.x,b.min.y,b.center.z);holder.transform.localPosition=pos;holder.transform.localRotation=Quaternion.Euler(0,yaw,0);return holder;
    }
    Material Styled(Material source){if(!source)return null;Material copy;if(materials.TryGetValue(source,out copy))return copy;copy=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=source.name+"_Poultry",enableInstancing=true};copy.SetTexture("_BaseMap",source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):source.mainTexture);copy.SetColor("_BaseColor",source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.color);copy.SetFloat("_Smoothness",.1f);materials[source]=copy;return copy;}
    static Bounds LocalBounds(Transform root){bool first=true;var b=new Bounds();foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){if(!filter.sharedMesh)continue;var mesh=filter.sharedMesh.bounds;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var p=root.InverseTransformPoint(filter.transform.TransformPoint(mesh.center+Vector3.Scale(mesh.extents,new Vector3(x,y,z))));if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}}return b;}
    void Update(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;RefreshNow();}
    void OnDestroy(){foreach(var material in materials.Values)if(material)Destroy(material);}
}
