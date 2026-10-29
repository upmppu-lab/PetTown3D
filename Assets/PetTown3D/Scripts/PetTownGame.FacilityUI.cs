using System.Collections.Generic;
using UnityEngine;

public partial class PetTownGame
{
    void OpenFacilities(int kindFilter=0)
    {
        SetMode(Mode.Play);RefreshFacilityRooms();var list=new List<PetTownInteriorState>(facilityRooms);if(kindFilter>0)list.RemoveAll(r=>!r||r.Kind!=kindFilter);var entries=new List<PetTownCatalogEntry>();
        for(int i=0;i<list.Count;i++){var room=list[i];var b=Progression.Facilities.Get(room.Id,room.Kind);entries.Add(OperationsEntry(i,PetTownFacilityState.KindName(room.Kind)+" "+(i+1),b.open?"영업 중":"준비 중","누적 "+b.served+"건 · "+b.income.ToString("N0")+" 골드","운영 보기","펫과 함께 사는 실제 주민이 방문합니다.\n완전한 이동 경로와 가구를 확인한 뒤 안내합니다.",3));}
        if(entries.Count==0)entries.Add(OperationsEntry(-1,kindFilter==3?"펫 카페를 건설해 주세요":kindFilter==4?"펫 병원을 건설해 주세요":"카페·병원을 건설해 주세요","아직 운영할 시설이 없습니다","기존 건설 목록을 이용해 주세요","건설 목록","건설 메뉴에서 펫 카페나 펫 병원을 지어 주세요.",11));
        entries.Add(OperationsEntry(-2,"시설 결제 기록",Progression.Facilities.receipts.Count+"건","카페·검진·치료 수납 내역","기록 보기","실제로 수납한 금액만 기록합니다.",8));
        catalogWindow.Show(kindFilter==3?"펫 카페 운영":kindFilter==4?"펫 병원 운영":"카페·병원 운영",entries,e=>{if(e.Key==-1){OnNavClick(1);return;}if(e.Key==-2){OpenFacilityReceipts(null);return;}var room=list[e.Key];if(room)OpenFacilityBusiness(room.Id);else OpenFacilities();},()=>{},()=>{});
    }
    void OpenFacilityBusiness(string id)
    {
        var room=FacilityRoom(id);if(!room){OpenFacilities();return;}SetMode(Mode.Play);var state=Progression.Facilities;var b=state.Get(id,room.Kind);int kind=room.Kind;var visitIds=new List<string>();
        var entries=new List<PetTownCatalogEntry>{
            OperationsEntry(-1,b.open?"영업 마감":"영업 시작",b.open?"신규 손님 접수 중":"신규 접수 중지","기존 방문은 마감 후에도 마칠 수 있어요",b.open?"마감하기":"영업하기","마감하면 새 손님만 받지 않습니다.\n이미 방문한 손님은 조리·검진·수납을 마칠 수 있어요.",3),
            OperationsEntry(-2,b.automatic?"자동 방문 끄기":"자동 방문 켜기",b.automatic?"30초마다 방문 안내":"직접 주민 선택","직원 자동 업무는 별도 설정","전환하기","영업 중이고 재료와 빈 테이블이 있을 때만 펫을 기르는 주민을 안내합니다.\n방문만으로 자동 수익이 발생하지 않습니다.",5),
            OperationsEntry(-3,kind==3?"카페 재료 보충":"진료 소모품 보충",state.Stock(kind)+"개 보유 · 예약 "+(state.Stock(kind)-state.AvailableSupplies(kind)),"5개 / "+(kind==3?100:250)+" 골드","5개 구매",kind==3?"간단한 간식·차의 조리 재료입니다.\n조리 시작 시1개를 사용합니다.\n필요한 만큼 보충해 주세요.":"예방검진은1개, 치료는2개 사용합니다.\n아직 진료하지 않은 예약분을 제외하고 보충합니다.",2),
            OperationsEntry(-4,"손님 안내","펫과 함께 사는 주민 선택",kind==3?"펫별 하루1회 이용":"검진·치료는 각각 하루1회","주민 선택","이미 다른 시설을 방문 중인 주민은 중복 안내하지 않습니다.",5),
            OperationsEntry(-5,"시설 가구 배치",FacilityTables(room).Count+"개 테이블","계산대와 원목·낮은 테이블 필요","가구 목록","선택한 시설 안에서 기존 격자·90도 회전·충돌 검사로 가구를 배치합니다.\n이동 가능한 실제 테이블만 운영에 사용됩니다.",1),
            OperationsEntry(-6,"결제 기록",b.served+"건 · "+b.income.ToString("N0")+" 골드","이 시설의 실제 수납 내역","기록 보기","최근50건의 시설 결제 내역을 보관합니다.",8)};
        var staffToggle=OperationsEntry(-8,b.staffService?"직원 자동 업무 끄기":"직원 자동 업무 켜기",b.staffService?"배치 직원이 조리·진료·수납":"직접 서비스 진행",FacilityStaffSummary(id),"전환하기","요리사/수의사와 계산 담당을 따로 배치해 주세요. 주민·펫·직원이 실제 도착한 뒤 작업하며 재료는 자동 구매하지 않습니다. 끄면 진행 중인 직원 업무는 대기하며 방문 관리에서 직접 이어갈 수 있어요.",5);staffToggle.SizeProvider=()=>FacilityStaffSummary(id);entries.Add(staffToggle);
        entries.Add(OperationsEntry(-9,"시설 직원 배치",FacilityStaffSummary(id),kind==3?"카페 요리사·계산 담당":"수의사·계산 담당","직원 선택","직원 한 명은 한 시설에만 배치됩니다. 휴무·미지급 급여와 실제 이동 경로를 확인합니다.",5));
        if(kind==4){entries.Add(OperationsEntry(-11,"병원 환자·건강","주민 펫의 검진·치료 안내","검진140G · 치료260G","환자 보기","검진으로 발견한 증상은 별도 치료 방문을 안내할 수 있습니다. 치료 후 새날을 보내며 회복합니다.",9));entries.Add(OperationsEntry(-12,b.automaticTreatment?"자동 치료 허용 끄기":"자동 치료 허용 켜기",b.automaticTreatment?"진단받은 펫도 자동 방문":"자동 방문은 검진만 안내","자동 방문·직원 업무는 별도 설정","전환하기","켜면 자동 방문이 진단받은 펫의 치료도 안내합니다. 치료 소모품2개를 보유해야 합니다. 기본값은 꺼짐이며 직접 치료 안내는 항상 가능합니다.",3));}
        if(kind==3)entries.Add(OperationsEntry(-10,"카페 메뉴·레시피",PetTownCafeRecipes.Name(b.cafeMenu),"조리 가능 "+state.AvailableFor(b)+"인분 · "+PetTownCafeRecipes.Price(b.cafeMenu)+" 골드","메뉴 관리","농장·목장 생산물로 메뉴 재료를 준비하고 이 카페에서 판매할 메뉴를 고릅니다. 메뉴 변경은 새 주문부터 적용합니다.",3));
        var refresh=OperationsEntry(-7,"방문 목록 새로고침","","진행 상태는 자동으로 갱신됩니다","목록 새로고침","새로 도착한 방문을 목록에 표시합니다.",5);refresh.PriceProvider=()=>state.visits.FindAll(v=>v.buildingId==id).Count+"건 진행 중";entries.Add(refresh);
        foreach(var v in state.visits)if(v.buildingId==id)
        {
            var visit=v;visitIds.Add(visit.id);var entry=OperationsEntry(visitIds.Count-1,visit.residentName+" · "+visit.petName,FacilityVisitStatus(visit),(kind==3?PetTownCafeRecipes.Name(visit.cafeMenu):PetTownClinic.ServiceName(visit.clinicService))+" · "+PetTownFacilityState.VisitFee(visit)+" 골드","방문 관리","조리·검진은 작업 지점에 도착한 뒤 시작할 수 있어요.\n완료 후 접수대에서 수납합니다.",9);
            entry.PriceProvider=()=>state.visits.Contains(visit)?FacilityVisitStatus(visit):"방문 완료";entries.Add(entry);
        }
        catalogWindow.Show(PetTownFacilityState.KindName(kind)+" 운영",entries,e=>
        {
            if(!OperationsReady())return;var current=FacilityRoom(id);if(!current){OpenFacilities();return;}
            if(e.Key==-11){OpenClinicPatients(id);return;}if(e.Key==-12){b.automaticTreatment=!b.automaticTreatment;SaveFacilityStaffProgress();OpenFacilityBusiness(id);return;}if(e.Key==-10){OpenCafeRecipes(id);return;}if(e.Key==-7){OpenFacilityBusiness(id);return;}if(e.Key==-9){OpenFacilityWorkers(id);return;}
            if(e.Key==-8){b.staffService=!b.staffService;if(!b.staffService)CancelFacilityWorkersForBuilding(id);SaveFacilityStaffProgress();OpenFacilityBusiness(id);return;}
            if(e.Key==-1)b.open=!b.open;else if(e.Key==-2)b.automatic=!b.automatic;
            else if(e.Key==-3){if(state.BuySupplies(kind,ref money))Message("소모품5개를 보충했어요.");else Message("골드가 부족하거나 보관 한도에 도달했어요.");}
            else if(e.Key==-4){OpenFacilityGuests(id);return;}else if(e.Key==-5){OpenFacilityFurniture(id);return;}else if(e.Key==-6){OpenFacilityReceipts(id);return;}
            else{OpenFacilityVisit(visitIds[e.Key]);return;}
            progressionDirty=true;SaveProgressionSilent();OpenFacilityBusiness(id);
        },()=>{},()=>{});
    }
    void OpenFacilityGuests(string id)
    {
        var room=FacilityRoom(id);if(!room){OpenFacilities();return;}var owners=new List<string>();var pets=new List<string>();var entries=new List<PetTownCatalogEntry>();
        foreach(var person in EnsureResidents().people)foreach(var pet in person.pets)
        {
            bool used=room.Kind==3?pet.lastCafeDay==day:!PetTownClinic.CanVisit(pet,day,out _);owners.Add(person.id);pets.Add(pet.id);entries.Add(OperationsEntry(pets.Count-1,person.name+" · "+pet.name,used?"오늘 이용 완료":"방문 가능","Lv."+pet.level+" · "+PetTownProgressionState.Species[Mathf.Clamp(pet.species,0,3)],used?"이용 확인":"시설로 안내","마을에 실제로 존재하는 주민과 주민 소유 펫입니다.\n입구·계산대·테이블 경로와 빈자리를 확인합니다.",9));
        }
        if(entries.Count==0)entries.Add(OperationsEntry(-1,"펫과 함께 사는 주민이 없어요","주민이 펫을 분양받으면 방문할 수 있어요","주민에게 펫을 분양해 주세요","주민 목록","펫을 가족으로 맞이한 주민이 카페와 병원을 이용할 수 있어요.",5));
        catalogWindow.Show("시설 방문 주민",entries,e=>{if(e.Key<0){OpenResidentRoster();return;}string reason;if(TryAdmitFacility(room,owners[e.Key],pets[e.Key],out reason))Message("시설로 안내했어요. 실제 도착 후 서비스를 진행해 주세요.");else Message(reason);OpenFacilityBusiness(id);},()=>{},()=>{});
    }
    void OpenFacilityVisit(string visitId)
    {
        var v=Progression.Facilities.visits.Find(p=>p.id==visitId);if(v==null){OpenFacilities();return;}
        string action=v.stage==PetTownFacilityStage.Waiting?(v.kind==3?"조리 시작":PetTownClinic.ServiceName(v.clinicService)+" 시작"):v.stage==PetTownFacilityStage.Payment?"결제 받기":v.stage==PetTownFacilityStage.Working&&v.staffRequired?"직접 업무 이어하기":"상태 새로고침";
        var entries=new List<PetTownCatalogEntry>{OperationsEntry(0,v.residentName+" · "+v.petName,FacilityVisitStatus(v),(v.kind==3?PetTownCafeRecipes.Name(v.cafeMenu):PetTownClinic.ServiceName(v.clinicService))+" · "+PetTownFacilityState.VisitFee(v)+" 골드",action,"작업 지점 도착을 확인한 뒤 진행합니다.\n조리·검진 재료는1개, 치료 소모품은2개 사용하며 결제는 완료 후1회만 받을 수 있습니다.",9),OperationsEntry(1,"방문 종료",v.suppliesUsed?"사용한 소모품은 반환되지 않습니다":"아직 소모품을 사용하지 않았습니다","수납 전 종료하면 수익 없음","방문 종료 확인","주민은 기존 마을 생활로 돌아갑니다.",5)};
        if(v.kind==4)entries.Add(OperationsEntry(2,"진료 결과",v.clinicApplied?v.clinicResult:"진료 완료 후 확인",PetTownClinic.ServiceName(v.clinicService),"새로고침","검진 후 이상이 있으면 수납·귀가 후 별도 치료 방문을 안내합니다. 치료가 끝나면 집에서1~2일 휴식합니다.",9));
        entries[0].PriceProvider=()=>Progression.Facilities.visits.Contains(v)?FacilityVisitStatus(v):"방문 완료";
        entries[0].ActionProvider=()=>v.stage==PetTownFacilityStage.Waiting?(v.kind==3?"조리 시작":PetTownClinic.ServiceName(v.clinicService)+" 시작"):v.stage==PetTownFacilityStage.Payment?"결제 받기":v.stage==PetTownFacilityStage.Working&&v.staffRequired?"직접 업무 이어하기":"상태 새로고침";
        catalogWindow.Show("시설 방문 관리",entries,e=>
        {
            if(!OperationsReady())return;var current=Progression.Facilities.visits.Find(p=>p.id==visitId);if(current==null){OpenFacilities();return;}string id=current.buildingId;
            if(e.Key==2){OpenFacilityVisit(visitId);return;}if(e.Key==1)CancelFacilityVisit(visitId,false);
            else if(current.stage==PetTownFacilityStage.Waiting)
            {
                string reason;if(!FacilityActionReady(current))Message("주민과 펫이 함께 도착할 때까지 기다려 주세요.");else if(!Progression.Facilities.Begin(visitId,out reason))Message(reason);else Message(current.kind==3?"조리를 시작했어요.":PetTownClinic.ServiceName(current.clinicService)+"을 시작했어요.");
            }
            else if(current.stage==PetTownFacilityStage.Working&&current.staffRequired)
            {
                if(!FacilityActionReady(current))Message("주민과 펫이 함께 도착할 때까지 기다려 주세요.");else{CancelFacilityWorkersForVisit(visitId);if(Progression.Facilities.ResumeWorkManually(visitId))Message("남은 시간부터 직접 업무를 이어갑니다.");}
            }
            else if(current.stage==PetTownFacilityStage.Payment)
            {
                int income;if(!FacilityActionReady(current))Message("주민과 펫이 수납대에 함께 도착할 때까지 기다려 주세요.");else if(Progression.Facilities.TakePayment(Progression,visitId,day,ref money,out income)){Message("수납 완료! +"+income+" 골드");}else Message("이미 수납했거나 이용 내역을 다시 확인해야 해요.");
            }
            progressionDirty=true;SaveProgressionSilent();OpenFacilityBusiness(id);
        },()=>{},()=>{});
    }
    void OpenFacilityFurniture(string id)
    {
        var room=FacilityRoom(id);if(!room){OpenFacilities();return;}SetMode(Mode.Furniture);facilityFurnitureTarget=room;OpenFurnitureCatalog();Message(PetTownFacilityState.KindName(room.Kind)+" 안에 가구를 배치해 주세요.");
    }
    void OpenFacilityReceipts(string building)
    {
        var entries=new List<PetTownCatalogEntry>();int i=0;foreach(var p in Progression.Facilities.receipts)if(building==null||building==p.buildingId)entries.Add(OperationsEntry(i++,p.residentName+" · "+p.petName,p.income+" 골드","Day "+p.day+" · "+(p.kind==3?PetTownCafeRecipes.Name(p.cafeMenu):PetTownClinic.ServiceName(p.clinicService)),"운영 목록",(p.kind==4?p.clinicResult+"\n":"")+"실제로 수납한 결제입니다.",8));
        if(entries.Count==0)entries.Add(OperationsEntry(0,"아직 결제 기록이 없어요","실제 수납 후 기록됩니다","자동 수익 없음","운영 목록","주민 방문을 끝까지 진행하고 접수대에서 수납해 주세요.",8));
        catalogWindow.Show("시설 결제 기록",entries,e=>{if(building==null)OpenFacilities();else OpenFacilityBusiness(building);},()=>{},()=>{});
    }
}
