using UnityEngine;

// 주문(손님 메시지)은 실제로 펫샵 안에 구경 온 주민이 있을 때만 생긴다.
public partial class PetTownGame
{
    GameObject orderVisitor, lastOrderVisitor;
    string orderVisitorName;
    float orderNoVisitorTime;

    GameObject FindShopVisitor()
    {
        foreach (var pair in villagerWander)
        {
            if (pair.Key == null || !pair.Key.activeInHierarchy || pair.Value == null) continue;
            if (pair.Value.goal != VillagerGoal.AtShop) continue;
            if (Vector3.Distance(pair.Key.transform.position, shopPosition) > 12f) continue;
            return pair.Key;
        }
        return null;
    }

    string ResidentNameOf(GameObject actor)
    {
        if (actor == null) return null;
        foreach (var pair in residentActors)
            if (pair.Value == actor)
            {
                var person = EnsureResidents().people.Find(p => p.id == pair.Key);
                return person != null ? person.name : null;
            }
        return null;
    }
}
