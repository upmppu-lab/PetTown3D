using UnityEngine;

public sealed partial class PetTownFacilityState
{
    public static int VisitSupplyCost(PetTownFacilityVisit v)=>v.kind==4?PetTownClinic.SupplyCost(v.clinicService):1;
    public int ReservedSupplies(int kind)
    {
        int reserved=0;foreach(var v in visits)if(v.kind==kind&&(kind!=3||v.cafeMenu==0)&&!v.suppliesUsed&&!v.paid)reserved+=VisitSupplyCost(v);return reserved;
    }
    public bool ApplyClinicResult(PetTownProgressionState data,PetTownFacilityVisit v,int day)
    {
        if(v==null||!visits.Contains(v)||v.kind!=4||!v.clinicCompleted||v.clinicApplied||!v.suppliesUsed||
            (v.stage!=PetTownFacilityStage.ToPay&&v.stage!=PetTownFacilityStage.Payment&&v.stage!=PetTownFacilityStage.Leaving))return false;
        var owner=data.residents?.people.Find(x=>x.id==v.residentId);var pet=owner?.pets.Find(x=>x.id==v.petId);if(pet==null)return false;
        var h=PetTownClinic.Ensure(pet,day);v.clinicApplied=true;
        // Never use a saved old visit to treat a different illness episode.
        if(h.episode!=v.clinicEpisode||h.condition!=v.clinicCondition){v.clinicResult="컨디션 변화 · 재검 필요";return true;}
        if(PetTownClinic.IsExam(v.clinicService))
        {
            h.lastExamDay=day;if(h.condition>0){h.diagnosed=true;v.clinicResult=PetTownClinic.ConditionName(h.condition)+" · 치료 필요";}
            else{h.nextRiskDay=Mathf.Max(h.nextRiskDay,day+(v.clinicService==2?7:3));v.clinicResult="검진 정상";}
        }
        else if(h.condition>0&&h.diagnosed&&!h.recovering)
        {
            if(v.clinicService==3){h.condition=0;h.diagnosed=false;h.recovering=false;h.restDays=0;h.nextRiskDay=day+7;h.lastDay=Mathf.Max(h.lastDay,day);v.clinicResult="입원 치료 완료 · 바로 퇴원";}
            else{h.recovering=true;h.restDays=h.condition==1?1:2;h.lastDay=Mathf.Max(h.lastDay,day);v.clinicResult="치료 완료 · 휴식 "+h.restDays+"일";}
        }
        else v.clinicResult="컨디션 변화 · 재검 필요";
        return true;
    }
    void NormalizeClinic()
    {
        foreach(var v in visits)
        {
            v.clinicService=v.kind==4?Mathf.Clamp(v.clinicService,0,PetTownClinic.ServiceCount-1):0;v.clinicCondition=v.kind==4?Mathf.Clamp(v.clinicCondition,0,3):0;v.clinicEpisode=Mathf.Max(0,v.clinicEpisode);
            v.clinicVersion=v.kind==4?Mathf.Clamp(v.clinicVersion,0,1):0;
            if(v.kind!=4){v.clinicCompleted=false;v.clinicApplied=false;v.clinicResult=null;}
        }
        foreach(var r in receipts)if(r!=null){r.clinicService=r.kind==4?Mathf.Clamp(r.clinicService,0,PetTownClinic.ServiceCount-1):0;r.clinicCondition=r.kind==4?Mathf.Clamp(r.clinicCondition,0,3):0;}
    }
}
