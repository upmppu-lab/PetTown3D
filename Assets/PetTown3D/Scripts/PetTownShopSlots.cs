using System.Collections.Generic;
using UnityEngine;

// 펫샵 전시 자리(케이지/쿠션/펫타워) 배정 규칙. 장면 의존성이 없는 순수 로직입니다.
// 자리는 실제로 배치된 가구(PetTownFurniturePlacement.Id)이며, 소속은 펫 기록의 slotId에 저장됩니다.
public static class PetTownShopSlots
{
    public struct Rule{public string label;public int capacity;public int[] species;}
    // detailKind: CreatePetShopDetail의 종류 번호 (2 RAT 케이지, 4 강아지 쿠션, 7 고양이 펫타워)
    public static bool TryRule(int detailKind,out Rule rule)
    {
        switch(detailKind)
        {
            case 2:rule=new Rule{label="RAT 케이지",capacity=2,species=new[]{2,3}};return true;
            case 4:rule=new Rule{label="강아지 쿠션",capacity=1,species=new[]{0}};return true;
            case 7:rule=new Rule{label="고양이 펫타워",capacity=2,species=new[]{1}};return true;
        }
        rule=default(Rule);return false;
    }
    public static bool Accepts(Rule rule,int species){if(rule.species==null)return false;foreach(var s in rule.species)if(s==species)return true;return false;}
    public static string SpeciesNames(Rule rule)
    {
        var names=new List<string>();if(rule.species!=null)foreach(var s in rule.species)names.Add(PetTownProgressionState.Species[Mathf.Clamp(s,0,PetTownProgressionState.Species.Length-1)]);
        return string.Join("·",names.ToArray());
    }
    public static string SpeciesLabel(Rule rule)
    {
        var names=new List<string>();if(rule.species!=null)foreach(var s in rule.species)names.Add(PetTownProgressionState.Species[Mathf.Clamp(s,0,PetTownProgressionState.Species.Length-1)]);
        return string.Join("·",names.ToArray())+" 전용 · 정원 "+rule.capacity;
    }
    public static List<PetTownPetCareRecord> Occupants(IEnumerable<PetTownPetCareRecord> pets,string slotId)
    {
        var result=new List<PetTownPetCareRecord>();if(pets==null||string.IsNullOrEmpty(slotId))return result;
        foreach(var p in pets)if(p!=null&&!p.displayOnly&&p.slotId==slotId)result.Add(p);return result;
    }
    public static bool CanAssign(PetTownPetCareRecord pet,string slotId,Rule rule,IEnumerable<PetTownPetCareRecord> pets,out string reason)
    {
        reason="";if(pet==null||string.IsNullOrEmpty(slotId)){reason="펫과 자리를 다시 선택해 주세요.";return false;}
        if(pet.displayOnly){reason="전시 동물은 자리에 배정하지 않아요.";return false;}
        if(pet.slotId==slotId){reason="이미 이 자리에 있어요.";return false;}
        if(!Accepts(rule,pet.speciesKind)){reason=SpeciesNames(rule)+"만 지낼 수 있어요.";return false;}
        int count=0;foreach(var other in Occupants(pets,slotId))if(other!=pet)count++;
        if(count>=rule.capacity){reason="자리가 가득 찼어요.";return false;}
        return true;
    }
    public static bool Assign(PetTownPetCareRecord pet,string slotId,Rule rule,IEnumerable<PetTownPetCareRecord> pets,out string reason)
    {
        if(!CanAssign(pet,slotId,rule,pets,out reason))return false;pet.slotId=slotId;return true;
    }
    public static bool Release(PetTownPetCareRecord pet){if(pet==null||string.IsNullOrEmpty(pet.slotId))return false;pet.slotId="";return true;}
    // 사라진 가구에 묶인 배정을 자유 활동으로 되돌립니다. 되돌린 펫 수를 반환합니다.
    public static int Prune(IEnumerable<PetTownPetCareRecord> pets,ICollection<string> validSlotIds)
    {
        int count=0;if(pets==null)return 0;
        foreach(var p in pets)if(p!=null&&!string.IsNullOrEmpty(p.slotId)&&(validSlotIds==null||!validSlotIds.Contains(p.slotId))){p.slotId="";count++;}
        return count;
    }
    // 자리 주변 고리(반경 .75~1.2m) 위의 한 점. u,v는 0~1 난수입니다.
    public static Vector3 RoamPoint(Vector3 center,float u,float v)
    {
        float angle=Mathf.Clamp01(u)*Mathf.PI*2f,radius=.75f+.45f*Mathf.Clamp01(v);
        return center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
    }
}
