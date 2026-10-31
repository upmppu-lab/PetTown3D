using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public sealed partial class PetTownCommunityView : MonoBehaviour
{
    public string Id{get;private set;}public int Kind{get;private set;}
    public Vector3 EntranceLocal{get;private set;}
    PetTownGame game;PetTownCommunityLibrary library;Transform models,exhibits;int signature=-1;float next;
    Material meadowSurface,pathSurface;
    PetTownStoneWalkway stoneLayout;
    readonly List<Material> ownedMaterials=new List<Material>();readonly List<Mesh> ownedMeshes=new List<Mesh>();
    public Vector3 VisitLocal(int slot)
    {
        slot=Mathf.Clamp(slot,0,Kind==5?PetTownPlaygroundRig.SlotCount-1:3);if(Kind==5)return ParkVisit[slot];if(Kind==6)return new[]{new Vector3(-4.8f,.045f,-2),new Vector3(4.8f,.045f,-2),new Vector3(-4.8f,.045f,3),new Vector3(4.8f,.045f,3)}[slot];
        if(Kind==7)return PetTownZooLayout.Visit(slot);
        return new[]{new Vector3(-3.7f,.045f,-2),new Vector3(3.7f,.045f,-2),new Vector3(-3.7f,.045f,2),new Vector3(3.7f,.045f,2)}[slot];
    }
    public void Initialize(PetTownGame source,string id,int kind,Vector3 entrance)
    {
        game=source;Id=id;Kind=kind;EntranceLocal=entrance;library=PetTownCommunityLibrary.Current;if(!library){Debug.LogError("Community asset library missing");return;}
        if((kind==5||kind==6||kind==7)&&library.stonePathMaterial)stoneLayout=new PetTownStoneWalkway(kind,entrance);models=new GameObject("ImportedCommunityModels").transform;models.SetParent(transform,false);var mod=models.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();mod.ignoreFromBuild=true;mod.applyToChildren=true;
        var forest=PetTownForestLibrary.Current;if(forest&&forest.groundMaterial)
        {
            meadowSurface=new Material(forest.groundMaterial){name="CommunityMeadow"};var localGround=Resources.Load<Shader>("Environment/Community/MeadowGround");if(localGround)meadowSurface.shader=localGround;meadowSurface.SetFloat("_GrassTiling",2.1f);meadowSurface.SetFloat("_PaintStrength",0);meadowSurface.SetFloat("_UseCoverage",.25f);meadowSurface.SetTexture("_Coverage",forest.meadowMask);meadowSurface.SetVector("_CoverageBounds",forest.meadowBounds);ownedMaterials.Add(meadowSurface);
            pathSurface=new Material(forest.groundMaterial){name="CommunityEarthPath"};pathSurface.SetFloat("_PaintStrength",3);pathSurface.SetFloat("_UseCoverage",0);ownedMaterials.Add(pathSurface);
        }
        float half=kind==7?11:7.5f;Spawn(library.grass,"CommunityGround",Vector3.zero,new Vector3(half*2,.045f,half*2));
        if(kind==5)BuildPark();else if(kind==6)BuildLake();else BuildZoo();
        if(kind==5)FenceRing(Vector3.zero,half,half,true);
        Spawn(library.path,"EntrancePath",new Vector3(entrance.x,.05f,-half+1),new Vector3(2.6f,.025f,2.1f));
        Spawn(library.lamp,"EntranceLamp",new Vector3(entrance.x-1.8f,.045f,-half+.5f),new Vector3(.7f,2.4f,.7f));
        if(kind==5||kind==6)gameObject.AddComponent<PetTownSceneryDecor>().Initialize(kind);
        BuildStonePaths();gameObject.AddComponent<PetTownCommunityMeadow>().Initialize(this,stoneLayout);RefreshAnimals();
    }
    void BuildPark()
    {
        Spawn(library.path,"Walkway_NS",new Vector3(0,.05f,0),new Vector3(2.6f,.025f,14.5f));Spawn(library.path,"Walkway_EW",new Vector3(0,.052f,0),new Vector3(14.5f,.025f,2.6f));
        Spawn(library.fountain,"Fountain",new Vector3(0,.08f,0),new Vector3(2.4f,1.5f,2.4f));Block("FountainCollision",new Vector3(0,.65f,0),new Vector3(2.4f,1.3f,2.4f));
        for(int i=0;i<4;i++){float x=i%2==0?-4.8f:4.8f,z=i<2?-2:2;Spawn(library.bench,"ParkBench",new Vector3(x,.045f,z),new Vector3(2.3f,.95f,.8f),x<0?90:-90);Block("BenchCollision",new Vector3(x,.45f,z),new Vector3(.8f,.9f,2.3f));}
        for(int side=-1;side<=1;side+=2){Tree(side<0?new Vector3(-6.45f,.045f,-6.4f):new Vector3(6.35f,.045f,6.1f),side<0?4.3f:3.8f);FlowerPatch(new Vector3(side*4.8f,.045f,-5),2.3f,1.8f);}
        BuildParkPlayground();
    }
    void BuildLake()
    {
        // Irregular shoreline, with a dry walkable ring inside the owned footprint.
        Disc("SandyShore",new Vector3(0,.055f,1),5.3f,4.1f,new Color(.67f,.59f,.40f),false);
        Disc("LakeWater",new Vector3(0,.085f,1),4.6f,3.45f,Color.white,true);
        Block("LakeWaterSafety",new Vector3(0,.3f,1),new Vector3(9.3f,.65f,7));
        for(int side=-1;side<=1;side+=2){Spawn(library.bench,"LakesideBench",new Vector3(side*6,.045f,0),new Vector3(2.2f,.9f,.75f),side<0?90:-90);Block("BenchCollision",new Vector3(side*6,.45f,0),new Vector3(.75f,.9f,2.2f));Tree(new Vector3(side*5.7f,.045f,5.7f),3.8f);}
        Spawn(library.path,"ShoreApproach",new Vector3(0,.05f,-4.8f),new Vector3(2.6f,.025f,4.5f));
        if(library.stonePathMaterial)
        {
            // Dry promenades stay outside the existing rectangular water safety boundary.
            Spawn(library.path,"ShoreWalk_W",new Vector3(-5.225f,.05f,1),new Vector3(1.15f,.025f,7));
            Spawn(library.path,"ShoreWalk_E",new Vector3(5.225f,.05f,1),new Vector3(1.15f,.025f,7));
            Spawn(library.path,"ShoreWalk_N",new Vector3(0,.05f,4.95f),new Vector3(11.6f,.025f,.9f));
            Spawn(library.path,"ShoreWalk_S",new Vector3(0,.05f,-2.95f),new Vector3(11.6f,.025f,.9f));
        }
        for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Spawn(library.rock,"ShoreRock",new Vector3(Mathf.Cos(a)*4.3f,.07f,1+Mathf.Sin(a)*3.15f),new Vector3(.65f,.35f,.6f),i*37);}
    }
    void BuildZoo(){BuildDetailedZoo();}
    void FlowerPatch(Vector3 center,float width,float depth)
    {
        var detail=PetTownSceneryLibrary.Current;var flower=detail&&detail.detailedFlowers!=null&&detail.detailedFlowers.Length>0?detail.detailedFlowers[0]:library.flowers;
        for(int i=0;i<6;i++)Spawn(flower,"ImportedFlower",center+new Vector3((i%3-1)*width*.3f,0,(i/3-.5f)*depth*.5f),Vector3.one*(.28f+i%2*.07f),i*57,true);
    }
    void Tree(Vector3 pos,float height)
    {
        var forest=PetTownForestLibrary.Current;if(!forest||forest.trees==null||forest.trees.Length==0)return;
        int variant=Kind==5?(pos.x<0?0:1):Kind==6?(pos.x<0?2:3):0;var prefab=forest.trees[variant%forest.trees.Length];if(!prefab)return;
        Spawn(prefab,"ImportedTree",pos,Vector3.one*height,0,true);
        Block("TreeTrunkCollision",pos+Vector3.up*.7f,new Vector3(.5f,1.4f,.5f));
    }
    void FenceRing(Vector3 center,float halfW,float halfD,bool gate)
    {
        for(int side=0;side<4;side++){bool x=side<2;float length=x?halfW*2:halfD*2;int count=Mathf.CeilToInt(length/1.5f);float step=length/count;for(int i=0;i<count;i++){float u=-length*.5f+(i+.5f)*step;if(gate&&side==0&&Mathf.Abs(u-EntranceLocal.x)<1.55f)continue;var pos=center+(x?new Vector3(u,.045f,side==0?-halfD:halfD):new Vector3(side==2?-halfW:halfW,.045f,u));Spawn(library.fence,"Fence",pos,new Vector3(step,.95f,.15f),x?0:90);if(gate)Block("FenceCollision",pos+Vector3.up*.47f,x?new Vector3(step,.95f,.15f):new Vector3(.15f,.95f,step));}}
    }
    void Block(string label,Vector3 pos,Vector3 size){var node=new GameObject(label);node.transform.SetParent(transform,false);node.transform.localPosition=pos;var box=node.AddComponent<BoxCollider>();box.size=size;var obstacle=node.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=size;obstacle.carving=true;obstacle.carveOnlyStationary=false;}
    GameObject Spawn(GameObject prefab,string label,Vector3 pos,Vector3 size,float yaw=0,bool uniform=false)
    {
        var holder=new GameObject(label);holder.transform.SetParent(models,false);if(!prefab)return holder;
        if((Kind==5||Kind==6||Kind==7)&&prefab==library.path&&library.stonePathMaterial)
        {
            // A single curved union is built after all decoration is placed.
            holder.transform.localPosition=pos;return holder;
        }
        // Fit in an identity parent: imported FBX roots may have a -90 degree axis correction.
        var fit=new GameObject("AlignedFit").transform;fit.SetParent(holder.transform,false);var instance=Instantiate(prefab,fit);instance.transform.localPosition=Vector3.zero;
        foreach(var c in instance.GetComponentsInChildren<Collider>(true))c.enabled=false;foreach(var script in instance.GetComponentsInChildren<MonoBehaviour>(true))if(script)script.enabled=false;
        var surface=label=="CommunityGround"?meadowSurface:prefab==library.path?pathSurface:null;if(surface)foreach(var renderer in instance.GetComponentsInChildren<Renderer>()){var slots=renderer.sharedMaterials;for(int i=0;i<slots.Length;i++)slots[i]=surface;renderer.sharedMaterials=slots;}
        var b=BoundsOf(holder.transform);if(!uniform&&size.x>size.z*2&&b.size.z>b.size.x*1.5f){instance.transform.localRotation=Quaternion.Euler(0,90,0)*instance.transform.localRotation;b=BoundsOf(holder.transform);}
        fit.localScale=label=="Fountain"?Vector3.one*Mathf.Min(size.x/Mathf.Max(.001f,b.size.x),size.y/Mathf.Max(.001f,b.size.y),size.z/Mathf.Max(.001f,b.size.z)):uniform?Vector3.one*(size.y/Mathf.Max(.001f,b.size.y)):new Vector3(size.x/Mathf.Max(.001f,b.size.x),size.y/Mathf.Max(.001f,b.size.y),size.z/Mathf.Max(.001f,b.size.z));
        b=BoundsOf(holder.transform);fit.localPosition-=new Vector3(b.center.x,b.min.y,b.center.z);holder.transform.localPosition=pos;holder.transform.localRotation=Quaternion.Euler(0,yaw,0);if(Application.isPlaying&&label=="Fountain"&&PetTownSceneryLibrary.Current&&PetTownSceneryLibrary.Current.fountainFlow)holder.AddComponent<PetTownFountainFlow>().Initialize(instance,PetTownSceneryLibrary.Current.fountainFlow);return holder;
    }
    internal bool IsStonePath(Vector3 point,float margin)=>stoneLayout!=null&&stoneLayout.Contains(point,margin);
    void BuildStonePaths()
    {
        if(stoneLayout==null||!library.stonePathMaterial)return;
        if(pathSurface){var fringe=stoneLayout.CreateMesh(null,.22f);var color=new Color[fringe.vertexCount];var v=fringe.vertices;for(int i=0;i<v.Length;i++){float d=stoneLayout.Field(new Vector2(v[i].x,v[i].z));color[i]=new Color(Mathf.Clamp01((d+.22f)/.32f)*.8f,0,0,1);v[i].y=.0465f;}fringe.vertices=v;fringe.colors=color;fringe.RecalculateBounds();ownedMeshes.Add(fringe);var soil=new GameObject("SoftSoilPathEdge");soil.transform.SetParent(models,false);soil.AddComponent<MeshFilter>().sharedMesh=fringe;var edgeMaterial=new Material(meadowSurface?meadowSurface:pathSurface){name="FeatheredEarthGrassSeam"};edgeMaterial.SetFloat("_PaintStrength",1);ownedMaterials.Add(edgeMaterial);soil.AddComponent<MeshRenderer>().sharedMaterial=edgeMaterial;}
        var mesh=stoneLayout.CreateMesh(library.stonePathHeight);ownedMeshes.Add(mesh);
        var node=new GameObject("StonePromenade");node.transform.SetParent(models,false);node.AddComponent<MeshFilter>().sharedMesh=mesh;node.AddComponent<MeshRenderer>().sharedMaterial=library.stonePathMaterial;
    }
    static Bounds BoundsOf(Transform root)
    {
        bool first=true;var bounds=new Bounds();foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!filter.sharedMesh)continue;var b=filter.sharedMesh.bounds;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var point=root.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}
        }
        return bounds;
    }
    void Disc(string label,Vector3 pos,float rx,float rz,Color color,bool water)
    {
        const int count=64;var vertices=new Vector3[count+1];var uv=new Vector2[count+1];var colors=new Color[count+1];var triangles=new int[count*3];uv[0]=new Vector2(.5f,.5f);colors[0]=new Color(.8f,0,0,1);
        for(int i=0;i<count;i++){float a=i*2*Mathf.PI/count;float irregular=1+Mathf.Sin(a*3)*.035f+Mathf.Cos(a*5)*.025f;vertices[i+1]=new Vector3(Mathf.Cos(a)*rx*irregular,0,Mathf.Sin(a)*rz*irregular);uv[i+1]=new Vector2(0,i/(float)count);colors[i+1]=new Color(0,0,0,1);triangles[i*3]=0;triangles[i*3+1]=(i+1)%count+1;triangles[i*3+2]=i+1;}
        var mesh=new Mesh{name=label};mesh.vertices=vertices;mesh.triangles=triangles;mesh.uv=uv;mesh.colors=colors;mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);var node=new GameObject(label);node.transform.SetParent(models,false);node.transform.localPosition=pos;node.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=node.AddComponent<MeshRenderer>();var material=new Material(water?Resources.Load<Shader>("Environment/PetTownCozyStream"):Shader.Find("Universal Render Pipeline/Lit"));if(!water){material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.12f);}ownedMaterials.Add(material);renderer.sharedMaterial=material;
    }
    public void RefreshAnimals()
    {
        if(Kind!=7||!library||!game)return;var lot=game.Progression.Community.Get(Id,Kind,game.CurrentFarmDay);int hash=17;foreach(int count in lot.animals)hash=hash*31+count;if(hash==signature)return;signature=hash;if(exhibits){exhibits.gameObject.SetActive(false);Destroy(exhibits.gameObject);}exhibits=new GameObject("ExhibitAnimals").transform;exhibits.SetParent(models,false);
        for(int s=0;s<Mathf.Min(lot.animals.Length,library.animals.Length);s++)for(int i=0;i<lot.animals[s];i++){var def=library.animals[s];if(def==null||!def.prefab)continue;var actor=new GameObject(PetTownCommunityState.AnimalNames[s]+"_"+i);actor.transform.SetParent(exhibits,false);actor.transform.localPosition=PetTownZooLayout.Animal(s,i);actor.transform.localRotation=Quaternion.Euler(0,35+s*43+i*35,0);var exhibit=actor.AddComponent<PetTownExhibitActor>();exhibit.Initialize(game,def);exhibit.EnableRoam(s);}
    }
    void Update(){if(Time.unscaledTime<next)return;next=Time.unscaledTime+1;RefreshAnimals();}
    void OnDestroy(){foreach(var m in ownedMaterials)if(m){if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);}foreach(var m in ownedMeshes)if(m){if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);}}
}
