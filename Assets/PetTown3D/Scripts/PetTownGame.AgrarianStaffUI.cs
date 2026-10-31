using System.Collections.Generic;
using UnityEngine;

public partial class PetTownGame
{
    string AgrarianEmployeeWorkplace(PetTownEmployeeRecord p)
    {
        if(string.IsNullOrEmpty(p.facilityId))return "근무지 미지정";
        return AgrarianWorkplace(p.facilityId,p.role=="farmer")?(p.role=="farmer"?"농장":"목장"):"근무지 확인 필요";
    }
    bool AssignAgrarianWorkplace(PetTownEmployeeRecord p,string id)
    {
        if(!PetTownAgrarianWork.IsRole(p.role))return false;
        if(!string.IsNullOrEmpty(id)&&!AgrarianWorkplace(id,p.role=="farmer"))return false;
        EmployeeActor actor;if(employeeActors.TryGetValue(p.id,out actor))CancelEmployeeJob(actor);p.facilityId=id;SaveFacilityStaffProgress();return true;
    }
    void OpenAgrarianWorkplaces(string employeeId)
    {
        var p=Operations.employees.Find(x=>x.id==employeeId);if(p==null){OpenEmployees();return;}
        bool farm=p.role=="farmer";var ids=new List<string>();var entries=new List<PetTownCatalogEntry>();
        entries.Add(OperationsEntry(-1,"근무지 해제",EmployeeWorkplace(p),"고용 계약과 급여는 유지됩니다","배정 해제","배정이 없어도 휴무 전환 전까지 기존 직원 급여 규칙이 적용됩니다.",5));
        if(farm)foreach(var view in FarmRoots())ids.Add(view.Id);else foreach(var view in RanchRoots())ids.Add(view.Id);
        for(int i=0;i<ids.Count;i++){string id=ids[i];entries.Add(OperationsEntry(i,(farm?"농장 ":"목장 ")+(i+1),p.facilityId==id?"현재 근무지":"배치 가능",FacilityStaffSummary(id),"이곳에 배치","관리 화면에서 직원 자동 업무를 켜 주세요. 물품 구매와 판매·출하·심기는 직접 결정합니다.",3));}
        if(ids.Count==0)entries.Add(OperationsEntry(-2,"배치할 "+(farm?"농장":"목장")+"이 없어요","건설한 곳만 선택할 수 있어요","먼저 건설해 주세요","직원 목록","고용한 직원은 근무지를 배정한 뒤 일합니다.",3));
        catalogWindow.Show(p.name+" · 근무지",entries,e=>{if(!OperationsReady())return;if(e.Key==-2){OpenEmployees();return;}var worker=Operations.employees.Find(x=>x.id==employeeId);if(!AssignEmployeeFacility(worker,e.Key<0?null:ids[e.Key]))Message("현재 직원과 근무지를 다시 확인해 주세요.");OpenEmployees();},()=>{},()=>{});
    }
    void OpenAgrarianWorkers(string id,bool farm)
    {
        if(!AgrarianWorkplace(id,farm)){if(farm)OpenFarmManagement();else OpenRanches();return;}
        var ids=new List<string>();var entries=new List<PetTownCatalogEntry>();string role=farm?"farmer":"rancher";
        foreach(var p in Operations.employees)if(p.role==role)
        {
            ids.Add(p.id);entries.Add(OperationsEntry(ids.Count-1,p.name,EmployeeStatus(p),"Lv."+p.level+" · "+EmployeeWorkplace(p),p.facilityId==id?"배치 해제":"이곳에 배치",farm?"작업 지점에 도착해 밭 물주기·수확, 닭 먹이·물·청소와 달걀 수집을 합니다. 보유 먹이만 사용하며 씨앗 구매와 심기는 직접 결정합니다.":"현장 작업 지점에서 먹이·물·청소, 우유·양털 수집을 합니다. 보유 먹이만 사용하며 동물 구매와 돼지 출하는 직접 선택합니다.",5));
        }
        entries.Add(OperationsEntry(-1,"관리사 고용",farm?"농장 관리사":"목장 관리사","급여·휴무·교육 규칙 적용","고용 목록","고용 후 이곳에 배치하고 직원 자동 업무를 켜 주세요.",5));
        entries.Add(OperationsEntry(-2,(farm?"농장":"목장")+" 관리로",FacilityStaffSummary(id),"자동 업무와 자원 관리","관리 보기","관리 화면으로 돌아갑니다.",3));
        catalogWindow.Show((farm?"농장":"목장")+" · 직원 배치",entries,e=>{if(e.Key==-1){OpenHireEmployees();return;}if(e.Key==-2){if(farm)OpenFarmLot(id);else OpenRanchLot(id);return;}if(!OperationsReady())return;var worker=Operations.employees.Find(x=>x.id==ids[e.Key]);if(!AssignEmployeeFacility(worker,worker!=null&&worker.facilityId==id?null:id))Message("직원이나 근무지를 다시 확인해 주세요.");OpenAgrarianWorkers(id,farm);},()=>{},()=>{});
    }
    void CancelAgrarianWorkers(string id){foreach(var a in employeeActors.Values)if(a.task==EmployeeTask.Agrarian&&a.workplaceId==id)CancelEmployeeJob(a);}
}
