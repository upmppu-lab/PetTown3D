using System.Collections.Generic;
using UnityEngine;

public partial class PetTownGame
{
    int BreedingCapacity
    {
        get{var room=shop?shop.GetComponent<PetTownInteriorState>():null;return room?Mathf.Clamp(Mathf.FloorToInt(room.LocalBounds.width*room.LocalBounds.height/6),8,200):0;}
    }
    void TickBreeding(float seconds)
    {
        if(loadingTown||expansionPending||!shop)return;
        var br=Progression.Breeding;
        if(br.auto&&Progression.playerLevel>=PetTownBreedingState.AutoLevel)
        {
            br.autoClock+=seconds;
            if(br.autoClock>=20){br.autoClock=0;string autoWhy;if(br.TryAuto(Progression,BreedingCapacity,out autoWhy)){progressionDirty=true;SaveProgressionSilent();Message("자동 교배를 시작했어요.");}}
        }
        int born=br.Tick(seconds,Progression,day);if(born<=0)return;
        SyncProgressionStock();money+=Progression.AddExperience(born*5);Operations.Record("born");progressionDirty=true;SaveProgressionSilent();int rareBorn=PetTownRarity.TakeRareBorn();Message("새끼 "+born+"마리가 태어났어요!"+(rareBorn>0?" ✦ 레어 "+rareBorn+"마리 포함!":"")+" 펫 정보에서 돌봐 주세요.");
    }
    string BreedingSpace=>"재고 "+Progression.care.pets.FindAll(p=>!p.displayOnly).Count+" · 예약 "+Progression.Breeding.Reserved+" · 교배 정원 "+BreedingCapacity;
    void OpenBreeding()
    {
        SetMode(Mode.Play);var entries=new List<PetTownCatalogEntry>();var ids=new List<string>();
        entries.Add(OperationsEntry(-1,"새로운 교배",BreedingSpace,"실내 면적에 따른 새끼 돌봄 공간","부모 선택","같은 종류의 성체 암수를 선택해 주세요.\n배부름40%·컨디션50% 이상이어야 합니다.\n교배는 활성 플레이150~200초, 다시 교배하려면600초 휴식이 필요합니다.\n정원은 새 교배를 시작할 때 검사하며 기존 재고는 삭제하지 않습니다.",9));
        entries.Add(OperationsEntry(-3,"자동 교배",Progression.Breeding.auto?"켜짐":"꺼짐",Progression.playerLevel>=PetTownBreedingState.AutoLevel?"등급이 높은 짝부터 자동 선택":"플레이어 Lv."+PetTownBreedingState.AutoLevel+"에 해금",Progression.Breeding.auto?"끄기":"켜기","20초마다 교배 가능한 암수를 찾아 최대 "+PetTownBreedingState.AutoMaxNests+"쌍까지 자동으로 시작합니다. 공간 정원과 휴식 규칙은 수동 교배와 같습니다.",9));
        entries.Add(OperationsEntry(-2,"탄생 기록",Progression.Breeding.births.Count+"건","태어난 새끼와 부모 확인","기록 보기","최근30건의 탄생 기록을 확인합니다.",9));
        for(int i=0;i<Progression.Breeding.nests.Count;i++)
        {
            var n=Progression.Breeding.nests[i];ids.Add(n.id);entries.Add(OperationsEntry(i,n.nameA+" + "+n.nameB,Mathf.CeilToInt(n.remaining)+"초 남음","새끼 "+n.litter+"마리 공간 예약","교배 취소","진행 중에는 부모를 분양할 수 없습니다.\n취소하면 새끼는 태어나지 않으며 부모의 휴식 시간은 유지됩니다.",9));
        }
        catalogWindow.Show("교배와 탄생",entries,e=>{if(e.Key==-1){OpenBreedingParents();return;}if(e.Key==-2){OpenBirthHistory();return;}if(e.Key==-3){if(Progression.playerLevel<PetTownBreedingState.AutoLevel)Message("자동 교배는 플레이어 Lv."+PetTownBreedingState.AutoLevel+"에 열려요.");else{Progression.Breeding.auto=!Progression.Breeding.auto;progressionDirty=true;SaveProgressionSilent();Message(Progression.Breeding.auto?"자동 교배를 켰어요.":"자동 교배를 껐어요.");}OpenBreeding();return;}if(!OperationsReady())return;if(Progression.Breeding.Cancel(ids[e.Key],Progression.care.pets)){progressionDirty=true;SaveProgressionSilent();Message("교배를 취소했어요. 부모는 휴식을 이어갑니다.");}OpenBreeding();},()=>{},()=>{});
    }
    void OpenBreedingParents(string firstId=null)
    {
        var first=firstId==null?null:Progression.care.pets.Find(p=>p.id==firstId);if(firstId!=null&&first==null){OpenBreeding();return;}
        SetMode(Mode.Play);var entries=new List<PetTownCatalogEntry>();var ids=new List<string>();
        foreach(var p in Progression.care.pets)
        {
            if(p.displayOnly||p.speciesKind<0||p.speciesKind>=PetTownProgressionState.Species.Length||p.id==firstId||(first!=null&&p.speciesKind!=first.speciesKind))continue;
            string reason;bool ready=PetTownBreedingState.CanBreed(p,out reason);if(first!=null&&first.sex==p.sex){ready=false;reason="다른 성별의 짝을 선택해 주세요.";}
            ids.Add(p.id);var e=OperationsEntry(ids.Count-1,p.nickname,p.SexLabel+" · "+(p.growth>=1?"성체":"성장 중")+" · "+p.GradeLabel,"컨디션 "+Mathf.RoundToInt(p.Condition)+"%",ready?(first==null?"이 펫 선택":"교배 시작"):"상태 확인",ready?(first==null?"다음 화면에서 짝을 고릅니다.":first.nickname+"과 교배합니다.\n"+BreedingSpace+"\n성장 전 새끼는 분양되지 않습니다."):reason,9);
            if(p.speciesKind==0)e.Thumbnail=Resources.Load<Texture2D>("UI/Cozy/Pet_"+p.appearance);entries.Add(e);
        }
        if(entries.Count==0)entries.Add(OperationsEntry(-1,"선택할 펫이 없어요","입고한 펫이 성장해야 합니다","전시 동물은 교배에서 제외됩니다","매장 관리","분양용 펫을 입고하고 성체가 될 때까지 돌봐 주세요.",9));
        catalogWindow.Show(first==null?"교배 부모 선택":"교배할 짝 선택",entries,e=>
        {
            if(e.Key<0){OpenShopManagement();return;}var p=Progression.care.pets.Find(v=>v.id==ids[e.Key]);string reason;
            if(!OperationsReady())return;if(!PetTownBreedingState.CanBreed(p,out reason)){Message(reason);OpenBreedingParents(firstId);return;}
            if(firstId==null){OpenBreedingParents(p.id);return;}
            int litter=Random.Range(1,p.speciesKind==2?4:3);
            if(Progression.Breeding.Start(Progression,firstId,p.id,BreedingCapacity,litter,out reason)){progressionDirty=true;SaveProgressionSilent();Message("교배를 시작했어요. 부모와 새끼의 공간을 예약했습니다.");OpenBreeding();}else{Message(reason);OpenBreedingParents(firstId);}
        },()=>{},()=>{});
    }
    void OpenBirthHistory()
    {
        var entries=new List<PetTownCatalogEntry>();int i=0;foreach(var n in Progression.Breeding.births)entries.Add(OperationsEntry(i++,PetTownProgressionState.Species[Mathf.Clamp(n.species,0,PetTownProgressionState.Species.Length-1)]+" "+n.count+"마리","Day "+n.day,n.parentA+" + "+n.parentB,"교배 목록","새끼는 기존 펫 정보와 분양 재고에서 관리합니다.\n성장과 돌봄·경험치·분양 규칙은 기존 개체와 같습니다.",9));
        if(entries.Count==0)entries.Add(OperationsEntry(0,"아직 탄생 기록이 없어요","첫 새끼를 기다리고 있어요","교배 완료 후 기록됩니다","교배 목록","기존 펫의 정보는 변경하지 않습니다.",9));
        catalogWindow.Show("탄생 기록",entries,e=>OpenBreeding(),()=>{},()=>{});
    }
}
