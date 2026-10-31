using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// GPU instancing of the supplied grass/flower/clover meshes. No generated blade stand-ins,
// terrain edits, per-blade GameObjects or navigation obstacles.
public sealed class PetTownGrassField : MonoBehaviour
{
    sealed class Piece {public Mesh mesh;public Material material;public Matrix4x4 local;public int sub;}
    sealed class Species {public readonly List<Piece> pieces=new List<Piece>();}
    struct Plant {public Matrix4x4 matrix;public float rank,variant;}
    sealed class Patch {public Bounds bounds;public readonly List<Plant>[] plants=new List<Plant>[6];}
    const float PatchSize=8;float Cell=>carpet ? .34f:.48f;bool carpet;
    readonly List<Species> species=new List<Species>();readonly Dictionary<Vector2Int,Patch> patches=new Dictionary<Vector2Int,Patch>();
    readonly List<Material> materials=new List<Material>();readonly List<Patch> visible=new List<Patch>();
    readonly List<Vector2Int> stale=new List<Vector2Int>();
    readonly Matrix4x4[] drawMatrices=new Matrix4x4[1023];readonly Plane[] planes=new Plane[6];
    PetTownForestLibrary library;
    PetTownGame game;int revision=-1;Vector3 focus;bool ready;
    public int PatchCount=>patches.Count;
    public int CachedPlantCount {get;private set;}
    public float DensityFraction {get;private set;}=1;
    const int PlantBudget=16000;
    public int VisibleFarPatchCount {get;private set;}
    public int GeneratedThisFrame {get;private set;}
    public int VisiblePlantCount {get;private set;}
    public void Initialize(PetTownGame source)
    {
        game=source;library=PetTownForestLibrary.Current;if(!library)return;carpet=library.approvedMeadow;
        foreach(var key in new[]{"Grass_Common_Short","Grass_Wispy_Short","Flower_3_Group","Flower_4_Group","Clover_1","Fern_1"})
        {
            var prefab=library.Find(key);if(!prefab)return;var value=new Species();
            foreach(var mf in prefab.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=mf.GetComponent<Renderer>();if(!renderer||!mf.sharedMesh)continue;
                for(int i=0;i<mf.sharedMesh.subMeshCount&&i<renderer.sharedMaterials.Length;i++)
                {
                    var material=new Material(renderer.sharedMaterials[i]);material.name=key+"_Instanced";material.enableInstancing=true;if(material.HasProperty("_FadeStart"))material.SetFloat("_FadeStart",0);if(material.HasProperty("_FadeEnd"))material.SetFloat("_FadeEnd",0);materials.Add(material);
                    value.pieces.Add(new Piece{mesh=mf.sharedMesh,material=material,sub=i,local=prefab.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix});
                }
            }
            species.Add(value);
        }
        ready=true;
    }
    void Update()
    {
        if(!ready||!game)return;var camera=Camera.main;if(!camera)return;
        var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));var ground=new Plane(Vector3.up,Vector3.zero);
        focus=ground.Raycast(ray,out float distance)?ray.GetPoint(distance):game.transform.position;
        Shader.SetGlobalVector("_PetTownGrassFocus",focus);
        if(revision!=game.VegetationRevision){revision=game.VegetationRevision;patches.Clear();}
        GeometryUtility.CalculateFrustumPlanes(camera,planes);
        int firstWorld=Mathf.FloorToInt(-game.WorldHalfExtent/PatchSize),lastWorld=Mathf.CeilToInt(game.WorldHalfExtent/PatchSize)-1;
        var center=new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(focus.x/PatchSize),firstWorld,lastWorld),Mathf.Clamp(Mathf.FloorToInt(focus.z/PatchSize),firstWorld,lastWorld));
        float minX=float.PositiveInfinity,minZ=float.PositiveInfinity,maxX=float.NegativeInfinity,maxZ=float.NegativeInfinity;
        for(int i=0;i<4;i++)
        {
            var corner=camera.ViewportPointToRay(new Vector3(i%2,i/2,0));float hitDistance;
            if(!ground.Raycast(corner,out hitDistance))continue;var point=corner.GetPoint(hitDistance);
            minX=Mathf.Min(minX,point.x);maxX=Mathf.Max(maxX,point.x);minZ=Mathf.Min(minZ,point.z);maxZ=Mathf.Max(maxZ,point.z);
        }
        if(float.IsInfinity(minX)){minX=focus.x-game.WorldHalfExtent;maxX=focus.x+game.WorldHalfExtent;minZ=focus.z-game.WorldHalfExtent;maxZ=focus.z+game.WorldHalfExtent;}
        int firstX=Mathf.Max(firstWorld,Mathf.FloorToInt((minX-16)/PatchSize)),lastX=Mathf.Min(lastWorld,Mathf.FloorToInt((maxX+16)/PatchSize));
        int firstZ=Mathf.Max(firstWorld,Mathf.FloorToInt((minZ-16)/PatchSize)),lastZ=Mathf.Min(lastWorld,Mathf.FloorToInt((maxZ+16)/PatchSize));
        int maxRing=Mathf.Min(lastWorld-firstWorld+1,Mathf.Max(Mathf.Max(Mathf.Abs(firstX-center.x),Mathf.Abs(lastX-center.x)),Mathf.Max(Mathf.Abs(firstZ-center.y),Mathf.Abs(lastZ-center.y))));
        int made=0,budget=camera.orthographicSize>24?12:6;
        // Cover the visible world, rather than a fixed 42m circle. Work stays bounded per frame.
        for(int ring=0;ring<=maxRing&&made<budget;ring++)for(int x=-ring;x<=ring&&made<budget;x++)for(int z=-ring;z<=ring&&made<budget;z++)
        {
            if(ring>0&&Mathf.Abs(x)!=ring&&Mathf.Abs(z)!=ring)continue;var key=center+new Vector2Int(x,z);
            if(key.x<firstX||key.x>lastX||key.y<firstZ||key.y>lastZ)continue;
            Vector3 point=new Vector3((key.x+.5f)*PatchSize,0,(key.y+.5f)*PatchSize);
            var bounds=new Bounds(new Vector3(point.x,game.GetGroundHeight(point.x,point.z)+2,point.z),new Vector3(PatchSize,16,PatchSize));
            if(!GeometryUtility.TestPlanesAABB(planes,bounds))continue;
            if(PetTownDistanceFog.Current&&Vector2.Distance(new Vector2(point.x,point.z),new Vector2(focus.x,focus.z))>PetTownDistanceFog.Current.FarDistance+PatchSize&&!PetTownDistanceFog.Current.HasExplored(bounds))continue;
            if(PetTownDistanceFog.Current&&PetTownDistanceFog.Current.FullyCovers(bounds))continue;
            // A camera zoom never changes or rebuilds a patch population.
            if(patches.ContainsKey(key))continue;
            patches[key]=Generate(key,bounds);made++;
        }
        GeneratedThisFrame=made;
        stale.Clear();foreach(var pair in patches)
        {
            var retained=pair.Value.bounds;retained.Expand(PatchSize*3);
            if(!GeometryUtility.TestPlanesAABB(planes,retained)||(PetTownDistanceFog.Current&&Vector2.Distance(new Vector2(pair.Value.bounds.center.x,pair.Value.bounds.center.z),new Vector2(focus.x,focus.z))>PetTownDistanceFog.Current.FarDistance+PatchSize*3&&!PetTownDistanceFog.Current.HasExplored(retained)))stale.Add(pair.Key);
        }
        foreach(var pair in patches)
        {
            if(!PetTownDistanceFog.Current)break;var padded=pair.Value.bounds;
            if(PetTownDistanceFog.Current.FullyCovers(padded))stale.Add(pair.Key);
        }
        foreach(var key in stale)patches.Remove(key);
    }
    static float Random(uint seed,uint salt)
    {
        uint value=seed^salt;value^=value>>16;value*=0x7feb352d;value^=value>>15;value*=0x846ca68b;value^=value>>16;
        return (value&0x00ffffff)/16777216f;
    }
    Patch Generate(Vector2Int key,Bounds bounds)
    {
        var patch=new Patch{bounds=bounds};for(int i=0;i<6;i++)patch.plants[i]=new List<Plant>();
        int cells=Mathf.CeilToInt(PatchSize/Cell);
        for(int x=0;x<cells;x++)for(int z=0;z<cells;z++)
        {
            uint seed=unchecked((uint)(key.x*73856093^key.y*19349663^x*83492791^z*15592117^71891));
            float px=key.x*PatchSize+x*Cell+Random(seed,17)*Cell,pz=key.y*PatchSize+z*Cell+Random(seed,31)*Cell;
            if(px<-game.WorldHalfExtent+1||px>game.WorldHalfExtent-1||pz<-game.WorldHalfExtent+1||pz>game.WorldHalfExtent-1)continue;
            var coverage=library.Coverage(px,pz);float density=Mathf.PerlinNoise(px*.105f+19,pz*.105f+31);
            if(Random(seed,47)>(carpet?coverage.g:density>.43f?.9f:.28f))continue;
            var point=new Vector3(px,game.GetGroundHeight(px,pz),pz);if(!game.VegetationAllowed(point))continue;
            {float edge=.45f;if(!game.VegetationAllowed(point+Vector3.right*edge)||!game.VegetationAllowed(point-Vector3.right*edge)||!game.VegetationAllowed(point+Vector3.forward*edge)||!game.VegetationAllowed(point-Vector3.forward*edge))continue;}
            int kind=Random(seed,59)<.77f?0:1;float cluster=Mathf.PerlinNoise(px*.19f+7,pz*.19f+17),choice=Random(seed,67);
            if(carpet){if(cluster>.57f&&choice>.82f)kind=2+Mathf.Min(3,(int)(Random(seed,71)*4));}
            else if(cluster>.54f&&choice>.89f)kind=2+Mathf.Min(1,(int)(Random(seed,71)*2));else if(choice>.96f)kind=4;else if(density>.65f&&choice>.93f)kind=5;
            float height=kind<2?.24f+Random(seed,83)*.13f:kind<4?.34f+Random(seed,83)*.18f:kind==4?.10f:.31f;
            if(carpet)height=(kind<2?.29f+Random(seed,83)*.13f:.40f+Random(seed,83)*.17f)*Mathf.Lerp(.55f,1,coverage.b);
            float spread=carpet?1.12f:kind==0?3.2f:kind==1?1.65f:1;
            float y=point.y,dx=game.GetGroundHeight(px+.2f,pz)-y,dz=game.GetGroundHeight(px,pz+.2f)-y;var normal=new Vector3(-dx/.2f,1,-dz/.2f).normalized;
            var rotation=Quaternion.FromToRotation(Vector3.up,normal)*Quaternion.Euler(0,Random(seed,97)*360,0);
            patch.plants[kind].Add(new Plant{matrix=Matrix4x4.TRS(point,rotation,new Vector3(height*spread,height,height*spread)),rank=Random(seed,113),variant=Random(seed,139)});
        }
        foreach(var list in patch.plants)list.Sort((a,b)=>a.rank.CompareTo(b.rank));
        return patch;
    }
    void LateUpdate()
    {
        if(!ready)return;var camera=Camera.main;if(!camera)return;GeometryUtility.CalculateFrustumPlanes(camera,planes);visible.Clear();VisiblePlantCount=0;VisibleFarPatchCount=0;
        int population=0;CachedPlantCount=0;
        foreach(var patch in patches.Values)
        {
            foreach(var list in patch.plants)CachedPlantCount+=list.Count;
            if(!GeometryUtility.TestPlanesAABB(planes,patch.bounds))continue;
            visible.Add(patch);foreach(var list in patch.plants)population+=list.Count;
        }
        // Use one continuous screen-space density for the whole view. There are no distance rings
        // and no old dense patches left waiting to be regenerated at another zoom level.
        float viewSize=camera.orthographic?camera.orthographicSize:Vector3.Distance(camera.transform.position,focus)*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
        float desired=Mathf.Clamp(Mathf.Pow(20/Mathf.Max(20,viewSize),1.35f),.13f,1);
        desired=Mathf.Min(desired,PlantBudget/(float)Mathf.Max(1,population));
        DensityFraction=desired;float spread=Mathf.Min(2.7f,1/Mathf.Sqrt(Mathf.Max(.13f,desired)));
        var scale=Matrix4x4.Scale(new Vector3(spread,1,spread));
        
        VisibleFarPatchCount=desired<.999f?visible.Count:0;
        for(int s=0;s<species.Count;s++)
        {
            // Distribute the switch to the lighter imported mesh across individual plants,
            // rather than switching every common-grass patch at one zoom threshold.
            float commonShare=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.55f,desired));
            for(int variant=0;variant<(s==0?2:1);variant++)
            {
            var shown=s==0&&variant==1?species[1]:species[s];
            foreach(var piece in shown.pieces)
            {
                int count=0;
                foreach(var patch in visible)
                {
                    var list=patch.plants[s];int selected=SelectedCount(list,desired);
                    for(int i=0;i<selected;i++)
                    {
                        var plant=list[i];
                        if(PetTownDistanceFog.Current){float fog=PetTownDistanceFog.Current.Amount(plant.matrix.GetColumn(3));if(plant.rank>=desired*Mathf.Pow(1-fog,1.5f))continue;}
                        if(s==0&&(plant.variant<commonShare)!=(variant==0))continue;
                        if(piece==shown.pieces[0])VisiblePlantCount++;
                        var matrix=plant.matrix;
                        drawMatrices[count++]=(s<2?matrix*scale:matrix)*piece.local;
                        if(count==1023){Draw(piece,count,camera);count=0;}
                    }
                }
                if(count>0)Draw(piece,count,camera);
            }
            }
        }
    }
    static int SelectedCount(List<Plant> list,float fraction)
    {
        // Sorted stable random ranks remove isolated plants gradually instead of clipping a region.
        int lo=0,hi=list.Count;
        while(lo<hi){int mid=(lo+hi)/2;if(list[mid].rank<fraction)lo=mid+1;else hi=mid;}
        return lo;
    }

    void Draw(Piece piece,int count,Camera camera)
    {
        if(SystemInfo.supportsInstancing)Graphics.DrawMeshInstanced(piece.mesh,piece.sub,piece.material,drawMatrices,count,null,ShadowCastingMode.Off,true,0,camera,LightProbeUsage.Off);
        else for(int i=0;i<Mathf.Min(count,160);i++)Graphics.DrawMesh(piece.mesh,drawMatrices[i],piece.material,0,camera,piece.sub,null,ShadowCastingMode.Off,true,null,LightProbeUsage.Off);
    }
    void OnDestroy(){foreach(var material in materials)if(material)Destroy(material);}
}
