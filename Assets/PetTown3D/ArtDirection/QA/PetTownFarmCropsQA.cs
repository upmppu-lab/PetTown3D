#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public static class PetTownFarmCropsQA
{
    public static Dictionary<string,bool> Run()
    {
        var checks=new Dictionary<string,bool>();Action<string,bool> test=(key,ok)=>{checks[key]=ok;if(!ok)throw new Exception(key);};
        var f=new PetTownFarmState();var lot=f.Get("farm",100);int gold=10000;
        test("seven_definitions",PetTownFarmState.CropCount==7&&PetTownFarmState.Names.Length==7&&PetTownFarmState.GrowDays.Length==7&&f.seeds.Length==7&&f.produce.Length==7);
        for(int crop=0;crop<PetTownFarmState.CropCount;crop++)
        {
            string key="crop_"+crop+"_";int before=gold;
            test(key+"buy_exact",f.BuySeeds(crop,5,ref gold)&&gold==before-PetTownFarmState.SeedPrice[crop]*5&&f.seeds[crop]==5);
            test(key+"plant_exact",f.Plant(lot,crop,crop)&&f.seeds[crop]==4&&lot.plots[crop].crop==crop);
            test(key+"occupied_plot_no_spend",!f.Plant(lot,crop,crop)&&f.seeds[crop]==4);
            test(key+"harvest_requires_growth",!f.Harvest(lot,crop,out _));
            test(key+"water_once",f.Water(lot,crop,100)&&!f.Water(lot,crop,100));
        }
        for(int day=101;day<=104;day++)
        {
            f.AdvanceDay(day);int[] stages=lot.plots.ConvertAll(p=>p.grown).ToArray();f.AdvanceDay(day);test("day_"+day+"_no_double_growth",Same(stages,lot.plots.ConvertAll(p=>p.grown).ToArray()));
            for(int i=0;i<7;i++)f.Water(lot,i,day);
        }
        for(int crop=0;crop<7;crop++)
        {
            string key="crop_"+crop+"_";test(key+"growth_days",lot.plots[crop].grown==PetTownFarmState.GrowDays[crop]);
            test(key+"harvest_exact",f.Harvest(lot,crop,out int harvested)&&harvested==crop&&f.produce[crop]==1&&lot.plots[crop].crop==-1);
            test(key+"duplicate_harvest_rejected",!f.Harvest(lot,crop,out _));int before=gold;
            test(key+"sell_exact",f.Sell(crop,1,ref gold)&&gold==before+PetTownFarmState.SellPrice[crop]&&f.produce[crop]==0);test(key+"duplicate_sale_rejected",!f.Sell(crop,1,ref gold));
        }
        var old=JsonUtility.FromJson<PetTownFarmState>("{\"version\":1,\"seeds\":[8,9],\"produce\":[6,7],\"lots\":[{\"id\":\"old\",\"lastDay\":150,\"staffService\":true,\"plots\":[{\"crop\":1,\"grown\":2,\"wateredDay\":150}]}]}");old.Normalize();
        test("legacy_stock_preserved",old.seeds.Length==7&&old.produce.Length==7&&old.seeds[0]==8&&old.seeds[1]==9&&old.produce[0]==6&&old.produce[1]==7&&old.seeds[2]==0&&old.produce[6]==0);
        test("legacy_plot_staff_preserved",old.lots[0].id=="old"&&old.lots[0].staffService&&old.lots[0].plots.Count==9&&old.lots[0].plots[0].crop==1&&old.lots[0].plots[0].grown==2&&old.lots[0].plots[0].wateredDay==150);
        var loaded=JsonUtility.FromJson<PetTownFarmState>(JsonUtility.ToJson(f));loaded.Normalize();test("new_stock_roundtrip",Same(loaded.seeds,f.seeds)&&Same(loaded.produce,f.produce));
        int held=gold;test("invalid_crop_no_spend",!f.BuySeeds(-1,5,ref gold)&&!f.BuySeeds(7,5,ref gold)&&!f.Plant(lot,0,99)&&gold==held);
        f.seeds[2]=10000;test("seed_capacity_no_charge",!f.BuySeeds(2,5,ref gold)&&gold==held);
        f.seeds[2]=1;f.Plant(lot,0,2);lot.plots[0].grown=2;f.produce[2]=10000;test("full_storage_keeps_crop",!f.Harvest(lot,0,out _)&&lot.plots[0].crop==2&&f.produce[2]==10000);
        f.produce[2]=1;int maximum=int.MaxValue;test("sale_overflow_atomic",!f.Sell(2,1,ref maximum)&&f.produce[2]==1&&maximum==int.MaxValue);
        var dry=new PetTownFarmState();var dl=dry.Get("dry",10);dry.seeds[5]=1;dry.Plant(dl,0,5);dry.AdvanceDay(11);test("new_crop_requires_water",dl.plots[0].grown==0);dry.Water(dl,0,11);dry.AdvanceDay(20);test("skipped_days_no_growth_backfill",dl.plots[0].grown==0);dry.Water(dl,0,20);dry.AdvanceDay(21);test("care_resumes_new_crop",dl.plots[0].grown==1);
        var grains=new PetTownFarmState();grains.produce[3]=3;grains.produce[4]=3;var poultry=new PetTownPoultryState();var ranch=new PetTownRanchState();
        test("corn_to_poultry",grains.MakePoultryFeed(3,poultry)&&poultry.feed==20&&grains.produce[3]==2&&ranch.feed==0);
        test("wheat_to_poultry",grains.MakePoultryFeed(4,poultry)&&poultry.feed==30&&grains.produce[4]==2);
        test("corn_to_ranch",grains.MakeRanchFeed(3,ranch)&&ranch.feed==20&&grains.produce[3]==1&&poultry.feed==30);
        test("wheat_to_ranch",grains.MakeRanchFeed(4,ranch)&&ranch.feed==30&&grains.produce[4]==1);
        string beforeFeed=JsonUtility.ToJson(grains);test("invalid_grain_atomic",!grains.MakeRanchFeed(2,ranch)&&!grains.MakePoultryFeed(6,poultry)&&beforeFeed==JsonUtility.ToJson(grains));ranch.feed=9990;
        test("feed_capacity_atomic",!grains.MakeRanchFeed(3,ranch)&&ranch.feed==9990&&grains.produce[3]==1);poultry.feed=9995;test("poultry_capacity_atomic",!grains.MakePoultryFeed(4,poultry)&&poultry.feed==9995&&grains.produce[4]==1);
        grains.produce[0]=3;grains.produce[1]=3;grains.produce[5]=8;var cafe=new PetTownFacilityState();test("old_cafe_conversion_keeps_new_crops",grains.MakeIngredients(cafe,5)&&cafe.cafeIngredients==5&&grains.produce[0]==0&&grains.produce[1]==1&&grains.produce[5]==8);
        test("ranch_breeding_regression",PetTownRanchBreedingQA.Run().Count==66);
        return checks;
    }
    static bool Same(int[] a,int[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
}
#endif
