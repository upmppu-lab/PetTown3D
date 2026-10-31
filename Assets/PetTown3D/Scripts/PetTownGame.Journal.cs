using System.Collections.Generic;
using UnityEngine;

public partial class PetTownGame
{
    static readonly string[] QuestKeys={"feed","wash","play","adopt","hire","delivery","groom"};
    static readonly string[] QuestNames={"첫 식사 돌봄","깨끗한 펫샵","함께 놀아요","새 가족 찾기","든든한 동료","도착한 선물","단정한 새 출발"};
    static readonly int[] QuestTargets={5,3,3,1,1,1,1},QuestGold={100,100,100,200,150,100,200},QuestXp={5,5,5,10,10,5,10};
    bool ClaimJournalQuest(string key)
    {
        if(loadingTown||expansionPending||!shop)return false;
        int i=System.Array.IndexOf(QuestKeys,key);if(i<0||Operations.claimedQuests.Contains(key)||Operations.Activity(key)<QuestTargets[i])return false;
        Operations.claimedQuests.Add(key);money+=QuestGold[i]+Progression.AddExperience(QuestXp[i]);progressionDirty=true;SaveProgressionSilent();return true;
    }
    void OpenJournalQuests()
    {
        SetMode(Mode.Play);var entries=new List<PetTownCatalogEntry>();for(int i=0;i<QuestKeys.Length;i++)
        {
            string key=QuestKeys[i];bool claimed=Operations.claimedQuests.Contains(key),ready=Operations.Activity(key)>=QuestTargets[i];
            entries.Add(OperationsEntry(i,QuestNames[i],claimed?"보상 수령 완료":Mathf.Min(Operations.Activity(key),QuestTargets[i])+" / "+QuestTargets[i],QuestGold[i]+" 골드 · XP "+QuestXp[i],claimed?"완료 확인":ready?"보상 받기":"진행 확인","마을 안내 퀘스트입니다.\n진행 조건: "+QuestNames[i]+" "+QuestTargets[i]+"회\n보상은 한 번만 받을 수 있습니다.",5));
        }
        entries.Add(OperationsEntry(-1,"오늘의 의뢰로","매일 새 의뢰 5개","게임 날짜 기준","게시판 보기","마을의 일일 의뢰를 확인합니다.",5));
        catalogWindow.Show("마을 안내 퀘스트",entries,e=>{if(e.Key==-1){OpenDailyQuests();return;}if(ClaimJournalQuest(QuestKeys[e.Key]))Message("퀘스트 보상을 받았어요!");else Message(Operations.claimedQuests.Contains(QuestKeys[e.Key])?"이미 받은 보상입니다.":"목표를 달성하면 보상을 받을 수 있어요.");OpenJournalQuests();},()=>{},()=>{});
    }
    void OpenPetSpeciesDex()
    {
        SetMode(Mode.Play);var entries=new List<PetTownCatalogEntry>();string[] notes={"산책과 놀이를 좋아하는 강아지예요.","느긋한 휴식과 깨끗한 공간을 좋아해요.","조용한 공간에서 편안히 쉬게 해 주세요.","호기심이 많고 활발한 페럿이에요."};
        for(int i=0;i<PetTownProgressionState.Species.Length;i++){int kind=i;bool known=Operations.discovered[i];entries.Add(OperationsEntry(i,known?PetTownProgressionState.Species[i]:"아직 만나지 못했어요",known?"발견 완료":"미발견",known?"기본 사료 1개 "+PetTownProgressionState.FoodPrice[PetTownProgressionState.FoodLineOf(i)]+" 골드":"입고하거나 돌보면 기록됩니다","확인",known?PetTownProgressionState.Notes[i]+"\n보유 중인 펫을 돌보며 레벨을 올릴 수 있어요.":"새로운 펫을 마을에서 만나 보세요.",9));}
        catalogWindow.Show("펫 도감",entries,e=>{},()=>{},()=>{});
    }
}
