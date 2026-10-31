using UnityEngine;

public partial class PetTownGame
{
    // 2D 게임의 콤보: 90초 안에 연달아 분양하면 단계마다 +6%(최대 +30%) 팁.
    float lastSaleTime=-999f;int saleCombo;
    int RegisterSaleCombo(int income,out int count)
    {
        saleCombo=(Time.time-lastSaleTime<90f)?saleCombo+1:1;lastSaleTime=Time.time;count=saleCombo;
        if(saleCombo<2)return 0;
        return Mathf.RoundToInt(income*Mathf.Min(.3f,.06f*(saleCombo-1)));
    }
}
