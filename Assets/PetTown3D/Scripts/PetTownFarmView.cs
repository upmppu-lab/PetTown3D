using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The farm is assembled from the imported farm assets. All sizing is local to its rotatable lot.
public sealed class PetTownFarmView : MonoBehaviour
{
    public string Id {get;private set;}
    PetTownGame game;PetTownFarmLibrary library;Transform models,crops;
    readonly Dictionary<Material,Material> materials=new Dictionary<Material,Material>();
    readonly List<Mesh> cropMeshes=new List<Mesh>();
    readonly List<Renderer>[] soil=new List<Renderer>[PetTownFarmState.PlotsFor(PetTownFarmState.MaxExpansion)];
    MaterialPropertyBlock wet;Material meadowGround;
    int revision=-1,lastDay=-1,signature;float next;
    public static Vector3 PlotPosition(int i)
    {
        if(i<9)return new Vector3((i%3-1)*4,.16f,(i/3)*4-5);
        if(i<11)return new Vector3(i==9?-3.6f:3.6f,.16f,-8.3f);   // front yard beds
        return new Vector3(-6.5f,.16f,7);                           // beside the barn
    }
    void BuildBed(int i)
    {
        if(i<0||i>=soil.Length||soil[i]!=null)return;
        var bed=Spawn(PetTownSceneryLibrary.Current&&PetTownSceneryLibrary.Current.furrow?PetTownSceneryLibrary.Current.furrow:library.soil,"SoilBed_"+(i+1),PlotPosition(i)-Vector3.up*.11f,new Vector3(3.2f,.11f,3.2f));
        soil[i]=new List<Renderer>(bed.GetComponentsInChildren<Renderer>());
    }
    public void Initialize(PetTownGame source,string id)
    {
        wet=new MaterialPropertyBlock();game=source;Id=id;library=PetTownFarmLibrary.Current;if(!library){Debug.LogError("Farm asset library is missing");return;}
        models=new GameObject("ImportedFarmModels").transform;models.SetParent(transform,false);
        var modifier=models.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();modifier.ignoreFromBuild=true;modifier.applyToChildren=true;
        var earth=Spawn(library.soil,"FarmEarth",new Vector3(0,0,0),new Vector3(18,.045f,20));
        var forest=PetTownForestLibrary.Current;if(forest&&forest.groundMaterial){meadowGround=new Material(forest.groundMaterial){name="FarmMeadow"};meadowGround.SetFloat("_PaintStrength",0);meadowGround.SetFloat("_UseCoverage",1);meadowGround.SetTexture("_Coverage",forest.meadowMask);meadowGround.SetVector("_CoverageBounds",forest.meadowBounds);foreach(var renderer in earth.GetComponentsInChildren<Renderer>()){var slots=renderer.sharedMaterials;for(int i=0;i<slots.Length;i++)slots[i]=meadowGround;renderer.sharedMaterials=slots;}}
        for(int i=0;i<PetTownFarmState.PlotCount;i++)BuildBed(i);
        var barn=Spawn(library.barn,"ToolBarn",new Vector3(0,.045f,7),new Vector3(4.3f,3.2f,4.6f));
        Block("BarnCollision",new Vector3(0,1.6f,7),new Vector3(4.2f,3.2f,4.5f));
        Spawn(library.crate,"SeedCrates",new Vector3(-6.6f,.05f,-7.5f),new Vector3(1.1f,.75f,1));
        Spawn(library.wateringCan,"WateringCan",new Vector3(6.5f,.05f,-7.5f),new Vector3(.7f,.7f,.7f));
        for(int side=0;side<4;side++)
        {
            bool x=side<2;float length=x?18:20;int count=Mathf.CeilToInt(length/1.5f);float step=length/count;
            for(int i=0;i<count;i++)
            {
                float u=-length*.5f+(i+.5f)*step;if(side==0&&Mathf.Abs(u)<1.5f)continue;
                var pos=x?new Vector3(u,.045f,side==0?-10:10):new Vector3(side==2?-9:9,.045f,u);
                Spawn(library.fence,"Fence",pos,new Vector3(step,.92f,.18f),x?0:90);
                Block("FenceCollision",pos+Vector3.up*.46f,x?new Vector3(step,.92f,.16f):new Vector3(.16f,.92f,step));
            }
        }
        gameObject.AddComponent<PetTownSceneryDecor>().Initialize(9);
        Refresh();
        gameObject.AddComponent<PetTownCoopView>().Initialize(game,Id);
    }
    void Block(string label,Vector3 position,Vector3 size)
    {
        var node=new GameObject(label);node.transform.SetParent(transform,false);node.transform.localPosition=position;
        var collider=node.AddComponent<BoxCollider>();collider.size=size;
        var obstacle=node.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=size;obstacle.carving=true;obstacle.carveOnlyStationary=false;
    }
    GameObject Spawn(GameObject prefab,string label,Vector3 position,Vector3 size,float yaw=0,Transform parent=null,bool uniform=false)
    {
        var holder=new GameObject(label);holder.transform.SetParent(parent?parent:models,false);if(!prefab)return holder;
        var fit=new GameObject("AlignedFit").transform;fit.SetParent(holder.transform,false);var instance=Instantiate(prefab,fit);instance.transform.localPosition=Vector3.zero;
        foreach(var collider in instance.GetComponentsInChildren<Collider>()){collider.enabled=false;Destroy(collider);}
        foreach(var renderer in instance.GetComponentsInChildren<Renderer>())
        {
            var replaced=renderer.sharedMaterials;for(int i=0;i<replaced.Length;i++)replaced[i]=Styled(replaced[i]);renderer.sharedMaterials=replaced;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;renderer.receiveShadows=true;
        }
        var bounds=LocalBounds(holder.transform);var scale=new Vector3(size.x/Mathf.Max(.01f,bounds.size.x),size.y/Mathf.Max(.01f,bounds.size.y),size.z/Mathf.Max(.01f,bounds.size.z));fit.localScale=uniform?Vector3.one*Mathf.Min(scale.x,scale.y,scale.z):scale;
        bounds=LocalBounds(holder.transform);fit.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        holder.transform.localPosition=position;holder.transform.localRotation=Quaternion.Euler(0,yaw,0);return holder;
    }
    Material Styled(Material source)
    {
        if(!source)return null;if(materials.TryGetValue(source,out var cached))return cached;
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=source.name+"_FarmLit",enableInstancing=true};
        var texture=source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):source.HasProperty("baseColorTexture")?source.GetTexture("baseColorTexture"):source.mainTexture;var color=source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.HasProperty("baseColorFactor")?source.GetColor("baseColorFactor"):source.color;
        material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.12f);material.SetFloat("_Metallic",0);material.SetFloat("_Cull",0);materials[source]=material;return material;
    }
    static Bounds LocalBounds(Transform root)
    {
        var bounds=new Bounds();bool first=true;foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!filter.sharedMesh)continue;var b=filter.sharedMesh.bounds;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var p=root.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
        }
        return bounds;
    }
    int Signature(PetTownFarmRecord lot){int hash=17+lot.plots.Count*101;foreach(var p in lot.plots)hash=unchecked(hash*31+p.crop*7+p.grown*13);return hash;}
    void Update()
    {
        if(!game||Time.unscaledTime<next)return;next=Time.unscaledTime+.25f;var state=game.Progression.Farm;
        if(revision==state.revision&&lastDay==game.CurrentFarmDay)return;var lot=state.lots.Find(l=>l.id==Id);
        if(lot!=null)Refresh();else revision=state.revision;
    }
    public void Refresh()
    {
        if(!game||!library)return;var state=game.Progression.Farm;var lot=state.lots.Find(l=>l.id==Id);if(lot==null)return;
        for(int i=0;i<lot.plots.Count&&i<soil.Length;i++)BuildBed(i);
        int nextSignature=Signature(lot);bool rebuild=!crops||nextSignature!=signature;
        revision=state.revision;lastDay=game.CurrentFarmDay;signature=nextSignature;RefreshWetSoil(lot);if(!rebuild)return;
        foreach(var mesh in cropMeshes)if(mesh)Destroy(mesh);cropMeshes.Clear();if(crops){crops.gameObject.SetActive(false);Destroy(crops.gameObject);}crops=new GameObject("GrowingCrops").transform;crops.SetParent(models,false);
        for(int i=0;i<lot.plots.Count;i++)
        {
            var plot=lot.plots[i];
            if(!PetTownFarmState.ValidCrop(plot.crop))continue;float growth=Mathf.Lerp(.28f,1,plot.grown/(float)PetTownFarmState.GrowDays[plot.crop]);
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
            {
                var prefab=library.Crop(plot.crop);if(!prefab)continue;var plant=Spawn(prefab,PetTownFarmState.Names[plot.crop],PlotPosition(i)+new Vector3(x*.87f,0,z*.87f),PetTownFarmLibrary.CropSize(plot.crop),plot.crop>=2?((i*31+x*17+z*13)%4)*90:0,crops,plot.crop>=2);plant.transform.localScale=Vector3.one*growth;
            }
        }
        BatchCrops();
    }
    void RefreshWetSoil(PetTownFarmRecord lot)
    {
        for(int i=0;i<soil.Length&&i<lot.plots.Count;i++)if(soil[i]!=null)foreach(var renderer in soil[i])if(renderer)
        {
            var slots=renderer.sharedMaterials;for(int m=0;m<slots.Length;m++){wet.Clear();var color=slots[m]&&slots[m].HasProperty("_BaseColor")?slots[m].GetColor("_BaseColor"):Color.white;wet.SetColor("_BaseColor",color*(lot.plots[i].wateredDay==lastDay?new Color(.64f,.64f,.64f,1):Color.white));renderer.SetPropertyBlock(wet,m);}
        }
    }
    void BatchCrops()
    {
        var groups=new Dictionary<Material,List<CombineInstance>>();var used=new List<MeshRenderer>();
        foreach(var filter in crops.GetComponentsInChildren<MeshFilter>())
        {
            var renderer=filter.GetComponent<MeshRenderer>();var mesh=filter.sharedMesh;if(!renderer||!mesh||!mesh.isReadable)continue;var slots=renderer.sharedMaterials;
            if(slots.Length<mesh.subMeshCount)continue;bool complete=true;for(int sub=0;sub<mesh.subMeshCount;sub++)if(!slots[sub])complete=false;if(!complete)continue;
            for(int sub=0;sub<mesh.subMeshCount;sub++){var material=slots[sub];if(!groups.TryGetValue(material,out var list)){list=new List<CombineInstance>();groups[material]=list;}list.Add(new CombineInstance{mesh=mesh,subMeshIndex=sub,transform=crops.worldToLocalMatrix*filter.transform.localToWorldMatrix});}used.Add(renderer);
        }
        foreach(var group in groups)
        {
            var mesh=new Mesh{name="FarmCropBatch"};long count=0;foreach(var item in group.Value)count+=item.mesh.vertexCount;if(count>65000)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(group.Value.ToArray(),true,true);cropMeshes.Add(mesh);
            var node=new GameObject("CropBatch_"+group.Key.name,typeof(MeshFilter),typeof(MeshRenderer));node.transform.SetParent(crops,false);node.GetComponent<MeshFilter>().sharedMesh=mesh;node.GetComponent<MeshRenderer>().sharedMaterial=group.Key;
        }
        foreach(var renderer in used)renderer.enabled=false;
    }
    void OnDestroy(){foreach(var mesh in cropMeshes)if(mesh)Destroy(mesh);if(meadowGround)Destroy(meadowGround);foreach(var material in materials.Values)if(material)Destroy(material);}
}
