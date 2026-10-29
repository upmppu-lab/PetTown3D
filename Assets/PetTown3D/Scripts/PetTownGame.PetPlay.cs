using System.Collections.Generic;
using UnityEngine;

public partial class PetTownGame
{
    PetTownPetPlayWindow petPlayWindow;PetTownPetCareRecord playingPet;int playGeneration;
    bool PetPlayOpen=>petPlayWindow&&petPlayWindow.IsOpen;
    void OpenPetPlaySelection()
    {
        SetMode(Mode.Play);var entries=new List<PetTownCatalogEntry>();var ids=new List<string>();
        foreach(var p in Progression.care.pets){ids.Add(p.id);entries.Add(OperationsEntry(ids.Count-1,p.nickname,"Lv."+p.Level,"심심함 "+Mathf.RoundToInt(p.boredom)+"%","함께 놀기","12초 동안 나타나는 장난감을 터치해 주세요.\n심심함과 스트레스를 낮추고 경험치를 얻습니다.\n이미 만족한 펫에게는 보상을 중복 지급하지 않습니다.",9));}
        catalogWindow.Show("펫과 놀기",entries,e=>StartPetPlay(ids[e.Key]),()=>{},()=>{});
    }
    void StartPetPlay(string id)
    {
        if(!OperationsReady())return;var pet=Progression.care.pets.Find(p=>p.id==id);if(pet==null||pet.busy)return;
        if(pet.IsBreeding){Message("교배 중인 펫은 놀이를 잠시 쉬어 주세요.");return;}
        if(!pet.NeedsCare(3)){Message("지금은 충분히 즐거워하고 있어요. 잠시 뒤에 놀아 주세요.");return;}
        SetMode(Mode.Play);playingPet=pet;pet.busy=true;int token=++playGeneration;
        if(!petPlayWindow)petPlayWindow=uiRoot.gameObject.AddComponent<PetTownPetPlayWindow>();
        petPlayWindow.Show(pet.nickname,score=>FinishPetPlay(id,token,score),()=>{if(token==playGeneration){pet.busy=false;playingPet=null;playGeneration++;}});
    }
    string FinishPetPlay(string id,int token,float score)
    {
        var pet=Progression.care.pets.Find(p=>p.id==id);
        if(token!=playGeneration||pet==null)return "이번 놀이는 종료되었어요.";
        playGeneration++;pet.busy=false;playingPet=null;
        if(!pet.NeedsCare(3))return "함께 즐겁게 놀았어요!";
        score=Mathf.Clamp01(score);if(score<=0)return "장난감을 잡지 못했어요. 다시 도전해 보세요!";
        pet.boredom=Mathf.Max(0,pet.boredom-55*score);pet.stress=Mathf.Max(0,pet.stress-22*score);pet.fullness=Mathf.Max(0,pet.fullness-6*score);
        int petXP=Mathf.Max(1,Mathf.RoundToInt(3*score)),playerXP=Mathf.Max(1,Mathf.RoundToInt(2*score));
        pet.AddExperience(petXP);money+=Progression.AddExperience(playerXP);Operations.Record("play");progressionDirty=true;SaveProgressionSilent();return "놀이 완료! "+Mathf.RoundToInt(score*100)+"점\n펫 XP +"+petXP+" · 사장 XP +"+playerXP;
    }
    void ClosePetPlay(){if(playingPet!=null)playingPet.busy=false;playingPet=null;playGeneration++;if(petPlayWindow)petPlayWindow.Close();}
}
