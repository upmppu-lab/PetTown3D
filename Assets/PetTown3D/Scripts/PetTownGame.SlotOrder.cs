using System.Collections.Generic;
using UnityEngine;

// 펫 주문은 펫샵에 놓은 자리(강아지 쿠션·고양이 펫타워·RAT 케이지·수족관)에 빈 정원이 있을 때만 가능합니다.
public partial class PetTownGame
{
    static readonly int[] SlotKinds = { 2, 3, 4, 7 };

    // 빈 자리 수. 자리가 하나도 없으면 필요한 가구 이름을 need에 돌려줍니다.
    int OrderableSlots(int species, out string need)
    {
        need = "자리";
        foreach (int kind in SlotKinds)
        {
            PetTownShopSlots.Rule rule;
            if (!PetTownShopSlots.TryRule(kind, out rule) || !PetTownShopSlots.Accepts(rule, species)) continue;
            need = rule.label;
            int capacity = 0;
            foreach (var f in ShopSlotFurniture()) if (f.Kind == kind) capacity += rule.capacity;
            int used = 0;
            if (Progression != null && Progression.care != null)
                foreach (var p in Progression.care.pets) if (p != null && !p.displayOnly && PetTownShopSlots.Accepts(rule, p.speciesKind)) used++;
            if (OperationsReady())
                foreach (var d in Operations.deliveries) if (d != null && d.IsPet && PetTownShopSlots.Accepts(rule, d.species)) used++;
            return Mathf.Max(0, capacity - used);
        }
        return 0;
    }
}
