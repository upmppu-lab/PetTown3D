using UnityEngine;

public partial class PetTownGame
{
    int lastReputationGrade=-1;
    void CheckReputationGrade()
    {
        int g=PetTownReputation.GradeIndex(reputation);
        if(lastReputationGrade>=0&&g>lastReputationGrade)Message("명성 등급이 '"+PetTownReputation.Grade(reputation)+"'(으)로 올랐어요! 분양가가 "+Mathf.RoundToInt((PetTownReputation.PriceMultiplier(reputation)-1f)*100)+"% 올라요.");
        lastReputationGrade=g;
    }
}
