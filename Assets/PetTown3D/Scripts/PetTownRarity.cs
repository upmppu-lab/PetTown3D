using System.Collections.Generic;
using UnityEngine;

// 레어 외형: 분양가 x2.5. VIP 손님은 레어만 사 간다(2D 게임의 레어 털색 + VIP).
public static class PetTownRarity
{
    static readonly HashSet<int> Rare=new HashSet<int>{0*100+7,0*100+12,0*100+15,0*100+19,1*100+4,1*100+7,2*100+2,3*100+1,4*100+2,5*100+1,7*100+1,8*100+4};
    public static bool IsRare(int species,int appearance)=>Rare.Contains(species*100+appearance);
    public static int RareBorn;
    public static int TakeRareBorn(){int n=RareBorn;RareBorn=0;return n;}
    // 교배로 태어난 새끼의 레어 확률: 부모 중 레어가 있으면 50%(부모와 같은 외형 우선), 없으면 6%.
    public static bool RollBirth(PetTownPetCareRecord child,PetTownPetCareRecord pa,PetTownPetCareRecord pb)
    {
        if(child==null)return false;PetTownPetCareRecord rareParent=null;
        if(pa!=null&&pa.speciesKind==child.speciesKind&&IsRare(pa.speciesKind,pa.appearance))rareParent=pa;else if(pb!=null&&pb.speciesKind==child.speciesKind&&IsRare(pb.speciesKind,pb.appearance))rareParent=pb;
        if(UnityEngine.Random.value>=(rareParent!=null?.5f:.06f))return false;
        if(rareParent!=null){child.appearance=rareParent.appearance;RareBorn++;return true;}
        var options=new List<int>();for(int a=0;a<60;a++)if(Rare.Contains(child.speciesKind*100+a))options.Add(a);
        if(options.Count==0)return false;child.appearance=options[UnityEngine.Random.Range(0,options.Count)];RareBorn++;return true;
    }
}
