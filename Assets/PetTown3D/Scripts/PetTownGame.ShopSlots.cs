using System.Collections.Generic;
using UnityEngine;

// 펫샵 전시 자리: 실제로 배치한 RAT 케이지·강아지 쿠션·고양이 펫타워에 입고 펫을 배정합니다.
public partial class PetTownGame
{
    List<PetTownFurniturePlacement> ShopSlotFurniture()
    {
        var result=new List<PetTownFurniturePlacement>();if(!shop)return result;PetTownShopSlots.Rule rule;
        foreach(var f in shop.GetComponentsInChildren<PetTownFurniturePlacement>())if(f.Owner&&f.Owner.Kind==0&&f.Detail&&f.gameObject.activeInHierarchy&&PetTownShopSlots.TryRule(f.Kind,out rule))result.Add(f);
        result.Sort((a,b)=>string.CompareOrdinal(a.Id,b.Id));return result;
    }
    string ShopSlotLabel(List<PetTownFurniturePlacement> slots,int index)
    {
        PetTownShopSlots.Rule rule;PetTownShopSlots.TryRule(slots[index].Kind,out rule);int ordinal=1;for(int i=0;i<index;i++)if(slots[i].Kind==slots[index].Kind)ordinal++;return rule.label+" "+ordinal;
    }
    // 배정된 펫이 가구 주변에서 지내도록 이동 목표의 중심을 돌려줍니다. 배정이 없거나 가구가 사라졌으면 false(기존 자유 활동).
    bool TryShopSlotAnchor(LivePet live,out Vector3 anchor)
    {
        anchor=Vector3.zero;if(live==null||string.IsNullOrEmpty(live.careId)||!shop||Progression==null||Progression.care==null)return false;
        var care=Progression.care.pets.Find(p=>p!=null&&p.id==live.careId);if(care==null||care.displayOnly||string.IsNullOrEmpty(care.slotId))return false;
        var slot=ShopSlotFurniture().Find(f=>f.Id==care.slotId);if(!slot)return false;anchor=slot.transform.position;return true;
    }
    // 펫 정보 창에 보여 줄 현재 자리 이름. 배정이 없거나 가구가 사라졌으면 빈 문자열.
    string ShopSlotName(PetTownPetCareRecord pet)
    {
        if(pet==null||pet.displayOnly||string.IsNullOrEmpty(pet.slotId))return "";var slots=ShopSlotFurniture();int index=slots.FindIndex(f=>f.Id==pet.slotId);return index<0?"":ShopSlotLabel(slots,index);
    }
    void OpenPetSlots()
    {
        SetMode(Mode.Play);if(!OperationsReady())return;var slots=ShopSlotFurniture();var entries=new List<PetTownCatalogEntry>();var ids=new List<string>();var pets=Progression.care.pets;
        var valid=new List<string>();foreach(var f in slots)valid.Add(f.Id);if(PetTownShopSlots.Prune(pets,valid)>0){progressionDirty=true;SaveProgressionSilent();}
        for(int i=0;i<slots.Count;i++)
        {
            PetTownShopSlots.Rule rule;PetTownShopSlots.TryRule(slots[i].Kind,out rule);var occupants=PetTownShopSlots.Occupants(pets,slots[i].Id);string names=occupants.Count==0?"비어 있어요":string.Join(", ",occupants.ConvertAll(p=>p.nickname).ToArray());
            ids.Add(slots[i].Id);entries.Add(OperationsEntry(i,ShopSlotLabel(slots,i),"입주 "+occupants.Count+" / "+rule.capacity+(DirtyKind(slots[i].Kind)?" · 오염 "+Mathf.RoundToInt(HabDirt(slots[i].Id))+"%":""),PetTownShopSlots.SpeciesLabel(rule),"자리 관리",names+"\n배정한 펫은 이 가구 주변에서 지내요. 가구를 치우면 자유 활동으로 돌아갑니다.",9));
        }
        if(entries.Count==0)entries.Add(OperationsEntry(-1,"배치된 전시 자리가 없어요","가구 목록 > 펫 시설","케이지·쿠션·펫타워","펫샵 관리","매장 안에 RAT 케이지(토끼·페럿), 강아지 쿠션, 고양이 펫타워를 놓으면 펫을 배정할 수 있어요.",9));
        catalogWindow.Show("펫 전시 자리",entries,e=>{if(e.Key<0){OpenShopManagement();return;}if(e.Key<ids.Count)OpenPetSlot(ids[e.Key]);},()=>{},()=>{});
    }
    void OpenPetSlot(string slotId)
    {
        if(!OperationsReady())return;var slots=ShopSlotFurniture();int index=slots.FindIndex(x=>x.Id==slotId);if(index<0){Message("자리가 사라졌어요. 목록을 다시 확인해 주세요.");OpenPetSlots();return;}
        PetTownShopSlots.Rule rule;PetTownShopSlots.TryRule(slots[index].Kind,out rule);string title=ShopSlotLabel(slots,index);var pets=Progression.care.pets;var occupants=PetTownShopSlots.Occupants(pets,slotId);
        var entries=new List<PetTownCatalogEntry>();var occupantIds=new List<string>();var candidateIds=new List<string>();
        foreach(var p in occupants){occupantIds.Add(p.id);entries.Add(OperationsEntry(100+occupantIds.Count-1,p.nickname,PetTownProgressionState.Species[Mathf.Clamp(p.speciesKind,0,PetTownProgressionState.Species.Length-1)]+" · "+p.SexLabel,"이 자리에서 지내는 중","내보내기","내보내면 매장 안을 자유롭게 돌아다녀요.",9));}
        foreach(var p in pets)
        {
            if(p==null||p.displayOnly||p.slotId==slotId)continue;string why;bool ok=PetTownShopSlots.CanAssign(p,slotId,rule,pets,out why);candidateIds.Add(p.id);
            entries.Add(OperationsEntry(200+candidateIds.Count-1,p.nickname,PetTownProgressionState.Species[Mathf.Clamp(p.speciesKind,0,PetTownProgressionState.Species.Length-1)]+" · "+(string.IsNullOrEmpty(p.slotId)?"자유 활동":"다른 자리"),ok?"넣을 수 있어요":why,ok?"넣기":"확인",ok?p.nickname+"을(를) "+title+"에 넣어요. 다른 자리에 있었다면 옮겨집니다.":why,9));
        }
        if(DirtyKind(slots[index].Kind))entries.Add(OperationsEntry(8,"우리 청소하기","오염 "+Mathf.RoundToInt(HabDirt(slotId))+"%","청소하면 0%","청소하기","우리가 더러우면 안에 있는 펫이 스트레스를 받아요. 가까이 가거나 여기서 청소할 수 있고, 직원도 치워 줘요.",1));
        entries.Add(OperationsEntry(7,"자리 목록으로 돌아가기","입주 "+occupants.Count+" / "+rule.capacity,PetTownShopSlots.SpeciesLabel(rule),"목록 보기","펫 전시 자리 목록으로 돌아갑니다.",9));
        catalogWindow.Show("펫 전시 · "+title,entries,e=>
        {
            if(e.Key==7){OpenPetSlots();return;}if(e.Key==8){SetHabDirt(slotId,0f);money+=Progression.AddExperience(1);SaveProgressionSilent();Message("우리를 깨끗하게 청소했어요. +1 XP");OpenPetSlot(slotId);return;}if(!OperationsReady())return;var current=Progression.care.pets;bool done=false;string note="";string reason="";
            if(e.Key>=100&&e.Key<200){int i=e.Key-100;var p=i<occupantIds.Count?current.Find(x=>x!=null&&x.id==occupantIds[i]):null;if(p!=null&&PetTownShopSlots.Release(p)){done=true;note=p.nickname+"이(가) 자유 활동으로 돌아갔어요.";}}
            else if(e.Key>=200&&e.Key<300){int i=e.Key-200;var p=i<candidateIds.Count?current.Find(x=>x!=null&&x.id==candidateIds[i]):null;if(p!=null){if(PetTownShopSlots.Assign(p,slotId,rule,current,out reason)){done=true;Operations.Record("slot");note=p.nickname+"이(가) "+title+"에 자리잡았어요.";}else note=reason;}}
            if(done){progressionDirty=true;SaveProgressionSilent();}if(note!="")Message(note);OpenPetSlot(slotId);
        },()=>{},()=>{});
    }
}
