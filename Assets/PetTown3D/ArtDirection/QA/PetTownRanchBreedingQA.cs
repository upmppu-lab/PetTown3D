#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public static class PetTownRanchBreedingQA
{
    static PetTownRanchState Pair(int species,out PetTownRanchLot lot)
    {
        var s=new PetTownRanchState{feed=100};lot=s.Get("ranch",100);int gold=10000;
        s.BuyAnimalOfSex(lot,species,2,100,ref gold);s.BuyAnimalOfSex(lot,species,1,100,ref gold);foreach(var a in lot.animals)a.grown=2;s.Feed(lot,100);s.Water(lot,100);return s;
    }
    static void Care(PetTownRanchState s,PetTownRanchLot lot,int day){s.Feed(lot,day);s.Water(lot,day);if(lot.cleanliness<90)s.Clean(lot);}
    public static Dictionary<string,bool> Run()
    {
        var checks=new Dictionary<string,bool>();Action<string,bool> test=(k,v)=>{checks[k]=v;if(!v)throw new Exception(k);};string reason;
        foreach(int species in new[]{0,1,2})
        {
            PetTownRanchLot lot;var s=Pair(species,out lot);var mother=lot.animals[0];var father=lot.animals[1];string prefix="species_"+species+"_";int gold=10000;
            test(prefix+"sex_selection",mother.sex==2&&father.sex==1);
            test(prefix+"start_and_reserve",s.StartBreeding(lot,mother.id,father.id,100,out reason)&&s.Reserved(lot,species)==1&&s.pregnancies[0].remainingDays==2&&mother.breedRestDays==3);
            test(prefix+"duplicate_parent_blocked",!s.StartBreeding(lot,mother.id,father.id,100,out reason)&&s.pregnancies.Count==1);
            test(prefix+"parent_sale_blocked",!s.SellAnimal(lot,mother.id,ref gold,out reason)&&gold==10000);
            if(species==2)test(prefix+"parent_meat_shipment_blocked",!s.ShipPig(lot,father.id)&&s.products[2]==0);
            while(s.BuyAnimalOfSex(lot,species,2,100,ref gold)){}
            test(prefix+"reserved_slot_not_sold",lot.animals.FindAll(a=>a.species==species).Count+s.Reserved(lot,species)==PetTownRanchState.Capacity[species]);
            s.AdvanceDay(101,new[]{lot.id});test(prefix+"first_care_day",s.pregnancies[0].remainingDays==1&&s.births.Count==0);s.AdvanceDay(101,new[]{lot.id});test(prefix+"same_day_no_double_progress",s.pregnancies[0].remainingDays==1&&mother.breedRestDays==2);
            var copy=JsonUtility.FromJson<PetTownRanchState>(JsonUtility.ToJson(s));copy.Normalize();var cl=copy.lots[0];test(prefix+"pending_save_roundtrip",copy.Reserved(cl,species)==1&&copy.pregnancies[0].remainingDays==1&&cl.animals[0].sex==2&&cl.animals[1].sex==1);
            Care(copy,cl,101);copy.AdvanceDay(102,new[]{cl.id});var baby=cl.animals.Find(a=>a.parentA==mother.id&&a.parentB==father.id);
            test(prefix+"birth_single_baby",baby!=null&&copy.pregnancies.Count==0&&copy.births.Count==1&&copy.Reserved(cl,species)==0&&cl.animals.Count==PetTownRanchState.Capacity[species]);
            test(prefix+"newborn_identity_parentage_size",baby.grown==0&&baby.generation==1&&baby.boughtDay==102&&Mathf.Approximately(baby.Size,.45f)&&(baby.sex==1||baby.sex==2));
            copy.AdvanceDay(102,new[]{cl.id});test(prefix+"birth_idempotent",copy.births.Count==1&&cl.animals.FindAll(a=>a.id==baby.id).Count==1);
            string childId=baby.id;var restored=JsonUtility.FromJson<PetTownRanchState>(JsonUtility.ToJson(copy));restored.Normalize();test(prefix+"birth_survives_restart",restored.births.Count==1&&restored.pregnancies.Count==0&&restored.lots[0].animals.Exists(a=>a.id==childId));
            Care(copy,cl,102);copy.AdvanceDay(103,new[]{cl.id});Care(copy,cl,103);copy.AdvanceDay(104,new[]{cl.id});test(prefix+"baby_grows_with_care",baby.Adult&&Mathf.Approximately(baby.Size,1));
            int before=gold,quote=PetTownRanchState.AnimalSellQuote(baby);test(prefix+"individual_sale_exact",copy.SellAnimal(cl,baby.id,ref gold,out reason)&&gold==before+quote&&!cl.animals.Exists(a=>a.id==baby.id));test(prefix+"duplicate_sale_blocked",!copy.SellAnimal(cl,baby.id,ref gold,out reason)&&gold==before+quote);
        }
        PetTownRanchLot pausedLot;var paused=Pair(2,out pausedLot);paused.StartBreeding(pausedLot,pausedLot.animals[0].id,pausedLot.animals[1].id,100,out reason);pausedLot.animals[1].wateredDay=-1;paused.AdvanceDay(101,new[]{pausedLot.id});test("missing_parent_water_pauses",paused.pregnancies[0].remainingDays==2);Care(paused,pausedLot,101);pausedLot.cleanliness=39;paused.AdvanceDay(102,new[]{pausedLot.id});test("dirty_lot_pauses",paused.pregnancies[0].remainingDays==2);Care(paused,pausedLot,102);paused.AdvanceDay(103,new[]{pausedLot.id});test("care_resumes_pending_birth",paused.pregnancies[0].remainingDays==1);
        var skip=JsonUtility.FromJson<PetTownRanchState>(JsonUtility.ToJson(paused));skip.Normalize();skip.AdvanceDay(140,new[]{pausedLot.id});test("skipped_days_no_birth_backfill",skip.pregnancies[0].remainingDays==1&&skip.births.Count==0);int previous=skip.lots[0].lastDay;skip.AdvanceDay(141,new string[0]);test("removed_ranch_not_simulated",skip.lots[0].lastDay==previous&&skip.pregnancies[0].remainingDays==1);
        int rest=pausedLot.animals[0].breedRestDays;test("cancel_releases_only_reservation",paused.CancelBreeding(pausedLot,paused.pregnancies[0].id)&&paused.Reserved(pausedLot,2)==0&&pausedLot.animals[0].breedRestDays==rest);test("cancel_twice_rejected",!paused.CancelBreeding(pausedLot,"missing"));
        PetTownRanchLot eligibility;var e=Pair(1,out eligibility);var a=eligibility.animals[0];var b=eligibility.animals[1];b.sex=2;test("same_sex_blocked",!e.StartBreeding(eligibility,a.id,b.id,100,out reason));b.sex=1;b.grown=1;test("young_parent_blocked",!e.StartBreeding(eligibility,a.id,b.id,100,out reason));b.grown=2;b.parentA=a.id;test("direct_relative_blocked",!e.StartBreeding(eligibility,a.id,b.id,100,out reason));b.parentA="same-parent";a.parentA="same-parent";test("shared_parent_blocked",!e.StartBreeding(eligibility,a.id,b.id,100,out reason));a.parentA=b.parentA=null;a.breedRestDays=1;test("parent_rest_blocked",!e.StartBreeding(eligibility,a.id,b.id,100,out reason));a.breedRestDays=0;eligibility.cleanliness=59;test("start_requires_clean_sixty",!e.StartBreeding(eligibility,a.id,b.id,100,out reason));
        var legacy=JsonUtility.FromJson<PetTownRanchState>("{\"feed\":4,\"products\":[2,3,4],\"lots\":[{\"id\":\"old\",\"lastDay\":100,\"cleanliness\":100,\"animals\":[{\"id\":\"cow\",\"name\":\"기존 젖소\",\"species\":0,\"grown\":2,\"ready\":1}]}]}");legacy.Normalize();test("legacy_cow_production_preserved",legacy.lots[0].animals[0].sex==2&&legacy.lots[0].animals[0].ready==1&&legacy.products[0]==2&&legacy.pregnancies.Count==0);
        int money=1000;test("ready_product_blocks_sale",!legacy.SellAnimal(legacy.lots[0],"cow",ref money,out reason)&&money==1000);legacy.Collect(legacy.lots[0],0);test("collect_before_sale",legacy.SellAnimal(legacy.lots[0],"cow",ref money,out reason)&&legacy.products[0]==3);
        var purchase=new PetTownRanchState();var pl=purchase.Get("purchase",1);int cash=1000;test("invalid_sex_no_charge",!purchase.BuyAnimalOfSex(pl,0,5,1,ref cash)&&cash==1000&&pl.animals.Count==0);purchase.BuyAnimalOfSex(pl,0,1,1,ref cash);purchase.BuyAnimalOfSex(pl,0,2,1,ref cash);foreach(var animal in pl.animals)animal.grown=2;purchase.feed=10;Care(purchase,pl,1);purchase.AdvanceDay(2,new[]{pl.id});test("only_female_cow_milk",pl.animals[0].ready==0&&pl.animals[1].ready==1);
        pl.animals[0].ready=0;int maximum=int.MaxValue;test("sale_gold_overflow_atomic",!purchase.SellAnimal(pl,pl.animals[0].id,ref maximum,out reason)&&pl.animals.Count==2&&maximum==int.MaxValue);
        test("hospital_state_regression",PetTownClinicHealthQA.Run().Count==41);
        return checks;
    }
}
#endif
