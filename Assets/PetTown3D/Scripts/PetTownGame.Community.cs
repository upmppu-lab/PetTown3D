using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Uses the existing facility interception and release points; does not replace NPC wander/animation.
public partial class PetTownGame
{
    readonly Dictionary<string,FacilityActor> communityActors=new Dictionary<string,FacilityActor>();float nextCommunityGuest;
    void CreateCommunityBuilding(GameObject root,Vector3 point,int kind)
    {
        float half=kind==7?11:7.5f;FlattenBuildingFootprint(point,half,half,point.y);ClearBuildingFootprint(point,half,half);
        buildingFloors.Add(new BuildingFloor{owner=root,center=point,halfW=half,halfD=half,floorY=point.y+.045f});
        var record=activeBuildingPlacement;if(record==null){var def=PetTownBuildingDefinition.Default(kind);record=new PetTownBuildingSaveRecord{id=Guid.NewGuid().ToString("N"),kind=kind,groundY=point.y,footprintMeters=def.footprintMeters,entranceLocal=def.entranceLocal,facingQuarter=2};root.AddComponent<PetTownBuildingPlacement>().Initialize(record);}else if(kind==6)record.hasEntrance=true;
        Progression.Community.Get(record.id,kind,day);root.AddComponent<PetTownCommunityView>().Initialize(this,record.id,kind,record.entranceLocal);
    }
    List<PetTownCommunityView> CommunityRoots(){var roots=new List<PetTownCommunityView>();foreach(var b in buildings){var view=b?b.GetComponent<PetTownCommunityView>():null;if(view&&b.activeInHierarchy)roots.Add(view);}return roots;}
    PetTownCommunityView CommunityRoot(string id){foreach(var view in CommunityRoots())if(view.Id==id)return view;return null;}
    void TickCommunity(float seconds)
    {
        if(loadingTown||expansionPending||!shop||seconds<=0||Progression.community==null)return;var state=Progression.Community;
        for(int i=state.visits.Count-1;i>=0;i--){var v=state.visits[i];var lot=state.lots.Find(x=>x.id==v.buildingId);if(!CommunityRoot(v.buildingId)||lot==null||!lot.Usable||!EnsureResidents().people.Exists(x=>x.id==v.residentId))CancelCommunityVisit(v);}
        if(Time.unscaledTime<nextCommunityGuest)return;nextCommunityGuest=Time.unscaledTime+15;
        foreach(var view in CommunityRoots()){var lot=state.Get(view.Id,view.Kind,day);if(!lot.automatic||!lot.Usable)continue;foreach(var person in EnsureResidents().people){string reason;if(TryCommunityVisit(view,person.id,out reason))break;}}
    }
    bool CommunityPoint(PetTownCommunityView view,int stage,int slot,NavMeshAgent agent,out Vector3 point)
    {
        point=Vector3.zero;var local=stage==0||stage==3?view.EntranceLocal+Vector3.back*1.25f:view.VisitLocal(slot);var origin=view.transform.TransformPoint(local);origin.y=Ground(origin.x,origin.z);
        for(int i=0;i<9;i++){var candidate=origin+(i==0?Vector3.zero:new Vector3(Mathf.Cos(i*Mathf.PI/4),0,Mathf.Sin(i*Mathf.PI/4))*.3f);NavMeshHit hit;if(!NavMesh.SamplePosition(candidate,out hit,.45f,agent.areaMask)||Mathf.Abs(hit.position.y-origin.y)>.65f)continue;var path=new NavMeshPath();if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete||!CompanionDryPath(path))continue;point=hit.position;return true;}return false;
    }
    bool TryCommunityVisit(PetTownCommunityView view,string residentId,out string reason)
    {
        reason="";if(loadingTown||expansionPending||!view){reason="시설을 준비해 주세요.";return false;}var state=Progression.Community;var lot=state.Get(view.Id,view.Kind,day);if(!lot.Usable){reason="개방 상태와 동물의 먹이·청결을 확인해 주세요.";return false;}
        if(lot.rewarded.Exists(x=>x.residentId==residentId&&x.day==day)){reason="이 주민은 오늘 이미 이용했어요.";return false;}if(state.visits.Exists(x=>x.residentId==residentId)||Progression.Facilities.visits.Exists(x=>x.residentId==residentId)){reason="현재 다른 시설을 이용하고 있어요.";return false;}
        var person=EnsureResidents().people.Find(x=>x.id==residentId);GameObject actor;if(person==null||!residentActors.TryGetValue(residentId,out actor)||!actor){reason="주민이 외출 준비 중이에요.";return false;}var agent=actor.GetComponent<NavMeshAgent>();if(!agent||!agent.isOnNavMesh){reason="주민의 이동 경로를 확인해 주세요.";return false;}
        int slot=-1;var freeSlots=new List<int>();for(int i=0;i<view.SlotCount;i++)if(!state.visits.Exists(x=>x.buildingId==view.Id&&x.slot==i))freeSlots.Add(i);if(freeSlots.Count>0)slot=view.Kind==5?freeSlots[UnityEngine.Random.Range(0,freeSlots.Count)]:freeSlots[0];if(slot<0){reason="현재 네 자리를 모두 이용하고 있어요.";return false;}Vector3 entrance,goal;if(!CommunityPoint(view,0,slot,agent,out entrance)||!CommunityPoint(view,1,slot,agent,out goal)){reason="입구와 휴식 자리까지의 통로를 확보해 주세요.";return false;}
        state.visits.Add(new PetTownCommunityVisit{id=Guid.NewGuid().ToString("N"),buildingId=view.Id,residentId=residentId,slot=slot});progressionDirty=true;SaveProgressionSilent();return true;
    }
    bool UpdateCommunityVisit(GameObject actor,NavMeshAgent agent,float seconds)
    {
        if(progression==null||progression.community==null||progression.community.visits.Count==0)return false;string personId=null;foreach(var p in residentActors)if(p.Value==actor){personId=p.Key;break;}if(personId==null)return false;var state=Progression.Community;var v=state.visits.Find(x=>x.residentId==personId);if(v==null)return false;
        var view=CommunityRoot(v.buildingId);var lot=state.lots.Find(x=>x.id==v.buildingId);if(!view||lot==null||!lot.Usable){CancelCommunityVisit(v);return false;}
        FacilityActor run;if(!communityActors.TryGetValue(v.id,out run)){if(!agent||!agent.isOnNavMesh)return true;run=new FacilityActor{root=actor,agent=agent,wasStopped=agent.isStopped,originalSpeed=agent.speed,previous=actor.transform.position};communityActors[v.id]=run;}
        if(view.Kind==6&&v.slot<PetTownCommunityState.FishingSlots){if(v.stage==2)PetTownFishingRod.Hold(actor,PetTownCommunityLibrary.Current,view,v.slot,v.rest);else PetTownFishingRod.Drop(actor);}
        if(view.Kind==5){if(v.stage==2)PetTownPlaygroundPlay.Play(actor,view,v.slot,v.rest);else PetTownPlaygroundPlay.Drop(actor);}
        else if(view.Kind==6&&v.slot==2){if(v.stage==2)PetTownStoneThrow.Hold(actor);else PetTownStoneThrow.Drop(actor);}
        else if(!(view.Kind==6&&v.slot<PetTownCommunityState.FishingSlots)){if(v.stage==2)PetTownSeatPose.Seat(actor);else PetTownSeatPose.Stand(actor);}
        if(!Application.isFocused||seconds<=0||loadingTown||expansionPending){if(agent&&agent.isOnNavMesh)agent.isStopped=true;return true;}seconds=Mathf.Min(seconds,.25f);v.elapsed+=seconds;if(v.elapsed>120||!agent||!agent.isOnNavMesh){CancelCommunityVisit(v);return false;}
        float moved=Vector3.Distance(actor.transform.position,run.previous);run.previous=actor.transform.position;UpdateVillagerAnim(actor,moved/Mathf.Max(.0001f,seconds)>.1f,false);
        if(!run.valid||run.revision!=constructionWorldRevision||run.stage!=(PetTownFacilityStage)v.stage){Vector3 goal;if(!CommunityPoint(view,v.stage,v.slot,agent,out goal)){CancelCommunityVisit(v);return false;}run.goal=goal;run.valid=true;run.stage=(PetTownFacilityStage)v.stage;run.revision=constructionWorldRevision;agent.isStopped=false;agent.speed=1.15f;if(!agent.SetDestination(goal)){CancelCommunityVisit(v);return false;}}
        bool arrived=!agent.pathPending&&Vector3.Distance(actor.transform.position,run.goal)<=.65f&&(!agent.hasPath||agent.remainingDistance<=.75f);
        if(!arrived){agent.isStopped=false;return true;}if(v.stage==0){v.stage=1;v.elapsed=0;run.valid=false;}
        else if(v.stage==1){v.stage=2;v.elapsed=0;agent.ResetPath();agent.isStopped=true;}
        else if(v.stage==2){agent.isStopped=true;v.rest+=seconds;if(v.rest>=15){var person=EnsureResidents().people.Find(x=>x.id==personId);int fishBefore=state.fish;if(state.Complete(v,person,day)){progressionDirty=true;if(state.fish>fishBefore)Operations.Record("fish",state.fish-fishBefore);}if(v.stage==3){run.valid=false;agent.isStopped=false;}}}
        else CancelCommunityVisit(v);return true;
    }
    void CancelCommunityVisit(PetTownCommunityVisit v){Progression.Community.visits.Remove(v);FacilityActor run;if(communityActors.TryGetValue(v.id,out run)){ReleaseFacilityActor(run);communityActors.Remove(v.id);}progressionDirty=true;}
    void ReleaseCommunityActors(){foreach(var run in communityActors.Values)ReleaseFacilityActor(run);communityActors.Clear();}
    void OpenCommunityFacilities()
    {
        SetMode(Mode.Play);var roots=CommunityRoots();var entries=new List<PetTownCatalogEntry>();for(int i=0;i<roots.Count;i++){var view=roots[i];var lot=Progression.Community.Get(view.Id,view.Kind,day);var e=OperationsEntry(i,PetTownCommunityState.Name(view.Kind)+" "+(i+1),lot.Usable?"이용 가능":"준비 중","누적 방문 "+lot.totalVisits+"명","관리하기","주민이 입구를 지나 실제로15초간 쉬거나 관람한 뒤 행복도가 올라갑니다.",3);e.Thumbnail=PetTownCommunityLibrary.Current?.thumbnails[view.Kind-5];entries.Add(e);}entries.Add(OperationsEntry(-1,"편의시설 짓기","공원·호수·동물원","입구 방향을 정해 배치","건설 목록","건설한 시설은 이 목록에서 관리합니다.",0));catalogWindow.Show("공원·호수·동물원",entries,e=>{if(e.Key<0){SetMode(Mode.Build);OpenConstructionCatalog();}else if(roots[e.Key])OpenCommunityLot(roots[e.Key].Id);},()=>{},()=>{});
    }
    void OpenCommunityLot(string id)
    {
        var view=CommunityRoot(id);if(!view){OpenCommunityFacilities();return;}var lot=Progression.Community.Get(id,view.Kind,day);var state=Progression.Community;var entries=new List<PetTownCatalogEntry>{OperationsEntry(0,lot.open?"시설 닫기":"시설 열기",lot.open?"주민에게 개방 중":"신규 방문 중지","무료로 이용하는 편의시설","전환하기","입구와 휴식 자리가 실제로 연결되어 있어야 이용합니다.",3),OperationsEntry(1,lot.automatic?"자동 방문 끄기":"자동 방문 켜기","15초마다 주민1명 안내","동시4명 · 주민당 하루1회","전환하기","이미 다른 시설을 이용 중인 주민은 중복 안내하지 않습니다.",5),OperationsEntry(2,"주민 안내","현재 "+state.visits.FindAll(x=>x.buildingId==id).Count+"명 방문 중","직접 방문할 주민 선택","주민 선택","실제 도착과15초 이용을 확인한 뒤 공원·호수 +2, 동물원 +3 행복도를 받습니다.",5)};
        if(view.Kind==7){for(int i=0;i<PetTownCommunityState.AnimalNames.Length;i++)entries.Add(OperationsEntry(10+i,PetTownCommunityState.AnimalNames[i]+" 맞이하기",lot.animals[i]+" / "+PetTownCommunityState.Limit(lot,i)+"마리",PetTownCommunityState.AnimalPrices[i]+" 골드 / 마리","맞이하기","우리별 정원 안에서 실제 동물 모델을 배치합니다.",9));entries.Add(OperationsEntry(20,"동물 먹이 구매",state.food+"개 보유","10개 /100 골드","10개 구매","동물 전체 먹이주기에 마리당1개 사용합니다.",2));entries.Add(OperationsEntry(21,"동물 먹이주기","배부름 "+Mathf.RoundToInt(lot.fullness)+"%","동물 "+lot.AnimalCount+"마리 · 먹이 "+state.food+"개","먹이주기","배부름이90% 미만일 때 먹일 수 있어요. 매일18% 줄어듭니다.35% 미만이면 관람을 쉽니다.",9));entries.Add(OperationsEntry(23,"우리 넓히기","확장 "+lot.expansion+" / "+PetTownCommunityState.MaxExpansion,lot.expansion>=PetTownCommunityState.MaxExpansion?"최대 확장":PetTownCommunityState.ExpandPrice(lot.expansion)+" 골드","넓히기","모든 동물 우리의 정원이 1마리씩 늘어납니다. 새 자리는 우리 안쪽에 배치됩니다.",9));entries.Add(OperationsEntry(24,"전담 사육사",lot.keeper?"배치됨":"플레이어 Lv."+PetTownCommunityState.KeeperLevel+" · "+PetTownCommunityState.KeeperCost+" 골드","매일 먹이 재고로 급식하고 청소",lot.keeper?"근무 중":"고용","사육사가 매일 아침 보유 먹이(마리당 1개)로 먹이를 주고 우리를 청소합니다. 먹이가 부족하면 급식은 건너뜁니다.",5));entries.Add(OperationsEntry(25,"동물 건강·번식",lot.sick?"아픈 동물이 있어요 · 관람 중지":"모두 건강해요",lot.sick?PetTownCommunityState.TreatCost+" 골드로 진료":"새끼 누적 "+lot.breedBorn+"마리",lot.sick?"진료하기":"상태 확인","먹이·청결이 부족한 날이 이어지면 아플 수 있어요. 건강하고(배부름·청결 60%↑) 같은 종이 2마리 이상이며 정원에 여유가 있으면 "+PetTownCommunityState.BreedDaysNeeded+"일마다 새끼가 태어납니다.",9));entries.Add(OperationsEntry(22,"우리 청소","청결 "+Mathf.RoundToInt(lot.cleanliness)+"%","매일12% 감소 · 청소 비용 없음","청소하기","청결이90% 미만일 때 청소합니다.35% 미만이면 관람을 쉽니다.",1));}
        if(view.Kind==6){entries.Add(OperationsEntry(40,"호수 물고기 "+lot.fishStock+" / "+PetTownCommunityState.FishCap,"보관 물고기 "+state.fish+"마리","낚시 자리 2곳 · 매일 +"+PetTownCommunityState.FishRegenPerDay,"모두 판매","주민이 낚시 자리에서 이용을 마치면 물고기 1마리를 얻고 행복도 +3을 받아요. 1마리 "+PetTownCommunityState.FishPrice+" 골드에 판매합니다.",3));entries.Add(OperationsEntry(41,"물고기로 카페 재료 만들기","보관 물고기 "+state.fish+"마리","물고기 2마리 → 카페 재료 2개","재료 만들기","실제 카페를 보유한 경우에만 만들 수 있어요.",2));}
        entries.Add(OperationsEntry(30,"편의시설 목록","누적 방문 "+lot.totalVisits+"명","다른 시설 관리","목록 보기","공원·호수·동물원 목록으로 돌아갑니다.",3));catalogWindow.Show(PetTownCommunityState.Name(view.Kind)+" 관리",entries,e=>{if(!CommunityRoot(id)||!OperationsReady())return;if(e.Key==30){OpenCommunityFacilities();return;}if(e.Key==2){OpenCommunityGuests(id);return;}bool done=true;if(e.Key==0)lot.open=!lot.open;else if(e.Key==1)lot.automatic=!lot.automatic;else if(e.Key>=10&&e.Key<10+PetTownCommunityState.AnimalNames.Length)done=state.BuyAnimal(lot,e.Key-10,ref money);else if(e.Key==20)done=state.BuyFood(ref money);else if(e.Key==21)done=state.Feed(lot);else if(e.Key==22)done=state.Clean(lot);else if(e.Key==23)done=state.ExpandZoo(lot,ref money);else if(e.Key==24)done=state.HireKeeper(lot,Progression.playerLevel,ref money);else if(e.Key==25)done=state.TreatZoo(lot,ref money);else if(e.Key==40)done=state.SellFish(state.fish,ref money);else if(e.Key==41)done=buildings.Exists(b=>b&&b.GetComponent<PetTownInteriorState>()&&b.GetComponent<PetTownInteriorState>().Kind==3)&&state.FishToIngredients(Progression.Facilities);if(done&&(e.Key==21||e.Key==22||e.Key==23||e.Key==25))Operations.Record("zoo");if(done){state.revision++;progressionDirty=true;SaveProgressionSilent();view.RefreshAnimals();}else Message("골드·정원·보유 먹이와 현재 상태를 확인해 주세요.");OpenCommunityLot(id);},()=>{},()=>{});
    }
    void OpenCommunityGuests(string id)
    {
        var view=CommunityRoot(id);if(!view){OpenCommunityFacilities();return;}var people=new List<PetTownResidentRecord>(EnsureResidents().people);var entries=new List<PetTownCatalogEntry>();for(int i=0;i<people.Count;i++)entries.Add(OperationsEntry(i,people[i].name,"행복도 "+Mathf.RoundToInt(people[i].happiness)+"%","입구와 휴식 자리까지 경로 검사","시설로 안내","오늘 이미 이용했거나 다른 시설을 방문 중이면 다시 안내하지 않습니다.",5));entries.Add(OperationsEntry(-1,"시설 관리로 돌아가기","","개방·방문·동물 관리","뒤로","시설 관리로 돌아갑니다.",3));catalogWindow.Show("주민 안내",entries,e=>{if(e.Key<0){OpenCommunityLot(id);return;}string reason;if(TryCommunityVisit(view,people[e.Key].id,out reason))Message("시설로 안내했어요.");else Message(reason);OpenCommunityLot(id);},()=>{},()=>{});
    }
}
