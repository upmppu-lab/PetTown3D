using UnityEngine;

public static class PetTownFacilityStaffRules
{
    public static string Specialist(int kind)=>kind==3?"chef":kind==4?"vet":"";
    public static bool Supports(string role,int kind)=>(kind==3||kind==4)&&(role=="cashier"||role==Specialist(kind));
    public static bool Available(PetTownEmployeeRecord worker,PetTownFacilityBusiness business)
        =>worker!=null&&business!=null&&business.staffService&&!worker.paused&&worker.unpaid==0&&worker.facilityId==business.id&&Supports(worker.role,business.kind);
    public static float Efficiency(int level)=>1+.08f*(Mathf.Clamp(level,1,10)-1);
}

// Saved staff ownership pauses work after reload until a physical worker reclaims it.
public sealed partial class PetTownFacilityState
{
    public bool BeginWithStaff(string id,PetTownEmployeeRecord worker,out string reason)
    {
        reason="담당 직원과 근무 시설을 확인해 주세요.";var v=visits.Find(x=>x.id==id);
        if(v==null||!PetTownFacilityStaffRules.Available(worker,Get(v.buildingId,v.kind))||worker.role!=PetTownFacilityStaffRules.Specialist(v.kind))return false;
        if(!Begin(id,out reason))return false;
        v.staffRequired=true;v.workerId=worker.id;v.remaining/=PetTownFacilityStaffRules.Efficiency(worker.level);return true;
    }
    public bool ReclaimStaffWork(string id,PetTownEmployeeRecord worker)
    {
        var v=visits.Find(x=>x.id==id);if(v==null||v.stage!=PetTownFacilityStage.Working||!v.suppliesUsed||!v.staffRequired||
            !PetTownFacilityStaffRules.Available(worker,Get(v.buildingId,v.kind))||worker.role!=PetTownFacilityStaffRules.Specialist(v.kind))return false;
        v.workerId=worker.id;return true;
    }
    public bool ResumeWorkManually(string id)
    {
        var v=visits.Find(x=>x.id==id);if(v==null||v.stage!=PetTownFacilityStage.Working||!v.staffRequired||!v.suppliesUsed)return false;
        v.staffRequired=false;v.workerId=null;return true;
    }
}
