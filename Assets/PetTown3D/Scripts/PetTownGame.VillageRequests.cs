using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class PetTownVillageRequest { public string who; public int item, amount, reward; public bool done; }

// 2D parity (village board 'vdeliver'): every day three residents ask for crops / eggs. Delivering pays more than selling and cheers the resident up.
public partial class PetTownGame
{
    const int EggItem = 7;
    static string RequestItemName(int item) { return item == EggItem ? "달걀" : PetTownFarmState.Names[Mathf.Clamp(item, 0, PetTownFarmState.CropCount - 1)]; }
    int RequestHave(int item)
    {
        var farm = Progression == null ? null : Progression.farm; if (farm == null) return 0;
        if (item == EggItem) return farm.poultry == null ? 0 : farm.poultry.eggs;
        return farm.produce != null && item >= 0 && item < farm.produce.Length ? farm.produce[item] : 0;
    }
    void EnsureVillageRequests()
    {
        if (loadingTown || Progression == null || !shop || Progression.farm == null || FarmRoots().Count == 0) return;
        if (Progression.requestDay == day && Progression.requests != null && Progression.requests.Count > 0) return;
        if (Progression.requests == null) Progression.requests = new List<PetTownVillageRequest>(); else Progression.requests.Clear();
        var names = new List<string>(); var state = EnsureResidents();
        foreach (var p in state.people) if (p != null && !string.IsNullOrEmpty(p.name)) names.Add(p.name);
        bool eggs = Progression.farm.poultry != null && Progression.farm.poultry.coops != null && Progression.farm.poultry.coops.Count > 0;
        var used = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            int item = -1;
            for (int tries = 0; tries < 8 && (item < 0 || used.Contains(item)); tries++)
            {
                int roll = UnityEngine.Random.Range(0, eggs ? PetTownFarmState.CropCount + 1 : PetTownFarmState.CropCount);
                item = roll >= PetTownFarmState.CropCount ? EggItem : roll;
            }
            used.Add(item);
            int unit = item == EggItem ? PetTownPoultryState.EggPrice : PetTownFarmState.SellValue(item, day);
            int amount = item == EggItem ? UnityEngine.Random.Range(3, 9) : UnityEngine.Random.Range(2, 6);
            string who = names.Count > 0 ? names[UnityEngine.Random.Range(0, names.Count)] : "이웃 주민";
            Progression.requests.Add(new PetTownVillageRequest { who = who, item = item, amount = amount, reward = Mathf.RoundToInt(unit * amount * 1.7f) + 40 });
        }
        Progression.requestDay = day; progressionDirty = true;
    }
    void DeliverVillageRequest(int index)
    {
        if (Progression == null || Progression.requests == null || index < 0 || index >= Progression.requests.Count) return;
        var r = Progression.requests[index];
        if (r.done) { Message("이미 전해 드린 부탁이에요."); return; }
        int have = RequestHave(r.item);
        if (have < r.amount) { Message(RequestItemName(r.item) + "이(가) 부족해요. (" + have + " / " + r.amount + ")"); return; }
        var farm = Progression.farm;
        if (r.item == EggItem) { farm.poultry.eggs -= r.amount; farm.poultry.revision++; } else { farm.produce[r.item] -= r.amount; farm.revision++; }
        r.done = true; money += r.reward + Progression.AddExperience(2);
        var person = EnsureResidents().people.Find(x => x != null && x.name == r.who);
        if (person != null) person.happiness = Mathf.Min(100f, person.happiness + 4f);
        progressionDirty = true; SaveProgressionSilent();
        Message(r.who + "님에게 " + RequestItemName(r.item) + " " + r.amount + "개를 전했어요! +" + r.reward + " 골드");
    }
    void AddVillageRequestEntries(List<PetTownCatalogEntry> entries)
    {
        EnsureVillageRequests();
        if (Progression == null || Progression.requests == null) return;
        int at = Mathf.Min(1, entries.Count);
        for (int i = 0; i < Progression.requests.Count; i++)
        {
            var r = Progression.requests[i]; int have = RequestHave(r.item);
            var e = OperationsEntry(1000 + i, r.who + "의 부탁", r.done ? "전달 완료" : RequestItemName(r.item) + " " + r.amount + "개", "보상 " + r.reward.ToString("N0") + " 골드",
                r.done ? "완료 확인" : have >= r.amount ? "전해 주기" : have + " / " + r.amount + " 모으는 중",
                r.who + "님이 " + RequestItemName(r.item) + " " + r.amount + "개를 구해 달래요.\n지금 " + have + "개 있어요. 그냥 파는 것보다 많이 받아요.\n부탁은 하루가 지나면 새로 바뀌어요.", 5);
            e.Category = "주민 부탁"; entries.Insert(at + i, e);
        }
    }
}
