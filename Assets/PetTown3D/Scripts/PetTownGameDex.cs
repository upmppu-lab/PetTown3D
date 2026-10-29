using System.Collections.Generic;
using UnityEngine;

// Encyclopedia with many sections: pets, zoo animals, crops, livestock and the jobs of the townsfolk.
// Everything is derived from what is actually in the town, so nothing new needs saving.
public partial class PetTownGame
{
    static readonly string[] ZooNotes =
    {
        "숲속 초원을 가볍게 달리는 온순한 동물이에요.", "위풍당당한 숲의 왕이에요. 먹이를 잘 챙겨 주세요.", "뒤뚱뒤뚱 걷는 모습이 귀여운 친구예요.",
        "커다란 몸집과 긴 코가 특징인 온순한 동물이에요.", "높은 나뭇잎도 쉽게 먹는 키다리 동물이에요.", "멋진 줄무늬를 가진 초원의 달리기 선수예요."
    };
    static readonly string[] CropNotes =
    {
        "아삭한 맛이 일품인 기본 채소예요.", "여름 햇살을 듬뿍 받고 자라는 붉은 열매예요.", "땅속에서 단맛이 드는 뿌리채소예요.",
        "알알이 노랗게 여무는 곡식이에요.", "가을 들판을 황금빛으로 물들이는 곡식이에요.", "늦가을에 크게 자라는 둥근 열매예요.", "든든한 간식이 되는 알뿌리예요."
    };
    static readonly string[] RanchNotes =
    {
        "느긋하게 풀을 먹으며 신선한 우유를 만들어요.", "포근한 털을 이틀마다 모아 둘 수 있어요.", "무럭무럭 자라 고기를 얻을 수 있어요."
    };
    static readonly string[] JobNotes =
    {
        "매일 성실하게 출근하는 직장인이에요.", "아이들에게 지식을 가르치는 선생님이에요.", "맛있는 음식으로 마을 사람들을 기쁘게 해요.", "마을 풍경을 캔버스에 담아요.",
        "신선한 작물을 길러 내는 농부예요.", "아픈 사람과 펫을 따뜻하게 돌봐요.", "밤늦게까지 코드를 짜는 개발자예요.", "마을 이야기를 글로 남겨요.",
        "아름다운 순간을 사진으로 남겨요.", "마을 곳곳에 물건을 배달해요.", "마을 광장에서 선율을 들려줘요.", "마을의 안전을 지키는 든든한 소방관이에요.",
        "고소한 빵 냄새로 아침을 열어요.", "병원에서 펫 진료를 도와요.", "책 속에서 마을의 역사를 지켜요.", "공원과 꽃밭을 가꿔요."
    };

    void OpenPetEncyclopedia()
    {
        SetMode(Mode.Play);
        var entries = new List<PetTownCatalogEntry>();
        var state = EnsureResidents();
        int key = 0, known = 0, total = 0;
        string[] petNotes = { "산책과 놀이를 좋아하는 강아지예요.", "느긋한 휴식과 깨끗한 공간을 좋아해요.", "조용한 공간에서 편안히 쉬게 해 주세요.", "호기심이 많고 활발한 페럿이에요." };
        for (int i = 0; i < PetTownProgressionState.Species.Length; i++)
        {
            bool found = Operations.discovered[i]; total++; if (found) known++;
            var entry = OperationsEntry(key++, found ? PetTownProgressionState.Species[i] : "아직 만나지 못했어요", found ? "발견 완료" : "미발견", found ? "기본 사료 1개 " + PetTownProgressionState.FoodPrice[PetTownProgressionState.FoodLineOf(i)] + " 골드" : "입고하거나 돌보면 기록됩니다", "확인", found ? PetTownProgressionState.Notes[i] + "\n보유 중인 펫을 돌보며 레벨을 올릴 수 있어요." : "새로운 펫을 마을에서 만나 보세요.", 9);
            entry.Category = "펫";
            entries.Add(entry);
        }
        var community = Progression.Community;
        for (int i = 0; i < PetTownCommunityState.AnimalNames.Length; i++)
        {
            int index = i;
            int count = 0;
            if (community != null) foreach (var lot in community.lots) if (lot.animals != null && index < lot.animals.Length) count += lot.animals[index];
            bool found = count > 0; total++; if (found) known++;
            var entry = OperationsEntry(key++, found ? PetTownCommunityState.AnimalNames[i] : "아직 만나지 못했어요", found ? "동물원에서 " + count + "마리" : "미발견", found ? "동물원 동물" : "동물원에서 맞이하면 기록됩니다", "확인", found ? ZooNotes[Mathf.Clamp(i, 0, ZooNotes.Length - 1)] : "동물원을 짓고 새 동물을 맞이해 보세요.", 5);
            entry.Category = "동물원";
            entries.Add(entry);
        }
        var farm = Progression.Farm;
        for (int i = 0; i < PetTownFarmState.CropCount; i++)
        {
            int index = i;
            bool found = farm != null && (farm.seeds[index] > 0 || farm.produce[index] > 0 || farm.lots.Exists(l => l.plots.Exists(p => p.crop == index)));
            total++; if (found) known++;
            var entry = OperationsEntry(key++, found ? PetTownFarmState.Names[i] : "아직 심어 보지 않았어요", found ? "재배 기록 있음" : "미발견", found ? "작물 도감" : "씨앗을 사서 심어 보세요", "확인", found ? CropNotes[Mathf.Clamp(i, 0, CropNotes.Length - 1)] : "농장에서 씨앗을 사서 심으면 기록됩니다.", 9);
            entry.Category = "작물";
            entries.Add(entry);
        }
        var ranch = Progression.Ranch;
        for (int i = 0; i < PetTownRanchState.Species.Length; i++)
        {
            int index = i;
            bool found = ranch != null && (ranch.products[index] > 0 || ranch.lots.Exists(l => l.animals.Exists(a => a.species == index)));
            total++; if (found) known++;
            var entry = OperationsEntry(key++, found ? PetTownRanchState.Species[i] : "아직 만나지 못했어요", found ? "목장 기록 있음" : "미발견", found ? PetTownRanchState.Products[i] + " 생산" : "목장에서 맞이해 보세요", "확인", found ? RanchNotes[Mathf.Clamp(i, 0, RanchNotes.Length - 1)] : "목장을 짓고 어린 가축을 맞이하면 기록됩니다.", 9);
            entry.Category = "가축";
            entries.Add(entry);
        }
        for (int i = 0; i < PetTownResidentProfile.JobCount; i++)
        {
            string job = PetTownResidentProfile.JobName(i);
            var people = state.people.FindAll(p => { PetTownResidentProfile.Ensure(p); return p.job == job; });
            bool found = people.Count > 0; total++; if (found) known++;
            var entry = OperationsEntry(key++, found ? job : "아직 만나지 못한 직업", found ? "마을에서 " + people.Count + "명" : "미발견", found ? string.Join(", ", people.ConvertAll(p => p.name).ToArray()) : "주민이 늘어나면 만날 수 있어요", "확인", found ? JobNotes[Mathf.Clamp(i, 0, JobNotes.Length - 1)] : "더 많은 주민이 입주하면 새로운 직업을 만날 수 있어요.", 5);
            entry.Category = "주민 직업";
            entries.Add(entry);
        }
        catalogWindow.Show("마을 도감 (" + known + "/" + total + ")", entries, e => { }, () => { }, () => { });
    }
}
