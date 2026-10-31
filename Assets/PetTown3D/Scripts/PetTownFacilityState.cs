using System;
using System.Collections.Generic;
using UnityEngine;

public enum PetTownFacilityStage { Entrance, Reception, Registering, ToWork, Waiting, Working, Eating, ToPay, Payment, Leaving }
[Serializable]
public sealed class PetTownFacilityBusiness
{
    public string id;public int kind;public bool open,automatic,staffService,automaticTreatment;public int served,income,cafeMenu;
}
[Serializable]
public sealed class PetTownFacilityVisit
{
    public string id,buildingId,residentId,petId,residentName,petName,counterId,stationId;
    public int clinicVersion,clinicService,clinicCondition,clinicEpisode;public bool clinicCompleted,clinicApplied;public string clinicResult;
    public int kind,admittedDay,cafeMenu;public PetTownFacilityStage stage;
    public float elapsed,remaining;public bool suppliesUsed,paid,staffRequired;public string workerId;
}
[Serializable]
public sealed class PetTownFacilityReceipt
{
    public int clinicService,clinicCondition;public string clinicResult;
    public string visitId,buildingId,residentName,petName;public int kind,day,income,cafeMenu;
}
// No agents/transforms in the save. The world adapter must verify arrival before advancing.
[Serializable]
public sealed partial class PetTownFacilityState
{
    public int cafeIngredients,medicalSupplies;
    public List<PetTownFacilityBusiness> businesses=new List<PetTownFacilityBusiness>();
    public List<PetTownFacilityVisit> visits=new List<PetTownFacilityVisit>();
    public List<PetTownFacilityReceipt> receipts=new List<PetTownFacilityReceipt>();
    public static string KindName(int kind)=>kind==3?"펫 카페":"펫 병원";
    public static int Fee(int kind)=>kind==3?70:140;
    public int Stock(int kind)=>kind==3?cafeIngredients:medicalSupplies;
    public int AvailableSupplies(int kind)=>Mathf.Max(0,Stock(kind)-ReservedSupplies(kind));
    public PetTownFacilityBusiness Get(string id,int kind)
    {
        var p=businesses.Find(x=>x.id==id);if(p==null){p=new PetTownFacilityBusiness{id=id,kind=kind};businesses.Add(p);}return p;
    }
    public bool BuySupplies(int kind,ref int money)
    {
        if(kind!=3&&kind!=4)return false;int cost=kind==3?100:250;if(money<cost)return false;
        if(Stock(kind)>9995)return false;money-=cost;if(kind==3)cafeIngredients+=5;else medicalSupplies+=5;return true;
    }
    public bool Admit(PetTownProgressionState data,string building,int kind,string residentId,string petId,string counter,string station,int capacity,int day,out string reason,int service=-1)
    {
        reason="";if(kind!=3&&kind!=4){reason="운영할 시설을 다시 선택해 주세요.";return false;}
        var owner=data.residents?.people.Find(p=>p.id==residentId);var pet=owner?.pets.Find(p=>p.id==petId);
        if(owner==null||pet==null){reason="펫과 함께 사는 주민만 방문할 수 있어요.";return false;}
        if(kind==3&&pet.lastCafeDay==day){reason="오늘 이미 이용한 펫이에요.";return false;}
        if(kind==4&&!PetTownClinic.CanVisit(pet,day,out reason))return false;
        int svc=kind==4?(service<0?PetTownClinic.ServiceFor(pet):service):0;if(kind==4&&!PetTownClinic.Allowed(pet,svc,out reason))return false;
        if(!Get(building,kind).open){reason="먼저 영업을 시작해 주세요.";return false;}
        if(AvailableFor(Get(building,kind))<(kind==4?PetTownClinic.SupplyCost(svc):1)){reason=kind==3?"카페 재료가 필요해요.":"진료 소모품이 필요해요.";return false;}
        if(string.IsNullOrEmpty(counter)||string.IsNullOrEmpty(station)){reason="계산대와 테이블이 필요해요.";return false;}
        if(visits.Exists(p=>p.residentId==residentId||p.petId==petId)){reason="이미 다른 방문을 진행 중이에요.";return false;}
        if(capacity<=0||visits.FindAll(p=>p.buildingId==building).Count>=capacity||visits.Exists(p=>p.buildingId==building&&p.stationId==station)){reason="이용 가능한 테이블이 없어요.";return false;}
        visits.Add(new PetTownFacilityVisit{id=Guid.NewGuid().ToString("N"),buildingId=building,kind=kind,residentId=residentId,petId=petId,residentName=owner.name,petName=pet.name,counterId=counter,stationId=station,admittedDay=day,clinicVersion=kind==4?1:0,clinicService=svc,clinicCondition=kind==4&&pet.health!=null?pet.health.condition:0,clinicEpisode=kind==4&&pet.health!=null?pet.health.episode:0,cafeMenu=kind==3?Get(building,kind).cafeMenu:0,stage=PetTownFacilityStage.Entrance});return true;
    }
    public bool Arrive(string id)
    {
        var v=visits.Find(p=>p.id==id);if(v==null)return false;
        switch(v.stage)
        {
            case PetTownFacilityStage.Entrance:v.stage=PetTownFacilityStage.Reception;break;
            case PetTownFacilityStage.Reception:v.stage=v.kind==3?PetTownFacilityStage.Waiting:PetTownFacilityStage.Registering;v.remaining=v.kind==3?0:4.5f;break;
            case PetTownFacilityStage.ToWork:v.stage=v.kind==3?PetTownFacilityStage.Eating:PetTownFacilityStage.Waiting;v.remaining=v.kind==3?12:0;break;
            case PetTownFacilityStage.ToPay:v.stage=PetTownFacilityStage.Payment;break;
            default:return false;
        }
        return true;
    }
    public bool Begin(string id,out string reason)
    {
        reason="";var v=visits.Find(p=>p.id==id);if(v==null||v.stage!=PetTownFacilityStage.Waiting||v.suppliesUsed){reason="손님이 작업 지점에 도착할 때까지 기다려 주세요.";return false;}
        if(VisitStock(v)<VisitSupplyCost(v)){reason="소모품이 부족해요.";return false;}
        if(v.kind==3){if(v.cafeMenu==0)cafeIngredients--;else cafeRecipeSupplies[v.cafeMenu-1]--;}else medicalSupplies-=VisitSupplyCost(v);v.suppliesUsed=true;v.staffRequired=false;v.workerId=null;v.stage=PetTownFacilityStage.Working;v.remaining=v.kind==3?PetTownCafeRecipes.CookSeconds(v.cafeMenu):PetTownClinic.WorkSeconds(v.clinicService);return true;
    }
    public void TickVisit(string id,float seconds,bool atTarget,bool staffAtTarget=false)
    {
        var v=visits.Find(p=>p.id==id);if(v==null||seconds<=0)return;v.elapsed+=seconds;if(!atTarget)return;
        if(v.stage==PetTownFacilityStage.Working&&v.staffRequired&&!staffAtTarget)return;
        if(v.stage!=PetTownFacilityStage.Working&&v.stage!=PetTownFacilityStage.Registering&&v.stage!=PetTownFacilityStage.Eating)return;
        v.remaining=Mathf.Max(0,v.remaining-seconds);if(v.remaining>0)return;
        if(v.kind==4&&v.stage==PetTownFacilityStage.Working)v.clinicCompleted=true;
        v.stage=v.stage==PetTownFacilityStage.Registering?PetTownFacilityStage.ToWork:v.stage==PetTownFacilityStage.Eating?PetTownFacilityStage.ToPay:v.kind==3?PetTownFacilityStage.ToWork:PetTownFacilityStage.ToPay;
    }
    public bool TakePayment(PetTownProgressionState data,string id,int day,ref int money,out int income)
    {
        income=0;var v=visits.Find(p=>p.id==id);if(v==null||v.stage!=PetTownFacilityStage.Payment||v.paid||!v.suppliesUsed)return false;
        var owner=data.residents?.people.Find(p=>p.id==v.residentId);var pet=owner?.pets.Find(p=>p.id==v.petId);if(pet==null||(v.kind==3?pet.lastCafeDay==day:PetTownClinic.AlreadyPaid(v,pet,day)))return false;
        if(receipts.Exists(p=>p.visitId==id))return false;
        if(v.kind==4){if(v.clinicVersion>=1&&!v.clinicCompleted)return false;ApplyClinicResult(data,v,day);}
        income=VisitFee(v);v.paid=true;v.stage=PetTownFacilityStage.Leaving;money+=income+data.AddExperience(v.kind==3?3:4);
        if(v.kind==3)pet.lastCafeDay=day;else if(PetTownClinic.IsTreatment(v.clinicService))PetTownClinic.Ensure(pet,day).lastTreatmentDay=day;else pet.lastCheckupDay=day;pet.GainServiceExperience(v.kind==3?1:2);owner.happiness=Mathf.Min(100,owner.happiness+(v.kind==3?3:2));data.serviceHappiness=Mathf.Clamp(data.serviceHappiness+(v.kind==3?.4f:.6f),-20,15);
        data.Operations(day).Record(v.kind==3?"cafe":"clinic");
        var business=Get(v.buildingId,v.kind);business.served++;business.income+=income;receipts.Insert(0,new PetTownFacilityReceipt{visitId=id,buildingId=v.buildingId,kind=v.kind,day=day,income=income,cafeMenu=v.cafeMenu,clinicService=v.clinicService,clinicCondition=v.clinicCondition,clinicResult=v.clinicResult,residentName=v.residentName,petName=v.petName});if(receipts.Count>50)receipts.RemoveRange(50,receipts.Count-50);return true;
    }
    public bool Cancel(string id){var v=visits.Find(p=>p.id==id);return v!=null&&visits.Remove(v);}
    public void Normalize()
    {
        cafeIngredients=Mathf.Clamp(cafeIngredients,0,10000);medicalSupplies=Mathf.Clamp(medicalSupplies,0,10000);
        if(businesses==null)businesses=new List<PetTownFacilityBusiness>();if(visits==null)visits=new List<PetTownFacilityVisit>();if(receipts==null)receipts=new List<PetTownFacilityReceipt>();
        var ids=new HashSet<string>();businesses.RemoveAll(p=>p==null||string.IsNullOrEmpty(p.id)||(p.kind!=3&&p.kind!=4)||!ids.Add(p.id));ids.Clear();
        var residents=new HashSet<string>();var stations=new HashSet<string>();
        visits.RemoveAll(p=>p==null||string.IsNullOrEmpty(p.id)||string.IsNullOrEmpty(p.buildingId)||string.IsNullOrEmpty(p.residentId)||string.IsNullOrEmpty(p.petId)||(p.kind!=3&&p.kind!=4)||!ids.Add(p.id)||!residents.Add(p.residentId)||!stations.Add(p.buildingId+":"+p.stationId));
        foreach(var p in visits){p.elapsed=Mathf.Max(0,p.elapsed);p.remaining=Mathf.Max(0,p.remaining);if(p.paid)p.stage=PetTownFacilityStage.Leaving;else if((int)p.stage<0||(int)p.stage>9)p.stage=PetTownFacilityStage.Entrance;if(p.stage!=PetTownFacilityStage.Working||!p.staffRequired){p.staffRequired=false;p.workerId=null;}}
        NormalizeCafeRecipes();NormalizeClinic();
        receipts.RemoveAll(p=>p==null);if(receipts.Count>50)receipts.RemoveRange(50,receipts.Count-50);
    }
}
