using System;
using System.Collections.Generic;
using UnityEngine;

// Optional progression extension. Movement, animation and navigation remain in their existing owners.
public partial class PetTownGame
{
    PetTownProgressionState progression;
    bool progressionDirty,silentProgressSave,adoptionSelectionOpen;
    float progressionSaveAt;
    int adoptionGeneration;
    public PetTownProgressionState Progression {get{EnsureProgression();return progression;}}
    void EnsureProgression()
    {
        if(progression!=null)return;
        progression=new PetTownProgressionState{care=PetTownPetCareState.Load()};progression.Normalize();progression.MigrateStock(dogStock,catStock,rabbitStock);SyncProgressionStock();
    }
    void LoadProgression(PetTownProgressionState value)
    {
        progression=value;
        if(progression==null)EnsureProgression();
        else{progression.Normalize();progression.MigrateStock(dogStock,catStock,rabbitStock);SyncProgressionStock();}
    }
    void SyncProgressionStock(){dogStock=progression.Stock(0);catStock=progression.Stock(1);rabbitStock=progression.Stock(2);inventory=dogStock+catStock+rabbitStock;}
    void SaveProgressionSilent(){if(loadingTown||expansionPending||cam==null)return;silentProgressSave=true;try{SaveGame();progressionDirty=false;}finally{silentProgressSave=false;}}
    void UpdateProgression(float seconds)
    {
        if(!Application.isFocused||!Application.isPlaying||seconds<=0)return;
        RefreshShopPetActors();UpdateResidentActors();Progression.Tick(Mathf.Min(seconds,.25f));TickOperations(Mathf.Min(seconds,.25f));TickBreeding(Mathf.Min(seconds,.25f));TickFacilities(Mathf.Min(seconds,.25f));TickCommunity(Mathf.Min(seconds,.25f));UpdateResidentCompanions(Mathf.Min(seconds,.25f));progressionDirty=true;
        if(Time.unscaledTime>=progressionSaveAt){progressionSaveAt=Time.unscaledTime+30;SaveProgressionSilent();}
    }
    void OnApplicationPause(bool pause){if(pause&&progression!=null&&progressionDirty)SaveProgressionSilent();}
    void OnApplicationQuit(){if(progression!=null&&progressionDirty)SaveProgressionSilent();}
    void ProgressionNewDay(){ResidentsNewDay();PetTownClinic.AdvanceDay(Progression,day);OperationsNewDay();FarmNewDay();RanchNewDay();if(Progression.community!=null)Progression.community.AdvanceDay(day);Progression.serviceHappiness*=.6f;progressionDirty=true;}
    int FoodLine(PetTownPetCareRecord p)=>PetTownProgressionState.FoodLineOf(Mathf.Clamp(p.speciesKind,0,PetTownProgressionState.Species.Length-1));
    string FoodDescription(PetTownPetCareRecord p){int kind=FoodLine(p);return PetTownProgressionState.FoodNames[kind]+" 기본 사료  "+Progression.food[kind]+"개";}
    void BuyCareFood(PetTownPetCareRecord pet)
    {
        if(loadingTown||expansionPending){Message("건물 변경이 끝난 뒤 구매해 주세요.");return;}
        int kind=FoodLine(pet),price=PetTownProgressionState.FoodPrice[kind]*5;
        if(money<price){Message("사료 구매 골드가 부족합니다.");return;}
        money-=price;Progression.food[kind]+=5;progressionDirty=true;SaveProgressionSilent();Message("기본 사료 5개를 구매했어요. -"+price+" 골드");
    }
    GameObject PetRootFor(string id)
    {
        LivePet shopActor;if(shopPetActors.TryGetValue(id,out shopActor)&&shopActor.root)return shopActor.root;
        foreach(var live in livePets)if(live.root&&!live.inventory&&live.careId==id)return live.root;return null;
    }
    string ApplyProgressionCare(PetTownPetCareRecord pet,int action)
    {
        if(loadingTown||expansionPending)return "건물 변경이 끝난 뒤 돌봐 주세요.";
        if(!Progression.care.pets.Contains(pet))return "현재 펫 목록을 다시 열어 주세요.";
        if(pet.busy)return "지금은 함께 놀고 있어요.";
        if(action==7)return GroomPet(pet);
        if(!pet.NeedsCare(action))return "지금은 충분히 만족하고 있어요.";
        if(action==0){int kind=FoodLine(pet);if(Progression.food[kind]<=0)return "기본 사료가 없어요. 사료를 구매해 주세요.";Progression.food[kind]--;}
        pet.Care(action);Operations.Record(action==0?"feed":action==1?"wash":action==3?"play":"care");if(action==0||action==1||action==3)money+=Progression.AddExperience(2);
        progressionDirty=true;PetTownPetAction.Do(PetRootFor(pet.id),action==0?PetTownPetAction.Eat:action==1||action==2?PetTownPetAction.Groom:PetTownPetAction.Happy,2.4f);
        return action==0?"맛있게 먹었어요!":action==1?"깨끗하게 씻었어요!":action==2?"털을 가지런히 빗어 줬어요!":action==3?"함께 신나게 놀았어요!":action==4?"부드럽게 쓰다듬어 줬어요!":"포근하게 안아 줬어요!";
    }
    List<PetTownPetCareTarget> ProgressionPetTargets()
    {
        var data=Progression;var roots=new Dictionary<string,GameObject>();
        for(int i=0;i<livePets.Count;i++){var live=livePets[i];if(!live.root||live.inventory)continue;bool ferret=live.root.name.Contains("ferret");string id=live.careId??live.root.name+"_"+i;var p=data.care.Get(id,ferret?"전시 페럿":"전시 강아지 "+(i+1));p.displayOnly=true;p.speciesKind=ferret?3:0;p.appearance=i%2;roots[id]=live.root;}
        foreach(var pair in shopPetActors)if(pair.Value.root)roots[pair.Key]=pair.Value.root;
        var targets=new List<PetTownPetCareTarget>();
        foreach(var p in data.care.pets){if(p.speciesKind<0)p.speciesKind=p.id!=null&&p.id.Contains("ferret")?3:0;var portrait=p.speciesKind==0?Resources.Load<Texture2D>("UI/Cozy/Pet_"+p.appearance):p.speciesKind>=3?Resources.Load<Texture2D>("UI/Cozy/Pet_2"):null;var visual=PetVisual(p.speciesKind,p.appearance);if(visual!=null&&visual.portrait)portrait=visual.portrait;var uv=new Rect(0,0,1,1);
            if(!portrait&&PetTownCozyLibrary.Current){portrait=PetTownCozyLibrary.Current.style.iconAtlas;uv=new Rect(.25f,0,.25f,1f/3);}
            roots.TryGetValue(p.id,out var root);targets.Add(new PetTownPetCareTarget{id=p.id,name=p.nickname,species=PetTownProgressionState.Species[p.speciesKind],root=root,portrait=portrait,portraitUv=uv,saleAvailable=p.Adoptable});
        }
        return targets;
    }
    PetTownPetCareContext ProgressionCareContext()=>new PetTownPetCareContext{state=Progression.care,care=ApplyProgressionCare,play=p=>StartPetPlay(p.id),food=FoodDescription,buyFood=BuyCareFood,save=SaveProgressionSilent,slot=ShopSlotName,toggleFavorite=p=>{if(p.displayOnly)return;p.favorite=!p.favorite;progressionDirty=true;SaveProgressionSilent();}};
    int AdoptionBasePrice(int species)=>(species==2?330:240)+Mathf.Min(150,shopLevel*20)+Mathf.Min(100,population*2);
    void OpenAdoptionSelection()
    {
        if(orderPet<0){Message("분양을 기다리는 손님이 없습니다.");return;}SetMode(Mode.Play);EnsureProgression();int species=orderPet,token=adoptionGeneration;var ids=new List<string>();var entries=new List<PetTownCatalogEntry>();
        foreach(var p in progression.care.pets){if(p.displayOnly||p.speciesKind!=species)continue;int key=ids.Count;ids.Add(p.id);string reason=p.favorite?"보호 설정으로 분양할 수 없습니다.":p.growth<1?"성장이 끝나면 분양할 수 있습니다.":"현재 손님에게 분양할 수 있습니다.";
            entries.Add(new PetTownCatalogEntry{Key=key,Name=p.nickname+" · Lv."+p.Level,Category=p.Adoptable?"분양 가능":"돌봄 중",PriceLabel=progression.Quote(p,AdoptionBasePrice(species)).ToString("N0")+" 골드",FootprintLabel="성장 "+Mathf.RoundToInt(p.growth*100)+"% · 컨디션 "+Mathf.RoundToInt(p.Condition)+"%",ActionLabel=p.Adoptable?"분양 확정":"펫 돌보기",Thumbnail=PetVisual(p.speciesKind,p.appearance)?.portrait,Description=reason+"\n가격은 현재 컨디션과 펫 레벨을 반영합니다.\n아기부터 레벨에 따라 자라며 Lv.3부터 분양할 수 있습니다."});
        }
        if(entries.Count==0){Message("요청한 종류의 펫이 없어요. 펫샵 관리에서 입고해 주세요.");return;}
        adoptionSelectionOpen=true;catalogWindow.Show(PetTownProgressionState.Species[species]+" 분양",entries,e=>{adoptionSelectionOpen=false;if(token!=adoptionGeneration||orderPet!=species||Time.time>orderDeadline){Message("손님의 요청이 종료되었습니다.");return;}var p=progression.care.pets.Find(v=>v.id==ids[e.Key]);if(p==null)return;if(!p.Adoptable){OpenPetCare();petCareWindow.SelectPet(p.id);return;}CompleteIndividualAdoption(p.id,species,token);},()=>adoptionSelectionOpen=false,()=>{});
    }
    bool CompleteIndividualAdoption(string id,int species,int token)
    {
        if(loadingTown||expansionPending||token!=adoptionGeneration||orderPet!=species||Time.time>orderDeadline)return false;
        if(!Progression.Adopt(id,species,AdoptionBasePrice(species),day,out int income,out int reward)){Message("이 펫은 지금 분양할 수 없습니다.");return false;}
        money+=income+reward;SyncProgressionStock();sold++;completedOrders++;reputation+=2;EndOrder();SaveProgressionSilent();Message("분양 완료! +"+income+" 골드 · 명성 +2"+(reward>0?" · 레벨 보상 +"+reward:""));return true;
    }
    void EndProgressionOrder(){adoptionGeneration++;if(adoptionSelectionOpen){adoptionSelectionOpen=false;if(catalogWindow)catalogWindow.Hide();}}
    public int TownHappiness
    {
        get{TownHappinessValues(out float facility,out float support,out float crowd);float core=45+Progression.serviceHappiness-crowd,raw=core+facility;float result=raw<=70?raw:core<=70?70+(raw-70)*.2f:core+facility*.2f;return Mathf.Clamp(Mathf.RoundToInt(result),0,100);}
    }
    void TownHappinessValues(out float facility,out float support,out float crowd)
    {
        facility=0;support=16;foreach(var b in buildings){if(!b||b.name.StartsWith("Dark_"))continue;string n=b.name;
            var community=b.GetComponent<PetTownCommunityView>();if(community){var lot=Progression.Community.Get(community.Id,community.Kind,day);if(lot.Usable){facility+=community.Kind==5?10:community.Kind==6?8:12;support+=community.Kind==5?40:30;}continue;}
            if(n.StartsWith("Pet_Cafe")){facility+=6;support+=20;}else if(n.StartsWith("Pet_Hospital")){facility+=6;support+=25;}}
        facility=Mathf.Min(40,facility);crowd=Mathf.Min(30,Mathf.Max(0,population-support)*.8f);
    }
    void OpenTownProgression()
    {
        SetMode(Mode.Play);TownHappinessValues(out float facility,out float support,out float crowd);var entries=new List<PetTownCatalogEntry>{
            new PetTownCatalogEntry{Key=0,TextOnlyDetail=true,IconKey=5,Name="마을 행복도",Category="주민",PriceLabel=TownHappiness+" / 100",FootprintLabel=ResidentHousingSummary(),ActionLabel="확인",Description="기본 45\n시설 +"+facility+"\n서비스 "+Progression.serviceHappiness.ToString("+0.0;-0.0;0")+"\n혼잡 -"+crowd.ToString("0.0")+"\n야생 나무는 조경 점수로 세지 않습니다.\n빈 주택과 행복도에 따라 매일 입주합니다."},
            new PetTownCatalogEntry{Key=1,TextOnlyDetail=true,IconKey=4,Name="사장 성장",Category="성장",PriceLabel="Lv. "+Progression.playerLevel,FootprintLabel=Progression.playerExperience+" / "+Progression.ExperienceNeeded+" XP",ActionLabel="확인",Description="유효한 먹이·목욕·놀이와 분양으로 경험치를 얻습니다.\n레벨이 오르면 새 레벨 × 50 골드를 받습니다.\n캐릭터 꾸미기와 매장 레벨은 그대로 유지합니다."},
            new PetTownCatalogEntry{Key=2,TextOnlyDetail=true,IconKey=9,Name="분양 기록",Category="분양",PriceLabel="누적 "+sold+"마리",FootprintLabel="개체 기록 "+Progression.adoptions.Count+"건",ActionLabel="기록 보기",Description="개체 ID·이름·분양 날짜·컨디션·레벨·수익을 기록합니다. 기존 판매 수는 보존합니다."}};
        entries.Add(new PetTownCatalogEntry{Key=3,TextOnlyDetail=true,IconKey=5,Name="주민 목록",Category="주민",PriceLabel=EnsureResidents().Population+"명",FootprintLabel=ResidentHousingSummary(),ActionLabel="목록 보기",Description="주민별 이름·입주일·행복도·주택 정보를 확인합니다.\n실제 NPC와 같은 주민 목록을 사용합니다."});
        entries.Add(new PetTownCatalogEntry{Key=4,TextOnlyDetail=true,IconKey=0,Name="주택과 정원",Category="주택",PriceLabel=PetTownResidentState.Capacity(ResidentHomes())+"명 정원",FootprintLabel="주민 주택 1채 / 2명",ActionLabel="주택 보기",Description="새 주택에는 대기 주민부터 배정합니다.\n빈집이 있으면 마을 행복도에 따라 다음 날 입주합니다."});
        entries.Add(new PetTownCatalogEntry{Key=5,TextOnlyDetail=true,IconKey=5,Name="이주 기록",Category="주민",PriceLabel="최근 "+EnsureResidents().history.Count+"일",ActionLabel="기록 보기",Description="처음 7일은 이탈 유예 기간입니다.\n그 뒤 행복도35 미만이 3일 연속이면 일부가 이사합니다.\n오프라인 날짜는 소급 적용하지 않습니다."});
        catalogWindow.Show("주민과 마을",entries,e=>{if(e.Key==2)OpenAdoptionHistory();else if(e.Key==3)OpenResidentRoster();else if(e.Key==4)OpenResidentHousing();else if(e.Key==5)OpenResidentHistory();},()=>{},()=>{});
    }
    void OpenAdoptionHistory()
    {
        var entries=new List<PetTownCatalogEntry>();int index=0;foreach(var p in Progression.adoptions)entries.Add(new PetTownCatalogEntry{Key=index++,TextOnlyDetail=true,IconKey=9,Name=p.name,Category=PetTownProgressionState.Species[p.species],PriceLabel=p.income.ToString("N0")+" 골드",FootprintLabel="Day "+p.day+" · Lv."+p.level,ActionLabel="확인",Description="분양 당시 컨디션 "+Mathf.RoundToInt(p.condition)+"%\n"+(string.IsNullOrEmpty(p.adopterName)?"매장 손님에게 분양":"입양 주민: "+p.adopterName)+"\n기록 ID: "+p.petId});
        catalogWindow.Show("분양 기록",entries,e=>{},()=>{},()=>{});
    }
    void OpenProgressionInventory()
    {
        SetMode(Mode.Play);var entries=new List<PetTownCatalogEntry>();for(int i=0;i<4;i++)entries.Add(new PetTownCatalogEntry{Key=i,Name=PetTownProgressionState.FoodNames[i]+" 기본 사료",Category="사료",PriceLabel=Progression.food[i]+"개 보유",FootprintLabel="5개 / "+PetTownProgressionState.FoodPrice[i]*5+" 골드",ActionLabel="5개 구매",Description="먹이 주기마다 1개를 사용합니다.\n기본 사료는 배부름을 55 회복합니다."});
        entries.Add(OperationsEntry(10,"미용 키트",Operations.kits+"개 보유","3개 / 150 골드","3개 구매","미용 한 번에1개를 사용합니다.\n미용 완료 개체의 분양가는20% 높아집니다.",9));
        entries.Add(OperationsEntry(11,"배송함",Operations.deliveries.Count+"건","사료와 키트 배송","배송 보기","주문한 물품을 수령합니다.",2));
        catalogWindow.Show("아이템 가방",entries,e=>{if(e.Key==11){OpenDeliveries();return;}if(e.Key==10)BuyGroomKits();else BuyCareFood(new PetTownPetCareRecord{speciesKind=e.Key});OpenProgressionInventory();},()=>{},()=>{});
    }
}