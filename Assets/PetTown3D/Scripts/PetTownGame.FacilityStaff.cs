using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public partial class PetTownGame
{
    static bool IsFacilityEmployeeTask(EmployeeTask task)=>task==EmployeeTask.FacilityService||task==EmployeeTask.FacilityPayment;
    bool FacilityEmployeeEligible(PetTownEmployeeRecord p,PetTownFacilityVisit v)
    {
        var room=FacilityRoom(v.buildingId);
        return room&&room.Kind==v.kind&&Operations.employees.Contains(p)&&PetTownFacilityStaffRules.Available(p,Progression.Facilities.Get(v.buildingId,v.kind));
    }
    void PickFacilityEmployeeJob(PetTownEmployeeRecord p,EmployeeActor a)
    {
        if(FacilityStaffInactive())return;
        if(string.IsNullOrEmpty(p.facilityId)){a.status="근무 시설 지정 필요";return;}
        var room=FacilityRoom(p.facilityId);if(!room||!PetTownFacilityStaffRules.Supports(p.role,room.Kind)){a.status="근무 시설 확인 필요";return;}
        if(!Progression.Facilities.Get(room.Id,room.Kind).staffService){a.status="시설 자동 업무 꺼짐";return;}
        a.status=p.role=="cashier"?"수납 손님 대기":"서비스 손님 대기";
        foreach(var v in Progression.Facilities.visits)
        {
            if(v.buildingId!=room.Id||v.paid||TargetReserved(v.id))continue;
            bool payment=p.role=="cashier";
            bool candidate=payment?v.stage==PetTownFacilityStage.Payment:v.stage==PetTownFacilityStage.Waiting&&!v.suppliesUsed||v.stage==PetTownFacilityStage.Working&&v.staffRequired&&v.suppliesUsed;
            if(!candidate||!FacilityEmployeeEligible(p,v)||!FacilityActionReady(v))continue;
            if(!AssignFacilityEmployeeJob(p,a,v,payment))continue;
            if(!payment&&v.stage==PetTownFacilityStage.Working)
            {
                if(!Progression.Facilities.ReclaimStaffWork(v.id,p)){CancelEmployeeJob(a);continue;}
                a.facilityStarted=true;SaveFacilityStaffProgress();
            }
            return;
        }
        GoToFacilityPost(p,a,room);
    }
    bool FacilityStaffInactive()=>!Application.isFocused||loadingTown||expansionPending;
    bool AssignFacilityEmployeeJob(PetTownEmployeeRecord p,EmployeeActor a,PetTownFacilityVisit v,bool payment)
    {
        if(!a.agent||!a.agent.enabled||!a.agent.isOnNavMesh)return false;
        PetTownInteriorState room;Vector3 origin;Quaternion rotation;float radius;bool inside;
        if(!FacilityOrigin(v,out room,out origin,out rotation,out radius,out inside)||!inside)return false;
        FacilityActor visitor;Vector3 away=Vector3.back;
        if(facilityActors.TryGetValue(v.id,out visitor)&&visitor.root)away=Vector3.ProjectOnPlane(origin-visitor.root.transform.position,Vector3.up).normalized;
        if(away.sqrMagnitude<.01f)away=rotation*Vector3.back;
        for(int i=0;i<24;i++)
        {
            var candidate=origin+Quaternion.Euler(0,(i%8)*45,0)*away*(radius+a.agent.radius+.3f+i/8*.35f);NavMeshHit hit;
            if(!NavMesh.SamplePosition(candidate,out hit,.35f,a.agent.areaMask)||Mathf.Abs(hit.position.y-origin.y)>.45f||IsWater(hit.position.x,hit.position.z))continue;
            var local=room.transform.InverseTransformPoint(hit.position);if(!room.LocalBounds.Contains(new Vector2(local.x,local.z)))continue;
            if(visitor!=null&&visitor.root&&Vector3.Distance(hit.position,visitor.root.transform.position)<.7f)continue;
            var path=new NavMeshPath();if(!a.agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete||!CompanionDryPath(path))continue;
            a.agent.isStopped=false;if(!a.agent.SetPath(path))continue;
            a.employeeId=p.id;a.task=payment?EmployeeTask.FacilityPayment:EmployeeTask.FacilityService;a.target=v.id;a.subject=origin;a.goal=hit.position;a.progress=0;a.assigned=Time.time;a.facilityRevision=constructionWorldRevision;a.facilityStarted=false;a.status="시설 작업 지점으로 이동";return true;
        }
        a.status="시설 통로 확인 필요";return false;
    }
    bool FacilityEmployeeAtTarget(EmployeeActor a,PetTownFacilityVisit v)
    {
        if(!a.root||!a.agent||!a.agent.enabled||!a.agent.isOnNavMesh||a.agent.pathPending||a.facilityRevision!=constructionWorldRevision)return false;
        if(a.agent.hasPath&&a.agent.pathStatus!=NavMeshPathStatus.PathComplete)return false;
        PetTownInteriorState room;Vector3 origin;Quaternion rotation;float radius;bool inside;
        if(!FacilityOrigin(v,out room,out origin,out rotation,out radius,out inside)||!inside||(origin-a.subject).sqrMagnitude>.01f)return false;
        var local=room.transform.InverseTransformPoint(a.root.transform.position);
        return room.LocalBounds.Contains(new Vector2(local.x,local.z))&&Vector3.Distance(a.root.transform.position,a.goal)<=a.agent.stoppingDistance+.25f;
    }
    bool FacilityWorkerPresent(PetTownFacilityVisit v)
    {
        if(!v.staffRequired)return true;
        if(FacilityStaffInactive()||string.IsNullOrEmpty(v.workerId))return false;
        var p=Operations.employees.Find(x=>x.id==v.workerId);EmployeeActor a;
        return FacilityEmployeeEligible(p,v)&&p.role==PetTownFacilityStaffRules.Specialist(v.kind)&&employeeActors.TryGetValue(p.id,out a)&&
            a.task==EmployeeTask.FacilityService&&a.target==v.id&&a.facilityStarted&&FacilityEmployeeAtTarget(a,v);
    }
    void UpdateFacilityEmployeeJob(PetTownEmployeeRecord p,EmployeeActor a,float seconds)
    {
        if(FacilityStaffInactive()||seconds<=0)return;
        var v=Progression.Facilities.visits.Find(x=>x.id==a.target);
        if(v==null||!FacilityEmployeeEligible(p,v)){CancelEmployeeJob(a);return;}
        if(a.task==EmployeeTask.FacilityService&&a.facilityStarted&&v.workerId==p.id&&
            v.stage==(v.kind==3?PetTownFacilityStage.ToWork:PetTownFacilityStage.ToPay))
        {
            p.completed++;v.workerId=null;v.staffRequired=false;CancelEmployeeJob(a);SaveFacilityStaffProgress();return;
        }
        bool valid=a.task==EmployeeTask.FacilityPayment?p.role=="cashier"&&v.stage==PetTownFacilityStage.Payment&&!v.paid:
            p.role==PetTownFacilityStaffRules.Specialist(v.kind)&&(v.stage==PetTownFacilityStage.Waiting&&!v.suppliesUsed||v.stage==PetTownFacilityStage.Working&&v.staffRequired&&v.workerId==p.id);
        PetTownInteriorState room;Vector3 origin;Quaternion rotation;float radius;bool inside;
        if(!valid||Time.time-a.assigned>120||a.facilityRevision!=constructionWorldRevision||!FacilityOrigin(v,out room,out origin,out rotation,out radius,out inside)||(origin-a.subject).sqrMagnitude>.01f){CancelEmployeeJob(a);return;}
        if(!FacilityEmployeeAtTarget(a,v)){a.status="시설 작업 지점으로 이동";return;}
        if(!FacilityActionReady(v)){a.status="주민·펫 도착 대기";return;}
        if(a.task==EmployeeTask.FacilityService)
        {
            if(v.stage==PetTownFacilityStage.Waiting)
            {
                string reason;if(!Progression.Facilities.BeginWithStaff(v.id,p,out reason)){a.status=reason;CancelEmployeeJob(a);return;}
                a.facilityStarted=true;SaveFacilityStaffProgress();
            }
            a.status=(v.kind==3?"조리 중 ":PetTownClinic.ServiceName(v.clinicService)+" 중 ")+Mathf.CeilToInt(v.remaining)+"초";
        }
        else
        {
            a.progress+=Mathf.Min(seconds,.25f);a.status="시설 수납 중";if(a.progress<PetTownShopOperationsState.WorkDuration(p.level))return;
            int income;if(Progression.Facilities.TakePayment(Progression,v.id,day,ref money,out income)){p.completed++;SaveFacilityStaffProgress();}CancelEmployeeJob(a);
        }
    }
    void ReleaseFacilityEmployee(EmployeeActor a)
    {
        if(!IsFacilityEmployeeTask(a.task)||progression==null||progression.facilities==null)return;
        var v=progression.facilities.visits.Find(x=>x.id==a.target);if(v!=null&&v.workerId==a.employeeId)v.workerId=null;
        a.facilityStarted=false;
    }
    void CancelFacilityWorkersForVisit(string id){foreach(var a in employeeActors.Values)if(IsFacilityEmployeeTask(a.task)&&a.target==id)CancelEmployeeJob(a);}
    void CancelFacilityWorkersForBuilding(string id){foreach(var a in employeeActors.Values)if(IsFacilityEmployeeTask(a.task)){var v=Progression.Facilities.visits.Find(x=>x.id==a.target);if(v!=null&&v.buildingId==id)CancelEmployeeJob(a);}}
    void SaveFacilityStaffProgress(){progressionDirty=true;SaveProgressionSilent();}
}
