using UnityEngine;
using UnityEngine.AI;

// Facility-only arrival handshake. Resident navigation remains owned by the existing visit adapter.
public partial class PetTownGame
{
    bool GuideFacilityCompanion(ResidentCompanion companion,NavMeshAgent agent)
    {
        var visit=Progression.Facilities.visits.Find(v=>v.residentId==companion.ownerId&&v.petId==companion.petId);
        FacilityActor run;
        if(visit==null||!facilityActors.TryGetValue(visit.id,out run)||!run.root||!run.agent||!run.agent.enabled||!run.agent.isOnNavMesh||!run.valid||run.stage!=visit.stage||run.agent.pathPending||
           Vector3.Distance(run.root.transform.position,run.goal)>run.agent.stoppingDistance+.25f)
        {
            companion.serviceGoalValid=false;return false;
        }
        PetTownInteriorState room;Vector3 origin;Quaternion rotation;float radius;bool inside;
        if(!FacilityOrigin(visit,out room,out origin,out rotation,out radius,out inside)){companion.serviceGoalValid=false;agent.ResetPath();return true;}
        Vector3 owner=run.root.transform.position;
        bool changed=companion.serviceVisit!=visit.id||companion.serviceStage!=visit.stage||companion.serviceRevision!=constructionWorldRevision||(companion.serviceOwner-owner).sqrMagnitude>.0225f;
        if(companion.serviceGoalValid&&!changed)
        {
            if(Vector3.Distance(agent.transform.position,companion.serviceGoal)<=agent.stoppingDistance+.15f){agent.ResetPath();return true;}
            if(agent.pathPending||(agent.hasPath&&agent.pathStatus==NavMeshPathStatus.PathComplete))return true;
        }
        companion.serviceGoalValid=false;
        Vector3 back=Vector3.ProjectOnPlane(-run.root.transform.forward,Vector3.up).normalized;if(back.sqrMagnitude<.1f)back=Vector3.back;
        for(int i=0;i<24;i++)
        {
            Vector3 candidate=owner+Quaternion.Euler(0,i%8*45,0)*back*(.9f+i/8*.35f);NavMeshHit hit;
            if(!NavMesh.SamplePosition(candidate,out hit,.35f,agent.areaMask)||Mathf.Abs(hit.position.y-owner.y)>.55f||CompanionWaterAt(hit.position))continue;
            Vector3 local=room.transform.InverseTransformPoint(hit.position);
            if(inside&&!room.LocalBounds.Contains(new Vector2(local.x,local.z)))continue;
            if(Vector3.Distance(hit.position,owner)<.65f)continue;
            var path=new NavMeshPath();if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete||!CompanionDryPath(path)||!agent.SetPath(path))continue;
            companion.serviceVisit=visit.id;companion.serviceStage=visit.stage;companion.serviceRevision=constructionWorldRevision;
            companion.serviceOwner=owner;companion.serviceGoal=hit.position;companion.serviceGoalValid=true;agent.speed=1.8f;return true;
        }
        agent.ResetPath();return true;
    }
    bool FacilityCompanionArrived(PetTownFacilityVisit visit,FacilityActor run)
    {
        var owner=EnsureResidents().people.Find(p=>p.id==visit.residentId);var pet=owner?.pets.Find(p=>p.id==visit.petId);
        if(pet==null)return false;
        // Preserve services for species whose 3D walking model has not been supplied yet.
        if(!CompanionSupported(pet))return true;
        ResidentCompanion companion;
        if(!residentCompanions.TryGetValue(visit.residentId,out companion)||companion.petId!=visit.petId||!companion.root||!companion.agent||!companion.agent.enabled||!companion.agent.isOnNavMesh||
           companion.agent.pathPending||!companion.serviceGoalValid||companion.serviceVisit!=visit.id||companion.serviceStage!=visit.stage||companion.serviceRevision!=constructionWorldRevision)return false;
        if((companion.serviceOwner-run.root.transform.position).sqrMagnitude>.0225f)return false;
        if(Vector3.Distance(companion.root.transform.position,companion.serviceGoal)>companion.agent.stoppingDistance+.2f||
           Vector3.Distance(companion.root.transform.position,run.root.transform.position)>2.3f||Mathf.Abs(companion.root.transform.position.y-run.root.transform.position.y)>.6f)return false;
        PetTownInteriorState room;Vector3 origin;Quaternion rotation;float radius;bool inside;
        if(!FacilityOrigin(visit,out room,out origin,out rotation,out radius,out inside))return false;
        var local=room.transform.InverseTransformPoint(companion.root.transform.position);
        return !inside||room.LocalBounds.Contains(new Vector2(local.x,local.z));
    }
    string FacilityVisitStatus(PetTownFacilityVisit visit)
    {
        if(visit.stage==PetTownFacilityStage.Working&&visit.staffRequired&&!FacilityWorkerPresent(visit))return "담당 직원 도착 대기 · "+Mathf.CeilToInt(visit.remaining)+"초";
        FacilityActor run;
        return facilityActors.TryGetValue(visit.id,out run)&&run.stage==visit.stage&&run.waitingForCompanion?"펫 도착 대기":FacilityStageLabel(visit);
    }
}
