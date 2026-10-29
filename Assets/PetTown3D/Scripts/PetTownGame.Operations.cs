using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public partial class PetTownGame
{
    public PetTownShopOperationsState Operations {get{var state=Progression.Operations(day);EnsureDailyBoard(state);return state;}}
    float nextOperationsDiscovery,nextStationLookup,nextGroomPath;int groomPathRevision=-1;bool groomPathReady;Vector3 groomPathPosition;string groomPathId;PetTownFurniturePlacement cachedStation;string cachedStationId;
    void TickOperations(float seconds)
    {
        if(loadingTown||expansionPending||!shop)return;
        Operations.Tick(seconds);UpdateEmployees(seconds);
        if(Time.unscaledTime>=nextOperationsDiscovery){nextOperationsDiscovery=Time.unscaledTime+2;foreach(var p in Progression.care.pets)if(p.speciesKind>=0&&p.speciesKind<PetTownProgressionState.Species.Length)Operations.discovered[p.speciesKind]=true;}
    }
    void OperationsNewDay(){if(Application.isFocused)Operations.NewDay(day,ref money);}
    bool OperationsReady(){if(!loadingTown&&!expansionPending&&shop)return true;Message("매장을 준비한 뒤 다시 시도해 주세요.");return false;}
    List<PetTownFurniturePlacement> GroomingTables()
    {
        var result=new List<PetTownFurniturePlacement>();if(!shop)return result;
        foreach(var f in shop.GetComponentsInChildren<PetTownFurniturePlacement>())if(f.Owner&&f.Owner.Kind==0&&((!f.Detail&&f.Kind==4)||(f.Detail&&f.Kind==13)))result.Add(f);return result;
    }
    PetTownFurniturePlacement GroomingStation()
    {
        string id=Operations.groomingStationId;if(string.IsNullOrEmpty(id))return null;
        if(cachedStation&&cachedStationId==id&&cachedStation.Owner&&cachedStation.Owner.gameObject==shop)return cachedStation;
        if(cachedStationId==id&&Time.unscaledTime<nextStationLookup)return null;
        cachedStationId=id;nextStationLookup=Time.unscaledTime+.5f;cachedStation=null;
        foreach(var p in GroomingTables())if(p.Capture().id==id){cachedStation=p;break;}return cachedStation;
    }
    bool GroomingReady()
    {
        var table=GroomingStation();if(!table)return false;
        if(groomPathId==Operations.groomingStationId&&groomPathRevision==constructionWorldRevision&&(groomPathPosition-table.transform.position).sqrMagnitude<.0001f&&Time.unscaledTime<nextGroomPath)return groomPathReady;
        groomPathId=Operations.groomingStationId;groomPathRevision=constructionWorldRevision;groomPathPosition=table.transform.position;nextGroomPath=Time.unscaledTime+.5f;groomPathReady=false;
        NavMeshHit start;if(!NavMesh.SamplePosition(EmployeeServicePoint(),out start,1.2f,NavMesh.AllAreas))return false;
        float radius=Mathf.Max(table.Footprint.x,table.Footprint.y)*.5f+.65f;
        for(int i=0;i<24;i++)
        {
            float angle=i*Mathf.PI/4;var candidate=table.transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(radius+i/8*.5f);
            NavMeshHit hit;if(!NavMesh.SamplePosition(candidate,out hit,.7f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-table.transform.position.y)>.45f)continue;
            Vector3 local=table.Owner.transform.InverseTransformPoint(hit.position);if(!table.Owner.LocalBounds.Contains(new Vector2(local.x,local.z)))continue;
            var path=new NavMeshPath();if(NavMesh.CalculatePath(start.position,hit.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete){groomPathReady=true;break;}
        }
        return groomPathReady;
    }
    string GroomPet(PetTownPetCareRecord pet)
    {
        if(!OperationsReady())return "매장 준비가 필요해요.";
        if(pet!=null&&pet.IsBreeding)return "교배 중인 펫은 미용을 잠시 쉬어 주세요.";
        if(!Progression.care.pets.Contains(pet))return "현재 펫 목록을 다시 열어 주세요.";
        if(!Operations.Groom(pet,GroomingReady(),out string reason))return reason;
        money+=Progression.AddExperience(4);progressionDirty=true;SaveProgressionSilent();PetTownPetAction.Do(PetRootFor(pet.id),PetTownPetAction.Groom,3f);return pet.displayOnly?"미용을 마쳤어요! 사장 XP +4":"미용을 마쳤어요! 분양가 +20% · 사장 XP +4";
    }
    void BuyGroomKits()
    {
        if(!OperationsReady())return;if(money<150){Message("키트 구매 골드가 부족합니다.");return;}
        money-=150;Operations.kits+=3;progressionDirty=true;SaveProgressionSilent();Message("미용 키트 3개를 구매했어요. -150 골드");
    }
    void AppendOperationsMenu(List<PetTownCatalogEntry> entries)
    {
        entries.Add(OperationsEntry(20,"직원 관리",Operations.employees.Count+"명 근무 계약","고용·교육·근무·급여 관리","직원 보기","직원은 실제 이동 가능한 지점에 도착한 뒤 일을 합니다.\n급여가 미지급되면 업무를 쉬고 정산을 기다립니다.",5));
        entries.Add(OperationsEntry(21,"미용 작업대",GroomingReady()?"지정됨":"미지정","원목 테이블을 작업대로 지정","작업대 지정","매장에 설치한 원목 테이블 또는 낮은 테이블을 지정합니다.\n가구 배치와 충돌은 그대로 유지합니다.\n성체 펫의 미용에는 키트 1개가 필요합니다.",1));
        entries.Add(OperationsEntry(22,"배송함",Operations.deliveries.Count+"건","활성 플레이 30초 배송","배송 보기","도착한 상자를 열면 주문한 사료와 키트가 가방에 들어갑니다.\n배송 담당 직원이 대신 수령할 수도 있습니다.",2));
        entries.Add(OperationsEntry(23,"퀘스트 게시판","매일 새 의뢰 5개","오늘의 의뢰·마을 안내","의뢰 보기","게임 날짜마다 새 의뢰가 올라옵니다.\n지난 의뢰는 다음 날까지 마칠 수 있어요.\n마을 안내 보상도 게시판에서 확인합니다.",5));
        entries.Add(OperationsEntry(25,"펫과 놀기","장난감 잡기","펫과 함께하는 12초 놀이","펫 선택","장난감을 터치하며 심심함과 스트레스를 낮춰 주세요.",9));
        entries.Add(OperationsEntry(26,"교배와 탄생",Progression.Breeding.nests.Count+"쌍 진행 중","성체 암수 · 새끼 공간 검사","교배 보기","같은 종류의 성체 암수를 선택합니다.\n교배 중에는 부모를 분양하지 않으며 진행 상황을 저장합니다.",9));
        entries.Add(OperationsEntry(31,"펫 전시 자리","케이지·쿠션·펫타워 배정","종류별 정원과 입주 확인","자리 보기","매장에 놓은 RAT 케이지·강아지 쿠션·고양이 펫타워에 입고한 펫을 배정합니다.\n배정한 펫은 그 가구 주변에서 지내요.",9));
        entries.Add(OperationsEntry(24,"펫 도감","발견한 펫 확인","현재 마을의 펫 종류","도감 보기","직접 입고하거나 돌본 펫 종류를 기록합니다.",9));
    }
    static PetTownCatalogEntry OperationsEntry(int key,string name,string price,string info,string action,string description,int icon=3)=>new PetTownCatalogEntry{Key=key,Name=name,Category="매장 운영",TextOnlyDetail=true,IconKey=icon,PriceLabel=price,FootprintLabel=info,ActionLabel=action,Description=description};
    void OpenGroomingStations()
    {
        SetMode(Mode.Play);var list=GroomingTables();var ids=new List<string>();var entries=new List<PetTownCatalogEntry>();
        for(int i=0;i<list.Count;i++){var p=list[i];string id=p.Capture().id;ids.Add(id);var def=PetTownCozyLibrary.Current?.Find(p.Kind,p.Detail);entries.Add(OperationsEntry(i,def?.title??"테이블",id==Operations.groomingStationId?"현재 미용 작업대":"지정 가능","매장에 배치된 실제 가구","이 작업대 지정","이 테이블을 미용 작업대로 사용합니다.\n가구를 옮기면 작업 위치도 함께 바뀝니다.\n테이블이 없어지면 미용은 대기합니다.",1));}
        if(entries.Count==0)entries.Add(OperationsEntry(-1,"사용할 테이블이 없습니다","꾸미기에서 테이블을 설치해 주세요","원목 테이블 또는 낮은 테이블","가구 목록","매장 안에 테이블을 설치한 뒤 돌아와 주세요.",1));
        catalogWindow.Show("미용 작업대 지정",entries,e=>{if(e.Key<0){OnNavClick(2);return;}var p=GroomingTables().Find(x=>x.Capture().id==ids[e.Key]);if(!p){Message("테이블 목록을 다시 확인해 주세요.");return;}string previous=Operations.groomingStationId;Operations.groomingStationId=ids[e.Key];if(!GroomingReady()){Operations.groomingStationId=previous;Message("작업대 주변 통로를 확보해 주세요.");return;}progressionDirty=true;SaveProgressionSilent();Message("미용 작업대를 지정했어요.");},()=>{},()=>{});
    }
    void OpenEmployees()
    {
        SetMode(Mode.Play);var entries=new List<PetTownCatalogEntry>();var ids=new List<string>();
        entries.Add(OperationsEntry(-1,"직원 고용","담당별 최대 5명","고용비와 일급 확인","고용 목록","청결·배송·계산·돌봄·미용·카페 요리사·수의사·농장·목장 관리사를 고용할 수 있어요.",5));
        entries.Add(OperationsEntry(-2,"급여 정산",Operations.Arrears+" 골드 미지급","현재 근무 일급 "+Operations.DailyWages+" 골드","미지급 정산","근무일마다 급여가 발생합니다.\n휴무 중에는 새 일급이 발생하지 않습니다.\n미지급 급여를 정산하면 다시 근무할 수 있어요.",8));
        entries.Add(OperationsEntry(-3,"급여 기록",Operations.payroll.Count+"일 기록","발생·지급·미지급 내역","기록 보기","매일 발생한 급여와 실제 지급액을 확인합니다.",8));
        for(int i=0;i<Operations.employees.Count;i++){var p=Operations.employees[i];ids.Add(p.id);entries.Add(OperationsEntry(i,p.name,"Lv."+p.level+" · "+EmployeeStatus(p),"일급 "+PetTownShopOperationsState.Wage(p)+" · 완료 "+p.completed+"건","직원 관리","미지급 급여 "+p.unpaid+" 골드\n휴무 설정과 교육을 관리합니다.",5));}
        catalogWindow.Show("직원 관리",entries,e=>{if(e.Key==-1)OpenHireEmployees();else if(e.Key==-3)OpenPayrollHistory();else if(e.Key==-2){if(!OperationsReady())return;int paid=Operations.PayArrears(ref money);progressionDirty=true;SaveProgressionSilent();Message("급여 "+paid+" 골드를 정산했어요.");OpenEmployees();}else OpenEmployeeDetails(ids[e.Key]);},()=>{},()=>{});
    }
    void OpenHireEmployees()
    {
        var entries=new List<PetTownCatalogEntry>();for(int i=0;i<PetTownEmployeeRole.All.Length;i++){var role=PetTownEmployeeRole.All[i];int count=Operations.employees.FindAll(p=>p.role==role.id).Count;entries.Add(OperationsEntry(i,role.title,PetTownShopOperationsState.HirePrice(role,count)+" 골드","사장 Lv."+role.level+" · 기본 일급 "+role.wage*3,"고용하기","고용 즉시 매장에 출근합니다.\n"+(role.id=="clean"?"청결55 미만인 펫을 씻깁니다.":role.id=="care"?"배부름60 미만인 펫에게 보유 사료를 줍니다.":role.id=="porter"?"도착한 배송 상자를 수령합니다.":role.id=="farmer"?"농장에 배치하면 밭 물주기·수확과 닭장 돌봄·달걀 수집을 합니다. 씨앗·사료 구매와 심기는 직접 결정합니다.":role.id=="rancher"?"목장에 배치하면 가축 먹이·물·청소와 우유·양털 수집을 합니다. 돼지 출하는 직접 선택합니다.":role.id=="chef"?"카페에 배치하면 실제 손님 주문을 조리합니다. 시설 직원 자동 업무를 켜 주세요.":role.id=="vet"?"펫 병원에 배치하면 실제 방문 펫을 예방검진합니다. 시설 직원 자동 업무를 켜 주세요.":role.id=="cashier"?"손님에게 분양 가능한 개체의 결제를 돕습니다.\n직접 분양 선택 중인 요청은 건드리지 않습니다.":"미용 작업대와 키트가 있을 때 성체 펫을 미용합니다.")+"\n휴무일에는 일급이 발생하지 않습니다.",5));}
        catalogWindow.Show("직원 고용",entries,e=>{if(!OperationsReady())return;var role=PetTownEmployeeRole.All[e.Key];if(Operations.Hire(role.id,Progression.playerLevel,GroomingReady(),ref money,out string reason)){progressionDirty=true;SaveProgressionSilent();Message(role.title+"을 고용했어요.");}else Message(reason);OpenEmployees();},()=>{},()=>{});
    }
    void OpenEmployeeDetails(string id)
    {
        var p=Operations.employees.Find(x=>x.id==id);if(p==null){OpenEmployees();return;}var entries=new List<PetTownCatalogEntry>{
            OperationsEntry(0,"교육하기",p.level>=10?"최고 레벨":PetTownShopOperationsState.UpgradePrice(p)+" 골드","현재 Lv."+p.level,"교육 확정","업무 시간이 짧아집니다.\n교육 후에는 일급도 높아집니다.\n다음 레벨 일급 "+(p.level>=10?PetTownShopOperationsState.Wage(p):EmployeeNextWage(p))+" 골드",5),
            OperationsEntry(1,p.paused?"근무 재개":"휴무 설정",EmployeeStatus(p),"미지급 "+p.unpaid+" 골드",p.paused?"근무하기":"휴무하기","휴무 중에는 일을 하지 않으며 새 일급도 발생하지 않습니다.\n이미 발생한 미지급 급여는 유지됩니다.",5),
            OperationsEntry(2,"계약 종료","환불 없음","미지급 급여 정산 후 가능","계약 종료 확정","이 직원과의 계약을 종료합니다.\n고용비와 교육비는 환불되지 않습니다.",5)};
        if(p.role=="cashier"||p.role=="chef"||p.role=="vet"||PetTownAgrarianWork.IsRole(p.role))entries.Add(OperationsEntry(3,"근무지 배정",EmployeeWorkplace(p),"직원이 실제로 이동해 일합니다","시설 선택","요리사는 카페, 수의사는 펫 병원에 배치합니다. 계산 담당은 펫샵 또는 한 시설에 배치할 수 있어요.",3));
        catalogWindow.Show(p.name,entries,e=>{var current=Operations.employees.Find(x=>x.id==id);if(current==null||!OperationsReady())return;if(e.Key==3){OpenEmployeeWorkplaces(id);return;}string reason="";if(e.Key==0){if(!Operations.Upgrade(id,ref money,out reason))Message(reason);}else if(e.Key==1)current.paused=!current.paused;else if(!Operations.Fire(id,out reason))Message(reason);progressionDirty=true;SaveProgressionSilent();OpenEmployees();},()=>{},()=>{});
    }
    int EmployeeNextWage(PetTownEmployeeRecord p)=>PetTownShopOperationsState.Wage(new PetTownEmployeeRecord{role=p.role,level=p.level+1});
    void OpenPayrollHistory()
    {
        var entries=new List<PetTownCatalogEntry>();int i=0;foreach(var p in Operations.payroll)entries.Add(OperationsEntry(i++,"Day "+p.day,"지급 "+p.paid+" 골드","발생 "+p.due+" · 미지급 "+p.arrears,"직원 목록","미지급 금액은 다음 정산까지 유지됩니다.\n휴무 중인 직원에게는 새 일급이 발생하지 않습니다.",8));
        if(entries.Count==0)entries.Add(OperationsEntry(0,"아직 급여 기록이 없어요","다음 날에 기록됩니다","지난 날짜로 소급하지 않습니다","직원 목록","직원별 일급은 직원 관리에서 확인할 수 있습니다.",8));
        catalogWindow.Show("급여 기록",entries,e=>OpenEmployees(),()=>{},()=>{});
    }
    void OpenSupplyOrders()
    {
        SetMode(Mode.Play);var entries=new List<PetTownCatalogEntry>();for(int i=0;i<5;i++){string name=i==4?"미용 키트":PetTownProgressionState.FoodNames[i]+" 기본 사료";entries.Add(OperationsEntry(i,name,PetTownShopOperationsState.DeliveryPrice(i)+" 골드 · 배송비 포함",PetTownShopOperationsState.ProductQuantity(i)+"개 · 30초 배송","배송 주문","주문 후 활성 플레이30초가 지나면 배송함에 도착합니다.\n앱을 사용하지 않는 동안 배송 시간은 멈춥니다.",2));}
        catalogWindow.Show("물품 배송 주문",entries,e=>{if(!OperationsReady())return;if(Operations.Order(e.Key,ref money,out string reason)){progressionDirty=true;SaveProgressionSilent();Message("물품을 주문했어요. 곧 배달 입구에 상자가 도착해요.");}else Message(reason);},()=>{},()=>{});
    }
    void OpenDeliveries()
    {
        SetMode(Mode.Play);var entries=new List<PetTownCatalogEntry>();var ids=new List<string>();entries.Add(OperationsEntry(-1,"물품 주문",Operations.deliveries.Count+"건 배송 중","사료·미용 키트","주문 목록","필요한 물품을 배송 주문합니다.",2));entries.Add(OperationsEntry(-2,"펫 주문","종류·성별·외형을 직접 골라요","활성 플레이 45초 후 상자 도착","펫 고르기","강아지·고양이·토끼를 직접 골라 주문합니다.\n주문한 펫은 펫샵 앞에 상자로 도착해요.",9));
        for(int i=0;i<Operations.deliveries.Count;i++){var p=Operations.deliveries[i];ids.Add(p.id);string name=p.IsPet?PetDeliveryName(p):p.kind==4?"미용 키트":p.kind>=0&&p.kind<4?PetTownProgressionState.FoodNames[p.kind]+" 사료":"물품";entries.Add(OperationsEntry(i,p.IsPet?name:name+" "+p.quantity+"개",p.Arrived?"도착했어요":Mathf.CeilToInt(p.remaining)+"초 남음","결제 "+p.paid+" 골드",p.Arrived?"상자 열기":"배송 상태 확인","상자를 열면 주문한 수량이 가방에 들어갑니다.\n배송 담당 직원이 대신 수령할 수도 있습니다.",2));}
        catalogWindow.Show("배송함",entries,e=>{if(e.Key==-2){OpenPetOrder(-1);return;}if(e.Key<0){OpenSupplyOrders();return;}if(!OperationsReady())return;if(Operations.deliveries.Find(x=>x.id==ids[e.Key])!=null&&Operations.deliveries.Find(x=>x.id==ids[e.Key]).IsPet){OpenPetCrate(ids[e.Key]);return;}if(Operations.Collect(ids[e.Key],Progression)){progressionDirty=true;SaveProgressionSilent();Message("배송 물품을 받았어요.");}else Message("아직 배송 중이거나 이미 받은 상자입니다.");OpenDeliveries();},()=>{},()=>{});
    }
}
