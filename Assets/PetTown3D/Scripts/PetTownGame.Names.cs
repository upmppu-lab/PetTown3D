using UnityEngine;

// 내 이름·마을 이름 (진행 저장에 함께 저장됩니다).
public partial class PetTownGame
{
    public string PlayerName{get{var n=Progression!=null?Progression.playerName:null;return string.IsNullOrEmpty(n)?"내 이름":n;}}
    public string TownName{get{var n=Progression!=null?Progression.townName:null;return string.IsNullOrEmpty(n)?"펫 타운":n;}}
    public void SetPlayerTownNames(string player,string town)
    {
        if(Progression==null)return;
        Progression.playerName=(player??"").Trim();Progression.townName=(town??"").Trim();
        progressionDirty=true;SaveProgressionSilent();Message("이름을 저장했어요.");
    }
}
