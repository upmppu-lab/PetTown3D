using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Opt-in resident companions. Existing residents keep their original controller and animation.
public partial class PetTownGame
{
    sealed class ResidentCompanion
    {
        public GameObject root;public NavMeshAgent agent;public Transform owner;
        public string petId,ownerId;public Vector3 previous,previousOwner;public float nextPath,walked;public bool hasPrevious;
        public string serviceVisit;public PetTownFacilityStage serviceStage;public int serviceRevision=-1;
        public Vector3 serviceGoal,serviceOwner;public bool serviceGoalValid;
        public readonly List<AnimationClip> clips=new List<AnimationClip>();
    }
    readonly Dictionary<string,ResidentCompanion> residentCompanions=new Dictionary<string,ResidentCompanion>();
    readonly HashSet<string> unavailableCompanions=new HashSet<string>();
    readonly Dictionary<string,float> companionRetryAt=new Dictionary<string,float>();float nextCompanionSpawn;
    static string CompanionResource(PetTownResidentPetRecord p){var visual=PetVisual(p.species,p.appearance);return visual!=null&&!string.IsNullOrEmpty(visual.resourcePath)?visual.resourcePath:p.species==0?(p.appearance%2==0?"Characters/Pets/Dog_Maltese_Beige/Dog_Maltese_Beige":"Characters/Pets/Dog_Maltese_Beige/Dog_Maltese_Beige"):p.species==3?"Characters/Pets/Dog_Maltese_Beige/Dog_Maltese_Beige":null;}
    bool CompanionSupported(PetTownResidentPetRecord pet)=>pet!=null&&!string.IsNullOrEmpty(CompanionResource(pet))&&Resources.Load<GameObject>(CompanionResource(pet))!=null;
    PetTownResidentPetRecord DesiredCompanion(PetTownResidentRecord owner)
    {
        var visit=Progression.Facilities.visits.Find(v=>v.residentId==owner.id);
        string id=visit!=null?visit.petId:owner.followPetId;
        return string.IsNullOrEmpty(id)?null:owner.pets.Find(p=>p.id==id);
    }
    void UpdateResidentCompanions(float seconds)
    {
        if(loadingTown||expansionPending||!shop||seconds<=0)return;
        var state=EnsureResidents();var remove=new List<string>();
        foreach(var pair in residentCompanions)
        {
            var owner=state.people.Find(p=>p.id==pair.Key);var desired=owner!=null?DesiredCompanion(owner):null;
            GameObject actor;bool same=residentActors.TryGetValue(pair.Key,out actor)&&actor&&pair.Value.owner==actor.transform;
            if(desired==null||desired.id!=pair.Value.petId||!same||!pair.Value.root){DisposeCompanion(pair.Value);remove.Add(pair.Key);}
        }
        foreach(var id in remove)residentCompanions.Remove(id);
        foreach(var owner in state.people)
        {
            var pet=DesiredCompanion(owner);if(pet==null||!CompanionSupported(pet))continue;
            GameObject actor;if(!residentActors.TryGetValue(owner.id,out actor)||!actor)continue;
            ResidentCompanion companion;
            if(!residentCompanions.TryGetValue(owner.id,out companion))
            {
                float retryAt;
                if(Time.unscaledTime<nextCompanionSpawn||unavailableCompanions.Contains(pet.id)||(companionRetryAt.TryGetValue(owner.id,out retryAt)&&Time.unscaledTime<retryAt))continue;
                nextCompanionSpawn=Time.unscaledTime+.25f;companionRetryAt[owner.id]=Time.unscaledTime+2;
                bool assetFailed;companion=CreateResidentCompanion(owner,pet,actor.transform,out assetFailed);
                if(companion==null){if(assetFailed)unavailableCompanions.Add(pet.id);continue;}
                companionRetryAt.Remove(owner.id);residentCompanions[owner.id]=companion;
            }
            TickResidentCompanion(companion,pet,Mathf.Min(seconds,.25f));
        }
    }
    ResidentCompanion CreateResidentCompanion(PetTownResidentRecord owner,PetTownResidentPetRecord pet,Transform person,out bool assetFailed)
    {
        assetFailed=false;var ownerAgent=person.GetComponent<NavMeshAgent>();if(!ownerAgent||!ownerAgent.isOnNavMesh)return null;
        Vector3 position=Vector3.zero;bool found=false;
        for(int i=0;i<16;i++)
        {
            float angle=i*Mathf.PI/4;Vector3 candidate=person.position-person.forward*(1+i/8*.5f)+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.5f;
            NavMeshHit hit;if(!NavMesh.SamplePosition(candidate,out hit,.6f,ownerAgent.areaMask)||Mathf.Abs(hit.position.y-person.position.y)>.7f||CompanionWaterAt(hit.position))continue;
            var path=new NavMeshPath();if(!ownerAgent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete||!CompanionDryPath(path))continue;position=hit.position;found=true;break;
        }
        if(!found)return null;
        var root=new GameObject("RESIDENT_PET_"+pet.id);root.transform.position=position;MarkNavMeshIgnore(root);
        var model=Instantiate(Resources.Load<GameObject>(CompanionResource(pet)),root.transform);model.name="CompanionModel";
        PreparePetSource(model);
        var initialVisual=PetVisual(pet.species,pet.appearance);if(initialVisual!=null&&initialVisual.idle)initialVisual.idle.SampleAnimation(model,0);
        float min,max;if(PetVisualModelHeight(model,out min,out max)&&max-min>.00001f)
        {
            float scale=Mathf.Clamp((PetVisual(pet.species,pet.appearance)?.height??(pet.species>=3?.42f:.75f))/(max-min),.001f,1000);float pivot=model.transform.position.y;model.transform.localScale*=scale;model.transform.position+=Vector3.up*(position.y-(pivot+(min-pivot)*scale));
        }
        var legacy=model.GetComponentInChildren<Animation>(true);var animator=model.GetComponentInChildren<Animator>(true);
        if(legacy)legacy.Stop();if(animator){animator.enabled=false;animator.applyRootMotion=false;}
        var bones=model.GetComponentsInChildren<Transform>(true);var poses=new Vector3[bones.Length];var rotations=new Quaternion[bones.Length];var scales=new Vector3[bones.Length];
        for(int i=0;i<bones.Length;i++){poses[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;scales[i]=bones[i].localScale;}
        var result=new ResidentCompanion{root=root,owner=person,petId=pet.id,ownerId=owner.id,previous=position,previousOwner=person.position};
        if(!legacy)legacy=model.AddComponent<Animation>();string idle=null,walk=null,run=null,first=null,locomotion=null;
        foreach(var source in PetVisualClipsFor(pet))
        {
            if(!source)continue;if(locomotion==null)locomotion=source.name;var copy=Instantiate(source);copy.name=source.name;copy.legacy=true;copy.wrapMode=WrapMode.Loop;result.clips.Add(copy);legacy.AddClip(copy,copy.name);ClassifyClip(copy.name,ref idle,ref walk,ref run,ref first);
        }
        var visualDefinition=PetVisual(pet.species,pet.appearance);if(visualDefinition!=null){if(visualDefinition.idle)idle=visualDefinition.idle.name;if(visualDefinition.walk)walk=visualDefinition.walk.name;if(visualDefinition.run)run=visualDefinition.run.name;}
        foreach(AnimationState clip in legacy)if(clip!=null)ClassifyClip(clip.name,ref idle,ref walk,ref run,ref first);
        // These inspected GLBs contain one locomotion clip named "...baselayer".
        // The shared NPC classifier excludes names containing "lay"; do not change that NPC rule.
        walk=walk??run??first??locomotion;legacy.playAutomatically=false;legacy.enabled=true;
        if(string.IsNullOrEmpty(walk)||!legacy.Play(walk)){assetFailed=true;DisposeCompanion(result);return null;}
        legacy.Stop();if(idle==walk)idle=null;
        var info=new VillagerAnim{anim=legacy,walkClip=walk,runClip=run??walk,idleClip=idle,model=model.transform,poseBones=bones,restPositions=poses,restRotations=rotations,restScales=scales};
        villagerAnimInfo[root]=info;RestoreVillagerRestPose(info);
        var anchor=root.AddComponent<PetTownCompanionPose>();anchor.Initialize(bones,poses);
        PetTownVisualGrounding.Attach(this,root.transform,model.transform);
        var agent=root.AddComponent<NavMeshAgent>();agent.enabled=false;agent.agentTypeID=ownerAgent.agentTypeID;agent.radius=pet.species>=3?.18f:.25f;agent.height=pet.species>=3?.45f:.8f;agent.speed=1.8f;agent.acceleration=8;agent.angularSpeed=420;agent.stoppingDistance=.25f;agent.autoBraking=true;agent.avoidancePriority=70;agent.areaMask=ownerAgent.areaMask;result.agent=agent;agent.enabled=true;
        if(!agent.isOnNavMesh){DisposeCompanion(result);return null;}root.AddComponent<PetTownPetGrowthVisual>().InitializeResident(model.transform,pet);return result;
    }
    void TickResidentCompanion(ResidentCompanion companion,PetTownResidentPetRecord pet,float seconds)
    {
        var agent=companion.agent;if(!agent||!agent.isOnNavMesh||!companion.owner)return;
        Vector3 position=companion.root.transform.position,ownerPosition=companion.owner.position;
        float moved=companion.hasPrevious?Vector3.Distance(position,companion.previous):0,ownerMoved=companion.hasPrevious?Vector3.Distance(ownerPosition,companion.previousOwner):0;
        bool moving=moved/Mathf.Max(.0001f,seconds)>.1f;UpdateVillagerAnim(companion.root,moving,false);
        if(moving&&moved<1&&ownerMoved>.005f&&ownerMoved<1&&Vector3.Distance(position,ownerPosition)<3.5f)
        {
            companion.walked+=Mathf.Min(moved,ownerMoved);
            if(companion.walked>=20){companion.walked-=20;if(pet.walkDay!=day){pet.walkDay=day;pet.walkXP=0;}if(pet.walkXP<5){pet.walkXP++;pet.GainServiceExperience(1);progressionDirty=true;}}
        }
        companion.previous=position;companion.previousOwner=ownerPosition;companion.hasPrevious=true;
        if(Time.unscaledTime<companion.nextPath)return;companion.nextPath=Time.unscaledTime+.5f;
        if(GuideFacilityCompanion(companion,agent))return;
        if(Vector3.Distance(position,ownerPosition)<1.1f){agent.ResetPath();return;}
        for(int i=0;i<8;i++)
        {
            float angle=i*Mathf.PI/4;Vector3 goal=ownerPosition-companion.owner.forward+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.55f;NavMeshHit hit;
            if(!NavMesh.SamplePosition(goal,out hit,.55f,agent.areaMask)||Mathf.Abs(hit.position.y-ownerPosition.y)>.7f||CompanionWaterAt(hit.position))continue;
            var path=new NavMeshPath();if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete||!CompanionDryPath(path))continue;agent.speed=Vector3.Distance(position,ownerPosition)>4?2.8f:1.8f;agent.SetPath(path);return;
        }
        agent.ResetPath();
    }
    // The pet GLB mesh.bounds may be empty; its imported skin.localBounds is valid.
    // Transform all eight corners so imported axis rotations do not distort fitting.
    static bool CompanionModelHeight(GameObject model,out float min,out float max)
    {
        min=float.PositiveInfinity;max=float.NegativeInfinity;
        foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            var skin=renderer as SkinnedMeshRenderer;Bounds bounds;
            if(skin!=null)bounds=skin.localBounds;
            else{var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;bounds=filter.sharedMesh.bounds;}
            if(bounds.size.sqrMagnitude<.0000001f)continue;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                float height=renderer.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))).y;
                min=Mathf.Min(min,height);max=Mathf.Max(max,height);
            }
        }
        return min<float.PositiveInfinity&&max>min;
    }
    // The current terrain NavMesh also covers the carved river bed. Reject submerged
    // segments, but retain paths on a bridge deck above the procedural water level.
    bool CompanionWaterAt(Vector3 point)=>PetTownWorldSafety.IsWater(point.x,point.z,1.6f)&&point.y<.1f;
    bool CompanionDryPath(NavMeshPath path)
    {
        var points=path.corners;int samples=0;
        for(int i=1;i<points.Length;i++)
        {
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(points[i-1],points[i])/.5f));
            if((samples+=steps)>256)return false;
            for(int j=0;j<=steps;j++)if(CompanionWaterAt(Vector3.Lerp(points[i-1],points[i],j/(float)steps)))return false;
        }
        return true;
    }
    void DisposeCompanion(ResidentCompanion companion)
    {
        if(!ReferenceEquals(companion.root,null))villagerAnimInfo.Remove(companion.root);
        if(companion.root){companion.root.SetActive(false);Destroy(companion.root);}foreach(var clip in companion.clips)if(clip)Destroy(clip);companion.clips.Clear();
    }
    void ClearResidentCompanions(){foreach(var c in residentCompanions.Values)DisposeCompanion(c);residentCompanions.Clear();unavailableCompanions.Clear();companionRetryAt.Clear();nextCompanionSpawn=0;}
    void OpenResidentPetActions(string ownerId,string petId)
    {
        SetMode(Mode.Play);var owner=EnsureResidents().people.Find(p=>p.id==ownerId);var pet=owner?.pets.Find(p=>p.id==petId);if(pet==null){OpenResidentRoster();return;}
        bool supported=CompanionSupported(pet),following=owner.followPetId==pet.id;
        var entries=new List<PetTownCatalogEntry>{OperationsEntry(0,pet.name,following?"함께 산책 중":supported?"산책 가능":"산책 모델 준비 중","Lv."+pet.level+" · 산책 XP "+(pet.walkDay==day?pet.walkXP:0)+" / 5",following?"산책 마치기":supported?"같이 산책":"상태 확인","주민 한 명당 한 마리와 함께 산책합니다.\n실제 이동20m마다 펫 XP1, 하루 최대5XP.\n길이 막히면 멈추고 다시 경로를 확인합니다.",9),OperationsEntry(1,"주민의 펫 목록","","함께 사는 펫 확인","펫 목록","",5)};
        entries.Add(OperationsEntry(2,"건강 상태",PetTownClinic.HealthLabel(pet),"검진·치료·회복","건강 보기","병원 검사 결과와 회복 상태를 확인합니다.",9));
        var thumbnail=Resources.Load<Texture2D>("UI/Cozy/Pet_"+(pet.species>=3?2:Mathf.Abs(pet.appearance%2)));
        if(supported&&thumbnail){entries[0].Thumbnail=thumbnail;entries[0].TextOnlyDetail=false;}
        catalogWindow.Show(owner.name+"의 펫",entries,e=>
        {
            if(e.Key==2){OpenResidentPetHealth(ownerId,petId);return;}if(e.Key==1){OpenResidentPets(ownerId);return;}if(!supported){Message("이 펫의 산책 모델을 준비하고 있어요.");return;}
            var current=EnsureResidents().people.Find(p=>p.id==ownerId);if(current==null||!current.pets.Exists(p=>p.id==petId))return;
            current.followPetId=current.followPetId==petId?null:petId;unavailableCompanions.Remove(petId);companionRetryAt.Remove(ownerId);progressionDirty=true;SaveProgressionSilent();OpenResidentPetActions(ownerId,petId);
        },()=>{},()=>{});
        catalogWindow.Choose(0);
    }
}
