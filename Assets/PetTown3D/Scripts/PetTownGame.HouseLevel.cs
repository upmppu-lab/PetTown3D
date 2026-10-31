using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class PetTownHouseLevel { public string id; public int lv = 1; }

// 2D parity (v1.3): houses can be upgraded Lv1 -> Lv3, each level adds one resident slot (2 -> 3 -> 4).
public partial class PetTownGame
{
    readonly List<PetTownResidentHome> houseList = new List<PetTownResidentHome>();
    const int HouseBaseCost = 250;
    int HouseLv(string id)
    {
        if (Progression == null || Progression.houseLevels == null) return 1;
        var r = Progression.houseLevels.Find(x => x != null && x.id == id); return r == null ? 1 : Mathf.Clamp(r.lv, 1, 3);
    }
    static int HouseUpCost(int lv) { return Mathf.RoundToInt(HouseBaseCost * lv * .8f); }
    static int HouseUpNeed(int nextLv) { return nextLv >= 3 ? 16 : 6; }
    string HouseLevelInfo(string id)
    {
        int lv = HouseLv(id); string s = "주민 주택 · Lv." + lv + " · " + (1 + lv) + "명 정원";
        if (lv < 3) s += " · 다음: 주민 " + HouseUpNeed(lv + 1) + "명, " + HouseUpCost(lv).ToString("N0") + " 골드";
        return s;
    }
    void TryUpgradeHouse(PetTownResidentHome home)
    {
        if (home == null) return;
        int lv = HouseLv(home.id);
        if (lv >= 3) { Message("이미 최고 레벨이에요."); OpenResidentHousing(); return; }
        int need = HouseUpNeed(lv + 1), cost = HouseUpCost(lv);
        if (EnsureResidents().Population < need) { Message("주택 레벨업에는 주민이 " + need + "명 이상 필요해요. (지금 " + EnsureResidents().Population + "명)"); OpenResidentHousing(); return; }
        if (money < cost) { Message("골드가 부족해요. 필요한 금액: " + cost.ToString("N0") + " 골드"); OpenResidentHousing(); return; }
        money -= cost;
        if (Progression.houseLevels == null) Progression.houseLevels = new List<PetTownHouseLevel>();
        var r = Progression.houseLevels.Find(x => x != null && x.id == home.id);
        if (r == null) { r = new PetTownHouseLevel { id = home.id }; Progression.houseLevels.Add(r); }
        r.lv = lv + 1; progressionDirty = true; nextCapacityRead = 0f;
        UpgradeHouseModelById(home.id, lv + 1);
        ResidentHousingChanged(); SaveProgressionSilent(); SaveGame();
        Message(home.label + "을(를) Lv." + (lv + 1) + "로 키웠어요! 정원이 " + (2 + lv) + "명이 됐어요.");
        OpenResidentHousing();
    }
}
