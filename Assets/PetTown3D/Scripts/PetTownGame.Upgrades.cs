using System.Collections.Generic;
using UnityEngine;

// 2D parity: shop automation upgrades (auto feeder / auto cleaner).
public partial class PetTownGame
{
    const int FeederCost = 1800, FeederLevel = 5, CleanerCost = 1500, CleanerLevel = 4;
    void OpenShopUpgrades()
    {
        SetMode(Mode.Play);
        var entries = new List<PetTownCatalogEntry>();
        bool f = Progression.autoFeeder, c = Progression.autoCleaner;
        entries.Add(OperationsEntry(0, "자동 급식기", f ? "설치 완료" : FeederCost.ToString("N0") + " 골드 · Lv." + FeederLevel, "배고픔 35% 미만이면 자동으로 사료", f ? "확인" : "설치하기", "가방에 사료가 있으면 배고픈 펫에게 알아서 먹이를 줘요.\n사료가 떨어지면 멈춰요.", 2));
        entries.Add(OperationsEntry(1, "자동 청소기", c ? "설치 완료" : CleanerCost.ToString("N0") + " 골드 · Lv." + CleanerLevel, "청결 35% 미만이면 자동 청소", c ? "확인" : "설치하기", "더러워진 펫을 알아서 깨끗하게 해 줘요.", 1));
        catalogWindow.Show("펫샵 자동 설비", entries, e =>
        {
            bool has = e.Key == 0 ? Progression.autoFeeder : Progression.autoCleaner;
            int cost = e.Key == 0 ? FeederCost : CleanerCost, need = e.Key == 0 ? FeederLevel : CleanerLevel;
            if (has) { Message("이미 설치했어요."); return; }
            if (Progression.playerLevel < need) { Message("플레이어 Lv." + need + " 이상일 때 설치할 수 있어요."); return; }
            if (money < cost) { Message("골드가 부족해요. 필요한 금액: " + cost.ToString("N0") + " 골드"); return; }
            money -= cost;
            if (e.Key == 0) Progression.autoFeeder = true; else Progression.autoCleaner = true;
            progressionDirty = true; SaveProgressionSilent();
            Message((e.Key == 0 ? "자동 급식기" : "자동 청소기") + "를 설치했어요!");
            OpenShopUpgrades();
        }, () => { }, () => { });
    }
}
