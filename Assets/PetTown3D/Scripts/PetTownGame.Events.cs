using System.Collections.Generic;
using UnityEngine;

// 2D parity: random town events. Police station blocks thieves, library / hotel attract donors, chapel hosts weddings.
public partial class PetTownGame
{
    bool DecorOwned(int defId) { return decorTags.Exists(t => t && t.defId == defId); }

    void TownEventsNewDay()
    {
        if (loadingTown || Progression == null || !shop) return;
        var state = EnsureResidents(); int pop = state.Population;
        if (pop < 8) return;
        var lines = new List<string>();
        bool police = DecorOwned(409), fire = DecorOwned(410), library = DecorOwned(411), hotel = DecorOwned(414), chapel = DecorOwned(416);
        if (Random.value < .12f)
        {
            if (police && Random.value < .7f) lines.Add("경찰서 덕분에 도둑을 막았어요!");
            else
            {
                int loss = Mathf.Min(money, Mathf.Clamp(Mathf.RoundToInt(money * .02f), 20, 600));
                if (loss > 0) { money -= loss; lines.Add("밤사이 도둑이 들어 " + loss.ToString("N0") + " 골드를 잃었어요." + (police ? "" : " 경찰서를 지으면 막을 수 있어요.")); }
            }
        }
        if (Random.value < .06f)
        {
            float drop = fire ? 1f : 2.5f;
            foreach (var p in state.people) p.happiness = Mathf.Max(0f, p.happiness - drop);
            lines.Add(fire ? "작은 불이 났지만 소방서가 금방 껐어요." : "마을에 작은 불이 나서 주민들이 놀랐어요. 소방서를 지으면 피해가 줄어요.");
        }
        if (library && Random.value < .15f) { int gold = 100 + pop * 5; money += gold; lines.Add("수집가가 도서관을 구경하고 기부금 " + gold.ToString("N0") + " 골드를 남겼어요."); }
        if (hotel && Random.value < .2f) { int gold = 150 + pop * 4; money += gold; lines.Add("펫 호텔에 부자 손님이 묵고 " + gold.ToString("N0") + " 골드를 냈어요."); }
        if (chapel && Random.value < .1f)
        {
            foreach (var p in state.people) p.happiness = Mathf.Min(100f, p.happiness + 3f);
            money += 200; lines.Add("웨딩 채플에서 결혼식이 열렸어요! 모두 행복해요. 축의금 +200 골드");
        }
        if (lines.Count == 0) return;
        progressionDirty = true;
        Message(string.Join(" / ", lines.ToArray()));
    }
}
