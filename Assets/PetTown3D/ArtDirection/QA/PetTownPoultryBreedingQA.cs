#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

// 닭 개체 성별/번식/판매 순수 상태 검사. 장면·PlayerPrefs·실제 저장을 건드리지 않습니다.
public static class PetTownPoultryBreedingQA
{
    static void Care(PetTownPoultryState s,PetTownCoopRecord c,int day){s.Feed(c,day);s.Water(c,day);s.Clean(c);}
    public static Dictionary<string,bool> Run()
    {
        var checks=new Dictionary<string,bool>();Action<string,bool> test=(k,v)=>{checks[k]=v;if(!v)throw new Exception(k);};string reason;var act=new[]{"f1","f2","f3","f4"};

        // 기존 저장 이관: 성별 없는 닭은 암탉, ID/이름/둥지/먹이 유지
        var ls=JsonUtility.FromJson<PetTownPoultryState>("{\"coops\":[{\"farmId\":\"f\",\"lastDay\":3,\"nestEggs\":2,\"cleanliness\":100,\"birds\":[{\"id\":\"a1\",\"name\":\"닭 1\",\"grown\":2,\"purchasedDay\":0,\"fedDay\":-1,\"wateredDay\":-1},{\"id\":\"a2\",\"name\":\"닭 2\",\"grown\":0,\"purchasedDay\":3,\"fedDay\":-1,\"wateredDay\":-1}]}],\"feed\":5,\"eggs\":0,\"revision\":1}");ls.Normalize();var lc=ls.Find("f");
        test("legacy_birds_kept",lc!=null&&lc.birds.Count==2&&lc.birds[0].id=="a1"&&lc.birds[0].name=="닭 1");
        test("legacy_become_hens_and_lay",lc.birds[0].sex==2&&lc.birds[1].sex==2&&lc.birds[0].Lays&&lc.birds[0].Adult);
        test("legacy_eggs_feed_kept",lc.nestEggs==2&&ls.feed==5&&lc.clutches!=null&&lc.births!=null&&lc.clutches.Count==0);
        test("legacy_hatched_zero",lc.hatched==0&&ls.TotalHatched()==0);
        int lg=1000;test("legacy_next_name_unique",ls.BuyBird(lc,3,ref lg)&&lc.birds[2].name=="닭 3"&&lg==920);

        // 구매 성별 / 산란은 암탉만
        var s=new PetTownPoultryState{feed=1000};int gold=20000;s.Build("f1",0,ref gold);s.Build("f2",0,ref gold);var c1=s.Find("f1");var c2=s.Find("f2");
        test("buy_hen_rooster",s.BuyBird(c1,0,ref gold)&&s.BuyBirdOfSex(c1,1,0,ref gold)&&c1.birds[0].sex==2&&c1.birds[1].sex==1);
        int beforeInvalid=gold;test("invalid_sex_no_charge",!s.BuyBirdOfSex(c1,3,0,ref gold)&&gold==beforeInvalid);
        s.BuyBirdOfSex(c2,1,0,ref gold);
        for(int d=0;d<5;d++){Care(s,c1,d);Care(s,c2,d);s.AdvanceDay(d+1,act);}
        test("hen_lays_rooster_does_not",c1.nestEggs>0&&c2.nestEggs==0);

        // 번식 조건
        int day=5;var hen=c1.birds[0];var roo=c1.birds[1];
        test("needs_food_water_today",!s.StartBreeding(c1,hen.id,roo.id,day,out reason));
        s.Feed(c1,day);s.Water(c1,day);
        test("same_bird_blocked",!s.StartBreeding(c1,hen.id,hen.id,day,out reason));
        test("start_and_reserve",s.StartBreeding(c1,hen.id,roo.id,day,out reason)&&s.Reserved(c1)==1&&c1.clutches[0].remainingDays==2&&hen.breedRestDays==3&&roo.breedRestDays==3);
        test("duplicate_parent_blocked",!s.StartBreeding(c1,hen.id,roo.id,day,out reason)&&c1.clutches.Count==1);
        int g0=gold;test("parent_sale_blocked",!s.SellBird(c1,hen.id,ref gold,out reason)&&gold==g0);

        // 건너뛴 날짜는 소급하지 않음
        var skip=new PetTownPoultryState{feed=100};int sg=5000;skip.Build("f4",0,ref sg);var k=skip.Find("f4");skip.BuyBird(k,0,ref sg);skip.BuyBirdOfSex(k,1,0,ref sg);
        for(int d=0;d<3;d++){Care(skip,k,d);skip.AdvanceDay(d+1,act);}
        skip.Feed(k,3);skip.Water(k,3);skip.StartBreeding(k,k.birds[0].id,k.birds[1].id,3,out reason);skip.AdvanceDay(7,act);
        test("skipped_days_no_progress",k.clutches.Count==1&&k.clutches[0].remainingDays==2);

        // 부화
        for(int d=5;d<7;d++){Care(s,c1,d);s.AdvanceDay(d+1,act);}
        test("hatch_after_two_cared_days",c1.clutches.Count==0&&c1.birds.Count==3);var chick=c1.birds[2];
        test("chick_parentage",chick.generation==1&&chick.parentA==hen.id&&chick.parentB==roo.id&&!chick.Adult&&(chick.sex==1||chick.sex==2));
        test("hatched_counter",c1.hatched==1&&s.TotalHatched()==1&&c2.hatched==0);
        test("birth_log",c1.births.Count==1&&c1.births[0].childId==chick.id);
        test("parents_unlocked",!s.BreedingLocked(c1,hen.id)&&!s.BreedingLocked(c1,roo.id));
        test("sell_quotes",PetTownPoultryState.SellQuote(chick)==24&&PetTownPoultryState.SellQuote(hen)==48);
        int cg=100;test("sell_chick",s.SellBird(c1,chick.id,ref cg,out reason)&&cg==124&&c1.birds.Count==2);test("sell_twice_blocked",!s.SellBird(c1,chick.id,ref cg,out reason)&&cg==124);
        int maxGold=int.MaxValue;test("sale_overflow_atomic",!s.SellBird(c1,hen.id,ref maxGold,out reason)&&c1.birds.Count==2&&maxGold==int.MaxValue);

        // 근친
        var pa=new PetTownChickenRecord{id="p",sex=2};var ch=new PetTownChickenRecord{id="c",parentA="p",parentB="q",sex=1};var sib=new PetTownChickenRecord{id="s",parentA="p",parentB="z",sex=2};var other=new PetTownChickenRecord{id="o",sex=1};
        test("related_parent_child",PetTownPoultryState.Related(pa,ch));test("related_siblings",PetTownPoultryState.Related(ch,sib));test("unrelated_allowed",!PetTownPoultryState.Related(pa,other)&&!PetTownPoultryState.Related(ch,other));

        // 정원(예약 포함)
        var cap=new PetTownPoultryState{feed=1000};int cgold=50000;cap.Build("f3",0,ref cgold);var q=cap.Find("f3");cap.BuyBird(q,0,ref cgold);cap.BuyBirdOfSex(q,1,0,ref cgold);
        for(int d=0;d<2;d++){Care(cap,q,d);cap.AdvanceDay(d+1,act);}
        for(int i=0;i<3;i++)cap.BuyBird(q,2,ref cgold);cap.Feed(q,2);cap.Water(q,2);
        test("capacity_start",q.birds.Count==5&&cap.StartBreeding(q,q.birds[0].id,q.birds[1].id,2,out reason));
        test("reserved_slot_not_sold",!cap.BuyBird(q,2,ref cgold)&&cap.Reserved(q)==1);
        test("cancel_keeps_rest",cap.CancelBreeding(q,q.clutches[0].id)&&cap.Reserved(q)==0&&q.birds[0].breedRestDays==3&&cap.BuyBird(q,2,ref cgold));

        // 저장 왕복 + 유령 알 정리
        var rt=JsonUtility.FromJson<PetTownPoultryState>(JsonUtility.ToJson(s));rt.Normalize();var rc=rt.Find("f1");
        test("json_roundtrip",rc.birds.Count==c1.birds.Count&&rc.births.Count==1&&rc.birds[0].sex==c1.birds[0].sex);
        rc.clutches.Add(new PetTownChickenClutch{id="bad",henId="없음",roosterId="없음"});rt.Normalize();test("ghost_clutch_removed",rc.clutches.Count==0);
        return checks;
    }
}
#endif
