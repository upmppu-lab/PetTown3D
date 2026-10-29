using System;
using UnityEngine;

// Special menu supplies are uncooked portions. Cooking still requires the real visit workflow.
public static class PetTownCafeRecipes
{
    public const int Count=5,Limit=10000;
    static readonly int[] unlockLevel={1,1,1,3,5};
    public static int UnlockLevel(int menu)=>menu>=0&&menu<unlockLevel.Length?unlockLevel[menu]:99;
    public static bool Unlocked(int menu,int level)=>Valid(menu)&&level>=UnlockLevel(menu);
    public static bool Valid(int menu)=>menu>=0&&menu<Count;
    public static string Name(int menu)=>menu==1?"채소 샌드위치":menu==2?"우유 푸딩":menu==3?"당근 옥수수 수프":menu==4?"감자 그라탱":"기본 간식·차";
    public static int Price(int menu)=>menu==1?110:menu==2?130:menu==3?150:menu==4?175:70;
    public static int Portions(int menu)=>menu==1?2:menu==2?3:menu==3?3:menu==4?3:0;
    public static float CookSeconds(int menu)=>menu==1?10:menu==2?12:menu==3?13:menu==4?15:8;
    public static string Needs(int menu)=>menu==1?"양배추1 · 토마토1 · 기본 재료2 → 2인분":menu==2?"우유2 · 기본 재료3 → 3인분":menu==3?"당근2 · 옥수수1 · 기본 재료3 → 3인분":menu==4?"감자2 · 우유1 · 기본 재료4 → 3인분":"기본 재료1 → 1인분";
    public static string Short(int menu)=>menu==1?"샌드위치":menu==2?"푸딩":menu==3?"수프":menu==4?"그라탱":"기본";
    public static int BaseCost(int menu)=>menu==1?2:menu==2?3:menu==3?3:menu==4?4:1;
}

public sealed partial class PetTownFacilityState
{
    public int[] cafeRecipeSupplies=new int[PetTownCafeRecipes.Count-1];
    public int CafeStock(int menu)=>menu==0?cafeIngredients:PetTownCafeRecipes.Valid(menu)&&cafeRecipeSupplies!=null&&cafeRecipeSupplies.Length>=menu?cafeRecipeSupplies[menu-1]:0;
    public int AvailableCafe(int menu)=>PetTownCafeRecipes.Valid(menu)?Mathf.Max(0,CafeStock(menu)-visits.FindAll(v=>v.kind==3&&v.cafeMenu==menu&&!v.suppliesUsed&&!v.paid).Count):0;
    public int AvailableFor(PetTownFacilityBusiness b)=>b==null?0:b.kind==3?AvailableCafe(b.cafeMenu):AvailableSupplies(b.kind);
    public int VisitStock(PetTownFacilityVisit v)=>v==null?0:v.kind==3?CafeStock(v.cafeMenu):Stock(v.kind);
    public static int VisitFee(PetTownFacilityVisit v)=>v.kind==3?PetTownCafeRecipes.Price(v.cafeMenu):PetTownClinic.Fee(v.clinicService);
    public bool SelectCafeMenu(PetTownFacilityBusiness business,int menu)
    {
        if(business==null||business.kind!=3||!businesses.Contains(business)||!PetTownCafeRecipes.Valid(menu))return false;
        business.cafeMenu=menu;return true;
    }
    public bool PrepareCafeRecipe(int menu,PetTownFarmState farm,PetTownRanchState ranch,out string reason)
    {
        reason="준비할 메뉴를 확인해 주세요.";
        if(menu<1||!PetTownCafeRecipes.Valid(menu))return false;
        int portions=PetTownCafeRecipes.Portions(menu);
        if(CafeStock(menu)>PetTownCafeRecipes.Limit-portions){reason="메뉴 재료의 보관 한도에 도달했어요.";return false;}
        // Reserved basic orders must not be consumed by preparation for another menu.
        if(AvailableCafe(0)<portions){reason="예약분을 제외한 기본 재료가 부족해요.";return false;}
        bool fOk=farm!=null&&farm.produce!=null&&farm.produce.Length>=7,rOk=ranch!=null&&ranch.products!=null&&ranch.products.Length>=1;
        if(menu==1&&(!fOk||farm.produce[0]<1||farm.produce[1]<1)){reason="양배추1개와 토마토1개가 필요해요.";return false;}
        if(menu==2&&(!rOk||ranch.products[0]<2)){reason="우유2개가 필요해요.";return false;}
        if(menu==3&&(!fOk||farm.produce[2]<2||farm.produce[3]<1)){reason="당근2개와 옥수수1개가 필요해요.";return false;}
        if(menu==4&&(!fOk||!rOk||farm.produce[6]<2||ranch.products[0]<1)){reason="감자2개와 우유1개가 필요해요.";return false;}
        if(AvailableCafe(0)<PetTownCafeRecipes.BaseCost(menu)){reason="예약분을 제외한 기본 재료가 "+PetTownCafeRecipes.BaseCost(menu)+"개 필요해요.";return false;}
        if(cafeRecipeSupplies==null||cafeRecipeSupplies.Length!=PetTownCafeRecipes.Count-1)Array.Resize(ref cafeRecipeSupplies,PetTownCafeRecipes.Count-1);
        if(menu==1){farm.produce[0]--;farm.produce[1]--;farm.revision++;}
        else if(menu==2){ranch.products[0]-=2;ranch.revision++;}
        else if(menu==3){farm.produce[2]-=2;farm.produce[3]--;farm.revision++;}
        else{farm.produce[6]-=2;farm.revision++;ranch.products[0]--;ranch.revision++;}
        cafeIngredients-=PetTownCafeRecipes.BaseCost(menu);cafeRecipeSupplies[menu-1]+=portions;reason="조리용 재료를 "+portions+"인분 준비했어요.";return true;
    }
    void NormalizeCafeRecipes()
    {
        if(cafeRecipeSupplies==null||cafeRecipeSupplies.Length!=PetTownCafeRecipes.Count-1)Array.Resize(ref cafeRecipeSupplies,PetTownCafeRecipes.Count-1);
        for(int i=0;i<cafeRecipeSupplies.Length;i++)cafeRecipeSupplies[i]=Mathf.Clamp(cafeRecipeSupplies[i],0,PetTownCafeRecipes.Limit);
        foreach(var b in businesses)b.cafeMenu=b.kind==3&&PetTownCafeRecipes.Valid(b.cafeMenu)?b.cafeMenu:0;
        foreach(var v in visits)v.cafeMenu=v.kind==3&&PetTownCafeRecipes.Valid(v.cafeMenu)?v.cafeMenu:0;
        foreach(var r in receipts)if(r!=null)r.cafeMenu=r.kind==3&&PetTownCafeRecipes.Valid(r.cafeMenu)?r.cafeMenu:0;
    }
}
