using System.Collections.Generic;
using UnityEngine;

public partial class PetTownGame
{
    string EmployeeWorkplace(PetTownEmployeeRecord p)
    {
        if(PetTownAgrarianWork.IsRole(p.role))return AgrarianEmployeeWorkplace(p);
        if(string.IsNullOrEmpty(p.facilityId))return p.role=="chef"||p.role=="vet"?"근무 시설 미지정":"펫샵";
        var room=FacilityRoom(p.facilityId);return room?PetTownFacilityState.KindName(room.Kind):"시설 확인 필요";
    }
    string FacilityStaffSummary(string id)
    {
        int assigned=0,working=0;foreach(var p in Operations.employees)if(p.facilityId==id){assigned++;if(!p.paused&&p.unpaid==0)working++;}
        return "배치 "+assigned+"명 · 근무 가능 "+working+"명";
    }
    bool AssignEmployeeFacility(PetTownEmployeeRecord p,string id)
    {
        if(p==null||!Operations.employees.Contains(p))return false;
        if(PetTownAgrarianWork.IsRole(p.role))return AssignAgrarianWorkplace(p,id);
        if(!string.IsNullOrEmpty(id)){var room=FacilityRoom(id);if(!room||!PetTownFacilityStaffRules.Supports(p.role,room.Kind))return false;}
        EmployeeActor actor;if(employeeActors.TryGetValue(p.id,out actor))CancelEmployeeJob(actor);
        p.facilityId=id;SaveFacilityStaffProgress();return true;
    }
    void OpenEmployeeWorkplaces(string employeeId)
    {
        var p=Operations.employees.Find(x=>x.id==employeeId);if(p==null){OpenEmployees();return;}
        if(PetTownAgrarianWork.IsRole(p.role)){OpenAgrarianWorkplaces(employeeId);return;}
        RefreshFacilityRooms();var ids=new List<string>();var entries=new List<PetTownCatalogEntry>();
        entries.Add(OperationsEntry(-1,p.role=="cashier"?"펫샵에서 근무":"근무지 해제",EmployeeWorkplace(p),"고용 계약과 급여는 유지됩니다",p.role=="cashier"?"펫샵으로 배치":"배정 해제","카페·병원은 시설에서 직원 자동 업무를 켜야 일을 시작합니다. 직원이 직접 이동하므로 통로를 확보해 주세요.",5));
        foreach(var room in facilityRooms)if(room&&PetTownFacilityStaffRules.Supports(p.role,room.Kind))
        {
            string id=room.Id;ids.Add(id);entries.Add(OperationsEntry(ids.Count-1,PetTownFacilityState.KindName(room.Kind)+" "+ids.Count,p.facilityId==id?"현재 근무 시설":"배치 가능",FacilityStaffSummary(id),"이 시설에 배치","다른 시설과 동시에 근무하지 않습니다. 휴무·미지급 급여는 그대로 적용됩니다.",3));
        }
        if(ids.Count==0)entries.Add(OperationsEntry(-2,"배치할 시설이 없어요",p.role=="chef"?"카페를 건설해 주세요":p.role=="vet"?"펫 병원을 건설해 주세요":"카페 또는 펫 병원을 건설해 주세요","건설한 시설만 선택할 수 있어요","직원 목록","필요한 시설을 건설하고 계산대·테이블을 배치해 주세요.",3));
        catalogWindow.Show(p.name+" · 근무지",entries,e=>{if(!OperationsReady())return;if(e.Key==-2){OpenEmployees();return;}var current=Operations.employees.Find(x=>x.id==employeeId);if(AssignEmployeeFacility(current,e.Key<0?null:ids[e.Key]))Message("근무지를 변경했어요.");else Message("현재 직원과 시설을 다시 확인해 주세요.");OpenEmployees();},()=>{},()=>{});
    }
    void OpenFacilityWorkers(string id)
    {
        var room=FacilityRoom(id);if(!room){OpenFacilities();return;}var ids=new List<string>();var entries=new List<PetTownCatalogEntry>();
        foreach(var p in Operations.employees)if(PetTownFacilityStaffRules.Supports(p.role,room.Kind))
        {
            ids.Add(p.id);entries.Add(OperationsEntry(ids.Count-1,p.name,EmployeeStatus(p),"Lv."+p.level+" · "+EmployeeWorkplace(p),p.facilityId==id?"배치 해제":"이 시설에 배치",p.role=="cashier"?"주민과 펫이 수납대에 도착한 뒤 실제 결제를 받습니다.":"주민·펫·직원이 모두 도착해야 조리 또는 예방검진을 시작합니다. 재료는 작업당 한 번 사용하며 직접 업무로 전환할 수도 있습니다.",5));
        }
        entries.Add(OperationsEntry(-1,"직원 고용",room.Kind==3?"카페 요리사·계산 담당":"수의사·계산 담당","급여는 기존 직원 규칙 적용","고용 목록","시설 자동 업무를 켜고 담당 직원을 배치해 주세요. 재료 구매는 직접 관리합니다.",5));
        entries.Add(OperationsEntry(-2,"시설 운영으로",FacilityStaffSummary(id),"영업·자동 방문·자동 업무","운영 보기","시설 운영으로 돌아갑니다.",3));
        catalogWindow.Show(PetTownFacilityState.KindName(room.Kind)+" · 직원 배치",entries,e=>{if(e.Key==-1){OpenHireEmployees();return;}if(e.Key==-2){OpenFacilityBusiness(id);return;}if(!OperationsReady())return;var p=Operations.employees.Find(x=>x.id==ids[e.Key]);if(!AssignEmployeeFacility(p,p!=null&&p.facilityId==id?null:id))Message("직원이나 시설을 다시 확인해 주세요.");OpenFacilityWorkers(id);},()=>{},()=>{});
    }
}
