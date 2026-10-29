using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Fixed cosmetic dressing attached to an owned lot. Saved crops/animals remain the only producers.
public sealed class PetTownSceneryDecor : MonoBehaviour
{
    PetTownSceneryLibrary art;PetTownCommunityView community;Transform models;int sceneryKind;readonly List<Material> materials=new List<Material>();readonly Dictionary<Material,Material> styled=new Dictionary<Material,Material>();
    public void Initialize(int kind)
    {
        sceneryKind=kind;community=GetComponent<PetTownCommunityView>();art=PetTownSceneryLibrary.Current;if(!art)return;models=new GameObject("AssetSceneryDressing").transform;models.SetParent(transform,false);var modifier=models.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();modifier.ignoreFromBuild=true;modifier.applyToChildren=true;
        if(kind==5)Park();else if(kind==6)Lake();else if(kind==9)Farm();else if(kind==10)Ranch();
    }
    void Park()
    {
        if(!art.fountainFlow)TintFountainWater();
        Put(art.kiosk,"Park_Kiosk",new Vector3(4.5f,.05f,4.9f),new Vector3(3.1f,3,3.1f),180,true);
        Put(art.picnic,"Park_Picnic",new Vector3(4.6f,.05f,-4.8f),new Vector3(2.8f,1.1f,1.8f),0,true);
        Put(art.parkFlowerBed,"Park_RaisedFlowerBed",new Vector3(-4.6f,.05f,-4.9f),new Vector3(2.5f,.95f,1.8f));
        Shrubs(new Vector3(-6.2f,.05f,0),7,.85f);Shrubs(new Vector3(1.35f,.05f,6.2f),3,.9f,true);
        Put(art.trash,"Park_Bin",new Vector3(-2.2f,.05f,-5.8f),new Vector3(.55f,.85f,.55f),0,true);
        var community=PetTownCommunityLibrary.Current;if(community){Put(community.lamp,"Park_Lamp",new Vector3(-6.3f,.05f,3.3f),new Vector3(.7f,2.3f,.7f),0,true);Put(community.lamp,"Park_Lamp",new Vector3(6.3f,.05f,-3.3f),new Vector3(.7f,2.3f,.7f),0,true);}
        // NW corner is the playground now; no extra grass cluster there.
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2){var pos=new Vector3(x*2.65f,.05f,z*2.65f);Put(art.parkFlowerBed,"Park_GardenBed",pos,new Vector3(1.4f,.75f,1.15f));}
    }
    void Lake()
    {
        Put(art.boat,"Park_MooredBoat",new Vector3(1.6f,.09f,-1.65f),new Vector3(1.25f,.55f,2),-20);
        // A small closed-view landing in the water, outside all four saved walking/rest points.
        Put(art.woodenBridge,"Park_BoatLanding",new Vector3(.2f,.065f,-2.55f),new Vector3(1.7f,.16f,1.8f));
        var c=PetTownCommunityLibrary.Current;if(c)Put(c.fence,"LandingRail",new Vector3(.2f,.10f,-3.45f),new Vector3(1.8f,.65f,.15f),0,true);
        Put(art.picnic,"Lakeside_Picnic",new Vector3(3.6f,.05f,-5.2f),new Vector3(2.6f,1.05f,1.6f),0,true);Put(art.trash,"Lakeside_Bin",new Vector3(5.7f,.05f,-4.5f),new Vector3(.5f,.8f,.5f),0,true);
        FlowerCluster(new Vector3(-5.8f,.05f,-4.7f),1.7f,1.6f,12);FlowerCluster(new Vector3(5.8f,.05f,3.8f),1.3f,1.3f,9);
        GrassCluster(new Vector3(-5.9f,.05f,2),1.5f,4.5f,20);GrassCluster(new Vector3(5.8f,.05f,2.6f),1.5f,3.5f,16);Shrubs(new Vector3(0,.05f,6.2f),5,1.1f,true);
    }
    void Farm()
    {
        Put(art.scarecrow,"Farm_Scarecrow",new Vector3(-6.9f,.05f,.8f),new Vector3(1.25f,1.85f,.9f),15);
        Put(art.wheelbarrow,"Farm_Wheelbarrow",new Vector3(6.8f,.05f,-7.7f),new Vector3(1.5f,1.05f,2),-20,true);
        Put(art.produceCrate,"Farm_ProduceCrate",new Vector3(-6.9f,.05f,-7.2f),new Vector3(1.1f,.8f,.8f));Put(art.basket,"Farm_HarvestBasket",new Vector3(-5.8f,.05f,-8.2f),new Vector3(.75f,.75f,.75f));
        Put(art.barrel,"Farm_Barrel",new Vector3(-7.9f,.05f,-5.9f),new Vector3(.75f,1,.75f),0,true);Put(art.shovel,"Farm_Shovel",new Vector3(-7.9f,.05f,-7.6f),new Vector3(.6f,1.35f,.6f));
        Put(art.sign,"Farm_Sign",new Vector3(-2.2f,.05f,-8.9f),new Vector3(1.3f,1.45f,.6f),180,true);
        Put(art.cart,"Farm_HarvestCart",new Vector3(-6.2f,.05f,7.3f),new Vector3(2.6f,1.65f,2.8f),-20,true);
        Put(art.hayRound,"Farm_RoundHay",new Vector3(-7.5f,.05f,5.6f),new Vector3(1.2f,1.1f,1.2f));Put(art.barrel,"Farm_WaterBarrel",new Vector3(-3.6f,.05f,8.5f),new Vector3(.8f,1,.8f),0,true);
        Put(art.rake,"Farm_Rake",new Vector3(-2.8f,.05f,6),new Vector3(.65f,1.6f,.5f));Put(art.pitchfork,"Farm_Pitchfork",new Vector3(2.7f,.05f,8.9f),new Vector3(.6f,1.5f,.5f));
        // Small display seedlings and border planting; harvest remains bound to the nine real plots.
        for(int i=0;i<4;i++)Put(i%2==0?art.carrot:art.pumpkin,"Farm_BorderCrop",new Vector3(-7.4f+i*.4f,.05f,4.7f),new Vector3(.55f,.5f,.55f));
        for(int i=0;i<9;i++)Put(art.wheat,"Farm_BorderWheat",new Vector3(-7.7f,.05f,-4.3f+i*.56f),new Vector3(.6f,.7f,.6f));
        GrassCluster(new Vector3(7.6f,.05f,.1f),.7f,7.5f,18);FlowerCluster(new Vector3(3.1f,.05f,-8.7f),1.1f,.6f,6);
    }
    void Ranch()
    {
        Put(art.cart,"Ranch_FeedCart",new Vector3(-2.2f,.05f,-8.4f),new Vector3(1.5f,1.25f,2),10,true);Put(art.hayRound,"Ranch_HayStack",new Vector3(1.7f,.05f,-8.6f),new Vector3(1.15f,1.15f,1.15f));
        Put(art.churn,"Ranch_MilkChurn",new Vector3(-2.1f,.05f,4.2f),new Vector3(.8f,1,.8f),0,true);Put(art.barrel,"Ranch_MilkBarrel",new Vector3(-2,.05f,2.9f),new Vector3(.7f,.95f,.7f),0,true);
        Put(art.pitchfork,"Ranch_Pitchfork",new Vector3(2.1f,.05f,8.1f),new Vector3(.6f,1.65f,.5f));Put(art.rake,"Ranch_Rake",new Vector3(-2.1f,.05f,7.4f),new Vector3(.65f,1.6f,.5f));
        Put(art.sign,"Ranch_Sign",new Vector3(2.3f,.05f,-9.9f),new Vector3(1.4f,1.4f,.5f),180,true);
        Put(art.wheelbarrow,"Ranch_Wheelbarrow",new Vector3(1.9f,.05f,-6.4f),new Vector3(1.25f,.95f,1.7f),-15,true);
        // A denser short pasture, using the approved grass meshes rather than a flat color alone.
        for(int s=0;s<3;s++){var center=PetTownRanchView.PenCenter(s);var half=PetTownRanchView.PenHalf(s);GrassCluster(center+Vector3.up*.05f,half.x*1.65f,half.y*1.65f,s==0?60:30);}
    }
    void Shrubs(Vector3 center,int count,float step,bool alongX=false){for(int i=0;i<count;i++)Put(art.parkBush,"Park_Hedge",center+(alongX?Vector3.right:Vector3.forward)*(i-(count-1)*.5f)*step,new Vector3(.8f,.75f,.8f),0,true);}
    void FlowerCluster(Vector3 center,float width,float depth,int count){var forest=PetTownForestLibrary.Current;if(!forest||forest.flowers==null||forest.flowers.Length==0)return;var flowers=(sceneryKind==5||sceneryKind==6)&&art.detailedFlowers!=null&&art.detailedFlowers.Length>0?art.detailedFlowers:forest.flowers;for(int i=0;i<count;i++){float x=((i%4)/3f-.5f)*width,z=((i/4)/(float)Mathf.Max(1,(count-1)/4)-.5f)*depth;Put(flowers[i%flowers.Length],"Asset_Flowers",center+new Vector3(x,0,z),Vector3.one*(.36f+i%3*.045f),i*53,false,true);}}
    void GrassCluster(Vector3 center,float width,float depth,int count){var forest=PetTownForestLibrary.Current;if(!forest||forest.grasses==null||forest.grasses.Length==0)return;var random=new System.Random(count*13+Mathf.RoundToInt(center.x*17+center.z*31));for(int i=0;i<count;i++){var pos=center+new Vector3(((float)random.NextDouble()-.5f)*width,0,((float)random.NextDouble()-.5f)*depth);Put(forest.grasses[i%forest.grasses.Length],"Asset_Grass",pos,Vector3.one*(.18f+(float)random.NextDouble()*.13f),i*71,false,true);}}
    GameObject Put(GameObject prefab,string label,Vector3 pos,Vector3 envelope,float yaw=0,bool solid=false,bool nativePlant=false)
    {
        if(!prefab)return null;if(community&&(label=="Asset_Flowers"||label=="Asset_Grass")&&community.IsStonePath(pos,.2f))return null;var holder=new GameObject(label);holder.transform.SetParent(models,false);var fit=new GameObject("AlignedFit").transform;fit.SetParent(holder.transform,false);var instance=Instantiate(prefab,fit);instance.transform.localPosition=Vector3.zero;foreach(var collider in instance.GetComponentsInChildren<Collider>(true))collider.enabled=false;foreach(var script in instance.GetComponentsInChildren<MonoBehaviour>(true))if(script)script.enabled=false;
        foreach(var renderer in instance.GetComponentsInChildren<Renderer>()){if(!nativePlant){var slots=renderer.sharedMaterials;for(int i=0;i<slots.Length;i++)slots[i]=Styled(slots[i]);renderer.sharedMaterials=slots;}renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;renderer.receiveShadows=true;}
        var b=BoundsOf(holder.transform);if((envelope.x>envelope.z*1.3f&&b.size.z>b.size.x)||(envelope.z>envelope.x*1.3f&&b.size.x>b.size.z)){instance.transform.localRotation=Quaternion.Euler(0,90,0)*instance.transform.localRotation;b=BoundsOf(holder.transform);}
        float scale=nativePlant?envelope.y/Mathf.Max(.001f,b.size.y):Mathf.Min(envelope.x/Mathf.Max(.001f,b.size.x),envelope.y/Mathf.Max(.001f,b.size.y),envelope.z/Mathf.Max(.001f,b.size.z));fit.localScale=Vector3.one*scale;b=BoundsOf(holder.transform);fit.localPosition-=new Vector3(b.center.x,b.min.y,b.center.z);holder.transform.localPosition=pos;holder.transform.localRotation=Quaternion.Euler(0,yaw,0);
        if(solid){var node=new GameObject(label+"_Collision");node.transform.SetParent(holder.transform,false);var shape=BoundsOf(holder.transform);var box=node.AddComponent<BoxCollider>();box.center=shape.center;box.size=shape.size;var obstacle=node.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=box.center;obstacle.size=new Vector3(Mathf.Max(.15f,box.size.x),Mathf.Max(.3f,box.size.y),Mathf.Max(.15f,box.size.z));obstacle.carving=true;obstacle.carveOnlyStationary=false;}
        return holder;
    }
    Material Styled(Material source)
    {
        if(!source)return null;Material material;if(styled.TryGetValue(source,out material))return material;if(source.shader.name.StartsWith("Universal Render Pipeline/")){material=new Material(source){name=source.name+"_SceneryLit",enableInstancing=true};styled[source]=material;materials.Add(material);return material;}material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=source.name+"_SceneryLit",enableInstancing=true};material.SetTexture("_BaseMap",source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):source.mainTexture);material.SetColor("_BaseColor",source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.HasProperty("_Color")?source.GetColor("_Color"):Color.white);material.SetFloat("_Smoothness",.12f);styled[source]=material;materials.Add(material);return material;
    }
    void TintFountainWater()
    {
        var fountain=transform.Find("ImportedCommunityModels/Fountain");var shader=Resources.Load<Shader>("Environment/Scenery/ParkWaterProps");if(!fountain||!shader)return;foreach(var renderer in fountain.GetComponentsInChildren<Renderer>()){var slots=renderer.sharedMaterials;for(int i=0;i<slots.Length;i++){if(!slots[i])continue;var material=new Material(slots[i]);material.shader=shader;material.SetColor("_WaterColor",new Color(.18f,.59f,.72f));slots[i]=material;materials.Add(material);}renderer.sharedMaterials=slots;}
    }
    void OnDestroy(){foreach(var material in materials)if(material){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}}
    static Bounds BoundsOf(Transform root){bool first=true;var bounds=new Bounds();foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){if(!filter.sharedMesh)continue;var b=filter.sharedMesh.bounds;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var p=root.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}}return bounds;}
}
