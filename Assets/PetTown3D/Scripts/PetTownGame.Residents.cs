using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.AI;

// Bridges the saved roster to the existing NPC spawner. No movement/animation logic is replaced.
public partial class PetTownGame
{
    readonly Dictionary<string,GameObject> residentActors=new Dictionary<string,GameObject>();
    float nextResidentSpawn;
    List<PetTownResidentHome> ResidentHomes()
    {
        var homes=new List<PetTownResidentHome>();
        foreach(var b in buildings)
        {
            if(!b||!b.name.StartsWith("Resident_House",StringComparison.Ordinal))continue;
            var metadata=b.GetComponent<PetTownBuildingPlacement>();var p=b.transform.position;
            string id=metadata!=null&&metadata.Record!=null?metadata.Record.id:null;
            if(string.IsNullOrEmpty(id))id="legacy-house:"+p.x.ToString("F3",CultureInfo.InvariantCulture)+":"+p.z.ToString("F3",CultureInfo.InvariantCulture);
            Vector3 forward=metadata!=null&&metadata.Record!=null?metadata.EntranceForward:b.transform.forward*-1;
            Vector3 entrance=metadata!=null&&metadata.Record!=null?metadata.EntranceWorld:p+forward*3.5f;
            homes.Add(new PetTownResidentHome{id=id,label="주민 주택 "+(homes.Count+1),capacity=2,entrance=entrance,forward=forward});
        }
        return homes;
    }
    PetTownResidentState EnsureResidents()
    {
        var data=Progression;
        if(data.residentVersion<1||data.residents==null)
        {
            data.residents=new PetTownResidentState();data.residents.Migrate(population,day,TownHappiness,ResidentHomes());data.residentVersion=1;progressionDirty=true;
        }
        return data.residents;
    }
    void ResidentsWorldReady()
    {
        var state=EnsureResidents();state.Rehouse(ResidentHomes());population=state.Population;
        // Retain any existing actors when this extension is first attached to a running world.
        for(int i=0;i<Mathf.Min(villagers.Count,state.people.Count);i++)if(villagers[i])residentActors[state.people[i].id]=villagers[i];
    }
    void ResidentHousingChanged()
    {
        var state=EnsureResidents();state.Rehouse(ResidentHomes());population=state.Population;progressionDirty=true;
    }
    void ResidentsNewDay()
    {
        if(!Application.isFocused)return;
        var state=EnsureResidents();var change=state.AdvanceDay(day,TownHappiness,ResidentHomes());population=state.Population;
        if(change!=null){PetTownResidentProfile.Daily(state);progressionDirty=true;if(change.moved>0||change.left>0)Message("오늘 주민 입주 "+change.moved+"명 · 이사 "+change.left+"명");}
    }
    bool ResidentSpawnPoint(PetTownResidentRecord person,List<PetTownResidentHome> homes,out Vector3 point)
    {
        var home=homes.Find(h=>h.id==person.homeId);Vector3 forward=home!=null?home.forward:Vector3.back;
        Vector3 origin=home!=null?home.entrance:shopPosition+Vector3.right*10;
        for(int i=0;i<16;i++)
        {
            float angle=(person.appearance*73+i*137)*Mathf.Deg2Rad;
            Vector3 candidate=origin+forward*1.2f+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(1+i*.55f);candidate.y=Ground(candidate.x,candidate.z);
            NavMeshHit hit;if(!NavMesh.SamplePosition(candidate,out hit,1.5f,NavMesh.AllAreas)||IsWater(hit.position.x,hit.position.z))continue;
            if(Physics.CheckCapsule(hit.position+Vector3.up*.55f,hit.position+Vector3.up*1.35f,.35f,~(1<<TerrainLayer),QueryTriggerInteraction.Ignore))continue;
            bool crowded=false;foreach(var actor in residentActors.Values)if(actor&&(actor.transform.position-hit.position).sqrMagnitude<1.2f){crowded=true;break;}
            if(crowded)continue;point=hit.position;return true;
        }
        point=origin;return false;
    }
    void RemoveResidentActor(GameObject actor)
    {
        if(!actor)return;villagers.Remove(actor);villagerWander.Remove(actor);villagerAnimInfo.Remove(actor);actor.SetActive(false);Destroy(actor);
    }
    void UpdateResidentActors()
    {
        if(loadingTown||expansionPending||!shop||!cam)return;
        var state=EnsureResidents();population=state.Population;
        var removed=new List<string>();foreach(var pair in residentActors)if(!pair.Value||!state.people.Exists(p=>p.id==pair.Key)){RemoveResidentActor(pair.Value);removed.Add(pair.Key);}
        foreach(var id in removed)residentActors.Remove(id);
        if(Time.unscaledTime<nextResidentSpawn||residentActors.Count>=state.Population)return;
        nextResidentSpawn=Time.unscaledTime+.15f;var homes=ResidentHomes();
        foreach(var person in state.people)
        {
            if(residentActors.ContainsKey(person.id))continue;
            Vector3 point;if(!ResidentSpawnPoint(person,homes,out point))continue;
            int before=villagers.Count;SpawnVillager(point,person.appearance);
            if(villagers.Count>before)residentActors[person.id]=villagers[villagers.Count-1];
            break;
        }
    }
    string ResidentHousingSummary()
    {
        var state=EnsureResidents();var homes=ResidentHomes();int waiting=state.people.FindAll(p=>string.IsNullOrEmpty(p.homeId)).Count;
        return "주민 "+state.Population+"명 / 주택 정원 "+PetTownResidentState.Capacity(homes)+"명"+(waiting>0?" · 주택 대기 "+waiting+"명":"");
    }
    void OpenResidentRoster()
    {
        SetMode(Mode.Play);var state=EnsureResidents();var homes=ResidentHomes();var entries=new List<PetTownCatalogEntry>();var ids=new List<string>();
        for(int i=0;i<state.people.Count;i++)
        {
            var p=state.people[i];ids.Add(p.id);var home=homes.Find(h=>h.id==p.homeId);residentActors.TryGetValue(p.id,out var actor);
            var agent=actor?actor.GetComponent<NavMeshAgent>():null;
            entries.Add(new PetTownCatalogEntry{Key=i,IconKey=5,TextOnlyDetail=true,Name=p.name,Category="주민",PriceLabel="행복도 "+Mathf.RoundToInt(p.happiness)+"%",FootprintLabel=home!=null?home.label:"주택 배정 대기",ActionLabel=p.pets.Count>0?"함께 사는 펫":"마을 요약",Description="입주: Day "+p.joinedDay+"\n"+(home!=null?"실제 주택에 거주합니다.":"아직 살 집을 찾고 있어요.\n빈 주택이 생기면 입주합니다.")+"\n"+(agent&&agent.isOnNavMesh?"마을에서 생활 중":"외출 준비 중")+"\n함께 사는 펫: "+p.pets.Count+"마리\n마을 시설과 서비스가 좋아지면\n행복도도 조금씩 올라갑니다."});
        }
        catalogWindow.Show("마을 주민",entries,e=>{var person=state.people.Find(p=>p.id==ids[e.Key]);if(person==null){OpenResidentRoster();return;}if(person.pets.Count>0)OpenResidentPets(person.id);else OpenTownProgression();},()=>{},()=>{});
    }
    void OpenResidentPets(string ownerId)
    {
        SetMode(Mode.Play);var owner=EnsureResidents().people.Find(p=>p.id==ownerId);if(owner==null){OpenResidentRoster();return;}
        var entries=new List<PetTownCatalogEntry>();var petIds=new List<string>();int i=0;
        foreach(var p in owner.pets)
        {
            petIds.Add(p.id);var preview=p.species==0?Resources.Load<Texture2D>("UI/Cozy/Pet_"+p.appearance):null;
            entries.Add(new PetTownCatalogEntry{Key=i++,IconKey=9,Thumbnail=preview,TextOnlyDetail=preview==null,Name=p.name,Category=PetTownProgressionState.Species[Mathf.Clamp(p.species,0,PetTownProgressionState.Species.Length-1)],PriceLabel="Lv."+p.level,FootprintLabel="Day "+p.adoptedDay+" 분양",ActionLabel="산책·펫 정보",Description=owner.name+"과 함께 사는 펫입니다.\n분양받은 날: Day "+p.adoptedDay+"\n이름: "+p.name+" · 레벨 "+p.level});
        }
        catalogWindow.Show(owner.name+"의 펫",entries,e=>OpenResidentPetActions(ownerId,petIds[e.Key]),()=>{},()=>{});
    }
    void OpenResidentHousing()
    {
        SetMode(Mode.Play);var state=EnsureResidents();var entries=new List<PetTownCatalogEntry>();int i=0;
        foreach(var home in ResidentHomes())entries.Add(new PetTownCatalogEntry{Key=i++,IconKey=0,TextOnlyDetail=true,Name=home.label,Category="주택",PriceLabel=state.Occupants(home.id)+" / "+home.capacity+"명",FootprintLabel="주민 주택 · 2명 정원",ActionLabel="주민 목록",Description="건물을 지은 즉시 주민이 생기지 않습니다.\n빈집과 행복도에 따라 새날에 입주합니다.\n주택 한 채에 최대 2명이 거주합니다."});
        if(entries.Count==0)entries.Add(new PetTownCatalogEntry{Key=0,IconKey=0,TextOnlyDetail=true,Name="주민 주택이 없습니다",Category="주택",PriceLabel="정원 0명",FootprintLabel=ResidentHousingSummary(),ActionLabel="주민 목록",Description="건설 목록에서 주민 주택을 설치해 주세요.\n지금은 주택 배정을 기다리고 있습니다.\n새 주택에는 대기 주민부터 배정됩니다."});
        catalogWindow.Show("주택과 정원",entries,e=>OpenResidentRoster(),()=>{},()=>{});
    }
    void OpenResidentHistory()
    {
        SetMode(Mode.Play);var entries=new List<PetTownCatalogEntry>();int i=0;
        foreach(var change in EnsureResidents().history)entries.Add(new PetTownCatalogEntry{Key=i++,IconKey=5,TextOnlyDetail=true,Name="Day "+change.day,Category="이주 기록",PriceLabel="입주 +"+change.moved+"명 · 이사 -"+change.left+"명",FootprintLabel="마을 행복도 "+change.happiness+"%",ActionLabel="마을 요약",Description="행복도와 빈 주택을 기준으로 계산했습니다.\n처음 7일은 이탈 유예 기간입니다.\n이후 행복도 35 미만이 3일 연속이면 일부가 이사합니다.\n오프라인 날짜는 소급 계산하지 않습니다."});
        if(entries.Count==0)entries.Add(new PetTownCatalogEntry{Key=0,IconKey=5,TextOnlyDetail=true,Name="아직 이주 기록이 없습니다",Category="이주 기록",PriceLabel="새날에 갱신",ActionLabel="마을 요약",Description="현재 날짜부터 기록을 시작합니다.\n과거 날짜의 입주·이탈은 소급 적용하지 않습니다."});
        catalogWindow.Show("주민 이주 기록",entries,e=>OpenTownProgression(),()=>{},()=>{});
    }
    void TryProgressionVisitorAdoption(GameObject visitor)
    {
        if(loadingTown||expansionPending||!visitor)return;
        string residentId=null;foreach(var pair in residentActors)if(pair.Value==visitor){residentId=pair.Key;break;}
        var owner=EnsureResidents().people.Find(p=>p.id==residentId);
        if(owner==null||owner.pets.Count>=PetTownResidentState.PetLimit||UnityEngine.Random.value>.5f)return;
        var eligible=Progression.care.pets.FindAll(p=>p.Adoptable&&p.speciesKind>=0&&p.speciesKind<=2);
        if(eligible.Count==0)return;var pet=eligible[UnityEngine.Random.Range(0,eligible.Count)];
        int basePrice=(pet.speciesKind==2?200:150)+Mathf.Min(80,shopLevel*12);
        if(!Progression.AdoptToResident(pet.id,owner.id,basePrice,day,out int income,out int reward))return;
        money+=income+reward;sold++;reputation++;SyncProgressionStock();progressionDirty=true;SaveProgressionSilent();
        Message(owner.name+"이 "+pet.nickname+"을 분양받았어요! +"+income+" 골드");
    }
}
