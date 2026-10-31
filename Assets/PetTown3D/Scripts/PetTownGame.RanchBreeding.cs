using System.Collections.Generic;
using UnityEngine;

public partial class PetTownGame
{
    string RanchAnimalStatus(PetTownRanchLot lot,PetTownRanchAnimal a)
    {
        a.EnsureSex();return Progression.Ranch.BreedingLocked(lot,a.id)?"번식 중 · "+a.SexLabel:(a.Adult?"성체":a.grown+" /2일 성장")+" · "+a.SexLabel;
    }
    void OpenRanchPurchase(string id,int species)
    {
        if(!RanchReady(id,out var lot))return;var state=Progression.Ranch;var entries=new List<PetTownCatalogEntry>();
        for(int sex=1;sex<=2;sex++)
        {
            var row=OperationsEntry(sex,PetTownRanchState.Species[species]+" "+(sex==1?"수컷":"암컷"),PetTownRanchState.Prices[species]+" 골드","어린 개체 · 성장2일","1마리 맞이하기","현재 "+lot.animals.FindAll(x=>x.species==species).Count+"마리 · 새끼 예약 "+state.Reserved(lot,species)+" / 정원 "+PetTownRanchState.Capacity[species]+"마리\n먹이·물과 청결40 이상인2일 동안 성체로 자랍니다.\n"+(species==0&&sex==1?"수컷은 번식용으로 우유를 생산하지 않습니다.":species==0?"성체 암컷은 우유를 생산합니다.":species==1?"성체 양은 성별에 관계없이 양털을 생산합니다.":"성체 돼지는 개체 판매 또는 고기 출하를 선택할 수 있습니다."),9);row.Thumbnail=PetTownRanchLibrary.Current.animals[species].portrait;entries.Add(row);
        }
        entries.Add(OperationsEntry(-1,"가축 관리로","성별을 선택해 맞이합니다","새끼 예약 자리 보존","돌아가기","목장 관리로 돌아갑니다.",3));
        catalogWindow.Show("가축 맞이하기",entries,e=>{if(e.Key<0){OpenRanchLot(id);return;}if(!RanchReady(id,out var current))return;if(state.BuyAnimalOfSex(current,species,e.Key,day,ref money)){progressionDirty=true;RefreshRanch(id);SaveProgressionSilent();Message("어린 가축을 맞이했어요.");}else Message("골드와 새끼 예약을 포함한 정원을 확인해 주세요.");OpenRanchPurchase(id,species);},()=>{},()=>{});
    }
    void OpenRanchAnimal(string id,string animalId)
    {
        if(!RanchReady(id,out var lot))return;var a=lot.animals.Find(x=>x.id==animalId);if(a==null){OpenRanchLot(id);return;}var state=Progression.Ranch;
        string production=a.species==0&&a.sex==1?"번식용 수컷 · 우유 없음":a.ready>0?PetTownRanchState.Products[a.species]+" 준비 완료":a.Adult?"돌봄 후 생산":"성장 중";
        var entries=new List<PetTownCatalogEntry>{
            OperationsEntry(0,a.name,RanchAnimalStatus(lot,a),production,"개체 판매",PetTownRanchState.Species[a.species]+" · "+a.SexLabel+" · "+a.generation+"세대\n"+(string.IsNullOrEmpty(a.parentA)?"목장에 맞이한 개체입니다.":"목장에서 태어난 개체입니다. 부모는 탄생 기록에서 확인할 수 있어요.")+"\n판매 예상가 "+PetTownRanchState.AnimalSellQuote(a)+" 골드\n준비된 생산물을 먼저 모아 주세요. 번식 중인 부모는 판매할 수 없습니다.",9),
            OperationsEntry(1,"이 개체의 짝 찾기",state.BreedingLocked(lot,a.id)?"번식 진행 중":a.breedRestDays>0?"번식 휴식 "+a.breedRestDays+"일":"성체 암수 선택","같은 종류 · 새끼 자리1칸","짝 선택","오늘 먹이·물과 청결60 이상이 필요합니다. 부모·자식과 같은 부모의 자식끼리는 선택할 수 없습니다.",9),
            OperationsEntry(2,"가축 관리로",(a.fedDay==day?"먹이 완료":"먹이 필요")+" · "+(a.wateredDay==day?"물 완료":"물 필요"),"목장 개체 목록","돌아가기","목장 관리로 돌아갑니다.",3)};
        entries[0].Thumbnail=PetTownRanchLibrary.Current.animals[a.species].portrait;
        catalogWindow.Show("목장 개체 정보",entries,e=>{if(e.Key==2){OpenRanchLot(id);return;}if(e.Key==0){OpenRanchAnimalSale(id,animalId);return;}if(state.CanBreedAnimal(lot,a,day,out string reason))OpenRanchBreedPartners(id,animalId);else{Message(reason);OpenRanchAnimal(id,animalId);}},()=>{},()=>{});
    }
    void OpenRanchAnimalSale(string id,string animalId)
    {
        if(!RanchReady(id,out var lot))return;var a=lot.animals.Find(x=>x.id==animalId);if(a==null){OpenRanchLot(id);return;}
        var row=OperationsEntry(0,a.name+" 판매",PetTownRanchState.AnimalSellQuote(a)+" 골드",PetTownRanchState.Species[a.species]+" · "+a.SexLabel,"이 개체 판매","선택한 개체가 목장을 떠나고 표시된 골드를 받습니다.\n가방의 우유·양털·고기는 바뀌지 않습니다. 번식 중인 부모와 생산물이 준비된 개체는 판매할 수 없습니다.\n돼지의 고기 출하는 별도 메뉴입니다.",9);row.Thumbnail=PetTownRanchLibrary.Current.animals[a.species].portrait;
        catalogWindow.Show("목장 개체 판매",new[]{row,OperationsEntry(1,"판매하지 않고 돌아가기",a.name,"개체 정보 유지","돌아가기","개체 정보로 돌아갑니다.",3)},e=>{if(e.Key==1){OpenRanchAnimal(id,animalId);return;}if(!RanchReady(id,out var current))return;if(Progression.Ranch.SellAnimal(current,animalId,ref money,out string reason)){CancelAgrarianWorkers(id);RefreshRanch(id);progressionDirty=true;SaveProgressionSilent();Message("선택한 가축을 판매했어요.");OpenRanchLot(id);}else{Message(reason);OpenRanchAnimal(id,animalId);}},()=>{},()=>{});
    }
    void OpenRanchBreeding(string id)
    {
        if(!RanchReady(id,out var lot))return;var state=Progression.Ranch;var entries=new List<PetTownCatalogEntry>{OperationsEntry(-1,"새로운 번식","성체 암컷·수컷 한 쌍","돌봄2일 · 새끼1마리","부모 선택","오늘 먹이·물과 청결60 이상이 필요합니다. 새끼 자리1칸을 미리 예약하며 실제 돌봄을 한 연속된 새날에만 진행됩니다.",9),OperationsEntry(-2,"목장 탄생 기록",state.births.FindAll(x=>x.lotId==id).Count+"건","새끼·부모·세대 기록","기록 보기","최근30건의 목장 출생을 보관합니다.",9)};var ids=new List<string>();
        foreach(var pregnancy in state.pregnancies)if(pregnancy.lotId==id){ids.Add(pregnancy.id);entries.Add(OperationsEntry(ids.Count-1,PetTownRanchState.Species[pregnancy.species]+" 번식",pregnancy.remainingDays+"일 돌봄 남음","새끼1마리 자리 예약","진행 보기",pregnancy.motherName+" + "+pregnancy.fatherName+"\n부모 모두에게 먹이와 물을 주고 청결40 이상을 유지해 주세요. 날짜를 건너뛰거나 돌봄이 부족한 날은 진행하지 않습니다.",9));}
        entries.Add(OperationsEntry(-3,"가축 관리로","번식 중인 부모는 판매·출하 불가","취소해도 부모 휴식 유지","돌아가기","목장 관리로 돌아갑니다.",3));
        catalogWindow.Show("목장 번식",entries,e=>{if(e.Key==-1){OpenRanchBreedPartners(id,null);return;}if(e.Key==-2){OpenRanchBirths(id);return;}if(e.Key==-3){OpenRanchLot(id);return;}OpenRanchPregnancy(id,ids[e.Key]);},()=>{},()=>{});
    }
    void OpenRanchBreedPartners(string id,string firstId)
    {
        if(!RanchReady(id,out var lot))return;var state=Progression.Ranch;var first=string.IsNullOrEmpty(firstId)?null:lot.animals.Find(x=>x.id==firstId);if(firstId!=null&&first==null){OpenRanchBreeding(id);return;}
        var ids=new List<string>();var entries=new List<PetTownCatalogEntry>();foreach(var a in lot.animals)
        {
            a.EnsureSex();if(first!=null&&(a==first||a.species!=first.species||a.sex==first.sex))continue;
            bool ready=state.CanBreedAnimal(lot,a,day,out string reason);if(first!=null&&PetTownRanchState.Related(first,a)){ready=false;reason="가까운 혈통은 선택 불가";}
            ids.Add(a.id);var row=OperationsEntry(ids.Count-1,a.name,RanchAnimalStatus(lot,a),ready?"번식 후보":reason,ready?(first==null?"첫 부모 선택":"이 짝으로 번식"):"상태 확인",first==null?"다음 화면에서 같은 종류의 다른 성별 개체를 선택합니다.":first.name+"과 짝을 이룹니다.\n새끼1마리 자리 예약 · 돌봄2일 · 부모 휴식3일. 준비된 새끼 자리는 추가 구매로 사용하지 않습니다.",9);row.Thumbnail=PetTownRanchLibrary.Current.animals[a.species].portrait;entries.Add(row);
        }
        if(ids.Count==0)entries.Add(OperationsEntry(-1,"맞는 짝이 없어요","같은 종류의 성체 암수가 필요해요","성별을 골라 어린 개체 맞이하기","가축 맞이하기","성장 후 먹이·물·청결을 준비해 주세요.",9));entries.Add(OperationsEntry(-2,"번식 목록으로","부모를 선택하지 않고 돌아갑니다","진행 중인 번식 확인","돌아가기","번식 관리로 돌아갑니다.",3));
        catalogWindow.Show(first==null?"첫 부모 선택":"두 번째 부모 선택",entries,e=>{if(e.Key==-2){OpenRanchBreeding(id);return;}if(e.Key==-1){OpenRanchPurchase(id,first?.species??0);return;}if(!RanchReady(id,out var current))return;string selected=ids[e.Key];var animal=current.animals.Find(x=>x.id==selected);if(first==null){if(state.CanBreedAnimal(current,animal,day,out string reason))OpenRanchBreedPartners(id,selected);else{Message(reason);OpenRanchBreedPartners(id,null);}return;}if(state.StartBreeding(current,first.id,selected,day,out string result)){progressionDirty=true;SaveProgressionSilent();Message("새끼1마리의 자리를 예약했어요. 부모를 돌봐 주세요.");OpenRanchBreeding(id);}else{Message(result);OpenRanchBreedPartners(id,first.id);}},()=>{},()=>{});
    }
    void OpenRanchPregnancy(string id,string pregnancyId)
    {
        if(!RanchReady(id,out var lot))return;var state=Progression.Ranch;var p=state.pregnancies.Find(x=>x.id==pregnancyId&&x.lotId==id);if(p==null){OpenRanchBreeding(id);return;}
        catalogWindow.Show("목장 번식 진행",new[]{OperationsEntry(0,PetTownRanchState.Species[p.species]+" 번식",p.remainingDays+"일 돌봄 남음","새끼1마리 자리 예약","번식 취소",p.motherName+" + "+p.fatherName+"\n취소하면 새끼가 태어나지 않고 예약 자리만 풀립니다. 부모의 남은 휴식 일수는 유지합니다.",9),OperationsEntry(1,"번식 목록으로","번식을 계속 진행합니다","먹이·물·청결 유지","돌아가기","번식 관리로 돌아갑니다.",3)},e=>{if(e.Key==0&&RanchReady(id,out var current)&&state.CancelBreeding(current,pregnancyId)){progressionDirty=true;SaveProgressionSilent();Message("번식을 취소하고 예약 자리를 풀었어요.");}OpenRanchBreeding(id);},()=>{},()=>{});
    }
    void OpenRanchBirths(string id)
    {
        if(!RanchReady(id,out var lot))return;var entries=new List<PetTownCatalogEntry>();foreach(var birth in Progression.Ranch.births)if(birth.lotId==id)entries.Add(OperationsEntry(entries.Count,birth.childName,"Day "+birth.day+" · "+birth.generation+"세대",PetTownRanchState.Species[birth.species]+" · "+(birth.sex==1?"수컷":"암컷"),"번식 목록",birth.motherName+" + "+birth.fatherName+"의 새끼입니다.\n어린 개체로 태어나 먹이·물과 청결40 이상인2일 동안 성장합니다.",9));if(entries.Count==0)entries.Add(OperationsEntry(0,"아직 탄생 기록이 없어요","부모 돌봄2일 뒤 탄생","새끼 자리 예약 필요","번식 목록","번식 목록에서 부모를 선택해 주세요.",9));catalogWindow.Show("목장 탄생 기록",entries,e=>OpenRanchBreeding(id),()=>{},()=>{});
    }
}
