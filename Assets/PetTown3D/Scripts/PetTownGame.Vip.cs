using System.Collections.Generic;
using UnityEngine;

public partial class PetTownGame
{
    bool orderVip;
    int orderKind; // 0 일반, 1 가족(+12%), 2 수집가(레어/Lv.5 이상 +80%), 3 부자(+60%, 펫 호텔이 있으면 더 자주), 4 유명인(레어 +100%, 아니면 +20%)
    void PickGuestKind()
    {
        orderKind=0;if(orderVip||Progression==null)return;int pop=EnsureResidents().Population;float r=UnityEngine.Random.value;
        float celeb=pop>=60?.05f:0f,rich=pop>=40?.08f+(DecorOwned(414)?.05f:0f):0f,coll=pop>=24?.12f:0f;
        if(r<celeb){orderKind=4;orderWantSex=0;}else if(r<celeb+rich){orderKind=3;orderWantSex=0;}else if(r<celeb+rich+coll){orderKind=2;orderWantSex=0;}else if(pop>=10&&r<celeb+rich+coll+.18f)orderKind=1;
    }
    string GuestTag(){return orderKind==1?"[가족] ":orderKind==2?"[수집가] ":orderKind==3?"[부자] ":orderKind==4?"[유명인] ":"";}
    string GuestNote(){return orderKind==1?" · 가족 용품 세트":orderKind==2?" · 수집가 특별가":orderKind==3?" · 부자 손님 팁":" · 유명인 효과";}
    int GuestBonus(int income,PetTownPetCareRecord pet,bool rare)
    {
        switch(orderKind)
        {
            case 1:return Mathf.RoundToInt(income*.12f);
            case 2:return pet!=null&&(rare||pet.Level>=5)?Mathf.RoundToInt(income*.8f):0;
            case 3:return Mathf.RoundToInt(income*.6f);
            case 4:return Mathf.RoundToInt(income*(rare?1f:.2f));
        }
        return 0;
    }
    // 분양 가능한 레어 펫(강아지·고양이·토끼)이 있으면 25% 확률로 VIP 주문이 된다.
    void PickVipOrder()
    {
        orderVip=false;if(Progression==null||Progression.care==null)return;
        var rares=new List<int>();
        foreach(var p in Progression.care.pets)if(p!=null&&!p.displayOnly&&p.Adoptable&&p.speciesKind>=0&&p.speciesKind<=2&&PetTownRarity.IsRare(p.speciesKind,p.appearance))rares.Add(p.speciesKind);
        if(rares.Count==0||UnityEngine.Random.value>.25f)return;
        orderVip=true;orderPet=rares[UnityEngine.Random.Range(0,rares.Count)];orderWantSex=0;
    }
}
