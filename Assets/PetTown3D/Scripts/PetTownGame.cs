using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Pet Town 3D - standalone prototype. Every visual is a replaceable GameObject.
// No third-party assets or packages are required.
public partial class PetTownGame : MonoBehaviour
{
    enum Mode { Start, Play, Build, Furniture, Customize }
    Mode mode = Mode.Start;
    readonly List<GameObject> buildings = new List<GameObject>();
    readonly List<GameObject> props = new List<GameObject>();
    readonly List<GameObject> vegetationProps = new List<GameObject>(); // grass/flower/mushroom scatter, so it can be swept out of a building's footprint once the building exists
    readonly List<GameObject> fog = new List<GameObject>();
    readonly List<GameObject> villagers = new List<GameObject>();
    class VillagerAnim
    {
        public Animation anim; public Animator animator;
        public string idleClip, walkClip, runClip;
        public Transform model;
        // Bind/rest pose captured before the first runtime animation sample.
        public Transform[] poseBones;
        public Vector3[] restPositions;
        public Quaternion[] restRotations;
        public Vector3[] restScales;
        public bool wasMoving;
        public Transform leftUpperArm, rightUpperArm;
        public Quaternion leftRelaxedRotation, rightRelaxedRotation;
    }
    static void RestoreVillagerRestPose(VillagerAnim info)
    {
        if (info.poseBones == null) return;
        for (int i = 0; i < info.poseBones.Length; i++)
        {
            Transform bone = info.poseBones[i];
            if (bone == null) continue;
            bone.localPosition = info.restPositions[i];
            bone.localRotation = info.restRotations[i];
            bone.localScale = info.restScales[i];
        }
    }
    readonly Dictionary<GameObject, VillagerAnim> villagerAnimInfo = new Dictionary<GameObject, VillagerAnim>();
    class BuildingFloor { public GameObject owner; public Vector3 center; public float halfW, halfD, floorY; }
    readonly List<BuildingFloor> buildingFloors = new List<BuildingFloor>();
    // Registered by FlattenTerrain() every time it runs, so Ground() can report the REAL
    // (already-flattened) terrain height for any point inside a flattened zone instead of
    // falling back to the raw procedural noise height. Without this, a later building/road
    // placed just past a building's own floor footprint (but still inside its wider terrain
    // flatten radius) would read Height()'s stale pre-flatten noise value, flatten itself to
    // THAT wrong height, and punch a mismatched bump back into terrain that was already flat.
    class FlatZone { public Vector3 center; public float halfW, halfD, y; }
    readonly List<FlatZone> flatZones = new List<FlatZone>();
    enum VillagerGoal { Stroll, GoToShop, AtShop }
    class VillagerWander { public Vector3 target; public float nextPick; public bool running; public VillagerGoal goal; public float stuckSince=-1f; public Vector3 previousPosition; public bool hasPreviousPosition; }
    readonly Dictionary<GameObject, VillagerWander> villagerWander = new Dictionary<GameObject, VillagerWander>();
    // Villager pathfinding runs on a runtime-baked NavMesh instead of hand-rolled capsule
    // avoidance — that approach kept failing in new ways (corner clipping, sideways jitter,
    // getting stuck) because it was re-deriving what Unity's own navigation system already
    // solves properly. Rebaked whenever the world geometry changes (new building placed/loaded).
    NavMeshSurface navSurface;
    void RebakeNavMesh()
    {
        if (navSurface == null)
        {
            navSurface = gameObject.GetComponent<NavMeshSurface>();
            if (navSurface == null) navSurface = gameObject.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.All;
            navSurface.layerMask = ~0; // include everything (terrain, walls, floors)
            navSurface.overrideVoxelSize = true;
            navSurface.voxelSize = 0.17f;
        }
        navSurface.BuildNavMesh();
        constructionWorldRevision++;
        if(placementValidator!=null)placementValidator.Invalidate();
    }
    // Imported Meshy/asset-store meshes (trees, furniture, the player, villagers) are often
    // marked "not readable" for runtime, which makes NavMesh baking throw a read-access error
    // for every one of them whenever it runs while they're in the scene. None of them need to
    // contribute static navmesh geometry anyway (the player/villagers move and are their own
    // NavMeshAgents; trees/furniture are just decoration here), so just tell the bake to skip
    // their render meshes entirely instead of fighting mesh import settings.
    void MarkNavMeshIgnore(GameObject go)
    {
        if (go == null) return;
        var mod = go.GetComponent<NavMeshModifier>();
        if (mod == null) mod = go.AddComponent<NavMeshModifier>();
        mod.ignoreFromBuild = true;
        mod.applyToChildren = true;
    }
    // Runtime NavMesh carving for non-readable imported meshes and decorative furniture.
    // Obstacles are independent of the imported mesh and do not alter asset files.
    void AddWalkObstacle(GameObject root, Bounds b, float inset = 0f)
    {
        if (root == null || b.size.x < .12f || b.size.z < .12f) return;
        var obstacle = root.GetComponent<NavMeshObstacle>();
        if (obstacle == null) obstacle = root.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        // Bounds are world-axis aligned; NavMeshObstacle.size is ROOT-LOCAL.
        // Transform all eight corners to prevent rotated modular walls from
        // acquiring a wrongly sized carving footprint.
        Vector3 localMin = new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);
        Vector3 localMax = new Vector3(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);
        for (int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
        {
            Vector3 corner=b.center+Vector3.Scale(b.extents,new Vector3(x,y,z));
            Vector3 local=root.transform.InverseTransformPoint(corner);
            localMin=Vector3.Min(localMin,local);
            localMax=Vector3.Max(localMax,local);
        }
        obstacle.center=(localMin+localMax)*.5f;
        Vector3 localSize=localMax-localMin;
        obstacle.size=new Vector3(Mathf.Max(.12f,localSize.x-inset),
            Mathf.Max(.3f,localSize.y),Mathf.Max(.12f,localSize.z-inset));
        obstacle.carving=true;
        obstacle.carveOnlyStationary=false;
    }
    IEnumerator AddFittedFurnitureObstacle(GameObject root)
    {
        yield return null; yield return null; // FitModelNextFrame has completed.
        if (root == null) yield break;
        var rr = root.GetComponentsInChildren<Renderer>();
        if (rr.Length == 0) yield break;
        Bounds b = rr[0].bounds;
        for (int i=1;i<rr.Length;i++) b.Encapsulate(rr[i].bounds);
        AddWalkObstacle(root,b,.08f);
    }
    void AddShopWallObstacle(GameObject wall)
    {
        if (wall == null) return;
        var rr=wall.GetComponentsInChildren<Renderer>();
        if(rr.Length==0)return;
        Bounds b=rr[0].bounds;
        for(int i=1;i<rr.Length;i++)b.Encapsulate(rr[i].bounds);
        AddWalkObstacle(wall,b);
    }
    // If an imported villager visibly faces away from its walk direction, flip this to 180.
    const float VillagerFacingOffsetY = 0f;
    // Unused Unity layer index — the terrain mesh goes here so the villager wall-avoidance
    // raycast (which must ignore gentle hills, only real walls/furniture) can exclude it.
    const int TerrainLayer = 9;
    GameObject hero, cameraPivot, shop, roof, ghost;
    // User request: Road is placed by click-dragging a curved path (not one tile per click).
    List<Vector3> roadDragPoints; bool roadDragging; GameObject roadDragPreview;
    Transform heroLeftArm, heroRightArm, heroLeftLeg, heroRightLeg;
    float walkPhase;
    Animator meshyAnimator;
    bool usingMeshyPlayer;
    bool meshyWasMoving;
    Animation[] meshyLegacyAnimations;
    bool meshyLegacyPresent;
    Camera cam;
    Material grass, road, water, dark, wood, wall, roofMat, skin, shirt, hairMat;
    Material groundDirt, groundGrassSparse, groundHeather, groundBlend; // ground-zone textures; groundBlend is the single shader that smoothly blends them via per-vertex weights
    Material foliageLight, foliageDark, trim, windowMat, gold, blush;
    // EasyRoads3D Free is already in the project (Assets/EasyRoads3D) — its own dirt-road
    // material/texture is reused for the road ribbon instead of a flat gray color, which is
    // the practical way to use that asset here: EasyRoads3D's own road-building workflow is a
    // hand-authored Scene-view tool (place markers, build splines in the Editor), which doesn't
    // fit this project's fully procedural, no-manual-scene-authoring approach. Its material
    // already lives under an "EasyRoads3D/Resources" folder, so it's loadable by name directly.
    Material dirtRoadMat;
    Material roadSurfaceMaterial;
    Material flowingRiverMaterial; // separate from the still-water lake material
    static readonly string[] MedievalHouseResources = { "MedievalHouses/SmallMedievalHouse", "MedievalHouses/MedievalHouse", "MedievalHouses/TwoStoryMedievalHouse", "MedievalHouses/TwoStoryMedievalHouse_2" };
    static readonly string[] RockResources = { "Rocks/SmallRock", "Rocks/MossyBoulder", "Rocks/RockCluster" };
    const string FenceResource = "FenceKit/WoodenFence", GateResource = "FenceKit/WoodenGate";
    // Small swimming fish for the pet-shop aquarium prop. The "Floreswa" pack's fish01/02/03
    // models are plain low-poly fish shapes, which is what actually fits a little aquarium tank
    // — the user's other two suggestions were checked and don't: MAR_Fish_Market_Building is a
    // whole market storefront (sign/hooks/awning, no tank), and Low-Poly Medieval Market's fish
    // assets are seafood-stall produce (fish on ice/in crates), not an aquarium pet. Copied into
    // Resources the same way as the medieval houses/rocks/fence (no .meta, fresh GUID).
    static readonly string[] AquariumFishResources = { "AquariumFish/fish01", "AquariumFish/fish02", "AquariumFish/fish03" };
    int money = 100000000, population = 0, sold = 0, inventory = 4;
    const int RoadCostPerMeter = 20;
    int reputation=0, shopLevel=1, day=1, completedOrders=0, missedOrders=0;
    int dogStock=2, catStock=2, rabbitStock=0;
    float dayTimer=0f, orderTimer=0f, orderDeadline=0f;
    int orderPet=-1;
    GameObject waitingCustomer;
    readonly string[] petNames={"Puppy", "Kitten", "Bunny"};
    int selected = -1, hairStyle = 0, faceStyle = 0, bodyStyle = 0, legStyle = 0, accessory = 0;
    int furnitureChoice = 0;
    float worldClock, customerClock, noticeTime, zoom = 7f;
    float cameraYaw = 42f, cameraPitch = 48f;
    Vector3 cameraFocus = Vector3.zero;
    // True while the player has panned the camera away with middle-mouse drag to look at the
    // rest of the map. Stays true after the button is released (so the view doesn't immediately
    // snap back to the character) and only clears once the character actually moves again.
    bool cameraFreeLook = false;
    Vector3 freeLookStartCharPos;
    string notice = "Choose a location for your first Pet Shop";
    bool showRoofs = false;
    Vector3 shopPosition;
    [SerializeField,Min(220)] int WorldSize = 440;
    public float WorldHalfExtent=>WorldSize*.5f;
    readonly string[] types = { "Pet Shop", "Home", "Resident House", "Pet Cafe", "Pet Hospital", "Park", "Lake", "Zoo", "Road", "Farm", "Ranch" };
    readonly int[] costs = { 0, 600, 250, 1500, 1800, 450, 700, 2400, 20, 3000, 2500 };
    readonly string[] furniture = { "Counter", "Pet Bed", "Display", "Chair", "Table", "Plant" };
    GUIStyle titleStyle, textStyle, buttonStyle;

    // Lets other scripts (the player controller, which computes its own ground height
    // independently) ask this single source of truth for standing height — including the
    // raised-floor-inside-a-building case handled by Ground()/buildingFloors below.
    public static PetTownGame Instance;
    public float GetGroundHeight(float x,float z) => Ground(x,z);
    // Click-to-move is disabled while editing roads; keyboard/joystick movement remains available.
    public bool IsRoadConstructionActive => mode == Mode.Build && selected == 8;
    [Header("Construction Grid (new placements only)")]
    [SerializeField, Min(.25f)] float constructionGridSize=1f;
    [SerializeField] Vector2 constructionGridOrigin=Vector2.zero;
    [SerializeField, Range(4,24)] int constructionGridRadius=12;
    PetTownGridOverlay constructionGridOverlay;
    bool UsesBuildingGrid => mode==Mode.Start || (mode==Mode.Build && selected>=0 && selected!=8);
    [SerializeField] bool constructionRoadAlignment=true;
    [SerializeField] PetTownRoadMode roadMode=PetTownRoadMode.FreeCurve;
    PetTownRoadMode roadStrokeMode;
    readonly List<Vector3> unresolvedLegacyRoads=new List<Vector3>();
    Vector3 BuildingPlacementPoint(Vector3 point)
    {
        point=PetTownBuildGrid.SnapHorizontal(point,constructionGridSize,constructionGridOrigin);
        if(constructionRoadAlignment&&PreparePlacementPlan(point))
        {
            RefreshConstructionObstacles(false);
            point=PetTownRoadAlignment.Suggest(point,placementPlanner.Snapshot(),constructionGridSize,constructionGridOrigin,constructionRoadSearchDistance,Mathf.Max(0f,constructionSafety.roadGap-constructionGridSize*.5f),4f,constructionObstacles);
        }
        point.y=RoadTerrainY(point.x,point.z);
        return point;
    }
    void UpdateConstructionGrid()
    {
        if(mode==Mode.Furniture&&!CatalogOpen&&FurnitureOwner!=null)
        {
            if(constructionGridOverlay!=null)constructionGridOverlay.Hide();if(entrancePreview!=null)entrancePreview.Hide();
            if(furnitureGrid==null)furnitureGrid=new GameObject("PetTown_InteriorGrid").AddComponent<PetTownFurnitureGrid>();
            furnitureGrid.Show(FurnitureOwner,furnitureGridSize);return;
        }
        if(furnitureGrid!=null)furnitureGrid.Hide();
        if(mode!=Mode.Build && mode!=Mode.Start)
        {
            if(constructionGridOverlay!=null)constructionGridOverlay.Hide();
            if(entrancePreview!=null)entrancePreview.Hide();
            return;
        }
        if(!UsesBuildingGrid&&entrancePreview!=null)entrancePreview.Hide();
        if(IsPointerOverUI())return;
        if(!MouseRoadGround(out Vector3 point))
        {
            if(constructionGridOverlay!=null)constructionGridOverlay.Hide();
            if(entrancePreview!=null)entrancePreview.Hide();
            return;
        }
        if(constructionGridOverlay==null)
        {
            var overlay=new GameObject("PetTown_ConstructionGrid");
            overlay.transform.SetParent(transform,false);
            constructionGridOverlay=overlay.AddComponent<PetTownGridOverlay>();
        }
        constructionGridOverlay.Show(point,constructionGridSize,constructionGridOrigin,constructionGridRadius,WorldSize*.5f,RoadTerrainY);
    }
    void OnDisable()
    {
        ClosePetPlay();
        ClearShopPetActors();
        ReleaseFacilityActors();
        ClearResidentCompanions();
        if(furnitureGrid!=null)furnitureGrid.Hide();
        if(constructionGridOverlay!=null)constructionGridOverlay.Hide();
        if(entrancePreview!=null)entrancePreview.Hide();
    }
    [Header("New Building Direction")]
    [SerializeField] PetTownBuildingDefinition[] constructionDefinitions=PetTownBuildingDefinition.Defaults();
    [SerializeField, Range(4f,32f)] float constructionRoadSearchDistance=16f;
    PetTownPlacementPlanner placementPlanner=new PetTownPlacementPlanner();
    PetTownBuildingSaveRecord activeBuildingPlacement;
    PetTownEntrancePreview entrancePreview;
    [Header("Construction Catalog UI")]
    [SerializeField] bool useNewConstructionCatalog=true,useNewFurnitureCatalog=true,useNewManagementUI=true;
    PetTownCatalogWindow catalogWindow;
    GameObject uiCatalogLauncher;
    bool CatalogOpen=>catalogWindow!=null&&catalogWindow.IsOpen;
    bool CameraInputBlocked=>PetTownCustomizationUI.IsWindowOpen||CatalogOpen||ExpansionOpen||PetCareOpen;
    Text uiPlacementHint,uiConstructionToolLabel;
    GameObject uiConstructionTools;
    PetTownBuildingDefinition DefinitionFor(int kind)
    {
        if(kind>=5&&kind<=7)return PetTownBuildingDefinition.Default(kind);
        if(constructionDefinitions!=null)foreach(var definition in constructionDefinitions)
            if(definition!=null&&definition.kind==kind)
            {
                var minimum=PetTownBuildingDefinition.Default(kind).footprintMeters;
                definition.footprintMeters=Vector2.Max(definition.footprintMeters,minimum);
                definition.entranceBaseYaw=Mathf.Round(definition.entranceBaseYaw/90f)*90f;
                return definition;
            }
        return PetTownBuildingDefinition.Default(kind);
    }
    bool PreparePlacementPlan(Vector3 point)
    {
        if(!UsesBuildingGrid)return false;
        int kind=mode==Mode.Start?0:selected;
        if(placementPlanner==null)placementPlanner=new PetTownPlacementPlanner();
        if(placementPlanner.Kind!=kind)placementPlanner.Select(kind,DefinitionFor(kind),kind==2?UnityEngine.Random.Range(0,MedievalHouseResources.Length):0);
        placementPlanner.Recommend(point,constructionRoadSearchDistance);
        return true;
    }
    void UpdatePlacementDirection(Vector3 point)
    {
        if(!PreparePlacementPlan(point))return;
        var record=placementPlanner.Snapshot();
        if(ghost!=null)ghost.transform.rotation=record.Rotation;
        if(!record.hasEntrance){if(entrancePreview!=null)entrancePreview.Hide();return;}
        if(entrancePreview==null)
        {
            var marker=new GameObject("PetTown_EntrancePreview");marker.transform.SetParent(transform,false);
            entrancePreview=marker.AddComponent<PetTownEntrancePreview>();
        }
        Vector3 entrance=point+record.Rotation*record.entranceLocal;
        entrance.y=Mathf.Max(entrance.y,RoadTerrainY(entrance.x,entrance.z))+.08f;
        entrancePreview.Show(entrance,record.EntranceForward);
    }
    void FlattenBuildingFootprint(Vector3 center,float halfW,float halfD,float y)
    {
        bool swap=activeBuildingPlacement!=null&&Mathf.RoundToInt(activeBuildingPlacement.BuildingYaw/90f)%2!=0;
        FlattenTerrain(center,swap?halfD:halfW,swap?halfW:halfD,y);
    }
    void ClearBuildingFootprint(Vector3 center,float halfW,float halfD)
    {
        bool swap=activeBuildingPlacement!=null&&Mathf.RoundToInt(activeBuildingPlacement.BuildingYaw/90f)%2!=0;
        ClearPropsAndRoadsInFootprint(center,swap?halfD:halfW,swap?halfW:halfD);
    }
    GameObject CreatePlannedBuilding(int kind,Vector3 point,PetTownBuildingSaveRecord record)
    {
        if(record==null||!record.ValidFor(kind))return CreateBuilding(kind,point,false);
        int firstProp=props.Count,firstTree=roadTrees.Count,firstFloor=buildingFloors.Count,firstFish=tankFish.Count;
        var previous=activeBuildingPlacement;GameObject root;
        activeBuildingPlacement=record;
        try{root=CreateBuilding(kind,point,false);}finally{activeBuildingPlacement=previous;}
        record=record.Copy(-1);record.groundY=point.y;
        root.transform.rotation=record.Rotation;
        var metadata=root.AddComponent<PetTownBuildingPlacement>();metadata.Initialize(record);
        for(int i=firstProp;i<props.Count;i++)RotateNewBuildingPart(props[i],root,point,record.Rotation);
        for(int i=firstTree;i<roadTrees.Count;i++)RotateNewBuildingPart(roadTrees[i],root,point,record.Rotation);
        for(int i=firstFish;i<tankFish.Count;i++)
        {
            tankFish[i].center=point+record.Rotation*(tankFish[i].center-point);
            tankFish[i].orientation=record.Rotation;
        }
        bool swap=Mathf.RoundToInt(record.BuildingYaw/90f)%2!=0;
        for(int i=firstFloor;i<buildingFloors.Count;i++)
        {
            var floor=buildingFloors[i];floor.owner=root;
            if(swap){float old=floor.halfW;floor.halfW=floor.halfD;floor.halfD=old;}
        }
        return root;
    }
    static void RotateNewBuildingPart(GameObject part,GameObject root,Vector3 center,Quaternion rotation)
    {
        if(part==null)return;
        part.transform.position=center+rotation*(part.transform.position-center);
        part.transform.rotation=rotation*part.transform.rotation;
        part.transform.SetParent(root.transform,true);
    }
    static PetTownBuildingSaveRecord SavedPlacement(SaveData data,int index,int kind)
    {
        if(data.constructionVersion<2||data.buildingPlacements==null)return null;
        foreach(var placement in data.buildingPlacements)
            if(placement!=null&&placement.legacyIndex==index&&placement.ValidFor(kind))return placement;
        return null;
    }
    [Header("Interior Furniture Placement")]
    [SerializeField,Min(.25f)] float furnitureGridSize=.5f;
    bool loadingTown;
    PetTownInteriorState furnishingOwner;
    PetTownFurnitureGrid furnitureGrid;
    int furnitureQuarter;
    bool furnitureGhostReady;
    float furnitureGhostLift;
    Vector2 furnitureGhostSize=Vector2.one;
    PetTownPlacementValidation furnitureValidation;
    bool furnitureCached,lastFurnitureReady;
    Vector3 lastFurniturePoint;
    Vector2 lastFurnitureSize;
    int lastFurnitureQuarter,lastFurnitureChoice,lastFurnitureRevision,lastFurnitureMoney;
    float lastFurnitureTime;
    readonly List<PetTownInteriorSaveRecord> unresolvedInteriors=new List<PetTownInteriorSaveRecord>();
    PetTownInteriorState FurnitureOwner=>mode==Mode.Furniture&&facilityFurnitureTarget?facilityFurnitureTarget:shop!=null?shop.GetComponent<PetTownInteriorState>():null;
    Vector2 FurnitureSize(int kind)
    {
        var cozy=PetTownCozyLibrary.Current?.Find(kind,false);if(cozy!=null)return cozy.footprint;
        var fallback=new[]{new Vector2(2,1),new Vector2(.7f,.7f),new Vector2(1.8f,.7f),new Vector2(.8f,.8f),new Vector2(1.6f,1.2f),new Vector2(.8f,.8f)};
        if(kind<0||kind>=FurnitureResource.Length)return Vector2.one;
        var prefab=Resources.Load<GameObject>(FurnitureResource[kind]);
        var renderers=prefab!=null?prefab.GetComponentsInChildren<Renderer>(true):null;
        if(renderers==null||renderers.Length==0)return fallback[kind];
        Bounds bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        if(bounds.size.y<.001f)return fallback[kind];
        float scale=FurnitureHeight[kind]/bounds.size.y;
        return new Vector2(Mathf.Max(.2f,bounds.size.x*scale),Mathf.Max(.2f,bounds.size.z*scale));
    }
    void RegisterFurniture(GameObject root,int kind,bool detail,float height=0)
    {
        var owner=furnishingOwner!=null?furnishingOwner:FurnitureOwner;if(owner==null)return;
        Vector2[] details={new Vector2(1.5f,.6f),new Vector2(1.6f,.6f),new Vector2(.9f,.7f),new Vector2(1.8f,.9f),new Vector2(.7f,.7f),new Vector2(.6f,.6f),new Vector2(.7f,.7f),new Vector2(1.1f,1.1f),new Vector2(.9f,.65f),new Vector2(1.5f,1.3f)};
        var cozy=PetTownCozyLibrary.Current?.Find(kind,detail);
        var metadata=root.AddComponent<PetTownFurniturePlacement>();metadata.Initialize(owner,kind,detail,cozy!=null?cozy.footprint:detail?details[Mathf.Clamp(kind,0,9)]:FurnitureSize(kind),height);
    }
    bool furniturePreviewBuilding;
    readonly List<Material> furniturePreviewMaterials=new List<Material>();
    static readonly float[] DetailHeights={1.9f,2.05f,1.58f,1.6f,.36f,.3f,2.2f,1.61f,.48f,1.6f};
    float SelectedFurnitureHeight=>CozySelected!=null?CozySelected.height:furnitureChoice<6?FurnitureHeight[furnitureChoice]:DetailHeights[Mathf.Clamp(furnitureChoice-6,0,9)];
    bool TryComputerSupport(Vector3 point,out float top)
    {
        top=point.y;var owner=FurnitureOwner;
        foreach(var prop in props)
        {
            var m=prop!=null?prop.GetComponent<PetTownFurniturePlacement>():null;
            if(m==null||m.Owner!=owner||(!m.Detail?(m.Kind!=0&&m.Kind!=4):(m.Kind!=12&&m.Kind!=13)))continue;
            var rr=prop.GetComponentsInChildren<Renderer>();if(rr.Length==0)continue;var b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);
            if(point.x>b.min.x+.2f&&point.x<b.max.x-.2f&&point.z>b.min.z+.18f&&point.z<b.max.z-.18f){top=b.max.y+.01f;return true;}
        }
        return false;
    }
    IEnumerator FitDetailGhost(GameObject instance)
    {
        yield return null;yield return null;
        if(instance==null||instance!=ghost)yield break;
        var rr=instance.GetComponentsInChildren<Renderer>();if(rr.Length==0)yield break;var b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);
        furnitureGhostLift=instance.transform.position.y-b.min.y;
        bool swap=Mathf.RoundToInt(instance.transform.eulerAngles.y/90f)%2!=0;
        furnitureGhostSize=swap?new Vector2(b.size.z,b.size.x):new Vector2(b.size.x,b.size.z);
        ghostRenderers.AddRange(rr);furnitureGhostReady=true;UpdateGhostTint();
    }
    void RotateFurnitureClockwise()
    {
        furnitureQuarter=(furnitureQuarter+1)%4;furnitureCached=false;
        if(ghost!=null&&FurnitureOwner!=null)ghost.transform.rotation=FurnitureOwner.transform.rotation*Quaternion.Euler(0,furnitureQuarter*90f,0);
    }
    Vector3 FurniturePlacementPoint(Vector3 point)
    {
        if(FurnitureOwner==null)return point;point=PetTownFurniturePlanner.Snap(point,FurnitureOwner,furnitureGridSize);
        if(FurnitureRequiresSurface&&TryComputerSupport(point,out float top))point.y=top;return point;
    }
    bool MouseFurnitureGround(out Vector3 point)
    {
        point=Vector3.zero;if(FurnitureOwner==null)return false;
        var ray=cam.ScreenPointToRay(Input.mousePosition);var plane=new Plane(Vector3.up,new Vector3(0,FurnitureOwner.FloorY,0));
        if(!plane.Raycast(ray,out float distance))return false;point=FurniturePlacementPoint(ray.GetPoint(distance));return true;
    }
    PetTownPlacementValidation ValidateFurniture(Vector3 point,bool force=false)
    {
        if(!force&&furnitureCached&&lastFurniturePoint==point&&lastFurnitureSize==furnitureGhostSize&&lastFurnitureQuarter==furnitureQuarter&&lastFurnitureChoice==furnitureChoice&&lastFurnitureRevision==constructionWorldRevision&&lastFurnitureMoney==money&&lastFurnitureReady==furnitureGhostReady&&Time.unscaledTime-lastFurnitureTime<.15f)return furnitureValidation;
        lastFurniturePoint=point;lastFurnitureSize=furnitureGhostSize;lastFurnitureQuarter=furnitureQuarter;lastFurnitureChoice=furnitureChoice;lastFurnitureRevision=constructionWorldRevision;lastFurnitureMoney=money;lastFurnitureReady=furnitureGhostReady;lastFurnitureTime=Time.unscaledTime;furnitureCached=true;
        return furnitureValidation=EvaluateFurniture(point);
    }
    PetTownPlacementValidation EvaluateFurniture(Vector3 point)
    {
        var owner=FurnitureOwner;
        if(owner==null)return furnitureValidation=PetTownPlacementValidation.Block("Room","펫샵 실내에 가구를 배치해 주세요.");
        if(constructionNavRefreshQueued)return furnitureValidation=PetTownPlacementValidation.Block("NavigationUpdating","이동 경로를 갱신 중입니다.");
        if(!furnitureGhostReady||ghostFurnitureFor!=furnitureChoice)return furnitureValidation=PetTownPlacementValidation.Block("Preview","가구 미리보기를 준비 중입니다.");
        if(money<50)return furnitureValidation=PetTownPlacementValidation.Block("Money","코인이 부족합니다.");
        if(FurnitureRequiresSurface&&!TryComputerSupport(point,out float supportTop))return furnitureValidation=PetTownPlacementValidation.Block("Support","이 가구는 계산대나 테이블 위에 배치해 주세요.");
        Vector2 size=PetTownFurniturePlanner.ReservedSize(furnitureGhostSize,furnitureGridSize);
        string reason=PetTownFurniturePlanner.CheckRoom(point,owner,size,furnitureQuarter);
        if(reason!=null)return furnitureValidation=PetTownPlacementValidation.Block("InteriorBounds",reason);
        var rotation=owner.transform.rotation*Quaternion.Euler(0,furnitureQuarter*90f,0);
        foreach(var collider in Physics.OverlapBox(point+Vector3.up*(SelectedFurnitureHeight*.5f+.04f),new Vector3(size.x*.5f,SelectedFurnitureHeight*.5f+.02f,size.y*.5f),rotation,~(1<<TerrainLayer),QueryTriggerInteraction.Ignore))
            if(ConstructionSolid(collider))return furnitureValidation=PetTownPlacementValidation.Block("FurnitureObstacle","벽이나 다른 가구와 겹칩니다.");
        var area=PetTownFurniturePlanner.Area(point,owner,size,furnitureQuarter);
        var padded=PetTownPlacementValidator.Expand(area,.3f);
        var anchors=new List<Vector3>{owner.transform.TransformPoint(owner.EntranceLocal)};
        foreach(var agent in FindObjectsByType<NavMeshAgent>())
        {
            var local=owner.transform.InverseTransformPoint(agent.transform.position);
            if(padded.Contains(new Vector2(local.x,local.z))&&Mathf.Abs(agent.transform.position.y-point.y)<2f)return furnitureValidation=PetTownPlacementValidation.Block("Actor","캐릭터나 동물이 있어 배치할 수 없습니다.");
            if(agent.isOnNavMesh){anchors.Add(agent.transform.position);if(agent.hasPath)anchors.Add(agent.destination);}
        }
        var player=FindAnyObjectByType<PetTownPlayerControls>();
        if(player!=null)
        {
            var local=owner.transform.InverseTransformPoint(player.transform.position);
            if(padded.Contains(new Vector2(local.x,local.z))&&Mathf.Abs(player.transform.position.y-point.y)<2f)return furnitureValidation=PetTownPlacementValidation.Block("Actor","캐릭터가 있어 배치할 수 없습니다.");
            anchors.Add(player.transform.position);
        }
        bool swap=Mathf.RoundToInt((owner.transform.eulerAngles.y+furnitureQuarter*90f)/90f)%2!=0;
        var worldArea=PetTownPlacementValidator.Footprint(point,swap?new Vector2(size.y,size.x):size);
        reason=FurnitureRequiresSurface?null:PetTownNavigationQueries.Check(point,false,worldArea,.6f,anchors);
        if(reason!=null)return furnitureValidation=PetTownPlacementValidation.Block("FurnitureNavigation",reason);
        return furnitureValidation=new PetTownPlacementValidation{CanPlace=true,Code="FurnitureAllowed",Message="배치 가능합니다."};
    }
    void PlaceInteriorFurniture(Vector3 point)
    {
        point=FurniturePlacementPoint(point);var validation=ValidateFurniture(point,true);
        if(!validation.CanPlace){Message(validation.Message);return;}
        var owner=FurnitureOwner;var previous=furnishingOwner;GameObject created=null;
        try
        {
            furnishingOwner=owner;created=furnitureChoice<6?CreateFurniture(furnitureChoice,point):CreatePetShopDetail(furnitureChoice-6,point);
            Quaternion rotation=owner.transform.rotation*Quaternion.Euler(0,furnitureQuarter*90f,0);
            foreach(var fish in tankFish)if(fish.root!=null&&fish.root.IsChildOf(created.transform)){fish.center=point+rotation*(fish.center-point);fish.orientation=rotation;}
            created.transform.SetParent(owner.transform,true);created.transform.rotation=rotation;
            var model=created.transform.Find("Model");if(model!=null)model.localRotation=Quaternion.identity;
            var metadata=created.GetComponent<PetTownFurniturePlacement>();metadata.IsDefault=false;metadata.Footprint=PetTownFurniturePlanner.ReservedSize(furnitureGhostSize,furnitureGridSize);
        }
        catch(Exception e)
        {
            if(created!=null){props.Remove(created);Destroy(created);}Debug.LogWarning("Furniture placement failed: "+e.Message);Message("가구를 배치하지 못했습니다. 코인은 차감되지 않았습니다.");return;
        }
        finally{furnishingOwner=previous;}
        money-=50;reputation++;QueueConstructionNavRefresh();SaveGame();
    }
    void AppendInteriorSnapshot(SaveData data,GameObject building,int index)
    {
        var owner=building!=null?building.GetComponent<PetTownInteriorState>():null;if(owner==null)return;
        var record=owner.Capture(index);
        foreach(var prop in props)
        {
            var metadata=prop!=null?prop.GetComponent<PetTownFurniturePlacement>():null;
            if(metadata!=null&&metadata.Owner==owner)record.furniture.Add(metadata.Capture());
        }
        record.furniture.AddRange(owner.Unresolved);data.interiors.Add(record);
    }
    void RestoreInteriors(List<PetTownInteriorSaveRecord> records,Dictionary<int,GameObject> owners)
    {
        foreach(var record in records)
        {
            if(record==null)continue;
            if(!owners.TryGetValue(record.legacyIndex,out var building)||building==null){unresolvedInteriors.Add(record);continue;}
            var owner=building.GetComponent<PetTownInteriorState>();
            if(owner==null||owner.Kind!=record.kind||!record.Valid){unresolvedInteriors.Add(record);continue;}
            owner.Initialize(record.id,record.size,record.floorOffset,record.entranceLocal,record.kind);
            owner.RestoreBounds(record);RestoreExpandedArchitecture(owner);
            for(int i=props.Count-1;i>=0;i--)
            {
                var prop=props[i];var metadata=prop!=null?prop.GetComponent<PetTownFurniturePlacement>():null;
                if(metadata==null||metadata.Owner!=owner)continue;
                tankFish.RemoveAll(f=>f.root==null||f.root.IsChildOf(prop.transform));prop.SetActive(false);props.RemoveAt(i);Destroy(prop);
            }
            if(record.furniture==null)continue;
            var previous=furnishingOwner;furnishingOwner=owner;
            try
            {
                foreach(var item in record.furniture)
                {
                    if(item==null||!item.Valid){if(item!=null)owner.Unresolved.Add(item);continue;}
                    Vector3 point=owner.transform.TransformPoint(item.localPosition);
                    var root=item.detail?CreatePetShopDetail(item.kind,point):CreateFurniture(item.kind,point,item.modelHeight);
                    Quaternion rotation=owner.transform.rotation*item.localRotation;
                    foreach(var fish in tankFish)if(fish.root!=null&&fish.root.IsChildOf(root.transform)){fish.center=point+rotation*(fish.center-point);fish.orientation=rotation;}
                    root.transform.rotation=rotation;root.transform.SetParent(owner.transform,true);
                    var model=root.transform.Find("Model");if(model!=null)model.localRotation=item.modelRotation;
                    FitCozySavedFurniture(root,item);
                    var metadata=root.GetComponent<PetTownFurniturePlacement>();metadata.Initialize(owner,item.kind,item.detail,item.footprint,item.modelHeight,item.id);metadata.IsDefault=item.isDefault;
                }
            }
            finally{furnishingOwner=previous;}
        }
    }

    [Header("Natural Environment Presentation")]
    [SerializeField] bool useNaturalEnvironment=true;
    PetTownEnvironmentStyle environmentStyle;
    IEnumerator ApplyEnvironmentAfterLoad()
    {
        yield return null;yield return null;yield return null;
        if(useNaturalEnvironment)ApplyEnvironmentStyle();
    }
    void ApplyEnvironmentStyle()
    {
        if(environmentStyle==null)environmentStyle=gameObject.AddComponent<PetTownEnvironmentStyle>();
        Renderer Surface(string name){var go=GameObject.Find(name);return go!=null?go.GetComponent<Renderer>():null;}
        environmentStyle.Apply(Surface("EDITABLE_Terrain_Mesh"),Surface("River_Smooth_Banked_Stream"),Surface("River_Terrain_Conforming_Banks"),FindObjectsByType<PetTownNatureObstacle>());
    }
    void ToggleEnvironmentStyle()
    {
        useNaturalEnvironment=!useNaturalEnvironment;if(useNaturalEnvironment)ApplyEnvironmentStyle();else if(environmentStyle!=null)environmentStyle.Restore();
    }

    [Header("Modular Interior Expansion")]
    [SerializeField] PetTownExpansionSettings expansionSettings=new PetTownExpansionSettings();
    PetTownExpansionWindow expansionWindow;
    bool expansionPending;
    readonly List<string> clearedNature=new List<string>();
    bool ExpansionOpen=>expansionWindow!=null&&expansionWindow.IsOpen;
    void OpenExpansion()
    {
        if(FurnitureOwner==null){Message("펫샵을 먼저 건설해 주세요.");return;}
        SetMode(Mode.Play);uiShopOpen=false;
        if(expansionWindow==null)expansionWindow=uiRoot.gameObject.AddComponent<PetTownExpansionWindow>();
        Vector3 previousFocus=cameraFocus,previousStart=freeLookStartCharPos;bool previousFree=cameraFreeLook;
        cameraFocus=FurnitureOwner.transform.position+Vector3.up*1.5f+(cam!=null?cam.transform.right*3f:Vector3.zero);cameraFreeLook=true;
        var controls=FindFirstObjectByType<PetTownPlayerControls>();freeLookStartCharPos=controls!=null?controls.transform.position:hero!=null?hero.transform.position:Vector3.zero;
        expansionWindow.Show(uiRoot,FurnitureOwner,expansionSettings,plan=>ValidateExpansion(FurnitureOwner,plan),plan=>{if(!expansionPending)StartCoroutine(CommitExpansion(FurnitureOwner,plan));},()=>{cameraFocus=previousFocus;cameraFreeLook=previousFree;freeLookStartCharPos=previousStart;});
    }
    PetTownExpansionCheck ValidateExpansion(PetTownInteriorState owner,PetTownExpansionPlan plan)
    {
        if(owner==null||owner.Kind!=0)return PetTownExpansionCheck.Block("현재는 펫샵 증축부터 지원합니다.");
        if(expansionPending||constructionNavRefreshQueued)return PetTownExpansionCheck.Block("건설 및 이동 영역을 갱신 중입니다.");
        if(plan.Before!=owner.LocalBounds)return PetTownExpansionCheck.Block("실내 면적이 바뀌었습니다. 방향을 다시 선택해 주세요.");
        if(plan.After.width>expansionSettings.maximumSpan||plan.After.height>expansionSettings.maximumSpan)return PetTownExpansionCheck.Block("현재 최대 증축 크기에 도달했습니다.");
        if(money<plan.Cost)return PetTownExpansionCheck.Block("골드가 부족합니다.");
        var area=PetTownExpansionPlan.WorldArea(owner,plan.Added);var halo=PetTownPlacementValidator.Expand(area,2.5f);
        if(halo.xMin<-WorldSize*.5f||halo.xMax>WorldSize*.5f||halo.yMin<-WorldSize*.5f||halo.yMax>WorldSize*.5f)return PetTownExpansionCheck.Block("마을 경계를 벗어납니다.");
        for(float x=halo.xMin;x<=halo.xMax+.01f;x+=.5f)for(float z=halo.yMin;z<=halo.yMax+.01f;z+=.5f)
        {
            if(ConstructionWater(x,z))return PetTownExpansionCheck.Block("강이나 물가에는 증축할 수 없습니다.");
            if(Mathf.Abs(RoadTerrainY(x,z)-owner.transform.position.y)>2f)return PetTownExpansionCheck.Block("지형 높이 차이가 너무 큽니다.");
        }
        if(PetTownRoadPath.Overlaps(halo))return PetTownExpansionCheck.Block("도로와 겹칩니다. 도로를 먼저 편집해 주세요.");
        RefreshConstructionObstacles(true);var own=PetTownExpansionPlan.WorldArea(owner,owner.LocalBounds);
        foreach(var obstacle in constructionObstacles)if(obstacle.Footprint!=own&&halo.Overlaps(obstacle.Footprint))return PetTownExpansionCheck.Block("다른 건물 또는 출입구 부지와 겹칩니다.");
        bool needsClearing=false;
        foreach(var natural in FindObjectsByType<PetTownNatureObstacle>())if(natural.isActiveAndEnabled&&halo.Overlaps(natural.WorldArea))needsClearing=true;
        foreach(var c in Physics.OverlapBox(new Vector3(halo.center.x,owner.transform.position.y+1.5f,halo.center.y),new Vector3(halo.width*.5f,3,halo.height*.5f),Quaternion.identity,~(1<<TerrainLayer),QueryTriggerInteraction.Ignore))
        {
            if(c.transform.IsChildOf(owner.transform))continue;
            var furniture=c.GetComponentInParent<PetTownFurniturePlacement>();if(furniture!=null&&furniture.Owner==owner)continue;
            if(c.GetComponentInParent<PetTownNatureObstacle>()!=null){needsClearing=true;continue;}
            if(ConstructionSolid(c))return PetTownExpansionCheck.Block("다른 건물 또는 장식물과 겹칩니다.");
        }
        var anchors=new List<Vector3>();
        foreach(var agent in FindObjectsByType<NavMeshAgent>())
        {
            if(PetTownPlacementValidator.Expand(area,.6f).Contains(new Vector2(agent.transform.position.x,agent.transform.position.z)))return PetTownExpansionCheck.Block("캐릭터나 펫이 부지에 있습니다. 이동 후 시도해 주세요.");
            anchors.Add(agent.transform.position);if(agent.isOnNavMesh&&agent.hasPath)anchors.Add(agent.destination);
        }
        foreach(var player in FindObjectsByType<PetTownPlayerControls>())if(PetTownPlacementValidator.Expand(area,.6f).Contains(new Vector2(player.transform.position.x,player.transform.position.z)))return PetTownExpansionCheck.Block("플레이어가 증축 부지에 있습니다.");
        var navReason=PetTownNavigationQueries.Check(Vector3.zero,false,area,.6f,anchors);
        if(navReason!=null)return PetTownExpansionCheck.Block(navReason);
        return new PetTownExpansionCheck{Status=needsClearing?PetTownExpansionStatus.NeedsClearing:PetTownExpansionStatus.Ready,Reason=needsClearing?"자연물이 있습니다. 증축과 별도로 선택하여 제거한 후 다시 시도해 주세요.":"증축 가능합니다. 표시된 비용을 확인하고 확정해 주세요."};
    }
    void OpenNatureClearing()
    {
        if(FurnitureOwner==null||expansionPending)return;
        var owner=FurnitureOwner;var candidates=new List<PetTownNatureObstacle>();var entries=new List<PetTownCatalogEntry>();
        foreach(var item in FindObjectsByType<PetTownNatureObstacle>())
        {
            bool near=false;
            foreach(PetTownExpansionDirection direction in Enum.GetValues(typeof(PetTownExpansionDirection)))
            {var plan=PetTownExpansionPlan.Create(owner,direction,expansionSettings);if(PetTownPlacementValidator.Expand(PetTownExpansionPlan.WorldArea(owner,plan.Added),2.5f).Overlaps(item.WorldArea)){near=true;break;}}
            if(!near)continue;int index=candidates.Count;candidates.Add(item);
            var local=owner.transform.InverseTransformPoint(item.transform.position);
            entries.Add(new PetTownCatalogEntry{Key=index,Name=(item.name.Contains("Tree")?"나무":item.name.Contains("Rock")?"바위":"풀·꽃")+" ("+local.x.ToString("0.0")+", "+local.z.ToString("0.0")+")",Category="증축 주변",PriceLabel="제거 후 저장",FootprintLabel="",ActionLabel="이 자연물 제거",Description="펫샵 기준 로컬 좌표입니다. 선택한 자연물만 제거합니다. 도로·건물·가구는 정리 대상이 아닙니다."});
        }
        catalogWindow.Show("증축 주변 자연물 정리",entries,entry=>
        {
            var item=candidates[entry.Key];if(item==null||!item.isActiveAndEnabled)return;
            if(!clearedNature.Contains(item.Id))clearedNature.Add(item.Id);item.gameObject.SetActive(false);QueueConstructionNavRefresh();SaveGame();Message("선택한 자연물을 제거했습니다.");
        },()=>{},()=>{});
    }
    void RegisterExpansionFloor(PetTownInteriorState owner,Rect added)
    {
        var area=PetTownExpansionPlan.WorldArea(owner,added);buildingFloors.Add(new BuildingFloor{owner=owner.gameObject,center=new Vector3(area.center.x,owner.transform.position.y,area.center.y),halfW=area.width*.5f,halfD=area.height*.5f,floorY=owner.FloorY});
    }
    void RestoreExpandedArchitecture(PetTownInteriorState owner)
    {
        if(owner.Kind!=0||owner.LocalBounds==owner.OriginalBounds)return;
        var all=PetTownExpansionPlan.WorldArea(owner,owner.LocalBounds);
        FlattenTerrain(new Vector3(all.center.x,owner.transform.position.y,all.center.y),all.width*.5f+2.5f,all.height*.5f+2.5f,owner.transform.position.y);
        PetTownExpansionShell.Build(owner,owner.LocalBounds,showRoofs,MarkNavMeshIgnore,AddShopWallObstacle);
        foreach(Transform child in owner.transform)if(PetTownExpansionShell.IsOriginalArchitecture(child))child.gameObject.SetActive(false);
        RegisterExpansionFloor(owner,owner.LocalBounds);
    }
    IEnumerator CommitExpansion(PetTownInteriorState owner,PetTownExpansionPlan plan)
    {
        var check=ValidateExpansion(owner,plan);if(!check.CanCommit){Message(check.Reason);yield break;}
        expansionPending=true;
        var terrain=GameObject.Find("EDITABLE_Terrain_Mesh");var mesh=terrain!=null?terrain.GetComponent<MeshFilter>().sharedMesh:null;
        var oldVertices=mesh!=null?mesh.vertices:null;int oldFlat=flatZones.Count,oldFloors=buildingFloors.Count;
        var oldBounds=owner.LocalBounds;var oldShell=owner.transform.Find("Expansion_Shell");var previous=new List<GameObject>();
        foreach(Transform child in owner.transform)if(PetTownExpansionShell.IsOriginalArchitecture(child)&&child.gameObject.activeSelf)previous.Add(child.gameObject);
        GameObject next=null;string error=null;
        try
        {
            next=PetTownExpansionShell.Build(owner,plan.After,showRoofs,MarkNavMeshIgnore,AddShopWallObstacle);
            foreach(var go in previous)go.SetActive(false);if(oldShell!=null)oldShell.gameObject.SetActive(false);
            var area=PetTownExpansionPlan.WorldArea(owner,plan.Added);
            FlattenTerrain(new Vector3(area.center.x,owner.transform.position.y,area.center.y),area.width*.5f+2.5f,area.height*.5f+2.5f,owner.transform.position.y);
            owner.SetBounds(plan.After);RegisterExpansionFloor(owner,plan.Added);owner.GetComponent<PetTownCozyInterior>()?.RebuildNow();
        }
        catch(Exception e){error=e.Message;}
        yield return null;yield return null;yield return null;
        if(error==null){try{RebakeNavMesh();}catch(Exception e){error=e.Message;}}
        yield return null;yield return null;
        if(error==null)
        {
            Vector3 sample=owner.transform.TransformPoint(new Vector3(plan.Added.center.x,owner.FloorOffset,plan.Added.center.y));
            if(!NavMesh.SamplePosition(sample,out var hit,1.2f,NavMesh.AllAreas))error="새 바닥의 이동 영역을 만들 수 없습니다.";
            else
            {
                Vector3 inside=owner.transform.TransformPoint(new Vector3(plan.Before.center.x,owner.FloorOffset,plan.Before.center.y));var path=new NavMeshPath();
                if(!NavMesh.CalculatePath(inside,hit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)error="기존 실내와 새 바닥이 연결되지 않습니다.";
            }
            if(money<plan.Cost)error="골드가 부족합니다.";
        }
        int balance=money;string save=PlayerPrefs.GetString("PetTown3D_Save","");bool hadSave=PlayerPrefs.HasKey("PetTown3D_Save");
        if(error==null)
        {
            try{money-=plan.Cost;expansionPending=false;SaveGame();}
            catch(Exception e){error=e.Message;money=balance;if(hadSave)PlayerPrefs.SetString("PetTown3D_Save",save);else PlayerPrefs.DeleteKey("PetTown3D_Save");PlayerPrefs.Save();}
        }
        if(error!=null)
        {
            if(next!=null){next.SetActive(false);Destroy(next);}if(oldShell!=null)oldShell.gameObject.SetActive(true);foreach(var go in previous)go.SetActive(true);
            owner.SetBounds(oldBounds);owner.GetComponent<PetTownCozyInterior>()?.RebuildNow();if(flatZones.Count>oldFlat)flatZones.RemoveRange(oldFlat,flatZones.Count-oldFlat);if(buildingFloors.Count>oldFloors)buildingFloors.RemoveRange(oldFloors,buildingFloors.Count-oldFloors);
            if(mesh!=null&&oldVertices!=null){mesh.vertices=oldVertices;mesh.RecalculateNormals();mesh.RecalculateBounds();var collider=terrain.GetComponent<MeshCollider>();if(collider!=null){collider.sharedMesh=null;collider.sharedMesh=mesh;}}
            RebakeNavMesh();Message("증축을 취소하고 복구했습니다: "+error);
        }
        else{if(oldShell!=null)Destroy(oldShell.gameObject);Message("증축 완료! +"+(plan.Added.width*plan.Added.height)+"m² / -"+plan.Cost+" 골드");}
        expansionPending=false;constructionWorldRevision++;furnitureCached=false;if(expansionWindow!=null)expansionWindow.Close();
    }

    [Header("Construction Safety")]
    [SerializeField] PetTownPlacementSafetySettings constructionSafety=new PetTownPlacementSafetySettings();
    PetTownPlacementValidator placementValidator=new PetTownPlacementValidator();
    PetTownPlacementValidation placementValidation;
    int constructionWorldRevision,cachedObstacleRevision=-1;
    readonly List<PetTownPlacementObstacle> constructionObstacles=new List<PetTownPlacementObstacle>();
    readonly List<Vector3> constructionEntrances=new List<Vector3>();
    bool constructionNavRefreshQueued;
    void QueueConstructionNavRefresh()
    {
        if(constructionNavRefreshQueued)return;
        constructionNavRefreshQueued=true;StartCoroutine(RefreshConstructionNavigation());
    }
    IEnumerator RefreshConstructionNavigation()
    {
        // Existing fitted house/furniture colliders are created after 1-2 frames.
        yield return null;yield return null;yield return null;
        RebakeNavMesh();
        yield return null;yield return null;
        constructionNavRefreshQueued=false;constructionWorldRevision++;
        if(placementValidator!=null)placementValidator.Invalidate();
    }
    void RefreshConstructionObstacles(bool force)
    {
        if(!force&&cachedObstacleRevision==constructionWorldRevision)return;
        cachedObstacleRevision=constructionWorldRevision;constructionObstacles.Clear();constructionEntrances.Clear();
        for(int sceneIndex=0;sceneIndex<UnityEngine.SceneManagement.SceneManager.sceneCount;sceneIndex++)
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIndex);
            if(!scene.isLoaded)continue;
            foreach(var root in scene.GetRootGameObjects())
            {
                string name=root.name.StartsWith("Dark_")?root.name.Substring(5):root.name;
                int kind=-1;
                for(int i=0;i<types.Length;i++)if(i!=8&&name.StartsWith(types[i].Replace(" ","_")+"_REPLACEABLE")){kind=i;break;}
                if(kind<0)continue;
                var metadata=root.GetComponent<PetTownBuildingPlacement>();
                var definition=PetTownBuildingDefinition.Default(kind);
                Vector2 size=metadata!=null?metadata.Record.WorldFootprint:definition.footprintMeters;
                Vector3 center=root.transform.position;
                Vector3 forward=metadata!=null?metadata.EntranceForward:root.transform.rotation*(Quaternion.Euler(0,definition.entranceBaseYaw,0)*Vector3.forward);
                Vector3 entrance=metadata!=null?metadata.EntranceWorld:root.transform.TransformPoint(definition.entranceLocal);
                if(metadata==null&&kind==2)
                {
                    var box=root.GetComponent<BoxCollider>();
                    if(box!=null){size=Vector2.Max(size,new Vector2(box.bounds.size.x,box.bounds.size.z));}
                    var model=root.transform.Find("Model");
                    if(model!=null){forward=model.forward;forward.y=0;forward.Normalize();}
                    float length=Mathf.Min(Mathf.Abs(forward.x)>.001f?size.x*.5f/Mathf.Abs(forward.x):float.MaxValue,Mathf.Abs(forward.z)>.001f?size.y*.5f/Mathf.Abs(forward.z):float.MaxValue);
                    entrance=center+forward*(length+.1f);entrance.y=root.transform.position.y;
                }
                else if(metadata==null)
                {
                    float angle=root.transform.eulerAngles.y*Mathf.Deg2Rad;
                    size=new Vector2(Mathf.Abs(Mathf.Cos(angle))*size.x+Mathf.Abs(Mathf.Sin(angle))*size.y,Mathf.Abs(Mathf.Sin(angle))*size.x+Mathf.Abs(Mathf.Cos(angle))*size.y);
                }
                var interior=root.GetComponent<PetTownInteriorState>();
                if(interior!=null){var world=PetTownExpansionPlan.WorldArea(interior,interior.LocalBounds);size=world.size;center=new Vector3(world.center.x,center.y,world.center.y);entrance=interior.transform.TransformPoint(interior.EntranceLocal);}
                bool hasEntrance=metadata!=null?metadata.Record.hasEntrance:definition.hasEntrance;
                constructionObstacles.Add(new PetTownPlacementObstacle{Footprint=PetTownPlacementValidator.Footprint(center,size),GroundY=root.transform.position.y,HasEntrance=hasEntrance,Entrance=entrance,Forward=forward,IsLake=kind==6});
                if(hasEntrance&&!root.name.StartsWith("Dark_"))constructionEntrances.Add(entrance+forward*constructionSafety.entranceDepth);
            }
        }
    }
    bool ConstructionWater(float x,float z)
    {
        if(IsWater(x,z))return true;
        float slope=RiverCenterSlope(z);
        if(Mathf.Abs(x-RiverCenterX(z))/Mathf.Sqrt(1+slope*slope)<5.65f)return true;
        float dx=(x-62f)/10.85f,dz=(z-50f)/12.35f;
        return dx*dx+dz*dz<=1f;
    }
    static bool ConstructionSolid(Collider collider)
    {
        if(collider==null||collider.isTrigger||collider.gameObject.layer==TerrainLayer)return false;
        if(collider.GetComponentInParent<NavMeshAgent>()!=null||collider.GetComponentInParent<PetTownPlayerControls>()!=null)return false;
        var root=collider.transform.root;
        return !root.name.StartsWith("PLACEMENT_PREVIEW")&&!root.name.StartsWith("PLAYER_Customizable");
    }
    bool ConstructionAreaClear(Rect area,float y)
    {
        var colliders=Physics.OverlapBox(new Vector3(area.center.x,y+1.5f,area.center.y),new Vector3(area.width*.5f,1.4f,area.height*.5f),Quaternion.identity,~(1<<TerrainLayer),QueryTriggerInteraction.Ignore);
        foreach(var collider in colliders)if(ConstructionSolid(collider))return false;
        return true;
    }
    bool ConstructionPassageClear(Vector3 from,Vector3 to,float radius)=>ConstructionPassageClear(from,to,radius,null);
    bool ConstructionPassageClear(Vector3 from,Vector3 to,float radius,Transform ignore)
    {
        from.y=RoadTerrainY(from.x,from.z)+.85f;to.y=RoadTerrainY(to.x,to.z)+.85f;
        foreach(var collider in Physics.OverlapCapsule(from,to,Mathf.Max(.4f,radius),~(1<<TerrainLayer),QueryTriggerInteraction.Ignore))
            if(ConstructionSolid(collider)&&(ignore==null||!collider.transform.IsChildOf(ignore)))return false;
        return true;
    }
    public PetTownRoadConnection GetRoadConnection(PetTownBuildingPlacement building)
    {
        if(building==null)return default;
        RefreshConstructionObstacles(true);
        var interior=building.GetComponent<PetTownInteriorState>();var connectionRecord=building.Record.Copy(-1);
        if(interior!=null)connectionRecord.entranceLocal=interior.EntranceLocal;
        Rect own=interior!=null?PetTownExpansionPlan.WorldArea(interior,interior.LocalBounds):PetTownPlacementValidator.Footprint(building.transform.position,building.Record.WorldFootprint);
        var obstacles=new List<Rect>();foreach(var obstacle in constructionObstacles)if(obstacle.Footprint!=own)obstacles.Add(obstacle.Footprint);
        return PetTownRoadConnections.Query(building.transform.position,connectionRecord,constructionSafety.roadGap,constructionSafety.minimumPassageWidth,RoadTerrainY,ConstructionWater,(a,b,r)=>ConstructionPassageClear(a,b,r,building.transform),obstacles,own);
    }
    PetTownPlacementSafetyContext ConstructionContext(bool force)
    {
        RefreshConstructionObstacles(force);
        var context=new PetTownPlacementSafetyContext{Height=RoadTerrainY,Water=ConstructionWater,WorldHalfSize=WorldSize*.5f,AreaClear=ConstructionAreaClear,PassageClear=ConstructionPassageClear,Navigation=PetTownNavigationQueries.Check};
        context.Buildings.AddRange(constructionObstacles);context.NavigationAnchors.AddRange(constructionEntrances);
        foreach(var agent in FindObjectsByType<NavMeshAgent>())
        {
            context.Actors.Add(agent.transform.position);
            if(agent.isOnNavMesh&&!ConstructionWater(agent.transform.position.x,agent.transform.position.z))
            {
                context.ActorAnchors.Add(agent.transform.position);
                if(agent.hasPath&&!ConstructionWater(agent.destination.x,agent.destination.z))context.ActorAnchors.Add(agent.destination);
            }
        }
        var player=FindAnyObjectByType<PetTownPlayerControls>();
        if(player!=null)
        {
            context.Actors.Add(player.transform.position);
            if(!ConstructionWater(player.transform.position.x,player.transform.position.z))context.ActorAnchors.Add(player.transform.position);
        }
        return context;
    }
    PetTownPlacementValidation ValidateBuildingPlacement(Vector3 point,bool force=false)
    {
        if(!PreparePlacementPlan(point))return PetTownPlacementValidation.Block("Selection","건물을 먼저 선택해 주세요.");
        if(constructionNavRefreshQueued)return placementValidation=PetTownPlacementValidation.Block("NavigationUpdating","이동 경로를 갱신 중입니다. 잠시 기다려 주세요.");
        int cost=mode==Mode.Start?0:costs[selected];
        if(money<cost)return placementValidation=PetTownPlacementValidation.Block("Money","코인이 부족합니다.");
        if(placementValidator==null)placementValidator=new PetTownPlacementValidator();
        if(constructionSafety==null)constructionSafety=new PetTownPlacementSafetySettings();
        placementValidation=placementValidator.Evaluate(point,placementPlanner.Snapshot(),constructionSafety,()=>ConstructionContext(force),constructionWorldRevision,PetTownRoadPath.Revision,Time.unscaledTime,force);
        return placementValidation;
    }
    void Start()
    {
        Instance=this;
        Application.targetFrameRate = 60;
        StartCoroutine(PreloadCharactersRoutine());
        // User request: textured grass re-enabled (previously reverted to flat color, now restored because flat color read as an empty void).
        grass = LoadMat("Materials/Generated/GrassLiving_MAT", new Color(.39f,.67f,.37f)); // dense grass zone
        groundDirt = LoadMat("Materials/Generated/Ground_DirtSoil_MAT", new Color(.55f,.48f,.33f));
        groundGrassSparse = LoadMat("Materials/Generated/Ground_GrassSparse_MAT", new Color(.55f,.65f,.35f));
        groundHeather = LoadMat("Materials/Generated/Ground_Heather_MAT", new Color(.6f,.5f,.62f));
        groundBlend = LoadMat("Materials/Generated/GroundBlend_MAT", new Color(.5f,.6f,.35f)); // single shader blending the 4 textures above via per-vertex weights, no hard patch edges
        road=Mat(new Color(.55f,.52f,.48f));
        water=CreateVillageWaterMaterial(); dark=Mat(new Color(.22f,.19f,.29f));
        wood=LoadMat("Materials/Adler Light Wood", new Color(.68f,.44f,.26f));
        // Int_Apt_01 등 "Interior Exterior" 카테고리 머티리얼은 트림시트(아틀라스) 텍스처라
        // 전용 모델이 아닌 일반 큐브에 입히면 검은 여백/경계가 그대로 보여서 깨져 보임.
        // WhiteDull은 텍스처 없는 단색 서피스라 큐브에 입혀도 안전함. 메탈릭만 낮춰서 페인트 느낌으로.
        wall=LoadMat("Materials/WhiteDull", new Color(.97f,.87f,.73f));
        if(wall.HasProperty("_Metallic"))wall.SetFloat("_Metallic",0.03f);
        if(wall.HasProperty("_Glossiness"))wall.SetFloat("_Glossiness",0.32f);
        wall.color=new Color(.97f,.93f,.85f);
        roofMat=Mat(new Color(.82f,.35f,.39f)); skin=Mat(new Color(1f,.79f,.65f));
        shirt=Mat(new Color(.42f,.66f,.92f)); hairMat=Mat(new Color(.35f,.23f,.16f));
        foliageLight=Mat(new Color(.60f,.79f,.42f));foliageDark=Mat(new Color(.24f,.50f,.33f));
        trim=LoadMat("Materials/Brushed Metal_Gold", new Color(.98f,.94f,.82f));
        windowMat=LoadMat("Materials/Glass_Dark", new Color(.45f,.76f,.87f));
        gold=Mat(new Color(.97f,.76f,.34f));blush=Mat(new Color(.97f,.55f,.54f));
        // Use the actual dirt-road diffuse texture in a URP-compatible material.
        // EasyRoads3D's original custom shader can render incorrectly in URP.
        Texture2D dirtTex = Resources.Load<Texture2D>("RoadTextures/PetTown_DirtRoad");
        if (dirtTex != null)
        {
            dirtRoadMat = Mat(new Color(1f,.97f,.86f,1f));
            dirtRoadMat.name = "PetTown_DirtRoad_URP";
            dirtRoadMat.mainTexture = dirtTex;
            dirtRoadMat.mainTextureScale = Vector2.one;
            // Preserve soft ragged alpha edges from the road texture.
            if (dirtRoadMat.HasProperty("_Surface")) dirtRoadMat.SetFloat("_Surface",1f);
            if (dirtRoadMat.HasProperty("_SrcBlend")) dirtRoadMat.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (dirtRoadMat.HasProperty("_DstBlend")) dirtRoadMat.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (dirtRoadMat.HasProperty("_ZWrite")) dirtRoadMat.SetFloat("_ZWrite",0f);
            dirtRoadMat.SetOverrideTag("RenderType","Transparent");
            dirtRoadMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            dirtRoadMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            dirtRoadMat.EnableKeyword("_ALPHABLEND_ON");
            // Lift the very dark original diffuse without washing out its soil detail.
            if(dirtRoadMat.HasProperty("_EmissionColor")) { dirtRoadMat.EnableKeyword("_EMISSION"); dirtRoadMat.SetColor("_EmissionColor",new Color(.16f,.125f,.075f)); }
        }
        else
        {
            Debug.LogWarning("PetTown dirt texture missing: Resources/RoadTextures/PetTown_DirtRoad.png");
            dirtRoadMat=Mat(new Color(.56f,.45f,.33f));
        }
        BuildWorld();
        SetupCamera();
        PetTownNatureObstacle.RegisterGenerated();
        LoadGame();
        StartCoroutine(ApplyEnvironmentAfterLoad());
        // One-time starting-capital grant for existing saves; retain buildings and all other progress.
        if(PlayerPrefs.GetInt("PetTown3D_StartingCapital100M_v1",0)==0)
        {
            money=100000000;
            PlayerPrefs.SetInt("PetTown3D_StartingCapital100M_v1",1);
            SaveGame();
        }
        PlaceMasterOnSafeGround();
        if (FindFirstObjectByType<PetTownPlayerControls>() != null && hero != null)
            foreach (var r in hero.GetComponentsInChildren<Renderer>()) r.enabled = false;
        if (shop == null) { mode=Mode.Start; Message("Choose a flat area and click to place your Pet Shop"); }
        else mode=Mode.Play;
        BuildUI();
        QueueConstructionNavRefresh();
    }

    // Place the controllable character on dry ground near the saved shop.
    // Runs after LoadGame so the shop location is known; does not change the shop.
    void PlaceMasterOnSafeGround()
    {
        var player = FindFirstObjectByType<PetTownPlayerControls>();
        if (player == null || shop == null) return;
        Vector3 center = shopPosition;
        Vector3 chosen = player.transform.position;
        bool found = false;
        for (int ring = 0; ring <= 12 && !found; ring++)
        {
            float radius = 12f + ring * 2f;
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f;
                float x = center.x + Mathf.Cos(a) * radius;
                float z = center.z + Mathf.Sin(a) * radius;
                if (IsWater(x,z) || PetTownWorldSafety.IsWater(x,z,1.2f)) continue;
                Vector3 candidate = new Vector3(x, Ground(x,z), z);
                // Avoid starting inside the shop or other physical obstacles.
                Collider[] nearby = Physics.OverlapCapsule(candidate + Vector3.up * .4f,
                    candidate + Vector3.up * 1.5f, .4f);
                bool blocked = false;
                foreach (var c in nearby)
                {
                    if (c.isTrigger || c.transform.IsChildOf(player.transform)) continue;
                    if (c.name == "EDITABLE_Terrain_Mesh" || c.name.Contains("Terrain")) continue;
                    blocked = true; break;
                }
                if (blocked) continue;
                chosen = candidate; found = true; break;
            }
        }
        if (found) player.transform.position = chosen;
        else Debug.LogWarning("PetTown: no verified safe spawn near the shop; existing position preserved.");
    }

    Material Mat(Color c) { var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")); m.color=c; if(furniturePreviewBuilding)furniturePreviewMaterials.Add(m);return m; }
    Material TransparentPetTankMaterial(Color color)
    {
        var m = Mat(color);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHATEST_ON");
        m.color = color;
        return m;
    }

    // 리소스 폴더(Assets/PetTown3D/Resources/Materials)에서 실제 텍스처가 입혀진 머티리얼을 불러온다.
    // 못 찾으면 예전처럼 단색으로 대체(Fallback)해서 절대 에러로 멈추지 않는다.
    Material LoadMat(string resourcePath, Color fallback)
    {
        var m = Resources.Load<Material>(resourcePath);
        return m != null ? new Material(m) : Mat(fallback);
    }
    // 큐브는 크기를 늘려도 UV가 0~1로 고정이라 텍스처가 쭉 늘어나 보인다.
    // 벽/바닥 크기(미터)에 맞춰 타일 반복 수를 다시 계산해서 늘어짐을 없앤다.
    Material Tiled(Material src, float sizeU, float sizeV, float tileMeters = 2f)
    {
        var m = new Material(src);
        Vector2 s = new Vector2(Mathf.Max(0.5f, sizeU / tileMeters), Mathf.Max(0.5f, sizeV / tileMeters));
        if (m.HasProperty("_MainTex")) m.SetTextureScale("_MainTex", s);
        if (m.HasProperty("_BumpMap")) m.SetTextureScale("_BumpMap", s);
        if (m.HasProperty("_MetallicGlossMap")) m.SetTextureScale("_MetallicGlossMap", s);
        if (m.HasProperty("_OcclusionMap")) m.SetTextureScale("_OcclusionMap", s);
        return m;
    }
    // 바닥/몰딩/지붕처럼 순전히 장식용인 조각은 콜라이더를 떼서 캐릭터가 걸리지 않게 한다.
    // (Part()가 만드는 Primitive는 기본으로 Collider가 붙는데, 그걸 그대로 두면 PetTownPlayerControls의
    // CapsuleCast 충돌 체크가 "턱"으로 인식해서 캐릭터가 가게 안으로 못 들어감.)
    GameObject Deco(PrimitiveType primitive, string name, Vector3 p, Vector3 scale, Material m, Transform parent=null)
    {
        GameObject o=Part(primitive,name,p,scale,m,parent);
        Collider col=o.GetComponent<Collider>(); if(col!=null)Destroy(col);
        return o;
    }
    // 건물을 놓은 자리 주변 지형을 건물 바닥 높이에 맞춰 평평하게 다듬는다.
    // 안 하면 울퉁불퉁한 언덕 위에 평평한 건물이 얹혀서 한쪽은 땅에 파묻히고 한쪽은 붕 뜬 것처럼 보임.
    float gradeMarginOverride=0f;
    void FlattenTerrain(Vector3 center, float halfW, float halfD, float targetY)
    {
        // Record the hard-flatten footprint so Ground() can return this exact target
        // height for any later query inside it, instead of stale raw noise (see FlatZone).
        flatZones.Add(new FlatZone{center=center,halfW=halfW,halfD=halfD,y=targetY});
        GameObject terrainObj=GameObject.Find("EDITABLE_Terrain_Mesh");
        if(terrainObj==null)return;
        MeshFilter mf=terrainObj.GetComponent<MeshFilter>();
        if(mf==null||mf.sharedMesh==null)return;
        Mesh mesh=mf.sharedMesh;
        Vector3[] verts=mesh.vertices;
        float margin=gradeMarginOverride>0f?gradeMarginOverride:2.2f; // 건물 밖으로 부드럽게 원래 지형과 섞이는 구간(미터)
        bool changed=false;
        for(int i=0;i<verts.Length;i++)
        {
            float dx=Mathf.Abs(verts[i].x-center.x), dz=Mathf.Abs(verts[i].z-center.z);
            if(dx>halfW+margin||dz>halfD+margin)continue;
            float outside=Mathf.Max(dx-halfW,dz-halfD,0f);
            float t=Mathf.Clamp01(outside/margin);
            float blend=1f-Mathf.SmoothStep(0f,1f,t); // 1=완전 평탄화(건물 발밑), 0=원래 지형 그대로
            verts[i].y=Mathf.Lerp(verts[i].y,targetY,blend);
            changed=true;
        }
        if(!changed)return;
        mesh.vertices=verts; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        if(constructionGridOverlay!=null)constructionGridOverlay.Invalidate();
        constructionWorldRevision++;
        if(placementValidator!=null)placementValidator.Invalidate();
        MeshCollider mc=terrainObj.GetComponent<MeshCollider>();
        if(mc!=null){mc.sharedMesh=null;mc.sharedMesh=mesh;}
    }
    // Unity renders an object with a null material as solid magenta (its "missing material"
    // placeholder). That can happen transiently if a script hot-reload during Play mode clears
    // a material field before Start() reruns — this safety net substitutes a plain neutral grey
    // instead of ever showing the glaring magenta/pink error color in-game.
    Material safeFallbackMat;
    Material SafeFallbackMat() { if(safeFallbackMat==null) safeFallbackMat=Mat(new Color(.6f,.6f,.6f)); return safeFallbackMat; }
    GameObject Part(PrimitiveType primitive, string name, Vector3 p, Vector3 scale, Material m, Transform parent=null)
    {
        GameObject o=GameObject.CreatePrimitive(primitive); o.name=name; o.transform.position=p;
        o.transform.localScale=scale; if(parent!=null) o.transform.SetParent(parent,true);
        o.GetComponent<Renderer>().sharedMaterial = m!=null ? m : SafeFallbackMat(); return o;
    }
    // Pet Shop remodel (user request): rebuild the shop's walls/roof with the owned
    // "3D Free Modular Kit" (Barking_Dog) asset instead of flat primitive boxes. The kit is
    // built on a 3m module grid, so the footprint is snapped up to whole 3m tiles and the
    // wall/roof modules tile edge-to-edge with no gaps. Falls back to the old flat-box shell
    // if the kit hasn't been copied into Resources/Modular/ yet.
    void BuildModularShopShell(Vector3 p, float w, float d, bool open, Transform parent, bool rival)
    {
        GameObject wallPrefab=Resources.Load<GameObject>("Modular/Wall_Simple_01");
        GameObject roofPrefab=Resources.Load<GameObject>("Modular/Roof_01");
        GameObject floorPrefab=Resources.Load<GameObject>("Modular/Floor_01");
        if(wallPrefab==null||roofPrefab==null||floorPrefab==null)
        {
            Material wallMatBDF=Tiled(wood,w,5), wallMatLRF=Tiled(wood,d,5);
            Deco(PrimitiveType.Cube,"Editable_Floor",p+Vector3.up*.2f,new Vector3(w,.4f,d),Tiled(wood,w,d),parent);
            Part(PrimitiveType.Cube,"Back_Wall",p+new Vector3(0,2.5f,d/2),new Vector3(w,5,.35f),wallMatBDF,parent);
            Part(PrimitiveType.Cube,"Left_Wall",p+new Vector3(-w/2,2.5f,0),new Vector3(.35f,5,d),wallMatLRF,parent);
            var topF=Deco(PrimitiveType.Cube,"Toggle_Roof",p+new Vector3(0,5.4f,0),new Vector3(w+1,.65f,d+1),rival?dark:RoofMatFor(0),parent);
            topF.SetActive(!open||showRoofs);
            return;
        }
        int cx=Mathf.Max(1,Mathf.CeilToInt(w/3f)), cz=Mathf.Max(1,Mathf.CeilToInt(d/3f));
        float ox=-cx*3f/2f, oz=-cz*3f/2f; // grid origin = back-left corner, relative to p
        // Floor tiles (user request: use the kit's own floor piece instead of a flat primitive).
        // Same corner convention as Roof_01: each tile's origin sits at its +X/-Z corner and the
        // mesh extends -X/+Z from there, so tile (i,j) covers [ox+i*3,ox+(i+1)*3] x [oz+j*3,oz+(j+1)*3].
        for(int i=0;i<cx;i++) for(int j=0;j<cz;j++)
        {
            // FlattenTerrain() fully flattens the ground under this footprint to the building's
            // base height, so the floor only needs a small lift to sit flush and avoid z-fighting
            // with the terrain mesh beneath it (confirmed by sampling terrain vertex heights live:
            // perfectly flat under the shop once this method has run for it).
            // Confirmed live (not a terrain-bump problem — FlattenTerrain makes the ground under
            // this footprint perfectly flat): the screenshot tool's camera still shows the terrain
            // mesh on top of the floor at anything under ~0.8m clearance (depth-buffer precision at
            // this world's long view distance). 0.75f is the smallest offset that stayed reliably
            // clear in repeated testing (0.5f and 0.6f both still showed terrain through the floor).
            var fl=Instantiate(floorPrefab,p+new Vector3(ox+(i+1)*3f,0.12f,oz+j*3f),Quaternion.identity,parent);
            fl.name="Modular_Floor_Tile";
            foreach(var c in fl.GetComponentsInChildren<Collider>()) Destroy(c);
        }
        // Back wall (+Z edge), runs along X — rotate 90° so the module's native length axis (Z) lines up with world X.
        for(int i=0;i<cx;i++)
        {
            var w1=Instantiate(wallPrefab,p+new Vector3(ox+i*3f,0,oz+cz*3f),Quaternion.Euler(0,90,0),parent);
            w1.name="Modular_Wall_Back"; MarkNavMeshIgnore(w1); AddShopWallObstacle(w1);
        }
        // Left wall (-X edge), runs along Z — native orientation.
        for(int i=0;i<cz;i++)
        {
            var w2=Instantiate(wallPrefab,p+new Vector3(ox,0,oz+i*3f),Quaternion.identity,parent);
            w2.name="Modular_Wall_Left"; MarkNavMeshIgnore(w2); AddShopWallObstacle(w2);
        }
        if(!open)
        {
            for(int i=0;i<cx;i++)
            {
                var w3=Instantiate(wallPrefab,p+new Vector3(ox+(i+1)*3f,0,oz),Quaternion.Euler(0,-90,0),parent);
                w3.name="Modular_Wall_Front"; MarkNavMeshIgnore(w3); AddShopWallObstacle(w3);
            }
            for(int i=0;i<cz;i++)
            {
                var w4=Instantiate(wallPrefab,p+new Vector3(ox+cx*3f,0,oz+(i+1)*3f),Quaternion.Euler(0,180,0),parent);
                w4.name="Modular_Wall_Right"; MarkNavMeshIgnore(w4); AddShopWallObstacle(w4);
            }
        }
        var roofRoot=new GameObject("Toggle_Roof"); roofRoot.transform.position=p; roofRoot.transform.SetParent(parent,true);
        for(int i=0;i<cx;i++) for(int j=0;j<cz;j++)
        {
            var r=Instantiate(roofPrefab,p+new Vector3(ox+(i+1)*3f,0,oz+j*3f),Quaternion.identity,roofRoot.transform);
            r.name="Modular_Roof_Tile"; MarkNavMeshIgnore(r);
        }
        roofRoot.SetActive(!open||showRoofs);
        // Low foundation: top at p.y + 0.08m, just below the modular floor at +0.12m.
        // Only the pet shop modular shell is changed; the rest of the world is untouched.
        Deco(PrimitiveType.Cube,"Foundation_Trim",p+new Vector3(0,.04f,0),new Vector3(w+.35f,.08f,d+.35f),trim,parent);
    }
    float Height(float x,float z)
    {
        float h = Mathf.PerlinNoise(x*.018f+4,z*.018f+7)*7.5f-3.0f;
        h += Mathf.PerlinNoise(x*.052f+2,z*.052f+6)*1.1f;
        return h;
    }
    // The gameplay river corridor remains the same as before.  Rendering and terrain
    // now share this exact centerline, preventing the zigzag banks caused by the old
    // binary -0.5m height jump at IsWater's boundary.
    float RiverCenterX(float z)
    {
        return Mathf.Sin(z*.031f)*12f + Mathf.Sin(z*.09f)*3f;
    }
    float RiverCenterSlope(float z)
    {
        return .372f*Mathf.Cos(z*.031f) + .27f*Mathf.Cos(z*.09f);
    }
    bool IsWater(float x,float z)
    {
        return Mathf.Abs(x-RiverCenterX(z))<3.5f || (x>52 && x<72 && z>35 && z<66);
    }
    // The bank is carved several metres OUTSIDE the visible water edge.  This is
    // intentional: terrain mesh triangles are 2m wide and otherwise cut across the
    // river's surface, producing the saw-tooth wedges in the user's screenshot.
    float RiverTerrainHeight(float x,float z)
    {
        float natural=Height(x,z);
        float dx=Mathf.Abs(x-RiverCenterX(z));
        float distance=dx/Mathf.Sqrt(1f+RiverCenterSlope(z)*RiverCenterSlope(z));
        const float flatBedHalfWidth=5.25f;
        const float bankOuterWidth=9.0f;
        float blend=Mathf.SmoothStep(0f,1f,
            Mathf.Clamp01((distance-flatBedHalfWidth)/(bankOuterWidth-flatBedHalfWidth)));
        return Mathf.Lerp(-.90f,natural,blend);
    }
    float Ground(float x,float z)
    {
        for(int i=0;i<buildingFloors.Count;i++)
        {
            var b=buildingFloors[i];
            if(Mathf.Abs(x-b.center.x)<=b.halfW && Mathf.Abs(z-b.center.z)<=b.halfD) return b.floorY;
        }
        for(int i=0;i<flatZones.Count;i++)
        {
            var fz=flatZones[i];
            if(Mathf.Abs(x-fz.center.x)<=fz.halfW && Mathf.Abs(z-fz.center.z)<=fz.halfD) return fz.y;
        }
        // Keep the existing lake footprint/level.  The stream's terrain is smoothly
        // sculpted instead of jumping between two unrelated heights.
        if(x>52 && x<72 && z>35 && z<66) return -.5f;
        return RiverTerrainHeight(x,z);
    }
    // Small, tiling procedural surface texture: restrained streaks and colour
    // variation, with no vertex displacement and no dependency on external assets.
    Material CreateVillageWaterMaterial()
    {
        Shader shader=Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null) shader=Shader.Find("Standard");
        Material m=new Material(shader);
        m.name="PetTown_Gentle_Stream_Water";
        const int size=128;
        Texture2D tex=new Texture2D(size,size,TextureFormat.RGBA32,false);
        tex.name="PetTown_Stream_Surface_Procedural";
        tex.wrapMode=TextureWrapMode.Repeat;
        tex.filterMode=FilterMode.Bilinear;
        Color[] pixels=new Color[size*size];
        for(int y=0;y<size;y++) for(int x=0;x<size;x++)
        {
            // Periodic samples ensure no seams when the texture repeats.
            float u=(float)x/size, v=(float)y/size;
            float n=Mathf.PerlinNoise(u*5f+2f,v*5f+7f);
            float ripple=Mathf.Sin(v*55f + Mathf.Sin(u*12f)*1.6f);
            float fleck=Mathf.Pow(Mathf.Clamp01((ripple+.25f)*.5f),6f)*.10f;
            float t=(n-.5f)*.14f+fleck;
            pixels[y*size+x]=new Color(.37f+t*.7f,.72f+t,.69f+t,1f);
        }
        tex.SetPixels(pixels);tex.Apply();
        m.mainTexture=tex;
        if(m.HasProperty("_BaseMap"))m.SetTexture("_BaseMap",tex);
        if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",Color.white);
        if(m.HasProperty("_Color"))m.SetColor("_Color",Color.white);
        if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.68f);
        if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",0f);
        m.enableInstancing=true;
        return m;
    }
    // Only the river scrolls: UV.y is accumulated distance along the winding stream.
    // The lake keeps the original stationary water material.
    Material CreateFlowingRiverMaterial()
    {
        Shader shader=Resources.Load<Shader>("Materials/PetTownRiverSurface");
        if(shader==null)return new Material(water);
        var material=new Material(shader);
        material.name="PetTown_Clear_Flowing_River";
        return material;
    }
    void CreateRiverBanks(Mesh terrain,Transform parent)
    {
        Shader shader=Resources.Load<Shader>("Materials/PetTownRiverSurface");
        if(shader==null)return;
        var material=new Material(shader);
        material.name="PetTown_Soil_Sand_Gravel_Banks";
        material.SetFloat("_Bank",1f);
        material.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent-30;
        if(groundBlend!=null&&groundBlend.HasProperty("_TexDirt"))material.mainTexture=groundBlend.GetTexture("_TexDirt");
        // Reuse the actual terrain mesh, including later FlattenTerrain edits.
        // A separately tessellated overlay can cut through its triangles and
        // recreate jagged banks. The shader restricts this skin to the shoreline.
        var bank=new GameObject("River_Terrain_Conforming_Banks");bank.transform.SetParent(parent,false);
        bank.AddComponent<MeshFilter>().sharedMesh=terrain;
        var renderer=bank.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        MarkNavMeshIgnore(bank);
    }
    void CreateRiverSurface(List<Vector3> points,float width,Transform parent)
    {
        if(points==null||points.Count<2)return;
        int n=points.Count;
        const int across=32;
        int stride=across+1;
        Vector3[] vertices=new Vector3[n*stride];
        Vector2[] uv=new Vector2[vertices.Length];
        Color[] depths=new Color[vertices.Length];
        int[] triangles=new int[(n-1)*across*6];
        float distance=0f;
        for(int i=0;i<n;i++)
        {
            float z=points[i].z;
            float slope=RiverCenterSlope(z);
            Vector3 side=new Vector3(1f,0f,-slope).normalized;
            if(i>0)distance+=Vector3.Distance(points[i],points[i-1]);
            // A continuous smooth offset curve, without discontinuous segment miters.
            float w=width*(1f+.012f*Mathf.Sin(z*.075f));
            for(int j=0;j<=across;j++)
            {
                float fraction=(float)j/across;
                Vector3 v=points[i]+side*((fraction-.5f)*w);
                v.y=-.18f;
                vertices[i*stride+j]=v;
                uv[i*stride+j]=new Vector2(fraction,distance/5f);
                depths[i*stride+j]=new Color(Mathf.Clamp01((v.y-RoadTerrainY(v.x,v.z))/1.2f),0f,0f,1f);
            }
            if(i==n-1)continue;
            for(int j=0;j<across;j++)
            {
                int k=(i*across+j)*6;
                int a=i*stride+j,b=a+1,c=(i+1)*stride+j,d=c+1;
                triangles[k]=a;triangles[k+1]=c;triangles[k+2]=b;
                triangles[k+3]=b;triangles[k+4]=c;triangles[k+5]=d;
            }
        }
        Mesh mesh=new Mesh();
        mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices=vertices;mesh.uv=uv;mesh.colors=depths;mesh.triangles=triangles;
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        GameObject go=new GameObject("River_Smooth_Banked_Stream");
        go.transform.SetParent(parent,false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        MeshRenderer renderer=go.AddComponent<MeshRenderer>();
        flowingRiverMaterial=CreateFlowingRiverMaterial();
        renderer.sharedMaterial=flowingRiverMaterial;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows=false;
        MarkNavMeshIgnore(go);

    }
    void BuildWorld()
    {
        WorldSize=Mathf.Clamp(WorldSize,220,880);
        int n=Mathf.CeilToInt(WorldSize/2f); float step=WorldSize/(float)n;
        Vector3[] vertices=new Vector3[(n+1)*(n+1)]; Vector2[] uv=new Vector2[vertices.Length];
        int[] triangles=new int[n*n*6];
        Color[] vcolors=new Color[vertices.Length];
        // Ground blend weights per vertex (R=dirt,G=sparse grass,B=dense grass,A=heather flowers):
        // smooth S-curve transitions between noise thresholds instead of a hard cut, so the
        // GroundBlend shader fades textures into each other across each triangle instead of
        // showing a blocky hard edge between patches.
        for(int z=0;z<=n;z++) for(int x=0;x<=n;x++)
        {
            int i=z*(n+1)+x; float px=x*step-WorldHalfExtent,pz=z*step-WorldHalfExtent;
            vertices[i]=new Vector3(px,Ground(px,pz),pz); uv[i]=new Vector2(x*.09f,z*.09f);
            float zoneNoise=Mathf.PerlinNoise((px+1500f)*.012f,(pz+1500f)*.012f);
            float flowerNoise=Mathf.PerlinNoise((px-4000f)*.05f,(pz-4000f)*.05f);
            float t1=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(0.33f-0.1f,0.33f+0.1f,zoneNoise));
            float t2=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(0.58f-0.1f,0.58f+0.1f,zoneNoise));
            float wDirt=1f-t1, wSparse=t1*(1f-t2), wDense=t1*t2;
            float wHeather=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(0.82f-0.06f,0.82f+0.06f,flowerNoise))*t1;
            float keep=1f-wHeather;
            // Expose a subtle soil/pebble-colored riverbed beside the faded water.
            // Keep the original ground-blend shader and all non-river ground unchanged.
            float bankDistance=Mathf.Abs(px-RiverCenterX(pz))/Mathf.Sqrt(1f+RiverCenterSlope(pz)*RiverCenterSlope(pz));
            float bankWeight=1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01((bankDistance-3.3f)/5.0f));
            float soil=Mathf.Clamp01(bankWeight*.75f);
            wDirt=wDirt*(1f-soil)+soil;
            wSparse*=1f-soil;wDense*=1f-soil;wHeather*=1f-soil;
            vcolors[i]=new Color(wDirt*(1f-wHeather),wSparse*(1f-wHeather),wDense*(1f-wHeather),wHeather);
        }
        int t=0; for(int z=0;z<n;z++) for(int x=0;x<n;x++)
        {
            int i=z*(n+1)+x;
            triangles[t++]=i;triangles[t++]=i+n+1;triangles[t++]=i+1;
            triangles[t++]=i+1;triangles[t++]=i+n+1;triangles[t++]=i+n+2;
        }
        Mesh mesh=new Mesh(); mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices=vertices;mesh.uv=uv;mesh.colors=vcolors;mesh.triangles=triangles;
        mesh.RecalculateNormals();
        GameObject terrain=new GameObject("EDITABLE_Terrain_Mesh");
        terrain.AddComponent<MeshFilter>().sharedMesh=mesh;
        terrain.AddComponent<MeshRenderer>().sharedMaterial=groundBlend;
        terrain.AddComponent<MeshCollider>().sharedMesh=mesh;
        terrain.layer=TerrainLayer; // villagers' wall-avoidance raycast ignores this layer
        // A single smooth river surface replaces the overlapping bright-blue cubes.
        // Keep the original IsWater/Ground geometry so NPCs, buildings and saves behave as before.
        var riverParent=new GameObject("River_And_Streams");
        var riverPoints=new List<Vector3>();
        for(float z=-WorldHalfExtent+1;z<=WorldHalfExtent-1;z+=0.5f)
        {
            float x=RiverCenterX(z);
            riverPoints.Add(new Vector3(x,-.18f,z));
        }
        CreateRiverSurface(riverPoints,10.4f,riverParent.transform);
        CreateRiverBanks(mesh,riverParent.transform);
        var lake=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lake.name="Lake_Water";lake.transform.position=new Vector3(62,-.30f,50);
        lake.transform.localScale=new Vector3(21,.12f,24);
        lake.GetComponent<Renderer>().sharedMaterial=water;
        Destroy(lake.GetComponent<Collider>());
        // Trees and grass clusters: a handful of denser "groves" (natural-looking clumps of
        // trees instead of every tree being evenly spaced) plus a wider, sparser scatter of
        // individual trees and grass across the rest of the map.
        var random=new System.Random(41);
        bool FoliageClear(float fx,float fz) => !IsWater(fx,fz) && !(fx>58 && fz>55) && !(Mathf.Abs(fx)<15 && Mathf.Abs(fz)<15);
        var groveCenters=new List<Vector3>();
        for(int g=0;g<7;g++)
        {
            float gx=(float)random.NextDouble()*176-88, gz=(float)random.NextDouble()*176-88;
            if(FoliageClear(gx,gz)) groveCenters.Add(new Vector3(gx,0,gz));
        }
        foreach(var gc in groveCenters)
        {
            int clusterCount=random.Next(4,8);
            for(int k=0;k<clusterCount;k++)
            {
                float x=gc.x+(float)(random.NextDouble()*9-4.5), z=gc.z+(float)(random.NextDouble()*9-4.5);
                if(FoliageClear(x,z)) MakeTree(new Vector3(x,Ground(x,z),z));
            }
        }
        for(int i=0;i<170;i++)
        {
            float x=(float)random.NextDouble()*204-102,z=(float)random.NextDouble()*204-102;
            if(!FoliageClear(x,z)) continue;
            if(i%6==0) MakeTree(new Vector3(x,Ground(x,z),z));
            else if(i%19==0)
            {
                MakeRock(new Vector3(x,Ground(x,z),z));
            }
            else
            {
                // Real grass/flower prefabs (Ultimate Nature – Starter, already URP) instead of
                // tinted capsule primitives — reads as an actual clump of grass blades, not a pill.
                float scale=UnityEngine.Random.Range(.35f,.75f);
                string path=(i%4==3)?"Vegetation/UNS_Flower":"Vegetation/UNS_Grass";
                var prefab=Resources.Load<GameObject>(path);
                if(prefab!=null)
                {
                    var inst=Instantiate(prefab,new Vector3(x,Ground(x,z),z),Quaternion.Euler(0,UnityEngine.Random.Range(0f,360f),0));
                    inst.name="Grass_Clump"; vegetationProps.Add(inst); MarkNavMeshIgnore(inst); // these meshes aren't read/write-enabled, which spams NavMesh-bake read-access errors otherwise
                    inst.transform.localScale=Vector3.one*scale;
                    foreach(var c in inst.GetComponentsInChildren<Collider>()) Destroy(c);
                }
            }
        }
        // Extra grass patches using the same real prefabs at larger scale, since UNS_Grass
        // is already a multi-blade clump mesh — this reads as denser grass cover growing out
        // of the dirt-blend ground without needing hundreds of tiny clustered tufts.
        int grassPatchCount=140;
        for(int pIdx=0; pIdx<grassPatchCount; pIdx++)
        {
            float pcx=(float)random.NextDouble()*204-102, pcz=(float)random.NextDouble()*204-102;
            if(!FoliageClear(pcx,pcz)) continue;
            float scale=UnityEngine.Random.Range(.55f,1.25f);
            string path=(pIdx%11==0)?"Vegetation/UNS_Mushroom_Patch":(pIdx%5==0)?"Vegetation/UNS_Flower":"Vegetation/UNS_Grass";
            var prefab=Resources.Load<GameObject>(path);
            if(prefab==null) continue;
            var inst=Instantiate(prefab,new Vector3(pcx,Ground(pcx,pcz),pcz),Quaternion.Euler(0,UnityEngine.Random.Range(0f,360f),0));
            inst.name="Grass_Patch"; vegetationProps.Add(inst); MarkNavMeshIgnore(inst);
            inst.transform.localScale=Vector3.one*scale;
            foreach(var c in inst.GetComponentsInChildren<Collider>()) Destroy(c);
        }
        // The far-side rival village is present from the beginning.
        Vector3 rivalHouseA=new Vector3(76,Ground(76,-76),-76);
        Vector3 rivalHouseB=new Vector3(88,Ground(88,-70),-70);
        Vector3 rivalShop=new Vector3(80,Ground(80,-84),-84);
        CreateBuilding(2,rivalHouseA,true);
        CreateBuilding(2,rivalHouseB,true);
        CreateBuilding(0,rivalShop,true);
        BuildPathTo(rivalHouseA,rivalShop);
        BuildPathTo(rivalHouseB,rivalShop);
        CreateExpandedOutskirts();
        new GameObject("PetTown_DistanceFog").AddComponent<PetTownDistanceFog>().Initialize(this);
        var sun=new GameObject("Sun"); var light=sun.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;
        sun.transform.rotation=Quaternion.Euler(45,-35,0);
        RenderSettings.ambientLight=new Color(.72f,.75f,.79f);
        RebakeNavMesh();
    }
    static readonly string[] TreeResource={"Trees/TreePine","Trees/TreeFruit"};
    void MakeTree(Vector3 p)
    {
        var root=new GameObject("Replaceable_Tree");root.transform.position=p;roadTrees.Add(root);
        GameObject prefab=Resources.Load<GameObject>(TreeResource[UnityEngine.Random.Range(0,TreeResource.Length)]);
        if(prefab!=null)
        {
            GameObject instance=Instantiate(prefab,root.transform);
            instance.name="Model";
            instance.transform.localPosition=Vector3.zero;
            instance.transform.localRotation=Quaternion.Euler(0,UnityEngine.Random.Range(0f,360f),0);
            MarkNavMeshIgnore(root);
            var renderers=instance.GetComponentsInChildren<Renderer>();
            if(renderers.Length>0)
            {
                Bounds b=renderers[0].bounds;
                for(int i=1;i<renderers.Length;i++)b.Encapsulate(renderers[i].bounds);
                float targetHeight=UnityEngine.Random.Range(3.6f,5.2f);
                float scale=Mathf.Clamp(targetHeight/Mathf.Max(.001f,b.size.y),.01f,50f);
                instance.transform.localScale=Vector3.one*scale;
                renderers=instance.GetComponentsInChildren<Renderer>();
                b=renderers[0].bounds;
                for(int i=1;i<renderers.Length;i++)b.Encapsulate(renderers[i].bounds);
                instance.transform.position+=Vector3.up*(p.y-b.min.y);
            }
            return;
        }
        // 에셋을 못 찾으면 예전 도형 나무로 안전하게 대체.
        Part(PrimitiveType.Cylinder,"Trunk",p+Vector3.up*1.15f,new Vector3(.5f,1.15f,.5f),wood,root.transform);
        Part(PrimitiveType.Sphere,"Crown_Lower",p+Vector3.up*2.45f,new Vector3(2.65f,2.1f,2.6f),foliageDark,root.transform);
        Part(PrimitiveType.Sphere,"Crown_Top",p+Vector3.up*3.2f,new Vector3(2.05f,2.0f,2.05f),foliageLight,root.transform);
    }
    // Real rock models (Low Poly Medieval Starter Pack) instead of a plain dark sphere — much
    // more recognizable as "a rock" at a glance. Falls back to the old primitive if the asset
    // is ever missing. Uses a solid BoxCollider sized after the fit so it still blocks walking
    // through it, without needing the imported mesh to be read/write-enabled for a MeshCollider.
    void MakeRock(Vector3 p)
    {
        var root=new GameObject("Replaceable_Rock");root.transform.position=p;
        GameObject prefab=Resources.Load<GameObject>(RockResources[UnityEngine.Random.Range(0,RockResources.Length)]);
        if(prefab!=null)
        {
            GameObject instance=Instantiate(prefab,root.transform);
            instance.name="Model";
            instance.transform.localPosition=Vector3.zero;
            instance.transform.localRotation=Quaternion.Euler(0,UnityEngine.Random.Range(0f,360f),0);
            foreach(var c in instance.GetComponentsInChildren<Collider>()) Destroy(c);
            MarkNavMeshIgnore(root);
            float targetHeight=UnityEngine.Random.Range(.55f,1.1f);
            StartCoroutine(FitPropWithColliderNextFrame(instance,root,p.y,targetHeight,.95f));
            return;
        }
        float rs=UnityEngine.Random.Range(.35f,.7f);
        Part(PrimitiveType.Sphere,"Rock",p+Vector3.up*rs*.3f,new Vector3(rs,rs*.68f,rs*.85f),dark,root.transform);
    }
    // Shared fit-and-solidify coroutine for any prefab instantiated at runtime that needs both
    // the one-frame-delayed bounds fix (see FitModelNextFrame above) AND a real collider to
    // block walking through it — used for rocks, medieval houses and the fence/gate kit. A
    // plain BoxCollider sized from the measured bounds is used instead of a MeshCollider so it
    // works even on imported meshes that aren't marked Read/Write-enabled.
    IEnumerator FitPropWithColliderNextFrame(GameObject instance, GameObject root, float groundY, float targetHeight, float colliderInset)
    {
        yield return null;
        if(instance==null||root==null) yield break;
        var renderers=instance.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0) yield break;
        Bounds b=renderers[0].bounds;
        for(int r=1;r<renderers.Length;r++) b.Encapsulate(renderers[r].bounds);
        if(b.size.y<=0.0001f) yield break;
        float scale=Mathf.Clamp(targetHeight/b.size.y,.001f,2000f);
        instance.transform.localScale=Vector3.one*scale;
        renderers=instance.GetComponentsInChildren<Renderer>();
        b=renderers[0].bounds;
        for(int r=1;r<renderers.Length;r++) b.Encapsulate(renderers[r].bounds);
        instance.transform.position+=Vector3.up*(groundY-b.min.y);
        var box=root.AddComponent<BoxCollider>();
        Vector3 localSize=root.transform.InverseTransformVector(b.size);
        box.size=new Vector3(Mathf.Abs(localSize.x)*colliderInset,Mathf.Abs(localSize.y),Mathf.Abs(localSize.z)*colliderInset);
        box.center=root.transform.InverseTransformPoint(b.center);
        var placed=root.GetComponent<PetTownBuildingPlacement>();
        if(placed!=null&&placed.Record.kind==2)AddWalkObstacle(root,b,1f-colliderInset);
    }
    void SetupCamera()
    {
        var c=new GameObject("PetTown_Camera"); c.tag="MainCamera"; cam=c.AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=zoom;
        cam.transform.rotation=Quaternion.Euler(cameraPitch,cameraYaw,0);
        cam.transform.position=cameraFocus-cam.transform.forward*75f;
        cam.backgroundColor=new Color(.66f,.83f,.94f);
        cam.clearFlags=CameraClearFlags.SolidColor; // URP 전환 후 Skybox/Procedural 셰이더 비호환으로 마젠타(핑크) 렌더링되는 문제 방지 - 항상 솔리드 하늘색 배경 사용
        SetupPostProcessing(c);
    }
    // URP post-processing: Bloom/ColorAdjustments/Tonemapping/Vignette profile built in the editor
    // (Assets/PetTown3D/Resources/PetTown3D_PostFX.asset) and wired up here at runtime since the
    // whole scene (camera included) is procedurally created, nothing is hand-authored in the scene.
    void SetupPostProcessing(GameObject cameraGO)
    {
        var camData = cameraGO.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (camData == null) camData = cameraGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.volumeLayerMask = ~0; // Everything, so the global volume below is always picked up

        var profile = Resources.Load<UnityEngine.Rendering.VolumeProfile>("PetTown3D_PostFX");
        if (profile == null) { Debug.LogWarning("PetTown3D_PostFX volume profile not found in Resources; post-processing skipped."); return; }

        var volGO = new GameObject("PetTown_PostFX_Volume");
        var volume = volGO.AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;
        volume.weight = 1f;
        volume.priority = 0f;
        volume.sharedProfile = profile;
    }
    void Update()
    {
        if(cam==null)return;
        float dt=Time.deltaTime; worldClock+=dt;
        UpdateProgression(dt);
        // UV.y follows the river centerline, so the pattern drifts around bends.
        // No vertex displacement, no movement of the lake or other materials.
        // River shader animates accumulated centerline distance with _Time.
        if(Input.GetKeyDown(KeyCode.Escape)) { mode=Mode.Play; ClearGhost(); }
        // The master character's controller owns movement when it is installed.
        bool masterControlsActive = FindFirstObjectByType<PetTownPlayerControls>() != null;
        float h=masterControlsActive ? 0f : Input.GetAxisRaw("Horizontal");
        float v=masterControlsActive ? 0f : Input.GetAxisRaw("Vertical");
        Vector3 forward=Vector3.ProjectOnPlane(cam.transform.forward,Vector3.up).normalized;
        Vector3 right=Vector3.ProjectOnPlane(cam.transform.right,Vector3.up).normalized;
        if(hero!=null && mode==Mode.Play && (Mathf.Abs(h)+Mathf.Abs(v)>.1f))
        {
            cameraFreeLook=false;
            Vector3 move=(forward*v+right*h).normalized*dt*8;
            Vector3 next=hero.transform.position+move;
            if(!IsWater(next.x,next.z))
            {
                next.y=Ground(next.x,next.z);hero.transform.position=next;
                // The placeholder face and shoes are authored toward local -Z, not +Z.
                hero.transform.rotation=Quaternion.Slerp(hero.transform.rotation,Quaternion.LookRotation(usingMeshyPlayer ? move : -move),dt*10);
            }
        }
        // glTFast GLB imports may use Unity Legacy Animation, not Animator.
        // Control BOTH systems so an imported autoplay Running clip cannot keep playing.
        bool shouldRun = mode==Mode.Play && (Mathf.Abs(h)+Mathf.Abs(v)>.1f);
        if (meshyLegacyAnimations != null && meshyLegacyAnimations.Length>0)
        {
            foreach (var legacy in meshyLegacyAnimations)
            {
                if (legacy==null) continue;
                if (shouldRun)
                {
                    if (!legacy.isPlaying)
                    {
                        string clipName = null;
                        foreach (AnimationState state in legacy)
                        {
                            if (state.name.ToLowerInvariant().Contains("running")) { clipName=state.name; break; }
                            if (clipName==null && state.name.ToLowerInvariant().Contains("walking")) clipName=state.name;
                            if (clipName==null) clipName=state.name;
                        }
                        if (!string.IsNullOrEmpty(clipName))
                        {
                            legacy.wrapMode=WrapMode.Loop;
                            legacy.Play(clipName);
                        }
                    }
                }
                else if (legacy.isPlaying) legacy.Stop();
            }
        }
        if (meshyAnimator!=null)
        {
            // The Legacy Animation component owns imported GLB clips when present.
            // Disable Animator in that case to prevent competing bone transforms.
            if (meshyLegacyPresent)
            {
                if (meshyAnimator.enabled) meshyAnimator.enabled=false;
            }
            else
            {
                meshyAnimator.enabled=true;
                if (meshyAnimator.runtimeAnimatorController!=null)
                {
                    if (shouldRun && !meshyWasMoving)
                    {
                        meshyAnimator.speed=1f;
                        meshyAnimator.Play("Running",0,0f);
                    }
                    else if (!shouldRun && meshyWasMoving)
                    {
                        meshyAnimator.speed=0f;
                    }
                }
                else meshyAnimator.speed=0f;
            }
        }
        meshyWasMoving=shouldRun;
        if(heroLeftArm!=null && !usingMeshyPlayer)
        {
            bool walking=hero!=null && mode==Mode.Play && (Mathf.Abs(h)+Mathf.Abs(v)>.1f);
            walkPhase+=dt*(walking?11f:3f);
            float swing=walking?Mathf.Sin(walkPhase)*32f:Mathf.Sin(walkPhase)*3f;
            heroLeftArm.localRotation=Quaternion.Euler(swing,0,0);
            heroRightArm.localRotation=Quaternion.Euler(-swing,0,0);
            heroLeftLeg.localRotation=Quaternion.Euler(-swing*.65f,0,0);
            heroRightLeg.localRotation=Quaternion.Euler(swing*.65f,0,0);
        }
        // Right mouse drag rotates the quarter-view camera around its focus.
        // Q/E rotate in 45-degree increments; R restores the original quarter view.
        if(!CameraInputBlocked && Input.GetMouseButton(1))
        {
            cameraYaw += Input.GetAxis("Mouse X")*3f;
            cameraPitch = Mathf.Clamp(cameraPitch-Input.GetAxis("Mouse Y")*2f,0f,75f);
        }
        if(!CameraInputBlocked && Input.GetKeyDown(KeyCode.Q))cameraYaw-=45f;
        if(!CameraInputBlocked && Input.GetKeyDown(KeyCode.E))cameraYaw+=45f;
        if(!CameraInputBlocked && Input.GetKeyDown(KeyCode.R))
        {
            if(UsesBuildingGrid)
            {
                Vector3 point=ghost!=null?ghost.transform.position:Vector3.zero;
                if(PreparePlacementPlan(point))placementPlanner.RotateClockwise();
            }
            else if(mode==Mode.Furniture)RotateFurnitureClockwise();
            else{cameraYaw=42f;cameraPitch=48f;}
        }
        // Middle-mouse drag freely pans the camera around the whole map, independent of the
        // character. Pan speed scales with the current zoom level so it still covers real
        // ground when zoomed way out instead of crawling at a fixed screen-space rate. Once
        // released, the camera stays put (doesn't snap back to the character) until the player
        // actually moves again — otherwise letting go immediately undid the whole point of
        // panning away to look at the rest of the map.
        if(!CameraInputBlocked && Input.GetMouseButton(2))
        {
            float panSpeed = Mathf.Max(zoom,4f)*.045f;
            Vector3 panDelta=(-right*Input.GetAxis("Mouse X")-forward*Input.GetAxis("Mouse Y"))*panSpeed;
            if(panDelta.sqrMagnitude>0f)
            {
                cameraFocus+=panDelta;
                if(!cameraFreeLook)
                {
                    var ft=FindFirstObjectByType<PetTownPlayerControls>();
                    freeLookStartCharPos = ft!=null ? ft.transform.position : (hero!=null?hero.transform.position:cameraFocus);
                }
                cameraFreeLook=true;
            }
        }
        // Close-up zoom for character face. Keep one camera owner to avoid jitter.
        float zoomDelta = CameraInputBlocked ? 0f : Input.mouseScrollDelta.y;
        if (!CameraInputBlocked && Input.touchCount == 2)
        {
            Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
            float previous = Vector2.Distance(a.position - a.deltaPosition, b.position - b.deltaPosition);
            float current = Vector2.Distance(a.position, b.position);
            zoomDelta += (current - previous) * .015f;
        }
        // Proportional zoom provides fine control near the character's face.
        zoom = Mathf.Clamp(zoom * Mathf.Pow(0.84f, zoomDelta), 0.6f, 75f);
        cam.orthographicSize = zoom;
        // Follow the controllable master instead of the hidden legacy hero.
        // Only PetTownGame writes the camera transform (prevents camera jitter).
        var masterControls = FindFirstObjectByType<PetTownPlayerControls>();
        Transform followTarget = masterControls != null && masterControls.followCamera
            ? masterControls.transform : (hero != null ? hero.transform : null);
        if(cameraFreeLook && followTarget!=null && Vector3.Distance(followTarget.position,freeLookStartCharPos)>0.3f)
            cameraFreeLook=false; // the character actually moved — resume following it
        if(followTarget!=null && mode==Mode.Play && !Input.GetMouseButton(2) && !cameraFreeLook)
            cameraFocus=Vector3.Lerp(cameraFocus,followTarget.position+Vector3.up*1.5f,dt*5f);
        cam.transform.rotation=Quaternion.Euler(cameraPitch,cameraYaw,0);
        cam.transform.position=cameraFocus-cam.transform.forward*75f;
        UpdateConstructionGrid();
        if((mode==Mode.Start||mode==Mode.Build||mode==Mode.Furniture)&&!CatalogOpen)
        {
            if((mode==Mode.Furniture&&ghostFurnitureFor!=furnitureChoice)||(mode==Mode.Build&&selected>=0&&selected!=8&&ghostBuildFor!=selected)||(ghost==null&&mode==Mode.Start))
                RebuildGhost();
            if(!UsesBuildingGrid&&mode!=Mode.Furniture)UpdateGhostTint();
        }
        // Road dragging must continue even when the cursor passes over UI or another collider.
        // Only the initial press is blocked by UI; mouse-up always completes the stroke.
        if(mode==Mode.Build && selected==8)
        {
            if(ghost!=null) ghost.SetActive(false);
            Vector3 roadPoint;
            bool hasRoadPoint=MouseRoadGround(out roadPoint);
            if(!roadDragging && !IsPointerOverUI() && hasRoadPoint)
                UpdateRoadHoverPreview(roadPoint);
            else if(!roadDragging && roadDragPreview!=null) StopRoadPreview();
            if(Input.GetMouseButtonDown(0) && !IsPointerOverUI() && hasRoadPoint && RoadPointAllowed(roadPoint))
            {
                roadDragging=true;roadStrokeMode=roadMode;
                if(roadStrokeMode==PetTownRoadMode.GridStraight)roadPoint=PetTownBuildGrid.SnapHorizontal(roadPoint,constructionGridSize,constructionGridOrigin);
                roadDragPoints=new List<Vector3>{roadPoint};
                StartRoadPreview();
            }
            if(roadDragging && Input.GetMouseButton(0) && hasRoadPoint)
            {
                if(Vector2.Distance(new Vector2(roadDragPoints[roadDragPoints.Count-1].x,roadDragPoints[roadDragPoints.Count-1].z),new Vector2(roadPoint.x,roadPoint.z))>0.45f)
                    roadDragPoints.Add(roadPoint);
                UpdateRoadPreview(hasRoadPoint?roadPoint:roadDragPoints[roadDragPoints.Count-1]);
            }
            if(roadDragging && Input.GetMouseButtonUp(0))
            {
                if(hasRoadPoint && Vector3.Distance(roadDragPoints[roadDragPoints.Count-1],roadPoint)>0.15f)
                    roadDragPoints.Add(roadPoint);
                roadDragging=false;
                StopRoadPreview();
                PlaceRoadPath(roadDragPoints,roadStrokeMode);
                roadDragPoints=null;
            }
        }
        else
        {
            if(roadDragging){roadDragging=false;roadDragPoints=null;StopRoadPreview();}
            if((mode==Mode.Start||(mode==Mode.Build&&selected>=0)||mode==Mode.Furniture) && !IsPointerOverUI())
            {
                Vector3 p;
                bool hasPoint=mode==Mode.Furniture?MouseFurnitureGround(out p):UsesBuildingGrid?MouseRoadGround(out p):MouseGround(out p);
                if(hasPoint)
                {
                    if(UsesBuildingGrid){p=BuildingPlacementPoint(p);UpdatePlacementDirection(p);}
                    if(mode==Mode.Furniture)
                    {
                        p=FurniturePlacementPoint(p);
                        if(ghost!=null){ghost.SetActive(true);ghost.transform.position=p+Vector3.up*furnitureGhostLift;ghost.transform.rotation=FurnitureOwner.transform.rotation*Quaternion.Euler(0,furnitureQuarter*90f,0);}
                    }
                    else if(ghost!=null) { ghost.SetActive(true); ghost.transform.position=UsesBuildingGrid?p:new Vector3(p.x,Ground(p.x,p.z),p.z); }
                    if(UsesBuildingGrid||mode==Mode.Furniture)UpdateGhostTint();
                    if(Input.GetMouseButtonDown(0)) Place(p);
                }
            }
        }
        if(shop!=null)
        {
            UpdatePet(dt);
            if(AdvanceWorldTime(dt)) {day++;BeginWorldDay();money-=Mathf.Min(Mathf.Max(0,money),Mathf.Min(80,30+population*2));Message("새로운 하루가 시작됐어요. 매장 유지비를 정산했어요.");SaveGame();}
            orderTimer+=dt;
            if(orderPet<0 && orderTimer>8f) StartOrder();
            if(orderPet>=0 && Time.time>orderDeadline) FailOrder();
            customerClock+=dt;
            // Customers must be served manually. Automatic income is disabled.
            foreach(var person in villagers) if(person!=null)
            {
                var agent=person.GetComponent<NavMeshAgent>();
                if(UpdateVillagerSit(person,agent,dt))continue;
                if(UpdateFacilityVisit(person,agent,dt))continue;
                if(UpdateVillagerChat(person,agent,dt))continue;
                chatFree.Add(person);
                sitFree.Add(person);
                VillagerWander w;
                if(!villagerWander.TryGetValue(person,out w))
                {
                    w=new VillagerWander{goal=VillagerGoal.Stroll,target=PickWanderTarget(person.transform.position),
                        nextPick=Time.time+UnityEngine.Random.Range(4f,9f), running=false};
                    villagerWander[person]=w;
                    if(agent!=null && agent.isOnNavMesh) SetSafeVillagerDestination(agent,w);
                }
                bool arrived = w.goal!=VillagerGoal.AtShop && agent!=null && agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance<1.2f;
                bool timedOut = Time.time > w.nextPick &&
                    (agent==null || !agent.isOnNavMesh || !agent.hasPath ||
                     (!agent.pathPending && agent.remainingDistance<1.2f));
                if(arrived || timedOut)
                {
                    PickNextVillagerGoal(person,w);
                    if(agent!=null && agent.isOnNavMesh)
                    {
                        if(w.goal==VillagerGoal.AtShop) agent.ResetPath();
                        else SetSafeVillagerDestination(agent,w);
                    }
                }
                bool moving=false;
                if(agent!=null && agent.isOnNavMesh)
                {
                    agent.speed = w.running ? 3.0f : 1.3f;
                    float measuredSpeed=w.hasPreviousPosition && dt>.0001f
                        ? Vector2.Distance(new Vector2(person.transform.position.x,person.transform.position.z),
                            new Vector2(w.previousPosition.x,w.previousPosition.z))/dt : 0f;
                    moving = measuredSpeed>.12f;
                    if(!agent.pathPending && agent.hasPath && agent.remainingDistance>1.3f && measuredSpeed<.045f)
                    {
                        if(w.stuckSince<0f)w.stuckSince=Time.time;
                        if(Time.time-w.stuckSince>2.5f)
                        {
                            agent.ResetPath();
                            w.goal=VillagerGoal.Stroll;
                            w.nextPick=0f;
                            w.stuckSince=-1f;
                        }
                    }
                    else w.stuckSince=-1f;
                    w.previousPosition=person.transform.position;
                    w.hasPreviousPosition=true;
                }
                UpdateVillagerAnim(person,moving,moving && w.running);
            }
            TryStartVillagerChats();
            PerfSweep();
            TryStartVillagerSits();
        }
        if(Input.GetKeyDown(KeyCode.F5)) SaveGame();
        if(!CameraInputBlocked && Input.GetKeyDown(KeyCode.E) && orderPet>=0) ServeOrder();
        RefreshUI();
    }
    bool IsPointerOverUI() { return CatalogOpen || ExpansionOpen || PetTownCustomizationUI.IsScreenPointOverUI(Input.mousePosition) || (EventSystem.current!=null && EventSystem.current.IsPointerOverGameObject()); }
    bool MouseGround(out Vector3 p)
    {
        Ray ray=cam.ScreenPointToRay(Input.mousePosition); RaycastHit hit;
        if(Physics.Raycast(ray,out hit,500f))
        {
            p=hit.point; return true;
        }
        Plane plane=new Plane(Vector3.up,Vector3.zero);float distance;
        if(plane.Raycast(ray,out distance)) { p=ray.GetPoint(distance);return true; }
        p=Vector3.zero;return false;
    }
    // Prefer the actual terrain collider; shop floors, NPCs and furniture must not steal
    // the mouse ray while drawing a road. The terrain mesh is on TerrainLayer.
    bool MouseRoadGround(out Vector3 p)
    {
        Ray ray=cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if(Physics.Raycast(ray,out hit,1000f,1<<TerrainLayer,QueryTriggerInteraction.Ignore))
        {
            p=hit.point;
            p.y=Ground(p.x,p.z);
            return true;
        }
        Plane plane=new Plane(Vector3.up,Vector3.zero);
        float distance;
        if(plane.Raycast(ray,out distance))
        {
            p=ray.GetPoint(distance);
            p.y=Ground(p.x,p.z);
            return true;
        }
        p=Vector3.zero;return false;
    }
    // Sample the REAL, deformed terrain mesh at each road vertex, not procedural
    // Ground() (which may differ from the flattened/interpolated mesh on slopes).
    float RoadTerrainY(float x, float z)
    {
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(x, 500f, z), Vector3.down, out hit,
            1000f, 1 << TerrainLayer, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return Ground(x, z);
    }
    Vector3 RoadSurfacePoint(Vector3 p, float lift = 0.085f)
    {
        return new Vector3(p.x, RoadTerrainY(p.x, p.z) + lift, p.z);
    }
    void UpdateRoadHoverPreview(Vector3 point)
    {
        if(roadMode==PetTownRoadMode.GridStraight)point=PetTownBuildGrid.SnapHorizontal(point,constructionGridSize,constructionGridOrigin);
        Vector3 a = point + new Vector3(-0.65f, 0f, 0f);
        Vector3 b = point + new Vector3(0.65f, 0f, 0f);
        UpdateRoadPreviewMesh(new List<Vector3>{a,b}, RoadPointAllowed(point));
    }
    List<BuildingFloor> RoadBuildingFootprints()
    {
        var result=new List<BuildingFloor>(buildingFloors);
        // Imported resident houses do not register a procedural floor. Read their
        // existing collision bounds for road clipping without changing the prefab.
        foreach(var building in buildings)
        {
            if(building==null||!building.name.StartsWith("Resident_House"))continue;
            var colliders=building.GetComponentsInChildren<Collider>();
            if(colliders.Length==0)continue;
            Bounds bounds=colliders[0].bounds;
            for(int i=1;i<colliders.Length;i++)bounds.Encapsulate(colliders[i].bounds);
            result.Add(new BuildingFloor{center=bounds.center,halfW=bounds.extents.x,halfD=bounds.extents.z,floorY=bounds.min.y});
        }
        return result;
    }
    bool RoadInsideBuilding(Vector3 p,float margin)
    {
        foreach(var b in RoadBuildingFootprints())
            if(Mathf.Abs(p.x-b.center.x)<b.halfW+margin&&Mathf.Abs(p.z-b.center.z)<b.halfD+margin)return true;
        return false;
    }
    bool RoadPointAllowed(Vector3 pt)
    {
        if(IsWater(pt.x,pt.z)||RoadInsideBuilding(pt,0.02f))return false;
        foreach(var tree in roadTrees)
            if(tree!=null && (new Vector2(tree.transform.position.x-pt.x,tree.transform.position.z-pt.z)).sqrMagnitude<2.25f)
                return false;
        return true;
    }
    readonly List<GameObject> roadTrees=new List<GameObject>();
    void StartRoadPreview()
    {
        StopRoadPreview();
        roadDragPreview = new GameObject("Road_Drag_Preview");
        roadDragPreview.AddComponent<MeshFilter>();
        var renderer = roadDragPreview.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = RoadSurfaceMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
    float nextRoadPreviewTime;
    void UpdateRoadPreviewMesh(List<Vector3> points, bool valid)
    {
        if (roadDragPreview == null) StartRoadPreview();
        else if(Time.unscaledTime<nextRoadPreviewTime)return;
        nextRoadPreviewTime=Time.unscaledTime+.10f;
        if (points == null || points.Count < 2) return;
        var filter = roadDragPreview.GetComponent<MeshFilter>();
        Mesh previous = filter.sharedMesh;
        filter.sharedMesh = CreateTerrainRoadMesh(points, PetTownRoadTypes.Width(roadBuildType), 0.035f,(roadDragging?roadStrokeMode:roadMode)!=PetTownRoadMode.GridStraight);
        if (previous != null) Destroy(previous);
        // Preview and final road use the same textured geometry and terrain sampling.
        roadDragPreview.GetComponent<MeshRenderer>().sharedMaterial =
            valid ? RoadSurfaceMaterial() : SafeFallbackMat();
    }
    void UpdateRoadPreview(Vector3 endPoint)
    {
        if(roadDragPoints==null || roadDragPoints.Count==0)return;
        var pts=new List<Vector3>(roadDragPoints);
        if(Vector2.Distance(new Vector2(pts[pts.Count-1].x,pts[pts.Count-1].z),
            new Vector2(endPoint.x,endPoint.z))>0.05f)pts.Add(endPoint);
        pts=roadStrokeMode==PetTownRoadMode.GridStraight?PetTownRoadDrawing.Straight(pts[0],endPoint,constructionGridSize,constructionGridOrigin):SmoothPath(pts,0.8f);
        UpdateRoadPreviewMesh(pts,true);
    }
    void StopRoadPreview()
    {
        if(roadDragPreview!=null)Destroy(roadDragPreview);
        roadDragPreview=null;
    }
    void Place(Vector3 p)
    {
        Vector3 rawPlacePoint=p;
        p=UsesBuildingGrid?BuildingPlacementPoint(p):mode==Mode.Furniture?FurniturePlacementPoint(p):
            new Vector3(Mathf.Round(p.x/2)*2,Ground(p.x,p.z),Mathf.Round(p.z/2)*2);
        if(mode==Mode.Furniture){PlaceInteriorFurniture(p);return;}
        if(UsesBuildingGrid)
        {
            var validation=ValidateBuildingPlacement(p,true);
            if(!validation.CanPlace){if(PromptTerrainGrade(rawPlacePoint,p,validation))return;Message(validation.Message);return;}
        }
        if(mode==Mode.Build && selected==8)
        {
            // User request: Road should be placeable anywhere except inside an existing
            // building's footprint or in water — not the generic building-spacing rule below
            // (flat radius around every building regardless of that building's real size).
            if(IsWater(p.x,p.z)){Message("Cannot build in water");return;}
            if(InsideAnyBuildingFootprint(p.x,p.z,0.15f)){Message("Cannot build inside a building");return;}
        }
        else if (mode != Mode.Furniture && !UsesBuildingGrid && !PetTownWorldSafety.CanPlaceBuilding(p, mode == Mode.Start ? 0 : selected, buildings, UsesBuildingGrid?(Vector2?)placementPlanner.Snapshot().WorldFootprint:null))
        { Message("Cannot build: water, steep ground or another building"); return; }
        if(IsWater(p.x,p.z)) {Message("Cannot build in water");return;}
        if(mode==Mode.Start)
        {
            shop=CreatePlannedBuilding(0,p,placementPlanner.Snapshot(true));shopPosition=p;CreateHero(p+new Vector3(8,0,8));
            dogStock=2;catStock=2;rabbitStock=0;inventory=4;CreatePetDisplays();
            RebakeNavMesh();
            QueueConstructionNavRefresh();
            mode=Mode.Play;Message("Pet Shop opened! Build a town and sell pets.");SaveGame();return;
        }
        if(mode==Mode.Build)
        {
            if(selected<1 || selected>=types.Length){Message("Select a building first");return;}
            if(selected==10&&(!PetTownRanchLibrary.Current||!PetTownRanchLibrary.Current.Ready)){Message("목장 에셋을 준비하고 있어요.");return;}
            if(selected==9&&(!PetTownFarmLibrary.Current||!PetTownFarmLibrary.Current.Ready)){Message("농장 에셋을 준비하고 있어요.");return;}
            if(money<costs[selected]) {Message("Not enough coins");return;}
            money-=costs[selected];
            var builtRecord=placementPlanner.Snapshot(true);
            CreatePlannedBuilding(selected,p,builtRecord);
            RebakeNavMesh();
            PushActorsOutOfBuilding(p,builtRecord.WorldFootprint);
            QueueConstructionNavRefresh();
            // User request: do not auto-build a path to the shop when constructing a building — roads now appear only when the player places a "Road" tile themselves (selected==8) in the Build menu.
            if(selected==2)ResidentHousingChanged();
            if(selected==5)reputation+=2;
            if(selected==3||selected==4)reputation+=5;
            Message(types[selected]+" built");SaveGame();
        }
    }
    // A little roof-color variety per building type makes the town read at a glance instead
    // of every roof being the exact same red. Built lazily and cached so each kind only
    // allocates one Material, not one per building.
    readonly Dictionary<int,Material> roofMatCache = new Dictionary<int,Material>();
    Material RoofMatFor(int kind)
    {
        if (roofMatCache.TryGetValue(kind, out var cached)) return cached;
        Color c;
        switch (kind)
        {
            case 1: c = new Color(.40f,.52f,.80f); break;  // Home - blue
            case 2: c = new Color(.56f,.46f,.78f); break;  // Resident House - violet
            case 3: c = new Color(.93f,.63f,.28f); break;  // Pet Cafe - warm orange
            case 4: c = new Color(.36f,.70f,.62f); break;  // Pet Hospital - clinical teal
            default: c = new Color(.82f,.35f,.39f); break; // Pet Shop / fallback - original red
        }
        Material m = Mat(c);
        roofMatCache[kind] = m;
        return m;
    }
    // Lays a simple stepping-stone path from the shop to a newly built building, so the town
    // feels connected without the player having to manually place Road tiles everywhere.
    // Purely decorative (Deco — no collider) and regenerated fresh each time the building is
    // (re)created, same convention as furniture/pet displays: not individually saved.
    // Buildings here are always open on the -Z (front) and +X (right) sides and walled on
    // +Z/-X (see CreateBuilding below) — so a path aimed at a building's bare center point
    // could easily end up heading straight at a real wall instead of the entrance. This finds
    // the matching building (if `center` is one) and returns a point just outside its open
    // front-right corner instead, so roads always approach from a side that's actually open.
    Vector3 EntrancePoint(Vector3 center)
    {
        foreach (var b in buildingFloors)
        {
            if (Mathf.Abs(b.center.x - center.x) < 0.5f && Mathf.Abs(b.center.z - center.z) < 0.5f)
            {
                var placed=b.owner!=null?b.owner.GetComponent<PetTownBuildingPlacement>():null;
                if(placed!=null&&placed.Record.hasEntrance)return placed.EntranceWorld+placed.EntranceForward*.8f;
                return new Vector3(b.center.x + b.halfW + 0.8f, center.y, b.center.z - b.halfD - 0.8f);
            }
        }
        return center;
    }
    bool InsideAnyBuildingFootprint(float x, float z, float margin)
    {
        foreach (var b in buildingFloors)
            if (Mathf.Abs(x - b.center.x) < b.halfW + margin && Mathf.Abs(z - b.center.z) < b.halfD + margin) return true;
        return false;
    }
    // Removes any already-scattered grass/flower/mushroom prop, and any already-built Road
    // segment, that falls inside a building's footprint once that building exists — covers both
    // auto-scattered vegetation (placed before the player chose a building site) and a Road
    // dragged across ground that a building later claims. User request: grass/road should never
    // render inside a building.
    void ClearPropsAndRoadsInFootprint(Vector3 center, float halfW, float halfD)
    {
        for(int i=vegetationProps.Count-1;i>=0;i--)
        {
            var v=vegetationProps[i];
            if(v==null){vegetationProps.RemoveAt(i);continue;}
            Vector3 vp=v.transform.position;
            if(Mathf.Abs(vp.x-center.x)<halfW && Mathf.Abs(vp.z-center.z)<halfD){ Destroy(v); vegetationProps.RemoveAt(i); }
        }
        if(activeBuildingPlacement!=null)return;
        for(int i=buildings.Count-1;i>=0;i--)
        {
            var b=buildings[i];
            if(b==null || !b.name.StartsWith("Road")) continue;
            Vector3 bp=b.transform.position;
            if(Mathf.Abs(bp.x-center.x)<halfW && Mathf.Abs(bp.z-center.z)<halfD){ Destroy(b); buildings.RemoveAt(i); }
        }
    }
    // User request: Road is drawn by click-dragging a path instead of stamping one flat tile at
    // a time — captured mouse-world points are smoothed into a bounded curve with distance-based sampling and
    // built as one continuous ribbon (reusing BuildRoadRibbon, the same mesh generator the
    // auto-generated shop paths use), clipped out of water and any building's footprint.
    List<Vector3> SmoothPath(List<Vector3> pts, float spacing)
    {
        var clean=new List<Vector3>();
        foreach(var p in pts)if(clean.Count==0||Vector2.Distance(new Vector2(p.x,p.z),new Vector2(clean[clean.Count-1].x,clean[clean.Count-1].z))>.01f)clean.Add(p);
        if(clean.Count<2)return clean;
        var curve=new List<Vector3>{clean[0]};
        for(int i=1;i<clean.Count-1;i++)
        {
            curve.Add(Vector3.Lerp(clean[i],clean[i-1],.18f));
            curve.Add(Vector3.Lerp(clean[i],clean[i+1],.18f));
        }
        curve.Add(clean[clean.Count-1]);
        var result=new List<Vector3>{curve[0]};
        for(int i=1;i<curve.Count;i++)
        {
            float length=Vector2.Distance(new Vector2(curve[i-1].x,curve[i-1].z),new Vector2(curve[i].x,curve[i].z));
            int count=Mathf.Max(1,Mathf.CeilToInt(length/Mathf.Min(spacing,.25f)));
            for(int j=1;j<=count;j++)result.Add(Vector3.Lerp(curve[i-1],curve[i],j/(float)count));
        }
        return result;
    }
    // Find where a drawn segment actually meets a building's rectangular footprint.
    // This prevents a road stopping randomly short or running under a building.
    Vector3 RoadBoundary(Vector3 outside,Vector3 inside)
    {
        Vector3 a=outside,b=inside;
        for(int j=0;j<16;j++)
        {
            Vector3 mid=(a+b)*.5f;
            if(RoadInsideBuilding(mid,0f))b=mid;else a=mid;
        }
        return RoadSurfacePoint(a);
    }
    void PlaceRoadPath(List<Vector3> rawPoints)=>PlaceRoadPath(rawPoints,PetTownRoadMode.FreeCurve);
    void PlaceRoadPath(List<Vector3> rawPoints,PetTownRoadMode drawingMode)
    {
        if(rawPoints==null||rawPoints.Count<2) return;
        var smoothed=drawingMode==PetTownRoadMode.GridStraight?PetTownRoadDrawing.Straight(rawPoints[0],rawPoints[rawPoints.Count-1],constructionGridSize,constructionGridOrigin):SmoothPath(rawPoints,1.2f);
        if(smoothed.Count<2)return;
        var junctions=PetTownRoadDrawing.Junctions(smoothed,PetTownRoadTypes.Width(roadBuildType));
        var strips=new List<List<Vector3>>();
        var current=new List<Vector3>();
        Vector3 previous=smoothed[0];
        bool previousBuilding=RoadInsideBuilding(previous,0f);
        foreach(var pt in smoothed)
        {
            bool building=RoadInsideBuilding(pt,0f);
            bool valid=RoadPointAllowed(pt);
            if(building && !previousBuilding && current.Count>0)
            {
                current.Add(RoadBoundary(previous,pt));
                if(current.Count>=2)strips.Add(current);
                current=new List<Vector3>();
            }
            else if(!building && previousBuilding && valid)
            {
                current=new List<Vector3>{RoadBoundary(pt,previous)};
            }
            if(valid)current.Add(RoadSurfacePoint(pt));
            else if(!building)
            {
                if(current.Count>=2)strips.Add(current);
                current=new List<Vector3>();
            }
            previous=pt;previousBuilding=building;
        }
        if(current.Count>=2)strips.Add(current);
        if(strips.Count==0){Message("Road blocked by a building, tree or river");return;}
        float length=0f;
        foreach(var strip in strips)
            for(int i=1;i<strip.Count;i++)length+=Vector2.Distance(new Vector2(strip[i-1].x,strip[i-1].z),new Vector2(strip[i].x,strip[i].z));
        int price=Mathf.Max(1,Mathf.CeilToInt(length))*PetTownRoadTypes.Cost(roadBuildType);
        if(money<price){Message("Not enough coins for road: "+price);return;}
        money-=price;
        var pathParent=new GameObject("Road_REPLACEABLE");
        pathParent.transform.position=Vector3.zero;
        foreach(var strip in strips)
        {
            BuildRoadRibbon(strip,PetTownRoadTypes.Width(roadBuildType),pathParent.transform,drawingMode!=PetTownRoadMode.GridStraight,roadBuildType);
            var paths=pathParent.GetComponentsInChildren<PetTownRoadPath>();var path=paths[paths.Length-1];
            var record=path.Capture();record.mode=(int)drawingMode;path.RestoreIdentity(record);
        }
        buildings.Add(pathParent);
        RebakeNavMesh();
        Message("Road built: -"+price+" coins"+(junctions.Count>0?" · 기존 도로 연결 "+junctions.Count:""));
        SaveGame();
    }
    // A continuous dirt-road ribbon mesh (EasyRoads3D's own dirt texture/material, already in
    // the project) that hugs the actual terrain height at every sampled point and is clipped
    // out of any building's footprint and out of water — replaces the old flat stepping-stone
    // tiles, which could sink into slopes or lay straight across a building's floor since they
    // only sampled ground height at their own center, not along a real path.
    void BuildPathTo(Vector3 from, Vector3 to)
    {
        Vector3 start = EntrancePoint(from), end = EntrancePoint(to);
        Vector3 diff = end - start; diff.y = 0;
        float dist = diff.magnitude;
        if (dist < 3f) return; // close enough already, don't bother
        int segments = Mathf.Clamp(Mathf.CeilToInt(dist / 1.4f), 2, 90);
        var points = new List<Vector3>();
        for (int s = 0; s <= segments; s++)
        {
            float t = s / (float)segments;
            Vector3 pt = Vector3.Lerp(start, end, t);
            if (IsWater(pt.x, pt.z)) continue;
            if (InsideAnyBuildingFootprint(pt.x, pt.z, 0.6f)) continue;
            pt.y = RoadTerrainY(pt.x, pt.z) + 0.085f;
            points.Add(pt);
        }
        if (points.Count < 2) return;
        var pathParent = new GameObject("Path_REPLACEABLE");
        BuildRoadRibbon(points, 2.2f, pathParent.transform);
        int lampEvery = 7;
        for (int i = lampEvery; i < points.Count - 1; i += lampEvery)
        {
            Vector3 dir = (points[Mathf.Min(i + 1, points.Count - 1)] - points[i - 1]);
            dir.y = 0;
            if (dir.sqrMagnitude < 0.0001f) continue;
            Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);
            MakeLampPost(points[i] + side * 1.7f);
        }
    }
    // Clip a triangle/polygon against a scalar half-plane (positive = retained).
    static List<Vector2> ClipRoadPolygon(List<Vector2> polygon, Func<Vector2,float> field)
    {
        var result=new List<Vector2>();
        if(polygon.Count==0)return result;
        Vector2 a=polygon[polygon.Count-1]; float fa=field(a);
        foreach(var b in polygon)
        {
            float fb=field(b);
            if((fa>=0f)!=(fb>=0f))result.Add(Vector2.Lerp(a,b,fa/(fa-fb)));
            if(fb>=0f)result.Add(b);
            a=b;fa=fb;
        }
        return result;
    }
    static float RoadSegmentDistance(Vector2 p,Vector2 a,Vector2 b)
    {
        Vector2 d=b-a;
        return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(d.sqrMagnitude,.000001f)));
    }
    Vector3 RoadEntranceEnd(Vector3 p,float radius)
    {
        // The procedural buildings have open -Z/+X faces. Snap only nearby ends,
        // never reroute a passing road or move any building/entrance geometry.
        Vector3 best=p;float distance=radius*.8f;
        foreach(var b in buildingFloors)
        {
            var placed=b.owner!=null?b.owner.GetComponent<PetTownBuildingPlacement>():null;
            if(placed!=null)
            {
                if(placed.Record.hasEntrance)
                {
                    Vector3 entrance=placed.EntranceWorld;
                    float d=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(entrance.x,entrance.z));
                    if(d<distance){distance=d;best=entrance;best.y=p.y;}
                }
                continue;
            }
            Vector3 front=new Vector3(Mathf.Clamp(p.x,b.center.x-b.halfW+radius,b.center.x+b.halfW-radius),p.y,b.center.z-b.halfD);
            Vector3 right=new Vector3(b.center.x+b.halfW,p.y,Mathf.Clamp(p.z,b.center.z-b.halfD+radius,b.center.z+b.halfD-radius));
            foreach(var candidate in new[]{front,right})
            {
                float d=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(candidate.x,candidate.z));
                if(d<distance){distance=d;best=candidate;}
            }
        }
        return best;
    }
    Mesh CreateTerrainRoadMesh(List<Vector3> points, float width, float lift,bool alignEntrances=true)
    {
        var mesh=new Mesh();
        if(points==null||points.Count<2||width<=0f)return mesh;
        float radius=width*.5f;
        var path=new List<Vector2>();
        for(int i=0;i<points.Count;i++)
        {
            Vector3 p=alignEntrances&&(i==0||i==points.Count-1)?RoadEntranceEnd(points[i],radius):points[i];
            Vector2 q=new Vector2(p.x,p.z);
            if(path.Count==0||(path[path.Count-1]-q).sqrMagnitude>.000001f)path.Add(q);
        }
        if(path.Count<2)return mesh;
        // Union of swept discs on a fixed planar grid: one face per cell, even
        // at reversals/self-intersections. No offset-curve miters can fold over.
        const float step=.20f;
        var footprints=RoadBuildingFootprints();
        var cells=new Dictionary<Vector2Int,List<int>>();
        for(int i=1;i<path.Count;i++)
        {
            Vector2 a=path[i-1],b=path[i];
            int x0=Mathf.FloorToInt((Mathf.Min(a.x,b.x)-radius-step*2f)/step),x1=Mathf.FloorToInt((Mathf.Max(a.x,b.x)+radius+step*2f)/step);
            int z0=Mathf.FloorToInt((Mathf.Min(a.y,b.y)-radius-step*2f)/step),z1=Mathf.FloorToInt((Mathf.Max(a.y,b.y)+radius+step*2f)/step);
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
            {
                var key=new Vector2Int(x,z);
                if(!cells.TryGetValue(key,out var segments)){segments=new List<int>();cells.Add(key,segments);}
                segments.Add(i);
            }
        }
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();
        var vertexMap=new Dictionary<Vector2Int,int>();
        foreach(var cell in cells)
        {
            float Field(Vector2 p)
            {
                float value=-step*2f;
                foreach(int i in cell.Value)value=Mathf.Max(value,radius-RoadSegmentDistance(p,path[i-1],path[i]));
                value=Mathf.Min(value,Mathf.Abs(p.x-RiverCenterX(p.y))-3.5f);
                // Preserve the existing lake exclusion and tree clearance.
                value=Mathf.Min(value,Mathf.Max(Mathf.Max(52f-p.x,p.x-72f),Mathf.Max(35f-p.y,p.y-66f)));
                foreach(var tree in roadTrees)if(tree!=null)
                    value=Mathf.Min(value,Vector2.Distance(p,new Vector2(tree.transform.position.x,tree.transform.position.z))-1.5f);
                return value;
            }
            int Vertex(Vector2 p)
            {
                var key=new Vector2Int(Mathf.RoundToInt(p.x*100000f),Mathf.RoundToInt(p.y*100000f));
                if(vertexMap.TryGetValue(key,out int index))return index;
                index=vertices.Count;vertexMap.Add(key,index);
                float y=RoadTerrainY(p.x,p.y)+lift;
                foreach(var b in buildingFloors)
                {
                    var placed=b.owner!=null?b.owner.GetComponent<PetTownBuildingPlacement>():null;
                    if(placed!=null)
                    {
                        Vector3 delta=new Vector3(p.x,0,p.y)-placed.EntranceWorld;delta.y=0;
                        float entryGap=Vector3.Dot(delta,placed.EntranceForward);
                        float lateral=Mathf.Abs(Vector3.Dot(delta,Vector3.Cross(Vector3.up,placed.EntranceForward)));
                        if(placed.Record.hasEntrance&&entryGap>=-.001f&&entryGap<.85f&&lateral<width*.5f)
                            y=Mathf.Lerp(y,Mathf.Max(y,b.floorY+.012f),1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01(entryGap/.85f)));
                        continue;
                    }
                    float dx=p.x-b.center.x,dz=p.y-b.center.z;
                    float gap=float.MaxValue;
                    if(Mathf.Abs(dx)<=b.halfW+.001f&&dz<=-b.halfD+.001f)gap=-b.halfD-dz;
                    if(Mathf.Abs(dz)<=b.halfD+.001f&&dx>=b.halfW-.001f)gap=Mathf.Min(gap,dx-b.halfW);
                    if(gap<.85f)y=Mathf.Lerp(y,Mathf.Max(y,b.floorY+.012f),1f-Mathf.SmoothStep(0f,1f,gap/.85f));
                }
                vertices.Add(new Vector3(p.x,y,p.y));
                // World-distance UVs stay the same scale at bends, joins and caps.
                uv.Add(p/2.5f);
                colors.Add(new Color(1f,1f,1f,Mathf.SmoothStep(0f,1f,Mathf.Clamp01(Field(p)/.12f))));
                return index;
            }
            Vector2 a=new Vector2(cell.Key.x*step,cell.Key.y*step),b=a+Vector2.right*step,c=a+Vector2.up*step,d=b+Vector2.up*step;
            foreach(var seed in new[]{new List<Vector2>{a,c,b},new List<Vector2>{b,c,d}})
            {
                var fragments=new List<List<Vector2>>{ClipRoadPolygon(seed,Field)};
                if(fragments[0].Count<3)continue;
                // Exact rectangle subtraction: do not project individual vertices
                // to different walls, and do not let triangles span a building corner.
                foreach(var building in footprints)
                {
                    var next=new List<List<Vector2>>();
                    foreach(var fragment in fragments)
                    {
                        var inside=fragment;
                        Func<Vector2,float>[] faces={p=>building.center.x-building.halfW-p.x,p=>p.x-building.center.x-building.halfW,p=>building.center.z-building.halfD-p.y,p=>p.y-building.center.z-building.halfD};
                        foreach(var face in faces)
                        {
                            var outside=ClipRoadPolygon(inside,face);
                            if(outside.Count>=3)next.Add(outside);
                            inside=ClipRoadPolygon(inside,p=>-face(p));
                        }
                    }
                    fragments=next;
                }
                foreach(var polygon in fragments)for(int j=1;j<polygon.Count-1;j++)
                {
                    Vector2 e=polygon[j]-polygon[0],f=polygon[j+1]-polygon[0];
                    if(Mathf.Abs(e.x*f.y-e.y*f.x)<.0000001f)continue;
                    triangles.Add(Vertex(polygon[0]));triangles.Add(Vertex(polygon[j]));triangles.Add(Vertex(polygon[j+1]));
                }
            }
        }
        mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        return mesh;
    }
    Material RoadSurfaceMaterial()
    {
        if(roadSurfaceMaterial!=null)return roadSurfaceMaterial;
        Shader shader=Resources.Load<Shader>("Materials/PetTownRoadSurface");
        if(shader==null)return dirtRoadMat!=null?dirtRoadMat:road;
        roadSurfaceMaterial=new Material(shader);
        roadSurfaceMaterial.mainTexture=dirtRoadMat!=null?dirtRoadMat.mainTexture:null;
        return roadSurfaceMaterial;
    }
    void BuildRoadRibbon(List<Vector3> points, float width, Transform parent,bool alignEntrances=true,int type=0)
    {
        if(points==null||points.Count<2)return;
        var go=new GameObject("Road_Ribbon");
        go.transform.SetParent(parent,true);
        go.AddComponent<MeshFilter>().sharedMesh=CreateTerrainRoadMesh(points,width,0.035f,alignEntrances);
        go.AddComponent<MeshRenderer>().sharedMaterial=RoadMaterialFor(type);
        var roadPath=go.AddComponent<PetTownRoadPath>();roadPath.Initialize(points,width);roadPath.SetType(type);
        MarkNavMeshIgnore(go);
    }
    // Perimeter fence for Park/Zoo using the real Low Poly Medieval Starter Pack wooden fence
    // kit, with a gate segment in the middle of the south side (falls back to bare posts if the
    // asset is ever missing). Replaces plain primitive posts so the boundary reads as an actual
    // built structure, and gives it a clear single entrance instead of a solid unbroken ring.
    void BuildFenceRing(Vector3 center, float halfSize, Transform parent)
    {
        float side = halfSize*2f;
        float spacing = 2.6f;
        int perSide = Mathf.Max(3, Mathf.RoundToInt(side/spacing));
        GameObject fencePrefab = Resources.Load<GameObject>(FenceResource);
        GameObject gatePrefab = Resources.Load<GameObject>(GateResource);
        for(int fs=0; fs<4; fs++)
        for(int fi=0; fi<perSide; fi++)
        {
            float t = fi/(float)(perSide-1)*side-halfSize;
            bool isGateSpot = fs==0 && fi==perSide/2;
            Vector3 local = fs==0?new Vector3(t,0,-halfSize): fs==1?new Vector3(t,0,halfSize): fs==2?new Vector3(-halfSize,0,t): new Vector3(halfSize,0,t);
            float yaw = (fs==0||fs==1) ? 0f : 90f;
            Vector3 pos = center+local;
            GameObject prefab = isGateSpot ? gatePrefab : fencePrefab;
            if(prefab==null)
            {
                Deco(PrimitiveType.Cylinder,"Fence_Post",pos+Vector3.up*.45f,new Vector3(.08f,.45f,.08f),wood,parent);
                continue;
            }
            var segRoot=new GameObject(isGateSpot?"Gate":"Fence_Segment");
            segRoot.transform.SetParent(parent,true);
            segRoot.transform.position=pos;
            GameObject instance=Instantiate(prefab,segRoot.transform);
            instance.name="Model";
            instance.transform.localPosition=Vector3.zero;
            instance.transform.localRotation=Quaternion.Euler(0,yaw,0);
            instance.transform.localScale=Vector3.one;
            foreach(var c in instance.GetComponentsInChildren<Collider>()) Destroy(c);
            MarkNavMeshIgnore(instance);
            StartCoroutine(FitPropWithColliderNextFrame(instance,segRoot,center.y,isGateSpot?1.6f:1.1f,.9f));
        }
    }
    // A simple primitive lamp post — pole, lamp head, warm glow — dropped along paths for a
    // little night-time atmosphere. Built entirely from primitives, no new assets needed.
    void MakeLampPost(Vector3 p)
    {
        var root = new GameObject("Lamp_Post_REPLACEABLE"); root.transform.position = p;
        Deco(PrimitiveType.Cylinder, "Pole", p + Vector3.up * 1.1f, new Vector3(.08f, 1.1f, .08f), dark, root.transform);
        Deco(PrimitiveType.Sphere, "Lamp_Head", p + Vector3.up * 2.25f, new Vector3(.32f, .32f, .32f), gold, root.transform);
        var lightGO = new GameObject("Lamp_Light"); lightGO.transform.SetParent(root.transform, false);
        lightGO.transform.position = p + Vector3.up * 2.25f;
        var lt = lightGO.AddComponent<Light>();
        lt.type = LightType.Point; lt.color = new Color(1f, .85f, .55f); lt.intensity = 1.1f; lt.range = 7f;
    }
    GameObject CreateBuilding(int kind,Vector3 p,bool rival)
    {
        var root=new GameObject((rival?"Dark_":"")+types[kind].Replace(" ","_")+"_REPLACEABLE");
        root.transform.position=p;
        var previousOwner=furnishingOwner;furnishingOwner=null;
        if(kind==0||kind==1||kind==3||kind==4)
        {
            var definition=DefinitionFor(kind);furnishingOwner=root.AddComponent<PetTownInteriorState>();
            furnishingOwner.Initialize(activeBuildingPlacement!=null?activeBuildingPlacement.id:null,new Vector2(kind==0?15:11,kind==0?15:10),kind==0?.12f:.4f,definition.entranceLocal,kind);
        }
        try
        {
        Material m=rival?dark:wall;
        if(kind==10)
        {
            CreateRanchBuilding(root,p);
        }
        else if(kind==9)
        {
            CreateFarmBuilding(root,p);
        }
        else if(kind==8)
        {
            FlattenBuildingFootprint(p,0.4f,0.4f,p.y);
            Deco(PrimitiveType.Cube,"Road_Tile",p+Vector3.up*.06f,new Vector3(0.75f,.12f,0.75f),road,root.transform); // user request: 1/4 of the previous 3x3 size — reads as a small paving stone
            // Thin dark curb along all four edges so a purchased Road tile reads as an actual
            // road/sidewalk instead of a flat gray square dropped on the grass.
            float rw=0.75f, rhalf=rw/2f, curbW=.05f;
            Deco(PrimitiveType.Cube,"Curb_N",p+new Vector3(0,.1f, rhalf-curbW/2f),new Vector3(rw,.06f,curbW),dark,root.transform);
            Deco(PrimitiveType.Cube,"Curb_S",p+new Vector3(0,.1f,-(rhalf-curbW/2f)),new Vector3(rw,.06f,curbW),dark,root.transform);
            Deco(PrimitiveType.Cube,"Curb_E",p+new Vector3(rhalf-curbW/2f,.1f,0),new Vector3(curbW,.06f,rw),dark,root.transform);
            Deco(PrimitiveType.Cube,"Curb_W",p+new Vector3(-(rhalf-curbW/2f),.1f,0),new Vector3(curbW,.06f,rw),dark,root.transform);
        }
        else if(kind==2)
        {
            // Villager housing uses real Low Poly Medieval Starter Pack house models instead of
            // the plain procedural box — these are solid/closed meshes with no walk-in interior,
            // so no floor/furniture is built for them; a BoxCollider sized to the fitted model
            // keeps them solid obstacles for players, villagers and the navmesh alike.
            FlattenBuildingFootprint(p,4.6f,4.6f,p.y);
            GameObject housePrefab = MedievalHouseResources.Length>0 ? Resources.Load<GameObject>(MedievalHouseResources[activeBuildingPlacement!=null?Mathf.Clamp(activeBuildingPlacement.modelVariant,0,MedievalHouseResources.Length-1):UnityEngine.Random.Range(0,MedievalHouseResources.Length)]) : null;
            if(housePrefab!=null&&!EnterableHouses)
            {
                GameObject instance=Instantiate(housePrefab,root.transform);
                instance.name="Model";
                instance.transform.localPosition=Vector3.zero;
                instance.transform.localRotation=Quaternion.Euler(0,activeBuildingPlacement!=null?activeBuildingPlacement.modelYawCorrection:UnityEngine.Random.Range(0f,360f),0);
                instance.transform.localScale=Vector3.one;
                foreach(var c in instance.GetComponentsInChildren<Collider>()) Destroy(c);
                MarkNavMeshIgnore(instance);
                StartCoroutine(FitPropWithColliderNextFrame(instance,root,p.y,activeBuildingPlacement!=null?activeBuildingPlacement.modelHeight:UnityEngine.Random.Range(3.4f,4.2f),.8f));
            }
            else
            {
                // Asset missing — fall back to the old simple procedural box house.
                float w=7,d=7;
                Material floorMatF=rival?Tiled(dark,w,d):Tiled(wood,w,d);
                Material wallMatBDF=Tiled(m,w,5), wallMatLRF=Tiled(m,d,5);
                Deco(PrimitiveType.Cube,"Editable_Floor",p+Vector3.up*.2f,new Vector3(w,.4f,d),floorMatF,root.transform);
                Part(PrimitiveType.Cube,"Back_Wall",p+new Vector3(0,2.5f,d/2),new Vector3(w,5,.35f),wallMatBDF,root.transform);
                Part(PrimitiveType.Cube,"Left_Wall",p+new Vector3(-w/2,2.5f,0),new Vector3(.35f,5,d),wallMatLRF,root.transform);
                Deco(PrimitiveType.Cube,"Toggle_Roof",p+new Vector3(0,5.4f,0),new Vector3(w+1,.65f,d+1),rival?dark:RoofMatFor(kind),root.transform);
                buildingFloors.Add(new BuildingFloor{center=p,halfW=w/2f,halfD=d/2f,floorY=p.y+.4f});
                ClearBuildingFootprint(p,w/2f,d/2f);
            }
        }
        else if(kind==5||kind==6||kind==7)
        {
            CreateCommunityBuilding(root,p,kind);
        }
        else
        {
            bool open=kind==0||kind==1||kind==2||kind==3||kind==4;
            float w=kind==0?15:kind==2?7:11, d=kind==0?15:kind==2?7:10;
            // 건물 발밑 지형을 평평하게 다듬어서 "땅에 파묻힘/붕 뜸" 문제를 없앤다.
            // Widen the hard-flatten zone a couple meters past the building's own half-size:
            // the terrain's vertex grid (2m steps) rarely lines up exactly with the building edge,
            // so a vertex just past w/2,d/2 could sit in the softer margin-blend zone (not fully
            // flat) while the floor mesh's own edge already extends out that far — showing as a
            // jagged strip of terrain poking through right at the floor's border.
            FlattenBuildingFootprint(p,w/2f+2.5f,d/2f+2.5f,p.y);
            bool rivalBuild = rival;
            Material floorMat = rivalBuild ? Tiled(dark,w,d) : Tiled(wood,w,d);
            Material wallMatBD = Tiled(m,w,5);
            Material wallMatLR = Tiled(m,d,5);
            // 바닥/몰딩/창틀/지붕은 Deco()로 만들어서 콜라이더가 없음 → 입구가 열려 있으면
            // 캐릭터가 "턱"에 걸리지 않고 그대로 걸어 들어올 수 있다. 실제 벽(Part)만 충돌 처리됨.
            if(kind!=0) Deco(PrimitiveType.Cube,"Editable_Floor",p+Vector3.up*.2f,new Vector3(w,.4f,d),floorMat,root.transform);
            buildingFloors.Add(new BuildingFloor{center=p,halfW=w/2f,halfD=d/2f,floorY=p.y+(kind==0 ? .12f : .4f)});
            ClearBuildingFootprint(p,w/2f,d/2f);
            if(kind==0)
            {
                BuildModularShopShell(p,w,d,open,root.transform,rival);
                for(int i=-1;i<=1;i++)
                {
                    Deco(PrimitiveType.Cube,"Window_Glass",p+new Vector3(i*w*.27f,1.85f,d/2-.13f),new Vector3(1.1f,1.2f,.09f),windowMat,root.transform);
                    Deco(PrimitiveType.Cube,"Window_Sill",p+new Vector3(i*w*.27f,1.2f,d/2-.13f),new Vector3(1.4f,.15f,.2f),trim,root.transform);
                }
            }
            else
            {
                Part(PrimitiveType.Cube,"Back_Wall",p+new Vector3(0,2.5f,d/2),new Vector3(w,5,.35f),wallMatBD,root.transform);
                Deco(PrimitiveType.Cube,"Back_Trim",p+new Vector3(0,4.85f,d/2-.23f),new Vector3(w,.28f,.15f),trim,root.transform);
                for(int i=-1;i<=1;i++)
                {
                    Deco(PrimitiveType.Cube,"Window_Glass",p+new Vector3(i*w*.27f,2.95f,d/2-.28f),new Vector3(1.25f,1.55f,.09f),windowMat,root.transform);
                    Deco(PrimitiveType.Cube,"Window_Sill",p+new Vector3(i*w*.27f,2.13f,d/2-.37f),new Vector3(1.55f,.17f,.22f),trim,root.transform);
                }
                Part(PrimitiveType.Cube,"Left_Wall",p+new Vector3(-w/2,2.5f,0),new Vector3(.35f,5,d),wallMatLR,root.transform);
                if(!open)
                {
                    Part(PrimitiveType.Cube,"Right_Wall",p+new Vector3(w/2,2.5f,0),new Vector3(.35f,5,d),wallMatLR,root.transform);
                    Part(PrimitiveType.Cube,"Front_Wall",p+new Vector3(0,2.5f,-d/2),new Vector3(w,5,.35f),wallMatBD,root.transform);
                }
                Deco(PrimitiveType.Cube,"Foundation_Trim",p+new Vector3(0,.39f,0),new Vector3(w+.35f,.13f,d+.35f),trim,root.transform);
                var top=Deco(PrimitiveType.Cube,"Toggle_Roof",p+new Vector3(0,5.4f,0),new Vector3(w+1,.65f,d+1),rival?dark:RoofMatFor(kind),root.transform);
                top.SetActive(!open||showRoofs);
            }
            if(kind==0&&!rival)
            {
                shop=root;shopPosition=p;
                if(!loadingTown)CreateInitialShopInterior(p);
                else
                {
                CreateFurniture(0,p+new Vector3(0,0,-2));       // Counter / register
                CreateFurniture(1,p+new Vector3(-4,0,-1));      // Pet Bed (decorative basket; moved off the live pet's wander patch)
                CreateFurniture(2,p+new Vector3(4,0,2));        // Display
                CreatePetShopDetail(0,p+new Vector3(-6.3f,0,-4f)); // Food display
                CreatePetShopDetail(1,p+new Vector3(-6.3f,0,0));   // Toy display
                CreatePetShopDetail(2,p+new Vector3(-6.3f,0,4f));  // Rat cage
                CreatePetShopDetail(3,p+new Vector3(0,0,5.6f));    // Aquarium
                CreatePetShopDetail(4,p+new Vector3(3.2f,0,5.2f)); // Dog cushion
                CreatePetShopDetail(5,p+new Vector3(5.5f,0,3.4f)); // Cat cushion
                CreatePetShopDetail(6,p+new Vector3(2.2f,0,-4.2f));// Bird cage (hanging)
                }
            }
            // Auto-furnish every other indoor building type too, not just the Pet Shop — an
            // empty room reads as unfinished. Each uses the same 6 Furniture/*.prefab pieces,
            // just arranged to suit what that building is for, and kept well clear of the open
            // front/right side so nothing blocks the entrance villagers walk through.
            if(kind==1&&!rival) // Home
            {
                CreateFurniture(4,p+new Vector3(0,0,0));      // Table
                CreateFurniture(3,p+new Vector3(1.6f,0,-.2f)); // Chair
                CreateFurniture(5,p+new Vector3(-3.6f,0,3.2f)); // Plant
            }
            if(kind==2&&!rival) // Resident House — gives the "sit" animation an actual chair
                                 // to eventually be triggered by, instead of nothing at all.
            {
                CreateFurniture(4,p+new Vector3(-1f,0,-.6f));  // Table
                CreateFurniture(3,p+new Vector3(.7f,0,-.6f));  // Chair
                CreateFurniture(5,p+new Vector3(2f,0,1.9f));   // Plant
            }
            if(kind==3&&!rival) // Pet Cafe
            {
                CreateFurniture(0,p+new Vector3(0,0,-3f));     // Counter
                CreateFurniture(4,p+new Vector3(-3f,0,2f));    // Table
                CreateFurniture(3,p+new Vector3(-1.4f,0,2f));  // Chair
                CreateFurniture(5,p+new Vector3(3.6f,0,3.2f)); // Plant
            }
            if(kind==4&&!rival) // Pet Hospital
            {
                CreateFurniture(0,p+new Vector3(0,0,-3f));     // Counter
                CreateFurniture(2,p+new Vector3(-3.6f,0,2f));  // Display
                CreateFurniture(5,p+new Vector3(3.6f,0,3.2f)); // Plant
            }
        }
        if(!rival)buildings.Add(root);
        if(!rival&&root.GetComponent<PetTownInteriorState>()!=null&&PetTownCozyLibrary.Current!=null)root.AddComponent<PetTownCozyInterior>();
        return root;
        }
        finally{furnishingOwner=previousOwner;}
    }
    void CreateInitialShopInterior(Vector3 p)
    {
        // Aisles and entry remain open; no modification of existing saved layouts.
        var counter=CreateFurniture(0,p+new Vector3(-2,.12f,-4));
        var chair=CreateFurniture(3,p+new Vector3(-2,.12f,-2.5f));
        chair.transform.rotation=Quaternion.Euler(0,180,0);
        var plant=CreateFurniture(5,p+new Vector3(5.5f,.12f,-4.5f));
        foreach(var root in new[]{counter,chair,plant}){var model=root.transform.Find("Model");if(model!=null)model.localRotation=Quaternion.identity;}
        CreatePetShopDetail(8,p+new Vector3(-2,1.18f,-4));
        var feed=CreatePetShopDetail(9,p+new Vector3(-6,.12f,-.5f));feed.transform.rotation=Quaternion.Euler(0,90,0);
        var toys=CreatePetShopDetail(1,p+new Vector3(-6.5f,.12f,2));toys.transform.rotation=Quaternion.Euler(0,90,0);
        CreatePetShopDetail(2,p+new Vector3(-6,.12f,5));
        CreatePetShopDetail(3,p+new Vector3(0,.12f,5.5f));
        CreatePetShopDetail(4,p+new Vector3(3.5f,.12f,0));
        CreatePetShopDetail(6,p+new Vector3(5.5f,.12f,3));
        CreatePetShopDetail(7,p+new Vector3(5.5f,.12f,5.5f));
    }

    // 가구별로 쓸 실제 에셋 프리팹 경로(Resources 기준)와 목표 높이(미터).
    // 0 Counter(카운터)→주방 벤치, 1 Pet Bed(펫 베드)→바구니, 2 Display(진열대)→TV 스탠드(오픈 선반),
    // 3 Chair(의자)→식탁 의자, 4 Table(테이블)→식탁, 5 Plant(화분)→덤불.
    static readonly string[] FurnitureResource={"Furniture/FurnCounter","Furniture/FurnPetBed","Furniture/FurnDisplay","Furniture/FurnChair","Furniture/FurnTable","Furniture/FurnPlant"};
    static readonly float[] FurnitureHeight={1.05f,.22f,1.0f,.9f,.95f,1.1f}; // PetBed lowered to match the new round cushion-bed shape (was .55f, sized for the old basket mesh)
    GameObject CreateFurniture(int type,Vector3 p,float heightOverride=-1f)
    {
        if(TryCreateCozyFurniture(type,false,p,heightOverride,out var cozy))return cozy;
        var root=new GameObject("Furniture_"+furniture[type]+"_REPLACEABLE");root.transform.position=p;
        float targetHeight=heightOverride>0?heightOverride:FurnitureHeight[type];
        GameObject prefab=Resources.Load<GameObject>(FurnitureResource[type]);
        if(prefab!=null)
        {
            GameObject instance=Instantiate(prefab,root.transform);
            instance.name="Model";
            instance.transform.localPosition=Vector3.zero;
            // A small random yaw so repeated furniture layouts (every Home/Resident House
            // has the same set) don't look perfectly identical/robotic, without rotating far
            // enough to face a chair/table into a wall.
            instance.transform.localRotation=Quaternion.Euler(0,UnityEngine.Random.Range(-8f,8f),0);
            MarkNavMeshIgnore(root);
            // Scaling right after Instantiate() reads stale/unreliable Renderer.bounds (same bug
            // that made pet models come out invisible or screen-filling) — wait one frame first.
            StartCoroutine(FitModelNextFrame(instance,p.y,targetHeight));
            StartCoroutine(AddFittedFurnitureObstacle(root));
        }
        else
        {
            // 에셋을 못 찾으면(아직 Resources에 안 들어갔거나 삭제됨) 예전 도형 가구로 안전하게 대체.
            var m=type==5?grass:type==1?roofMat:wood;
            Part(PrimitiveType.Cube,"Body",p+Vector3.up*.65f,new Vector3(type==0?3:1.6f,1.2f,1.4f),m,root.transform);
            if(type==5) Part(PrimitiveType.Sphere,"Leaves",p+Vector3.up*1.7f,new Vector3(1.5f,1.4f,1.5f),grass,root.transform);
            StartCoroutine(AddFittedFurnitureObstacle(root));
        }
        props.Add(root);RegisterFurniture(root,type,false,targetHeight);return root;
    }
    // Pet-shop interior fixtures the user asked for by name: a food display, toy display, rat
    // cage, aquarium, dog cushion, cat cushion and bird cage. None of the owned asset packs had
    // anything pet-shop-shaped (checked ithappy, Brick Project Studio, Polytope Studio,
    // InnerverseInteractive — only generic kitchen/fantasy props), so these are hand-built from
    // primitives, with the aquarium using real Floreswa fish models for the fish. All collider-
    // free (Deco) so they never block a villager or the player walking through the shop.
    GameObject CreatePetShopDetail(int type, Vector3 p)
    {
        if(TryCreateCozyFurniture(type,true,p,-1,out var cozy))return cozy;
        var root = new GameObject("PetShopDetail_" + type + "_REPLACEABLE"); root.transform.position = p;
        switch (type)
        {
            case 0: // Food display — a wooden rack with two shelves of colored feed bags
                Deco(PrimitiveType.Cube, "Rack_Frame", p + new Vector3(0, .95f, 0), new Vector3(1.5f, 1.9f, .55f), wood, root.transform);
                for (int shelf = 0; shelf < 2; shelf++)
                {
                    float sy = .55f + shelf * .75f;
                    Deco(PrimitiveType.Cube, "Shelf", p + new Vector3(0, sy, 0), new Vector3(1.4f, .06f, .5f), trim, root.transform);
                    for (int i = 0; i < 3; i++)
                    {
                        Color bag = i == 0 ? new Color(.86f, .62f, .24f) : i == 1 ? new Color(.52f, .74f, .33f) : new Color(.74f, .34f, .30f);
                        Deco(PrimitiveType.Cube, "Feed_Bag", p + new Vector3(-.42f + i * .42f, sy + .18f, 0), new Vector3(.32f, .34f, .3f), Mat(bag), root.transform);
                    }
                }
                break;
            case 1: // Toy display — a pegboard with hanging balls and a bone
                Deco(PrimitiveType.Cube, "Pegboard", p + new Vector3(0, 1.3f, -.1f), new Vector3(1.6f, 1.5f, .1f), Mat(new Color(.90f, .85f, .72f)), root.transform);
                Color[] toyColors = { new Color(.92f, .35f, .34f), new Color(.30f, .62f, .92f), new Color(.95f, .80f, .25f), new Color(.45f, .80f, .45f) };
                for (int i = 0; i < 4; i++)
                    Deco(PrimitiveType.Sphere, "Toy_Ball", p + new Vector3(-.6f + i * .4f, 1.75f, .12f), Vector3.one * .22f, Mat(toyColors[i]), root.transform);
                var toyBone = Deco(PrimitiveType.Capsule, "Toy_Bone", p + new Vector3(0, .95f, .15f), new Vector3(.14f, .32f, .14f), Mat(new Color(.95f, .92f, .82f)), root.transform);
                toyBone.transform.localRotation = Quaternion.Euler(0, 0, 90);
                break;
            case 2: // Rat cage — wire-frame box on a stand with a small rat
                Deco(PrimitiveType.Cube, "Cage_Stand", p + new Vector3(0, .35f, 0), new Vector3(.9f, .7f, .7f), wood, root.transform);
                Material wire = Mat(new Color(.55f, .55f, .58f));
                Deco(PrimitiveType.Cube, "Cage_Base", p + new Vector3(0, .76f, 0), new Vector3(.85f, .08f, .65f), wire, root.transform);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 corner = new Vector3(i < 2 ? -.4f : .4f, 1.15f, i % 2 == 0 ? -.3f : .3f);
                    Deco(PrimitiveType.Cylinder, "Cage_Bar", p + corner, new Vector3(.025f, .4f, .025f), wire, root.transform);
                }
                Deco(PrimitiveType.Cube, "Cage_Top", p + new Vector3(0, 1.55f, 0), new Vector3(.85f, .06f, .65f), wire, root.transform);
                Material ratMat = Mat(new Color(.58f, .52f, .50f));
                Deco(PrimitiveType.Sphere, "Rat_Body", p + new Vector3(0, .95f, 0), new Vector3(.3f, .2f, .42f), ratMat, root.transform);
                Deco(PrimitiveType.Sphere, "Rat_Head", p + new Vector3(0, 1f, .22f), Vector3.one * .18f, ratMat, root.transform);
                Deco(PrimitiveType.Capsule, "Rat_Tail", p + new Vector3(0, .9f, -.32f), new Vector3(.03f, .18f, .03f), ratMat, root.transform);
                break;
            case 3: // Aquarium — a glass tank with tinted water and real fish models swimming in it
                // Each aquarium owns its fish; restoring furniture must not reset other tanks.
                Deco(PrimitiveType.Cube, "Tank_Stand", p + new Vector3(0, .4f, 0), new Vector3(1.7f, .8f, .8f), wood, root.transform);
                Deco(PrimitiveType.Cube, "Tank_Glass", p + new Vector3(0, 1.15f, 0), new Vector3(1.6f, .9f, .7f), TransparentPetTankMaterial(new Color(.75f,.92f,1f,.12f)), root.transform);
                Deco(PrimitiveType.Cube, "Tank_Water", p + new Vector3(0, 1.08f, 0), new Vector3(1.52f, .74f, .62f), TransparentPetTankMaterial(new Color(.25f, .62f, .78f, .13f)), root.transform);
                for (int i = 0; i < 3; i++)
                {
                    string fishPath = i == 0 ? "pets/fish_1" : AquariumFishResources[(i-1) % AquariumFishResources.Length];
                    GameObject fishPrefab = Resources.Load<GameObject>(fishPath);
                    Vector3 fp = p + new Vector3(-.45f + i * .45f, 1.1f + (i % 2 == 0 ? .08f : -.05f), (i % 2 == 0 ? .08f : -.08f));
                    if (fishPrefab != null)
                    {
                        var fishRoot = new GameObject("Aquarium_Fish");
                        fishRoot.transform.SetParent(root.transform, true);
                        fishRoot.transform.position = fp;
                        GameObject fi = Instantiate(fishPrefab, fishRoot.transform);
                        fi.name = "Model";
                        fi.transform.localPosition = Vector3.zero;
                        fi.transform.localRotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
                        foreach (var c in fi.GetComponentsInChildren<Collider>()) Destroy(c);
                        MarkNavMeshIgnore(fishRoot);
                        StartCoroutine(FitModelNextFrame(fi, fp.y, .22f));
                        if(!furniturePreviewBuilding){VillagerAnim fishAnim; SetupPetAnimation(fi, fishPath, out fishAnim);
                        tankFish.Add(new TankFish { root=fishRoot.transform,center=fp,phase=i*2.1f,animation=fishAnim });}
                    }
                    else Deco(PrimitiveType.Sphere, "Fish", fp, new Vector3(.12f, .08f, .2f), Mat(new Color(.95f, .55f, .2f)), root.transform);
                }
                break;
            case 4: // Dog cushion — a round cushion with a bone resting on it
                Deco(PrimitiveType.Cylinder, "Cushion", p + Vector3.up * .14f, new Vector3(.55f, .14f, .55f), Mat(new Color(.76f, .42f, .30f)), root.transform);
                var cushionBone = Deco(PrimitiveType.Capsule, "Cushion_Bone", p + new Vector3(0, .32f, 0), new Vector3(.08f, .2f, .08f), Mat(new Color(.95f, .92f, .82f)), root.transform);
                cushionBone.transform.localRotation = Quaternion.Euler(0, 0, 90);
                break;
            case 5: // Cat cushion — smaller cushion with a yarn ball
                Deco(PrimitiveType.Cylinder, "Cushion", p + Vector3.up * .11f, new Vector3(.4f, .11f, .4f), Mat(new Color(.55f, .58f, .82f)), root.transform);
                Deco(PrimitiveType.Sphere, "Yarn_Ball", p + new Vector3(.18f, .26f, .1f), Vector3.one * .15f, Mat(new Color(.90f, .62f, .68f)), root.transform);
                break;
            case 6: // Bird cage — hanging dome cage on a pole stand with a perch and a small bird
                Deco(PrimitiveType.Cylinder, "Cage_Pole", p + Vector3.up * .95f, new Vector3(.04f, .95f, .04f), Mat(new Color(.4f, .36f, .3f)), root.transform);
                Deco(PrimitiveType.Cylinder, "Cage_Base_Disc", p + Vector3.up * .04f, new Vector3(.3f, .04f, .3f), wood, root.transform);
                Material cageWire = Mat(new Color(.68f, .66f, .60f));
                Deco(PrimitiveType.Sphere, "Cage_Dome", p + Vector3.up * 1.95f, new Vector3(.46f, .5f, .46f), cageWire, root.transform);
                var perch = Deco(PrimitiveType.Cylinder, "Perch", p + Vector3.up * 1.82f, new Vector3(.28f, .015f, .015f), wood, root.transform);
                perch.transform.localRotation = Quaternion.Euler(0, 0, 90);
                Deco(PrimitiveType.Sphere, "Bird_Body", p + new Vector3(0, 1.95f, 0), new Vector3(.12f, .13f, .18f), Mat(new Color(.92f, .80f, .25f)), root.transform);
                break;
            case 7: // Cat tower: owned materials, explicit fallback for absent specialist model.
                Deco(PrimitiveType.Cube,"Tower_Base",p+Vector3.up*.08f,new Vector3(1.05f,.16f,1.05f),wood,root.transform);
                Deco(PrimitiveType.Cylinder,"Scratching_Post",p+new Vector3(-.2f,.8f,0),new Vector3(.18f,.72f,.18f),trim,root.transform);
                Deco(PrimitiveType.Cube,"Tower_Lower",p+new Vector3(.12f,.68f,.12f),new Vector3(.8f,.12f,.8f),wood,root.transform);
                Deco(PrimitiveType.Cube,"Tower_Upper",p+new Vector3(-.2f,1.55f,0),new Vector3(.72f,.12f,.72f),wood,root.transform);
                if(!furniturePreviewBuilding)StartCoroutine(AddFittedFurnitureObstacle(root));
                break;
            case 8:
            case 9:
                var resource=Resources.Load<GameObject>("Furniture/Generated/"+(type==8?"DesktopComputer":"FeedShelf"));
                if(resource==null){Destroy(root);throw new InvalidOperationException("기본 가구 프리팹이 없습니다.");}
                var model=Instantiate(resource,root.transform);model.name="Model";model.transform.localPosition=Vector3.zero;
                MarkNavMeshIgnore(root);if(!furniturePreviewBuilding)StartCoroutine(AddFittedFurnitureObstacle(root));
                break;

        }
        if (!furniturePreviewBuilding&&(type <= 3 || type == 6))
        {
            // Deco intentionally removes primitive colliders. Restore one simple SOLID
            // footprint for the player (NavMeshObstacle alone has no physics collision).
            var solid = root.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, type == 6 ? .9f : .75f, 0f);
            solid.size = type == 0 ? new Vector3(1.5f,1.5f,.55f) :
                         type == 1 ? new Vector3(1.6f,1.5f,.55f) :
                         type == 2 ? new Vector3(.9f,1.5f,.7f) :
                         type == 3 ? new Vector3(1.7f,1.5f,.8f) :
                                     new Vector3(.65f,1.8f,.65f);
            if(!furniturePreviewBuilding)StartCoroutine(AddFittedFurnitureObstacle(root));
        }
        if(!furniturePreviewBuilding){props.Add(root);RegisterFurniture(root,type,true);}return root;
    }
    // Keep the original visitor purchase opportunity, using the same individual inventory as manual adoption.
    void TryVillagerPetPurchase(GameObject visitor){TryProgressionVisitorAdoption(visitor);}
    // ============================================================================================
    // Modern uGUI HUD — replaces the old OnGUI-based HUD (DrawDesktopHUD/DrawMobileHUD, now dead
    // code, see OnGUI() above). OnGUI is Unity's original 1.0-era immediate-mode GUI system —
    // flat rectangles with no real animation or rounded shapes — which is exactly what read as
    // "20-year-old game" about this UI. This builds a real Canvas (uGUI) hierarchy once in
    // Start(), with rounded "card" panels (a single antialiased rounded-rect sprite generated at
    // runtime, 9-sliced so it works at any size without an external image file), soft drop
    // shadows, a bottom nav bar, a right-side action panel shared by Build/Decorate/Customize/
    // Shop (rebuilt only when its content actually needs to change, not every frame), a toast
    // notification that fades/slides instead of just appearing, and a small scale-punch on every
    // button press for a bit of game-y "juice". RefreshUI(), called once per Update(), keeps the
    // live numbers (coins/day/etc.) and selected-state highlighting in sync — it's cheap (only
    // text/color writes, no allocation) so running it every frame is fine.
    Font uiFontModern;
    Canvas uiCanvas; RectTransform uiRoot;
    Sprite uiRoundedSprite;
    readonly Color uiCream = new Color(1f, .98f, .93f, .97f);
    readonly Color uiCocoa = new Color(.30f, .20f, .13f, 1f);
    readonly Color uiMuted = new Color(.52f, .42f, .34f, 1f);
    readonly Color uiMint = new Color(.42f, .78f, .62f, 1f);
    readonly Color uiMintDark = new Color(.27f, .58f, .44f, 1f);
    readonly Color uiPeach = new Color(1f, .78f, .52f, 1f);
    readonly Color uiRed = new Color(.92f, .40f, .38f, 1f);
    readonly Color uiShadow = new Color(0f, 0f, 0f, .16f);
    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("PetTown_EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }
    Sprite RoundedSprite()
    {
        if (uiRoundedSprite != null) return uiRoundedSprite;
        const int size = 48, radius = 16;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float cx = Mathf.Clamp(x, radius, size - 1 - radius);
            float cy = Mathf.Clamp(y, radius, size - 1 - radius);
            float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            float a = Mathf.Clamp01(radius - dist + 0.5f);
            px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255));
        }
        tex.SetPixels32(px); tex.Apply();
        uiRoundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return uiRoundedSprite;
    }
    RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot; rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
    }
    Image NewCard(string name, Transform parent, Color color)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = RoundedSprite(); img.type = Image.Type.Sliced; img.color = color;
        return img;
    }
    // Shadow sibling added first (renders behind), then the real card on top of it.
    Image NewCardWithShadow(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        var shadow = NewCard(name + "_Shadow", parent, uiShadow);
        Anchor(shadow.rectTransform, anchorMin, anchorMax, pivot, anchoredPos + new Vector2(3, -4), size);
        var card = NewCard(name, parent, color);
        Anchor(card.rectTransform, anchorMin, anchorMax, pivot, anchoredPos, size);
        return card;
    }
    Text NewText(string name, Transform parent, string txt, int size, Color color, TextAnchor anchor, FontStyle style = FontStyle.Bold)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.text = txt; t.font = uiFontModern; t.fontSize = size; t.color = color; t.alignment = anchor; t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }
    Button NewButton(string name, Transform parent, Image onImage)
    {
        var btn = onImage.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None; // colors are driven by RefreshUI()/RebuildActionPanel() instead
        btn.targetGraphic = onImage;
        btn.onClick.AddListener(() => StartCoroutine(PunchScale(onImage.rectTransform)));
        return btn;
    }
    // A tiny scale-punch on press — the one-line difference between a flat clickable rectangle
    // and a button that feels like it's actually part of a game.
    IEnumerator PunchScale(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector3 baseScale = Vector3.one;
        rt.localScale = baseScale * .92f;
        float t = 0f;
        while (t < 1f && rt != null) { t += Time.deltaTime * 9f; rt.localScale = Vector3.Lerp(baseScale * .92f, baseScale, t); yield return null; }
        if (rt != null) rt.localScale = baseScale;
    }

    // -- Status bar (top-left): title/day/population/fame card + a coin pill --------------------
    Text uiTitleText, uiSubText, uiCoinText; RawImage uiPortraitRaw;
    void BuildStatusBar()
    {
        var main = NewCardWithShadow("StatusMain", uiRoot, uiCream, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(338, 70));
        var portraitGO = NewRect("Portrait", main.transform);
        uiPortraitRaw = portraitGO.gameObject.AddComponent<RawImage>();
        uiPortraitRaw.texture = heroPortraitTex; // may already be generated if RebuildHero() ran before BuildUI()
        Anchor(portraitGO, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(46, 46));
        uiTitleText = NewText("StatusTitle", main.transform, "PET TOWN 3D", 19, uiCocoa, TextAnchor.UpperLeft);
        Anchor(uiTitleText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -12), new Vector2(250, 24));
        uiSubText = NewText("StatusSub", main.transform, "", 14, uiMuted, TextAnchor.UpperLeft, FontStyle.Normal);
        Anchor(uiSubText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -40), new Vector2(250, 22));
        var coin = NewCardWithShadow("CoinChip", uiRoot, uiCocoa, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(374, -24), new Vector2(148, 46));
        uiCoinText = NewText("CoinText", coin.transform, "", 18, uiPeach, TextAnchor.MiddleCenter);
        Anchor(uiCoinText.rectTransform, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
    }

    // -- Bottom nav bar: Explore / Build / Decorate / Customize / Shop --------------------------
    readonly List<Image> uiNavButtons = new List<Image>();
    readonly List<Text> uiNavLabels = new List<Text>();
    void BuildNavBar()
    {
        var bar = NewCardWithShadow("NavBar", uiRoot, uiCream, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(560, 62));
        string[] labels = { "탐험", "건설", "꾸미기", "커스텀", "상점" };
        float bw = 104, gap = 6, startX = -((labels.Length - 1) * (bw + gap)) / 2f;
        for (int i = 0; i < labels.Length; i++)
        {
            int idx = i;
            var btnImg = NewCard("Nav_" + idx, bar.transform, uiMint);
            Anchor(btnImg.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(startX + idx * (bw + gap), 0), new Vector2(bw, 48));
            var lbl = NewText("NavLabel_" + idx, btnImg.transform, labels[idx], 15, Color.white, TextAnchor.MiddleCenter);
            Anchor(lbl.rectTransform, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            NewButton("btn", btnImg.transform, btnImg).onClick.AddListener(() => OnNavClick(idx));
            uiNavButtons.Add(btnImg); uiNavLabels.Add(lbl);
        }
    }
    void OnNavClick(int idx)
    {
        if(decorSelected!=-1)ClearDecorSelection();
        if(idx==RoadButtonRoute){OpenRoadChooser();return;}
        if(idx==BuyPetButtonRoute){OpenPetOrder(-1);return;}
        if(idx>=PetTownCozyHudView.ShortcutRouteBase){OpenFacilityShortcut(idx);return;}
        if(idx>=5){OpenCozyInformation(idx);return;}
        if(idx==3&&PetTownCozyLibrary.Current!=null){SetMode(Mode.Play);FindFirstObjectByType<PetTownCustomizationUI>()?.OpenFromHud();return;}
        if (idx == 0) SetMode(Mode.Play);
        else if (idx == 1) { SetMode(Mode.Build); if(useNewConstructionCatalog)OpenConstructionCatalog(); }
        else if (idx == 2) { SetMode(Mode.Furniture); if(useNewFurnitureCatalog)OpenFurnitureCatalog(); }
        else if (idx == 3) SetMode(Mode.Customize);
        else
        {
            if(uiShopOpen&&CatalogOpen){catalogWindow.Hide();uiShopOpen=false;}
            else{uiShopOpen=!uiShopOpen;if(uiShopOpen){SetMode(Mode.Play);if(useNewManagementUI)OpenShopManagement();}}
        }
        uiActionBuiltFor = -999; // force the action panel to rebuild for the new tab
    }

    // -- Right-side action panel: one reusable list used by Build/Decorate/Customize/Shop -------
    class UIEntry { public string label; public string sub; public Texture2D icon; public Func<bool> selected; public Action onClick; }
    RectTransform uiActionPanelRoot; Text uiActionTitle;
    readonly List<GameObject> uiActionButtons = new List<GameObject>();
    int uiActionBuiltFor = -2; // -2 = never built; tracks (mode,furnitureChoice,selected,shopOpen) signature
    bool uiShopOpen;
    List<PetTownCatalogEntry> ConstructionCatalogEntries()
    {
        string[] labels={"펫샵","집","주민 주택","펫 카페","펫 병원","공원","호수","동물원","흙길","농장","목장"};
        string[] descriptions={"","실내를 자유롭게 꾸밀 수 있는 집입니다.","주민이 머무는 주택입니다.","마을의 펫 카페입니다.","마을의 펫 병원입니다.","분수·벤치·산책로가 있는 공원입니다. 주민이 실제로 이용하면 행복도가 올라갑니다.","물가를 산책하고 쉴 수 있는 호수입니다. 수면 안쪽은 들어갈 수 없어요.","사슴·호랑이·펭귄을 맞이하고 먹이와 우리 청결을 관리합니다. 동물은 별도 골드로 맞이합니다.","드래그로 만드세요. 자유 곡선과 격자 직선 모드를 선택할 수 있어요.","밭 9칸에서 양배추와 토마토를 기릅니다. 씨앗 구매 → 심기 → 매일 물주기 → 수확 → 판매 또는 카페 재료로 사용합니다.","젖소·양·돼지를 키워 우유·양털·고기를 생산합니다. 성체 돼지를 출하하면 해당 개체가 목장을 떠나고 고기3개를 얻습니다. 가축은 별도로 맞이합니다."};
        var result=new List<PetTownCatalogEntry>();
        for(int i=1;i<types.Length;i++)
        {
            int kind=i;
            if(i==8)continue; // roads have their own button now
            result.Add(new PetTownCatalogEntry{Key=i,Name=labels[i],Price=i==8?RoadCostPerMeter:costs[i],PriceUnit=i==8?"/m":null,
                Category=i==9||i==10?"농업":i==8?"도로":i==1||i==2?"주거":i==3||i==4?"생활":"야외",Description=descriptions[i],
                Footprint=i==8?Vector2.zero:DefinitionFor(i).footprintMeters,
                Thumbnail=i==10?PetTownRanchLibrary.Current?.thumbnail:i==9?PetTownFarmLibrary.Current?.thumbnail:i>=5&&i<=7?PetTownCommunityLibrary.Current?.thumbnails[i-5]:null,
                ThumbnailProvider=i==2?(Func<Texture2D>)(()=>GetItemIcon(MedievalHouseResources[0])):null});
        }
        result.Add(new PetTownCatalogEntry{Key=DecorEntryKey,Name="마을 꾸미기",Category="꾸미기",TextOnlyDetail=true,IconKey=3,PriceLabel="나무·꽃·장식·상점",FootprintLabel="탭해서 하나씩 놓기",ActionLabel="열기",Description="나무와 꽃을 심고 분수·벤치·기념탑·편의점·학교 같은 것을 놓아요.\n많이 놓을수록 주변 주민의 행복도가 올라가요."});
        return result;
    }
    void OpenConstructionCatalog()
    {
        if(catalogWindow==null||!catalogWindow.Ready){useNewConstructionCatalog=false;uiActionBuiltFor=-999;return;}
        uiShopOpen=false;selected=-1;ClearGhost();if(constructionGridOverlay!=null)constructionGridOverlay.Hide();
        catalogWindow.Show("건물 목록",ConstructionCatalogEntries(),entry=>
        {
            if(entry.Key==DecorEntryKey){OpenDecorChooser();return;}
            selected=entry.Key;uiShopOpen=false;uiActionBuiltFor=-999;ghostBuildFor=-1;
        },()=>SetMode(Mode.Play),()=>{useNewConstructionCatalog=false;uiActionBuiltFor=-999;});
        uiActionBuiltFor=-999;
    }
    void OpenFurnitureCatalog()
    {
        if(OpenCozyFurnitureCatalog())return;
        if(catalogWindow==null||!catalogWindow.Ready){useNewFurnitureCatalog=false;uiActionBuiltFor=-999;return;}
        uiShopOpen=false;ClearGhost();var entries=new List<PetTownCatalogEntry>();
        string[] labels={"계산대","펫 쿠션","진열대","의자","테이블","화분"};
        string[] categories={"진열","휴식","진열","좌석","좌석","장식"};
        for(int i=0;i<FurnitureResource.Length;i++)
        {
            int kind=i;entries.Add(new PetTownCatalogEntry{Key=i,Name=labels[i],Category=categories[i],Price=50,Footprint=PetTownFurniturePlanner.ReservedSize(FurnitureSize(i),furnitureGridSize),Description="실내 격자에 맞춰 배치합니다. R키로 90°씩 회전할 수 있어요.",ThumbnailProvider=()=>GetItemIcon(FurnitureResource[kind])});
        }
        string[] detailNames={"장난감 진열대","RAT 케이지","수족관","강아지 쿠션","새장","고양이 펫타워","컴퓨터","사료 진열대"};
        int[] detailKinds={1,2,3,4,6,7,8,9};
        Vector2[] detailSizes={new Vector2(1.6f,.6f),new Vector2(.9f,.7f),new Vector2(1.8f,.9f),new Vector2(.7f,.7f),new Vector2(.7f,.7f),new Vector2(1.1f,1.1f),new Vector2(.9f,.65f),new Vector2(1.5f,1.3f)};
        for(int i=0;i<detailNames.Length;i++){int detailKind=detailKinds[i];entries.Add(new PetTownCatalogEntry{Key=6+detailKind,Name=detailNames[i],Category=detailKind==8?"업무":"펫 시설",Price=50,Footprint=PetTownFurniturePlanner.ReservedSize(detailSizes[i],furnitureGridSize),Description=detailKind==8?"계산대나 테이블 위에 격자로 배치합니다. R키로 90° 회전합니다.":"실내 격자에 맞춰 배치합니다. R키로 90° 회전합니다.",ThumbnailProvider=detailKind==8?()=>GetItemIcon("Furniture/Generated/DesktopComputer"):detailKind==9?()=>GetItemIcon("Furniture/Generated/FeedShelf"):(Func<Texture2D>)null});}
        catalogWindow.Show("가구 목록",entries,entry=>{furnitureChoice=entry.Key;furnitureQuarter=0;ghostFurnitureFor=-1;furnitureGhostReady=false;furnitureCached=false;uiActionBuiltFor=-999;},()=>SetMode(Mode.Play),()=>{useNewFurnitureCatalog=false;uiActionBuiltFor=-999;});uiActionBuiltFor=-999;
    }
    void OpenShopManagement()
    {
        if(catalogWindow==null||!catalogWindow.Ready){useNewManagementUI=false;uiActionBuiltFor=-999;return;}
        uiShopOpen=true;var entries=new List<PetTownCatalogEntry>();
        entries.Add(new PetTownCatalogEntry{Key=0,Name="강아지 주문",Category="펫 재고",Price=120,PriceLabel="70 골드 · 직접 고르기",FootprintLabel="",ActionLabel="펫 고르기",Description="현재 강아지 재고: "+dogStock+"마리"});
        entries.Add(new PetTownCatalogEntry{Key=1,Name="고양이 주문",Category="펫 재고",Price=120,PriceLabel="70 골드 · 직접 고르기",FootprintLabel="",ActionLabel="펫 고르기",Description="현재 고양이 재고: "+catStock+"마리"});
        if(shopLevel>=2)entries.Add(new PetTownCatalogEntry{Key=2,Name="토끼 주문",Category="펫 재고",Price=180,PriceLabel="100 골드 · 직접 고르기",FootprintLabel="",ActionLabel="펫 고르기",Description="현재 토끼 재고: "+rabbitStock+"마리"});
        entries.Add(new PetTownCatalogEntry{Key=10,Name="지붕 보기",Category="매장 관리",PriceLabel="",FootprintLabel="",ActionLabel="전환하기",Description="매장 지붕의 표시 상태를 바꿉니다."});
        entries.Add(new PetTownCatalogEntry{Key=14,Name="환경 스타일 전환",Category="매장 관리",PriceLabel="기존 / 새 자연환경",FootprintLabel="",ActionLabel="전환하기",Description="지형·충돌·강 경로를 유지하며 환경 렌더링만 전환합니다."});
        entries.Add(new PetTownCatalogEntry{Key=13,Name="증축 주변 자연물 정리",Category="매장 관리",PriceLabel="선택한 자연물만 제거",FootprintLabel="",ActionLabel="목록 열기",Description="증축은 자연물을 자동 삭제하지 않습니다. 방해되는 자연물을 이 목록에서 개별 확인 후 제거할 수 있습니다."});
        entries.Add(new PetTownCatalogEntry{Key=12,Name="증축하기",Category="매장 관리",PriceLabel="방향별 면적·비용 확인",FootprintLabel="",ActionLabel="방향 선택",Description="기존 가구를 유지하며 3m 모듈 단위로 실내를 늘립니다. 부지 검사 후 골드를 지불합니다."});
        entries.Add(new PetTownCatalogEntry{Key=11,Name="저장하기",Category="매장 관리",PriceLabel="",FootprintLabel="",ActionLabel="저장하기",Description="현재 마을과 가구 배치를 저장합니다."});
        AppendOperationsMenu(entries);
        catalogWindow.Show("펫샵 관리",entries,entry=>
        {
            if(entry.Key>=20){uiShopOpen=false;if(entry.Key==20)OpenEmployees();else if(entry.Key==21)OpenGroomingStations();else if(entry.Key==22)OpenDeliveries();else if(entry.Key==23)OpenDailyQuests();else if(entry.Key==24)OpenPetEncyclopedia();else if(entry.Key==25)OpenPetPlaySelection();else if(entry.Key==26)OpenBreeding();else if(entry.Key==27)OpenFacilities();else if(entry.Key==28)OpenFarmManagement();else if(entry.Key==29)OpenCommunityFacilities();else if(entry.Key==30)OpenRanches();else if(entry.Key==31)OpenPetSlots();return;}
            if(entry.Key==0)BuyPets(0,120);else if(entry.Key==1)BuyPets(1,120);else if(entry.Key==2)BuyPets(2,180);else if(entry.Key==10)ToggleMobileRoof();else if(entry.Key==11)SaveGame();else if(entry.Key==12)OpenExpansion();else if(entry.Key==13)OpenNatureClearing();else if(entry.Key==14)ToggleEnvironmentStyle();
            uiShopOpen=false;uiActionBuiltFor=-999;
        },()=>{uiShopOpen=false;uiActionBuiltFor=-999;},()=>{useNewManagementUI=false;uiActionBuiltFor=-999;});uiActionBuiltFor=-999;
    }
    void BuildActionPanel()
    {
        var panel = NewCardWithShadow("ActionPanel", uiRoot, uiCream, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-24, 0), new Vector2(300, 440));
        uiActionPanelRoot = panel.rectTransform;
        uiActionTitle = NewText("ActionTitle", panel.transform, "", 18, uiCocoa, TextAnchor.UpperLeft);
        Anchor(uiActionTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(-32, 26));
        uiActionTitle.alignment = TextAnchor.MiddleCenter;
    }
    List<UIEntry> BuildEntriesForCurrentMode(out string title)
    {
        var entries = new List<UIEntry>();
        if (uiShopOpen)
        {
            title = "PET SHOP";
            entries.Add(new UIEntry { label = "강아지 주문", sub = "-70", selected = () => false, onClick = () => BuyPets(0, 120) });
            entries.Add(new UIEntry { label = "고양이 주문", sub = "-70", selected = () => false, onClick = () => BuyPets(1, 120) });
            if (shopLevel >= 2) entries.Add(new UIEntry { label = "토끼 주문", sub = "-100", selected = () => false, onClick = () => BuyPets(2, 180) });
            entries.Add(new UIEntry { label = "지붕 보기 전환", sub = "", selected = () => false, onClick = ToggleMobileRoof });
            entries.Add(new UIEntry { label = "저장하기", sub = "", selected = () => false, onClick = SaveGame });
            entries.Add(new UIEntry { label = "닫기", sub = "", selected = () => false, onClick = () => { uiShopOpen = false; uiActionBuiltFor = -999; } });
            return entries;
        }
        if (mode == Mode.Build)
        {
            title = "BUILDINGS";
            for (int i = 1; i < types.Length; i++)
            {
                int idx = i;
                entries.Add(new UIEntry { label = types[idx], sub = costs[idx].ToString(), selected = () => selected == idx, onClick = () => { selected = idx; uiActionBuiltFor = -999; } });
            }
            return entries;
        }
        if (mode == Mode.Furniture)
        {
            title = "DECORATE";
            for (int i = 0; i < furniture.Length; i++)
            {
                int idx = i;
                entries.Add(new UIEntry { label = furniture[idx], sub = "50", icon = GetItemIcon(FurnitureResource[idx]), selected = () => furnitureChoice == idx, onClick = () => { furnitureChoice = idx; uiActionBuiltFor = -999; } });
            }
            return entries;
        }
        if (mode == Mode.Customize)
        {
            title = "CHARACTER";
            entries.Add(new UIEntry { label = "머리  " + hairStyle, selected = () => false, onClick = () => { hairStyle = (hairStyle + 1) % 4; uiActionBuiltFor = -999; } });
            entries.Add(new UIEntry { label = "얼굴  " + faceStyle, selected = () => false, onClick = () => { faceStyle = (faceStyle + 1) % 4; uiActionBuiltFor = -999; } });
            entries.Add(new UIEntry { label = "몸통  " + bodyStyle, selected = () => false, onClick = () => { bodyStyle = (bodyStyle + 1) % 4; uiActionBuiltFor = -999; } });
            entries.Add(new UIEntry { label = "다리  " + legStyle, selected = () => false, onClick = () => { legStyle = (legStyle + 1) % 4; uiActionBuiltFor = -999; } });
            entries.Add(new UIEntry { label = "액세서리  " + accessory, selected = () => false, onClick = () => { accessory = (accessory + 1) % 3; uiActionBuiltFor = -999; } });
            entries.Add(new UIEntry { label = "적용하기", selected = () => false, onClick = () => { RebuildHero(); SaveGame(); SetMode(Mode.Play); } });
            return entries;
        }
        title = "";
        return entries;
    }
    void RebuildActionPanel()
    {
        foreach (var go in uiActionButtons) if (go != null) Destroy(go);
        uiActionButtons.Clear();
        bool show = !CatalogOpen&&((mode == Mode.Build && !useNewConstructionCatalog) || (mode == Mode.Furniture && !useNewFurnitureCatalog) || mode == Mode.Customize || uiShopOpen);
        uiActionPanelRoot.gameObject.SetActive(show);
        uiActionPanelRoot.parent.Find("ActionPanel_Shadow")?.gameObject.SetActive(show);
        if (!show) return;
        var entries = BuildEntriesForCurrentMode(out string title);
        uiActionTitle.text = title;
        float bh = 52, gap = 8, y = -48;
        float panelH = Mathf.Clamp(48 + entries.Count * (bh + gap) + 16, 120, 560);
        uiActionPanelRoot.sizeDelta = new Vector2(300, panelH);
        (uiActionPanelRoot.parent.Find("ActionPanel_Shadow") as RectTransform)?.SetAsFirstSibling();
        var shadowRt = uiActionPanelRoot.parent.Find("ActionPanel_Shadow") as RectTransform;
        if (shadowRt != null) shadowRt.sizeDelta = new Vector2(300, panelH);
        foreach (var e in entries)
        {
            var entry = e;
            bool isSelected = entry.selected != null && entry.selected();
            var card = NewCard("ActionBtn", uiActionPanelRoot, isSelected ? uiPeach : uiCream);
            Anchor(card.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, y), new Vector2(-24, bh));
            if (entry.icon != null)
            {
                var iconGO = NewRect("Icon", card.transform);
                var raw = iconGO.gameObject.AddComponent<RawImage>();
                raw.texture = entry.icon;
                Anchor(iconGO, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(8, 0), new Vector2(bh - 10, bh - 10));
            }
            float textX = entry.icon != null ? bh + 4 : 16;
            var lbl = NewText("Label", card.transform, entry.label, 15, uiCocoa, TextAnchor.MiddleLeft, FontStyle.Bold);
            Anchor(lbl.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, .5f), new Vector2(textX, 0), new Vector2(-textX - 54, 0));
            if (!string.IsNullOrEmpty(entry.sub))
            {
                var sub = NewText("Price", card.transform, entry.sub, 14, uiMintDark, TextAnchor.MiddleRight);
                Anchor(sub.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, .5f), new Vector2(-14, 0), new Vector2(60, 0));
            }
            NewButton("btn", card.transform, card).onClick.AddListener(() => entry.onClick());
            uiActionButtons.Add(card.gameObject);
            y -= bh + gap;
        }
    }

    // -- Toast notification: fades + slides instead of just appearing ---------------------------
    CanvasGroup uiToastGroup; Text uiToastText;
    void BuildToast()
    {
        var card = NewCard("Toast", uiRoot, uiCocoa);
        Anchor(card.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 94), new Vector2(460, 48));
        uiToastGroup = card.gameObject.AddComponent<CanvasGroup>();
        uiToastGroup.alpha = 0f;
        uiToastText = NewText("ToastText", card.transform, "", 15, uiPeach, TextAnchor.MiddleCenter, FontStyle.Normal);
        Anchor(uiToastText.rectTransform, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-20, 0));
    }

    // -- Customer order card (top-center, only while a customer is waiting) ---------------------
    GameObject uiOrderCardRoot; Text uiOrderText; Image uiOrderCard;
    void BuildOrderCard()
    {
        var card = NewCardWithShadow("OrderCard", uiRoot, uiPeach, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(320, 64));
        uiOrderCard = card; uiOrderCardRoot = card.transform.parent.Find("OrderCard_Shadow")?.gameObject;
        uiOrderText = NewText("OrderText", card.transform, "", 15, uiCocoa, TextAnchor.MiddleCenter, FontStyle.Bold);
        Anchor(uiOrderText.rectTransform, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-16, -8));
        NewButton("btn", card.transform, card).onClick.AddListener(() => { if (orderPet >= 0) ServeOrder(); });
        card.gameObject.SetActive(false);
    }
    [SerializeField] bool useKenneyHud=true;
    PetTownHud kenneyHud;
    void SetLegacyHudVisible(bool visible)
    {
        string[] names={"StatusMain","StatusMain_Shadow","CoinChip","CoinChip_Shadow","NavBar","NavBar_Shadow","Toast","OrderCard","OrderCard_Shadow"};
        foreach(var name in names){var child=uiRoot.Find(name);if(child!=null)child.gameObject.SetActive(visible);}
    }
    void BuildUI()
    {
        EnsureEventSystem();
        uiFontModern = Resources.Load<Font>("Fonts/Jua-Regular") ?? Resources.Load<Font>("Fonts/NanumSquareB");
        if (uiFontModern == null) uiFontModern = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvasGO = new GameObject("PetTown_ModernCanvas");
        uiCanvas = canvasGO.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        uiRoot = canvasGO.GetComponent<RectTransform>();
        BuildStatusBar();
        BuildNavBar();
        BuildActionPanel();
        BuildToast();
        BuildOrderCard();
        uiPlacementHint=NewText("PlacementDirectionHint",uiRoot,"",14,uiCocoa,TextAnchor.UpperLeft);
        uiPlacementHint.raycastTarget=false;
        var tool=NewCard("ConstructionTools",uiRoot,uiCream);uiConstructionTools=tool.gameObject;
        Anchor(tool.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),new Vector2(24,useKenneyHud?-278:-170),new Vector2(280,40));
        uiConstructionToolLabel=NewText("ToolLabel",tool.transform,"",14,uiCocoa,TextAnchor.MiddleCenter);
        uiConstructionToolLabel.raycastTarget=false;Anchor(uiConstructionToolLabel.rectTransform,Vector2.zero,Vector2.one,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
        NewButton("btn",tool.transform,tool).onClick.AddListener(()=>
        {
            if(mode==Mode.Furniture){RotateFurnitureClockwise();return;}
            if(IsRoadConstructionActive)
            {
                if(roadDragging)return;
                roadMode=roadMode==PetTownRoadMode.FreeCurve?PetTownRoadMode.GridStraight:PetTownRoadMode.FreeCurve;StopRoadPreview();
            }
            else{constructionRoadAlignment=!constructionRoadAlignment;placementValidator.Invalidate();}
        });
        Anchor(uiPlacementHint.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),new Vector2(24,useKenneyHud?-216:-110),new Vector2(700,56));
        catalogWindow=uiRoot.gameObject.AddComponent<PetTownCatalogWindow>();catalogWindow.Initialize(uiRoot);
        var launcher=NewCard("CatalogLauncher",uiRoot,uiMintDark);uiCatalogLauncher=launcher.gameObject;
        Anchor(launcher.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),new Vector2(24,useKenneyHud?-324:-216),new Vector2(180,38));
        var launcherLabel=NewText("Label",launcher.transform,"건물 목록",15,Color.white,TextAnchor.MiddleCenter);
        Anchor(launcherLabel.rectTransform,Vector2.zero,Vector2.one,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);launcherLabel.raycastTarget=false;
        NewButton("btn",launcher.transform,launcher).onClick.AddListener(()=>{if(mode==Mode.Furniture){useNewFurnitureCatalog=true;OpenFurnitureCatalog();}else{useNewConstructionCatalog=true;OpenConstructionCatalog();}});
        if(useKenneyHud){kenneyHud=uiRoot.gameObject.AddComponent<PetTownHud>();kenneyHud.Initialize(uiRoot,OnNavClick,SaveGame,ToggleMobileRoof,()=>{if(orderPet>=0)ServeOrder();},SetLegacyHudVisible);}

    }
    // Called once per Update() — cheap (text/color writes only), keeps the live numbers and
    // selected-state highlighting in sync, rebuilds the action panel only when something about
    // its content actually changed (mode/tab/selection), and drives the toast fade/slide.
    void RefreshUI()
    {
        if (uiCanvas == null) return;
        if(uiPlacementHint!=null)
        {
            uiPlacementHint.gameObject.SetActive((UsesBuildingGrid||mode==Mode.Furniture)&&!CatalogOpen);
            if(mode==Mode.Furniture)
            {
                uiPlacementHint.text="R: 90° 회전  ·  실내 격자 "+furnitureGridSize+"m  ·  "+(furnitureQuarter*90)+"°\n"+(furnitureValidation.Message??"실내 배치 위치를 선택해 주세요.");
                uiPlacementHint.color=furnitureValidation.CanPlace?uiMintDark:uiRed;
            }
            else if(UsesBuildingGrid)
            {
                string direction=placementPlanner!=null&&placementPlanner.Kind>=0?(placementPlanner.FacingQuarter*90)+"° "+(placementPlanner.ManualRotation?"수동":placementPlanner.HasRoadSuggestion?"도로 방향 추천":"기본 방향"):"";
                uiPlacementHint.text="R: 90° 회전  ·  화살표: 출입구  ·  "+direction+"\n"+(placementValidation.Message??"설치 위치를 선택해 주세요.");
                uiPlacementHint.color=placementValidation.CanPlace?uiMintDark:placementValidation.NeedsGrading?new Color(.80f,.40f,.02f):uiRed;
            }
        }
        if(uiConstructionTools!=null)
        {
            uiConstructionTools.SetActive((UsesBuildingGrid||IsRoadConstructionActive||mode==Mode.Furniture)&&!CatalogOpen);
            uiConstructionToolLabel.text=mode==Mode.Furniture?"가구 회전: "+(furnitureQuarter*90)+"° (R)":IsRoadConstructionActive?(roadMode==PetTownRoadMode.FreeCurve?"도로: 자유 곡선 (클릭하여 전환)":"도로: 격자 직선 (클릭하여 전환)"):"도로 줄 정렬: "+(constructionRoadAlignment?"켜짐":"꺼짐");
        }
        if(uiCatalogLauncher!=null)
        {
            uiCatalogLauncher.SetActive((mode==Mode.Build||mode==Mode.Furniture)&&!CatalogOpen);
            uiCatalogLauncher.GetComponentInChildren<Text>().text=mode==Mode.Furniture?"가구 목록":"건물 목록";
        }
        uiTitleText.text = shop == null ? "PET TOWN 3D" : "PET TOWN 3D";
        uiSubText.text = "Day " + day + "  ·  주민 " + population + "  ·  명성 " + reputation;
        uiCoinText.text = "●  " + money.ToString("N0");
        for (int i = 0; i < uiNavButtons.Count; i++)
        {
            bool active = (i == 0 && mode == Mode.Play && !uiShopOpen) || (i == 1 && mode == Mode.Build) || (i == 2 && mode == Mode.Furniture) || (i == 3 && mode == Mode.Customize) || (i == 4 && uiShopOpen);
            uiNavButtons[i].color = active ? uiMintDark : uiMint;
        }
        // furnitureChoice/selected are part of this signature, so any change to which item is
        // selected already forces a full RebuildActionPanel() below — no need to additionally
        // re-evaluate every entry's "selected" state every single frame (that would mean
        // reallocating the whole entries list + its closures 60 times a second for nothing).
        int sig = (uiShopOpen ? 1000 : 0) + (int)mode * 100 + furnitureChoice * 10 + selected;
        if (sig != uiActionBuiltFor) { RebuildActionPanel(); uiActionBuiltFor = sig; }
        if (Time.time < noticeTime)
        {
            uiToastText.text = notice;
            uiToastGroup.alpha = Mathf.Lerp(uiToastGroup.alpha, 1f, Time.deltaTime * 10f);
        }
        else uiToastGroup.alpha = Mathf.Lerp(uiToastGroup.alpha, 0f, Time.deltaTime * 6f);
        bool hasOrder = orderPet >= 0;
        uiOrderCard.gameObject.SetActive(hasOrder);
        if (uiOrderCardRoot != null) uiOrderCardRoot.SetActive(hasOrder);
        if (hasOrder) uiOrderText.text = "손님이 " + petNames[orderPet] + "를 찾고 있어요!  (" + Mathf.CeilToInt(orderDeadline - Time.time) + "s)  — 눌러서 분양 선택 [E]";
        if(kenneyHud!=null&&kenneyHud.Modern)
        {
            int active=PetCareOpen?7:uiShopOpen?4:mode==Mode.Build?1:mode==Mode.Furniture?2:mode==Mode.Customize?3:0;
            kenneyHud.SetState(day,population,reputation,money,heroPortraitTex,notice,Time.time<noticeTime,hasOrder?uiOrderText.text:"",hasOrder,active);SetLegacyHudVisible(false);
        }

    }
    void CreateHero(Vector3 p)
    {
        hero=new GameObject("PLAYER_Customizable_3D");hero.transform.position=new Vector3(p.x,Ground(p.x,p.z),p.z);
        RebuildHero();
    }
    // All body components are child objects and can be replaced with Meshy meshes later.
    Transform Joint(string name,Vector3 local,Transform parent)
    {
        var joint=new GameObject(name).transform;
        joint.SetParent(parent,false);joint.localPosition=local;
        return joint;
    }
    GameObject LocalPart(PrimitiveType type,string name,Transform parent,Vector3 pos,Vector3 size,Material material)
    {
        GameObject item=Part(type,name,parent.position, size,material,parent);
        item.transform.localPosition=pos;item.transform.localRotation=Quaternion.identity;
        Collider col=item.GetComponent<Collider>();if(col!=null)Destroy(col);
        return item;
    }
    void BuildPerson(Transform root,bool player,int variant)
    {
        Material outfit=player?shirt:Mat(Color.HSVToRGB((variant*.17f+.1f)%1f,.43f,.85f));
        var torso=LocalPart(PrimitiveType.Capsule,"BODY_REPLACEABLE",root,new Vector3(0,1.53f,0),new Vector3(.92f,1.04f,.68f),outfit);
        torso.transform.localScale*=1f+(player?bodyStyle*.08f:0);
        LocalPart(PrimitiveType.Sphere,"NECK",root,new Vector3(0,2.10f,0),new Vector3(.29f,.29f,.27f),skin);
        LocalPart(PrimitiveType.Sphere,"FACE_REPLACEABLE",root,new Vector3(0,2.68f,0),new Vector3(1.23f,1.16f,1.09f),skin);
        LocalPart(PrimitiveType.Sphere,"HAIR_REPLACEABLE",root,new Vector3(0,3.12f,.04f),new Vector3(1.27f,.48f+(player?hairStyle*.1f:0),1.12f),player?hairMat:dark);
        LocalPart(PrimitiveType.Sphere,"Hair_Fringe",root,new Vector3(0,2.95f,-.48f),new Vector3(1.03f,.24f,.28f),player?hairMat:dark);
        for(int sign=-1;sign<=1;sign+=2)
        {
            LocalPart(PrimitiveType.Sphere,"Eye",root,new Vector3(sign*.245f,2.69f,-.514f),new Vector3(.145f,.19f,.10f),dark);
            LocalPart(PrimitiveType.Sphere,"Eye_Shine",root,new Vector3(sign*.27f,2.76f,-.59f),new Vector3(.05f,.06f,.03f),trim);
            LocalPart(PrimitiveType.Sphere,"Cheek",root,new Vector3(sign*.44f,2.49f,-.43f),new Vector3(.19f,.09f,.04f),blush);
        }
        LocalPart(PrimitiveType.Sphere,"Nose",root,new Vector3(0,2.52f,-.552f),new Vector3(.095f,.075f,.055f),gold);
        Transform leftArm=Joint("LEFT_ARM_PIVOT",new Vector3(-.57f,1.98f,0),root);
        Transform rightArm=Joint("RIGHT_ARM_PIVOT",new Vector3(.57f,1.98f,0),root);
        LocalPart(PrimitiveType.Capsule,"LEFT_ARM_REPLACEABLE",leftArm,new Vector3(-.13f,-.46f,0),new Vector3(.33f,.61f,.36f),outfit);
        LocalPart(PrimitiveType.Capsule,"RIGHT_ARM_REPLACEABLE",rightArm,new Vector3(.13f,-.46f,0),new Vector3(.33f,.61f,.36f),outfit);
        LocalPart(PrimitiveType.Sphere,"Left_Hand",leftArm,new Vector3(-.15f,-.91f,0),new Vector3(.32f,.30f,.31f),skin);
        LocalPart(PrimitiveType.Sphere,"Right_Hand",rightArm,new Vector3(.15f,-.91f,0),new Vector3(.32f,.30f,.31f),skin);
        Transform leftLeg=Joint("LEFT_LEG_PIVOT",new Vector3(-.25f,1.06f,0),root);
        Transform rightLeg=Joint("RIGHT_LEG_PIVOT",new Vector3(.25f,1.06f,0),root);
        float legExtra=player?legStyle*.055f:0;
        LocalPart(PrimitiveType.Capsule,"LEFT_LEG_REPLACEABLE",leftLeg,new Vector3(0,-.39f,0),new Vector3(.38f,.55f+legExtra,.4f),dark);
        LocalPart(PrimitiveType.Capsule,"RIGHT_LEG_REPLACEABLE",rightLeg,new Vector3(0,-.39f,0),new Vector3(.38f,.55f+legExtra,.4f),dark);
        LocalPart(PrimitiveType.Sphere,"Left_Shoe",leftLeg,new Vector3(0,-.79f,-.15f),new Vector3(.46f,.24f,.65f),wood);
        LocalPart(PrimitiveType.Sphere,"Right_Shoe",rightLeg,new Vector3(0,-.79f,-.15f),new Vector3(.46f,.24f,.65f),wood);
        if(player)
        {
            heroLeftArm=leftArm;heroRightArm=rightArm;heroLeftLeg=leftLeg;heroRightLeg=rightLeg;
            if(accessory>0)
            {
                LocalPart(PrimitiveType.Cylinder,"ACCESSORY_REPLACEABLE",root,new Vector3(0,3.42f,0),new Vector3(1.4f,.12f,1.35f),roofMat);
                LocalPart(PrimitiveType.Cylinder,"Hat_Top",root,new Vector3(0,3.62f,.13f),new Vector3(1f,.23f,1f),roofMat);
            }
        }
    }
    void RebuildHero()
    {
        if(hero==null)return;
        for(int i=hero.transform.childCount-1;i>=0;i--) Destroy(hero.transform.GetChild(i).gameObject);
        heroLeftArm=heroRightArm=heroLeftLeg=heroRightLeg=null;
        meshyAnimator=null;
        meshyWasMoving=false;
        meshyLegacyAnimations=null;
        meshyLegacyPresent=false;
        usingMeshyPlayer=false;

        // 새 마스터 캐릭터(헤어/옷 커스터마이징 되는 쪽)가 준비되어 있으면 그걸 실제 플레이어로 쓰고,
        // 예전 Meshy 스쿨걸/도형 캐릭터는 아예 만들지 않는다. (PetTownGame.Update()의 64번째 줄 근처 로직이
        // PetTownPlayerControls가 씬에 하나라도 있으면 레거시 hero 렌더러를 자동으로 숨겨주지만,
        // 애초에 안 만들면 숨길 것도 없어서 더 깔끔함.)
        GameObject existingMaster = GameObject.Find("PetTown_MasterCharacter");
        GameObject masterPrefab = Resources.Load<GameObject>("MasterPlayer");
        if (masterPrefab != null)
        {
            GameObject masterGO = existingMaster;
            if (masterGO == null)
            {
                masterGO = Instantiate(masterPrefab, hero.transform.position, Quaternion.identity);
                masterGO.name = "PetTown_MasterCharacter";
                if (masterGO.GetComponent<PetTownPlayerControls>() == null)
                    masterGO.AddComponent<PetTownPlayerControls>();
                MarkNavMeshIgnore(masterGO);
            }
            RefreshHeroPortrait(masterGO);
            Debug.Log("Pet Town 3D: Master character is the active player.");
            return;
        }
        Debug.LogWarning("Pet Town 3D: MasterPlayer prefab not found. Falling back to legacy character. " +
                          "Use menu Pet Town 3D > Install Master Player once in the Unity Editor.");

        GameObject model=Resources.Load<GameObject>("MeshyPlayer");
        if(model!=null)
        {
            GameObject instance=Instantiate(model,hero.transform);
            instance.name="MESHY_CHARACTER_REPLACEABLE";
            instance.transform.localPosition=Vector3.zero;
            instance.transform.localRotation=Quaternion.identity;
            MarkNavMeshIgnore(instance);
            // Normalize the imported FBX height and place its feet on the ground.
            var renderers=instance.GetComponentsInChildren<Renderer>();
            if(renderers.Length>0)
            {
                Bounds bounds=renderers[0].bounds;
                for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
                float scale=Mathf.Clamp(3.3f/Mathf.Max(.001f,bounds.size.y), .01f, 100f);
                instance.transform.localScale=Vector3.one*scale;
                // Bounds must be recalculated after scaling.
                renderers=instance.GetComponentsInChildren<Renderer>();
                bounds=renderers[0].bounds;
                for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
                instance.transform.position+=Vector3.up*(hero.transform.position.y-bounds.min.y);
            }
            foreach(var c in instance.GetComponentsInChildren<Collider>())Destroy(c);
            meshyAnimator=instance.GetComponentInChildren<Animator>(true);
            if(meshyAnimator==null)meshyAnimator=instance.AddComponent<Animator>();
            RuntimeAnimatorController controller=Resources.Load<RuntimeAnimatorController>("MeshyRun");
            if(controller!=null)meshyAnimator.runtimeAnimatorController=controller;
            meshyAnimator.applyRootMotion=false;
            meshyLegacyAnimations=instance.GetComponentsInChildren<Animation>(true);
            meshyLegacyPresent=false;
            foreach(var legacy in meshyLegacyAnimations)
            {
                if(legacy==null)continue;
                int clipCount=0;
                foreach(AnimationState state in legacy)clipCount++;
                if(clipCount>0)meshyLegacyPresent=true;
                legacy.playAutomatically=false;
                legacy.Stop();
            }
            if(meshyLegacyPresent) meshyAnimator.enabled=false;
            Debug.Log("Pet Town GLB: legacy Animation components="+meshyLegacyAnimations.Length+
                      ", imported clips="+(meshyLegacyPresent?"yes":"no")+
                      ", Animator controller="+(meshyAnimator.runtimeAnimatorController!=null));
            // Never autoplay a running-only GLB while the player is stationary.
            meshyAnimator.speed=0f;
            usingMeshyPlayer=true;
            Debug.Log("Pet Town 3D: Meshy player model loaded.");
            return;
        }
        Debug.LogWarning("Pet Town 3D: MeshyPlayer prefab not found. Showing placeholder character. Use menu Pet Town 3D > Install Meshy Player.");
        BuildPerson(hero.transform,true,0);
    }
    void SpawnVillager(Vector3 p,int appearance=-1)
    {
        if(IsWater(p.x,p.z))p=shopPosition+new Vector3(10,0,10);
        var root=new GameObject("NPC_SINGLE_ROOT_REPLACEABLE");
        root.transform.position=new Vector3(p.x,Ground(p.x,p.z),p.z);
        VillagerAnim animInfo;
        if(SpawnVillagerModel(root.transform,out animInfo,appearance))
        {
            if(animInfo!=null) villagerAnimInfo[root]=animInfo;
            var visual=root.transform.Find("VILLAGER_MODEL_REPLACEABLE");if(visual)PetTownVisualGrounding.Attach(this,root.transform,visual);
        }
        else BuildPerson(root.transform,false,villagers.Count);
        // NavMeshAgent drives this villager's actual movement and obstacle avoidance; our own
        // code only decides WHERE it wants to go (PickNextVillagerGoal) and reads agent.velocity
        // to pick which animation to play. This replaces the old hand-rolled capsule-avoidance
        // movement, which kept finding new ways to fail (corner clipping, sideways jitter).
        var agent=root.AddComponent<NavMeshAgent>();
        agent.radius=0.4f; agent.height=Mathf.Max(1f,GetHeroTargetHeight());
        agent.speed=1.3f; agent.angularSpeed=420f; agent.acceleration=10f;
        agent.stoppingDistance=0.3f; agent.autoBraking=true; agent.baseOffset=0f;
        // Avoid spawning a villager on a distant floor/roof island (visible teleport).
        if(NavMesh.SamplePosition(root.transform.position,out NavMeshHit navHit,1.5f,NavMesh.AllAreas))
            agent.Warp(navHit.position);
        villagers.Add(root);
    }

    // Meshy villager models live in Resources/Villagers/<name>.glb. Add more paths here as
    // more are made — one is picked at random for each new villager.
    static readonly string[] VillagerResources = { "MasterPlayer" };
    string staffModelOverride;
    bool SpawnVillagerModel(Transform parent, out VillagerAnim animInfo,int appearance=-1)
    {
        animInfo=null;
        string resourcePath = !string.IsNullOrEmpty(staffModelOverride)?staffModelOverride:VillagerResources[appearance>=0?appearance%VillagerResources.Length:UnityEngine.Random.Range(0,VillagerResources.Length)];
        GameObject model = Resources.Load<GameObject>(resourcePath);
        if (model == null) return false;

        GameObject instance = Instantiate(model, parent);
        // Laptop lite: prevent the shared player prefab from controlling NPC input.
        foreach (var control in instance.GetComponentsInChildren<PetTownPlayerControls>(true)) control.enabled = false;
        instance.name = "VILLAGER_MODEL_REPLACEABLE";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        MarkNavMeshIgnore(instance);

        // Measure height from the mesh's own (pose-independent) bounds rather than
        // Renderer.bounds — a SkinnedMeshRenderer's world bounds can read back wrong
        // (near-zero) on the very first frame after Instantiate(), which is what made
        // earlier villagers scale up far too large.
        float minY, maxY;
        if (MeasureLocalHeight(instance, out minY, out maxY) && maxY - minY > 0.01f)
        {
            float feetWorldY = minY;
            float scale = Mathf.Clamp(GetHeroTargetHeight() / (maxY - minY), .01f, 100f);
            instance.transform.localScale = Vector3.one * scale;
            float pivotY = instance.transform.position.y;
            float newFeetWorldY = pivotY + (feetWorldY - pivotY) * scale;
            instance.transform.position += Vector3.up * (parent.position.y - newFeetWorldY);
        }

        // Villagers avoid walls via raycasts in Update(), so they don't need their own colliders.
        foreach (var c in instance.GetComponentsInChildren<Collider>()) Destroy(c);

        // Capture the model's original standing pose before starting a walk clip.
        // Stop() alone freezes the last walking frame instead of returning to rest.
        Transform[] restBones = instance.GetComponentsInChildren<Transform>(true);
        Vector3[] restPositions = new Vector3[restBones.Length];
        Quaternion[] restRotations = new Quaternion[restBones.Length];
        Vector3[] restScales = new Vector3[restBones.Length];
        for (int i = 0; i < restBones.Length; i++)
        {
            restPositions[i] = restBones[i].localPosition;
            restRotations[i] = restBones[i].localRotation;
            restScales[i] = restBones[i].localScale;
        }

        // glTFast can import animated glb files either as a legacy Animation component
        // (one AnimationState per clip) or as a Mecanim Animator with an auto-generated
        // AnimatorController (one state per clip, state name == clip name). Handle both —
        // if neither is wired up, the model just sits frozen in its bind/A-pose while we
        // move it, which is exactly the "sliding T-pose" symptom.
        var legacy = instance.GetComponentInChildren<Animation>(true);
        var animator = instance.GetComponentInChildren<Animator>(true);
        string idleName = null, walkName = null, runName = null, firstName = null;
        if (legacy != null)
        {
            foreach (AnimationState st in legacy)
            {
                st.wrapMode = WrapMode.Loop;
                if (st.clip != null) StripBakedRootMotion(st.clip);
                ClassifyClip(st.name, ref idleName, ref walkName, ref runName, ref firstName);
            }
            if (walkName == null) walkName = runName;
            if (runName == null) runName = walkName;
            // Imported single-clip villagers often have no Idle-labelled clip.
            // Keep their animation driver initialized, and use the available clip for walking.
            if (walkName == null && runName == null && firstName != null) walkName = firstName;
            if (idleName != null || walkName != null)
            {
                legacy.wrapMode = WrapMode.Loop;
                if (idleName != null) legacy.Play(idleName);
                animInfo = new VillagerAnim { anim = legacy, idleClip = idleName, walkClip = walkName, runClip = runName, model = instance.transform };
                if (animator != null) animator.enabled = false; // don't fight the legacy component over bones
            }
        }
        if (animator != null) animator.applyRootMotion = false;
        if (animInfo == null && animator != null && animator.runtimeAnimatorController != null)
        {
            var clips = animator.runtimeAnimatorController.animationClips;
            idleName = walkName = runName = firstName = null;
            if (clips != null)
                foreach (var clip in clips)
                    if (clip != null) { StripBakedRootMotion(clip); ClassifyClip(clip.name, ref idleName, ref walkName, ref runName, ref firstName); }
            if (walkName == null) walkName = runName;
            if (runName == null) runName = walkName;
            // Imported single-clip villagers often have no Idle-labelled clip.
            // Keep their animation driver initialized, and use the available clip for walking.
            if (walkName == null && runName == null && firstName != null) walkName = firstName;
            if (idleName != null || walkName != null)
            {
                animator.applyRootMotion = false;
                animator.enabled = true;
                animator.speed = 1f;
                if (idleName != null) animator.Play(idleName, 0, 0f);
                animInfo = new VillagerAnim { animator = animator, idleClip = idleName, walkClip = walkName, runClip = runName, model = instance.transform };
            }
        }
        AnimationClip[] allClips = Resources.LoadAll<AnimationClip>(resourcePath);
        if (animInfo == null)
        {
            // Some glTFast imports add an Animator but never wire a RuntimeAnimatorController
            // to it — the clips still exist as sub-assets of the .glb file itself, so pull them
            // directly with Resources.LoadAll and drive them through our own legacy Animation
            // component instead of relying on whatever (if anything) glTFast hooked up.
            if (allClips != null && allClips.Length > 0)
            {
                var legacyPlayer = instance.GetComponent<Animation>();
                if (legacyPlayer == null) legacyPlayer = instance.AddComponent<Animation>();
                idleName = walkName = runName = firstName = null;
                foreach (var clip in allClips)
                {
                    if (clip == null) continue;
                    StripBakedRootMotion(clip);
                    clip.legacy = true;
                    legacyPlayer.AddClip(clip, clip.name);
                    var state = legacyPlayer[clip.name];
                    if (state != null) state.wrapMode = WrapMode.Loop;
                    ClassifyClip(clip.name, ref idleName, ref walkName, ref runName, ref firstName);
                }
                if (walkName == null) walkName = runName;
                if (runName == null) runName = walkName;
                // Imported single-clip villagers often have no Idle-labelled clip.
            // Keep their animation driver initialized, and use the available clip for walking.
            if (walkName == null && runName == null && firstName != null) walkName = firstName;
                if (idleName != null || walkName != null)
                {
                    legacyPlayer.wrapMode = WrapMode.Loop;
                    if (idleName != null) legacyPlayer.Play(idleName);
                    animInfo = new VillagerAnim { anim = legacyPlayer, idleClip = idleName, walkClip = walkName, runClip = runName, model = instance.transform };
                    if (animator != null) animator.enabled = false;
                }
            }
        }
        if (animInfo != null)
        {
            animInfo.poseBones = restBones;
            animInfo.restPositions = restPositions;
            animInfo.restRotations = restRotations;
            animInfo.restScales = restScales;
            // GLB villagers have no standing Idle clip. Prepare a relaxed standing
            // pose by bringing each upper arm toward the side of the body.
            foreach (var bone in restBones)
            {
                if (bone.name.EndsWith("LeftUpperArm", StringComparison.OrdinalIgnoreCase)) animInfo.leftUpperArm = bone;
                else if (bone.name.EndsWith("RightUpperArm", StringComparison.OrdinalIgnoreCase)) animInfo.rightUpperArm = bone;
            }
            animInfo.leftRelaxedRotation = MakeRelaxedArmRotation(animInfo.leftUpperArm);
            animInfo.rightRelaxedRotation = MakeRelaxedArmRotation(animInfo.rightUpperArm);
        }
        string allNames = allClips != null ? string.Join(",", Array.ConvertAll(allClips, c => c != null ? c.name : "null")) : "none";
        Debug.Log("Pet Town 3D villager anim: legacy=" + (legacy != null) +
                   " animator=" + (animator != null) +
                   " controller=" + (animator != null && animator.runtimeAnimatorController != null) +
                   " clipNames=[" + allNames + "]" +
                   " resolved idle=" + (animInfo != null ? animInfo.idleClip : "-") +
                   " walk=" + (animInfo != null ? animInfo.walkClip : "-") +
                   " run=" + (animInfo != null ? animInfo.runClip : "-"));

        return true;
    }

    // Keeps clip-name matching in one place: idle/walk/run are matched by increasingly
    // specific keywords so "Walking_Cycle" or "Running_Fast" style names still resolve.
    // Meshy/Mixamo exported clips often bake the root/hip translation straight onto the
    // clip's own position curves (sometimes through an extra "Armature" wrapper node, so the
    // path depth varies per model). Since we drive villager position manually, that baked
    // translation fights our code every frame and snaps back to the clip's authored offset
    // whenever the clip loops - this is what causes villagers to visibly "teleport" back
    // toward their spawn/animation origin mid-walk. Skeletal rigs basically never need real
    // position animation other than that root motion, so strip EVERY position curve in the
    // clip (at any depth) rather than trying to guess which bone is "the" root.
    readonly HashSet<AnimationClip> strippedRootMotionClips = new HashSet<AnimationClip>();
    void StripBakedRootMotion(AnimationClip clip)
    {
        if (clip == null || strippedRootMotionClips.Contains(clip)) return;
        strippedRootMotionClips.Add(clip);
#if UNITY_EDITOR
        var bindings = AnimationUtility.GetCurveBindings(clip);
        foreach (var b in bindings)
        {
            if (b.type != typeof(Transform)) continue;
            if (b.propertyName != "m_LocalPosition.x" && b.propertyName != "m_LocalPosition.y" && b.propertyName != "m_LocalPosition.z") continue;
            AnimationUtility.SetEditorCurve(clip, b, null);
        }
#endif
    }

    void ClassifyClip(string rawName, ref string idle, ref string walk, ref string run, ref string first)
    {
        string n = rawName.ToLowerInvariant();
        // Sit/lie/transition/etc. clips need a real trigger (a chair, a bed...) we don't have
        // yet — never let one of these become the "whatever's first" default standing/walking
        // pose, or villagers end up floating in a sitting pose with no chair under them.
        bool special = n.Contains("sit") || n.Contains("lie") || n.Contains("lay") || n.Contains("sleep")
                     || n.Contains("jump") || n.Contains("leap") || n.Contains("penguin") || n.Contains("push") || n.Contains("kettlebell") || n.Contains("hip_hop") || n.Contains("happy") || n.Contains("wave") || n.Contains("dance") || n.Contains("transition");
        if (special) return; // never let a sit/lie/etc. clip become idle, walk, run OR the "first" fallback
        if (n.Contains("turn") || n.Contains("rotate") || n.Contains("pivot")) return;
        if (first == null) first = rawName;
        // "Walk_Turn_Left/Right" etc. are turn-IN-PLACE animations, not a forward walk cycle —
        // picking one as the general walk/idle made villagers visibly spin on the spot instead
        // of walking, and look like they "teleport" in place whenever the turn loops back to its
        // start pose. Prefer a plain clip over a turn-variant for the same slot; only fall back
        // to the turn-variant if nothing else is offered.
        bool isTurn = n.Contains("turn");
        if (n.Contains("idle") && (idle == null || (!isTurn && idle.ToLowerInvariant().Contains("turn")))) idle = rawName;
        if (n.Contains("walk") && (walk == null || (!isTurn && walk.ToLowerInvariant().Contains("turn")))) walk = rawName;
        if ((n.Contains("run") || n.Contains("jog") || n.Contains("sprint")) && (run == null || (!isTurn && run.ToLowerInvariant().Contains("turn")))) run = rawName;
    }

    // Gives a villager an actual reason to be walking somewhere, instead of endless random
    // wandering: mostly they're just out for a stroll, but sometimes they head over to visit
    // the Pet Shop and hang around there a while before moving on. Running is now rare and
    // only kicks in on the (uncommon) occasions they're hurrying somewhere specific.
    void PickNextVillagerGoal(GameObject person, VillagerWander w)
    {
        if (w.goal == VillagerGoal.AtShop)
        {
            w.goal = VillagerGoal.Stroll;
            w.target = PickWanderTarget(person.transform.position);
            w.running = false;
            w.nextPick = Time.time + UnityEngine.Random.Range(6f, 14f);
            return;
        }
        if (w.goal == VillagerGoal.GoToShop)
        {
            // Arrived at the shop — stand around for a while before heading off again.
            w.goal = VillagerGoal.AtShop;
            w.target = person.transform.position;
            w.running = false;
            w.nextPick = Time.time + UnityEngine.Random.Range(5f, 10f);
            TryVillagerPetPurchase(person);
            return;
        }
        // Currently strolling. Small chance to set off toward the Pet Shop instead of another
        // aimless walk; otherwise just pick a new nearby spot to stroll to.
        if (shop != null && UnityEngine.Random.value < 0.25f)
        {
            w.goal = VillagerGoal.GoToShop;
            Vector3 offset = new Vector3(UnityEngine.Random.Range(-3f, 3f), 0, UnityEngine.Random.Range(1f, 4f));
            w.target = shopPosition + offset;
            // "Running late to the shop" is the one semi-legitimate reason to run, and even then rarely.
            w.running = UnityEngine.Random.value < 0.08f;
        }
        else
        {
            w.goal = VillagerGoal.Stroll;
            w.target = PickWanderTarget(person.transform.position);
            w.running = UnityEngine.Random.value < 0.03f; // a casual stroll is basically never a run
        }
        w.nextPick = Time.time + UnityEngine.Random.Range(6f, 14f);
    }

    // A destination must be on the same connected NavMesh island as the NPC.
    // Reject partial/invalid paths rather than forcing an agent into a wall.
    void SetSafeVillagerDestination(NavMeshAgent agent, VillagerWander w)
    {
        if(agent==null || !agent.isOnNavMesh) return;
        NavMeshHit hit;
        if(!NavMesh.SamplePosition(w.target,out hit,1.2f,agent.areaMask))
        {
            agent.ResetPath(); w.nextPick=Time.time+1f; return;
        }
        var candidatePath=new NavMeshPath();
        if(!agent.CalculatePath(hit.position,candidatePath) || candidatePath.status!=NavMeshPathStatus.PathComplete)
        {
            agent.ResetPath(); w.nextPick=Time.time+1f; return;
        }
        w.target=hit.position;
        agent.SetPath(candidatePath);
    }

    // Picks a random nearby spot for a villager to wander toward — clamped to the world
    // bounds and rejected if it lands in water. Falls back to the shop if unlucky.
    Vector3 PickWanderTarget(Vector3 from)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            float ang = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = UnityEngine.Random.Range(6f, 16f);
            Vector3 candidate = from + new Vector3(Mathf.Sin(ang) * dist, 0, Mathf.Cos(ang) * dist);
            float half = WorldSize / 2f - 6f;
            candidate.x = Mathf.Clamp(candidate.x, -half, half);
            candidate.z = Mathf.Clamp(candidate.z, -half, half);
            if (!IsWater(candidate.x, candidate.z)) return candidate;
        }
        return shopPosition;
    }

    // Reads the live player character's current rendered height so villagers can be scaled
    // to match exactly, whatever size the installed MasterPlayer model actually turned out to
    // be (that model is never auto-scaled on install, unlike villagers/furniture/trees).
    float GetHeroTargetHeight()
    {
        GameObject master = GameObject.Find("PetTown_MasterCharacter");
        if (master == null) return 3.3f;
        var renderers = master.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 3.3f;
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b.size.y > 0.1f ? b.size.y : 3.3f;
    }

    // Combines the mesh asset's own local bounds (bind pose, not whatever frame the
    // Animation component happens to be on) so the height reading is stable regardless
    // of when this runs relative to Unity's render/animation update order.
    bool MeasureLocalHeight(GameObject instance, out float minY, out float maxY)
    {
        bool has=false; minY=0; maxY=0;
        foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;
            Bounds lb = smr.sharedMesh.bounds;
            Vector3 c0 = smr.transform.TransformPoint(lb.center - lb.extents);
            Vector3 c1 = smr.transform.TransformPoint(lb.center + lb.extents);
            float lo = Mathf.Min(c0.y, c1.y), hi = Mathf.Max(c0.y, c1.y);
            if (!has) { minY = lo; maxY = hi; has = true; } else { minY = Mathf.Min(minY, lo); maxY = Mathf.Max(maxY, hi); }
        }
        foreach (var mf in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds lb = mf.sharedMesh.bounds;
            Vector3 c0 = mf.transform.TransformPoint(lb.center - lb.extents);
            Vector3 c1 = mf.transform.TransformPoint(lb.center + lb.extents);
            float lo = Mathf.Min(c0.y, c1.y), hi = Mathf.Max(c0.y, c1.y);
            if (!has) { minY = lo; maxY = hi; has = true; } else { minY = Mathf.Min(minY, lo); maxY = Mathf.Max(maxY, hi); }
        }
        return has;
    }

    // Excludes TerrainLayer so rolling hills are never mistaken for an obstacle (that was the
    // "drunk wandering" bug — a forward check kept hitting the ground mesh itself on any slope).
    static readonly int VillagerBlockMask = ~(1 << TerrainLayer);
    // A capsule overlap AT THE DESTINATION, not a single forward ray from the start point — this
    // is what actually catches corners and thin/angled walls that a plain raycast slips past.
    bool PositionClear(Vector3 p)
    {
        Vector3 lo = p + Vector3.up * 0.35f;
        Vector3 hi = p + Vector3.up * 1.6f;
        return !Physics.CheckCapsule(lo, hi, 0.4f, VillagerBlockMask);
    }
    // Tries the desired direction first, then fans outward (±30°, ±60°, ±90°, ±135°, 180°) and
    // takes the first one that's actually clear. Sweeps the capsule along the WHOLE probe
    // distance (CapsuleCast), not just a single sampled endpoint — an endpoint-only check can
    // miss a corner or thin wall that the capsule would clip through on the way there, which is
    // what let villagers visibly grind into wall corners.
    static readonly float[] DirectionFanAngles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f };
    Vector3 FindClearDirection(Vector3 origin, Vector3 desired, float probeDist)
    {
        Vector3 lo = origin + Vector3.up * 0.35f;
        Vector3 hi = origin + Vector3.up * 1.6f;
        foreach (float a in DirectionFanAngles)
        {
            Vector3 cand = Quaternion.Euler(0, a, 0) * desired;
            if (cand.sqrMagnitude < 0.0001f) continue;
            if (!Physics.CapsuleCast(lo, hi, 0.4f, cand.normalized, out RaycastHit hit, probeDist, VillagerBlockMask))
                return cand;
        }
        return Vector3.zero;
    }

    // The animation itself (walk/run clips Meshy exported) often bakes a bit of forward
    // translation into the skeleton so the gait looks convincing in isolation. Since our own
    // Update() loop already moves the logical root transform, that baked motion just adds on
    // top of it — and snaps back to zero every time the clip loops, which is exactly the
    // "walks forward, then teleports back" symptom. Re-lock each model to its root every
    // frame, after animation evaluation, so only our own movement code actually relocates it.
    void LateUpdate()
    {
        foreach (var info in villagerAnimInfo.Values)
        {
            if (info != null && info.model != null)
            {
                info.model.localPosition = Vector3.zero;
                if (!info.wasMoving && string.IsNullOrEmpty(info.idleClip)) ApplyRelaxedVillagerArms(info);
            }
        }
    }
    // Rotate the upper arm so its elbow points mostly downward, with a small
    // natural outward angle. Works directly on the imported GLB skeleton;
    // no external Humanoid Avatar or replacement walk clips are needed.
    static Quaternion MakeRelaxedArmRotation(Transform upperArm)
    {
        if (upperArm == null) return Quaternion.identity;
        Transform lowerArm = null;
        foreach (Transform child in upperArm)
            if (child.name.EndsWith("LowerArm", StringComparison.OrdinalIgnoreCase)) { lowerArm = child; break; }
        if (lowerArm == null) return upperArm.localRotation;
        Vector3 from = lowerArm.position - upperArm.position;
        if (from.sqrMagnitude < 0.00001f) return upperArm.localRotation;
        bool left = upperArm.name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0;
        Vector3 desired = upperArm.root.TransformDirection(new Vector3(left ? -0.18f : 0.18f, -1f, 0.03f)).normalized;
        Quaternion worldTarget = Quaternion.FromToRotation(from.normalized, desired) * upperArm.rotation;
        return Quaternion.Inverse(upperArm.parent.rotation) * worldTarget;
    }

    static void ApplyRelaxedVillagerArms(VillagerAnim info)
    {
        if (info.leftUpperArm != null) info.leftUpperArm.localRotation = info.leftRelaxedRotation;
        if (info.rightUpperArm != null) info.rightUpperArm.localRotation = info.rightRelaxedRotation;
    }

    void UpdateVillagerAnim(GameObject person, bool moving, bool running)
    {
        VillagerAnim info;
        if (!villagerAnimInfo.TryGetValue(person, out info) || info == null) return;
        string target;
        // Never play a walking clip while the NavMeshAgent is stationary.
        // Many imported villagers have Walk but no Idle clip: the old fallback
        // (idleClip ?? walkClip) caused endless walking-in-place.
        if (!moving)
        {
            if (info.anim != null)
            {
                if (!string.IsNullOrEmpty(info.idleClip))
                {
                    if (!info.anim.IsPlaying(info.idleClip)) info.anim.CrossFade(info.idleClip, 0.15f);
                }
                else
                {
                    if (info.anim.isPlaying) info.anim.Stop();
                    RestoreVillagerRestPose(info);
                }
            }
            else if (info.animator != null)
            {
                if (!string.IsNullOrEmpty(info.idleClip))
                {
                    var state = info.animator.GetCurrentAnimatorStateInfo(0);
                    if (!state.IsName(info.idleClip) && !(info.animator.IsInTransition(0) && info.animator.GetNextAnimatorStateInfo(0).IsName(info.idleClip))) info.animator.CrossFade(info.idleClip, 0.15f);
                    info.animator.speed = 1f;
                }
                else
                {
                    info.animator.enabled = false;
                    RestoreVillagerRestPose(info);
                }
            }
            info.wasMoving = false;
            return;
        }
        info.wasMoving = true;
        target = running ? (info.runClip ?? info.walkClip) : (info.walkClip ?? info.runClip);
        if (string.IsNullOrEmpty(target)) return;
        if (info.anim != null)
        {
            if (!info.anim.IsPlaying(target)) info.anim.CrossFade(target, 0.15f);
        }
        else if (info.animator != null)
        {
            if (!info.animator.enabled) info.animator.enabled = true;
            info.animator.speed = 1f;
            var state = info.animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName(target) && !(info.animator.IsInTransition(0) && info.animator.GetNextAnimatorStateInfo(0).IsName(target))) info.animator.CrossFade(target, 0.15f);
        }
    }
    void Message(string message) {if(RoutineMessage(message))return;notice=message;noticeTime=Time.time+6f;}
    void ClearGhost() {if(ghost!=null)Destroy(ghost);ghost=null;ghostRenderers.Clear();foreach(var material in furniturePreviewMaterials)if(material!=null)Destroy(material);furniturePreviewMaterials.Clear();if(entrancePreview!=null)entrancePreview.Hide();}
    void SetMode(Mode m,int index=0)
    {
        ClosePetPlay();
        adoptionSelectionOpen=false;
        if(petCareWindow!=null)petCareWindow.Close();
        if(catalogWindow!=null)catalogWindow.Hide();
        if(expansionWindow!=null)expansionWindow.Close();
        if(roadDragging){roadDragging=false;roadDragPoints=null;StopRoadPreview();}
        facilityFurnitureTarget=null;
        mode=m;selected=(m==Mode.Build?-1:index);furnitureQuarter=0;
        if(furnitureGrid!=null)furnitureGrid.Hide();
        placementPlanner=new PetTownPlacementPlanner();
        if(entrancePreview!=null)entrancePreview.Hide();
        ghostFurnitureFor=-1;ghostBuildFor=-1; // force RebuildGhost() to run on the next Update() tick
        ClearGhost();
    }
    // A plain flat box the same size whatever was selected read as "placeholder", not a real
    // placement preview — a real game shows the actual thing about to be placed. This instead
    // clones the real selected furniture prefab (or sizes a box to the real building footprint)
    // and re-tints every one of its renderers a single solid color so it still reads clearly as
    // a "ghost"/ghost outline rather than the final textured object, the same convention most
    // city-builders use for their placement preview.
    readonly List<Renderer> ghostRenderers = new List<Renderer>();
    int ghostFurnitureFor=-1, ghostBuildFor=-1;
    Vector2 BuildingFootprint(int kind)
    {
        if(kind==8)return new Vector2(.75f,.75f);
        return DefinitionFor(kind).footprintMeters;
    }
    void RebuildGhost()
    {
        ClearGhost();furnitureGhostReady=false;furnitureGhostLift=0;
        if(mode==Mode.Furniture)
        {
            ghostFurnitureFor=furnitureChoice;
            if(furnitureChoice>=6)
            {
                furniturePreviewBuilding=true;try{ghost=CreatePetShopDetail(furnitureChoice-6,Vector3.zero);}finally{furniturePreviewBuilding=false;}
                ghost.name="PLACEMENT_PREVIEW";MarkNavMeshIgnore(ghost);StartCoroutine(FitDetailGhost(ghost));return;
            }
            GameObject prefab=CozySelected!=null?CozySelected.prefab:Resources.Load<GameObject>(FurnitureResource[furnitureChoice]);
            if(prefab!=null)
            {
                ghost=Instantiate(prefab);
                ghost.name="PLACEMENT_PREVIEW";
                foreach(var c in ghost.GetComponentsInChildren<Collider>())Destroy(c);
                MarkNavMeshIgnore(ghost);
                StartCoroutine(FitGhostNextFrame(ghost,SelectedFurnitureHeight));
                return;
            }
            ghost=Part(PrimitiveType.Cube,"PLACEMENT_PREVIEW",Vector3.up*.6f,new Vector3(1.6f,1.2f,1.4f),Mat(Color.white),null);
            Destroy(ghost.GetComponent<Collider>());
            ghostRenderers.AddRange(ghost.GetComponentsInChildren<Renderer>());
        }
        else if(mode==Mode.Build && selected>=0 && selected!=8)
        {
            ghostBuildFor=selected;
            Vector2 fp=BuildingFootprint(selected);
            ghost=Part(PrimitiveType.Cube,"PLACEMENT_PREVIEW",Vector3.up*.1f,new Vector3(fp.x,.2f,fp.y),Mat(Color.white),null);
            Destroy(ghost.GetComponent<Collider>());
            ghostRenderers.AddRange(ghost.GetComponentsInChildren<Renderer>());
        }
        else if(mode==Mode.Start)
        {
            ghost=Part(PrimitiveType.Cube,"PLACEMENT_PREVIEW",Vector3.up*.1f,new Vector3(15,.2f,13),Mat(Color.white),null);
            Destroy(ghost.GetComponent<Collider>());
            ghostRenderers.AddRange(ghost.GetComponentsInChildren<Renderer>());
        }
        if(ghost!=null)MarkNavMeshIgnore(ghost);
    }
    IEnumerator FitGhostNextFrame(GameObject instance, float targetHeight)
    {
        yield return null;
        if(instance==null)yield break;
        var renderers=instance.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0)yield break;
        Bounds b=renderers[0].bounds;
        for(int r=1;r<renderers.Length;r++)b.Encapsulate(renderers[r].bounds);
        if(b.size.y>0.0001f)
        {
            float scale=Mathf.Clamp(targetHeight/b.size.y,.001f,2000f);
            instance.transform.localScale=Vector3.one*scale;
        }
        renderers=instance.GetComponentsInChildren<Renderer>();b=renderers[0].bounds;foreach(var renderer in renderers)b.Encapsulate(renderer.bounds);
        furnitureGhostLift=instance.transform.position.y-b.min.y;
        bool swap=Mathf.RoundToInt(instance.transform.eulerAngles.y/90f)%2!=0;
        furnitureGhostSize=swap?new Vector2(b.size.z,b.size.x):new Vector2(b.size.x,b.size.z);
        furnitureGhostReady=true;
        ghostRenderers.Clear();
        ghostRenderers.AddRange(renderers);
    }
    // Live afford/can't-afford feedback, same idea as a disabled "Buy" button turning red —
    // checked every frame since the player's coin total can change while they're still browsing
    // the ghost around (selling, other purchases, etc).
    Material ghostGreenMat, ghostRedMat;
    Material buildValidPreview,buildWarningPreview,buildBlockedPreview;
    Material ConstructionPreviewMaterial(ref Material material,Color color)
    {
        if(material!=null)return material;
        var shader=Resources.Load<Shader>("Materials/PetTownPlacementPreview");
        if(shader==null||!shader.isSupported)return Mat(color);
        material=new Material(shader);material.SetColor("_BaseColor",color);return material;
    }
    void OnDestroy()
    {
        ClosePetPlay();
        ClearShopPetActors();
        ReleaseFacilityActors();
        ClearResidentCompanions();
        foreach(var material in new[]{buildValidPreview,buildWarningPreview,buildBlockedPreview})
            if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
    }
    void UpdateGhostTint()
    {
        if(ghost==null||ghostRenderers.Count==0)return;
        if(ghostGreenMat==null)
        {
            ghostGreenMat=Mat(new Color(.35f,.95f,.55f,.85f));
            ghostRedMat=Mat(new Color(.95f,.30f,.30f,.85f));
        }
        bool afford = mode==Mode.Furniture ? money>=50 : mode==Mode.Build ? (selected>=0 && selected<costs.Length && money>=costs[selected]) : true;
        Material use = afford?ghostGreenMat:ghostRedMat;
        if(mode==Mode.Furniture)
        {
            var point=FurniturePlacementPoint(ghost.transform.position);var validation=ValidateFurniture(point);
            if(furnitureGrid!=null)furnitureGrid.ShowPlacement(point,PetTownFurniturePlanner.ReservedSize(furnitureGhostSize,furnitureGridSize),furnitureQuarter,validation.CanPlace);
            use=validation.CanPlace?ConstructionPreviewMaterial(ref buildValidPreview,new Color(.18f,1f,.35f,.26f)):ConstructionPreviewMaterial(ref buildBlockedPreview,new Color(1f,.16f,.12f,.32f));
        }
        else if(UsesBuildingGrid)
        {
            var validation=ValidateBuildingPlacement(BuildingPlacementPoint(ghost.transform.position));
            // green = buildable, orange = buildable after leveling the ground, red = cannot build at all
            use=validation.CanPlace?ConstructionPreviewMaterial(ref buildValidPreview,new Color(.18f,1f,.35f,.26f)):
                validation.NeedsGrading?ConstructionPreviewMaterial(ref buildWarningPreview,new Color(1f,.52f,.06f,.36f)):
                ConstructionPreviewMaterial(ref buildBlockedPreview,new Color(1f,.16f,.12f,.32f));
        }
        foreach(var r in ghostRenderers) if(r!=null) r.sharedMaterial=use;
    }
    // Real rendered 3D thumbnails for the DECORATE furniture list, instead of plain text
    // buttons. A hidden camera, far off in empty space on its own unused layer, renders each
    // furniture prefab in isolation to a small RenderTexture and the pixels are read back into
    // a cached Texture2D per resource path — generated once per item, lazily, the first time
    // its button is drawn.
    const int IconLayer = 11;
    Camera iconCam; RenderTexture iconRT; bool iconSystemInit;
    readonly Dictionary<string, Texture2D> itemIconCache = new Dictionary<string, Texture2D>();
    readonly HashSet<string> itemIconRequested = new HashSet<string>();
    void InitIconSystem()
    {
        if (iconSystemInit) return;
        iconSystemInit = true;
        var camGO = new GameObject("Icon_Render_Camera_HIDDEN");
        camGO.transform.position = new Vector3(9000, 9000, 9000);
        iconCam = camGO.AddComponent<Camera>();
        iconCam.orthographic = true;
        iconCam.orthographicSize = 1f;
        iconCam.clearFlags = CameraClearFlags.SolidColor;
        iconCam.backgroundColor = new Color(0, 0, 0, 0);
        iconCam.cullingMask = 1 << IconLayer;
        iconCam.nearClipPlane = 0.05f;
        iconCam.farClipPlane = 20f;
        iconCam.enabled = false;
        iconRT = new RenderTexture(128, 128, 16, RenderTextureFormat.ARGB32);
        var lightGO = new GameObject("Icon_Render_Light_HIDDEN");
        lightGO.transform.SetParent(camGO.transform, false);
        lightGO.transform.position = new Vector3(9000, 9003, 8997);
        lightGO.transform.rotation = Quaternion.Euler(40, -30, 0);
        var lt = lightGO.AddComponent<Light>();
        lt.type = LightType.Directional; lt.intensity = 1.1f; lt.cullingMask = 1 << IconLayer;
    }
    void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
    }
    // DECORATE's furniture loop calls GetItemIcon() for every item on every OnGUI repaint, so on
    // the very first frame all 6 requests land in the same frame. Starting one StartCoroutine()
    // per item let them all instantiate their prefab at the same hidden spot (9000,9000,9000)
    // at once — each render then captured every pending item's geometry overlapping in the same
    // shot, which is what showed up as a corrupted solid-magenta thumbnail. A single queue
    // drained by one persistent worker coroutine guarantees only one prefab ever sits at that
    // spot at a time, so each render sees exactly the one item it's supposed to.
    readonly Queue<string> iconQueue = new Queue<string>();
    bool iconWorkerRunning;
    void RequestItemIcon(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath) || itemIconRequested.Contains(resourcePath)) return;
        itemIconRequested.Add(resourcePath);
        InitIconSystem();
        iconQueue.Enqueue(resourcePath);
        if (!iconWorkerRunning) StartCoroutine(IconQueueWorker());
    }
    Texture2D GetItemIcon(string resourcePath)
    {
        RequestItemIcon(resourcePath);
        return itemIconCache.TryGetValue(resourcePath, out var t) ? t : null;
    }
    IEnumerator IconQueueWorker()
    {
        iconWorkerRunning = true;
        while (iconQueue.Count > 0)
            yield return GenerateItemIcon(iconQueue.Dequeue());
        iconWorkerRunning = false;
    }
    IEnumerator GenerateItemIcon(string resourcePath)
    {
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null) yield break;
        Vector3 origin = new Vector3(9000, 9000, 9000);
        GameObject instance = Instantiate(prefab, origin, Quaternion.Euler(0, 35, 0));
        SetLayerRecursive(instance, IconLayer);
        yield return null; // one-frame-delayed bounds measurement, same reason as everywhere else in this file
        var renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Destroy(instance); yield break; }
        Bounds b = renderers[0].bounds;
        for (int r = 1; r < renderers.Length; r++) b.Encapsulate(renderers[r].bounds);
        float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z, 0.001f);
        float scale = 1.5f / maxDim;
        instance.transform.localScale *= scale;
        renderers = instance.GetComponentsInChildren<Renderer>();
        b = renderers[0].bounds;
        for (int r = 1; r < renderers.Length; r++) b.Encapsulate(renderers[r].bounds);
        instance.transform.position += origin - b.center;
        iconCam.transform.position = origin + new Vector3(0, 0.55f, -2.6f);
        iconCam.transform.LookAt(origin);
        iconCam.targetTexture = iconRT;
        iconCam.Render();
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = iconRT;
        var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;
        iconCam.targetTexture = null;
        itemIconCache[resourcePath] = tex; uiActionBuiltFor = -999; // icon finished generating async; force RebuildActionPanel() on the next RefreshUI() tick so it actually shows up
        Destroy(instance);
    }
    // -- HUD character portrait: headshot rendered from the live hero model itself (not a
    // clone) -- its render layer is swapped to IconLayer for one synchronous Camera.Render()
    // call so only the hero (not terrain/buildings at the same spot) ends up in the shot, then
    // swapped straight back, all within the same frame so the player's own camera never sees it.
    Camera portraitCam; RenderTexture portraitRT; Texture2D heroPortraitTex; bool portraitCamInit;
    void InitPortraitCam()
    {
        if (portraitCamInit) return;
        portraitCamInit = true;
        var camGO = new GameObject("Portrait_Render_Camera_HIDDEN");
        portraitCam = camGO.AddComponent<Camera>();
        portraitCam.orthographic = true;
        portraitCam.clearFlags = CameraClearFlags.SolidColor;
        portraitCam.backgroundColor = uiCream;
        portraitCam.cullingMask = 1 << IconLayer;
        portraitCam.nearClipPlane = 0.05f;
        portraitCam.farClipPlane = 20f;
        portraitCam.enabled = false;
        portraitRT = new RenderTexture(128, 128, 16, RenderTextureFormat.ARGB32);
        heroPortraitTex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
    }
    void RefreshHeroPortrait(GameObject root)
    {
        if (root == null) return;
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        InitPortraitCam();
        Bounds b = renderers[0].bounds;
        for (int r = 1; r < renderers.Length; r++) b.Encapsulate(renderers[r].bounds);
        float headSize = Mathf.Max(b.size.y * 0.42f, 0.3f);
        Vector3 focus = b.center + Vector3.up * (b.extents.y - headSize * 0.5f);
        portraitCam.orthographicSize = headSize * 0.62f;
        portraitCam.transform.position = focus + new Vector3(0, 0, -2.6f);
        portraitCam.transform.LookAt(focus);
        int originalLayer = root.layer;
        SetLayerRecursive(root, IconLayer);
        portraitCam.targetTexture = portraitRT;
        portraitCam.Render();
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = portraitRT;
        heroPortraitTex.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
        heroPortraitTex.Apply();
        RenderTexture.active = prevActive;
        portraitCam.targetTexture = null;
        SetLayerRecursive(root, originalLayer);
        if (uiPortraitRaw != null) uiPortraitRaw.texture = heroPortraitTex;
    }
    // v06: landscape desktop and portrait mobile share the same minimal visual language.
    void DrawDesktopHUD()
    {
        InitMobileHUD();
        float scale=Mathf.Clamp(Mathf.Min(Screen.width/1000f,Screen.height/600f),.65f,1.7f);
        float w=Screen.width/scale,h=Screen.height/scale;
        Matrix4x4 previous=GUI.matrix;
        GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        Card(new Rect(12,12,258,74),Color.black);
        InkLabel(new Rect(22,15,240,30),"PET TOWN 3D",mobileTitle,Color.white);
        InkLabel(new Rect(22,49,244,26),"LV "+shopLevel+"  |  Day "+day+"  |  Fame "+reputation,mobileSmall,Color.white);
        Card(new Rect(278,12,198,74),Color.black);
        InkLabel(new Rect(289,16,180,26),"Coins  "+money.ToString("N0"),mobileText,Color.white);
        InkLabel(new Rect(289,49,180,26),"Residents  "+population,mobileSmall,Color.white);
        Card(new Rect(12,94,464,60),Color.black);
        InkLabel(new Rect(22,98,444,24),"PETS  Dog "+dogStock+"  Cat "+catStock+"  Bunny "+rabbitStock,mobileSmall,Color.white);
        InkLabel(new Rect(22,125,310,24),orderPet>=0?"Order: "+petNames[orderPet]+"  "+Mathf.Max(0,Mathf.CeilToInt(orderDeadline-Time.time))+"s":"Adoptions "+sold+"  |  Waiting",mobileSmall,Color.white);
        if(orderPet>=0 && FancyButton(new Rect(359,120,105,28),"SERVE",mint))ServeOrder();
        string[] icons={"adopt","build","decorate","style"};
        string[] labels={"Explore","Build","Decorate","Style"};
        Mode[] modes={Mode.Play,Mode.Build,Mode.Furniture,Mode.Customize};
        for(int i=0;i<4;i++)if(IconButton(new Rect(12+i*62,h-68,56,56),icons[i],labels[i])){SetMode(modes[i]);mobileManagementOpen=false;}
        float rx=w-69;
        if(IconButton(new Rect(rx,12,56,56),"shop","SHOP"))mobileManagementOpen=!mobileManagementOpen;
        if(IconButton(new Rect(rx,74,56,56),"roof","ROOF"))ToggleMobileRoof();
        if(IconButton(new Rect(rx,136,56,56),"save","SAVE"))SaveGame();
        if(IconButton(new Rect(rx,198,56,56),"more","MORE"))mobileMenuOpen=!mobileMenuOpen;
        if(mobileMenuOpen)
        {
            Card(new Rect(w-320,263,242,104),Color.black);
            if(FancyButton(new Rect(w-309,274,220,39),"Reset save",pink)){PlayerPrefs.DeleteKey("PetTown3D_Save");Message("Save removed. Restart Play mode.");}
            if(FancyButton(new Rect(w-309,319,220,38),"Close",mint))mobileMenuOpen=false;
        }
        if(mode==Mode.Build || mode==Mode.Furniture || mode==Mode.Customize || mobileManagementOpen)
        {
            float pw=260, px=w-pw-82, py=166;
            float ph=Mathf.Max(155,h-py-83);
            Card(new Rect(px,py,pw,ph),Color.black);
            float inner=pw-34;
            int count=mobileManagementOpen?7:mode==Mode.Build?types.Length:mode==Mode.Furniture?furniture.Length:6;
            mobileScroll=GUI.BeginScrollView(new Rect(px+8,py+8,pw-16,ph-16),mobileScroll,new Rect(0,0,inner,45+count*47));
            InkLabel(new Rect(8,0,inner,32),mobileManagementOpen?"PET SHOP":mode==Mode.Build?"BUILDINGS":mode==Mode.Furniture?"DECORATE":"CHARACTER",mobileText,Color.white);
            if(mobileManagementOpen)
            {
                if(FancyButton(new Rect(8,36,inner-8,40),"2 Puppies  -120",pink))BuyPets(0,120);
                if(FancyButton(new Rect(8,83,inner-8,40),"2 Kittens  -120",mint))BuyPets(1,120);
                if(shopLevel>=2 && FancyButton(new Rect(8,130,inner-8,40),"2 Bunnies  -180",lilac))BuyPets(2,180);
                if(FancyButton(new Rect(8,177,inner-8,40),"Toggle roof",blue))ToggleMobileRoof();
                if(FancyButton(new Rect(8,224,inner-8,40),"Save",mint))SaveGame();
                if(FancyButton(new Rect(8,271,inner-8,40),"Close",pink))mobileManagementOpen=false;
            }
            else if(mode==Mode.Build) {for(int i=1;i<types.Length;i++)if(FancyButton(new Rect(8,36+i*47,inner-8,40),types[i]+"  ·  "+costs[i],i==selected?mint:cream))selected=i;}
            else if(mode==Mode.Furniture) {for(int i=0;i<furniture.Length;i++)if(FancyButton(new Rect(8,36+i*47,inner-8,40),furniture[i]+"  ·  50",i==furnitureChoice?mint:cream,GetItemIcon(FurnitureResource[i])))furnitureChoice=i;}
            else
            {
                if(FancyButton(new Rect(8,36,inner-8,40),"Hair  "+hairStyle,pink))hairStyle=(hairStyle+1)%4;
                if(FancyButton(new Rect(8,83,inner-8,40),"Face  "+faceStyle,mint))faceStyle=(faceStyle+1)%4;
                if(FancyButton(new Rect(8,130,inner-8,40),"Body  "+bodyStyle,blue))bodyStyle=(bodyStyle+1)%4;
                if(FancyButton(new Rect(8,177,inner-8,40),"Legs  "+legStyle,lilac))legStyle=(legStyle+1)%4;
                if(FancyButton(new Rect(8,224,inner-8,40),"Accessory  "+accessory,pink))accessory=(accessory+1)%3;
                if(FancyButton(new Rect(8,271,inner-8,40),"Apply",mint)){RebuildHero();SaveGame();SetMode(Mode.Play);}
            }
            GUI.EndScrollView();
        }
        if(Time.time<noticeTime)
        {
            Card(new Rect(268,h-59,Mathf.Min(500,w-360),35),Color.black);
            InkLabel(new Rect(278,h-56,Mathf.Min(480,w-380),29),notice,mobileSmall,Color.white);
        }
        GUI.matrix=previous;
    }

    // The old OnGUI-based HUD (DrawDesktopHUD/DrawMobileHUD below) is retired — it's Unity's
    // original 2005-era immediate-mode GUI system, which is exactly what read as "20-year-old
    // game" about this UI. It's left in place, unused, rather than deleted, in case any of its
    // helper methods (FancyButton/Card/FlatRect/InkLabel) are wanted again later. The real HUD
    // now is the uGUI Canvas built in BuildUI()/kept in sync by RefreshUI() (see the "Modern
    // uGUI HUD" region below CreateHero).
    void OnGUI()
    {
        return;
        if(titleStyle==null)
        {
            titleStyle=new GUIStyle(GUI.skin.label){fontSize=22,fontStyle=FontStyle.Bold};
            titleStyle.normal.textColor=Color.white;
            textStyle=new GUIStyle(GUI.skin.label){fontSize=15};textStyle.normal.textColor=Color.white;
        }
        GUI.Box(new Rect(8,8,280,mode==Mode.Start?230:460),"PET TOWN 3D  |  PROTOTYPE");
        GUILayout.BeginArea(new Rect(20,35,260,mode==Mode.Start?195:425));
        GUILayout.Label("Coins: "+money+"    Residents: "+population,textStyle);
        GUILayout.Label("Day "+day+"  |  Shop Lv "+shopLevel+"  |  Fame "+reputation,textStyle);
        GUILayout.Label("Dogs "+dogStock+" / Cats "+catStock+" / Bunnies "+rabbitStock,textStyle);
        GUILayout.Label("Adoptions: "+sold+"  |  Missed: "+missedOrders,textStyle);
        if(orderPet>=0) GUILayout.Label("CUSTOMER WANTS: "+petNames[orderPet]+" ("+Mathf.CeilToInt(orderDeadline-Time.time)+"s)",textStyle);
        if(mode==Mode.Start)
        {
            GUILayout.Space(12);GUILayout.Label("Click anywhere on dry land",textStyle);
            GUILayout.Label("to choose your Pet Shop location.",textStyle);
            GUILayout.Label("River and hills are part of the map.",textStyle);
        }
        else
        {
            if(GUILayout.Button("Explore / Move (WASD)")) SetMode(Mode.Play);
            if(GUILayout.Button("Build town / choose building"))SetMode(Mode.Build);
            if(GUILayout.Button("Decorate Pet Shop"))SetMode(Mode.Furniture);
            if(GUILayout.Button("Customize my character"))SetMode(Mode.Customize);
            if(GUILayout.Button("Order 2 puppies (-120)")) BuyPets(0,120);
            if(GUILayout.Button("Order 2 kittens (-120)")) BuyPets(1,120);
            if(shopLevel>=2 && GUILayout.Button("Order 2 bunnies (-180)")) BuyPets(2,180);
            if(orderPet>=0 && GUILayout.Button("SERVE CUSTOMER [E]")) ServeOrder();
            if(reputation>=shopLevel*10 && GUILayout.Button("Upgrade Shop Level (-"+(shopLevel*400)+")"))
            {if(money>=shopLevel*400){money-=shopLevel*400;shopLevel++;Message("Shop upgraded! Bunny stock unlocks at level 2.");SaveGame();}}
            if(GUILayout.Button("Toggle roof view")){showRoofs=!showRoofs;foreach(var b in buildings) if(b!=null)foreach(var r in b.GetComponentsInChildren<Transform>(true))if(r.name=="Toggle_Roof"&&!(r.parent==b.transform&&b.transform.Find("Expansion_Shell")!=null))r.gameObject.SetActive(showRoofs);}
            if(GUILayout.Button("Save game (F5)"))SaveGame();
            if(GUILayout.Button("Reset save")){PlayerPrefs.DeleteKey("PetTown3D_Save");Message("Save removed. Restart Play mode.");}
        }
        GUILayout.EndArea();
        if(mode==Mode.Build)
        {
            GUI.Box(new Rect(Screen.width-235,8,225,350),"BUILDINGS");
            GUILayout.BeginArea(new Rect(Screen.width-225,38,205,310));
            for(int i=1;i<types.Length;i++){int k=i;if(GUILayout.Button(types[i]+"  "+costs[i]))selected=k;}
            GUILayout.Label("Selected: "+(selected>=0?types[selected]:"None"),textStyle);GUILayout.EndArea();
        }
        if(mode==Mode.Furniture)
        {
            GUI.Box(new Rect(Screen.width-230,8,220,275),"FURNITURE (50 coins)");
            GUILayout.BeginArea(new Rect(Screen.width-220,38,200,230));
            for(int i=0;i<furniture.Length;i++){int k=i;if(GUILayout.Button(furniture[i]))furnitureChoice=k;}
            GUILayout.Label("Selected: "+furniture[Mathf.Clamp(furnitureChoice,0,furniture.Length-1)],textStyle);GUILayout.EndArea();
        }
        if(mode==Mode.Customize)
        {
            GUI.Box(new Rect(Screen.width-245,8,235,275),"PLAYER CUSTOMIZATION");
            GUILayout.BeginArea(new Rect(Screen.width-235,38,215,230));
            if(GUILayout.Button("Hair style: "+hairStyle))hairStyle=(hairStyle+1)%4;
            if(GUILayout.Button("Face style: "+faceStyle))faceStyle=(faceStyle+1)%4;
            if(GUILayout.Button("Body style: "+bodyStyle))bodyStyle=(bodyStyle+1)%4;
            if(GUILayout.Button("Leg style: "+legStyle))legStyle=(legStyle+1)%4;
            if(GUILayout.Button("Accessory: "+accessory))accessory=(accessory+1)%3;
            if(GUILayout.Button("Apply character")){RebuildHero();SaveGame();SetMode(Mode.Play);}
            GUILayout.EndArea();
        }
        GUI.Box(new Rect(Screen.width/2-265,Screen.height-70,530,62),"");
        GUI.Label(new Rect(Screen.width/2-250,Screen.height-64,510,25),"WASD move | Right drag rotate | Q/E 45 deg | R reset | Wheel zoom | F5 save",textStyle);
        if(Time.time<noticeTime)GUI.Label(new Rect(Screen.width/2-250,Screen.height-38,510,22),notice,textStyle);
        GUI.Label(new Rect(Screen.width-330,Screen.height-35,320,25),"Far southeast: DARK PET SHOP (preview)",textStyle);
    }

    // v13: transparent PNG HUD sprites loaded from Resources/HUD; original gameplay preserved.
    GUIStyle mobileTitle, mobileText, mobileButton, mobileSmall, mobileCenter;
    Texture2D mobileWhite;
    readonly System.Collections.Generic.Dictionary<string,Texture2D> hudSprites = new System.Collections.Generic.Dictionary<string,Texture2D>();
    Texture2D HudSprite(string name) { Texture2D t; if (!hudSprites.TryGetValue(name,out t)) { t=Resources.Load<Texture2D>("HUD/"+name); hudSprites[name]=t; } return t; }
    void HudImage(Rect r,string name) { var t=HudSprite(name); if(t!=null)GUI.DrawTexture(r,t,ScaleMode.StretchToFill,true); }
    Color cream = Color.white, cocoa = Color.black;
    Color mint = Color.white, pink = Color.white;
    Color blue = Color.white, amber = Color.white;
    Color lilac = Color.white;
    Vector2 mobileScroll;
    bool mobileMenuOpen;
    bool mobileManagementOpen;
    Texture2D uiFontTexture;
    Font uiFont;
    void InitMobileHUD()
    {
        if(mobileTitle!=null)return;
        uiFont=Resources.Load<Font>("Fonts/Jua-Regular") ?? Resources.Load<Font>("Fonts/NanumSquareB");
        mobileTitle=new GUIStyle(GUI.skin.label){fontSize=21,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,font=uiFont};
        mobileText=new GUIStyle(GUI.skin.label){fontSize=16,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,wordWrap=true,font=uiFont};
        mobileSmall=new GUIStyle(mobileText){fontSize=13};
        mobileCenter=new GUIStyle(mobileText){alignment=TextAnchor.MiddleCenter};
        mobileButton=new GUIStyle(mobileText){fontSize=15,alignment=TextAnchor.MiddleCenter};
    }
    void FlatRect(Rect r, Color fill, float alpha=.30f)
    {
        Color prev=GUI.color;
        GUI.color=new Color(fill.r,fill.g,fill.b,alpha);
        GUI.DrawTexture(r,Texture2D.whiteTexture);
        GUI.color=Color.black;
        GUI.DrawTexture(new Rect(r.x,r.y,r.width,2),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x,r.yMax-2,r.width,2),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x,r.y,2,r.height),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax-2,r.y,2,r.height),Texture2D.whiteTexture);
        GUI.color=prev;
    }
    void Card(Rect r,Color fill) { FlatRect(r,Color.black,.30f); }
    void InkLabel(Rect r,string text,GUIStyle style,Color color)
    {
        Color old=style.normal.textColor;style.normal.textColor=color;
        GUI.Label(r,text,style);style.normal.textColor=old;
    }
    bool FancyButton(Rect r,string label,Color color,bool active=false)
    {
        FlatRect(r,Color.black,.30f);
        InkLabel(new Rect(r.x+3,r.y+2,r.width-6,r.height-4),label,mobileButton,Color.white);
        return GUI.Button(r,GUIContent.none,GUIStyle.none);
    }
    // Same button, but with a real rendered thumbnail (from GetItemIcon) to the left of the
    // label instead of plain text, for the DECORATE furniture list. Falls back to the plain
    // text-only layout while the icon hasn't finished generating yet (first frame or two).
    bool FancyButton(Rect r,string label,Color color,Texture2D icon)
    {
        FlatRect(r,Color.black,.30f);
        if(icon!=null)
        {
            float iconSize=Mathf.Min(r.height-8f,40f);
            Color old=GUI.color; GUI.color=Color.white;
            GUI.DrawTexture(new Rect(r.x+4,r.y+(r.height-iconSize)/2f,iconSize,iconSize),icon,ScaleMode.ScaleToFit,true);
            GUI.color=old;
            InkLabel(new Rect(r.x+iconSize+9,r.y+2,r.width-iconSize-13,r.height-4),label,mobileButton,Color.white);
        }
        else InkLabel(new Rect(r.x+3,r.y+2,r.width-6,r.height-4),label,mobileButton,Color.white);
        return GUI.Button(r,GUIContent.none,GUIStyle.none);
    }
    bool IconButton(Rect r,string sprite,string label)
    {
        // No square backdrop or border. Center caption beneath the artwork.
        var t=HudSprite(sprite);
        float iconSize=Mathf.Min(r.width-8f,r.height-19f);
        Rect art=new Rect(r.x+(r.width-iconSize)*.5f,r.y+1f,iconSize,iconSize);
        if(t!=null)
        {
            Color old=GUI.color;
            GUI.color=Color.white;
            GUI.DrawTexture(art,t,ScaleMode.ScaleToFit,true);
            GUI.color=old;
        }
        GUIStyle centered=new GUIStyle(mobileSmall){alignment=TextAnchor.MiddleCenter,clipping=TextClipping.Clip};
        InkLabel(new Rect(r.x,r.y+r.height-17f,r.width,17f),label,centered,Color.white);
        return GUI.Button(r,GUIContent.none,GUIStyle.none);
    }
    void DrawMobileHUD()
    {
        InitMobileHUD();
        float scale=Mathf.Min(Screen.width/390f,Screen.height/780f);
        float w=Screen.width/scale,h=Screen.height/scale;
        Matrix4x4 previous=GUI.matrix;
        GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        // Top status: small, independent capsules leave the world visible.
        Card(new Rect(9,10,w*.55f,66),new Color(.44f,.68f,.39f,.96f));
        InkLabel(new Rect(22,14,w*.5f,25),"PET TOWN 3D",mobileTitle,Color.white);
        InkLabel(new Rect(22,41,w*.5f,26),"LV "+shopLevel+"  |  Day "+day+"  |  Fame "+reputation,mobileSmall,cream);
        Card(new Rect(w*.59f,10,w*.39f,42),cocoa);
        InkLabel(new Rect(w*.60f,13,w*.36f,34),"●  "+money.ToString("N0"),mobileCenter,cream);
        Card(new Rect(w*.59f,54,w*.39f,36),new Color(.37f,.23f,.16f));
        InkLabel(new Rect(w*.60f,57,w*.36f,29),"Residents  "+population,mobileCenter,cream);
        if(mode==Mode.Start)
        {
            Card(new Rect(12,105,w-24,73),new Color(.96f,.80f,.66f));
            InkLabel(new Rect(24,110,w-48,64),"Tap dry land to place your first Pet Shop.",mobileText,cocoa);
        }
        else
        {
            Card(new Rect(10,102,w-20,55),new Color(.40f,.67f,.40f,.97f));
            InkLabel(new Rect(22,105,w-44,22),"PETS   Dog "+dogStock+"   Cat "+catStock+"   Bunny "+rabbitStock,mobileSmall,Color.white);
            if(orderPet>=0)
            {
                InkLabel(new Rect(22,128,w-139,23),"Order: "+petNames[orderPet]+"  "+Mathf.Max(0,Mathf.CeilToInt(orderDeadline-Time.time))+"s",mobileSmall,cream);
                if(FancyButton(new Rect(w-120,124,102,29),"SERVE",amber))ServeOrder();
            }
            else InkLabel(new Rect(22,127,w-44,23),"Adoptions  "+sold+"   |   Waiting for customer",mobileSmall,cream);
            // Right-side utility buttons. Each is a genuine gameplay action.
            float rx=w-66;
            if(IconButton(new Rect(rx,190,57,57),"shop","SHOP"))mobileManagementOpen=!mobileManagementOpen;
            if(IconButton(new Rect(rx,250,57,57),"roof","ROOF"))ToggleMobileRoof();
            if(IconButton(new Rect(rx,310,57,57),"save","SAVE"))SaveGame();
            if(IconButton(new Rect(rx,370,57,57),"more","MORE"))mobileMenuOpen=!mobileMenuOpen;
            if(mobileMenuOpen)
            {
                Card(new Rect(12,h-231,w-24,103),new Color(.96f,.89f,.76f));
                if(FancyButton(new Rect(22,h-221,(w-52)/2,40),"Reset save",pink)) {PlayerPrefs.DeleteKey("PetTown3D_Save");Message("Save removed. Restart Play mode.");}
                if(FancyButton(new Rect(w/2+4,h-221,(w-52)/2,40),"Close",mint))mobileMenuOpen=false;
                InkLabel(new Rect(22,h-176,w-44,33),"Camera: drag to rotate / pinch to zoom",mobileSmall,cocoa);
            }
            // Compact bottom navigation.
            float bw=49f; // Four small buttons clustered in the bottom-left
            string[] labels={"Explore","Build","Decorate","Style"};
            string[] icons={"adopt","build","decorate","style"};
            Mode[] modes={Mode.Play,Mode.Build,Mode.Furniture,Mode.Customize};
            Color[] colors={mint,blue,amber,pink};
            for(int i=0;i<4;i++)
            {
                Rect r=new Rect(8+i*(bw+4),h-59,bw,49);
                if(IconButton(r,icons[i],labels[i])){SetMode(modes[i]);mobileManagementOpen=false;}
            }
            // Open drawers only when needed; never cover the playfield by default.
            if(mode==Mode.Build || mode==Mode.Furniture || mode==Mode.Customize || mobileManagementOpen)
            {
                float top=170, bottom=h-92, panelH=Mathf.Min(405,bottom-top);
                Rect panel=new Rect(10,top,w-80,panelH);
                Card(panel,new Color(.99f,.94f,.83f,.97f));
                float inner=panel.width-26;
                int count=mode==Mode.Build?types.Length:mode==Mode.Furniture?furniture.Length:mode==Mode.Customize?6:7;
                if(mobileManagementOpen)count=7;
                mobileScroll=GUI.BeginScrollView(new Rect(panel.x+9,panel.y+9,panel.width-18,panel.height-18),mobileScroll,new Rect(0,0,inner,44+count*51));
                bool manage=mobileManagementOpen;
                InkLabel(new Rect(8,0,inner,32),manage?"PET SHOP":mode==Mode.Build?"BUILDINGS":mode==Mode.Furniture?"DECORATE":"CHARACTER",mobileText,cocoa);
                if(!manage && mode==Mode.Build)
                {
                    for(int i=1;i<types.Length;i++)if(FancyButton(new Rect(8,36+i*51,inner-6,44),types[i]+"  ·  "+costs[i],i==selected?mint:cream))selected=i;
                }
                else if(!manage && mode==Mode.Furniture)
                {
                    for(int i=0;i<furniture.Length;i++)if(FancyButton(new Rect(8,36+i*51,inner-6,44),furniture[i]+"  ·  50",i==furnitureChoice?mint:cream,GetItemIcon(FurnitureResource[i])))furnitureChoice=i;
                }
                else if(!manage && mode==Mode.Customize)
                {
                    if(FancyButton(new Rect(8,36,inner-6,44),"Hair  "+hairStyle,pink))hairStyle=(hairStyle+1)%4;
                    if(FancyButton(new Rect(8,87,inner-6,44),"Face  "+faceStyle,cream))faceStyle=(faceStyle+1)%4;
                    if(FancyButton(new Rect(8,138,inner-6,44),"Body  "+bodyStyle,blue))bodyStyle=(bodyStyle+1)%4;
                    if(FancyButton(new Rect(8,189,inner-6,44),"Legs  "+legStyle,lilac))legStyle=(legStyle+1)%4;
                    if(FancyButton(new Rect(8,240,inner-6,44),"Accessory  "+accessory,pink))accessory=(accessory+1)%3;
                    if(FancyButton(new Rect(8,291,inner-6,44),"Apply",mint)){RebuildHero();SaveGame();SetMode(Mode.Play);}
                }
                else
                {
                    if(FancyButton(new Rect(8,36,inner-6,44),"2 Puppies  ·  120",pink))BuyPets(0,120);
                    if(FancyButton(new Rect(8,87,inner-6,44),"2 Kittens  ·  120",cream))BuyPets(1,120);
                    if(shopLevel>=2 && FancyButton(new Rect(8,138,inner-6,44),"2 Bunnies  ·  180",lilac))BuyPets(2,180);
                    if(reputation>=shopLevel*10 && FancyButton(new Rect(8,189,inner-6,44),"Upgrade  ·  "+shopLevel*400,mint))
                    {if(money>=shopLevel*400){money-=shopLevel*400;shopLevel++;Message("Shop upgraded!");SaveGame();}}
                    if(FancyButton(new Rect(8,240,inner-6,44),"Toggle roof",blue))ToggleMobileRoof();
                    if(FancyButton(new Rect(8,291,inner-6,44),"Save",mint))SaveGame();
                    if(FancyButton(new Rect(8,342,inner-6,44),"Close",amber))mobileManagementOpen=false;
                }
                GUI.EndScrollView();
            }
        }
        if(Time.time<noticeTime)
        {
            Card(new Rect(45,h-118,w-90,31),cocoa);
            InkLabel(new Rect(53,h-116,w-106,26),notice,mobileSmall,cream);
        }
        GUI.matrix=previous;
    }
    void ToggleMobileRoof()
    {
        showRoofs=!showRoofs;
        foreach(var b in buildings)if(b!=null)foreach(var r in b.GetComponentsInChildren<Transform>(true))if(r.name=="Toggle_Roof"&&!(r.parent==b.transform&&b.transform.Find("Expansion_Shell")!=null))r.gameObject.SetActive(showRoofs);
    }


    void BuyPets(int pet,int price)
    {
        if(pet<0||pet>2||loadingTown||expansionPending)return;
        OpenPetOrder(pet);
    }
    void StartOrder()
    {
        if(shop==null)return;
        adoptionGeneration++;
        orderTimer=0;orderPet=UnityEngine.Random.Range(0,shopLevel>=2?3:2);
        orderDeadline=Time.time+42f;
        Vector3 p=shopPosition+new Vector3(0,0,-7);
        waitingCustomer=new GameObject("WAITING_CUSTOMER_REPLACEABLE");
        waitingCustomer.transform.position=new Vector3(p.x,Ground(p.x,p.z),p.z);
        // User request: no visible customer model standing in the shop (a new order starts
        // automatically ~8s after the last one ends, so a model there read as "the same citizen
        // always stuck inside"). waitingCustomer stays as an invisible position marker only —
        // the order timer/messages/ServeOrder flow are unaffected.
        Message("Customer arrived! Wants a "+petNames[orderPet]+". Press E or SERVE.");
    }
    void ServeOrder(){OpenAdoptionSelection();}
    void FailOrder()
    {
        Progression.serviceHappiness=Mathf.Max(-20,Progression.serviceHappiness-1);
        missedOrders++;reputation=Mathf.Max(0,reputation-1);
        Message("Customer left without a pet. -1 fame");EndOrder();SaveGame();
    }
    void EndOrder()
    {
        EndProgressionOrder();
        orderPet=-1;orderTimer=0;
        if(waitingCustomer!=null)Destroy(waitingCustomer);
        waitingCustomer=null;
    }
    // Real pet models live in Resources/pets/<name>.glb. Add more paths here as more are
    // made (dog_2, cat_1, ...) — assigned round-robin across the 4 shop display slots, and
    // any slot beyond what's been added yet falls back to the simple placeholder shape.
    static readonly string[] PetResources = { "Characters/Pets/Dog_Maltese_Beige/Dog_Maltese_Beige", "Characters/Pets/Dog_Maltese_Beige/Dog_Maltese_Beige", "Characters/Pets/Dog_Maltese_Beige/Dog_Maltese_Beige" };
    class LivePet { public bool inventory;public string careId;public int growthReadyFrame; public GameObject root; public GameObject model; public VillagerAnim animation; public Vector3 home, target; public float nextPick, phase, speed; public bool flying; public NavMeshAgent agent; public Vector3 lastPosition; public float stuckTime; }
    readonly List<LivePet> livePets = new List<LivePet>();
    class TankFish { public Quaternion orientation=Quaternion.identity; public Transform root; public Vector3 center; public float phase; public VillagerAnim animation; }
    readonly List<TankFish> tankFish = new List<TankFish>();
    void CreatePetDisplays()
    {
        if (shop == null) return;
        foreach (var old in livePets) if (old.root != null) Destroy(old.root);
        livePets.Clear();
        string[] paths = { "Characters/Pets/Dog_Maltese_Beige/Dog_Maltese_Beige", "Characters/Pets/Dog_Maltese_Beige/Dog_Maltese_Beige", "Characters/Pets/Dog_Maltese_Beige/Dog_Maltese_Beige" };
        Vector3[] offsets = { new Vector3(-3.6f,0,-1f), new Vector3(-1.5f,0,2f), new Vector3(2.6f,0,1.5f) };
        for (int i=0; i<3; i++)
        {
            var prefab = Resources.Load<GameObject>(paths[i]);
            if (prefab == null) { Debug.LogWarning("PetTown: missing pet prefab: " + paths[i]); continue; }
            Vector3 pos = shopPosition + offsets[i]; pos.y = Ground(pos.x,pos.z);
            var root = new GameObject("LIVE_PET_" + paths[i].Substring(5)); root.transform.position=pos;
            var model = Instantiate(prefab, root.transform); model.name="Rigged_Model";
            model.transform.localPosition=Vector3.zero; model.transform.localRotation=Quaternion.identity;
            foreach(var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            MarkNavMeshIgnore(root);
            StartCoroutine(FitModelNextFrame(model,pos.y,i==2?.42f:.75f));
            VillagerAnim anim; SetupPetAnimation(model,paths[i],out anim);
            PetTownVisualGrounding.Attach(this,root.transform,model.transform);
            var petAgent=root.AddComponent<NavMeshAgent>();
            petAgent.radius=i==2?.18f:.25f; petAgent.height=i==2?.45f:.8f;
            petAgent.speed=i==2?1.25f:.9f; petAgent.angularSpeed=400f;
            petAgent.acceleration=8f; petAgent.stoppingDistance=.22f;
            petAgent.updateRotation=true; petAgent.autoBraking=true;
            if (NavMesh.SamplePosition(pos,out NavMeshHit petHit,2f,NavMesh.AllAreas)) petAgent.Warp(petHit.position);
            livePets.Add(new LivePet { root=root,model=model,animation=anim,agent=petAgent,
                lastPosition=root.transform.position,home=pos,target=pos,careId=root.name+"_"+i,growthReadyFrame=Time.frameCount+4,
                nextPick=Time.time+UnityEngine.Random.Range(1f,3f),speed=i==2?1.25f:.9f });
        }
    }
    // Same idle/walk clip resolution SpawnVillagerModel uses (legacy Animation, Animator with a
    // wired controller, or raw sub-asset clips glTFast left unwired) kept separate so villager
    // behavior is never risked by pet-specific changes.
    // glTFast Legacy imports expose an Animation component on the imported model,
    // but sometimes its states are empty even though Resources.LoadAll finds the clip.
    // Register the clip on THAT component (same hierarchy as the animated bones),
    // not on an extra Animation added to the outer instance root.
    bool SetupPetAnimation(GameObject instance, string resourcePath, out VillagerAnim animInfo)
    {
        animInfo = null;
        Animation legacy = instance.GetComponentInChildren<Animation>(true);
        Animator animator = instance.GetComponentInChildren<Animator>(true);
        AnimationClip[] clips = Resources.LoadAll<AnimationClip>(resourcePath);
        if (legacy != null)
        {
            // glTFast's Animation component is the correct playback owner.
            // Some imported clips are present as sub-assets but not registered as states.
            string first = null, idle = null, walk = null, run = null;
            foreach (AnimationState state in legacy)
            {
                if (state == null || state.clip == null) continue;
                state.wrapMode = WrapMode.Loop;
                ClassifyClip(state.name, ref idle, ref walk, ref run, ref first);
            }
            if (clips != null)
            {
                foreach (AnimationClip source in clips)
                {
                    if (source == null) continue;
                    // Clone rather than mutate an imported shared clip asset.
                    string key = source.name;
                    if (legacy.GetClip(key) == null)
                    {
                        AnimationClip copy = Instantiate(source);
                        copy.name = key;
                        copy.legacy = true;
                        copy.wrapMode = WrapMode.Loop;
                        legacy.AddClip(copy, key);
                    }
                    AnimationState state = legacy[key];
                    if (state == null) continue;
                    state.wrapMode = WrapMode.Loop;
                    ClassifyClip(key, ref idle, ref walk, ref run, ref first);
                }
            }
            string selected = walk ?? run ?? first ?? idle;
            if (!string.IsNullOrEmpty(selected))
            {
                legacy.enabled = true;
                legacy.wrapMode = WrapMode.Loop;
                if (animator != null) animator.enabled = false;
                bool started = legacy.Play(selected);
                if (started)
                {
                    // These three GLBs have one unnamed locomotion clip; use it
                    // continuously rather than stopping on each short wander pause.
                    animInfo = new VillagerAnim { anim=legacy, idleClip=selected,
                        walkClip=selected, runClip=selected, model=instance.transform };
                    Debug.Log("PetTown PET ANIMATION PLAYING: " + resourcePath +
                        " | Clip=" + selected + " | Owner=" + legacy.gameObject.name);
                    return true;
                }
            }
        }
        // Preserve the existing Mecanim fallback for other pet imports.
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            string first = null, idle = null, walk = null, run = null;
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null) ClassifyClip(clip.name, ref idle, ref walk, ref run, ref first);
            string selected = walk ?? run ?? first ?? idle;
            if (!string.IsNullOrEmpty(selected))
            {
                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.Play(selected, 0, 0f);
                animInfo = new VillagerAnim { animator=animator, idleClip=selected,
                    walkClip=selected, runClip=selected, model=instance.transform };
                return true;
            }
        }
        Debug.LogWarning("PetTown PET ANIMATION NOT FOUND: " + resourcePath +
            " | Legacy=" + (legacy != null) + " | Animator=" + (animator != null) +
            " | Controller=" + (animator != null && animator.runtimeAnimatorController != null) +
            " | ResourceClips=" + (clips == null ? 0 : clips.Length));
        return false;
    }
    // Moves each pet across the shop interior and drives its walk/idle
    // animation. Kept out of the villager wander system entirely (no NavMeshAgent, no shared
    // dictionaries) so it can never wander out of the shop and never risks villager behavior.
    void UpdatePet(float dt)
    {
        foreach(var pet in livePets)
        {
            if(pet.root==null) continue;
            Vector3 cur=pet.root.transform.position;
            var agent=pet.agent;
            bool valid=agent!=null && agent.enabled && agent.isOnNavMesh;
            bool arrived=valid && !agent.pathPending && agent.remainingDistance<.38f;
            if(Time.time>pet.nextPick || arrived || (valid && agent.pathStatus==NavMeshPathStatus.PathInvalid))
            {
                // Sample reachable destinations inside the pet shop, not arbitrary world positions.
                bool found=false;bool caged=TryShopSlotAnchor(pet,out Vector3 slotAnchor);float limit=caged?7.4f:6.25f;
                for(int attempt=0;attempt<18;attempt++)
                {
                    float x=UnityEngine.Random.Range(-6.0f,6.0f);
                    float z=UnityEngine.Random.Range(-6.0f,6.0f);
                    Vector3 candidate=caged?PetTownShopSlots.RoamPoint(slotAnchor,UnityEngine.Random.value,UnityEngine.Random.value):shopPosition+new Vector3(x,0,z);
                    candidate.y=Ground(candidate.x,candidate.z);
                    if(!NavMesh.SamplePosition(candidate,out NavMeshHit hit,.9f,NavMesh.AllAreas))continue;
                    if(Mathf.Abs(hit.position.x-shopPosition.x)>limit ||
                       Mathf.Abs(hit.position.z-shopPosition.z)>limit)continue;
                    if(valid)
                    {
                        NavMeshPath path=new NavMeshPath();
                        if(!agent.CalculatePath(hit.position,path) || path.status!=NavMeshPathStatus.PathComplete)continue;
                        agent.SetPath(path);
                    }
                    pet.target=hit.position; found=true; break;
                }
                pet.nextPick=Time.time+(found?UnityEngine.Random.Range(12f,20f):2f);
            }
            float actualSpeed=dt>.0001f?Vector3.Distance(cur,pet.lastPosition)/dt:0f;
            pet.lastPosition=cur;
            bool moving=valid && actualSpeed>.06f;
            if(valid && agent.hasPath && !agent.pathPending && agent.remainingDistance>.6f && actualSpeed<.035f)
            {
                pet.stuckTime+=dt;
                if(pet.stuckTime>2.5f){agent.ResetPath();pet.nextPick=0f;pet.stuckTime=0f;}
            }
            else pet.stuckTime=0f;
            if(pet.inventory){UpdateVillagerAnim(pet.root,moving,false);WobbleClipless(pet,moving,dt);continue;}
            if(pet.animation!=null)
            {
                string target=moving?(pet.animation.walkClip??pet.animation.idleClip):pet.animation.idleClip;
                if(!string.IsNullOrEmpty(target))
                {
                    if(pet.animation.anim!=null && !pet.animation.anim.IsPlaying(target)) pet.animation.anim.CrossFade(target,.15f);
                    else if(pet.animation.animator!=null && !pet.animation.animator.GetCurrentAnimatorStateInfo(0).IsName(target)) pet.animation.animator.CrossFade(target,.15f);
                }
            }
        }
        foreach(var fish in tankFish)
        {
            if(fish.root==null) continue;
            fish.phase+=dt*.65f;
            fish.root.position=fish.center+fish.orientation*new Vector3(Mathf.Sin(fish.phase)*.38f,Mathf.Sin(fish.phase*1.6f)*.12f,Mathf.Cos(fish.phase)*.16f);
            Vector3 direction=new Vector3(Mathf.Cos(fish.phase)*.38f,Mathf.Cos(fish.phase*1.6f)*.12f,-Mathf.Sin(fish.phase)*.16f);
            if(direction.sqrMagnitude>.0001f) fish.root.rotation=fish.orientation*Quaternion.LookRotation(direction);
        }
    }

    // 걷기 동작이 없는 펫(햄스터·쥐·친칠라 등)은 좌우로 뒤뚱뒤뚱 흔들며 걷는다.
    void WobbleClipless(LivePet pet,bool moving,float dt)
    {
        if(pet==null||!pet.model)return;
        var a=pet.animation;if(a!=null&&(!string.IsNullOrEmpty(a.walkClip)||!string.IsNullOrEmpty(a.idleClip)))return;
        var t=pet.model.transform;float yaw=t.localEulerAngles.y;
        if(moving){pet.phase+=dt*12f;float s=Mathf.Sin(pet.phase);t.localRotation=Quaternion.Euler(Mathf.Sin(pet.phase*2f)*3f,yaw,s*11f);t.localPosition=new Vector3(0,Mathf.Abs(s)*.035f,0);}
        else{t.localRotation=Quaternion.Slerp(t.localRotation,Quaternion.Euler(0,yaw,0),Mathf.Clamp01(dt*10f));t.localPosition=Vector3.Lerp(t.localPosition,Vector3.zero,Mathf.Clamp01(dt*10f));}
    }
    // Waits one frame after Instantiate() before measuring Renderer.bounds — by then Unity
    // has actually run its skinning/bounds update, so this reads the model's TRUE on-screen
    // size instead of a same-frame value that kept coming out wildly wrong (both far too small
    // and, when blindly trusted and corrected for, far too large).
    IEnumerator FitPetModelNextFrame(GameObject instance, float bedTopY, float targetHeight, int slotIndex)
    {
        yield return null;
        if (instance == null) yield break;
        var renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) yield break;
        Bounds b = renderers[0].bounds;
        for (int r = 1; r < renderers.Length; r++) b.Encapsulate(renderers[r].bounds);
        if (b.size.y <= 0.0001f)
        {
            Debug.LogWarning("Pet Town 3D pet display " + slotIndex + ": still no usable bounds one frame later (size.y=" + b.size.y + "), leaving at native scale.");
            yield break;
        }
        float feetWorldY = b.min.y;
        float scale = Mathf.Clamp(targetHeight / b.size.y, .001f, 2000f);
        instance.transform.localScale = Vector3.one * scale;
        // Re-measure after scaling — scaling the transform doesn't retroactively change the
        // bounds we already read, so recompute from the renderers again post-scale for an
        // exact fit instead of assuming the pre-scale ratio carries over perfectly.
        renderers = instance.GetComponentsInChildren<Renderer>();
        b = renderers[0].bounds;
        for (int r = 1; r < renderers.Length; r++) b.Encapsulate(renderers[r].bounds);
        instance.transform.position += Vector3.up * (bedTopY - b.min.y);
        Debug.Log("Pet Town 3D pet display " + slotIndex + ": next-frame scale=" + scale + " finalHeight=" + b.size.y + " finalPos=" + instance.transform.position);
    }
    // Generic version of the fitting coroutine above — waits one frame so Renderer.bounds is
    // trustworthy, then uniformly scales to targetHeight and drops the model so its lowest
    // point sits exactly at groundY. Reused for anything instantiated from a prefab at runtime
    // that previously measured bounds on the same frame as Instantiate() (rocks, fences,
    // gates, houses, furniture) — same-frame bounds are the exact bug that made the pet and
    // furniture models randomly come out invisible-tiny or screen-filling-huge.
    IEnumerator FitModelNextFrame(GameObject instance, float groundY, float targetHeight)
    {
        yield return null;
        if (instance == null) yield break;
        var renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) yield break;
        Bounds b = renderers[0].bounds;
        for (int r = 1; r < renderers.Length; r++) b.Encapsulate(renderers[r].bounds);
        if (b.size.y <= 0.0001f) yield break;
        float scale = Mathf.Clamp(targetHeight / b.size.y, .001f, 2000f);
        instance.transform.localScale = Vector3.one * scale;
        renderers = instance.GetComponentsInChildren<Renderer>();
        b = renderers[0].bounds;
        for (int r = 1; r < renderers.Length; r++) b.Encapsulate(renderers[r].bounds);
        instance.transform.position += Vector3.up * (groundY - b.min.y);
    }
    [Serializable] class SaveData
    {
        public int coins,population,sold,inventory,hair,face,body,legs,acc,rep,level,day,dogs,cats,bunnies,done,missed;
        public float shopX=999,shopZ;
        public int progressionVersion;
        public PetTownProgressionState progression;
        public int constructionVersion;
        public int interiorVersion;
        public List<string> clearedNature=new List<string>();
        public List<PetTownInteriorSaveRecord> interiors=new List<PetTownInteriorSaveRecord>();
        public int roadVersion;
        public List<PetTownRoadSaveRecord> roads=new List<PetTownRoadSaveRecord>();
        public PetTownBuildingSaveRecord shopPlacement;
        public List<PetTownBuildingSaveRecord> buildingPlacements=new List<PetTownBuildingSaveRecord>();
        public List<int> types=new List<int>();
        public List<float> x=new List<float>(),z=new List<float>();
    }
    void SaveGame()
    {
        if(expansionPending)return;
        var s=CaptureTownSave();
        PlayerPrefs.SetString("PetTown3D_Save",JsonUtility.ToJson(s));PlayerPrefs.Save();progressionDirty=false;if(!silentProgressSave)Message("Game saved");
    }
    // Snapshot construction is shared with exports/validation and never writes a save slot itself.
    SaveData CaptureTownSave()
    {
        EnsureProgression();SyncProgressionStock();
        EnsureResidents().Rehouse(ResidentHomes());population=EnsureResidents().Population;
        SaveData s=new SaveData{coins=money,population=population,sold=sold,inventory=inventory,
            hair=hairStyle,face=faceStyle,body=bodyStyle,legs=legStyle,acc=accessory,
            rep=reputation,level=shopLevel,day=day,dogs=dogStock,cats=catStock,bunnies=rabbitStock,done=completedOrders,missed=missedOrders,
            shopX=shop==null?999:shopPosition.x,shopZ=shopPosition.z};
        UpdateWorldExploration(true);
        if(progression.exploration!=null)progression.exploration.PrepareSave();
        s.progressionVersion=1;s.progression=progression;
        s.clearedNature.AddRange(clearedNature);
        s.constructionVersion=2;s.roadVersion=1;s.interiorVersion=1;
        AppendInteriorSnapshot(s,shop,-1);
        foreach(var road in FindObjectsByType<PetTownRoadPath>())if(road.isActiveAndEnabled&&road.transform.root.name=="Road_REPLACEABLE"){var record=road.Capture();if(record.Valid)s.roads.Add(record);}
        var shopMetadata=shop!=null?shop.GetComponent<PetTownBuildingPlacement>():null;
        if(shopMetadata!=null)s.shopPlacement=shopMetadata.Record.Copy(-1);
        foreach(var b in buildings)
        {
            if(b==null||b==shop||b.GetComponentInChildren<PetTownRoadPath>()!=null)continue;
            string n=b.name;for(int i=0;i<types.Length;i++)if(n.StartsWith(types[i].Replace(" ","_")))
                        {
                s.types.Add(i);s.x.Add(b.transform.position.x);s.z.Add(b.transform.position.z);
                AppendInteriorSnapshot(s,b,s.types.Count-1);
                var metadata=b.GetComponent<PetTownBuildingPlacement>();
                if(metadata!=null)s.buildingPlacements.Add(metadata.Record.Copy(s.types.Count-1));
                break;
            }
        }
        s.interiors.AddRange(unresolvedInteriors);
        foreach(var p in unresolvedLegacyRoads){s.types.Add(8);s.x.Add(p.x);s.z.Add(p.z);}
        return s;
    }
    void LoadGame()
    {
        string json=PlayerPrefs.GetString("PetTown3D_Save","");
        if(string.IsNullOrEmpty(json))return;
        try
        {
            loadingTown=true;
            var s=JsonUtility.FromJson<SaveData>(json);
            if(s.clearedNature!=null){clearedNature.AddRange(s.clearedNature);foreach(var item in FindObjectsByType<PetTownNatureObstacle>())if(clearedNature.Contains(item.Id))item.gameObject.SetActive(false);}
            money=s.coins;population=s.population;sold=s.sold;inventory=s.inventory;
            hairStyle=s.hair;faceStyle=s.face;bodyStyle=s.body;legStyle=s.legs;accessory=s.acc;
            reputation=s.rep;shopLevel=Mathf.Max(1,s.level);day=Mathf.Max(1,s.day);dogStock=s.dogs;catStock=s.cats;rabbitStock=s.bunnies;completedOrders=s.done;missedOrders=s.missed;
            LoadProgression(s.progressionVersion>=1?s.progression:null);
            var loadedOwners=new Dictionary<int,GameObject>();
            if(s.shopX!=999)
            {
                Vector3 p=new Vector3(s.shopX,Ground(s.shopX,s.shopZ),s.shopZ);
                if(s.constructionVersion>=2&&s.shopPlacement!=null&&s.shopPlacement.ValidFor(0))
                {
                    p.y=s.shopPlacement.groundY;CreatePlannedBuilding(0,p,s.shopPlacement);
                }
                else CreateBuilding(0,p,false);
                loadedOwners[-1]=shop;
                CreateHero(p+new Vector3(8,0,8));CreatePetDisplays();
            }
            // Residents are restored from the versioned roster after the world/NavMesh is ready.
            for(int i=0;i<s.types.Count;i++)
            {
                Vector3 p=new Vector3(s.x[i],Ground(s.x[i],s.z[i]),s.z[i]);
                // Legacy arrays contain no centerline. Preserve their entries losslessly instead of inventing paths.
                if(s.types[i]==8){unresolvedLegacyRoads.Add(p);continue;}
                var placement=SavedPlacement(s,i,s.types[i]);
                if(placement!=null){p.y=placement.groundY;loadedOwners[i]=CreatePlannedBuilding(s.types[i],p,placement);}
                else loadedOwners[i]=CreateBuilding(s.types[i],p,false);
            }
            if(s.roadVersion>=1&&s.roads!=null)foreach(var road in s.roads)
            {
                if(road==null||!road.Valid){Debug.LogWarning("Skipped invalid saved road record");continue;}
                var parent=new GameObject("Road_REPLACEABLE");
                BuildRoadRibbon(new List<Vector3>(road.points),road.width,parent.transform,road.mode!=(int)PetTownRoadMode.GridStraight,road.type);
                parent.GetComponentInChildren<PetTownRoadPath>().RestoreIdentity(road);buildings.Add(parent);
            }
            if(s.interiorVersion>=1&&s.interiors!=null)RestoreInteriors(s.interiors,loadedOwners);
            RebakeNavMesh();
            ResidentsWorldReady();
            SaveGame(); // retain legacy fields and the optional road centerline extension
            Message("Saved town loaded");
        }catch(Exception e){Debug.LogWarning("Save could not be loaded: "+e.Message);}
        finally{loadingTown=false;}
    }
}
