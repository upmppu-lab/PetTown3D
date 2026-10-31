#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public static class PetTownCafeRecipesQA
{
    public static Dictionary<string,bool> Run()
    {
        var checks=new Dictionary<string,bool>();Action<string,bool> test=(key,ok)=>{checks[key]=ok;if(!ok)throw new Exception(key);};
        var s=new PetTownFacilityState{cafeIngredients=10,medicalSupplies=4};var farm=new PetTownFarmState();var ranch=new PetTownRanchState();farm.produce=new[]{3,3};ranch.products[0]=4;string reason;
        test("sandwich_batch",s.PrepareCafeRecipe(1,farm,ranch,out reason)&&s.CafeStock(1)==2&&s.cafeIngredients==8&&farm.produce[0]==2&&farm.produce[1]==2);
        test("pudding_batch",s.PrepareCafeRecipe(2,farm,ranch,out reason)&&s.CafeStock(2)==3&&s.cafeIngredients==5&&ranch.products[0]==2);
        test("only_used_state_revisions",farm.revision==1&&ranch.revision==1);
        string before=JsonUtility.ToJson(s)+JsonUtility.ToJson(farm)+JsonUtility.ToJson(ranch);
        test("invalid_menu_rejected",!s.PrepareCafeRecipe(-1,farm,ranch,out reason)&&!s.PrepareCafeRecipe(0,farm,ranch,out reason)&&!s.PrepareCafeRecipe(3,farm,ranch,out reason));
        test("invalid_menu_no_mutation",before==JsonUtility.ToJson(s)+JsonUtility.ToJson(farm)+JsonUtility.ToJson(ranch));
        var empty=new PetTownFarmState();test("missing_produce_atomic",!s.PrepareCafeRecipe(1,empty,ranch,out reason)&&s.cafeIngredients==5&&s.CafeStock(1)==2);
        test("missing_milk_atomic",!s.PrepareCafeRecipe(2,farm,new PetTownRanchState(),out reason)&&s.cafeIngredients==5&&s.CafeStock(2)==3);
        s.cafeRecipeSupplies[0]=9999;test("capacity_atomic",!s.PrepareCafeRecipe(1,farm,ranch,out reason)&&farm.produce[0]==2&&s.cafeIngredients==5);s.cafeRecipeSupplies[0]=2;
        var data=new PetTownProgressionState{stockInitialized=true,residentVersion=1,residents=new PetTownResidentState(),facilityVersion=1,facilities=s};
        for(int i=0;i<6;i++){var person=new PetTownResidentRecord{id="o"+i,name="검사 주민"};person.pets.Add(new PetTownResidentPetRecord{id="p"+i,name="검사 펫",lastCafeDay=-1,lastCheckupDay=-1});data.residents.people.Add(person);}
        var b=s.Get("cafeA",3);b.open=true;b.staffService=true;var b2=s.Get("cafeB",3);b2.open=true;
        test("menu_selection_validation",!s.SelectCafeMenu(s.Get("clinic",4),1)&&!s.SelectCafeMenu(new PetTownFacilityBusiness{kind=3},1)&&!s.SelectCafeMenu(b,3));
        s.SelectCafeMenu(b,1);test("snapshot_admit",s.Admit(data,b.id,3,"o0","p0","counter","table0",4,1,out reason)&&s.visits[0].cafeMenu==1&&s.AvailableCafe(1)==1);
        var v=s.visits[0];s.SelectCafeMenu(b,2);test("menu_switch_preserves_order",v.cafeMenu==1&&PetTownFacilityState.VisitFee(v)==110&&s.AvailableFor(b)==3);
        s.SelectCafeMenu(b2,1);test("shared_stock_second_cafe",s.Admit(data,b2.id,3,"o1","p1","counter","table1",4,1,out reason)&&s.AvailableCafe(1)==0);
        test("no_overbooking",!s.Admit(data,b2.id,3,"o2","p2","counter","table2",4,1,out reason));
        test("menu_pools_independent",s.AvailableCafe(0)==5&&s.AvailableCafe(2)==3);
        string second=s.visits[1].id;test("cancel_releases_reservation",s.Cancel(second)&&s.AvailableCafe(1)==1&&s.CafeStock(1)==2);
        test("begin_before_arrival_rejected",!s.Begin(v.id,out reason)&&s.CafeStock(1)==2);
        s.Arrive(v.id);s.Arrive(v.id);var chef=new PetTownEmployeeRecord{id="chef",role="chef",facilityId=b.id};
        test("staff_begin_uses_correct_menu",s.BeginWithStaff(v.id,chef,out reason)&&s.CafeStock(1)==1&&s.cafeIngredients==5&&s.CafeStock(2)==3);
        test("repeat_begin_no_double_spend",!s.BeginWithStaff(v.id,chef,out reason)&&s.CafeStock(1)==1);
        float remaining=v.remaining;s.TickVisit(v.id,2,true,false);test("staff_absence_pauses",v.remaining==remaining);
        string json=JsonUtility.ToJson(s);var loaded=JsonUtility.FromJson<PetTownFacilityState>(json);loaded.Normalize();var lv=loaded.visits[0];
        test("save_keeps_stock_menu_worker",loaded.CafeStock(1)==1&&loaded.Get(b.id,3).cafeMenu==2&&lv.cafeMenu==1&&lv.staffRequired&&lv.workerId==chef.id&&lv.remaining==remaining);
        test("resume_no_double_spend",loaded.ReclaimStaffWork(lv.id,chef)&&loaded.CafeStock(1)==1);
        loaded.TickVisit(lv.id,100,true,true);loaded.Arrive(lv.id);loaded.TickVisit(lv.id,100,true);loaded.Arrive(lv.id);int money=1000,income;
        test("payment_matches_order",loaded.TakePayment(data,lv.id,1,ref money,out income)&&income==110&&loaded.receipts[0].cafeMenu==1&&loaded.receipts[0].income==110);
        test("duplicate_payment_rejected",!loaded.TakePayment(data,lv.id,1,ref money,out income)&&loaded.receipts.Count==1&&data.Operations(1).Activity("cafe")==1);
        var reserve=new PetTownFacilityState{cafeIngredients=3};reserve.visits.Add(new PetTownFacilityVisit{kind=3,cafeMenu=0});reserve.visits.Add(new PetTownFacilityVisit{kind=3,cafeMenu=0});
        test("basic_reservation_protected",!reserve.PrepareCafeRecipe(1,farm,ranch,out reason)&&reserve.cafeIngredients==3&&farm.produce[0]==2);
        reserve.visits.Clear();test("basic_preparation_available",reserve.PrepareCafeRecipe(1,farm,ranch,out reason)&&reserve.cafeIngredients==1);
        var legacy=JsonUtility.FromJson<PetTownFacilityState>("{\"cafeIngredients\":7,\"businesses\":[{\"id\":\"old\",\"kind\":3}],\"visits\":[],\"receipts\":[]}");legacy.Normalize();
        test("legacy_migration_default",legacy.Get("old",3).cafeMenu==0&&legacy.CafeStock(0)==7&&legacy.CafeStock(1)==0&&legacy.CafeStock(2)==0);
        legacy.cafeRecipeSupplies=new[]{-3,20000,4};legacy.Get("old",3).cafeMenu=99;legacy.Normalize();test("normalize_invalid_saved_values",legacy.CafeStock(1)==0&&legacy.CafeStock(2)==10000&&legacy.Get("old",3).cafeMenu==0);
        test("clinic_supplies_unchanged",s.medicalSupplies==4&&s.AvailableSupplies(4)==4&&PetTownFacilityState.Fee(4)==140);
        var cancel=new PetTownFacilityState{cafeRecipeSupplies=new[]{1,0}};cancel.visits.Add(new PetTownFacilityVisit{id="used",kind=3,cafeMenu=1,stage=PetTownFacilityStage.Waiting});cancel.Begin("used",out reason);cancel.Cancel("used");test("cancel_after_cooking_no_refund",cancel.CafeStock(1)==0);
        return checks;
    }
}
#endif
