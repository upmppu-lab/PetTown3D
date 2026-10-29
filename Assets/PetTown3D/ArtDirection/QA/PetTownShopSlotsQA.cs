#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

// 펫샵 전시 자리 배정 규칙 순수 검사. 장면/PlayerPrefs/실제 저장을 건드리지 않습니다.
public static class PetTownShopSlotsQA
{
    static PetTownPetCareRecord Pet(string id,int species,bool display=false)=>new PetTownPetCareRecord{id=id,nickname=id,speciesKind=species,displayOnly=display,petLevel=PetTownPetCareRecord.AdultLevel,growthRulesVersion=1};
    public static Dictionary<string,bool> Run()
    {
        var checks=new Dictionary<string,bool>();Action<string,bool> test=(k,v)=>{checks[k]=v;if(!v)throw new Exception(k);};string reason;PetTownShopSlots.Rule cage,cushion,tower,none;
        test("rule_cage",PetTownShopSlots.TryRule(2,out cage)&&cage.capacity==2&&PetTownShopSlots.Accepts(cage,2)&&PetTownShopSlots.Accepts(cage,3));
        test("rule_cushion",PetTownShopSlots.TryRule(4,out cushion)&&cushion.capacity==1&&PetTownShopSlots.Accepts(cushion,0));
        test("rule_tower",PetTownShopSlots.TryRule(7,out tower)&&tower.capacity==2&&PetTownShopSlots.Accepts(tower,1));
        test("rule_other_furniture_not_slots",!PetTownShopSlots.TryRule(1,out none)&&!PetTownShopSlots.TryRule(3,out none)&&!PetTownShopSlots.TryRule(6,out none)&&!PetTownShopSlots.TryRule(8,out none)&&!PetTownShopSlots.TryRule(9,out none));
        test("species_filter",!PetTownShopSlots.Accepts(cage,0)&&!PetTownShopSlots.Accepts(cage,1)&&!PetTownShopSlots.Accepts(cushion,1)&&!PetTownShopSlots.Accepts(tower,2));
        test("species_label",PetTownShopSlots.SpeciesLabel(cage).Contains("토끼")&&PetTownShopSlots.SpeciesLabel(cage).Contains("정원 2"));

        var pets=new List<PetTownPetCareRecord>{Pet("rabbit",2),Pet("ferret",3),Pet("rabbit2",2),Pet("dog",0),Pet("cat",1),Pet("cat2",1),Pet("cat3",1),Pet("show",2,true)};
        test("assign_rabbit",PetTownShopSlots.Assign(pets[0],"cageA",cage,pets,out reason)&&pets[0].slotId=="cageA");
        test("assign_second_ferret",PetTownShopSlots.Assign(pets[1],"cageA",cage,pets,out reason));
        test("capacity_blocks_third",!PetTownShopSlots.Assign(pets[2],"cageA",cage,pets,out reason)&&pets[2].slotId==null&&reason.Contains("가득"));
        test("occupants_two",PetTownShopSlots.Occupants(pets,"cageA").Count==2);
        test("same_slot_rejected",!PetTownShopSlots.CanAssign(pets[0],"cageA",cage,pets,out reason));
        test("wrong_species_rejected",!PetTownShopSlots.Assign(pets[3],"cageA",cage,pets,out reason)&&pets[3].slotId==null&&reason.Contains("만 지낼"));
        test("display_animal_rejected",!PetTownShopSlots.Assign(pets[7],"cageA",cage,pets,out reason)&&reason.Contains("전시"));
        test("release_then_third_fits",PetTownShopSlots.Release(pets[0])&&PetTownShopSlots.Assign(pets[2],"cageA",cage,pets,out reason)&&PetTownShopSlots.Occupants(pets,"cageA").Count==2);
        test("release_twice_false",!PetTownShopSlots.Release(pets[0]));
        // 다른 자리로 이동하면 원래 자리가 비워짐
        test("move_between_slots",PetTownShopSlots.Assign(pets[1],"cageB",cage,pets,out reason)&&PetTownShopSlots.Occupants(pets,"cageA").Count==1&&PetTownShopSlots.Occupants(pets,"cageB").Count==1);
        test("cushion_capacity_one",PetTownShopSlots.Assign(pets[3],"cushion1",cushion,pets,out reason)&&!PetTownShopSlots.Assign(Pet("dog2",0),"cushion1",cushion,pets,out reason));
        test("tower_two_cats",PetTownShopSlots.Assign(pets[4],"tower1",tower,pets,out reason)&&PetTownShopSlots.Assign(pets[5],"tower1",tower,pets,out reason)&&!PetTownShopSlots.Assign(pets[6],"tower1",tower,pets,out reason));
        test("null_inputs_safe",!PetTownShopSlots.CanAssign(null,"x",cage,pets,out reason)&&!PetTownShopSlots.CanAssign(pets[2],"",cage,pets,out reason)&&PetTownShopSlots.Occupants(null,"x").Count==0&&!PetTownShopSlots.Release(null));

        // 사라진 가구 정리
        int before=PetTownShopSlots.Occupants(pets,"cageB").Count+PetTownShopSlots.Occupants(pets,"cushion1").Count+PetTownShopSlots.Occupants(pets,"tower1").Count;
        int freed=PetTownShopSlots.Prune(pets,new List<string>{"cushion1","tower1"});
        test("prune_removed_slot_only",freed==2&&PetTownShopSlots.Occupants(pets,"cageB").Count==0&&PetTownShopSlots.Occupants(pets,"cageA").Count==0&&PetTownShopSlots.Occupants(pets,"cushion1").Count==1&&PetTownShopSlots.Occupants(pets,"tower1").Count==2&&before==4);
        test("prune_null_list_frees_all",PetTownShopSlots.Prune(pets,null)==3&&PetTownShopSlots.Occupants(pets,"cushion1").Count==0);

        // 저장 호환
        var legacy=JsonUtility.FromJson<PetTownPetCareState>("{\"version\":2,\"pets\":[{\"id\":\"old\",\"nickname\":\"기존\",\"speciesKind\":2,\"petLevel\":3,\"growthRulesVersion\":1}]}");legacy.pets[0].Normalize();
        test("legacy_record_no_slot",legacy.pets[0].slotId==""&&legacy.pets[0].nickname=="기존"&&legacy.pets[0].petLevel==3);
        var saved=new PetTownPetCareState();saved.pets.Add(Pet("a",3));saved.pets[0].slotId="cageZ";var copy=JsonUtility.FromJson<PetTownPetCareState>(JsonUtility.ToJson(saved));copy.pets[0].Normalize();
        test("json_roundtrip_slot",copy.pets[0].slotId=="cageZ"&&copy.pets[0].speciesKind==3);
        var show=Pet("s",0,true);show.slotId="ghost";show.Normalize();test("display_pet_slot_cleared",show.slotId=="");

        // 이동 목표: 자리 주변 고리 안
        bool ring=true;var c=new Vector3(10,2,-3);for(int i=0;i<=10;i++)for(int j=0;j<=10;j++){var p=PetTownShopSlots.RoamPoint(c,i/10f,j/10f);float d=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(c.x,c.z));if(d<.749f||d>1.201f||!Mathf.Approximately(p.y,c.y))ring=false;}
        test("roam_point_ring_around_slot",ring);test("roam_point_clamped",Vector3.Distance(PetTownShopSlots.RoamPoint(c,-5,9),PetTownShopSlots.RoamPoint(c,0,1))<.0001f);
        return checks;
    }
}
#endif
