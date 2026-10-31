using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

public partial class PetTownGame
{
    sealed class FacilityActor
    {
        public GameObject root;public NavMeshAgent agent;public TextMeshPro label;
        public PetTownFacilityStage stage=(PetTownFacilityStage)(-1);public Vector3 goal,origin,previous;public Quaternion rotation;
        public float retry;public int revision=-1;public bool valid,hasPrevious,wasStopped,waitingForCompanion;public float originalSpeed;
    }
    readonly Dictionary<string,FacilityActor> facilityActors=new Dictionary<string,FacilityActor>();
    readonly Dictionary<string,PetTownFurniturePlacement> facilityFurnitureCache=new Dictionary<string,PetTownFurniturePlacement>();
    readonly List<PetTownInteriorState> facilityRooms=new List<PetTownInteriorState>();
    float nextFacilityScan,nextFacilityGuest;int facilityScanRevision=-1;
    PetTownInteriorState facilityFurnitureTarget;
    void RefreshFacilityRooms()
    {
        if(Time.unscaledTime<nextFacilityScan&&facilityScanRevision==constructionWorldRevision)return;
        nextFacilityScan=Time.unscaledTime+1;facilityScanRevision=constructionWorldRevision;facilityRooms.Clear();
        foreach(var b in buildings){var room=b?b.GetComponent<PetTownInteriorState>():null;if(room&&(room.Kind==3||room.Kind==4))facilityRooms.Add(room);}
    }
    PetTownInteriorState FacilityRoom(string id){RefreshFacilityRooms();foreach(var room in facilityRooms)if(room&&room.Id==id)return room;return null;}
    List<PetTownFurniturePlacement> FacilityTables(PetTownInteriorState room)
    {
        var list=new List<PetTownFurniturePlacement>();if(!room)return list;
        foreach(var f in room.GetComponentsInChildren<PetTownFurniturePlacement>())if(f.Owner==room&&((!f.Detail&&f.Kind==4)||(f.Detail&&f.Kind==13)))list.Add(f);return list;
    }
    PetTownFurniturePlacement FacilityCounter(PetTownInteriorState room)
    {
        if(!room)return null;foreach(var f in room.GetComponentsInChildren<PetTownFurniturePlacement>())if(f.Owner==room&&!f.Detail&&f.Kind==0)return f;return null;
    }
    PetTownFurniturePlacement FacilityFurniture(PetTownInteriorState room,string id)
    {
        if(!room||string.IsNullOrEmpty(id))return null;PetTownFurniturePlacement cached;
        if(facilityFurnitureCache.TryGetValue(id,out cached)&&cached&&cached.Id==id&&cached.Owner==room&&cached.transform.IsChildOf(room.transform))return cached;
        foreach(var furniture in room.GetComponentsInChildren<PetTownFurniturePlacement>())if(furniture.Owner==room&&furniture.Id==id){facilityFurnitureCache[id]=furniture;return furniture;}
        facilityFurnitureCache.Remove(id);return null;
    }
    void TickFacilities(float seconds)
    {
        if(loadingTown||expansionPending||!shop)return;RefreshFacilityRooms();var state=Progression.Facilities;
        var invalid=new List<string>();foreach(var v in state.visits){var person=EnsureResidents().people.Find(p=>p.id==v.residentId);if(!FacilityRoom(v.buildingId)||person==null||!person.pets.Exists(p=>p.id==v.petId))invalid.Add(v.id);}
        foreach(var id in invalid)CancelFacilityVisit(id,false);
        var removed=new List<string>();foreach(var pair in facilityActors)if(!state.visits.Exists(v=>v.id==pair.Key)){ReleaseFacilityActor(pair.Value);removed.Add(pair.Key);}foreach(var id in removed)facilityActors.Remove(id);
        if(Time.unscaledTime<nextFacilityGuest)return;nextFacilityGuest=Time.unscaledTime+30;
        foreach(var room in facilityRooms)
        {
            if(!room)continue;var business=state.Get(room.Id,room.Kind);if(!business.open||!business.automatic||state.AvailableFor(business)<=0)continue;
            foreach(var person in EnsureResidents().people)
            {
                bool accepted=false;foreach(var pet in person.pets){if(room.Kind==4&&PetTownClinic.NeedsTreatment(pet)&&!business.automaticTreatment)continue;string reason;if(TryAdmitFacility(room,person.id,pet.id,out reason)){accepted=true;break;}}if(accepted)break;
            }
        }
    }
    bool TryAdmitFacility(PetTownInteriorState room,string residentId,string petId,out string reason,int service=-1)
    {
        reason="";if(!OperationsReady()||!room){reason="시설을 준비해 주세요.";return false;}
        if(room.Kind==4&&service>=0&&Progression.playerLevel<PetTownClinic.UnlockLevel(service)){reason=PetTownClinic.ServiceName(service)+"은 플레이어 Lv."+PetTownClinic.UnlockLevel(service)+"에 열려요.";return false;}
        GameObject actor;if(!residentActors.TryGetValue(residentId,out actor)||!actor){reason="주민이 외출 준비 중이에요.";return false;}
        var agent=actor.GetComponent<NavMeshAgent>();if(!agent||!agent.isOnNavMesh){reason="주민의 이동 경로를 확인해 주세요.";return false;}
        if(Progression.community!=null&&Progression.community.visits.Exists(v=>v.residentId==residentId)){reason="현재 편의시설을 방문하고 있어요.";return false;}
        var state=Progression.Facilities;var owner=EnsureResidents().people.Find(p=>p.id==residentId);var pet=owner?.pets.Find(p=>p.id==petId);
        if(pet==null){reason="펫과 함께 사는 주민을 선택해 주세요.";return false;}
        if(room.Kind==3&&pet.lastCafeDay==day||state.visits.Exists(v=>v.residentId==residentId)){reason="오늘 이미 이용했거나 다른 방문을 진행 중이에요.";return false;}
        if(!state.Get(room.Id,room.Kind).open||state.AvailableFor(state.Get(room.Id,room.Kind))<=0){reason="영업 상태와 예약 가능한 소모품을 확인해 주세요.";return false;}
        if(room.Kind==4&&!PetTownClinic.CanVisit(pet,day,out reason))return false;
        var counter=FacilityCounter(room);var tables=FacilityTables(room);if(!counter||tables.Count==0){reason="시설 안에 계산대와 원목 또는 낮은 테이블을 설치해 주세요.";return false;}
        if(state.visits.FindAll(v=>v.buildingId==room.Id).Count>=Mathf.Min(4,tables.Count)){reason="현재 이용 중인 자리가 모두 찼어요.";return false;}
        foreach(var table in tables)
        {
            string stationId=table.Id;if(Progression.Facilities.visits.Exists(v=>v.buildingId==room.Id&&v.stationId==stationId))continue;
            var probe=new PetTownFacilityVisit{id="approach:"+residentId,buildingId=room.Id,kind=room.Kind,counterId=counter.Id,stationId=stationId};Vector3 goal,origin;Quaternion rotation;
            bool reachable=true;foreach(var stage in new[]{PetTownFacilityStage.Entrance,PetTownFacilityStage.Reception,PetTownFacilityStage.ToWork}){probe.stage=stage;if(!TryFacilityGoal(probe,agent,out goal,out origin,out rotation)){reachable=false;break;}}
            if(!reachable)continue;
            if(!Progression.Facilities.Admit(Progression,room.Id,room.Kind,residentId,petId,counter.Id,stationId,Mathf.Min(4,tables.Count),day,out reason,service))return false;
            progressionDirty=true;SaveProgressionSilent();return true;
        }
        reason="빈 테이블과 입구·계산대까지의 통로를 확보해 주세요.";return false;
    }
    bool FacilityOrigin(PetTownFacilityVisit v,out PetTownInteriorState room,out Vector3 origin,out Quaternion rotation,out float radius,out bool inside)
    {
        room=FacilityRoom(v.buildingId);origin=Vector3.zero;rotation=Quaternion.identity;radius=0;inside=false;if(!room)return false;
        var placed=room.GetComponent<PetTownBuildingPlacement>();Vector3 forward=placed?placed.EntranceForward:room.transform.rotation*Vector3.back;
        if(v.stage==PetTownFacilityStage.Entrance||v.stage==PetTownFacilityStage.Leaving){origin=room.transform.TransformPoint(room.EntranceLocal)+forward*(v.stage==PetTownFacilityStage.Leaving?2:1);origin.y=GetGroundHeight(origin.x,origin.z);rotation=room.transform.rotation;return true;}
        bool work=v.stage==PetTownFacilityStage.ToWork||v.stage==PetTownFacilityStage.Eating||((v.stage==PetTownFacilityStage.Waiting||v.stage==PetTownFacilityStage.Working)&&v.kind==4);
        var furniture=FacilityFurniture(room,work?v.stationId:v.counterId);if(!furniture)return false;
        if(work&&!((!furniture.Detail&&furniture.Kind==4)||(furniture.Detail&&furniture.Kind==13)))return false;
        if(!work&&(furniture.Detail||furniture.Kind!=0))return false;
        origin=furniture.transform.position;origin.y=room.FloorY;rotation=furniture.transform.rotation;radius=Mathf.Max(furniture.Footprint.x,furniture.Footprint.y)*.5f;inside=true;return true;
    }
    bool TryFacilityGoal(PetTownFacilityVisit v,NavMeshAgent agent,out Vector3 goal,out Vector3 origin,out Quaternion rotation)
    {
        goal=origin=Vector3.zero;rotation=Quaternion.identity;PetTownInteriorState room;float radius;bool inside;
        if(!agent||!agent.isOnNavMesh||!FacilityOrigin(v,out room,out origin,out rotation,out radius,out inside))return false;
        int seed=0;foreach(char c in v.id)seed=(seed+c)%8;
        for(int i=0;i<24;i++)
        {
            float a=(i+seed)*Mathf.PI/4;float distance=inside?radius+agent.radius+.3f+i/8*.4f:i==0?0:.25f+i/8*.25f;
            Vector3 candidate=origin+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*distance;NavMeshHit hit;
            if(!NavMesh.SamplePosition(candidate,out hit,.45f,agent.areaMask)||Mathf.Abs(hit.position.y-origin.y)>.65f||IsWater(hit.position.x,hit.position.z))continue;
            var local=room.transform.InverseTransformPoint(hit.position);if(inside&&!room.LocalBounds.Contains(new Vector2(local.x,local.z)))continue;
            var path=new NavMeshPath();if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)continue;goal=hit.position;return true;
        }
        return false;
    }
    bool UpdateFacilityVisit(GameObject actor,NavMeshAgent agent,float seconds)
    {
        if(UpdateDecorVisit(actor,agent,seconds))return true;
        if(UpdateCommunityVisit(actor,agent,seconds))return true;
        if(progression==null||progression.facilityVersion<1||progression.facilities==null||progression.facilities.visits.Count==0)return false;
        string ownerId=null;foreach(var p in residentActors)if(p.Value==actor){ownerId=p.Key;break;}if(ownerId==null)return false;
        var visit=Progression.Facilities.visits.Find(v=>v.residentId==ownerId);if(visit==null)return false;
        FacilityActor run;if(!facilityActors.TryGetValue(visit.id,out run))
        {
            if(!agent||!agent.isOnNavMesh)return true;
            run=new FacilityActor{root=actor,agent=agent,wasStopped=agent.isStopped,originalSpeed=agent.speed};facilityActors[visit.id]=run;
            var labelObject=new GameObject("FacilityVisitStatus");labelObject.transform.SetParent(actor.transform,false);labelObject.transform.localPosition=Vector3.up*(GetHeroTargetHeight()+.35f);run.label=labelObject.AddComponent<TextMeshPro>();run.label.font=PetTownCozyLibrary.Current.style.font;run.label.fontSize=2.1f;run.label.alignment=TextAlignmentOptions.Center;run.label.rectTransform.sizeDelta=new Vector2(4,1);run.label.color=new Color(1,.94f,.72f);
        }
        if(!Application.isFocused||seconds<=0||loadingTown||expansionPending){if(agent&&agent.isOnNavMesh)agent.isStopped=true;return true;}
        seconds=Mathf.Min(seconds,.25f);if(!agent||!agent.isOnNavMesh){Progression.Facilities.TickVisit(visit.id,seconds,false);if(visit.elapsed>180)CancelFacilityVisit(visit.id,true);return true;}
        PetTownInteriorState room;Vector3 origin;Quaternion rotation;float radius;bool inside;
        if(!FacilityOrigin(visit,out room,out origin,out rotation,out radius,out inside)){CancelFacilityVisit(visit.id,false);return false;}
        bool changed=run.stage!=visit.stage||run.revision!=constructionWorldRevision||(run.origin-origin).sqrMagnitude>.01f||Quaternion.Angle(run.rotation,rotation)>1;
        if(changed||(!run.valid&&Time.unscaledTime>=run.retry))
        {
            run.stage=visit.stage;run.revision=constructionWorldRevision;run.origin=origin;run.rotation=rotation;run.retry=Time.unscaledTime+1;
            Vector3 point,subject;Quaternion facing;run.valid=TryFacilityGoal(visit,agent,out point,out subject,out facing);agent.ResetPath();
            if(run.valid){run.goal=point;agent.isStopped=false;agent.SetDestination(point);}
        }
        if(run.valid&&!agent.pathPending&&agent.hasPath&&agent.pathStatus!=NavMeshPathStatus.PathComplete){run.valid=false;agent.ResetPath();run.retry=Time.unscaledTime+1;}
        bool arrived=run.valid&&!agent.pathPending&&Vector3.Distance(actor.transform.position,run.goal)<=agent.stoppingDistance+.25f;
        // Avoid leaving a visitor stranded after local avoidance nudges it off a completed path.
        if(run.valid&&!arrived&&!agent.pathPending&&!agent.hasPath&&Time.unscaledTime>=run.retry){agent.SetDestination(run.goal);run.retry=Time.unscaledTime+1;}
        bool together=arrived&&FacilityCompanionArrived(visit,run);run.waitingForCompanion=arrived&&!together;
        agent.speed=1.3f;agent.isStopped=arrived||!run.valid;
        bool moving=run.hasPrevious&&Vector3.Distance(run.previous,actor.transform.position)/seconds>.12f;run.previous=actor.transform.position;run.hasPrevious=true;UpdateVillagerAnim(actor,moving,false);
        if(arrived&&!moving&&(visit.stage==PetTownFacilityStage.Waiting||visit.stage==PetTownFacilityStage.Eating))PetTownSeatPose.Seat(actor);else PetTownSeatPose.Stand(actor);
        ResidentCompanion petCompanion;if(residentCompanions.TryGetValue(ownerId,out petCompanion)&&petCompanion.root&&petCompanion.petId==visit.petId)
        {
            int petKind=visit.kind==3&&visit.stage==PetTownFacilityStage.Eating?PetTownPetAction.Eat:visit.kind==4&&visit.stage==PetTownFacilityStage.Working?PetTownPetAction.Treat:together&&visit.stage==PetTownFacilityStage.Waiting?PetTownPetAction.Sit:0;
            if(petKind>0)PetTownPetAction.Hold(petCompanion.root,petKind);
        }
        if(run.label){if(cam)run.label.transform.rotation=cam.transform.rotation;run.label.text=visit.petName+"\n"+(run.valid?FacilityVisitStatus(visit):"통로 확인 중");}
        Progression.Facilities.TickVisit(visit.id,seconds,together,FacilityWorkerPresent(visit));
        if(Progression.Facilities.ApplyClinicResult(Progression,visit,day))SaveFacilityStaffProgress();
        if(visit.elapsed>180){CancelFacilityVisit(visit.id,!visit.paid);return true;}
        if(together)
        {
            if(visit.stage==PetTownFacilityStage.Leaving){CancelFacilityVisit(visit.id,false);return true;}
            // Only advance an arrival for the stage whose physical target was reached this frame.
            if(run.stage==visit.stage)Progression.Facilities.Arrive(visit.id);
        }
        return true;
    }
    static string FacilityStageLabel(PetTownFacilityVisit v)
    {
        switch(v.stage){case PetTownFacilityStage.Entrance:return "시설로 이동";case PetTownFacilityStage.Reception:return "접수대로 이동";case PetTownFacilityStage.Registering:return "접수 중";case PetTownFacilityStage.ToWork:return v.kind==3?"테이블로 이동":"진료대로 이동";case PetTownFacilityStage.Waiting:return v.kind==3?"주문·조리 대기":PetTownClinic.ServiceName(v.clinicService)+" 대기";case PetTownFacilityStage.Working:return (v.kind==3?"조리 중 ":PetTownClinic.ServiceName(v.clinicService)+" 중 ")+Mathf.CeilToInt(v.remaining)+"초";case PetTownFacilityStage.Eating:return "식사 중 "+Mathf.CeilToInt(v.remaining)+"초";case PetTownFacilityStage.ToPay:return "수납대로 이동";case PetTownFacilityStage.Payment:return "결제 대기";default:return "귀가 중";}
    }
    bool FacilityActionReady(PetTownFacilityVisit visit)
    {
        FacilityActor run;if(!facilityActors.TryGetValue(visit.id,out run)||!run.root||!run.agent||!run.agent.isOnNavMesh||!run.valid||run.stage!=visit.stage||run.agent.pathPending)return false;
        PetTownInteriorState room;Vector3 origin;Quaternion rotation;float radius;bool inside;if(!FacilityOrigin(visit,out room,out origin,out rotation,out radius,out inside))return false;
        return (origin-run.origin).sqrMagnitude<.01f&&Quaternion.Angle(rotation,run.rotation)<1&&Vector3.Distance(run.root.transform.position,run.goal)<=run.agent.stoppingDistance+.25f&&FacilityCompanionArrived(visit,run);
    }
    void CancelFacilityVisit(string id,bool unhappy)
    {
        var v=Progression.Facilities.visits.Find(p=>p.id==id);if(v==null)return;
        if(unhappy&&!v.paid)Progression.serviceHappiness=Mathf.Max(-20,Progression.serviceHappiness-(v.kind==3?1:.8f));
        Progression.Facilities.Cancel(id);FacilityActor run;if(facilityActors.TryGetValue(id,out run)){ReleaseFacilityActor(run);facilityActors.Remove(id);}progressionDirty=true;
    }
    void ReleaseFacilityActor(FacilityActor run)
    {
        if(run.label)Destroy(run.label.gameObject);if(run.agent&&run.agent.isOnNavMesh){run.agent.ResetPath();run.agent.isStopped=run.wasStopped;run.agent.speed=run.originalSpeed;}
        VillagerWander w;if(run.root&&villagerWander.TryGetValue(run.root,out w)){w.goal=VillagerGoal.Stroll;w.nextPick=0;w.running=false;w.hasPreviousPosition=false;w.stuckSince=-1;}
    }
    void ReleaseFacilityActors(){ReleaseCommunityActors();foreach(var run in facilityActors.Values)ReleaseFacilityActor(run);facilityActors.Clear();facilityFurnitureCache.Clear();}
}
