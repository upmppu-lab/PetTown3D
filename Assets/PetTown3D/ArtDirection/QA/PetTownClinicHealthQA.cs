#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public static class PetTownClinicHealthQA
{
    static PetTownProgressionState Data(int condition=0,bool diagnosed=false)
    {
        var d=new PetTownProgressionState{stockInitialized=true,residentVersion=1,residents=new PetTownResidentState()};
        var o=new PetTownResidentRecord{id="owner",name="검사 주민",happiness=50};o.pets.Add(new PetTownResidentPetRecord{id="pet",name="검사 펫",level=3,lastCheckupDay=-1,lastCafeDay=-1,health=new PetTownPetHealth{version=1,condition=condition,diagnosed=diagnosed,episode=condition>0?1:0,lastDay=100,nextRiskDay=105}});d.residents.people.Add(o);
        d.Facilities.Get("clinic",4).open=true;d.facilities.medicalSupplies=10;return d;
    }
    static PetTownResidentPetRecord Pet(PetTownProgressionState d)=>d.residents.people[0].pets[0];
    static PetTownFacilityVisit Admit(PetTownProgressionState d,int day=100)
    {
        string reason;if(!d.Facilities.Admit(d,"clinic",4,"owner","pet","counter","table",4,day,out reason))throw new Exception("admit: "+reason);return d.facilities.visits[d.facilities.visits.Count-1];
    }
    static void Arrive(PetTownFacilityState s,PetTownFacilityVisit v){s.Arrive(v.id);s.Arrive(v.id);s.TickVisit(v.id,5,true);s.Arrive(v.id);}
    static void Finish(PetTownProgressionState d,PetTownFacilityVisit v,int day=100){d.Facilities.TickVisit(v.id,100,true,true);d.facilities.ApplyClinicResult(d,v,day);}
    public static Dictionary<string,bool> Run()
    {
        var c=new Dictionary<string,bool>();Action<string,bool> test=(k,v)=>{c[k]=v;if(!v)throw new Exception(k);};string reason;int money=1000,income;
        var d=Data(2);var pet=Pet(d);var s=d.Facilities;var v=Admit(d);
        test("symptoms_hide_diagnosis",PetTownClinic.HealthLabel(pet)=="컨디션 확인 필요"&&!pet.health.diagnosed);
        test("initial_visit_is_exam",v.clinicService==0&&v.clinicVersion==1&&v.clinicCondition==2&&v.clinicEpisode==1&&s.AvailableSupplies(4)==9);
        test("exam_cannot_start_before_arrival",!s.Begin(v.id,out reason)&&s.medicalSupplies==10);
        Arrive(s,v);test("arrive_exam_waiting",v.stage==PetTownFacilityStage.Waiting);
        test("begin_exam_cost_one",s.Begin(v.id,out reason)&&s.medicalSupplies==9&&v.remaining==10);
        test("no_result_before_work_complete",!s.ApplyClinicResult(d,v,100)&&!pet.health.diagnosed);
        s.TickVisit(v.id,100,false);test("pet_absent_no_diagnosis",!v.clinicCompleted&&!pet.health.diagnosed);
        Finish(d,v);test("exam_result_before_payment",v.clinicCompleted&&v.clinicApplied&&pet.health.diagnosed&&pet.health.lastExamDay==100&&!pet.health.recovering&&money==1000);
        test("exam_result_once",!s.ApplyClinicResult(d,v,100));s.Arrive(v.id);
        test("exam_fee_and_record",s.TakePayment(d,v.id,100,ref money,out income)&&income==140&&s.receipts[0].clinicService==0&&s.receipts[0].clinicCondition==2&&pet.lastCheckupDay==100);
        test("exam_duplicate_payment_rejected",!s.TakePayment(d,v.id,100,ref money,out income)&&s.receipts.Count==1);
        s.Cancel(v.id);test("same_day_followup_allowed",PetTownClinic.CanVisit(pet,100,out reason));v=Admit(d);
        test("treatment_snapshot_and_weighted_reservation",v.clinicService==1&&PetTownFacilityState.VisitFee(v)==260&&s.ReservedSupplies(4)==2&&s.AvailableSupplies(4)==7);
        Arrive(s,v);var vet=new PetTownEmployeeRecord{id="vet",role="vet",facilityId="clinic",level=2};s.Get("clinic",4).staffService=true;
        test("vet_starts_treatment",s.BeginWithStaff(v.id,vet,out reason)&&s.medicalSupplies==7&&v.staffRequired&&v.workerId=="vet");
        float held=v.remaining;s.TickVisit(v.id,100,true,false);test("missing_vet_pauses_treatment",v.remaining==held&&!v.clinicCompleted&&!pet.health.recovering);
        var clone=JsonUtility.FromJson<PetTownProgressionState>(JsonUtility.ToJson(d));clone.Normalize();var cv=clone.Facilities.visits[0];test("treatment_save_roundtrip",cv.clinicService==1&&cv.clinicCondition==2&&cv.remaining==held&&cv.staffRequired&&Pet(clone).health.condition==2&&clone.facilities.medicalSupplies==7);
        test("resume_does_not_spend_again",clone.facilities.ReclaimStaffWork(cv.id,vet)&&clone.facilities.medicalSupplies==7);
        Finish(clone,cv);test("treatment_starts_two_days_recovery",Pet(clone).health.recovering&&Pet(clone).health.restDays==2&&cv.clinicApplied&&cv.clinicResult.Contains("치료 완료"));
        test("recovery_rejects_new_clinic_visit",!PetTownClinic.CanVisit(Pet(clone),100,out reason));clone.facilities.Arrive(cv.id);int otherMoney=1000;
        test("treatment_fee_and_receipt",clone.facilities.TakePayment(clone,cv.id,100,ref otherMoney,out income)&&income==260&&clone.facilities.receipts[0].clinicService==1&&Pet(clone).health.lastTreatmentDay==100&&clone.Operations(100).Activity("clinic")==2);
        test("treatment_duplicate_payment_rejected",!clone.facilities.TakePayment(clone,cv.id,100,ref otherMoney,out income));clone.facilities.Cancel(cv.id);
        PetTownClinic.AdvanceDay(clone,101);test("first_rest_day",Pet(clone).health.recovering&&Pet(clone).health.restDays==1);PetTownClinic.AdvanceDay(clone,101);test("same_day_no_double_recovery",Pet(clone).health.restDays==1);
        var savedRecovery=JsonUtility.FromJson<PetTownProgressionState>(JsonUtility.ToJson(clone));savedRecovery.Normalize();test("recovery_persists_restart",Pet(savedRecovery).health.recovering&&Pet(savedRecovery).health.restDays==1);
        PetTownClinic.AdvanceDay(savedRecovery,102);test("second_day_recovers",Pet(savedRecovery).health.condition==0&&!Pet(savedRecovery).health.recovering&&Pet(savedRecovery).health.nextRiskDay==107);
        var skipped=JsonUtility.FromJson<PetTownProgressionState>(JsonUtility.ToJson(clone));PetTownClinic.AdvanceDay(skipped,150);test("skipped_dates_no_recovery_backfill",Pet(skipped).health.restDays==1&&Pet(skipped).health.lastDay==150);
        var reserve=Data(1,true);reserve.Facilities.medicalSupplies=1;test("treatment_shortage_no_admission",!reserve.Facilities.Admit(reserve,"clinic",4,"owner","pet","counter","table",4,100,out reason)&&reserve.facilities.medicalSupplies==1&&reserve.facilities.visits.Count==0);
        reserve.facilities.medicalSupplies=2;var rv=Admit(reserve);test("exact_treatment_reservation",reserve.facilities.AvailableSupplies(4)==0);reserve.facilities.Cancel(rv.id);test("cancel_releases_weighted_reservation",reserve.facilities.AvailableSupplies(4)==2);
        rv=Admit(reserve);Arrive(reserve.facilities,rv);reserve.facilities.Begin(rv.id,out reason);reserve.facilities.Cancel(rv.id);test("cancel_before_completion_no_cure_or_refund",!Pet(reserve).health.recovering&&reserve.facilities.medicalSupplies==0);
        var stale=Data(1,true);var sv=Admit(stale);Arrive(stale.facilities,sv);stale.facilities.Begin(sv.id,out reason);Pet(stale).health.episode++;Finish(stale,sv);test("old_episode_cannot_cure_new_illness",!Pet(stale).health.recovering&&sv.clinicResult.Contains("재검"));
        var incomplete=Data();var iv=Admit(incomplete);iv.stage=PetTownFacilityStage.Payment;iv.suppliesUsed=true;test("new_visit_requires_completed_exam",!incomplete.facilities.TakePayment(incomplete,iv.id,100,ref money,out income));
        var legacy=JsonUtility.FromJson<PetTownResidentPetRecord>("{\"id\":\"old\",\"name\":\"기존 펫\",\"lastCheckupDay\":99}");test("legacy_pet_starts_healthy",(legacy.health==null||legacy.health.version==0)&&PetTownClinic.HealthLabel(legacy)=="건강함");
        var old=Data();Pet(old).health=null;PetTownClinic.AdvanceDay(old,500);test("legacy_first_day_grace",Pet(old).health.condition==0&&Pet(old).health.lastDay==500&&Pet(old).health.nextRiskDay==505);
        test("automatic_treatment_defaults_off",!old.Facilities.Get("clinic",4).automaticTreatment);
        var oldVisit=new PetTownFacilityVisit{id="legacy",kind=4,buildingId="clinic",residentId="owner",petId="pet",stage=PetTownFacilityStage.Payment,suppliesUsed=true};old.facilities.visits.Add(oldVisit);test("legacy_completed_visit_still_payable",old.facilities.TakePayment(old,"legacy",500,ref money,out income)&&income==140);
        var normalize=new PetTownPetHealth{version=1,condition=99,restDays=-9,recovering=true};normalize.Normalize();test("normalize_health_bounds",normalize.condition==3&&normalize.restDays==1&&normalize.diagnosed);
        var risk=Data();var rpet=Pet(risk);int riskDay=106;while(PetTownClinic.Roll(rpet.id,riskDay)%100>=6)riskDay++;rpet.health.lastDay=riskDay-1;rpet.health.nextRiskDay=100;PetTownClinic.AdvanceDay(risk,riskDay);test("deterministic_day_symptom",rpet.health.condition>0&&!rpet.health.diagnosed&&rpet.health.episode==1);int condition=rpet.health.condition;PetTownClinic.AdvanceDay(risk,riskDay);test("illness_roll_idempotent",rpet.health.episode==1&&rpet.health.condition==condition);
        var busy=Data();var busyPet=Pet(busy);busyPet.health.lastDay=riskDay-1;busyPet.health.nextRiskDay=100;busy.facilities.visits.Add(new PetTownFacilityVisit{petId=busyPet.id});PetTownClinic.AdvanceDay(busy,riskDay);test("active_visit_excluded_from_new_illness",busyPet.health.condition==0&&busyPet.health.lastDay==riskDay);
        test("existing_cafe_recipe_state_regression",PetTownCafeRecipesQA.Run().Count==29);
        return c;
    }
}
#endif
