using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public sealed class PetTownRanchView : MonoBehaviour
{
    public string Id{get;private set;}PetTownGame game;PetTownRanchLibrary library;Transform models;
    readonly Dictionary<Material,Material> materials=new Dictionary<Material,Material>();readonly List<Material> owned=new List<Material>();readonly Dictionary<string,PetTownRanchAnimalVisual> animals=new Dictionary<string,PetTownRanchAnimalVisual>();float next;
    public void Initialize(PetTownGame source,string id)
    {
        game=source;Id=id;library=PetTownRanchLibrary.Current;if(!library||!library.Ready){Debug.LogError("Ranch asset library missing");return;}models=new GameObject("ImportedRanchModels").transform;models.SetParent(transform,false);var mod=models.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();mod.ignoreFromBuild=true;mod.applyToChildren=true;
        var farm=PetTownFarmLibrary.Current;var supplies=PetTownPoultryLibrary.Current;var ground=Spawn(farm.soil,"PastureGround",Vector3.zero,new Vector3(22,.045f,22));var forest=PetTownForestLibrary.Current;
        if(forest&&forest.groundMaterial){var meadow=new Material(forest.groundMaterial){name="RanchMeadow"};meadow.SetFloat("_PaintStrength",0);meadow.SetFloat("_UseCoverage",1);meadow.SetTexture("_Coverage",forest.meadowMask);meadow.SetVector("_CoverageBounds",forest.meadowBounds);owned.Add(meadow);foreach(var renderer in ground.GetComponentsInChildren<Renderer>()){var slots=renderer.sharedMaterials;for(int i=0;i<slots.Length;i++)slots[i]=meadow;renderer.sharedMaterials=slots;}}
        Spawn(farm.barn,"RanchBarn",new Vector3(0,.05f,7.8f),new Vector3(5,3.6f,4.5f));Block("BarnCollision",new Vector3(0,1.8f,7.8f),new Vector3(5,3.6f,4.5f));
        Fence(Vector3.zero,11,11,true);for(int species=0;species<3;species++)
        {
            var center=PenCenter(species);var half=PenHalf(species);Fence(center,half.x,half.y,false);Spawn(supplies.gate,"ClosedPastureGate",new Vector3(center.x,.045f,center.z-half.y),new Vector3(1.3f,.9f,.15f));Block("PastureBoundary",center+Vector3.up*.8f,new Vector3(half.x*2,1.6f,half.y*2));Spawn(supplies.hay,"HayBedding",center+new Vector3(1.8f,.05f,half.y-1.6f),new Vector3(1.1f,.65f,.9f));
            var waterPos=species==1?new Vector3(2.2f,.05f,5):new Vector3(center.x,.05f,-6.3f);Spawn(supplies.bucket,"WaterBucket",waterPos,new Vector3(.65f,.55f,.65f));Spawn(supplies.sack,"LivestockFeed",waterPos+new Vector3(species==1?0:1.6f,0,-.9f),new Vector3(.6f,.8f,.6f));
            if(forest&&forest.grasses!=null&&forest.grasses.Length>0)for(int i=0;i<(species==0?10:6);i++)Spawn(forest.grasses[0],"PastureGrass",center+new Vector3(i%2==0?-2.2f:2.2f,.05f,Mathf.Lerp(-half.y+1,half.y-1,i/(float)(species==0?9:5))),Vector3.one*.25f,i*57,true,true);
        }
        gameObject.AddComponent<PetTownSceneryDecor>().Initialize(10);
        RefreshAnimals();
    }
    public bool AllowsPastureGrass(Vector3 world)
    {
        var point=transform.InverseTransformPoint(world);for(int species=0;species<3;species++){var c=PenCenter(species);var h=PenHalf(species);if(Mathf.Abs(point.x-c.x)<h.x-.75f&&Mathf.Abs(point.z-c.z)<h.y-.75f&&!(point.x>c.x+.5f&&point.z>c.z+h.y-3))return true;}return false;
    }
    public static Vector3 PenCenter(int species)=>species==0?new Vector3(-6.4f,0,2):species==1?new Vector3(6.4f,0,5.6f):new Vector3(6.4f,0,-1.9f);
    public static Vector2 PenHalf(int species)=>new Vector2(3.4f,species==0?7.2f:species==1?3.6f:3.3f);
    public static Vector3 InitialPosition(int species,int index){var c=PenCenter(species);var h=PenHalf(species);return new Vector3(c.x+(index%2==0?-1:1),.05f,c.z-h.y+1.6f+index*(species==0?2.4f:1.4f));}
    public static Vector3 RoamPoint(int species,System.Random random){var c=PenCenter(species);var h=PenHalf(species);return new Vector3(c.x+Mathf.Lerp(-h.x+1.3f,h.x-1.3f,(float)random.NextDouble()),.05f,c.z+Mathf.Lerp(-h.y+1.5f,h.y-1.5f,(float)random.NextDouble()));}
    void Fence(Vector3 center,float hw,float hd,bool outer)
    {
        for(int side=0;side<4;side++){bool x=side<2;float length=(x?hw:hd)*2;int count=Mathf.CeilToInt(length/1.5f);float step=length/count;for(int i=0;i<count;i++){float u=-length*.5f+(i+.5f)*step;if(side==0&&Mathf.Abs(u)<(outer?1.55f:.7f))continue;var pos=center+(x?new Vector3(u,.045f,side==0?-hd:hd):new Vector3(side==2?-hw:hw,.045f,u));Spawn(PetTownFarmLibrary.Current.fence,"Fence",pos,new Vector3(step,.95f,.15f),x?0:90);if(outer)Block("PerimeterFence",pos+Vector3.up*.47f,x?new Vector3(step,.95f,.15f):new Vector3(.15f,.95f,step));}}
    }
    void Block(string label,Vector3 pos,Vector3 size){var node=new GameObject(label);node.transform.SetParent(transform,false);node.transform.localPosition=pos;var collider=node.AddComponent<BoxCollider>();collider.size=size;var obstacle=node.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=size;obstacle.carving=true;obstacle.carveOnlyStationary=false;}
    GameObject Spawn(GameObject prefab,string label,Vector3 pos,Vector3 size,float yaw=0,bool uniform=false,bool nativeMaterials=false)
    {
        var holder=new GameObject(label);holder.transform.SetParent(models,false);var fit=new GameObject("AlignedFit").transform;fit.SetParent(holder.transform,false);var model=Instantiate(prefab,fit);model.transform.localPosition=Vector3.zero;foreach(var c in model.GetComponentsInChildren<Collider>(true))c.enabled=false;foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))if(script)script.enabled=false;
        if(!nativeMaterials)foreach(var renderer in model.GetComponentsInChildren<Renderer>()){var slots=renderer.sharedMaterials;for(int i=0;i<slots.Length;i++)slots[i]=Styled(slots[i]);renderer.sharedMaterials=slots;}
        var b=BoundsOf(holder.transform);if(!uniform&&size.x>size.z*2&&b.size.z>b.size.x*1.5f){model.transform.localRotation=Quaternion.Euler(0,90,0)*model.transform.localRotation;b=BoundsOf(holder.transform);}fit.localScale=uniform?Vector3.one*(size.y/Mathf.Max(.001f,b.size.y)):new Vector3(size.x/Mathf.Max(.001f,b.size.x),size.y/Mathf.Max(.001f,b.size.y),size.z/Mathf.Max(.001f,b.size.z));b=BoundsOf(holder.transform);fit.localPosition-=new Vector3(b.center.x,b.min.y,b.center.z);holder.transform.localPosition=pos;holder.transform.localRotation=Quaternion.Euler(0,yaw,0);return holder;
    }
    Material Styled(Material source){if(!source)return null;Material copy;if(materials.TryGetValue(source,out copy))return copy;copy=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=source.name+"_Ranch",enableInstancing=true};copy.SetTexture("_BaseMap",source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):source.mainTexture);copy.SetColor("_BaseColor",source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.color);copy.SetFloat("_Smoothness",.1f);materials[source]=copy;return copy;}
    static Bounds BoundsOf(Transform root){bool first=true;var b=new Bounds();foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){if(!filter.sharedMesh)continue;var mesh=filter.sharedMesh.bounds;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var p=root.InverseTransformPoint(filter.transform.TransformPoint(mesh.center+Vector3.Scale(mesh.extents,new Vector3(x,y,z))));if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}}return b;}
    public void RefreshAnimals()
    {
        if(!game||!library||!library.Ready)return;var lot=game.Progression.Ranch.Get(Id,game.CurrentFarmDay);var removed=new List<string>();foreach(var pair in animals)if(!pair.Value||!lot.animals.Exists(x=>x.id==pair.Key)){if(pair.Value){pair.Value.gameObject.SetActive(false);Destroy(pair.Value.gameObject);}removed.Add(pair.Key);}foreach(var id in removed)animals.Remove(id);var indices=new int[3];foreach(var record in lot.animals){int i=indices[record.species]++;PetTownRanchAnimalVisual visual;if(!animals.TryGetValue(record.id,out visual)){var node=new GameObject("RANCH_ANIMAL_"+record.id);node.transform.SetParent(models,false);node.transform.localPosition=InitialPosition(record.species,i);visual=node.AddComponent<PetTownRanchAnimalVisual>();animals[record.id]=visual;}visual.Initialize(game,record,library.animals[record.species]);}
    }
    void Update(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;RefreshAnimals();}
    void OnDestroy(){foreach(var mat in materials.Values)if(mat)Destroy(mat);foreach(var mat in owned)if(mat)Destroy(mat);}
}
