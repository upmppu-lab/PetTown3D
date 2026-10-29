#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

// 호수 낚시(물고기 수급/판매/카페 재료) 순수 상태 검사. 장면/PlayerPrefs/실제 저장을 건드리지 않습니다.
public static class PetTownLakeFishingQA
{
    static PetTownCommunityVisit Visit(string id,string lot,string resident,int slot)=>new PetTownCommunityVisit{id=id,buildingId=lot,residentId=resident,slot=slot,stage=2,rest=15};
    public static Dictionary<string,bool> Run()
    {
        var checks=new Dictionary<string,bool>();Action<string,bool> test=(k,v)=>{checks[k]=v;if(!v)throw new Exception(k);};

        // 기존 저장(물고기 필드 없음) 이관
        var legacy=JsonUtility.FromJson<PetTownCommunityState>("{\"lots\":[{\"id\":\"old\",\"kind\":6,\"lastDay\":5,\"open\":true,\"totalVisits\":4}],\"visits\":[],\"food\":3}");legacy.Normalize();
        test("legacy_lake_default_stock",legacy.lots.Count==1&&legacy.lots[0].fishStock==PetTownCommunityState.FishCap/2&&legacy.fish==0&&legacy.food==3&&legacy.lots[0].totalVisits==4);

        int day=10;var s=new PetTownCommunityState();var lot=s.Get("lake",6,day);var park=s.Get("park",5,day);var zoo=s.Get("zoo",7,day);
        var p1=new PetTownResidentRecord{id="p1",happiness=50};var p2=new PetTownResidentRecord{id="p2",happiness=50};var p3=new PetTownResidentRecord{id="p3",happiness=50};var p4=new PetTownResidentRecord{id="p4",happiness=50};
        int startStock=lot.fishStock;
        var v1=Visit("v1","lake","p1",0);s.visits.Add(v1);
        test("fishing_slot_catches_fish",s.Complete(v1,p1,day)&&s.fish==1&&lot.fishStock==startStock-1&&Mathf.Approximately(p1.happiness,53)&&lot.totalVisits==1);
        var v2=Visit("v2","lake","p2",2);s.visits.Add(v2);
        test("rest_slot_no_fish",s.Complete(v2,p2,day)&&s.fish==1&&lot.fishStock==startStock-1&&Mathf.Approximately(p2.happiness,52));
        var v1b=Visit("v1b","lake","p1",1);s.visits.Add(v1b);
        test("same_resident_same_day_no_extra_fish",!s.Complete(v1b,p1,day)&&s.fish==1&&Mathf.Approximately(p1.happiness,53));
        lot.fishStock=0;var v3=Visit("v3","lake","p3",1);s.visits.Add(v3);
        test("empty_lake_no_fish_but_normal_reward",s.Complete(v3,p3,day)&&s.fish==1&&lot.fishStock==0&&Mathf.Approximately(p3.happiness,52));
        var vp=Visit("vp","park","p4",0);s.visits.Add(vp);
        test("park_slot0_never_fishes",s.Complete(vp,p4,day)&&s.fish==1&&park.fishStock==PetTownCommunityState.FishCap/2&&Mathf.Approximately(p4.happiness,52));
        var vz=Visit("vz","zoo","p4",0);zoo.animals[0]=1;zoo.fullness=100;zoo.cleanliness=100;s.visits.Add(vz);var p5=new PetTownResidentRecord{id="p5",happiness=50};vz.residentId="p5";
        test("zoo_reward_unchanged",s.Complete(vz,p5,day)&&Mathf.Approximately(p5.happiness,53)&&s.fish==1);
        var vi=Visit("vi","lake","p1",0);vi.stage=1;s.visits.Add(vi);test("incomplete_visit_gives_nothing",!s.Complete(vi,p1,day+1)&&s.fish==1);

        // 호수 물고기 재생
        lot.fishStock=2;s.AdvanceDay(day+1);test("regen_three_per_day",lot.fishStock==5);
        s.AdvanceDay(day+31);test("regen_capped",lot.fishStock==PetTownCommunityState.FishCap);
        park.fishStock=1;s.AdvanceDay(day+32);test("park_not_regenerating",park.fishStock==1);

        // 판매
        s.fish=5;int gold=100;test("sell_three",s.SellFish(3,ref gold)&&gold==100+3*PetTownCommunityState.FishPrice&&s.fish==2);
        test("sell_too_many_rejected",!s.SellFish(3,ref gold)&&s.fish==2&&gold==220);
        test("sell_zero_rejected",!s.SellFish(0,ref gold)&&!s.SellFish(-1,ref gold));
        int maximum=int.MaxValue;test("sell_overflow_atomic",!s.SellFish(1,ref maximum)&&s.fish==2&&maximum==int.MaxValue);

        // 카페 재료
        var cafe=new PetTownFacilityState();int before=cafe.cafeIngredients;s.fish=5;
        test("fish_to_ingredients",s.FishToIngredients(cafe)&&s.fish==3&&cafe.cafeIngredients==before+2);
        s.fish=1;test("one_fish_not_enough",!s.FishToIngredients(cafe)&&s.fish==1);
        s.fish=4;cafe.cafeIngredients=9999;test("ingredient_cap_atomic",!s.FishToIngredients(cafe)&&s.fish==4&&cafe.cafeIngredients==9999);test("null_cafe_safe",!s.FishToIngredients(null)&&s.fish==4);

        // 저장 왕복 + 정리
        s.fish=7;lot.fishStock=4;var copy=JsonUtility.FromJson<PetTownCommunityState>(JsonUtility.ToJson(s));copy.Normalize();
        test("json_roundtrip_fish",copy.fish==7&&copy.lots.Find(x=>x.id=="lake").fishStock==4);
        copy.fish=-5;copy.lots.Find(x=>x.id=="lake").fishStock=99;copy.Normalize();test("normalize_clamps",copy.fish==0&&copy.lots.Find(x=>x.id=="lake").fishStock==PetTownCommunityState.FishCap);
        copy.lots.Find(x=>x.id=="lake").fishStock=-3;copy.fish=100000;copy.Normalize();test("normalize_clamps_low_high",copy.lots.Find(x=>x.id=="lake").fishStock==0&&copy.fish==9999);
        return checks;
    }
}
#endif
