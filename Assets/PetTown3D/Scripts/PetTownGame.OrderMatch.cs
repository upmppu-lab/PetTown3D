using UnityEngine;

public partial class PetTownGame
{
    // 손님 취향: 0=상관없음, 1=수컷, 2=암컷. 맞으면 찰떡(+가격·명성·서비스점수).
    int orderWantSex;
    string MatchTag(PetTownPetCareRecord p)
    {
        if(p==null)return "";
        string rare=PetTownRarity.IsRare(p.speciesKind,p.appearance)?" · 레어 ✦":(orderVip?" · VIP는 레어만":"");
        if(orderWantSex==0)return rare;
        return rare+(p.sex==orderWantSex?" · 찰떡 ♥":" · 성별 다름");
    }
}
