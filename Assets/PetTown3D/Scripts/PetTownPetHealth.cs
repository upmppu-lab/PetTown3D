using System;
using System.Collections.Generic;
using UnityEngine;

// Fictional game conditions; no death, contagion, offline illness rolls, or real medical instructions.
[Serializable]
public sealed class PetTownPetHealth
{
    public int version,condition,episode,lastDay,nextRiskDay,restDays;
    public int lastExamDay=-1,lastTreatmentDay=-1;
    public bool diagnosed,recovering;
    public void Normalize()
    {
        version=Mathf.Clamp(version,0,1);condition=version==1?Mathf.Clamp(condition,0,3):0;episode=Mathf.Max(0,episode);lastDay=Mathf.Max(0,lastDay);nextRiskDay=Mathf.Max(0,nextRiskDay);restDays=Mathf.Clamp(restDays,0,2);
        if(condition==0){diagnosed=false;recovering=false;restDays=0;}else if(recovering){diagnosed=true;restDays=Mathf.Max(1,restDays);}else restDays=0;
    }
}
public static class PetTownClinic
{
    public static string ConditionName(int condition)=>condition==1?"피로":condition==2?"배탈":condition==3?"피부 트러블":"건강함";
    public static string HealthLabel(PetTownResidentPetRecord pet)
    {
        var h=pet?.health;if(h==null||h.version<1||h.condition==0)return "건강함";
        if(h.recovering)return "회복 중 · 휴식 "+h.restDays+"일";
        return h.diagnosed?ConditionName(h.condition)+" · 치료 필요":"컨디션 확인 필요";
    }
    public static PetTownPetHealth Ensure(PetTownResidentPetRecord pet,int day)
    {
        if(pet.health==null||pet.health.version<1)pet.health=new PetTownPetHealth{version=1,lastDay=Mathf.Max(0,day),nextRiskDay=Mathf.Max(0,day)+5};return pet.health;
    }
    public static bool NeedsTreatment(PetTownResidentPetRecord pet)=>pet?.health!=null&&pet.health.version>=1&&pet.health.condition>0&&pet.health.diagnosed&&!pet.health.recovering;
    public static int ServiceFor(PetTownResidentPetRecord pet)=>NeedsTreatment(pet)?1:0;
    public const int ServiceCount=4;
    public static string ServiceName(int service)=>service==1?"치료":service==2?"정밀검진":service==3?"입원 치료":"예방검진";
    public static int SupplyCost(int service)=>service==1?2:service==2?2:service==3?3:1;
    public static int Fee(int service)=>service==1?260:service==2?230:service==3?450:140;
    public static float WorkSeconds(int service)=>service==1?16:service==2?15:service==3?24:10;
    public static int UnlockLevel(int service)=>service==2?4:service==3?6:1;
    public static bool IsTreatment(int service)=>service==1||service==3;
    public static bool IsExam(int service)=>service==0||service==2;
    public static string ServiceNote(int service)=>service==1?"진단받은 증상을 치료합니다. 완료 후 1~2일 휴식합니다.":service==2?"혈액·영상 검사를 포함한 정밀검진입니다. 이상이 없으면 7일간 컨디션이 안정됩니다.":service==3?"입원해 집중 치료합니다. 소모품이 많이 들지만 휴식일 없이 바로 퇴원합니다.":"기본 건강 확인입니다. 이상이 없으면 3일간 안정됩니다.";
    public static bool Allowed(PetTownResidentPetRecord pet,int service,out string reason)
    {
        reason="";if(pet==null||service<0||service>=ServiceCount){reason="진료 종류를 다시 선택해 주세요.";return false;}
        bool ill=NeedsTreatment(pet);
        if(IsTreatment(service)&&!ill){reason="진단받아 치료가 필요한 펫만 받을 수 있어요.";return false;}
        if(IsExam(service)&&ill){reason="이미 진단받은 펫은 치료를 받아야 해요.";return false;}
        return true;
    }
    public static bool CanVisit(PetTownResidentPetRecord pet,int day,out string reason)
    {
        reason="";if(pet==null){reason="주민의 펫을 다시 선택해 주세요.";return false;}
        if(pet.health!=null&&pet.health.recovering){reason="치료를 마쳤어요. 새날까지 편히 쉬게 해 주세요.";return false;}
        if(NeedsTreatment(pet)){if(pet.health.lastTreatmentDay!=day)return true;}
        else if(pet.lastCheckupDay!=day||pet.health!=null&&pet.health.condition>0&&!pet.health.diagnosed)return true;
        reason="오늘 이미 이용한 펫이에요.";return false;
    }
    public static bool AlreadyPaid(PetTownFacilityVisit visit,PetTownResidentPetRecord pet,int day)=>IsTreatment(visit.clinicService)?pet.health!=null&&pet.health.lastTreatmentDay==day:pet.lastCheckupDay==day;
    public static uint Roll(string id,int day)
    {
        unchecked{uint h=2166136261;foreach(char c in id??"")h=(h^c)*16777619;h=(h^(uint)day)*16777619;h^=h>>16;h*=2246822519;return h^(h>>13);}
    }
    public static bool AdvanceDay(PetTownProgressionState data,int day)
    {
        if(data.residents==null)return false;var visiting=new HashSet<string>();if(data.facilities?.visits!=null)foreach(var v in data.facilities.visits)visiting.Add(v.petId);
        bool changed=false;foreach(var owner in data.residents.people)foreach(var pet in owner.pets)
        {
            if(pet.health==null||pet.health.version<1){Ensure(pet,day);changed=true;continue;}var h=pet.health;if(day<=h.lastDay)continue;
            int previous=h.lastDay;h.lastDay=day;changed=true;if(day!=previous+1||visiting.Contains(pet.id))continue;
            if(h.recovering){h.restDays--;if(h.restDays<=0){h.condition=0;h.recovering=false;h.diagnosed=false;h.restDays=0;h.nextRiskDay=day+5;}continue;}
            if(h.condition>0||day<h.nextRiskDay)continue;
            uint roll=Roll(pet.id,day);if(roll%100>=6)continue;h.condition=1+(int)(roll/100%3);h.episode++;h.diagnosed=false;
        }
        return changed;
    }
}
