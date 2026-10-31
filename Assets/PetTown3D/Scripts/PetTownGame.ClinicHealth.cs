using System.Collections.Generic;

public partial class PetTownGame
{
    void OpenClinicPatients(string id)
    {
        var room=FacilityRoom(id);if(!room||room.Kind!=4){OpenFacilities();return;}
        var owners=new List<string>();var pets=new List<string>();var entries=new List<PetTownCatalogEntry>();
        var people=EnsureResidents().people;
        foreach(var owner in people)foreach(var pet in owner.pets)
        {
            owners.Add(owner.id);pets.Add(pet.id);int service=PetTownClinic.ServiceFor(pet);bool ready=PetTownClinic.CanVisit(pet,day,out _);
            entries.Add(OperationsEntry(pets.Count-1,pet.name,PetTownClinic.HealthLabel(pet),ready?PetTownClinic.ServiceName(service)+" · "+PetTownClinic.Fee(service)+" 골드":pet.health!=null&&pet.health.recovering?"새날을 보내며 회복해요":"오늘 진료를 마쳤어요",ready?PetTownClinic.ServiceName(service)+" 안내":"상태 확인",owner.name+"의 펫입니다.\n"+PetTownClinic.HealthLabel(pet)+"\n"+(service==1?"진단받은 증상을 치료합니다. 치료 소모품2개를 사용하며 완료 후1~2일 휴식합니다.":"검진 소모품1개로 건강 상태를 확인합니다. 이상이 발견되면 수납·귀가 후 치료 방문을 따로 안내할 수 있습니다.")+"\n주민·펫·수의사가 실제 도착해야 직원 업무가 진행됩니다.",9));
        }
        if(entries.Count==0)entries.Add(OperationsEntry(-1,"주민 펫이 없어요","분양 후 병원을 이용할 수 있어요","주민의 펫 목록에서 건강 확인","주민 목록","주민이 펫을 가족으로 맞이하면 검진과 치료를 안내할 수 있습니다.",5));
        entries.Add(OperationsEntry(-2,"병원 운영으로","소모품·직원·자동 치료 설정","진료 후 수납은 접수대에서","돌아가기","병원 운영 화면으로 돌아갑니다.",3));
        catalogWindow.Show("병원 환자·건강",entries,e=>
        {
            if(e.Key==-1){OpenResidentRoster();return;}if(e.Key==-2){OpenFacilityBusiness(id);return;}if(!OperationsReady())return;
            OpenClinicServiceChoices(id,owners[e.Key],pets[e.Key]);
        },()=>{},()=>{});
    }
    void OpenClinicServiceChoices(string id,string ownerId,string petId)
    {
        var room=FacilityRoom(id);var owner=EnsureResidents().people.Find(x=>x.id==ownerId);var pet=owner?.pets.Find(x=>x.id==petId);if(!room||room.Kind!=4||pet==null){OpenClinicPatients(id);return;}
        var entries=new List<PetTownCatalogEntry>();string why;bool can=PetTownClinic.CanVisit(pet,day,out why);
        for(int s=0;s<PetTownClinic.ServiceCount;s++)
        {
            string reason;bool fit=PetTownClinic.Allowed(pet,s,out reason);bool open=Progression.playerLevel>=PetTownClinic.UnlockLevel(s);
            if(!fit)continue;
            entries.Add(OperationsEntry(s,PetTownClinic.ServiceName(s),open?PetTownClinic.Fee(s)+" 골드 · 소모품 "+PetTownClinic.SupplyCost(s)+"개":"플레이어 Lv."+PetTownClinic.UnlockLevel(s)+"에 해금","약 "+(int)(PetTownClinic.WorkSeconds(s)+.5f)+"초",!can?"이용 불가":open?"안내":"잠김",pet.name+" · "+PetTownClinic.HealthLabel(pet)+"\n"+PetTownClinic.ServiceNote(s)+"\n주민·펫·수의사가 실제 도착해야 진료가 진행됩니다.",9));
        }
        entries.Add(OperationsEntry(-1,"환자 목록으로",pet.name,"다른 펫 선택","돌아가기","환자 목록으로 돌아갑니다.",3));
        catalogWindow.Show(pet.name+" 진료 선택",entries,e=>
        {
            if(e.Key==-1){OpenClinicPatients(id);return;}if(!OperationsReady())return;
            string reason;if(!can)Message(why);else if(TryAdmitFacility(FacilityRoom(id),ownerId,petId,out reason,e.Key)){Message(PetTownClinic.ServiceName(e.Key)+" 안내를 시작했어요. 도착 후 진료합니다.");OpenClinicPatients(id);return;}else Message(reason);
            OpenClinicServiceChoices(id,ownerId,petId);
        },()=>{},()=>{});
    }
    void OpenResidentPetHealth(string ownerId,string petId)
    {
        var owner=EnsureResidents().people.Find(x=>x.id==ownerId);var pet=owner?.pets.Find(x=>x.id==petId);if(pet==null){OpenResidentRoster();return;}
        var h=pet.health;var entries=new List<PetTownCatalogEntry>{
            OperationsEntry(0,pet.name+" 건강",PetTownClinic.HealthLabel(pet),h!=null&&h.lastExamDay>=0?"최근 검진 Day "+h.lastExamDay:"아직 검진 기록이 없어요","병원 선택","컨디션 변화는 플레이 중 새날에만 계산합니다. 검진으로 증상을 확인한 뒤 치료할 수 있으며 치료 후1~2일 쉬면 회복합니다. 오프라인 날짜의 질병과 회복을 소급 적용하지 않습니다.",9),
            OperationsEntry(1,"펫 정보로",owner.name+"의 펫","산책·성장 정보","돌아가기","펫 정보로 돌아갑니다.",9)};
        catalogWindow.Show("펫 건강",entries,e=>{if(e.Key==1)OpenResidentPetActions(ownerId,petId);else OpenPetClinicChoices(ownerId,petId);},()=>{},()=>{});
    }
    void OpenPetClinicChoices(string ownerId,string petId)
    {
        RefreshFacilityRooms();var list=new List<string>();var entries=new List<PetTownCatalogEntry>();var owner=EnsureResidents().people.Find(x=>x.id==ownerId);var pet=owner?.pets.Find(x=>x.id==petId);if(pet==null){OpenResidentRoster();return;}
        foreach(var room in facilityRooms)if(room&&room.Kind==4)
        {
            var b=Progression.Facilities.Get(room.Id,4);list.Add(room.Id);entries.Add(OperationsEntry(list.Count-1,"펫 병원 "+list.Count,b.open?"영업 중":"준비 중",PetTownClinic.HealthLabel(pet),"이곳으로 안내","입구·진료대·접수대의 실제 이동 경로와 소모품을 확인한 뒤 안내합니다.",3));
        }
        if(list.Count==0)entries.Add(OperationsEntry(-1,"펫 병원을 지어 주세요","이용할 병원이 없어요","건설 후 검진·치료 가능","시설 목록","건설 목록에서 펫 병원을 설치하고 계산대와 진료용 테이블을 배치해 주세요.",11));
        entries.Add(OperationsEntry(-2,"건강 정보로",PetTownClinic.HealthLabel(pet),"치료 완료 후 휴식 필요","돌아가기","건강 정보로 돌아갑니다.",9));
        catalogWindow.Show("이용할 병원",entries,e=>{if(e.Key==-1){OpenFacilities();return;}if(e.Key==-2){OpenResidentPetHealth(ownerId,petId);return;}if(!OperationsReady())return;string reason;if(TryAdmitFacility(FacilityRoom(list[e.Key]),ownerId,petId,out reason)){Message("병원으로 안내했어요.");OpenFacilityBusiness(list[e.Key]);}else{Message(reason);OpenPetClinicChoices(ownerId,petId);}},()=>{},()=>{});
    }
}
