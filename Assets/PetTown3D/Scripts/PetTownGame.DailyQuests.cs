using System.Collections.Generic;
using UnityEngine;

public partial class PetTownGame
{
    void EnsureDailyBoard(PetTownShopOperationsState state)
    {
        if(loadingTown||!shop)return;
        if(state.dailyQuests==null)state.dailyQuests=new PetTownDailyQuestState();
        if(state.dailyQuests.lastDay>=day)return;
        var eligible=new List<string>{"feed","wash","play","adopt","delivery"};
        if((state.kits>0||money>=150)&&Progression.care.pets.Exists(p=>!p.groomed&&p.growth>=1)&&
            !string.IsNullOrEmpty(state.groomingStationId)&&GroomingTables().Exists(f=>f.Capture().id==state.groomingStationId))eligible.Add("groom");
        if(FarmRoots().Count>0)eligible.Add("harvest");
        if(Progression.farm?.poultry!=null&&FarmRoots().Exists(f=>{var coop=Progression.farm.poultry.Find(f.Id);return coop!=null&&coop.birds.Exists(b=>b.Adult&&b.Lays);}))eligible.Add("eggs");
        if(Progression.ranch!=null)foreach(var ranch in RanchRoots()){var lot=Progression.ranch.lots.Find(x=>x.id==ranch.Id);if(lot==null)continue;if(!eligible.Contains("milk")&&lot.animals.Exists(a=>a.species==0&&a.Adult))eligible.Add("milk");if(!eligible.Contains("meat")&&lot.animals.Exists(a=>a.species==2&&a.Adult))eligible.Add("meat");if(!eligible.Contains("wool")&&lot.animals.Exists(a=>a.species==1&&a.Adult&&(a.ready>0||a.productProgress>=1)))eligible.Add("wool");}
        bool hasPet=Progression.residents!=null&&Progression.residents.people.Exists(r=>r.pets!=null&&r.pets.Count>0);
        if(hasPet)foreach(var b in buildings)
        {
            var room=b?b.GetComponent<PetTownInteriorState>():null;
            if(room&&room.Kind==3&&!eligible.Contains("cafe"))eligible.Add("cafe");
            if(room&&room.Kind==4&&!eligible.Contains("clinic"))eligible.Add("clinic");
        }
        if(CommunityRoots().Exists(v=>v.Kind==6))eligible.Add("fish");
        // 병아리 의뢰는 곧 부화할 알(남은 1일 이하)이 있을 때만 나옵니다: 부화에는 2일이 걸려 의뢰 기간 안에 달성할 수 있어야 합니다.
        if(Progression.farm?.poultry!=null&&FarmRoots().Exists(f=>{var coop=Progression.farm.poultry.Find(f.Id);return coop!=null&&coop.birds.Count<PetTownPoultryState.Capacity&&coop.clutches!=null&&coop.clutches.Exists(c=>c.remainingDays<=1);}))eligible.Add("chick");
        if(ShopSlotFurniture().Count>0&&Progression.care.pets.Exists(p=>!p.displayOnly))eligible.Add("slot");
        if(Progression.Community.lots.Exists(l=>l.kind==7&&l.AnimalCount>0))eligible.Add("zoo");
        if(Progression.care.pets.Exists(a=>!a.displayOnly&&a.sex==1&&a.growth>=1&&Progression.care.pets.Exists(b=>!b.displayOnly&&b.sex==2&&b.speciesKind==a.speciesKind&&b.growth>=1)))eligible.Add("born");
        if(state.dailyQuests.EnsureDay(day,Progression.playerLevel,eligible,state.Activity))progressionDirty=true;
    }
    bool ClaimDailyQuest(string id)
    {
        if(!OperationsReady())return false;var state=Operations;
        if(state.dailyQuests==null||!state.dailyQuests.Claim(id,day,state.Activity,out int gold,out int xp,out int bonus))return false;
        money+=gold+bonus+Progression.AddExperience(xp);progressionDirty=true;SaveProgressionSilent();
        Message("의뢰 완료! +"+gold+" 골드 · XP +"+xp+(bonus>0?" · 다섯 의뢰 완료 보너스 +"+bonus+" 골드":""));return true;
    }
    void OpenDailyQuests()
    {
        SetMode(Mode.Play);var state=Operations;var board=state.dailyQuests;if(board==null)return;
        var ids=new List<string>();var entries=new List<PetTownCatalogEntry>();
        for(int i=board.batches.Count-1;i>=0;i--)
        {
            var batch=board.batches[i];
            foreach(var q in batch.quests)
            {
                if(day>batch.day+1)continue;int key=ids.Count;ids.Add(q.id);var def=PetTownDailyQuestDefinition.Find(q.key);int progress=q.Progress(state.Activity);
                var entry=OperationsEntry(key,def.name,q.claimed?"보상 수령 완료":progress+" / "+q.target,
                    q.gold+" 골드 · XP "+q.xp,q.claimed?"완료 확인":progress>=q.target?"보상 받기":"진행 확인",
                    "Day "+batch.day+"에 올라온 의뢰입니다.\nDay "+(batch.day+1)+"이 끝나기 전까지 완료·수령할 수 있어요.\n게시 이후의 활동만 셉니다.\n같은 날의 다섯 의뢰를 모두 수령하면 "+batch.bonusGold+" 골드를 추가로 받습니다."+(batch.bonusClaimed?"\n완료 보너스 수령됨":""),5);
                entry.IconKey=q.key=="delivery"?2:q.key=="cafe"||q.key=="clinic"?3:9;
                entry.Category="Day "+batch.day;
                entry.PriceProvider=()=>day>batch.day+1?"마감된 의뢰":q.claimed?"보상 수령 완료":q.Progress(state.Activity)+" / "+q.target;
                entry.ActionProvider=()=>day>batch.day+1?"마감 확인":q.claimed?"완료 확인":q.Progress(state.Activity)>=q.target?"보상 받기":"진행 확인";
                entries.Add(entry);
            }
        }
        var news=OperationsEntry(-3,"마을 소식","Day "+day+" · "+TimeWeather.ClockText+" · "+TimeWeather.WeatherName,"행복도 "+TownHappiness+" · 주민 "+population+"명 · 오물 "+petMess.Count+"개","확인","오늘의 마을 소식입니다.\n날씨: "+TimeWeather.WeatherName+"\n마을 행복도: "+TownHappiness+" / 100\n주민: "+population+"명\n펫샵 오물: "+petMess.Count+"개 (3개 이상이면 서비스 점수가 내려가요)\n오물은 가까이 가면 치우고 청소 직원도 치워 줘요.",5);news.Category="마을 소식";entries.Insert(0,news);AddVillageRequestEntries(entries);
        var unlock=OperationsEntry(-2,"레벨 해금표","내 레벨 Lv."+Progression.playerLevel,"무엇이 언제 열리는지 확인","해금표 보기","카페 메뉴, 병원 진료, 동물원 사육사, 자동 교배, 밭 확장이 열리는 레벨을 보여줍니다.",5);unlock.Category="마을 안내";entries.Add(unlock);var guide=OperationsEntry(-1,"마을 안내 퀘스트","처음 배우는 마을 운영","기존 7개 안내 과제","안내 보기","하루 의뢰와 별개인 일회성 안내 보상을 확인합니다.",5);guide.Category="마을 안내";entries.Add(guide);
        catalogWindow.Show("퀘스트 게시판",entries,e=>
        {
            if(e.Key==-1){OpenJournalQuests();return;}if(e.Key==-2){OpenUnlockTable();return;}
            if(e.Key>=1000){DeliverVillageRequest(e.Key-1000);OpenDailyQuests();return;}
            if(e.Key<0||e.Key>=ids.Count)return;
            if(!ClaimDailyQuest(ids[e.Key]))Message("목표 진행도와 수령 여부를 확인해 주세요. 만료된 의뢰는 종료됩니다.");
            OpenDailyQuests();
        },()=>{},()=>{});
    }
}
