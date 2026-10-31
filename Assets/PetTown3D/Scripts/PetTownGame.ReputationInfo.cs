using System.Collections.Generic;
using UnityEngine;

// 2D parity (repinfo panel): reputation grades with what each one gives. Opened by tapping the reputation chip in the top bar.
public partial class PetTownGame
{
    public void OpenReputationInfo()
    {
        SetMode(Mode.Play);
        var entries = new List<PetTownCatalogEntry>();
        int rep = reputation, now = PetTownReputation.GradeIndex(rep);
        for (int g = 4; g >= 0; g--)
        {
            string perk = g == 0 ? "기본 분양가" : "분양가 +" + (g * 4) + "%";
            if (g == 1) perk += " · 펫 카페 건설";
            if (g == 2) perk += " · 펫 병원 건설";
            string state = g < now ? "달성" : g == now ? "현재 등급" : (PetTownReputation.NeedOf(g) - rep) + " 더 필요";
            string desc = "필요 명성 " + PetTownReputation.NeedOf(g) + "\n혜택: " + perk;
            if (g == now)
            {
                int next = PetTownReputation.NextNeed(rep);
                desc += "\n\n지금 명성 " + rep + (next > 0 ? " · 다음 등급까지 " + (next - rep) : " · 최고 등급이에요!");
                desc += "\n명성 올리는 법: 펫 분양 +2~3, 펫 카페·병원 건설 +5, 공원 +2. 손님을 놓치면 -1이에요.";
            }
            entries.Add(OperationsEntry(g, PetTownReputation.NameOf(g) + " 등급", "명성 " + PetTownReputation.NeedOf(g) + " 이상", perk, state, desc, 5));
        }
        catalogWindow.Show("명성 등급", entries, e => { }, () => { }, () => { });
    }
}
